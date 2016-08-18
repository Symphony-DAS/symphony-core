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
        string FullName { get; }
        NIChannelInfo ChannelInfo { get; }
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
        NIChannelInfo ChannelInfo(ChannelType channelType, string channelName);

        void Preload(IDictionary<string, double[]> output);
    }

    /// <summary>
    /// DAQController for the National Instruments DAQ interface. Uses the NI-DAQmx driver.
    /// 
    /// Some NI hardware supports heterogeneous sampling rates for each channel. The current
    /// controller supports only a single sampling rate.
    /// </summary>
    public sealed class NIDAQController : DAQControllerBase, IDisposable
    {
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
            if (IsHardwareReady)
            {
                IsHardwareReady = false;
                Device.CloseDevice();
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
            IDictionary<string, double[]> output = new Dictionary<string, double[]>();

            foreach (var s in ActiveStreams.Cast<NIDAQOutputStream>())
            {
                s.Reset();
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

                output[s.FullName] = outputSamples.ToArray();
            }

            Device.Preload(output);
        }

        public override void Start(bool waitForTrigger)
        {
            if (!IsHardwareReady)
                OpenDevice();

            Device.ConfigureChannels(ActiveStreams.Cast<NIDAQStream>());
            PreloadStreams();

            base.Start(waitForTrigger);
        }

        public override IInputData ReadStreamAsync(IDAQInputStream s)
        {
            throw new NotImplementedException();
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(NIDAQController));
        private bool _disposed = false;

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit, CancellationToken token)
        {
            throw new NotImplementedException();
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

        public NIChannelInfo ChannelInfo(ChannelType channelType, string channelName)
        {
            return Device.ChannelInfo(channelType, channelName);
        }
    }
}
