using System.Runtime.InteropServices;
using HDF.PInvoke;

using hsize_t = System.UInt64;

#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    public class H5Dataset : H5Object
    {
        internal H5Dataset(H5File file, string path)
            : base(file, path)
        {
        }

        private H5AttributeManager _attributes;

        public H5AttributeManager Attributes 
        { 
            get { return _attributes ?? (_attributes = new H5AttributeManager(File, Path)); }
        }

        public long NumberOfElements
        {
            get
            {
                hid_t did, sid;
                did = sid = -1;
                try
                {
                    did = H5D.open(File.Fid, Path);
                    sid = H5D.get_space(did);
                    return H5S.get_simple_extent_npoints(sid);
                }
                finally
                {
                    if (sid > 0)
                        H5S.close(sid);
                    if (did > 0)
                        H5D.close(did);
                }
            }
        }

        public void SetData<T>(T[] data)
        {
            hid_t did, tid;
            did = tid = -1;
            try
            {
                did = H5D.open(File.Fid, Path);
                tid = H5D.get_type(did);

                GCHandle pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
                H5D.write(did, tid, H5S.ALL, H5S.ALL, H5P.DEFAULT, pinnedData.AddrOfPinnedObject());
                pinnedData.Free();
            }
            finally
            {
                if (tid > 0)
                    H5T.close(tid);
                if (did > 0)
                    H5D.close(did);
            }
        }

        public void SetData<T>(T[] data, ulong[] start, ulong[] count)
        {
            hid_t did, sid, mid, tid;
            did = sid = mid = tid = -1;
            try
            {
                did = H5D.open(File.Fid, Path);
                tid = H5D.get_type(did);
                sid = H5D.get_space(did);
                H5S.select_hyperslab(sid, H5S.seloper_t.SET, start, null, count, null); //H5S.SelectOperator.SET, start, count);
                mid = H5S.create_simple(1, count, null);

                GCHandle pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
                H5D.write(did, tid, mid, sid, H5P.DEFAULT, pinnedData.AddrOfPinnedObject());
                pinnedData.Free();
            }
            finally
            {
                if (tid > 0)
                    H5T.close(tid);
                if (mid > 0)
                    H5S.close(mid);
                if (sid > 0)
                    H5S.close(sid);
                if (did > 0)
                    H5D.close(did);
            }
        }

        public T[] GetData<T>()
        {
            hid_t did, tid, sid;
            did = tid = sid = -1;
            try
            {
                did = H5D.open(File.Fid, Path);
                tid = H5D.get_type(did);
                sid = H5D.get_space(did);
                long npoints = H5S.get_simple_extent_npoints(sid);
                var data = new T[npoints];
                if (npoints > 0)
                {
                    GCHandle pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
                    H5D.read(did, tid, H5S.ALL, H5S.ALL, H5P.DEFAULT, pinnedData.AddrOfPinnedObject());
                    pinnedData.Free();
                }
                return data;
            }
            finally
            {
                if (sid > 0)
                    H5S.close(sid);
                if (tid > 0)
                    H5T.close(tid);
                if (did > 0)
                    H5D.close(did);
            }
        }

        public void Extend(ulong[] newDims)
        {
            hid_t did = -1;
            try
            {
                did = H5D.open(File.Fid, Path);
                H5D.set_extent(did, newDims);
            }
            finally
            {
                if (did > 0)
                    H5D.close(did);
            }
        }
    }
}
