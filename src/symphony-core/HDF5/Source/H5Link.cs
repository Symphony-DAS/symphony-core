using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HDF5
{
    public class H5Link : H5Object
    {
        internal H5Link(H5File file, string path) : base(file, path)
        {
        }
    }
}
