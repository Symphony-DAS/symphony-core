using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using ApprovalTests;
using ApprovalTests.Reporters;
using HDF5DotNet;
using Symphony.Core;
using Symphony.SimulationDAQController;
using NUnit.Framework;

namespace IntegrationTests
{
    [TestFixture]
    class SimulationIntegration
    {
        private const short SIMULATION_TIMESTEP = 500;

        [Test]
        public void SingleEpoch(
            [Values(1000, 5000, 10000, 20000)] double sampleRate,
            [Values(1, 2)] int nChannels
            )
        {
            RunSingleEpoch(sampleRate, nChannels, new FakeEpochPersistor());
        }

        private void RunSingleEpoch(double sampleRate, int nChannels, IEpochPersistor epochPersistor)
        {
            Epoch e;
            IExternalDevice dev0;
            RenderedStimulus stim1;
            IExternalDevice dev1;
            RenderedStimulus stim2;
            IList<IMeasurement> stimData;
            var controller = SetupController(sampleRate, out e, out dev0, out stim1, out dev1, out stim2, out stimData, nChannels);

            var time = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));

            epochPersistor.BeginEpochBlock(e.ProtocolID, time);
            
            controller.RunEpoch(e, epochPersistor);
            
            epochPersistor.EndEpochBlock(time.Add(e.Duration));

            Assert.AreEqual((TimeSpan)stim1.Duration, e.Responses[dev0].Duration);
            if (nChannels > 1)
                Assert.AreEqual((TimeSpan)stim2.Duration, e.Responses[dev1].Duration);

            var inputData = e.Responses[dev0].Data;
            const double MAX_VOLTAGE_DIFF = 0.001;
            int failures = inputData.Select((t, i) => t.QuantityInBaseUnit - stimData[i].QuantityInBaseUnit)
                .Count(dif => Math.Abs(dif) > (decimal) MAX_VOLTAGE_DIFF);

            Assert.AreEqual(0, failures);
        }

        [Test]
        [UseReporter(typeof(NUnitReporter))]
        public void SingleH5EpochPersistence()
        {
            if (File.Exists("SingleH5Persistence.h5"))
                File.Delete("SingleH5Persistence.h5");

            using (var persistor = H5EpochPersistor.Create("SingleH5Persistence.h5", "for testing purposes"))
            {
                var time = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));

                var src = persistor.AddSource("source1", null);

                persistor.BeginEpochGroup("label1", src, time);

                RunSingleEpoch(5000, 2, persistor);
                persistor.EndEpochGroup(time.AddMinutes(2));
                
                persistor.Close();
            }

            //VerifyHDF5File("SingleH5Persistence.h5");
        }

        [Test]
        //[UseReporter(typeof(FileLauncherReporter))]
        public void AppendToExistingHDF5()
        {
            if (File.Exists("AppendToExistingHDF5.h5"))
                File.Delete("AppendToExistingHDF5.h5");

            using (var persistor = H5EpochPersistor.Create("AppendToExistingHDF5.h5", "for testing purposes"))
            {
                var time = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));

                var src = persistor.AddSource("source1", null);
                persistor.BeginEpochGroup("label1", src, time);

                RunSingleEpoch(5000, 2, persistor);
                persistor.EndEpochGroup(time.AddMilliseconds(100));

                persistor.Close();
            }

            using (var persistor = new H5EpochPersistor("AppendToExistingHDF5.h5"))
            {
                var time = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));

                var src = persistor.AddSource("source2", null);
                persistor.BeginEpochGroup("label2", src, time);

                RunSingleEpoch(5000, 2, persistor);
                persistor.EndEpochGroup(time.AddMilliseconds(100));

                persistor.Close();
            }


            //VerifyHDF5File("AppendToExistingHDF5.h5");
        }


        [Test]
        public void MultipleEpochs(
            [Values(1000, 5000, 10000, 20000)] double sampleRate
            )
        {
            Epoch e;
            IExternalDevice dev0;
            RenderedStimulus stim1;
            IExternalDevice dev1;
            RenderedStimulus stim2;
            IList<IMeasurement> stimData;
            var controller = SetupController(sampleRate, out e, out dev0, out stim1, out dev1, out stim2, out stimData, 2);

            controller.RunEpoch(e, new FakeEpochPersistor());

            var inputData = e.Responses[dev0].Data;
            const double MAX_VOLTAGE_DIFF = 0.001;
            int failures = inputData.Select((t, i) => t.QuantityInBaseUnit - stimData[i].QuantityInBaseUnit)
                .Count(dif => Math.Abs(dif) > (decimal) MAX_VOLTAGE_DIFF);

            Assert.AreEqual(0, failures);

            e = new Epoch("LowGainSimulation");
            dev0 = controller.GetDevice("Device0");
            dev1 = controller.GetDevice("Device1");

            var srate = new Measurement((decimal) sampleRate, "Hz");

            stim1 = new RenderedStimulus((string) "RenderedStimulus",
                                         (IDictionary<string, object>) new Dictionary<string, object>(),
                                         (IOutputData) new OutputData(stimData, srate, false));
            stim2 = new RenderedStimulus((string) "RenderedStimulus",
                                         (IDictionary<string, object>) new Dictionary<string, object>(),
                                         (IOutputData) new OutputData(stimData, srate, false));

            e.Stimuli[dev0] = stim1;
            e.Stimuli[dev1] = stim2;

            e.Responses[dev0] = new Response();
            e.Responses[dev1] = new Response();

            e.Backgrounds[dev1] = new Background(new Measurement(0, "V"), srate);
            e.Backgrounds[dev0] = new Background(new Measurement(0, "V"), srate);

            Assert.DoesNotThrow(() => controller.RunEpoch(e, new FakeEpochPersistor()));

            inputData = e.Responses[dev0].Data;
            failures = inputData.Select((t, i) => t.QuantityInBaseUnit - stimData[i].QuantityInBaseUnit)
                .Count(dif => Math.Abs(dif) > (decimal) MAX_VOLTAGE_DIFF);


            Assert.AreEqual(0, failures);

        }

        private static Controller SetupController(double sampleRate, out Epoch e, out IExternalDevice dev0, out RenderedStimulus stim1, out IExternalDevice dev1, out RenderedStimulus stim2, out IList<IMeasurement> stimData, int nChannels)
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            // use an incrementing clock so timestamps are predictable
            var incrementingClock = new IncrementingClock();

            var daq = new SimulationDAQController { Clock = incrementingClock };

            var out0 = new DAQOutputStream("Out0")
            {
                MeasurementConversionTarget = "V",
                Clock = daq.Clock,
                SampleRate = new Measurement((decimal)sampleRate, "Hz")
            };

            var out1 = new DAQOutputStream("Out1")
            {
                MeasurementConversionTarget = "V",
                Clock = daq.Clock,
                SampleRate = new Measurement((decimal)sampleRate, "Hz")
            };

            var in0 = new DAQInputStream("In0")
                {
                    MeasurementConversionTarget = "V",
                    Clock = daq.Clock,
                    SampleRate = new Measurement((decimal) sampleRate, "Hz")
                };

            var in1 = new DAQInputStream("In1")
            {
                MeasurementConversionTarget = "V",
                Clock = daq.Clock,
                SampleRate = new Measurement((decimal)sampleRate, "Hz")
            };

            daq.AddStream(out0);
            daq.AddStream(out1);
            daq.AddStream(in0);
            daq.AddStream(in1);

            var controller = new Controller(daq, daq.Clock);

            var streamNameMap = new Dictionary<string, string>();
            streamNameMap["Out0"] = "In0";
            if (nChannels > 1)
                streamNameMap["Out1"] = "In1";

            foreach (var stream in daq.Streams)
            {
                stream.SampleRate = new Measurement((decimal) sampleRate, "Hz");
            }

            daq.SimulationRunner += (output, timestep) =>
                                        {
                                            var input = new ConcurrentDictionary<IDAQInputStream, IInputData>();

                                            Parallel.ForEach(output, (kv) =>
                                                                         {
                                                                             var outData = kv.Value;
                                                                             var outStream = kv.Key;
                                                                             var inStream = daq.InputStreams.Where((s) =>
                                                                                                                   s.Name == streamNameMap[outStream.Name]).First();

                                                                             var data = outData.DataWithUnits("V").Data;
                                                                             var inData = new InputData(data,
                                                                                                        outData.SampleRate,
                                                                                                        incrementingClock.Now)
                                                                                                        .DataWithNodeConfiguration("SimulationController",daq.Configuration);

                                                                             input[inStream] = inData;
                                                                         }
                                                );

                                            return input;
                                        };


            var protocolParams = new Dictionary<string, object>(1);
            protocolParams["key1"] = "value1";

            e = new Epoch("LowGainSimulation", protocolParams);

            dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, new Measurement(0, "V"))
                {
                    MeasurementConversionTarget = "V",
                    Clock = daq.Clock,
                    InputSampleRate = new Measurement((decimal)sampleRate, "Hz"),
                    OutputSampleRate = new Measurement((decimal)sampleRate, "Hz")
                };
            dev0.BindStream(out0);
            dev0.BindStream(in0);

            dev1 = new UnitConvertingExternalDevice("Device1", "Manufacturer", controller, new Measurement(0, "V"))
                {
                    MeasurementConversionTarget = "V",
                    Clock = daq.Clock,
                    InputSampleRate = new Measurement((decimal)sampleRate, "Hz"),
                    OutputSampleRate = new Measurement((decimal)sampleRate, "Hz")
                };
            dev1.BindStream(out1);
            dev1.BindStream(in1);

            if (nChannels == 1)
            {
                dev1.UnbindStream(dev1.Streams.Values.First().Name);
            }

            stimData = Enumerable.Range(0, (int)(10 * sampleRate))
                                   .Select(i => new Measurement(i, -3, "V") as IMeasurement)
                                   .ToList();
            var srate = new Measurement((decimal) sampleRate, "Hz");

            stim1 = new RenderedStimulus((string) "RenderedStimulus",
                                         (IDictionary<string, object>) new Dictionary<string, object>(),
                                         (IOutputData) new OutputData(stimData, srate, false));
            stim2 = new RenderedStimulus((string) "RenderedStimulus",
                                         (IDictionary<string, object>) new Dictionary<string, object>(),
                                         (IOutputData) new OutputData(stimData, srate, false));

            e.Stimuli[dev0] = stim1;

            if (nChannels > 1)
                e.Stimuli[dev1] = stim2;

            e.Responses[dev0] = new Response();
            if (nChannels > 1)
                e.Responses[dev1] = new Response();

            controller.BackgroundDataStreams[dev0] = new BackgroundOutputDataStream(new Background(new Measurement(0, "V"), srate));
            controller.BackgroundDataStreams[dev1] = new BackgroundOutputDataStream(new Background(new Measurement(0, "V"), srate));

            e.Backgrounds[dev0] = new Background(new Measurement(0, "V"), srate);
            e.Backgrounds[dev1] = new Background(new Measurement(0, "V"), srate);

            return controller;
        }

        private static void VerifyHDF5File(string file)
        {
            // Directly comparing HDF5 files does not work because of some
            // unknown discrepancies in the binary files. We'll compare the 
            // XML dump via h5dump instead.
            var startInfo = new ProcessStartInfo(@"..\..\..\..\..\..\externals\HDF5\bin\h5dump",
                                                 @" --xml " + file);
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            Process proc = Process.Start(startInfo);

            Approvals.VerifyXml(proc.StandardOutput.ReadToEnd());
        }
    }
}
