﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
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
        AI = ITCMM.D2H,
        AO = ITCMM.H2D,
        DI_PORT = ITCMM.DIGITAL_INPUT,
        DO_PORT = ITCMM.DIGITAL_OUTPUT,
        XI = ITCMM.AUX_INPUT,
        XO = ITCMM.AUX_OUTPUT
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
    /// Heka/Instrutech-specific details of a digital DAQ stream. Each digital
    /// DAQ stream groups 16-bits, where each bit represents a physical port on
    /// the device.
    /// 
    /// All devices associated with a HekaDigitalDAQStream must indicate an
    /// associated bit position through which to push/pull data.
    /// </summary>
    public interface HekaDigitalDAQStream : HekaDAQStream
    {
        IDictionary<IExternalDevice, ushort> BitPositions { get; } 
    }

    /// <summary>
    /// Encapsulates interaction with the ITCMM driver. Client code should not use this interface
    /// directly; a IHekaDevice is managed by the HekaDAQController.
    /// </summary>
    public interface IHekaDevice
    {
        void PreloadSamples(StreamType channelType, ushort channelNumber, IList<short> samples);

        IEnumerable<KeyValuePair<ChannelIdentifier, short[]>> ReadWrite(IDictionary<ChannelIdentifier, short[]> output,
                                                                        IList<ChannelIdentifier> input,
                                                                        int nsamples,
                                                                        CancellationToken token);

        void SetStreamBackgroundAsyncIO(HekaDAQOutputStream stream);

        //Now gets the current time from the ITC clock

        bool Running { get; }
        bool Overflow { get; }
        bool Underrun { get; }

        void CloseDevice();
        void ConfigureChannels(IEnumerable<HekaDAQStream> streams);
        void StartHardware(bool waitForTrigger);
        void StopHardware();

        ITCMM.GlobalDeviceInfo DeviceInfo { get; }
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
    public sealed class HekaDAQController : DAQControllerBase, IDisposable
    {
        private const double DEFAULT_TRANSFER_BLOCK_SECONDS = 0.25;

        private IHekaDevice Device { get; set; }

        private const string SAMPLE_RATE_KEY = "sampleRate";
        private const string DEVICE_TYPE_KEY = "deviceType";
        private const string DEVICE_NUMBER_KEY = "deviceNumber";

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
                var rateProcessInterval = value.QuantityInBaseUnits > 10000m
                                              ? TimeSpan.FromSeconds(2*DEFAULT_TRANSFER_BLOCK_SECONDS)
                                              : TimeSpan.FromSeconds(DEFAULT_TRANSFER_BLOCK_SECONDS);

                if(rateProcessInterval != ProcessInterval)
                {
                    ProcessInterval = rateProcessInterval;
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
            if(IsRunning && !IsStopRequested)
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
                return IsRunning;
            }
        }
        
        public IEnumerable<IDAQStream> StreamsOfType(StreamType streamType)
        {
            return Streams.Cast<HekaDAQStream>().Where(x => x.ChannelType == streamType);
        }

        /// <summary>
        /// Constructs a new HekaDAQController for the given device type and number, using 
        /// the system (CPU) clock.
        /// </summary>
        /// <param name="deviceType">Heka device type (e.g. ITCMM.ITC18_ID)</param>
        /// <param name="deviceNumber">Device number (0-indexed)</param>
        public HekaDAQController(uint deviceType, uint deviceNumber)
            : this(deviceType, deviceNumber, new SystemClock())
        {
        }

        /// <summary>
        /// Constructs a HekaDAQController for the default PCI-18 #0 device, using the system 
        /// (CPU) clock.
        /// </summary>
        public HekaDAQController()
            : this(ITCMM.ITC18_ID, 0)
        {
        }

        /// <summary>
        /// Constructs a new HekaDAQController for the given device type and number, using
        /// the given clock.
        /// </summary>
        /// <param name="deviceType"></param>
        /// <param name="deviceNumber"></param>
        /// <param name="clock"></param>
        public HekaDAQController(uint deviceType, uint deviceNumber, IClock clock)
        {
            this.DeviceType = deviceType;
            this.DeviceNumber = deviceNumber;
            this.IsHardwareReady = false;
            this.ProcessInterval = TimeSpan.FromSeconds(DEFAULT_TRANSFER_BLOCK_SECONDS);
            this.Clock = clock;
        }

        /// <summary>
        /// Initializes the Heka/Instructech hardware.
        /// </summary>
        public override void BeginSetup()
        {
            base.BeginSetup();
            if (!IsHardwareReady)
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
            if (!this.IsHardwareReady)
            {
                var deviceInfo = OpenDevice();
                
                if (!DAQStreams.Any())
                {
                    //set-up ADC channels
                    for (ushort i = 0; i < deviceInfo.NumberOfADCs; i++)
                    {
                        string name = String.Format("{0}{1}", "ai", i);
                        this.DAQStreams.Add(new HekaDAQInputStream(name, StreamType.AI, i, this));
                    }


                    for (ushort i = 0; i < deviceInfo.NumberOfDACs; i++)
                    {
                        string name = String.Format("{0}{1}", "ao", i);
                        this.DAQStreams.Add(new HekaDAQOutputStream(name, StreamType.AO, i, this));
                    }

                    for (ushort i = 0; i < deviceInfo.NumberOfDIs; i++)
                    {
                        string name = String.Format("{0}{1}", "diport", i);
                        this.DAQStreams.Add(new HekaDigitalDAQInputStream(name, i, this));
                    }

                    for (ushort i = 0; i < deviceInfo.NumberOfDOs; i++)
                    {
                        string name = String.Format("{0}{1}", "doport", i);
                        this.DAQStreams.Add(new HekaDigitalDAQOutputStream(name, i, this));
                    }
                }

                this.IsHardwareReady = true;
            }
        }

        private ITCMM.GlobalDeviceInfo OpenDevice()
        {
            ITCMM.GlobalDeviceInfo deviceInfo;
            this.Device = QueuedHekaHardwareDevice.OpenDevice(DeviceType, DeviceNumber, out deviceInfo);
            IsHardwareReady = true;
            return deviceInfo;
        }

        /// <summary>
        /// Closes the ITC driver connection to this controller's Heka device.
        /// </summary>
        public void CloseHardware()
        {
            try
            {
                if (IsHardwareReady)
                {
                    IsHardwareReady = false;
                    Device.CloseDevice();
                }   
            }
            catch (HekaDAQException)
            {
                //pass
            }
        }

        private void ResetHardware()
        {
            Stop();
            CloseHardware();
            OpenDevice();
            SetStreamsBackground();
        }

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
                while (TimeSpanExtensions.FromSamples((uint)outputSamples.Count(), s.SampleRate).Ticks < ProcessInterval.Ticks * 2) // && s.HasMoreData
                {
                    var nextOutputDataForStream = NextOutputDataForStream(s);
                    var nextSamples = nextOutputDataForStream.DataWithUnits(HekaDAQOutputStream.DAQCountUnits).Data.
                        Select(
                            (m) => (short)m.QuantityInBaseUnits);

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
            if (!IsHardwareReady)
                OpenDevice();

            Device.ConfigureChannels(this.ActiveStreams.Cast<HekaDAQStream>());
            PreloadStreams();

            base.Start(waitForTrigger);
        }

        protected override bool ShouldStop()
        {
            return IsStopRequested;
        }

        protected override void CommonStop()
        {
            if (IsRunning)
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

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit, CancellationToken token)
        {
            IDictionary<ChannelIdentifier, short[]> output = new Dictionary<ChannelIdentifier, short[]>();
            IDictionary<ChannelIdentifier, short[]> deficitOutput = new Dictionary<ChannelIdentifier, short[]>();

            foreach (var s in ActiveOutputStreams.Cast<HekaDAQOutputStream>())
            {
                var outputData = outData[s];

                var cons = outputData.DataWithUnits(HekaDAQOutputStream.DAQCountUnits).SplitData(deficit);

                short[] deficitOutputSamples = cons.Head.Data.Select((m) => (short)m.QuantityInBaseUnits).ToArray();
                deficitOutput[new ChannelIdentifier { ChannelNumber = s.ChannelNumber, ChannelType = (ushort)s.ChannelType }] =
                    deficitOutputSamples;

                short[] outputSamples = cons.Rest.Data.Select((m) => (short)m.QuantityInBaseUnits).ToArray();
                output[new ChannelIdentifier { ChannelNumber = s.ChannelNumber, ChannelType = (ushort)s.ChannelType }] =
                    outputSamples;
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
                nsamples = (int)ProcessInterval.Samples(SampleRate);
            }

            IEnumerable<KeyValuePair<ChannelIdentifier, short[]>> input = Device.ReadWrite(output, inputChannels, nsamples, token);

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
                                                    v => MeasurementPool.GetMeasurement(v, 0, HekaDAQInputStream.DAQCountUnits)).ToList(),
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

                if (SampleRate.BaseUnits.ToLower() != "hz")
                    return Maybe<string>.No("Sample rate must be in Hz.");

                if (SampleRate.QuantityInBaseUnits <= 0)
                    return Maybe<string>.No("Sample rate must be greater than 0");

                if (!ActiveStreams.Any())
                    return Maybe<string>.No("Must have at least one active stream (a stream with an associated device)");


                // This is a workaround for issue #41 (https://github.com/Symphony-DAS/Symphony/issues/41)
                foreach (var s in InputStreams)
                {
                    foreach (var ed in s.Devices.OfType<NullDevice>().ToList())
                    {
                        s.RemoveDevice(ed);
                    }
                }

                if (Math.Max(ActiveOutputStreams.Count(), ActiveInputStreams.Count()) >= 1)
                {
                    var samplingInterval = 1e9m / (SampleRate.QuantityInBaseUnits * Math.Max(ActiveOutputStreams.Count(), ActiveInputStreams.Count()));

                    while (samplingInterval % Device.DeviceInfo.MinimumSamplingStep != 0m)
                    {
                        var inactive = InputStreams.Where(s => !s.Active).ToList();

                        if (!inactive.Any())
                            return Maybe<string>.No("A well-aligned sampling interval is not possible with the current sampling rate");

                        var dev = new NullDevice();
                        dev.BindStream(inactive.First());

                        samplingInterval = 1e9m / (SampleRate.QuantityInBaseUnits * Math.Max(ActiveOutputStreams.Count(), ActiveInputStreams.Count()));
                    }
                }
            }

            return result;
        }

        private class NullDevice : ExternalDeviceBase
        {
            public NullDevice()
                : base("NULL", "NULL", (Measurement)null)
            {
            }

            public override IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration)
            {
                throw new NotImplementedException();
            }

            public override void PushInputData(IDAQInputStream stream, IInputData inData)
            {
            }

            public override Maybe<string> Validate()
            {
                return Maybe<string>.Yes();
            }
        }

        internal void PipelineException(Exception e)
        {
            StopWithException(e);
        }

        public static IEnumerable<HekaDAQController> AvailableControllers()
        {
            return QueuedHekaHardwareDevice.AvailableControllers();
        }

        public void ConfigureChannels()
        {
            if(IsRunning)
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
        public override IInputData ReadStreamAsync(IDAQInputStream daqInputStream)
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
