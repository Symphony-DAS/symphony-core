using System;
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
    /// channel type and full physical channel name (e.g. Dev1/ai1) for this stream.
    /// </summary>
    public interface NIDAQStream : IDAQStream
    {
        PhysicalChannelTypes PhysicalChannelType { get; }
        string PhysicalName { get; }
    }

    /// <summary>
    /// Encapsulates interaction with the NI-DAQmx driver. Client code should not use this interface
    /// directly; a INIDevice is managed by the NIDAQController.
    /// </summary>
    public interface INIDevice
    {
        void SetStreamBackground(NIDAQOutputStream stream);

        void CloseDevice();
        void ConfigureChannels(IEnumerable<NIDAQStream> streams);
        void StartHardware(bool waitForTrigger);
        void StopHardware();

        NIDeviceInfo DeviceInfo { get; }
        Channel Channel(string channelName);

        IInputData ReadStream(NIDAQInputStream instream);
        void PreloadAnalog(IDictionary<string, double[]> output);
        void PreloadDigital(IDictionary<string, UInt32[]> output);
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

        private const string SAMPLE_RATE_KEY = "SampleRate";
        private const string DEVICE_NAME_KEY = "DeviceName";

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
        /// Constructs a new NIDAQController for the given device name, using the system (CPU) clock.
        /// </summary>
        /// <param name="deviceName">NI device name (e.g. "Dev1")</param>
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
                var deviceInfo = OpenDevice();

                if (!DAQStreams.Any())
                {
                    foreach (var c in deviceInfo.AIPhysicalChannels)
                    {
                        DAQStreams.Add(new NIDAQInputStream(c, PhysicalChannelTypes.AI, this));
                    }

                    foreach (var c in deviceInfo.AOPhysicalChannels)
                    {
                        DAQStreams.Add(new NIDAQOutputStream(c, PhysicalChannelTypes.AO, this));
                    }

                    foreach (var p in deviceInfo.DIPorts)
                    {
                        DAQStreams.Add(new NIDAQInputStream(p, PhysicalChannelTypes.DIPort, this));
                    }

                    foreach (var p in deviceInfo.DOPorts)
                    {
                        DAQStreams.Add(new NIDAQInputStream(p, PhysicalChannelTypes.DOPort, this));
                    }
                }
                
                IsHardwareReady = true;
            }
        }

        private NIDeviceInfo OpenDevice()
        {
            NIDeviceInfo deviceInfo;
            Device = NIHardwareDevice.OpenDevice(DeviceName, out deviceInfo);
            IsHardwareReady = true;
            return deviceInfo;
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
            IDictionary<string, double[]> analogOutput = new Dictionary<string, double[]>();
            IDictionary<string, UInt32[]> digitalOutput = new Dictionary<string, UInt32[]>();

            foreach (var s in ActiveOutputStreams.Cast<NIDAQOutputStream>())
            {
                s.Reset();

                if (s.PhysicalChannelType == PhysicalChannelTypes.AO)
                {
                    var outputSamples = new List<double>();
                    while (TimeSpanExtensions.FromSamples((uint)outputSamples.Count(), s.SampleRate) < ProcessInterval) // && s.HasMoreData
                    {
                        var nextOutputDataForStream = NextOutputDataForStream(s);
                        var nextSamples =
                            nextOutputDataForStream.DataWithUnits("V").Data.Select(m => (double) m.QuantityInBaseUnits);

                        outputSamples = outputSamples.Concat(nextSamples).ToList();
                    }

                    if (!outputSamples.Any())
                        throw new DAQException("Unable to pull data to preload stream " + s.Name);

                    analogOutput[s.PhysicalName] = outputSamples.ToArray();
                }
                else if (s.PhysicalChannelType == PhysicalChannelTypes.DOPort)
                {
                    var outputSamples = new List<UInt32>();
                    while (TimeSpanExtensions.FromSamples((uint)outputSamples.Count(), s.SampleRate) < ProcessInterval) // && s.HasMoreData
                    {
                        var nextOutputDataForStream = NextOutputDataForStream(s);
                        var nextSamples =
                            nextOutputDataForStream.DataWithUnits(Measurement.UNITLESS)
                                                   .Data.Select(m => (UInt32) m.QuantityInBaseUnits);

                        outputSamples = outputSamples.Concat(nextSamples).ToList();
                    }

                    if (!outputSamples.Any())
                        throw new DAQException("Unable to pull data to preload stream " + s.Name);

                    digitalOutput[s.PhysicalName] = outputSamples.ToArray();
                }
            }

            Device.PreloadAnalog(analogOutput);
            Device.PreloadDigital(digitalOutput);
        }

        public override void Start(bool waitForTrigger)
        {
            if (!IsHardwareReady)
                OpenDevice();

            Device.ConfigureChannels(ActiveStreams.Cast<NIDAQStream>());
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
            throw new NotImplementedException();
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

        public void ConfigureChannels()
        {
            if (IsRunning)
            {
                throw new DAQException("Cannot configure channels while hardware is running.");
            }

            Device.ConfigureChannels(ActiveStreams.Cast<NIDAQStream>());
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
