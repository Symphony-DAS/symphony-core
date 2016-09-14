using System;
using HDF.PInvoke;

using size_t = System.IntPtr;

#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    public class H5File : H5Group, IDisposable
    {
        public hid_t Fid { get; private set; }

        public H5File(string filename) : base(null, "/")
        {
            if (System.IO.File.Exists(filename))
            {
                Fid = H5F.open(filename, H5F.ACC_RDWR);
            }
            else
            {
                Fid = H5F.create(filename, H5F.ACC_EXCL);
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
            if (Fid <= 0)
                return;
            H5F.close(Fid);
            Fid = -1;
        }

        public void Delete(string path)
        {
            H5L.delete(Fid, path);
        }

        public H5Group CreateGroup(string path)
        {
            hid_t gid = -1;
            try
            {
                gid = H5G.create(Fid, path);
            }
            finally
            {
                if (gid > 0)
                    H5G.close(gid);
            }
            return new H5Group(this, path);
        }

        public H5Datatype CreateDatatype(string name, H5T.class_t typeClass, long typeSize)
        {
            hid_t tid = -1;
            try
            {
                switch (typeClass)
                {
                    case H5T.class_t.STRING:
                        tid = H5T.copy(H5T.C_S1);
                        H5T.set_size(tid, new IntPtr(typeSize));
                        break;
                    default:
                        throw new NotImplementedException();
                }
                H5T.commit(Fid, name, tid);
            }
            finally
            {
                if (tid > 0)
                    H5T.close(tid);
            }
            return new H5Datatype(this, name);
        }

        public H5Datatype CreateDatatype(string name, string[] memberNames, H5Datatype[] memberTypes)
        {
            int nMembers = memberNames.Length;
            var mTypes = new hid_t[nMembers];

            hid_t tid = -1;
            try
            {
                long typeSize = 0;
                for (int i = 0; i < nMembers; i++)
                {
                    mTypes[i] = memberTypes[i].ToNative();
                    typeSize += H5T.get_size(mTypes[i]).ToInt64();
                }

                tid = H5T.create(H5T.class_t.COMPOUND, new IntPtr(typeSize));
                long offset = 0;
                for (int i = 0; i < memberNames.Length; i++)
                {
                    H5T.insert(tid, memberNames[i], new IntPtr(offset), mTypes[i]);
                    offset += H5T.get_size(mTypes[i]).ToInt32();
                }
                H5T.commit(Fid, name, tid);
            }
            finally
            {
                for (int i = 0; i < nMembers; i++)
                {
                    if (mTypes[i] > 0)
                        H5T.close(mTypes[i]);
                }
                if (tid > 0)
                    H5T.close(tid);
            }
            return new H5Datatype(this, name);
        }

        public H5Dataset CreateDataset(string path, H5Datatype type, ulong[] dims, ulong[] maxDims, ulong[] chunks, uint compression)
        {
            hid_t tid, sid, pid, did;
            tid = sid = pid = did = -1;
            try
            {
                tid = type.ToNative();
                int rank = dims.Length;
                sid = H5S.create_simple(rank, dims, maxDims);
                pid = H5P.create(H5P.DATASET_CREATE);
                H5P.set_deflate(pid, compression);

                if (chunks == null)
                {
                    chunks = new ulong[dims.Length];
                    for (int i = 0; i < dims.Length; i++)
                    {
                        chunks[i] = Math.Min(dims[i], 64);
                    }
                }
                H5P.set_chunk(pid, chunks.Length, chunks);

                did = H5D.create(Fid, path, tid, sid, H5P.DEFAULT, pid);
            }
            finally
            {
                if (did > 0)
                    H5D.close(did);
                if (pid > 0)
                    H5P.close(pid);
                if (sid > 0)
                    H5S.close(sid);
                if (tid > 0)
                    H5T.close(tid);
            }
            return new H5Dataset(this, path);
        }

        public H5Dataset CreateDataset<T>(string path, H5Datatype type, T[] data, uint compression)
        {
            var dataset = CreateDataset(path, type, new[] {(ulong) data.Length}, null, null, compression);
            dataset.SetData(data);
            return dataset;
        }

        public H5Link CreateHardLink(string path, H5Object obj)
        {
            H5L.create_hard(obj.File.Fid, obj.Path, Fid, path);
            return new H5Link(this, path);
        }
    }
}
