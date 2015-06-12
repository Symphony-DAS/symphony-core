using System;
using System.Collections.Generic;

namespace Symphony.Core
{
    public interface IEpochPersistor
    {
        void Close();
        IExperiment Experiment { get; }
        ISource AddSource(string label, ISource parent);
        IEpochGroup BeginEpochGroup(string label, ISource source, DateTimeOffset startTime);
        IEpochGroup EndEpochGroup(DateTimeOffset endTime);
        void Delete(IEntity entity);
    }

    public interface IEntity
    {
        IEnumerable<KeyValuePair<string, object>> Properties { get; } 
        void AddProperty(string key, object value);
        void RemoveProperty(string key);
        IEnumerable<string> Keywords { get; }
        void AddKeyword(string keyword);
        void RemoveKeyword(string keyword);
        IEnumerable<INote> Notes { get; } 
        INote AddNote(DateTimeOffset time, string text);
    }

    public interface IExperiment : IEntity
    {
        string Purpose { get; set; }
        IEnumerable<ISource> Sources { get; }
        IEnumerable<IEpochGroup> EpochGroups { get; } 
    }

    public interface ISource : IEntity
    {
        string Label { get; set; }
        IEnumerable<ISource> Sources { get; }
        IEnumerable<IEpochGroup> EpochGroups { get; } 
    }

    public interface IEpochGroup : IEntity
    {
        string Label { get; set; }
        DateTimeOffset StartTime { get; }
        DateTimeOffset? EndTime { get; }
        ISource Source { get; }
    }

    public interface INote
    {
        DateTimeOffset Time { get; }
        string Text { get; }
    }
}
