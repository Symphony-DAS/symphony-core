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
            _nativeTypeId = nativeTypeId;
        }

        private readonly H5T.H5Type _nativeTypeId;

        internal H5DataTypeId ToNative()
        {
            return File != null ? H5T.open(File.Fid, Path) : H5T.copy(_nativeTypeId);
        }

        private H5AttributeManager _attributes;

        public H5AttributeManager Attributes
        {
            get { return _attributes ?? (_attributes = new H5AttributeManager(File, Path)); }
        }
    }
}
