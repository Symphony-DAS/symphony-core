using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MessageTrapper
{
    /// <summary>
    /// Set of P/Invoke declarations
    /// </summary>
    static class Win32Interop
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Overload for PostMessage that takes a Message struct instead of the individual params
        /// </summary>
        /// <param name="msg">the HWND, UINT msg, WPARAM, LPARAM to send</param>
        /// <returns>Whatever PostMessage() returns</returns>
        public static int PostMessage(Message msg)
        {
            return PostMessage(msg.HWnd, msg.Msg, msg.WParam, msg.LParam);
        }


        /// <summary>
        /// The "broadcast-message" window handle
        /// </summary>
        public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;


        /// <summary>
        /// WM_COPYDATA from Windows.h
        /// </summary>
        public const int WM_COPYDATA = 0x004A;

        /// <summary>
        /// Data structure sent by WM_COPYDATA
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        /// <summary>
        /// Event package sent when a Win32 WM_ message is received.
        /// </summary>
        public class MessageReceivedEventArgs : EventArgs
        {
            private readonly Message message;
            public MessageReceivedEventArgs(Message message) { this.message = message; }
            public Message Message { get { return message; } }
        }

        /// <summary>
        /// Static class designed to retrieve Windows messages (WM_*, BN_*, etc) broadcast across
        /// process boundaries. Code (slightly altered) comes from Stephen Toub's article in the
        /// June 2007 MSDN ".NET Matters" column ("Handling Messages in Console Apps").
        /// 
        /// To hook up an event-handler to a message, just do the following:
        /// <pre>
        /// int customMessage =
        ///    Win32Interop.RegisterWindowMessage("com.tedneward.messages.custom");
        ///
        /// MessageEvents.WatchMessage(customMessage,
        ///        delegate(object sender, MessageReceivedEventArgs mreArgs)
        ///        {
        ///            Console.WriteLine("Message received: {0}", mreArgs.Message);
        ///        });
        /// </pre>
        /// Doing so will start firing messages to that event handler as messages come
        /// in to the process.
        /// </summary>
        public static class MessageEvents
        {
            private static object lockObj = new object();
            private static MessageWindow window;
            private static IntPtr hwnd;
            private static SynchronizationContext context;

            /// <summary>
            /// Assign the event handler "handler" to be fired when a Windows message matching the
            /// value passed in "message" is fired.
            /// </summary>
            /// <param name="message"></param>
            /// <param name="handler"></param>
            public static void WatchMessage(int message, EventHandler<MessageReceivedEventArgs> handler)
            {
                EnsureInitialized();
                window.RegisterEventForMessage(message, handler);
            }

            /// <summary>
            /// The HWND for the hidden Form window receiving messages
            /// </summary>
            public static IntPtr WindowHandle
            {
                get
                {
                    EnsureInitialized();
                    return hwnd;
                }
            }

            private static void EnsureInitialized()
            {
                lock (lockObj)
                {
                    if (window == null)
                    {
                        context = AsyncOperationManager.SynchronizationContext;
                        using (ManualResetEvent mre = new ManualResetEvent(false))
                        {
                            Thread t = new Thread((ThreadStart)delegate
                            {
                                window = new MessageWindow();
                                hwnd = window.Handle;
                                mre.Set();
                                Application.Run();
                            });
                            t.Name = "MessageEvents message loop";
                            t.IsBackground = true;
                            t.Start();

                            mre.WaitOne();
                        }
                    }
                }
            }

            private class MessageWindow : Form
            {
                private ReaderWriterLock rwLock = new ReaderWriterLock();
                private Dictionary<int, EventHandler<MessageReceivedEventArgs>> handlers =
                    new Dictionary<int, EventHandler<MessageReceivedEventArgs>>();

                public void RegisterEventForMessage(int messageID, EventHandler<MessageReceivedEventArgs> handler)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    rwLock.AcquireWriterLock(Timeout.Infinite);
                    handlers[messageID] = handler;
                    rwLock.ReleaseWriterLock();
                }

                protected override void WndProc(ref Message m)
                {
                    rwLock.AcquireReaderLock(Timeout.Infinite);
                    bool handleMessage = handlers.ContainsKey(m.Msg);
                    rwLock.ReleaseReaderLock();

                    Message msg = m;

                    if (handleMessage)
                    {
                        MessageEvents.context.Post(delegate(object state)
                        {
                            EventHandler<MessageReceivedEventArgs> handler = handlers[msg.Msg];
                            if (handler != null)
                                handler(null, new MessageReceivedEventArgs((Message)state));
                        }, msg);
                    }

                    base.WndProc(ref m);
                }
            }
        }
    }

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
                return String.Format("{{\n\nOperatingMode={0}\n" +
                                     "Alpha={1}\n" +
                                     "ScaleFactor={2} " +
                                     "ScaleFactorUnits={3}\n" +
                                     "ExternalCommandSensitivity={4}" +
                                     " ExternalCommandSensitivityUnits={5}\n" +
                                     "RawScaleFator={6}, RawScaleFactorUnits={7}\n" +
                                     "SecondaryAlpha={8}\n" +
                                     "... }}",
                    OperatingMode, Alpha, ScaleFactor, ScaleFactorUnits, ExternalCommandSensitivity, ExternalCommandSensitivityUnits, RawScaleFactor, RawScaleFactorUnits, SecondaryAlpha);
            }
        }
    }



    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // According to http://www.rialverde.com/en/hobbies/ERVPhys.html, the serial number for the MultiClamp in demo mode
            // is 00000000. Given the UI, I'm assuming this is a 2-channel device and the ID numbers are 0 and 1 or 1 and 2; in either
            // case, 1 seems like a safe bet.
            //
            uint uSerialNum = 0;
            uint uChannelID = 1;

            // Give command-line arguments a chance to override default serial # and channel ID
            //
            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "/serial")
                        uSerialNum = UInt32.Parse(args[++i]);
                    if (args[i] == "/channel")
                        uChannelID = UInt32.Parse(args[++i]);
                }
            }

            Console.WriteLine("Registering for MultiClamp conversation...");
            int mctgOpenMsg = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_OPEN_MESSAGE_STR);
            Console.WriteLine("MCTG_OPEN_MESSAGE_STR = {0}", mctgOpenMsg);
            int mctgCloseMsg = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_CLOSE_MESSAGE_STR);
            Console.WriteLine("MCTG_CLOSE_MESSAGE_STR = {0}", mctgCloseMsg);
            int mctgReconnectMsg = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_RECONNECT_MESSAGE_STR);
            Console.WriteLine("MCTG_RECONNECT_MSG_STR = {0}", mctgReconnectMsg);
            int mctgRequestMsg = Win32Interop.RegisterWindowMessage(MulticlampInterop.MCTG_REQUEST_MESSAGE_STR);
         

            Console.WriteLine("Opening MultiClamp conversation...");
            UInt32 lParam = MulticlampInterop.MCTG_Pack700BSignalIDs(uSerialNum, uChannelID); // Pack the above two into an UInt32(?)
            int result = Win32Interop.PostMessage((IntPtr)0xffff, mctgOpenMsg, (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);
            Console.WriteLine("result = {0}", result);


            Console.WriteLine("Registering for WM_COPYDATA messages....");
            Win32Interop.MessageEvents.WatchMessage(Win32Interop.WM_COPYDATA, (sender, evtArgs) =>
                {
                    // WM_COPYDATA LPARAM is a pointer to a COPYDATASTRUCT structure
                    Win32Interop.COPYDATASTRUCT cds = new Win32Interop.COPYDATASTRUCT();
                    cds = (Win32Interop.COPYDATASTRUCT)
                        Marshal.PtrToStructure(evtArgs.Message.LParam, typeof(Win32Interop.COPYDATASTRUCT));

                    // WM_COPYDATA structure (COPYDATASTRUCT)
                    // dwData -- RegisterWindowMessage(MCTG_REQUEST_MESSAGE_STR)
                    // cbData -- size (in bytes) of the MC_TELEGRAPH_DATA structure being sent
                    // lpData -- MC_TELEGRAPH_DATA*
                    MulticlampInterop.MC_TELEGRAPH_DATA mtd = new MulticlampInterop.MC_TELEGRAPH_DATA();
                    mtd = (MulticlampInterop.MC_TELEGRAPH_DATA)Marshal.PtrToStructure(cds.lpData, typeof(MulticlampInterop.MC_TELEGRAPH_DATA));
                    MulticlampInterop.MulticlampData md = new MulticlampInterop.MulticlampData(mtd);
                    Console.WriteLine("Received WM_COPYDATA message; cracking it: {0}", md);
                });

            Console.WriteLine("Registering for MCTG_RECONNECT_MESSAGE messages...");
            Win32Interop.MessageEvents.WatchMessage(mctgReconnectMsg, (sndr, eArgs)=>
                                                                          {
                                                                              Console.WriteLine("Received MCTG_RECONNECT_MESSAGE: {0}", eArgs.Message);

                                                                              if ((IntPtr)lParam == eArgs.Message.LParam)
                                                                              {
                                                                                  Console.WriteLine("Opening MultiClamp conversation...");
                                                                                  int localResult = Win32Interop.PostMessage((IntPtr)0xffff, mctgOpenMsg, (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);
                                                                                  Console.WriteLine("result = {0}", localResult);

                                                                                  Console.WriteLine("Registering for WM_COPYDATA messages....");
                                                                                  Win32Interop.MessageEvents.WatchMessage(Win32Interop.WM_COPYDATA, (sender, evtArgs) =>
                                                                                  {
                                                                                      // WM_COPYDATA LPARAM is a pointer to a COPYDATASTRUCT structure
                                                                                      Win32Interop.COPYDATASTRUCT cds = new Win32Interop.COPYDATASTRUCT();
                                                                                      cds = (Win32Interop.COPYDATASTRUCT)
                                                                                          Marshal.PtrToStructure(evtArgs.Message.LParam, typeof(Win32Interop.COPYDATASTRUCT));

                                                                                      // WM_COPYDATA structure (COPYDATASTRUCT)
                                                                                      // dwData -- RegisterWindowMessage(MCTG_REQUEST_MESSAGE_STR)
                                                                                      // cbData -- size (in bytes) of the MC_TELEGRAPH_DATA structure being sent
                                                                                      // lpData -- MC_TELEGRAPH_DATA*
                                                                                      MulticlampInterop.MC_TELEGRAPH_DATA mtd = new MulticlampInterop.MC_TELEGRAPH_DATA();
                                                                                      mtd = (MulticlampInterop.MC_TELEGRAPH_DATA)Marshal.PtrToStructure(cds.lpData, typeof(MulticlampInterop.MC_TELEGRAPH_DATA));
                                                                                      MulticlampInterop.MulticlampData md = new MulticlampInterop.MulticlampData(mtd);
                                                                                      Console.WriteLine("Received WM_COPYDATA message; cracking it: {0}", md);
                                                                                  });
                                                                              }
                                                                          });


            Console.WriteLine("Requesting telegraph data...");
            result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, mctgRequestMsg,
                                                  (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);
            Console.WriteLine("  result = {0}", result);

            //var t = new Thread(() => Thread.Sleep(10 * 1000));
            //t.Start();

            Console.WriteLine("Press Enter to quit....");
            Console.ReadLine();

            Console.WriteLine("Closing MultiClamp conversation...");
            result = Win32Interop.PostMessage((IntPtr)0xffff, mctgCloseMsg, (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);
            Console.WriteLine("result = {0}", result);
        }
    }
}
