using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using log4net;

namespace Symphony.Core
{
    /// <summary>
    /// EpochPersistor subclass that writes Epochs to a single XML file.
    /// </summary>
    public class EpochXMLPersistor : EpochPersistor
    {
        private XmlWriter writer;


        private static readonly ILog log = LogManager.GetLogger(typeof(EpochXMLPersistor));

        private const string RootElementName = "experiment";
        private const string EpochGroupElementName = "epochGroup";

        /// <summary>
        /// Constructs a new EpochXMLPersistor writing to the given XmlWriter
        /// </summary>
        /// <param name="xw">XmlWriter to write persisted Epochs to</param>
        public EpochXMLPersistor(XmlWriter xw)
            : this(xw, Guid.NewGuid)
        {
        }

        public EpochXMLPersistor(XmlWriter xw, Func<Guid> guidGenerator)
            : base(guidGenerator)
        {
            this.writer = xw;
            writer.WriteStartDocument();
            writer.WriteStartElement(RootElementName);
            writer.WriteAttributeString("version", Version.ToString());
        }

        /// <summary>
        /// Constructs a new EpochXMLPersistor writing to an XML file
        /// at the given path. Creates the given file if none exists.
        /// </summary>
        /// <param name="path"></param>
        public EpochXMLPersistor(string path) :
            this(XmlWriter.Create(path))
        {
        }

        public override void CloseDocument()
        {
            writer.WriteEndElement(); //RootElementName
            writer.WriteEndDocument();

            writer.Flush();
            writer.Close();
        }

        protected override void WriteEpochGroupStart(string label, string source, string[] keywords, IDictionary<string, object> properties, Guid identifier, DateTimeOffset startTime, double timeZoneOffset)
        {
            writer.WriteStartElement(EpochGroupElementName);

            writer.WriteAttributeString("label", label);
            writer.WriteAttributeString("identifier", identifier.ToString());
            writer.WriteAttributeString("startTime", startTime.ToString());
            writer.WriteAttributeString("timeZoneUTCOffsetHours", timeZoneOffset.ToString());
            writer.WriteAttributeString("source", source);

            writer.WriteStartElement("keywords");
            foreach (string k in keywords)
            {
                writer.WriteStartElement("keyword");
                writer.WriteElementString("keyword", k);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("properties");
            foreach (var kv in properties)
            {
                writer.WriteStartElement("property");
                writer.WriteElementString(kv.Key, kv.Value.ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        protected override void WriteEpochGroupEnd(DateTimeOffset endTime)
        {
            writer.WriteEndElement(); //EpochGroupElementName
        }

        protected override void WriteEpochStart(Epoch e,
            string protocolID,
            Maybe<DateTimeOffset> startTime,
            string fileTag)
        {
            writer.WriteStartElement("epoch");
            writer.WriteAttributeString("protocolID", protocolID);
            writer.WriteAttributeString("UUID", GuidGenerator().ToString());

            if (startTime)
                writer.WriteAttributeString("startTime", ((DateTimeOffset)startTime).ToString());
            if (fileTag != null)
                writer.WriteAttributeString("fileTag", fileTag);
        }

        protected override void WriteKeywords(Epoch epoch, ISet<string> keywords)
        {
            writer.WriteStartElement("keywords");
            base.WriteKeywords(epoch, keywords);
            writer.WriteEndElement();
        }


        protected override void WriteBackground(Epoch e, IDictionary<IExternalDevice, Epoch.EpochBackground> background)
        {
            writer.WriteStartElement("background");
            base.WriteBackground(e, background);
            writer.WriteEndElement();
        }

        protected override void WriteKeyword(Epoch e, string keyword)
        {
            writer.WriteStartElement("keyword");
            writer.WriteAttributeString("tag", keyword);
            writer.WriteEndElement();
        }

        protected override void WriteBackgroundElement(Epoch e, IExternalDevice ed, Epoch.EpochBackground bg)
        {
            writer.WriteStartElement(ed.Name);

            writer.WriteStartElement("backgroundMeasurement");
            WriteMeasurement(bg.Background);
            writer.WriteEndElement();

            writer.WriteStartElement("sampleRate");
            WriteMeasurement(bg.SampleRate);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        protected override void WriteProtocolParams(Epoch e, IDictionary<string, object> protoParams)
        {
            WriteDictionary("protocolParameters", protoParams);
        }

        protected override void WriteStimuliStart(Epoch e)
        {
            writer.WriteStartElement("stimuli");
        }
        protected override void WriteStimulus(Epoch e, IExternalDevice ed, IStimulus s)
        {
            writer.WriteStartElement("stimulus");
            writer.WriteAttributeString("device", ed.Name);
            writer.WriteAttributeString("stimulusID", s.StimulusID);
            writer.WriteAttributeString("stimulusUnits", s.Units);

            WriteDictionary("parameters", s.Parameters);

            WriteStimulusConfigurationSpans(s);

            writer.WriteEndElement();
        }
        protected override void WriteStimuliEnd(Epoch e)
        {
            writer.WriteEndElement();
        }

        protected override void WriteResponsesStart(Epoch e)
        {
            writer.WriteStartElement("responses");
        }
        protected override void WriteResponse(Epoch e, IExternalDevice ed, Response r)
        {
            writer.WriteStartElement("response");
            writer.WriteAttributeString("device", ed.Name);

            writer.WriteElementString("inputTime", r.DataSegments.First().InputTime.ToUniversalTime().ToString());

            writer.WriteStartElement("sampleRate");
            WriteMeasurement(r.SampleRate);
            writer.WriteEndElement();

            WriteResponseConfigurationSpans(r);

            WriteResponseData(r);

            writer.WriteEndElement();
        }

        private void WriteResponseData(Response r)
        {
            writer.WriteStartElement("data");
            foreach (var d in r.Data)
                WriteMeasurement(d);
            writer.WriteEndElement();
        }

        private void WriteResponseConfigurationSpans(Response r)
        {
            WriteConfigurationSpans(r.DataConfigurationSpans);
        }

        private void WriteStimulusConfigurationSpans(IStimulus s)
        {
            WriteConfigurationSpans(s.OutputConfigurationSpans);
        }

        private void WriteConfigurationSpans(IEnumerable<IConfigurationSpan> configurationSpans)
        {
            var totalTime = TimeSpan.Zero;

            writer.WriteStartElement("nodeConfigurationSpans");
            foreach (var configSpan in configurationSpans)
            {
                writer.WriteStartElement("nodeConfigurationSpan");

                writer.WriteAttributeString("startTimeSeconds", totalTime.TotalSeconds.ToString());
                totalTime += configSpan.Time;

                writer.WriteAttributeString("timeSpanSeconds", configSpan.Time.TotalSeconds.ToString());
                foreach (var nodeConfig in configSpan.Nodes)
                {
                    WriteDictionary(nodeConfig.Name, nodeConfig.Configuration);
                }
                writer.WriteEndElement();

            }
            writer.WriteEndElement();
        }

        protected override void WriteResponsesEnd(Epoch e)
        {
            writer.WriteEndElement();
        }

        protected override void WriteEpochEnd(Epoch e)
        {
            writer.WriteEndElement();
            writer.Flush();
        }

        private void WriteMeasurement(IMeasurement m)
        {
            writer.WriteStartElement("measurement");
            writer.WriteAttributeString("qty", m.QuantityInBaseUnit.ToString());
            writer.WriteAttributeString("unit", m.BaseUnit);
            writer.WriteEndElement();
        }
        private void WriteDictionary(string dictName, IDictionary<string, object> d)
        {
            writer.WriteStartElement(dictName);

            foreach (var k in d.Keys)
            {
                var value = d[k] ?? "<null>";
                writer.WriteElementString(k, value.ToString());
            }

            writer.WriteEndElement();
        }
    }
}