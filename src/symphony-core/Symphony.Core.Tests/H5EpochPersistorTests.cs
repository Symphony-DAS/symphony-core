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
        const string TEST_PURPOSE = "the test purpose here";

        private H5EpochPersistor persistor;
        private DateTimeOffset startTime;
        private DateTimeOffset endTime;

        [SetUp]
        public void Setup()
        {
            if (System.IO.File.Exists(TEST_FILE))
                System.IO.File.Delete(TEST_FILE);

            startTime = new DateTimeOffset(2015, 6, 9, 10, 30, 50, TimeSpan.Zero);
            endTime = startTime.AddMinutes(55);

            persistor = H5EpochPersistor.CreatePersistor(TEST_FILE, TEST_PURPOSE, startTime);
        }

        [TearDown]
        public void Teardown()
        {
            try
            {
                persistor.Close(endTime);
            }
            finally
            {
                //if (System.IO.File.Exists(TEST_FILE))
                //    System.IO.File.Delete(TEST_FILE);
            }
        }

        [Test]
        public void ShouldContainExperiment()
        {
            var experiment = persistor.Experiment;
            Assert.AreEqual(TEST_PURPOSE, experiment.Purpose);
            Assert.AreEqual(startTime, experiment.StartTime);
            Assert.IsNull(experiment.EndTime);
            Assert.AreEqual(0, experiment.Devices.Count());
            Assert.AreEqual(0, experiment.Sources.Count());
            Assert.AreEqual(0, experiment.EpochGroups.Count());
        }

        [Test]
        public void ShouldAddAndRemoveProperties()
        {
            var expected = new Dictionary<string, object>();

            var experiment = persistor.Experiment;

            for (int i = 0; i < 10; i++)
            {
                experiment.AddProperty(i.ToString(), i);
                expected.Add(i.ToString(), i);
            }
            CollectionAssert.AreEquivalent(expected, experiment.Properties);

            for (int i = 0; i < 5; i++)
            {
                experiment.RemoveProperty(i.ToString());
                expected.Remove(i.ToString());
            }
            CollectionAssert.AreEquivalent(expected, experiment.Properties);

            persistor.Close(endTime);
            persistor = new H5EpochPersistor(TEST_FILE);
            experiment = persistor.Experiment;

            CollectionAssert.AreEquivalent(expected, experiment.Properties);

            foreach (var key in expected.Keys.ToList())
            {
                experiment.RemoveProperty(key);
                expected.Remove(key);
            }
            CollectionAssert.AreEquivalent(expected, experiment.Properties);
        }

        [Test]
        public void ShouldAddAndRemoveKeywords()
        {
            var expected = new HashSet<string>();

            var experiment = persistor.Experiment;

            for (int i = 0; i < 10; i++)
            {
                experiment.AddKeyword(i.ToString());
                expected.Add(i.ToString());
            }
            CollectionAssert.AreEquivalent(expected, experiment.Keywords);

            for (int i = 0; i < 5; i++)
            {
                experiment.RemoveKeyword(i.ToString());
                expected.Remove(i.ToString());
            }
            CollectionAssert.AreEquivalent(expected, experiment.Keywords);

            persistor.Close(endTime);
            persistor = new H5EpochPersistor(TEST_FILE);
            experiment = persistor.Experiment;

            CollectionAssert.AreEquivalent(expected, experiment.Keywords);

            foreach (var word in expected.ToList())
            {
                experiment.RemoveKeyword(word);
                expected.Remove(word);
            }
            CollectionAssert.AreEquivalent(expected, experiment.Keywords);
        }

        [Test]
        public void ShouldAddNotes()
        {
            var expected = new List<INote>();

            var experiment = persistor.Experiment;

            for (int i = 0; i < 100; i++)
            {
                DateTimeOffset time = DateTimeOffset.Now;
                string text = "This is note number " + i;

                var n = experiment.AddNote(time, "This is note number " + i);
                Assert.AreEqual(time, n.Time);
                Assert.AreEqual(text, n.Text);

                expected.Add(n);
            }
            CollectionAssert.AreEquivalent(expected, experiment.Notes);

            persistor.Close(endTime);
            persistor = new H5EpochPersistor(TEST_FILE);
            experiment = persistor.Experiment;

            CollectionAssert.AreEquivalent(expected, experiment.Notes);
        }

        [Test]
        public void ShouldAddAndDeleteDevices()
        {
            var expected = new HashSet<IPersistentDevice>();

            for (int i = 0; i < 10; i++)
            {
                string name = "dev" + i;
                string manufacturer = "man" + i;

                var d = persistor.AddDevice(name, manufacturer);
                Assert.AreEqual(name, d.Name);
                Assert.AreEqual(manufacturer, d.Manufacturer);

                expected.Add(d);
            }
            CollectionAssert.AreEquivalent(expected, persistor.Experiment.Devices);

            for (int i = 0; i < 5; i++)
            {
                var d = expected.First();
                persistor.Delete(d);
                expected.Remove(d);
            }
            CollectionAssert.AreEquivalent(expected, persistor.Experiment.Devices);

            persistor.Close(endTime);
            persistor = new H5EpochPersistor(TEST_FILE);

            CollectionAssert.AreEquivalent(expected, persistor.Experiment.Devices);
        }

        [Test]
        public void ShouldNotAllowAddingDuplicateDevices()
        {
            const string name = "Device";
            const string manufacturer = "Manufacturer";

            persistor.AddDevice(name, manufacturer);
            Assert.Throws(typeof(ArgumentException), () => persistor.AddDevice(name, manufacturer));
        }

        [Test]
        public void ShouldAddSources()
        {

        }
    }
}
