using System;
using System.Linq;
using System.Collections.Generic;
using Heka.NativeInterop;
using Symphony.Core;

namespace Heka
{
    /// <summary>
    /// IDAQInputStream implementation for the Heka/Instrutech DAQ hardware. Public
    /// only so that users can call RegisterConverters() until we've MEF-erized
    /// the unit conversion system.
    /// </summary>
    public class HekaDAQInputStream : DAQInputStream, HekaDAQStream
    {
        public const string DAQCountUnits = "HekaDAQCounts";

        private HekaDAQController Controller { get; set; }

        public StreamType ChannelType { get; private set; } //should be internal, but testing needs access
        public ushort ChannelNumber { get; private set; } //should be internal, but testing needs access

        public HekaDAQInputStream(string name, StreamType streamType, ushort channelNumber, HekaDAQController controller)
            : base(name, controller)
        {
            this.ChannelType = streamType;
            this.ChannelNumber = channelNumber;
            this.MeasurementConversionTarget = (ChannelType == StreamType.DIGITAL_IN || ChannelType == StreamType.AUX_IN) 
                ? Measurement.UNITLESS : "V";
            this.Controller = controller;
            this.Clock = controller.Clock;
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
            get { return this.Controller.SampleRate; }
            set { throw new NotSupportedException("HekaDAQInputStream.SampleRate set by DAQController."); }
        }

        public override bool CanSetSampleRate
        {
            get { return false; }
        }

        public ITCMM.ITCChannelInfo ChannelInfo
        {
            get
            {
                ITCMM.ITCChannelInfo result = new ITCMM.ITCChannelInfo();
                result.ChannelNumber = ChannelNumber;
                result.ChannelType = (uint)ChannelType;
                result.SamplingIntervalFlag = ITCMM.USE_FREQUENCY;// &ITCMM.NO_SCALE & ITCMM.ADJUST_RATE;
                result.SamplingRate = (double) this.SampleRate.QuantityInBaseUnits;
                result.Gain = 0;
                result.FIFOPointer = System.IntPtr.Zero;

                return result;
            }
        }

        public static void RegisterConverters()
        {
            Converters.Register(DAQCountUnits,
                                "V",
                                (m) => MeasurementPool.GetMeasurement(m.QuantityInBaseUnits / (decimal)ITCMM.ANALOGVOLT, 0, "V")
                );

            Converters.Register(DAQCountUnits,
                Measurement.UNITLESS,
                (m) => m);

            Converters.Register(DAQCountUnits,
                Measurement.NORMALIZED,
                (m) => MeasurementPool.GetMeasurement(m.QuantityInBaseUnits < 0 ?
                    m.QuantityInBaseUnits / (decimal)ITCMM.NEGATIVEVOLT / -(decimal)ITCMM.ANALOGVOLT :
                    m.QuantityInBaseUnits / (decimal)ITCMM.POSITIVEVOLT / +(decimal)ITCMM.ANALOGVOLT,
                    0, Measurement.NORMALIZED));
        }
    }

    /// <summary>
    /// IDAQInputStream implementation for the Heka/Instrutech digital DAQ hardware.
    /// </summary>
    public class HekaDigitalDAQInputStream : HekaDAQInputStream, HekaDigitalDAQStream
    {
        public IDictionary<IExternalDevice, ushort> BitPositions { get; private set; }

        public HekaDigitalDAQInputStream(string name, ushort channelNumber, HekaDAQController controller) 
            : base(name, StreamType.DIGITAL_IN, channelNumber, controller)
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
                                         MeasurementPool.GetMeasurement(((long) m.QuantityInBaseUnits >> bitPosition) & 1, 0, Measurement.UNITLESS)));
                ed.PushInputData(this, data.DataWithStreamConfiguration(this, this.Configuration));
            }
        }

        public override Maybe<string> Validate()
        {
            if (Devices.Any(d => !BitPositions.ContainsKey(d)))
                return Maybe<string>.No("All devices must have an associated bit position");

            if (BitPositions.Values.Any(n => n >= 16))
                return Maybe<string>.No("No bit position can be greater than or equal to 16");

            return base.Validate();
        }
    }
}