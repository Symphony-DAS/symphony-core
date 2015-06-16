using System;
using System.Collections.Generic;
using System.Linq;
using HDF5DotNet;
using NUnit.Framework;

namespace Symphony.Core
{
    class H5EpochPersistorTests
    {
        const string TEST_FILE = "myCSharp.h5";

        private H5EpochPersistor persistor;

        [SetUp]
        public void Setup()
        {
            if (System.IO.File.Exists(TEST_FILE))
            {
                System.IO.File.Delete(TEST_FILE);
            }

            persistor = new H5EpochPersistor(TEST_FILE);
        }

        [TearDown]
        public void Teardown()
        {
            persistor.Close();
        }

        [Test]
        public void Test()
        {
            var s = persistor.AddSource("first", null);
            var s2 = persistor.AddSource("second", s);
            persistor.AddSource("top", null);

            s.AddNote(DateTimeOffset.Now, "hello world!");
            s.AddNote(DateTimeOffset.Now, "what do you think of this?");

            s2.AddNote(DateTimeOffset.Now, "And this is another one on second");

            Assert.AreEqual(2, persistor.Sources.Count());

            persistor.BeginExperiment("my purpose here", DateTimeOffset.Now);
            persistor.BeginEpochGroup("one", s, DateTimeOffset.Now);
            persistor.BeginEpochGroup("two", s, DateTimeOffset.Now);

            persistor.Delete(s2);

            persistor.Close();
            //persistor.BeginEpochGroup("three", s2, DateTimeOffset.Now);

            //persistor.Delete(s);
        }

    }
}
