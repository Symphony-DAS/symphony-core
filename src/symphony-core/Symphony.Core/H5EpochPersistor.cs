using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF5;
using HDF5DotNet;
using log4net;

namespace Symphony.Core
{
    /// <summary>
    /// IEpochPersistor implementation for persisting Epochs to an HDF5 data file. HDF5 does not currently
    /// offer atomic operations so, while this implementation does it's best to maintain data integrity it's
    /// not full-proof. In practice I think we'll find this sufficient. However this should probably be looked 
    /// at again when/if HDF5 begins to offer transactions.
    /// </summary>
    public class H5EpochPersistor : IEpochPersistor, IDisposable
    {
        private const string VersionKey = "version";
        private const uint PersistenceVersion = 2;

        private readonly H5File _file;

        private H5PersistentEntityFactory _entityFactory;
        private H5PersistentExperiment _experiment;
        private Stack<H5PersistentEpochGroup> _openEpochGroups;

        public static H5EpochPersistor Create(string filename, string purpose)
        {
            return Create(filename, purpose, DateTimeOffset.Now);
        }

        /// <summary>
        /// Creates a new H5EpochPersistor with a new HDF5 file.
        /// </summary>
        /// <param name="filename">Desired HDF5 path</param>
        /// <param name="purpose">Purpose for the root Experiment entity</param>
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
                H5PersistentExperiment.InsertExperiment(file, new H5PersistentEntityFactory(), purpose, startTime);
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

            _file = new H5File(filename);
            if (!_file.Attributes.ContainsKey(VersionKey))
                throw new FileLoadException("File does not have a version attribute. Are you sure this is a Symphony file?");

            Version = _file.Attributes[VersionKey];
            if (Version != PersistenceVersion)
                throw new FileLoadException("Version mismatch. This file may have been produced by an older version.");

            if (_file.Groups.Count() != 1)
                throw new FileLoadException("Expected a single top-level group. Are you sure this is a Symphony file?");

            _entityFactory = new H5PersistentEntityFactory();
            _experiment = _entityFactory.Create<H5PersistentExperiment>(_file.Groups.First());
            _openEpochGroups = new Stack<H5PersistentEpochGroup>();

            IsClosed = false;
        }

        ~H5EpochPersistor()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    CloseDocument();
                    GC.SuppressFinalize(this);
                }
            }
            _disposed = true;
        }

        public void Close()
        {
            Close(DateTimeOffset.Now);
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
            if (_experiment != null)
            {
                _experiment.SetEndTime(endTime);
                _experiment = null;
            }

            CloseDocument();
        }

        public void CloseDocument()
        {
            _file.Close();
            IsClosed = true;
        }

        public bool IsClosed { get; private set; }

        internal static ILog Log = LogManager.GetLogger(typeof(H5EpochPersistor));

        public uint Version { get; private set; }

        public IPersistentExperiment Experiment { get { return _experiment; } }

        public IPersistentDevice AddDevice(string name, string manufacturer)
        {
            return _experiment.InsertDevice(name, manufacturer);
        }

        public IPersistentSource AddSource(string label, IPersistentSource parent)
        {
            return parent == null
                       ? _experiment.InsertSource(label)
                       : ((H5PersistentSource) parent).InsertSource(label);
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source)
        {
            return BeginEpochGroup(label, source, DateTimeOffset.Now);
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source, DateTimeOffset startTime)
        {
            var epochGroup = CurrentEpochGroup == null
                       ? _experiment.InsertEpochGroup(label, (H5PersistentSource) source, startTime)
                       : ((H5PersistentEpochGroup) CurrentEpochGroup).InsertEpochGroup(label, (H5PersistentSource) source, startTime);

            _openEpochGroups.Push(epochGroup);
            return epochGroup;
        }

        public IPersistentEpochGroup EndEpochGroup()
        {
            return EndEpochGroup(DateTimeOffset.Now);
        }

        public IPersistentEpochGroup EndEpochGroup(DateTimeOffset endTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There are no open epoch groups");
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");
            
            ((H5PersistentEpochGroup) CurrentEpochGroup).SetEndTime(endTime);
            return _openEpochGroups.Pop();
        }

        public IPersistentEpochGroup CurrentEpochGroup
        {
            get { return _openEpochGroups.Count == 0 ? null : _openEpochGroups.Peek(); }
        }

        public IPersistentEpochBlock BeginEpochBlock(string protocolID, DateTimeOffset startTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There are no open epoch groups");
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");
            
            CurrentEpochBlock = ((H5PersistentEpochGroup) CurrentEpochGroup).InsertEpochBlock(protocolID, startTime);
            return CurrentEpochBlock;
        }

        public IPersistentEpochBlock EndEpochBlock(DateTimeOffset endTime)
        {
            if (CurrentEpochBlock == null)
                throw new InvalidOperationException("There is no open epoch block");

            var block = (H5PersistentEpochBlock) CurrentEpochBlock;
            block.SetEndTime(endTime);
            CurrentEpochBlock = null;

            return block;
        }

        public IPersistentEpochBlock CurrentEpochBlock { get; set; }

        public IPersistentEpoch Serialize(Epoch epoch)
        {
            if (CurrentEpochBlock == null)
                throw new InvalidOperationException("There is no open epoch block");

            return ((H5PersistentEpochBlock) CurrentEpochBlock).InsertEpoch(epoch);
        }

        public void Delete(IPersistentEntity entity)
        {
            if (entity.Equals(_experiment))
                throw new InvalidOperationException("You cannot delete the experiment");
            if (_openEpochGroups.Contains(entity))
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

        private H5Group _propertiesGroup;
        private H5Dataset _notesDataset;

        protected static H5Group InsertEntityGroup(H5Group container, string prefix)
        {
            var uuid = Guid.NewGuid();
            var group = container.AddGroup(prefix + "-" + uuid);
            try
            {
                group.Attributes[UUIDKey] = uuid.ToString();
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return group;
        }

        protected H5PersistentEntity(H5Group group, H5PersistentEntityFactory factory)
        {
            Group = group;
            EntityFactory = factory;

            UUID = GetUUID(group);

            _propertiesGroup = group.Groups.FirstOrDefault(g => g.Name == PropertiesGroupName);
            _notesDataset = group.Datasets.FirstOrDefault(ds => ds.Name == NotesDatasetName);
        }

        public H5PersistentEntityFactory EntityFactory { get; private set; }

        // The HDF5 group representing the persistent entity.
        public H5Group Group { get; private set; }

        public Guid UUID { get; private set; }

        public virtual void Delete()
        {
            Group.Delete();
        }

        public override bool Equals(object obj)
        {
            var e = obj as H5PersistentEntity;
            return e != null && UUID.Equals(e.UUID);
        }

        public override int GetHashCode()
        {
            return UUID.GetHashCode();
        }

        public static Guid GetUUID(H5Group entityGroup)
        {
            return new Guid(entityGroup.Attributes[UUIDKey]);
        }

        public IEnumerable<KeyValuePair<string, object>> Properties 
        { 
            get
            {
                return _propertiesGroup == null
                           ? Enumerable.Empty<KeyValuePair<string, object>>()
                           : _propertiesGroup.Attributes.Select(a => new KeyValuePair<string, object>(a.Name, a.GetValue()));
            } 
        }

        public void AddProperty(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be empty");
            if (value == null || (value is string && ((string) value).Length == 0))
                throw new ArgumentException("Value cannot be empty");
            if (!H5AttributeManager.IsSupportedType(value.GetType()))
                throw new ArgumentException(string.Format("Value ({0} : {1}) is of unsupported type", key, value));

            if (_propertiesGroup == null)
            {
                _propertiesGroup = Group.AddGroup(PropertiesGroupName);
            }

            _propertiesGroup.Attributes[key] = new H5Attribute(value);
            TryFlush();
        }

        public bool RemoveProperty(string key)
        {
            if (_propertiesGroup == null)
                return false;

            bool removed = _propertiesGroup.Attributes.Remove(key);
            TryFlush();

            return removed;
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

        public bool AddKeyword(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                throw new ArgumentException("Keyword cannot be empty");

            var newKeywords = new HashSet<string>(Keywords);
            bool added = newKeywords.Add(keyword);

            Group.Attributes[KeywordsKey] = string.Join(",", newKeywords);
            TryFlush();

            return added;
        }

        public bool RemoveKeyword(string keyword)
        {
            if (!Keywords.Contains(keyword))
                return false;

            var newKeywords = new HashSet<string>(Keywords);
            bool removed = newKeywords.Remove(keyword);

            if (newKeywords.Any())
            {
                Group.Attributes[KeywordsKey] = string.Join(",", newKeywords);
            }
            else
            {
                Group.Attributes.Remove(KeywordsKey);
            }
            TryFlush();

            return removed;
        }

        public IEnumerable<INote> Notes
        {
            get
            {
                return _notesDataset == null
                           ? Enumerable.Empty<INote>()
                           : _notesDataset.GetData<H5Map.NoteT>().Select(H5Map.Convert);
            }
        }

        public INote AddNote(DateTimeOffset time, string text)
        {
            return AddNote(new H5Note(time, text));
        }

        public INote AddNote(INote note)
        {
            if (note == null)
                throw new ArgumentException("Note cannot be null");

            if (_notesDataset == null)
            {
                _notesDataset = Group.AddDataset(NotesDatasetName, H5Map.GetNoteType(Group.File), new[] {0L}, new[] {-1L}, new[] {64L});
            }

            long n = _notesDataset.NumberOfElements;
            _notesDataset.Extend(new[] {n + 1});
            var nt = H5Map.Convert(note);
            try
            {
                _notesDataset.SetData(new[] {nt}, new[] {n}, new[] {1L});
            }
            finally
            {
                H5Map.Free(nt);
            }
            TryFlush();

            return note;
        }

        protected void TryFlush()
        {
            try
            {
                Group.Flush();
            }
            catch (Exception x)
            {
                H5EpochPersistor.Log.WarnFormat("Unable to flush buffers to disk: {0}", x.Message);
            }
        }
    }

    class H5PersistentDevice : H5PersistentEntity, IPersistentDevice
    {
        private const string NameKey = "name";
        private const string ManufacturerKey = "manufacturer";
        private const string ExperimentGroupName = "experiment";

        private readonly H5Group _experimentGroup;

        public static H5PersistentDevice InsertDevice(H5Group container, H5PersistentEntityFactory factory, H5PersistentExperiment experiment, string name, string manufacturer)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Device name cannot be empty");
            if (string.IsNullOrEmpty(manufacturer))
                throw new ArgumentException("Device manufacturer cannot be empty");

            var group = InsertEntityGroup(container, name);
            try
            {
                group.Attributes[NameKey] = name;
                group.Attributes[ManufacturerKey] = manufacturer;

                group.AddHardLink(ExperimentGroupName, experiment.Group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return factory.Create<H5PersistentDevice>(group);
        }

        public H5PersistentDevice(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            Name = group.Attributes[NameKey];
            Manufacturer = group.Attributes[ManufacturerKey];

            _experimentGroup = group.Groups.First(g => g.Name == ExperimentGroupName);
        }

        public string Name { get; private set; }

        public string Manufacturer { get; private set; }

        public IPersistentExperiment Experiment
        {
            get { return EntityFactory.Create<H5PersistentExperiment>(_experimentGroup); }
        }
    }

    class H5PersistentSource : H5PersistentEntity, IPersistentSource
    {
        private const string LabelKey = "label";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";
        private const string ParentGroupName = "parent";
        private const string ExperimentGroupName = "experiment";

        private readonly H5Group _sourcesGroup;
        private readonly H5Group _epochGroupsGroup;
        private readonly H5Group _parentGroup;
        private readonly H5Group _experimentGroup;

        public static H5PersistentSource InsertSource(H5Group container, H5PersistentEntityFactory factory, H5PersistentSource parent, H5PersistentExperiment experiment, string label)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Source label cannot be empty");

            var group = InsertEntityGroup(container, label);
            try
            {
                group.Attributes[LabelKey] = label;

                group.AddGroup(SourcesGroupName);
                group.AddGroup(EpochGroupsGroupName);
                group.AddHardLink(ExperimentGroupName, experiment.Group);

                if (parent != null)
                {
                    group.AddHardLink(ParentGroupName, parent.Group);
                }
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return factory.Create<H5PersistentSource>(group);
        }

        public H5PersistentSource(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            Label = group.Attributes[LabelKey];
            
            var subGroups = Group.Groups.ToList();
            _sourcesGroup = subGroups.First(g => g.Name == SourcesGroupName);
            _epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
            _parentGroup = subGroups.FirstOrDefault(g => g.Name == ParentGroupName);
            _experimentGroup = subGroups.First(g => g.Name == ExperimentGroupName);
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
            get { return _sourcesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentSource>(g)); }
        }

        public IEnumerable<IPersistentSource> AllSources
        {
            get { return Sources.Aggregate(Sources, (current, source) => current.Concat(source.AllSources)); }
        }

        public H5PersistentSource InsertSource(string label)
        {
            var source = InsertSource(_sourcesGroup, EntityFactory, this, (H5PersistentExperiment) Experiment, label);
            TryFlush();

            return source;
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return _epochGroupsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochGroup>(g)); }
        }

        public IEnumerable<IPersistentEpochGroup> AllEpochGroups
        {
            get { return Sources.Aggregate(EpochGroups, (current, source) => current.Concat(source.AllEpochGroups)); }
        }

        public void AddEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            _epochGroupsGroup.AddHardLink(epochGroup.Group.Name, epochGroup.Group);
        }

        public bool RemoveEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            var eg = _epochGroupsGroup.Groups.FirstOrDefault(g => g.Name == epochGroup.Group.Name);
            if (eg == null)
                return false;

            eg.Delete();
            return true;
        }

        public IPersistentSource Parent
        {
            get { return _parentGroup == null ? null : EntityFactory.Create<H5PersistentSource>(_parentGroup); }
        }

        public IPersistentExperiment Experiment
        {
            get { return EntityFactory.Create<H5PersistentExperiment>(_experimentGroup); }
        }
    }

    abstract class H5TimelinePersistentEntity : H5PersistentEntity, ITimelinePersistentEntity
    {
        private const string StartTimeTicksKey = "startTimeDotNetDateTimeOffsetTicks";
        private const string StartTimeOffsetHoursKey = "startTimeDotNetDateTimeOffsetOffsetHours";
        private const string EndTimeTicksKey = "endTimeDotNetDateTimeOffsetTicks";
        private const string EndTimeOffsetHoursKey = "endTimeDotNetDateTimeOffsetOffsetHours";

        protected static H5Group InsertTimelineEntityGroup(H5Group container, string prefix, DateTimeOffset startTime)
        {
            var group = InsertEntityGroup(container, prefix);
            try
            {
                group.Attributes[StartTimeTicksKey] = startTime.Ticks;
                group.Attributes[StartTimeOffsetHoursKey] = startTime.Offset.TotalHours;
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return group;
        }

        protected static H5Group InsertTimelineEntityGroup(H5Group container, string prefix, DateTimeOffset startTime,
                                                           DateTimeOffset endTime)
        {
            var group = InsertTimelineEntityGroup(container, prefix, startTime);
            try
            {
                group.Attributes[EndTimeTicksKey] = endTime.Ticks;
                group.Attributes[EndTimeOffsetHoursKey] = endTime.Offset.TotalHours;
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return group;
        }

        protected H5TimelinePersistentEntity(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            var attr = group.Attributes;
            StartTime = new DateTimeOffset(attr[StartTimeTicksKey], TimeSpan.FromHours(attr[StartTimeOffsetHoursKey]));
            if (attr.ContainsKey(EndTimeTicksKey) && attr.ContainsKey(EndTimeOffsetHoursKey))
            {
                EndTime = new DateTimeOffset(attr[EndTimeTicksKey], TimeSpan.FromHours(attr[EndTimeOffsetHoursKey]));
            }
        }

        public DateTimeOffset StartTime { get; private set; }

        public DateTimeOffset? EndTime { get; private set; }

        public void SetEndTime(DateTimeOffset time)
        {
            Group.Attributes[EndTimeTicksKey] = time.Ticks;
            Group.Attributes[EndTimeOffsetHoursKey] = time.Offset.TotalHours;
            TryFlush();
            
            EndTime = time;
        }
    }

    class H5PersistentExperiment : H5TimelinePersistentEntity, IPersistentExperiment
    {
        private const string PurposeKey = "purpose";
        private const string DevicesGroupName = "devices";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";

        private readonly H5Group _devicesGroup;
        private readonly H5Group _sourcesGroup;
        private readonly H5Group _epochGroupsGroup;

        public static H5PersistentExperiment InsertExperiment(H5Group container, H5PersistentEntityFactory factory, string purpose, DateTimeOffset startTime)
        {
            if (string.IsNullOrEmpty(purpose))
                throw new ArgumentException("Purpose cannot be empty");

            var group = InsertTimelineEntityGroup(container, "experiment", startTime);
            try
            {
                group.Attributes[PurposeKey] = purpose;

                group.AddGroup(DevicesGroupName);
                group.AddGroup(SourcesGroupName);
                group.AddGroup(EpochGroupsGroupName);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return factory.Create<H5PersistentExperiment>(group);
        }

        public H5PersistentExperiment(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            Purpose = group.Attributes[PurposeKey];

            var subGroups = group.Groups.ToList();
            _devicesGroup = subGroups.First(g => g.Name == DevicesGroupName);
            _sourcesGroup = subGroups.First(g => g.Name == SourcesGroupName);
            _epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
        }

        public string Purpose { get; private set; }

        public IEnumerable<IPersistentDevice> Devices
        {
            get { return _devicesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentDevice>(g)); }
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
            
            var device = H5PersistentDevice.InsertDevice(_devicesGroup, EntityFactory, this, name, manufacturer);
            TryFlush();

            return device;
        }

        public IEnumerable<IPersistentSource> Sources
        {
            get { return _sourcesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentSource>(g)); }
        }

        public IEnumerable<IPersistentSource> AllSources
        {
            get { return Sources.Aggregate(Sources, (current, source) => current.Concat(source.AllSources)); }
        }

        public H5PersistentSource InsertSource(string label)
        {
            var source = H5PersistentSource.InsertSource(_sourcesGroup, EntityFactory, null, this, label);
            TryFlush();

            return source;
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return _epochGroupsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochGroup>(g)); }
        }

        public H5PersistentEpochGroup InsertEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var group = H5PersistentEpochGroup.InsertEpochGroup(_epochGroupsGroup, EntityFactory, null, this, label, source, startTime);
            TryFlush();

            return group;
        }
    }

    class H5PersistentEpochGroup : H5TimelinePersistentEntity, IPersistentEpochGroup
    {
        private const string LabelKey = "label";
        private const string SourceGroupName = "source";
        private const string EpochGroupsGroupName = "epochGroups";
        private const string EpochBlocksGroupName = "epochBlocks";
        private const string ParentGroupName = "parent";
        private const string ExperimentGroupName = "experiment";
        
        private readonly H5Group _sourceGroup;
        private readonly H5Group _epochGroupsGroup;
        private readonly H5Group _epochBlocksGroup;
        private readonly H5Group _parentGroup;
        private readonly H5Group _experimentGroup;

        public static H5PersistentEpochGroup InsertEpochGroup(H5Group container, H5PersistentEntityFactory factory, H5PersistentEpochGroup parent, H5PersistentExperiment experiment, string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Epoch group label cannot be empty");
            if (source == null)
                throw new ArgumentException("Epoch group source cannot be null");

            H5PersistentEpochGroup epochGroup = null;

            var group = InsertTimelineEntityGroup(container, label, startTime);
            try
            {
                group.Attributes[LabelKey] = label;

                group.AddHardLink(SourceGroupName, source.Group);
                group.AddGroup(EpochGroupsGroupName);
                group.AddGroup(EpochBlocksGroupName);
                group.AddHardLink(ExperimentGroupName, experiment.Group);

                if (parent != null)
                {
                    group.AddHardLink(ParentGroupName, parent.Group);
                }

                epochGroup = factory.Create<H5PersistentEpochGroup>(group);
                source.AddEpochGroup(epochGroup);
            }
            catch (Exception x)
            {
                source.RemoveEpochGroup(epochGroup);
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            
            return epochGroup;
        }

        public H5PersistentEpochGroup(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            Label = group.Attributes[LabelKey];

            var subGroups = group.Groups.ToList();
            _sourceGroup = subGroups.First(g => g.Name == SourceGroupName);
            _epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
            _epochBlocksGroup = subGroups.First(g => g.Name == EpochBlocksGroupName);
            _parentGroup = subGroups.FirstOrDefault(g => g.Name == ParentGroupName);
            _experimentGroup = subGroups.First(g => g.Name == ExperimentGroupName);
        }

        public override void Delete()
        {
            foreach (var g in EpochGroups.ToList())
            {
                ((H5PersistentEpochGroup)g).Delete();
            }
            ((H5PersistentSource) Source).RemoveEpochGroup(this);
            base.Delete();
        }

        public string Label { get; private set; }

        public IPersistentSource Source
        {
            get { return EntityFactory.Create<H5PersistentSource>(_sourceGroup); }
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return _epochGroupsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochGroup>(g)); }
        }

        public H5PersistentEpochGroup InsertEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var group = InsertEpochGroup(_epochGroupsGroup, EntityFactory, this, (H5PersistentExperiment) Experiment, label, source, startTime);
            TryFlush();

            return group;
        }

        public IEnumerable<IPersistentEpochBlock> EpochBlocks
        {
            get { return _epochBlocksGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochBlock>(g)); }
        }

        public H5PersistentEpochBlock InsertEpochBlock(string protocolID, DateTimeOffset startTime)
        {
            var block = H5PersistentEpochBlock.InsertEpochBlock(_epochBlocksGroup, EntityFactory, this, protocolID, startTime);
            TryFlush();

            return block;
        }

        public IPersistentEpochGroup Parent
        {
            get { return _parentGroup == null ? null : EntityFactory.Create<H5PersistentEpochGroup>(_parentGroup); }
        }

        public IPersistentExperiment Experiment
        {
            get { return EntityFactory.Create<H5PersistentExperiment>(_experimentGroup); }
        }
    }

    class H5PersistentEpochBlock : H5TimelinePersistentEntity, IPersistentEpochBlock
    {
        private const string ProtocolIDKey = "protocolID";
        private const string EpochsGroupName = "epochs";
        private const string EpochGroupGroupName = "epochGroup";

        private readonly H5Group _epochsGroup;
        private readonly H5Group _epochGroupGroup;

        public static H5PersistentEpochBlock InsertEpochBlock(H5Group container, H5PersistentEntityFactory factory, H5PersistentEpochGroup epochGroup, string protocolID, DateTimeOffset startTime)
        {
            if (string.IsNullOrEmpty(protocolID))
                throw new ArgumentException("Epoch block protocol id cannot be empty");

            var group = InsertTimelineEntityGroup(container, protocolID, startTime);
            try
            {
                group.Attributes[ProtocolIDKey] = protocolID;

                group.AddGroup(EpochsGroupName);
                group.AddHardLink(EpochGroupGroupName, epochGroup.Group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return factory.Create<H5PersistentEpochBlock>(group);
        }

        public H5PersistentEpochBlock(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            ProtocolID = group.Attributes[ProtocolIDKey];

            var subGroups = group.Groups.ToList();
            _epochsGroup = subGroups.First(g => g.Name == EpochsGroupName);
            _epochGroupGroup = subGroups.First(g => g.Name == EpochGroupGroupName);
        }

        public string ProtocolID { get; private set; }

        public IEnumerable<IPersistentEpoch> Epochs
        {
            get { return _epochsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpoch>(g)); }
        }

        public H5PersistentEpoch InsertEpoch(Epoch epoch)
        {
            if (epoch.ProtocolID != ProtocolID)
                throw new ArgumentException("Epoch protocol id does not match epoch block protocol id");
            
            var pEpoch = H5PersistentEpoch.InsertEpoch(_epochsGroup, EntityFactory, this, epoch);
            TryFlush();

            return pEpoch;
        }

        public IPersistentEpochGroup EpochGroup
        {
            get { return EntityFactory.Create<H5PersistentEpochGroup>(_epochGroupGroup); }
        }
    }

    class H5PersistentEpoch : H5TimelinePersistentEntity, IPersistentEpoch
    {
        private const string BackgroundsGroupName = "backgrounds";
        private const string ProtocolParametersGroupName = "protocolParameters";
        private const string ResponsesGroupName = "responses";
        private const string StimuliGroupName = "stimuli";
        private const string EpochBlockGroupName = "epochBlock";

        private readonly H5Group _backgroundGroup;
        private readonly H5Group _protocolParametersGroup;
        private readonly H5Group _responsesGroup;
        private readonly H5Group _stimuliGroup;
        private readonly H5Group _epochBlockGroup;

        public static H5PersistentEpoch InsertEpoch(H5Group container, H5PersistentEntityFactory factory, H5PersistentEpochBlock block, Epoch epoch)
        {
            H5PersistentEpoch persistentEpoch;

            var group = InsertTimelineEntityGroup(container, "epoch", epoch.StartTime, (DateTimeOffset)epoch.StartTime + epoch.Duration);
            try
            {
                var backgroundsGroup = group.AddGroup(BackgroundsGroupName);
                var parametersGroup = group.AddGroup(ProtocolParametersGroupName);
                var responsesGroup = group.AddGroup(ResponsesGroupName);
                var stimuliGroup = group.AddGroup(StimuliGroupName);
                group.AddHardLink(EpochBlockGroupName, block.Group);

                var experiment = (H5PersistentExperiment)block.EpochGroup.Experiment;

                persistentEpoch = factory.Create<H5PersistentEpoch>(group);

                // ToList() everything before enumerating to guard against external collection modification
                // causing exceptions during serialization

                foreach (var kv in epoch.Backgrounds.ToList())
                {
                    var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                    H5PersistentBackground.InsertBackground(backgroundsGroup, factory, persistentEpoch, device, kv.Value);
                }

                foreach (var kv in epoch.ProtocolParameters.ToList())
                {
                    if (H5AttributeManager.IsSupportedType(kv.Value.GetType()))
                    {
                        parametersGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
                    }
                    else
                    {
                        H5EpochPersistor.Log.WarnFormat("Protocol parameter value ({0} : {1}) is of usupported type. Falling back to string representation.", kv.Key, kv.Value);
                        parametersGroup.Attributes[kv.Key] = kv.Value.ToString();
                    }
                }

                foreach (var kv in epoch.Responses.ToList())
                {
                    var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                    H5PersistentResponse.InsertResponse(responsesGroup, factory, persistentEpoch, device, kv.Value);
                }

                foreach (var kv in epoch.Stimuli.ToList())
                {
                    var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                    H5PersistentStimulus.InsertStimulus(stimuliGroup, factory, persistentEpoch, device, kv.Value);
                }

                foreach (var keyword in epoch.Keywords.ToList())
                {
                    persistentEpoch.AddKeyword(keyword);
                }
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return persistentEpoch;
        }

        public H5PersistentEpoch(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            var subGroups = group.Groups.ToList();
            _backgroundGroup = subGroups.First(g => g.Name == BackgroundsGroupName);
            _protocolParametersGroup = subGroups.First(g => g.Name == ProtocolParametersGroupName);
            _responsesGroup = subGroups.First(g => g.Name == ResponsesGroupName);
            _stimuliGroup = subGroups.First(g => g.Name == StimuliGroupName);
            _epochBlockGroup = subGroups.First(g => g.Name == EpochBlockGroupName);
        }

        public IEnumerable<IPersistentBackground> Backgrounds
        {
            get { return _backgroundGroup.Groups.Select(g => EntityFactory.Create<H5PersistentBackground>(g)); }
        }

        public IEnumerable<KeyValuePair<string, object>> ProtocolParameters
        {
            get { return _protocolParametersGroup.Attributes.Select(a => new KeyValuePair<string, object>(a.Name, a.GetValue())); }
        }

        public IEnumerable<IPersistentResponse> Responses
        {
            get { return _responsesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentResponse>(g)); }
        }

        public IEnumerable<IPersistentStimulus> Stimuli
        {
            get { return _stimuliGroup.Groups.Select(g => EntityFactory.Create<H5PersistentStimulus>(g)); }
        }

        public IPersistentEpochBlock EpochBlock
        {
            get { return EntityFactory.Create<H5PersistentEpochBlock>(_epochBlockGroup); }
        }
    }

    class H5PersistentBackground : H5PersistentEntity, IPersistentBackground
    {
        private const string ValueKey = "value";
        private const string ValueUnitsKey = "valueUnits";
        private const string SampleRateKey = "sampleRate";
        private const string SampleRateUnitsKey = "sampleRateUnits";
        private const string DeviceGroupName = "device";

        private readonly H5Group _deviceGroup;

        public static H5PersistentBackground InsertBackground(H5Group container, H5PersistentEntityFactory factory, H5PersistentEpoch epoch, H5PersistentDevice device, Background background)
        {
            var group = InsertEntityGroup(container, device.Name);
            try
            {
                group.Attributes[ValueKey] = (double)background.Value.QuantityInBaseUnit;
                group.Attributes[ValueUnitsKey] = background.Value.BaseUnit;
                group.Attributes[SampleRateKey] = (double)background.SampleRate.QuantityInBaseUnit;
                group.Attributes[SampleRateUnitsKey] = background.SampleRate.BaseUnit;

                group.AddHardLink(DeviceGroupName, device.Group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return factory.Create<H5PersistentBackground>(group);
        }

        public H5PersistentBackground(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            double value = group.Attributes[ValueKey];
            string valueUnits = group.Attributes[ValueUnitsKey];
            Value = new Measurement(value, valueUnits);

            double sampleRate = group.Attributes[SampleRateKey];
            string sampleRateUnits = group.Attributes[SampleRateUnitsKey];
            SampleRate = new Measurement(sampleRate, sampleRateUnits);

            _deviceGroup = group.Groups.First(g => g.Name == DeviceGroupName);
        }

        public IPersistentDevice Device
        {
            get { return EntityFactory.Create<H5PersistentDevice>(_deviceGroup); }
        }

        public IMeasurement Value { get; private set; }

        public IMeasurement SampleRate { get; private set; }
    }

    abstract class H5PersistentIOBase : H5PersistentEntity, IPersistentIOBase
    {
        private const string DeviceGroupName = "device";
        private const string DataConfigurationSpansGroupName = "dataConfigurationSpans";
        private const string SpanGroupPrefix = "span_";
        private const string SpanIndexKey = "index";
        private const string SpanStartTimeKey = "startTimeSeconds";
        private const string SpanDurationKey = "timeSpanSeconds";

        private readonly H5Group _deviceGroup;
        private readonly H5Group _dataConfigurationSpansGroup;

        public static H5Group InsertIOBaseGroup(H5Group container, H5PersistentEpoch epoch, H5PersistentDevice device, IEnumerable<IConfigurationSpan> configSpans)
        {
            var group = InsertEntityGroup(container, device.Name);
            try
            {
                group.AddHardLink(DeviceGroupName, device.Group);
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
                            if (H5AttributeManager.IsSupportedType(kv.Value.GetType()))
                            {
                                nodeGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
                            }
                            else if (kv.Value is IMeasurement)
                            {
                                var m = (IMeasurement) kv.Value;
                                nodeGroup.Attributes[kv.Key + "_quantity"] = (double) m.Quantity;
                                nodeGroup.Attributes[kv.Value + "_units"] = m.DisplayUnit;
                                nodeGroup.Attributes[kv.Key] = m.ToString();
                            }
                            else
                            {
                                H5EpochPersistor.Log.WarnFormat("Node configuration value ({0} : {1}) is of usupported type. Falling back to string representation.", kv.Key, kv.Value);
                                nodeGroup.Attributes[kv.Key] = kv.Value.ToString();
                            }
                        }
                    }

                    i++;
                }
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return group;
        }

        protected H5PersistentIOBase(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            var subGroups = group.Groups.ToList(); 
            _deviceGroup = subGroups.First(g => g.Name == DeviceGroupName);
            _dataConfigurationSpansGroup = subGroups.First(g => g.Name == DataConfigurationSpansGroupName);
        }

        public IPersistentDevice Device
        {
            get { return EntityFactory.Create<H5PersistentDevice>(_deviceGroup); }
        }

        public IEnumerable<IConfigurationSpan> ConfigurationSpans
        {
            get
            {
                var spanGroups = _dataConfigurationSpansGroup.Groups.ToList();
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
        private const string InputTimeTicksKey = "inputTimeDotNetDateTimeOffsetTicks";
        private const string InputTimeOffsetHoursKey = "inputTimeDotNetDateTimeOffsetOffsetHours";
        private const string DataDatasetName = "data";

        private readonly H5Dataset _dataDataset;

        public static H5PersistentResponse InsertResponse(H5Group container, H5PersistentEntityFactory factory, H5PersistentEpoch epoch, H5PersistentDevice device, Response response)
        {
            var group = InsertIOBaseGroup(container, epoch, device, response.DataConfigurationSpans);
            try
            {
                group.Attributes[SampleRateKey] = (double)response.SampleRate.QuantityInBaseUnit;
                group.Attributes[SampleRateUnitsKey] = response.SampleRate.BaseUnit;
                group.Attributes[InputTimeTicksKey] = response.InputTime.Ticks;
                group.Attributes[InputTimeOffsetHoursKey] = response.InputTime.Offset.TotalHours;

                group.AddDataset(DataDatasetName, H5Map.GetMeasurementType(container.File), response.Data.Select(H5Map.Convert).ToArray());
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return factory.Create<H5PersistentResponse>(group);
        }

        public H5PersistentResponse(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            double rate = group.Attributes[SampleRateKey];
            string units = group.Attributes[SampleRateUnitsKey];
            SampleRate = new Measurement(rate, units);

            long ticks = group.Attributes[InputTimeTicksKey];
            double offset = group.Attributes[InputTimeOffsetHoursKey];
            InputTime = new DateTimeOffset(ticks, TimeSpan.FromHours(offset));

            _dataDataset = group.Datasets.First(ds => ds.Name == DataDatasetName);
        }

        public IMeasurement SampleRate { get; private set; }

        public DateTimeOffset InputTime { get; private set; }

        public IEnumerable<IMeasurement> Data
        {
            get { return _dataDataset.GetData<H5Map.MeasurementT>().Select(H5Map.Convert); }
        }
    }

    class H5PersistentStimulus : H5PersistentIOBase, IPersistentStimulus
    {
        private const string StimulusIDKey = "stimulusID";
        private const string UnitsKey = "units";
        private const string SampleRateKey = "sampleRate";
        private const string SampleRateUnitsKey = "sampleRateUnits";
        private const string ParametersGroupName = "parameters";
        private const string DurationKey = "durationSeconds";
        private const string DataDatasetName = "data";

        private readonly H5Group _parametersGroup;
        private readonly H5Dataset _dataDataset;

        public static H5PersistentStimulus InsertStimulus(H5Group container, H5PersistentEntityFactory factory, H5PersistentEpoch epoch, H5PersistentDevice device, IStimulus stimulus)
        {
            var group = InsertIOBaseGroup(container, epoch, device, stimulus.OutputConfigurationSpans);
            try
            {
                group.Attributes[StimulusIDKey] = stimulus.StimulusID;
                group.Attributes[UnitsKey] = stimulus.Units;
                group.Attributes[SampleRateKey] = (double)stimulus.SampleRate.QuantityInBaseUnit;
                group.Attributes[SampleRateUnitsKey] = stimulus.SampleRate.BaseUnit;
                if (stimulus.Duration.IsSome())
                {
                    group.Attributes[DurationKey] = ((TimeSpan)stimulus.Duration).TotalSeconds;
                }

                var parametersGroup = group.AddGroup(ParametersGroupName);

                foreach (var kv in stimulus.Parameters.ToList())
                {
                    if (H5AttributeManager.IsSupportedType(kv.Value.GetType()))
                    {
                        parametersGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
                    }
                    else
                    {
                        H5EpochPersistor.Log.WarnFormat("Stimulus parameter value ({0} : {1}) is of usupported type. Falling back to string representation.", kv.Key, kv.Value);
                        parametersGroup.Attributes[kv.Key] = kv.Value.ToString();
                    }
                    
                }

                if (stimulus.Data.IsSome())
                {
                    IEnumerable<IMeasurement> data = stimulus.Data.Get();
                    group.AddDataset(DataDatasetName, H5Map.GetMeasurementType(container.File), data.Select(H5Map.Convert).ToArray());
                }
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(x.Message);
            }
            return factory.Create<H5PersistentStimulus>(group);
        }

        public H5PersistentStimulus(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            StimulusID = group.Attributes[StimulusIDKey];
            Units = group.Attributes[UnitsKey];

            double rate = group.Attributes[SampleRateKey];
            string units = group.Attributes[SampleRateUnitsKey];
            SampleRate = new Measurement(rate, units);

            Duration = group.Attributes.ContainsKey(DurationKey)
                           ? Option<TimeSpan>.Some(TimeSpan.FromSeconds(group.Attributes[DurationKey]))
                           : Option<TimeSpan>.None();

            _parametersGroup = group.Groups.First(g => g.Name == ParametersGroupName);
            _dataDataset = group.Datasets.FirstOrDefault(g => g.Name == DataDatasetName);
        }

        public string StimulusID { get; private set; }

        public string Units { get; private set; }

        public IMeasurement SampleRate { get; private set; }

        public IEnumerable<KeyValuePair<string, object>> Parameters
        {
            get { return _parametersGroup.Attributes.Select(a => new KeyValuePair<string, object>(a.Name, a.GetValue())); }
        }

        public Option<TimeSpan> Duration { get; private set; }

        public Option<IEnumerable<IMeasurement>> Data
        {
            get
            {
                return _dataDataset == null
                           ? Option<IEnumerable<IMeasurement>>.None()
                           : Option<IEnumerable<IMeasurement>>.Some(_dataDataset.GetData<H5Map.MeasurementT>().Select(H5Map.Convert));
            }
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
            if (obj.GetType() != GetType()) return false;
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

    class H5PersistentEntityFactory
    {
        private readonly Dictionary<Guid, H5PersistentEntity> _cache;

        public H5PersistentEntityFactory()
        {
            _cache = new Dictionary<Guid, H5PersistentEntity>();

            Constructor<H5PersistentDevice>.Func = (g, f) => new H5PersistentDevice(g, f);
            Constructor<H5PersistentSource>.Func = (g, f) => new H5PersistentSource(g, f);
            Constructor<H5PersistentExperiment>.Func = (g, f) => new H5PersistentExperiment(g, f);
            Constructor<H5PersistentEpochGroup>.Func = (g, f) => new H5PersistentEpochGroup(g, f);
            Constructor<H5PersistentEpochBlock>.Func = (g, f) => new H5PersistentEpochBlock(g, f);
            Constructor<H5PersistentEpoch>.Func = (g, f) => new H5PersistentEpoch(g, f);
            Constructor<H5PersistentBackground>.Func = (g, f) => new H5PersistentBackground(g, f);
            Constructor<H5PersistentResponse>.Func = (g, f) => new H5PersistentResponse(g, f);
            Constructor<H5PersistentStimulus>.Func = (g, f) => new H5PersistentStimulus(g, f);
        }

        public T Create<T>(H5Group group) where T : H5PersistentEntity
        {
            Guid uuid = H5PersistentEntity.GetUUID(group);
            if (_cache.ContainsKey(uuid))
                return (T) _cache[uuid];

            T entity = Constructor<T>.Func(group, this);
            _cache.Add(uuid, entity);

            return entity;
        }

        static class Constructor<T>
        {
            public static Func<H5Group, H5PersistentEntityFactory, T> Func { get; set; } 
        }
    }

    /// <summary>
    /// Conversion routines to turn our .NET objects into HDF5 friendly structures and vice versa.
    /// </summary>
    static class H5Map
    {
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct DateTimeOffsetT
        {
            [FieldOffset(0)]
            public long ticks;
            [FieldOffset(8)]
            public double offset;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public unsafe struct NoteT
        {
            [FieldOffset(0)]
            public DateTimeOffsetT time;
            [FieldOffset(16)]
            public byte* text;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public unsafe struct MeasurementT
        {
            [FieldOffset(0)]
            public double quantity;
            [FieldOffset(8)]
            public fixed byte unit[UnitsStringLength];
        }

        private const int UnitsStringLength = 10;

        private const string DateTimeOffsetTypeName = "DATETIMEOFFSET";
        private const string NoteTextTypeName = "NOTE_TEXT";
        private const string NoteTypeName = "NOTE";
        private const string UnitsTypeName = "UNITS";
        private const string MeasurementTypeName = "MEASUREMENT";

        public static void InsertTypes(H5File file)
        {
            var dateTimeOffsetType = file.CreateDatatype(DateTimeOffsetTypeName,
                                                         new[] {"ticks", "offsetHours"},
                                                         new[]
                                                             {
                                                                 new H5Datatype(H5T.H5Type.NATIVE_LLONG),
                                                                 new H5Datatype(H5T.H5Type.NATIVE_DOUBLE)
                                                             });

            var noteTextType = file.CreateDatatype(NoteTextTypeName, H5T.H5TClass.STRING, -1);

            file.CreateDatatype(NoteTypeName,
                                new[] {"time", "text"},
                                new[] {dateTimeOffsetType, noteTextType});

            var unitsType = file.CreateDatatype(UnitsTypeName, H5T.H5TClass.STRING, UnitsStringLength);

            file.CreateDatatype(MeasurementTypeName,
                                new[] {"quantity", "units"},
                                new[] {new H5Datatype(H5T.H5Type.NATIVE_DOUBLE), unitsType});
        }

        public static H5Datatype GetNoteType(H5File file)
        {
            return file.Datatypes.First(t => t.Name == NoteTypeName);
        }

        public static H5Datatype GetMeasurementType(H5File file)
        {
            return file.Datatypes.First(t => t.Name == MeasurementTypeName);
        }

        // The returned NoteT must be freed using Free() when it is no longer in use.
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
            unsafe
            {
                nt.text = (byte*) Marshal.StringToHGlobalAnsi(n.Text);
            }
            return nt;
        }

        public static unsafe void Free(NoteT nt)
        {
            if (((IntPtr)nt.text) != IntPtr.Zero)
            {
                Marshal.FreeHGlobal((IntPtr) nt.text);
                nt.text = (byte*) IntPtr.Zero;
            }
        }

        public static INote Convert(NoteT nt)
        {
            long ticks = nt.time.ticks;
            double offset = nt.time.offset;
            var time = new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            string text;
            unsafe
            {
                text = Marshal.PtrToStringAnsi((IntPtr) nt.text);
            }
            return new H5Note(time, text);
        }

        public static MeasurementT Convert(IMeasurement m)
        {
            var mt = new MeasurementT {quantity = (double) m.Quantity};
            var unitdata = Encoding.ASCII.GetBytes(m.DisplayUnit);
            unsafe
            {
                Marshal.Copy(unitdata, 0, (IntPtr) mt.unit, Math.Min(unitdata.Length, UnitsStringLength));
            }
            return mt;
        }

        public static IMeasurement Convert(MeasurementT mt)
        {
            string unit;
            unsafe
            {
                unit = Marshal.PtrToStringAnsi((IntPtr) mt.unit);
            }
            return new Measurement(mt.quantity, unit);
        }
    }

}
