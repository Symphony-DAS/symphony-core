using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
            StimulusDataEnumerators = new ConcurrentDictionary<IExternalDevice, IEnumerator<IOutputData>>();
            Background = new Dictionary<IExternalDevice, EpochBackground>();
            StartTime = Maybe<DateTimeOffset>.No();
            Keywords = new HashSet<string>();
        }


        /// <summary>
        /// Indicates if any Stimulus is indefinite. If so, the Epoch will be presented until
        /// the user cancels the Epoch or requests the Controller to move to the next queued
        /// Epoch.
        /// </summary>
        /// <see cref="Controller.CancelEpoch"/>
        /// <see cref="Controller.NextEpoch"/>
        /// <see cref="Stimulus.Duration"/>
        public bool IsIndefinite
        {
            get { return Stimuli.Values.Where(s => !(bool)s.Duration).Any(); }
        }

        /// <summary>
        /// Responses for this epoch, indexd by the ExternalDevice from which they were recorded.
        /// The Epoch declares the devices from which it *wants* to record data by the presence of
        /// that ExternalDevice in Responses.Keys. Note that the set of ExternalDevices making up the keys
        /// for this property may not (and most likely will not) be the same set of ExternalDevices
        /// in the Stimuli set.
        /// </summary>
        public IDictionary<IExternalDevice, Response> Responses { get; private set; }

        /// <summary>
        /// The stimuli for this epoch, indexed by the ExternalDevice to which the individual
        /// stimulus should be applied. Note that the set of ExternalDevices making up the keys
        /// for this property may not (and most likely will not) be the same set of ExternalDevices
        /// in the Responses set.
        /// </summary>
        public IDictionary<IExternalDevice, IStimulus> Stimuli { get; private set; }

        /// <summary>
        /// Dictionary of background values to be applied to any external devices for which no stimulus is
        /// supplied. Values are instances of Epoch.EpochBackground. The Symphony.Core output pipeline will
        /// generate stimulus data for active ExternalDevices without explicit Stimuli according to the 
        /// value and SamplingRate given in the associated EpochBackground.
        /// </summary>
        public IDictionary<IExternalDevice, EpochBackground> Background { get; private set; }

        public void SetBackground(IExternalDevice dev,
            Measurement background,
            Measurement sampleRate)
        {
            Background[dev] = new EpochBackground(background, sampleRate);
        }

        /// <summary>
        /// Flag indicating that this epoch is complete. A complete Epoch has Responses that are all greater 
        /// than or equal to the Epoch duration. Indefinite Epochs are never complete.
        /// </summary>
        public virtual bool IsComplete
        {
            get
            {
                lock (_completeLock)
                {
                    return (!IsIndefinite &&
                            Responses.Values.All(
                                (r) => r.Duration.Ticks >= ((TimeSpan) Duration).Ticks));
                }
            }
        }

        private readonly object _completeLock = new object();


        /// <summary>
        /// Describes the intended "background" for output devices that are "active" but do not have an associated
        /// Stimulus in this Epoch.
        /// </summary>
        public class EpochBackground
        {

            /// <summary>
            /// Constructs a new EpochBackground with the given background value and sample rate.
            /// </summary>
            /// <param name="background">Background measurement</param>
            /// <param name="sampleRate">Sampling rate for generated stimulus data</param>
            public EpochBackground(IMeasurement background,
                IMeasurement sampleRate)
            {
                Background = background;
                SampleRate = sampleRate;
            }

            /// <summary>
            /// Background measurement.
            /// </summary>
            public IMeasurement Background { get; private set; }
            /// <summary>
            /// Sample rate for generated stimulus data.
            /// </summary>
            public IMeasurement SampleRate { get; private set; }
        }

        /// <summary>
        /// An Epoch's duration is the duration of its longest stimulus. If
        /// this Epoch is indefinite, result is a Maybe.No.
        /// </summary>
        public Maybe<TimeSpan> Duration
        {
            get
            {
                if (IsIndefinite)
                    return Maybe<TimeSpan>.No();

                return Maybe<TimeSpan>.Yes(Stimuli
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
        /// Epoch has StartTime recorded when stimulis presentation and/or recording starts.
        /// </summary>
        public Maybe<DateTimeOffset> StartTime { get; set; }

        /// <summary>
        /// Pulls output data from the given device of the given duration.
        /// <para>If the given device has an associated Stimulus in this.Stimuli,
        /// the data is pulled from that Stimulus. If there is no associated Stimulus,
        /// data is generated according to this.Background[dev].</para>
        /// </summary>
        /// <param name="dev">Output device requesting data</param>
        /// <param name="blockDuration">Requested duration of the IOutputData</param>
        /// <returns>IOutputData intsance with duration less than or equal to blockDuration</returns>
        public IOutputData PullOutputData(IExternalDevice dev, TimeSpan blockDuration)
        {
            if (Stimuli.ContainsKey(dev))
            {
                var blockIter = StimulusDataEnumerators.GetOrAdd(dev,
                    (d) => Stimuli[d].DataBlocks(blockDuration).GetEnumerator()
                );

                IOutputData stimData = null;
                while (stimData == null || stimData.Duration < blockDuration)
                {
                    if (!blockIter.MoveNext())
                    {
                        break;
                    }

                    stimData =
                        stimData == null ? blockIter.Current : stimData.Concat(blockIter.Current);
                }

                if (stimData == null)
                {
                    return BackgroundDataForDevice(dev, blockDuration);

                }

                if (stimData.Duration < blockDuration)
                {
                    var remainingDuration = blockDuration - stimData.Duration;
                    stimData = stimData.Concat(BackgroundDataForDevice(dev, remainingDuration));
                }

                return stimData;
            }


            log.DebugFormat("Will send background for device {0} ({1})", dev.Name, blockDuration);
            return BackgroundDataForDevice(dev, blockDuration);
        }

        private IOutputData BackgroundDataForDevice(IExternalDevice dev, TimeSpan blockDuration)
        {
            //log.DebugFormat("Presenting Epoch background for {0}.", dev.Name);

            if (!Background.ContainsKey(dev))
                throw new ArgumentException("Epoch does not have a stimulus or background for " + dev.Name);

            //Calculate background
            var srate = Background[dev].SampleRate;
            var value = Background[dev].Background;

            IOutputData result = new OutputData(ConstantMeasurementList(blockDuration, srate, value),
                                                srate,
                                                false);

            return result;
        }

        private static IEnumerable<IMeasurement> ConstantMeasurementList(TimeSpan blockDuration, IMeasurement srate, IMeasurement value)
        {
            //log.DebugFormat("Generating constant measurment: {0} x {1} samles @ {2}", value, blockDuration.Samples(srate), srate);
            return Enumerable.Range(0, (int)blockDuration.Samples(srate))
                .Select(i => value)
                .ToList();
        }

        private ConcurrentDictionary<IExternalDevice, IEnumerator<IOutputData>> StimulusDataEnumerators { get; set; }

        public ISet<string> Keywords { get; private set; }

        private static readonly ILog log = LogManager.GetLogger(typeof(Epoch));

        /// <summary>
        /// Informs this Epoch that stimulus data was output by the Symphony.Core output pipeline.
        /// </summary>
        /// <param name="device">ExternalDevice that was the target of the output data</param>
        /// <param name="outputTime">Approximate time the data was written "to the wire"</param>
        /// <param name="duration">Duration of the output data segment</param>
        /// <param name="configuration">Pipeline node configuration(s) for nodes that processed the outgoing data</param>
        public virtual void DidOutputData(IExternalDevice device, DateTimeOffset outputTime, TimeSpan duration, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            //virtual so that we can mock it

            if (outputTime < StartTime)
                throw new ArgumentException("Data output time must be after Epoch start time", "outputTime");

            if (Stimuli.ContainsKey(device))
                Stimuli[device].DidOutputData(duration, configuration);
        }
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
        /// Duration of this stimulus. May be No (false) to indicate that this Stimulus
        /// generates data indefinitely.
        /// </summary>
        Option<TimeSpan> Duration { get; }


        /// <summary>
        /// BaseUnits for this stimulus' output data
        /// </summary>
        string Units { get; }

        //TODO comment
        IEnumerable<IConfigurationSpan> OutputConfigurationSpans { get; }

        /// <summary>
        /// Informs this stimulus that a segment of its data was pushed "to the wire"
        /// </summary>
        /// <param name="timeSpan">Duration of the data that was written</param>
        /// <param name="configuration">Pipeline node configuration(s) of nodes that processed the outgoing data</param>
        void DidOutputData(TimeSpan timeSpan, IEnumerable<IPipelineNodeConfiguration> configuration);
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
            OutputConfigurationSpanSet = new HashSet<IConfigurationSpan>();

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

        private ISet<IConfigurationSpan> OutputConfigurationSpanSet
        {
            get;
            set;
        }

        public IEnumerable<IConfigurationSpan> OutputConfigurationSpans
        {
            get
            {
                var result = OutputConfigurationSpanSet.ToList();
                result.Sort((s1, s2) => s1.Time.CompareTo(s2.Time));

                return result;
            }
        }

        public void DidOutputData(TimeSpan time, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            OutputConfigurationSpanSet.Add(new ConfigurationSpan(time, configuration));
        }
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
        /// <param name="units"></param>
        /// <param name="parameters">Stimulus parameters</param>
        /// <param name="blockRender">BlockRender delegate method</param>
        /// <param name="duration">Duration delegate method</param>
        public DelegatedStimulus(string stimulusID, string units, IDictionary<string, object> parameters, BlockRenderer blockRender, DurationCalculator duration)
            : base(stimulusID, units, parameters)
        {

            if (blockRender == null || duration == null)
            {
                throw new ArgumentException("Delegates may not be null");
            }

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

                yield return current;
                current = BlockDelegate(Parameters, blockDuration);
            }
        }

        public override Option<TimeSpan> Duration
        {
            get { return DurationDelegate(Parameters); }
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
}
