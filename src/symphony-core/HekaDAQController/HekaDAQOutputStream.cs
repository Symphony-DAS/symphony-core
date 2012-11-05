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
    public sealed class HekaDAQOutputStream : DAQOutputStream, HekaDAQStream
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
            this.MeasurementConversionTarget = (ChannelType == StreamType.DIGITAL_OUT || ChannelType == StreamType.AUX_OUT)
                ? Measurement.UNITLESS : DAQCountUnits;
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
            set { throw new NotSupportedException("HekaDAQOutputStream.SampleRate set by DAQController."); }
        }

        /// <summary>
        /// Register ConversionProcs for V=>HekaDAQCounts
        /// </summary>
        public static void RegisterConverters()
        {
            Converters.Register("V",
                                DAQCountUnits,
                                (m) => new Measurement((decimal)Math.Round((double)m.QuantityInBaseUnit * ITCMM.ANALOGVOLT), DAQCountUnits)
                );

            Converters.Register(Measurement.UNITLESS,
                DAQCountUnits,
                (m) => m);
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
                                     SamplingRate = (double)SampleRate.QuantityInBaseUnit,
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
            var inputUnits = (ChannelType == StreamType.DIGITAL_OUT || ChannelType == StreamType.AUX_OUT)
                ? Measurement.UNITLESS : "V";

            var sampleData = data.DataWithUnits(inputUnits).DataWithUnits(DAQCountUnits);
            var samples = sampleData.Data.Select(m => (short)m.Quantity).ToList();

            device.PreloadSamples(ChannelType, ChannelNumber, samples);
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(HekaDAQOutputStream));
    }
}