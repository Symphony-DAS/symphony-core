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

            persistor = H5EpochPersistor.CreatePersistor(TEST_FILE, "my purpose", DateTimeOffset.Now);
        }

        [TearDown]
        public void Teardown()
        {
            persistor.Close(DateTimeOffset.Now);
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

            var x = persistor.Experiment;
            Assert.AreEqual(2, x.Sources.Count());

            persistor.BeginEpochGroup("one", s, DateTimeOffset.Now);
            persistor.EndEpochGroup(DateTimeOffset.Now);
            persistor.BeginEpochGroup("two", s, DateTimeOffset.Now);
            persistor.EndEpochGroup(DateTimeOffset.Now);
            persistor.BeginEpochGroup("three", s, DateTimeOffset.Now);
            persistor.EndEpochGroup(DateTimeOffset.Now);

            var groups = x.EpochGroups.ToList();
            groups.Sort((g1, g2) => g1.StartTime.CompareTo(g2.StartTime));

            var k = x.Keywords;
            Assert.AreEqual(0, x.Keywords.Count());

            persistor.Delete(s2);
        }

    }
}
