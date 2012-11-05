using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HekkaDAQ.Tests.Properties;

namespace Heka
{
    using Heka.NativeInterop;
    using NUnit.Framework;
    using Symphony.Core;

    [TestFixture]
    class HekaDAQControllerTests
    {

        Controller Controller { get; set; }
        TestDevice OutDevice { get; set; }
        TestDevice InputDevice { get; set; }
        IOutputData Data { get; set; }
        IDAQOutputStream OutStream { get; set; }
        IDAQInputStream InputStream { get; set; }

        [SetUp]
        public void SetUp()
        {
            Controller = new Controller();

            IDictionary<string, object> config = new Dictionary<string, object>(2);
            config["param1"] = 10;
            config["param2"] = 1;

            Converters.Clear();
            HekaDAQOutputStream.RegisterConverters();
            HekaDAQInputStream.RegisterConverters();

        }

        [TearDown]
        public void StopDAQControllers()
        {
            foreach (HekaDAQController controller in HekaDAQController.AvailableControllers())
            {
                if (controller.Running)
                {
                    controller.Stop();
                }
                if (controller.HardwareReady)
                {
                    //controller.CloseHardware();
                }

            }
        }

        private void BindStreams(IDAQController c, ExternalDeviceBase outDevice, ExternalDeviceBase inDevice)
        {
            outDevice.BindStream("OUT", OutStream);
            inDevice.BindStream("IN", InputStream);

            Assert.True(InputStream.Active);
            Assert.True(OutStream.Active);
        }

        [Test]
        public void AvailableControllers()
        {
            Assert.GreaterOrEqual(HekaDAQController.AvailableControllers().Count(), 1);
        }


        [Test]
        public void SampleRateMustBeGreaterThanZero()
        {
            var c = new HekaDAQController();

            c.SampleRate = new Measurement(0, "Hz");
            Assert.False((bool)c.Validate());

            c.SampleRate = new Measurement(-.1m, "Hz");
            Assert.False((bool)c.Validate());

            c.SampleRate = new Measurement(1, "Hz");
            Assert.True((bool)c.Validate());
        }

        [Test]
        public void SampleRateMustBeInHz()
        {
            var c = new HekaDAQController();

            c.SampleRate = new Measurement(1, "barry");
            Assert.False((bool)c.Validate());

            c.SampleRate = new Measurement(1, "Hz");
            Assert.True((bool)c.Validate());
        }

        [Test]
        public void InitializesHardware()
        {
            foreach (HekaDAQController controller in HekaDAQController.AvailableControllers())
            {
                Assert.False(controller.HardwareReady);
                controller.InitHardware();

                try
                {
                    Assert.True(controller.HardwareReady);
                }
                finally
                {
                    controller.CloseHardware();
                    Assert.False(controller.HardwareReady);
                }
            }

        }

        [Test]
        public void SegregatesStreams()
        {
            foreach (HekaDAQController controller in HekaDAQController.AvailableControllers())
            {
                Assert.False(controller.HardwareReady);
                controller.InitHardware();

                try
                {
                    CollectionAssert.AllItemsAreInstancesOfType(controller.StreamsOfType(StreamType.ANALOG_IN), typeof(HekaDAQInputStream));
                    CollectionAssert.AllItemsAreInstancesOfType(controller.StreamsOfType(StreamType.ANALOG_OUT), typeof(HekaDAQOutputStream));
                }
                finally
                {
                    controller.CloseHardware();
                }
            }
        }



        private void FixtureForController(HekaDAQController controller, double durationSeconds = 10)
        {
            controller.Clock = controller;

            controller.SampleRate = new Measurement(10000, "Hz");
            controller.InitHardware();

            OutStream = controller.OutputStreams
                .OfType<HekaDAQOutputStream>()
                .Where(str => str.ChannelNumber == 0)
                .First();
            InputStream = controller.InputStreams
                .OfType<HekaDAQInputStream>()
                .Where(str => str.ChannelNumber == 0)
                .First();

            InputStream.Configuration["SampleRate"] = InputStream.SampleRate;

            OutStream.Configuration["SampleRate"] = OutStream.SampleRate;

            IDAQOutputStream s = OutStream;

            var dataQueue = new Dictionary<IDAQOutputStream, Queue<IOutputData>>();

            dataQueue[s] = new Queue<IOutputData>();
            Data = new OutputData(
                Enumerable.Range(0, (int)(TimeSpan.FromSeconds(durationSeconds).Samples(controller.SampleRate)))
                    .Select(i => new Measurement(i % 10, "V")).ToList(),
                s.SampleRate,
                false);

            TimeSpan d = new TimeSpan(controller.ProcessInterval.Ticks / 2);
            var outData = (IOutputData)Data.Clone();
            while (outData.Duration > TimeSpan.Zero)
            {
                var split = outData.SplitData(d);

                dataQueue[s].Enqueue(new OutputData(split.Head, split.Rest.Duration == TimeSpan.Zero));

                outData = split.Rest;
            }


            OutDevice = new TestDevice("Output", dataQueue);
            InputDevice = new TestDevice("Intput", null);

            OutDevice.MeasurementConversionTarget = "V";
            InputDevice.MeasurementConversionTarget = "V";

            BindStreams(controller, OutDevice, InputDevice);

        }




        [Test]
        public void RoundTripStreamITChannelInfo()
        {
            foreach (HekaDAQController daq in HekaDAQController.AvailableControllers())
            {
                daq.InitHardware();
                try
                {
                    daq.ExceptionalStop += (c, arg) =>
                    {
                        throw arg.Exception;
                    };

                    foreach (HekaDAQStream stream in daq.Streams.Cast<HekaDAQStream>())
                    {
                        daq.SampleRate = new Measurement(stream.ChannelNumber, 1, "Hz");
                        ITCMM.ITCChannelInfo info = stream.ChannelInfo;

                        Assert.AreEqual(stream.ChannelNumber, info.ChannelNumber);
                        Assert.AreEqual((uint)stream.ChannelType, info.ChannelType);

                        //NO_SCALE is seconds scale (Hz)
                        Assert.AreEqual(ITCMM.USE_FREQUENCY & ITCMM.NO_SCALE & ITCMM.ADJUST_RATE, info.SamplingIntervalFlag);
                        Assert.That(info.SamplingRate, Is.EqualTo(stream.SampleRate.QuantityInBaseUnit));
                        Assert.AreEqual(IntPtr.Zero, info.FIFOPointer);
                        Assert.AreEqual(0, info.Gain);
                    }
                }
                finally
                {
                    daq.CloseHardware();
                }
            }
        }


        [Test]
        public void SetsChannelInfo()
        {
            foreach (HekaDAQController daq in HekaDAQController.AvailableControllers())
            {
                const decimal srate = 10000;

                daq.InitHardware();
                Assert.True(daq.HardwareReady);
                Assert.False(daq.HardwareRunning);

                try
                {
                    foreach (IDAQOutputStream s in daq.OutputStreams)
                    {
                        daq.SampleRate = new Measurement(srate, "Hz");
                        TestDevice externalDevice = new TestDevice("OUT-DEVICE", null);

                        s.Device = externalDevice;
                    }

                    daq.ConfigureChannels();

                    foreach (HekaDAQStream s in daq.OutputStreams.Cast<HekaDAQStream>())
                    {
                        ITCMM.ITCChannelInfo actual = daq.ChannelInfo(s.ChannelType, s.ChannelNumber);

                        ITCMM.ITCChannelInfo expected = s.ChannelInfo;

                        Assert.AreEqual(expected.ChannelNumber, actual.ChannelNumber);
                        Assert.AreEqual(expected.ChannelType, actual.ChannelType);
                        Assert.AreEqual(expected.SamplingIntervalFlag, actual.SamplingIntervalFlag);
                        Assert.AreEqual(expected.SamplingRate, actual.SamplingRate);
                        // Gain set by hardware.
                    }
                }
                finally
                {
                    daq.CloseHardware();
                }
            }
        }

        [Test]
        public void ExceptionalStopOnPushException()
        {
            foreach (HekaDAQController daq in HekaDAQController.AvailableControllers())
            {
                try
                {
                    bool receivedExc = false;

                    FixtureForController(daq, durationSeconds: 1.0);

                    InputDevice.InputData[InputStream] = new List<IInputData>();

                    daq.ProcessIteration += (c, args) =>
                                                {
                                                    throw new Exception("bam!");
                                                };

                    daq.ExceptionalStop += (c, args) => receivedExc = true;

                    daq.Start(false);

                    Assert.That(receivedExc, Is.True.After(1000, 100));
                }
                finally
                {
                    if(daq.HardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        /// <summary>
        /// Controller should exeptional-stop when an output stream's buffer underruns
        /// </summary>
        [Test]
        public void ExceptionalStopOnOutputUnderrun()
        {
            foreach (HekaDAQController daq in HekaDAQController.AvailableControllers())
            {

                try
                {
                    bool receivedExc = false;

                    FixtureForController(daq, durationSeconds: 1.0);

                    InputDevice.InputData[InputStream] = new List<IInputData>();

                    daq.ExceptionalStop += (c, args) => receivedExc = true;

                    daq.Start(false);

                    Assert.That(receivedExc, Is.True.After(1000,10));
                }
                finally
                {
                    if(daq.HardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        public void ShouldResetHardwareWhenStopsWithException()
        {
            foreach (HekaDAQController daq in HekaDAQController.AvailableControllers())
            {
                bool receivedExc = false;

                FixtureForController(daq, durationSeconds: 1.0);

                InputDevice.InputData[InputStream] = new List<IInputData>();

                daq.ProcessIteration += (c, args) =>
                {
                    Console.WriteLine("Blowing up the pipeline.");
                    throw new Exception("bam!");
                };

                daq.ExceptionalStop += (c, args) => receivedExc = true;

                daq.Start(false);

                Assert.That(receivedExc, Is.True.After(1000,10));
                
                daq.CloseHardware();

                //Should be ready to initialize again
                try
                {
                    Assert.That(()=>daq.InitHardware(), Throws.Nothing);
                }
                finally
                {
                    if(daq.HardwareReady)
                        daq.CloseHardware();    
                }
                
            }
        }

        [Test]
        public void ShouldOpenHardwareOnStart()
        {
            foreach (HekaDAQController daq in HekaDAQController.AvailableControllers())
            {
                try
                {
                    bool receivedExc = false;
                    FixtureForController(daq, durationSeconds: 1.0);

                    InputDevice.InputData[InputStream] = new List<IInputData>();

                    daq.ProcessIteration += (c, args) =>
                    {
                        Console.WriteLine("Blowing up the pipeline.");
                        throw new Exception("bam!");
                    };

                    daq.ExceptionalStop += (c, args) => receivedExc = true;

                    daq.CloseHardware();
                    daq.Start(false);

                    Assert.That(receivedExc, Is.True.After(1000, 10));
                }
                finally
                {
                    daq.CloseHardware();
                }
            }
        }


        [Test]
        public void ShouldStoreSampleRateInConfiguration()
        {
            var c = new HekaDAQController();

            var expected = new Measurement(1000, "Hz");
            c.SampleRate = expected;

            Assert.That(c.SampleRate, Is.EqualTo(expected));
            Assert.That(c.Configuration.ContainsKey("SampleRate"));
        }

    }

}
