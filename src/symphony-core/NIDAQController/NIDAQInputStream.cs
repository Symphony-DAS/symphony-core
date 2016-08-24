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
        private NIDAQController Controller { get; set; }

        public string PhysicalName { get; private set; }
        public PhysicalChannelTypes PhysicalChannelType { get; private set; }

        public NIDAQInputStream(string physicalName, PhysicalChannelTypes channelType, NIDAQController controller)
            : this(physicalName, physicalName, channelType, controller)
        {
        }

        public NIDAQInputStream(string name, string physicalName, PhysicalChannelTypes channelType,
                                NIDAQController controller)
            : base(name, controller)
        {
            PhysicalName = physicalName;
            PhysicalChannelType = channelType;
            DAQUnits = (PhysicalChannelType == PhysicalChannelTypes.DIPort ||
                        PhysicalChannelType == PhysicalChannelTypes.DILine)
                           ? Measurement.UNITLESS
                           : "V";
            MeasurementConversionTarget = DAQUnits;
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

        public string DAQUnits { get; private set; }
    }

    /// <summary>
    /// IDAQInputStream implementation for the National Instruments digital DAQ hardware.
    /// </summary>
    public class NIDigitalDAQInputStream : NIDAQInputStream, NIDigitalDAQStream
    {
        public IDictionary<IExternalDevice, ushort> BitPositions { get; private set; }

        public NIDigitalDAQInputStream(string physicalName, NIDAQController controller)
            : this(physicalName, physicalName, controller)
        {
        }

        public NIDigitalDAQInputStream(string name, string physicalName, NIDAQController controller)
            : base(name, physicalName, PhysicalChannelTypes.DIPort, controller)
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
                                         MeasurementPool.GetMeasurement(((int)m.QuantityInBaseUnits >> bitPosition) & 1, 0, Measurement.UNITLESS)));
                ed.PushInputData(this, data.DataWithStreamConfiguration(this, this.Configuration));
            }
        }

        public override Maybe<string> Validate()
        {
            if (Devices.Any(d => !BitPositions.ContainsKey(d)))
                return Maybe<string>.No("All devices must have an associated bit position");

            var width = DaqSystem.Local.LoadPhysicalChannel(PhysicalName).DIPortWidth;
            if (BitPositions.Values.Any(n => n >= width))
                return Maybe<string>.No("No bit position can be greater than or equal to " + width);

            return base.Validate();
        }
    }
}
