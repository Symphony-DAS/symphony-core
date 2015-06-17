using System;
using System.Collections.Generic;

namespace Symphony.Core
{
    public interface IPersistor
    {
        void Close(DateTimeOffset endTime);
        IPersistentExperiment Experiment { get; }
        IPersistentSource AddSource(string label, IPersistentSource parent);
        IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source, DateTimeOffset startTime);
        IPersistentEpochGroup EndEpochGroup(DateTimeOffset endTime);
        IPersistentEpoch Serialize(Epoch epoch);
        void Delete(IPersistentEntity entity);
    }

    public interface IPersistentEntity
    {
        string ID { get; }
    }

    public interface IAnnotatablePersistentEntity : IPersistentEntity
    {
        IEnumerable<KeyValuePair<string, object>> Properties { get; }
        void AddProperty(string key, object value);
        void RemoveProperty(string key);
        IEnumerable<string> Keywords { get; }
        void AddKeyword(string keyword);
        void RemoveKeyword(string keyword);
        IEnumerable<Note> Notes { get; }
        Note AddNote(DateTimeOffset time, string text);
    }

    public interface IPersistentSource : IAnnotatablePersistentEntity
    {
        string Label { get; }
        IEnumerable<IPersistentSource> Sources { get; }
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }
    }

    public interface ITimelinePersistentEntity : IAnnotatablePersistentEntity
    {
        DateTimeOffset StartTime { get; }
        DateTimeOffset? EndTime { get; }
    }

    public interface IPersistentExperiment : ITimelinePersistentEntity
    {
        string Purpose { get; }
        IEnumerable<IPersistentSource> Sources { get; }
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }
    }

    public interface IPersistentEpochGroup : ITimelinePersistentEntity
    {
        string Label { get; }
        IPersistentSource Source { get; }
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }
        IEnumerable<IPersistentEpoch> Epochs { get; } 
    }

    public interface IPersistentEpoch : ITimelinePersistentEntity
    {
        string ProtocolID { get; }
        TimeSpan Duration { get; }
        IEnumerable<KeyValuePair<string, object>> ProtocolParameters { get; }
        IEnumerable<IPersistentResponse> Responses { get; }
        IEnumerable<IPersistentStimulus> Stimuli { get; } 
    }

    public interface IPersistentResponse : IPersistentEntity
    {
        string DeviceName { get; }
        IEnumerable<IMeasurement> Data { get; } 
    }

    public interface IPersistentStimulus : IPersistentEntity
    {
        
    }

    public class Note
    {
        public DateTimeOffset Time { get; private set; }
        public string Text { get; private set; }

        public Note(DateTimeOffset time, string text)
        {
            Time = time;
            Text = text;
        }
    }
}
