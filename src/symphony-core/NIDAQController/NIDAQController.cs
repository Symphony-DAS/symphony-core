using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NationalInstruments.DAQmx;
using Symphony.Core;
using log4net;

namespace NI
{
    /// <summary>
    /// National Instruments-specific details of a DAQ stream. Gives the 
    /// full physical channel name (e.g. Dev1/ai1) and channel type for this stream.
    /// </summary>
    public interface NIDAQStream : IDAQStream
    {
        string PhysicalName { get; }
        PhysicalChannelTypes PhysicalChannelType { get; }
        Channel GetChannel();
        string DAQUnits { get; }
    }

    /// <summary>
    /// National Instruments-specific details of a digital DAQ stream. Each digital
    /// DAQ stream groups some number of bits, where each bit represents a physical line on
    /// the device.
    /// 
    /// All devices associated with a NIDigitalDAQStream must indicate an
    /// associated bit position through which to push/pull data.
    /// </summary>
    public interface NIDigitalDAQStream : NIDAQStream
    {
        IDictionary<IExternalDevice, ushort> BitPositions { get; }
        bool SupportsContinuousSampling { get; }
    }

    /// <summary>
    /// Encapsulates interaction with the NI-DAQmx driver. Client code should not use this interface
    /// directly; a INIDevice is managed by the NIDAQController.
    /// </summary>
    public interface INIDevice
    {
        IEnumerable<KeyValuePair<Channel, double[]>> Read(IList<Channel> input, int nsamples, CancellationToken token);

        void SetStreamBackground(NIDAQOutputStream stream);

        void CloseDevice();
        void ConfigureChannels(IEnumerable<NIDAQStream> streams, long bufferSizePerChannel);
        void StartHardware(bool waitForTrigger);
        void StopHardware();

        string[] AIPhysicalChannels { get; }
        string[] AOPhysicalChannels { get; }
        string[] DIPorts { get; }
        string[] DOPorts { get; }
        double MinAIVoltage { get; }
        double MaxAIVoltage { get; }
        double MinAOVoltage { get; }
        double MaxAOVoltage { get; }
        Channel Channel(string channelName);

        IInputData ReadStream(NIDAQInputStream instream);
        void Write(IDictionary<Channel, double[]> output);
    }

    /// <summary>
    /// DAQController for the National Instruments DAQ interface. Uses the NI-DAQmx driver.
    /// 
    /// Some NI hardware supports heterogeneous sampling rates for each channel. The current
    /// controller supports only a single sampling rate.
    /// </summary>
    public sealed class NIDAQController : DAQControllerBase, IDisposable
    {
        private const double DEFAULT_TRANSFER_BLOCK_SECONDS = 0.25;

        private INIDevice Device { get; set; }

        private const string SAMPLE_RATE_KEY = "sampleRate";
        private const string DEVICE_NAME_KEY = "deviceName";

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
                                              ? TimeSpan.FromSeconds(2 * DEFAULT_TRANSFER_BLOCK_SECONDS)
                                              : TimeSpan.FromSeconds(DEFAULT_TRANSFER_BLOCK_SECONDS);

                if (rateProcessInterval != ProcessInterval)
                {
                    ProcessInterval = rateProcessInterval;
                    log.Info("Updating process loop duration: " + ProcessInterval);
                }

                Configuration[SAMPLE_RATE_KEY] = value;
            }
        }

        public override string Name
        {
            get { return string.Format("NI DAQ Controller ({0})", DeviceName); }
        }

        public override void ApplyStreamBackgroundAsync(IDAQOutputStream s, IMeasurement background)
        {
            if (IsRunning && !IsStopRequested)
            {
                throw new DAQException("Cannot set stream background while running");
            }

            log.DebugFormat("Setting stream background: {0}", s.Background);
            Device.SetStreamBackground(s as NIDAQOutputStream);
        }

        public string DeviceName
        {
            get { return (string) Configuration[DEVICE_NAME_KEY]; }
            private set { Configuration[DEVICE_NAME_KEY] = value; }
        }

        public IEnumerable<IDAQStream> StreamsOfType(PhysicalChannelTypes channelType)
        {
            return Streams.Cast<NIDAQStream>().Where(x => x.PhysicalChannelType == channelType);
        }

        /// <summary>
        /// Constructs a NIDAQController for the "Dev0" device, using the system (CPU) clock.
        /// </summary>
        public NIDAQController()
            : this("Dev0")
        {
        }

        /// <summary>
        /// Constructs a new NIDAQController for the given device name, using the system (CPU) clock.
        /// </summary>
        /// <param name="deviceName">NI device name (e.g. "Dev0")</param>
        public NIDAQController(string deviceName)
            : this(deviceName, new SystemClock())
        {
        }

        /// <summary>
        /// Constructs a new NIDAQController for the given device name, using the given clock.
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="clock"></param>
        public NIDAQController(string deviceName, IClock clock)
        {
            DeviceName = deviceName;
            IsHardwareReady = false;
            ProcessInterval = TimeSpan.FromSeconds(DEFAULT_TRANSFER_BLOCK_SECONDS);
            Clock = clock;
        }

        /// <summary>
        /// Initializes the National Instruments hardware.
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

        ~NIDAQController()
        {
            Dispose(false);
        }

        /// <summary>
        /// Detects the streams present on this controller's NI Device and configures the available
        /// IDAQStreams accordingly.
        /// </summary>
        public void InitHardware()
        {
            if (!IsHardwareReady)
            {
                var device = OpenDevice();

                if (!DAQStreams.Any())
                {
                    foreach (var c in device.AIPhysicalChannels)
                    {
                        string physicalName = c;
                        string name = physicalName.Split('/').Last();
                        DAQStreams.Add(new NIDAQInputStream(name, physicalName, PhysicalChannelTypes.AI, this));
                    }

                    foreach (var c in device.AOPhysicalChannels)
                    {
                        string physicalName = c;
                        string name = physicalName.Split('/').Last();
                        DAQStreams.Add(new NIDAQOutputStream(name, physicalName, PhysicalChannelTypes.AO, this));
                    }

                    foreach (var p in device.DIPorts)
                    {
                        string physicalName = p;
                        string name = "di" + physicalName.Split('/').Last();
                        DAQStreams.Add(new NIDigitalDAQInputStream(name, physicalName, this));
                    }

                    foreach (var p in device.DOPorts)
                    {
                        string physicalName = p;
                        string name = "do" + physicalName.Split('/').Last();
                        DAQStreams.Add(new NIDigitalDAQOutputStream(name, physicalName, this));
                    }
                }
                
                IsHardwareReady = true;
            }
        }

        private INIDevice OpenDevice()
        {
            Device = NIHardwareDevice.OpenDevice(DeviceName);
            IsHardwareReady = true;
            return Device;
        }

        /// <summary>
        /// Closes the NI driver connection to this controller's NI device.
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
            catch (DaqException)
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
            IDictionary<Channel, double[]> output = new Dictionary<Channel, double[]>();

            foreach (var s in ActiveOutputStreams.Cast<NIDAQOutputStream>())
            {
                s.Reset();
                var outputSamples = new List<double>();
                while (TimeSpanExtensions.FromSamples((uint)outputSamples.Count(), s.SampleRate).Ticks < ProcessInterval.Ticks * 2) // && s.HasMoreData
                {
                    var nextOutputDataForStream = NextOutputDataForStream(s);
                    var nextSamples = nextOutputDataForStream.DataWithUnits(s.DAQUnits).Data.
                        Select(
                            (m) => (double)m.QuantityInBaseUnits);

                    outputSamples = outputSamples.Concat(nextSamples).ToList();

                }

                if (!outputSamples.Any())
                    throw new DaqException("Unable to pull data to preload stream " + s.Name);

                output[s.GetChannel()] = outputSamples.ToArray();
            }

            Device.Write(output);
        }

        public override void Start(bool waitForTrigger)
        {
            if (!IsHardwareReady)
                OpenDevice();

            ConfigureChannels();
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

        private static readonly ILog log = LogManager.GetLogger(typeof(NIDAQController));
        private bool _disposed = false;

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit, CancellationToken token)
        {
            IDictionary<Channel, double[]> output = new Dictionary<Channel, double[]>();
            IDictionary<Channel, double[]> deficitOutput = new Dictionary<Channel, double[]>();

            foreach (var s in ActiveOutputStreams.Cast<NIDAQOutputStream>())
            {
                var outputData = outData[s];

                var cons = outputData.DataWithUnits(s.DAQUnits).SplitData(deficit);

                double[] deficitOutputSamples = cons.Head.Data.Select((m) => (double)m.QuantityInBaseUnits).ToArray();
                deficitOutput[s.GetChannel()] = deficitOutputSamples;

                double[] outputSamples = cons.Rest.Data.Select((m) => (double)m.QuantityInBaseUnits).ToArray();
                output[s.GetChannel()] = outputSamples;
            }

            if (deficitOutput.Any())
            {
                Device.Write(deficitOutput);
            }

            var inputChannels =
                ActiveInputStreams.
                Cast<NIDAQInputStream>().
                Select((s) => s.GetChannel()).
                ToList();

            int nsamples;
            if (output.Values.Any())
            {
                if (output.Values.Select(a => a.Length).Distinct().Count() > 1)
                    throw new DAQException("Output buffers are not equal length.");

                nsamples = output.Values.Select((a) => a.Length).Distinct().First();
            }
            else
            {
                nsamples = (int)ProcessInterval.Samples(SampleRate);
            }

            Device.Write(output);
            IEnumerable<KeyValuePair<Channel, double[]>> input = Device.Read(inputChannels, nsamples, token);

            var result = new ConcurrentDictionary<IDAQInputStream, IInputData>();
            Parallel.ForEach(input, (kvp) =>
                {
                    var s = StreamWithChannel(kvp.Key) as NIDAQInputStream;
                    if (s == null)
                    {
                        throw new DAQException(
                            "Channel does not specify an input stream.");
                    }

                    //Create the raw input data

                    IInputData rawData = new InputData(
                        kvp.Value.Select(
                            v => MeasurementPool.GetMeasurement((decimal) v, 0, s.DAQUnits)).ToList(),
                        StreamWithChannel(kvp.Key).SampleRate,
                        Clock.Now
                        ).DataWithNodeConfiguration("NI.NIDAQController", Configuration);


                    //Convert to input units and store
                    result[s] = rawData;
                });

            return result;
        }

        private NIDAQStream StreamWithChannel(Channel channel)
        {
            NIDAQStream result =
                Streams.OfType<NIDAQStream>().First(s => s.PhysicalName == channel.PhysicalName);

            if (result == null)
            {
                throw new DAQException("Unable to find stream with channel " + channel);
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
            }

            return result;
        }

        public static IEnumerable<NIDAQController> AvailableControllers()
        {
            return NIHardwareDevice.AvailableControllers();
        }

        public double MinAIVoltage()
        {
            if (!IsHardwareReady)
                throw new DAQException("Hardware must be initialized before calling this method.");
            return Device.MinAIVoltage;
        }

        public double MaxAIVoltage()
        {
            if (!IsHardwareReady)
                throw new DAQException("Hardware must be initialized before calling this method.");
            return Device.MaxAIVoltage;
        }

        public double MinAOVoltage()
        {
            if (!IsHardwareReady)
                throw new DAQException("Hardware must be initialized before calling this method.");
            return Device.MinAOVoltage;
        }

        public double MaxAOVoltage()
        {
            if (!IsHardwareReady)
                throw new DAQException("Hardware must be initialized before calling this method.");
            return Device.MaxAOVoltage;
        }

        public void ConfigureChannels()
        {
            if (IsRunning)
            {
                throw new DAQException("Cannot configure channels while hardware is running.");
            }

            long bufferSize = SampleRate.QuantityInBaseUnits > 1000000 ? 1000000 : 100000;
            Device.ConfigureChannels(ActiveStreams.Cast<NIDAQStream>(), bufferSize);
        }

        public Channel Channel(string channelName)
        {
            return Device.Channel(channelName);
        }

        /// <summary>
        /// Reads the given input stream. Should not be called while Running.
        /// </summary>
        /// <remarks>All output streams are automatically set to their associated ExternalDevice's Background value on stop</remarks>
        /// <param name="daqInputStream">IDAQInputStream to read</param>
        /// <returns>IInputData with a single read sample</returns>
        /// <exception cref="ArgumentException">If the given stream is not an input stream belonging to this NIDAQController</exception>"
        public override IInputData ReadStreamAsync(IDAQInputStream daqInputStream)
        {
            if (!InputStreams.Contains(daqInputStream))
                throw new ArgumentException("Input stream is not present on this device.", "daqInputStream");

            var instream = daqInputStream as NIDAQInputStream;
            if (instream != null)
            {
                return Device.ReadStream(instream);
            }

            return null;
        }
    }
}
