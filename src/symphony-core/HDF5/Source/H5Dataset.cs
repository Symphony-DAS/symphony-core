using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HDF5
{
    public class H5Dataset : H5ObjectWithMetadata
    {
        internal H5Dataset(H5File file, string path)
            : base(file, path)
        {
        }

        public object GetData()
        {
            throw new NotImplementedException();
        }
    }
}
