using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Symphony.Core;
using System.Linq;

namespace Symphony.SimulationDAQController
{
    [TestFixture]
    class ControllerTests
    {
        private const int IterationMilliseconds = 500;


        [Test]
        public void PullsOutput()
        {
            TimeSpan loopDuration = TimeSpan.FromMilliseconds(IterationMilliseconds);

            SimulationDAQController c = new SimulationDAQController(loopDuration);
            c.Clock = new FakeClock();

            IOutputData expectedOutput;
            DAQOutputStream outStream;
            SetupOutputPipeline(c, out expectedOutput, out outStream);

            IDictionary<IDAQOutputStream, IOutputData> actualOutput = null;
            TimeSpan actualTimeStep = TimeSpan.FromMilliseconds(0);

            c.SimulationRunner = (IDictionary<IDAQOutputStream, IOutputData> output, TimeSpan timeStep) =>
            {
                actualOutput = output;
                actualTimeStep = timeStep;

                return new Dictionary<IDAQInputStream, IInputData>();
            };

            c.ProcessIteration += (controller, evtArgs) =>
            {
                ((Symphony.Core.IDAQController)controller).Stop();
            };
            c.Start(false);

            while (c.Running) { }

            Assert.AreEqual(loopDuration, actualTimeStep);
            CollectionAssert.AreEqual(actualOutput[outStream].Data, expectedOutput.SplitData(loopDuration).Head.Data);
        }

        private void SetupOutputPipeline(IDAQController c, out IOutputData expectedOutput, out DAQOutputStream outStream)
        {
            string units = "V";
            var data = Enumerable.Range(0, 1000).Select(i => new Measurement(i, units)).ToList();
            var sampleRate = new Measurement(100, "Hz");
            var config = new Dictionary<string, object>();

            expectedOutput = new OutputData(data,
                sampleRate, true);

            outStream = new DAQOutputStream("OUT");
            outStream.SampleRate = sampleRate;
            outStream.MeasurementConversionTarget = units;
            (c as IMutableDAQController).AddStream(outStream);

            var outQueue = new Dictionary<IDAQOutputStream, Queue<IOutputData>>();
            outQueue[outStream] = new Queue<IOutputData>();
            outQueue[outStream].Enqueue(expectedOutput);

            TestDevice dev = new TestDevice("OUT-DEVICE", outQueue);
            dev.BindStream(outStream);
            dev.MeasurementConversionTarget = units;
        }



        [Test]
        [Timeout(2 * 1000)]
        public void PushesInput()
        {
            TimeSpan loopDuration = TimeSpan.FromMilliseconds(IterationMilliseconds);

            var c = new SimulationDAQController(loopDuration) { Clock = new FakeClock() };

            IOutputData expectedOutput;
            DAQOutputStream outStream;
            DAQInputStream inStream;
            SetupInputPipeline(c, out expectedOutput, out outStream, out inStream);


            c.SimulationRunner = (IDictionary<IDAQOutputStream, IOutputData> output, TimeSpan timeStep) =>
            {
                var inputData = new Dictionary<IDAQInputStream, IInputData>(1);
                expectedOutput = output[outStream];
                inputData[inStream] = new InputData(expectedOutput.Data, expectedOutput.SampleRate, DateTimeOffset.Now);

                return inputData;
            };

            c.ProcessIteration += (controller, evtArgs) => ((IDAQController)controller).Stop();
            bool stopped = false;
            c.Stopped += (controller, evtArgs) =>
                             {
                                 stopped = true;
                             };
            c.Start(false);
            Thread.Sleep(500);

            var actualInput = ((TestDevice)outStream.Device).InputData.ContainsKey(inStream) ? ((TestDevice)outStream.Device).InputData[inStream].First() : null;

            Assert.That(actualInput, Is.Not.Null);
            Assert.That(actualInput.Data, Is.EqualTo(expectedOutput.Data));
        }

        private void SetupInputPipeline(IDAQController c, out IOutputData expectedOutput, out DAQOutputStream outStream, out DAQInputStream inStream)
        {


            SetupOutputPipeline(c, out expectedOutput, out outStream);

            inStream = new DAQInputStream("IN");
            inStream.SampleRate = outStream.SampleRate;
            outStream.Device.BindStream(inStream);
            inStream.MeasurementConversionTarget = outStream.MeasurementConversionTarget;
        }
    }
}
