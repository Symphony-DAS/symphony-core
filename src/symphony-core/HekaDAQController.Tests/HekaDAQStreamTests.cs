using System;
using System.Collections.Generic;
using System.Linq;
using Heka.NativeInterop;
using Moq;
using NUnit.Framework;
using Symphony.Core;

namespace Heka
{
    [TestFixture]
    class HekaDAQStreamTests
    {
        [Test]
        public void DAQCountUnitsConsistent()
        {
            Assert.AreEqual(HekaDAQInputStream.DAQCountUnits, HekaDAQOutputStream.DAQCountUnits);
        }
    }
    [TestFixture]
    class HekaDAQOutputStreamTests
    {
        [Test]
        public void PreloadShouldFeedBuffer()
        {
            /*
             * We want to keep HardwareBufferTargetDuration available in the hardware buffer.
             */

            Converters.Clear();
            HekaDAQOutputStream.RegisterConverters();

            var itcMock = new Mock<IHekaDevice>();
            StreamType channelType = StreamType.ANALOG_OUT;
            ushort channelNumber = 0;
            var time = DateTimeOffset.Now;

            int availableSamplesStart = 1000000;
            int availableSamples = availableSamplesStart;
            //itcMock.Setup(itc => itc.AvailableSamples(channelType, channelNumber)).Returns(availableSamples);
            itcMock.Setup(itc => itc.PreloadSamples(channelType, channelNumber, It.IsAny<IList<short>>()))
                .Callback<StreamType, ushort, IList<short>>((type, number, sampleList) => availableSamples -= sampleList.Count);


            var controller = new HekaDAQController();
            HekaDAQOutputStream s = new HekaDAQOutputStream("OUT", channelType, channelNumber, controller);
            s.MeasurementConversionTarget = "V";
            controller.SampleRate = new Measurement(10000, 1, "Hz");

            var dataQueue = new Dictionary<IDAQOutputStream, Queue<IOutputData>>();

            dataQueue[s] = new Queue<IOutputData>();
            var bigData = new OutputData(Enumerable.Range(0, 100000).Select(i => new Measurement(i % 5, "V")).ToList(),
                s.SampleRate, false);

            TimeSpan bufferDuration = TimeSpan.FromSeconds(0.45);
            dataQueue[s].Enqueue(bigData.SplitData(bufferDuration).Head);
            dataQueue[s].Enqueue(bigData.SplitData(bufferDuration).Head);

            TestDevice dev = new TestDevice("OUT-DEVICE", dataQueue);
            dev.BindStream(s);
            dev.Controller = new Controller();

            s.Preload(itcMock.Object as IHekaDevice, new OutputData(dataQueue[s].Peek()));

            int expectedSamples = (int)Math.Ceiling(bufferDuration.TotalSeconds * (double)s.SampleRate.QuantityInBaseUnits);
            itcMock.VerifyAll();
            Assert.AreEqual(availableSamplesStart - expectedSamples, availableSamples); //Preload should not affect buffer availablility because PRELOAD_FIFO is used
        }

        [Test]
        public void DelegatesSampleRateToController()
        {
            var c = new HekaDAQController();
            var s = new HekaDAQOutputStream("none", 0, 0, c);
            var srate = new Measurement(1000, "Hz");
            c.SampleRate = srate;

            Assert.That(s.SampleRate, Is.EqualTo(c.SampleRate));
            Assert.Throws<NotSupportedException>(() => s.SampleRate = srate);
        }

        [Test]
        public void ChannelInfoShouldGiveCompleteITCChannelInfo(
            [Values((ushort)0, (ushort)1, (ushort)8)] 
            ushort channelNumber,
    [Values(StreamType.ANALOG_OUT, StreamType.DIGITAL_OUT, StreamType.AUX_OUT)] 
            StreamType streamType
            )
        {
            var controller = new HekaDAQController();
            const string name = "UNUSED_NAME";
            var s = new HekaDAQOutputStream(name,
                streamType,
                channelNumber,
                controller);

            const decimal sampleRate = 9000;
            var srate = new Measurement(sampleRate, "Hz");
            controller.SampleRate = srate;

            ITCMM.ITCChannelInfo info = s.ChannelInfo;

            Assert.AreEqual(channelNumber, info.ChannelNumber);
            Assert.AreEqual((int)streamType, info.ChannelType);
            Assert.AreEqual(s.SampleRate.QuantityInBaseUnits, info.SamplingRate);
            Assert.AreEqual(ITCMM.USE_FREQUENCY, info.SamplingIntervalFlag);
            Assert.AreEqual(0, info.Gain);
            Assert.AreEqual(IntPtr.Zero, info.FIFOPointer);
        }
    }

    [TestFixture]
    class HekaDigitalDAQOutputStreamTests
    {
        [Test]
        public void ShouldBitShiftAndMergeBackground()
        {
            var controller = new HekaDAQController();
            var s = new HekaDigitalDAQOutputStream("OUT", 0, controller);
            controller.SampleRate = new Measurement(10000, 1, "Hz");

            for (ushort bitPosition = 1; bitPosition < 16; bitPosition += 2)
            {
                TestDevice dev = new TestDevice {Background = new Measurement(1, Measurement.UNITLESS)};
                dev.BindStream(s);
                s.BitPositions[dev] = bitPosition;
            }

            ulong q = 0xaaaa;
            var expected = new Measurement((long)q, Measurement.UNITLESS);

            Assert.AreEqual(expected, s.Background);
        }

        [Test]
        public void ShouldBitShiftAndMergePulledOutputData()
        {
            var controller = new HekaDAQController();
            var s = new HekaDigitalDAQOutputStream("OUT", 0, controller);
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

            var expected = Enumerable.Range(0, 10000).Select(i => new Measurement((long)(i % 2 * 0xaaaa), Measurement.UNITLESS)).ToList();

            var pull1 = s.PullOutputData(duration);
            Assert.AreEqual(expected, pull1.Data);

            var pull2 = s.PullOutputData(duration);
            Assert.AreEqual(expected, pull2.Data);
        }
    }

    [TestFixture]
    class HekkaDAQInputStreamTests
    {
        [Test]
        public void ChannelInfoShouldGiveCompleteITCChannelInfo(
            [Values((ushort)0, (ushort)1, (ushort)8)]
            ushort channelNumber,
    [Values(StreamType.ANALOG_IN, StreamType.DIGITAL_IN, StreamType.AUX_IN)] 
            StreamType streamType
            )
        {
            const string name = "UNUSED_NAME";
            var controller = new HekaDAQController();
            var s = new HekaDAQInputStream(name,
                streamType,
                channelNumber,
                controller);

            const decimal sampleRate = 9000;
            var srate = new Measurement(sampleRate, "Hz");
            controller.SampleRate = srate;

            ITCMM.ITCChannelInfo info = s.ChannelInfo;

            Assert.AreEqual(channelNumber, info.ChannelNumber);
            Assert.AreEqual((int)streamType, info.ChannelType);
            Assert.AreEqual(s.SampleRate.QuantityInBaseUnits, info.SamplingRate);
            Assert.AreEqual(ITCMM.USE_FREQUENCY, info.SamplingIntervalFlag);
            Assert.AreEqual(0, info.Gain);
            Assert.AreEqual(IntPtr.Zero, info.FIFOPointer);
        }

        [Test]
        public void DelegatesSampleRateToController()
        {
            var c = new HekaDAQController();
            var s = new HekaDAQInputStream("none", 0, 0, c);
            var srate = new Measurement(1000, "Hz");
            c.SampleRate = srate;

            Assert.That(s.SampleRate, Is.EqualTo(c.SampleRate));
            Assert.Throws<NotSupportedException>(() => s.SampleRate = srate);
        }
    }

    [TestFixture]
    class HekaDigitalDAQInputStreamTests
    {
        [Test]
        public void ShouldBitShiftAndMaskPushedInputData()
        {
            var controller = new HekaDAQController();
            var s = new HekaDigitalDAQInputStream("IN", 0, controller);
            controller.SampleRate = new Measurement(10000, 1, "Hz");

            TimeSpan duration = TimeSpan.FromSeconds(0.5);

            var devices = new List<TestDevice>();

            for (ushort bitPosition = 1; bitPosition < 16; bitPosition += 2)
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
