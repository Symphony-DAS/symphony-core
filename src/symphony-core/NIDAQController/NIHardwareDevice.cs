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
            if (stream == null) 
                return;

            switch (stream.PhysicalChannelType)
            {
                case PhysicalChannelTypes.AO:
                    WriteSingleAnalog(stream, (double) Converters.Convert(stream.Background, "V").QuantityInBaseUnits);
                    break;
                case PhysicalChannelTypes.DOPort:
                    WriteSingleDigital(stream, (byte) Converters.Convert(stream.Background, Measurement.UNITLESS).QuantityInBaseUnits);
                    break;
            }
        }

        private void WriteSingleAnalog(NIDAQOutputStream stream, double value)
        {
            //using (var t = new Task())
            //{
            //    t.AOChannels.CreateVoltageChannel(stream.PhysicalName, "", Device.AOVoltageRanges.First(),
            //                                      Device.AOVoltageRanges.Last(), AOVoltageUnits.Volts);
            //    var writer = new AnalogSingleChannelWriter(t.Stream);
            //    writer.WriteSingleSample(true, value);
            //}
        }

        private void WriteSingleDigital(NIDAQOutputStream stream, uint value)
        {
            //using (var t = new Task())
            //{
            //    t.DOChannels.CreateChannel(stream.PhysicalName, "", ChannelLineGrouping.OneChannelForAllLines);
            //    var writer = new DigitalSingleChannelWriter(t.Stream);
            //    writer.WriteSingleSamplePort(true, value);
            //}
        }

        public void PreloadAnalog(IDictionary<string, double[]> output)
        {
            if (!output.Any())
                return;

            var ns = output.Values.Select(v => v.Count()).Distinct().ToList();
            if (ns.Count() > 1)
                throw new ArgumentException("Preload sample buffers must be homogenous in length");
            int nsamples = ns.First();

            var data = new double[output.Count, nsamples];
            var chans = _tasks.AnalogOut.AOChannels.Cast<AOChannel>().ToList();

            foreach (var o in output)
            {
                int chanIndex = chans.FindIndex(c => c.VirtualName == o.Key);
                for (int i = 0; i < o.Value.Count(); i++)
                {
                    data[chanIndex, i] = o.Value[i];
                }
            }

            var writer = new AnalogMultiChannelWriter(_tasks.AnalogOut.Stream);
            writer.WriteMultiSample(false, data);
        }

        public void PreloadDigital(IDictionary<string, UInt32[]> output)
        {
            if (!output.Any())
                return;

            var ns = output.Values.Select(v => v.Count()).Distinct().ToList();
            if (ns.Count() > 1)
                throw new ArgumentException("Preload sample buffers must be homogenous in length");
            int nsamples = ns.First();

            var data = new UInt32[output.Count, nsamples];
            var chans = _tasks.DigitalOut.DOChannels.Cast<DOChannel>().ToList();

            foreach (var o in output)
            {
                int chanIndex = chans.FindIndex(c => c.VirtualName == o.Key);
                for (int i = 0; i < o.Value.Count(); i++)
                {
                    data[chanIndex, i] = o.Value[i];
                }
            }

            var writer = new DigitalMultiChannelWriter(_tasks.DigitalOut.Stream);
            writer.WriteMultiSamplePort(false, data);
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
            var chanNames = streams.GroupBy(s => s.PhysicalChannelType).ToDictionary(g => g.Key, g => g.Select(s => s.PhysicalName));

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
                throw new ArgumentException("Streams need a common sample rate");
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
            if (_tasks != null)
            {
                _tasks.All.ForEach(t => t.Stop());   
            }
        }

        public NIChannelInfo ChannelInfo(string channelName)
        {
            var channels = new List<Channel>();
            channels.AddRange(_tasks.All.SelectMany(t => t.AIChannels.Cast<Channel>()));
            channels.AddRange(_tasks.All.SelectMany(t => t.AOChannels.Cast<Channel>()));
            channels.AddRange(_tasks.All.SelectMany(t => t.DIChannels.Cast<Channel>()));
            channels.AddRange(_tasks.All.SelectMany(t => t.DOChannels.Cast<Channel>()));
            foreach (Channel c in channels.Where(c => c.VirtualName == channelName))
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

            public Task TaskForChannelType(ChannelType type)
            {
                switch (type)
                {
                    case ChannelType.AI:
                        return AnalogIn;
                    case ChannelType.AO:
                        return AnalogOut;
                    case ChannelType.DI:
                        return DigitalIn;
                    case ChannelType.DO:
                        return DigitalOut;
                    default:
                        throw new ArgumentException("No task for channel type: " + type);
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

    public static class PhysicalChannelTypesExtensions
    {
        public static ChannelType ToChannelType(this PhysicalChannelTypes pct)
        {
            switch (pct)
            {
                case PhysicalChannelTypes.AI:
                    return ChannelType.AI;
                case PhysicalChannelTypes.AO:
                    return ChannelType.AO;
                case PhysicalChannelTypes.DILine:
                case PhysicalChannelTypes.DIPort:
                    return ChannelType.DI;
                case PhysicalChannelTypes.DOLine:
                case PhysicalChannelTypes.DOPort:
                    return ChannelType.DO;
                case PhysicalChannelTypes.CI:
                    return ChannelType.CI;
                case PhysicalChannelTypes.CO:
                    return ChannelType.CO;
                default:
                    throw new ArgumentException();
            }
        }
    }
}
