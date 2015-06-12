using System.Linq;

namespace HDF5
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

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((H5Object) obj);
        }

        protected bool Equals(H5Object other)
        {
            return Equals(File, other.File) && string.Equals(Path, other.Path);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((File != null ? File.GetHashCode() : 0) * 397) ^ (Path != null ? Path.GetHashCode() : 0);
            }
        }
    }
}
