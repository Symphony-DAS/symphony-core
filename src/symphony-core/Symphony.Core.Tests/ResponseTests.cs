using System;
using System.Collections.Generic;
using System.Linq;

namespace Symphony.Core
{
    using NUnit.Framework;


    [TestFixture]
    class ResponseTests
    {
        [Test]
        public void ShouldAppendData()
        {
            Response r = new Response();

            IInputData d1;
            IInputData d2;
            OrderedFakeInputData(out d1, out d2);

            r.AppendData(d1);
            r.AppendData(d2);

            Assert.AreEqual(d1, r.DataSegments[0]);
            Assert.AreEqual(d2, r.DataSegments[1]);

        }

        private static void OrderedFakeInputData(out IInputData d1, out IInputData d2)
        {
            Random random = new Random();
            IList<IMeasurement> data = (IList<IMeasurement>) Enumerable.Repeat(0, 100).Select(i => random.Next()).Select(v => new Measurement(v, "V") as IMeasurement).ToList(); ;//generate random data
            IMeasurement srate = new Measurement(1000, "Hz");
            DateTimeOffset time1 = DateTimeOffset.Now;
            IDictionary<string, object> config = new Dictionary<string, object>();
            var dev = new UnitConvertingExternalDevice("DevName", "DevManufacturer", new Measurement(0, "V"));
            var stream = new DAQOutputStream("StreamName");

            d1 = new InputData(data, srate, time1)
                .DataWithExternalDeviceConfiguration(dev, config)
                .DataWithStreamConfiguration(stream, config);

            DateTimeOffset time2 = time1.AddSeconds((double) (data.Count / srate.Quantity));
            d2 = new InputData(data, srate, time2)
                .DataWithExternalDeviceConfiguration(dev, config)
                .DataWithStreamConfiguration(stream, config);
        }

        [Test]
        public void OrdersInput()
        {
            Response r = new Response();

            IInputData d1;
            IInputData d2;
            OrderedFakeInputData(out d1, out d2);

            r.AppendData(d2);
            r.AppendData(d1);

            Assert.AreEqual(d1, r.DataSegments[0]);
            Assert.AreEqual(d2, r.DataSegments[1]);
        }

        [Test]
        public void CoalecesInput()
        {
            Response r = new Response();

            IInputData d1;
            IInputData d2;
            OrderedFakeInputData(out d1, out d2);

            r.AppendData(d1);
            r.AppendData(d2);

            var expected = d1.Data.Concat(d2.Data);

            Assert.NotNull(r.DataSegments);
            Assert.AreEqual(expected, r.Data);
            Assert.AreEqual(d1.SampleRate, r.SampleRate);
            Assert.AreEqual(d1.Duration + d2.Duration, r.Duration);
        }

        [Test]
        public void DataThrowsIfUnitsChange()
        {

        }

        [Test]
        public void DataIsNullForEmptySegments()
        {
            Response r = new Response();

            Assert.That(r.Data, Is.Empty);
        }

        [Test]
        public void ShouldComputeDuration()
        {
            Response r = new Response();

            Random random = new Random();

            IList<IMeasurement> data = (IList<IMeasurement>) Enumerable.Repeat(0, 100)
                .Select(i => random.Next()).Select(v => new Measurement(v, "V") as IMeasurement)
                .ToList();//generate random data

            IMeasurement srate = new Measurement(1000, "Hz");
            DateTimeOffset time1 = DateTimeOffset.Now;
            IDictionary<string, object> config = new Dictionary<string, object>();

            IInputData d1 = new InputData(data, srate, time1);

            DateTimeOffset time2 = time1.AddSeconds((double) (data.Count / srate.Quantity));
            IInputData d2 = new InputData(data, srate, time2);

            r.AppendData(d1);
            r.AppendData(d2);

            TimeSpan expected = new TimeSpan(0, 0, 0, 0, 200);
            Assert.AreEqual(new TimeSpan(r.DataSegments.Select(d => d.Duration.Ticks).Sum()), r.Duration);
        }
    }
}
