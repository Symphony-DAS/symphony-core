using System;
using System.Runtime.InteropServices;

namespace Symphony.ExternalDevices
{
    /// <summary>
    /// Declarations and utility methods from the MultiClamp documentation
    /// </summary>
    public static class MultiClampInterop
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

        public static readonly int MCTG_OPEN_MESSAGE = Win32Interop.RegisterWindowMessage(MultiClampInterop.MCTG_OPEN_MESSAGE_STR);
        public static readonly int MCTG_CLOSE_MESSAGE = Win32Interop.RegisterWindowMessage(MultiClampInterop.MCTG_CLOSE_MESSAGE_STR);
        public static readonly int MCTG_SCAN_MESSAGE = Win32Interop.RegisterWindowMessage(MultiClampInterop.MCTG_SCAN_MESSAGE_STR);
        public static readonly int MCTG_RECONNECT_MESSAGE = Win32Interop.RegisterWindowMessage(MultiClampInterop.MCTG_RECONNECT_MESSAGE_STR);
        public static readonly int MCTG_BROADCAST_MESSAGE = Win32Interop.RegisterWindowMessage(MultiClampInterop.MCTG_BROADCAST_MESSAGE_STR);
        public static readonly int MCTG_REQUEST_MESSAGE = Win32Interop.RegisterWindowMessage(MultiClampInterop.MCTG_REQUEST_MESSAGE_STR);
        public static readonly int MCTG_ID_MESSAGE = Win32Interop.RegisterWindowMessage(MultiClampInterop.MCTG_ID_MESSAGE_STR);

        public static uint MCTG_Pack700ASignalIDs(uint uComPortID, uint uAxoBusID, uint uChannelID)
        {
            uint lparamSignalIDs = 0;
            lparamSignalIDs |= (uComPortID);
            lparamSignalIDs |= (uAxoBusID << 8);
            lparamSignalIDs |= (uChannelID << 16);
            return lparamSignalIDs;
        }

        public static bool MCTG_Unpack700ASignalIDs(uint lparamSignalIDs, out uint uComPortID, out uint uAxoBusID,
                                                    out uint uChannelID)
        {
            uComPortID = (lparamSignalIDs) & 0x000000FF;
            uAxoBusID = (lparamSignalIDs >> 8) & 0x000000FF;
            uChannelID = (lparamSignalIDs >> 16) & 0x0000FFFF;
            return true;
        }

        public static uint MCTG_Pack700BSignalIDs(uint uSerialNum, uint uChannelID)
        {
            uint lparamSignalIDs = 0;
            lparamSignalIDs |= (uSerialNum & 0x0FFFFFFF);
            lparamSignalIDs |= (uChannelID << 28);
            return lparamSignalIDs;
        }

        public static bool MCTG_Unpack700BSignalIDs(uint lparamSignalIDs, out uint uSerialNum, out uint uChannelID)
        {
            uSerialNum = (lparamSignalIDs) & 0x0FFFFFFF;
            uChannelID = (lparamSignalIDs >> 28) & 0x0000000F;
            return true;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public unsafe struct MC_TELEGRAPH_DATA
        {
            /// <summary>
            ///  UINT; must be set to MCTG_API_VERSION
            /// </summary>
            public UInt32 uVersion;

            /// <summary>
            /// UINT; must be set to sizeof( MC_TELEGRAPH_DATA ) 
            // uVersion &lt;= 6 was 128 bytes, expanded size for uVersion > 6 
            /// </summary>
            public UInt32 uStructSize;

            /// <summary>
            /// UINT; ( one-based  counting ) 1 -> 8 
            /// </summary>
            public UInt32 uComPortID;

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
            /// // 700A
            /// const UINT MCTG_OUT_MUX_COMMAND = 0;
            /// const UINT MCTG_OUT_MUX_I_MEMBRANE = 1;
            /// const UINT MCTG_OUT_MUX_V_MEMBRANE = 2;
            /// const UINT MCTG_OUT_MUX_V_MEMBRANEx100 = 3;
            /// const UINT MCTG_OUT_MUX_I_BATH = 4;
            /// const UINT MCTG_OUT_MUX_V_BATH = 5;
            /// const UINT MCTG_OUT_MUX_NUMCHOICES = 6;
            /// 
            /// // 700B
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

        public enum SignalIdentifier700A
        {
            //See "700A Telegraph Output Signal Mux Identifiers" section in MultiClampBroadcastMsg.hpp
            MCTG_OUT_MUX_I_CMD_SUMMED = 0,
            MCTG_OUT_MUX_V_CMD_SUMMED = 1,
            MCTG_OUT_MUX_I_CMD_EXT = 2,
            MCTG_OUT_MUX_V_CMD_EXT = 3,
            MCTG_OUT_MUX_I_MEMBRANE = 4,
            MCTG_OUT_MUX_V_MEMBRANE = 5,
            MCTG_OUT_MUX_V_MEMBRANEx100 = 6,
            MCTG_OUT_MUX_I_AUX1 = 7,
            MCTG_OUT_MUX_V_AUX1 = 8,
            MCTG_OUT_MUX_I_AUX2 = 9,
            MCTG_OUT_MUX_V_AUX2 = 10,
            MCTG_OUT_MUX_NUMCHOICES = 11
        }

        public enum SignalIdentifier700B
        {
            //See "700B Primary and Secondary Output Signal Constants" section in MultiClampBroadcastMsg.hpp
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

        public enum RawOutputSignalIdentifier700A
        {
            //See "700A Telegraph Output Signal Mux Identifiers" section in MultiClampBroadcastMsg.hpp
            MCTG_OUT_MUX_I_CMD_SUMMED = 0,
            MCTG_OUT_MUX_V_CMD_SUMMED = 1,
            MCTG_OUT_MUX_I_CMD_EXT = 2,
            MCTG_OUT_MUX_V_CMD_EXT = 3,
            MCTG_OUT_MUX_I_MEMBRANE = 4,
            MCTG_OUT_MUX_V_MEMBRANE = 5,
            MCTG_OUT_MUX_V_MEMBRANEx100 = 6,
            MCTG_OUT_MUX_I_AUX1 = 7,
            MCTG_OUT_MUX_V_AUX1 = 8,
            MCTG_OUT_MUX_I_AUX2 = 9,
            MCTG_OUT_MUX_V_AUX2 = 10,
            MCTG_OUT_MUX_NUMCHOICES = 11
        }

        public enum RawOutputSignalIdentifier700B
        {
            //See "700B Primary and Secondary Output Signal Constants" section in MultiClampBroadcastMsg.hpp
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
                RawScaleFactor = mtd.dRawScaleFactor;
                RawScaleFactorUnits = (ScaleFactorUnits)mtd.uRawScaleFactorUnits;
                ScaleFactor = mtd.dScaleFactor;
                ScaleFactorUnits = (ScaleFactorUnits)mtd.uScaleFactorUnits;
                SecondaryAlpha = mtd.dSecondaryAlpha;
                SecondaryLPFCutoff = mtd.dSecondaryLPFCutoff;

                if (HardwareType == HardwareType.MCTG_HW_TYPE_MC700A)
                {
                    RawOutputSignal700A = (RawOutputSignalIdentifier700A)mtd.uRawOutSignal;
                    ScaledOutputSignal700A = (SignalIdentifier700A)mtd.uScaledOutSignal;
                    RawOutputSignal700B = null;
                    ScaledOutputSignal700B = null;
                }
                else
                {
                    RawOutputSignal700A = null;
                    ScaledOutputSignal700A = null;
                    RawOutputSignal700B = (RawOutputSignalIdentifier700B)mtd.uRawOutSignal;
                    ScaledOutputSignal700B = (SignalIdentifier700B)mtd.uScaledOutSignal;
                }

                switch(OperatingMode)
                {
                    case OperatingMode.VClamp:
                        ExternalCommandSensitivityUnits = ExternalCommandSensitivityUnits.V_V;
                        break;
                    case OperatingMode.IClamp:
                        ExternalCommandSensitivityUnits = ExternalCommandSensitivityUnits.A_V;
                        break;
                    case OperatingMode.I0:
                        ExternalCommandSensitivityUnits = ExternalCommandSensitivityUnits.OFF;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                //Marshal char[]
                //AppVersion = string(mtd.szAppVersion);
                //FirmwareVersion = xx;
                //SerialNumber = xx;
            }

            public OperatingMode OperatingMode { get; set; } //uOperatingMode
            public SignalIdentifier700A? ScaledOutputSignal700A { get; set; } //uScaledOutSignal (700A only)
            public SignalIdentifier700B? ScaledOutputSignal700B { get; set; } //uScaledOutSignal (700B only)
            public double Alpha { get; set; } //dAlpha
            public double ScaleFactor { get; set; } //dScaleFactor
            public ScaleFactorUnits ScaleFactorUnits { get; set; } //uScaleFactorUnits
            public double LPFCutoff { get; set; } //dLPFCutoff
            public double MembraneCapacitance { get; set; } //dMembraneCap
            public ExternalCommandSensitivityUnits ExternalCommandSensitivityUnits { get; set; }
            public double ExternalCommandSensitivity { get; set; } //dExtCmdSens
            public RawOutputSignalIdentifier700A? RawOutputSignal700A { get; set; } //uRawOutSignal (700A only)
            public RawOutputSignalIdentifier700B? RawOutputSignal700B { get; set; } //uRawOutSignal (700B only)
            public double RawScaleFactor { get; set; } //dRawScaleFactor
            public ScaleFactorUnits RawScaleFactorUnits { get; set; } //uRawScaleFactorUnits
            public HardwareType HardwareType { get; set; } //uHardwareType
            public double SecondaryAlpha { get; set; } //dSecondaryAlpha (700B only)
            public double SecondaryLPFCutoff { get; set; } //dSecondaryLPFCutoff (700B only)
            public string AppVersion { get; set; } //szAppVersion (700B only)
            public string FirmwareVersion { get; set; } //szFirmwareVersion (700B only)
            public string DSPVersion { get; set; } //szDSPVersion (700B only)
            public string SerialNumber { get; set; } //szSerialNumber (700B only)

            public override string ToString()
            {
                string result;
                if (HardwareType == HardwareType.MCTG_HW_TYPE_MC700A)
                {
                    result = String.Format("{{ OperatingMode={0}, ScaledOutputSignal={1}, Alpha={2}, ... }}",
                                           OperatingMode, ScaledOutputSignal700A, Alpha);
                }
                else
                {
                    result = String.Format("{{ OperatingMode={0}, ScaledOutputSignal={1}, Alpha={2}, ... }}",
                                           OperatingMode, ScaledOutputSignal700B, Alpha);
                }
                return result;
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
