using System;
using System.Collections.Generic;

namespace Symphony.Core
{
    public interface IEpochPersistor
    {
        void Close();
        IEnumerable<IPeristedSource> Sources { get; }
        IPeristedSource AddSource(string label, IPeristedSource parent);
        IEnumerable<IPersistedExperiment> Experiments { get; }
        IPersistedExperiment CurrentExperiment { get; }
        void BeginExperiment(string purpose, DateTimeOffset startTime);
        void EndExperiment(DateTimeOffset endTime);
        IPersistedEpochGroup CurrentEpochGroup { get; }
        void BeginEpochGroup(string label, IPeristedSource source, DateTimeOffset startTime);
        void EndEpochGroup(DateTimeOffset endTime);
        void Delete(IPersistedEntity entity);
    }

    public interface IPersistedEntity
    {
        IEnumerable<KeyValuePair<string, object>> Properties { get; }
        void AddProperty(string key, object value);
        void RemoveProperty(string key);
        IEnumerable<string> Keywords { get; }
        void AddKeyword(string keyword);
        void RemoveKeyword(string keyword);
        IEnumerable<IPersistedNote> Notes { get; }
        IPersistedNote AddNote(DateTimeOffset time, string text);
    }

    public interface IPeristedSource : IPersistedEntity
    {
        string Label { get; set; }
        IEnumerable<IPeristedSource> Sources { get; }
        IEnumerable<IPersistedEpochGroup> EpochGroups { get; }
    }

    public interface ITimelinePersistedEntity : IPersistedEntity
    {
        DateTimeOffset StartTime { get; }
        DateTimeOffset? EndTime { get; }
    }

    public interface IPersistedExperiment : ITimelinePersistedEntity
    {
        string Purpose { get; set; }
        IEnumerable<IPersistedEpochGroup> EpochGroups { get; } 
    }

    public interface IPersistedEpochGroup : ITimelinePersistedEntity
    {
        string Label { get; set; }
        IPeristedSource Source { get; }
        IEnumerable<IPersistedEpochGroup> EpochGroups { get; }
        IEnumerable<IPersistedEpoch> Epochs { get; } 
    }

    public interface IPersistedEpoch : ITimelinePersistedEntity
    {
        IEnumerable<KeyValuePair<string, object>> ProtocolParameters { get; } 
    }

    public interface IPersistedNote
    {
        DateTimeOffset Time { get; }
        string Text { get; }
    }
}
