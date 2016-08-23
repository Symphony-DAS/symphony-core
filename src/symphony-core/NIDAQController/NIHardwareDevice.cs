using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NationalInstruments.DAQmx;
using Symphony.Core;
using Task = NationalInstruments.DAQmx.Task;

namespace NI
{
    sealed class NIHardwareDevice : INIDevice
    {
        private const int TRANSFER_BLOCK_SAMPLES = 512;

        private Device Device { get; set; }
        private DAQTasks Tasks { get; set; }

        public NIHardwareDevice(Device device)
        {
            Device = device;
        }

        public IEnumerable<KeyValuePair<Channel, double[]>> ReadWrite(IDictionary<Channel, double[]> output,
                                                                      IList<Channel> input, int nsamples,
                                                                      CancellationToken token)
        {
            if (nsamples < 0)
                throw new ArgumentException("nsamples may not be less than zero");

            var outGroups = output.GroupBy(kv => kv.Key.Type).ToDictionary(g => g.Key, g => g.ToDictionary(kv => kv.Key, kv => kv.Value));
            var inGroups = input.GroupBy(i => i.Type).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<KeyValuePair<Channel, double[]>>();
            if (outGroups.ContainsKey(ChannelType.AI))
                result.AddRange(ReadWriteAnalog(outGroups[ChannelType.AI], inGroups[ChannelType.AI], nsamples, token));
            if (outGroups.ContainsKey(ChannelType.DI))
                result.AddRange(ReadWriteDigital(outGroups[ChannelType.DI], inGroups[ChannelType.DI], nsamples, token));

            return result;
        }

        private IEnumerable<KeyValuePair<Channel, double[]>> ReadWriteAnalog(IDictionary<Channel, double[]> output,
                                                                             IList<Channel> input, int nsamples,
                                                                             CancellationToken token)
        {
            int nIn = 0;
            int nOut = 0;

            var inputSamples = new double[input.Count, 2*nsamples];
            var outputSamples = new double[output.Count, nsamples];

            var outChans = Tasks.AIChannels.Cast<AOChannel>().ToList();
            for (int i = 0; i < output.Count; i++)
            {
                for (int j = 0; j < nsamples; j++)
                {
                    outputSamples[i, j] = output[outChans[i]][j];
                }
            }

            int transferBlock = Math.Min(nsamples, TRANSFER_BLOCK_SAMPLES);
            var inputData = new double[input.Count, transferBlock];

            var reader = new AnalogMultiChannelReader(Tasks.AIStream);
            var readerResult = reader.BeginMemoryOptimizedReadMultiSample(transferBlock, null, null, inputData);

            var writer = new AnalogMultiChannelWriter(Tasks.AOStream);

            while ((nOut < nsamples && output.Any()) || (nIn < nsamples && input.Any()))
            {
                if (token.IsCancellationRequested)
                    break;

                bool inBlockAvailable = Tasks.AIStream.AvailableSamplesPerChannel >= transferBlock;
                if (inBlockAvailable)
                {
                    int nRead;
                    inputData = reader.EndMemoryOptimizedReadMultiSample(readerResult, out nRead);

                    for (int i = 0; i < input.Count; i++)
                    {
                        for (int j = 0; j < nRead; j++)
                        {
                            inputSamples[i, nIn + j] = inputData[i, j];
                        }
                    }
                    nIn += nRead;

                    if (nIn < nsamples)
                    {
                        readerResult = reader.BeginMemoryOptimizedReadMultiSample(transferBlock, null, null, inputData);
                    }
                }

                bool outBlockAvailable = Tasks.AOStream.OutputBufferSpaceAvailable >= transferBlock;
                if (outBlockAvailable)
                {
                    if (nOut < nsamples)
                    {
                        int nWrite = Math.Min(transferBlock, nsamples - nOut);
                        var outputData = new double[output.Count, nWrite];
                        for (int i = 0; i < output.Count; i++)
                        {
                            for (int j = 0; j < nWrite; j++)
                            {
                                outputData[i, j] = outputSamples[i, nOut + j];
                            }
                        }
                        writer.WriteMultiSample(false, outputData);
                        nOut += nWrite;
                    }
                }
            }

            var result = new Dictionary<Channel, double[]>();
            var inChans = Tasks.AIChannels.Cast<AIChannel>().ToList();

            foreach (Channel chan in input)
            {
                var inData = new double[nIn];
                int chanIndex = inChans.FindIndex(c => c.PhysicalName == chan.PhysicalName);

                for (int j = 0; j < nIn; j++)
                {
                    inData[j] = inputSamples[chanIndex, j];
                }

                result[chan] = inData;
            }

            return result;
        }

        private IEnumerable<KeyValuePair<Channel, double[]>> ReadWriteDigital(IDictionary<Channel, double[]> output,
                                                                              IList<Channel> input, int nsamples,
                                                                              CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public void SetStreamBackground(NIDAQOutputStream stream)
        {
            if (stream != null)
            {
                WriteSingle(stream, stream.Background);
            }
        }

        private void WriteSingle(NIDAQOutputStream stream, IMeasurement value)
        {
            var quantity = (double) Converters.Convert(value, NIDAQOutputStream.DAQUnits).QuantityInBaseUnits;
            if (stream.PhysicalChannelType == PhysicalChannelTypes.AO)
                WriteSingleAnalog(stream, quantity);
            else if (stream.PhysicalChannelType == PhysicalChannelTypes.DOPort)
                WriteSingleDigital(stream, quantity);
        }

        private void WriteSingleAnalog(NIDAQOutputStream stream, double value)
        {
            using (var t = new Task())
            {
                t.AOChannels.CreateVoltageChannel(stream.PhysicalName, "", Device.AOVoltageRanges.Min(),
                                                  Device.AOVoltageRanges.Max(), AOVoltageUnits.Volts);
                var writer = new AnalogSingleChannelWriter(t.Stream);
                writer.WriteSingleSample(true, value);
            }
        }

        private void WriteSingleDigital(NIDAQOutputStream stream, double value)
        {
            using (var t = new Task())
            {
                t.DOChannels.CreateChannel(stream.PhysicalName, "", ChannelLineGrouping.OneChannelForAllLines);
                var writer = new DigitalSingleChannelWriter(t.Stream);
                writer.WriteSingleSamplePort(true, (UInt32) value);
            }
        }

        public IInputData ReadStream(NIDAQInputStream stream)
        {
            double quantity;
            if (stream.PhysicalChannelType == PhysicalChannelTypes.AI)
                quantity = ReadSingleAnalog(stream);
            else if (stream.PhysicalChannelType == PhysicalChannelTypes.DIPort)
                quantity = ReadSingleDigital(stream);
            else
                throw new NotSupportedException("Unsupported stream channel type");

            var inData =
                new InputData(
                    new List<IMeasurement> {new Measurement(quantity, 0, NIDAQOutputStream.DAQUnits)},
                    new Measurement(0, 0, "Hz"),
                    DateTimeOffset.Now)
                    .DataWithStreamConfiguration(stream, stream.Configuration);

            return inData.DataWithUnits(stream.MeasurementConversionTarget);
        }

        private double ReadSingleAnalog(NIDAQInputStream stream)
        {
            using (var t = new Task())
            {
                t.AIChannels.CreateVoltageChannel(stream.PhysicalName, "", (AITerminalConfiguration) (-1),
                                                  Device.AIVoltageRanges.Min(), Device.AIVoltageRanges.Max(),
                                                  AIVoltageUnits.Volts);
                var reader = new AnalogSingleChannelReader(t.Stream);
                return reader.ReadSingleSample();
            }
        }

        private double ReadSingleDigital(NIDAQInputStream stream)
        {
            using (var t = new Task())
            {
                t.DIChannels.CreateChannel(stream.PhysicalName, "", ChannelLineGrouping.OneChannelForAllLines);
                var reader = new DigitalSingleChannelReader(t.Stream);
                return reader.ReadSingleSamplePortUInt32();
            }
        }

        public void Write(IDictionary<Channel, double[]> output)
        {
            var ns = output.Values.Select(v => v.Count()).Distinct().ToList();
            if (ns.Count() > 1)
                throw new ArgumentException("Preload sample buffers must be homogenous in length");
            int nsamples = ns.First();

            var groups = output.GroupBy(kv => kv.Key.Type).ToDictionary(g => g.Key, g => g.ToDictionary(kv => kv.Key, kv => kv.Value));
            if (groups.ContainsKey(ChannelType.AO))
                WriteAnalog(groups[ChannelType.AO], nsamples);
            if (groups.ContainsKey(ChannelType.DO))
                WriteDigital(groups[ChannelType.DO], nsamples);
        }

        private void WriteAnalog(IDictionary<Channel, double[]> output, int nsamples)
        {
            var data = new double[output.Count, nsamples];
            var chans = Tasks.AOChannels.Cast<AOChannel>().ToList();

            foreach (var o in output)
            {
                int chanIndex = chans.FindIndex(c => c.PhysicalName == o.Key.PhysicalName);
                for (int i = 0; i < o.Value.Count(); i++)
                {
                    data[chanIndex, i] = o.Value[i];
                }
            }

            var writer = new AnalogMultiChannelWriter(Tasks.AOStream);
            writer.WriteMultiSample(false, data);
        }

        private void WriteDigital(IDictionary<Channel, double[]> output, int nsamples)
        {
            var data = new UInt32[output.Count, nsamples];
            var chans = Tasks.DOChannels.Cast<DOChannel>().ToList();

            foreach (var o in output)
            {
                int chanIndex = chans.FindIndex(c => c.PhysicalName == o.Key.PhysicalName);
                for (int i = 0; i < o.Value.Count(); i++)
                {
                    data[chanIndex, i] = (UInt32) o.Value[i];
                }
            }

            var writer = new DigitalMultiChannelWriter(Tasks.DOStream);
            writer.WriteMultiSamplePort(false, data);
        }

        public static IEnumerable<NIDAQController> AvailableControllers()
        {
            return DaqSystem.Local.Devices.Select(d => new NIDAQController(d));
        }

        internal static INIDevice OpenDevice(string deviceName)
        {
            return new NIHardwareDevice(DaqSystem.Local.LoadDevice(deviceName));
        }

        public void CloseDevice()
        {
            if (Tasks != null)
            {
                Tasks.All.ForEach(t => t.Dispose());
            }
            Device.Dispose();
        }

        public void ConfigureChannels(IEnumerable<NIDAQStream> daqStreams)
        {
            var streams = daqStreams.ToList();
            if (!streams.Any())
                throw new ArgumentException("Streams cannot be empty");

            var tasks = new DAQTasks();
            var chanNames = streams.GroupBy(s => s.PhysicalChannelType).ToDictionary(g => g.Key, g => g.Select(s => s.PhysicalName));

            // Create appropriate tasks
            if (chanNames.ContainsKey(PhysicalChannelTypes.AI))
                tasks.CreateAITask(chanNames[PhysicalChannelTypes.AI], Device.AIVoltageRanges.Min(), Device.AIVoltageRanges.Max());
            if (chanNames.ContainsKey(PhysicalChannelTypes.AO))
                tasks.CreateAOTask(chanNames[PhysicalChannelTypes.AO], Device.AOVoltageRanges.Min(), Device.AOVoltageRanges.Max());
            if (chanNames.ContainsKey(PhysicalChannelTypes.DIPort))
                tasks.CreateDITask(chanNames[PhysicalChannelTypes.DIPort]);
            if (chanNames.ContainsKey(PhysicalChannelTypes.DOPort))
                tasks.CreateDOTask(chanNames[PhysicalChannelTypes.DOPort]);

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

            if (Tasks != null)
            {
                Tasks.All.ForEach(t => t.Dispose());
            }
            Tasks = tasks;
        }

        public void StartHardware(bool waitForTrigger)
        {
            if (waitForTrigger)
            {
                string source = "/" + Device.DeviceID + "/pfi0";
                Tasks.Master.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger(source, DigitalEdgeStartTriggerEdge.Rising);
            }
            else
            {
                Tasks.Master.Triggers.StartTrigger.ConfigureNone();
            }

            Tasks.Slaves.ForEach(t => t.Start());
            Tasks.Master.Start();
        }

        public void StopHardware()
        {
            if (Tasks != null)
            {
                Tasks.All.ForEach(t => t.Stop());
                Tasks.All.ForEach(t => t.Control(TaskAction.Unreserve));
            }
        }

        public string[] AIPhysicalChannels
        {
            get { return Device.AIPhysicalChannels; }
        }

        public string[] AOPhysicalChannels
        {
            get { return Device.AOPhysicalChannels; }
        }

        public string[] DIPorts
        {
            get { return Device.DIPorts; }
        }

        public string[] DOPorts
        {
            get { return Device.DOPorts; }
        }

        public Channel Channel(string channelName)
        {
            Channel chan = Tasks.All.SelectMany(t => t.AIChannels.Cast<Channel>()
                                                      .Concat(t.AOChannels.Cast<Channel>())
                                                      .Concat(t.DIChannels.Cast<Channel>())
                                                      .Concat(t.DOChannels.Cast<Channel>()))
                                .FirstOrDefault(c => c.VirtualName == channelName);
            
            if (chan == null)
                throw new ArgumentException("Channel " + channelName + " is not configured");

            return chan;
        }

        private class DAQTasks
        {
            private Task _analogIn;
            private Task _analogOut;
            private Task _digitalIn;
            private Task _digitalOut;

            public readonly List<Task> All = new List<Task>();

            public Task Master { get { return All.First(); } }
            public List<Task> Slaves { get { return All.Where(t => t != Master).ToList(); } }

            public void CreateAITask(IEnumerable<string> physicalNames, double min, double max)
            {
                if (_analogIn != null)
                    throw new InvalidOperationException("Analog input task already created");

                var t = new Task();
                t.AIChannels.CreateVoltageChannel(string.Join(",", physicalNames), "", (AITerminalConfiguration) (-1),
                                                  min, max, AIVoltageUnits.Volts);
                
                _analogIn = t;
                All.Add(t);
            }

            public AIChannelCollection AIChannels { get { return _analogIn.AIChannels; } }

            public DaqStream AIStream { get { return _analogIn.Stream; } }

            public void CreateAOTask(IEnumerable<string> physicalNames, double min, double max)
            {
                if (_analogOut != null)
                    throw new InvalidOperationException("Analog output task already created");

                var t = new Task();
                t.AOChannels.CreateVoltageChannel(string.Join(",", physicalNames), "", min, max, AOVoltageUnits.Volts);

                _analogOut = t;
                All.Add(t);
            }

            public AOChannelCollection AOChannels { get { return _analogOut.AOChannels; } }

            public DaqStream AOStream { get { return _analogOut.Stream; } }

            public void CreateDITask(IEnumerable<string> physicalNames)
            {
                if (_digitalIn != null)
                    throw new InvalidOperationException("Digital input task already created");

                var t = new Task();
                t.DIChannels.CreateChannel(string.Join(",", physicalNames), "",
                                           ChannelLineGrouping.OneChannelForAllLines);

                _digitalIn = t;
                All.Add(t);
            }

            public DIChannelCollection DIChannels { get { return _digitalIn.DIChannels; } }

            public DaqStream DIStream { get { return _digitalIn.Stream; } }

            public void CreateDOTask(IEnumerable<string> physicalNames)
            {
                if (_digitalOut != null)
                    throw new InvalidOperationException("Digital output task already created");

                var t = new Task();
                t.DOChannels.CreateChannel(string.Join(",", physicalNames), "",
                                           ChannelLineGrouping.OneChannelForAllLines);

                _digitalOut = t;
                All.Add(t);
            }

            public DOChannelCollection DOChannels { get { return _digitalOut.DOChannels; } }

            public DaqStream DOStream { get { return _digitalOut.Stream; } }

            public string MasterType
            {
                get
                {
                    string type;
                    if (Master == _analogIn)
                        type = "ai";
                    else if (Master == _analogOut)
                        type = "ao";
                    else if (Master == _digitalIn)
                        type = "di";
                    else
                        type = "do";
                    return type;
                }
            }
        }
    }
}
