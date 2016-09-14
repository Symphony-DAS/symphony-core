using System.Collections.Generic;
using System.Text;
using HDF.PInvoke;
using ssize_t = System.IntPtr;

#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    public class H5Group : H5Object
    {
        internal H5Group(H5File file, string path)
            : base(file, path)
        {
        }

        private H5AttributeManager _attributes;

        public H5AttributeManager Attributes
        {
            get { return _attributes ?? (_attributes = new H5AttributeManager(File, Path)); }
        }

        public IEnumerable<H5Group> Groups { get { return GetObjects<H5Group>(); } }

        public H5Group AddGroup(string name)
        {
            string path = Combine(Path, name);
            return File.CreateGroup(path);
        }

        public IEnumerable<H5Datatype> Datatypes { get { return GetObjects<H5Datatype>(); } } 

        public IEnumerable<H5Dataset> Datasets { get { return GetObjects<H5Dataset>(); } } 

        public H5Dataset AddDataset(string name, H5Datatype type, ulong[] dims, ulong[] maxDims = null, ulong[] chunks = null, uint compression = 0)
        {
            string path = Combine(Path, name);
            return File.CreateDataset(path, type, dims, maxDims, chunks, compression);
        }

        public H5Dataset AddDataset<T>(string name, H5Datatype type, T[] data, uint compression = 0)
        {
            string path = Combine(Path, name);
            return File.CreateDataset(path, type, data, compression);
        }

        public H5Link AddHardLink(string name, H5Object obj)
        {
            string path = Combine(Path, name);
            return File.CreateHardLink(path, obj);
        }

        public void Delete()
        {
            File.Delete(Path);
        }

        private IEnumerable<T> GetObjects<T>() where T : H5Object
        {
            var ginfo = new H5G.info_t();
            H5G.get_info_by_name(File.Fid, Path, ref ginfo);
            ulong n = ginfo.nlinks;
            for (ulong i = 0; i < n; i++)
            {
                ssize_t size = H5L.get_name_by_idx(File.Fid, Path, H5.index_t.NAME, H5.iter_order_t.INC, i, null, ssize_t.Zero);

                var buffer = new byte[size.ToInt64() + 1];
                var bufferSize = new ssize_t(size.ToInt64() + 1);

                H5L.get_name_by_idx(File.Fid, Encoding.ASCII.GetBytes(Path), H5.index_t.NAME, H5.iter_order_t.INC, i, buffer, bufferSize);

                string name = Encoding.ASCII.GetString(buffer).TrimEnd((char) 0);
                string fullPath = Combine(Path, name);

                var oinfo = new H5O.info_t();
                H5O.get_info_by_idx(File.Fid, Path, H5.index_t.NAME, H5.iter_order_t.INC, i, ref oinfo);
                H5Object obj = null;
                switch (oinfo.type)
                {
                    case H5O.type_t.DATASET:
                        obj = new H5Dataset(File, fullPath);
                        break;
                    case H5O.type_t.GROUP:
                        obj = new H5Group(File, fullPath);
                        break;
                    case H5O.type_t.NAMED_DATATYPE:
                        obj = new H5Datatype(File, fullPath);
                        break;
                }
                if (obj is T)
                {
                    yield return obj as T;
                }
            }
        }

        private static string Combine(string p1, string p2)
        {
            if (p1.StartsWith("/"))
                return p2.Trim();
            p1 = p1.TrimEnd('/');
            p2 = p2.TrimStart('/');
            return string.Format("{0}/{1}", p1, p2);
        }
    }
}
