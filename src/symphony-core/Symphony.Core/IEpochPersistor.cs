using System;
using System.Collections.Generic;

namespace Symphony.Core
{
    public interface IEpochPersistor
    {
        void Close(DateTimeOffset endTime);
        IPersistentExperiment Experiment { get; }
        IPersistentDevice AddDevice(string name, string manufacturer);
        IPersistentSource AddSource(string label, IPersistentSource parent);
        IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source, DateTimeOffset startTime);
        IPersistentEpochGroup EndEpochGroup(DateTimeOffset endTime);
        IPersistentEpochBlock BeginEpochBlock(string protocolID, IEnumerable<KeyValuePair<string, object>> protocolParameters, DateTimeOffset startTime);
        IPersistentEpochBlock EndEpochBlock(DateTimeOffset endTime);
        IPersistentEpoch Serialize(Epoch epoch);
        void Delete(IPersistentEntity entity);
    }

    public interface IPersistentEntity
    {
        Guid UUID { get; }
        IEnumerable<KeyValuePair<string, object>> Properties { get; }
        void AddProperty(string key, object value);
        bool RemoveProperty(string key);
        IEnumerable<string> Keywords { get; }
        void AddKeyword(string keyword);
        bool RemoveKeyword(string keyword);
        IEnumerable<INote> Notes { get; }
        INote AddNote(DateTimeOffset time, string text);
    }

    public interface IPersistentDevice : IPersistentEntity
    {
        string Name { get; }
        string Manufacturer { get; }
    }

    public interface IPersistentSource : IPersistentEntity
    {
        string Label { get; }
        IEnumerable<IPersistentSource> Sources { get; }
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; } 
    }

    public interface ITimelinePersistentEntity : IPersistentEntity
    {
        DateTimeOffset StartTime { get; }
        DateTimeOffset? EndTime { get; }
    }

    public interface IPersistentExperiment : ITimelinePersistentEntity
    {
        string Purpose { get; }
        IEnumerable<IPersistentDevice> Devices { get; }
        IEnumerable<IPersistentSource> Sources { get; }
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }
    }

    public interface IPersistentEpochGroup : ITimelinePersistentEntity
    {
        string Label { get; }
        IPersistentSource Source { get; }
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }
        IEnumerable<IPersistentEpochBlock> EpochBlocks { get; } 
    }

    public interface IPersistentEpochBlock : ITimelinePersistentEntity
    {
        string ProtocolID { get; }
        IEnumerable<KeyValuePair<string, object>> ProtocolParameters { get; }
        IEnumerable<IPersistentEpoch> Epochs { get; } 
    }

    public interface IPersistentEpoch : ITimelinePersistentEntity
    {
        TimeSpan Duration { get; }
        IEnumerable<IPersistentResponse> Responses { get; }
        IEnumerable<IPersistentStimulus> Stimuli { get; }
    }

    public interface IPersistentResponse : IPersistentEntity
    {
        IEnumerable<IMeasurement> Data { get; }
        IPersistentDevice Device { get; }
    }

    public interface IPersistentStimulus : IPersistentEntity
    {
    }

    public interface INote
    {
        DateTimeOffset Time { get; }
        string Text { get; }
    }
}
