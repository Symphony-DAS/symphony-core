using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Heka.NativeInterop;
using log4net;
using Symphony.Core;

namespace Heka
{
    /// <summary>
    /// IDAQOutputStream implementation for the Heka/Instrutech DAQ hardware. Public
    /// only so that users can call RegisterConverters() until we've MEF-erized
    /// the unit conversion system.
    /// </summary>
    public class HekaDAQOutputStream : DAQOutputStream, HekaDAQStream
    {
        private HekaDAQController Controller { get; set; }

        public const string DAQCountUnits = "HekaDAQCounts";

        public StreamType ChannelType { get; private set; } //should be internal, but testing needs access
        public ushort ChannelNumber { get; private set; } //should be internal, but testing needs access


        public HekaDAQOutputStream(string name, StreamType streamType, ushort channelNumber, HekaDAQController controller)
            : base(name, controller)
        {
            this.ChannelType = streamType;
            this.ChannelNumber = channelNumber;
            this.MeasurementConversionTarget = (ChannelType == StreamType.DO_PORT || ChannelType == StreamType.XO)
                ? Measurement.UNITLESS : DAQCountUnits;
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
            set { throw new NotSupportedException("HekaDAQOutputStream.SampleRate set by DAQController."); }
        }

        public override bool CanSetSampleRate
        {
            get { return false; }
        }

        /// <summary>
        /// Register ConversionProcs for V=>HekaDAQCounts
        /// </summary>
        public static void RegisterConverters()
        {
            Converters.Register("V",
                                DAQCountUnits,
                                (m) => MeasurementPool.GetMeasurement((decimal)Math.Round((double)m.QuantityInBaseUnits * ITCMM.ANALOGVOLT), 0, DAQCountUnits)
                );

            Converters.Register(Measurement.UNITLESS,
                DAQCountUnits,
                (m) => m);

            Converters.Register(Measurement.NORMALIZED,
                DAQCountUnits,
                (m) => MeasurementPool.GetMeasurement(m.QuantityInBaseUnits < 0 ? 
                    Math.Round(m.QuantityInBaseUnits * (decimal)ITCMM.NEGATIVEVOLT * -(decimal)ITCMM.ANALOGVOLT) : 
                    Math.Round(m.QuantityInBaseUnits * (decimal)ITCMM.POSITIVEVOLT * +(decimal)ITCMM.ANALOGVOLT), 
                    0, DAQCountUnits));
        }
        public ITCMM.ITCChannelInfo ChannelInfo
        {
            get
            {
                var result = new ITCMM.ITCChannelInfo
                                 {
                                     ChannelNumber = ChannelNumber,
                                     ChannelType = (uint)ChannelType,
                                     SamplingIntervalFlag = ITCMM.USE_FREQUENCY,
                                     SamplingRate = (double)SampleRate.QuantityInBaseUnits,
                                     Gain = 0, //Gain = 1x
                                     FIFOPointer = IntPtr.Zero,
                                 };

                return result;
            }
        }

        public void Preload(IHekaDevice device, IOutputData data)
        {
            PreloadData(device, data);
        }

        void PreloadData(IHekaDevice device, IOutputData data)
        {
            var inputUnits = (ChannelType == StreamType.DO_PORT || ChannelType == StreamType.XO)
                ? Measurement.UNITLESS : "V";

            var sampleData = data.DataWithUnits(inputUnits).DataWithUnits(DAQCountUnits);
            var samples = sampleData.Data.Select(m => (short)m.Quantity).ToList();

            device.PreloadSamples(ChannelType, ChannelNumber, samples);
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(HekaDAQOutputStream));
    }

    /// <summary>
    /// IDAQOutputStream implementation for the Heka/Instrutech digital DAQ streams.
    /// </summary>
    public class HekaDigitalDAQOutputStream : HekaDAQOutputStream, HekaDigitalDAQStream
    {
        public IDictionary<IExternalDevice, ushort> BitPositions { get; private set; }

        public HekaDigitalDAQOutputStream(string name, ushort channelNumber, HekaDAQController controller) 
            : base(name, StreamType.DO_PORT, channelNumber, controller)
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
                    m = MeasurementPool.GetMeasurement((long)((long)m.QuantityInBaseUnits << bitPosition), 0, m.BaseUnits);

                    background = background == null
                        ? m
                        : MeasurementPool.GetMeasurement((long)background.QuantityInBaseUnits | (long)m.QuantityInBaseUnits, 0, background.BaseUnits);
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

                    return MeasurementPool.GetMeasurement((long)((long)m.QuantityInBaseUnits << bitPosition), 0, m.BaseUnits);
                }));

                outData = outData == null
                              ? pulled
                              : outData.Zip(pulled,
                                            (m1, m2) =>
                                            MeasurementPool.GetMeasurement((long) m1.QuantityInBaseUnits | (long) m2.QuantityInBaseUnits, 0, m1.BaseUnits));
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

            if (BitPositions.Values.Any(n => n >= 16))
                return Maybe<string>.No("No bit position can be greater than or equal to 16");

            return base.Validate();
        }
    }
}