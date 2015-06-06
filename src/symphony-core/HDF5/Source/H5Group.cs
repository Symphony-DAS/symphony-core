using System.Collections.Generic;
using HDF5DotNet;

namespace HDF5
{
    public class H5Group : H5ObjectWithMetadata
    {
        internal H5Group(H5File file, string path)
            : base(file, path)
        {
        }

        public IEnumerable<H5Group> Groups { get { return GetObjects<H5Group>(); } }

        public H5Group AddGroup(string name)
        {
            string path = string.Format("{0}/{1}", Path.TrimEnd('/'), name);
            File.CreateGroup(path);
            return new H5Group(File, path);
        }

        private IEnumerable<T> GetObjects<T>() where T : H5Object
        {
            H5GInfo ginfo = H5G.getInfoByName(File.Fid, Path);
            int n = (int)ginfo.nLinks;
            for (int i = 0; i < n; i++)
            {
                string name = H5L.getNameByIndex(File.Fid, Path, H5IndexType.NAME, H5IterationOrder.INCREASING, i);
                string fullPath = Path + "/" + name;
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
                        obj = new H5Dataset(File, fullPath);
                        break;
                }
                if (obj is T)
                {
                    yield return obj as T;
                }
            }
        }
    }
}
