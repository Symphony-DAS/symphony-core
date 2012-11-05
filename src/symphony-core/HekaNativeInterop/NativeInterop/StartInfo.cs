
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {
        /// DontUseTimerThread -> 1
        public const int DontUseTimerThread = 1;

        /// FastPointerUpdate -> 2
        public const int FastPointerUpdate = 2;

        /// ShortDataAcquisition -> 4
        public const int ShortDataAcquisition = 4;

        /// TimerResolutionMask -> 0x00FF0000
        public const int TimerResolutionMask = 16711680;

        /// TimerIntervalMask -> 0xFF000000
        public const int TimerIntervalMask = -16777216;

        /// TimerResolutionShift -> 16
        public const int TimerResolutionShift = 16;

        /// TimerIntervalShift -> 24
        public const int TimerIntervalShift = 24;


        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCStartInfo
        {

            /// unsigned int
            public uint ExternalTrigger;

            /// unsigned int
            public uint OutputEnable;

            /// unsigned int
            public uint StopOnOverflow;

            /// unsigned int
            public uint StopOnUnderrun;

            /// unsigned int
            public uint RunningOption;

            /// unsigned int
            public uint ResetFIFOs;

            /// unsigned int
            public uint NumberOf640usToRun;

            /// unsigned int
            public uint Reserved3;

            /// double
            public double StartTime;

            /// double
            public double StopTime;
        }
    }
}
