using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Symphony.Core;

namespace NI
{
    public class NIDAQOutputStream : DAQOutputStream, NIDAQStream
    {
        private NIDAQController Controller { get; set; }

        public StreamType ChannelType { get; private set; }

        public NIDAQOutputStream(string name, StreamType streamType, NIDAQController controller)
            : base(name.Split('/').Last(), controller)
        {
            ChannelType = streamType;
            MeasurementConversionTarget = (ChannelType == StreamType.DIGITAL_OUT) ? Measurement.UNITLESS : "V";
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

        public string FullName
        {
            get { return Controller.DeviceName + '/' + Name; }
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
    }
}
