using HDF5DotNet;
using NUnit.Framework;

namespace Symphony.Core
{
    class H5DocumentTests
    {
        const string TEST_FILE = "myCSharp.h5";

        private H5FileId fileId;

        [SetUp]
        public void Setup()
        {
            if (System.IO.File.Exists(TEST_FILE))
            {
                System.IO.File.Delete(TEST_FILE);
            }
            fileId = H5F.create(TEST_FILE, H5F.CreateMode.ACC_EXCL);
        }

        [TearDown]
        public void Teardown()
        {
            H5F.close(fileId);
        }

        [Test]
        public void WriteReadBooleanAttribute()
        {
            const bool expected = true;
            H5Document.WriteBooleanAttribute(fileId, "attr", expected);
            Assert.AreEqual(expected, H5Document.ReadBooleanAttribute(fileId, "attr"));
        }

        [Test]
        public void WriteReadLongAttribute()
        {
            const long expected = 1534;
            H5Document.WriteLongAttribute(fileId, "attr", expected);
            Assert.AreEqual(expected, H5Document.ReadLongAttribute(fileId, "attr"));
        }

        [Test]
        public void WriteReadDoubleArrayAttribute()
        {
            var expected = new double[] {1.2, 2.3, 4.5, 1.3};
            H5Document.WriteDoubleArrayAttribute(fileId, "attr", expected);
            Assert.AreEqual(expected, H5Document.ReadDoubleArrayAttribute(fileId, "attr"));
        }

        [Test]
        public void WriteReadStringAttribute()
        {
            const string expected = "Hello World!";
            H5Document.WriteStringAttribute(fileId, "attr", expected);
            Assert.AreEqual(expected, H5Document.ReadStringAttribute(fileId, "attr"));
        }

        [Test]
        public void OverwriteAttribute()
        {
            H5Document.WriteStringAttribute(fileId, "attr", "one");
            H5Document.WriteStringAttribute(fileId, "attr", "two");
            Assert.AreEqual("two", H5Document.ReadStringAttribute(fileId, "attr"));
        }
    }
}
