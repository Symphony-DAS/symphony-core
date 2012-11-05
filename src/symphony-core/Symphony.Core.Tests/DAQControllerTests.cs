using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;
using NUnit.Mocks;

namespace Symphony.Core
{
    [TestFixture]
    class DAQControllerTests
    {
        [Test]
        public void DAQControllerImplementsTimelineProducer()
        {
            Assert.True(typeof(DAQControllerBase).FindInterfaces((t, criteria) => { return true; }, null).Contains(typeof(ITimelineProducer)));
        }

        [Test]
        public void ShouldThrowForMultipleStreamsWhenGettingTypedStream()
        {
            var c = new SimpleDAQController();

            string name = "stream-name";
            (c as IMutableDAQController).AddStream(new DAQOutputStream(name));
            (c as IMutableDAQController).AddStream(new DAQOutputStream(name)); 

            Assert.Throws<InvalidOperationException>(() => c.GetStream<IDAQOutputStream>(name));
        }

        [Test]
        public void ShouldThrowForMultipleStreamsWhenGettingStream()
        {
            var c = new SimpleDAQController();

            string name = "stream-name";
            (c as IMutableDAQController).AddStream(new DAQOutputStream(name));
            (c as IMutableDAQController).AddStream(new DAQOutputStream(name));

            Assert.Throws<InvalidOperationException>(() => c.GetStream(name));
        }

        [Test]
        public void FiresOnStartedEvent()
        {
            bool fired = false;
            var controller = new TestDAQController();
            controller.Clock = controller as IClock;

            controller.Started += (c, ts) =>
            {
                fired = true;
                controller.Stop();
            };

            controller.ExceptionalStop += (c, ex) => Assert.Fail(ex.Exception.ToString());

            controller.Start(false);

            Assert.IsTrue(fired);
        }


        [Test]
        public void FiresOnStoppedEvent()
        {

            var controller = new TestDAQController();
            controller.Clock = controller;

            bool fired = false;

            controller.Stopped += (c, args) => fired = true;
            controller.ExceptionalStop += (c, ex) => Assert.Fail(ex.Exception.ToString());

            controller.Start(false);
            controller.Stop();

            Assert.IsTrue(fired);
        }

        [Test]
        public void FiresStimulusOutputEvents()
        {
            var c = new TestDAQController();
            var srate = new Measurement(10, "Hz");
            var outStream = new DAQOutputStream("out")
                                {
                                    MeasurementConversionTarget = "V",
                                    SampleRate = srate
                                };
            var inStream = new DAQInputStream("in")
                               {
                                   MeasurementConversionTarget = "V",
                                   SampleRate = srate
                               };

            var outputData = new Dictionary<IDAQOutputStream, Queue<IOutputData>>();
            var dataQueue = new Queue<IOutputData>();
            var outputIOData = new OutputData(
                Enumerable.Range(0, 100).Select(i => new Measurement(i, "V")),
                new Measurement(10, "Hz")
                );

            dataQueue.Enqueue(outputIOData);

            outputData[outStream] = dataQueue;

            var dev = new TestDevice("test", outputData) { MeasurementConversionTarget = "V" };

            dev.BindStream(outStream);

            bool fired = false;
            c.StimulusOutput += (daq, args) =>
                                    {
                                        Assert.That(args.Stream == outStream);
                                        Assert.That(args.Data != null);
                                        Assert.That(args.Data.Configuration.Count() > 0);
                                        fired = true;
                                    };
            c.ProcessIteration += (daq, args) => ((IDAQController) daq).RequestStop();

            c.AddStreamMapping(outStream, inStream);

            c.Start(false);

            while (c.Running) ;

            Assert.That(fired, Is.True.After(1000,10));
        }


        [Test]
        public void GetStreamReturnsStreamWithName()
        {
            var c = new SimpleDAQController();

            const string name = "stream-name";
            var outStream = new DAQOutputStream(name);
            (c as IMutableDAQController).AddStream(outStream);
            var inputStream = new DAQInputStream(name);
            (c as IMutableDAQController).AddStream(inputStream);

            Assert.That(c.GetStream<IDAQOutputStream>(name), Is.EqualTo(outStream));
            Assert.That(c.GetStream<IDAQInputStream>(name), Is.EqualTo(inputStream));
        }

        [Test]
        public void GetStreamReturnsNullForUnknownName()
        {
            var c = new SimpleDAQController();

            const string name = "stream-name";
            const string distractor = "distractor-name";
            var outStream = new DAQOutputStream(name);
            (c as IMutableDAQController).AddStream(outStream);

            Assert.That(c.GetStream<IDAQOutputStream>(distractor), Is.Null);
            Assert.That(c.GetStream<IDAQInputStream>(distractor), Is.Null);
            Assert.That(c.GetStream<IDAQInputStream>(name), Is.Null);
        }


        [Test]
        public void SpinUpOtherThreadAndWait()
        {
            // This test ensures that we can spin up a thread, then hold the outer thread
            // until the inner one is completed; this lets us mock the active-threaded
            // nature of the IExternalDevices more accurately, since they will be the ones
            // owning the threads.
            //
            EventWaitHandle ewh = new EventWaitHandle(false, EventResetMode.ManualReset);
            bool Flag = false;

            new Thread(delegate()
            {
                Thread.Sleep(1 * 1000);
                Flag = true;

                // This should be the last thing in the spun-up thread; whatever
                // happens after this Set() is happening in parallel to the rest
                // of the unit tests being run
                //
                ewh.Set();
            }).Start();

            ewh.WaitOne();
            Assert.IsTrue(Flag);
        }

        [Test]
        public void ShouldSetStreamBackgroundOnStop()
        {
            var c = new SimpleDAQController(new IDAQStream[0]) {BackgroundSet = false};
            c.Start(false);
            Assert.That(c.BackgroundSet, Is.True);
        }

        [Test]
        public void ShouldSetStreamBackgroundOnExceptionalStop()
        {
            var c = new ExceptionThrowingDAQController {BackgroundSet = false};
            Assert.That(c.BackgroundSet, Is.False);
            c.Start(false);
            Assert.That(c.BackgroundSet, Is.True);
        }

        [Test]
        public void ShouldNotSetBackgroundWhenStreamDoesNotUseThisController()
        {
            var c = new SimpleDAQController(new IDAQStream[0]) { BackgroundSet = false };
            var s = new DAQOutputStream("test", null);

            Assert.That(()=> c.ApplyStreamBackground(s), Throws.Exception.TypeOf<DAQException>());
        }

        [Test]
        public void ShouldSetStreamBackgroundWhenStopped()
        {
            var c = new SimpleDAQController(new IDAQStream[0]) { BackgroundSet = false, AsyncBackground = null};
            var s = new DAQOutputStream("test", c);
            c.AddStream(s);
            c.SetRunning(false);

            var device = new DynamicMock(typeof (IExternalDevice));
            var background = new Measurement(0, "V");
            device.ExpectAndReturn("get_OutputBackground", background);

            s.MeasurementConversionTarget = "V";
            s.Device = device.MockInstance as IExternalDevice;

            s.ApplyBackground();

            Assert.That(c.AsyncBackground, Is.EqualTo(background));
        }

    }


}
