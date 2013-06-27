using System;
using System.Collections.Generic;
using System.Linq;
using log4net;

namespace Symphony.Core
{
    /// <summary>
    /// An Epoch represents a bounded region on the experimental timeline. It is roughly equivalent
    /// to a trial, but is generalized to allow for experiments that record continuously.
    /// 
    /// <para>An Epoch is the fundamental unit of data collection within the Symphony.Core framework.
    /// The Core.Controller maintains a queue of Epochs and presents them in sequence. The results
    /// from each Epoch are persisted with that Epoch, along with the parameters of the stimulus, protocol
    /// and relevant metadata.</para>
    /// 
    /// <para>The Epoch is fundamentally a mapping from external device to Stimulus data and from external device
    /// to Response.</para>
    /// </summary>
    public class Epoch
    {
        /// <summary>
        /// Convenience constructor; construct an Epoch with an empty parameters dictionary.
        /// </summary>
        /// <param name="protocolID"></param>
        public Epoch(string protocolID)
            : this(protocolID, new Dictionary<string, object>())
        {
        }


        /// <summary>
        /// Constructs a new Epoch instance.
        /// </summary>
        /// <param name="protocolID">Protocol ID of the Epoch</param>
        /// <param name="parameters">Protocol paramters of the Epoch</param>
        public Epoch(string protocolID, IDictionary<string, object> parameters)
        {
            ProtocolID = protocolID;
            ProtocolParameters = parameters;
            Responses = new Dictionary<IExternalDevice, Response>();
            Stimuli = new Dictionary<IExternalDevice, IStimulus>();
            Backgrounds = new Dictionary<IExternalDevice, Background>();
            Keywords = new HashSet<string>();
            WaitForTrigger = false;
        }


        /// <summary>
        /// Indicates if any Stimulus is indefinite. If so, the Epoch will be presented until
        /// the user requests the Controller to stop.
        /// </summary>
        /// <see cref="Controller.RequestStop"/>
        /// <see cref="Stimulus.Duration"/>
        public bool IsIndefinite
        {
            get { return Stimuli.Values.Any(s => !(bool)s.Duration); }
        }

        /// <summary>
        /// Indicates if this Epoch should wait for an external trigger before running. If this value
        /// is false, the Epoch will begin immediately after being pushed to the Controller. If this value
        /// is true, the Epoch will begin following the next external trigger after being pushed to the
        /// Controller.  
        /// </summary>
        public bool WaitForTrigger { get; set; }

        /// <summary>
        /// Responses for this epoch, indexd by the ExternalDevice from which they were recorded.
        /// The Epoch declares the devices from which it *wants* to record data by the presence of
        /// that ExternalDevice in Responses.Keys. Note that the set of ExternalDevices making up the keys
        /// for this property may not (and most likely will not) be the same set of ExternalDevices
        /// in the Stimuli set.
        /// </summary>
        public IDictionary<IExternalDevice, Response> Responses { get; set; }

        /// <summary>
        /// The stimuli for this epoch, indexed by the ExternalDevice to which the individual
        /// stimulus should be applied. Note that the set of ExternalDevices making up the keys
        /// for this property may not (and most likely will not) be the same set of ExternalDevices
        /// in the Responses set.
        /// </summary>
        public IDictionary<IExternalDevice, IStimulus> Stimuli { get; set; }

        /// <summary>
        /// Dictionary of background values to be applied to any external devices for which no stimulus is
        /// supplied. The Symphony.Core output pipeline will generate stimulus data for active ExternalDevices 
        /// without explicit Stimuli according to the value and SamplingRate given in the associated Background.
        /// </summary>
        public IDictionary<IExternalDevice, Background> Backgrounds { get; private set; }

        public void SetBackground(IExternalDevice dev,
            Measurement background,
            Measurement sampleRate)
        {
            Backgrounds[dev] = new Background(background, sampleRate);
        }

        private const double DEFAULT_BLOCK_SECONDS = 0.5;

        /// <summary>
        /// Gets a new output stream around the stimulus or background associated with the given device in
        /// this Epoch. If both a stimulus and background are associated with the same device in this Epoch,
        /// an output stream around the stimulus will always provided. The stream duration will match the 
        /// Epoch duration.
        /// </summary>
        /// <returns>An output stream for the given device, or null if this Epoch has no stimulus or
        /// background associated with the given device.</returns>
        public IOutputStream GetOutputStream(IExternalDevice device)
        {
            return GetOutputStream(device, TimeSpan.FromSeconds(DEFAULT_BLOCK_SECONDS));
        }

        /// <summary>
        /// Gets a new output stream around the stimulus or background associated with the given device in
        /// this Epoch, with a hint of the block duration to use while enumerating the backing store.
        /// </summary>
        /// <param name="blockDuration">A hint of the stream enumerator block duration</param>
        /// <returns>An output stream for the given device, or null if this Epoch has no stimulus or
        /// background associated with the given device.</returns>
        public IOutputStream GetOutputStream(IExternalDevice device, TimeSpan blockDuration)
        {
            IOutputStream stream = null;

            if (Stimuli.ContainsKey(device))
            {
                IStimulus stimulus;
                Stimuli.TryGetValue(device, out stimulus);

                stream = new StimulusOutputStream(stimulus, blockDuration);
            }
            else if (Backgrounds.ContainsKey(device))
            {
                Background background;
                Backgrounds.TryGetValue(device, out background);

                stream = new BackgroundOutputStream(background, Duration);
            }

            return stream;
        }

        /// <summary>
        /// Gets a new input stream around the response associated with the given device in this Epoch. The
        /// stream duration will match the Epoch duration.
        /// </summary>
        /// <param name="device"></param>
        /// <returns>An input stream for the given device, or null if this Epoch has no response
        /// associated with the given device.</returns>
        public IInputStream GetInputStream(IExternalDevice device)
        {
            IInputStream stream = null;

            if (Responses.ContainsKey(device))
            {
                Response response;
                Responses.TryGetValue(device, out response);

                stream = new ResponseInputStream(response, Duration);
            }

            return stream;
        }

        /// <summary>
        /// Flag indicating that this epoch is complete. A complete Epoch has stimuli that are all complete
        /// and responses that are all greater than or equal to the Epoch duration. Indefinite Epochs are 
        /// never complete.
        /// </summary>
        /// <seealso cref="IStimulus.IsComplete"/>
        public virtual bool IsComplete
        {
            get
            {
                return (Stimuli.Values.All(s => s.IsComplete)
                        && Responses.Values.All((r) => r.Duration.Ticks >= ((TimeSpan) Duration).Ticks));
            }
        }

        /// <summary>
        /// An Epoch's duration is the duration of its longest stimulus. If
        /// this Epoch is indefinite, result is a Option.None.
        /// </summary>
        public Option<TimeSpan> Duration
        {
            get
            {
                if (IsIndefinite)
                    return Option<TimeSpan>.None();

                return Option<TimeSpan>.Some(Stimuli
                    .Values
                    .Max(s => (TimeSpan)s.Duration));
            }
        }

        /// <summary>
        /// The ID of the protocol describing this Epoch. 
        /// </summary>
        public string ProtocolID { get; private set; }

        /// <summary>
        /// Parameters of the Protocol for this Epoch.
        /// </summary>
        public IDictionary<string, object> ProtocolParameters { get; private set; }

        /// <summary>
        /// The earliest Stimulus start time in this Epoch, or Maybe.No if no Stimulus has been started. 
        /// </summary>
        public virtual Maybe<DateTimeOffset> StartTime
        {
            get
            {
                var times = Stimuli.Values.Where(s => s.StartTime).Select(s => s.StartTime).ToList();
                times.Sort((s1, s2) => ((DateTimeOffset)s1).CompareTo(s2));

                return times.Any() ? times.First() : Maybe<DateTimeOffset>.No();
            }
        }

        public ISet<string> Keywords { get; private set; }

        private static readonly ILog log = LogManager.GetLogger(typeof(Epoch));
    }

    /// <summary>
    /// Interface for objects that can provide data for Stimulus output to the Symphony.Core
    /// output pipeline.
    /// 
    /// <para>Symphony does not persist stimulus data. Rather it persists the identifier and parameters
    /// of each Stimulus presented during the experiment. The expectation is that user code is capable
    /// of reconstructing Stimuli given this ID and parameters. The expected identifier is a reverse
    /// domain format such as com.physionconsulting.symphony.stimulus.MyStimulusPlugin.</para>
    /// <para>In the case of stimuli that cannot be
    /// regenerated, the parameters should contain the stimulus data or a reference to that data
    /// in a persistent form. The identifier for such a stimulus should indicate that the stimulus
    /// data is persisted and the "algorithm" for finding it from the Stimulus' parameters. For example
    /// the user may define edu.university.lab.stimulus.OnDiskStimulus as the identifier of Stimuli
    /// whose data is stored on disk at a path given in the Parameters.</para>
    /// 
    /// </summary>
    public interface IStimulus
    {
        /// <summary>
        /// The identifier of this Stimulus. For stimuli that are generated algorithmically,
        /// this ID should identify the algorithm/code capable of regenerating this stimulus
        /// given Parameters.
        /// </summary>
        string StimulusID { get; }

        /// <summary>
        /// Parameters of stimulus generation
        /// </summary>
        IDictionary<string, object> Parameters { get; }

        /// <summary>
        /// Returns an enumerable over the Stimulus' data with given block duration.
        /// </summary>
        /// <param name="blockDuration">Duration of enumeration items</param>
        /// <returns>IEnumerable over this Stimulus's data, split into blockDuration chunks</returns>
        IEnumerable<IOutputData> DataBlocks(TimeSpan blockDuration);

        /// <summary>
        /// Duration of this stimulus. Option No (false) to indicate that this Stimulus
        /// generates data indefinitely.
        /// </summary>
        Option<TimeSpan> Duration { get; }
        
        /// <summary>
        /// Sample rate of this stimulus.
        /// </summary>
        IMeasurement SampleRate { get; }

        /// <summary>
        /// The approximate time this stimulus began being processed by the DAQController, or
        /// Maybe.No if this stimulus has not yet started processing. 
        /// </summary>
        Maybe<DateTimeOffset> StartTime { get; } 

        /// <summary>
        /// BaseUnits for this stimulus' output data
        /// </summary>
        string Units { get; }

        //TODO comment
        IEnumerable<IConfigurationSpan> OutputConfigurationSpans { get; }

        /// <summary>
        /// A flag indicating if this stimulus is complete. A complete stimulus has been informed that its entire
        /// duration has been pushed "to the wire". Indefinite stimulus are never complete.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Informs this stimulus that a segment of its data was pushed "to the wire". This method expects to be called
        /// in the sequence that data was output.
        /// </summary>
        /// <param name="outputTime">Approximate time the data was written "to the wire"</param>
        /// <param name="timeSpan">Duration of the data that was written</param>
        /// <param name="configuration">Pipeline node configuration(s) of nodes that processed the outgoing data</param>
        void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration);
    }

    public abstract class Stimulus : IStimulus
    {
        /// <summary>
        /// Constructs a Stimulus with the given ID and parameters.
        /// </summary>
        /// <param name="stimulusID">Stimulus generator ID</param>
        /// <param name="units">Stimulus data units</param>
        /// <param name="parameters">Parameters of stimulus generation</param>
        protected Stimulus(string stimulusID, string units, IDictionary<string, object> parameters)
        {
            if (stimulusID == null)
            {
                throw new ArgumentException("StimulusID may not be null", "stimulusID");
            }

            if (stimulusID.Length == 0)
            {
                throw new ArgumentException("StimulusID may not be empty", "stimulusID");
            }

            if (parameters == null)
            {
                throw new ArgumentException("Parameters may not be null", "Parameters");
            }

            StimulusID = stimulusID;
            Parameters = parameters;
            _units = units;
            OutputConfigurationSpanList = new List<IConfigurationSpan>();
            StartTime = Maybe<DateTimeOffset>.No();
        }

        public string Units
        {
            get { return _units; }
        }

        private readonly string _units;

        /// <summary>
        /// Identifier of this Stimulus' data generating algorithm/code/plugin.
        /// </summary>
        public string StimulusID { get; private set; }

        /// <summary>
        /// Parameters of stimulus generation for this Stimulus instance
        /// </summary>
        public IDictionary<string, object> Parameters { get; private set; }

        public abstract IEnumerable<IOutputData> DataBlocks(TimeSpan blockDuration);

        public abstract Option<TimeSpan> Duration { get; }

        /// <summary>
        /// Sample rate of this stimulus.
        /// </summary>
        public abstract IMeasurement SampleRate { get; }

        /// <summary>
        /// The approximate time this stimulus began being processed by the DAQController, or
        /// Maybe.No if this stimulus has not yet started processing. 
        /// </summary>
        public Maybe<DateTimeOffset> StartTime { get; private set; }

        private IList<IConfigurationSpan> OutputConfigurationSpanList
        {
            get;
            set;
        }

        public IEnumerable<IConfigurationSpan> OutputConfigurationSpans
        {
            get
            {
                return OutputConfigurationSpanList;
            }
        }

        private readonly object _completeLock = new object();

        public bool IsComplete
        {
            get
            {
                lock (_completeLock) return Duration && OutputConfigurationSpans.Select(s => s.Time.Ticks).Sum() >= ((TimeSpan)Duration).Ticks;
            }
        }

        public void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            if (_outputTimes.Any(t => outputTime < t))
                throw new ArgumentException("Output time is out of sequence", "outputTime");

            _outputTimes.Add(outputTime);

            if (!StartTime)
            {
                StartTime = Maybe<DateTimeOffset>.Some(outputTime);
            }

            lock (_completeLock) OutputConfigurationSpanList.Add(new ConfigurationSpan(timeSpan, configuration));
        }

        private readonly ISet<DateTimeOffset> _outputTimes = new HashSet<DateTimeOffset>();
    }

    /// <summary>
    /// Describes a simple "background" for output pipeline.
    /// </summary>
    public class Background
    {
        /// <summary>
        /// Constructs a new Background with the given value and sample rate.
        /// </summary>
        /// <param name="value">Background measurement</param>
        /// <param name="sampleRate">Sampling rate for generated stimulus data</param>
        public Background(IMeasurement value,
                          IMeasurement sampleRate)
        {
            Value = value;
            SampleRate = sampleRate;
        }

        /// <summary>
        /// Background measurement.
        /// </summary>
        public IMeasurement Value { get; private set; }

        /// <summary>
        /// Sample rate for generated stimulus data.
        /// </summary>
        public IMeasurement SampleRate { get; private set; }
    }

    public class ResponseException : SymphonyException
    {
        internal ResponseException(string msg)
            : base(msg)
        {
        }
    }

    public class StimulusException : SymphonyException
    {
        internal StimulusException(string msg)
            : base(msg)
        { }
    }


    /// <summary>
    /// The Response class represents data recorded from a single ExternalDevice for a single Epoch.
    /// </summary>
    public class Response
    {
        /// <summary>
        /// List of IInputData appended to this Response by the Symphony input pipeline. The durations of thse
        /// segments may not be homogenous.
        /// </summary>
        public IList<IInputData> DataSegments
        {
            get
            {
                var result = RawData.ToList();
                
                result.Sort((IInputData d1, IInputData d2) => d1.InputTime.CompareTo(d2.InputTime));

                return result;
            }
        }

        /// <summary>
        /// A single enumerable of Measurement that is the concatenation of the DataSegments' data of this Response.
        /// </summary>
        public IEnumerable<IMeasurement> Data
        {
            get
            {
                return DataSegments.Select(d => d.Data)
                    .Aggregate<IEnumerable<IMeasurement>, IEnumerable<IMeasurement>>(new List<IMeasurement>(), (curr, next) => curr.Concat(next));
            }
        }

        public IEnumerable<IConfigurationSpan> DataConfigurationSpans
        {
            get //TODO test (Response)
            {
                return DataSegments.Select(d => new ConfigurationSpan(d.Duration, d.Configuration));
            }
        }

        public IMeasurement SampleRate
        {
            get //TODO test (Response)
            {
                var sampleRates = DataSegments.Select(s => s.SampleRate).Distinct();

                if (sampleRates.Count() > 1)
                    throw new ResponseException("Response data segments have multiple sample rates");

                return sampleRates.FirstOrDefault();
            }
        }

        public DateTimeOffset InputTime
        {
            get { return DataSegments.Select(s => s.InputTime).FirstOrDefault(); } //TODO test (Resposne)
        }

        ISet<IInputData> RawData { get; set; }

        /// <summary>
        /// Constructs a new Response instance
        /// </summary>
        public Response()
        {
            this.RawData = new HashSet<IInputData>();
        }

        /// <summary>
        /// Appends the given IInputData to this Response.
        /// </summary>
        /// <param name="data">Data to append</param>
        virtual public void AppendData(IInputData data)
        {
            RawData.Add(data);
        }

        /// <summary>
        /// Duration of this Response, the sum of the Duration of all DataSegments.
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                return new TimeSpan(this.DataSegments.Select(x => x.Duration.Ticks).Sum());
            }
        }
    }

    /// <summary>
    /// A simple IStimulus implementation that delegates stimulus blocks and duration to delegate methods.
    /// 
    /// <para>A DelegatedStimulus is a convenient way to allow the user to specify the stimulus
    /// generation algorithm without having to write an entire IStimulus implementation. In fact,
    /// this class allows delegation to simple functions rather than objects.</para>
    /// </summary>
    public class DelegatedStimulus : Stimulus
    {

        /// <summary>
        /// Delegate that returns the next IOutputData for this stimulus given this Stimulus' parameters.
        /// The returned IOutputData should be of duration less than or equal to blockDuration.
        /// <para>The delegate should return null to indicate the end of the generated data</para>
        /// </summary>
        /// <param name="parameters">Stimulus parameters for this Stimulus</param>
        /// <param name="blockDuration">Requested block duration</param>
        /// <returns>An IOutputData instance representing the next block of stimulus of duration less than or equal to blockDuration or
        /// null to indicate end of stimulus.</returns>
        public delegate IOutputData BlockRenderer(IDictionary<string, object> parameters, TimeSpan blockDuration);

        //Returns Option(false) for indefinite
        /// <summary>
        /// Delegate that returns the total duration of the stimulus.
        /// </summary>
        /// <param name="parameters">Parameters for this stimulus</param>
        /// <returns>An Option(duration) to indicate stimulus duration or Option(false) to indicate an indefinite stimulus.</returns>
        public delegate Option<TimeSpan> DurationCalculator(IDictionary<string, object> parameters);

        /// <summary>
        /// Constructs a new DelegatedStimulus.
        /// </summary>
        /// <param name="stimulusID">Stimulus generation ID</param>
        /// <param name="units">Units of the stimulus</param>
        /// <param name="sampleRate">Sample rate of the stimulus</param>
        /// <param name="parameters">Stimulus parameters</param>
        /// <param name="blockRender">BlockRender delegate method</param>
        /// <param name="duration">Duration delegate method</param>
        public DelegatedStimulus(string stimulusID, string units, IMeasurement sampleRate, IDictionary<string, object> parameters, BlockRenderer blockRender, DurationCalculator duration)
            : base(stimulusID, units, parameters)
        {

            if (blockRender == null || duration == null)
            {
                throw new ArgumentException("Delegates may not be null");
            }

            if (sampleRate == null)
                throw new ArgumentNullException("sampleRate");

            _sampleRate = sampleRate;
            BlockDelegate = blockRender;
            DurationDelegate = duration;
        }

        private DurationCalculator DurationDelegate { get; set; }
        private BlockRenderer BlockDelegate { get; set; }

        public override IEnumerable<IOutputData> DataBlocks(TimeSpan blockDuration)
        {
            IOutputData current = BlockDelegate(Parameters, blockDuration);
            while (current != null)
            {
                if (current.Data.BaseUnits() != Units)
                    throw new StimulusException("Data units do not match stimulus units");

                if (!Equals(current.SampleRate, SampleRate))
                    throw new StimulusException("Data sample rate does not match stimulus sample rate");

                yield return current;
                current = BlockDelegate(Parameters, blockDuration);
            }
        }

        public override Option<TimeSpan> Duration
        {
            get { return DurationDelegate(Parameters); }
        }

        private readonly IMeasurement _sampleRate;

        public override IMeasurement SampleRate
        {
            get { return _sampleRate; }
        }
    }

    /// <summary>
    /// A simple IStimulus implementation that holds arbitrary data, prerendered from a plugin.
    /// </summary>
    public class RenderedStimulus : Stimulus
    {
        private IOutputData Data { get; set; }

        /// <summary>
        /// Constructs a new RenderedStimulus instance.
        /// </summary>
        /// <param name="stimulusID">Stimulus plugin ID</param>
        /// <param name="parameters">Stimulus parameters</param>
        /// <param name="data">Pre-rendered stimulus data</param>
        /// <exception cref="MeasurementIncompatibilityException">If data measurements do not have homogenous BaseUnits</exception>
        public RenderedStimulus(string stimulusID, IDictionary<string, object> parameters, IOutputData data)
            : base(stimulusID, data.Data.BaseUnits(), parameters)
        {
            if (data == null)
            {
                throw new ArgumentException("Data may not be null", "data");
            }

            if (parameters == null)
            {
                throw new ArgumentException("Parameters may not be null", "Parameters");
            }

            Data = data;
        }


        public override Option<TimeSpan> Duration
        {
            get { return Option<TimeSpan>.Some(Data.Duration); }
        }


        public override IMeasurement SampleRate
        {
            get { return Data.SampleRate; }
        }


        public override IEnumerable<IOutputData> DataBlocks(TimeSpan blockDuration)
        {
            var local = (IOutputData)Data.Clone();

            while (local.Duration > TimeSpan.Zero)
            {
                var cons = local.SplitData(blockDuration);

                local = cons.Rest;

                yield return new OutputData(cons.Head, cons.Rest.Duration <= TimeSpan.Zero);
            }
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(Epoch));
    }

    /// <summary>
    /// A simple IStimulus implementation that holds arbitrary data, prerendered from a plugin, 
    /// and repeats it for a specified duration.
    /// </summary>
    public class RepeatingRenderedStimulus : Stimulus
    {
        private readonly IOutputData _data;
        private readonly Option<TimeSpan> _duration;

        /// <summary>
        /// Constructs a new RepeatingRenderedStimulus instance.
        /// </summary>
        /// <param name="stimulusID">Stimulus plugin ID</param>
        /// <param name="parameters">Stimulus parameters</param>
        /// <param name="data">Pre-rendered stimulus data to repeat</param>
        /// <param name="duration">Duration to repeat the stimulus data</param>
        /// <exception cref="MeasurementIncompatibilityException">If data measurements do not have homogenous BaseUnits</exception>
        public RepeatingRenderedStimulus(string stimulusID, IDictionary<string, object> parameters, IOutputData data, Option<TimeSpan> duration)
            : base(stimulusID, data.Data.BaseUnits(), parameters)
        {
            if (data == null)
                throw new ArgumentException("Data may not be null", "data");

            if (parameters == null)
                throw new ArgumentException("Parameters may not be null", "parameters");

            if (duration == null)
                throw new ArgumentException("Duration may not be null", "duration");

            _data = data;
            _duration = duration;
        }

        public override IEnumerable<IOutputData> DataBlocks(TimeSpan blockDuration)
        {
            var local = (IOutputData)_data.Clone();
            var index = TimeSpan.Zero;

            bool isIndefinite = !((bool)Duration);

            while (index < Duration || isIndefinite)
            {
                var dur = blockDuration <= Duration - index || isIndefinite 
                    ? blockDuration 
                    : Duration - index;

                while (local.Duration < dur)
                {
                    local = local.Concat(_data);
                }

                var cons = local.SplitData(dur);
                local = cons.Rest;

                index = index.Add(dur);

                yield return new OutputData(cons.Head, index >= Duration && !isIndefinite);
            }
        }

        public override Option<TimeSpan> Duration
        {
            get { return _duration; }
        }

        public override IMeasurement SampleRate
        {
            get { return _data.SampleRate; }
        }
    }
}
