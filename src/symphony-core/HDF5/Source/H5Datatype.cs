using HDF.PInvoke;
#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    public class H5Datatype : H5Object
    {
        internal H5Datatype(H5File file, string path) : base(file, path)
        {
        }

        public H5Datatype(hid_t nativeTypeId) : base(null, null)
        {
            _nativeTypeId = nativeTypeId;
        }

        private readonly hid_t _nativeTypeId;

        internal hid_t ToNative()
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
