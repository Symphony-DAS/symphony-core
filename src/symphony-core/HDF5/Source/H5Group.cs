using System.Collections.Generic;
using HDF5DotNet;

namespace HDF5
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

        public H5Dataset AddDataset(string name, H5Datatype type, long[] dims, long[] maxDims = null, long[] chunks = null, uint compression = 0)
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
            H5GInfo ginfo = H5G.getInfoByName(File.Fid, Path);
            int n = (int)ginfo.nLinks;
            for (int i = 0; i < n; i++)
            {
                string name = H5L.getNameByIndex(File.Fid, Path, H5IndexType.NAME, H5IterationOrder.INCREASING, i);
                string fullPath = Combine(Path, name);
                H5ObjectInfo oinfo = H5O.getInfoByIndex(File.Fid, Path, H5IndexType.NAME, H5IterationOrder.INCREASING, i);
                H5Object obj = null;
                switch (oinfo.objectType)
                {
                    case H5ObjectType.DATASET:
                        obj = new H5Dataset(File, fullPath);
                        break;
                    case H5ObjectType.GROUP:
                        obj = new H5Group(File, fullPath);
                        break;
                    case H5ObjectType.NAMED_DATATYPE:
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
