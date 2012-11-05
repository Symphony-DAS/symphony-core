using System;
using System.Collections.Generic;

namespace Symphony.Core
{
    /// <summary>
    /// Class to take Epoch instances and persist them in some fashion.
    /// 
    /// EpochPersistor is stateful;  Epochs are persisted to the currently open EpochGroup.
    /// </summary>
    public abstract class EpochPersistor
    {
        protected const uint _persistenceVersion = 1;

        private readonly Func<Guid> _guidGenerator;
        protected Func<Guid> GuidGenerator
        {
            get { return _guidGenerator; }
        }

        protected EpochPersistor() : this(Guid.NewGuid) { }

        protected EpochPersistor(Func<Guid> guidGenerator)
        {
            _guidGenerator = guidGenerator;
        }

        /// <summary>
        /// Begins a new Epoch Group, a logical group of consecutive Epochs. EpochGroups may be nested. Nested EpochGroups implicitly define their
        /// containing EpochGroup as their parent.
        /// </summary>
        /// <param name="label">Epoch group label</param>
        /// <param name="source">Identifier of the Source for this EpochGroup.</param>
        /// <param name="keywords">Array of keyword tags associated with this epoch</param>
        /// <param name="properties">Properties (key-value metadata) associated with this EpochGroup</param>
        /// <param name="identifier">Universally Unique Identifier for this Epoch. Other persited EpochGroups may refer to this EpochGroup by identifier, e.g. as a parent.</param>
        /// <param name="startTime">EpochGroup start time</param>
        public virtual void BeginEpochGroup(string label,
            string source,
            string[] keywords,
            IDictionary<string, object> properties,
            Guid identifier,
            DateTimeOffset startTime)
        {

            double timeZoneOffset = startTime.Offset.TotalHours;

            EpochGroupNestCount += 1;
            WriteEpochGroupStart(label, source, keywords, properties, identifier, startTime.UtcDateTime, timeZoneOffset);
        }

        protected int EpochGroupNestCount { get; set; }

        /// <summary>
        /// A prefix associated as a prefix to the file or group of files
        /// </summary>
        public string AssociatedFilePrefix { get; set; }

        public static uint Version
        {
            get { return _persistenceVersion; }
        }

        /// <summary>
        /// Closes output to this persistor.
        /// </summary>
        public virtual void CloseDocument()
        {
            Console.WriteLine("Close");
        }

        public void Close()
        {
            while (EpochGroupNestCount > 0)
            {
                EndEpochGroup();
            }

            CloseDocument();
        }

        /// <summary>
        /// Serialize an Epoch instance to some kind of persistent medium (file/database/etc).
        /// </summary>
        /// <param name="e">The Epoch to serialize.</param>
        public virtual void Serialize(Epoch e)
        {
            Serialize(e, null);
        }

        /// <summary>
        /// Serialize an Epoch instance to some kind of persistent medium (file/database/etc).
        /// </summary>
        /// <param name="e">The Epoch to serialize.</param>
        /// <param name="fileTag">File tag for associated external file. May be null to indicate no associated file</param>
        public virtual void Serialize(Epoch e, string fileTag)
        {
            // write e.ProtocolID;
            WriteEpochStart(e, e.ProtocolID, e.StartTime, fileTag);

            WriteBackground(e, e.Background);

            WriteProtocolParams(e, e.ProtocolParameters);

            WriteKeywords(e, e.Keywords);

            WriteStimuliStart(e);
            WriteStimuli(e);
            WriteStimuliEnd(e);

            WriteResponsesStart(e);
            WriteResponses(e);
            WriteResponsesEnd(e);

            WriteEpochEnd(e);
        }

        protected virtual void WriteKeywords(Epoch epoch, ISet<string> keywords)
        {
            foreach (var kw in keywords)
            {
                WriteKeyword(epoch, kw);
            }
        }

        protected virtual void WriteKeyword(Epoch epoch, string kw)
        {
            Console.WriteLine("Epoch keyword: {0}", kw);
        }

        /// <summary>
        /// Ends the current Epoch Group.
        /// </summary>
        public virtual void EndEpochGroup()
        {
            DateTimeOffset endTime = DateTimeOffset.Now;
            EndEpochGroup(endTime);
        }

        public virtual void EndEpochGroup(DateTimeOffset endTime)
        {
            WriteEpochGroupEnd(endTime);
            EpochGroupNestCount -= 1;
        }

        protected virtual void WriteEpochGroupStart(string label, string source, string[] keywords, IDictionary<string, object> properties, Guid identifier, DateTimeOffset startTime, double timeZoneOffset)
        {
            Console.WriteLine("EpochGroup start: {0} parents sourceHierarchy {1} (UTC {2})", label, startTime, timeZoneOffset);
        }

        protected virtual void WriteEpochGroupEnd(DateTimeOffset endTime)
        {
            Console.WriteLine("EpochGroup end: {0}", endTime);
        }

        protected virtual void WriteEpochStart(Epoch e,
            string protocolID,
            Maybe<DateTimeOffset> startTime,
            string fileTag)
        {
            Console.WriteLine("Epoch start: {0} {1}", protocolID, (startTime.Item1 ? startTime.Item2.ToString() : "<No>"), (fileTag != null ? fileTag : "<<no file tag>>"));
        }

        protected virtual void WriteBackground(Epoch e, IDictionary<IExternalDevice, Epoch.EpochBackground> background)
        {
            foreach (var ed in background.Keys)
                WriteBackgroundElement(e, ed, background[ed]);
        }
        protected virtual void WriteBackgroundElement(Epoch e, IExternalDevice ed, Epoch.EpochBackground bg)
        {
            Console.WriteLine("Epoch background element for device {0} for measurement {1}, samplerate {2}",
                              ed.Name, bg.Background.ToString(), bg.SampleRate.ToString());
        }

        protected virtual void WriteProtocolParams(Epoch e, IDictionary<string, object> protoParams)
        {
            foreach (var k in protoParams.Keys)
                WriteProtocolParam(e, k, protoParams[k]);
        }
        protected virtual void WriteProtocolParam(Epoch e, string k, object v)
        {
            Console.WriteLine("ProtocolParam: {0}={1}", k, v);
        }

        protected virtual void WriteStimuliStart(Epoch e)
        {
            Console.WriteLine("Epoch stimuli start");
        }
        protected virtual void WriteStimuli(Epoch e)
        {
            foreach (var ed in e.Stimuli.Keys)
            {
                var s = e.Stimuli[ed];
                WriteStimulus(e, ed, s);
            }
        }
        protected virtual void WriteStimulus(Epoch e, IExternalDevice ed, IStimulus s)
        {
            // write s.StimulusID
            Console.WriteLine("Epoch stimulus id {0} for device {1}", s.StimulusID, ed.Name);

            WriteStimulusParameters(e, ed, s, s.Parameters);
        }
        protected virtual void WriteStimulusParameters(Epoch e, IExternalDevice ed, IStimulus s, IDictionary<string, object> p)
        {
            foreach (var k in s.Parameters.Keys)
            {
                var v = s.Parameters[k];

                WriteStimulusParameter(e, ed, s, k, v);
            }
        }
        protected virtual void WriteStimulusParameter(Epoch e, IExternalDevice ed, IStimulus s, string k, object v)
        {
            Console.WriteLine("Epoch stimulus parameter {0}={1}", k, v);
        }
        protected virtual void WriteStimuliEnd(Epoch e)
        {
            Console.WriteLine("Epoch stimuli end");
        }

        protected virtual void WriteResponsesStart(Epoch e)
        {
            Console.WriteLine("Epoch responses start");
        }
        protected virtual void WriteResponses(Epoch e)
        {
            foreach (var ed in e.Responses.Keys)
            {
                var r = e.Responses[ed];

                WriteResponse(e, ed, r);
            }
        }
        protected virtual void WriteResponse(Epoch e, IExternalDevice ed, Response r)
        {
            // write r.Data
        }
        protected virtual void WriteResponsesEnd(Epoch e)
        {
            Console.WriteLine("Epoch responses end");
        }

        protected virtual void WriteEpochEnd(Epoch e)
        {
            Console.WriteLine("Epoch end");
        }

    }
}