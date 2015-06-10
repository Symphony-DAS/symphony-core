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
            Attributes = new H5AttributeManager(file, path);
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

        public H5AttributeManager Attributes { get; private set; }
    }
}
