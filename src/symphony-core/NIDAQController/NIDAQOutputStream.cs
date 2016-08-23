using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.DAQmx;
using Symphony.Core;

namespace NI
{
    public class NIDAQOutputStream : DAQOutputStream, NIDAQStream
    {
        public const string DAQUnits = "NIDAQUnits";

        public NIDAQOutputStream(string name, PhysicalChannelTypes channelType, NIDAQController controller)
            : base(name.Split('/').Last(), controller)
        {
            PhysicalChannelType = channelType;
            MeasurementConversionTarget = (PhysicalChannelType == PhysicalChannelTypes.DIPort ||
                                           PhysicalChannelType == PhysicalChannelTypes.DILine)
                                              ? Measurement.UNITLESS
                                              : "V";
            Controller = controller;
            Clock = controller.Clock;
        }

        public override IDictionary<string, object> Configuration
        {
            get
            {
                var config = base.Configuration;
                config["SampleRate"] = SampleRate;

                return config;
            }
        }

        public string PhysicalName
        {
            get { return Controller.DeviceName + '/' + Name; }
        }

        public PhysicalChannelTypes PhysicalChannelType { get; private set; }

        private NIDAQController Controller { get; set; }

        public Channel GetChannel()
        {
            return Controller.Channel(PhysicalName);
        }

        public override IMeasurement SampleRate
        {
            get { return Controller.SampleRate; }
            set { throw new NotSupportedException("NIDAQOutputStream.SampleRate set by DAQController."); }
        }

        public override bool CanSetSampleRate
        {
            get { return false; }
        }

        /// <summary>
        /// Register ConversionProcs for V=>DAQUnits
        /// </summary>
        public static void RegisterConverters()
        {
            Converters.Register("V", DAQUnits, m => m);

            Converters.Register(Measurement.UNITLESS, DAQUnits, m => m);
        }
    }
}
