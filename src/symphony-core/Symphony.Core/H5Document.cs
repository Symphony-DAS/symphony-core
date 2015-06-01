using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF5DotNet;
using log4net;

namespace Symphony.Core
{
    public class H5Document : IDocument
    {
        protected const uint _persistenceVersion = 2;

        public IExperimentData Experiment { get; protected set; }

        public readonly H5FileId fileId;

        private readonly ILog log = LogManager.GetLogger(typeof(H5Document));

        private const int FIXED_STRING_LENGTH = 40;

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
            public fixed byte text[FIXED_STRING_LENGTH];
        }

        readonly H5DataTypeId string_t;
        readonly H5DataTypeId datetimeoffset_t;
        readonly H5DataTypeId note_t;

        public H5Document(String filePath)
        {
            var currentFile = new FileInfo(filePath);
            if (currentFile.Exists)
            {
                fileId = H5F.open(filePath, H5F.OpenMode.ACC_RDWR);

                string_t = H5T.open(fileId, "STRING40");
                datetimeoffset_t = H5T.open(fileId, "DATETIMEOFFSET");
                note_t = H5T.open(fileId, "NOTE");

                //TODO Check persistence version
            }
            else
            {
                fileId = H5F.create(filePath, H5F.CreateMode.ACC_EXCL);
                
                WriteAttribute(fileId, "version", Version);

                // Create our standard String type (string of length FIXED_STRING_LENGTH characters)
                string_t = H5T.copy(H5T.H5Type.C_S1);
                H5T.setSize(string_t, FIXED_STRING_LENGTH);
                H5T.commit(fileId, "STRING40", string_t);

                // Create the DateTimeOffset compound type
                datetimeoffset_t = H5T.create(H5T.CreateClass.COMPOUND, Marshal.SizeOf(typeof(DateTimeOffsetT)));
                H5T.insert(datetimeoffset_t, "utcTicks", 0, H5T.H5Type.NATIVE_LLONG);
                H5T.insert(datetimeoffset_t, "offsetHours", H5T.getSize(H5T.H5Type.NATIVE_LLONG), H5T.H5Type.NATIVE_DOUBLE);
                H5T.commit(fileId, "DATETIMEOFFSET", datetimeoffset_t);

                // Create the Note compound type
                note_t = H5T.create(H5T.CreateClass.COMPOUND, Marshal.SizeOf(typeof(NoteT)));
                H5T.insert(note_t, "time", 0, datetimeoffset_t);
                H5T.insert(note_t, "text", H5T.getSize(datetimeoffset_t), string_t);
                H5T.commit(fileId, "NOTE", note_t);

                H5DataSpaceId spaceId = H5S.create_simple(1, new long[] {0}, new long[] {-1});
                H5PropertyListId propertyListId = H5P.create(H5P.PropertyListClass.DATASET_CREATE);
                H5P.setChunk(propertyListId, new long[] {1});
                H5DataSetId setId = H5D.create(fileId, "notes", note_t, spaceId, new H5PropertyListId(H5P.Template.DEFAULT), propertyListId, new H5PropertyListId(H5P.Template.DEFAULT));
                
                H5D.close(setId);
                H5P.close(propertyListId);
                H5S.close(spaceId);
            }
        }

        public void Close()
        {
            H5F.close(fileId);
        }

        public static uint Version
        {
            get { return _persistenceVersion; }
        }

        public IEnumerable<Note> ReadNotes(H5DataSetId dataSetId)
        {
            H5DataSpaceId spaceId = H5D.getSpace(dataSetId);
            int nNotes = H5S.getSimpleExtentNPoints(spaceId);

            var data = new NoteT[nNotes];
            H5D.read(dataSetId, note_t, new H5Array<NoteT>(data));

            var notes = new List<Note>(nNotes);
            for (int i = 0; i < nNotes; i++)
            {
                Note n = Convert(data[i]);
                notes.Add(n);
            }

            H5S.close(spaceId);

            return notes;
        }

        public void WriteNote(H5DataSetId dataSetId, Note note)
        {
            H5DataSpaceId spaceId = H5D.getSpace(dataSetId);
            int nNotes = H5S.getSimpleExtentNPoints(spaceId);

            H5D.setExtent(dataSetId, new long[] {nNotes + 1});

            H5DataSpaceId fileSpaceId = H5D.getSpace(dataSetId);
            H5S.selectHyperslab(fileSpaceId, H5S.SelectOperator.SET, new long[] {nNotes}, new long[] {1});
            H5DataSpaceId memSpaceId = H5S.create_simple(1, new long[] {1});

            NoteT nt = Convert(note);
            H5D.writeScalar(dataSetId, note_t, memSpaceId, fileSpaceId, new H5PropertyListId(H5P.Template.DEFAULT), ref nt);

            H5S.close(memSpaceId);
            H5S.close(fileSpaceId);
            H5S.close(spaceId);
        }

        public static IDictionary<string, object> ReadAttributes(H5ObjectWithAttributes target)
        {
            H5ObjectInfo info = H5O.getInfo(target);

            int n = (int) info.nAttributes;
            var attributes = new Dictionary<string, object>(n);

            for (int i = 0; i < n; i++)
            {
                H5AttributeId attributeId = H5A.openByIndex(target, ".", H5IndexType.NAME, H5IterationOrder.INCREASING, i);
                
                string name = H5A.getName(attributeId);
                object value = ReadAttribute(target, name);

                attributes.Add(name, value);

                H5A.close(attributeId);
            }

            return attributes;
        }

        public static object ReadAttribute(H5ObjectWithAttributes target, string name)
        {
            H5AttributeId attributeId = H5A.open(target, name);

            H5DataTypeId tempId = H5A.getType(attributeId);
            H5DataTypeId typeId = H5T.getNativeType(tempId, H5T.Direction.DEFAULT);
            H5T.close(tempId);

            object value;
            if (H5T.getClass(typeId) == H5T.H5TClass.STRING)
            {
                var buffer = new byte[H5T.getSize(typeId)];
                H5A.read(attributeId, typeId, new H5Array<byte>(buffer));
                value = Encoding.ASCII.GetString(buffer);
            }
            else
            {
                Type elementType = GetSystemType(typeId);

                H5DataSpaceId spaceId = H5A.getSpace(attributeId);
                long[] dims = H5S.getSimpleExtentDims(spaceId);
                H5S.close(spaceId);

                Array data = Array.CreateInstance(elementType, dims.Any() ? dims : new long[] { 1 });

                //H5Array<type> buffer = new H5Array<type>(data);
                var bufferType = typeof(H5Array<>).MakeGenericType(new[] { elementType });
                var buffer = Activator.CreateInstance(bufferType, new object[] { data });

                //H5A.read(attributeId, typeId, buffer);
                var methodInfo = typeof(H5A).GetMethod("read").MakeGenericMethod(new[] { elementType });
                methodInfo.Invoke(null, new[] { attributeId, typeId, buffer });

                value = dims.Any() ? data : data.GetValue(0);
            }

            H5T.close(typeId);
            H5A.close(attributeId);

            return value;
        }

        public static void WriteAttribute(H5ObjectWithAttributes target, string name, object value)
        {
            H5DataTypeId typeId;
            H5DataSpaceId spaceId;
            H5AttributeId attributeId;

            if (value is string)
            {
                typeId = H5T.copy(H5T.H5Type.C_S1);
                H5T.setSize(typeId, ((string) value).Length);
                spaceId = H5S.create(H5S.H5SClass.SCALAR);
                attributeId = H5A.create(target, name, typeId, spaceId);
                H5A.write(attributeId, typeId, new H5Array<byte>(Encoding.ASCII.GetBytes((string) value)));
            }
            else
            {
                Type valueType = value.GetType();
                Type elementType = valueType.IsArray ? valueType.GetElementType() : valueType;
                typeId = GetNativeType(elementType);

                Array data;
                if (valueType.IsArray)
                {
                    int rank = ((Array)value).Rank;
                    var dims = new long[rank];
                    for (int i = 0; i < rank; i++)
                    {
                        dims[i] = ((Array)value).GetLength(i);
                    }
                    spaceId = H5S.create_simple(rank, dims);
                    data = (Array)value;
                }
                else
                {
                    spaceId = H5S.create(H5S.H5SClass.SCALAR);
                    data = Array.CreateInstance(elementType, 1);
                    data.SetValue(value, 0);
                }

                attributeId = H5A.create(target, name, typeId, spaceId);

                //H5Array<elementType> buffer = new H5Array<elementType>(data);
                var bufferType = typeof(H5Array<>).MakeGenericType(new[] { elementType });
                var buffer = Activator.CreateInstance(bufferType, new object[] { data });

                //H5A.write(attributeId, typeId, buffer);
                var methodInfo = typeof(H5A).GetMethod("write").MakeGenericMethod(new[] { elementType });
                methodInfo.Invoke(null, new[] { attributeId, typeId, buffer });
            }

            H5T.close(typeId);
            H5S.close(spaceId);
            H5A.close(attributeId);
        }

        private static H5DataTypeId GetNativeType(Type systemType)
        {
            switch (Type.GetTypeCode(systemType))
            {
                case TypeCode.Byte:
                    return H5T.copy(H5T.H5Type.NATIVE_UCHAR);
                case TypeCode.SByte:
                    return H5T.copy(H5T.H5Type.NATIVE_SCHAR);
                case TypeCode.Int16:
                    return H5T.copy(H5T.H5Type.NATIVE_SHORT);
                case TypeCode.UInt16:
                    return H5T.copy(H5T.H5Type.NATIVE_USHORT);
                case TypeCode.Int32:
                    return H5T.copy(H5T.H5Type.NATIVE_INT);
                case TypeCode.UInt32:
                    return H5T.copy(H5T.H5Type.NATIVE_UINT);
                case TypeCode.Int64:
                    return H5T.copy(H5T.H5Type.NATIVE_LLONG);
                case TypeCode.UInt64:
                    return H5T.copy(H5T.H5Type.NATIVE_ULLONG);
                case TypeCode.Char:
                    return H5T.copy(H5T.H5Type.NATIVE_USHORT);
                case TypeCode.Single:
                    return H5T.copy(H5T.H5Type.NATIVE_FLOAT);
                case TypeCode.Double:
                    return H5T.copy(H5T.H5Type.NATIVE_DOUBLE);
                case TypeCode.Boolean:
                    return H5T.copy(H5T.H5Type.NATIVE_UCHAR);
                default:
                    throw new ArgumentException("Unsupported system type");
            }
        }

        private static Type GetSystemType(H5DataTypeId nativeType)
        {
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_UCHAR)))
                return typeof(byte);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_SCHAR)))
                return typeof(sbyte);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_SHORT)))
                return typeof(short);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_USHORT)))
                return typeof(ushort);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_INT)))
                return typeof(int);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_UINT)))
                return typeof(uint);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_LLONG)))
                return typeof(long);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_ULLONG)))
                return typeof(ulong);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_USHORT)))
                return typeof(char);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_FLOAT)))
                return typeof(float);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_DOUBLE)))
                return typeof(double);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_UCHAR)))
                return typeof(bool);
            throw new ArgumentException("Unsupported native type");
        }

        private Note Convert(NoteT nt)
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

        private NoteT Convert(Note n)
        {
            NoteT nt = new NoteT
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

        private class H5DataObject : IDataObject
        {
            protected H5Document Document;

            protected string Path;

            private IDictionary<string, object> properties; 

            public IDictionary<string, object> Properties
            {
                get
                {
                    if (properties == null)
                    {
                        H5GroupId gid = H5G.open(Document.fileId, Path + "/properties");
                        properties = ReadAttributes(gid);
                        H5G.close(gid);
                    }
                    return properties;
                }
            }

            private IDictionary<string, object> attributes; 

            protected IDictionary<string, object> Attributes
            {
                get
                {
                    if (attributes == null)
                    {
                        H5GroupId gid = H5G.open(Document.fileId, Path);
                        attributes = ReadAttributes(gid);
                        H5G.close(gid);
                    }
                    return attributes;
                }
            }

            protected H5DataObject(H5Document document, string path)
            {
                Document = document;
                Path = path;
            }

            public void AddProperty(string key, object value)
            {
                H5GroupId gid = H5G.open(Document.fileId, Path + "/properties");
                WriteAttribute(gid, key, value);
                H5G.close(gid);
            }

            public IEnumerable<Note> Notes { get; private set; }

            public void AddNote(string text)
            {
                H5DataSetId did = H5D.open(Document.fileId, Path + "/notes");
                //WriteNote(did, null);
                H5D.close(did);
            }
        }

        private class H5ExperimentData : H5DataObject, IExperimentData
        {
            private const string PurposeName = "purpose";
            private const string StartTimeUtcName = "startTimeDotNetDateTimeOffsetUTCTicks";
            private const string StartTimeOffsetName = "startTimeUTCOffsetHours";

            public H5ExperimentData(H5Document document, string path) : base(document, path)
            {
            }

            public string Purpose
            {
                get { return (string)Attributes[PurposeName]; }
            }

            public DateTimeOffset StartTime
            {
                get
                {
                    long ticks = (long)Attributes[StartTimeUtcName];
                    double offset = (double)Attributes[StartTimeOffsetName];
                    return new DateTimeOffset(ticks, TimeSpan.FromHours(offset));
                }
            }

        }

    }
}
