using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF.PInvoke;
using size_t = System.IntPtr;
using ssize_t = System.IntPtr;

#if HDF5_VER1_10
using hid_t = System.Int64;
#else
using hid_t = System.Int32;
#endif

namespace HDF
{
    public class H5AttributeManager : H5Object, IEnumerable<H5Attribute>
    {
        internal H5AttributeManager(H5File file, string path) : base(file, path)
        {
        }

        public H5Attribute this[string key]
        {
            get
            {
                if (!ContainsKey(key))
                    throw new KeyNotFoundException();
                return GetAttributes().First(a => a.Name == key);
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                if (ContainsKey(key))
                    Remove(key);
                CreateAttribute(key, value.Value);
            }
        }

        public void Add(H5Attribute item)
        {
            CreateAttribute(item.Name, item.Value);
        }

        public bool Remove(string key)
        {
            if (!ContainsKey(key))
                return false;

            hid_t oid = -1;
            try
            {
                oid = H5O.open(File.Fid, Path);
                H5A.delete(oid, key);
                return !ContainsKey(key);
            }
            finally
            {
                if (oid > 0)
                    H5O.close(oid);
            }
        }

        public bool ContainsKey(string key)
        {
            return GetAttributes().Any(a => a.Name == key);
        }

        private IEnumerable<H5Attribute> GetAttributes()
        {
            hid_t oid = -1;
            try
            {
                oid = H5O.open(File.Fid, Path);
                var oinfo = new H5O.info_t();
                H5O.get_info_by_name(File.Fid, Path, ref oinfo);
                ulong n = oinfo.num_attrs;
                for (ulong i = 0; i < n; i++)
                {
                    ssize_t size = H5A.get_name_by_idx(File.Fid, Path, H5.index_t.NAME, H5.iter_order_t.INC, i, null, IntPtr.Zero);

                    var buffer = new byte[size.ToInt64() + 1];
                    var bufferSize = new IntPtr(size.ToInt64() + 1);
                    
                    H5A.get_name_by_idx(File.Fid, Encoding.ASCII.GetBytes(Path), H5.index_t.NAME, H5.iter_order_t.INC, i, buffer, bufferSize);

                    yield return new H5Attribute(File, Path, Encoding.ASCII.GetString(buffer).TrimEnd((char) 0));
                }
            }
            finally
            {
                if (oid > 0)
                    H5O.close(oid);
            }
        }

        private H5Attribute CreateAttribute(string name, object value)
        {
            hid_t oid, tid, sid, aid;
            oid = tid = sid = aid = -1;
            try
            {
                oid = H5O.open(File.Fid, Path);

                if (value is string || value is char)
                {
                    string svalue = value.ToString();
                    if (svalue.Length == 0)
                    {
                        tid = H5T.copy(H5T.C_S1);
                        sid = H5S.create(H5S.class_t.NULL);
                        aid = H5A.create(oid, name, tid, sid);
                    }
                    else
                    {
                        tid = H5T.copy(H5T.C_S1);
                        H5T.set_size(tid, new IntPtr(svalue.Length));
                        sid = H5S.create(H5S.class_t.SCALAR);
                        aid = H5A.create(oid, name, tid, sid);

                        IntPtr valueArray = Marshal.StringToHGlobalAnsi(svalue);
                        H5A.write(aid, tid, valueArray);
                        Marshal.FreeHGlobal(valueArray);
                    }
                }
                else
                {
                    Type valueType = value.GetType();
                    Type elementType = valueType.IsArray ? valueType.GetElementType() : valueType;
                    tid = H5Tx.getNativeType(elementType);

                    Array data;
                    if (valueType.IsArray)
                    {
                        int rank = ((Array)value).Rank;
                        var dims = new ulong[rank];
                        for (int i = 0; i < rank; i++)
                        {
                            dims[i] = (ulong) ((Array)value).GetLength(i);
                        }
                        sid = H5S.create_simple(rank, dims, null);
                        data = (Array)value;
                    }
                    else
                    {
                        sid = H5S.create(H5S.class_t.SCALAR);
                        data = Array.CreateInstance(elementType, 1);
                        data.SetValue(value, 0);
                    }

                    aid = H5A.create(oid, name, tid, sid);

                    if (!valueType.IsArray || ((Array) value).Length > 0)
                    {
                        GCHandle pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
                        H5A.write(aid, tid, pinnedData.AddrOfPinnedObject());
                        pinnedData.Free();
                    }
                }

                return new H5Attribute(File, Path, name);
            }
            finally
            {
                if (aid > 0)
                    H5A.close(aid);
                if (sid > 0)
                    H5S.close(sid);
                if (tid > 0)
                    H5T.close(tid);
                if (oid > 0)
                    H5O.close(oid);
            }
        }

        public IEnumerator<H5Attribute> GetEnumerator()
        {
            return GetAttributes().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static bool IsSupportedType(Type type)
        {
            return type == typeof(string) || type.IsPrimitive || (type.IsArray && type.GetElementType().IsPrimitive);
        }
    }
}
