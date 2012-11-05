using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using ApprovalTests;
using ApprovalTests.Reporters;

namespace Symphony.Core
{
    using NUnit.Framework;


    [TestFixture]
    class EpochPersistorTests
    {

        [Test]
        public void ShouldProvidePersistenceVersion()
        {
            Assert.That(EpochPersistor.Version, Is.EqualTo(1));
        }

        [Test]
        //[UseReporter(typeof(DiffReporter))]
        public void ShouldCloseEnclosingEpochGroups()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            XmlWriter sxw = XmlWriter.Create(sb);
            EpochXMLPersistor exp = new EpochXMLPersistor(sxw);

            const string label1 = "label1";
            const string label2 = "label2";
            Guid g1ID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306f3");
            Guid g2ID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306f4");
            var startTime1 = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));
            var startTime2 = new DateTimeOffset(2011, 8, 22, 11, 15, 0, 0, TimeSpan.FromHours(-6));

            exp.BeginEpochGroup(label1, "", new string[0], new Dictionary<string, object>(), g1ID, startTime1);

            exp.BeginEpochGroup(label2, "source", new string[0], new Dictionary<string, object>(), g2ID, startTime2);
            exp.EndEpochGroup();

            exp.EndEpochGroup();

            exp.Close();

            Approvals.VerifyXml(sb.ToString());
        }

        [Test]
        //[UseReporter(typeof(DiffReporter))]
        public void ShouldAllowNestedEpochGroupsInXML()
        {
            StringBuilder sb = new System.Text.StringBuilder();
            var sxw = XmlWriter.Create(sb);
            var exp = new EpochXMLPersistor(sxw);

            const string label1 = "label1";
            const string label2 = "label2";
            var g1ID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306f3");
            var g2ID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306f4");
            var startTime1 = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));
            var startTime2 = new DateTimeOffset(2011, 8, 22, 11, 15, 0, 0, TimeSpan.FromHours(-6));

            exp.BeginEpochGroup(label1, "", new string[0], new Dictionary<string, object>(), g1ID, startTime1);

            exp.BeginEpochGroup(label2, "source", new string[0], new Dictionary<string, object>(), g2ID, startTime2);
            exp.EndEpochGroup();

            exp.Close();

            Approvals.VerifyXml(sb.ToString());
        }

        [Test]
        public void ShouldWriteEpochGroupMetadata()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            XmlWriter sxw = XmlWriter.Create(sb);
            EpochXMLPersistor exp = new EpochXMLPersistor(sxw);

            string label = "EpochGroup_label";
            string source = "source";
            string[] keywords = new string[] { "keyword1", "keyword2" };
            Dictionary<string, object> properties = new Dictionary<string, object> { { "key1", "value1" }, { "key2", 12 } };
            Guid guid = Guid.NewGuid();
            exp.BeginEpochGroup(label, source, keywords, properties, guid, DateTimeOffset.Now);
            exp.EndEpochGroup();
            exp.Close();

            string xml = sb.ToString();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlElement egElement = (XmlElement)doc.DocumentElement.ChildNodes[0];

            Assert.That(egElement.Name, Is.EqualTo("epochGroup"));

            Assert.That(egElement.Attributes["source"].Value, Is.EqualTo(source));

            Assert.DoesNotThrow(() => DateTimeOffset.Parse(egElement.Attributes["startTime"].InnerText));

            var actualStart = DateTimeOffset.Parse(egElement.Attributes["startTime"].InnerText);
            Assert.That(actualStart.UtcDateTime.Ticks, Is.EqualTo(actualStart.Ticks));

            Assert.That(egElement.Attributes["timeZoneUTCOffsetHours"].InnerText, Is.EqualTo(
                TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalHours.ToString()));
        }

        [Test]
        //[UseReporter(typeof(DiffReporter))]
        public void ShouldAllowMultipleEpochGroupsInXML()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            XmlWriter sxw = XmlWriter.Create(sb);
            EpochXMLPersistor exp = new EpochXMLPersistor(sxw);

            const string label1 = "label1";
            const string label2 = "label2";
            Guid g1ID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306f3");
            Guid g2ID = new Guid("a5839fe9-90ef-4e39-bf26-8f75048306f4");
            var startTime1 = new DateTimeOffset(2011, 8, 22, 11, 12, 0, 0, TimeSpan.FromHours(-6));
            var startTime2 = new DateTimeOffset(2011, 8, 22, 11, 15, 0, 0, TimeSpan.FromHours(-6));

            exp.BeginEpochGroup(label1, "", new string[0], new Dictionary<string, object>(), g1ID, startTime1);
            exp.EndEpochGroup();

            exp.BeginEpochGroup(label2, "source", new string[0], new Dictionary<string, object>(), g2ID, startTime2);
            exp.EndEpochGroup();

            exp.Close();

            Approvals.VerifyXml(sb.ToString());
        }

        [Test]
        public void EpochXmlFixture()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            XmlWriter sxw = XmlWriter.Create(sb);
            EpochXMLPersistor exp = new EpochXMLPersistor(sxw);
            exp.BeginEpochGroup("", "", new string[0], new Dictionary<string, object>(), Guid.NewGuid(), DateTimeOffset.Now);
            exp.Serialize(testEpoch);
            exp.EndEpochGroup();
            exp.Close();

            string xml = sb.ToString();
            //Console.WriteLine("XML = {0}", xml);
            //Assert.AreEqual(xml, (Symphony.Core.Properties.Resources.EpochXML).Trim());
            // This is a bit of a fragile unit test--if the file's XML is pretty-printed, for example,
            // the CR/LF will be in the string coming from the file and cause this test to fail.
            // Comment out the test if that seems too fragile. (It does to me, but this also a good
            // way to red-flag unexpected XML changes.)

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlElement epochEl = doc["experiment"]["epochGroup"]["epoch"];
            Assert.AreEqual("epoch", epochEl.Name);
            Assert.AreEqual(testEpoch.ProtocolID, epochEl.GetAttribute("protocolID"));

            var kwsEl = (XmlElement)epochEl.GetElementsByTagName("keywords")[0];
            Assert.That(kwsEl.ChildNodes.Count == 2);

            var keywords = new HashSet<string>();
            for (int i = 0; i < kwsEl.ChildNodes.Count; i++)
            {
                var kwEl = kwsEl.ChildNodes[i];
                keywords.Add(kwEl.Attributes["tag"].Value);
            }

            Assert.That(keywords, Has.Member(kw1));
            Assert.That(keywords, Has.Member(kw2));


            Assert.AreEqual(1, epochEl.GetElementsByTagName("background").Count);
            XmlElement backgroundEl = (XmlElement)epochEl.GetElementsByTagName("background")[0];

            // There should be 2 devices in the Background
            Assert.AreEqual(2, backgroundEl.ChildNodes.Count);
            bool device1Found = false;
            bool device2Found = false;
            XmlNodeList backgroundList = backgroundEl.ChildNodes;
            for (int i = 0; i < backgroundList.Count; i++)
            {
                XmlElement backgroundChild = (XmlElement)backgroundList[i];
                if (backgroundChild.Name == dev1Name)
                {
                    device1Found = true;
                    bool bgFound = false;
                    bool srFound = false;
                    for (int j = 0; j < backgroundChild.ChildNodes.Count; j++)
                    {
                        XmlElement m = (XmlElement)backgroundChild.ChildNodes[j];
                        if (m.Name == "backgroundMeasurement")
                        {
                            XmlElement b = (XmlElement)m.ChildNodes[0];
                            Assert.That(b.GetAttribute("qty"), Is.EqualTo("0"));
                            Assert.That(b.GetAttribute("unit"), Is.EqualTo("V"));
                            bgFound = true;
                        }
                        else if (m.Name == "sampleRate")
                        {
                            XmlElement sr = (XmlElement)m.ChildNodes[0];
                            Assert.That(sr.GetAttribute("unit"), Is.EqualTo("Hz"));
                            srFound = true;
                        }
                        else
                        {
                            Assert.Fail("Unexpected background element name {0}", m.Name);
                        }

                    }

                    Assert.That(bgFound && srFound);

                }
                if (backgroundChild.Name == dev2Name)
                {
                    device2Found = true;
                    bool bgFound = false;
                    bool srFound = false;
                    for (int j = 0; j < backgroundChild.ChildNodes.Count; j++)
                    {
                        XmlElement m = (XmlElement)backgroundChild.ChildNodes[j];
                        XmlElement c = (XmlElement)m.ChildNodes[0];
                        if (m.Name == "backgroundMeasurement")
                        {
                            Assert.That(c.GetAttribute("qty"), Is.EqualTo("1"));
                            Assert.That(c.GetAttribute("unit"), Is.EqualTo("V"));
                            bgFound = true;
                        }
                        else if (m.Name == "sampleRate")
                        {
                            Assert.That(c.GetAttribute("unit") == "Hz");
                            srFound = true;
                        }
                        else
                        {
                            Assert.Fail("Unexpected background element name {0}", m.Name);
                        }

                    }
                    Assert.That(bgFound && srFound);
                }
            }
            Assert.IsTrue(device1Found);
            Assert.IsTrue(device2Found);

            Assert.AreEqual(1, epochEl.GetElementsByTagName("protocolParameters").Count);
            XmlElement protoParamsEl = (XmlElement)epochEl.GetElementsByTagName("protocolParameters")[0];

            // There should be 2 key/value pairs in protocol parameters
            Assert.AreEqual(2, protoParamsEl.ChildNodes.Count); // for key1/value1 and key2/value2
            for (int i = 0; i < protoParamsEl.ChildNodes.Count; i++)
            {
                XmlElement proParamChild = (XmlElement)protoParamsEl.ChildNodes[i];
                if (proParamChild.Name == param1)
                    Assert.AreEqual(value1.ToString(), proParamChild.InnerText);
                if (proParamChild.Name == param2)
                    Assert.AreEqual(value2, proParamChild.InnerText);
            }

            // still need to verify stimuli and responses; leave that as a TODO, in favor
            // of getting HDF5 work started in the meantime. --TKN
        }

        Epoch testEpoch;
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

            var samples = Enumerable.Range(0, 1000).Select(i => new Measurement((decimal)Math.Sin((double)i / 100), "V")).ToList();
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
    }
}
