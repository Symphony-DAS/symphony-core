
namespace Heka.NativeInterop
{

    public partial class ITCMM
    {

        /// READ_TOTALTIME -> 0x01
        public const int READ_TOTALTIME = 1;

        /// READ_RUNTIME -> 0x02
        public const int READ_RUNTIME = 2;

        /// READ_ERRORS -> 0x04
        public const int READ_ERRORS = 4;

        /// READ_RUNNINGMODE -> 0x08
        public const int READ_RUNNINGMODE = 8;

        /// READ_OVERFLOW -> 0x10
        public const int READ_OVERFLOW = 16;

        /// READ_CLIPPING -> 0x20
        public const int READ_CLIPPING = 32;

        /// READ_ASYN_ADC -> 0x40
        public const int READ_ASYN_ADC = 64;

        /// RACKLCAISALIVE -> 0x80000000
        public const int RACKLCAISALIVE = -2147483648;

        /// PLLERRORINDICATOR -> 0x08000000
        public const int PLLERRORINDICATOR = 134217728;

        /// RACK0MODEMASK -> 0x70000000
        public const int RACK0MODEMASK = 1879048192;

        /// RACK1MODEMASK -> 0x07000000
        public const int RACK1MODEMASK = 117440512;

        /// RACK0IDERROR -> 0x00020000
        public const int RACK0IDERROR = 131072;

        /// RACK1IDERROR -> 0x00010000
        public const int RACK1IDERROR = 65536;

        /// RACK0CRCERRORMASK -> 0x0000FF00
        public const int RACK0CRCERRORMASK = 65280;

        /// RACK1CRCERRORMASK -> 0x000000FF
        public const int RACK1CRCERRORMASK = 255;

        /// SWITCH_OUTPUT_LINE0 -> 1
        public const int SWITCH_OUTPUT_LINE0 = 1;

        /// SWITCH_OUTPUT_LINE1 -> 2
        public const int SWITCH_OUTPUT_LINE1 = 2;

        /// SWITCH_OUTPUT_LINE0_LINE1 -> 3
        public const int SWITCH_OUTPUT_LINE0_LINE1 = 3;

        /// SWITCH_OUTPUT_MASK -> 3
        public const int SWITCH_OUTPUT_MASK = 3;

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCStatus
        {

            /// unsigned int
            public uint CommandStatus;

            /// unsigned int
            public uint RunningMode;

            /// unsigned int
            public uint Overflow;

            /// unsigned int
            public uint Clipping;

            /// unsigned int
            public uint State;

            /// unsigned int
            public uint Reserved0;

            /// unsigned int
            public uint Reserved1;

            /// unsigned int
            public uint Reserved2;

            /// double
            public double TotalSeconds;

            /// double
            public double RunSeconds;
        }

    }

}
