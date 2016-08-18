using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NationalInstruments.DAQmx;
using Symphony.Core;
using Task = NationalInstruments.DAQmx.Task;

namespace NI
{
    sealed class NIHardwareDevice : INIDevice
    {
        private Device Device { get; set; }

        private Tasks _tasks;

        public NIHardwareDevice(Device device)
        {
            Device = device;
        }

        public void SetStreamBackground(NIDAQOutputStream stream)
        {
            if (stream != null)
            {
                WriteIO(stream, stream.Background);
            }
        }

        private void WriteIO(NIDAQOutputStream stream, IMeasurement value)
        {
            using (var t = new Task())
            {
                t.AOChannels.CreateVoltageChannel(stream.FullName, "", Device.AOVoltageRanges.First(),
                                                  Device.AOVoltageRanges.Last(), AOVoltageUnits.Volts);
                var writer = new AnalogSingleChannelWriter(t.Stream);
                writer.WriteSingleSample(true, (double) Converters.Convert(value, "V").QuantityInBaseUnits);
            }
        }

        public void Preload(IDictionary<string, double[]> output)
        {
            foreach (var kv in output)
            {

            }

            //var aoNames = output.Keys.Where(s => s.ChannelType == StreamType.ANALOG_OUT).Select(s => s.FullName);
            //if (aoNames.Any())
            //{
            //    _tasks.AnalogOut.AOChannels.All.PhysicalName
            //    var writer = new AnalogMultiChannelWriter(_tasks.AnalogOut.Stream);
            //    writer.WriteMultiSample(false, new double[1, 1]);
            //}
        }

        public NIDeviceInfo DeviceInfo
        {
            get { return NIDeviceInfo.FromDevice(Device); }
        }

        public static IEnumerable<NIDAQController> AvailableControllers()
        {
            return DaqSystem.Local.Devices.Select(d => new NIDAQController(d));
        }

        internal static INIDevice OpenDevice(string deviceName, out NIDeviceInfo deviceInfo)
        {
            Device dev = DaqSystem.Local.LoadDevice(deviceName);

            deviceInfo = NIDeviceInfo.FromDevice(dev);

            return new NIHardwareDevice(dev);
        }

        public void CloseDevice()
        {
            if (_tasks != null)
            {
                _tasks.All.ForEach(t => t.Dispose());
            }
            Device.Dispose();
        }

        public void ConfigureChannels(IEnumerable<NIDAQStream> daqStreams)
        {
            var streams = daqStreams.ToList();
            if (!streams.Any())
                throw new ArgumentException("Streams cannot be empty");

            var tasks = new Tasks();
            var chanNames = streams.GroupBy(s => s.PhysicalChannelType).ToDictionary(g => g.Key, g => g.Select(s => s.FullName));

            // Create appropriate tasks
            if (chanNames.ContainsKey(PhysicalChannelTypes.AI))
            {
                tasks.CreateAITask(chanNames[PhysicalChannelTypes.AI], Device.AIVoltageRanges.First(), Device.AIVoltageRanges.Last());
            }
            if (chanNames.ContainsKey(PhysicalChannelTypes.AO))
            {
                tasks.CreateAOTask(chanNames[PhysicalChannelTypes.AO], Device.AIVoltageRanges.First(), Device.AIVoltageRanges.Last());
            }
            if (chanNames.ContainsKey(PhysicalChannelTypes.DIPort))
            {
                tasks.CreateDITask(chanNames[PhysicalChannelTypes.DIPort]);
            }
            if (chanNames.ContainsKey(PhysicalChannelTypes.DOPort))
            {
                tasks.CreateDOTask(chanNames[PhysicalChannelTypes.DOPort]);
            }

            // Setup master and slave timing
            var rates = streams.Select(s => s.SampleRate).Distinct().ToList();
            if (rates.Count() > 1)
                throw new DaqException("Streams need a common sample rate");
            var sampleRate = (double)rates.First().QuantityInBaseUnits;

            string masterClock = "/" + Device.DeviceID + "/" + tasks.MasterType + "/SampleClock";
            tasks.Slaves.ForEach(t => t.Timing.ConfigureSampleClock(masterClock, sampleRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples));
            tasks.Master.Timing.ConfigureSampleClock("", sampleRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples);

            // Setup slave tasks to start with master task
            string masterTrigger = "/" + Device.DeviceID + "/" + tasks.MasterType + "/StartTrigger";
            tasks.Slaves.ForEach(t => t.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(masterTrigger, DigitalEdgeStartTriggerEdge.Rising));

            // Verify tasks
            tasks.All.ForEach(t => t.Control(TaskAction.Verify));

            if (_tasks != null)
            {
                _tasks.All.ForEach(t => t.Dispose());
            }
            _tasks = tasks;
        }

        public void StartHardware(bool waitForTrigger)
        {
            if (waitForTrigger)
            {
                string source = "/" + Device.DeviceID + "/pfi0";
                _tasks.Master.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(source, DigitalEdgeStartTriggerEdge.Rising);
            }
            else
            {
                _tasks.Master.Triggers.StartTrigger.ConfigureNone();
            }

            _tasks.Slaves.ForEach(t => t.Start());
            _tasks.Master.Start();
        }

        public void StopHardware()
        {
            _tasks.All.ForEach(t => t.Stop());
        }

        public NIChannelInfo ChannelInfo(ChannelType channelType, string channelName)
        {
            ICollection channels;
            switch (channelType)
            {
                case ChannelType.AI:
                    channels = _tasks.AnalogIn.AIChannels;
                    break;
                case ChannelType.AO:
                    channels = _tasks.AnalogOut.AOChannels;
                    break;
                case ChannelType.DI:
                    channels = _tasks.DigitalIn.DIChannels;
                    break;
                case ChannelType.DO:
                    channels = _tasks.DigitalOut.DOChannels;
                    break;
                default:
                    throw new ArgumentException("Unsupported stream type");
            }
            foreach (Channel c in channels.Cast<Channel>().Where(c => c.VirtualName == channelName))
            {
                return NIChannelInfo.FromChannel(c);
            }
            throw new ArgumentException("Specified channel is not configured");
        }

        private class Tasks
        {
            public Task AnalogIn { get; private set; }
            public Task AnalogOut { get; private set; }
            public Task DigitalIn { get; private set; }
            public Task DigitalOut { get; private set; }

            public readonly List<Task> All = new List<Task>();

            public Task Master { get { return All.First(); } }
            public List<Task> Slaves { get { return All.Where(t => t != Master).ToList(); } }

            public void CreateAITask(IEnumerable<string> physicalNames, double min, double max)
            {
                if (AnalogIn != null)
                    throw new InvalidOperationException();

                var t = new Task();
                t.AIChannels.CreateVoltageChannel(string.Join(",", physicalNames), "", (AITerminalConfiguration) (-1),
                                                  min, max, AIVoltageUnits.Volts);

                AnalogIn = t;
                All.Add(t);
            }

            public void CreateAOTask(IEnumerable<string> physicalNames, double min, double max)
            {
                if (AnalogOut != null)
                    throw new InvalidOperationException();

                var t = new Task();
                t.AOChannels.CreateVoltageChannel(string.Join(",", physicalNames), "", min, max, AOVoltageUnits.Volts);

                AnalogOut = t;
                All.Add(t);
            }

            public void CreateDITask(IEnumerable<string> physicalNames)
            {
                if (DigitalIn != null)
                    throw new InvalidOperationException();

                var t = new Task();
                t.DIChannels.CreateChannel(string.Join(",", physicalNames), "",
                                           ChannelLineGrouping.OneChannelForAllLines);

                DigitalIn = t;
                All.Add(t);
            }

            public void CreateDOTask(IEnumerable<string> physicalNames)
            {
                if (DigitalOut != null)
                    throw new InvalidOperationException();

                var t = new Task();
                t.DOChannels.CreateChannel(string.Join(",", physicalNames), "",
                                           ChannelLineGrouping.OneChannelForAllLines);

                DigitalOut = t;
                All.Add(t);
            }

            public string MasterType
            {
                get
                {
                    if (Master == AnalogIn)
                        return "ai";
                    if (Master == AnalogOut)
                        return "ao";
                    if (Master == DigitalIn)
                        return "di";
                    if (Master == DigitalOut)
                        return "do";
                    return "";
                }
            }
        }
    }

    public struct NIDeviceInfo
    {
        public string DeviceID;
        public string[] AIPhysicalChannels;
        public string[] AOPhysicalChannels;
        public string[] DIPorts;
        public string[] DOPorts;

        public static NIDeviceInfo FromDevice(Device device)
        {
            return new NIDeviceInfo
                {
                    DeviceID = device.DeviceID,
                    AIPhysicalChannels = device.AIPhysicalChannels,
                    AOPhysicalChannels = device.AOPhysicalChannels,
                    DIPorts = device.DIPorts,
                    DOPorts = device.DOPorts
                };
        }
    }

    public struct NIChannelInfo
    {
        public string PhysicalName;

        public static NIChannelInfo FromChannel(Channel channel)
        {
            return new NIChannelInfo
                {
                    PhysicalName = channel.PhysicalName
                };
        }
    }
}
