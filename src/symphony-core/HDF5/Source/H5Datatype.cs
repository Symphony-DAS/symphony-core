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

        private H5AttributeManager attributes;

        public H5AttributeManager Attributes
        {
            get { return attributes ?? (attributes = new H5AttributeManager(File, Path)); }
        }
    }
}
