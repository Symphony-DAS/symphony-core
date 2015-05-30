using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HDF5DotNet;
using log4net;

namespace Symphony.Core
{
    public class H5Document : IDocument
    {
        public IExperimentData Experiment { get; protected set; }

        private readonly H5FileId fileId;

        private readonly ILog log = LogManager.GetLogger(typeof(H5Document));

        public H5Document(String filePath)
        {
            var currentFile = new FileInfo(filePath);
            if (currentFile.Exists)
            {
                fileId = H5F.open(filePath, H5F.OpenMode.ACC_RDWR);
            }
            else
            {
                fileId = H5F.create(filePath, H5F.CreateMode.ACC_EXCL);
            }
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
                    return H5T.copy(H5T.H5Type.NATIVE_LONG);
                case TypeCode.UInt64:
                    return H5T.copy(H5T.H5Type.NATIVE_ULONG);
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
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_LONG)))
                return typeof(long);
            if (H5T.equal(nativeType, new H5DataTypeId(H5T.H5Type.NATIVE_ULONG)))
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

            public void AddNote(string text)
            {
                throw new NotImplementedException();
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
