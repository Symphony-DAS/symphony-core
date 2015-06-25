using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF5;
using HDF5DotNet;

namespace Symphony.Core
{
    /// <summary>
    /// IEpochPersistor implementation for persisting Epochs to an HDF5 data file. HDF5 does not currently
    /// offer atomic operations so, while this implementation does it's best to maintain data integrity it's
    /// not full-proof. In practice I think we'll find this sufficient. However this should probably be looked 
    /// at again when/if HDF5 begins to offer transactions.
    /// </summary>
    public class H5EpochPersistor : IEpochPersistor
    {
        private const string VersionKey = "version";
        private const uint PersistenceVersion = 2;

        private readonly H5File file;
        private readonly H5PersistentExperiment experiment;
        private readonly Stack<H5PersistentEpochGroup> openEpochGroups;

        /// <summary>
        /// Creates a new H5EpochPersistor with a new HDF5 file.
        /// </summary>
        /// <param name="filename">Desired HDF5 path</param>
        /// <param name="purpose">Experimental purpose for the root Experiment entity</param>
        /// <param name="startTime">Start time for the root Experiment entity</param>
        /// <returns>The new Epoch Persistor</returns>
        public static H5EpochPersistor Create(string filename, string purpose, DateTimeOffset startTime)
        {
            if (File.Exists(filename))
                throw new IOException("File already exists");

            using (var file = new H5File(filename))
            {
                file.Attributes[VersionKey] = PersistenceVersion;

                H5Map.InsertTypes(file);
                H5PersistentExperiment.InsertExperiment(file, purpose, startTime);
            }

            return new H5EpochPersistor(filename);
        }

        /// <summary>
        /// Constructs a new H5EpochPersistor with an existing HDF5 file at the given path.
        /// </summary>
        /// <param name="filename">Existing HDF5 file path</param>
        public H5EpochPersistor(string filename)
        {
            if (!File.Exists(filename))
                throw new IOException("File does not exist");

            file = new H5File(filename);
            if (!file.Attributes.ContainsKey(VersionKey))
                throw new FileLoadException(
                    "File does not have a version attribute. Are you sure this is a Symphony file?");

            Version = file.Attributes[VersionKey];
            if (Version != PersistenceVersion)
                throw new FileLoadException("Version mismatch. This file may have been produced by an older version.");

            if (file.Groups.Count() != 1)
                throw new FileLoadException("Expected a single top-level group");

            experiment = new H5PersistentExperiment(file.Groups.First());
            openEpochGroups = new Stack<H5PersistentEpochGroup>();
        }

        public void Close(DateTimeOffset endTime)
        {
            if (CurrentEpochBlock != null)
            {
                EndEpochBlock(endTime);
            }
            while (CurrentEpochGroup != null)
            {
                EndEpochGroup(endTime);
            }
            experiment.SetEndTime(endTime);
            file.Close();
        }

        public uint Version { get; private set; }

        public IPersistentExperiment Experiment { get { return experiment; } }

        public IPersistentDevice AddDevice(string name, string manufacturer)
        {
            return experiment.InsertDevice(name, manufacturer);
        }

        public IPersistentSource AddSource(string label, IPersistentSource parent)
        {
            return parent == null
                       ? experiment.InsertSource(label)
                       : ((H5PersistentSource) parent).InsertSource(label);
        }

        private H5PersistentEpochGroup CurrentEpochGroup
        {
            get { return openEpochGroups.Count == 0 ? null : openEpochGroups.Peek(); }
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source, DateTimeOffset startTime)
        {
            var epochGroup = CurrentEpochGroup == null
                       ? experiment.InsertEpochGroup(label, (H5PersistentSource) source, startTime)
                       : CurrentEpochGroup.InsertEpochGroup(label, (H5PersistentSource) source, startTime);
            openEpochGroups.Push(epochGroup);
            return epochGroup;
        }

        public IPersistentEpochGroup EndEpochGroup(DateTimeOffset endTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There are no open epoch groups");
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");
            CurrentEpochGroup.SetEndTime(endTime);
            return openEpochGroups.Pop();
        }

        private H5PersistentEpochBlock CurrentEpochBlock { get; set; }

        public IPersistentEpochBlock BeginEpochBlock(string protocolID, DateTimeOffset startTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There are no open epoch groups");
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");
            CurrentEpochBlock = CurrentEpochGroup.InsertEpochBlock(protocolID, startTime);
            return CurrentEpochBlock;
        }

        public IPersistentEpochBlock EndEpochBlock(DateTimeOffset endTime)
        {
            if (CurrentEpochBlock == null)
                throw new InvalidOperationException("There is no open epoch block");
            var block = CurrentEpochBlock;
            block.SetEndTime(endTime);
            CurrentEpochBlock = null;
            return block;
        }

        public IPersistentEpoch Serialize(Epoch epoch)
        {
            if (CurrentEpochBlock == null)
                throw new InvalidOperationException("There is no open epoch block");
            return CurrentEpochBlock.InsertEpoch(experiment, epoch);
        }

        public void Delete(IPersistentEntity entity)
        {
            if (entity.Equals(experiment))
                throw new InvalidOperationException("You cannot delete the experiment");
            if (openEpochGroups.Contains(entity))
                throw new InvalidOperationException("You cannot delete an open epoch group");
            if (entity.Equals(CurrentEpochBlock))
                throw new InvalidOperationException("You cannot delete an open epoch block");
            ((H5PersistentEntity) entity).Delete();
        }
    }

    /// <summary>
    /// An H5PersistentEntity is stored as a group in the H5 file. The group uses attributes, datasets, and subgroups
    /// to store fields of the entity. 
    /// 
    /// The vast majority of persistent entities will have NO associated keywords, properties, or notes, so we only create
    /// actual H5 objects for those fields if necessary (i.e. when a keyword, property, or note is actually associated with
    /// the entity).
    /// </summary>
    abstract class H5PersistentEntity : IPersistentEntity
    {
        private const string UUIDKey = "uuid";
        private const string KeywordsKey = "keywords";
        private const string PropertiesGroupName = "properties";
        private const string NotesDatasetName = "notes";

        private H5Group propertiesGroup;
        private H5Dataset notesDataset;

        protected static H5Group InsertEntityGroup(H5Group parent, string name)
        {
            var uuid = Guid.NewGuid();
            var group = parent.AddGroup(name + "-" + uuid);

            group.Attributes[UUIDKey] = uuid.ToString();

            return group;
        }

        protected H5PersistentEntity(H5Group group)
        {
            Group = group;
            UUID = new Guid(group.Attributes[UUIDKey]);

            propertiesGroup = group.Groups.FirstOrDefault(g => g.Name == PropertiesGroupName);
            notesDataset = group.Datasets.FirstOrDefault(ds => ds.Name == NotesDatasetName);
        }

        // The HDF5 group representing the persistent entity.
        public H5Group Group { get; private set; }

        public Guid UUID { get; private set; }

        public virtual void Delete()
        {
            Group.Delete();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((H5PersistentEntity) obj);
        }

        protected bool Equals(H5PersistentEntity other)
        {
            return UUID.Equals(other.UUID);
        }

        public override int GetHashCode()
        {
            return UUID.GetHashCode();
        }

        public IEnumerable<KeyValuePair<string, object>> Properties 
        { 
            get
            {
                return propertiesGroup == null
                           ? Enumerable.Empty<KeyValuePair<string, object>>()
                           : propertiesGroup.Attributes.Select(a => new KeyValuePair<string, object>(a.Name, a.GetValue()));
            } 
        }

        public void AddProperty(string key, object value)
        {
            if (propertiesGroup == null)
            {
                propertiesGroup = Group.AddGroup(PropertiesGroupName);
            }
            propertiesGroup.Attributes[key] = new H5Attribute(value);
        }

        public bool RemoveProperty(string key)
        {
            return propertiesGroup != null && propertiesGroup.Attributes.Remove(key);
        }

        public IEnumerable<string> Keywords
        {
            get
            {
                return Group.Attributes.ContainsKey(KeywordsKey)
                           ? ((string) Group.Attributes[KeywordsKey]).Split(new[] {','})
                           : Enumerable.Empty<string>();
            }
        }

        public void AddKeyword(string keyword)
        {
            var newKeywords = new HashSet<string>(Keywords);
            newKeywords.Add(keyword);
            Group.Attributes[KeywordsKey] = string.Join(",", newKeywords);
        }

        public bool RemoveKeyword(string keyword)
        {
            var newKeywords = new HashSet<string>(Keywords);
            newKeywords.Remove(keyword);
            if (!newKeywords.Any())
            {
                Group.Attributes.Remove(KeywordsKey);
            }
            else
            {
                Group.Attributes[KeywordsKey] = string.Join(",", newKeywords);
            }
            return !Keywords.Contains(keyword);
        }

        public IEnumerable<INote> Notes
        {
            get
            {
                return notesDataset == null
                           ? Enumerable.Empty<INote>()
                           : notesDataset.GetData<H5Map.NoteT>().Select(H5Map.Convert);
            }
        }

        public INote AddNote(DateTimeOffset time, string text)
        {
            return AddNote(new H5Note(time, text));
        }

        public INote AddNote(INote note)
        {
            if (notesDataset == null)
            {
                notesDataset = Group.AddDataset(NotesDatasetName, H5Map.GetNoteType(Group.File), new[] {0L}, new[] {-1L}, new[] {10L});
            }
            long n = notesDataset.NumberOfElements;
            notesDataset.Extend(new[] {n + 1});
            notesDataset.SetData(new[] {H5Map.Convert(note)}, new[] {n}, new[] {1L});
            return note;
        }
    }

    class H5PersistentDevice : H5PersistentEntity, IPersistentDevice
    {
        private const string NameKey = "name";
        private const string ManufacturerKey = "manufacturer";

        public static H5PersistentDevice InsertDevice(H5Group parent, string name, string manufacturer)
        {
            var group = InsertEntityGroup(parent, name);

            group.Attributes[NameKey] = name;
            group.Attributes[ManufacturerKey] = manufacturer;

            return new H5PersistentDevice(group);
        }

        public H5PersistentDevice(H5Group group) : base(group)
        {
            Name = group.Attributes[NameKey];
            Manufacturer = group.Attributes[ManufacturerKey];
        }

        public string Name { get; private set; }

        public string Manufacturer { get; private set; }
    }

    class H5PersistentSource : H5PersistentEntity, IPersistentSource
    {
        private const string LabelKey = "label";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";

        private readonly H5Group sourcesGroup;
        private readonly H5Group epochGroupsGroup;

        public static H5PersistentSource InsertSource(H5Group parent, string label)
        {
            var group = InsertEntityGroup(parent, label);

            group.Attributes[LabelKey] = label;

            group.AddGroup(SourcesGroupName);
            group.AddGroup(EpochGroupsGroupName);

            return new H5PersistentSource(group);
        }

        public H5PersistentSource(H5Group group) : base(group)
        {
            Label = group.Attributes[LabelKey];
            
            var subGroups = Group.Groups.ToList();
            sourcesGroup = subGroups.First(g => g.Name == SourcesGroupName);
            epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
        }

        public override void Delete()
        {
            if (AllEpochGroups.Any())
                throw new InvalidOperationException("Cannot delete source with associated epoch groups");
            base.Delete();
        }

        public string Label { get; private set; }

        public IEnumerable<IPersistentSource> Sources
        {
            get { return sourcesGroup.Groups.Select(g => new H5PersistentSource(g)); }
        }

        public H5PersistentSource InsertSource(string label)
        {
            return InsertSource(sourcesGroup, label);
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return epochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(g)); }
        }

        public IEnumerable<IPersistentEpochGroup> AllEpochGroups
        {
            get { return Sources.Aggregate(EpochGroups, (current, source) => current.Concat(source.AllEpochGroups)); }
        }

        public void AddEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            epochGroupsGroup.AddHardLink(epochGroup.Group.Name, epochGroup.Group);
        }

        public void RemoveEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            epochGroupsGroup.Groups.First(g => g.Name == epochGroup.Group.Name).Delete();
        }
    }

    abstract class H5TimelinePersistentEntity : H5PersistentEntity, ITimelinePersistentEntity
    {
        private const string StartTimeUtcTicksKey = "startTimeDotNetDateTimeOffsetTicks";
        private const string StartTimeOffsetHoursKey = "startTimeOffsetHours";
        private const string EndTimeUtcTicksKey = "endTimeDotNetDateTimeOffsetTicks";
        private const string EndTimeOffsetHoursKey = "endTimeOffsetHours";

        protected static H5Group InsertTimelineEntityGroup(H5Group parent, string prefix, DateTimeOffset startTime)
        {
            var group = InsertEntityGroup(parent, prefix);

            group.Attributes[StartTimeUtcTicksKey] = startTime.Ticks;
            group.Attributes[StartTimeOffsetHoursKey] = startTime.Offset.TotalHours;

            return group;
        }

        protected static H5Group InsertTimelineEntityGroup(H5Group parent, string prefix, DateTimeOffset startTime,
                                                           DateTimeOffset endTime)
        {
            var group = InsertTimelineEntityGroup(parent, prefix, startTime);

            group.Attributes[EndTimeUtcTicksKey] = endTime.Ticks;
            group.Attributes[EndTimeOffsetHoursKey] = endTime.Offset.TotalHours;

            return group;
        }

        protected H5TimelinePersistentEntity(H5Group group) : base(group)
        {
            var attr = group.Attributes;
            StartTime = new DateTimeOffset(attr[StartTimeUtcTicksKey], TimeSpan.FromHours(attr[StartTimeOffsetHoursKey]));
            if (attr.ContainsKey(EndTimeUtcTicksKey) && attr.ContainsKey(EndTimeOffsetHoursKey))
            {
                EndTime = new DateTimeOffset(attr[EndTimeUtcTicksKey], TimeSpan.FromHours(attr[EndTimeOffsetHoursKey]));
            }
        }

        public DateTimeOffset StartTime { get; private set; }

        public DateTimeOffset? EndTime { get; private set; }

        public void SetEndTime(DateTimeOffset time)
        {
            Group.Attributes[EndTimeUtcTicksKey] = time.Ticks;
            Group.Attributes[EndTimeOffsetHoursKey] = time.Offset.TotalHours;
            EndTime = time;
        }
    }

    class H5PersistentExperiment : H5TimelinePersistentEntity, IPersistentExperiment
    {
        private const string PurposeKey = "purpose";
        private const string DevicesGroupName = "devices";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";

        private readonly H5Group devicesGroup;
        private readonly H5Group sourcesGroup;
        private readonly H5Group epochGroupsGroup;

        public static H5PersistentExperiment InsertExperiment(H5Group parent, string purpose, DateTimeOffset startTime)
        {
            var group = InsertTimelineEntityGroup(parent, "experiment", startTime);

            group.Attributes[PurposeKey] = purpose;

            group.AddGroup(DevicesGroupName);
            group.AddGroup(SourcesGroupName);
            group.AddGroup(EpochGroupsGroupName);

            return new H5PersistentExperiment(group);
        }

        public H5PersistentExperiment(H5Group group) : base(group)
        {
            Purpose = group.Attributes[PurposeKey];

            var subGroups = group.Groups.ToList();
            devicesGroup = subGroups.First(g => g.Name == DevicesGroupName);
            sourcesGroup = subGroups.First(g => g.Name == SourcesGroupName);
            epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
        }

        public string Purpose { get; private set; }

        public IEnumerable<IPersistentDevice> Devices
        {
            get { return devicesGroup.Groups.Select(g => new H5PersistentDevice(g)); }
        }

        public H5PersistentDevice Device(string name, string manufacture)
        {
            return (H5PersistentDevice) (Devices.FirstOrDefault(d => d.Name == name && d.Manufacturer == manufacture) ??
                                         InsertDevice(name, manufacture));
        }

        public H5PersistentDevice InsertDevice(string name, string manufacturer)
        {
            if (Devices.Any(d => d.Name == name && d.Manufacturer == manufacturer))
                throw new ArgumentException("Device already exists");
            return H5PersistentDevice.InsertDevice(devicesGroup, name, manufacturer);
        }

        public IEnumerable<IPersistentSource> Sources
        {
            get { return sourcesGroup.Groups.Select(g => new H5PersistentSource(g)); }
        }

        public H5PersistentSource InsertSource(string label)
        {
            return H5PersistentSource.InsertSource(sourcesGroup, label);
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return epochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(g)); }
        }

        public H5PersistentEpochGroup InsertEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            return H5PersistentEpochGroup.InsertEpochGroup(epochGroupsGroup, label, source, startTime);
        }
    }

    class H5PersistentEpochGroup : H5TimelinePersistentEntity, IPersistentEpochGroup
    {
        private const string LabelKey = "label";
        private const string SourceGroupName = "source";
        private const string EpochGroupsGroupName = "epochGroups";
        private const string EpochBlocksGroupName = "epochBlocks";
        
        private readonly H5Group sourceGroup;
        private readonly H5Group epochGroupsGroup;
        private readonly H5Group epochBlocksGroup;

        public static H5PersistentEpochGroup InsertEpochGroup(H5Group parent, string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var group = InsertTimelineEntityGroup(parent, label, startTime);

            group.Attributes[LabelKey] = label;

            group.AddHardLink(SourceGroupName, source.Group);
            group.AddGroup(EpochGroupsGroupName);
            group.AddGroup(EpochBlocksGroupName);

            var g = new H5PersistentEpochGroup(group);
            source.AddEpochGroup(g);
            return g;
        }

        public H5PersistentEpochGroup(H5Group group) : base(group)
        {
            Label = group.Attributes[LabelKey];

            var subGroups = group.Groups.ToList();
            sourceGroup = subGroups.First(g => g.Name == SourceGroupName);
            epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
            epochBlocksGroup = subGroups.First(g => g.Name == EpochBlocksGroupName);
        }

        public override void Delete()
        {
            ((H5PersistentSource) Source).RemoveEpochGroup(this);
            base.Delete();
        }

        public string Label { get; private set; }

        public IPersistentSource Source
        {
            get { return new H5PersistentSource(sourceGroup); }
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return epochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(g)); }
        }

        public H5PersistentEpochGroup InsertEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            return InsertEpochGroup(epochGroupsGroup, label, source, startTime);
        }

        public IEnumerable<IPersistentEpochBlock> EpochBlocks
        {
            get { return epochBlocksGroup.Groups.Select(g => new H5PersistentEpochBlock(g)); }
        }

        public H5PersistentEpochBlock InsertEpochBlock(string protocolID, DateTimeOffset startTime)
        {
            return H5PersistentEpochBlock.InsertEpochBlock(epochBlocksGroup, protocolID, startTime);
        }
    }

    class H5PersistentEpochBlock : H5TimelinePersistentEntity, IPersistentEpochBlock
    {
        private const string ProtocolIDKey = "protocolID";
        private const string EpochsGroupName = "epochs";

        private readonly H5Group epochsGroup;

        public static H5PersistentEpochBlock InsertEpochBlock(H5Group parent, string protocolID, DateTimeOffset startTime)
        {
            var group = InsertTimelineEntityGroup(parent, protocolID, startTime);

            group.Attributes[ProtocolIDKey] = protocolID;

            group.AddGroup(EpochsGroupName);

            return new H5PersistentEpochBlock(group);
        }

        public H5PersistentEpochBlock(H5Group group) : base(group)
        {
            ProtocolID = group.Attributes[ProtocolIDKey];

            var subGroups = group.Groups.ToList();
            epochsGroup = subGroups.First(g => g.Name == EpochsGroupName);
        }

        public string ProtocolID { get; private set; }

        public IEnumerable<IPersistentEpoch> Epochs
        {
            get { return epochsGroup.Groups.Select(g => new H5PersistentEpoch(g)); }
        }

        public H5PersistentEpoch InsertEpoch(H5PersistentExperiment experiment, Epoch epoch)
        {
            if (epoch.ProtocolID != ProtocolID)
                throw new ArgumentException("Epoch protocol id does not match epoch block protocol id");
            return H5PersistentEpoch.InsertEpoch(epochsGroup, experiment, epoch);
        }
    }

    class H5PersistentEpoch : H5TimelinePersistentEntity, IPersistentEpoch
    {
        private const string ProtocolParametersGroupName = "protocolParameters";
        private const string ResponsesGroupName = "responses";
        private const string StimuliGroupName = "stimuli";

        private readonly H5Group protocolParametersGroup;
        private readonly H5Group responsesGroup;
        private readonly H5Group stimuliGroup;

        public static H5PersistentEpoch InsertEpoch(H5Group parent, H5PersistentExperiment experiment, Epoch epoch)
        {
            var group = InsertTimelineEntityGroup(parent, "epoch", epoch.StartTime, (DateTimeOffset)epoch.StartTime + epoch.Duration);

            var parametersGroup = group.AddGroup(ProtocolParametersGroupName);
            var responsesGroup = group.AddGroup(ResponsesGroupName);
            var stimuliGroup = group.AddGroup(StimuliGroupName);

            foreach (var kv in epoch.ProtocolParameters)
            {
                parametersGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
            }

            foreach (var kv in epoch.Responses)
            {
                var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                H5PersistentResponse.InsertResponse(responsesGroup, device, kv.Value);
            }

            foreach (var kv in epoch.Stimuli)
            {
                var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                H5PersistentStimulus.InsertStimulus(stimuliGroup, device, kv.Value);
            }

            var e = new H5PersistentEpoch(group);
            foreach (var keyword in epoch.Keywords)
            {
                e.AddKeyword(keyword);
            }
            return e;
        }

        public H5PersistentEpoch(H5Group group) : base(group)
        {
            var subGroups = group.Groups.ToList();
            protocolParametersGroup = subGroups.First(g => g.Name == ProtocolParametersGroupName);
            responsesGroup = subGroups.First(g => g.Name == ResponsesGroupName);
            stimuliGroup = subGroups.First(g => g.Name == StimuliGroupName);
        }

        public IEnumerable<KeyValuePair<string, object>> ProtocolParameters
        {
            get { return protocolParametersGroup.Attributes.Select(a => new KeyValuePair<string, object>(a.Name, a.GetValue())); }
        }

        public IEnumerable<IPersistentResponse> Responses
        {
            get { return responsesGroup.Groups.Select(g => new H5PersistentResponse(g)); }
        }

        public IEnumerable<IPersistentStimulus> Stimuli
        {
            get { return stimuliGroup.Groups.Select(g => new H5PersistentStimulus(g)); }
        }
    }

    abstract class H5PersistentIOBase : H5PersistentEntity, IPersistentIOBase
    {
        private const string DeviceGroupName = "device";
        private const string DataConfigurationSpansGroupName = "dataConfigurationSpans";
        private const string SpanGroupPrefix = "span_";
        private const string SpanIndexKey = "index";
        private const string SpanStartTimeKey = "startTimeSeconds";
        private const string SpanDurationKey = "timeSpanSeconds";

        private readonly H5Group deviceGroup;
        private readonly H5Group dataConfigurationSpansGroup;

        public static H5Group InsertIOBaseGroup(H5Group parent, H5PersistentDevice device, IEnumerable<IConfigurationSpan> configSpans)
        {
            var group = InsertEntityGroup(parent, device.Name);

            var deviceGroup = group.AddHardLink(DeviceGroupName, device.Group);
            var spansGroup = group.AddGroup(DataConfigurationSpansGroupName);

            uint i = 0;
            var totalTime = TimeSpan.Zero;
            foreach (var span in configSpans)
            {
                var spanGroup = spansGroup.AddGroup(SpanGroupPrefix + i);
                spanGroup.Attributes[SpanIndexKey] = i;

                spanGroup.Attributes[SpanStartTimeKey] = totalTime.TotalSeconds;
                totalTime += span.Time;

                spanGroup.Attributes[SpanDurationKey] = span.Time.TotalSeconds;
                foreach (var node in span.Nodes)
                {
                    var nodeGroup = spanGroup.AddGroup(node.Name);
                    foreach (var kv in node.Configuration)
                    {
                        nodeGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
                    }
                }

                i++;
            }

            return group;
        }

        protected H5PersistentIOBase(H5Group group) : base(group)
        {
            var subGroups = group.Groups.ToList(); 
            deviceGroup = subGroups.First(g => g.Name == DeviceGroupName);
            dataConfigurationSpansGroup = subGroups.First(g => g.Name == DataConfigurationSpansGroupName);
        }

        public IPersistentDevice Device
        {
            get { return new H5PersistentDevice(deviceGroup); }
        }

        public IEnumerable<IConfigurationSpan> ConfigurationSpans
        {
            get
            {
                var spanGroups = dataConfigurationSpansGroup.Groups.ToList();
                spanGroups.Sort((g1, g2) => ((uint) g1.Attributes[SpanIndexKey]).CompareTo(g2.Attributes[SpanIndexKey]));
                foreach (var spanGroup in spanGroups)
                {
                    TimeSpan duration = TimeSpan.FromSeconds(spanGroup.Attributes[SpanDurationKey]);

                    var nodeGroups = spanGroup.Groups.ToList();
                    var nodes = new List<PipelineNodeConfiguration>(nodeGroups.Count);
                    foreach (var nodeGroup in nodeGroups)
                    {
                        var attrs = nodeGroup.Attributes.ToDictionary(a => a.Name, a => a.GetValue());
                        nodes.Add(new PipelineNodeConfiguration(nodeGroup.Name, attrs));
                    }

                    yield return new ConfigurationSpan(duration, nodes);
                }
            }
        }
    }

    class H5PersistentResponse : H5PersistentIOBase, IPersistentResponse
    {
        private const string SampleRateKey = "sampleRate";
        private const string SampleRateUnitsKey = "sampleRateUnits";
        private const string DataDatasetName = "data";

        private readonly H5Dataset dataDataset;

        public static H5PersistentResponse InsertResponse(H5Group parent, H5PersistentDevice device, Response response)
        {
            var group = InsertIOBaseGroup(parent, device, response.DataConfigurationSpans);

            group.Attributes[SampleRateKey] = (double) response.SampleRate.QuantityInBaseUnit;
            group.Attributes[SampleRateUnitsKey] = response.SampleRate.BaseUnit;

            group.AddDataset(DataDatasetName, H5Map.GetMeasurementType(parent.File), response.Data.Select(H5Map.Convert).ToArray());

            return new H5PersistentResponse(group);
        }

        public H5PersistentResponse(H5Group group) : base(group)
        {
            double rate = group.Attributes[SampleRateKey];
            string units = group.Attributes[SampleRateUnitsKey];
            SampleRate = new Measurement(rate, units);

            dataDataset = group.Datasets.First(ds => ds.Name == DataDatasetName);
        }

        public IMeasurement SampleRate { get; private set; }

        public IEnumerable<IMeasurement> Data
        {
            get { return dataDataset.GetData<H5Map.MeasurementT>().Select(H5Map.Convert); }
        }
    }

    class H5PersistentStimulus : H5PersistentIOBase, IPersistentStimulus
    {
        private const string StimulusIDKey = "stimulusID";
        private const string UnitsKey = "units";
        private const string ParametersGroupName = "parameters";

        private readonly H5Group parametersGroup;

        private readonly Lazy<Dictionary<string, object>> parameters;

        public static H5PersistentStimulus InsertStimulus(H5Group parent, H5PersistentDevice device, IStimulus stimulus)
        {
            var group = InsertIOBaseGroup(parent, device, stimulus.OutputConfigurationSpans);

            group.Attributes[StimulusIDKey] = stimulus.StimulusID;
            group.Attributes[UnitsKey] = stimulus.Units;

            var parametersGroup = group.AddGroup(ParametersGroupName);

            foreach (var kv in stimulus.Parameters)
            {
                parametersGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
            }

            return new H5PersistentStimulus(group);
        }

        public H5PersistentStimulus(H5Group group) : base(group)
        {
            StimulusID = group.Attributes[StimulusIDKey];
            Units = group.Attributes[UnitsKey];

            parametersGroup = group.Groups.First(g => g.Name == ParametersGroupName);

            parameters = new Lazy<Dictionary<string, object>>(() => parametersGroup.Attributes.ToDictionary(a => a.Name, a => a.GetValue()));
        }

        public string StimulusID { get; private set; }

        public string Units { get; private set; }

        public IEnumerable<KeyValuePair<string, object>> Parameters
        {
            get { return parametersGroup.Attributes.Select(a => new KeyValuePair<string, object>(a.Name, a.GetValue())); }
        }
    }

    class H5Note : INote
    {
        public H5Note(DateTimeOffset time, string text)
        {
            Time = time;
            Text = text;
        }

        public DateTimeOffset Time { get; private set; }

        public string Text { get; private set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((H5Note) obj);
        }

        protected bool Equals(H5Note other)
        {
            return Time.Equals(other.Time) && string.Equals(Text, other.Text);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Time.GetHashCode() * 397) ^ Text.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Conversion routines to turn our .NET objects into HDF5 friendly structures and vice versa.
    /// </summary>
    static class H5Map
    {
        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct DateTimeOffsetT
        {
            [FieldOffset(0)]
            public long ticks;
            [FieldOffset(8)]
            public double offset;
        }

        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct NoteT
        {
            [FieldOffset(0)]
            public DateTimeOffsetT time;
            [FieldOffset(16)]
            public fixed byte text[FixedStringLength];
        }

        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct MeasurementT
        {
            [FieldOffset(0)]
            public double quantity;
            [FieldOffset(8)]
            public fixed byte unit[FixedStringLength];
        }

        private const int FixedStringLength = 40;

        private const string StringTypeName = "STRING40";
        private const string DateTimeOffsetTypeName = "DATETIMEOFFSET";
        private const string NoteTypeName = "NOTE";
        private const string MeasurementTypeName = "MEASUREMENT";

        public static void InsertTypes(H5File file)
        {
            var stringType = file.CreateDatatype(StringTypeName, H5T.H5TClass.STRING, FixedStringLength);

            var dateTimeOffsetType = file.CreateDatatype(DateTimeOffsetTypeName,
                                                         new[] {"ticks", "offsetHours"},
                                                         new[]
                                                             {
                                                                 new H5Datatype(H5T.H5Type.NATIVE_LLONG),
                                                                 new H5Datatype(H5T.H5Type.NATIVE_DOUBLE)
                                                             });

            file.CreateDatatype(NoteTypeName,
                                new[] {"time", "text"},
                                new[] {dateTimeOffsetType, stringType});

            file.CreateDatatype(MeasurementTypeName,
                                new[] {"quantity", "unit"},
                                new[] {new H5Datatype(H5T.H5Type.NATIVE_DOUBLE), stringType});
        }

        public static H5Datatype GetNoteType(H5File file)
        {
            return file.Datatypes.First(t => t.Name == NoteTypeName);
        }

        public static H5Datatype GetMeasurementType(H5File file)
        {
            return file.Datatypes.First(t => t.Name == MeasurementTypeName);
        }

        public static NoteT Convert(INote n)
        {
            var nt = new NoteT
            {
                time = new DateTimeOffsetT
                {
                    ticks = n.Time.Ticks,
                    offset = n.Time.Offset.TotalHours
                }
            };
            byte[] textdata = Encoding.ASCII.GetBytes(n.Text);
            unsafe
            {
                Marshal.Copy(textdata, 0, (IntPtr)nt.text, textdata.Length);
            }
            return nt;
        }

        public static INote Convert(NoteT nt)
        {
            long ticks = nt.time.ticks;
            double offset = nt.time.offset;
            var time = new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            string text;
            unsafe
            {
                text = Marshal.PtrToStringAnsi((IntPtr)nt.text);
            }
            return new H5Note(time, text);
        }

        public static MeasurementT Convert(IMeasurement m)
        {
            var mt = new MeasurementT {quantity = (double) m.Quantity};
            byte[] textdata = Encoding.ASCII.GetBytes(m.DisplayUnit);
            unsafe
            {
                Marshal.Copy(textdata, 0, (IntPtr)mt.unit, textdata.Length);
            }
            return mt;
        }

        public static IMeasurement Convert(MeasurementT mt)
        {
            string unit;
            unsafe
            {
                unit = Marshal.PtrToStringAnsi((IntPtr)mt.unit);
            }
            return new Measurement(mt.quantity, unit);
        }
    }
}
