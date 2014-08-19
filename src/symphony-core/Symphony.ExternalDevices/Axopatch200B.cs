using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Symphony.Core;

namespace Symphony.ExternalDevices
{
    public class Axopatch200B : IAxopatch
    {
        public double Beta { get; private set; }

        public Axopatch200B()
        {
            Beta = 1;
        }

        public AxopatchInterop.AxopatchData ReadTelegraphData(IDictionary<string, IInputData> data)
        {
            if (!data.ContainsKey(AxopatchDevice.GAIN_TELEGRAPH_STREAM_NAME))
                throw new ArgumentException("Data does not contain gain telegraph");

            if (!data.ContainsKey(AxopatchDevice.MODE_TELEGRAPH_STREAM_NAME))
                throw new ArgumentException("Data does not contain mode telegraph");

            var telegraph = new AxopatchInterop.AxopatchData();

            var gain = ReadVoltage(data[AxopatchDevice.GAIN_TELEGRAPH_STREAM_NAME]);
            try
            {
                telegraph.Gain = _voltageToGain[Math.Round(gain * 2) / 2];
            }
            catch (KeyNotFoundException)
            {
                throw new ArgumentException("Unknown gain telegraph");
            }

            var mode = ReadVoltage(data[AxopatchDevice.MODE_TELEGRAPH_STREAM_NAME]);
            try
            {
                telegraph.OperatingMode = _voltageToMode[Math.Round(mode * 2) / 2];
            }
            catch (KeyNotFoundException)
            {
                throw new ArgumentException("Unknown mode telegraph");
            }

            switch (telegraph.OperatingMode)
            {
                case AxopatchInterop.OperatingMode.IClampFast:
                case AxopatchInterop.OperatingMode.IClampNormal:
                    telegraph.ExternalCommandSensitivity = 2e-9/Beta;
                    telegraph.ExternalCommandSensitivityUnits = AxopatchInterop.ExternalCommandSensitivityUnits.A_V;
                    break;
                case AxopatchInterop.OperatingMode.VClamp:
                    telegraph.ExternalCommandSensitivity = 0.02;
                    telegraph.ExternalCommandSensitivityUnits = AxopatchInterop.ExternalCommandSensitivityUnits.V_V;
                    break;
                case AxopatchInterop.OperatingMode.I0:
                case AxopatchInterop.OperatingMode.Track:
                    telegraph.ExternalCommandSensitivityUnits = AxopatchInterop.ExternalCommandSensitivityUnits.OFF;
                    break;
            }

            return telegraph;
        }

        private static decimal ReadVoltage(IInputData data)
        {
            var measurements = data.DataWithUnits("V").Data;
            if (Math.Abs(measurements.Max(m => m.Quantity)- measurements.Min(m => m.Quantity)) >= 0.5m)
            {
                throw new ArgumentException("Telegraph reading is unstable");
            }
            return measurements.Average(m => m.Quantity);
        }

        private readonly IDictionary<decimal, double> _voltageToGain = new Dictionary<decimal, double>
        {
            {0.5m, 0.05},
            {1.0m, 0.1},
            {1.5m, 0.2},
            {2.0m, 0.5},
            {2.5m, 1},
            {3.0m, 2},
            {3.5m, 5},
            {4.0m, 10},
            {4.5m, 20},
            {5.0m, 50},
            {5.5m, 100},
            {6.0m, 200},
            {6.5m, 500}
        };

        private readonly IDictionary<decimal, AxopatchInterop.OperatingMode> _voltageToMode = new Dictionary<decimal, AxopatchInterop.OperatingMode>
        {
            {1, AxopatchInterop.OperatingMode.IClampFast},
            {2, AxopatchInterop.OperatingMode.IClampNormal},
            {3, AxopatchInterop.OperatingMode.I0},
            {4, AxopatchInterop.OperatingMode.Track},
            {6, AxopatchInterop.OperatingMode.VClamp}
        };
    }
}
