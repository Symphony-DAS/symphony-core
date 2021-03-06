﻿using System;
using HDF5DotNet;

namespace HDF5
{
    public class H5File : H5Group, IDisposable
    {
        public H5FileId Fid { get; private set; }

        public H5File(string filename) : base(null, "/")
        {
            if (System.IO.File.Exists(filename))
            {
                Fid = H5F.open(filename, H5F.OpenMode.ACC_RDWR);
            }
            else
            {
                Fid = H5F.create(filename, H5F.CreateMode.ACC_EXCL);
            }
            File = this;
        }

        ~H5File()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed = false;

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            Close();
            _disposed = true;
        }

        public void Close()
        {
            if (Fid == null || Fid.Id <= 0)
                return;
            H5F.close(Fid);
            Fid = null;
        }

        public void Delete(string path)
        {
            H5L.Delete(Fid, path);
        }

        public H5Group CreateGroup(string path)
        {
            H5GroupId gid = null;
            try
            {
                gid = H5G.create(Fid, path);
            }
            finally
            {
                if (gid != null && gid.Id > 0)
                    H5G.close(gid);
            }
            return new H5Group(this, path);
        }

        public H5Datatype CreateDatatype(string name, H5T.H5TClass typeClass, int typeSize)
        {
            H5DataTypeId tid = null;
            try
            {
                switch (typeClass)
                {
                    case H5T.H5TClass.STRING:
                        tid = H5T.copy(H5T.H5Type.C_S1);
                        H5T.setSize(tid, typeSize);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                H5T.commit(Fid, name, tid);
            }
            finally
            {
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
            }
            return new H5Datatype(this, name);
        }

        public H5Datatype CreateDatatype(string name, string[] memberNames, H5Datatype[] memberTypes)
        {
            int nMembers = memberNames.Length;
            var mTypes = new H5DataTypeId[nMembers];

            H5DataTypeId tid = null;
            try
            {
                int typeSize = 0;
                for (int i = 0; i < nMembers; i++)
                {
                    mTypes[i] = memberTypes[i].ToNative();
                    typeSize += H5T.getSize(mTypes[i]);
                }

                tid = H5T.create(H5T.CreateClass.COMPOUND, typeSize);
                int offset = 0;
                for (int i = 0; i < memberNames.Length; i++)
                {
                    H5T.insert(tid, memberNames[i], offset, mTypes[i]);
                    offset += H5T.getSize(mTypes[i]);
                }
                H5T.commit(Fid, name, tid);
            }
            finally
            {
                for (int i = 0; i < nMembers; i++)
                {
                    if (mTypes[i] != null && mTypes[i].Id > 0)
                        H5T.close(mTypes[i]);
                }
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
            }
            return new H5Datatype(this, name);
        }

        public H5Dataset CreateDataset(string path, H5Datatype type, long[] dims, long[] maxDims, long[] chunks, uint compression)
        {
            H5DataTypeId tid = null;
            H5DataSpaceId sid = null;
            H5PropertyListId pid = null;
            H5DataSetId did = null;
            try
            {
                tid = type.ToNative();
                int rank = dims.Length;
                sid = maxDims == null ? H5S.create_simple(rank, dims) : H5S.create_simple(rank, dims, maxDims);
                pid = H5P.create(H5P.PropertyListClass.DATASET_CREATE);
                H5P.setDeflate(pid, compression);

                if (chunks == null)
                {
                    chunks = new long[dims.Length];
                    for (int i = 0; i < dims.Length; i++)
                    {
                        chunks[i] = Math.Min(dims[i], 64);
                    }
                }
                H5P.setChunk(pid, chunks);

                did = H5D.create(Fid, path, tid, sid, new H5PropertyListId(H5P.Template.DEFAULT), pid,
                                 new H5PropertyListId(H5P.Template.DEFAULT));
            }
            finally
            {
                if (did != null && did.Id > 0)
                    H5D.close(did);
                if (pid != null && pid.Id > 0)
                    H5P.close(pid);
                if (sid != null && sid.Id > 0)
                    H5S.close(sid);
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
            }
            return new H5Dataset(this, path);
        }

        public H5Dataset CreateDataset<T>(string path, H5Datatype type, T[] data, uint compression)
        {
            var dataset = CreateDataset(path, type, new long[] {data.Length}, null, null, compression);
            dataset.SetData(data);
            return dataset;
        }

        public H5Link CreateHardLink(string path, H5Object obj)
        {
            H5L.createHardLink(obj.File.Fid, obj.Path, Fid, path);
            return new H5Link(this, path);
        }
    }
}
