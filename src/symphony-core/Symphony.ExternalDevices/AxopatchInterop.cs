using System;

namespace Symphony.ExternalDevices
{
    public static class AxopatchInterop
    {
        public class AxopatchData
        {
            public double Gain { get; set; }
            public OperatingMode OperatingMode { get; set; }
            public double ExternalCommandSensitivity { get; set; }
            public ExternalCommandSensitivityUnits ExternalCommandSensitivityUnits { get; set; }

            public override string ToString()
            {
                return String.Format("{{ OperatingMode={0}, Gain={1}, ... }}", OperatingMode, Gain);
            }
        }

        public enum OperatingMode
        {
            Track,
            VClamp,
            I0,
            IClampNormal,
            IClampFast
        }

        public enum ExternalCommandSensitivityUnits
        {
            V_V,
            A_V,
            OFF
        }
    }
}
