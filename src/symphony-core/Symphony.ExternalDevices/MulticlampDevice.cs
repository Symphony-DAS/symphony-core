﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Symphony.Core;

namespace Symphony.ExternalDevices
{

    /// <summary>
    /// ExternalDevice implementation for MultiClamp 700[A,B] device.
    /// 
    /// Spec:
    /// Should use a unit conversion proc that uses device parameters to convert incoming/outgoing units
    /// Given a queue of parameters updates
    ///     Should use the most recent parameter update to calculate unit conversion for IOutputData
    /// Given a queue of paramteter updates
    ///     Should use the parameter update most recent but preceeding (in time) the input data InputTime to calculate unit conversion of IInputData
    ///     Should discard parameter updates older than the used parameter update
    /// </summary>
    public sealed class MultiClampDevice : ExternalDeviceBase, IDisposable
    {

        private static readonly ILog log = LogManager.GetLogger(typeof(MultiClampDevice));

        private const int MEMBRANE_CURRENT_INPUT_EXPONENT = -12; //pA
        private const int MEMBRANE_VOLTAGE_INPUT_EXPONENT = -3; //mV

        private IDictionary<MultiClampInterop.OperatingMode, IMeasurement> Backgrounds { get; set; }

        /// <summary>
        /// Constructs a new MultiClampDevice
        /// </summary>
        /// <param name="commander">MultiClampCommander instance</param>
        /// <param name="c">Symphony Controller instance</param>
        /// <param name="background">Dictionary of background Measurements for each MultiClamp operating mode</param>
        public MultiClampDevice(IMultiClampCommander commander, Controller c, IDictionary<MultiClampInterop.OperatingMode, IMeasurement> background)
            : base("Multiclamp-" + commander.SerialNumber + "-" + commander.Channel, "Molecular Devices", c)
        {
            InputParameters = new ConcurrentDictionary<DateTimeOffset, MultiClampParametersChangedArgs>();
            OutputParameters = new ConcurrentDictionary<DateTimeOffset, MultiClampParametersChangedArgs>();

            Commander = commander;
            Commander.ParametersChanged += (sender, mdArgs) =>
                {
                    lock (_bindLock)
                    {
                        log.DebugFormat(
                            "MultiClamp parameters changed. Mode = {0}, External Command Sensistivity Units = {1} Timestamp = {2}",
                            mdArgs.Data.OperatingMode,
                            mdArgs.Data.ExternalCommandSensitivityUnits,
                            mdArgs.TimeStamp);

                        if (!HasBoundInputStream)
                            InputParameters.Clear();
                            
                        InputParameters[mdArgs.TimeStamp.ToUniversalTime()] = mdArgs;

                        if (!HasBoundOutputStream)
                            OutputParameters.Clear();

                        OutputParameters[mdArgs.TimeStamp.ToUniversalTime()] = mdArgs;

                        foreach (var outputStream in OutputStreams.Where(s => s.DAQ != null && s.DAQ.IsHardwareReady && !s.DAQ.IsRunning))
                        {
                            log.DebugFormat("Setting new background for stream {0}", outputStream.Name);
                            try
                            {
                                outputStream.ApplyBackground();
                            }
                            catch (Exception x)
                            {
                                log.ErrorFormat("Error applying new background: {0}", x);
                                throw;
                            }
                            
                        }
                    }
                };
            Commander.RequestTelegraphValue();

            Backgrounds = background;
        }

        /// <summary>
        /// Constructs a new 700B MultiClampDevice
        /// </summary>
        /// <param name="serialNumber">MultiClamp serial number</param>
        /// <param name="channel">MultiClamp channel</param>
        /// <param name="clock">Clock instance defining cononical time</param>
        /// <param name="c">Controller instance for this device</param>
        /// <param name="backgroundModes">Enumerable of operating modes</param>
        /// <param name="backgroundMeasurements">Corresponding background Measurement for each operating mode in backgroundModes</param>
        public MultiClampDevice(uint serialNumber,
            uint channel,
            IClock clock,
            Controller c,
            IEnumerable<MultiClampInterop.OperatingMode> backgroundModes,
            IEnumerable<IMeasurement> backgroundMeasurements)
            : this(new MultiClampCommander(serialNumber, channel, clock),
            c,
            backgroundModes.Zip(backgroundMeasurements,
            (k, v) => new { Key = k, Value = v })
            .ToDictionary(x => x.Key, x => x.Value))
        {
            this.Clock = clock;
        }

        /// <summary>
        /// Constructs a new 700B MultiClampDevice.
        /// 
        /// This constructor is provided as a convenience for Matlab clients. .Net clients should use the typed version.
        /// </summary>
        /// <param name="serialNumber">MultiClamp serial number</param>
        /// <param name="channel">MultiClamp channel</param>
        /// <param name="clock">Clock instance defining cononical time</param>
        /// <param name="c">Controller instance for this device</param>
        /// <param name="backgroundModes">Enumerable of operating mode names. Allowed values are "VClamp", "IClamp", and "I0".</param>
        /// <param name="backgroundMeasurements">Corresponding background Measurement for each operating mode in backgroundModes</param>
        public MultiClampDevice(uint serialNumber,
            uint channel,
            IClock clock,
            Controller c,
            IEnumerable<string> backgroundModes,
            IEnumerable<IMeasurement> backgroundMeasurements)
            : this(serialNumber,
            channel,
            clock,
            c,
            backgroundModes.Select(k =>
            {
                MultiClampInterop.OperatingMode mode;
                Enum.TryParse(k, false, out mode);
                return mode;
            }),
            backgroundMeasurements)
        {
        }

        /// <summary>
        /// Constructs a new 700A MultiClampDevice
        /// </summary>
        /// <param name="comPort">MultiClamp COM port</param>
        /// <param name="deviceNumber">MultiClamp device number (AKA AxoBus ID)</param>
        /// <param name="channel">MultiClamp channel</param>
        /// <param name="clock">Clock instance defining cononical time</param>
        /// <param name="c">Controller instance for this device</param>
        /// <param name="backgroundModes">Enumerable of operating modes</param>
        /// <param name="backgroundMeasurements">Corresponding background Measurement for each operating mode in backgroundModes</param>
        public MultiClampDevice(uint comPort,
            uint deviceNumber,
            uint channel,
            IClock clock,
            Controller c,
            IEnumerable<MultiClampInterop.OperatingMode> backgroundModes,
            IEnumerable<IMeasurement> backgroundMeasurements)
            : this(new MultiClampCommander(comPort, deviceNumber, channel, clock),
            c,
            backgroundModes.Zip(backgroundMeasurements,
            (k, v) => new { Key = k, Value = v })
            .ToDictionary(x => x.Key, x => x.Value))
        {
            this.Clock = clock;
        }

        /// <summary>
        /// Constructs a new 700A MultiClampDevice.
        /// 
        /// This constructor is provided as a convenience for Matlab clients. .Net clients should use the typed version.
        /// </summary>
        /// <param name="comPort">MultiClamp COM port</param>
        /// <param name="deviceNumber">MultiClamp device number (AKA AxoBus ID)</param>
        /// <param name="channel">MultiClamp channel</param>
        /// <param name="clock">Clock instance defining cononical time</param>
        /// <param name="c">Controller instance for this device</param>
        /// <param name="backgroundModes">Enumerable of operating mode names. Allowed values are "VClamp", "IClamp", and "I0".</param>
        /// <param name="backgroundMeasurements">Corresponding background Measurement for each operating mode in backgroundModes</param>
        public MultiClampDevice(uint comPort,
            uint deviceNumber,
            uint channel,
            IClock clock,
            Controller c,
            IEnumerable<string> backgroundModes,
            IEnumerable<IMeasurement> backgroundMeasurements)
            : this(comPort,
            deviceNumber,
            channel,
            clock,
            c,
            backgroundModes.Select(k =>
            {
                MultiClampInterop.OperatingMode mode;
                Enum.TryParse(k, false, out mode);
                return mode;
            }),
            backgroundMeasurements)
        {
        }

        public static IEnumerable<uint> AvailableSerialNumbers()
        {
            return MultiClampCommander.AvailableSerialNumbers();
        }

        private IDictionary<string, object> MergeDeviceParametersIntoConfiguration(IDictionary<string, object> config,
            MultiClampInterop.MulticlampData deviceParameters)
        {

            var result = config == null ?
                new Dictionary<string, object>() :
                new Dictionary<string, object>(config);

            result["alpha"] = deviceParameters.Alpha;
            result["externalCommandSensitivity"] = deviceParameters.ExternalCommandSensitivity;
            result["externalCommandSensitivityUnits"] = deviceParameters.ExternalCommandSensitivityUnits.ToString();
            result["hardwareType"] = deviceParameters.HardwareType.ToString();
            result["lpfCutoff"] = deviceParameters.LPFCutoff;
            result["membraneCapacitance"] = deviceParameters.MembraneCapacitance;
            result["operatingMode"] = deviceParameters.OperatingMode.ToString();
            result["rawScaleFactor"] = deviceParameters.RawScaleFactor;
            result["rawScaleFactorUnits"] = deviceParameters.RawScaleFactorUnits.ToString();
            result["scaleFactor"] = deviceParameters.ScaleFactor;
            result["scaleFactorUnits"] = deviceParameters.ScaleFactorUnits.ToString();
            result["seriesResistance"] = deviceParameters.SeriesResistance;

            if (Commander.HardwareType == MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A)
            {
                result["rawOutputSignal"] = deviceParameters.RawOutputSignal700A.ToString();
                result["scaledOutputSignal"] = deviceParameters.ScaledOutputSignal700A.ToString();
            }
            else
            {
                result["appVersion"] = deviceParameters.AppVersion;
                result["dspVersion"] = deviceParameters.DSPVersion;
                result["firmwareVersion"] = deviceParameters.FirmwareVersion;
                result["rawOutputSignal"] = deviceParameters.RawOutputSignal700B.ToString();
                result["scaledOutputSignal"] = deviceParameters.ScaledOutputSignal700B.ToString();
                result["secondaryAlpha"] = deviceParameters.SecondaryAlpha;
                result["secondaryLPFCutoff"] = deviceParameters.SecondaryLPFCutoff;
                result["serialNumber"] = deviceParameters.SerialNumber;
            }

            return result;
        }

        public const string MultiClampDeviceConfigurationKey = "MultiClampDeviceConfiguration";

        /// <summary>
        /// Overrides Background to use current device parameters for conversion.
        /// </summary>
        public override IMeasurement Background
        {
            get
            {
                var parameters = CurrentDeviceOutputParameters;

                var bg = Backgrounds[parameters.Data.OperatingMode];

                log.DebugFormat("Desired background value: {0} ({1} {2})", bg, bg.Quantity, bg.DisplayUnits);
                log.DebugFormat("  Current parameters:");
                log.DebugFormat("     Mode: {0}", parameters.Data.OperatingMode);
                log.DebugFormat("     ExtCmdSensitivity: {0}", parameters.Data.ExternalCommandSensitivity);
                log.DebugFormat("     ExtCmdUnits: {0}", parameters.Data.ExternalCommandSensitivityUnits);
                log.DebugFormat("  Parameters timestamp: {0}", parameters.TimeStamp);
                
                return bg;
            }
            set
            {
                try
                {
                    Backgrounds[CurrentDeviceOutputParameters.Data.OperatingMode] = value;
                }
                catch (MultiClampDeviceException ex)
                {
                    throw new MultiClampDeviceException(
                        "MultiClampDevice cannot set Background because no telegraph messages have been received. Device operating mode is unknown.",
                        ex);
                }
            }
        }

        public override IMeasurement OutputBackground
        {
            get
            {
                var bg = base.OutputBackground;

                var parameters = CurrentDeviceOutputParameters;

                log.DebugFormat("Output background value: {0} ({1} {2})", bg, bg.Quantity, bg.DisplayUnits);
                log.DebugFormat("  Current parameters:");
                log.DebugFormat("     Mode: {0}", parameters.Data.OperatingMode);
                log.DebugFormat("     ExtCmdSensitivity: {0}", parameters.Data.ExternalCommandSensitivity);
                log.DebugFormat("     ExtCmdUnits: {0}", parameters.Data.ExternalCommandSensitivityUnits);
                log.DebugFormat("  Parameters timestamp: {0}", parameters.TimeStamp);

                return base.OutputBackground;
            }
        }

        protected override IMeasurement ConvertOutput(IMeasurement deviceOutput)
        {
            return ConvertOutput(deviceOutput, CurrentDeviceOutputParameters.Data);
        }

        private readonly object _bindLock = new object();

        public override ExternalDeviceBase BindStream(string name, IDAQInputStream inputStream)
        {
            lock(_bindLock) base.BindStream(name, inputStream);
            Commander.RequestTelegraphValue();
            return this;
        }

        public override ExternalDeviceBase BindStream(string name, IDAQOutputStream outputStream)
        {
            lock(_bindLock) base.BindStream(name, outputStream);
            Commander.RequestTelegraphValue();
            return this;
        }

        public override void UnbindStream(string name)
        {
            lock(_bindLock) base.UnbindStream(name);
            Commander.RequestTelegraphValue();
        }

        private bool HasBoundOutputStream
        {
            get { return OutputStreams.Any(); }
        }

        private bool HasBoundInputStream
        {
            get { return InputStreams.Any(); }
        }

        private static IEnumerable<MultiClampParametersChangedArgs> DeviceParametersPreceedingDate(
            IDictionary<DateTimeOffset, MultiClampParametersChangedArgs> parameters,
            DateTimeOffset dto)
        {
            return parameters.Keys.Where((a) => a.UtcTicks <= dto.UtcTicks).OrderBy((a) => a.UtcTicks).Select(k => parameters[k]);
        }


        /// <summary>
        /// Returns the most recent (by TimeStamp) paramter changed args preceeding the given date.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="dto"></param>
        /// <returns>null to indicate no available parameter args</returns>
        private static MultiClampParametersChangedArgs MostRecentDeviceParameterPreceedingDate(
            IDictionary<DateTimeOffset, MultiClampParametersChangedArgs> parameters,
            DateTimeOffset dto)
        {

            IEnumerable<MultiClampParametersChangedArgs> e = DeviceParametersPreceedingDate(
                parameters,
                dto);

            return e.Any() ? e.Last() : null;
        }


        public MultiClampParametersChangedArgs DeviceParametersForInput(DateTimeOffset dto)
        {
            var deviceParams = MostRecentDeviceParameterPreceedingDate(InputParameters, dto);


            if (deviceParams == null)
                throw new MultiClampDeviceException("No device parameters for " + dto);


            log.DebugFormat("{0} retrieved device parameters for time {1}: Mode={2},Timestamp={3}",
                            this,
                            dto,
                            deviceParams.Data.OperatingMode,
                            deviceParams.TimeStamp);


            PurgeDeviceParameters(InputParameters, deviceParams, ParameterStalenessInterval);

            return deviceParams;
        }

        private static void PurgeDeviceParameters(
            IDictionary<DateTimeOffset, MultiClampParametersChangedArgs> parameters,
            MultiClampParametersChangedArgs deviceParams,
            TimeSpan stalenessInterval)
        {
            if (deviceParams == null)
                return;

            var marker = ((MultiClampParametersChangedArgs)deviceParams).TimeStamp.ToUniversalTime();
            var cacheLimit = marker.Subtract(stalenessInterval);

            var staleParamsTimes = parameters.Keys.Where(t => t < cacheLimit).OrderBy(t => t);
            if (staleParamsTimes.Count() > 1) //always keep one past staleness interval
            {
                foreach (var t in staleParamsTimes.TakeWhile(t => t < staleParamsTimes.Last()))
                {
                    parameters.Remove(t);
                }
            }

        }

        public MultiClampParametersChangedArgs DeviceParametersForOutput(DateTimeOffset dto)
        {
            var deviceParams = MostRecentDeviceParameterPreceedingDate(OutputParameters, dto);
            if (deviceParams == null)
            {
                throw new MultiClampDeviceException("No device parameters available for " + dto);
            }
            PurgeDeviceParameters(OutputParameters, deviceParams, ParameterStalenessInterval);

            return deviceParams;
        }

        public IMeasurement ConvertInput(IMeasurement sample, DateTimeOffset time)
        {
            return ConvertInput(sample, DeviceParametersForInput(time.ToUniversalTime()).Data);
        }

        public static IMeasurement ConvertInput(IMeasurement sample, MultiClampInterop.MulticlampData deviceParams)
        {
            switch (deviceParams.OperatingMode)
            {
                case MultiClampInterop.OperatingMode.VClamp:
                    return ConvertVClampInput(sample, deviceParams);
                case MultiClampInterop.OperatingMode.IClamp:
                case MultiClampInterop.OperatingMode.I0:
                    return ConvertIClampInput(sample, deviceParams);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static IMeasurement ConvertIClampInput(IMeasurement sample, MultiClampInterop.MulticlampData deviceParams)
        {
            if (deviceParams.HardwareType == MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A)
            {
                switch (deviceParams.ScaledOutputSignal700A)
                {
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_I_CMD_SUMMED:
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_V_CMD_SUMMED:
                        CheckScaleFactorUnits(deviceParams, new[]
                            {
                                MultiClampInterop.ScaleFactorUnits.V_A,
                                MultiClampInterop.ScaleFactorUnits.V_mA,
                                MultiClampInterop.ScaleFactorUnits.V_nA,
                                MultiClampInterop.ScaleFactorUnits.V_uA,
                                MultiClampInterop.ScaleFactorUnits.V_pA
                            });
                        break;
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_I_CMD_EXT:
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_V_CMD_EXT:
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_V_MEMBRANE:
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_V_MEMBRANEx100:
                        CheckScaleFactorUnits(deviceParams, new[]
                            {
                                MultiClampInterop.ScaleFactorUnits.V_mV,
                                MultiClampInterop.ScaleFactorUnits.V_uV,
                                MultiClampInterop.ScaleFactorUnits.V_V
                            });
                        break;
                    default:
                        log.ErrorFormat("MultiClamp scaled output signal is not a valid IClamp mode: {0}",
                                        deviceParams.ScaledOutputSignal700A);
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                //MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB,
                //              MultiClampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED
                switch (deviceParams.ScaledOutputSignal700B)
                {
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_IC_GLDR_I_CMD_MEMB:
                        CheckScaleFactorUnits(deviceParams, new[]
                            {
                                MultiClampInterop.ScaleFactorUnits.V_A,
                                MultiClampInterop.ScaleFactorUnits.V_mA,
                                MultiClampInterop.ScaleFactorUnits.V_nA,
                                MultiClampInterop.ScaleFactorUnits.V_uA,
                                MultiClampInterop.ScaleFactorUnits.V_pA
                            });
                        break;
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_IC_GLDR_MIN:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_IC_GLDR_MAX:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx100:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_IC_GLDR_I_CMD_EXT:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_IC_GLDR_AUX1:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_IC_GLDR_MIN:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_IC_GLDR_MAX:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_IC_GLDR_V_MEMB:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_IC_GLDR_V_MEMBx100:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_IC_GLDR_I_CMD_EXT:
                        CheckScaleFactorUnits(deviceParams, new[]
                            {
                                MultiClampInterop.ScaleFactorUnits.V_mV,
                                MultiClampInterop.ScaleFactorUnits.V_uV,
                                MultiClampInterop.ScaleFactorUnits.V_V
                            });
                        break;
                    default:
                        log.ErrorFormat("MultiClamp scaled output signal is not a valid IClamp mode: {0}",
                                        deviceParams.ScaledOutputSignal700B);
                        throw new ArgumentOutOfRangeException();
                }
            }

            return ConvertInputMeasurement(sample, deviceParams);
        }

        private static IMeasurement ConvertInputMeasurement(IMeasurement sample, MultiClampInterop.MulticlampData deviceParams)
        {
            var desiredUnitsMultiplier = (decimal) Math.Pow(10,
                                                            (ExponentForScaleFactorUnits(deviceParams.ScaleFactorUnits) -
                                                             DesiredUnitsExponentForScaleFactorUnits(
                                                                 deviceParams.ScaleFactorUnits))
                                                       );

            return MeasurementPool.GetMeasurement(
                    (sample.QuantityInBaseUnits/(decimal) deviceParams.ScaleFactor/(decimal) deviceParams.Alpha)*
                    desiredUnitsMultiplier,
                    DesiredUnitsExponentForScaleFactorUnits(deviceParams.ScaleFactorUnits),
                    UnitsForScaleFactorUnits(deviceParams.ScaleFactorUnits));
        }

        private static IMeasurement ConvertVClampInput(IMeasurement sample, MultiClampInterop.MulticlampData deviceParams)
        {
            if (deviceParams.HardwareType == MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A)
            {
                switch (deviceParams.ScaledOutputSignal700A)
                {
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_V_CMD_SUMMED:
                        CheckScaleFactorUnits(deviceParams, new[] {
                            MultiClampInterop.ScaleFactorUnits.V_A,
                            MultiClampInterop.ScaleFactorUnits.V_mA,
                            MultiClampInterop.ScaleFactorUnits.V_nA,
                            MultiClampInterop.ScaleFactorUnits.V_uA,
                            MultiClampInterop.ScaleFactorUnits.V_pA
                        });
                        break;
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_I_CMD_SUMMED:
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_I_CMD_EXT:
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_V_CMD_EXT:
                    case MultiClampInterop.SignalIdentifier700A.MCTG_OUT_MUX_V_MEMBRANE:
                        CheckScaleFactorUnits(deviceParams, new[] {
                            MultiClampInterop.ScaleFactorUnits.V_mV,
                            MultiClampInterop.ScaleFactorUnits.V_uV,
                            MultiClampInterop.ScaleFactorUnits.V_V
                        });
                        break;
                    default:
                        log.ErrorFormat("MultiClamp scaled output signal is not a valid VClamp mode: {0}", deviceParams.ScaledOutputSignal700A);
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                switch (deviceParams.ScaledOutputSignal700B)
                {

                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_VC_GLDR_I_MEMB:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_VC_GLDR_I_MEMB:
                        CheckScaleFactorUnits(deviceParams, new[] {
                            MultiClampInterop.ScaleFactorUnits.V_A,
                            MultiClampInterop.ScaleFactorUnits.V_mA,
                            MultiClampInterop.ScaleFactorUnits.V_nA,
                            MultiClampInterop.ScaleFactorUnits.V_uA,
                            MultiClampInterop.ScaleFactorUnits.V_pA
                        });
                        break;
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_VC_GLDR_V_CMD_SUMMED:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMB:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMBx100:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_PRI_VC_GLDR_V_CMD_EXT:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_VC_GLDR_V_CMD_MEMBx10:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_VC_GLDR_V_CMD_SUMMED:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_VC_GLDR_V_CMD_MEMBx100:
                    case MultiClampInterop.SignalIdentifier700B.AXMCD_OUT_SEC_VC_GLDR_V_CMD_EXT:
                        CheckScaleFactorUnits(deviceParams, new[] {
                            MultiClampInterop.ScaleFactorUnits.V_mV,
                            MultiClampInterop.ScaleFactorUnits.V_uV,
                            MultiClampInterop.ScaleFactorUnits.V_V
                        });
                        break;
                    default:
                        log.ErrorFormat("MultiClamp scaled output signal is not a valid VClamp mode: {0}", deviceParams.ScaledOutputSignal700B);
                        throw new ArgumentOutOfRangeException();
                }
            }

            return ConvertInputMeasurement(sample, deviceParams);
        }

        public IMeasurement ConvertOutput(IMeasurement sample, DateTimeOffset time)
        {
            return ConvertOutput(sample, DeviceParametersForOutput(time.ToUniversalTime()).Data);
        }

        public static IMeasurement ConvertOutput(IMeasurement sample, MultiClampInterop.MulticlampData deviceParams)
        {
            switch (deviceParams.OperatingMode)
            {
                //Output cmd in Volts
                case MultiClampInterop.OperatingMode.VClamp:

                    if (string.Compare(sample.BaseUnits, "V", false) != 0) //output
                    {
                        throw new ArgumentException("Sample units must be in Volts.", "sample.BaseUnit");
                    }

                    if (deviceParams.ExternalCommandSensitivityUnits != MultiClampInterop.ExternalCommandSensitivityUnits.V_V)
                    {
                        throw new MultiClampDeviceException("External command units are not V/V as expected for current device mode.");
                    }

                    break;
                //Output cmd in amps
                case MultiClampInterop.OperatingMode.IClamp:
                    if (deviceParams.ExternalCommandSensitivityUnits != MultiClampInterop.ExternalCommandSensitivityUnits.A_V)
                    {
                        log.ErrorFormat("External Command Sensitivity Units " + deviceParams.ExternalCommandSensitivityUnits + " do not match expected (" + MultiClampInterop.ExternalCommandSensitivityUnits.A_V + ")");
                        throw new MultiClampDeviceException("External command units " + deviceParams.ExternalCommandSensitivityUnits + " are not A/V as expected for current deivce mode.");
                    }


                    if (string.Compare(sample.BaseUnits, "A", false) != 0) //output
                    {
                        throw new ArgumentException("Sample units must be in Amps.", "sample.BaseUnit");
                    }

                    break;

                case MultiClampInterop.OperatingMode.I0:
                    if (! (deviceParams.ExternalCommandSensitivityUnits == MultiClampInterop.ExternalCommandSensitivityUnits.A_V ||
                        deviceParams.ExternalCommandSensitivityUnits == MultiClampInterop.ExternalCommandSensitivityUnits.OFF))
                    {
                        log.ErrorFormat("External Command Sensitivity Units " + deviceParams.ExternalCommandSensitivityUnits + " do not match expected (" + MultiClampInterop.ExternalCommandSensitivityUnits.A_V + ")");
                        throw new MultiClampDeviceException("External command units " + deviceParams.ExternalCommandSensitivityUnits + " are not A/V as expected for current deivce mode.");
                    }

                    if (string.Compare(sample.BaseUnits, "A", false) != 0) //output
                    {
                        throw new ArgumentException("Sample units must be in Amps.", "sample.BaseUnit");
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();

            }

            IMeasurement result;

            if (deviceParams.OperatingMode == MultiClampInterop.OperatingMode.I0)
            {
                result = MeasurementPool.GetMeasurement(0, 0, "V");
            }
            else
            {
                result = (decimal) deviceParams.ExternalCommandSensitivity == 0
                             ? MeasurementPool.GetMeasurement(sample.Quantity, sample.Exponent, "V")
                             : MeasurementPool.GetMeasurement(sample.Quantity/(decimal) deviceParams.ExternalCommandSensitivity,
                                                  sample.Exponent, "V");
            }

            return result;
        }

        private static string UnitsForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits scaleFactorUnits)
        {
            switch (scaleFactorUnits)
            {
                case MultiClampInterop.ScaleFactorUnits.V_V:
                case MultiClampInterop.ScaleFactorUnits.V_mV:
                case MultiClampInterop.ScaleFactorUnits.V_uV:
                    return "V";

                case MultiClampInterop.ScaleFactorUnits.V_A:
                case MultiClampInterop.ScaleFactorUnits.V_mA:
                case MultiClampInterop.ScaleFactorUnits.V_uA:
                case MultiClampInterop.ScaleFactorUnits.V_nA:
                case MultiClampInterop.ScaleFactorUnits.V_pA:
                    return "A";
                default:
                    throw new ArgumentOutOfRangeException("scaleFactorUnits");
            }
        }

        private static int ExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits scaleFactorUnits)
        {
            switch (scaleFactorUnits)
            {
                case MultiClampInterop.ScaleFactorUnits.V_V:
                    return 0;
                case MultiClampInterop.ScaleFactorUnits.V_mV:
                    return -3;
                case MultiClampInterop.ScaleFactorUnits.V_uV:
                    return -6;
                case MultiClampInterop.ScaleFactorUnits.V_A:
                    return 0;
                case MultiClampInterop.ScaleFactorUnits.V_mA:
                    return -3;
                case MultiClampInterop.ScaleFactorUnits.V_uA:
                    return -6;
                case MultiClampInterop.ScaleFactorUnits.V_nA:
                    return -9;
                case MultiClampInterop.ScaleFactorUnits.V_pA:
                    return -12;
                default:
                    throw new ArgumentOutOfRangeException("scaleFactorUnits");
            }
        }

        private static int DesiredUnitsExponentForScaleFactorUnits(MultiClampInterop.ScaleFactorUnits scaleFactorUnits)
        {
            switch (scaleFactorUnits)
            {
                case MultiClampInterop.ScaleFactorUnits.V_V:
                case MultiClampInterop.ScaleFactorUnits.V_mV:
                case MultiClampInterop.ScaleFactorUnits.V_uV:
                    return MEMBRANE_VOLTAGE_INPUT_EXPONENT;
                case MultiClampInterop.ScaleFactorUnits.V_A:
                case MultiClampInterop.ScaleFactorUnits.V_mA:
                case MultiClampInterop.ScaleFactorUnits.V_uA:
                case MultiClampInterop.ScaleFactorUnits.V_nA:
                case MultiClampInterop.ScaleFactorUnits.V_pA:
                    return MEMBRANE_CURRENT_INPUT_EXPONENT;
                default:
                    throw new ArgumentOutOfRangeException("scaleFactorUnits");
            }
        }

        private static void CheckScaleFactorUnits(MultiClampInterop.MulticlampData deviceParams, MultiClampInterop.ScaleFactorUnits[] allowedScaleFactorUnits)
        {
            if (!allowedScaleFactorUnits.Contains(deviceParams.ScaleFactorUnits))
                throw new MultiClampDeviceException(deviceParams.ScaleFactorUnits + " is not an allowed unit conversion for scaled output mode.");
        }

        /// <summary>
        /// Pulls data for output to the given IDAQStream. Default implementation pulls data from
        /// this Device's Controller.
        /// </summary>
        /// <remarks>Appends this Device's Configuration to the IOutputData</remarks>
        /// <param name="stream">Stream for output</param>
        /// <param name="duration">Requested duration</param>
        /// <returns>IOutputData of duration less than or equal to duration</returns>
        /// <exception cref="ExternalDeviceException">Requested duration is less than one sample</exception>
        public override IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration)
        {
            /* 
             * IOuputData will be directed to a device (not an DAQStream) by the controller.
             * Controller should get mapping (device=>data) from the current Epoch instance.
             * 
             * Thus the standard PullOuputData will pull from the controller's queue for this
             * device.
             */

            try
            {
                //TODO should raise exception if duration is less than one sample

                var deviceParameters = DeviceParametersForOutput(Clock.Now.UtcDateTime).Data;
                var config = MergeDeviceParametersIntoConfiguration(Configuration, deviceParameters);

                log.DebugFormat("Pulling OutputData with parameters {0} (units {1})",
                                config,
                                UnitsForScaleFactorUnits(deviceParameters.ScaleFactorUnits));

                IOutputData data = this.Controller.PullOutputData(this, duration);

                return data.DataWithConversion(m => ConvertOutput(m, deviceParameters))
                    .DataWithExternalDeviceConfiguration(this, config);
            }
            catch (Exception ex)
            {
                log.DebugFormat("Error pulling data from controller: {0} ({1})", ex.Message, ex);
                throw;
            }
        }


        /// <summary>
        /// Pushes input data to this Device's controller.
        /// </summary>
        /// <param name="stream">Stream supplying the input data</param>
        /// <param name="inData">IInputData to push to the controller</param>
        public override void PushInputData(IDAQInputStream stream, IInputData inData)
        {
            try
            {
                var deviceParameters = DeviceParametersForInput(inData.InputTime.UtcDateTime).Data;

                IInputData convertedData = inData.DataWithConversion(
                    m => ConvertInput(m, deviceParameters)
                    );

                var config = MergeDeviceParametersIntoConfiguration(Configuration, deviceParameters);

                log.DebugFormat("Pushing InputData with parameters {0} (units {1})",
                                config,
                                UnitsForScaleFactorUnits(deviceParameters.ScaleFactorUnits));

                this.Controller.PushInputData(this, convertedData.DataWithExternalDeviceConfiguration(this, config));
            }
            catch (Exception ex)
            {
                log.DebugFormat("Error pushing data to controller: {0} ({1})", ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Verify that everything is hooked up correctly
        /// </summary>
        /// <returns></returns>
        public override Maybe<string> Validate()
        {
            Maybe<string> v = base.Validate();
            if (v)
            {
                //Specific validation here
            }

            return v;
        }


        // Indirect Dispose method from http://msdn.microsoft.com/en-us/library/system.idisposable.aspx
        private bool _disposed = false;

        void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    Commander.Dispose();

                    // This object will be cleaned up by the Dispose method.
                    // Therefore, you should call GC.SupressFinalize to
                    // take this object off the finalization queue
                    // and prevent finalization code for this object
                    // from executing a second time.
                    GC.SuppressFinalize(this);
                }

            }

        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~MultiClampDevice()
        {
            Dispose(false);
        }

        /// <summary>
        /// Duration to keep parameter change records, in case they are need by a delayed incoming/outgoing
        /// data. Default is 5 seconds.
        /// </summary>
        public static readonly TimeSpan ParameterStalenessInterval = TimeSpan.FromSeconds(5);

        private ConcurrentDictionary<DateTimeOffset, MultiClampParametersChangedArgs> OutputParameters { get; set; }
        private ConcurrentDictionary<DateTimeOffset, MultiClampParametersChangedArgs> InputParameters { get; set; }

        private IMultiClampCommander Commander { get; set; }

        /// <summary>
        /// Indicates if the device has received any input parameters from the Commander. Call this method
        /// before calling CurrentDeviceInputParameters for the first time.
        /// </summary>
        public bool HasDeviceInputParameters
        {
            get
            {
                bool b = InputParameters.Any();

                if (!b)
                {
                    Commander.RequestTelegraphValue();
                    b = InputParameters.Any();
                }

                return b;
            }
        }

        public MultiClampParametersChangedArgs CurrentDeviceInputParameters
        {
            get
            {
                if (!HasDeviceInputParameters)
                {
                    string type = Commander.HardwareType == MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A ? "700A" : "700B";
                    throw new MultiClampDeviceException("No current MultiClamp input parameters. Make sure MultiClamp " + type + " Commander is open or try toggling the mode.");
                }

                return MostRecentDeviceParameterPreceedingDate(InputParameters, Clock.Now);
            }
        }

        /// <summary>
        /// Indicates if the device has received any output parameters from the Commander. Call this method
        /// before calling CurrentDeviceOutputParameters for the first time.
        /// </summary>
        public bool HasDeviceOutputParameters
        {
            get
            {
                bool b = OutputParameters.Any();

                if (!b)
                {
                    Commander.RequestTelegraphValue();
                    b = OutputParameters.Any();
                }

                return b;
            }
        }

        public MultiClampParametersChangedArgs CurrentDeviceOutputParameters
        {
            get
            {
                if (!HasDeviceOutputParameters)
                {
                    string type = Commander.HardwareType == MultiClampInterop.HardwareType.MCTG_HW_TYPE_MC700A ? "700A" : "700B";
                    throw new MultiClampDeviceException("No current MultiClamp output parameters. Make sure MultiClamp " + type + " Commander is open or try toggling the mode.");
                }

                return MostRecentDeviceParameterPreceedingDate(OutputParameters, Clock.Now);
            }
        }


        /// <summary>
        /// Sets the device background for a particular operating mode.
        /// </summary>
        /// <param name="operatingMode">Device operating mode</param>
        /// <param name="background">Desired background</param>
        public void SetBackgroundForMode(MultiClampInterop.OperatingMode operatingMode, IMeasurement background)
        {
            Backgrounds[operatingMode] = background;
        }

        public void SetBackgroundForMode(string operatingMode, IMeasurement background)
        {
            MultiClampInterop.OperatingMode mode;
            Enum.TryParse(operatingMode, false, out mode);
            SetBackgroundForMode(mode, background);
        }

        /// <summary>
        /// Gets the device's background for a particular mode.
        /// </summary>
        /// <param name="operatingMode">Device operating mode</param>
        /// <returns>Background Measurement for the given mode.</returns>
        public IMeasurement BackgroundForMode(MultiClampInterop.OperatingMode operatingMode)
        {
            return Backgrounds[operatingMode];
        }

        public IMeasurement BackgroundForMode(string operatingMode)
        {
            MultiClampInterop.OperatingMode mode;
            Enum.TryParse(operatingMode, false, out mode);
            return BackgroundForMode(mode);
        }
    }


    /// <summary>
    /// Exception indicating an error in the MultiClampDevice instance or associated MultiClamp hardware
    /// </summary>
    public class MultiClampDeviceException : ExternalDeviceException
    {
        public MultiClampDeviceException(string message)
            : base(message)
        {
        }

        public MultiClampDeviceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
