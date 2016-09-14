using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF;
using HDF.PInvoke;
using NUnit.Framework;

namespace HDF.Tests
{
    [TestFixture]
    public class HDFTests
    {
        [StructLayout(LayoutKind.Explicit)]
        unsafe struct Point
        {
            [FieldOffset(0)]
            public double x;
            [FieldOffset(8)]
            public double y;
        }

        [StructLayout(LayoutKind.Explicit)]
        unsafe struct NamedPoint
        {
            [FieldOffset(0)]
            public fixed byte name[40];
            [FieldOffset(40)] 
            public Point point;
        }

        private const string TEST_FILE = "myCSharp.h5";

        [SetUp]
        public void DeleteTestFiles()
        {
            if (File.Exists(TEST_FILE))
                File.Delete(TEST_FILE);
        }

        [Test]
        public void SimpleOpenClose()
        {
            using (new H5File(TEST_FILE))
            {
            }
            Assert.IsTrue(File.Exists(TEST_FILE));
        }

        [Test]
        public void SimpleGroupCreateFind()
        {
            using (var file = new H5File(TEST_FILE))
            {
                file.AddGroup("simple");
            }

            using (var file = new H5File(TEST_FILE))
            {
                Assert.AreEqual(1, file.Groups.Count());

                var group = file.Groups.First();
                Assert.AreEqual("simple", group.Name);
                Assert.AreEqual(0, group.Groups.Count());
            }
        }

        [Test]
        public void NestsGroups()
        {
            using (var file = new H5File(TEST_FILE))
            {
                file.AddGroup("out").AddGroup("mid").AddGroup("in");

                Assert.AreEqual(1, file.Groups.Count());
                var o = file.Groups.First();
                Assert.AreEqual("out", o.Name);
                Assert.AreEqual(1, o.Groups.Count());
                var m = o.Groups.First();
                Assert.AreEqual("mid", m.Name);
                Assert.AreEqual(1, m.Groups.Count());
                var i = m.Groups.First();
                Assert.AreEqual("in", i.Name);
                Assert.AreEqual(0, i.Groups.Count());
            }    
        }

        [Test]
        public void SimpleAttributeCreateFind()
        {
            var attributes = new Dictionary<string, object>()
                {
                    {"attr1", "hello world!"},
                    {"attr2", 15.6},
                    {"attr3", new[] {3, 2, 1}}
                };

            using (var file = new H5File(TEST_FILE))
            {
                var group = file.AddGroup("simple");

                foreach (var kv in attributes)
                {
                    group.Attributes[kv.Key] = new H5Attribute(kv.Value);
                }
            }

            using (var file = new H5File(TEST_FILE))
            {
                Assert.AreEqual(1, file.Groups.Count());

                var group = file.Groups.First();
                var actual = group.Attributes.ToDictionary(a => a.Name, a => a.GetValue());

                Assert.AreEqual(attributes, actual);
            }
        }

        [Test]
        public void ShouldAddEmptyStringAttribute()
        {
            using (var file = new H5File(TEST_FILE))
            {
                file.Attributes["attr"] = "";
                Assert.AreEqual("", file.Attributes["attr"].GetValue());
            }
        }

        [Test]
        public void ShouldOverwriteAttribute()
        {
            using (var file = new H5File(TEST_FILE))
            {
                file.Attributes["attr"] = "banana";
                file.Attributes["attr"] = 123;

                var attr = file.Attributes.First();
                Assert.AreEqual("attr", attr.Name);
                Assert.AreEqual(123, attr.GetValue());
            }
        }

        [Test]
        public void ShouldRemoveAttribute()
        {
            using (var file = new H5File(TEST_FILE))
            {
                file.Attributes["attr"] = "wowow";
                file.Attributes.Remove("attr");

                Assert.AreEqual(0, file.Attributes.Count());
            }
        }

        [Test]
        public void SimpleDatatypeCreateFind()
        {
            var points = new Point[10];
            for (int i = 0; i < points.Length; i++)
            {
                points[i].x = i;
                points[i].y = i*3;
            }

            using (var file = new H5File(TEST_FILE))
            {
                var type = file.CreateDatatype("POINT",
                                               new[] {"x", "y"},
                                               new[]
                                                   {
                                                       new H5Datatype(H5T.NATIVE_DOUBLE),
                                                       new H5Datatype(H5T.NATIVE_DOUBLE)
                                                   });

                var dataset = file.AddDataset("points", type, points);
            }

            using (var file = new H5File(TEST_FILE))
            {
                Assert.AreEqual(1, file.Datasets.Count());
                
                var dataset = file.Datasets.First();
                var actual = dataset.GetData<Point>();
                Assert.AreEqual(points, actual);
            }
        }

        [Test]
        public void ComplexDatatypeCreate()
        {
            var points = new NamedPoint[10];
            for (int i = 0; i < 10; i++)
            {
                var p = new NamedPoint {point = {x = i*2, y = i*3}};
                var name = Encoding.ASCII.GetBytes(i.ToString());
                unsafe
                {
                    Marshal.Copy(name, 0, (IntPtr)p.name, name.Length);
                }
                points[i] = p;
            }

            using (var file = new H5File(TEST_FILE))
            {
                var stringType = file.CreateDatatype("STRING", H5T.class_t.STRING, 40);
                var pointType = file.CreateDatatype("POINT",
                                                    new[] {"x", "y"},
                                                    new[]
                                                        {
                                                            new H5Datatype(H5T.NATIVE_DOUBLE),
                                                            new H5Datatype(H5T.NATIVE_DOUBLE)
                                                        });

                var type = file.CreateDatatype("NAMED_POINT",
                                                new[] {"name", "point"},
                                                new[] {stringType, pointType});


                var dataset = file.AddDataset("points", type, points);
                Assert.AreEqual(points, dataset.GetData<NamedPoint>());
            }
        }

        [Test]
        public void ShouldExtendChunkDataset()
        {
            using (var file = new H5File(TEST_FILE))
            {
                var type = file.CreateDatatype("POINT",
                                               new[] { "x", "y" },
                                               new[]
                                                   {
                                                       new H5Datatype(H5T.NATIVE_DOUBLE),
                                                       new H5Datatype(H5T.NATIVE_DOUBLE)
                                                   });

                var dataset = file.AddDataset("points", type, new ulong[] {0}, new[] {H5S.UNLIMITED}, new ulong[] {64});

                var points = new[] {new Point {x = 1, y = 2}};

                // Dataset not yet extended
                dataset.SetData(points);
                Assert.AreEqual(0, dataset.GetData<Point>().Count());

                dataset.Extend(new ulong[] {1});
                dataset.SetData(points);
                Assert.AreEqual(1, dataset.GetData<Point>().Count());
                Assert.AreEqual(points, dataset.GetData<Point>());
            }
        }

        [Test]
        public void ShouldDeleteGroup()
        {
            using (var file = new H5File(TEST_FILE))
            {
                var group = file.AddGroup("group");
                group.Delete();
                Assert.AreEqual(0, file.Groups.Count());
            }

            using (var file = new H5File(TEST_FILE))
            {
                Assert.AreEqual(0, file.Groups.Count());
            }
        }

        [Test]
        public void PropertyShouldReturnSameObject()
        {
            using (var file = new H5File(TEST_FILE))
            {
                var g1 = file.AddGroup("group");
                var g2 = file.Groups.First();
                var g3 = file.Groups.First();
                Assert.IsTrue(g1 == g2 && g2 == g3);
            }
        }

        [Test]
        public void ObjectsWithSamePathShouldBeEqual()
        {
            using (var file = new H5File(TEST_FILE))
            {
                var group = file.AddGroup("group");
                Assert.AreEqual(group, file.Groups.First());
            }
        }
    }
}
