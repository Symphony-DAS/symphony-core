using System;
using System.Collections.Generic;

namespace Symphony.Core
{
    public interface IEpochPersistor
    {
        void Close();
        IEnumerable<ISource> Sources { get; }
        ISource AddSource(string label, ISource parent);
        IEnumerable<IExperiment> Experiments { get; }
        IExperiment CurrentExperiment { get; }
        void BeginExperiment(string purpose, DateTimeOffset startTime);
        void EndExperiment(DateTimeOffset endTime);
        IEpochGroup CurrentEpochGroup { get; }
        void BeginEpochGroup(string label, ISource source, DateTimeOffset startTime);
        void EndEpochGroup(DateTimeOffset endTime);
        void Delete(IEntity entity);
    }

    public interface IEntity
    {
        Guid Uuid { get; }
        IEnumerable<KeyValuePair<string, object>> Properties { get; } 
        void AddProperty(string key, object value);
        void RemoveProperty(string key);
        IEnumerable<string> Keywords { get; }
        void AddKeyword(string keyword);
        void RemoveKeyword(string keyword);
        IEnumerable<INote> Notes { get; } 
        INote AddNote(DateTimeOffset time, string text);
    }

    public interface ISource : IEntity
    {
        string Label { get; set; }
        IEnumerable<ISource> Sources { get; }
        IEnumerable<IEpochGroup> EpochGroups { get; }
    }

    public interface ITimelineEntity : IEntity
    {
        DateTimeOffset StartTime { get; }
        DateTimeOffset? EndTime { get; }
    }

    public interface IExperiment : ITimelineEntity
    {
        string Purpose { get; set; }
        IEnumerable<IEpochGroup> EpochGroups { get; } 
    }

    public interface IEpochGroup : ITimelineEntity
    {
        string Label { get; set; }
        ISource Source { get; }
    }

    public interface INote
    {
        DateTimeOffset Time { get; }
        string Text { get; }
    }
}
