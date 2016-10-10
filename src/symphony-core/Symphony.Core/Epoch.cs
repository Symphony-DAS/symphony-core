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
        /// <param name="parameters">Protocol parameters of the Epoch</param>
        public Epoch(string protocolID, IDictionary<string, object> parameters)
        {
            ProtocolID = protocolID;
            ProtocolParameters = parameters;
            Responses = new Dictionary<IExternalDevice, Response>();
            Stimuli = new Dictionary<IExternalDevice, IStimulus>();
            Backgrounds = new Dictionary<IExternalDevice, Background>();
            Properties = new Dictionary<string, object>();
            Keywords = new HashSet<string>();
            ShouldWaitForTrigger = false;
            ShouldBePersisted = true;
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
        /// Controller. The default value is false.
        /// </summary>
        public bool ShouldWaitForTrigger { get; set; }

        /// <summary>
        /// Indicates if this Epoch should be persisted by the EpochPersistor upon completion. The default
        /// value is true.
        /// </summary>
        public bool ShouldBePersisted { get; set; }

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

        public void SetBackground(IExternalDevice dev, IMeasurement background, IMeasurement sampleRate)
        {
            Backgrounds[dev] = new Background(background, sampleRate);
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
        /// An Epoch's duration is the duration of its longest stimulus or response. If
        /// this Epoch is indefinite, result is a Option.None.
        /// </summary>
        public Option<TimeSpan> Duration
        {
            get
            {
                if (IsIndefinite)
                    return Option<TimeSpan>.None();

                TimeSpan d1 = Stimuli.Any() ? Stimuli.Values.Max(s => (TimeSpan) s.Duration) : TimeSpan.Zero;
                TimeSpan d2 = Responses.Any() ? Responses.Values.Max(r => r.Duration) : TimeSpan.Zero;

                return Option<TimeSpan>.Some(d1 > d2 ? d1 : d2);
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
                times.AddRange(Backgrounds.Values.Where(b => b.StartTime).Select(b => b.StartTime).ToList());
                times.Sort((s1, s2) => ((DateTimeOffset)s1).CompareTo(s2));

                return times.Any() ? times.First() : Maybe<DateTimeOffset>.No();
            }
        }

        public IDictionary<string, object> Properties { get; private set; }

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
        /// A single enumerable of Measurement that is the concatenation of the DataBlocks' data of this 
        /// Stimulus. The presence of this property, i.e. Option Yes (true), indicates that this data should
        /// be persisted with the Stimulus. In general, Stimulus data can be regenerated solely by its parameters
        /// and thus the presence of this property is unnecessary and will needlessly increase file size.
        /// </summary>
        Option<IEnumerable<IMeasurement>> Data { get; }
        
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

        public abstract Option<IEnumerable<IMeasurement>> Data { get; } 

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
    /// Describes a simple "background" in the absence of a stimulus.
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
            OutputConfigurationSpanList = new List<IConfigurationSpan>();
            StartTime = Maybe<DateTimeOffset>.No();
        }

        /// <summary>
        /// Background measurement.
        /// </summary>
        public IMeasurement Value { get; private set; }

        /// <summary>
        /// Sample rate for generated stimulus data.
        /// </summary>
        public IMeasurement SampleRate { get; private set; }

        /// <summary>
        /// The approximate time this background began being processed by the DAQController, or
        /// Maybe.No if this stimulus has not yet started processing. 
        /// </summary>
        public Maybe<DateTimeOffset> StartTime { get; private set; }

        private IList<IConfigurationSpan> OutputConfigurationSpanList
        {
            get;
            set;
        }

        //TODO comment
        public IEnumerable<IConfigurationSpan> OutputConfigurationSpans
        {
            get
            {
                return OutputConfigurationSpanList;
            }
        }

        /// <summary>
        /// Informs this background that a segment of its data was pushed "to the wire". This method expects to be called
        /// in the sequence that data was output.
        /// </summary>
        /// <param name="outputTime">Approximate time the data was written "to the wire"</param>
        /// <param name="timeSpan">Duration of the data that was written</param>
        /// <param name="configuration">Pipeline node configuration(s) of nodes that processed the outgoing data</param>
        public void DidOutputData(DateTimeOffset outputTime, TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            if (_outputTimes.Any(t => outputTime < t))
                throw new ArgumentException("Output time is out of sequence", "outputTime");

            _outputTimes.Add(outputTime);

            if (!StartTime)
            {
                StartTime = Maybe<DateTimeOffset>.Some(outputTime);
            }

            OutputConfigurationSpanList.Add(new ConfigurationSpan(timeSpan, configuration));
        }

        private readonly ISet<DateTimeOffset> _outputTimes = new HashSet<DateTimeOffset>();
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

        public override Option<IEnumerable<IMeasurement>> Data
        {
            get { return Option<IEnumerable<IMeasurement>>.None(); }
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
        private readonly IOutputData _data;
        private readonly Option<TimeSpan> _duration;

        /// <summary>
        /// Constructs a new RenderedStimulus instance.
        /// </summary>
        /// <param name="stimulusID">Stimulus plugin ID</param>
        /// <param name="parameters">Stimulus parameters</param>
        /// <param name="data">Pre-rendered stimulus data</param>
        /// <param name="duration">Duration of stimulus, clipping and/or repeating data as necessary</param>
        /// <exception cref="MeasurementIncompatibilityException">If data measurements do not have homogenous BaseUnits</exception>
        public RenderedStimulus(string stimulusID, IDictionary<string, object> parameters, IOutputData data, Option<TimeSpan> duration)
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

            ShouldDataBePersisted = false;
        }

        /// <summary>
        /// Constructs a new RenderedStimulus instance with duration equal to the specified stimulus data.
        /// </summary>
        /// <param name="stimulusID">Stimulus plugin ID</param>
        /// <param name="parameters">Stimulus parameters</param>
        /// <param name="data">Pre-rendered stimulus data</param>
        public RenderedStimulus(string stimulusID, IDictionary<string, object> parameters, IOutputData data)
            : this(stimulusID, parameters, data, Option<TimeSpan>.Some(data.Duration))
        {
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

        /// <summary>
        /// Indicates if this Stimulus' data should be persisted upon completion. The default value is false.
        /// </summary>
        public bool ShouldDataBePersisted { get; set; }

        public override Option<IEnumerable<IMeasurement>> Data
        {
            get
            {
                return ShouldDataBePersisted
                           ? Option<IEnumerable<IMeasurement>>.Some(_data.Data)
                           : Option<IEnumerable<IMeasurement>>.None();
            }
        }

        public override IMeasurement SampleRate
        {
            get { return _data.SampleRate; }
        }
    }

    /// <summary>
    /// An IStimulus implementation that combines multiple stimuli of equal duration into a single stimulus of
    /// the same duration. The stimuli are combine according to a specified function.
    /// </summary>
    public class CombinedStimulus : Stimulus
    {
        /// <summary>
        /// A function that specifies how to combine data blocks of the underlying stimuli.
        /// </summary>
        public delegate IOutputData CombineProc(IDictionary<IStimulus, IOutputData> data);

        private readonly CombineProc _combine;

        /// <summary>
        /// A simple CombineProc that combines data blocks by adding them, producing a stimulus equal to the sum 
        /// of the underlying stimuli.
        /// </summary>
        public static CombineProc Add = data =>
            data.Values.Aggregate<IOutputData, IOutputData>(null,
                                                            (current, d) => current == null
                                                                ? d
                                                                : current.Zip(d, (m1, m2) => MeasurementPool.GetMeasurement(m1.QuantityInBaseUnits + m2.QuantityInBaseUnits, 0, m1.BaseUnits)));

        /// <summary>
        /// A simple CombineProc that combines data blocks by subtracting them, producing a stimulus equal to the
        /// difference of the underlying stimuli.
        /// </summary>
        public static CombineProc Subtract = data =>
            data.Values.Aggregate<IOutputData, IOutputData>(null,
                                                            (current, d) => current == null
                                                                ? d
                                                                : current.Zip(d, (m1, m2) => MeasurementPool.GetMeasurement(m1.QuantityInBaseUnits - m2.QuantityInBaseUnits, 0, m1.BaseUnits)));

        private readonly IEnumerable<IStimulus> _stimuli;

        /// <summary>
        /// Constructs a new CombinedStimulus instance from the specified stimuli.
        /// </summary>
        /// <param name="stimulusID">Stimulus plugin ID</param>
        /// <param name="parameters">Stimulus parameters of the combined stimulus</param>
        /// <param name="stimuli">Stimuli to combine</param>
        /// <param name="combine">Function specifing how to combine the stimuli</param>
        /// <exception cref="ArgumentException">If provided stimuli do not have uniform duration, units, or sample rate</exception>
        public CombinedStimulus(string stimulusID, IDictionary<string, object> parameters, IEnumerable<IStimulus> stimuli, CombineProc combine)
            : base(stimulusID, stimuli.Select(s => s.Units).FirstOrDefault(), parameters.Concat(CombineParameters(stimuli)).ToDictionary(kv => kv.Key, kv => kv.Value))
        {
            if (stimuli.Select(s => s.Duration).Distinct().Count() > 1)
                throw new ArgumentException("All stimulus durations must be equal", "stimuli");

            if (stimuli.Select(s => s.Units).Distinct().Count() > 1)
                throw new ArgumentException("All stimulus units must be equal", "stimuli");

            if (stimuli.Select(s => s.SampleRate).Distinct().Count() > 1)
                throw new ArgumentException("All stimulus sample rates must be equal", "stimuli");

            if (stimuli.Any(s => s.Data.IsSome()))
                throw new ArgumentException("Cannot combine stimuli that require data persistence", "stimuli");

            _combine = combine;
            _stimuli = stimuli;
        }

        private static IDictionary<string, object> CombineParameters(IEnumerable<IStimulus> stimuli)
        {
            var parameters = new Dictionary<string, object>();

            int i = 0;
            foreach (var stim in stimuli)
            {
                string prefix = "stim" + i + "_";
                parameters.Add(prefix + "stimulusID", stim.StimulusID);
                foreach (var param in stim.Parameters)
                {
                    parameters.Add(prefix + param.Key, param.Value);
                }
                i++;
            }

            return parameters;
        }

        public override IEnumerable<IOutputData> DataBlocks(TimeSpan blockDuration)
        {
            var enumerators = _stimuli.ToDictionary(s => s, s => s.DataBlocks(blockDuration).GetEnumerator());

            while (enumerators.All(e => e.Value.MoveNext()))
            {
                yield return _combine(enumerators.ToDictionary(e => e.Key, e => e.Value.Current));
            }
        }

        public override Option<TimeSpan> Duration
        {
            get { return _stimuli.Select(s => s.Duration).FirstOrDefault(); }
        }

        public override Option<IEnumerable<IMeasurement>> Data
        {
            get { return Option<IEnumerable<IMeasurement>>.None(); }
        } 

        public override IMeasurement SampleRate
        {
            get { return _stimuli.Select(s => s.SampleRate).FirstOrDefault(); }
        }
    }
}
