using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDF5DotNet;

namespace HDF5
{
    public class H5Datatype : H5Object
    {
        internal H5Datatype(H5File file, string path) : base(file, path)
        {
        }

        public H5Datatype(H5T.H5Type nativeTypeId) : base(null, null)
        {
            this.nativeTypeId = nativeTypeId;
        }

        private readonly H5T.H5Type nativeTypeId;

        internal H5DataTypeId ToNative()
        {
            return File != null ? H5T.open(File.Fid, Path) : H5T.copy(nativeTypeId);
        }

        //internal static H5DataTypeId ToNative(H5T.H5TClass typeClass, int typeSize)
        //{
        //    H5DataTypeId tid = null;
        //    switch (typeClass)
        //    {
        //        case H5T.H5TClass.INTEGER:
        //            if (typeSize == 1)
        //                tid = H5T.copy(H5T.H5Type.NATIVE_SCHAR);
        //            else if (typeSize == 2)
        //                tid = H5T.copy(H5T.H5Type.NATIVE_SHORT);
        //            else if (typeSize == 4)
        //                tid = H5T.copy(H5T.H5Type.NATIVE_INT);
        //            else if (typeSize == 8)
        //                tid = H5T.copy(H5T.H5Type.NATIVE_LLONG);
        //            break;
        //        case H5T.H5TClass.FLOAT:
        //            break;

        //    }
        //}
    }
}
