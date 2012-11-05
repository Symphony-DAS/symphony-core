using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sprache;
using System.Xml;

namespace Symphony.Core
{
    /// <summary>
    /// Home-grown parser for Symphony config files. Beware, this is difficult to debug
    /// and parser error messages are basically worthless.
    /// </summary>
    public class Parser
    {
        public class NameValuePair
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }
        public class NameValuesSet
        {
            public string Name { get; set; }
            public IEnumerable<string> Values { get; set; }
        }
        public class Configuration
        {
            public IEnumerable<NameValuePair> ConfigValues { get; set; }
        }
        public class BindConfig
        {
            public string Name { get; set; }
        }
        public class ConnectConfig
        {
            public IEnumerable<string> Values { get; set; }
        }
        public class ITimelineProducerConfig
        {
            public string Clock { get; set; } //Provider name to use for timeline
            public string ProvidesClock { get; set; } //Name of this provider (optional)
        }
        public class DeviceConfig : ITimelineProducerConfig
        {
            public string Name { get; set; } // "AD0"
            public string Manufacturer { get; set; } // "Axon Instruments"
            public string Type { get; set; } // Assembly/Class name

            public IEnumerable<BindConfig> Binds { get; set; }
            public IEnumerable<ConnectConfig> Connects { get; set; }
            public Configuration Config { get; set; }
            public IMeasurement Background { get; set; }
        }
        public class BackgroundConfig
        {
            public IMeasurement Value { get; set; }
        }

        public class StreamConfig : ITimelineProducerConfig
        {
            public string Name { get; set; } // "In0"
            public string Type { get; set; } // Assembly/Class name

            public Configuration Config { get; set; }

            public IMeasurement SampleRate { get; set; }
        }
        public class DAQControllerConfig : ITimelineProducerConfig
        {
            public string Type { get; set; } // Assembly/Class name

            public Configuration Config { get; set; }
            public IEnumerable<DeviceConfig> Devices { get; set; }
            public IEnumerable<StreamConfig> Streams { get; set; }
        }
        public class VideoControllerConfig : ITimelineProducerConfig
        {
            public Configuration Config { get; set; }
        }
        public class ClockConfig
        {
            public string Name { get; set; }
        }
        public class ControllerConfig : ITimelineProducerConfig
        {
            public DAQControllerConfig DAQController { get; set; }
            public VideoControllerConfig VideoController { get; set; }
        }
        public class SampleRate
        {
            public IMeasurement Value { get; set; }
        }

        public static Parser<string> Identifier =
            from leading in Parse.WhiteSpace.Many()
            from first in Parse.Letter.Once()
            from rest in Parse.LetterOrDigit.Many()
            from trailing in Parse.WhiteSpace.Many()
            select new string(first.Concat(rest).ToArray());
        public static Parser<string> QuotedText =
            (from open in Parse.Char('"')
             from content in Parse.CharExcept('"').Many().Text()
             from close in Parse.Char('"')
             select content).Token();
        public static Parser<decimal> NumericValue =
            from value in Parse.Decimal.Token()
            select decimal.Parse(value);
        public static Parser<NameValuePair> NameValuePairParser =
            from name in Identifier
            from value in QuotedText
            select new NameValuePair() { Name = name, Value = value };
        public static Parser<BindConfig> BindParser =
            from _keyword in Parse.String("Bind")
            from name in QuotedText
            select new BindConfig() { Name = name };
        public static Parser<ClockConfig> ClockParser =
            from txt in Parse.String("Clock")
            from name in QuotedText
            select new ClockConfig() { Name = name };
        public static Parser<BackgroundConfig> BackgroundParser =
            from _label in Parse.String("Background")
            from value in NumericValue
            from units in Identifier
            select new BackgroundConfig { Value = new Measurement((decimal)value, units) };
        public static Parser<ClockConfig> ClockProviderParser =
            from txt in Parse.String("Provides Clock")
            from name in QuotedText
            select new ClockConfig() { Name = name };
        public static Parser<ConnectConfig> ConnectParser =
            from _keyword in Parse.String("Connect")
            from _lp in Parse.Char('(').Token()
            from values in QuotedText.Many()
            from _rp in Parse.Char(')').Token()
            select new ConnectConfig() { Values = values };
        public static Parser<Configuration> ConfigParser =
            from _keyword in Parse.String("Configuration")
            from _lb in Parse.Char('[').Token()
            from nameValuePairs in NameValuePairParser.Many()
            from _rb in Parse.Char(']').Token()
            select new Configuration() { ConfigValues = nameValuePairs };
        public static Parser<SampleRate> SampleRateParser =
            from _label in Parse.String("SampleRate")
            from value in NumericValue
            from unit in Identifier
            select new SampleRate { Value = new Measurement((decimal)value, unit) };

        public static Parser<StreamConfig> StreamParser =
            from _keyword in Parse.String("InputStream").Or(Parse.String("OutputStream"))
            from name in QuotedText
            from classType in QuotedText
            from lbracket in Parse.Char('[').Token()
            from clock in ClockParser
            from config in ConfigParser
            from srate in SampleRateParser
            from rbracket in Parse.Char(']').Token()
            select new StreamConfig()
            {
                Name = name,
                Type = classType,
                Config = config,
                Clock = clock.Name,
                SampleRate = srate.Value
            };
        public static Parser<DeviceConfig> DeviceParser =
            from _keyword in Parse.String("ExternalDevice")
            from name in QuotedText
            from manufacturer in QuotedText
            from classType in QuotedText
            from _lb in Parse.Char('[').Token()
            from clock in ClockParser
            from background in BackgroundParser
            from binds in BindParser.Many()
            from connects in ConnectParser.Many()
            from config in ConfigParser
            from _rb in Parse.Char(']').Token()
            select new DeviceConfig()
            {
                Name = name,
                Manufacturer = manufacturer,
                Type = classType,
                Binds = binds,
                Connects = connects,
                Config = config,
                Clock = clock.Name,
                Background = background.Value,
            };
        //TODO: devices should be at Controller[], not DAQController[] level
        public static Parser<DAQControllerConfig> DAQControllerParser =
            from _keyword in Parse.String("DAQController")
            from classType in QuotedText
            from _lb in Parse.Char('[').Token()
            from clockProvider in ClockProviderParser
            from clock in ClockParser
            from config in ConfigParser
            from streams in StreamParser.Many()
            from devices in DeviceParser.Many()
            from _rb in Parse.Char(']').Token()
            select new DAQControllerConfig()
            {
                Type = classType,
                Config = config,
                Streams = streams,
                Devices = devices,
                Clock = clock.Name,
                ProvidesClock = clockProvider.Name,
            };
        public static Parser<ControllerConfig> ControllerParser =
            from _keyword in Parse.String("Controller")
            from _lb in Parse.Char('[').Token()
            from clock in ClockParser
            from daq in DAQControllerParser
            from video in VideoControllerParser
            from _rb in Parse.Char(']').Token()
            select new ControllerConfig()
            {
                Clock = clock.Name,
                DAQController = daq,
                VideoController = video,
            };
        public static Parser<VideoControllerConfig> VideoControllerParser =
            from _keyword in Parse.String("VideoController")
            from _lb in Parse.Char('[').Token()
            from clockProvider in ClockProviderParser
            from clock in ClockParser
            from config in ConfigParser
            from _rb in Parse.Char(']').Token()
            select new VideoControllerConfig()
            {
                Clock = clock.Name,
                ProvidesClock = clockProvider.Name,
                Config = config
            };

        public Controller ParseConfiguration(string input)
        {
            return ParseConfiguration(input, new Dictionary<string, IClock>());
        }

        public Controller ParseConfigurationFile(string configFilePath)
        {
            return ParseConfiguration(File.OpenText(configFilePath).ReadToEnd());
        }

        static string ClockKey = "Clock";

        public Controller ParseConfiguration(string input, Dictionary<string, IClock> additionalClockProviders)
        {
            Controller controller = new Controller();
            var devices = new List<ExternalDeviceBase>();
            var streams = new List<IDAQStream>();
            var clockProviders = new Dictionary<string, IClock>(additionalClockProviders);

            var controllerConfig = ControllerParser.Parse(input);
            controller.Configuration[ClockKey] = controllerConfig.Clock;
            if (!string.IsNullOrEmpty(controllerConfig.ProvidesClock))
                clockProviders[controllerConfig.ProvidesClock] = controller as IClock;

            var daqControllerConfig = controllerConfig.DAQController;
            IDAQController daqController = (IDAQController)Construct(daqControllerConfig.Type);

            foreach (var nvp in daqControllerConfig.Config.ConfigValues)
            {
                var key = nvp.Name;
                var value = nvp.Value;
                uint uintValue;
                if (UInt32.TryParse(value, out uintValue))
                    daqController.Configuration[key] = uintValue;
                else
                    daqController.Configuration[key] = value;
            }

            daqController.Configuration.Add(new KeyValuePair<string, object>(ClockKey, daqControllerConfig.Clock));

            if (!string.IsNullOrEmpty(daqControllerConfig.ProvidesClock))
                clockProviders[daqControllerConfig.ProvidesClock] = daqController as IClock;

            daqController.BeginSetup();

            foreach (var s in daqControllerConfig.Streams)
            {
                IDAQStream stream;

                if (daqController.GetStreams(s.Name).Count() > 1)
                    throw new ParserException("More than one stream with name" + s.Name);

                //If we can find the named stream, use the one created by DAQController, otherwise we'll add it.
                if(daqController.GetStreams(s.Name).Count() == 0)
                    stream = (IDAQStream)Construct(s.Type, s.Name);
                else
                    stream = daqController.GetStreams(s.Name).First();

                foreach (var nvp in s.Config.ConfigValues)
                {
                    var key = nvp.Name;
                    var value = nvp.Value;
                    stream.Configuration.Add(new KeyValuePair<string, object>(key, value));
                }

                stream.Configuration.Add(new KeyValuePair<string, object>(ClockKey, s.Clock));
                try
                {
                    stream.SampleRate = s.SampleRate;
                }
                catch (NotSupportedException)
                {
                    //Heka streams don't support setting sample rate
                }

                streams.Add(stream);

            }

            foreach (var d in daqControllerConfig.Devices)
            {
                ExternalDeviceBase ed = (ExternalDeviceBase)(Construct(d.Type, d.Name, d.Manufacturer, controller, d.Background));

                foreach (var nvp in d.Config.ConfigValues)
                {
                    var key = nvp.Name;
                    var value = nvp.Value;
                    ed.Configuration.Add(new KeyValuePair<string, object>(key, value));
                }

                ed.Configuration.Add(new KeyValuePair<string, object>(ClockKey, d.Clock));

                foreach (var bindCfg in d.Binds)
                {
                    IDAQStream st = streams.Find((s) => s.Name == bindCfg.Name);
                    if (st == null)
                        throw new Exception(String.Format("Attempting to bind {0} which does not exist in configuration", bindCfg.Name));
                    else
                    {
                        if (st is IDAQInputStream)
                            ed.BindStream((IDAQInputStream)st);
                        else if (st is IDAQOutputStream)
                            ed.BindStream((IDAQOutputStream)st);
                        else
                            throw new Exception(String.Format("Attempting to bind {0} which is neither an Input or Output stream", bindCfg.Name));
                    }
                }

                foreach (var connectCfg in d.Connects)
                {
                    if (ed is CoalescingDevice)
                        ((CoalescingDevice)ed).Connect(connectCfg.Values.ToArray());
                    else
                        throw new Exception(
                            String.Format("Attempting to connect {0} to a non-CoalescingDevice {1}", connectCfg.Values.ToString(), ed));
                }

                devices.Add(ed);
            }


            foreach (IDAQStream s in streams)
            {
                //if the stream came from DAQController.GetStreams, we don't need to add it
                if (!daqController.Streams.Contains(s) && (daqController as IMutableDAQController != null))
                    (daqController as IMutableDAQController).AddStream(s);
            }


            daqController.Clock = clockProviders[daqControllerConfig.Clock] as IClock;
            InjectClock(controller, clockProviders);

            controller.DAQController = daqController;


            return controller;
        }

        private void InjectClock(Controller controller, Dictionary<string, IClock> clockProviders)
        {
            controller.Clock = clockProviders[controller.Configuration[ClockKey] as string];
            foreach (var dev in controller.Devices)
            {
                dev.Clock = clockProviders[dev.Configuration[ClockKey] as string];
                foreach (var stream in dev.Streams.Values)
                {
                    stream.Clock = clockProviders[stream.Configuration[ClockKey] as string];
                }
            }
        }

        public object Construct(string assmClassPair, params object[] ctorParams)
        {
            string assemblyName = "Symphony.Core";
            string className = assmClassPair;
            if (assmClassPair.Contains(','))
            {
                assemblyName = assmClassPair.Split(',')[0];
                className = assmClassPair.Split(',')[1];
            }

            Assembly assm = assemblyName != "Symphony.Core" ?
                Assembly.Load(assemblyName) :
                Assembly.GetExecutingAssembly();

            Type classType = assm.GetType(className);
            Type[] ctorTypeParams =
                ctorParams.Select((o) => o.GetType()).ToArray();
            ConstructorInfo defCtor = classType.GetConstructor(ctorTypeParams);
            return defCtor.Invoke(ctorParams);
        }
    }
}
