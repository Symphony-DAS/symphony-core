using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    using NUnit.Framework;

    using HDF5DotNet;

    [TestFixture]
    class HDF5Tests
    {
        const string TEST_FILE = "myCSharp.h5";

        [TearDown]
        public void DeleteTestFiles()
        {
            if (System.IO.File.Exists(TEST_FILE))
                System.IO.File.Delete(TEST_FILE);
        }

        [Test]
        public void SimpleOpenClose()
        {
            // Create an HDF5 file.
            // The enumeration type H5F.CreateMode provides only the legal 
            // creation modes.  Missing H5Fcreate parameters are provided
            // with default values.
            H5FileId fileId = H5F.create(TEST_FILE, H5F.CreateMode.ACC_TRUNC);

            // Close the file.
            H5F.close(fileId);

            Assert.IsTrue(System.IO.File.Exists(TEST_FILE));
        }
        [Test]
        public void SimpleGroupCreateFind()
        {
            // Create an HDF5 file.
            // The enumeration type H5F.CreateMode provides only the legal 
            // creation modes.  Missing H5Fcreate parameters are provided
            // with default values.
            H5FileId fileId = H5F.create(TEST_FILE, H5F.CreateMode.ACC_TRUNC);

            // Create a group in the file
            H5GroupId groupId = H5G.create(fileId, "/simple");

            // Close everything down.
            H5G.close(groupId);
            H5F.close(fileId);

            Assert.IsTrue(System.IO.File.Exists(TEST_FILE));

            fileId = H5F.open(TEST_FILE, H5F.OpenMode.ACC_RDONLY);
            groupId = H5G.open(fileId, "/simple");
            Assert.AreEqual(0, H5G.getNumObjects(groupId));

            H5G.close(groupId);
            H5F.close(fileId);
        }
        [Test]
        public void SimpleDataReadWrite()
        {
            // Create an HDF5 file.
            // The enumeration type H5F.CreateMode provides only the legal 
            // creation modes.  Missing H5Fcreate parameters are provided
            // with default values.
            H5FileId fileId = H5F.create(TEST_FILE, H5F.CreateMode.ACC_TRUNC);

            // Create a group in the file
            H5GroupId groupId = H5G.create(fileId, "/simple");

            // Prepare to create a data space for writing a 1-dimensional
            // signed integer array.
            const int RANK = 1;
            long[] dims = new long[RANK];
            const int SIZE = 12;
            dims[0] = SIZE;

            // Put descending ramp data in an array so that we can
            // write it to the file.
            int[] dset_data = new int[SIZE];
            for (int i = 0; i < SIZE; i++)
                dset_data[i] = SIZE - i;

            // Create a data space to accommodate our 1-dimensional array.
            // The resulting H5DataSpaceId will be used to create the 
            // data set.
            H5DataSpaceId spaceId = H5S.create_simple(RANK, dims);

            // Create the data set.
            H5DataSetId dataSetId = H5D.create(fileId, "/arrayIntExample",
                                               H5T.H5Type.NATIVE_INT, spaceId);

            // Write the integer data to the data set.
            H5D.write(dataSetId, 
                new H5DataTypeId(H5T.H5Type.NATIVE_INT),
                new H5Array<int>(dset_data));

            // If we were writing a single value it might look like this.
            // Create the data set.
            H5DataSetId scalarId = H5D.create(fileId, "/scalarIntExample",
                                              H5T.H5Type.NATIVE_INT, spaceId);
            int singleValue = 100;
            H5D.writeScalar(scalarId, 
                new H5DataTypeId(H5T.H5Type.NATIVE_INT),
                ref singleValue);

            // Close everything down.
            H5D.close(dataSetId);
            H5D.close(scalarId);
            H5S.close(spaceId);
            H5G.close(groupId);
            H5F.close(fileId);

            Assert.IsTrue(System.IO.File.Exists(TEST_FILE));

            fileId = H5F.open(TEST_FILE, H5F.OpenMode.ACC_RDONLY);
            Assert.IsTrue(fileId.Id > 0);

            groupId = H5G.open(fileId, "/simple");
            Assert.IsTrue(groupId.Id > 0);
            Assert.AreEqual(0, H5G.getNumObjects(groupId));

            // Open the data set
            dataSetId = H5D.open(fileId, "/arrayIntExample");
            Assert.IsTrue(dataSetId.Id > 0);
            long datasetsize = H5D.getStorageSize(dataSetId);
            Assert.AreEqual(SIZE * sizeof(int), datasetsize);

            // Read the integer data back from the data set
            int[] readDataBack = new int[SIZE];
            H5D.read(dataSetId, new H5DataTypeId(H5T.H5Type.NATIVE_INT),
                new H5Array<int>(readDataBack));
            for (int i = 0; i < SIZE; i++)
                Assert.AreEqual(SIZE - i, readDataBack[i]);

            // Read back the single-int example
            scalarId = H5D.open(fileId, "/scalarIntExample");
            Assert.IsTrue(scalarId.Id > 0);

            H5D.readScalar<int>(scalarId,
                new H5DataTypeId(H5T.H5Type.NATIVE_INT),
                ref singleValue);
            Assert.AreEqual(100, singleValue);

            H5D.close(dataSetId);
            H5D.close(scalarId);
            H5G.close(groupId);
            H5F.close(fileId);
        }
    }
}
