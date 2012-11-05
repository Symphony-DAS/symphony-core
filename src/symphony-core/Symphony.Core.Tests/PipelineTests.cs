using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Symphony.Core
{
    [TestFixture]
    class PipelineTests
    {

        [SetUp]
        public void RegisterConverters()
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);
        }

        /// <summary>
        /// Output pipeline should provide continuous stimulus
        /// </summary>
        [Test]
        public void OutputPipelineContinuity(
            [Values(1000, 5000, 10000, 15000, 20000)] double sampleRate,
            [Values(0.1, 0.5, 1, 5)] double blockDurationSeconds
            )
        {

            const double epochDuration = 2; //seconds
            var srate = new Measurement((decimal) sampleRate, "Hz");

            var daq = new TestDAQController();
            var outStream = new DAQOutputStream("OUT")
                                {
                                    SampleRate = srate,
                                    MeasurementConversionTarget = "V"
                                };

            var controller = new Controller() { Clock = daq, DAQController = daq };

            var dev = new UnitConvertingExternalDevice("dev", "co", controller, new Measurement(0, "V"))
                          {
                              Clock = daq,
                              MeasurementConversionTarget = "V"
                          };
            dev.BindStream(outStream);

            // Setup Epoch
            var e = new Epoch("OutputPipelineContinuity");

            var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(srate);
            IList<IMeasurement> stimData = (IList<IMeasurement>) Enumerable.Range(0, nSamples)
                                                       .Select(i => new Measurement((decimal) (8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                       .ToList();
            IOutputData stimOutputData = new OutputData(stimData, srate);

            var stim = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) new Dictionary<string, object>(),
                                            stimOutputData);
            e.Stimuli[dev] = stim;
            e.Responses[dev] = new Response();
            e.Background[dev] = new Epoch.EpochBackground(new Measurement(0, "V"), srate);

            controller.EnqueueEpoch(e);
            controller.NextEpoch();

            var blockSpan = TimeSpan.FromSeconds(blockDurationSeconds);
            foreach (var stimBlock in stim.DataBlocks(blockSpan))
            {
                var cons = stimOutputData.SplitData(blockSpan);
                var expected = cons.Head.Data;
                stimOutputData = cons.Rest;

                Assert.That(stimBlock.Data, Is.EqualTo(expected));
            }

        }
    }
}
