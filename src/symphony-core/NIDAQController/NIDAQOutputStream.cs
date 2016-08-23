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
    /// IDAQOutputStream implementation for the National Instruments DAQ hardware. Public
    /// only so that users can call RegisterConverters() until we've MEF-erized
    /// the unit conversion system.
    /// </summary>
    public class NIDAQOutputStream : DAQOutputStream, NIDAQStream
    {
        private NIDAQController Controller { get; set; }

        public string PhysicalName { get; private set; }
        public PhysicalChannelTypes PhysicalChannelType { get; private set; }

        public NIDAQOutputStream(string physicalName, PhysicalChannelTypes channelType, NIDAQController controller)
            : this(physicalName, physicalName, channelType, controller)
        {
        }

        public NIDAQOutputStream(string name, string physicalName, PhysicalChannelTypes channelType,
                                 NIDAQController controller)
            : base(name, controller)
        {
            PhysicalName = physicalName;
            PhysicalChannelType = channelType;
            DAQUnits = (PhysicalChannelType == PhysicalChannelTypes.DOPort ||
                        PhysicalChannelType == PhysicalChannelTypes.DOLine)
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
            set { throw new NotSupportedException("NIDAQOutputStream.SampleRate set by DAQController."); }
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
    /// IDAQOutputStream implementation for the National Instruments digital DAQ streams.
    /// </summary>
    public class NIDigitalDAQOutputStream : NIDAQOutputStream, NIDigitalDAQStream
    {
        public IDictionary<IExternalDevice, ushort> BitPositions { get; private set; }

        public NIDigitalDAQOutputStream(string physicalName, NIDAQController controller)
            : this(physicalName, physicalName, controller)
        {
        }

        public NIDigitalDAQOutputStream(string name, string physicalName, NIDAQController controller)
            : base(name, physicalName, PhysicalChannelTypes.DOPort, controller)
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

        public override IMeasurement Background
        {
            get
            {
                IMeasurement background = null;
                foreach (var ed in Devices)
                {
                    var m = Converters.Convert(ed.Background, MeasurementConversionTarget);
                    if (m.QuantityInBaseUnits != 0 && m.QuantityInBaseUnits != 1)
                        throw new DAQException(ed.Name + " background must contain a value of 0 or 1");

                    ushort bitPosition = BitPositions[ed];
                    m = MeasurementPool.GetMeasurement((short)((short)m.QuantityInBaseUnits << bitPosition), 0, m.BaseUnits);

                    background = background == null
                        ? m
                        : MeasurementPool.GetMeasurement((short)background.QuantityInBaseUnits | (short)m.QuantityInBaseUnits, 0, background.BaseUnits);
                }
                return background;
            }
        }

        public override IOutputData PullOutputData(TimeSpan duration)
        {
            if (!Devices.Any())
                throw new DAQException("No bound external devices (check configuration)");

            IOutputData outData = null;
            foreach (var ed in Devices)
            {
                var pulled = ed.PullOutputData(this, duration).DataWithUnits(MeasurementConversionTarget);

                ushort bitPosition = BitPositions[ed];
                pulled = new OutputData(pulled, pulled.Data.Select(m =>
                {
                    if (m.QuantityInBaseUnits != 0 && m.QuantityInBaseUnits != 1)
                        throw new DAQException(ed.Name + " output data must contain only values of 0 and 1");

                    return MeasurementPool.GetMeasurement((short)((short)m.QuantityInBaseUnits << bitPosition), 0, m.BaseUnits);
                }));

                outData = outData == null
                              ? pulled
                              : outData.Zip(pulled,
                                            (m1, m2) =>
                                            MeasurementPool.GetMeasurement((short)m1.QuantityInBaseUnits | (short)m2.QuantityInBaseUnits, 0, m1.BaseUnits));
            }

            if (!outData.SampleRate.Equals(this.SampleRate))
                throw new DAQException("Sample rate mismatch.");

            if (outData.IsLast)
                LastDataPulled = true;

            return outData.DataWithStreamConfiguration(this, this.Configuration);
        }

        public override Maybe<string> Validate()
        {
            if (Devices.Any(d => !BitPositions.ContainsKey(d)))
                return Maybe<string>.No("All devices must have an associated bit position");

            if (BitPositions.Values.Any(n => n >= 32))
                return Maybe<string>.No("No bit position can be greater than or equal to 32");

            return base.Validate();
        }
    }
}
