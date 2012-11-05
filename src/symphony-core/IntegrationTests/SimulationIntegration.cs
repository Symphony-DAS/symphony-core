using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using IntegrationTests.Properties;

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

        private void RunSingleEpoch(double sampleRate, int nChannels, EpochPersistor epochPersistor)
        {
            Epoch e;
            IExternalDevice dev0;
            RenderedStimulus stim1;
            IExternalDevice dev1;
            RenderedStimulus stim2;
            IList<IMeasurement> stimData;
            var controller = SetupController(sampleRate, out e, out dev0, out stim1, out dev1, out stim2, out stimData, nChannels);

            controller.RunEpoch(e, epochPersistor);

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
        public void SingleEpochXMLPersistence()
        {
            var sb = new System.Text.StringBuilder();
            var sxw = XmlWriter.Create(sb);
            var gID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306a3");
            var exp = new EpochXMLPersistor(sxw, () => gID);

            var g1ID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306f3");
            var startTime1 = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));

            exp.BeginEpochGroup("label", "source", new string[0], new Dictionary<string, object>(), g1ID, startTime1);
            RunSingleEpoch(5, 2, exp);
            exp.EndEpochGroup();

            exp.Close();

            Approvals.VerifyXml(sb.ToString());

        }


        [Test]
        //[UseReporter(typeof(FileLauncherReporter))]
        public void SingleEpochHDF5Persistence()
        {
            if (File.Exists("SingleEpochHDF5Persistence.h5"))
                File.Delete("SingleEpochHDF5Persistence.h5");

            var gID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306a4");
            using (var exp = new EpochHDF5Persistor("SingleEpochHDF5Persistence.h5", null, () => gID))
            {
                var time = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));
                var guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");

                exp.BeginEpochGroup("label1",
                    "source1",
                    new string[0],
                    new Dictionary<string, object>(),
                    guid,
                    time);

                RunSingleEpoch(5000, 2, exp);
                exp.EndEpochGroup();
                
                exp.Close();
            }

            Approvals.VerifyFile("SingleEpochHDF5Persistence.h5");

        }

        [Test]
        //[UseReporter(typeof(FileLauncherReporter))]
        public void AppendToExistingHDF5()
        {
            if (File.Exists("AppendToExistingHDF5.h5"))
                File.Delete("AppendToExistingHDF5.h5");

            var gID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306a4");
            using (var exp = new EpochHDF5Persistor("AppendToExistingHDF5.h5", null, () => gID))
            {
                var time = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));
                var guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");

                exp.BeginEpochGroup("label1",
                    "source1",
                    new string[0],
                    new Dictionary<string, object>(),
                    guid,
                    time);

                RunSingleEpoch(5000, 2, exp);
                exp.EndEpochGroup();

                exp.Close();
            }

            gID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306a5");
            using (var exp = new EpochHDF5Persistor("AppendToExistingHDF5.h5", null, () => gID))
            {
                var time = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));
                var guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97E");

                exp.BeginEpochGroup("label1",
                    "source1",
                    new string[0],
                    new Dictionary<string, object>(),
                    guid,
                    time);

                RunSingleEpoch(5000, 2, exp);
                exp.EndEpochGroup();

                exp.Close();
            }


            Approvals.VerifyFile("AppendToExistingHDF5.h5");
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

            e.Background[dev1] = new Epoch.EpochBackground(new Measurement(0, "V"), srate);
            e.Background[dev0] = new Epoch.EpochBackground(new Measurement(0, "V"), srate);

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

            var streamNameMap = new Dictionary<string, string>();
            streamNameMap["Out0"] = "In0";
            if (nChannels > 1)
                streamNameMap["Out1"] = "In1";

            Controller controller = new Parser().ParseConfiguration(Resources.LowGainConfig);

            var daq = (SimulationDAQController)controller.DAQController;
            daq.Clock = daq;
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
                                                                                                        DateTimeOffset.Now)
                                                                                                        .DataWithNodeConfiguration("SimulationController",daq.Configuration);

                                                                             input[inStream] = inData;
                                                                         }
                                                );

                                            return input;
                                        };


            var protocolParams = new Dictionary<string, object>(1);
            protocolParams["key1"] = "value1";

            e = new Epoch("LowGainSimulation", protocolParams);
            dev0 = controller.GetDevice("Device0");
            dev1 = controller.GetDevice("Device1");

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

            e.Background[dev1] = new Epoch.EpochBackground(new Measurement(0, "V"), srate);
            e.Background[dev0] = new Epoch.EpochBackground(new Measurement(0, "V"), srate);

            return controller;
        }
    }
}
