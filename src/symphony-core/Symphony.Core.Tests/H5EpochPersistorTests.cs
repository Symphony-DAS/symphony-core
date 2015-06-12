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
            var s = persistor.AddSource("first");
            var s2 = persistor.AddSource("second", s);
            persistor.AddSource("top");

            s.AddNote(DateTimeOffset.Now, "hello world!");
            s.AddNote(DateTimeOffset.Now, "what do you think of this?");

            s2.AddNote(DateTimeOffset.Now, "And this is another one on second");

            var x = persistor.Experiment;
            Assert.AreEqual(2, x.Sources.Count());

            persistor.BeginEpochGroup("one", s, DateTimeOffset.Now);
            var g = persistor.BeginEpochGroup("two", s, DateTimeOffset.Now);
            persistor.EndEpochGroup(DateTimeOffset.Now);

            persistor.Delete(s2);
            persistor.Delete(g);
            //persistor.BeginEpochGroup("three", s2, DateTimeOffset.Now);

            //persistor.Delete(s);
        }

    }
}
