using System;
using System.IO;
using HDF5DotNet;

namespace HDF5
{
    public class H5File : IDisposable
    {
        public H5FileId Fid;

        public H5Group Root { get; private set; }

        public H5File(string filename)
        {
            if (File.Exists(filename))
            {
                Fid = H5F.open(filename, H5F.OpenMode.ACC_RDWR);
            }
            else
            {
                Fid = H5F.create(filename, H5F.CreateMode.ACC_EXCL);
            }
            Root = new H5Group(this, "/");
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

        private bool disposed = false;

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;
            Close();
            disposed = true;
        }

        public void Close()
        {
            if (Fid != null && Fid.Id > 0)
            {
                H5F.close(Fid);
                Fid = null;
            }
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

        public H5Dataset CreateDataset(string path)
        {
            throw new NotImplementedException();
        }
    }
}
