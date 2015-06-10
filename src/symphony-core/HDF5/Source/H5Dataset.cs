using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDF5DotNet;

namespace HDF5
{
    public class H5Dataset : H5Object
    {
        internal H5Dataset(H5File file, string path)
            : base(file, path)
        {
            Attributes = new H5AttributeManager(file, path);
        }

        public H5AttributeManager Attributes { get; private set; }

        public int NumberOfElements
        {
            get
            {
                H5DataSetId did = null;
                H5DataSpaceId sid = null;
                try
                {
                    did = H5D.open(File.Fid, Path);
                    sid = H5D.getSpace(did);
                    return H5S.getSimpleExtentNPoints(sid);
                }
                finally
                {
                    if (sid != null && sid.Id > 0)
                        H5S.close(sid);
                    if (did != null && did.Id > 0)
                        H5D.close(did);
                }
            }
        }

        public void SetData<T>(T[] data)
        {
            H5DataSetId did = null;
            H5DataTypeId tid = null;
            try
            {
                did = H5D.open(File.Fid, Path);
                tid = H5D.getType(did);
                H5D.write(did, tid, new H5Array<T>(data));
            }
            finally
            {
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
                if (did != null && did.Id > 0)
                    H5D.close(did);
            }
        }

        public void SetData<T>(T[] data, long[] start, long[] count)
        {
            H5DataSetId did = null;
            H5DataSpaceId sid = null;
            H5DataSpaceId mid = null;
            H5DataTypeId tid = null;
            try
            {
                did = H5D.open(File.Fid, Path);
                tid = H5D.getType(did);
                sid = H5D.getSpace(did);
                H5S.selectHyperslab(sid, H5S.SelectOperator.SET, start, count);
                mid = H5S.create_simple(1, count);
                H5D.write(did, tid, mid, sid, new H5PropertyListId(H5P.Template.DEFAULT), new H5Array<T>(data));
            }
            finally
            {
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
                if (mid != null && mid.Id > 0)
                    H5S.close(mid);
                if (sid != null && sid.Id > 0)
                    H5S.close(sid);
                if (did != null && did.Id > 0)
                    H5D.close(did);
            }
        }

        public T[] GetData<T>()
        {
            H5DataSetId did = null;
            H5DataTypeId tid = null;
            H5DataSpaceId sid = null;
            try
            {
                did = H5D.open(File.Fid, Path);
                tid = H5D.getType(did);
                sid = H5D.getSpace(did);
                int npoints = H5S.getSimpleExtentNPoints(sid);
                var data = new T[npoints];
                if (npoints > 0)
                {
                    H5D.read(did, tid, new H5Array<T>(data));
                }
                return data;
            }
            finally
            {
                if (sid != null && sid.Id > 0)
                    H5S.close(sid);
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
                if (did != null && did.Id > 0)
                    H5D.close(did);
            }
        }

        public void Extend(long[] newDims)
        {
            H5DataSetId did = null;
            try
            {
                did = H5D.open(File.Fid, Path);
                H5D.setExtent(did, newDims);
            }
            finally
            {
                if (did != null && did.Id > 0)
                    H5D.close(did);
            }
        }
    }
}
