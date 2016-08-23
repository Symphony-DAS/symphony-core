using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.DAQmx;
using Symphony.Core;

namespace NI
{
    /// <summary>
    /// IDAQInputStream implementation for the National Instruments DAQ hardware. Public
    /// only so that users can call RegisterConverters() until we've MEF-erized
    /// the unit conversion system.
    /// </summary>
    public class NIDAQInputStream : DAQInputStream, NIDAQStream
    {
        public const string DAQUnits = "NIDAQUnits";

        private NIDAQController Controller { get; set; }

        public PhysicalChannelTypes PhysicalChannelType { get; private set; }
        public string PhysicalName { get { return Controller.DeviceName + '/' + Name; } }
        
        public NIDAQInputStream(string name, PhysicalChannelTypes channelType, NIDAQController controller)
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

        public override IMeasurement SampleRate
        {
            get { return Controller.SampleRate; }
            set { throw new NotSupportedException("NIDAQInputStream.SampleRate set by DAQController."); }
        }

        public override bool CanSetSampleRate
        {
            get { return false; }
        }

        public Channel GetChannel()
        {
            return Controller.Channel(PhysicalName);
        }

        /// <summary>
        /// Register ConversionProcs for V=>DAQUnits
        /// </summary>
        public static void RegisterConverters()
        {
            Converters.Register(DAQUnits, "V", m => m);

            Converters.Register(DAQUnits, Measurement.UNITLESS, m => m);
        }
    }

    /// <summary>
    /// IDAQInputStream implementation for the National Instruments digital DAQ hardware.
    /// </summary>
    public class NIDigitalDAQInputStream : NIDAQInputStream, NIDigitalDAQStream
    {
        public IDictionary<IExternalDevice, ushort> BitPositions { get; private set; }

        public NIDigitalDAQInputStream(string name, NIDAQController controller)
            : base(name, PhysicalChannelTypes.DIPort, controller)
        {
            BitPositions = new Dictionary<IExternalDevice, ushort>();
        }

        public override IDictionary<string, object> Configuration
        {
            get
            {
                var config = base.Configuration;
                foreach (var ed in Devices)
                {
                    config[ed.Name + "_bitPosition"] = BitPositions[ed];
                }

                return config;
            }
        }

        public override void PushInputData(IInputData inData)
        {
            if (MeasurementConversionTarget == null)
                throw new DAQException("Input stream has null MeasurementConversionTarget");

            foreach (ExternalDeviceBase ed in Devices)
            {
                var data = inData.DataWithUnits(MeasurementConversionTarget);

                ushort bitPosition = BitPositions[ed];
                data = new InputData(data,
                                     data.Data.Select(
                                         m =>
                                         MeasurementPool.GetMeasurement(((short)m.QuantityInBaseUnits >> bitPosition) & 1, 0, Measurement.UNITLESS)));
                ed.PushInputData(this, data.DataWithStreamConfiguration(this, this.Configuration));
            }
        }

        public override Maybe<string> Validate()
        {
            if (Devices.Any(d => !BitPositions.ContainsKey(d)))
                return Maybe<string>.No("All devices must have an associated bit position");

            if (BitPositions.Values.Any(n => n >= 32))
                return Maybe<string>.No("No bit position can be greater than or equal to 16");

            return base.Validate();
        }
    }
}
