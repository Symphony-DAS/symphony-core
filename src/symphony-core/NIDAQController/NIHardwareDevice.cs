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

        private readonly Task _analogInputTask;
        private readonly Task _analogOutputTask;
        private readonly Task _digitalInputTask;
        private readonly Task _digitalOutputTask;

        public NIHardwareDevice(Device device)
        {
            Device = device;

            _analogInputTask = new Task();
            _analogOutputTask = new Task();
            _digitalInputTask = new Task();
            _digitalOutputTask = new Task();
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
            Device.Dispose();
        }

        public void ConfigureChannels(IEnumerable<NIDAQStream> daqStreams)
        {
            var streams = daqStreams.ToList();
            var aiNames = streams.Where(s => s.ChannelType == StreamType.ANALOG_IN).Select(s => s.FullName);
            var aoNames = streams.Where(s => s.ChannelType == StreamType.ANALOG_OUT).Select(s => s.FullName);
            var diNames = streams.Where(s => s.ChannelType == StreamType.DIGITAL_IN).Select(s => s.FullName);
            var doNames = streams.Where(s => s.ChannelType == StreamType.DIGITAL_OUT).Select(s => s.FullName);

            // Configure tasks
            _analogInputTask.AIChannels.CreateVoltageChannel(string.Join(",", aiNames), "",
                                                             (AITerminalConfiguration)(-1),
                                                             Device.AIVoltageRanges.First(),
                                                             Device.AIVoltageRanges.Last(), AIVoltageUnits.Volts);
            _analogOutputTask.AOChannels.CreateVoltageChannel(string.Join(",", aoNames), "",
                                                              Device.AOVoltageRanges.First(),
                                                              Device.AOVoltageRanges.Last(), AOVoltageUnits.Volts);
            _digitalInputTask.DIChannels.CreateChannel(string.Join(",", diNames), "",
                                                       ChannelLineGrouping.OneChannelForAllLines);
            _digitalOutputTask.DOChannels.CreateChannel(string.Join(",", doNames), "",
                                                        ChannelLineGrouping.OneChannelForAllLines);

            // Setup timing
            string signalSource = "/" + DeviceID + "/ai/SampleClock";
            var rates = streams.Select(s => s.SampleRate).Distinct().ToList();
            if (rates.Count() > 1)
                throw new DaqException("Streams need a common sample rate");
            var sampleRate = (double) rates.First().QuantityInBaseUnits;
            _analogInputTask.Timing.ConfigureSampleClock(signalSource, sampleRate, SampleClockActiveEdge.Rising,
                                                         SampleQuantityMode.ContinuousSamples);
            _analogOutputTask.Timing.ConfigureSampleClock(signalSource, sampleRate, SampleClockActiveEdge.Rising,
                                                          SampleQuantityMode.ContinuousSamples);
            _digitalInputTask.Timing.ConfigureSampleClock(signalSource, sampleRate, SampleClockActiveEdge.Rising,
                                                          SampleQuantityMode.ContinuousSamples);
            _digitalOutputTask.Timing.ConfigureSampleClock(signalSource, sampleRate, SampleClockActiveEdge.Rising,
                                                           SampleQuantityMode.ContinuousSamples);

            // Verify tasks
            _analogInputTask.Control(TaskAction.Verify);
            _analogOutputTask.Control(TaskAction.Verify);
            _digitalInputTask.Control(TaskAction.Verify);
            _digitalOutputTask.Control(TaskAction.Verify);
        }

        public string DeviceID { get { return Device.DeviceID; } }

        public string[] AIChannels { get { return Device.AIPhysicalChannels; } }

        public string[] AOChannels { get { return Device.AOPhysicalChannels; } }

        public string[] DIPorts { get { return Device.DIPorts; } }

        public string[] DOPorts { get { return Device.DIPorts; } }
    }
}
