using System;
using HDF5DotNet;

namespace HDF5
{
    internal static class H5Ox
    {
        public static H5ObjectWithAttributes open(H5FileId id, string path)
        {
            H5ObjectInfo oinfo = H5O.getInfoByName(id, path);
            switch (oinfo.objectType)
            {
                case H5ObjectType.DATASET:
                    return H5D.open(id, path);
                case H5ObjectType.GROUP:
                    return H5G.open(id, path);
                default:
                    throw new ArgumentException();
            }
        }

        public static void close(H5ObjectWithAttributes oid)
        {
            if (oid is H5DataSetId)
            {
                H5D.close(oid as H5DataSetId);
            }
            else if (oid is H5GroupId)
            {
                H5G.close(oid as H5GroupId);
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }

    internal static class H5Tx
    {
        public static H5DataTypeId getNativeType(Type systemType)
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

        public static Type getSystemType(H5DataTypeId nativeType)
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
    }
}
