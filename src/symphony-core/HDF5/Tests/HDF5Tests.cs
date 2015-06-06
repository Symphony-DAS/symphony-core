using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace HDF5.Tests
{
    [TestFixture]
    internal class HDF5Tests
    {
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
                file.Root.AddGroup("simple");
            }

            using (var file = new H5File(TEST_FILE))
            {
                Assert.AreEqual(1, file.Root.Groups.Count());

                var group = file.Root.Groups.First();
                Assert.AreEqual("simple", group.Name);
                Assert.AreEqual(0, group.Groups.Count());
            }
        }

        [Test]
        public void NestsGroups()
        {
            using (var file = new H5File(TEST_FILE))
            {
                file.Root.AddGroup("out").AddGroup("mid").AddGroup("in");

                Assert.AreEqual(1, file.Root.Groups.Count());
                var o = file.Root.Groups.First();
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
            var expected = new Dictionary<string, object>()
                {
                    {"attr1", "hello world!"},
                    {"attr2", 15.6},
                    {"attr3", new[] {3, 2, 1}}
                };

            using (var file = new H5File(TEST_FILE))
            {
                var group = file.Root.AddGroup("simple");

                foreach (var kv in expected)
                {
                    group.AddAttribute(kv.Key, kv.Value);
                }
            }

            using (var file = new H5File(TEST_FILE))
            {
                Assert.AreEqual(1, file.Root.Groups.Count());

                var group = file.Root.Groups.First();
                var attributes = group.Attributes.ToDictionary(a => a.Name, a => a.GetValue());

                Assert.AreEqual(expected, attributes);
            }
        }

        [Test]
        public void ShouldOverwriteAttribute()
        {
            using (var file = new H5File(TEST_FILE))
            {
                var group = file.Root;
                group.AddAttribute("attr", "banana");
                group.AddAttribute("attr", 123);

                var attr = group.Attributes.First();
                Assert.AreEqual("attr", attr.Name);
                Assert.AreEqual(123, attr.GetValue());
            }
        }

        [Test]
        public void ShouldRemoveAttribute()
        {
            using (var file = new H5File(TEST_FILE))
            {
                var group = file.Root;
                group.AddAttribute("attr", "wowow");
                group.RemoveAttribute("attr");

                Assert.AreEqual(0, group.Attributes.Count());
            }
        }
}
}
