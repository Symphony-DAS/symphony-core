using System;
using System.Collections.Generic;
using System.Linq;

namespace Symphony.Core
{
    /// <summary>
    /// Interface for streams around input/output data stores of the pipeline.
    /// </summary>
    public interface IIODataStream
    {
        /// <summary>
        /// Sample rate of this stream, or null if the stream has no sample rate.
        /// </summary>
        IMeasurement SampleRate { get; }

        /// <summary>
        /// Duration of this stream, or Option.None if this stream is indefinite.
        /// </summary>
        Option<TimeSpan> Duration { get; }

        /// <summary>
        /// Current position within this stream.
        /// </summary>
        TimeSpan Position { get; }

        /// <summary>
        /// A flag indicating if this stream's position has reached the end of the stream. This
        /// flag is never true for an indefinite stream.
        /// </summary>
        bool IsAtEnd { get; }
    }

    /// <summary>
    /// Interface for streams around data sources of the output pipeline.
    /// </summary>
    public interface IOutputDataStream : IIODataStream
    {
        /// <summary>
        /// Pulls output data from this stream and advances the stream position accordingly. The 
        /// result will have a duration but it may not be equal to the requested duration.
        /// </summary>
        /// <param name="duration">Requested duration</param>
        /// <returns>Output data with a duration</returns>
        /// <exception cref="InvalidOperationException">If the stream is at its end</exception>
        IOutputData PullOutputData(TimeSpan duration);

        /// <summary>
        /// Informs this stream that data was output by the Symphony.Core output pipeline.
        /// </summary>
        /// <param name="outputTime">Approximate time the data was written "to the wire"</param>
        /// <param name="timeSpan">Duration of the output data segment</param>
        /// <param name="configuration">Pipeline node configuration(s) for nodes that processed the outgoing data</param>
        void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration);

        /// <summary>
        /// Current position of data that was output by the pipeline. OutputPosition can never exceed
        /// the stream Position.
        /// </summary>
        TimeSpan OutputPosition { get; }

        /// <summary>
        /// A flag indicating if all data pulled from this stream has been output by the pipeline.
        /// </summary>
        bool IsOutputAtEnd { get; }
    }

    /// <summary>
    /// Interface for streams around data sinks of the input pipeline.
    /// </summary>
    public interface IInputDataStream : IIODataStream
    {
        /// <summary>
        /// Pushes input data to this stream and advances the stream position accordingly.
        /// </summary>
        /// <param name="inData">Input data to push</param>
        /// <exception cref="ArgumentException">If the pushed data would exceed the streams remaining duration</exception>
        void PushInputData(IInputData inData);
    }

    /// <summary>
    /// A concatenation of output data streams where concatenated streams are traversed in FIFO ordered.
    /// 
    /// <para>A SequenceOutputDataStream will only hold reference to an underlying stream long enough
    /// for the stream to be output by the pipeline. Thus the state of a SequenceOutputDataStream will 
    /// only reflect the state of the underlying streams that it has not yet released.
    /// </para>
    /// </summary>
    public class SequenceOutputDataStream : IOutputDataStream
    {
        /// <summary>
        /// Streams in the sequence not yet exhausted.
        /// </summary>
        private Queue<IOutputDataStream> UnendedStreams { get; set; }

        /// <summary>
        /// Streams that have been exhausted but are waiting to be informed that their pulled data has
        /// been output by the pipeline.
        /// </summary>
        private Queue<IOutputDataStream> EndedStreams { get; set; }

        private IEnumerable<IOutputDataStream> Streams
        {
            get { return EndedStreams.Concat(UnendedStreams); }
        }

        public SequenceOutputDataStream()
        {
            UnendedStreams = new Queue<IOutputDataStream>();
            EndedStreams = new Queue<IOutputDataStream>();
            IsAddingCompleted = false;
        }

        public virtual bool IsAddingCompleted { get; private set; }

        public virtual void CompleteAdding()
        {
            IsAddingCompleted = true;
        }

        /// <summary>
        /// Adds an IOutputDataStream to the end of the sequence.
        /// </summary>
        /// <param name="stream">Stream to add to the end of the sequence</param>
        public virtual void Add(IOutputDataStream stream)
        {
            if (IsAddingCompleted)
                throw new InvalidOperationException("Stream marked as adding complete");

            if (stream.SampleRate != null && SampleRate != null && !Equals(stream.SampleRate, SampleRate))
                throw new ArgumentException("Sample rate mismatch");

            if (stream == this)
                throw new ArgumentException("Cannot add a sequence stream to itself");

            if (!stream.IsAtEnd)
            {
                UnendedStreams.Enqueue(stream);   
            }
        }

        public virtual bool IsAtEnd
        {
            get { return Streams.All(s => s.IsAtEnd); }
        }

        public virtual IOutputData PullOutputData(TimeSpan duration)
        {
            if (IsAtEnd)
                throw new InvalidOperationException("Pulling from a stream that has ended");

            IOutputData data = null;

            while (UnendedStreams.Any() && (data == null || data.Duration < duration))
            {
                var stream = UnendedStreams.Peek();

                data = data == null
                    ? stream.PullOutputData(duration)
                    : data.Concat(stream.PullOutputData(duration - data.Duration));

                if (stream.IsAtEnd)
                {
                    EndedStreams.Enqueue(UnendedStreams.Dequeue());
                }
            }

            return new OutputData(data, IsAddingCompleted && !UnendedStreams.Any());
        }

        public virtual void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            if (OutputPosition + timeSpan > Position)
                throw new ArgumentException("Time span would set output position past stream position", "timeSpan");

            var outputSpan = TimeSpan.Zero;

            while (outputSpan < timeSpan)
            {
                var stream = EndedStreams.Any() ? EndedStreams.Peek() : UnendedStreams.Peek();

                var span = stream.Duration && timeSpan - outputSpan > stream.Duration - stream.OutputPosition
                    ? stream.Duration - stream.OutputPosition
                    : timeSpan - outputSpan;

                stream.DidOutputData(outputTime.Add(outputSpan), span, configuration);

                outputSpan += span;

                if (stream.IsOutputAtEnd && EndedStreams.Any())
                {
                    EndedStreams.Dequeue();
                }
            }
        }

        public virtual TimeSpan OutputPosition
        {
            get { return new TimeSpan(Streams.Select(s => s.OutputPosition.Ticks).Sum()); }
        }

        public virtual bool IsOutputAtEnd
        {
            get { return Streams.All(s => s.IsOutputAtEnd); }
        }

        public virtual IMeasurement SampleRate
        {
            get { return Streams.Select(s => s.SampleRate).FirstOrDefault(r => r != null); }
        }
        
        public virtual Option<TimeSpan> Duration
        {
            get 
            { 
                Option<TimeSpan> duration;
                if (Streams.Any(s => !s.Duration))
                {
                    duration = Option<TimeSpan>.None();
                }
                else
                {
                    var totalSpan = new TimeSpan(Streams.Select(s => ((TimeSpan)s.Duration).Ticks).Sum());
                    duration = Option<TimeSpan>.Some(totalSpan);
                }
                return duration;
            }
        }
        
        public virtual TimeSpan Position
        {
            get { return new TimeSpan(Streams.Select(s => s.Position.Ticks).Sum()); }
        }
        
        /// <summary>
        /// Returns a thread-safe wrapper for SequenceOutputDataStream.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static SequenceOutputDataStream Synchronized(SequenceOutputDataStream s)
        {
            return new SyncSequenceOutputDataStream(s);
        }

        private class SyncSequenceOutputDataStream : SequenceOutputDataStream
        {
            private readonly object _syncLock = new object();

            private readonly SequenceOutputDataStream _stream;

            internal SyncSequenceOutputDataStream(SequenceOutputDataStream stream)
            {
                _stream = stream;
            }

            public override bool IsAddingCompleted
            {
                get { lock (_syncLock) return _stream.IsAddingCompleted; }
            }

            public override void CompleteAdding()
            {
                lock (_syncLock) _stream.CompleteAdding();
            }

            public override void Add(IOutputDataStream stream)
            {
                lock (_syncLock) _stream.Add(stream);
            }

            public override bool IsAtEnd
            {
                get { lock (_syncLock) return _stream.IsAtEnd; }
            }

            public override IOutputData PullOutputData(TimeSpan duration)
            {
                lock (_syncLock) return _stream.PullOutputData(duration);
            }

            public override void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration)
            {
                lock (_syncLock) _stream.DidOutputData(outputTime, timeSpan, configuration);
            }

            public override TimeSpan OutputPosition
            {
                get { lock (_syncLock) return _stream.OutputPosition; }
            }

            public override bool IsOutputAtEnd
            {
                get { lock (_syncLock) return _stream.IsOutputAtEnd; }
            }

            public override IMeasurement SampleRate
            {
                get { lock (_syncLock) return _stream.SampleRate; }
            }

            public override Option<TimeSpan> Duration
            {
                get { lock (_syncLock) return _stream.Duration; }
            }

            public override TimeSpan Position
            {
                get { lock (_syncLock) return _stream.Position; }
            }
        }
    }

    /// <summary>
    /// An output data stream around a Stimulus.
    /// </summary>
    public class StimulusOutputDataStream : IOutputDataStream
    {
        private IStimulus Stimulus { get; set; }
        private IEnumerator<IOutputData> StimulusDataEnumerator { get; set; }
        private IOutputData UnusedData { get; set; }

        /// <summary>
        /// Constructs an output data stream around a given Stimulus with a hint at the block duration
        /// to use for enumerating the stimulus data.
        /// </summary>
        /// <param name="stimulus">Stimulus to stream</param>
        /// <param name="blockDuration">Block duration to use for enumerating the stimulus data</param>
        public StimulusOutputDataStream(IStimulus stimulus, TimeSpan blockDuration)
        {
            if (stimulus == null)
                throw new ArgumentNullException("stimulus");

            if (blockDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("blockDuration");

            Stimulus = stimulus;
            StimulusDataEnumerator = stimulus.DataBlocks(blockDuration).GetEnumerator();
            Position = TimeSpan.Zero;
            OutputPosition = TimeSpan.Zero;
        }

        public TimeSpan Position { get; private set; }

        public bool IsAtEnd
        {
            get { return Duration && Position >= Duration; }
        }

        public IOutputData PullOutputData(TimeSpan duration)
        {
            if (IsAtEnd)
                throw new InvalidOperationException("Pulling from a stream that has ended");

            var data = UnusedData;

            while (data == null || data.Duration < duration)
            {
                if (!StimulusDataEnumerator.MoveNext())
                    break;

                var current = StimulusDataEnumerator.Current;

                data = data == null ? current : data.Concat(current);
            }

            if (data == null)
                throw new StimulusException("Failed to enumerate stimulus: " + Stimulus.StimulusID);

            var cons = data.SplitData(duration);
            UnusedData = cons.Rest;

            Position += cons.Head.Duration;

            return cons.Head;
        }

        public void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            if (OutputPosition + timeSpan > Position)
                throw new ArgumentException("Time span would set output position past stream position", "timeSpan");

            Stimulus.DidOutputData(outputTime, timeSpan, configuration);

            OutputPosition += timeSpan;
        }

        public TimeSpan OutputPosition { get; private set; }
        
        public bool IsOutputAtEnd
        {
            get { return Duration && OutputPosition >= Duration; }
        }

        public IMeasurement SampleRate
        {
            get { return Stimulus.SampleRate; }
        }

        public Option<TimeSpan> Duration
        {
            get { return Stimulus.Duration; }
        }
    }

    /// <summary>
    /// An output data stream filled with a single Background.
    /// </summary>
    public class BackgroundOutputDataStream : IOutputDataStream
    {
        private Background Background { get; set; }

        /// <summary>
        /// Constructs an output data stream with the given Background, of indefinite duration.
        /// </summary>
        /// <param name="background"></param>
        public BackgroundOutputDataStream(Background background)
            : this(background, Option<TimeSpan>.None())
        {
        }

        /// <summary>
        /// Constructs an output data stream with the given Background, of a given duration.
        /// </summary>
        /// <param name="background"></param>
        /// <param name="duration">Duration of stream</param>
        public BackgroundOutputDataStream(Background background, Option<TimeSpan> duration)
        {
            if (background == null)
                throw new ArgumentNullException("background");

            if (duration == null)
                throw new ArgumentNullException("duration");

            Background = background;
            Duration = duration;
            Position = TimeSpan.Zero;
            OutputPosition = TimeSpan.Zero;
        }

        public IOutputData PullOutputData(TimeSpan duration)
        {
            var dur = Duration && duration > Duration - Position 
                ? Duration - Position 
                : duration;

            var nSamples = (int) dur.Samples(SampleRate);
            var data = Enumerable.Range(0, nSamples).Select(i => Background.Value);

            Position += dur;

            return new OutputData(data, SampleRate, IsAtEnd);
        }

        public void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            if (OutputPosition + timeSpan > Position)
                throw new ArgumentException("Time span would set output position past stream position", "timeSpan");

            Background.DidOutputData(outputTime, timeSpan, configuration);

            OutputPosition += timeSpan;
        }

        public TimeSpan OutputPosition { get; private set; }
        
        public bool IsOutputAtEnd
        {
            get { return Duration && OutputPosition >= Position; }
        }

        public IMeasurement SampleRate
        {
            get { return Background.SampleRate; }
        }

        public Option<TimeSpan> Duration { get; private set; }

        public TimeSpan Position { get; private set; }

        public bool IsAtEnd
        {
            get { return Duration && Position >= Duration; }
        }
    }

    /// <summary>
    /// An output data stream filled with a Device's background value.
    /// </summary>
    public class DeviceBackgroundOutputDataStream : IOutputDataStream
    {
        private IExternalDevice Device { get; set; }

        /// <summary>
        /// Constructs an output data stream with the given Device, of indefinite duration.
        /// </summary>
        /// <param name="device"></param>
        public DeviceBackgroundOutputDataStream(IExternalDevice device)
            : this(device, Option<TimeSpan>.None())
        {
        }

        /// <summary>
        /// Constructs an output data stream with the given Device, of a given duration.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="duration">Duration of stream</param>
        public DeviceBackgroundOutputDataStream(IExternalDevice device, Option<TimeSpan> duration)
        {
            if (device == null)
                throw new ArgumentNullException("device");

            if (duration == null)
                throw new ArgumentNullException("duration");

            Device = device;
            Duration = duration;
            Position = TimeSpan.Zero;
            OutputPosition = TimeSpan.Zero;
        }

        public IOutputData PullOutputData(TimeSpan duration)
        {
            var dur = Duration && duration > Duration - Position 
                ? Duration - Position 
                : duration;

            var nSamples = (int) dur.Samples(SampleRate);
            var data = Enumerable.Range(0, nSamples).Select(i => Background);

            Position += dur;

            return new OutputData(data, SampleRate, IsAtEnd);
        }

        public void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            if (OutputPosition + timeSpan > Position)
                throw new ArgumentException("Time span would set output position past stream position", "timeSpan");

            OutputPosition += timeSpan;
        }

        public TimeSpan OutputPosition { get; private set; }
        
        public bool IsOutputAtEnd
        {
            get { return Duration && OutputPosition >= Position; }
        }

        public IMeasurement Background
        {
            get { return Device.Background; }
        }

        public IMeasurement SampleRate
        {
            get { return Device.OutputSampleRate; }
        }

        public Option<TimeSpan> Duration { get; private set; }

        public TimeSpan Position { get; private set; }

        public bool IsAtEnd
        {
            get { return Duration && Position >= Duration; }
        }
    }

    /// <summary>
    /// A concatenation of input data streams where concatenated streams are traversed in FIFO ordered.
    /// 
    /// <para>A SequenceInputDataStream will only hold reference to an underlying stream long enough
    /// for the stream to be filled. Thus the state of a SequenceInputDataStream will only reflect 
    /// the state of the underlying streams that it has not yet released.
    /// </para>
    /// </summary>
    public class SequenceInputDataStream : IInputDataStream
    {
        Queue<IInputDataStream> Streams { get; set; }

        public SequenceInputDataStream()
        {
            Streams = new Queue<IInputDataStream>();
        }

        public virtual bool IsAddingCompleted { get; private set; }

        public virtual void CompleteAdding()
        {
            IsAddingCompleted = true;
        }

        /// <summary>
        /// Adds an IInputDataStream to the end of the sequence.
        /// </summary>
        /// <param name="stream">Stream to add to the end of the sequence</param>
        public virtual void Add(IInputDataStream stream)
        {
            if (IsAddingCompleted)
                throw new InvalidOperationException("Stream marked as adding complete");

            if (stream.SampleRate != null && SampleRate != null && !Equals(stream.SampleRate, SampleRate))
                throw new ArgumentException("Sample rate mismatch");

            if (stream == this)
                throw new ArgumentException("Cannot add a sequence stream to itself");

            if (!stream.IsAtEnd)
            {
                Streams.Enqueue(stream);   
            }
        }

        public virtual void PushInputData(IInputData inData)
        {
            var srate = SampleRate;
            if (srate != null && !Equals(inData.SampleRate, srate))
                throw new ArgumentException("Data sample rate does not equal stream sample rate");

            // Account for data granularity
            var epsilon = TimeSpanExtensions.FromSamples(1, inData.SampleRate);

            if (Duration && inData.Duration > Duration - Position + epsilon)
                throw new ArgumentException("Data duration is greater than stream duration minus position");

            var unpushedData = inData;

            while (unpushedData.Duration > TimeSpan.Zero)
            {
                var stream = Streams.Peek();

                var dur = stream.Duration 
                    ? stream.Duration - stream.Position 
                    : unpushedData.Duration;

                var cons = unpushedData.SplitData(dur);

                stream.PushInputData(cons.Head);
                unpushedData = cons.Rest;

                if (stream.IsAtEnd)
                {
                    Streams.Dequeue();
                }
            }
        }

        public virtual IMeasurement SampleRate
        {
            get { return Streams.Select(s => s.SampleRate).FirstOrDefault(r => r != null); }
        }

        public virtual Option<TimeSpan> Duration
        {
            get
            {
                Option<TimeSpan> duration;
                if (Streams.Any(s => !s.Duration))
                {
                    duration = Option<TimeSpan>.None();
                }
                else
                {
                    var totalSpan = new TimeSpan(Streams.Select(s => ((TimeSpan) s.Duration).Ticks).Sum());
                    duration = Option<TimeSpan>.Some(totalSpan);
                }
                return duration;
            }
        }

        public virtual TimeSpan Position
        {
            get { return new TimeSpan(Streams.Select(s => s.Position.Ticks).Sum()); }
        }

        public virtual bool IsAtEnd
        {
            get { return Streams.All(s => s.IsAtEnd); }
        }
        
        /// <summary>
        /// Returns a thread-safe wrapper for SequenceInputDataStream.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static SequenceInputDataStream Synchronized(SequenceInputDataStream s)
        {
            return new SyncSequenceInputDataStream(s);
        }

        private class SyncSequenceInputDataStream : SequenceInputDataStream
        {
            private readonly object _syncLock = new object();

            private SequenceInputDataStream _stream;

            internal SyncSequenceInputDataStream(SequenceInputDataStream stream)
            {
                _stream = stream;
            }

            public override bool IsAddingCompleted
            {
                get { lock (_syncLock) return _stream.IsAddingCompleted; }
            }

            public override void CompleteAdding()
            {
                lock (_syncLock) _stream.CompleteAdding();
            }

            public override void Add(IInputDataStream stream)
            {
                lock (_syncLock) _stream.Add(stream);
            }

            public override bool IsAtEnd
            {
                get { lock (_syncLock) return _stream.IsAtEnd; }
            }

            public override void PushInputData(IInputData inData)
            {
                lock (_syncLock) _stream.PushInputData(inData);
            }

            public override IMeasurement SampleRate
            {
                get { lock (_syncLock) return _stream.SampleRate; }
            }

            public override Option<TimeSpan> Duration
            {
                get { lock (_syncLock) return _stream.Duration; }
            }

            public override TimeSpan Position
            {
                get { lock (_syncLock) return _stream.Position; }
            }
        }
    }

    /// <summary>
    /// An input data stream around a Response.
    /// </summary>
    public class ResponseInputDataStream : IInputDataStream
    {
        private Response Response { get; set; }

        /// <summary>
        /// Constructs an input data stream around a given Response of a given duration.
        /// </summary>
        /// <param name="response">Response to stream</param>
        /// <param name="duration">Duration of stream</param>
        public ResponseInputDataStream(Response response, Option<TimeSpan> duration)
        {
            if (response == null)
                throw new ArgumentNullException("response");

            if (duration == null)
                throw new ArgumentNullException("duration");

            Response = response;
            Duration = duration;
            Position = TimeSpan.Zero;
        }

        public void PushInputData(IInputData inData)
        {
            if (SampleRate != null && !Equals(inData.SampleRate, SampleRate))
                throw new ArgumentException("Data sample rate does not equal stream sample rate");

            // Account for data granularity
            var epsilon = TimeSpanExtensions.FromSamples(1, inData.SampleRate);

            if (Duration && inData.Duration > Duration - Position + epsilon)
                throw new ArgumentException("Data duration is greater than stream duration minus position");

            Response.AppendData(inData);

            Position += inData.Duration;
        }

        public IMeasurement SampleRate
        {
            get { return Response.SampleRate; }
        }

        public Option<TimeSpan> Duration { get; private set; }

        public TimeSpan Position { get; private set; }

        public bool IsAtEnd
        {
            get { return Duration && Position >= Duration; }
        }
    }

    /// <summary>
    /// An input data stream with no backing store. A NullInputDataStream will advance its position as 
    /// data is pushed, but the pushed data will not be stored.
    /// </summary>
    public class NullInputDataStream : IInputDataStream
    {
        /// <summary>
        /// Constructs a NullInputDataStream of indefinite duration.
        /// </summary>
        public NullInputDataStream()
            : this(Option<TimeSpan>.None())
        {
        }

        /// <summary>
        /// Constructs a NullInputDataStream of the given duration.
        /// </summary>
        /// <param name="duration">Duration of stream</param>
        public NullInputDataStream(Option<TimeSpan> duration)
        {
            if (duration == null)
                throw new ArgumentNullException("duration");

            Duration = duration;
            Position = TimeSpan.Zero;
        }

        public IMeasurement SampleRate
        {
            get { return null; }
        }

        public Option<TimeSpan> Duration { get; private set; }
        
        public TimeSpan Position { get; private set; }

        public bool IsAtEnd
        {
            get { return Duration && Position >= Duration; }
        }
        
        public void PushInputData(IInputData inData)
        {
            // Account for data granularity
            var epsilon = TimeSpanExtensions.FromSamples(1, inData.SampleRate);

            if (Duration && inData.Duration > Duration - Position + epsilon)
                throw new ArgumentException("Data duration is greater than stream duration minus position");

            Position += inData.Duration;
        }
    }
}
