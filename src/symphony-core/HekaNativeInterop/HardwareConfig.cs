
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct HWFunction
        {

            /// unsigned int
            public uint Mode;

            /// void*
            public System.IntPtr U2F_File;

            /// unsigned int
            public uint SizeOfSpecialFunction;

            /// void*
            public System.IntPtr SpecialFunction;

            /// unsigned int
            public uint Reserved;

            /// unsigned int
            public uint id;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITC1600_Special_HWFunction
        {

            /// unsigned int
            public uint Function;

            /// unsigned int
            public uint DSPType;

            /// unsigned int
            public uint HOSTType;

            /// unsigned int
            public uint RACKType;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITC00_Special_HWFunction
        {

            /// unsigned int
            public uint Function;

            /// unsigned int
            public uint DSPType;

            /// unsigned int
            public uint HOSTType;

            /// unsigned int
            public uint RACKType;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITC18_Special_HWFunction
        {

            /// unsigned int
            public uint Function;

            /// void*
            public System.IntPtr InterfaceData;

            /// void*
            public System.IntPtr IsolatedData;

            /// unsigned int
            public uint Reserved;
        }

        /// USE_TRIG_IN -> 0x01
        public const int USE_TRIG_IN = 1;

        /// USE_TRIG_IN_HOST -> 0x02
        public const int USE_TRIG_IN_HOST = 2;

        /// USE_TRIG_IN_TIMER -> 0x04
        public const int USE_TRIG_IN_TIMER = 4;

        /// USE_TRIG_IN_RACK -> 0x08
        public const int USE_TRIG_IN_RACK = 8;

        /// USE_TRIG_IN_FDI0 -> 0x10
        public const int USE_TRIG_IN_FDI0 = 16;

        /// USE_TRIG_IN_FDI1 -> 0x20
        public const int USE_TRIG_IN_FDI1 = 32;

        /// USE_TRIG_IN_FDI2 -> 0x40
        public const int USE_TRIG_IN_FDI2 = 64;

        /// USE_TRIG_IN_FDI3 -> 0x80
        public const int USE_TRIG_IN_FDI3 = 128;

        /// TRIG_IN_MASK -> 0xFF
        public const int TRIG_IN_MASK = 255;

        /// USE_HARD_TRIG_IN -> 0x100
        public const int USE_HARD_TRIG_IN = 256;

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCPublicConfig
        {

            /// unsigned int
            public uint DigitalInputMode;

            /// unsigned int
            public uint ExternalTriggerMode;

            /// unsigned int
            public uint ExternalTrigger;

            /// unsigned int
            public uint EnableExternalClock;

            /// unsigned int
            public uint DACShiftValue;

            /// unsigned int
            public uint InputRange;

            /// unsigned int
            public uint TriggerOutPosition;

            /// unsigned int
            public uint OutputEnable;

            /// unsigned int
            public uint SequenceLength;

            /// unsigned int*
            public System.IntPtr Sequence;

            /// unsigned int
            public uint SequenceLengthIn;

            /// unsigned int*
            public System.IntPtr SequenceIn;

            /// unsigned int
            public uint ResetFIFOFlag;

            /// unsigned int
            public uint ControlLight;

            /// double
            public double SamplingInterval;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCGlobalConfig
        {

            /// int
            public int SoftwareFIFOSize;

            /// int
            public int HardwareFIFOSize_A;

            /// int
            public int HardwareFIFOSize_B;

            /// int
            public int TransferSizeLimitation;

            /// int
            public int Reserved0;

            /// int
            public int Reserved1;

            /// int
            public int Reserved2;

            /// int
            public int Reserved3;
        }


    }
}
