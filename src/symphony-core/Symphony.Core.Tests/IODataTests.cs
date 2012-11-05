using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Moq;

namespace Symphony.Core
{
    using NUnit.Framework;

    [TestFixture]
    class IODataTests
    {
        IList<IMeasurement> Data { get; set; }
        private static IMeasurement UNUSED_SRATE = new Measurement(1000, "Hz");

        [SetUp]
        public void SetUp()
        {
            this.Data = new List<IMeasurement>();
            this.Data.Add(new Measurement(0, "V"));
            this.Data.Add(new Measurement(1, "V"));
        }

        class NamedDevice : IExternalDevice
        {
            public IClock Clock
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public IDictionary<string, IDAQStream> Streams
            {
                get { throw new NotImplementedException(); }
            }

            public IMeasurement Background
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            /// <summary>
            /// The current Background, in output units.
            /// </summary>
            public IMeasurement OutputBackground
            {
                get { throw new NotImplementedException(); }
            }

            public string Name
            {
                get { return "NamedDevice"; }
            }

            public string Manufacturer
            {
                get { throw new NotImplementedException(); }
            }

            public Controller Controller
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public IDictionary<string, object> Configuration
            {
                get { throw new NotImplementedException(); }
            }

            public ExternalDeviceBase BindStream(IDAQInputStream inputStream)
            {
                throw new NotImplementedException();
            }

            public ExternalDeviceBase BindStream(string name, IDAQInputStream inputStream)
            {
                throw new NotImplementedException();
            }

            public ExternalDeviceBase BindStream(IDAQOutputStream outputStream)
            {
                throw new NotImplementedException();
            }

            public ExternalDeviceBase BindStream(string name, IDAQOutputStream outputStream)
            {
                throw new NotImplementedException();
            }

            public void UnbindStream(string name)
            {
                throw new NotImplementedException();
            }

            public IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration)
            {
                throw new NotImplementedException();
            }

            public void PushInputData(IDAQInputStream stream, IInputData inData)
            {
                throw new NotImplementedException();
            }

            public void DidOutputData(IDAQOutputStream stream, DateTimeOffset outputTime, TimeSpan duration, IEnumerable<IPipelineNodeConfiguration> configuration)
            {
                throw new NotImplementedException();
            }

            public Maybe<string> Validate()
            {
                throw new NotImplementedException();
            }
        }

        private readonly IExternalDevice devFake = new NamedDevice();
        private readonly IDAQStream streamFake = new DAQOutputStream("StreamFake");

        [Test]
        public void OutputData_DataWithExternalDeviceConfigFailsWithNonNullExistingConfig()
        {
            IOutputData data = new OutputData(this.Data, UNUSED_SRATE, false).
                DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>());

            Assert.Throws<ExistingConfigurationException>(
                () => data.DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>()));
        }

        [Test]
        public void InputData_DataWithExternalDeviceConfigFailsWithNonNullExistingConfig()
        {
            IInputData data = new InputData(this.Data, UNUSED_SRATE, DateTimeOffset.Now)
                .DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>());

            Assert.Throws<ExistingConfigurationException>(
                () => data.DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>()));
        }

        [Test]
        public void OutputData_DataWithStreamConfigFailsWithNonNullExistingConfig()
        {
            IOutputData data = new OutputData(this.Data, UNUSED_SRATE, false)
                .DataWithStreamConfiguration(streamFake, new Dictionary<string, object>());

            Assert.Throws<ExistingConfigurationException>(
                () => data.DataWithStreamConfiguration(streamFake, new Dictionary<string, object>()));
        }

        [Test]
        public void InputData_DataWithStreamConfigFailsWithNonNullExistingConfig()
        {
            IInputData data = new InputData(this.Data, UNUSED_SRATE, DateTimeOffset.Now)
                .DataWithStreamConfiguration(streamFake, new Dictionary<string, object>());
            Assert.That(() => data.DataWithStreamConfiguration(streamFake, new Dictionary<string, object>()),
                        Throws.TypeOf<ExistingConfigurationException>());

        }

        [Test]
        public void OutputDataCreation()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");

            IOutputData outData = new OutputData(this.Data,
                srate, false);

            Assert.IsNotNull(outData);
            Assert.AreEqual(this.Data, outData.Data);
            Assert.IsNull(outData.NodeConfigurationWithName(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME));
            Assert.IsNull(outData.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME));
            Assert.AreEqual(srate, outData.SampleRate);
            Assert.False(outData.IsLast);
        }

        [Test]
        public void OutputTimeThrowsUntilSet()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");

            IOutputData outData = new OutputData(this.Data,
                srate, false);

            Assert.Throws<InvalidOperationException>(() => outData.OutputTime = outData.OutputTime);

            DateTimeOffset expectedTime = DateTimeOffset.UtcNow;
            outData.OutputTime = expectedTime;

            Assert.AreEqual(expectedTime, outData.OutputTime);


        }

        [Test]
        public void ConcatThrowsWithEitherExternalDeviceConfiguration()
        {
            IMeasurement srate = new Measurement(1000, "Hz");

            IOutputData outData1 = new OutputData(this.Data,
                srate, false);

            IOutputData outData2 = new OutputData(this.Data,
                srate, false);

            Assert.DoesNotThrow(() => outData1.Concat(outData2));

            var dev = new UnitConvertingExternalDevice("DevName", "DevManufacturer", new Measurement(0, "V"));

            var outData3 = outData1.DataWithExternalDeviceConfiguration(dev, new Dictionary<string, object>());

            Assert.Throws<ArgumentException>(() => outData3.Concat(outData2));
            Assert.Throws<ArgumentException>(() => outData2.Concat(outData3));

        }

        [Test]
        public void ConcatThrowsWithEitherStreamConfiguration()
        {
            IMeasurement srate = new Measurement(1000, "Hz");

            IOutputData outData1 = new OutputData(this.Data,
                srate, false);

            IOutputData outData2 = new OutputData(this.Data,
                srate, false);

            Assert.DoesNotThrow(() => outData1.Concat(outData2));

            var outData3 = outData1.DataWithStreamConfiguration(streamFake, new Dictionary<string, object>());

            Assert.Throws<ArgumentException>(() => outData3.Concat(outData2));
            Assert.Throws<ArgumentException>(() => outData2.Concat(outData3));

        }

        [Test]
        public void ConcatThrowsForSampleRateMismatch()
        {
            var srate1 = new Measurement(1000, "Hz");
            var srate2 = new Measurement(100, "Hz");

            IOutputData outData1 = new OutputData(this.Data,
                srate1, false);

            IOutputData outData2 = new OutputData(this.Data,
                srate2, false);

            Assert.Throws<ArgumentException>(() => outData1.Concat(outData2));
            Assert.Throws<ArgumentException>(() => outData2.Concat(outData1));
        }

        [Test]
        public void ConcatSetsIsLast()
        {
            IMeasurement srate = new Measurement(1000, "Hz");

            IOutputData outData1 = new OutputData(this.Data,
                srate, false);

            IOutputData outData2 = new OutputData(this.Data,
                srate, true);

            Assert.That(outData1.IsLast, Is.Not.EqualTo(outData2.IsLast));
            Assert.That(outData1.Concat(outData1).IsLast, Is.EqualTo(outData1.IsLast));
            Assert.That(outData2.Concat(outData2).IsLast, Is.EqualTo(outData2.IsLast));
            Assert.That(outData1.Concat(outData2).IsLast, Is.True);
            Assert.That(outData2.Concat(outData1).IsLast, Is.True);

        }

        [Test]
        public void ConcatenatesOutputData()
        {
            IMeasurement srate = new Measurement(1000, "Hz");

            IOutputData outData1 = new OutputData(this.Data,
                srate, false);

            IOutputData outData2 = new OutputData(this.Data.Concat(this.Data).ToList(),
                srate, true);

            var actual = outData1.Concat(outData2);

            var expectedData = this.Data.Concat(this.Data).Concat(this.Data).ToList();
            Assert.That(actual.Data, Is.EqualTo(expectedData));
            Assert.That(actual.SampleRate, Is.EqualTo(srate));

        }

        [Test]
        public void OutputDataChainsConfiguration()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");

            IOutputData data1 = new OutputData(this.Data,
                srate, false);

            IOutputData data2 = new OutputData(data1, data1.Data);

            Assert.IsNotNull(data2);
            Assert.AreEqual(data1.Data, data2.Data);
            Assert.AreEqual(data1.SampleRate, data2.SampleRate);
        }

        [Test]
        public void OutputDataChainsIsLast()
        {
            IMeasurement srate = new Measurement(1000, "Hz");
            IOutputData data1 = new OutputData(this.Data,
                srate, false);

            Assert.AreEqual(data1.IsLast, new OutputData(data1, data1.Data).IsLast);

            IOutputData data2 = new OutputData(this.Data,
                srate, true);

            Assert.AreEqual(data2.IsLast, new OutputData(data2, data2.Data).IsLast);
        }

        [Test]
        public void InputDataCreation()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");

            DateTimeOffset expectedTime = DateTimeOffset.UtcNow;

            var nodeName = "test-node";

            IInputData inData = new InputData(this.Data,
                new Measurement(1000, "Hz"),
                expectedTime,
                new PipelineNodeConfiguration(nodeName, config));

            Assert.IsNotNull(inData);
            Assert.AreEqual(this.Data, inData.Data);
            Assert.IsNull(inData.NodeConfigurationWithName(nodeName+"x"));
            Assert.That(inData.NodeConfigurationWithName(nodeName).Configuration, Is.EqualTo(config));
            Assert.AreEqual(srate, inData.SampleRate);
            Assert.AreEqual(expectedTime, inData.InputTime);
        }

        [Test]
        public void InputDataConcats()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");

            DateTimeOffset expectedTime = DateTimeOffset.UtcNow;

            IInputData inData1 = new InputData(this.Data,
                new Measurement(1000, "Hz"),
                expectedTime,
                new PipelineNodeConfiguration("test",config));

            IInputData inData2 = new InputData(this.Data,
                new Measurement(1000, "Hz"),
                expectedTime.AddMilliseconds(2),
                new PipelineNodeConfiguration("test", config));

            var concatData = new List<IMeasurement>(this.Data).Concat(this.Data).ToList();
            Assert.AreEqual(2 * this.Data.Count, concatData.Count());

            IInputData expected = new InputData(concatData,
                new Measurement(1000, "Hz"),
                expectedTime,
                new PipelineNodeConfiguration("test", config));

            var actual = inData1.Data.Concat(inData2.Data);
            Assert.AreEqual(expected.Data, actual);
            Assert.AreEqual(expected.SampleRate, inData1.SampleRate);
        }



        [Test]
        public void OutputDataSplitDataReturnsEntireDataForOverDuration()
        {
            IDictionary<string, object> config = new Dictionary<string, object>();
            var srate = new Measurement(1000, "Hz");

            IOutputData outData = new OutputData(this.Data,
                srate, false);

            var duration = new TimeSpan(1, 0, 0); //1 hr
            var result = outData.SplitData(duration);

            Assert.AreEqual(outData.Data, result.Head.Data);
            Assert.AreEqual(0, result.Rest.Duration.Ticks);
        }

        [Test]
        public void InputDataSplitDataReturnsEntireDataForOverDuration()
        {
            IDictionary<string, object> config = new Dictionary<string, object>();
            var srate = new Measurement(1000, "Hz");

            IInputData inData = new InputData(this.Data, srate, DateTimeOffset.Now).DataWithStreamConfiguration(streamFake, new Dictionary<string, object>());

            var duration = new TimeSpan(1, 0, 0); //1 hr
            var result = inData.SplitData(duration);

            Assert.AreEqual(inData.Data, result.Head.Data);
            Assert.AreEqual(0, result.Rest.Duration.TotalSeconds);
        }


        //[Test]
        //public void SplitDataReturnsEmptyDataForZeroDuration()
        //{
        //    IDictionary<string, object> config = new Dictionary<string, object>();
        //    Measurement srate = new Measurement(1000, "Hz");

        //    var samples = Enumerable.Range(0, 1000).Select(i => new Measurement(i, "V")).ToList();

        //    IOutputData outData = new OutputData(samples,
        //        srate);

        //    var duration = new TimeSpan(0); //1 hr
        //    var result = outData.SplitData(duration);

        //    Assert.AreEqual(outData.Data, result.Rest.Data);
        //    Assert.AreEqual(0, result.Head.Duration.TotalSeconds);
        //}

        [Test]
        public void OutputDataSplitsDataAtDuration(
            [Values(0.01, 0.5, 0.751)] double splitDuration
            )
        {
            IDictionary<string, object> config = new Dictionary<string, object>();
            const int nSamples = 1000;
            var srate = new Measurement(nSamples, "Hz");

            var data = Enumerable.Range(0, nSamples).Select(i => new Measurement(i, "V")).ToList();

            IOutputData outData = new OutputData(data,
                srate, false);

            var duration = new TimeSpan((long)Math.Ceiling(splitDuration * TimeSpan.TicksPerSecond));
            var result = outData.SplitData(duration);

            //rounds samples up for duration
            int numSamples = (int)Math.Ceiling(duration.TotalSeconds * (double)outData.SampleRate.QuantityInBaseUnit);

            IEnumerable firstData = Enumerable.Range(0, numSamples).Select(i => outData.Data[i]).ToList();
            IEnumerable restData = Enumerable.Range(numSamples, outData.Data.Count - numSamples).Select(i => outData.Data[i]).ToList();


            Assert.AreEqual(firstData, result.Head.Data);
            Assert.AreEqual(restData, result.Rest.Data);

        }

        [Test]
        public void InputDataSplitsDataAtDuration(
            [Values(0.01, 0.5, 0.751)] double splitDuration
            )
        {
            IDictionary<string, object> config = new Dictionary<string, object>();
            const int nSamples = 1000;
            var srate = new Measurement(nSamples, "Hz");

            var data = Enumerable.Range(0, nSamples).Select(i => new Measurement(i, "V")).ToList();

            IInputData inData = new InputData(data, srate, DateTimeOffset.Now);

            var duration = new TimeSpan((long)Math.Ceiling(splitDuration * TimeSpan.TicksPerSecond));
            var result = inData.SplitData(duration);

            //rounds samples up for duration
            int numSamples = (int)Math.Ceiling(duration.TotalSeconds * (double)inData.SampleRate.QuantityInBaseUnit);

            IEnumerable firstData = Enumerable.Range(0, numSamples).Select(i => inData.Data[i]).ToList();
            IEnumerable restData = Enumerable.Range(numSamples, inData.Data.Count - numSamples).Select(i => inData.Data[i]).ToList();


            Assert.AreEqual(firstData, result.Head.Data);
            Assert.AreEqual(restData, result.Rest.Data);

        }

        [Test]
        public void OutputData_SetsExternalDeviceConfig()
        {
            var config = new Dictionary<string, object>();

            IOutputData data = new OutputData(Data, UNUSED_SRATE, false);

            data = data.DataWithExternalDeviceConfiguration(devFake, config);

            Assert.That(data.NodeConfigurationWithName(devFake.Name).Configuration,
                Is.EqualTo(config));
        }

        [Test]
        public void OutputData_SetsStreamConfig()
        {
            var config = new Dictionary<string, object>();

            IOutputData data = new OutputData(Data, UNUSED_SRATE, false);

            data = data.DataWithStreamConfiguration(streamFake, config);

            Assert.That(data.NodeConfigurationWithName(streamFake.Name).Configuration,
                Is.EqualTo(config));
        }

        [Test]
        public void InputData_SetsExternalDeviceConfig()
        {
            var config = new Dictionary<string, object>();
            config["key"] = "value";

            IInputData data = new InputData(Data, UNUSED_SRATE, DateTimeOffset.Now);

            data = data.DataWithExternalDeviceConfiguration(devFake, config);

            Assert.That(data.NodeConfigurationWithName(devFake.Name).Configuration, Is.EqualTo(config));
        }

        [Test]
        public void InputDataConversion()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");


            IInputData data = new InputData(this.Data,
                srate,
                DateTimeOffset.Now,
                new PipelineNodeConfiguration(IOData.STREAM_CONFIGURATION_NAME, new Dictionary<string, object>()))
                .DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>());


            Converters.Clear();
            ConvertProc fooConversion = (m) => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo");
            Converters.Register("V", "foo", fooConversion);

            IInputData expected = new InputData(data,
                data.Data.Select(m => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo")).ToList());

            Assert.NotNull(expected.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME));
            IInputData actual = data.DataWithUnits("foo");

            Assert.AreEqual(expected.Data, actual.Data);
            Assert.That(actual.NodeConfigurationWithName(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME),
                Is.EqualTo(expected.NodeConfigurationWithName(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME)));
            Assert.That(actual.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME),
                Is.EqualTo(expected.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME)));
            Assert.AreEqual(expected.InputTime, actual.InputTime);
        }

        [Test]
        public void OutputDataConversion()
        {

            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");


            IOutputData outData = new OutputData(this.Data,
                srate, false)
                .DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>())
                .DataWithStreamConfiguration(streamFake, new Dictionary<string, object>());

            outData.OutputTime = DateTimeOffset.Now;

            Converters.Clear();
            ConvertProc fooConversion = (m) => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo");
            Converters.Register("V", "foo", fooConversion);

            IOutputData expected = new OutputData(outData,
                outData.Data.Select(m => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo")).ToList());
            expected.OutputTime = outData.OutputTime;

            Assert.NotNull(expected.NodeConfigurationWithName(devFake.Name));
            Assert.NotNull(expected.NodeConfigurationWithName(streamFake.Name));
            IOutputData actual = outData.DataWithUnits("foo");

            Assert.AreEqual(expected.Data, actual.Data);
            Assert.AreEqual(expected.NodeConfigurationWithName(devFake.Name), 
                actual.NodeConfigurationWithName(devFake.Name));
            Assert.AreEqual(expected.NodeConfigurationWithName(streamFake.Name), 
                actual.NodeConfigurationWithName(streamFake.Name));
            Assert.AreEqual(expected.OutputTime, actual.OutputTime);

        }

        [Test]
        public void InputDataConversionViaProc()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            IMeasurement srate = new Measurement(1000, "Hz");


            IOutputData outData = new OutputData(this.Data,
                srate, false)
                .DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>())
                .DataWithStreamConfiguration(streamFake, new Dictionary<string, object>());

            outData.OutputTime = DateTimeOffset.Now;

            Converters.Clear();
            ConvertProc fooConversion = (m) => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo");
            Converters.Register("V", "foo", fooConversion);

            IOutputData expected = new OutputData(outData,
                outData.Data.Select(m => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo")).ToList()) { OutputTime = outData.OutputTime };


            IOutputData actual = outData.DataWithConversion((m) => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo"));

            Assert.AreEqual(expected.Data, actual.Data);
            Assert.AreEqual(expected.NodeConfigurationWithName(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME),
                actual.NodeConfigurationWithName(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME));
            Assert.AreEqual(expected.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME),
                actual.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME));
            Assert.AreEqual(expected.OutputTime, actual.OutputTime);
        }

        [Test]
        public void OutputDataConversionViaProc()
        {
            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            var srate = new Measurement(1000, "Hz");


            var outData = new OutputData(this.Data,
                srate, false)
                .DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>())
                .DataWithStreamConfiguration(streamFake, new Dictionary<string, object>());

            outData.OutputTime = DateTimeOffset.Now;


            var expected = new OutputData(outData,
                outData.Data.Select(m => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo")).ToList()) { OutputTime = outData.OutputTime };


            var actual = outData.DataWithConversion((m) => new Measurement(m.QuantityInBaseUnit * 10, 1, "foo"));

            Assert.AreEqual(expected.Data, actual.Data);
            Assert.AreEqual(expected.NodeConfigurationWithName(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME),
                actual.NodeConfigurationWithName(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME));
            Assert.AreEqual(expected.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME),
                actual.NodeConfigurationWithName(IOData.STREAM_CONFIGURATION_NAME));
            Assert.AreEqual(expected.OutputTime, actual.OutputTime);
        }

        [Test]
        public void HasOutputTime()
        {
            IOutputData data = new OutputData(Data, UNUSED_SRATE, false);

            Assert.False(data.HasOutputTime);

            data.OutputTime = DateTimeOffset.Now;

            Assert.True(data.HasOutputTime);
        }

        [Test]
        public void TestInputDataHasNodeConfiguration()
        {
            IInputData inData = new InputData(Data, UNUSED_SRATE, DateTimeOffset.Now);
            Assert.False(inData.HasNodeConfiguration(IOData.EXTERNAL_DEVICE_CONFIGURATION_NAME));

            inData = inData.DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>());

            Assert.True(inData.HasNodeConfiguration(devFake.Name));
        }

        [Test]
        public void TestOutputDataHasNodeConfiguration()
        {
            IOutputData outData = new OutputData(Data, UNUSED_SRATE, false);
            Assert.False(outData.HasNodeConfiguration(devFake.Name));

            outData = outData.DataWithExternalDeviceConfiguration(devFake, new Dictionary<string, object>());

            Assert.True(outData.HasNodeConfiguration(devFake.Name));
        }


        //[Test]
        //public void OutputDataEquality()
        //{
        //    IDictionary<string, object> config = new Dictionary<string, object>(2);
        //    config["param1"] = 10;
        //    config["param2"] = 1;

        //    Measurement srate = new Measurement(1000, "Hz");

        //    IOutputData d1 = new OutputData(this.Data,
        //        srate,
        //        config,
        //        this);

        //    IOutputData d2 = new OutputData(this.Data,
        //        srate,
        //        config,
        //        this);

        //    Assert.AreEqual(d1, d2);

        //    var otherData = this.Data.Select((v) => v).ToList();

        //    IOutputData d3 = new OutputData(otherData,
        //        new Measurement(1000, "Hz"),
        //        config,
        //        this);

        //    Assert.AreEqual(d1, d3);

        //    IOutputData d4 = new OutputData(d3, this.Data);

        //    Assert.AreNotEqual(d1, d4);

        //    IOutputData d5 = new OutputData(this.Data,
        //        new Measurement(1, "Hz"),
        //        config,
        //        this);

        //    Assert.AreNotEqual(d1,d5);

        //    IDictionary<string, object> config2 = new Dictionary<string, object>(2);
        //    config2["param1"] = 10;
        //    config2["param2"] = 0;

        //    IOutputData d6 = new OutputData(this.Data,
        //        srate,
        //        config2,
        //        this);

        //    Assert.AreNotEqual(d1, d6);

        //    IOutputData d7 = new OutputData(this.Data,
        //        srate,
        //        config,
        //        d6);

        //    Assert.AreNotEqual(d1, d7);

        //    d1.OutputTime = DateTimeOffset.Now;

        //    Assert.AreNotEqual(d1,d2);

        //    d2.OutputTime = DateTimeOffset.Now.AddMilliseconds(100);

        //    Assert.AreNotEqual(d1, d2);

        //    d2.OutputTime = d1.OutputTime;

        //    Assert.AreEqual(d1, d2);
        //}
    }
}
