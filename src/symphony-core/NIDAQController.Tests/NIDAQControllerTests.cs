using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using NationalInstruments.DAQmx;
using Symphony.Core;

namespace NI
{
    [TestFixture]
    class NIDAQControllerTests
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
        }

        [TearDown]
        public void StopDAQControllers()
        {
            foreach (var controller in NIDAQController.AvailableControllers())
            {
                if (controller.IsRunning)
                {
                    controller.Stop();
                }
                if (controller.IsHardwareReady)
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
            Assert.GreaterOrEqual(NIDAQController.AvailableControllers().Count(), 1);
        }

        [Test]
        public void SampleRateMustBeGreaterThanZero()
        {
            foreach (var c in NIDAQController.AvailableControllers())
            {
                try
                {
                    FixtureForController(c);

                    c.SampleRate = new Measurement(0, "Hz");
                    Assert.False((bool)c.Validate());

                    c.SampleRate = new Measurement(-.1m, "Hz");
                    Assert.False((bool)c.Validate());

                    c.SampleRate = new Measurement(1, "Hz");
                    Assert.True((bool)c.Validate());
                }
                finally
                {
                    c.CloseHardware();
                }
            }
        }

        [Test]
        public void SampleRateMustBeInHz()
        {
            foreach (var c in NIDAQController.AvailableControllers())
            {
                try
                {
                    FixtureForController(c);

                    c.SampleRate = new Measurement(1, "barry");
                    Assert.False((bool)c.Validate());

                    c.SampleRate = new Measurement(1, "Hz");
                    Assert.True((bool)c.Validate());
                }
                finally
                {
                    c.CloseHardware();
                }
            }
        }

        [Test]
        public void InitializesHardware()
        {
            foreach (var controller in NIDAQController.AvailableControllers())
            {
                Assert.False(controller.IsHardwareReady);
                controller.InitHardware();

                try
                {
                    Assert.True(controller.IsHardwareReady);
                }
                finally 
                {
                    controller.CloseHardware();
                    Assert.False(controller.IsHardwareReady);
                }
            }
        }

        [Test]
        public void SegregatesStreams()
        {
            foreach (var controller in NIDAQController.AvailableControllers())
            {
                Assert.False(controller.IsHardwareReady);
                controller.InitHardware();

                try
                {
                    CollectionAssert.AllItemsAreInstancesOfType(controller.StreamsOfType(PhysicalChannelTypes.AI), typeof(NIDAQInputStream));
                    CollectionAssert.AllItemsAreInstancesOfType(controller.StreamsOfType(PhysicalChannelTypes.AO), typeof(NIDAQOutputStream));
                }
                finally
                {
                    controller.CloseHardware();
                }
            }
        }

        private void FixtureForController(NIDAQController controller, double durationSeconds = 10)
        {
            controller.SampleRate = new Measurement(10000, "Hz");
            controller.InitHardware();

            OutStream = controller.OutputStreams
                                  .OfType<NIDAQOutputStream>().First(str => str.Name == "ao0");
            InputStream = controller.InputStreams
                                    .OfType<NIDAQInputStream>().First(str => str.Name == "ai0");

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
            InputDevice = new TestDevice("Input", null);

            OutDevice.MeasurementConversionTarget = "V";
            InputDevice.MeasurementConversionTarget = "V";

            BindStreams(controller, OutDevice, InputDevice);
        }

        [Test]
        public void SetsChannels()
        {
            foreach (var daq in NIDAQController.AvailableControllers())
            {
                const decimal srate = 10000;

                daq.InitHardware();
                Assert.True(daq.IsHardwareReady);
                Assert.False(daq.IsRunning);

                try
                {
                    foreach (IDAQOutputStream s in daq.OutputStreams)
                    {
                        daq.SampleRate = new Measurement(srate, "Hz");
                        var externalDevice = new TestDevice("OUT-DEVICE", null);

                        s.Devices.Add(externalDevice);
                    }

                    daq.ConfigureChannels();

                    foreach (NIDAQStream s in daq.OutputStreams.Cast<NIDAQStream>())
                    {
                        var chan = daq.Channel(s.PhysicalName);

                        Assert.AreEqual(s.PhysicalName, chan.PhysicalName);
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
            foreach (NIDAQController daq in NIDAQController.AvailableControllers())
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
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        /// <summary>
        /// Controller should exeptional-stop when an output stream's buffer underruns
        /// </summary>
        [Test]
        [Timeout(5 * 1000)]
        public void ExceptionalStopOnOutputUnderrun()
        {
            foreach (var daq in NIDAQController.AvailableControllers())
            {

                try
                {
                    bool receivedExc = false;

                    FixtureForController(daq, durationSeconds: 1.0);

                    InputDevice.InputData[InputStream] = new List<IInputData>();

                    daq.ExceptionalStop += (c, args) => receivedExc = true;

                    daq.Start(false);

                    Assert.That(receivedExc, Is.True.After(1000, 10));
                }
                finally
                {
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        public void ShouldResetHardwareWhenStopsWithException()
        {
            foreach (NIDAQController daq in NIDAQController.AvailableControllers())
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

                Assert.That(receivedExc, Is.True.After(1000, 10));

                daq.CloseHardware();

                //Should be ready to initialize again
                try
                {
                    Assert.That(() => daq.InitHardware(), Throws.Nothing);
                }
                finally
                {
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        public void ShouldOpenHardwareOnStart()
        {
            foreach (var daq in NIDAQController.AvailableControllers())
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
            var c = NIDAQController.AvailableControllers().First();

            var expected = new Measurement(1000, "Hz");
            c.SampleRate = expected;

            Assert.That(c.SampleRate, Is.EqualTo(expected));
            Assert.That(c.Configuration.ContainsKey("SampleRate"));
        }

    }
}
