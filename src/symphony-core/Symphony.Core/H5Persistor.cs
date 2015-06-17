using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF5;
using HDF5DotNet;

namespace Symphony.Core
{
    public class H5Persistor : IPersistor
    {
        private const uint PersistenceVersion = 2;
        private const string VersionKey = "version";
        private const int FixedStringLenth = 40;

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe struct DateTimeOffsetT
        {
            [FieldOffset(0)]
            public long ticks;
            [FieldOffset(8)]
            public double offset;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe struct NoteT
        {
            [FieldOffset(0)]
            public DateTimeOffsetT time;
            [FieldOffset(16)]
            public fixed byte text[FixedStringLenth];
        }

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe struct MeasurementT
        {
            [FieldOffset(0)]
            public double quantity;
            [FieldOffset(8)]
            public fixed byte unit[FixedStringLenth];
        }

        internal H5Datatype StringType { get; private set; }
        internal H5Datatype DateTimeOffsetType { get; private set; }
        internal H5Datatype NoteType { get; private set; }
        internal H5Datatype MeasurementType { get; private set; }

        public H5Persistor(string filename, string purpose, DateTimeOffset startTime)
        {
            file = new H5File(filename);

            StringType = file.CreateDatatype("STRING40", H5T.H5TClass.STRING, FixedStringLenth);
            DateTimeOffsetType = file.CreateDatatype("DATETIMEOFFSET",
                                                     new[] {"utcTicks", "offsetHours"},
                                                     new[]
                                                         {
                                                             new H5Datatype(H5T.H5Type.NATIVE_LLONG),
                                                             new H5Datatype(H5T.H5Type.NATIVE_DOUBLE)
                                                         });
            NoteType = file.CreateDatatype("NOTE",
                                           new[] {"time", "text"},
                                           new[] {DateTimeOffsetType, StringType});
            MeasurementType = file.CreateDatatype("MEASUREMENT",
                                                  new[] {"quantity", "unit"},
                                                  new[] {new H5Datatype(H5T.H5Type.NATIVE_DOUBLE), StringType});

            Version = PersistenceVersion;
            H5PersistentExperiment.CreateExperiment(this, file, purpose, startTime);
            openEpochGroups = new Stack<IPersistentEpochGroup>();
        }

        public void Close(DateTimeOffset endTime)
        {
            while (CurrentEpochGroup != null)
            {
                EndEpochGroup(endTime);
            }
            ((H5PersistentExperiment) Experiment).EndTime = endTime;
            file.Close();
        }

        private readonly H5File file;
        private readonly Stack<IPersistentEpochGroup> openEpochGroups;

        public uint Version
        {
            get { return (uint) file.Attributes[VersionKey].GetValue(); }
            private set { file.Attributes[VersionKey] = new H5Attribute(value); }
        }

        private H5Group ExperimentGroup
        {
            get { return file.Groups.First(); }
        }

        public IPersistentExperiment Experiment
        {
            get { return new H5PersistentExperiment(this, ExperimentGroup); }
        }

        public IPersistentSource AddSource(string label, IPersistentSource parent)
        {
            return parent == null
                       ? ((H5PersistentExperiment) Experiment).AddSource(label)
                       : ((H5PersistentSource) parent).AddSource(label);
        }

        public IPersistentEpochGroup CurrentEpochGroup
        {
            get { return openEpochGroups.Count == 0 ? null : openEpochGroups.Peek(); }
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source, DateTimeOffset startTime)
        {
            var epochGroup = CurrentEpochGroup == null
                       ? ((H5PersistentExperiment) Experiment).AddEpochGroup(label, (H5PersistentSource) source, startTime)
                       : ((H5PersistentEpochGroup) CurrentEpochGroup).AddEpochGroup(label, (H5PersistentSource) source, startTime);
            openEpochGroups.Push(epochGroup);
            return epochGroup;
        }

        public IPersistentEpochGroup EndEpochGroup(DateTimeOffset endTime)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There are no open epoch groups");
            ((H5PersistentEpochGroup) CurrentEpochGroup).EndTime = endTime;
            return openEpochGroups.Pop();
        }

        public IPersistentEpoch Serialize(Epoch epoch)
        {
            if (CurrentEpochGroup == null)
                throw new InvalidOperationException("There is no open epoch group");
            return ((H5PersistentEpochGroup) CurrentEpochGroup).AddEpoch(epoch);
        }

        public void Delete(IPersistentEntity entity)
        {
            if (entity.Equals(Experiment))
                throw new InvalidOperationException("You cannot delete the experiment");
            if (openEpochGroups.Contains(entity))
                throw new InvalidOperationException("You cannot delete an open epoch group");
            ((H5AnnotatablePersistentEntity) entity).Delete();
        }

        internal NoteT Convert(Note n)
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

        internal Note Convert(NoteT nt)
        {
            long ticks = nt.time.ticks;
            double offset = nt.time.offset;
            var time = new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            string text;
            unsafe
            {
                text = Marshal.PtrToStringAnsi((IntPtr)nt.text);
            }
            return new Note(time, text);
        }

        internal MeasurementT Convert(IMeasurement m)
        {
            var mt = new MeasurementT {quantity = (double) m.Quantity};
            byte[] textdata = Encoding.ASCII.GetBytes(m.DisplayUnit);
            unsafe
            {
                Marshal.Copy(textdata, 0, (IntPtr)mt.unit, textdata.Length);
            }
            return mt;
        }

        internal IMeasurement Convert(MeasurementT mt)
        {
            string unit;
            unsafe
            {
                unit = Marshal.PtrToStringAnsi((IntPtr)mt.unit);
            }
            return new Measurement(mt.quantity, unit);
        }
    }

    abstract class H5PersistentEntity : IPersistentEntity
    {
        protected H5PersistentEntity(H5Persistor persistor, H5Group group)
        {
            Persistor = persistor;
            ObjectGroup = group;
        }

        public H5Persistor Persistor { get; private set; }

        public H5Group ObjectGroup { get; private set; }

        public string ID
        {
            get { return ObjectGroup.Path; }
        }

        public virtual void Delete()
        {
            ObjectGroup.Delete();
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
            return Equals(Persistor, other.Persistor) && Equals(ObjectGroup, other.ObjectGroup);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Persistor != null ? Persistor.GetHashCode() : 0) * 397) ^ (ObjectGroup != null ? ObjectGroup.GetHashCode() : 0);
            }
        }
    }

    abstract class H5AnnotatablePersistentEntity : H5PersistentEntity, IAnnotatablePersistentEntity
    {
        private const string KeywordsKey = "keywords";
        private const string PropertiesGroupName = "properties";
        private const string NotesDatasetName = "notes";

        protected static H5Group CreateEntityGroup(H5Persistor persistor, H5Group parent, string name)
        {
            var group = parent.AddGroup(name);
            group.AddGroup(PropertiesGroupName);
            group.AddDataset(NotesDatasetName, persistor.NoteType, new[] {0L}, new[] {-1L}, new[] {10L});
            return group;
        }

        protected H5AnnotatablePersistentEntity(H5Persistor persistor, H5Group group) : base(persistor, group)
        {
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
                if (!ObjectGroup.Attributes.ContainsKey(KeywordsKey))
                    return new List<string>();

                var keywords = (string) ObjectGroup.Attributes[KeywordsKey].GetValue();
                return keywords.Split(new[] {","}, StringSplitOptions.None);
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
            get { return ObjectGroup.Datasets.First(ds => ds.Name == NotesDatasetName); }
        }

        public IEnumerable<Note> Notes
        {
            get { return NotesDataset.GetData<H5Persistor.NoteT>().Select(Persistor.Convert); }
        }

        public Note AddNote(DateTimeOffset time, string text)
        {
            return AddNote(new Note(time, text));
        }

        public Note AddNote(Note note)
        {
            long n = NotesDataset.NumberOfElements;
            NotesDataset.Extend(new[] {n + 1});
            NotesDataset.SetData(new[] {Persistor.Convert(note)}, new[] {n}, new[] {1L});
            return note;
        }
    }

    class H5PersistentSource : H5AnnotatablePersistentEntity, IPersistentSource
    {
        private const string LabelKey = "label";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";

        public static H5PersistentSource CreateSource(H5Persistor persistor, H5Group parent, string label)
        {
            var group = CreateEntityGroup(persistor, parent, label);
            group.Attributes[LabelKey] = new H5Attribute(label);
            group.AddGroup(SourcesGroupName);
            group.AddGroup(EpochGroupsGroupName);
            return new H5PersistentSource(persistor, group);
        }

        public H5PersistentSource(H5Persistor persistor, H5Group group) : base(persistor, group)
        {
        }

        public override void Delete()
        {
            if (EpochGroups.Any())
                throw new InvalidOperationException("Cannot delete source with associated epoch groups");
            base.Delete();
        }

        public string Label
        {
            get { return (string)ObjectGroup.Attributes[LabelKey].GetValue(); }
        }

        private H5Group SourcesGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == SourcesGroupName); }
        } 

        public IEnumerable<IPersistentSource> Sources
        {
            get { return SourcesGroup.Groups.Select(g => new H5PersistentSource(Persistor, g)); }
        }

        public IPersistentSource AddSource(string label)
        {
            return CreateSource(Persistor, SourcesGroup, label);
        }

        private H5Group EpochGroupsGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == EpochGroupsGroupName); }
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return EpochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(Persistor, g)); }
        }

        public void AddEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            EpochGroupsGroup.AddHardLink(epochGroup.ObjectGroup.Name, epochGroup.ObjectGroup);
        }

        public void RemoveEpochGroup(H5PersistentEpochGroup epochGroup)
        {
            EpochGroupsGroup.Groups.First(g => g.Name == epochGroup.ObjectGroup.Name).Delete();
        }
    }

    abstract class H5TimelinePersistentEntity : H5AnnotatablePersistentEntity, ITimelinePersistentEntity
    {
        private const string StartTimeUtcTicksKey = "startTimeDotNetDateTimeOffsetUTCTicks";
        private const string StartTimeOffsetHoursKey = "startTimeUTCOffsetHours";
        private const string EndTimeUtcTicksKey = "endTimeDotNetDateTimeOffsetUTCTicks";
        private const string EndTimeOffsetHoursKey = "endTimeUTCOffsetHours";

        protected static H5Group CreateTimelineEntityGroup(H5Persistor persistor, H5Group parent, string name, DateTimeOffset startTime)
        {
            var group = CreateEntityGroup(persistor, parent, name);
            group.Attributes[StartTimeUtcTicksKey] = new H5Attribute(startTime.UtcTicks);
            group.Attributes[StartTimeOffsetHoursKey] = new H5Attribute(startTime.Offset.TotalHours);
            return group;
        }

        protected H5TimelinePersistentEntity(H5Persistor persistor, H5Group group) : base(persistor, group)
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
        }

        public DateTimeOffset? EndTime
        {
            get
            {
                if (!ObjectGroup.Attributes.ContainsKey(EndTimeUtcTicksKey) ||
                    !ObjectGroup.Attributes.ContainsKey(EndTimeOffsetHoursKey))
                    return null;

                var ticks = (long)ObjectGroup.Attributes[EndTimeUtcTicksKey].GetValue();
                var offset = (double)ObjectGroup.Attributes[EndTimeOffsetHoursKey].GetValue();
                return new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
            }
            set
            {
                if (value == null)
                {
                    if (ObjectGroup.Attributes.ContainsKey(EndTimeUtcTicksKey))
                        ObjectGroup.Attributes.Remove(EndTimeUtcTicksKey);
                    if (ObjectGroup.Attributes.ContainsKey(EndTimeOffsetHoursKey))
                        ObjectGroup.Attributes.Remove(EndTimeOffsetHoursKey);
                }
                else
                {
                    ObjectGroup.Attributes[EndTimeUtcTicksKey] = new H5Attribute(value.Value.UtcTicks);
                    ObjectGroup.Attributes[EndTimeOffsetHoursKey] = new H5Attribute(value.Value.Offset.TotalHours);   
                }
            }
        }
    }

    class H5PersistentExperiment : H5TimelinePersistentEntity, IPersistentExperiment
    {
        private const string PurposeKey = "purpose";
        private const string SourcesGroupName = "sources";
        private const string EpochGroupsGroupName = "epochGroups";

        public static H5PersistentExperiment CreateExperiment(H5Persistor persistor, H5Group parent, string purpose, DateTimeOffset startTime)
        {
            var group = CreateTimelineEntityGroup(persistor, parent, purpose, startTime);
            group.Attributes[PurposeKey] = new H5Attribute(purpose);
            group.AddGroup(SourcesGroupName);
            group.AddGroup(EpochGroupsGroupName);
            return new H5PersistentExperiment(persistor, group);
        }

        public H5PersistentExperiment(H5Persistor persistor, H5Group group) : base(persistor, group)
        {
        }

        public string Purpose
        {
            get { return (string) ObjectGroup.Attributes[PurposeKey].GetValue(); } 
        }

        private H5Group SourcesGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == SourcesGroupName); }
        }

        public IEnumerable<IPersistentSource> Sources
        {
            get { return SourcesGroup.Groups.Select(g => new H5PersistentSource(Persistor, g)); }
        }

        public IPersistentSource AddSource(string label)
        {
            return H5PersistentSource.CreateSource(Persistor, SourcesGroup, label);
        }

        private H5Group EpochGroupsGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == EpochGroupsGroupName); }
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return EpochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(Persistor, g)); }
        }

        public IPersistentEpochGroup AddEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            return H5PersistentEpochGroup.CreateEpochGroup(Persistor, EpochGroupsGroup, label, source, startTime);
        }
    }

    class H5PersistentEpochGroup : H5TimelinePersistentEntity, IPersistentEpochGroup
    {
        private const string LabelKey = "label";
        private const string SourceGroupName = "source";
        private const string EpochGroupsGroupName = "epochGroups";
        private const string EpochsGroupName = "epochs";

        public static H5PersistentEpochGroup CreateEpochGroup(H5Persistor persistor, H5Group parent, string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            var group = CreateTimelineEntityGroup(persistor, parent, label, startTime);
            group.Attributes[LabelKey] = new H5Attribute(label);
            group.AddHardLink(SourceGroupName, source.ObjectGroup);
            group.AddGroup(EpochGroupsGroupName);
            group.AddGroup(EpochsGroupName);
            return new H5PersistentEpochGroup(persistor, group);
        }

        public H5PersistentEpochGroup(H5Persistor persistor, H5Group group) : base(persistor, group)
        {
        }

        public override void Delete()
        {
            ((H5PersistentSource) Source).RemoveEpochGroup(this);
            base.Delete();
        }
        
        public string Label
        {
            get { return (string) ObjectGroup.Attributes[LabelKey].GetValue(); }
        }

        private H5Group SourceGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == SourceGroupName); }
        }

        public IPersistentSource Source
        {
            get { return new H5PersistentSource(Persistor, SourceGroup); }
        }

        private H5Group EpochGroupsGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == EpochGroupsGroupName); }
        }

        public IEnumerable<IPersistentEpochGroup> EpochGroups
        {
            get { return EpochGroupsGroup.Groups.Select(g => new H5PersistentEpochGroup(Persistor, g)); }
        }

        public IPersistentEpochGroup AddEpochGroup(string label, H5PersistentSource source, DateTimeOffset startTime)
        {
            return CreateEpochGroup(Persistor, EpochGroupsGroup, label, source, startTime);
        }

        private H5Group EpochsGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == EpochsGroupName); }
        }

        public IEnumerable<IPersistentEpoch> Epochs
        {
            get { return EpochsGroup.Groups.Select(g => new H5PersistentEpoch(Persistor, g)); }
        }

        public IPersistentEpoch AddEpoch(Epoch epoch)
        {
            return H5PersistentEpoch.CreateEpoch(Persistor, EpochsGroup, epoch);
        }
    }

    class H5PersistentEpoch : H5TimelinePersistentEntity, IPersistentEpoch
    {
        private const string ProtocolIDKey = "protocolID";
        private const string DurationKey = "durationSeconds";
        private const string ProtocolParametersGroupName = "protocolParameters";
        private const string ResponsesGroupName = "responses";
        private const string StimuliGroupName = "stimuli";

        public static H5PersistentEpoch CreateEpoch(H5Persistor persistor, H5Group parent, Epoch epoch)
        {
            var group = CreateTimelineEntityGroup(persistor, parent, "epoch", epoch.StartTime);
            group.Attributes[ProtocolIDKey] = new H5Attribute(epoch.ProtocolID);
            group.Attributes[DurationKey] = new H5Attribute(((TimeSpan) epoch.Duration).TotalSeconds);
            
            var parametersGroup = group.AddGroup(ProtocolParametersGroupName);
            foreach (var kv in epoch.ProtocolParameters)
            {
                parametersGroup.Attributes[kv.Key] = new H5Attribute(kv.Value);
            }
            
            var responsesGroup = group.AddGroup(ResponsesGroupName);
            foreach (var kv in epoch.Responses)
            {
                H5PersistentResponse.CreateResponse(persistor, responsesGroup, kv.Key, kv.Value);
            }

            var stimuliGroup = group.AddGroup(StimuliGroupName);
            foreach (var kv in epoch.Stimuli)
            {
                H5PersistentStimulus.CreateStimulus(persistor, stimuliGroup, kv.Key, kv.Value);
            }

            return new H5PersistentEpoch(persistor, group);
        }

        public H5PersistentEpoch(H5Persistor persistor, H5Group group) : base(persistor, group)
        {
        }

        public string ProtocolID
        {
            get { return (string) ObjectGroup.Attributes[ProtocolIDKey].GetValue(); }
        }

        public TimeSpan Duration
        {
            get { return TimeSpan.FromSeconds((double) ObjectGroup.Attributes[DurationKey].GetValue()); }
        }

        private H5Group ProtocolParametersGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == ProtocolParametersGroupName); }
        }

        public IEnumerable<KeyValuePair<string, object>> ProtocolParameters
        {
            get { return ProtocolParametersGroup.Attributes.ToDictionary(a => a.Name, a => a.GetValue()); }
        }

        private H5Group ResponsesGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == ResponsesGroupName); }
        }

        public IEnumerable<IPersistentResponse> Responses
        {
            get { return ResponsesGroup.Groups.Select(g => new H5PersistentResponse(Persistor, g)); }
        }

        private H5Group StimuliGroup
        {
            get { return ObjectGroup.Groups.First(g => g.Name == StimuliGroupName); }
        }

        public IEnumerable<IPersistentStimulus> Stimuli
        {
            get { return StimuliGroup.Groups.Select(g => new H5PersistentStimulus(Persistor, g)); }
        }
    }

    class H5PersistentResponse : H5AnnotatablePersistentEntity, IPersistentResponse
    {
        private const string DeviceNameKey = "deviceName";
        private const string DataDatasetName = "data";

        public static H5PersistentResponse CreateResponse(H5Persistor persistor, H5Group parent, IExternalDevice device, Response response)
        {
            var group = CreateEntityGroup(persistor, parent, device.Name);
            group.Attributes[DeviceNameKey] = new H5Attribute(device.Name);

            var data = group.AddDataset(DataDatasetName, persistor.MeasurementType, new long[] {response.Data.Count()});
            data.SetData(response.Data.Select(persistor.Convert).ToArray());

            return new H5PersistentResponse(persistor, group);
        }

        public H5PersistentResponse(H5Persistor persistor, H5Group group) : base(persistor, group)
        {
        }

        public string DeviceName
        {
            get { return (string) ObjectGroup.Attributes[DeviceNameKey].GetValue(); } 
        }

        private H5Dataset DataDataset
        {
            get { return ObjectGroup.Datasets.First(ds => ds.Name == DataDatasetName); }
        }

        public IEnumerable<IMeasurement> Data
        {
            get { return DataDataset.GetData<H5Persistor.MeasurementT>().Select(Persistor.Convert); }
        }
    }

    class H5PersistentStimulus : H5AnnotatablePersistentEntity, IPersistentStimulus
    {
        private const string DeviceNameKey = "deviceName";

        public static H5PersistentStimulus CreateStimulus(H5Persistor persistor, H5Group parent, IExternalDevice device, IStimulus stimulus)
        {
            var group = CreateEntityGroup(persistor, parent, device.Name);
            group.Attributes[DeviceNameKey] = new H5Attribute(device.Name);
            return new H5PersistentStimulus(persistor, group);
        }

        public H5PersistentStimulus(H5Persistor persistor, H5Group group) : base(persistor, group)
        {
        }
    }

}
