using System.Collections.Generic;
using System.Linq;

namespace Symphony.Core
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    class EpochTests
    {
        [Test]
        public void InstantiatesDictionaries()
        {
            Epoch e = new Epoch("Protocol", new Dictionary<string, object>());

            Assert.IsNotNull(e.ProtocolParameters);
            Assert.IsNotNull(e.Stimuli);
            Assert.IsNotNull(e.Responses);
            Assert.IsNotNull(e.ProtocolParameters);
            Assert.IsNotNull(e.Backgrounds);
        }

        [Test]
        public void SimpleCreation()
        {
            Epoch epoch = new Epoch("Protocol", new Dictionary<string, object>());
            Assert.IsNotNull(epoch);
        }

        [Test]
        public void ConvenienceConstructor()
        {
            Epoch e = new Epoch("Protocol");

            Assert.IsNotNull(e.ProtocolParameters);
            Assert.IsNotNull(e.Stimuli);
            Assert.IsNotNull(e.Responses);
            Assert.IsNotNull(e.ProtocolParameters);
            Assert.IsNotNull(e.Backgrounds);
            Assert.IsFalse(e.WaitForTrigger);
        }

        [Test]
        public void SetsProtocolID()
        {
            string expected = "ted rocks";
            Epoch e = new Epoch(expected, new Dictionary<string, object>());

            Assert.AreEqual(expected, e.ProtocolID);
        }

        [Test]
        public void DurationsSumUpCorrectly()
        {
            Epoch e = new Epoch("Protocol");

            var ed = new UnitConvertingExternalDevice("TEST-DEVICE", "DEVICE-CO", new Measurement(0, "V"));

            IMeasurement UNUSED_SRATE = new Measurement(1000, "Hz");

            e.Stimuli[ed] = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) new Dictionary<string, object>(),
                 (IOutputData) new OutputData(new Measurement[] { 
                                                                    new Measurement(0, "units"), 
                                                                    new Measurement(1, "units") 
                                                                }.ToList(), UNUSED_SRATE, false));

            // More to do here (later)
        }

        [SetUp]
        public void TestEpochSetup()
        {
            const string protocolID = "Epoch.Fixture";
            var parameters = new Dictionary<string, object>();
            parameters[param1] = value1;
            parameters[param2] = value2;

            dev1 = new UnitConvertingExternalDevice(dev1Name, "DEVICECO", new Measurement(0, "V"));
            dev2 = new UnitConvertingExternalDevice(dev2Name, "DEVICECO", new Measurement(0, "V"));

            var stream1 = new DAQInputStream("Stream1");
            var stream2 = new DAQInputStream("Stream2");

            var stimParameters = new Dictionary<string, object>();
            stimParameters[param1] = value1;
            stimParameters[param2] = value2;

            var srate = new Measurement(1000, "Hz");

            var samples = Enumerable.Range(0, 1000).Select(i => new Measurement((decimal) Math.Sin((double)i / 100), "V")).ToList();
            var stimData = new OutputData(samples, srate, false);

            RenderedStimulus stim1 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted
            RenderedStimulus stim2 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted

            Epoch e = new Epoch(protocolID, parameters);
            e.Stimuli[dev1] = stim1;
            e.Stimuli[dev2] = stim2;

            var start = DateTimeOffset.Parse("1/11/2011 6:03:29 PM -08:00");

            e.Backgrounds[dev1] = new Background(new Measurement(0, "V"), new Measurement(1000, "Hz"));
            e.Backgrounds[dev2] = new Background(new Measurement(1, "V"), new Measurement(1000, "Hz"));

            e.Responses[dev1] = new Response();
            e.Responses[dev2] = new Response();

            var streamConfig = new Dictionary<string, object>();
            streamConfig[param1] = value1;

            var devConfig = new Dictionary<string, object>();
            devConfig[param2] = value2;

            var responseData1 = new InputData(samples, srate, start)
                .DataWithStreamConfiguration(stream1, streamConfig)
                .DataWithExternalDeviceConfiguration(dev1, devConfig);
            var responseData2 = new InputData(samples, srate, start)
                .DataWithStreamConfiguration(stream2, streamConfig)
                .DataWithExternalDeviceConfiguration(dev2, devConfig);

            e.Responses[dev1].AppendData(responseData1);
            e.Responses[dev2].AppendData(responseData2);

            e.Keywords.Add(kw1);
            e.Keywords.Add(kw2);

            testEpoch = e;
        }

        Epoch testEpoch;
        ExternalDeviceBase dev1;
        ExternalDeviceBase dev2;
        const string dev1Name = "Device1";
        const string dev2Name = "Device2";
        const string param1 = "key1";
        const int value1 = 1;
        const string param2 = "key2";
        const string value2 = "value2";
        private const string kw1 = "kw1";
        private const string kw2 = "kw2";

        [TearDown]
        public void BlowAwayTestEpoch()
        {
            testEpoch = null;
            dev1 = dev2 = null;
        }

        [Test]
        public void MaybeHasStartTime()
        {
            Epoch e = new Epoch("");

            Assert.False(e.StartTime);

            var dev = new UnitConvertingExternalDevice("name", "co", new Measurement(1.0m, "units"));

            var stim = new DelegatedStimulus("stimID", "units", new Measurement(1000, "Hz"),
                                             new Dictionary<string, object>(),
                                             (p, b) => null,
                                             (p) => Option<TimeSpan>.None());

            e.Stimuli[dev] = stim;

            var expected = DateTimeOffset.Now;
            
            stim.DidOutputData(expected, TimeSpan.FromSeconds(1), null);

            Assert.AreEqual(expected, (DateTimeOffset)e.StartTime);
        }

        [Test]
        public void IsIndefiniteIfAnyStimulusIsIndefinite()
        {
            Epoch e = new Epoch("");
            var dev = new UnitConvertingExternalDevice("name", "co", new Measurement(1.0m, "units"));
            var dev2 = new UnitConvertingExternalDevice("name2", "co", new Measurement(1.0m, "units"));

            Assert.That(e.IsIndefinite, Is.False);

            e.Stimuli[dev2] = new DelegatedStimulus("stimID", "units", new Measurement(1000, "Hz"), 
                                                   new Dictionary<string, object>(),
                                                   (p, b) => null,
                                                   (p) => Option<TimeSpan>.Some(TimeSpan.FromMilliseconds(100)));

            Assert.That(e.IsIndefinite, Is.False);

            e.Stimuli[dev] = new DelegatedStimulus("stimID", "units", new Measurement(1000, "Hz"),
                                                   new Dictionary<string, object>(),
                                                   (p, b) => null,
                                                   (p) => Option<TimeSpan>.None());

            Assert.That(e.IsIndefinite, Is.True);

            e.Stimuli.Clear();

            Assert.That(e.IsIndefinite, Is.False);
        }

        private const string UNUSED_PROTOCOL_ID = "UNUSED";
        
        [Test]
        public void ShouldAddKeywords()
        {
            var e = new Epoch(UNUSED_PROTOCOL_ID);
            var kw1 = "test";
            var kw2 = "other";

            Assert.That(e.Keywords, Is.Empty);
            e.Keywords.Add(kw1);
            e.Keywords.Add(kw2);

            Assert.That(e.Keywords, Contains.Item(kw1));
            Assert.That(e.Keywords, Contains.Item(kw2));
        }


        [Test]
        public void ShouldUniqueKeywords()
        {
            var e = new Epoch(UNUSED_PROTOCOL_ID);
            var kw1 = "test";

            e.Keywords.Add(kw1);
            e.Keywords.Add(kw1);

            Assert.That(e.Keywords, Contains.Item(kw1));
            Assert.That(e.Keywords.Count(), Is.EqualTo(1));
        }
    }
}
