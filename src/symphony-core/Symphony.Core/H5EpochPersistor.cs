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
        private readonly H5File file;

        private readonly Stack<H5EpochGroup> openEpochGroups;
        private H5EpochGroup CurrentEpochGroup
        {
            get { return openEpochGroups.Count == 0 ? null : openEpochGroups.Peek(); }
        }

        public H5EpochPersistor(string filename)
        {
            file = new H5File(filename);
            experiment = H5Experiment.CreateExperiment(file, "my experiment");
            openEpochGroups = new Stack<H5EpochGroup>();
        }

        public void Close()
        {
            file.Close();
        }

        private readonly H5Experiment experiment;

        public IExperiment Experiment
        {
            get { return experiment; }
        }
        
        public ISource AddSource(string label, ISource parent = null)
        {
            return parent == null ? experiment.AddSource(label) : ((H5Source) parent).AddSource(label);
        }

        public IEpochGroup BeginEpochGroup(string label, ISource source, DateTimeOffset startTime)
        {
            var group = CurrentEpochGroup == null
                       ? experiment.AddEpochGroup(label, (H5Source) source, startTime)
                       : CurrentEpochGroup.AddEpochGroup(label, (H5Source) source, startTime);
            openEpochGroups.Push(group);
            return group;
        }

        public IEpochGroup EndEpochGroup(DateTimeOffset endTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There is no open epoch group");
            CurrentEpochGroup.EndTime = endTime;
            return openEpochGroups.Pop();
        }

        public void Delete(IEntity entity)
        {
            if (openEpochGroups.Contains(entity))
                throw new InvalidOperationException("Cannot delete an open epoch group");
            ((H5Entity) entity).Delete();
        }
    }

    class H5Entity : IEntity
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

        private readonly H5Group objectGroup;
        private readonly H5Group propertiesGroup;
        private readonly H5Dataset notesDataset;

        private const string KeywordsKey = "keywords";

        private const string PropertiesName = "properties";
        private const string NotesName = "notes";

        protected static H5Entity CreateEntity(H5Group parent, string name)
        {
            H5Group group = parent.AddGroup(name);

            group.Attributes[KeywordsKey] = new H5Attribute("");

            group.AddGroup(PropertiesName);

            H5Datatype noteType;
            H5File file = group.File;
            if (file.Datatypes.Any(d => d.Name == "NOTE"))
            {
                noteType = file.Datatypes.First(d => d.Name == "NOTE");
            }
            else
            {
                H5Datatype stringType = file.CreateDatatype("STRING40", H5T.H5TClass.STRING, 40);
                H5Datatype dateTimeOffsetType = file.CreateDatatype("DATETIMEOFFSET",
                                                                    new[] {"utcTicks", "offsetHours"},
                                                                    new[]
                                                                        {
                                                                            new H5Datatype(H5T.H5Type.NATIVE_LLONG),
                                                                            new H5Datatype(H5T.H5Type.NATIVE_DOUBLE)
                                                                        });
                noteType = file.CreateDatatype("NOTE",
                                               new[] {"time", "text"},
                                               new[] {dateTimeOffsetType, stringType});
            }
            group.AddDataset(NotesName, noteType, new[] {0L}, new[] {-1L}, new[] {10L});

            return new H5Entity(group);
        }

        protected H5Entity(H5Group group)
        {
            objectGroup = group;
            propertiesGroup = group.Groups.First(g => g.Name == PropertiesName);
            notesDataset = group.Datasets.First(g => g.Name == NotesName);
        }

        internal H5Group ObjectGroup { get { return objectGroup; } }

        internal virtual void Delete()
        {
            objectGroup.Delete();
        }

        public IEnumerable<KeyValuePair<string, object>> Properties
        {
            get { return propertiesGroup.Attributes.ToDictionary(p => p.Name, p => p.GetValue()); }
        }

        public void AddProperty(string key, object value)
        {
            propertiesGroup.Attributes[key] = new H5Attribute(value);
        }

        public void RemoveProperty(string key)
        {
            if (!propertiesGroup.Attributes.ContainsKey(key))
                throw new KeyNotFoundException();
            propertiesGroup.Attributes.Remove(key);
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

        public IEnumerable<INote> Notes
        {
            get { return notesDataset.GetData<NoteT>().Select(Convert); }
        }

        public INote AddNote(DateTimeOffset time, string text)
        {
            return AddNote(new H5Note(time, text));
        }

        public INote AddNote(INote note)
        {
            long nNotes = notesDataset.NumberOfElements;
            notesDataset.Extend(new[] {nNotes + 1});
            notesDataset.SetData(new[] {Convert(note)}, new[] {nNotes}, new[] {1L});
            return note;
        }

        private NoteT Convert(INote n)
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

        private INote Convert(NoteT nt)
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
    }

    class H5Experiment : H5Entity, IExperiment
    {
        private readonly H5Group sourcesGroup;
        private readonly H5Group epochGroupsGroup;

        private const string PurposeKey = "purpose";

        private const string SourcesName = "sources";
        private const string EpochGroupsName = "epochGroups";

        internal static H5Experiment CreateExperiment(H5Group parent, string purpose)
        {
            var group = CreateEntity(parent, purpose).ObjectGroup;

            group.Attributes[PurposeKey] = new H5Attribute(purpose);

            group.AddGroup(SourcesName);
            group.AddGroup(EpochGroupsName);
            
            return new H5Experiment(group);
        }

        internal H5Experiment(H5Group group) : base(group)
        {
            sourcesGroup = ObjectGroup.Groups.First(g => g.Name == SourcesName);
            epochGroupsGroup = ObjectGroup.Groups.First(g => g.Name == EpochGroupsName);
        }

        public string Purpose
        {
            get { return (string) ObjectGroup.Attributes[PurposeKey].GetValue(); } 
            set { ObjectGroup.Attributes[PurposeKey] = new H5Attribute(value);}
        }

        public IEnumerable<ISource> Sources
        {
            get { return sourcesGroup.Groups.Select(g => new H5Source(g)); }
        }

        internal H5Source AddSource(string label)
        {
            return H5Source.CreateSource(sourcesGroup, label);
        }

        public IEnumerable<IEpochGroup> EpochGroups
        {
            get { return epochGroupsGroup.Groups.Select(g => new H5EpochGroup(g)); }
        }

        internal H5EpochGroup AddEpochGroup(string label, H5Source source, DateTimeOffset startTime)
        {
            return H5EpochGroup.CreateEpochGroup(epochGroupsGroup, label, source, startTime);
        }
    }

    class H5Source : H5Entity, ISource
    {
        private readonly H5Group sourcesGroup;
        private readonly H5Group epochGroupsGroup;

        private const string LabelKey = "label";

        private const string SourcesName = "sources";
        private const string EpochGroupsName = "epochGroups";

        internal static H5Source CreateSource(H5Group parent, string label)
        {
            var group = CreateEntity(parent, label).ObjectGroup;

            group.Attributes[LabelKey] = new H5Attribute(label);

            group.AddGroup(SourcesName);
            group.AddGroup(EpochGroupsName);

            return new H5Source(group);
        }

        internal H5Source(H5Group group) : base(group)
        {
            sourcesGroup = ObjectGroup.Groups.First(g => g.Name == SourcesName);
            epochGroupsGroup = ObjectGroup.Groups.First(g => g.Name == EpochGroupsName);
        }

        internal override void Delete()
        {
            if (EpochGroups.Any())
                throw new InvalidOperationException("Cannot delete source with associated epoch groups");
            base.Delete();
        }

        public string Label
        {
            get { return (string) ObjectGroup.Attributes[LabelKey].GetValue(); }
            set { ObjectGroup.Attributes[LabelKey] = new H5Attribute(value); }
        }

        public IEnumerable<ISource> Sources
        {
            get { return sourcesGroup.Groups.Select(g => new H5Source(g)); }
        }

        internal H5Source AddSource(string label)
        {
            return CreateSource(sourcesGroup, label);
        }

        public IEnumerable<IEpochGroup> EpochGroups
        {
            get { return epochGroupsGroup.Groups.Select(g => new H5EpochGroup(g)); }
        }

        internal void AddEpochGroup(H5EpochGroup epochGroup)
        {
            epochGroupsGroup.AddHardLink(epochGroup.Label, epochGroup.ObjectGroup);
        }

        internal void RemoveEpochGroup(H5EpochGroup epochGroup)
        {
            var group = epochGroupsGroup.Groups.First(g => g.Name == epochGroup.Label);
            group.Delete();
        }
    }

    class H5EpochGroup : H5Entity, IEpochGroup
    {
        private readonly H5Group sourceGroup;
        private readonly H5Group epochGroupsGroup;

        private const string LabelKey = "label";
        private const string StartTimeUtcTicksKey = "startTimeDotNetDateTimeOffsetUTCTicks";
        private const string StartTimeOffsetHoursKey = "startTimeUTCOffsetHours";
        private const string EndTimeUtcTicksKey = "endTimeDotNetDateTimeOffsetUTCTicks";
        private const string EndTimeOffsetHoursKey = "endTimeUTCOffsetHours";

        private const string SourceName = "source";
        private const string EpochGroupsName = "epochGroups";

        internal static H5EpochGroup CreateEpochGroup(H5Group parent, string label, H5Source source, DateTimeOffset startTime)
        {
            var group = CreateEntity(parent, label).ObjectGroup;

            group.Attributes[LabelKey] = new H5Attribute(label);
            group.Attributes[StartTimeUtcTicksKey] = new H5Attribute(startTime.UtcTicks);
            group.Attributes[StartTimeOffsetHoursKey] = new H5Attribute(startTime.Offset.TotalHours);

            group.AddHardLink(SourceName, source.ObjectGroup);
            group.AddGroup(EpochGroupsName);

            var epochGroup = new H5EpochGroup(group);
            source.AddEpochGroup(epochGroup);

            return epochGroup;
        }

        internal H5EpochGroup(H5Group group) : base(group)
        {
            sourceGroup = ObjectGroup.Groups.First(g => g.Name == SourceName);
            epochGroupsGroup = ObjectGroup.Groups.First(g => g.Name == EpochGroupsName);
        }

        internal override void Delete()
        {
            ((H5Source) Source).RemoveEpochGroup(this);
            base.Delete();
        }

        internal H5EpochGroup AddEpochGroup(string label, H5Source source, DateTimeOffset startTime)
        {
            return CreateEpochGroup(epochGroupsGroup, label, source, startTime);
        }
        
        public string Label
        {
            get { return (string) ObjectGroup.Attributes[LabelKey].GetValue(); }
            set { ObjectGroup.Attributes[LabelKey] = new H5Attribute(value); }
        }
        
        public DateTimeOffset StartTime
        {
            get
            {
                var ticks = (long) ObjectGroup.Attributes[StartTimeUtcTicksKey].GetValue();
                var offset = (double) ObjectGroup.Attributes[StartTimeOffsetHoursKey].GetValue();
                return new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
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
                    return;
                ObjectGroup.Attributes[EndTimeUtcTicksKey] = new H5Attribute(value.Value.UtcTicks);
                ObjectGroup.Attributes[EndTimeOffsetHoursKey] = new H5Attribute(value.Value.Offset.TotalHours);
            }
        }

        public ISource Source { get { return new H5Source(sourceGroup); } }
    }

    class H5Note : INote
    {
        public DateTimeOffset Time { get; private set; }
        public string Text { get; private set; }

        internal H5Note(DateTimeOffset time, string text)
        {
            Time = time;
            Text = text;
        }
    }

}
