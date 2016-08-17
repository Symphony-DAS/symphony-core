using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Symphony.Core;
using log4net;

namespace NI
{
    /// <summary>
    /// Channel (i.e. stream) types used by the NI DAQ hardware and driver.
    /// </summary>
    public enum StreamType
    {
        ANALOG_IN,
        ANALOG_OUT,
        DIGITAL_IN,
        DIGITAL_OUT
    }

    /// <summary>
    /// National Instruments-specific details of a DAQ stream. Gives the 
    /// channel type and full physical channel name (e.g. Dev1/ai1) for this stream.
    /// </summary>
    public interface NIDAQStream : IDAQStream
    {
        StreamType ChannelType { get; }
        string FullName { get; }
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

        string DeviceID { get; }
        string[] AIChannels { get; }
        string[] AOChannels { get; }
        string[] DIPorts { get; }
        string[] DOPorts { get; }
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
            get { return string.Format("NI ITC Controller ({0})", DeviceName); }
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

        public IEnumerable<IDAQStream> StreamsOfType(StreamType streamType)
        {
            return Streams.Cast<NIDAQStream>().Where(s => s.ChannelType == streamType);
        }

        public NIDAQController(string deviceName)
            : this(deviceName, new SystemClock())
        {
        }

        public NIDAQController(string deviceName, IClock clock)
        {
            DeviceName = deviceName;
            IsHardwareReady = false;
            Clock = clock;
        }

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

        public void InitHardware()
        {
            if (!IsHardwareReady)
            {
                OpenDevice();

                if (!DAQStreams.Any())
                {
                    foreach (var c in Device.AIChannels)
                    {
                        DAQStreams.Add(new NIDAQInputStream(c, StreamType.ANALOG_IN, this));
                    }

                    foreach (var c in Device.AOChannels)
                    {
                        DAQStreams.Add(new NIDAQOutputStream(c, StreamType.ANALOG_OUT, this));
                    }

                    foreach (var p in Device.DIPorts)
                    {
                        DAQStreams.Add(new NIDAQInputStream(p, StreamType.DIGITAL_IN, this));
                    }

                    foreach (var p in Device.DOPorts)
                    {
                        DAQStreams.Add(new NIDAQInputStream(p, StreamType.DIGITAL_OUT, this));
                    }
                }
                
                IsHardwareReady = true;
            }
        }

        private void OpenDevice()
        {
            Device = NIHardwareDevice.OpenDevice(DeviceName);
            IsHardwareReady = true;
        }

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

        }

        public override void Start(bool waitForTrigger)
        {
            if (!IsHardwareReady)
                OpenDevice();

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
    }
}
