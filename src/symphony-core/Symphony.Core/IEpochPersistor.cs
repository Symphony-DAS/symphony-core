using System;
using System.Collections.Generic;

namespace Symphony.Core
{
    /// <summary>
    /// Class to take Epoch instances and persist them in some fashion. Also exposes a persistent model 
    /// that allows persisted objects to be annotated and modified after persistence.
    /// 
    /// The persistent hierarchy:
    /// 
    /// Experiment
    ///     Devices
    ///     Sources
    ///         Sources (nestable)
    ///     EpochGroups
    ///         EpochGroups (nestable)
    ///         EpochBlocks
    ///             Epochs
    ///                 Responses
    ///                 Stimuli
    /// 
    /// EpochPersistor is stateful;  Epochs are persisted to the currently open EpochBlock.
    /// </summary>
    public interface IEpochPersistor
    {
        /// <summary>
        /// Closes output to this persistor. All open entities are closed.
        /// </summary>
        void Close();

        /// <summary>
        /// An Experiment is the root of the persistent hierarchy and is automatically created when a persistor
        /// is instantiated. A persistor will always have one Experiment. All persisted entities can be accessed 
        /// by tranversing the persistent hierarchy starting at the Experiment entity.
        /// </summary>
        IPersistentExperiment Experiment { get; }

        /// <summary>
        /// Adds a new Device to the persistent hierarchy. A Device represents a physical device that presents
        /// stimuli and/or records responses during an experiment, such as an amplifier, LED, or temperature controller.
        /// </summary>
        /// <param name="name">Name of the Device</param>
        /// <param name="manufacturer">Manufacturer of the Device</param>
        /// <returns>The added persistent Device</returns>
        IPersistentDevice AddDevice(string name, string manufacturer);

        /// <summary>
        /// Adds a new Source to the persistent hierarchy. A Source represents the subject of an experiment. Each Source
        /// has a label property to describe the type of the Source (e.g. "animal", "cortex", "cell", etc). Sources
        /// may be nested to create a Source hierarchy (e.g. "animal" -> "cortex" -> "cell").
        /// </summary>
        /// <param name="label">Label of the Source</param>
        /// <param name="parent">Parent of the Source, or null if the Source should have no parent</param>
        /// <returns>The added persistent Source</returns>
        IPersistentSource AddSource(string label, IPersistentSource parent);

        /// <summary>
        /// Begins a new Epoch Group, a logical group of consecutive Epoch Blocks. EpochGroups may be nested. Nested EpochGroups 
        /// implicitly define their containing EpochGroup as their parent.
        /// </summary>
        /// <param name="label">Label of the Epoch Group</param>
        /// <param name="source">Source associated with the Epoch Group</param>
        /// <returns>The Epoch Group that was started</returns>
        IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source);

        /// <summary>
        /// Ends the current Epoch Group.
        /// </summary>
        /// <returns>The Epoch Group that was ended</returns>
        IPersistentEpochGroup EndEpochGroup();

        /// <summary>
        /// Begins a new Epoch Block, a logical group of consecutive Epochs produced by a single protocol run.
        /// </summary>
        /// <param name="protocolID">Protocol ID of the protocol that produced the block</param>
        /// <param name="startTime">Start time of the Epoch Block</param>
        /// <returns>The Epoch Block that was started</returns>
        IPersistentEpochBlock BeginEpochBlock(string protocolID, DateTimeOffset startTime);

        /// <summary>
        /// Ends the current Epoch Block.
        /// </summary>
        /// <param name="endTime">End time of the Epoch Block</param>
        /// <returns>The Epoch Block that was ended</returns>
        IPersistentEpochBlock EndEpochBlock(DateTimeOffset endTime);

        /// <summary>
        /// Serializes an Epoch instance to some kind of persistent medium (file/database/etc).
        /// </summary>
        /// <param name="epoch">Epoch to serialized</param>
        /// <returns>The persistent Epoch created through serialization</returns>
        IPersistentEpoch Serialize(Epoch epoch);

        /// <summary>
        /// Deletes the given persistent entity from the persistent medium.
        /// </summary>
        /// <param name="entity">Entity to delete</param>
        void Delete(IPersistentEntity entity);
    }

    /// <summary>
    /// Base interface for all entities stored in the persistent medium.
    /// </summary>
    public interface IPersistentEntity
    {
        /// <summary>
        /// A universally unique identifier for this entity.
        /// </summary>
        Guid UUID { get; }

        /// <summary>
        /// Key-value properties associated with this entity.
        /// </summary>
        IEnumerable<KeyValuePair<string, object>> Properties { get; }

        /// <summary>
        /// Adds a key-value property to this entity.
        /// </summary>
        /// <param name="key">Key of the property to add</param>
        /// <param name="value">Value of the property to add</param>
        void AddProperty(string key, object value);

        /// <summary>
        /// Removes a key-value property from this entity.
        /// </summary>
        /// <param name="key">Key of the property to remove</param>
        /// <returns>True if the property was successfully removed</returns>
        bool RemoveProperty(string key);

        /// <summary>
        /// Keyword tags associated with this entity.
        /// </summary>
        IEnumerable<string> Keywords { get; }

        /// <summary>
        /// Adds a keyword tag to this entity.
        /// </summary>
        /// <param name="keyword">The keyword to add</param>
        void AddKeyword(string keyword);

        /// <summary>
        /// Removes a keyword tag from this entity.
        /// </summary>
        /// <param name="keyword">The keyword to remove</param>
        /// <returns>True if the keyword was successfully removed</returns>
        bool RemoveKeyword(string keyword);

        /// <summary>
        /// Notes associated with this entity.
        /// </summary>
        IEnumerable<INote> Notes { get; }

        /// <summary>
        /// Adds a note to this entity.
        /// </summary>
        /// <param name="time">Time to associate with the note</param>
        /// <param name="text">Text of the note</param>
        /// <returns>The added note</returns>
        INote AddNote(DateTimeOffset time, string text);
    }

    /// <summary>
    /// Represents a physical device that presents stimuli and/or records responses during an Experiment (e.g.
    /// amplifier, LED, temperature controller, etc).
    /// </summary>
    public interface IPersistentDevice : IPersistentEntity
    {
        /// <summary>
        /// The name of this Device.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The manufacturer of this Device.
        /// </summary>
        string Manufacturer { get; }

        /// <summary>
        /// The Experiment that contains this Device.
        /// </summary>
        IPersistentExperiment Experiment { get; }
    }

    /// <summary>
    /// Represents the subject of an Experiment (e.g. animal, cortex, cell, etc).
    /// </summary>
    public interface IPersistentSource : IPersistentEntity
    {
        /// <summary>
        /// The label of this Source.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// The child Sources of this Source.
        /// </summary>
        IEnumerable<IPersistentSource> Sources { get; }

        /// <summary>
        /// All child Source of this Source and all its children.
        /// </summary>
        IEnumerable<IPersistentSource> AllSources { get; }

        /// <summary>
        /// The top-level Epoch Groups associated with this Source.
        /// </summary>
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }

        /// <summary>
        /// All Epoch Groups associated with this Source and all its children.
        /// </summary>
        IEnumerable<IPersistentEpochGroup> AllEpochGroups { get; }

        /// <summary>
        /// The parent Source for this Source or null if this Source has no parent.
        /// </summary>
        IPersistentSource Parent { get; }

        /// <summary>
        /// The Experiment that contains this Source.
        /// </summary>
        IPersistentExperiment Experiment { get; }
    }

    /// <summary>
    /// Interface for entities that represent a region on the experiment time line. 
    /// </summary>
    public interface ITimelinePersistentEntity : IPersistentEntity
    {
        /// <summary>
        /// The start time of this entity.
        /// </summary>
        DateTimeOffset StartTime { get; }

        /// <summary>
        /// The end time of this entity or null if this entity has not been ended.
        /// </summary>
        DateTimeOffset? EndTime { get; }
    }

    /// <summary>
    /// Represents an experiment in the persistent medium. An Experiment is the root entity of the persistent 
    /// hierarchy. Each persistor only has one Experiment. All persistent entities can be accessed through 
    /// traversal from the Experiment entity.
    /// </summary>
    public interface IPersistentExperiment : ITimelinePersistentEntity
    {
        /// <summary>
        /// The purpose of this Experiment.
        /// </summary>
        string Purpose { get; }

        /// <summary>
        /// The Devices contained within this Experiment.
        /// </summary>
        IEnumerable<IPersistentDevice> Devices { get; }

        /// <summary>
        /// The top-level Sources contained within this Experiment.
        /// </summary>
        IEnumerable<IPersistentSource> Sources { get; }

        /// <summary>
        /// All Sources contained within this Experiment.
        /// </summary>
        IEnumerable<IPersistentSource> AllSources { get; }

        /// <summary>
        /// The top-level Epoch Groups contained within this Experiment.
        /// </summary>
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }
    }

    /// <summary>
    /// Represents a logical grouping of consecutive Epoch Blocks.
    /// </summary>
    public interface IPersistentEpochGroup : ITimelinePersistentEntity
    {
        /// <summary>
        /// The label of this Epoch Group.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// The Source associated with this Epoch Group.
        /// </summary>
        IPersistentSource Source { get; }

        /// <summary>
        /// The child Epoch Groups of this Epoch Group.
        /// </summary>
        IEnumerable<IPersistentEpochGroup> EpochGroups { get; }

        /// <summary>
        /// The Epoch Blocks contained within this Epoch Group.
        /// </summary>
        IEnumerable<IPersistentEpochBlock> EpochBlocks { get; }

        /// <summary>
        /// The parent Epoch Group for this Epoch Group or null if this Epoch Group has no parent.
        /// </summary>
        IPersistentEpochGroup Parent { get; }

        /// <summary>
        /// The Experiment that contains this Epoch Group.
        /// </summary>
        IPersistentExperiment Experiment { get; }
    }

    /// <summary>
    /// Represents a logical grouping of consecutive Epochs produced by a single protocol run.
    /// </summary>
    public interface IPersistentEpochBlock : ITimelinePersistentEntity
    {
        /// <summary>
        /// The ID of the protocol describing this Epoch Block.
        /// </summary>
        string ProtocolID { get; }

        /// <summary>
        /// The Epochs contained within this Epoch Block.
        /// </summary>
        IEnumerable<IPersistentEpoch> Epochs { get; }

        /// <summary>
        /// The Epoch Group that contains this Epoch Block.
        /// </summary>
        IPersistentEpochGroup EpochGroup { get; }
    }

    /// <summary>
    /// Represents a finite-duration epoch of the experiment time line. 
    /// </summary>
    public interface IPersistentEpoch : ITimelinePersistentEntity
    {
        /// <summary>
        /// The protocol parameters describing this Epoch.
        /// </summary>
        IEnumerable<KeyValuePair<string, object>> ProtocolParameters { get; }

        /// <summary>
        /// The Responses recorded during this Epoch.
        /// </summary>
        IEnumerable<IPersistentResponse> Responses { get; }

        /// <summary>
        /// The Stimuli presented during this Epoch.
        /// </summary>
        IEnumerable<IPersistentStimulus> Stimuli { get; }

        /// <summary>
        /// The Background values presented in the absence of a Stimulus.
        /// </summary>
        IEnumerable<IPersistentBackground> Backgrounds { get; }

        /// <summary>
        /// The Epoch Block that contains this Epoch.
        /// </summary>
        IPersistentEpochBlock EpochBlock { get; }
    }


    /// <summary>
    /// Represents a background in the absence of a stimulus
    /// </summary>
    public interface IPersistentBackground : IPersistentEntity
    {
        /// <summary>
        /// The Device through which this Background was presented.
        /// </summary>
        IPersistentDevice Device { get; }

        /// <summary>
        /// The value of this Background.
        /// </summary>
        IMeasurement Value { get; }

        /// <summary>
        /// The sampling rate of this Background.
        /// </summary>
        IMeasurement SampleRate { get; }
    }

    /// <summary>
    /// Interface for entities that describe I/O data.
    /// </summary>
    public interface IPersistentIOBase : IPersistentEntity
    {
        /// <summary>
        /// The Device through which this entity was presented or recorded.
        /// </summary>
        IPersistentDevice Device { get; }

        /// <summary>
        /// The parameters describing the configuration of the associated Device.
        /// </summary>
        IEnumerable<IConfigurationSpan> ConfigurationSpans { get; }
    }

    /// <summary>
    /// Represents a single recorded response produced by a Device.
    /// </summary>
    public interface IPersistentResponse : IPersistentIOBase
    {
        /// <summary>
        /// The sampling rate of this Response.
        /// </summary>
        IMeasurement SampleRate { get; }

        /// <summary>
        /// The input time of this Response.
        /// </summary>
        DateTimeOffset InputTime { get; }

        /// <summary>
        /// The Measurements recorded in this Response.
        /// </summary>
        IEnumerable<IMeasurement> Data { get; }
    }

    /// <summary>
    /// Represents a single presented stimulus produced by a Device.
    /// </summary>
    public interface IPersistentStimulus : IPersistentIOBase
    {
        /// <summary>
        /// The identifier of this Stimulus.
        /// </summary>
        string StimulusID { get; }

        /// <summary>
        /// The BaseUnits for this stimulus' output data.
        /// </summary>
        string Units { get; }

        /// <summary>
        /// The sampling rate of this Stimulus.
        /// </summary>
        IMeasurement SampleRate { get; }

        /// <summary>
        /// The parameters of stimulus generation.
        /// </summary>
        IEnumerable<KeyValuePair<string, object>> Parameters { get; }

        /// <summary>
        /// The duration of this stimulus. Option No (false) to indicate indefinite.
        /// </summary>
        Option<TimeSpan> Duration { get; }

        /// <summary>
        /// The Measurements presented by this Stimulus. None if data is not persisted.
        /// </summary>
        Option<IEnumerable<IMeasurement>> Data { get; }
    }

    /// <summary>
    /// Represents a timestamped text note that annotate an entity.
    /// </summary>
    public interface INote
    {
        /// <summary>
        /// The timestamp of this Note.
        /// </summary>
        DateTimeOffset Time { get; }

        /// <summary>
        /// The text of this Note.
        /// </summary>
        string Text { get; }
    }
}
