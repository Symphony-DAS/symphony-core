using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heka.NativeInterop;
using log4net;
using Symphony.Core;

namespace Heka
{
    using HekkaDevicePtr = System.IntPtr;

    /// <summary>
    /// Channel (i.e. stream) types used by the ITC DAQ hardware and driver.
    /// </summary>
    public enum StreamType
    {
        ANALOG_IN = ITCMM.D2H,
        ANALOG_OUT = ITCMM.H2D,
        DIGITAL_IN = ITCMM.DIGITAL_INPUT,
        DIGITAL_OUT = ITCMM.DIGITAL_OUTPUT,
        AUX_IN = ITCMM.AUX_INPUT,
        AUX_OUT = ITCMM.AUX_OUTPUT
    }

    /// <summary>
    /// Heka/Instrutech-specific details of a DAQ stream. Gives the
    /// channel type and number and the ITCChannelInfo for this stream.
    /// </summary>
    public interface HekaDAQStream : IDAQStream
    {
        StreamType ChannelType { get; }
        ushort ChannelNumber { get; }
        ITCMM.ITCChannelInfo ChannelInfo { get; }
    }

    /// <summary>
    /// Encapsulates interaction with the ITCMM driver. Client code should not use this interface
    /// directly; a IHekaDevice is managed by the HekaDAQController.
    /// </summary>
    public interface IHekaDevice : IClock
    {
        void PreloadSamples(StreamType channelType, ushort channelNumber, IList<short> samples);

        IEnumerable<KeyValuePair<ChannelIdentifier, short[]>> ReadWrite(IDictionary<ChannelIdentifier, short[]> output,
                                                          IList<ChannelIdentifier> input,
                                                          int nsamples);

        void SetStreamBackgroundAsyncIO(HekaDAQOutputStream stream);

        //Now gets the current time from the ITC clock

        bool Running { get; }
        bool Overflow { get; }
        bool Underrun { get; }

        void CloseDevice();
        void ConfigureChannels(IEnumerable<HekaDAQStream> streams);
        void StartHardware(bool waitForTrigger);
        void StopHardware();

        ITCMM.ITCChannelInfo ChannelInfo(StreamType channelType, ushort channelNumber);

        IInputData ReadStreamAsyncIO(HekaDAQInputStream instream);
        void Preload(IDictionary<ChannelIdentifier, short[]> output);
        void Write(IDictionary<ChannelIdentifier, short[]> output);
    }


    /// <summary>
    /// DAQController for the Heka/Instrutech DAQ interface. We currently support only
    /// the PCI/USB-18, but the system uses the "new style" ITC driver, so support for
    /// ITC/USB-16 and the ITC-1600 are all possible.
    /// 
    /// The ITC driver is a polling-style driver. By default, the HekaDAQController uses
    /// a 0.5 second buffer, polling the ITC driver once per buffer duration. Thus
    /// there is potentially a ~0.5s discrepancy between when we think data goes out the
    /// wire and when it actually does. By using the ITC hardware clock, however, this offset
    /// is fixed and timelocked to the actual output time.
    /// 
    /// The ITC hardware supports heterogeneous sampling rates for each channel. The current
    /// controller supports only a single sampling rate.
    /// </summary>
    public sealed class HekaDAQController : DAQControllerBase, IClock, IDisposable
    {
        private const double DEFAULT_TRANSFER_BLOCK_SECONDS = 0.25;
        private const double PRELOAD_DURATION_SECONDS = 2 * DEFAULT_TRANSFER_BLOCK_SECONDS;

        private IHekaDevice Device { get; set; }

        private const string SAMPLE_RATE_KEY = "SampleRate";
        private const string DEVICE_TYPE_KEY = "DeviceType";
        private const string DEVICE_NUMBER_KEY = "DeviceNumber";

        /// <summary>
        /// Common sampling rate for all analog and digital streams
        /// </summary>
        public IMeasurement SampleRate
        {
            get
            {
                if (Configuration.ContainsKey(SAMPLE_RATE_KEY))
                    return Configuration[SAMPLE_RATE_KEY] as Measurement;

                return null;
            }
            set
            {
                // Set the ProcessInterval longer for high sampling rates
                var rateProcessInterval = value.QuantityInBaseUnit > 10000m
                                              ? TimeSpan.FromSeconds(2*DEFAULT_TRANSFER_BLOCK_SECONDS)
                                              : TimeSpan.FromSeconds(DEFAULT_TRANSFER_BLOCK_SECONDS);

                if(rateProcessInterval != ProcessInterval)
                {
                    ProcessInterval = TimeSpan.FromSeconds(2*DEFAULT_TRANSFER_BLOCK_SECONDS);
                    log.Info("Updating process loop duration: " + ProcessInterval);
                }

                Configuration[SAMPLE_RATE_KEY] = value;
            }
        }

        public override string Name
        {
            get { return string.Format("Heka ITC Controller ({0},{1})", DeviceType, DeviceNumber); }
        }

        public override void ApplyStreamBackgroundAsync(IDAQOutputStream s, IMeasurement background)
        {
            if(Running && !StopRequested)
            {
                throw new HekaDAQException("Cannot set stream background while running");
            }

            log.DebugFormat("Setting stream background: {0}", s.Background);
            Device.SetStreamBackgroundAsyncIO(s as HekaDAQOutputStream);
        }

        /// <summary>
        /// ITC device type
        /// </summary>
        public uint DeviceType
        {
            get
            {
                return (uint)Configuration[DEVICE_TYPE_KEY];
            }
            private set { Configuration[DEVICE_TYPE_KEY] = value; }
        }

        /// <summary>
        /// ITC device number
        /// </summary>
        public uint DeviceNumber
        {
            get
            {
                return (uint)Configuration[DEVICE_NUMBER_KEY];
            }
            private set { Configuration[DEVICE_NUMBER_KEY] = value; }
        }

        /// <summary>
        /// Indicates if the ITC hardware is running
        /// </summary>
        public bool HardwareRunning
        {
            get
            {
                return Running;
            }
        }
        
        public IEnumerable<IDAQStream> StreamsOfType(StreamType streamType)
        {
            return Streams.Cast<HekaDAQStream>().Where(x => x.ChannelType == streamType);
        }

        /// <summary>
        /// Constructs a new HekaDAQController for the given device type and number.
        /// </summary>
        /// <param name="deviceType">Heka device type (e.g. ITCMM.ITC18_ID)</param>
        /// <param name="deviceNumber">Device number (0-indexed)</param>
        public HekaDAQController(uint deviceType, uint deviceNumber)
        {
            this.DeviceType = deviceType;
            this.DeviceNumber = deviceNumber;
            this.HardwareReady = false;
            this.ProcessInterval = TimeSpan.FromSeconds(DEFAULT_TRANSFER_BLOCK_SECONDS);
            this.Clock = this;
        }

        /// <summary>
        /// Constructs a HekaDAQController for the default PCI-18 #0 device.
        /// </summary>
        public HekaDAQController()
            : this(ITCMM.ITC18_ID, 0)
        {
        }


        /// <summary>
        /// Initializes the Heka/Instructech hardware.
        /// </summary>
        public override void BeginSetup()
        {
            base.BeginSetup();
            if (!HardwareReady)
            {
                InitHardware();
                CloseHardware();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // We can only close the hardware (which requires reference to other managed objects)
                // if called by user code (not by the finalizer)
                if (disposing)
                {
                    CloseHardware();
                    GC.SuppressFinalize(this);
                }

                _disposed = true;
            }
        }

        ~HekaDAQController()
        {
            Dispose(false);
        }

        /// <summary>
        /// Detects the streams present on this controller's Heka Device and configures the available
        /// IDAQStreams accordingly.
        /// </summary>
        public void InitHardware()
        {
            if (!this.HardwareReady)
            {
                var deviceInfo = OpenDevice();
                this.DAQStreams.Clear();

                //set-up ADC channels
                for (ushort i = 0; i < deviceInfo.NumberOfADCs; i++)
                {
                    string name = String.Format("{0}.{1}", "ANALOG_IN", i);
                    this.DAQStreams.Add(new HekaDAQInputStream(name, StreamType.ANALOG_IN, i, this));
                }


                for (ushort i = 0; i < deviceInfo.NumberOfDACs; i++)
                {
                    string name = String.Format("{0}.{1}", "ANALOG_OUT", i);
                    this.DAQStreams.Add(new HekaDAQOutputStream(name, StreamType.ANALOG_OUT, i, this));
                }

                for (ushort i = 0; i < deviceInfo.NumberOfDIs; i++)
                {
                    string name = String.Format("{0}.{1}", "DIGITAL_IN", i);
                    this.DAQStreams.Add(new HekaDAQInputStream(name, StreamType.DIGITAL_IN, i, this));
                }

                for (ushort i = 0; i < deviceInfo.NumberOfDOs; i++)
                {
                    string name = String.Format("{0}.{1}", "DIGITAL_OUT", i);
                    this.DAQStreams.Add(new HekaDAQOutputStream(name, StreamType.DIGITAL_OUT, i, this));
                }

                this.HardwareReady = true;
            }
        }

        private ITCMM.GlobalDeviceInfo OpenDevice()
        {
            ITCMM.GlobalDeviceInfo deviceInfo;
            this.Device = QueuedHekaHardwareDevice.OpenDevice(DeviceType, DeviceNumber, out deviceInfo);
            HardwareReady = true;
            return deviceInfo;
        }

        /// <summary>
        /// Closes the ITC driver connection to this controller's Heka device.
        /// </summary>
        public void CloseHardware()
        {
            try
            {
                if(HardwareReady)
                    Device.CloseDevice();
            }
            catch (HekaDAQException)
            {
                //pass
            }
            finally
            {
                HardwareReady = false;
            }
        }

        private void ResetHardware()
        {
            RequestStop();
            CloseHardware();
            OpenDevice();
            SetStreamsBackground();
        }

        /// <summary>
        /// Indicates whether the ITC hardware is initialized and ready for acquisition.
        /// </summary>
        public bool HardwareReady { get; private set; }

        protected override void StartHardware(bool waitForTrigger)
        {
            Device.StartHardware(waitForTrigger);
        }


        private void PreloadStreams()
        {

            IDictionary<ChannelIdentifier, short[]> output = new Dictionary<ChannelIdentifier, short[]>();

            foreach (var s in ActiveOutputStreams.Cast<HekaDAQOutputStream>())
            {
                s.Reset();
                var outputSamples = new List<short>();
                while (TimeSpanExtensions.FromSamples((uint)outputSamples.Count(), s.SampleRate).TotalSeconds < PRELOAD_DURATION_SECONDS) // && s.HasMoreData
                {
                    var nextOutputDataForStream = NextOutputDataForStream(s);
                    var nextSamples = nextOutputDataForStream.DataWithUnits(HekaDAQOutputStream.DAQCountUnits).Data.
                        Select(
                            (m) => (short)m.QuantityInBaseUnit);

                    outputSamples = outputSamples.Concat(nextSamples).ToList();

                }

                if (!outputSamples.Any())
                    throw new HekaDAQException("Unable to pull data to preload stream " + s.Name);


                output[new ChannelIdentifier { ChannelNumber = s.ChannelNumber, ChannelType = (ushort)s.ChannelType }] =
                    outputSamples.ToArray();
            }

            Device.Preload(output);
        }

        public override void Start(bool waitForTrigger)
        {
            if (!HardwareReady)
                OpenDevice();

            Device.ConfigureChannels(this.ActiveStreams.Cast<HekaDAQStream>());
            PreloadStreams();

            base.Start(waitForTrigger);
        }

        protected override bool ShouldStop()
        {
            return StopRequested;
        }

        protected override void CommonStop()
        {
            if (Running)
            {
                Device.StopHardware();
            }

            base.CommonStop();
        }

        protected override void StopWithException(Exception e)
        {
            log.ErrorFormat("Hardware reset required due to exception: {0}", e);
            ResetHardware();

            base.StopWithException(e);

        }

        private static readonly ILog log = LogManager.GetLogger(typeof(HekaDAQController));
        private bool _disposed = false;

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit)
        {

            IDictionary<ChannelIdentifier, short[]> output = new Dictionary<ChannelIdentifier, short[]>();
            IDictionary<ChannelIdentifier, short[]> deficitOutput = new Dictionary<ChannelIdentifier, short[]>();



            foreach (var s in ActiveOutputStreams.Cast<HekaDAQOutputStream>())
            {
                var outputData = outData[s];
                var cons = outputData.DataWithUnits(HekaDAQOutputStream.DAQCountUnits).SplitData(deficit);

                short[] outputSamples = cons.Rest.Data.Select((m) => (short)m.QuantityInBaseUnit).ToArray();
                short[] deficitOutputSamples = cons.Head.Data.Select((m) => (short)m.QuantityInBaseUnit).ToArray();
                output[new ChannelIdentifier { ChannelNumber = s.ChannelNumber, ChannelType = (ushort)s.ChannelType }] =
                    outputSamples;
                deficitOutput[new ChannelIdentifier { ChannelNumber = s.ChannelNumber, ChannelType = (ushort)s.ChannelType }] =
                    deficitOutputSamples;
            }

            if(deficitOutput.Any())
            {
                Device.Write(deficitOutput);
            }

            var inputChannels =
                ActiveInputStreams.
                Cast<HekaDAQInputStream>().
                Select((s) => new ChannelIdentifier { ChannelNumber = s.ChannelNumber, ChannelType = (ushort)s.ChannelType }).
                ToList();

            int nsamples;
            if (output.Values.Any())
            {
                if(output.Values.Select(a => a.Length).Distinct().Count() > 1)
                    throw new HekaDAQException("Output buffers are not equal length.");

                nsamples = output.Values.Select((a) => a.Length).Distinct().First(); 
            }
            else
            {
                nsamples = (int)TimeSpan.FromSeconds(DEFAULT_TRANSFER_BLOCK_SECONDS).Samples(SampleRate);
            }

            IEnumerable<KeyValuePair<ChannelIdentifier, short[]>> input = Device.ReadWrite(output, inputChannels, nsamples);

            var result = new ConcurrentDictionary<IDAQInputStream, IInputData>();
            Parallel.ForEach(input, (kvp) =>
                                        {
                                            var s = StreamWithIdentifier(kvp.Key) as IDAQInputStream;
                                            if (s == null)
                                            {
                                                throw new DAQException(
                                                    "ChannelIdentifier does not specify an input stream.");
                                            }

                                            //Create the raw input data

                                            IInputData rawData = new InputData(
                                                kvp.Value.Select(
                                                    v => new Measurement(v, HekaDAQInputStream.DAQCountUnits)).
                                                    ToList(),
                                                StreamWithIdentifier(kvp.Key).SampleRate,
                                                Clock.Now
                                                ).DataWithNodeConfiguration("Heka.HekaDAQController", Configuration);


                                            //Convert to input units and store
                                            result[s] = rawData;
                                        });


            return result;
        }



        private HekaDAQStream StreamWithIdentifier(ChannelIdentifier channelIdentifier)
        {
            HekaDAQStream result =
                Streams.OfType<HekaDAQStream>().First(s => s.ChannelNumber == channelIdentifier.ChannelNumber && s.ChannelType == (StreamType)channelIdentifier.ChannelType);

            if (result == null)
            {
                throw new DAQException("Unable to find stream with identifier " + channelIdentifier);
            }

            return result;
        }

        public override Maybe<string> Validate()
        {
            var result = base.Validate();

            if (result)
            {

                if (Streams.Any(s => !s.SampleRate.Equals(SampleRate)))
                    return Maybe<string>.No("All streams must have the same sample rate as controller.");

                if (SampleRate == null)
                    return Maybe<string>.No("Sample rate required.");

                if (SampleRate.BaseUnit.ToLower() != "hz")
                    return Maybe<string>.No("Sample rate must be in Hz.");

                if (SampleRate.QuantityInBaseUnit <= 0)
                    return Maybe<string>.No("Sample rate must be greater than 0");
            }

            return result;
        }


        internal void PipelineException(Exception e)
        {
            StopWithException(e);
        }

        public DateTimeOffset Now
        {
            get { return Device.Now; }
        }

        public static IEnumerable<HekaDAQController> AvailableControllers()
        {
            return QueuedHekaHardwareDevice.AvailableControllers();
        }

        public void ConfigureChannels()
        {
            if(Running)
            {
                throw new HekaDAQException("Cannot configure channels while hardware is running.");
            }

            Device.ConfigureChannels(ActiveStreams.Cast<HekaDAQStream>());
        }

        public ITCMM.ITCChannelInfo ChannelInfo(StreamType channelType, ushort channelNumber)
        {
            return Device.ChannelInfo(channelType, channelNumber);
        }

        /// <summary>
        /// Reads the given input stream, asynchronously (in the ITC sense, not the .Net sense). Should not be called while
        /// Running.
        /// </summary>
        /// <remarks>All output streams are automatically set to their associated ExternalDevice's Background value on stop</remarks>
        /// <param name="daqInputStream">IDAQInputStream to read</param>
        /// <returns>IInputData with a single read sample</returns>
        /// <exception cref="ArgumentException">If the given stream is not an input stream belonging to this HekaDAQController</exception>"
        public IInputData ReadStreamAsyncIO(IDAQInputStream daqInputStream)
        {
            if (!InputStreams.Contains(daqInputStream))
                throw new ArgumentException("Input stream is not present on this device.", "daqInputStream");

            var instream = daqInputStream as HekaDAQInputStream;
            if (instream != null)
            {
                return Device.ReadStreamAsyncIO(instream);
            }

            return null;
        }
    }
}
