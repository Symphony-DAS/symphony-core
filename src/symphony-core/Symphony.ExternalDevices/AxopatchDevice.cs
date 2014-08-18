using System;
using System.Collections.Generic;
using System.Linq;
using Symphony.Core;
using log4net;

namespace Symphony.ExternalDevices
{

    public sealed class AxopatchDevice : ExternalDeviceBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof (AxopatchDevice));

        private IDictionary<AxopatchInterop.OperatingMode, IMeasurement> Backgrounds { get; set; }

        private IAxopatch Axopatch { get; set; }

        public const string SCALED_OUTPUT_STREAM_NAME = "SCALED_OUTPUT";
        public const string CAPACITANCE_TELEGRAPH_STREAM_NAME = "CAPACITANCE_TELEGRAPH";
        public const string FREQUENCY_TELEGRAPH_STREAM_NAME = "FREQUENCY_TELEGRAPH";
        public const string GAIN_TELEGRAPH_STREAM_NAME = "GAIN_TELEGRAPH";
        public const string MODE_TELEGRAPH_STREAM_NAME = "MODE_TELEGRAPH";

        public AxopatchDevice(IAxopatch axopatch, Controller c, IDictionary<AxopatchInterop.OperatingMode, IMeasurement> background)
            : base("Axopatch", "Molecular Devices", c)
        {
            Axopatch = axopatch;

            c.Started += (sender, args) =>
                {
                    DeviceParameters = ReadDeviceParameters();
                };

            Backgrounds = background;
        }

        private readonly IDictionary<IDAQInputStream, IList<IInputData>> _queues = new Dictionary<IDAQInputStream, IList<IInputData>>();

        public override ExternalDeviceBase BindStream(string name, IDAQInputStream inputStream)
        {
            _queues.Add(inputStream, new List<IInputData>());
            return base.BindStream(name, inputStream);
        }

        public override void UnbindStream(string name)
        {
            if (Streams.ContainsKey(name) && Streams[name] is IDAQInputStream && _queues.ContainsKey((IDAQInputStream) Streams[name]))
            {
                _queues.Remove((IDAQInputStream)Streams[name]);
            }
            base.UnbindStream(name);
        }

        private static IDictionary<string, object> MergeDeviceParametersIntoConfiguration(
            IDictionary<string, object> config,
            AxopatchInterop.AxopatchData deviceParameters)
        {
            var result = config == null
                             ? new Dictionary<string, object>()
                             : new Dictionary<string, object>(config);

            result["Capacitance"] = deviceParameters.Capacitance;
            result["ExternalCommandSensitivity"] = deviceParameters.ExternalCommandSensitivity;
            result["ExternalCommandSensitivityUnits"] = deviceParameters.ExternalCommandSensitivityUnits.ToString();
            result["Frequency"] = deviceParameters.Frequency;
            result["Gain"] = deviceParameters.Gain;
            result["OperatingMode"] = deviceParameters.OperatingMode.ToString();

            return result;
        }

        private AxopatchInterop.AxopatchData DeviceParameters { get; set; }

        public AxopatchInterop.AxopatchData CurrentDeviceParameters
        {
            get 
            { 
                var devParams = ReadDeviceParameters();
                DeviceParameters = devParams;
                return devParams;
            }
        }

        private AxopatchInterop.AxopatchData ReadDeviceParameters()
        {
            IDictionary<string, IInputData> data = new Dictionary<string, IInputData>();

            var inStreams = InputStreams;
            foreach (var stream in inStreams)
            {
                string name = Streams.FirstOrDefault(x => x.Value == stream).Key;
                data[name] = stream.Read();
            }

            return Axopatch.ReadTelegraphData(data);
        }

        public override IMeasurement Background
        {
            get
            {
                var bg = Backgrounds[CurrentDeviceParameters.OperatingMode];
                return bg;
            }
            set 
            { 
                Backgrounds[CurrentDeviceParameters.OperatingMode] = value;
            }
        }

        public static IMeasurement ConvertInput(IMeasurement sample, AxopatchInterop.AxopatchData deviceParams)
        {
            return MeasurementPool.GetMeasurement(
                sample.QuantityInBaseUnit/(decimal) deviceParams.Gain,
                InputUnitsExponentForMode(deviceParams.OperatingMode),
                InputUnitsForMode(deviceParams.OperatingMode));
        }

        private static int InputUnitsExponentForMode(AxopatchInterop.OperatingMode mode)
        {
            switch (mode)
            {
                case AxopatchInterop.OperatingMode.Track:
                case AxopatchInterop.OperatingMode.VClamp:
                    return -12; //pA
                case AxopatchInterop.OperatingMode.I0:
                case AxopatchInterop.OperatingMode.IClampNormal:
                case AxopatchInterop.OperatingMode.IClampFast:
                    return -3; //mV
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }
        }

        private static string InputUnitsForMode(AxopatchInterop.OperatingMode mode)
        {
            switch (mode)
            {
                case AxopatchInterop.OperatingMode.Track:
                case AxopatchInterop.OperatingMode.VClamp:
                    return "A";
                case AxopatchInterop.OperatingMode.I0:
                case AxopatchInterop.OperatingMode.IClampNormal:
                case AxopatchInterop.OperatingMode.IClampFast:
                    return "V";
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }
        }

        protected override IMeasurement ConvertOutput(IMeasurement deviceOutput)
        {
            return ConvertOutput(deviceOutput, CurrentDeviceParameters);
        }

        public static IMeasurement ConvertOutput(IMeasurement sample, AxopatchInterop.AxopatchData deviceParams)
        {
            switch (deviceParams.OperatingMode)
            {
                case AxopatchInterop.OperatingMode.Track:
                case AxopatchInterop.OperatingMode.VClamp:
                    if (String.CompareOrdinal(sample.BaseUnit, "V") != 0)
                    {
                        throw new ArgumentException("Sample units must be in Volts.", "sample");
                    }

                    if (deviceParams.ExternalCommandSensitivityUnits != AxopatchInterop.ExternalCommandSensitivityUnits.V_V)
                    {
                        throw new AxopatchDeviceException("External command units are not V/V as expected for current device mode.");
                    }
                    break;
                case AxopatchInterop.OperatingMode.I0:
                case AxopatchInterop.OperatingMode.IClampNormal:
                case AxopatchInterop.OperatingMode.IClampFast:
                    if (String.CompareOrdinal(sample.BaseUnit, "A") != 0)
                    {
                        throw new ArgumentException("Sample units must be in Amps.", "sample");
                    }

                    if (deviceParams.ExternalCommandSensitivityUnits != AxopatchInterop.ExternalCommandSensitivityUnits.A_V)
                    {
                        throw new AxopatchDeviceException("External command units are not A/V as expected for current device mode.");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            IMeasurement result;

            if (deviceParams.OperatingMode == AxopatchInterop.OperatingMode.I0 || deviceParams.OperatingMode == AxopatchInterop.OperatingMode.Track)
            {
                result = MeasurementPool.GetMeasurement(0, 0, "V");
            }
            else
            {
                result = (decimal)deviceParams.ExternalCommandSensitivity == 0
                             ? MeasurementPool.GetMeasurement(sample.Quantity, sample.Exponent, "V")
                             : MeasurementPool.GetMeasurement(sample.Quantity / (decimal)deviceParams.ExternalCommandSensitivity,
                                                  sample.Exponent, "V");
            }

            return result;
        }

        public override IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration)
        {
            try
            {
                var deviceParameters = DeviceParameters;

                var config = MergeDeviceParametersIntoConfiguration(Configuration, deviceParameters);

                IOutputData data = this.Controller.PullOutputData(this, duration);

                return
                    data.DataWithConversion(m => ConvertOutput(m, deviceParameters))
                        .DataWithExternalDeviceConfiguration(this, config);
            }
            catch (Exception ex)
            {
                log.DebugFormat("Error pulling data from controller: {0} ({1})", ex.Message, ex);
                throw;
            }
        }

        public override void PushInputData(IDAQInputStream stream, IInputData inData)
        {
            _queues[stream].Add(inData);
            if (_queues.Values.Any(dataList => dataList.Count == 0))
            {
                return;
            }

            try
            {
                IDictionary<string, IInputData> data = new Dictionary<string, IInputData>();
                foreach (var dataList in _queues)
                {
                    string streamName = Streams.FirstOrDefault(x => x.Value == dataList.Key).Key;
                    data.Add(streamName, dataList.Value[0]);
                    dataList.Value.RemoveAt(0);
                }

                var deviceParameters = Axopatch.ReadTelegraphData(data);
                DeviceParameters = deviceParameters;

                IInputData scaledData = data[SCALED_OUTPUT_STREAM_NAME];
                IInputData convertedData = scaledData.DataWithConversion(m => ConvertInput(m, deviceParameters));

                var config = MergeDeviceParametersIntoConfiguration(Configuration, deviceParameters);

                this.Controller.PushInputData(this, convertedData.DataWithExternalDeviceConfiguration(this, config));
            }
            catch (Exception ex)
            {
                log.DebugFormat("Error pushing data to controller: {0} ({1})", ex.Message, ex);
                throw;
            }
        }

    }

    public class AxopatchDeviceException : ExternalDeviceException
    {
        public AxopatchDeviceException(string message)
            : base(message)
        {
        }
    }
}
