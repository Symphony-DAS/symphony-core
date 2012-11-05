using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using HDF5DotNet;
using log4net;

namespace Symphony.Core
{
    /// <summary>
    /// EpochPersistor subclass for persisting Epochs to an HDF5 data file.
    /// </summary>
    public class EpochHDF5Persistor : EpochPersistor, IDisposable
    {
        private const string startTimeUtcName = "startTimeDotNetDateTimeOffsetUTCTicks";
        private const string endTimeUtcName = "endTimeDotNetDateTimeOffsetUTCTicks";
        private const string startTimeOffsetName = "startTimeUTCOffsetHours";
        private const string endTimeOffsetName = "endTimeUTCOffsetHours";
        private const int FIXED_STRING_LENGTH = 40;

        [StructLayout(LayoutKind.Explicit)]
        unsafe struct KeyValT
        {
            [FieldOffset(0)]
            public fixed byte key[FIXED_STRING_LENGTH];
            [FieldOffset(FIXED_STRING_LENGTH)]
            public fixed byte val[FIXED_STRING_LENGTH];
        }

        [StructLayout(LayoutKind.Explicit)]
        unsafe struct MeasurementT
        {
            [FieldOffset(0)]
            public double quantity;
            [FieldOffset(8)]
            public fixed byte unit[FIXED_STRING_LENGTH];
        }

        [StructLayout(LayoutKind.Explicit)]
        unsafe struct ExtdevMeasurementT
        {
            [FieldOffset(0)]
            public fixed byte extDeviceName[FIXED_STRING_LENGTH];
            [FieldOffset(FIXED_STRING_LENGTH)]
            public MeasurementT measurement;
        }

        [StructLayout(LayoutKind.Explicit)]
        unsafe struct ExtdevBackgroundMeasurementT
        {
            [FieldOffset(0)]
            public fixed byte extDeviceName[FIXED_STRING_LENGTH];
            [FieldOffset(FIXED_STRING_LENGTH)]
            public MeasurementT measurement;
            [FieldOffset(8 + 2 * FIXED_STRING_LENGTH)]
            public MeasurementT sampleRate;
        }


        readonly H5FileId fileId;

        class EpochGroupIDs
        {
            public H5GroupId GroupId { get; private set; }
            public H5GroupId SubGroupsId { get; private set; }
            public H5GroupId EpochsId { get; private set; }

            public EpochGroupIDs(H5GroupId group,
                H5GroupId subGroups,
                H5GroupId epochs)
            {
                GroupId = group;
                SubGroupsId = subGroups;
                EpochsId = epochs;
            }
        }

        private Stack<EpochGroupIDs> EpochGroupsIDs { get; set; }
        EpochGroupIDs CurrentEpochGroupID
        {
            get
            {
                return EpochGroupsIDs.Count == 0 ? null : EpochGroupsIDs.Peek();
            }
        }
        readonly H5DataTypeId string_t;
        readonly H5DataTypeId keyval_t;
        readonly H5DataTypeId measurement_t;
        readonly H5DataTypeId extdevmeasurement_t;
        bool _disposed = false;

        /// <summary>
        /// Constructs a new EpochHDF5Persistor with an hdf5 file at the given path using maximum data compression
        /// </summary>
        /// <param name="filename">HDF5 file path</param>
        /// <param name="assocFilePrefix">Prefix for auxiliary (e.g. image) file associated with this HDF5 file</param>
        public EpochHDF5Persistor(string filename, string assocFilePrefix)
            : this(filename, assocFilePrefix, (uint)9)
        {
        }

        /// <summary>
        /// Constructs a new EpochHDF5Persistor with an hdf5 file at the given path.
        /// </summary>
        /// <param name="filename">HDF5 file path</param>
        /// <param name="assocFilePrefix">Prefix for auxiliary (e.g. image) file associated with this HDF5 file</param>
        /// <param name="compression">Automatically numeric data compression (0 = none, 9 = maximum)</param>
        public EpochHDF5Persistor(string filename, string assocFilePrefix, uint compression)
            : this(filename, assocFilePrefix, Guid.NewGuid, compression)
        {
        }

        /// <summary>
        /// Constructs a new EpochHDF5Persistor with an HDF5 file at the given path.
        /// </summary>
        /// <param name="filename">Desired HDF5 path</param>
        /// <param name="assocFilePrefix">Prefix for auxiliary (e.g. image) file associated with this HDF5 file</param>
        /// <param name="guidGenerator">Function for generating new UUIDs (e.g. Guid.NewGuid)</param>
        /// <param name="compression">Automatically numeric data compression (0 = none, 9 = maximum)</param>
        public EpochHDF5Persistor(string filename, string assocFilePrefix, Func<Guid> guidGenerator, uint compression = 9)
            : base(guidGenerator)
        {
            if (filename == null)
                throw new ArgumentException("File name must not be null", "filename");

            if(compression > 9)
                throw new ArgumentException("Compression must be 0-9", "compression");

            if (assocFilePrefix == null)
                assocFilePrefix = "";


            this.AssociatedFilePrefix = assocFilePrefix;

            NumericDataCompression = compression;

            EpochGroupsIDs = new Stack<EpochGroupIDs>();

            var fInfo = new FileInfo(filename);
            string prefixedFilePath = fInfo.DirectoryName + Path.DirectorySeparatorChar + this.AssociatedFilePrefix + fInfo.Name;

            var currentFile = new FileInfo(prefixedFilePath);
            if (currentFile.Exists)
            {
                fileId = H5F.open(prefixedFilePath, H5F.OpenMode.ACC_RDWR);

                string_t = H5T.open(fileId, "STRING40");
                keyval_t = H5T.open(fileId, "KEY40VAR40");
                measurement_t = H5T.open(fileId, "MEASUREMENT");
                extdevmeasurement_t = H5T.open(fileId, "EXTDEV_MEASUREMENT");

                //TODO Check persistence version
            }
            else
            {
                fileId = H5F.create(prefixedFilePath, H5F.CreateMode.ACC_EXCL);
                WriteAttribute(fileId, "version", Version);
                // Create our standard String type (string of length FIXED_STRING_LENGTH characters)
                string_t = H5T.copy(H5T.H5Type.C_S1);
                H5T.setSize(string_t, 40);
                H5T.commit(fileId, "STRING40", string_t);

                // Create our key/value compound type (two strings of length 40 characters)
                keyval_t = H5T.create(H5T.CreateClass.COMPOUND, 80);
                H5T.insert(keyval_t, "key", 0, string_t);
                H5T.insert(keyval_t, "value", FIXED_STRING_LENGTH, string_t);
                H5T.commit(fileId, "KEY40VAR40", keyval_t);

                // Create the Measurement compound type
                measurement_t = H5T.create(H5T.CreateClass.COMPOUND, 48); // confirm 48 is enough/too much/whatever
                H5T.insert(measurement_t, "quantity", 0, H5T.H5Type.NATIVE_DOUBLE);
                H5T.insert(measurement_t, "unit", H5T.getSize(H5T.H5Type.NATIVE_DOUBLE), string_t);
                H5T.commit(fileId, "MEASUREMENT", measurement_t);

                // Create the ExtDev/Measurement compound type
                extdevmeasurement_t = H5T.create(H5T.CreateClass.COMPOUND,
                                                 H5T.getSize(string_t) + 2 * H5T.getSize(measurement_t));
                H5T.insert(extdevmeasurement_t, "externalDevice", 0, string_t);
                H5T.insert(extdevmeasurement_t, "measurement", H5T.getSize(string_t), measurement_t);
                H5T.commit(fileId, "EXTDEV_MEASUREMENT", extdevmeasurement_t);
            }

            Interlocked.Increment(ref _openHdf5FileCount);
        }


        /// <summary>
        /// Apply automatic zlib compression to numeric data.
        /// </summary>
        public uint NumericDataCompression { get; private set; }

        private readonly ILog log = LogManager.GetLogger(typeof(EpochHDF5Persistor));

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    CloseDocument();
                    if (_openHdf5FileCount == 0)
                        H5.Close();

                    GC.SuppressFinalize(this);
                }
            }
            _disposed = true;
        }


        public void Dispose()
        {
            Dispose(true);
        }
        ~EpochHDF5Persistor()
        {
            Dispose(false);
        }


        private static int _openHdf5FileCount = 0;

        public override void CloseDocument()
        {
            if (fileId != null && fileId.Id > 0 && !_fileClosed)
            {
                foreach (var group in EpochGroupsIDs)
                {
                    try
                    {
                        H5G.close(group.GroupId);
                        H5G.close(group.SubGroupsId);
                        H5G.close(group.EpochsId);
                    }
                    catch (H5GcloseException ex)
                    {
                        log.DebugFormat("HDF5 group already closed: {0}", ex);
                    }
                }

                try
                {
                    H5F.close(fileId);
                }
                catch (H5FcloseException ex)
                {
                    log.DebugFormat("HDF5 file already closed: {0}", ex);
                }
                Interlocked.Decrement(ref _openHdf5FileCount);
                _fileClosed = true;
            }
        }

        private bool _fileClosed = false;

        protected override void WriteEpochGroupStart(string label,
            string source,
            string[] keywords,
            IDictionary<string, object> properties,
            Guid identifier,
            DateTimeOffset startTime,
            double timeZoneOffset)
        {
            H5FileOrGroupId parent = CurrentEpochGroupID == null ?
                (H5FileOrGroupId)fileId : CurrentEpochGroupID.SubGroupsId;

            var epochGroup = H5G.create((H5LocId)parent, label + "-" + identifier);

            var subGroups = H5G.create((H5LocId)epochGroup, "epochGroups");
            var epochs = H5G.create((H5LocId)epochGroup, "epochs");

            WriteAttribute(epochGroup, "label", label);
            WriteAttribute(epochGroup, "source", string.IsNullOrEmpty(source) ? "<none>" : source);
            WriteDictionary(epochGroup, "properties", properties);
            WriteAttribute(epochGroup, "symphony.uuid", identifier.ToString());
            WriteKeywords(epochGroup, new HashSet<string>(keywords));
            WriteAttribute(epochGroup, startTimeUtcName, startTime.Ticks);
            WriteAttribute(epochGroup, startTimeOffsetName, timeZoneOffset);

            //H5G.close(subGroups);
            //H5G.close(epochs);

            EpochGroupsIDs.Push(new EpochGroupIDs(epochGroup, subGroups, epochs));
        }

        public override void Serialize(Epoch e, string fileTag)
        {
            if (!e.StartTime)
                throw new PersistanceException("Epoch must have a start time.");

            if (CurrentEpochGroupID == null)
                throw new InvalidOperationException("Cannot serialize an Epoch without an open EpochGroup. Call BeginEpochGroup before attempting to serialize an Epoch.");

            var groupName = "epoch-" + e.ProtocolID + "-" + GuidGenerator();
            H5GroupId epochID = H5G.create(CurrentEpochGroupID.EpochsId, groupName); //epochGroupMetadata
            log.DebugFormat("Serializing Epoch to HDF5 {0}", groupName);

            WriteAttribute(epochID, "protocolID", e.ProtocolID);

            // WriteEpochStart and duration
            WriteAttribute(epochID, startTimeOffsetName, ((DateTimeOffset)e.StartTime).Offset.TotalHours); //TimeZone.CurrentTimeZone.GetUtcOffset(((DateTimeOffset)e.StartTime).DateTime).TotalHours);
            WriteAttribute(epochID, startTimeUtcName, ((DateTimeOffset)e.StartTime).Ticks);
            WriteAttribute(epochID, "durationSeconds", ((TimeSpan)e.Duration).TotalSeconds);
            if (fileTag != null)
                WriteAttribute(epochID, "fileTag", fileTag);

            //Write keywords
            WriteKeywords(epochID, e.Keywords);

            // WriteBackground
            Write(epochID, "background", e.Background);

            // WriteProtocolParams
            WriteDictionary(epochID, "protocolParameters", e.ProtocolParameters);

            // WriteStimulus
            WriteStimuli(epochID, "stimuli", e.Stimuli);

            // WriteResponse
            WriteResponses(epochID, "responses", e.Responses);


            H5G.close(epochID);

            H5F.flush(fileId);
        }

        private static void WriteKeywords(H5ObjectWithAttributes objectID, ISet<string> keywords)
        {
            if (keywords.Count == 0)
                return;

            WriteAttribute(objectID, "keywords", string.Join(",", keywords));
        }

        protected override void WriteEpochGroupEnd(DateTimeOffset endTime)
        {
            if (CurrentEpochGroupID == null)
                throw new InvalidOperationException("There is no open EpochGroup.");

            var epochGroup = EpochGroupsIDs.Pop();
            WriteAttribute(epochGroup.GroupId, endTimeUtcName, endTime.UtcTicks);
            WriteAttribute(epochGroup.GroupId, endTimeOffsetName, endTime.Offset.TotalHours);//TimeZone.CurrentTimeZone.GetUtcOffset(endTime.DateTime).TotalHours);

            H5G.close(epochGroup.SubGroupsId);
            H5G.close(epochGroup.EpochsId);
            H5G.close(epochGroup.GroupId);

            H5F.flush(fileId);

        }


        private void Write(H5GroupId parent, string name, long ul)
        {
            H5DataSpaceId spaceId = H5S.create_simple(1, new long[1] { 1 });
            H5DataSetId dataSetId = H5D.create(parent, name, H5T.H5Type.NATIVE_LLONG, spaceId);

            long data = ul;
            H5D.writeScalar<long>(dataSetId, new H5DataTypeId(H5T.H5Type.NATIVE_LLONG), ref data);

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }
        private void Write(H5GroupId parent, string name, ulong ul)
        {
            H5DataSpaceId spaceId = H5S.create_simple(1, new long[1] { 1 });
            H5DataSetId dataSetId = H5D.create(parent, name, H5T.H5Type.NATIVE_ULLONG, spaceId);

            ulong data = ul;
            H5D.writeScalar<ulong>(dataSetId, new H5DataTypeId(H5T.H5Type.NATIVE_ULLONG), ref data);

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }
        private void Write(H5GroupId parent, string name, double d)
        {
            H5DataSpaceId spaceId = H5S.create_simple(1, new long[1] { 1 });
            H5DataSetId dataSetId = H5D.create(parent, name, H5T.H5Type.NATIVE_DOUBLE, spaceId);

            double data = d;
            H5D.writeScalar<double>(dataSetId, new H5DataTypeId(H5T.H5Type.NATIVE_DOUBLE), ref data);

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }

        private void Write(H5GroupId parent, string name, string str)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name must not be empty (or null)", "name");

            if (string.IsNullOrEmpty(str))
                throw new ArgumentException("Value must not be empty or null", "value");

            H5DataTypeId dtype;
            byte[] data = EncodeStringData(str, out dtype);

            H5DataSpaceId spaceId = H5S.create_simple(1, new long[] { 1 });
            H5DataSetId dataSetId = H5D.create(parent, name, dtype, spaceId);

            H5D.write(dataSetId, string_t, new H5Array<byte>(data));

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }

        private static byte[] EncodeStringData(string str, out H5DataTypeId dtype)
        {
            byte[] strdata = System.Text.Encoding.UTF8.GetBytes(str);


            dtype = H5T.copy(H5T.H5Type.C_S1);
            H5T.setSize(dtype, strdata.Length);

            return strdata;
        }


        private static void WriteAttribute(H5ObjectWithAttributes target, string name, string value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name must not be empty (or null)", "name");

            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value must not be empty or null", "value");

            H5DataTypeId dtype;
            byte[] strdata = EncodeStringData(value, out dtype);

            H5DataSpaceId spaceId = H5S.create(H5S.H5SClass.SCALAR);
            H5AttributeId attributeId = H5A.create(target, name, dtype, spaceId);
            H5A.write(attributeId, dtype, new H5Array<byte>(strdata));

            H5A.close(attributeId);
            H5T.close(dtype);
            H5S.close(spaceId);
        }

        private static void WriteAttribute(H5ObjectWithAttributes target, string name, bool value)
        {
            H5DataTypeId dtype = H5T.copy(H5T.H5Type.NATIVE_HBOOL);

            H5DataSpaceId spaceId = H5S.create(H5S.H5SClass.SCALAR);
            H5AttributeId attributeId = H5A.create(target, name, dtype, spaceId);
            H5A.write(attributeId, dtype, new H5Array<bool>(new[] { value }));

            H5A.close(attributeId);
            H5T.close(dtype);
            H5S.close(spaceId);
        }


        private static void WriteAttribute(H5ObjectWithAttributes target, string name, long value)
        {
            H5DataTypeId dtype = H5T.copy(H5T.H5Type.NATIVE_LLONG);

            H5DataSpaceId spaceId = H5S.create(H5S.H5SClass.SCALAR);
            H5AttributeId attributeId = H5A.create(target, name, dtype, spaceId);
            H5A.write(attributeId, dtype, new H5Array<long>(new[] { value }));

            H5A.close(attributeId);
            H5T.close(dtype);
            H5S.close(spaceId);
        }


        private static void WriteAttribute(H5ObjectWithAttributes target, string name, decimal value)
        {
            WriteAttribute(target, name, (double)value);
        }

        private static void WriteAttribute(H5ObjectWithAttributes target, string name, double[] value)
        {
            H5DataTypeId dtype = H5T.copy(H5T.H5Type.NATIVE_DOUBLE);

            H5DataSpaceId spaceId = H5S.create_simple(1, new[] { value.LongCount() });

            H5AttributeId attributeId = H5A.create(target, name, dtype, spaceId);
            H5A.write(attributeId, dtype, new H5Array<double>(value));

            H5A.close(attributeId);
            H5T.close(dtype);
            H5S.close(spaceId);
        }

        private static void WriteAttribute(H5ObjectWithAttributes target, string name, double value)
        {
            WriteAttribute(target, name, new[] {value});
        }


        private void WriteDictionary(H5GroupId parent, string name, IDictionary<string, object> dict)
        {
            if (name == null)
                throw new ArgumentException("Dictionary name may not be null", "name");
            
            H5GroupId dictId = H5G.create(parent, name);
            try
            {
                // Make a local copy of Keys
                var keys = dict.Keys.Select(k => k).ToList();

                foreach (var key in keys)
                {
                    if(!dict.ContainsKey(key))
                    {
                        log.WarnFormat("Dictionary value for key {0} missing during persistence. This likely means someone else modified the dictionary while it was being persisted.", key);
                        continue;
                    }

                    var value = dict[key];

                    if(value == null)
                    {
                        WriteAttribute(dictId, key, "<null>");
                    }
                    else if (value is double[])
                    {
                        WriteAttribute(dictId, key, (double[])value);
                    }
                    else if (value is IMeasurement)
                    {
                        var measurement = value as IMeasurement;
                        WriteAttribute(dictId, key + "_quantity", measurement.Quantity);
                        WriteAttribute(dictId, key + "_units", measurement.DisplayUnit);
                        WriteAttribute(dictId, key, measurement.ToString());
                    }
                    else if (value is decimal)
                    {
                        WriteAttribute(dictId, key, (decimal)value);
                    }
                    else if (value is double)
                    {
                        WriteAttribute(dictId, key, (double)value);
                    }
                    else if (value is long)
                    {
                        WriteAttribute(dictId, key, (long)value);
                    }
                    else if (value is float)
                    {
                        WriteAttribute(dictId, key, (float)value);
                    }
                    else if (value is int)
                    {
                        WriteAttribute(dictId, key, (int)value);
                    }
                    else if (value is short)
                    {
                        WriteAttribute(dictId, key, (short) value);
                    }
                    else if (value is byte)
                    {
                        short tmp = (byte)value;
                        WriteAttribute(dictId, key, tmp);
                    }
                    else if (value is UInt16)
                    {
                        int tmp = (UInt16) value;
                        WriteAttribute(dictId, key, tmp);
                    }
                    else if (value is UInt32)
                    {
                        long tmp = (UInt32) value;
                        WriteAttribute(dictId, key, tmp);
                    }
                    else if (value is bool)
                    {
                        WriteAttribute(dictId, key, (bool)value);
                    }
                    else if (value is string)
                    {
                        WriteAttribute(dictId, key, (string)value);
                    }
                    else
                    {
                        log.WarnFormat("Dictionary value ({0} : {1}) is of usupported type. Falling back to string representation.", key, value);
                        WriteAttribute(dictId, key, String.Format("{0}", value));
                    }
                }
            }
            finally
            {
                H5G.close(dictId);
            }
        }


        private void Write(H5GroupId parent, string name, IMeasurement m)
        {
            H5DataSpaceId spaceId = H5S.create_simple(1, new long[1] { (long)1 });
            H5DataSetId dataSetId = H5D.create(parent, name, measurement_t, spaceId);

            MeasurementT mt = Convert(m);
            H5D.writeScalar<MeasurementT>(dataSetId, measurement_t, ref mt);

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }
        private void Write(H5GroupId parent, string name, IDictionary<IExternalDevice, Measurement> dict)
        {
            H5DataSpaceId spaceId = H5S.create_simple(1, new long[1] { (long)dict.Keys.Count });
            H5DataSetId dataSetId = H5D.create(parent, name, extdevmeasurement_t, spaceId);

            ExtdevMeasurementT[] emts = new ExtdevMeasurementT[dict.Keys.Count];
            int count = 0;

            foreach (var ed in dict.Keys)
            {
                IMeasurement m = dict[ed];

                emts[count] = Convert(ed, m);
                count++;
            }

            H5D.write<ExtdevMeasurementT>(dataSetId, extdevmeasurement_t, new H5Array<ExtdevMeasurementT>(emts));

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }

        private void Write(H5GroupId parent, string name, IDictionary<IExternalDevice, Epoch.EpochBackground> background)
        {
            H5DataSpaceId spaceId = H5S.create_simple(1, new long[1] { (long)background.Keys.Count });
            H5DataSetId dataSetId = H5D.create(parent, name, extdevmeasurement_t, spaceId);

            ExtdevBackgroundMeasurementT[] emts = new ExtdevBackgroundMeasurementT[background.Keys.Count];
            int count = 0;

            foreach (var ed in background.Keys)
            {
                IMeasurement m = background[ed].Background;
                IMeasurement sr = background[ed].SampleRate;

                emts[count] = Convert(ed, m, sr);
                count++;
            }

            H5D.write<ExtdevBackgroundMeasurementT>(dataSetId, extdevmeasurement_t, new H5Array<ExtdevBackgroundMeasurementT>(emts));

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }

        private void Write(H5GroupId parent, string name, IEnumerable<IMeasurement> measurements)
        {
            H5DataSpaceId spaceId = H5S.create_simple(1, new long[1] { (long)measurements.Count() });


            // Set compression options for dataset
            H5PropertyListId dataSetPropertyList = H5P.create(H5P.PropertyListClass.DATASET_CREATE);
            H5P.setDeflate(dataSetPropertyList, NumericDataCompression);
            H5P.setChunk(dataSetPropertyList, new long[] {(long)measurements.Count()});

            H5DataSetId dataSetId = H5D.create(parent, 
                name, 
                measurement_t, 
                spaceId,
                new H5PropertyListId(H5P.Template.DEFAULT), 
                dataSetPropertyList,
                new H5PropertyListId(H5P.Template.DEFAULT));

            MeasurementT[] ms = new MeasurementT[measurements.Count()];
            int ilmCount = 0;
            foreach (IMeasurement m in measurements)
            {
                MeasurementT mt = Convert(m);
                ms[ilmCount++] = mt;
            }

            H5D.write<MeasurementT>(dataSetId, measurement_t, new H5Array<MeasurementT>(ms));

            H5D.close(dataSetId);
            H5S.close(spaceId);
        }
        private void WriteStimuli(H5GroupId parent, string name, IDictionary<IExternalDevice, IStimulus> dict)
        {
            H5GroupId dictId = H5G.create(parent, name);

            foreach (var ed in dict.Keys)
            {
                var s = dict[ed];

                H5GroupId sId = H5G.create(dictId, ed.Name);
                WriteAttribute(sId, "deviceName", ed.Name);
                WriteAttribute(sId, "deviceManufacturer", ed.Manufacturer);
                WriteAttribute(sId, "stimulusID", s.StimulusID);
                WriteAttribute(sId, "stimulusUnits", s.Units);

                WriteDictionary(sId, "parameters", s.Parameters);

                WriteStimulusDataConfigurationSpans(sId, s);

                H5G.close(sId);
            }

            H5G.close(dictId);
        }
        private void WriteResponses(H5GroupId parent, string name, IDictionary<IExternalDevice, Response> dict)
        {
            H5GroupId dictId = H5G.create(parent, name);

            foreach (var ed in dict.Keys)
            {
                Response r = dict[ed];

                H5GroupId rId = H5G.create(dictId, ed.Name);
                WriteAttribute(rId, "deviceName", ed.Name);
                WriteAttribute(rId, "deviceManufacturer", ed.Manufacturer);
                WriteAttribute(rId, "inputTimeUTC", r.InputTime.ToUniversalTime().ToString());
                WriteAttribute(rId, "sampleRate", r.SampleRate.QuantityInBaseUnit);
                WriteAttribute(rId, "sampleRateUnits", r.SampleRate.BaseUnit);
                Write(rId, "data", r.Data);

                WriteResponseDataConfigurationSpans(rId, r);

                H5G.close(rId);
            }

            H5G.close(dictId);
        }

        private const string DATA_CONFIGURATION_SPANS_GROUP = "dataConfigurationSpans";
        private const string DATA_CONFIGURATION_SPAN_PREFIX = "span_";

        private void WriteStimulusDataConfigurationSpans(H5GroupId sId, IStimulus s)
        {
            WriteConfigurationSpans(sId, s.OutputConfigurationSpans);
        }

        private void WriteResponseDataConfigurationSpans(H5GroupId rId, Response r)
        {
            WriteConfigurationSpans(rId, r.DataConfigurationSpans);
        }

        private void WriteConfigurationSpans(H5GroupId gID, IEnumerable<IConfigurationSpan> configurationSpans)
        {

            var spansId = H5G.create(gID, DATA_CONFIGURATION_SPANS_GROUP);

            uint i = 0;
            var totalTime = TimeSpan.Zero;
            foreach (var configSpan in configurationSpans)
            {
                var spanId = H5G.create(spansId, DATA_CONFIGURATION_SPAN_PREFIX + i++);

                WriteAttribute(spanId, "startTimeSeconds", totalTime.TotalSeconds);
                totalTime += configSpan.Time;

                WriteAttribute(spanId, "timeSpanSeconds", configSpan.Time.TotalSeconds);
                foreach (var nodeConfig in configSpan.Nodes)
                {
                    WriteDictionary(spanId, nodeConfig.Name, nodeConfig.Configuration);
                }

            }
        }

        //================================================================
        // These are conversion routines to turn our .NET structures into 
        // their HDF5-persistable version equivalents.
        //
        private KeyValT Convert(string key, string val)
        {
            byte[] keydata = System.Text.Encoding.UTF8.GetBytes(key);
            byte[] valdata = System.Text.Encoding.UTF8.GetBytes(val);

            if (keydata.Length > FIXED_STRING_LENGTH)
                throw new InvalidOperationException("Key is longer than 40 characters.");

            if (valdata.Length > FIXED_STRING_LENGTH)
                throw new InvalidOperationException("Value string is longer than 40 characters.");

            KeyValT kv = new KeyValT();
            unsafe
            {
                for (int i = 0; i < Math.Min(keydata.Length, FIXED_STRING_LENGTH); i++)
                    kv.key[i] = keydata[i];
                for (int i = 0; i < Math.Min(valdata.Length, FIXED_STRING_LENGTH); i++)
                    kv.val[i] = valdata[i];
            }
            return kv;
        }
        private MeasurementT Convert(IMeasurement m)
        {
            MeasurementT mt = new MeasurementT { quantity = (double)m.Quantity };

            byte[] unitdata = System.Text.Encoding.UTF8.GetBytes(m.DisplayUnit);

            if (unitdata.Length > FIXED_STRING_LENGTH)
                throw new InvalidOperationException("BaseUnits string is longer than 40 characters.");

            unsafe
            {
                for (int i = 0; i < Math.Min(unitdata.Length, FIXED_STRING_LENGTH); i++)
                    mt.unit[i] = unitdata[i];
            }
            return mt;
        }

        private ExtdevMeasurementT Convert(IExternalDevice ed, IMeasurement m)
        {
            ExtdevMeasurementT emt = new ExtdevMeasurementT();

            byte[] edNameData = System.Text.Encoding.UTF8.GetBytes(ed.Name);
            if (edNameData.Length > FIXED_STRING_LENGTH)
                throw new InvalidOperationException("External device name is longer than 40 characters.");

            unsafe
            {
                for (int i = 0; i < Math.Min(edNameData.Length, FIXED_STRING_LENGTH); i++)
                    emt.extDeviceName[i] = edNameData[i];
            }
            emt.measurement = Convert(m);

            return emt;
        }
        private ExtdevBackgroundMeasurementT Convert(IExternalDevice ed, IMeasurement bg, IMeasurement sampleRate)
        {
            var result = new ExtdevBackgroundMeasurementT();

            byte[] edNameData = System.Text.Encoding.UTF8.GetBytes(ed.Name);
            if (edNameData.Length > FIXED_STRING_LENGTH)
                throw new InvalidOperationException("External device name is longer than 40 characters.");

            unsafe
            {
                for (int i = 0; i < Math.Min(edNameData.Length, FIXED_STRING_LENGTH); i++)
                    result.extDeviceName[i] = edNameData[i];
            }

            result.measurement = Convert(bg);
            result.sampleRate = Convert(sampleRate);

            return result;
        }
    }
}