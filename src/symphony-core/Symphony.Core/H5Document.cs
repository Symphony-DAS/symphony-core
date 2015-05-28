using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HDF5DotNet;

namespace Symphony.Core
{
    public class H5Document : IDocument
    {
        public IExperimentData Experiment { get; protected set; }

        private readonly H5FileId _fileId;

        public H5Document(String filePath)
        {
            var currentFile = new FileInfo(filePath);
            if (currentFile.Exists)
            {
                _fileId = H5F.open(filePath, H5F.OpenMode.ACC_RDWR);
            }
            else
            {
                _fileId = H5F.create(filePath, H5F.CreateMode.ACC_EXCL);
            }
        }

        private static void WriteBooleanAttribute(H5ObjectWithAttributes target, string name, bool value)
        {
            H5DataTypeId typeId = H5T.copy(H5T.H5Type.NATIVE_HBOOL);
            H5DataSpaceId spaceId = H5S.create(H5S.H5SClass.SCALAR);
            H5AttributeId attributeId = H5A.create(target, name, typeId, spaceId);
            H5A.write(attributeId, typeId, new H5Array<bool>(new[] { value }));

            H5T.close(typeId);
            H5S.close(spaceId);
            H5A.close(attributeId);
        }

        private static bool ReadBooleanAttribute(H5ObjectWithAttributes target, string name)
        {
            H5DataTypeId typeId = H5T.copy(H5T.H5Type.NATIVE_HBOOL);
            H5AttributeId attributeId = H5A.open(target, name);
            var buffer = new bool[1];
            H5A.read(attributeId, typeId, new H5Array<bool>(buffer));

            H5T.close(typeId);
            H5A.close(attributeId);

            return buffer[0];
        }

        private static void WriteLongAttribute(H5ObjectWithAttributes target, string name, long value)
        {
            H5DataTypeId typeId = H5T.copy(H5T.H5Type.NATIVE_LLONG);
            H5DataSpaceId spaceId = H5S.create(H5S.H5SClass.SCALAR);
            H5AttributeId attributeId = H5A.create(target, name, typeId, spaceId);
            H5A.write(attributeId, typeId, new H5Array<long>(new[] { value }));

            H5T.close(typeId);
            H5S.close(spaceId);
            H5A.close(attributeId);
        }

        private static long ReadLongAttribute(H5ObjectWithAttributes target, string name)
        {
            H5DataTypeId typeId = H5T.copy(H5T.H5Type.NATIVE_LLONG);
            H5AttributeId attributeId = H5A.open(target, name);
            var buffer = new long[1];
            H5A.read(attributeId, typeId, new H5Array<long>(buffer));

            H5T.close(typeId);
            H5A.close(attributeId);

            return buffer[0];
        }

        private static void WriteDoubleArrayAttribute(H5ObjectWithAttributes target, string name, double[] value)
        {
            H5DataTypeId typeId = H5T.copy(H5T.H5Type.NATIVE_DOUBLE);
            H5DataSpaceId spaceId = H5S.create_simple(1, new[] { value.LongCount() });
            H5AttributeId attributeId = H5A.create(target, name, typeId, spaceId);
            H5A.write(attributeId, typeId, new H5Array<double>(value));

            H5T.close(typeId);
            H5S.close(spaceId);
            H5A.close(attributeId);
        }

        private static double[] ReadDoubleArrayAttribute(H5ObjectWithAttributes target, string name)
        {
            H5AttributeId attributeId = H5A.open(target, name);
            H5DataSpaceId spaceId = H5A.getSpace(attributeId);
            H5DataTypeId typeId = H5A.getType(attributeId);
            var buffer = new double[H5S.getSimpleExtentNPoints(spaceId)];
            H5A.read(attributeId, typeId, new H5Array<double>(buffer));

            H5T.close(typeId);
            H5S.close(spaceId);
            H5A.close(attributeId);

            return buffer;
        }

        private static void WriteDoubleAttribute(H5ObjectWithAttributes target, string name, double value)
        {
            WriteDoubleArrayAttribute(target, name, new[] { value });
        }

        private static double ReadDoubleAttribute(H5ObjectWithAttributes target, string name)
        {
            return ReadDoubleArrayAttribute(target, name)[0];
        }

        private static void WriteStringAttribute(H5ObjectWithAttributes target, string name, string value)
        {
            H5DataTypeId typeId = H5T.copy(H5T.H5Type.C_S1);
            H5T.setSize(typeId, value.Length);
            H5DataSpaceId spaceId = H5S.create(H5S.H5SClass.SCALAR);
            H5AttributeId attributeId = H5A.create(target, name, typeId, spaceId);
            var encoding = new ASCIIEncoding();
            H5A.write(attributeId, typeId, new H5Array<byte>(encoding.GetBytes(value)));

            H5T.close(typeId);
            H5S.close(spaceId);
            H5A.close(attributeId);
        }

        private static string ReadStringAttribute(H5ObjectWithAttributes target, string name)
        {
            H5AttributeId attributeId = H5A.open(target, name);
            H5DataTypeId typeId = H5A.getType(attributeId);
            H5DataTypeId nativeTypeId = H5T.getNativeType(typeId, H5T.Direction.ASCEND);
            var buffer = new byte[H5T.getSize(nativeTypeId)];
            H5A.read(attributeId, nativeTypeId, new H5Array<byte>(buffer));

            H5T.close(nativeTypeId);
            H5T.close(typeId);
            H5A.close(attributeId);

            var encoding = new ASCIIEncoding();
            return encoding.GetString(buffer);
        }

        private class H5DataObject : IDataObject
        {
            public IDictionary<string, object> Properties
            {
                get { return new Dictionary<string, object>(); }
            }

            protected IDictionary<string, object> Attributes
            {
                get { return new Dictionary<string, object>(); }
            }

            public void AddProperty(string key, object value)
            {
                throw new NotImplementedException();
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
