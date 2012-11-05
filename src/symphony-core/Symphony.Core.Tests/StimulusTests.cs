using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Symphony.Core
{
    [TestFixture]
    class StimulusTests
    {
        private const string UNUSED_ID = "stimulus.id";
        private const string UNUSED_UNITS = "units";
        private readonly IDictionary<string,object> UNUSED_PARAMETERS = new Dictionary<string, object>();

        [Test]
        public void ShouldAccumulateConfigurationSpans()
        {
            var s = new TestStimulus(UNUSED_ID, UNUSED_UNITS, UNUSED_PARAMETERS);

            var t1 = TimeSpan.FromSeconds(1);
            var t2 = TimeSpan.FromSeconds(1);

            var c1 = new List<IPipelineNodeConfiguration>();
            var c2 = new List<IPipelineNodeConfiguration>();

            c1.Add(new PipelineNodeConfiguration("NODE1", new Dictionary<string, object>()));
            c1.Add(new PipelineNodeConfiguration("NODE2", new Dictionary<string, object>()));


            c2.Add(new PipelineNodeConfiguration("NODE1", new Dictionary<string, object>()));
            c2.Add(new PipelineNodeConfiguration("NODE2", new Dictionary<string, object>()));
            
            s.DidOutputData(t1, c1);

            Assert.That(s.OutputConfigurationSpans.Count(), Is.EqualTo(1));

            s.DidOutputData(t2, c2);

            Assert.That(s.OutputConfigurationSpans.Count(), Is.EqualTo(2));

            Assert.That(s.OutputConfigurationSpans.First().Time, Is.EqualTo(t1));
            Assert.That(s.OutputConfigurationSpans.Last().Time, Is.EqualTo(t2));

        }

        [Test]
        public void ShouldSortOutputConfigurationSpans()
        {
            var s = new TestStimulus(UNUSED_ID, UNUSED_UNITS, UNUSED_PARAMETERS);

            var t1 = TimeSpan.FromSeconds(1);
            var t2 = TimeSpan.FromSeconds(1);

            var c1 = new List<IPipelineNodeConfiguration>();
            var c2 = new List<IPipelineNodeConfiguration>();

            c1.Add(new PipelineNodeConfiguration("NODE1", new Dictionary<string, object>()));
            c1.Add(new PipelineNodeConfiguration("NODE2", new Dictionary<string, object>()));


            c2.Add(new PipelineNodeConfiguration("NODE1", new Dictionary<string, object>()));
            c2.Add(new PipelineNodeConfiguration("NODE2", new Dictionary<string, object>()));

            s.DidOutputData(t2, c1);

            s.DidOutputData(t1, c2);

            Assert.That(s.OutputConfigurationSpans.Count(), Is.EqualTo(2));

            Assert.That(s.OutputConfigurationSpans.First().Time, Is.EqualTo(t1));
            Assert.That(s.OutputConfigurationSpans.Last().Time, Is.EqualTo(t2));
        }


        class TestStimulus : Stimulus
        {
            /// <summary>
            /// Constructs a Stimulus with the given ID and parameters.
            /// </summary>
            /// <param name="stimulusID">Stimulus generator ID</param>
            /// <param name="units">Stimulus data units</param>
            /// <param name="parameters">Parameters of stimulus generation</param>
            public TestStimulus(string stimulusID, string units, IDictionary<string, object> parameters)
                : base(stimulusID, units, parameters)
            {
            }

            public override IEnumerable<IOutputData> DataBlocks(TimeSpan blockDuration)
            {
                return null;
            }

            public override Option<TimeSpan> Duration { get { return Option<TimeSpan>.Some(TimeSpan.Zero); } }

        }
    }

    [TestFixture]
    class DelegatedStimulusTests
    {

        [Test]
        public void HoldsParameters()
        {
            var parameters = new Dictionary<string, object>();
            parameters["key1"] = "value1";
            parameters["key2"] = 2;

            var s = new DelegatedStimulus("DelegatedStimulus", "units",
                parameters,
                (p, b) => null,
                (p) => Option<TimeSpan>.Some(TimeSpan.FromMilliseconds(0)));
            Assert.That(s.Parameters, Is.EqualTo(parameters));
        }

        [Test]
        public void HoldsStimulusID()
        {
            var parameters = new Dictionary<string, object>();
            string stimID = "my.ID";

            var s = new DelegatedStimulus(stimID, "units",
                parameters,
                (p, b) => null,
                (p) => Option<TimeSpan>.Some(TimeSpan.FromMilliseconds(0)));

            Assert.That(s.StimulusID, Is.EqualTo(stimID));
        }

        [Test]
        public void DelegatesDuration()
        {
            var parameters = new Dictionary<string, object>();
            parameters["duration"] = TimeSpan.FromMilliseconds(617);

            var s = new DelegatedStimulus("DelegatedStimulus", "units",
                                          parameters,
                                          (p, b) => null,
                                          (p) => Option<TimeSpan>.Some((TimeSpan)p["duration"]));

            Assert.That((TimeSpan)s.Duration, Is.EqualTo(parameters["duration"]));
        }

        [Test]
        public void DelegatesBlocks()
        {
            var parameters = new Dictionary<string, object>();
            parameters["sampleRate"] = new Measurement(1000, "Hz");

            var s = new DelegatedStimulus("DelegatedStimulus", "units",
                                          parameters,
                                          (p, b) => new OutputData(Enumerable.Range(0, (int)(b.TotalSeconds * (double)((IMeasurement)p["sampleRate"]).QuantityInBaseUnit))
                                                                       .Select(i => new Measurement(i, "units")).ToList(),
                                                                   (IMeasurement)p["sampleRate"],
                                                                   false),
                                          (p) => Option<TimeSpan>.None());


            var block = TimeSpan.FromMilliseconds(100);
            IEnumerator<IOutputData> iter = s.DataBlocks(block).GetEnumerator();
            int n = 0;
            while (iter.MoveNext() && n < 100)
            {
                var expected =
                    new OutputData(
                        Enumerable.Range(0, (int)(block.TotalSeconds * (double)((IMeasurement)parameters["sampleRate"]).QuantityInBaseUnit))
                            .Select(i => new Measurement(i, "units")).ToList(),
                        (IMeasurement)parameters["sampleRate"],
                        false);

                Assert.That(iter.Current.Duration, Is.EqualTo(expected.Duration));
                Assert.That(iter.Current.Data, Is.EqualTo(expected.Data));
                n++;
            }
        }

        [Test]
        public void ShouldThrowForUnitMismatch()
        {
            var parameters = new Dictionary<string, object>();
            parameters["sampleRate"] = new Measurement(1000, "Hz");

            var s = new DelegatedStimulus("DelegatedStimulus", "units",
                                          parameters,
                                          (p, b) => new OutputData(Enumerable.Range(0, (int)(b.TotalSeconds * (double)((IMeasurement)p["sampleRate"]).QuantityInBaseUnit))
                                                                       .Select(i => new Measurement(i, "other")).ToList(),
                                                                   (IMeasurement)p["sampleRate"],
                                                                   false),
                                          (p) => Option<TimeSpan>.None());

            var block = TimeSpan.FromMilliseconds(100);
            Assert.That(() => s.DataBlocks(block).GetEnumerator().MoveNext(), Throws.TypeOf(typeof(StimulusException)));
        }


    }


    [TestFixture]
    class RenderedStimulusTests
    {
        [Test]
        public void HoldsParameters()
        {
            var parameters = new Dictionary<string, object>();
            parameters["key1"] = "value1";
            parameters["key2"] = 2;

            var measurements = new List<IMeasurement> {new Measurement(1, "V")};
            var data = new OutputData(measurements, new Measurement(1, "Hz"), false);
            var s = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) parameters, (IOutputData) data);
            Assert.That(s.Parameters, Is.EqualTo(parameters));
        }

        [Test]
        public void HoldsStimulusID()
        {
            var parameters = new Dictionary<string, object>();
            const string stimID = "my.ID";

            var measurements = new List<IMeasurement> { new Measurement(1, "V") };
            var data = new OutputData(measurements, new Measurement(1, "Hz"), false);
            var s = new RenderedStimulus((string) stimID, (IDictionary<string, object>) parameters, (IOutputData) data);

            Assert.That(s.StimulusID, Is.EqualTo(stimID));
        }

        [Test]
        public void EnumeratesDataBlocks(
            [Values(100, 500, 1000, 5000)] double blockMilliseconds,
            [Values(1000, 5000, 10000)] double sampleRateHz
            )
        {
            var parameters = new Dictionary<string, object>();
            var sampleRate = new Measurement((decimal)sampleRateHz, "Hz");

            IOutputData data = new OutputData(Enumerable.Range(0, (int)TimeSpan.FromSeconds(3).Samples(new Measurement((decimal)sampleRateHz, "Hz")))
                .Select(i => new Measurement(i, "units")).ToList(),
                sampleRate,
                false);

            var s = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) parameters, data);

            var blockSpan = TimeSpan.FromMilliseconds(blockMilliseconds);
            IEnumerator<IOutputData> iter = s.DataBlocks(blockSpan).GetEnumerator();
            while (iter.MoveNext())
            {
                var cons = data.SplitData(blockSpan);
                data = cons.Rest;
                Assert.That(iter.Current.Duration, Is.EqualTo(cons.Head.Duration));
                Assert.That(iter.Current.Data, Is.EqualTo(cons.Head.Data));
            }
        }

        [Test]
        public void ShouldThrowForUnitMismatch()
        {
            const double sampleRateHz = 100d;
            var parameters = new Dictionary<string, object>();
            var sampleRate = new Measurement((decimal)sampleRateHz, "Hz");

            IOutputData data = new OutputData(Enumerable.Range(0, (int)TimeSpan.FromSeconds(3).Samples(new Measurement((decimal)sampleRateHz, "Hz")))
                .Select(i => new Measurement(i, i > 1 ? "other" : "first")).ToList(),
                sampleRate,
                false);

            Assert.That(() => new RenderedStimulus("RenderedStimulus", parameters, data), Throws.TypeOf(typeof(MeasurementIncompatibilityException)));
        }

        [Test]
        public void LastDataIsLast()
        {
            var parameters = new Dictionary<string, object>();
            var sampleRate = new Measurement(1000, "Hz");

            IOutputData data = new OutputData(Enumerable.Range(0, 1000).Select(i => new Measurement(i, "units")).ToList(),
                sampleRate,
                false);

            var s = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) parameters, data);

            var block = TimeSpan.FromMilliseconds(100);
            IEnumerator<IOutputData> iter = s.DataBlocks(block).GetEnumerator();
            IOutputData current = null;
            while (iter.MoveNext())
            {
                current = iter.Current;
            }

            Assert.That(current.IsLast, Is.True);
        }

        [Test]
        public void MarksAsNotLastIfMoreBlocks()
        {
            var parameters = new Dictionary<string, object>();
            var sampleRate = new Measurement(1000, "Hz");

            IOutputData data = new OutputData(Enumerable.Range(0, 1000).Select(i => new Measurement(i, "units")).ToList(),
                sampleRate,
                true);

            var s = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) parameters, data);

            var block = TimeSpan.FromMilliseconds(100);
            IEnumerator<IOutputData> iter = s.DataBlocks(block).GetEnumerator();
            Assert.True(iter.MoveNext());
            Assert.False(iter.Current.IsLast);
        }

    }
}
