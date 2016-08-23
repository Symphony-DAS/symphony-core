using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NationalInstruments.DAQmx;
using Symphony.Core;

namespace NI
{
    [TestFixture]
    class NIDAQStreamTests
    {
        [Test]
        public void DAQCountUnitsConsistent()
        {
            Assert.AreEqual(NIDAQInputStream.DAQUnits, NIDAQOutputStream.DAQUnits);
        }
    }
    [TestFixture]
    class NIDAQOutputStreamTests
    {
        [Test]
        public void DelegatesSampleRateToController()
        {
            var c = new NIDAQController();
            var s = new NIDAQOutputStream("none", 0, c);
            var srate = new Measurement(1000, "Hz");
            c.SampleRate = srate;

            Assert.That(s.SampleRate, Is.EqualTo(c.SampleRate));
            Assert.Throws<NotSupportedException>(() => s.SampleRate = srate);
        }
    }

    [TestFixture]
    class NIDigitalDAQOutputStreamTests
    {
        [Test]
        public void ShouldBitShiftAndMergeBackground()
        {
            var controller = new NIDAQController();
            var s = new NIDigitalDAQOutputStream("OUT", controller);
            controller.SampleRate = new Measurement(10000, 1, "Hz");

            for (ushort bitPosition = 1; bitPosition < 32; bitPosition += 2)
            {
                TestDevice dev = new TestDevice { Background = new Measurement(1, Measurement.UNITLESS) };
                dev.BindStream(s);
                s.BitPositions[dev] = bitPosition;
            }

            ushort q = 0xaaaa;
            var expected = new Measurement((short)q, Measurement.UNITLESS);

            Assert.AreEqual(expected, s.Background);
        }

        [Test]
        public void ShouldBitShiftAndMergePulledOutputData()
        {
            var controller = new NIDAQController();
            var s = new NIDigitalDAQOutputStream("OUT", controller);
            controller.SampleRate = new Measurement(10000, 1, "Hz");

            TimeSpan duration = TimeSpan.FromSeconds(0.5);

            for (ushort bitPosition = 1; bitPosition < 16; bitPosition += 2)
            {
                var dataQueue = new Dictionary<IDAQOutputStream, Queue<IOutputData>>();

                dataQueue[s] = new Queue<IOutputData>();
                var data = new OutputData(Enumerable.Range(0, 10000).Select(i => new Measurement(i % 2, Measurement.UNITLESS)).ToList(),
                    s.SampleRate, false);

                dataQueue[s].Enqueue(data.SplitData(duration).Head);
                dataQueue[s].Enqueue(data.SplitData(duration).Head);

                TestDevice dev = new TestDevice("OUT-DEVICE" + bitPosition, dataQueue);
                dev.BindStream(s);
                s.BitPositions[dev] = bitPosition;
            }

            var expected = Enumerable.Range(0, 10000).Select(i => new Measurement((short)(i % 2 * 0xaaaa), Measurement.UNITLESS)).ToList();

            var pull1 = s.PullOutputData(duration);
            Assert.AreEqual(expected, pull1.Data);

            var pull2 = s.PullOutputData(duration);
            Assert.AreEqual(expected, pull2.Data);
        }
    }

    [TestFixture]
    class NIDAQInputStreamTests
    {
        [Test]
        public void DelegatesSampleRateToController()
        {
            var c = new NIDAQController();
            var s = new NIDAQInputStream("none", 0, c);
            var srate = new Measurement(1000, "Hz");
            c.SampleRate = srate;

            Assert.That(s.SampleRate, Is.EqualTo(c.SampleRate));
            Assert.Throws<NotSupportedException>(() => s.SampleRate = srate);
        }
    }

    [TestFixture]
    class NIDigitalDAQInputStreamTests
    {
        [Test]
        public void ShouldBitShiftAndMaskPushedInputData()
        {
            Converters.Clear();
            NIDAQOutputStream.RegisterConverters();

            var controller = new NIDAQController();
            var s = new NIDigitalDAQInputStream("IN", controller);
            controller.SampleRate = new Measurement(10000, 1, "Hz");

            TimeSpan duration = TimeSpan.FromSeconds(0.5);

            var devices = new List<TestDevice>();

            for (ushort bitPosition = 1; bitPosition < 32; bitPosition += 2)
            {
                TestDevice dev = new TestDevice();
                dev.BindStream(s);
                s.BitPositions[dev] = bitPosition;

                devices.Add(dev);
            }

            var data = new InputData(Enumerable.Range(0, 10000).Select(i => new Measurement((short)(i % 2 * 0xaaaa), Measurement.UNITLESS)).ToList(),
                s.SampleRate, DateTime.Now);

            s.PushInputData(data);
            s.PushInputData(data);

            var expected = Enumerable.Range(0, 10000).Select(i => new Measurement(i % 2, Measurement.UNITLESS)).ToList();
            foreach (var ed in devices)
            {
                Assert.AreEqual(expected, ed.InputData[s].ElementAt(0).Data);
                Assert.AreEqual(expected, ed.InputData[s].ElementAt(1).Data);
            }
        }
    }
}
