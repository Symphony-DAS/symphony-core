using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NationalInstruments.DAQmx;
using Symphony.Core;

namespace NI
{
    public sealed class NIDAQController : DAQControllerBase, IDisposable
    {
        private Device Device { get; set; }

        private const string DEVICE_NAME_KEY = "DeviceName";

        public string DeviceName
        {
            get { return (string)Configuration[DEVICE_NAME_KEY]; }
            private set { Configuration[DEVICE_NAME_KEY] = value; }
        }

        public NIDAQController(string deviceName)
        {
            DeviceName = deviceName;
            IsHardwareReady = false;
        }

        public override void BeginSetup()
        {
            base.BeginSetup();
            if (!IsHardwareReady)
            {
                InitHardware();
            }
        }

        public void InitHardware()
        {
            if (!IsHardwareReady)
            {
                Device = DaqSystem.Local.LoadDevice(DeviceName);

                if (!DAQStreams.Any())
                {
                    foreach (var c in Device.AIPhysicalChannels)
                    {
                    }

                    foreach (var c in Device.AOPhysicalChannels)
                    {    
                    }

                    foreach (var p in Device.DIPorts)
                    {
                    }

                    foreach (var p in Device.DOPorts)
                    {
                    }
                }
                
                IsHardwareReady = true;
            }
        }

        protected override void StartHardware(bool waitForTrigger)
        {
            throw new NotImplementedException();
        }

        public override IInputData ReadStreamAsync(IDAQInputStream s)
        {
            throw new NotImplementedException();
        }

        public override void ApplyStreamBackgroundAsync(IDAQOutputStream s, IMeasurement background)
        {
            throw new NotImplementedException();
        }

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public static IEnumerable<NIDAQController> AvailableControllers()
        {
            return DaqSystem.Local.Devices.Select(d => new NIDAQController(d));
        }
    }
}
