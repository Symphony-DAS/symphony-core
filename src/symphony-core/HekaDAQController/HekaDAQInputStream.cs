using System;
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
    public sealed class HekaDAQInputStream : DAQInputStream, HekaDAQStream
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
            this.Clock = controller;
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

        public ITCMM.ITCChannelInfo ChannelInfo
        {
            get
            {
                ITCMM.ITCChannelInfo result = new ITCMM.ITCChannelInfo();
                result.ChannelNumber = ChannelNumber;
                result.ChannelType = (uint)ChannelType;
                result.SamplingIntervalFlag = ITCMM.USE_FREQUENCY;// &ITCMM.NO_SCALE & ITCMM.ADJUST_RATE;
                result.SamplingRate = (double) this.SampleRate.QuantityInBaseUnit;
                result.Gain = 0;
                result.FIFOPointer = System.IntPtr.Zero;

                return result;
            }
        }

        public static void RegisterConverters()
        {
            Converters.Register(DAQCountUnits,
                                "V",
                                (m) => new Measurement(m.QuantityInBaseUnit / (decimal) ITCMM.ANALOGVOLT, "V")
                );

            Converters.Register(DAQCountUnits,
                Measurement.UNITLESS,
                (m) => m);
        }
    }
}