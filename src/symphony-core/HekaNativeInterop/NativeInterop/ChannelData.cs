
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {
        /// RESET_FIFO_COMMAND -> 0x00010000
        public const uint RESET_FIFO_COMMAND = 0x00010000;

        /// PRELOAD_FIFO_COMMAND -> 0x00020000
        public const uint PRELOAD_FIFO_COMMAND = 0x00020000;

        /// LAST_FIFO_COMMAND -> 0x00040000
        public const uint LAST_FIFO_COMMAND = 0x00040000;

        /// FLUSH_FIFO_COMMAND -> 0x00080000
        public const uint FLUSH_FIFO_COMMAND = 0x00080000;

        /// ITC_SET_SHORT_ACQUISITION -> 0x00100000
        public const uint ITC_SET_SHORT_ACQUISITION = 0x00100000;

        /// READ_OUTPUT_ONLY -> 0x00200000
        public const uint READ_OUTPUT_ONLY = 0x00200000;

        /// DISABLE_CALIBRATION -> 0x00400000
        public const int DISABLE_CALIBRATION = 4194304;

        /// RESET_FIFO_COMMAND_EX -> 0x0001
        public const int RESET_FIFO_COMMAND_EX = 1;

        /// PRELOAD_FIFO_COMMAND_EX -> 0x0002
        public const int PRELOAD_FIFO_COMMAND_EX = 2;

        /// LAST_FIFO_COMMAND_EX -> 0x0004
        public const int LAST_FIFO_COMMAND_EX = 4;

        /// FLUSH_FIFO_COMMAND_EX -> 0x0008
        public const int FLUSH_FIFO_COMMAND_EX = 8;

        /// ITC_SET_SHORT_ACQUISITION_EX -> 0x0010
        public const int ITC_SET_SHORT_ACQUISITION_EX = 16;

        /// READ_OUTPUT_ONLY_EX -> 0x0020
        public const int READ_OUTPUT_ONLY_EX = 32;

        /// DISABLE_CALIBRATION_EX -> 0x0040
        public const int DISABLE_CALIBRATION_EX = 64;

        /// SKIP_INSERT_POINTS_EX -> 0x0080
        public const int SKIP_INSERT_POINTS_EX = 128;

        /// READ_FIFO_INFO -> 0
        public const int READ_FIFO_INFO = 0;

        /// READ_FIFO_READ_POINTER_COUNTER -> 1
        public const int READ_FIFO_READ_POINTER_COUNTER = 1;

        /// READ_FIFO_WRITE_POINTER_COUNTER -> 2
        public const int READ_FIFO_WRITE_POINTER_COUNTER = 2;


        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCChannelDataEx
        {

            /// unsigned short
            public ushort ChannelType;

            /// unsigned short
            public ushort Command;

            /// unsigned short
            public ushort ChannelNumber;

            /// unsigned short
            public ushort Status;

            /// unsigned int
            public uint Value;

            /// void*
            /// Is actually an array of short[Value].
            public System.IntPtr DataPointer;

        }
    }
}
