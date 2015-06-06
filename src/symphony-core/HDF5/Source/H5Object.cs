using System.Linq;

namespace HDF5
{
    public abstract class H5Object
    {
        public H5File File { get; private set; }
        public string Path { get; private set; }

        public virtual string Name
        {
            get { return Path.Split(new[] { '/' }).Last(); }
        }

        protected H5Object(H5File file, string path)
        {
            File = file;
            Path = path;
        }
    }
}
