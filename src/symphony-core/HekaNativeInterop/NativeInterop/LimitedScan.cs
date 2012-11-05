
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {
        /// ITC16_MaximumSingleScan -> 16*1024
        public const int ITC16_MaximumSingleScan = (16 * 1024);

        /// ITC18_MaximumSingleScan -> 256*1024
        public const int ITC18_MaximumSingleScan = (256 * 1024);

        /// ITC1600_MaximumSingleScan -> 1024
        public const int ITC1600_MaximumSingleScan = 1024;

        /// ITC00_MaximumSingleScan -> 1024
        public const int ITC00_MaximumSingleScan = 1024;

        /// ITC16_MAX_SEQUENCE_LENGTH -> 16
        public const int ITC16_MAX_SEQUENCE_LENGTH = 16;

        /// ITC18_MAX_SEQUENCE_LENGTH -> 16
        public const int ITC18_MAX_SEQUENCE_LENGTH = 16;

        /// ITC1600_MAX_SEQUENCE_LENGTH -> 16
        public const int ITC1600_MAX_SEQUENCE_LENGTH = 16;

        /// ITC00_MAX_SEQUENCE_LENGTH -> 16
        public const int ITC00_MAX_SEQUENCE_LENGTH = 16;

        /// ITC18_NOP_CHANNEL -> 0x80000000
        public const int ITC18_NOP_CHANNEL = -2147483648;

        /// ITC1600_NOP_CHANNEL_RACK0 -> 0x80000000
        public const int ITC1600_NOP_CHANNEL_RACK0 = -2147483648;

        /// ITC1600_NOP_CHANNEL_RACK1 -> 0x80000001
        public const int ITC1600_NOP_CHANNEL_RACK1 = -2147483647;

        /// ITC00_NOP_CHANNEL_RACK0 -> 0x80000000
        public const int ITC00_NOP_CHANNEL_RACK0 = -2147483648;

        /// ITC00_NOP_CHANNEL_RACK1 -> 0x80000001
        public const int ITC00_NOP_CHANNEL_RACK1 = -2147483647;

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCLimited
        {

            /// unsigned int
            public uint ChannelType;

            /// unsigned int
            public uint ChannelNumber;

            /// unsigned int
            public uint Reserved0;

            /// unsigned int
            public uint Reserved1;

            /// unsigned int
            public uint Reserved2;

            /// unsigned int
            public uint NumberOfPoints;

            /// unsigned int
            public uint DecimateMode;

            /// void*
            public System.IntPtr Data;
        }
    }
}
