using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HDF5DotNet;

namespace HDF5
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

            H5ObjectWithAttributes oid = null;
            try
            {
                oid = H5Ox.open(File.Fid, Path);
                H5A.Delete(oid, key);
                return !ContainsKey(key);
            }
            finally
            {
                if (oid != null && oid.Id > 0)
                    H5Ox.close(oid);
            }
        }

        public bool ContainsKey(string key)
        {
            return GetAttributes().Any(a => a.Name == key);
        }

        private IEnumerable<H5Attribute> GetAttributes()
        {
            H5ObjectWithAttributes oid = null;
            try
            {
                oid = H5Ox.open(File.Fid, Path);
                H5ObjectInfo oinfo = H5O.getInfoByName(File.Fid, Path);
                int n = (int)oinfo.nAttributes;
                for (int i = 0; i < n; i++)
                {
                    string name = H5A.getNameByIndex(File.Fid, Path, H5IndexType.NAME, H5IterationOrder.INCREASING, i);
                    yield return new H5Attribute(File, Path, name);
                }
            }
            finally
            {
                if (oid != null && oid.Id > 0)
                    H5Ox.close(oid);
            }
        }

        private H5Attribute CreateAttribute(string name, object value)
        {
            H5ObjectWithAttributes oid = null;
            H5DataTypeId tid = null;
            H5DataSpaceId sid = null;
            H5AttributeId aid = null;
            try
            {
                oid = H5Ox.open(File.Fid, Path);

                if (value is string || value is char)
                {
                    string svalue = value.ToString();
                    if (svalue.Length == 0)
                    {
                        tid = H5T.copy(H5T.H5Type.C_S1);
                        sid = H5S.create(H5S.H5SClass.NULLSPACE);
                        aid = H5A.create(oid, name, tid, sid);
                    }
                    else
                    {
                        tid = H5T.copy(H5T.H5Type.C_S1);
                        H5T.setSize(tid, svalue.Length);
                        sid = H5S.create(H5S.H5SClass.SCALAR);
                        aid = H5A.create(oid, name, tid, sid);
                        H5A.write(aid, tid, new H5Array<byte>(Encoding.ASCII.GetBytes(svalue)));
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
                        var dims = new long[rank];
                        for (int i = 0; i < rank; i++)
                        {
                            dims[i] = ((Array)value).GetLength(i);
                        }
                        sid = H5S.create_simple(rank, dims);
                        data = (Array)value;
                    }
                    else
                    {
                        sid = H5S.create(H5S.H5SClass.SCALAR);
                        data = Array.CreateInstance(elementType, 1);
                        data.SetValue(value, 0);
                    }

                    aid = H5A.create(oid, name, tid, sid);

                    if (!valueType.IsArray || ((Array) value).Length > 0)
                    {
                        // Equivalent to: H5Array<elementType> buffer = new H5Array<elementType>(data);
                        var bufferType = typeof(H5Array<>).MakeGenericType(new[] { elementType });
                        var buffer = Activator.CreateInstance(bufferType, new object[] { data });

                        // Equivalent to: H5A.write(attributeId, typeId, buffer);
                        var methodInfo = typeof(H5A).GetMethod("write").MakeGenericMethod(new[] { elementType });
                        methodInfo.Invoke(null, new[] { aid, tid, buffer });
                    }
                }

                return new H5Attribute(File, Path, name);
            }
            finally
            {
                if (aid != null && aid.Id > 0)
                    H5A.close(aid);
                if (sid != null && sid.Id > 0)
                    H5S.close(sid);
                if (tid != null && tid.Id > 0)
                    H5T.close(tid);
                if (oid != null && oid.Id > 0)
                    H5Ox.close(oid);
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
