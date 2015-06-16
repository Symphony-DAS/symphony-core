using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF5;
using HDF5DotNet;

namespace Symphony.Core
{
    public class H5EpochPersistor : IEpochPersistor
    {
        private const string VersionKey = "version";
        private const string SourcesGroupName = "sources";
        private const string ExperimentsGroupName = "experiments";

        public H5EpochPersistor(string filename)
        {
            file = new H5File(filename);

            file.AddGroup(SourcesGroupName);
            file.AddGroup(ExperimentsGroupName);

            openEpochGroups = new Stack<H5PersistedEpochGroup>();
        }

        public void Close()
        {
            while (CurrentEpochGroup != null)
            {
                EndEpochGroup(DateTimeOffset.Now);
            }
            if (CurrentExperiment != null)
            {
                EndExperiment(DateTimeOffset.Now);
            }
            file.Close();
        }

        private readonly H5File file;
        private readonly Stack<H5PersistedEpochGroup> openEpochGroups;

        public string Version
        {
            get { return (string) file.Attributes[VersionKey].GetValue(); }
            private set { file.Attributes[VersionKey] = new H5Attribute(value); }
        }

        private H5Group SourcesGroup
        {
            get { return file.Groups.First(g => g.Name == SourcesGroupName); }
        }

        public IEnumerable<IPeristedSource> Sources
        {
            get { return SourcesGroup.Groups.Select(g => new H5PersistedSource(g)); }
        }

        public IPeristedSource AddSource(string label, IPeristedSource parent)
        {
            return parent == null
                       ? H5PersistedSource.CreateSource(SourcesGroup, label)
                       : ((H5PersistedSource) parent).AddSource(label);
        }

        private H5Group ExperimentsGroup
        {
            get { return file.Groups.First(g => g.Name == ExperimentsGroupName); }
        }

        public IEnumerable<IPersistedExperiment> Experiments
        {
            get { return ExperimentsGroup.Groups.Select(g => new H5PersistedExperiment(g)); }
        }

        public IPersistedExperiment CurrentExperiment { get; private set; }

        public void BeginExperiment(string purpose, DateTimeOffset startTime)
        {
            if (CurrentExperiment != null)
                throw new InvalidOperationException("There is an open experiment");
            CurrentExperiment = H5PersistedExperiment.CreateExperiment(ExperimentsGroup, purpose, startTime);
        }

        public void EndExperiment(DateTimeOffset endTime)
        {
            if (CurrentExperiment == null)
                throw new InvalidOperationException("There is no open experiment");
            ((H5PersistedExperiment) CurrentExperiment).EndTime = endTime;
            CurrentExperiment = null;
        }

        public IPersistedEpochGroup CurrentEpochGroup
        {
            get { return openEpochGroups.Count == 0 ? null : openEpochGroups.Peek(); }
        }

        public void BeginEpochGroup(string label, IPeristedSource source, DateTimeOffset startTime)
        {
            if (CurrentExperiment == null)
                throw new InvalidOperationException("There is no open experiment");
            var epochGroup = CurrentEpochGroup == null
                       ? ((H5PersistedExperiment) CurrentExperiment).AddEpochGroup(label, (H5PersistedSource) source, startTime)
                       : ((H5PersistedEpochGroup) CurrentEpochGroup).AddEpochGroup(label, (H5PersistedSource) source, startTime);
            openEpochGroups.Push(epochGroup);
        }

        public void EndEpochGroup(DateTimeOffset endTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There is no open epoch group");
            ((H5PersistedEpochGroup) CurrentEpochGroup).EndTime = endTime;
            openEpochGroups.Pop();
        }

        public void Delete(IPersistedEntity entity)
        {
            if (entity.Equals(CurrentExperiment))
                throw new InvalidOperationException("Cannot delete an open experiment");
            if (openEpochGroups.Contains(entity))
                throw new InvalidOperationException("Cannot delete an open epoch group");
            ((H5PersistedEntity) entity).Delete();
        }
    }

    class H5PersistedEntity : IPersistedEntity
    {
        [StructLayout(LayoutKind.Explicit)]
        unsafe struct DateTimeOffsetT
        {
            [FieldOffset(0)]
            public long ticks;
            [FieldOffset(8)]
            public double offset;
        }

        [StructLayout(LayoutKind.Explicit)]
        unsafe struct NoteT
        {
            [FieldOffset(0)]
            public DateTimeOffsetT time;
            [FieldOffset(16)]
            public fixed byte text[40];
        }

        private const string UuidKey = "uuid";
        private const string KeywordsKey = "keywords";
        private const string NotesDatasetName = "notes";
        private const string PropertiesGroupName = "properties";

        protected H5PersistedEntity(H5Group parent)
        {
            var uuid = Guid.NewGuid();
            ObjectGroup = parent.AddGroup(uuid.ToString());
            Uuid = uuid;

            H5File file = ObjectGroup.File;
            H5Datatype noteType;
            if (file.Datatypes.Any(d => d.Name == "NOTE"))
            {
                noteType = file.Datatypes.First(d => d.Name == "NOTE");
            }
            else
            {
                H5Datatype stringType = file.CreateDatatype("STRING40", H5T.H5TClass.STRING, 40);
                H5Datatype dateTimeOffsetType = file.CreateDatatype("DATETIMEOFFSET",
                                                                    new[] { "utcTicks", "offsetHours" },
                                                                    new[]
                                                                        {
                                                                            new H5Datatype(H5T.H5Type.NATIVE_LLONG),
                                                                            new H5Datatype(H5T.H5Type.NATIVE_DOUBLE)
                                                                        });
                noteType = file.CreateDatatype("NOTE",
                                                new[] { "time", "text" },
                                                new[] { dateTimeOffsetType, stringType });
            }
            ObjectGroup.AddDataset(NotesDatasetName, noteType, new[] { 0L }, new[] { -1L }, new[] { 10L });
            ObjectGroup.AddGroup(PropertiesGroupName);
        }

        protected H5PersistedEntity()
        {
        }

        internal virtual void Delete()
        {
            ObjectGroup.Delete();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((H5PersistedEntity)obj);
        }

        protected bool Equals(H5PersistedEntity other)
        {
            return ObjectGroup.Equals(other.ObjectGroup);
        }

        public override int GetHashCode()
        {
            return ObjectGroup.GetHashCode();
        }

        public H5Group ObjectGroup { get; protected set; }

        public Guid Uuid
        {
            get { return new Guid((string) ObjectGroup.Attributes[UuidKey].GetValue()); }
            private set { ObjectGroup.Attributes[UuidKey] = new H5Attribute(value.ToString()); }
        }

        private H5Group PropertiesGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == PropertiesGroupName); }
        }

        public IEnumerable<KeyValuePair<string, object>> Properties
        {
            get { return PropertiesGroup.Attributes.ToDictionary(p => p.Name, p => p.GetValue()); }
        }

        public void AddProperty(string key, object value)
        {
            PropertiesGroup.Attributes[key] = new H5Attribute(value);
        }

        public void RemoveProperty(string key)
        {
            if (!PropertiesGroup.Attributes.ContainsKey(key))
                throw new KeyNotFoundException("There is no property named " + key);
            PropertiesGroup.Attributes.Remove(key);
        }

        public IEnumerable<string> Keywords
        {
            get 
            { 
                var k = (string) ObjectGroup.Attributes[KeywordsKey].GetValue();
                return k.Split(new[] {","}, StringSplitOptions.None);
            }
        }

        public void AddKeyword(string keyword)
        {
            var keywords = new HashSet<string>(Keywords);
            keywords.Add(keyword);
            ObjectGroup.Attributes[KeywordsKey] = new H5Attribute(string.Join(",", keywords));
        }

        public void RemoveKeyword(string keyword)
        {
            var keywords = new HashSet<string>(Keywords);
            keywords.Remove(keyword);
            ObjectGroup.Attributes[KeywordsKey] = new H5Attribute(string.Join(",", keywords));
        }

        private H5Dataset NotesDataset
        {
            get { return ObjectGroup.Datasets.First(g => g.Name == NotesDatasetName); }
        }

        public IEnumerable<IPersistedNote> Notes
        {
            get { return NotesDataset.GetData<NoteT>().Select(Convert); }
        }

        public IPersistedNote AddNote(DateTimeOffset time, string text)
        {
            return AddNote(new H5PersistedNote(time, text));
        }

        public IPersistedNote AddNote(IPersistedNote note)
        {
            long n = NotesDataset.NumberOfElements;
            NotesDataset.Extend(new[] {n + 1});
            NotesDataset.SetData(new[] {Convert(note)}, new[] {n}, new[] {1L});
            return note;
        }

        private NoteT Convert(IPersistedNote n)
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

        private IPersistedNote Convert(NoteT nt)
        {
            long ticks = nt.time.ticks;
            double offset = nt.time.offset;
            var time = new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            string text;
            unsafe
            {
                text = Marshal.PtrToStringAnsi((IntPtr)nt.text);
            }
            return new H5PersistedNote(time, text);
        }
    }

    class H5PersistedSource : H5PersistedEntity, IPeristedSource
    {
        private const string LabelKey = "label";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";

        internal static H5PersistedSource CreateSource(H5Group parent, string label)
        {
            return new H5PersistedSource(parent, label);
        }

        private H5PersistedSource(H5Group parent, string label) : base(parent)
        {
            Label = label;
            ObjectGroup.AddGroup(SourcesGroupName);
            ObjectGroup.AddGroup(EpochGroupsGroupName);
        }

        internal H5PersistedSource(H5Group group)
        {
            ObjectGroup = group;
        }

        internal override void Delete()
        {
            if (EpochGroups.Any())
                throw new InvalidOperationException("Cannot delete source with associated epoch groups");
            base.Delete();
        }

        public string Label
        {
            get { return (string)ObjectGroup.Attributes[LabelKey].GetValue(); }
            set { ObjectGroup.Attributes[LabelKey] = new H5Attribute(value); }
        }

        private H5Group SourcesGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == SourcesGroupName); }
        } 

        public IEnumerable<IPeristedSource> Sources
        {
            get { return SourcesGroup.Groups.Select(g => new H5PersistedSource(g)); }
        }

        internal H5PersistedSource AddSource(string label)
        {
            return CreateSource(SourcesGroup, label);
        }

        private H5Group EpochGroupsGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == EpochGroupsGroupName); }
        }

        public IEnumerable<IPersistedEpochGroup> EpochGroups
        {
            get { return EpochGroupsGroup.Groups.Select(g => new H5PersistedEpochGroup(g)); }
        }

        internal void AddEpochGroup(H5PersistedEpochGroup epochGroup)
        {
            EpochGroupsGroup.AddHardLink(epochGroup.Uuid.ToString(), epochGroup.ObjectGroup);
        }

        internal void RemoveEpochGroup(H5PersistedEpochGroup epochGroup)
        {
            var group = EpochGroupsGroup.Groups.First(g => g.Name == epochGroup.Uuid.ToString());
            group.Delete();
        }
    }

    class H5TimelinePersistedEntity : H5PersistedEntity, ITimelinePersistedEntity
    {
        private const string StartTimeUtcTicksKey = "startTimeDotNetDateTimeOffsetUTCTicks";
        private const string StartTimeOffsetHoursKey = "startTimeUTCOffsetHours";
        private const string EndTimeUtcTicksKey = "endTimeDotNetDateTimeOffsetUTCTicks";
        private const string EndTimeOffsetHoursKey = "endTimeUTCOffsetHours";

        protected H5TimelinePersistedEntity(H5Group parent, DateTimeOffset startTime) : base(parent)
        {
            StartTime = startTime;
        }

        protected H5TimelinePersistedEntity()
        {
        }

        public DateTimeOffset StartTime
        {
            get
            {
                var ticks = (long) ObjectGroup.Attributes[StartTimeUtcTicksKey].GetValue();
                var offset = (double) ObjectGroup.Attributes[StartTimeOffsetHoursKey].GetValue();
                return new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            }
            set 
            { 
                ObjectGroup.Attributes[StartTimeUtcTicksKey] = new H5Attribute(value.UtcTicks);
                ObjectGroup.Attributes[StartTimeOffsetHoursKey] = new H5Attribute(value.Offset.TotalHours);
            }
        }

        public DateTimeOffset? EndTime
        {
            get
            {
                if (!ObjectGroup.Attributes.ContainsKey(EndTimeUtcTicksKey))
                    return null;
                var ticks = (long)ObjectGroup.Attributes[EndTimeUtcTicksKey].GetValue();
                var offset = (double)ObjectGroup.Attributes[EndTimeOffsetHoursKey].GetValue();
                return new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            }
            internal set
            {
                if (value == null)
                {
                    ObjectGroup.Attributes.Remove(EndTimeUtcTicksKey);
                    ObjectGroup.Attributes.Remove(EndTimeOffsetHoursKey);
                    return;
                }
                ObjectGroup.Attributes[EndTimeUtcTicksKey] = new H5Attribute(value.Value.UtcTicks);
                ObjectGroup.Attributes[EndTimeOffsetHoursKey] = new H5Attribute(value.Value.Offset.TotalHours);
            }
        }
    }

    class H5PersistedExperiment : H5TimelinePersistedEntity, IPersistedExperiment
    {
        private const string PurposeKey = "purpose";
        private const string EpochGroupsGroupName = "epochGroups";

        internal static H5PersistedExperiment CreateExperiment(H5Group parent, string purpose, DateTimeOffset startTime)
        {
            return new H5PersistedExperiment(parent, purpose, startTime);
        }

        private H5PersistedExperiment(H5Group parent, string purpose, DateTimeOffset startTime) : base(parent, startTime)
        {
            Purpose = purpose;
            ObjectGroup.AddGroup(EpochGroupsGroupName);
        }

        internal H5PersistedExperiment(H5Group group)
        {
            ObjectGroup = group;
        }

        public string Purpose
        {
            get { return (string) ObjectGroup.Attributes[PurposeKey].GetValue(); } 
            set { ObjectGroup.Attributes[PurposeKey] = new H5Attribute(value); }
        }

        private H5Group EpochGroupsGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == EpochGroupsGroupName); }
        }

        public IEnumerable<IPersistedEpochGroup> EpochGroups
        {
            get { return EpochGroupsGroup.Groups.Select(g => new H5PersistedEpochGroup(g)); }
        }

        internal H5PersistedEpochGroup AddEpochGroup(string label, H5PersistedSource source, DateTimeOffset startTime)
        {
            return H5PersistedEpochGroup.CreateEpochGroup(EpochGroupsGroup, label, source, startTime);
        }
    }

    class H5PersistedEpochGroup : H5TimelinePersistedEntity, IPersistedEpochGroup
    {
        private const string LabelKey = "label";
        private const string SourceGroupName = "source";
        private const string EpochGroupsGroupName = "epochGroups";

        internal static H5PersistedEpochGroup CreateEpochGroup(H5Group parent, string label, H5PersistedSource source, DateTimeOffset startTime)
        {
            return new H5PersistedEpochGroup(parent, label, source, startTime);
        }

        private H5PersistedEpochGroup(H5Group parent, string label, H5PersistedSource source, DateTimeOffset startTime) : base(parent, startTime)
        {
            Label = label;
            ObjectGroup.AddHardLink(SourceGroupName, source.ObjectGroup);
            ObjectGroup.AddGroup(EpochGroupsGroupName);
        }

        internal H5PersistedEpochGroup(H5Group group)
        {
            ObjectGroup = group;
        }

        internal override void Delete()
        {
            ((H5PersistedSource) Source).RemoveEpochGroup(this);
            base.Delete();
        }
        
        public string Label
        {
            get { return (string) ObjectGroup.Attributes[LabelKey].GetValue(); }
            set { ObjectGroup.Attributes[LabelKey] = new H5Attribute(value); }
        }

        private H5Group SourceGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == SourceGroupName); }
        }

        public IPeristedSource Source
        {
            get { return new H5PersistedSource(SourceGroup); }
        }

        private H5Group EpochGroupsGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == EpochGroupsGroupName); }
        }

        public IEnumerable<IPersistedEpochGroup> EpochGroups
        {
            get { return EpochGroupsGroup.Groups.Select(g => new H5PersistedEpochGroup(g)); }
        }

        internal H5PersistedEpochGroup AddEpochGroup(string label, H5PersistedSource source, DateTimeOffset startTime)
        {
            return CreateEpochGroup(EpochGroupsGroup, label, source, startTime);
        }

        public IEnumerable<IPersistedEpoch> Epochs { get; private set; }
    }

    class H5PersistedNote : IPersistedNote
    {
        public DateTimeOffset Time { get; private set; }
        public string Text { get; private set; }

        internal H5PersistedNote(DateTimeOffset time, string text)
        {
            Time = time;
            Text = text;
        }
    }

}
