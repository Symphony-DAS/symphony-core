using System.Linq;
using HDF.PInvoke;
#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    public abstract class H5Object
    {
        public H5File File { get; protected set; }
        public string Path { get; protected set; }

        public virtual string Name
        {
            get { return Path.Split(new[] {'/'}).Last(); }
        }

        protected H5Object(H5File file, string path)
        {
            File = file;
            Path = path;
        }

        public void Flush()
        {
            H5F.flush(File.Fid, H5F.scope_t.LOCAL);
        }

        public override bool Equals(object obj)
        {
            var o = obj as H5Object;
            if (o != null)
            {
                return Equals(File, o.File) && string.Equals(Path, o.Path);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((File != null ? File.GetHashCode() : 0) * 397) ^ (Path != null ? Path.GetHashCode() : 0);
            }
        }

        public static bool operator ==(H5Object lhs, H5Object rhs)
        {
            if (!Equals(lhs, null) && !Equals(rhs, null))
            {
                return lhs.Equals(rhs);
            }
            return !Equals(lhs, null) ^ Equals(rhs, null);
        }

        public static bool operator !=(H5Object lhs, H5Object rhs)
        {
            return !(lhs == rhs);
        }
    }
}
