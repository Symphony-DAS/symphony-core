using System.Linq;
using Sprache;

namespace Symphony.Core
{
    using NUnit.Framework;
    using Symphony.Core.Properties;
    using System.Reflection;
    using System.IO;

    [TestFixture]
    public class ParserTests
    {
        [Test]
        public void ConstructFromPair()
        {
            string input = "Symphony.Core,Symphony.Core.Controller";
            object built = (new Parser().Construct(input));
            Assert.AreEqual("Symphony.Core.Controller", built.GetType().FullName);
        }
        [Test]
        public void ConstructFromPairWithParams()
        {
            string input = "Symphony.Core,Symphony.Core.DAQException";
            var msg = "TestException";
            object built = (new Parser().Construct(input, msg));
            Assert.AreEqual("Symphony.Core.DAQException", built.GetType().FullName);
            Assert.AreEqual(msg, (built as DAQException).Message);
        }
        [Test]
        public void ClockParse()
        {
            string expected = "Clock1";
            string input = "Clock \"" + expected + "\"";
            var clockCfg = Parser.ClockParser.Parse(input);
            Assert.AreEqual(expected, clockCfg.Name);
        }
        [Test]
        public void ClockProviderParse()
        {
            string expected = "ClockProvider";
            string input = "Provides Clock \"" + expected + "\"";
            var clockCfg = Parser.ClockProviderParser.Parse(input);
            Assert.AreEqual(expected, clockCfg.Name);

        }

        [Test]
        public void NumericValueParse()
        {
            string input = "123.5";
            var actual = Parser.NumericValue.Parse(input);
            Assert.AreEqual(decimal.Parse(input), actual);
        }

        [Test]
        public void SampleRateParse()
        {
            string input = "SampleRate 1000 Hz";
            const decimal value = 1000.0m;
            const string units = "Hz";
            var actual = Parser.SampleRateParser.Parse(input);
            Assert.AreEqual(new Measurement(value, units), actual.Value);
        }

        [Test]
        public void BindParse()
        {
            string input = "Bind \"In0\"";
            var bindCfg = Parser.BindParser.Parse(input);
            Assert.AreEqual("In0", bindCfg.Name);
        }
        [Test]
        public void ConnectParse()
        {
            string input = "Connect (\"In0\" \"In1\")";
            var connectCfg = Parser.ConnectParser.Parse(input);
            Assert.AreEqual(2, connectCfg.Values.Count());
            Assert.IsTrue(connectCfg.Values.Contains("In0"));
            Assert.IsTrue(connectCfg.Values.Contains("In1"));
        }
        [Test]
        public void ConfigurationParse()
        {
            string input =
                "Configuration" +
                "[" +
                    "key1 \"value1\"" +
                    "key2 \"value2\" key3 \"value3\"" +
                "]";
            var configCfg = Parser.ConfigParser.Parse(input);
            Assert.AreEqual(3, configCfg.ConfigValues.Count());
        }
        [Test]
        public void InputStreamParse()
        {
            string input =
                "InputStream \"In0\" \"Symphony.Core,Symphony.Core.DAQInputStream\"" +
                "[" +
                    "Clock \"CLOCK\"" +
                    "Configuration" +
                    "[" +
                        "key1 \"value1\"" +
                        "key2 \"value2\" key3 \"value3\"" +
                    "]" +
                    "SampleRate 1000 Hz" +
                "]";
            var streamCfg = Parser.StreamParser.Parse(input);
            Assert.AreEqual(3, streamCfg.Config.ConfigValues.Count());
            Assert.AreEqual("CLOCK", streamCfg.Clock);
            Assert.AreEqual(new Measurement(1000,"Hz"), streamCfg.SampleRate);
        }
        [Test]
        public void OutputStreamParse()
        {
            string input =
                "OutputStream \"Out0\" \"Symphony.Core,Symphony.Core.DAQOutputStream\"" +
                "[" +
                    "Clock \"CLOCK\"" +
                    "Configuration" +
                    "[" +
                        "key1 \"value1\"" +
                        "key2 \"value2\" key3 \"value3\"" +
                    "]" +
                    "SampleRate 1000 Hz" +
                "]";
            var streamCfg = Parser.StreamParser.Parse(input);
            Assert.AreEqual(3, streamCfg.Config.ConfigValues.Count());
            Assert.AreEqual("CLOCK", streamCfg.Clock);
            Assert.AreEqual(new Measurement(1000,"Hz"), streamCfg.SampleRate);
        }
        [Test]
        public void DeviceParse()
        {
            string input = Resources.ExternalDeviceBlock;

            var deviceCfg = Parser.DeviceParser.Parse(input);
            Assert.AreEqual(3, deviceCfg.Config.ConfigValues.Count());
            Assert.AreEqual(2, deviceCfg.Binds.Count());
            Assert.AreEqual(new Measurement(1, "V"), deviceCfg.Background);
            Assert.AreEqual("In0", deviceCfg.Binds.ToList()[0].Name);
            Assert.AreEqual("In1", deviceCfg.Binds.ToList()[1].Name);
            if (deviceCfg.Connects.Count() > 0)
                Assert.AreEqual(2, deviceCfg.Connects.First().Values.Count());
            Assert.AreEqual("CLOCK", deviceCfg.Clock);
        }
        [Test]
        public void DAQControllerParse()
        {
            string input = Resources.ControllerBlock;
            var controllerCfg = Parser.DAQControllerParser.Parse(input);
            Assert.AreEqual("HekkaClock", controllerCfg.Clock);
            Assert.AreEqual(2, controllerCfg.Streams.Count());

        }
        [Test]
        public void ControllerParse()
        {
            string input = Resources.CoreControllerBlock;
            var controllerCfg = Parser.ControllerParser.Parse(input);
            Assert.AreEqual("CLOCK", controllerCfg.Clock);
            Assert.IsNull(controllerCfg.ProvidesClock);

            Assert.AreEqual("Symphony.Core.Tests,Symphony.Core.SimpleDAQController", controllerCfg.DAQController.Type);
            Assert.AreEqual("CLOCK", controllerCfg.VideoController.Clock);
        }
        [Test]
        public void VideoControllerParse()
        {
            string input = Resources.VideoBlock;
            var cfg = Parser.VideoControllerParser.Parse(input);
            Assert.AreEqual(3, cfg.Config.ConfigValues.Count());
            Assert.AreEqual("CLOCK", cfg.Clock);
            Assert.AreEqual("CLOCK", cfg.ProvidesClock);
        }

        [Test]
        public void MinimalRigValidates()
        {
            // How do Converters get registered?
            Converters.Clear();
            Converters.Register("units", "units",
                // just an identity conversion for now, to pass Validate()
                (IMeasurement m) => m);
            Converters.Register("Hz", "Hz",
                // just an identity conversion for now, to pass Validate()
                (IMeasurement m) => m);

            Converters.Register("V", "units",
                // just an identity conversion for now, to pass Validate()
                (IMeasurement m) => m);

            ValidateObjectGraph(Resources.MinimalRigConfig);
        }

        private static void ValidateObjectGraph(string input)
        {
            var con = new Parser().ParseConfiguration(input);

            Assert.AreEqual(3, con.Devices.Count);

            var ledDevice = con.Devices.Where(ed => ed.Name == "LED").First();
            Assert.True(ledDevice.Streams.ContainsKey("Out0"));
            Assert.True(ledDevice.Streams["Out0"] is DAQOutputStream);
            Assert.AreEqual(new Measurement(1000, "Hz"), ledDevice.Streams["Out0"].SampleRate);



            // Validate and report the validation results
            Maybe<string> conVal = con.Validate();
            Assert.IsTrue(conVal, conVal);

            // These get assigned from Configuration at Validation
            Assert.AreEqual("units", ((UnitConvertingExternalDevice)ledDevice).MeasurementConversionTarget);
            Assert.AreEqual("units", ledDevice.Streams["Out0"].MeasurementConversionTarget);

            Assert.AreEqual(3, con.Devices.Count);

            foreach (ExternalDeviceBase ed in con.Devices)
            {
                if (ed.Name == "Amp")
                {
                    Assert.IsTrue(ed.Streams.Count() == 3);
                    Assert.IsTrue(ed is CoalescingDevice);
                    CoalescingDevice amp = (CoalescingDevice)ed;
                    Assert.IsTrue(amp.Coalesce == CoalescingDevice.OneItemCoalesce);
                }
                if (ed.Name == "LED")
                {
                    Assert.IsTrue(ed.Streams.Count() == 1);
                }
                if (ed.Name == "Temp")
                {
                    Assert.IsTrue(ed.Streams.Count() == 1);
                }
            }

            Assert.That(con.DAQController.Streams, Has.Count.GreaterThan(0));
            foreach (var s in con.DAQController.Streams)
            {
                //Tests that ApplyConfiguration is called
                Assert.That(s.MeasurementConversionTarget, Is.EqualTo("units"));
                Assert.That(s.Configuration["MeasurementConversionTarget"], Is.EqualTo("units"));
            }
        }

        //TODO uses additional (passed-in providers)
        //TODO allows optional VideoController (in ControllerParser)
    }
}
