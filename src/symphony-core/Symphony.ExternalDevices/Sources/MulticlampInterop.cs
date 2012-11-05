using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Symphony.ExternalDevices
{
    /// <summary>
    /// Declarations and utility methods from the MultiClamp documentation
    /// </summary>
    public static class MulticlampInterop
    {
        public const string MCTG_OPEN_MESSAGE_STR = "MultiClampTelegraphOpenMsg";
        public const string MCTG_CLOSE_MESSAGE_STR = "MultiClampTelegraphCloseMsg";
        public const string MCTG_REQUEST_MESSAGE_STR = "MultiClampTelegraphRequestMsg";
        public const string MCTG_SCAN_MESSAGE_STR = "MultiClampTelegraphScanMsg";
        public const string MCTG_RECONNECT_MESSAGE_STR = "MultiClampTelegraphReconnectMsg";
        public const string MCTG_BROADCAST_MESSAGE_STR = "MultiClampTelegraphBroadcastMsg";
        public const string MCTG_ID_MESSAGE_STR = "MultiClampTelegraphIdMsg";

        public const string MC_CONFIGREQUEST_MESSAGE_STR = "MultiClampConfigRequestMsg";
        public const string MC_CONFIGSENT_MESSAGE_STR = "MultiClampConfigSentMsg";
        public const string MC_COMMANDERLOCK_MESSAGE_STR = "MultiClampCommanderLock";
        public const string MC_COMMAND_MESSAGE_STR = "MultiClampCommandMsg";

        public static readonly int MCTG_OPEN_MESSAGE = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_OPEN_MESSAGE_STR);
        public static readonly int MCTG_CLOSE_MESSAGE = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_CLOSE_MESSAGE_STR);
        public static readonly int MCTG_SCAN_MESSAGE = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_SCAN_MESSAGE_STR);
        public static readonly int MCTG_RECONNECT_MESSAGE = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_RECONNECT_MESSAGE_STR);
        public static readonly int MCTG_BROADCAST_MESSAGE = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_BROADCAST_MESSAGE_STR);
        public static readonly int MCTG_ID_MESSAGE = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_ID_MESSAGE_STR);

        public static uint MCTG_Pack700BSignalIDs(uint uSerialNum, uint uChannelID)
        {
            uint lparamSignalIDs = 0;
            lparamSignalIDs |= (uSerialNum & 0x0FFFFFFF);
            lparamSignalIDs |= (uChannelID << 28);
            return lparamSignalIDs;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public unsafe struct MC_TELEGRAPH_DATA
        {
            /// <summary>
            ///  UINT; must be set to MCTG_API_VERSION
            /// </summary>
            public Int32 uVersion;

            /// <summary>
            /// UINT; must be set to sizeof( MC_TELEGRAPH_DATA ) 
            // uVersion &lt;= 6 was 128 bytes, expanded size for uVersion > 6 
            /// </summary>
            public Int32 uStructSize;

            /// <summary>
            /// UINT; ( one-based  counting ) 1 -> 8 
            /// </summary>
            public Int32 uComPortID;

            /// <summary>
            /// UINT; ( zero-based counting ) 0 -> 9 A.K.A. "Device Number"
            /// </summary>
            public UInt32 uAxoBusID;

            /// <summary>
            /// UINT; ( one-based  counting ) 1 -> 2
            /// </summary>
            public UInt32 uChannelID;

            /// <summary>
            /// UINT; use constants defined:
            /// const UINT MCTG_MODE_VCLAMP = 0;
            /// const UINT MCTG_MODE_ICLAMP = 1;
            /// const UINT MCTG_MODE_ICLAMPZERO = 2;
            /// const UINT MCTG_MODE_NUMCHOICES = 3;
            /// </summary>
            public UInt32 uOperatingMode;

            /// <summary>
            /// UINT;  use constants defined:
            /// // VC Primary
            /// const long AXMCD_OUT_PRI_VC_GLDR_MIN = 0;
            /// const long AXMCD_OUT_PRI_VC_GLDR_MAX = 6;
            /// const long AXMCD_OUT_PRI_VC_GLDR_I_MEMB = 0;
            /// const long AXMCD_OUT_PRI_VC_GLDR_V_CMD_SUMMED = 1;
            /// const long AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMB = 2;
            /// const long AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMBx100 = 3;
            /// const long AXMCD_OUT_PRI_VC_GLDR_V_CMD_EXT = 4;
            /// const long AXMCD_OUT_PRI_VC_GLDR_AUX1 = 5;
            /// const long AXMCD_OUT_PRI_VC_GLDR_AUX2 = 6;
            /// // IC Primary
            /// const long AXMCD_OUT_PRI_IC_GLDR_MIN = 20;
            /// const long AXMCD_OUT_PRI_IC_GLDR_MAX = 26;
            /// const long AXMCD_OUT_PRI_IC_GLDR_V_MEMBx10 = 20;
            /// const long AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB = 21;
            /// const long AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED = 22;
            /// const long AXMCD_OUT_PRI_IC_GLDR_V_MEMBx100 = 23;
            /// const long AXMCD_OUT_PRI_IC_GLDR_I_CMD_EXT = 24;
            /// const long AXMCD_OUT_PRI_IC_GLDR_AUX1 = 25;
            /// const long AXMCD_OUT_PRI_IC_GLDR_AUX2 = 26;
            /// </summary>
            public UInt32 uScaledOutSignal;

            /// <summary>
            /// double; output gain (dimensionless) for PRIMARY output signal.
            /// </summary>
            public double dAlpha;

            /// <summary>
            /// double; gain scale factor ( for dAlpha == 1 ) for PRIMARY output signal.
            /// </summary>
            public double dScaleFactor;

            /// <summary>
            /// UINT; use constants above for PRIMARY output signal:
            /// </summary>
            public UInt32 uScaleFactorUnits;

            /// <summary>
            /// double; ( Hz ) , ( MCTG_LPF_BYPASS indicates Bypass ) 
            /// </summary>
            public double dLPFCutoff;

            /// <summary>
            /// double; ( F  )  dMembraneCap will be MCTG_NOMEMBRANECAP if we are not in V-Clamp mode, 
            /// or 
            /// if Rf is set to range 2 (5G) or range 3 (50G),
            /// or 
            /// if whole cell comp is explicitly disabled.
            /// </summary>
            public double dMembraneCap;

            /// <summary>
            /// double; external command sensitivity; ( V/V ) for V-Clamp, ( A/V ) for I-Clamp, 0 (OFF) for I = 0 mode
            /// </summary>
            public double dExtCmdSens;

            /// <summary>
            /// UINT; use constants defined for SECONDARY output signal:
            /// </summary>
            public UInt32 uRawOutSignal;

            /// <summary>
            /// double; gain scale factor ( for Alpha == 1 ) for SECONDARY output signal.
            /// </summary>
            public double dRawScaleFactor;

            /// <summary>
            /// UINT; use constants defined for SECONDARY output signal:
            /// </summary>
            public UInt32 uRawScaleFactorUnits;

            /// <summary>
            /// UINT; use constants defined:
            /// </summary>
            public UInt32 uHardwareType;

            /// <summary>
            /// double; output gain (dimensionless) for SECONDARY output signal.
            /// </summary>
            public double dSecondaryAlpha;

            /// <summary>
            /// double; ( Hz ) , ( MCTG_LPF_BYPASS indicates Bypass ) for SECONDARY output signal.
            /// </summary>
            public double dSecondaryLPFCutoff;

            /// <summary>
            /// char[16]; application version number 
            /// </summary>
            public fixed byte szAppVersion[16];

            /// <summary>
            /// char[16]; firmware version number 
            /// </summary>
            public fixed byte szFirmwareVersion[16];

            /// <summary>
            /// char[16]; DSP version number 
            /// </summary>
            public fixed byte szDSPVersion[16];

            /// <summary>
            /// char[16];  serial number of device 
            /// </summary>
            public fixed byte szSerialNumber[16];

            /// <summary>
            /// double; ( Rs ) dSeriesResistance will be MCTG_NOSERIESRESIST if we are not in V-Clamp mode, or 
            /// if Rf is set to range 2 (5G) or range 3 (50G), or if whole cell comp is explicitly disabled.
            /// </summary>
            public double dSeriesResistance;

            /// <summary>
            /// char[76]; room for this structure to grow
            /// </summary>
            public fixed byte pcPadding[76];
        }

        public enum ScaleFactorUnits
        {
            V_V,
            V_mV,
            V_uV,
            V_A,
            V_mA,
            V_uA,
            V_nA,
            V_pA
        }

        public enum OperatingMode
        {
            VClamp,
            IClamp,
            I0
        }

        public enum SignalIdentifier
        {
            //See "700B Primary and Secondary Output Signal Constants" section in MCTelegrpahs.hpp
            // VC Primary
            AXMCD_OUT_PRI_VC_GLDR_MIN = 0,
            AXMCD_OUT_PRI_VC_GLDR_MAX = 6,

            AXMCD_OUT_PRI_VC_GLDR_I_MEMB = 0,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_SUMMED = 1,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMB = 2,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMBx100 = 3,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_EXT = 4,
            AXMCD_OUT_PRI_VC_GLDR_AUX1 = 5,
            AXMCD_OUT_PRI_VC_GLDR_AUX2 = 6,

            // VC Secondary
            AXMCD_OUT_SEC_VC_GLDR_MIN = 10,
            AXMCD_OUT_SEC_VC_GLDR_MAX = 16,

            AXMCD_OUT_SEC_VC_GLDR_I_MEMB = 10,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_MEMBx10 = 11,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_SUMMED = 12,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_MEMBx100 = 13,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_EXT = 14,
            AXMCD_OUT_SEC_VC_GLDR_AUX1 = 15,
            AXMCD_OUT_SEC_VC_GLDR_AUX2 = 16,

            // IC Primary
            AXMCD_OUT_PRI_IC_GLDR_MIN = 20,
            AXMCD_OUT_PRI_IC_GLDR_MAX = 26,

            AXMCD_OUT_PRI_IC_GLDR_V_MEMBx10 = 20,
            AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB = 21,
            AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED = 22,
            AXMCD_OUT_PRI_IC_GLDR_V_MEMBx100 = 23,
            AXMCD_OUT_PRI_IC_GLDR_I_CMD_EXT = 24,
            AXMCD_OUT_PRI_IC_GLDR_AUX1 = 25,
            AXMCD_OUT_PRI_IC_GLDR_AUX2 = 26,

            // IC Secondary
            AXMCD_OUT_SEC_IC_GLDR_MIN = 30,
            AXMCD_OUT_SEC_IC_GLDR_MAX = 36,

            AXMCD_OUT_SEC_IC_GLDR_V_MEMBx10 = 30,
            AXMCD_OUT_SEC_IC_GLDR_I_CMD_MEMB = 31,
            AXMCD_OUT_SEC_IC_GLDR_V_MEMB = 32,
            AXMCD_OUT_SEC_IC_GLDR_V_MEMBx100 = 33,
            AXMCD_OUT_SEC_IC_GLDR_I_CMD_EXT = 34,
            AXMCD_OUT_SEC_IC_GLDR_AUX1 = 35,
            AXMCD_OUT_SEC_IC_GLDR_AUX2 = 36,

            // Auxiliary signals (each auxiliary glider index maps to one of these)
            AXMCD_OUT_V_AUX1 = 40,
            AXMCD_OUT_I_AUX1 = 41,
            AXMCD_OUT_V_AUX2 = 42,
            AXMCD_OUT_I_AUX2 = 43,
            AXMCD_OUT_NOTCONNECTED_AUX = 44,
            AXMCD_OUT_RESERVED_AUX = 45,
            AXMCD_OUT_NOT_AUX = 46,

            // Number of signal choices available
            AXMCD_OUT_NAMES_NUMCHOICES = 40,
            AXMCD_OUT_CACHE_NUMCHOICES = 7
        }

        public enum ExternalCommandSensitivityUnits
        {
            V_V,
            A_V,
            OFF,
        }

        public enum RawOutputSignalIdentifier
        {
            //See "700B Primary and Secondary Output Signal Constants" section in MCTelegrpahs.hpp
            // VC Primary
            AXMCD_OUT_PRI_VC_GLDR_MIN = 0,
            AXMCD_OUT_PRI_VC_GLDR_MAX = 6,

            AXMCD_OUT_PRI_VC_GLDR_I_MEMB = 0,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_SUMMED = 1,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMB = 2,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMBx100 = 3,
            AXMCD_OUT_PRI_VC_GLDR_V_CMD_EXT = 4,
            AXMCD_OUT_PRI_VC_GLDR_AUX1 = 5,
            AXMCD_OUT_PRI_VC_GLDR_AUX2 = 6,

            // VC Secondary
            AXMCD_OUT_SEC_VC_GLDR_MIN = 10,
            AXMCD_OUT_SEC_VC_GLDR_MAX = 16,

            AXMCD_OUT_SEC_VC_GLDR_I_MEMB = 10,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_MEMBx10 = 11,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_SUMMED = 12,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_MEMBx100 = 13,
            AXMCD_OUT_SEC_VC_GLDR_V_CMD_EXT = 14,
            AXMCD_OUT_SEC_VC_GLDR_AUX1 = 15,
            AXMCD_OUT_SEC_VC_GLDR_AUX2 = 16,

            // IC Primary
            AXMCD_OUT_PRI_IC_GLDR_MIN = 20,
            AXMCD_OUT_PRI_IC_GLDR_MAX = 26,

            AXMCD_OUT_PRI_IC_GLDR_V_MEMBx10 = 20,
            AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB = 21,
            AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED = 22,
            AXMCD_OUT_PRI_IC_GLDR_V_MEMBx100 = 23,
            AXMCD_OUT_PRI_IC_GLDR_I_CMD_EXT = 24,
            AXMCD_OUT_PRI_IC_GLDR_AUX1 = 25,
            AXMCD_OUT_PRI_IC_GLDR_AUX2 = 26,

            // IC Secondary
            AXMCD_OUT_SEC_IC_GLDR_MIN = 30,
            AXMCD_OUT_SEC_IC_GLDR_MAX = 36,

            AXMCD_OUT_SEC_IC_GLDR_V_MEMBx10 = 30,
            AXMCD_OUT_SEC_IC_GLDR_I_CMD_MEMB = 31,
            AXMCD_OUT_SEC_IC_GLDR_V_MEMB = 32,
            AXMCD_OUT_SEC_IC_GLDR_V_MEMBx100 = 33,
            AXMCD_OUT_SEC_IC_GLDR_I_CMD_EXT = 34,
            AXMCD_OUT_SEC_IC_GLDR_AUX1 = 35,
            AXMCD_OUT_SEC_IC_GLDR_AUX2 = 36,

            // Auxiliary signals (each auxiliary glider index maps to one of these)
            AXMCD_OUT_V_AUX1 = 40,
            AXMCD_OUT_I_AUX1 = 41,
            AXMCD_OUT_V_AUX2 = 42,
            AXMCD_OUT_I_AUX2 = 43,
            AXMCD_OUT_NOTCONNECTED_AUX = 44,
            AXMCD_OUT_RESERVED_AUX = 45,
            AXMCD_OUT_NOT_AUX = 46,

            // Number of signal choices available
            AXMCD_OUT_NAMES_NUMCHOICES = 40,
            AXMCD_OUT_CACHE_NUMCHOICES = 7
        }

        public enum HardwareType
        {
            MCTG_HW_TYPE_MC700A,
            MCTG_HW_TYPE_MC700B
        }

        /// <summary>
        /// This is the .NET-friendly version of the MC_TELEGRAPH_DATA
        /// </summary>
        public struct MulticlampData
        {
            public MulticlampData(MC_TELEGRAPH_DATA mtd)
                : this()
            {
                Alpha = mtd.dAlpha;
                ExternalCommandSensitivity = mtd.dExtCmdSens;
                HardwareType = (HardwareType)mtd.uHardwareType;
                LPFCutoff = mtd.dLPFCutoff;
                MembraneCapacitance = mtd.dMembraneCap;
                OperatingMode = (OperatingMode)mtd.uOperatingMode;
                RawOutputSignal = (RawOutputSignalIdentifier)mtd.uRawOutSignal;
                RawScaleFactor = mtd.dRawScaleFactor;
                RawScaleFactorUnits = (ScaleFactorUnits)mtd.uRawScaleFactorUnits;
                ScaledOutputSignal = (SignalIdentifier)mtd.uScaledOutSignal;
                ScaleFactor = mtd.dScaleFactor;
                ScaleFactorUnits = (ScaleFactorUnits)mtd.uScaleFactorUnits;
                SecondaryAlpha = mtd.dSecondaryAlpha;
                SecondaryLPFCutoff = mtd.dSecondaryLPFCutoff;
            }

            public OperatingMode OperatingMode { get; set; } //uOperatingMode
            public SignalIdentifier ScaledOutputSignal { get; set; } //uScaledOutSignal
            public double Alpha { get; set; } //dAlpha
            public double ScaleFactor { get; set; } //dScaleFactor
            public ScaleFactorUnits ScaleFactorUnits { get; set; } //uScaleFactorUnits
            public double LPFCutoff { get; set; } //dLPFCutoff
            public double MembraneCapacitance { get; set; } //dMembraneCap
            public ExternalCommandSensitivityUnits ExternalCommandSensitivityUnits { get; set; }
            public double ExternalCommandSensitivity { get; set; } //dExtCmdSens
            public RawOutputSignalIdentifier RawOutputSignal { get; set; } //uRawOutSignal
            public double RawScaleFactor { get; set; } //dRawScaleFactor
            public ScaleFactorUnits RawScaleFactorUnits { get; set; } //uRawScaleFactorUnits
            public HardwareType HardwareType { get; set; } //uHardwareType
            public double SecondaryAlpha { get; set; } //dSecondaryAlpha
            public double SecondaryLPFCutoff { get; set; } //dSecondaryLPFCutoff
            public string AppVersion { get; set; } //szAppVersion
            public string FirmwareVersion { get; set; } //szFirmwareVersion
            public string DSPVersion { get; set; } //szDSPVersion
            public string SerialNumber { get; set; } //szSerialNumber

            public override string ToString()
            {
                return String.Format("{{ OperatingMode={0}, ScaledOutputSignal={1}, Alpha={2}, ... }}",
                    OperatingMode, ScaledOutputSignal, Alpha);
            }
        }

        public static double ExponentForScaleFactorUnits(ScaleFactorUnits scaleFactorUnits)
        {
            switch(scaleFactorUnits)
            {
                case ScaleFactorUnits.V_V:
                    return 1;
                case ScaleFactorUnits.V_mV:
                    return -3;
                case ScaleFactorUnits.V_uV:
                    return -6;
                case ScaleFactorUnits.V_A:
                    return 1;
                case ScaleFactorUnits.V_mA:
                    return -3;
                case ScaleFactorUnits.V_uA:
                    return -6;
                case ScaleFactorUnits.V_nA:
                    return -9;
                case ScaleFactorUnits.V_pA:
                    return -12;
                default:
                    throw new ArgumentOutOfRangeException("scaleFactorUnits");
            }
        }
    }

}
