using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Symphony.Core
{
    [TestFixture]
    class StimulusOutputStreamTests
    {
        OutputData data;

        IStimulus stim;
        StimulusOutputStream stream;

        IStimulus indefiniteStim;
        StimulusOutputStream indefiniteStream;

        [SetUp]
        public void SetUp()
        {
            const int srate = 1000;
            IList<IMeasurement> list = Enumerable.Range(0, srate * 2).Select(i => new Measurement(i, "V") as IMeasurement).ToList();
            data = new OutputData(list, new Measurement(srate, "Hz"));

            stim = new RenderedStimulus("stimID", new Dictionary<string, object>(), data);
            stream = new StimulusOutputStream(stim, TimeSpan.FromSeconds(0.13));

            indefiniteStim = new RepeatingRenderedStimulus("indefID", new Dictionary<string, object>(), data, Option<TimeSpan>.None());
            indefiniteStream = new StimulusOutputStream(indefiniteStim, TimeSpan.FromSeconds(0.52));
        }

        [Test]
        public void PullsOutputData()
        {
            var dur1 = TimeSpan.FromSeconds(0.75);
            var pull1 = stream.PullOutputData(dur1);

            var dur2 = TimeSpan.FromSeconds(0.05);
            var pull2 = stream.PullOutputData(dur2);

            var dur3 = TimeSpan.FromSeconds(1.2);
            var pull3 = stream.PullOutputData(dur3);

            var samples1 = (int) dur1.Samples(stream.SampleRate);
            Assert.That(pull1.Data, Is.EqualTo(data.Data.Take(samples1).ToList()));

            var samples2 = (int) dur2.Samples(stream.SampleRate);
            Assert.That(pull2.Data, Is.EqualTo(data.Data.Skip(samples1).Take(samples2).ToList()));

            var samples3 = (int) dur3.Samples(stream.SampleRate);
            Assert.That(pull3.Data, Is.EqualTo(data.Data.Skip(samples1).Skip(samples2).Take(samples3).ToList()));
        }

        [Test]
        public void PullsOutputDataIndefinitely()
        {
            for (int i = 0; i < 100; i++)
            {
                var pull = indefiniteStream.PullOutputData(data.Duration);
                Assert.AreEqual(pull.Data, data.Data);
            }
        }

        [Test]
        public void ShouldSetIsAtEnd()
        {
            Assert.IsFalse(stream.IsAtEnd);

            stream.PullOutputData(new TimeSpan(data.Duration.Ticks/2));
            Assert.IsFalse(stream.IsAtEnd);

            stream.PullOutputData(new TimeSpan(data.Duration.Ticks/2));
            Assert.IsTrue(stream.IsAtEnd);
        }

        [Test]
        public void ShouldPushOutputDataConfigurationToStimuli()
        {
            var pull1 = stream.PullOutputData(new TimeSpan(data.Duration.Ticks / 3));
            var pull2 = stream.PullOutputData(new TimeSpan(data.Duration.Ticks / 2));

            var configuration1 = new List<IPipelineNodeConfiguration>();
            var config1 = new Dictionary<string, object>() { {"key1", "value1"} };
            configuration1.Add(new PipelineNodeConfiguration("NODE1", config1));

            var configuration2 = new List<IPipelineNodeConfiguration>();
            var config2 = new Dictionary<string, object>() { {"key2", "value2"} };
            configuration1.Add(new PipelineNodeConfiguration("NODE2", config2));

            stream.DidOutputData(DateTime.Now, pull1.Duration, configuration1);
            stream.DidOutputData(DateTime.Now, pull2.Duration, configuration2);

            Assert.That(stim.OutputConfigurationSpans.ElementAt(0).Nodes, Is.EqualTo(configuration1));
            Assert.That(stim.OutputConfigurationSpans.ElementAt(0).Time, Is.EqualTo(pull1.Duration));

            Assert.That(stim.OutputConfigurationSpans.ElementAt(1).Nodes, Is.EqualTo(configuration2));
            Assert.That(stim.OutputConfigurationSpans.ElementAt(1).Time, Is.EqualTo(pull2.Duration));
        }
    }

    [TestFixture]
    class BackgroundOutputStreamTests
    {
        Background background;
        BackgroundOutputStream stream;
        BackgroundOutputStream indefiniteStream;

        [SetUp]
        public void SetUp()
        {
            background = new Background(new Measurement(1, "V"), new Measurement(10000, "Hz"));

            stream = new BackgroundOutputStream(background, Option<TimeSpan>.Some(TimeSpan.FromSeconds(0.97)));

            indefiniteStream = new BackgroundOutputStream(background);
        }

        [Test]
        public void PullsOutputData()
        {
            var dur1 = TimeSpan.FromSeconds(0.75);
            var pull1 = stream.PullOutputData(dur1);

            var dur2 = TimeSpan.FromSeconds(0.05);
            var pull2 = stream.PullOutputData(dur2);

            var dur3 = TimeSpan.FromSeconds(1.2);
            var pull3 = stream.PullOutputData(dur3);

            var streamDur = stream.Duration.Item2;
            var samples = streamDur.Samples(background.SampleRate);
            var data = Enumerable.Range(0, (int) samples).Select(i => background.Value);

            var samples1 = (int)dur1.Samples(stream.SampleRate);
            Assert.That(pull1.Data, Is.EqualTo(data.Take(samples1).ToList()));

            var samples2 = (int)dur2.Samples(stream.SampleRate);
            Assert.That(pull2.Data, Is.EqualTo(data.Skip(samples1).Take(samples2).ToList()));

            var samples3 = (int)dur3.Samples(stream.SampleRate);
            Assert.That(pull3.Data, Is.EqualTo(data.Skip(samples1).Skip(samples2).Take(samples3).ToList()));
        }

        [Test]
        public void PullsOutputDataIndefinitely()
        {
            var dur = TimeSpan.FromSeconds(0.23);

            var samples = (int) dur.Samples(background.SampleRate);
            var expected = Enumerable.Range(0, samples).Select(i => background.Value).ToList();

            for (int i = 0; i < 100; i++)
            {
                var pull = indefiniteStream.PullOutputData(dur);
                Assert.AreEqual(expected, pull.Data);
            }
        }

        [Test]
        public void ShouldSetIsAtEnd()
        {
            Assert.IsFalse(stream.IsAtEnd);

            stream.PullOutputData(new TimeSpan(((TimeSpan)stream.Duration).Ticks / 2));
            Assert.IsFalse(stream.IsAtEnd);

            stream.PullOutputData(new TimeSpan(((TimeSpan)stream.Duration).Ticks / 2));
            Assert.IsTrue(stream.IsAtEnd);
        }
    }

    [TestFixture]
    class SequenceOutputStreamTest
    {
        IOutputStream stream1;
        IOutputStream stream2;
        IOutputData seqData;
        SequenceOutputStream seqStream;

        [SetUp]
        public void SetUp()
        {
            const int srate = 1000;
            IList<IMeasurement> list = Enumerable.Range(0, srate * 2).Select(i => new Measurement(i, "V") as IMeasurement).ToList();
            var data = new OutputData(list, new Measurement(srate, "Hz"));

            var stim1 = new RenderedStimulus("stim1", new Dictionary<string, object>(), data);
            stream1 = new StimulusOutputStream(stim1, TimeSpan.FromSeconds(0.1));

            var stim2 = new RenderedStimulus("stim2", new Dictionary<string, object>(), data);
            stream2 = new StimulusOutputStream(stim2, TimeSpan.FromSeconds(0.1));

            seqData = data.Concat(data);

            seqStream = new SequenceOutputStream();
            seqStream.Add(stream1);
            seqStream.Add(stream2);
        }

        [Test]
        public void PullsOutputData()
        {
            var dur1 = new TimeSpan(seqData.Duration.Ticks / 3);
            var pull1 = seqStream.PullOutputData(dur1);

            var dur2 = new TimeSpan(seqData.Duration.Ticks);
            var pull2 = seqStream.PullOutputData(dur2);

            var samples1 = (int)dur1.Samples(seqStream.SampleRate);
            Assert.That(pull1.Data, Is.EqualTo(seqData.Data.Take(samples1).ToList()));

            var samples2 = (int)dur2.Samples(seqStream.SampleRate);
            Assert.That(pull2.Data, Is.EqualTo(seqData.Data.Skip(samples1).Take(samples2).ToList()));
        }
        
        [Test]
        public void ShouldPushOutputDataConfigurationToStreams()
        {
            var pull1 = seqStream.PullOutputData(new TimeSpan(seqData.Duration.Ticks / 4));
            var pull2 = seqStream.PullOutputData(new TimeSpan(seqData.Duration.Ticks / 2));

            seqStream.DidOutputData(DateTime.Now, pull1.Duration, null);
            seqStream.DidOutputData(DateTime.Now, pull2.Duration, null);

            Assert.That(stream1.OutputPosition, Is.EqualTo((TimeSpan)stream1.Duration));
            Assert.That(stream2.OutputPosition, Is.EqualTo(new TimeSpan(((TimeSpan)stream2.Duration).Ticks / 2)));
        }
    }

    [TestFixture]
    class ResponseInputStreamTests
    {
        Response response;
        ResponseInputStream stream;

        [SetUp]
        public void SetUp()
        {
            response = new Response();

            stream = new ResponseInputStream(response, Option<TimeSpan>.Some(TimeSpan.FromSeconds(3)));
        }

        [Test]
        public void PushesInputData()
        {
            var dur = stream.Duration.Item2;
            var srate = new Measurement(10000, "Hz");
            IList<IMeasurement> list = Enumerable.Range(0, (int)((double)srate.QuantityInBaseUnit * dur.TotalSeconds)).Select(i => new Measurement(i, "V") as IMeasurement).ToList();

            var dur1 = TimeSpan.FromTicks(dur.Ticks/4);
            var samples1 = (int) dur1.Samples(srate);
            var push1 = new InputData(list.Take(samples1), srate, DateTime.Now);
            stream.PushInputData(push1);

            var dur2 = TimeSpan.FromTicks(dur.Ticks/4);
            var samples2 = (int) dur2.Samples(srate);
            var push2 = new InputData(list.Skip(samples1).Take(samples2), srate, DateTime.Now);
            stream.PushInputData(push2);

            var dur3 = TimeSpan.FromTicks(dur.Ticks/2);
            var samples3 = (int) dur3.Samples(srate);
            var push3 = new InputData(list.Skip(samples1 + samples2).Take(samples3), srate, DateTime.Now);
            stream.PushInputData(push3);

            Assert.AreEqual(list, response.Data);
        }

        [Test]
        public void ShouldSetIsAtEnd()
        {
            var dur = new TimeSpan(stream.Duration.Item2.Ticks / 2);
            var srate = new Measurement(10000, "Hz");
            IList<IMeasurement> list = Enumerable.Range(0, (int)((double)srate.QuantityInBaseUnit * dur.TotalSeconds)).Select(i => new Measurement(i, "V") as IMeasurement).ToList();
            var push1 = new InputData(list, srate, DateTime.Now);
            var push2 = new InputData(list, srate, DateTime.Now);

            Assert.IsFalse(stream.IsAtEnd);

            stream.PushInputData(push1);
            Assert.IsFalse(stream.IsAtEnd);

            stream.PushInputData(push2);
            Assert.IsTrue(stream.IsAtEnd);
        }
    }

    [TestFixture]
    class SequenceOutputStreamTests
    {
        IInputStream stream1;
        IInputStream stream2;
        SequenceInputStream seqStream;

        [SetUp]
        public void SetUp()
        {
            stream1 = new NullInputStream(Option<TimeSpan>.Some(TimeSpan.FromSeconds(0.1)));
            stream2 = new NullInputStream(Option<TimeSpan>.Some(TimeSpan.FromSeconds(0.1)));

            seqStream = new SequenceInputStream();
            seqStream.Add(stream1);
            seqStream.Add(stream2);
        }

        [Test]
        public void PushesInputData()
        {
            var dur = seqStream.Duration.Item2;
            var srate = new Measurement(10000, "Hz");
            IList<IMeasurement> list = Enumerable.Range(0, (int)((double)srate.QuantityInBaseUnit * dur.TotalSeconds)).Select(i => new Measurement(i, "V") as IMeasurement).ToList();

            var dur1 = TimeSpan.FromTicks(dur.Ticks / 4);
            var samples1 = (int)dur1.Samples(srate);
            var push1 = new InputData(list.Take(samples1), srate, DateTime.Now);
            seqStream.PushInputData(push1);

            var dur2 = TimeSpan.FromTicks(dur.Ticks / 2);
            var samples2 = (int)dur2.Samples(srate);
            var push2 = new InputData(list.Skip(samples1).Take(samples2), srate, DateTime.Now);
            seqStream.PushInputData(push2);

            Assert.That(stream1.Position, Is.EqualTo((TimeSpan)stream1.Duration));
            Assert.That(stream2.Position, Is.EqualTo(new TimeSpan(((TimeSpan)stream2.Duration).Ticks / 2)));
        }
    }
}
