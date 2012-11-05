using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ApprovalTests.Reporters;
using HDF5DotNet;
using NUnit.Framework;
using Approvals = ApprovalTests.Approvals;

namespace Symphony.Core
{

    [TestFixture]
    //[UseReporter(typeof(NUnitReporter), typeof(ClipboardReporter))]
    class EpochHDF5PersistorTests
    {

        [Test]
        public void ShouldDiallowIllegalCompression()
        {
            Assert.That(()=> new EpochHDF5Persistor("unused", null, 10), Throws.InstanceOf<ArgumentException>());    
        }

        [Test]
        public void ShouldCompressNumericData()
        {
            String uncompressedPath = "ShouldCompressNumericData_0.h5";
            String compressedPath = "ShouldCompressNumericData_9.h5";

            long uncompressedLength = 0;
            long compressedLength = 0;
            var gID = new Guid("{2F2719F7-4A8C-4C22-9698-BE999DBC5385}");
            try
            {
                using (var exp = new EpochHDF5Persistor(uncompressedPath, null, () => gID, 0))
                {
                    DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                    Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                    var props = new Dictionary<string, object>();
                    props["key1"] = "value1";
                    props["key2"] = 2;
                    exp.BeginEpochGroup("label", "source identifier", new[] { "keyword1", "keyword2" }, props, guid,
                                        time);
                    exp.Serialize(testEpoch);
                    exp.EndEpochGroup();
                    exp.Close();
                }

                uncompressedLength = new FileInfo(uncompressedPath).Length;
            }
            finally
            {
                if(File.Exists(uncompressedPath))
                    File.Delete(uncompressedPath);
            }

            gID = new Guid("{2F2719F7-4A8C-4C22-9698-BE999DBC5386}");
            try
            {
                using (var exp = new EpochHDF5Persistor(compressedPath, null, () => gID, 9))
                {
                    DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                    Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97E");
                    var props = new Dictionary<string, object>();
                    props["key1"] = "value1";
                    props["key2"] = 2;
                    exp.BeginEpochGroup("label", "source identifier", new[] { "keyword1", "keyword2" }, props, guid,
                                        time);
                    exp.Serialize(testEpoch);
                    exp.EndEpochGroup();
                    exp.Close();
                }

                compressedLength = new FileInfo(compressedPath).Length;
            }
            finally
            {
                if (File.Exists(compressedPath))
                    File.Delete(compressedPath);
            }

            Console.WriteLine("{0} => {1} bytes", uncompressedLength, compressedLength);
            Assert.That(compressedLength, Is.LessThan(uncompressedLength / 2));

        }

        [Test]
        //[Ignore]
        public void ShouldPersistToHDF5()
        {
            // Check and delete the HDF5 file here, NOT in TearDown, because we
            // want the file to exist after the test run (contrary to everything
            // unit-testing preaches) so that we can look at the file contents via
            // h5dump to verify what it looks like; unfortunately, at this time,
            // unit-testing HDF5 is a PITA (pain in the ass, for those of you
            // who are acronymatically challenged), so approval testing
            // is the next-best-thing we can do at the moment.


            if (File.Exists("..\\..\\..\\ShouldPersistToHDF5.h5"))
                File.Delete("..\\..\\..\\ShouldPersistToHDF5.h5");

            var gID = new Guid("{2F2719F7-4A8C-4C22-9698-BE999DBC5385}");
            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldPersistToHDF5.h5", null, () => gID))
            {
                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                var props = new Dictionary<string, object>();
                props["key1"] = "value1";
                props["key2"] = 2;
                exp.BeginEpochGroup("label", "source identifier", new[] { "keyword1", "keyword2" }, props, guid,
                                    time);
                exp.Serialize(testEpoch);
                exp.EndEpochGroup();
                exp.Close();
            }

            H5.Close();
            Approvals.VerifyFile("..\\..\\..\\ShouldPersistToHDF5.h5");

        }

        [Test]       
        public void ShouldAllowLongStringEpochParameters()
        {
            if (File.Exists("..\\..\\..\\ShouldAllowLongStringEpochParameters.h5"))
                File.Delete("..\\..\\..\\ShouldAllowLongStringEpochParameters.h5");

            var gID = new Guid("{2F2719F7-4A8C-4C22-9698-BE999DBC5385}");
            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldAllowLongStringEpochParameters.h5", null, () => gID))
            {

                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                var props = new Dictionary<string, object>();
                props["key1"] = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"; //100 elements
                props["key2"] = 2;

                const string protocolID = "Epoch.Fixture";
                var parameters = props;

                dev1 = new UnitConvertingExternalDevice(dev1Name, "DEVICECO", new Measurement(0, "V"));
                dev2 = new UnitConvertingExternalDevice(dev2Name, "DEVICECO", new Measurement(0, "V"));

                var stream1 = new DAQInputStream("Stream1");
                var stream2 = new DAQInputStream("Stream2");

                var stimParameters = new Dictionary<string, object>();
                stimParameters[param1] = value1;
                stimParameters[param2] = value2;

                var srate = new Measurement(1000, "Hz");

                var samples = Enumerable.Range(0, 10000).Select(i => new Measurement((decimal)Math.Sin((double)i / 100), "mV")).ToList();
                var stimData = new OutputData(samples, srate, false);

                var stim1 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted
                var stim2 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted

                var e = new Epoch(protocolID, parameters);
                e.Stimuli[dev1] = stim1;
                e.Stimuli[dev2] = stim2;

                var start = DateTimeOffset.Parse("1/11/2011 6:03:29 PM -08:00");
                // Do this to match the XML stored in the EpochXML.txt resource
                e.StartTime = Maybe<DateTimeOffset>.Yes(start);

                e.Background[dev1] = new Epoch.EpochBackground(new Measurement(0, "V"), new Measurement(1000, "Hz"));
                e.Background[dev2] = new Epoch.EpochBackground(new Measurement(1, "V"), new Measurement(1000, "Hz"));

                e.Responses[dev1] = new Response();
                e.Responses[dev2] = new Response();

                var streamConfig = new Dictionary<string, object>();
                streamConfig[param1] = value1;

                var devConfig = new Dictionary<string, object>();
                devConfig[param2] = value2;

                var responseData1 = new InputData(samples, srate, start)
                    .DataWithStreamConfiguration(stream1, streamConfig)
                    .DataWithExternalDeviceConfiguration(dev1, devConfig);
                var responseData2 = new InputData(samples, srate, start)
                    .DataWithStreamConfiguration(stream2, streamConfig)
                    .DataWithExternalDeviceConfiguration(dev2, devConfig);

                e.Responses[dev1].AppendData(responseData1);
                e.Responses[dev2].AppendData(responseData2);

                e.Keywords.Add(kw1);
                e.Keywords.Add(kw2);

                exp.BeginEpochGroup("label", "source identifier", new[] { "keyword1", "keyword2" }, props, guid,
                                    time);

                exp.Serialize(e);
                
                exp.EndEpochGroup(time.AddMilliseconds(100));
                exp.Close();
            }

            H5.Close();

            var startInfo = new ProcessStartInfo(@"..\..\..\..\..\..\externals\HDF5\bin\h5dump", @" --xml ..\..\..\ShouldAllowLongStringEpochParameters.h5");
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            var proc = Process.Start(startInfo);
            
            Approvals.VerifyXml(proc.StandardOutput.ReadToEnd());
        }

        [Test]
        public void ShouldAllowNumericEpochParameters()
        {
            if (File.Exists("..\\..\\..\\ShouldAllowNumericEpochParameters.h5"))
                File.Delete("..\\..\\..\\ShouldAllowNumericEpochParameters.h5");

            var gID = new Guid("{2F2719F7-4A8C-4C22-9698-BE999DBC5385}");
            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldAllowNumericEpochParameters.h5", null, () => gID))
            {
                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                var props = new Dictionary<string, object>();
                props["int"] = 2;
                props["float"] = 2.0f;
                props["double"] = 2.0d;
                props["decimal"] = 2.0m;
                props["array"] = new[] {1.0, 2.0, 3.0};
                props["short"] = (short) 2;
                props["unit16"] = (UInt16) 1;
                props["uint32"] = (UInt32) 2;
                props["byte"] = (byte) 1;
                props["bool"] = true;

                const string protocolID = "Epoch.Fixture";
                var parameters = props;

                dev1 = new UnitConvertingExternalDevice(dev1Name, "DEVICECO", new Measurement(0, "V"));
                dev2 = new UnitConvertingExternalDevice(dev2Name, "DEVICECO", new Measurement(0, "V"));

                var stream1 = new DAQInputStream("Stream1");
                var stream2 = new DAQInputStream("Stream2");

                var stimParameters = new Dictionary<string, object>();
                stimParameters[param1] = value1;
                stimParameters[param2] = value2;

                var srate = new Measurement(1000, "Hz");

                var samples = Enumerable.Range(0, 10000).Select(i => new Measurement((decimal)Math.Sin((double)i / 100), "V")).ToList();
                var stimData = new OutputData(samples, srate, false);

                var stim1 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted
                var stim2 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted

                var e = new Epoch(protocolID, parameters);
                e.Stimuli[dev1] = stim1;
                e.Stimuli[dev2] = stim2;

                var start = DateTimeOffset.Parse("1/11/2011 6:03:29 PM -08:00");
                // Do this to match the XML stored in the EpochXML.txt resource
                e.StartTime = Maybe<DateTimeOffset>.Yes(start);

                e.Background[dev1] = new Epoch.EpochBackground(new Measurement(0, "V"), new Measurement(1000, "Hz"));
                e.Background[dev2] = new Epoch.EpochBackground(new Measurement(1, "V"), new Measurement(1000, "Hz"));

                e.Responses[dev1] = new Response();
                e.Responses[dev2] = new Response();

                var streamConfig = new Dictionary<string, object>();
                streamConfig[param1] = value1;

                var devConfig = new Dictionary<string, object>();
                devConfig[param2] = value2;

                var responseData1 = new InputData(samples, srate, start)
                    .DataWithStreamConfiguration(stream1, streamConfig)
                    .DataWithExternalDeviceConfiguration(dev1, devConfig);
                var responseData2 = new InputData(samples, srate, start)
                    .DataWithStreamConfiguration(stream2, streamConfig)
                    .DataWithExternalDeviceConfiguration(dev2, devConfig);

                e.Responses[dev1].AppendData(responseData1);
                e.Responses[dev2].AppendData(responseData2);

                e.Keywords.Add(kw1);
                e.Keywords.Add(kw2);

                exp.BeginEpochGroup("label", "source identifier", new[] { "keyword1", "keyword2" }, props, guid,
                                    time);
                exp.Serialize(e);
                exp.EndEpochGroup();
                exp.Close();
            }

            H5.Close();
            Approvals.VerifyFile("..\\..\\..\\ShouldAllowNumericEpochParameters.h5");
        }

        [Test]
        public void ShouldAllowMultipleOpenPersistors()
        {
            if (File.Exists("..\\..\\..\\ShouldAllowMultipleOpenPersistors1.h5"))
                File.Delete("..\\..\\..\\ShouldAllowMultipleOpenPersistors1.h5");

            if (File.Exists("..\\..\\..\\ShouldAllowMultipleOpenPersistors2.h5"))
                File.Delete("..\\..\\..\\ShouldAllowMultipleOpenPersistors2.h5");

            var gID = new Guid("{FE5B733F-B3FB-4523-BDCC-CECB97D1CB0B}");
            using(var p1 = new EpochHDF5Persistor("..\\..\\..\\ShouldAllowMultipleOpenPersistors1.h5", null, () => gID))
            {
                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                Guid guid2 = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97E");

                p1.BeginEpochGroup("label1", "source1", new string[0], new Dictionary<string, object>(), guid,
                                    time);

                p1.BeginEpochGroup("label2", "source2", new string[0], new Dictionary<string, object>(), guid2,
                                    time);

                using (var p2 = new EpochHDF5Persistor("..\\..\\..\\ShouldAllowMultipleOpenPersistors2.h5", null, () => gID))
                {
                    p2.BeginEpochGroup("label1", "source1", new string[0], new Dictionary<string, object>(), guid,
                                    time);

                    p2.BeginEpochGroup("label2", "source2", new string[0], new Dictionary<string, object>(), guid2,
                                        time);

                    p2.Serialize(testEpoch);
                    p2.EndEpochGroup();
                    p2.EndEpochGroup();
                }

                p1.Serialize(testEpoch);
                p1.EndEpochGroup();
                p1.EndEpochGroup();
            }

            Approvals.VerifyFile("..\\..\\..\\ShouldAllowMultipleOpenPersistors1.h5");
            Approvals.VerifyFile("..\\..\\..\\ShouldAllowMultipleOpenPersistors2.h5");
        }

        [Test]
        public void ShouldThrowInvalidOperationExcpetionClosingEpochGroupWithoutOpenEpochGroup()
        {
            if (File.Exists("epochtest.h5"))
                File.Delete("epochtest.h5");

            using (var exp = new EpochHDF5Persistor("epochtest.h5", null, 9))
            {
                Assert.That(() => exp.EndEpochGroup(), Throws.InvalidOperationException);
            }

        }

        [Test]
        //[Ignore]
        public void ShouldAutomaticallyCloseOpenEpochGroupsOnPersistorClose()
        {
            if (File.Exists("..\\..\\..\\ShouldAutomaticallyCloseOpenEpochGroupsOnPersistorClose.h5"))
                File.Delete("..\\..\\..\\ShouldAutomaticallyCloseOpenEpochGroupsOnPersistorClose.h5");

            var gID = new Guid("{FE5B733F-B3FB-4523-BDCC-CECB97D1CB0B}");
            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldAutomaticallyCloseOpenEpochGroupsOnPersistorClose.h5", null, () => gID))
            {
                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                Guid guid2 = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97E");

                exp.BeginEpochGroup("label1", "source1", new string[0], new Dictionary<string, object>(), guid,
                                    time);

                exp.BeginEpochGroup("label2", "source2", new string[0], new Dictionary<string, object>(), guid2,
                                    time);

                exp.Serialize(testEpoch);
                exp.Close();
            }

            Approvals.VerifyFile("..\\..\\..\\ShouldAutomaticallyCloseOpenEpochGroupsOnPersistorClose.h5");
        }

        [Test]
        public void ShouldThrowExceptionIfNoOpenEpochGroup()
        {
            if (System.IO.File.Exists("..\\..\\..\\ShouldThrowExceptionIfNoOpenEpochGroup.h5"))
                System.IO.File.Delete("..\\..\\..\\ShouldThrowExceptionIfNoOpenEpochGroup.h5");

            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldThrowExceptionIfNoOpenEpochGroup.h5", null, 9))
            {
                Assert.That(() => exp.Serialize(testEpoch), Throws.InvalidOperationException);
            }
        }

        [Test]
        //[Ignore]
        public void ShouldAllowNestedEpochGroupsInHDF5()
        {
            if (File.Exists("..\\..\\..\\ShouldAllowNestedEpochGroupsInHDF5.h5"))
                File.Delete("..\\..\\..\\ShouldAllowNestedEpochGroupsInHDF5.h5");

            var gID = new Guid("{FE5B733F-B3FB-4523-BDCC-CECB97D1CB0B}");
            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldAllowNestedEpochGroupsInHDF5.h5", null, () => gID))
            {

                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                Guid guid2 = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97E");

                exp.BeginEpochGroup("label1", "source1", new string[0], new Dictionary<string, object>(), guid,
                                    time);

                exp.BeginEpochGroup("label2", "source2", new string[0], new Dictionary<string, object>(), guid2,
                                    time);

                exp.Serialize(testEpoch);
                exp.EndEpochGroup();
                exp.EndEpochGroup();
                exp.Close();
            }

            Approvals.VerifyFile("..\\..\\..\\ShouldAllowNestedEpochGroupsInHDF5.h5");
        }

        [Test]
        public void ShouldAppendToExistingFile()
        {
            if (File.Exists("..\\..\\..\\ShouldAppendToExistingFile.h5"))
                File.Delete("..\\..\\..\\ShouldAppendToExistingFile.h5");

            var gID = new Guid("{FE5B733F-B3FB-4523-BDCC-CECB97D1CB0B}");
            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldAppendToExistingFile.h5", null, () => gID))
            {
                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97D");
                Guid guid2 = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97E");

                exp.BeginEpochGroup("label1", "source1", new string[0], new Dictionary<string, object>(), guid,
                                    time);

                exp.BeginEpochGroup("label2", "source2", new string[0], new Dictionary<string, object>(), guid2,
                                    time);

                exp.Serialize(testEpoch);
                exp.EndEpochGroup();
                exp.EndEpochGroup();
            }

            using (var exp = new EpochHDF5Persistor("..\\..\\..\\ShouldAppendToExistingFile.h5", null, () => gID))
            {
                DateTimeOffset time = new DateTimeOffset(1000, TimeSpan.Zero);
                Guid guid = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97E");
                Guid guid2 = new Guid("053C64D4-ED7A-4128-AA43-ED115716A97F");

                exp.BeginEpochGroup("label1", "source1", new string[0], new Dictionary<string, object>(), guid,
                                    time);

                exp.BeginEpochGroup("label2", "source2", new string[0], new Dictionary<string, object>(), guid2,
                                    time);

                exp.Serialize(testEpoch);
                exp.EndEpochGroup();
                exp.EndEpochGroup();
            }

            Approvals.VerifyFile("..\\..\\..\\ShouldAppendToExistingFile.h5");
        }

        [Test]
        public void ShouldNotAppendToFileWithDifferentPersistenceVersion()
        {
            Assert.Fail("needs test");
        }


        [SetUp]
        public void Setup()
        {
            const string protocolID = "Epoch.Fixture";
            var parameters = new Dictionary<string, object>();
            parameters[param1] = value1;
            parameters[param2] = value2;

            dev1 = new UnitConvertingExternalDevice(dev1Name, "DEVICECO", new Measurement(0, "V"));
            dev2 = new UnitConvertingExternalDevice(dev2Name, "DEVICECO", new Measurement(0, "V"));

            var stream1 = new DAQInputStream("Stream1");
            var stream2 = new DAQInputStream("Stream2");

            var stimParameters = new Dictionary<string, object>();
            stimParameters[param1] = value1;
            stimParameters[param2] = value2;

            var srate = new Measurement(1000, "Hz");

            var samples = Enumerable.Range(0, 10000).Select(i => new Measurement((decimal)Math.Sin((double)i / 100), "V")).ToList();
            var stimData = new OutputData(samples, srate, false);

            RenderedStimulus stim1 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted
            RenderedStimulus stim2 = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) stimParameters, (IOutputData) stimData); //.Data does not need to be persisted

            Epoch e = new Epoch(protocolID, parameters);
            e.Stimuli[dev1] = stim1;
            e.Stimuli[dev2] = stim2;

            var start = DateTimeOffset.Parse("1/11/2011 6:03:29 PM -08:00");
            // Do this to match the XML stored in the EpochXML.txt resource
            e.StartTime = Maybe<DateTimeOffset>.Yes(start);

            e.Background[dev1] = new Epoch.EpochBackground(new Measurement(0, "V"), new Measurement(1000, "Hz"));
            e.Background[dev2] = new Epoch.EpochBackground(new Measurement(1, "V"), new Measurement(1000, "Hz"));

            e.Responses[dev1] = new Response();
            e.Responses[dev2] = new Response();

            var streamConfig = new Dictionary<string, object>();
            streamConfig[param1] = value1;

            var devConfig = new Dictionary<string, object>();
            devConfig[param2] = value2;

            var responseData1 = new InputData(samples, srate, start)
                .DataWithStreamConfiguration(stream1, streamConfig)
                .DataWithExternalDeviceConfiguration(dev1, devConfig);
            var responseData2 = new InputData(samples, srate, start)
                .DataWithStreamConfiguration(stream2, streamConfig)
                .DataWithExternalDeviceConfiguration(dev2, devConfig);

            e.Responses[dev1].AppendData(responseData1);
            e.Responses[dev2].AppendData(responseData2);

            e.Keywords.Add(kw1);
            e.Keywords.Add(kw2);

            testEpoch = e;
        }

        private Epoch testEpoch;
        ExternalDeviceBase dev1;
        ExternalDeviceBase dev2;
        const string dev1Name = "Device1";
        const string dev2Name = "Device2";
        const string param1 = "key1";
        const int value1 = 1;
        const string param2 = "key2";
        const string value2 = "value2";
        private const string kw1 = "kw1";
        private const string kw2 = "kw2";
    }
}
