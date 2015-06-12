using System;
using System.Linq;
using System.Text;
using HDF5DotNet;

namespace HDF5
{
    public class H5Attribute : H5Object
    {
        private readonly string name;

        public override string Name { get { return name; } }

        internal object Value { get; private set; }

        internal H5Attribute(H5File file, string path, string name) : base(file, path)
        {
            this.name = name;
        }

        public H5Attribute(object value) : this(null, null, null)
        {
            Value = value;
        }

        public object GetValue()
        {
            H5ObjectWithAttributes oid = null;
            H5DataTypeId tmpid = null;
            H5DataTypeId tid = null;
            H5AttributeId aid = null;
            try
            {
                oid = H5Ox.open(File.Fid, Path);
                aid = H5A.open(oid, name);

                tmpid = H5A.getType(aid);
                tid = H5T.getNativeType(tmpid, H5T.Direction.DEFAULT);

                object value;
                if (H5T.getClass(tid) == H5T.H5TClass.STRING)
                {
                    var buffer = new byte[H5T.getSize(tid)];
                    H5A.read(aid, tid, new H5Array<byte>(buffer));
                    value = Encoding.ASCII.GetString(buffer);
                }
                else
                {
                    Type elementType = H5Tx.getSystemType(tid);

                    H5DataSpaceId spaceId = H5A.getSpace(aid);
                    long[] dims = H5S.getSimpleExtentDims(spaceId);
                    H5S.close(spaceId);

                    Array data = Array.CreateInstance(elementType, dims.Any() ? dims : new long[] { 1 });

                    //H5Array<type> buffer = new H5Array<type>(data);
                    var bufferType = typeof(H5Array<>).MakeGenericType(new[] { elementType });
                    var buffer = Activator.CreateInstance(bufferType, new object[] { data });

                    //H5A.read(attributeId, typeId, buffer);
                    var methodInfo = typeof(H5A).GetMethod("read").MakeGenericMethod(new[] { elementType });
                    methodInfo.Invoke(null, new[] { aid, tid, buffer });

                    value = dims.Any() ? data : data.GetValue(0);
                }

                return value;
            }
            finally
            {
                if (aid != null && aid.Id > 0)
                    H5A.close(aid);
                if (tmpid != null && tmpid.Id > 0)
                    H5T.close(tmpid);
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
                if (oid != null && oid.Id > 0)
                    H5Ox.close(oid);
            }
        }
    }
}
