using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF.PInvoke;
using hsize_t = System.UInt64;

#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    public class H5Attribute : H5Object
    {
        private readonly string _name;

        public override string Name { get { return _name; } }

        internal object Value { get; private set; }

        internal H5Attribute(H5File file, string path, string name) : base(file, path)
        {
            _name = name;
        }

        public H5Attribute(object value) : this(null, null, null)
        {
            Value = value;
        }

        public static implicit operator H5Attribute(string value)
        {
            return new H5Attribute(value);
        }

        public static implicit operator string(H5Attribute a)
        {
            return (string) a.GetValue();
        }

        public static implicit operator H5Attribute(long value)
        {
            return new H5Attribute(value);
        }

        public static implicit operator long(H5Attribute a)
        {
            return (long) a.GetValue();
        }

        public static implicit operator H5Attribute(double value)
        {
            return new H5Attribute(value);
        }

        public static implicit operator double(H5Attribute a)
        {
            return (double)a.GetValue();
        }

        public static implicit operator H5Attribute(uint value)
        {
            return new H5Attribute(value);
        }

        public static implicit operator uint(H5Attribute a)
        {
            return (uint)a.GetValue();
        }

        public object GetValue()
        {
            hid_t oid, tmpid, tid, sid, aid;
            oid = tmpid = tid = sid = aid = -1;
            try
            {
                oid = H5O.open(File.Fid, Path);
                aid = H5A.open(oid, _name);
                sid = H5A.get_space(aid);

                tmpid = H5A.get_type(aid);
                tid = H5T.get_native_type(tmpid, H5T.direction_t.DEFAULT);

                object value;
                if (H5T.get_class(tid) == H5T.class_t.STRING)
                {
                    if (H5S.get_simple_extent_type(sid) == H5S.class_t.NULL)
                    {
                        value = string.Empty;
                    }
                    else
                    {
                        var buffer = new byte[H5T.get_size(tid).ToInt32()];

                        GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        H5A.read(aid, tid, pinnedBuffer.AddrOfPinnedObject());
                        pinnedBuffer.Free();

                        value = Encoding.ASCII.GetString(buffer).TrimEnd((char) 0);
                    }
                }
                else
                {
                    Type elementType = H5Tx.getSystemType(tid);

                    int ndims = H5S.get_simple_extent_ndims(sid);
                    var dims = new hsize_t[ndims];

                    H5S.get_simple_extent_dims(sid, dims, null);

                    var ldims = new long[ndims];
                    for (int i = 0; i < ndims; i++)
                    {
                        if (dims[i] > Int32.MaxValue)
                            throw new NotSupportedException("Attribute dimension is too large");
                        ldims[i] = (long) dims[i];
                    }

                    Array data = Array.CreateInstance(elementType, dims.Any() ? ldims : new long[] { 1 });

                    if (data.Length > 0)
                    {
                        GCHandle pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
                        H5A.read(aid, tid, pinnedData.AddrOfPinnedObject());
                        pinnedData.Free();
                    }

                    value = dims.Any() ? data : data.GetValue(0);
                }

                return value;
            }
            finally
            {
                if (aid > 0)
                    H5A.close(aid);
                if (sid > 0)
                    H5S.close(sid);
                if (tmpid > 0)
                    H5T.close(tmpid);
                if (tid > 0)
                    H5T.close(tid);
                if (oid > 0)
                    H5O.close(oid);
            }
        }
    }
}
