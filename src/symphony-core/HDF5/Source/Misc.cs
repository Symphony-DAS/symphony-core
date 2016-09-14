using System;
using HDF.PInvoke;
#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    // "Extensions" for H5T
    internal static class H5Tx
    {
        public static hid_t getNativeType(Type systemType)
        {
            switch (Type.GetTypeCode(systemType))
            {
                case TypeCode.Byte:
                    return H5T.copy(H5T.NATIVE_UCHAR);
                case TypeCode.SByte:
                    return H5T.copy(H5T.NATIVE_SCHAR);
                case TypeCode.Int16:
                    return H5T.copy(H5T.NATIVE_SHORT);
                case TypeCode.UInt16:
                    return H5T.copy(H5T.NATIVE_USHORT);
                case TypeCode.Int32:
                    return H5T.copy(H5T.NATIVE_INT);
                case TypeCode.UInt32:
                    return H5T.copy(H5T.NATIVE_UINT);
                case TypeCode.Int64:
                    return H5T.copy(H5T.NATIVE_LLONG);
                case TypeCode.UInt64:
                    return H5T.copy(H5T.NATIVE_ULLONG);
                case TypeCode.Char:
                    return H5T.copy(H5T.NATIVE_USHORT);
                case TypeCode.Single:
                    return H5T.copy(H5T.NATIVE_FLOAT);
                case TypeCode.Double:
                    return H5T.copy(H5T.NATIVE_DOUBLE);
                case TypeCode.Boolean:
                    return H5T.copy(H5T.NATIVE_UCHAR);
                default:
                    throw new NotSupportedException("Unsupported system type");
            }
        }

        public static Type getSystemType(hid_t nativeType)
        {
            if (H5T.equal(nativeType, H5T.NATIVE_UCHAR) > 0)
                return typeof(byte);
            if (H5T.equal(nativeType, H5T.NATIVE_SCHAR) > 0)
                return typeof(sbyte);
            if (H5T.equal(nativeType, H5T.NATIVE_SHORT) > 0)
                return typeof(short);
            if (H5T.equal(nativeType, H5T.NATIVE_USHORT) > 0)
                return typeof(ushort);
            if (H5T.equal(nativeType, H5T.NATIVE_INT) > 0)
                return typeof(int);
            if (H5T.equal(nativeType, H5T.NATIVE_UINT) > 0)
                return typeof(uint);
            if (H5T.equal(nativeType, H5T.NATIVE_LLONG) > 0)
                return typeof(long);
            if (H5T.equal(nativeType, H5T.NATIVE_ULLONG) > 0)
                return typeof(ulong);
            if (H5T.equal(nativeType, H5T.NATIVE_USHORT) > 0)
                return typeof(char);
            if (H5T.equal(nativeType, H5T.NATIVE_FLOAT) > 0)
                return typeof(float);
            if (H5T.equal(nativeType, H5T.NATIVE_DOUBLE) > 0)
                return typeof(double);
            if (H5T.equal(nativeType, H5T.NATIVE_UCHAR) > 0)
                return typeof(bool);
            throw new NotSupportedException("Unsupported native type");
        }
    }
}
