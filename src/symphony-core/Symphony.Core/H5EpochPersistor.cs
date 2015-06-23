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
        /// Creates a new HDF5 file, persistor, and root experiment.
        /// </summary>
        /// <param name="filename">Desired HDF5 path</param>
        /// <param name="purpose">Experimental purpose for the root Experiment entity</param>
        /// <param name="startTime">Start time for the root Experiment entity</param>
        /// <returns>The new Epoch Persistor</returns>
        public static H5EpochPersistor CreatePersistor(string filename, string purpose, DateTimeOffset startTime)
        {
            if (File.Exists(filename))
                throw new Exception("file already exists");

            var file = new H5File(filename);

            file.Attributes[VersionKey] = PersistenceVersion;

            H5Map.CreateTypes(file);
            H5PersistentExperiment.CreateExperiment(file, purpose, startTime);
            
            return new H5EpochPersistor(filename);
        }

        /// <summary>
        /// Constructs a new H5EpochPersistor with an existing HDF5 file at the given path.
        /// </summary>
        /// <param name="filename">Existing HDF5 file path</param>
        public H5EpochPersistor(string filename)
        {
            if (!File.Exists(filename))
                throw new Exception("file doesn't exist");

            file = new H5File(filename);
            if (!file.Attributes.ContainsKey(VersionKey))
                throw new Exception("not symphony data file?");

            Version = file.Attributes[VersionKey];
            if (Version != PersistenceVersion)
                throw new Exception("version mismatch");

            if (file.Groups.Count() != 1)
                throw new Exception("expected only a single group");

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
            return experiment.AddDevice(name, manufacturer);
        }

        public IPersistentSource AddSource(string label, IPersistentSource parent)
        {
            return parent == null
                       ? experiment.AddSource(label)
                       : ((H5PersistentSource) parent).AddSource(label);
        }

        private H5PersistentEpochGroup CurrentEpochGroup
        {
            get { return openEpochGroups.Count == 0 ? null : openEpochGroups.Peek(); }
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source, DateTimeOffset startTime)
        {
            var epochGroup = CurrentEpochGroup == null
                       ? experiment.AddEpochGroup(label, (H5PersistentSource) source, startTime)
                       : CurrentEpochGroup.AddEpochGroup(label, (H5PersistentSource) source, startTime);
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

        public IPersistentEpochBlock BeginEpochBlock(string protocolID, IEnumerable<KeyValuePair<string, object>> protocolParameters, DateTimeOffset startTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There are no open epoch groups");
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");
            CurrentEpochBlock = CurrentEpochGroup.AddEpochBlock(protocolID, protocolParameters, startTime);
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
            return CurrentEpochBlock.AddEpoch(experiment, epoch);
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

        private HashSet<string> keywords;
        private readonly Lazy<Dictionary<string, object>> properties;
        private readonly Lazy<List<INote>> notes;

        protected static H5Group CreateEntityGroup(H5Group parent, string name)
        {
            var uuid = Guid.NewGuid();
            var group = parent.AddGroup(name + "-" + uuid);

            group.Attributes[UUIDKey] = uuid.ToString();

            return group;
        }

        protected H5PersistentEntity(H5Group group)
        {
            UUID = new Guid(group.Attributes[UUIDKey]);

            propertiesGroup = group.Groups.FirstOrDefault(g => g.Name == PropertiesGroupName);
            notesDataset = group.Datasets.FirstOrDefault(ds => ds.Name == NotesDatasetName);

            keywords = group.Attributes.ContainsKey(KeywordsKey)
                           ? new HashSet<string>(((string)group.Attributes[KeywordsKey]).Split(new[] { ',' }))
                           : new HashSet<string>();

            properties =
                new Lazy<Dictionary<string, object>>(
                    () =>
                    propertiesGroup == null
                        ? new Dictionary<string, object>()
                        : propertiesGroup.Attributes.ToDictionary(a => a.Name, a => a.GetValue()));

            notes =
                new Lazy<List<INote>>(
                    () =>
                    notesDataset == null
                        ? new List<INote>()
                        : notesDataset.GetData<H5Map.NoteT>().Select(H5Map.Convert).ToList());

            Group = group;
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

        public IEnumerable<KeyValuePair<string, object>> Properties { get { return properties.Value; } }

        public void AddProperty(string key, object value)
        {
            if (propertiesGroup == null)
            {
                propertiesGroup = Group.AddGroup(PropertiesGroupName);
            }
            propertiesGroup.Attributes[key] = new H5Attribute(value);
            if (properties.IsValueCreated)
            {
                properties.Value[key] = value;
            }
        }

        public bool RemoveProperty(string key)
        {
            if (propertiesGroup == null || !propertiesGroup.Attributes.ContainsKey(key))
                throw new KeyNotFoundException("There is no property named " + key);

            bool removed = propertiesGroup.Attributes.Remove(key);
            if (removed && properties.IsValueCreated)
            {
                removed = properties.Value.Remove(key);
            }
            return removed;
        }

        public IEnumerable<string> Keywords { get { return keywords; } }

        public void AddKeyword(string keyword)
        {
            // Need to be careful about adding the keyword to our in-memory collection before we're
            // sure it's been added as an attribute to the entity group.

            var newKeywords = new HashSet<string>(keywords);
            newKeywords.Add(keyword);
            Group.Attributes[KeywordsKey] = string.Join(",", newKeywords);
            keywords = newKeywords;
        }

        public bool RemoveKeyword(string keyword)
        {
            // Need to be careful about removing the keyword from our in-memory collection before we're
            // sure it's been removed from the attributes of the entity group.

            var newKeywords = new HashSet<string>(keywords);
            bool removed = newKeywords.Remove(keyword);
            Group.Attributes[KeywordsKey] = string.Join(",", newKeywords);
            keywords = newKeywords;
            return removed;
        }

        public IEnumerable<INote> Notes { get { return notes.Value; } }

        public INote AddNote(DateTimeOffset time, string text)
        {
            return AddNote(new H5Note(time, text));
        }

        public INote AddNote(INote note)
        {
            if (notesDataset == null)
            {
                notesDataset = Group.AddDataset(NotesDatasetName, H5Map.GetMeasurementType(Group.File), new[] {0L}, new[] {-1L}, new[] {10L});
            }
            long n = notesDataset.NumberOfElements;
            notesDataset.Extend(new[] {n + 1});
            notesDataset.SetData(new[] {H5Map.Convert(note)}, new[] {n}, new[] {1L});
            if (notes.IsValueCreated)
            {
                notes.Value.Add(note);
            }
            return note;
        }
    }

    class H5PersistentDevice : H5PersistentEntity, IPersistentDevice
    {
        private const string NameKey = "name";
        private const string ManufacturerKey = "manufacturer";

        public static H5PersistentDevice CreateDevice(H5Group parent, string name, string manufacturer)
        {
            var group = CreateEntityGroup(parent, name);

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

        private readonly Lazy<HashSet<H5PersistentSource>> sources;
        private readonly Lazy<HashSet<H5PersistentEpochGroup>> epochGroups;

        public static H5PersistentSource CreateSource(H5Group parent, string label)
        {
            var group = CreateEntityGroup(parent, label);

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

            sources = new Lazy<HashSet<H5PersistentSource>>(() => new HashSet<H5PersistentSource>(sourcesGroup.Groups.Select(g => new H5PersistentSource(g))));
            epochGroups = new Lazy<HashSet<H5PersistentEpochGroup>>(() => new HashSet<H5PersistentEpochGroup>(epochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(g))));
        }

        public override void Delete()
        {
            if (epochGroups.Value.Any())
                throw new InvalidOperationException("Cannot delete source with associated epoch groups");
            base.Delete();
        }

        public string Label { get; private set; }

        public IEnumerable<IPersistentSource> Sources { get { return sources.Value; }}

        public H5PersistentSource AddSource(string label)
        {
            var s = CreateSource(sourcesGroup, label);
            if (sources.IsValueCreated)
            {
                sources.Value.Add(s);
            }
            return s;
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups { get { return epochGroups.Value; }}

        public void AddEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            Group.AddHardLink(epochGroup.Group.Name, epochGroup.Group);
            if (epochGroups.IsValueCreated)
            {
                epochGroups.Value.Add(epochGroup);
            }
        }

        public void RemoveEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            epochGroupsGroup.Groups.First(g => g.Name == epochGroup.Group.Name).Delete();
            if (epochGroups.IsValueCreated)
            {
                epochGroups.Value.Remove(epochGroup);
            }
        }
    }

    abstract class H5TimelinePersistentEntity : H5PersistentEntity, ITimelinePersistentEntity
    {
        private const string StartTimeUtcTicksKey = "startTimeDotNetDateTimeOffsetUTCTicks";
        private const string StartTimeOffsetHoursKey = "startTimeUTCOffsetHours";
        private const string EndTimeUtcTicksKey = "endTimeDotNetDateTimeOffsetUTCTicks";
        private const string EndTimeOffsetHoursKey = "endTimeUTCOffsetHours";

        protected static H5Group CreateTimelineEntityGroup(H5Group parent, string prefix, DateTimeOffset startTime)
        {
            var group = CreateEntityGroup(parent, prefix);

            group.Attributes[StartTimeUtcTicksKey] = startTime.UtcTicks;
            group.Attributes[StartTimeOffsetHoursKey] = startTime.Offset.TotalHours;

            return group;
        }

        protected static H5Group CreateTimelineEntityGroup(H5Group parent, string prefix, DateTimeOffset startTime,
                                                           DateTimeOffset endTime)
        {
            var group = CreateTimelineEntityGroup(parent, prefix, startTime);

            group.Attributes[EndTimeUtcTicksKey] = endTime.UtcTicks;
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
            Group.Attributes[EndTimeUtcTicksKey] = time.UtcTicks;
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

        private readonly Lazy<HashSet<H5PersistentDevice>> devices;
        private readonly Lazy<HashSet<H5PersistentSource>> sources;
        private readonly Lazy<HashSet<H5PersistentEpochGroup>> epochGroups; 

        public static H5PersistentExperiment CreateExperiment(H5Group parent, string purpose, DateTimeOffset startTime)
        {
            var group = CreateTimelineEntityGroup(parent, "experiment", startTime);

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

            devices = new Lazy<HashSet<H5PersistentDevice>>(() => new HashSet<H5PersistentDevice>(devicesGroup.Groups.Select(g => new H5PersistentDevice(g))));
            sources = new Lazy<HashSet<H5PersistentSource>>(() => new HashSet<H5PersistentSource>(sourcesGroup.Groups.Select(g => new H5PersistentSource(g))));
            epochGroups = new Lazy<HashSet<H5PersistentEpochGroup>>(() => new HashSet<H5PersistentEpochGroup>(epochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(g))));
        }

        public string Purpose { get; private set; }

        public IEnumerable<IPersistentDevice> Devices { get { return devices.Value; }}

        public H5PersistentDevice AddDevice(string name, string manufacturer)
        {
            var d = H5PersistentDevice.CreateDevice(devicesGroup, name, manufacturer);
            if (devices.IsValueCreated)
            {
                devices.Value.Add(d);
            }
            return d;
        }

        public IEnumerable<IPersistentSource> Sources { get { return sources.Value; }}

        public H5PersistentSource AddSource(string label)
        {
            var s = H5PersistentSource.CreateSource(sourcesGroup, label);
            if (sources.IsValueCreated)
            {
                sources.Value.Add(s);
            }
            return s;
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups { get { return epochGroups.Value; }}

        public H5PersistentEpochGroup AddEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var g = H5PersistentEpochGroup.CreateEpochGroup(epochGroupsGroup, label, source, startTime);
            if (epochGroups.IsValueCreated)
            {
                epochGroups.Value.Add(g);
            }
            return g;
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
        
        private readonly Lazy<H5PersistentSource> source;
        private readonly Lazy<HashSet<H5PersistentEpochGroup>> epochGroups;
        private readonly Lazy<HashSet<H5PersistentEpochBlock>> epochBlocks;

        public static H5PersistentEpochGroup CreateEpochGroup(H5Group parent, string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var group = CreateTimelineEntityGroup(parent, label, startTime);

            group.Attributes[LabelKey] = label;

            group.AddHardLink(SourceGroupName, source.Group);
            group.AddGroup(EpochGroupsGroupName);
            group.AddGroup(EpochBlocksGroupName);

            return new H5PersistentEpochGroup(group);
        }

        public H5PersistentEpochGroup(H5Group group) : base(group)
        {
            Label = group.Attributes[LabelKey];

            var subGroups = group.Groups.ToList();
            sourceGroup = subGroups.First(g => g.Name == SourceGroupName);
            epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
            epochBlocksGroup = subGroups.First(g => g.Name == EpochBlocksGroupName);

            source = new Lazy<H5PersistentSource>(() => new H5PersistentSource(sourceGroup));
            epochGroups = new Lazy<HashSet<H5PersistentEpochGroup>>(() => new HashSet<H5PersistentEpochGroup>(epochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(g))));
            epochBlocks = new Lazy<HashSet<H5PersistentEpochBlock>>(() => new HashSet<H5PersistentEpochBlock>(epochBlocksGroup.Groups.Select(g => new H5PersistentEpochBlock(g))));
        }

        public override void Delete()
        {
            ((H5PersistentSource) Source).RemoveEpochGroup(this);
            base.Delete();
        }

        public string Label { get; private set; }

        public IPersistentSource Source { get { return source.Value; } }

        public IEnumerable<IPersistentEpochGroup> EpochGroups { get { return epochGroups.Value; } }

        public H5PersistentEpochGroup AddEpochGroup(string label, H5PersistentSource src, DateTimeOffset startTime)
        {
            var g = CreateEpochGroup(epochGroupsGroup, label, src, startTime);
            if (epochGroups.IsValueCreated)
            {
                epochGroups.Value.Add(g);
            }
            return g;
        }

        public IEnumerable<IPersistentEpochBlock> EpochBlocks { get { return epochBlocks.Value; } }

        public H5PersistentEpochBlock AddEpochBlock(string protocolID, IEnumerable<KeyValuePair<string, object>> protocolParameters, DateTimeOffset startTime)
        {
            var b = H5PersistentEpochBlock.CreateEpochBlock(epochBlocksGroup, protocolID, protocolParameters, startTime);
            if (epochBlocks.IsValueCreated)
            {
                epochBlocks.Value.Add(b);
            }
            return b;
        }
    }

    class H5PersistentEpochBlock : H5TimelinePersistentEntity, IPersistentEpochBlock
    {
        private const string ProtocolIDKey = "protocolID";
        private const string ProtocolParametersGroupName = "protocolParameters";
        private const string EpochsGroupName = "epochs";

        private readonly H5Group protocolParametersGroup;
        private readonly H5Group epochsGroup;

        private readonly Lazy<Dictionary<string, object>> protocolParameters;
        private readonly Lazy<HashSet<H5PersistentEpoch>> epochs;

        public static H5PersistentEpochBlock CreateEpochBlock(H5Group parent, string protocolID, IEnumerable<KeyValuePair<string, object>> protocolParameters, DateTimeOffset startTime)
        {
            var group = CreateTimelineEntityGroup(parent, protocolID, startTime);

            group.Attributes[ProtocolIDKey] = protocolID;

            var parametersGroup = group.AddGroup(ProtocolParametersGroupName);
            var epochsGroup = group.AddGroup(EpochsGroupName);

            foreach (var kv in protocolParameters)
            {
                parametersGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
            }

            return new H5PersistentEpochBlock(group);
        }

        public H5PersistentEpochBlock(H5Group group) : base(group)
        {
            ProtocolID = group.Attributes[ProtocolIDKey];

            var subGroups = group.Groups.ToList();
            protocolParametersGroup = subGroups.First(g => g.Name == ProtocolParametersGroupName);
            epochsGroup = subGroups.First(g => g.Name == EpochsGroupName);

            protocolParameters = new Lazy<Dictionary<string, object>>(() => protocolParametersGroup.Attributes.ToDictionary(a => a.Name, a => a.GetValue()));
            epochs = new Lazy<HashSet<H5PersistentEpoch>>(() => new HashSet<H5PersistentEpoch>(epochsGroup.Groups.Select(g => new H5PersistentEpoch(g))));
        }

        public string ProtocolID { get; private set; }

        public IEnumerable<KeyValuePair<string, object>> ProtocolParameters { get { return protocolParameters.Value; } }

        public IEnumerable<IPersistentEpoch> Epochs { get { return epochs.Value; } }

        public H5PersistentEpoch AddEpoch(H5PersistentExperiment experiment, Epoch epoch)
        {
            if (epoch.ProtocolID != ProtocolID)
                throw new ArgumentException("Epoch protocol id does not match epoch block protocol id");
            if (epoch.ProtocolParameters != ProtocolParameters)
                throw new ArgumentException("Epoch protocol parameters does not match epoch block protocol parameters");

            var e = H5PersistentEpoch.CreateEpoch(epochsGroup, experiment, epoch);
            if (epochs.IsValueCreated)
            {
                epochs.Value.Add(e);
            }
            return e;
        }
    }

    class H5PersistentEpoch : H5TimelinePersistentEntity, IPersistentEpoch
    {
        private const string ResponsesGroupName = "responses";
        private const string StimuliGroupName = "stimuli";

        private readonly H5Group responsesGroup;
        private readonly H5Group stimuliGroup;

        private readonly Lazy<HashSet<H5PersistentResponse>> responses;
        private readonly Lazy<HashSet<H5PersistentStimulus>> stimuli; 

        public static H5PersistentEpoch CreateEpoch(H5Group parent, H5PersistentExperiment experiment, Epoch epoch)
        {
            var group = CreateTimelineEntityGroup(parent, "epoch", epoch.StartTime, (DateTimeOffset)epoch.StartTime + epoch.Duration);
            
            var responsesGroup = group.AddGroup(ResponsesGroupName);
            var stimuliGroup = group.AddGroup(StimuliGroupName);

            var devices = experiment.Devices.ToDictionary(d => d.Name, d => (H5PersistentDevice) d);

            foreach (var kv in epoch.Responses)
            {
                H5PersistentResponse.CreateResponse(responsesGroup, devices[kv.Key.Name], kv.Value);
            }

            foreach (var kv in epoch.Stimuli)
            {
                H5PersistentStimulus.CreateStimulus(stimuliGroup, devices[kv.Key.Name], kv.Value);
            }

            return new H5PersistentEpoch(group);
        }

        public H5PersistentEpoch(H5Group group) : base(group)
        {
            var subGroups = group.Groups.ToList();
            responsesGroup = subGroups.First(g => g.Name == ResponsesGroupName);
            stimuliGroup = subGroups.First(g => g.Name == StimuliGroupName);

            responses = new Lazy<HashSet<H5PersistentResponse>>(() => new HashSet<H5PersistentResponse>(responsesGroup.Groups.Select(g => new H5PersistentResponse(g))));
            stimuli = new Lazy<HashSet<H5PersistentStimulus>>(() => new HashSet<H5PersistentStimulus>(stimuliGroup.Groups.Select(g => new H5PersistentStimulus(g))));
        }

        public IEnumerable<IPersistentResponse> Responses { get { return responses.Value; } }

        public IEnumerable<IPersistentStimulus> Stimuli { get { return stimuli.Value; } }
    }

    abstract class H5PersistentIOBase : H5PersistentEntity, IPersistentIOBase
    {
        private const string DeviceGroupName = "device";
        private const string DataConfigurationSpansGroupName = "dataConfigurationSpans";
        private const string SpanGroupPrefix = "span_";
        private const string SpanStartTimeKey = "startTimeSeconds";
        private const string SpanDurationKey = "timeSpanSeconds";

        private readonly H5Group deviceGroup;
        private readonly H5Group dataConfigurationSpansGroup;

        private readonly Lazy<IPersistentDevice> device;
        private readonly Lazy<List<ConfigurationSpan>> configurationSpans;

        public static H5Group CreateIOBaseGroup(H5Group parent, H5PersistentDevice device, IEnumerable<IConfigurationSpan> configSpans)
        {
            var group = CreateEntityGroup(parent, device.Name);

            var deviceGroup = group.AddHardLink(DeviceGroupName, device.Group);
            var spansGroup = group.AddGroup(DataConfigurationSpansGroupName);

            uint i = 0;
            var totalTime = TimeSpan.Zero;
            foreach (var span in configSpans)
            {
                var spanGroup = spansGroup.AddGroup(SpanGroupPrefix + i++);

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
            }

            return group;
        }

        protected H5PersistentIOBase(H5Group group) : base(group)
        {
            var subGroups = group.Groups.ToList(); 
            deviceGroup = subGroups.First(g => g.Name == DeviceGroupName);
            dataConfigurationSpansGroup = subGroups.First(g => g.Name == DataConfigurationSpansGroupName);

            device = new Lazy<IPersistentDevice>(() => new H5PersistentDevice(deviceGroup));

            configurationSpans = new Lazy<List<ConfigurationSpan>>(() =>
            {
                var spanGroups = dataConfigurationSpansGroup.Groups.ToList();
                var spans = new List<ConfigurationSpan>(spanGroups.Count);
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

                    spans.Add(new ConfigurationSpan(duration, nodes));
                }
                return spans;
            });
        }

        public IPersistentDevice Device { get { return device.Value; } }

        public IEnumerable<IConfigurationSpan> ConfigurationSpans { get { return configurationSpans.Value; } }
    }

    class H5PersistentResponse : H5PersistentIOBase, IPersistentResponse
    {
        private const string SampleRateKey = "sampleRate";
        private const string SampleRateUnitsKey = "sampleRateUnits";
        private const string DataDatasetName = "data";

        private readonly H5Dataset dataDataset;

        private readonly Lazy<List<IMeasurement>> data;

        public static H5PersistentResponse CreateResponse(H5Group parent, H5PersistentDevice device, Response response)
        {
            var group = CreateIOBaseGroup(parent, device, response.DataConfigurationSpans);

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

            data = new Lazy<List<IMeasurement>>(() => dataDataset.GetData<H5Map.MeasurementT>().Select(H5Map.Convert).ToList());
        }

        public IMeasurement SampleRate { get; private set; }

        public IEnumerable<IMeasurement> Data { get { return data.Value; } }
    }

    class H5PersistentStimulus : H5PersistentIOBase, IPersistentStimulus
    {
        private const string StimulusIDKey = "stimulusID";
        private const string UnitsKey = "units";
        private const string ParametersGroupName = "parameters";

        private readonly H5Group parametersGroup;

        private readonly Lazy<Dictionary<string, object>> parameters;

        public static H5PersistentStimulus CreateStimulus(H5Group parent, H5PersistentDevice device, IStimulus stimulus)
        {
            var group = CreateIOBaseGroup(parent, device, stimulus.OutputConfigurationSpans);

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

        public IEnumerable<KeyValuePair<string, object>> Parameters { get { return parameters.Value; } }
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

        public static void CreateTypes(H5File file)
        {
            var stringType = file.CreateDatatype(StringTypeName, H5T.H5TClass.STRING, FixedStringLength);

            var dateTimeOffsetType = file.CreateDatatype(DateTimeOffsetTypeName,
                                                         new[] {"utcTicks", "offsetHours"},
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
                    ticks = n.Time.UtcTicks,
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
