using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
        private const string SymphonyVersionKey = "symphonyVersion";
        private const string CompressionKey = "compression";
        private const uint PersistenceVersion = 2;

        private readonly H5File _file;

        private readonly H5PersistentExperiment _experiment;
        private readonly H5PersistentEntityFactory _entityFactory;
        private readonly Stack<H5PersistentEpochGroup> _openEpochGroups;

        public static H5EpochPersistor Create(string filename)
        {
            return Create(filename, DateTimeOffset.Now);
        }

        /// <summary>
        /// Creates a new H5EpochPersistor with a new HDF5 file.
        /// </summary>
        /// <param name="filename">Desired HDF5 path</param>
        /// <param name="startTime">Start time for the root Experiment entity</param>
        /// <param name="compression">Automatically numeric data compression (0 = none, 9 = maximum)</param>
        /// <returns>The new Epoch Persistor</returns>
        public static H5EpochPersistor Create(string filename, DateTimeOffset startTime, uint compression = 9)
        {
            if (File.Exists(filename))
                throw new IOException("File already exists");

            using (var file = new H5File(filename))
            {
                file.Attributes[VersionKey] = PersistenceVersion;
                file.Attributes[SymphonyVersionKey] = SymphonyFramework.VersionString;
                file.Attributes[CompressionKey] = compression;

                H5Map.InsertTypes(file);
                H5PersistentExperiment.InsertExperiment(file, new H5PersistentEntityFactory(), "", startTime);
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
                throw new FileLoadException(
                    "File does not have a version attribute. Are you sure this is a Symphony file?");

            Version = _file.Attributes[VersionKey];
            if (Version != PersistenceVersion)
                throw new FileLoadException("Version mismatch. This file may have been produced by an older version.");

            NumericDataCompression = _file.Attributes[CompressionKey];

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
            if (IsClosed)
                return;

            if (CurrentEpochBlock != null)
            {
                EndEpochBlock(endTime);
            }
            while (CurrentEpochGroup != null)
            {
                EndEpochGroup(endTime);
            }
            _experiment.SetEndTime(endTime);

            CloseDocument();
            IsClosed = true;
        }

        public void CloseDocument()
        {
            _file.Close();
        }

        public bool IsClosed { get; private set; }

        internal static ILog Log = LogManager.GetLogger(typeof(H5EpochPersistor));

        public uint Version { get; private set; }

        public uint NumericDataCompression { get; private set; }

        public IPersistentExperiment Experiment
        {
            get { return _experiment; }
        }

        public IPersistentDevice AddDevice(string name, string manufacturer)
        {
            return _experiment.InsertDevice(name, manufacturer);
        }

        public IPersistentDevice Device(string name, string manufacturer)
        {
            return _experiment.Device(name, manufacturer);
        }

        public IPersistentSource AddSource(string label, IPersistentSource parent)
        {
            return AddSource(label, parent, DateTimeOffset.Now);
        }

        public IPersistentSource AddSource(string label, IPersistentSource parent, DateTimeOffset creationTime)
        {
            return parent == null
                ? _experiment.InsertSource(label, creationTime)
                : ((H5PersistentSource) parent).InsertSource(label, creationTime);
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source)
        {
            return BeginEpochGroup(label, source, DateTimeOffset.Now);
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source, DateTimeOffset startTime)
        {
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");

            var epochGroup = CurrentEpochGroup == null
                ? _experiment.InsertEpochGroup(label, (H5PersistentSource) source, startTime)
                : ((H5PersistentEpochGroup) CurrentEpochGroup).InsertEpochGroup(label, (H5PersistentSource) source,
                    startTime);

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

        public Tuple<IPersistentEpochGroup, IPersistentEpochGroup> SplitEpochGroup(IPersistentEpochGroup group, IPersistentEpochBlock block)
        {
            if (_openEpochGroups.Contains(group) && group != CurrentEpochGroup)
                throw new InvalidOperationException("Cannot split an open epoch group that isn't the current epoch group");
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");

            var split = H5PersistentEpochGroup.Split((H5PersistentEpochGroup) group, (H5PersistentEpochBlock) block);

            if (group == CurrentEpochGroup)
            {
                ((H5PersistentEpochGroup) split.Item1).SetEndTime(DateTimeOffset.Now);
                _openEpochGroups.Pop();
                _openEpochGroups.Push((H5PersistentEpochGroup) split.Item2);
            }
            return split;
        }

        public IPersistentEpochGroup MergeEpochGroups(IPersistentEpochGroup group1, IPersistentEpochGroup group2)
        {
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");

            var merged = H5PersistentEpochGroup.Merge((H5PersistentEpochGroup)group1, (H5PersistentEpochGroup)group2);

            if (group1 == CurrentEpochGroup || group2 == CurrentEpochGroup)
            {
                _openEpochGroups.Pop();
                _openEpochGroups.Push(merged);
            }
            return merged;
        }

        public IPersistentEpochGroup CurrentEpochGroup
        {
            get { return _openEpochGroups.Count == 0 ? null : _openEpochGroups.Peek(); }
        }

        public IPersistentEpochBlock BeginEpochBlock(string protocolID, IDictionary<string, object> parameters)
        {
            return BeginEpochBlock(protocolID, parameters, DateTimeOffset.Now);
        }

        public IPersistentEpochBlock BeginEpochBlock(string protocolID, IDictionary<string, object> parameters,
            DateTimeOffset startTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There are no open epoch groups");
            if (CurrentEpochBlock != null)
                throw new InvalidOperationException("There is an open epoch block");

            CurrentEpochBlock = ((H5PersistentEpochGroup) CurrentEpochGroup).InsertEpochBlock(protocolID, parameters,
                startTime);
            return CurrentEpochBlock;
        }

        public IPersistentEpochBlock EndEpochBlock()
        {
            return EndEpochBlock(DateTimeOffset.Now);
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

            return ((H5PersistentEpochBlock) CurrentEpochBlock).InsertEpoch(epoch, NumericDataCompression);
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
        private const string ResourcesGroupName = "resources";
        private const string NotesDatasetName = "notes";

        private H5Group _propertiesGroup;
        private H5Group _resourcesGroup;
        private H5Dataset _notesDataset;

        protected static H5Group InsertEntityGroup(H5Group container, string prefix)
        {
            var uuid = Guid.NewGuid();
            var group = container.AddGroup(prefix + "-" + uuid);
            try
            {
                group.Attributes[UUIDKey] = uuid.ToString();

                return group;
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting entity group: " + uuid, x);
            }
        }

        protected H5PersistentEntity(H5Group group, H5PersistentEntityFactory factory)
        {
            Group = group;
            EntityFactory = factory;

            UUID = GetUUID(group);

            var subGroups = Group.Groups.ToList();
            _propertiesGroup = subGroups.FirstOrDefault(g => g.Name == PropertiesGroupName);
            _resourcesGroup = subGroups.FirstOrDefault(g => g.Name == ResourcesGroupName);
            _notesDataset = group.Datasets.FirstOrDefault(ds => ds.Name == NotesDatasetName);
        }

        public H5PersistentEntityFactory EntityFactory { get; private set; }

        // The HDF5 group representing the persistent entity.
        public H5Group Group { get; private set; }

        protected void SetGroup(H5Group group)
        {
            if (UUID != GetUUID(group))
                throw new ArgumentException("UUID of given group does not match the UUID of the entity");
            Group = group;
        }

        public Guid UUID { get; private set; }

        public virtual void Delete()
        {
            Group.Delete();
            TryFlush();
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
            if (value == null)
                throw new ArgumentNullException("value");
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

        public IPersistentResource AddResource(string uti, string name, byte[] data)
        {
            if (GetResourceNames().Contains(name))
                throw new ArgumentException(name + " already exists");

            if (_resourcesGroup == null)
            {
                _resourcesGroup = Group.AddGroup(ResourcesGroupName);
            }

            return H5PersistentResource.InsertResource(_resourcesGroup, EntityFactory, uti, name, data);
        }

        public bool RemoveResource(string name)
        {
            var resource = Resources.FirstOrDefault(r => r.Name == name);
            if (resource == null)
                return false;
            ((H5PersistentResource) resource).Delete();
            return true;
        }

        public IPersistentResource GetResource(string name)
        {
            var resource = Resources.FirstOrDefault(r => r.Name == name);
            if (resource == null)
                throw new KeyNotFoundException(name);
            return resource;
        }

        public IEnumerable<string> GetResourceNames()
        {
            return Resources.Select(r => r.Name);
        }

        public IEnumerable<IPersistentResource> Resources
        {
            get
            {
                return _resourcesGroup == null
                    ? Enumerable.Empty<IPersistentResource>()
                    : _resourcesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentResource>(g));
            }
        }

        public void CopyResourcesFrom(H5PersistentEntity from)
        {
            if (from.Resources.Any() && _resourcesGroup == null)
            {
                _resourcesGroup = Group.AddGroup(ResourcesGroupName);
            }
            var names = GetResourceNames().ToList();
            foreach (var r in from.Resources)
            {
                if (names.Contains(r.Name))
                {
                    RemoveResource(r.Name);
                }
                H5PersistentResource.CopyResource(_resourcesGroup, (H5PersistentResource) r);
            }
        }

        public IEnumerable<IPersistentNote> Notes
        {
            get
            {
                return _notesDataset == null
                    ? Enumerable.Empty<IPersistentNote>()
                    : _notesDataset.GetData<H5Map.NoteT>().Select(H5Map.Convert);
            }
        }

        public IPersistentNote AddNote(DateTimeOffset time, string text)
        {
            return AddNote(new H5PersistentNote(time, text));
        }

        public IPersistentNote AddNote(IPersistentNote note)
        {
            if (note == null)
                throw new ArgumentNullException("note");

            if (_notesDataset == null)
            {
                _notesDataset = Group.AddDataset(NotesDatasetName, H5Map.GetNoteType(Group.File), new[] {0L},
                    new[] {-1L}, new[] {64L});
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

        public void CopyAnnotationsFrom(H5PersistentEntity from)
        {
            foreach (var kv in from.Properties)
            {
                AddProperty(kv.Key, kv.Value);
            }
            foreach (var k in from.Keywords)
            {
                AddKeyword(k);
            }
            foreach (var n in from.Notes)
            {
                AddNote(n);
            }
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

        public static H5PersistentDevice InsertDevice(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentExperiment experiment, string name, string manufacturer)
        {
            var group = InsertEntityGroup(container, "device");
            try
            {
                group.Attributes[NameKey] = name;
                group.Attributes[ManufacturerKey] = manufacturer;

                group.AddHardLink(ExperimentGroupName, experiment.Group);

                return factory.Create<H5PersistentDevice>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting device: " + name, x);
            }
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
        private const string CreationTimeTicksKey = "creationTimeDotNetDateTimeOffsetTicks";
        private const string CreationTimeOffsetHoursKey = "creationTimeDotNetDateTimeOffsetOffsetHours";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";
        private const string ParentGroupName = "parent";
        private const string ExperimentGroupName = "experiment";

        private string _label;

        private readonly H5Group _sourcesGroup;
        private readonly H5Group _epochGroupsGroup;
        private readonly H5Group _parentGroup;
        private readonly H5Group _experimentGroup;

        public static H5PersistentSource InsertSource(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentSource parent, H5PersistentExperiment experiment, string label, DateTimeOffset creationTime)
        {
            var group = InsertEntityGroup(container, "source");
            try
            {
                group.Attributes[LabelKey] = label;
                group.Attributes[CreationTimeTicksKey] = creationTime.Ticks;
                group.Attributes[CreationTimeOffsetHoursKey] = creationTime.Offset.TotalHours;

                group.AddGroup(SourcesGroupName);
                group.AddGroup(EpochGroupsGroupName);
                group.AddHardLink(ExperimentGroupName, experiment.Group);

                if (parent != null)
                {
                    group.AddHardLink(ParentGroupName, parent.Group);
                }

                return factory.Create<H5PersistentSource>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting source: " + label, x);
            }
        }

        public H5PersistentSource(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            var attr = group.Attributes;
            _label = attr[LabelKey];
            CreationTime = new DateTimeOffset(attr[CreationTimeTicksKey],
                TimeSpan.FromHours(attr[CreationTimeOffsetHoursKey]));

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

        public string Label
        {
            get { return _label; }
            set
            {
                Group.Attributes[LabelKey] = value;
                _label = value;
            }
        }

        public DateTimeOffset CreationTime { get; private set; }

        public IEnumerable<IPersistentSource> Sources
        {
            get
            {
                return
                    _sourcesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentSource>(g))
                        .OrderBy(s => s.CreationTime);
            }
        }

        public IEnumerable<IPersistentSource> AllSources
        {
            get { return Sources.Aggregate(Sources, (current, source) => current.Concat(source.AllSources)); }
        }

        public H5PersistentSource InsertSource(string label, DateTimeOffset creationTime)
        {
            var source = InsertSource(_sourcesGroup, EntityFactory, this, (H5PersistentExperiment) Experiment, label,
                creationTime);
            TryFlush();

            return source;
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get
            {
                return
                    _epochGroupsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochGroup>(g))
                        .OrderBy(g => g.StartTime);
            }
        }

        public IEnumerable<IPersistentEpochGroup> AllEpochGroups
        {
            get { return Sources.Aggregate(EpochGroups, (current, source) => current.Concat(source.AllEpochGroups)); }
        }

        public H5Group AddEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            _epochGroupsGroup.AddHardLink(epochGroup.Group.Name, epochGroup.Group);
            return _epochGroupsGroup.Groups.First(g => g.Name == epochGroup.Group.Name);
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

                return group;
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting timeline entity: " + startTime, x);
            }
        }

        protected static H5Group InsertTimelineEntityGroup(H5Group container, string prefix, DateTimeOffset startTime,
            DateTimeOffset endTime)
        {
            var group = InsertTimelineEntityGroup(container, prefix, startTime);
            try
            {
                group.Attributes[EndTimeTicksKey] = endTime.Ticks;
                group.Attributes[EndTimeOffsetHoursKey] = endTime.Offset.TotalHours;

                return group;
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting timeline entity: " + startTime, x);
            }
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

        private string _purpose;

        private readonly H5Group _devicesGroup;
        private readonly H5Group _sourcesGroup;
        private readonly H5Group _epochGroupsGroup;

        public static H5PersistentExperiment InsertExperiment(H5Group container, H5PersistentEntityFactory factory,
            string purpose, DateTimeOffset startTime)
        {
            var group = InsertTimelineEntityGroup(container, "experiment", startTime);
            try
            {
                group.Attributes[PurposeKey] = purpose;

                group.AddGroup(DevicesGroupName);
                group.AddGroup(SourcesGroupName);
                group.AddGroup(EpochGroupsGroupName);

                return factory.Create<H5PersistentExperiment>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting experiment: " + purpose, x);
            }
        }

        public H5PersistentExperiment(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            _purpose = group.Attributes[PurposeKey];

            var subGroups = group.Groups.ToList();
            _devicesGroup = subGroups.First(g => g.Name == DevicesGroupName);
            _sourcesGroup = subGroups.First(g => g.Name == SourcesGroupName);
            _epochGroupsGroup = subGroups.First(g => g.Name == EpochGroupsGroupName);
        }

        public string Purpose
        {
            get { return _purpose; }
            set
            {
                Group.Attributes[PurposeKey] = value;
                _purpose = value;
            }
        }

        public IEnumerable<IPersistentDevice> Devices
        {
            get { return _devicesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentDevice>(g)); }
        }

        public H5PersistentDevice Device(string name, string manufacture)
        {
            return
                (H5PersistentDevice)
                (Devices.ToList().FirstOrDefault(d => d.Name == name && d.Manufacturer == manufacture) ??
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
            get
            {
                return
                    _sourcesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentSource>(g))
                        .OrderBy(s => s.CreationTime);
            }
        }

        public IEnumerable<IPersistentSource> AllSources
        {
            get { return Sources.Flatten(s => s.Sources); }
        }

        public H5PersistentSource InsertSource(string label, DateTimeOffset creationTime)
        {
            var source = H5PersistentSource.InsertSource(_sourcesGroup, EntityFactory, null, this, label, creationTime);
            TryFlush();

            return source;
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get
            {
                return
                    _epochGroupsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochGroup>(g))
                        .OrderBy(g => g.StartTime);
            }
        }

        public IEnumerable<IPersistentEpochGroup> AllEpochGroups
        {
            get { return EpochGroups.Flatten(g => g.EpochGroups); }
        }

        public H5PersistentEpochGroup InsertEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var group = H5PersistentEpochGroup.InsertEpochGroup(_epochGroupsGroup, EntityFactory, null, this, label,
                source, startTime);
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

        private string _label;

        private readonly H5Group _sourceGroup;
        private readonly H5Group _epochGroupsGroup;
        private readonly H5Group _epochBlocksGroup;
        private readonly H5Group _parentGroup;
        private readonly H5Group _experimentGroup;

        public static H5PersistentEpochGroup InsertEpochGroup(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentEpochGroup parent, H5PersistentExperiment experiment, string label, H5PersistentSource source,
            DateTimeOffset startTime)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            H5PersistentEpochGroup epochGroup = null;

            var group = InsertTimelineEntityGroup(container, "epochGroup", startTime);
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

                return epochGroup;
            }
            catch (Exception x)
            {
                source.RemoveEpochGroup(epochGroup);
                group.Delete();
                throw new PersistanceException("An error occurred while persisting epoch group: " + label, x);
            }
        }

        public H5PersistentEpochGroup(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            _label = group.Attributes[LabelKey];

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
                ((H5PersistentEpochGroup) g).Delete();
            }
            ((H5PersistentSource) Source).RemoveEpochGroup(this);
            base.Delete();
        }

        public string Label
        {
            get { return _label; }
            set
            {
                Group.Attributes[LabelKey] = value;
                _label = value;
            }
        }

        public IPersistentSource Source
        {
            get { return EntityFactory.Create<H5PersistentSource>(_sourceGroup); }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();

                H5Group group = ((H5PersistentSource) value).Group;
                ((H5PersistentSource) Source).RemoveEpochGroup(this);
                _sourceGroup.Delete();
                Group.AddHardLink(SourceGroupName, group);
                ((H5PersistentSource) Source).AddEpochGroup(this);
            }
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get
            {
                return
                    _epochGroupsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochGroup>(g))
                        .OrderBy(g => g.StartTime);
            }
        }

        public IEnumerable<IPersistentEpochGroup> AllEpochGroups
        {
            get { return EpochGroups.Aggregate(EpochGroups, (current, group) => current.Concat(group.AllEpochGroups)); }
        }

        public H5PersistentEpochGroup InsertEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var group = InsertEpochGroup(_epochGroupsGroup, EntityFactory, this, (H5PersistentExperiment) Experiment,
                label, source, startTime);
            TryFlush();

            return group;
        }

        public IEnumerable<IPersistentEpochBlock> EpochBlocks
        {
            get
            {
                return
                    _epochBlocksGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpochBlock>(g))
                        .OrderBy(b => b.StartTime);
            }
        }

        public H5PersistentEpochBlock InsertEpochBlock(string protocolID, IDictionary<string, object> parameters,
            DateTimeOffset startTime)
        {
            var b = H5PersistentEpochBlock.InsertEpochBlock(_epochBlocksGroup, EntityFactory, this, protocolID,
                parameters, startTime);
            TryFlush();

            return b;
        }

        public H5PersistentEpochBlock AddEpochBlock(H5PersistentEpochBlock block)
        {
            _epochBlocksGroup.AddHardLink(block.Group.Name, block.Group);
            var group = _epochBlocksGroup.Groups.First(g => g.Name == block.Group.Name);
            return new H5PersistentEpochBlock(group, EntityFactory);
        }

        public bool RemoveEpochBlock(H5PersistentEpochBlock block)
        {
            var bg = _epochBlocksGroup.Groups.FirstOrDefault(g => g.Name == block.Group.Name);
            if (bg == null)
                return false;

            bg.Delete();
            return true;
        }

        public IPersistentEpochGroup Parent
        {
            get { return _parentGroup == null ? null : EntityFactory.Create<H5PersistentEpochGroup>(_parentGroup); }
        }

        public IPersistentExperiment Experiment
        {
            get { return EntityFactory.Create<H5PersistentExperiment>(_experimentGroup); }
        }

        public static Tuple<IPersistentEpochGroup, IPersistentEpochGroup> Split(H5PersistentEpochGroup group, H5PersistentEpochBlock block)
        {
            if (!group.EpochBlocks.Contains(block))
                throw new ArgumentException("Epoch group does not contain the given block");
            if (block.EndTime == null)
                throw new ArgumentException("Epoch block must have an end time");

            var g1 = group.Parent == null
                ? ((H5PersistentExperiment)group.Experiment).InsertEpochGroup(group.Label, (H5PersistentSource)group.Source, group.StartTime)
                : ((H5PersistentEpochGroup)group.Parent).InsertEpochGroup(group.Label, (H5PersistentSource)group.Source, group.StartTime);
            var g2 = group.Parent == null
                ? ((H5PersistentExperiment)group.Experiment).InsertEpochGroup(group.Label, (H5PersistentSource)group.Source, block.EndTime.Value)
                : ((H5PersistentEpochGroup)group.Parent).InsertEpochGroup(group.Label, (H5PersistentSource)group.Source, block.EndTime.Value);
            try
            {
                g1.CopyAnnotationsFrom(group);
                g1.CopyResourcesFrom(group);

                g2.CopyAnnotationsFrom(group);
                g2.CopyResourcesFrom(group);

                var blocks = group.EpochBlocks.ToList();
                foreach (var b in blocks.Where(b => b.StartTime <= block.StartTime))
                {
                    ((H5PersistentEpochBlock) b).SetEpochGroup(g1);
                }
                foreach (var b in blocks.Where(b => b.StartTime > block.StartTime))
                {
                    ((H5PersistentEpochBlock) b).SetEpochGroup(g2);
                }

                g1.SetEndTime(block.EndTime.Value);

                if (group.EndTime != null)
                {
                    g2.SetEndTime(group.EndTime.Value);
                }

                group.Delete();

                return new Tuple<IPersistentEpochGroup, IPersistentEpochGroup>(g1, g2);
            }
            catch (Exception x)
            {
                g1.Delete();
                g2.Delete();
                throw new PersistanceException("An error occurred while splitting epoch group: " + group.Label, x);
            }
        }

        public static H5PersistentEpochGroup Merge(H5PersistentEpochGroup g1, H5PersistentEpochGroup g2)
        {
            if (Equals(g1, g2))
                throw new InvalidOperationException("Cannot merge an epoch group into itself");
            if (g1.Parent != g2.Parent)
                throw new InvalidOperationException("Cannot merge epoch groups on different levels");

            var firstGroup = g1.StartTime < g2.StartTime ? g1 : g2;
            var secondGroup = g1.StartTime < g2.StartTime ? g2 : g1;
            var groups = firstGroup.EpochGroups.ToList();
            if (groups.Any(g => g.StartTime > firstGroup.StartTime && g.EndTime < secondGroup.EndTime))
                throw new InvalidOperationException("Only adjacent epoch groups may be merged");

            var merged = g2.Parent == null
                ? ((H5PersistentExperiment)g2.Experiment).InsertEpochGroup(g2.Label, (H5PersistentSource)g2.Source, firstGroup.StartTime)
                : ((H5PersistentEpochGroup)g2.Parent).InsertEpochGroup(g2.Label, (H5PersistentSource)g2.Source, firstGroup.StartTime);
            try
            {
                merged.CopyAnnotationsFrom(g1);
                merged.CopyResourcesFrom(g1);

                merged.CopyAnnotationsFrom(g2);
                merged.CopyResourcesFrom(g2);

                var blocks = g1.EpochBlocks.Concat(g2.EpochBlocks).ToList();
                foreach (var b in blocks)
                {
                    ((H5PersistentEpochBlock) b).SetEpochGroup(merged);
                }

                var endTime = g1.EndTime > g2.EndTime ? g1.EndTime : g2.EndTime;
                if (endTime != null)
                {
                    merged.SetEndTime(endTime.Value);
                }

                g1.Delete();
                g2.Delete();

                return merged;
            }
            catch (Exception x)
            {
                merged.Delete();
                throw new PersistanceException("An error occurred while merging epoch groups: " + g1.Label + ", " + g2.Label, x);
            }
        }
    }

    class H5PersistentEpochBlock : H5TimelinePersistentEntity, IPersistentEpochBlock
    {
        private const string ProtocolIDKey = "protocolID";
        private const string ProtocolParametersGroupName = "protocolParameters";
        private const string EpochsGroupName = "epochs";
        private const string EpochGroupGroupName = "epochGroup";

        private H5Group _protocolParametersGroup;
        private H5Group _epochsGroup;
        private H5Group _epochGroupGroup;

        public static H5PersistentEpochBlock InsertEpochBlock(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentEpochGroup epochGroup, string protocolID, IDictionary<string, object> parameters,
            DateTimeOffset startTime)
        {
            var group = InsertTimelineEntityGroup(container, protocolID, startTime);
            try
            {
                group.Attributes[ProtocolIDKey] = protocolID;

                var parametersGroup = group.AddGroup(ProtocolParametersGroupName);
                group.AddGroup(EpochsGroupName);
                group.AddHardLink(EpochGroupGroupName, epochGroup.Group);

                foreach (var kv in parameters.ToList())
                {
                    var value = kv.Value ?? "";
                    if (H5AttributeManager.IsSupportedType(value.GetType()))
                    {
                        parametersGroup.Attributes[kv.Key] = new H5Attribute(value);
                    }
                    else
                    {
                        H5EpochPersistor.Log.WarnFormat(
                            "Protocol parameter value ({0} : {1}) is of usupported type. Falling back to string representation.",
                            kv.Key, value);
                        parametersGroup.Attributes[kv.Key] = value.ToString();
                    }
                }

                return factory.Create<H5PersistentEpochBlock>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting epoch block: " + protocolID, x);
            }
        }

        public H5PersistentEpochBlock(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            ProtocolID = group.Attributes[ProtocolIDKey];

            InitEpochBlockH5Objects();
        }

        private void InitEpochBlockH5Objects()
        {
            var subGroups = Group.Groups.ToList();
            _protocolParametersGroup = subGroups.First(g => g.Name == ProtocolParametersGroupName);
            _epochsGroup = subGroups.First(g => g.Name == EpochsGroupName);
            _epochGroupGroup = subGroups.First(g => g.Name == EpochGroupGroupName);
        }

        public string ProtocolID { get; private set; }

        public IEnumerable<KeyValuePair<string, object>> ProtocolParameters
        {
            get
            {
                return
                    _protocolParametersGroup.Attributes.Select(
                        a => new KeyValuePair<string, object>(a.Name, a.GetValue()));
            }
        }

        public IEnumerable<IPersistentEpoch> Epochs
        {
            get
            {
                return
                    _epochsGroup.Groups.Select(g => EntityFactory.Create<H5PersistentEpoch>(g))
                        .OrderBy(e => e.StartTime);
            }
        }

        public H5PersistentEpoch InsertEpoch(Epoch epoch, uint compression)
        {
            if (epoch.ProtocolID != ProtocolID)
                throw new ArgumentException("Epoch protocol id does not match epoch block protocol id");

            var pEpoch = H5PersistentEpoch.InsertEpoch(_epochsGroup, EntityFactory, this, epoch, compression);
            TryFlush();

            return pEpoch;
        }

        public H5PersistentEpoch AddEpoch(H5PersistentEpoch epoch)
        {
            _epochsGroup.AddHardLink(epoch.Group.Name, epoch.Group);
            var group = _epochsGroup.Groups.First(g => g.Name == epoch.Group.Name);
            return new H5PersistentEpoch(group, EntityFactory);
        }

        public bool RemoveEpoch(H5PersistentEpoch epoch)
        {
            var bg = _epochsGroup.Groups.FirstOrDefault(g => g.Name == epoch.Group.Name);
            if (bg == null)
                return false;

            bg.Delete();
            return true;
        }

        public IPersistentEpochGroup EpochGroup
        {
            get { return EntityFactory.Create<H5PersistentEpochGroup>(_epochGroupGroup); }
        }

        public void SetEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            var oldEpochGroup = (H5PersistentEpochGroup) EpochGroup;

            H5PersistentEpochBlock newBlock;
            if (epochGroup.UUID == oldEpochGroup.UUID)
            {
                EntityFactory.RemoveFromCache(this);
                newBlock = (H5PersistentEpochBlock) epochGroup.EpochBlocks.First(b => b.UUID == UUID);
            }
            else
            {
                newBlock = epochGroup.AddEpochBlock(this);
            }

            foreach (var e in Epochs)
            {
                ((H5PersistentEpoch)e).SetEpochBlock(newBlock);
            }

            if (epochGroup.UUID != oldEpochGroup.UUID)
            {
                oldEpochGroup.RemoveEpochBlock(this);
            }
            
            newBlock._epochGroupGroup.Delete();
            newBlock.Group.AddHardLink(EpochGroupGroupName, epochGroup.Group);
            SetGroup(newBlock.Group);

            InitEpochBlockH5Objects();
        }
    }

    class H5PersistentEpoch : H5TimelinePersistentEntity, IPersistentEpoch
    {
        private const string BackgroundsGroupName = "backgrounds";
        private const string ProtocolParametersGroupName = "protocolParameters";
        private const string ResponsesGroupName = "responses";
        private const string StimuliGroupName = "stimuli";
        private const string EpochBlockGroupName = "epochBlock";

        private H5Group _backgroundGroup;
        private H5Group _protocolParametersGroup;
        private H5Group _responsesGroup;
        private H5Group _stimuliGroup;
        private H5Group _epochBlockGroup;

        public static H5PersistentEpoch InsertEpoch(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentEpochBlock block, Epoch epoch, uint compression)
        {
            var group = InsertTimelineEntityGroup(container, "epoch", epoch.StartTime,
                (DateTimeOffset) epoch.StartTime + epoch.Duration);
            try
            {
                var backgroundsGroup = group.AddGroup(BackgroundsGroupName);
                var parametersGroup = group.AddGroup(ProtocolParametersGroupName);
                var responsesGroup = group.AddGroup(ResponsesGroupName);
                var stimuliGroup = group.AddGroup(StimuliGroupName);

                group.AddHardLink(EpochBlockGroupName, block.Group);

                var experiment = (H5PersistentExperiment) block.EpochGroup.Experiment;

                var persistentEpoch = factory.Create<H5PersistentEpoch>(group);

                // ToList() everything before enumerating to guard against external collection modification
                // causing exceptions during serialization

                foreach (var kv in epoch.ProtocolParameters.ToList())
                {
                    var value = kv.Value ?? "";
                    if (H5AttributeManager.IsSupportedType(value.GetType()))
                    {
                        parametersGroup.Attributes[kv.Key] = new H5Attribute(value);
                    }
                    else
                    {
                        H5EpochPersistor.Log.WarnFormat(
                            "Protocol parameter value ({0} : {1}) is of usupported type. Falling back to string representation.",
                            kv.Key, value);
                        parametersGroup.Attributes[kv.Key] = value.ToString();
                    }
                }

                foreach (var kv in epoch.Responses.ToList())
                {
                    var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                    H5PersistentResponse.InsertResponse(responsesGroup, factory, persistentEpoch, device, kv.Value,
                        compression);
                }

                foreach (var kv in epoch.Stimuli.ToList())
                {
                    var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                    H5PersistentStimulus.InsertStimulus(stimuliGroup, factory, persistentEpoch, device, kv.Value,
                        compression);
                }

                foreach (var kv in epoch.Backgrounds.ToList())
                {
                    var device = experiment.Device(kv.Key.Name, kv.Key.Manufacturer);
                    H5PersistentBackground.InsertBackground(backgroundsGroup, factory, persistentEpoch, device, kv.Value);
                }

                foreach (var keyword in epoch.Keywords.ToList())
                {
                    persistentEpoch.AddKeyword(keyword);
                }

                return persistentEpoch;
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting epoch: " + epoch.StartTime, x);
            }
        }

        public H5PersistentEpoch(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            InitEpochH5Objects();
        }

        private void InitEpochH5Objects()
        {
            var subGroups = Group.Groups.ToList();
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

        public H5PersistentBackground AddBackground(H5PersistentBackground background)
        {
            _backgroundGroup.AddHardLink(background.Group.Name, background.Group);
            var group = _backgroundGroup.Groups.First(g => g.Name == background.Group.Name);
            return new H5PersistentBackground(group, EntityFactory);
        }

        public bool RemoveBackground(H5PersistentBackground background)
        {
            var bg = _backgroundGroup.Groups.FirstOrDefault(g => g.Name == background.Group.Name);
            if (bg == null)
                return false;

            bg.Delete();
            return true;
        }

        public IEnumerable<KeyValuePair<string, object>> ProtocolParameters
        {
            get
            {
                return
                    _protocolParametersGroup.Attributes.Select(
                        a => new KeyValuePair<string, object>(a.Name, a.GetValue()));
            }
        }

        public IEnumerable<IPersistentResponse> Responses
        {
            get { return _responsesGroup.Groups.Select(g => EntityFactory.Create<H5PersistentResponse>(g)); }
        }

        public H5PersistentResponse AddResponse(H5PersistentResponse response)
        {
            _responsesGroup.AddHardLink(response.Group.Name, response.Group);
            var group = _responsesGroup.Groups.First(g => g.Name == response.Group.Name);
            return new H5PersistentResponse(group, EntityFactory);
        }

        public bool RemoveResponse(H5PersistentResponse response)
        {
            var bg = _responsesGroup.Groups.FirstOrDefault(g => g.Name == response.Group.Name);
            if (bg == null)
                return false;

            bg.Delete();
            return true;
        }

        public IEnumerable<IPersistentStimulus> Stimuli
        {
            get { return _stimuliGroup.Groups.Select(g => EntityFactory.Create<H5PersistentStimulus>(g)); }
        }

        public H5PersistentStimulus AddStimulus(H5PersistentStimulus stimulus)
        {
            _stimuliGroup.AddHardLink(stimulus.Group.Name, stimulus.Group);
            var group = _stimuliGroup.Groups.First(g => g.Name == stimulus.Group.Name);
            return new H5PersistentStimulus(group, EntityFactory);
        }

        public bool RemoveStimulus(H5PersistentStimulus stimulus)
        {
            var bg = _stimuliGroup.Groups.FirstOrDefault(g => g.Name == stimulus.Group.Name);
            if (bg == null)
                return false;

            bg.Delete();
            return true;
        }

        public IPersistentEpochBlock EpochBlock
        {
            get { return EntityFactory.Create<H5PersistentEpochBlock>(_epochBlockGroup); }
        }

        public void SetEpochBlock(H5PersistentEpochBlock epochBlock)
        {
            var oldEpochBlock = (H5PersistentEpochBlock)EpochBlock;

            H5PersistentEpoch newEpoch;
            if (epochBlock.UUID == oldEpochBlock.UUID)
            {
                EntityFactory.RemoveFromCache(this);
                newEpoch = (H5PersistentEpoch) epochBlock.Epochs.First(e => e.UUID == UUID);
            }
            else
            {
                newEpoch = epochBlock.AddEpoch(this);
            }

            foreach (var io in Backgrounds.Concat<IPersistentIOBase>(Responses).Concat(Stimuli))
            {
                ((H5PersistentIOBase)io).SetEpoch(newEpoch);
            }

            if (epochBlock.UUID != oldEpochBlock.UUID)
            {
                oldEpochBlock.RemoveEpoch(this);
            }

            newEpoch._epochBlockGroup.Delete();
            newEpoch.Group.AddHardLink(EpochBlockGroupName, epochBlock.Group);
            SetGroup(newEpoch.Group);

            InitEpochH5Objects();
        }
    }

    abstract class H5PersistentIOBase : H5PersistentEntity, IPersistentIOBase
    {
        protected const string DeviceGroupName = "device";
        protected const string DataConfigurationSpansGroupName = "dataConfigurationSpans";
        protected const string SpanGroupPrefix = "span_";
        protected const string SpanIndexKey = "index";
        protected const string SpanStartTimeKey = "startTimeSeconds";
        protected const string SpanDurationKey = "timeSpanSeconds";
        protected const string EpochGroupName = "epoch";

        protected H5Group _deviceGroup;
        protected H5Group _dataConfigurationSpansGroup;
        protected H5Group _epochGroup;

        protected static H5Group InsertIOBaseGroup(H5Group container, H5PersistentEpoch epoch, H5PersistentDevice device,
            IList<IConfigurationSpan> configSpans)
        {
            var group = InsertEntityGroup(container, device.Name);
            try
            {
                group.AddHardLink(DeviceGroupName, device.Group);
                var spansGroup = group.AddGroup(DataConfigurationSpansGroupName);

                uint i = 0;
                var totalTime = TimeSpan.Zero;
                foreach (var span in configSpans.Consolidate().ToList())
                {
                    var spanGroup = spansGroup.AddGroup(SpanGroupPrefix + i);
                    spanGroup.Attributes[SpanIndexKey] = i;

                    spanGroup.Attributes[SpanStartTimeKey] = totalTime.TotalSeconds;
                    totalTime += span.Time;

                    spanGroup.Attributes[SpanDurationKey] = span.Time.TotalSeconds;
                    foreach (var node in span.Nodes.ToList())
                    {
                        var nodeGroup = spanGroup.AddGroup(node.Name);
                        foreach (var kv in node.Configuration.ToList())
                        {
                            var value = kv.Value ?? "";
                            if (H5AttributeManager.IsSupportedType(value.GetType()))
                            {
                                nodeGroup.Attributes[kv.Key] = new H5Attribute(value);
                            }
                            else if (value is IMeasurement)
                            {
                                var m = (IMeasurement) kv.Value;
                                nodeGroup.Attributes[kv.Key + "_quantity"] = (double) m.Quantity;
                                nodeGroup.Attributes[kv.Key + "_units"] = m.DisplayUnits;
                                nodeGroup.Attributes[kv.Key] = m.ToString();
                            }
                            else
                            {
                                H5EpochPersistor.Log.WarnFormat(
                                    "Node configuration value ({0} : {1}) is of usupported type. Falling back to string representation.",
                                    kv.Key, value);
                                nodeGroup.Attributes[kv.Key] = value.ToString();
                            }
                        }
                    }

                    i++;
                }

                group.AddHardLink(EpochGroupName, epoch.Group);

                return group;
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(
                    "An error occurred while persisting iobase entity for epoch: " + epoch.StartTime, x);
            }
        }

        protected H5PersistentIOBase(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            InitIOBaseH5Objects();
        }

        protected void InitIOBaseH5Objects()
        {
            var subGroups = Group.Groups.ToList();
            _deviceGroup = subGroups.First(g => g.Name == DeviceGroupName);
            _dataConfigurationSpansGroup = subGroups.First(g => g.Name == DataConfigurationSpansGroupName);
            _epochGroup = subGroups.First(g => g.Name == EpochGroupName);
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

        public IPersistentEpoch Epoch
        {
            get { return EntityFactory.Create<H5PersistentEpoch>(_epochGroup); }
        }

        public abstract void SetEpoch(H5PersistentEpoch epoch);
    }

    class H5PersistentResponse : H5PersistentIOBase, IPersistentResponse
    {
        private const string SampleRateKey = "sampleRate";
        private const string SampleRateUnitsKey = "sampleRateUnits";
        private const string InputTimeTicksKey = "inputTimeDotNetDateTimeOffsetTicks";
        private const string InputTimeOffsetHoursKey = "inputTimeDotNetDateTimeOffsetOffsetHours";
        private const string DataDatasetName = "data";

        private H5Dataset _dataDataset;

        public static H5PersistentResponse InsertResponse(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentEpoch epoch, H5PersistentDevice device, Response response, uint compression)
        {
            var group = InsertIOBaseGroup(container, epoch, device, response.DataConfigurationSpans.ToList());
            try
            {
                group.Attributes[SampleRateKey] = (double) response.SampleRate.Quantity;
                group.Attributes[SampleRateUnitsKey] = response.SampleRate.DisplayUnits;
                group.Attributes[InputTimeTicksKey] = response.InputTime.Ticks;
                group.Attributes[InputTimeOffsetHoursKey] = response.InputTime.Offset.TotalHours;

                group.AddDataset(DataDatasetName, H5Map.GetMeasurementType(container.File),
                    response.Data.ToList().Select(H5Map.Convert).ToArray(), compression);

                return factory.Create<H5PersistentResponse>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(
                    "An error occurred while persisting response for device: " + device.Name, x);
            }
        }

        public H5PersistentResponse(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            double rate = group.Attributes[SampleRateKey];
            string units = group.Attributes[SampleRateUnitsKey];
            SampleRate = new Measurement(rate, units);

            long ticks = group.Attributes[InputTimeTicksKey];
            double offset = group.Attributes[InputTimeOffsetHoursKey];
            InputTime = new DateTimeOffset(ticks, TimeSpan.FromHours(offset));

            InitResponseH5Objects();
        }

        private void InitResponseH5Objects()
        {
            _dataDataset = Group.Datasets.First(ds => ds.Name == DataDatasetName);
        }

        public IMeasurement SampleRate { get; private set; }

        public DateTimeOffset InputTime { get; private set; }

        public IEnumerable<IMeasurement> Data
        {
            get { return _dataDataset.GetData<H5Map.MeasurementT>().Select(H5Map.Convert); }
        }

        public override void SetEpoch(H5PersistentEpoch epoch)
        {
            var oldEpoch = (H5PersistentEpoch)Epoch;

            H5PersistentResponse newResponse;
            if (epoch.UUID == oldEpoch.UUID)
            {
                EntityFactory.RemoveFromCache(this);
                newResponse = (H5PersistentResponse) epoch.Responses.First(r => r.UUID == UUID);
            }
            else
            {
                newResponse = epoch.AddResponse(this);
                oldEpoch.RemoveResponse(this);
            }

            newResponse._epochGroup.Delete();
            newResponse.Group.AddHardLink(EpochGroupName, epoch.Group);
            SetGroup(newResponse.Group);

            InitIOBaseH5Objects();
            InitResponseH5Objects();
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

        private H5Group _parametersGroup;
        private H5Dataset _dataDataset;

        public static H5PersistentStimulus InsertStimulus(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentEpoch epoch, H5PersistentDevice device, IStimulus stimulus, uint compression)
        {
            var group = InsertIOBaseGroup(container, epoch, device, stimulus.OutputConfigurationSpans.ToList());
            try
            {
                group.Attributes[StimulusIDKey] = stimulus.StimulusID;
                group.Attributes[UnitsKey] = stimulus.Units;
                group.Attributes[SampleRateKey] = (double) stimulus.SampleRate.Quantity;
                group.Attributes[SampleRateUnitsKey] = stimulus.SampleRate.DisplayUnits;
                if (stimulus.Duration.IsSome())
                {
                    group.Attributes[DurationKey] = ((TimeSpan) stimulus.Duration).TotalSeconds;
                }

                var parametersGroup = group.AddGroup(ParametersGroupName);

                foreach (var kv in stimulus.Parameters.ToList())
                {
                    var value = kv.Value ?? "";
                    if (H5AttributeManager.IsSupportedType(value.GetType()))
                    {
                        parametersGroup.Attributes[kv.Key] = new H5Attribute(value);
                    }
                    else
                    {
                        H5EpochPersistor.Log.WarnFormat(
                            "Stimulus parameter value ({0} : {1}) is of usupported type. Falling back to string representation.",
                            kv.Key, value);
                        parametersGroup.Attributes[kv.Key] = value.ToString();
                    }

                }

                if (stimulus.Data.IsSome())
                {
                    IList<IMeasurement> data = stimulus.Data.Get().ToList();
                    group.AddDataset(DataDatasetName, H5Map.GetMeasurementType(container.File),
                        data.Select(H5Map.Convert).ToArray(), compression);
                }

                return factory.Create<H5PersistentStimulus>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting stimulus: " + stimulus.StimulusID, x);
            }
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

            InitStimulusH5Objects();
        }

        private void InitStimulusH5Objects()
        {
            _parametersGroup = Group.Groups.First(g => g.Name == ParametersGroupName);
            _dataDataset = Group.Datasets.FirstOrDefault(g => g.Name == DataDatasetName);
        }

        public string StimulusID { get; private set; }

        public string Units { get; private set; }

        public IMeasurement SampleRate { get; private set; }

        public IEnumerable<KeyValuePair<string, object>> Parameters
        {
            get
            {
                return _parametersGroup.Attributes.Select(a => new KeyValuePair<string, object>(a.Name, a.GetValue()));
            }
        }

        public Option<TimeSpan> Duration { get; private set; }

        public Option<IEnumerable<IMeasurement>> Data
        {
            get
            {
                return _dataDataset == null
                    ? Option<IEnumerable<IMeasurement>>.None()
                    : Option<IEnumerable<IMeasurement>>.Some(
                        _dataDataset.GetData<H5Map.MeasurementT>().Select(H5Map.Convert));
            }
        }

        public override void SetEpoch(H5PersistentEpoch epoch)
        {
            var oldEpoch = (H5PersistentEpoch)Epoch;

            H5PersistentStimulus newStimulus;
            if (epoch.UUID == oldEpoch.UUID)
            {
                EntityFactory.RemoveFromCache(this);
                newStimulus = (H5PersistentStimulus) epoch.Stimuli.First(s => s.UUID == UUID);
            }
            else
            {
                newStimulus = epoch.AddStimulus(this);
                oldEpoch.RemoveStimulus(this);
            }

            newStimulus._epochGroup.Delete();
            newStimulus.Group.AddHardLink(EpochGroupName, epoch.Group);
            SetGroup(newStimulus.Group);

            InitIOBaseH5Objects();
            InitStimulusH5Objects();
        }
    }

    class H5PersistentBackground : H5PersistentIOBase, IPersistentBackground
    {
        private const string ValueKey = "value";
        private const string ValueUnitsKey = "valueUnits";
        private const string SampleRateKey = "sampleRate";
        private const string SampleRateUnitsKey = "sampleRateUnits";

        public static H5PersistentBackground InsertBackground(H5Group container, H5PersistentEntityFactory factory,
            H5PersistentEpoch epoch, H5PersistentDevice device, Background background)
        {
            var group = InsertIOBaseGroup(container, epoch, device, background.OutputConfigurationSpans.ToList());
            try
            {
                group.Attributes[ValueKey] = (double) background.Value.Quantity;
                group.Attributes[ValueUnitsKey] = background.Value.DisplayUnits;
                group.Attributes[SampleRateKey] = (double) background.SampleRate.Quantity;
                group.Attributes[SampleRateUnitsKey] = background.SampleRate.DisplayUnits;

                return factory.Create<H5PersistentBackground>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException(
                    "An error occurred while persisting background for device: " + device.Name, x);
            }
        }

        public H5PersistentBackground(H5Group group, H5PersistentEntityFactory factory)
            : base(group, factory)
        {
            double value = group.Attributes[ValueKey];
            string valueUnits = group.Attributes[ValueUnitsKey];
            Value = new Measurement(value, valueUnits);

            double sampleRate = group.Attributes[SampleRateKey];
            string sampleRateUnits = group.Attributes[SampleRateUnitsKey];
            SampleRate = new Measurement(sampleRate, sampleRateUnits);
        }

        public IMeasurement Value { get; private set; }

        public IMeasurement SampleRate { get; private set; }

        public override void SetEpoch(H5PersistentEpoch epoch)
        {
            var oldEpoch = (H5PersistentEpoch)Epoch;

            H5PersistentBackground newBackground;
            if (epoch.UUID == oldEpoch.UUID)
            {
                EntityFactory.RemoveFromCache(this);
                newBackground = (H5PersistentBackground) epoch.Backgrounds.First(b => b.UUID == UUID);
            }
            else
            {
                newBackground = epoch.AddBackground(this);
                oldEpoch.RemoveBackground(this);
            }

            newBackground._epochGroup.Delete();
            newBackground.Group.AddHardLink(EpochGroupName, epoch.Group);
            SetGroup(newBackground.Group);

            InitIOBaseH5Objects();
        }
    }

    class H5PersistentResource : H5PersistentEntity, IPersistentResource
    {
        private const string UTIKey = "uti";
        private const string NameKey = "name";
        private const string DataDatasetName = "data";

        private readonly H5Dataset _dataDataset;

        public static H5PersistentResource InsertResource(H5Group container, H5PersistentEntityFactory factory,
            string uti, string name, byte[] data)
        {
            var group = InsertEntityGroup(container, "resource");
            try
            {
                group.Attributes[UTIKey] = uti;
                group.Attributes[NameKey] = name;

                group.AddDataset(DataDatasetName, new H5Datatype(H5T.H5Type.NATIVE_UCHAR), data);

                return factory.Create<H5PersistentResource>(group);
            }
            catch (Exception x)
            {
                group.Delete();
                throw new PersistanceException("An error occurred while persisting resource: " + name, x);
            }
        }

        public static H5PersistentResource CopyResource(H5Group destination, H5PersistentResource resource)
        {
            var copy = InsertResource(destination, resource.EntityFactory, resource.UTI, resource.Name, resource.Data);
            try
            {
                copy.CopyAnnotationsFrom(resource);
                copy.CopyResourcesFrom(resource);

                return copy;
            }
            catch (Exception x)
            {
                copy.Delete();
                throw new PersistanceException("An error occurred while copying resource: " + resource.Name, x);
            }
        }

        public H5PersistentResource(H5Group group, H5PersistentEntityFactory factory) : base(group, factory)
        {
            UTI = group.Attributes[UTIKey];
            Name = group.Attributes[NameKey];

            _dataDataset = group.Datasets.First(ds => ds.Name == DataDatasetName);
        }

        public string UTI { get; private set; }

        public string Name { get; private set; }

        public byte[] Data
        {
            get { return _dataDataset.GetData<byte>(); }
        }
    }

    class H5PersistentNote : IPersistentNote
    {
        public H5PersistentNote(DateTimeOffset time, string text)
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
            return Equals((H5PersistentNote) obj);
        }

        protected bool Equals(H5PersistentNote other)
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
            Constructor<H5PersistentResource>.Func = (g, f) => new H5PersistentResource(g, f);
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

        public bool RemoveFromCache(H5PersistentEntity entity)
        {
            return _cache.Remove(entity.UUID);
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
            public fixed byte units[UnitsStringLength];
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
        public static NoteT Convert(IPersistentNote n)
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

        public static IPersistentNote Convert(NoteT nt)
        {
            long ticks = nt.time.ticks;
            double offset = nt.time.offset;
            var time = new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            string text;
            unsafe
            {
                text = Marshal.PtrToStringAnsi((IntPtr) nt.text);
            }
            return new H5PersistentNote(time, text);
        }

        public static MeasurementT Convert(IMeasurement m)
        {
            var mt = new MeasurementT {quantity = (double) m.Quantity};
            var unitsdata = Encoding.ASCII.GetBytes(m.DisplayUnits);
            unsafe
            {
                Marshal.Copy(unitsdata, 0, (IntPtr) mt.units, Math.Min(unitsdata.Length, UnitsStringLength));
            }
            return mt;
        }

        public static IMeasurement Convert(MeasurementT mt)
        {
            string units;
            unsafe
            {
                units = Marshal.PtrToStringAnsi((IntPtr) mt.units);
            }
            return new Measurement(mt.quantity, units);
        }
    }

}
