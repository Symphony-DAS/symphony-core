
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {
        /// unsigned int
        public uint ModeNumberOfPoints;

        /// unsigned int
        public uint ChannelType;

        /// unsigned int
        public uint ChannelNumber;

        /// unsigned int
        public uint Reserved0;

        /// unsigned int
        public uint ErrorMode;

        /// unsigned int
        public uint ErrorState;

        /// void*
        public System.IntPtr FIFOPointer;

        /// unsigned int
        public uint FIFONumberOfPoints;

        /// unsigned int
        public uint ModeOfOperation;

        /// unsigned int
        public uint SizeOfModeParameters;

        /// void*
        public System.IntPtr ModeParameters;

        /// unsigned int
        public uint SamplingIntervalFlag;

        /// double
        public double SamplingRate;

        /// double
        public double StartOffset;

        /// double
        public double Gain;

        /// double
        public double Offset;

        /// unsigned int
        public uint ExternalDecimation;

        /// unsigned int
        public uint HardwareUnderrunValue;

        /// unsigned int
        public uint Reserved2;

        /// unsigned int
        public uint Reserved3;

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCSpecialOutput
        {

            /// int
            public int Mode;

            /// int
            public int Coeff0;

            /// int
            public int Coeff1;

            /// int
            public int Coeff2;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCSpecialOutputTable
        {

            /// unsigned int
            public uint StartAddress;

            /// unsigned int
            public uint TableSize;

            /// int*
            public System.IntPtr Data;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ITCChannelInfo
        {

            /// unsigned int
            public uint ModeNumberOfPoints;

            /// unsigned int
            public uint ChannelType;

            /// unsigned int
            public uint ChannelNumber;

            /// unsigned int
            public uint Reserved0;

            /// unsigned int
            public uint ErrorMode;

            /// unsigned int
            public uint ErrorState;

            /// void*
            public System.IntPtr FIFOPointer;

            /// unsigned int
            public uint FIFONumberOfPoints;

            /// unsigned int
            public uint ModeOfOperation;

            /// unsigned int
            public uint SizeOfModeParameters;

            /// void*
            public System.IntPtr ModeParameters;

            /// unsigned int
            public uint SamplingIntervalFlag;

            /// double
            public double SamplingRate;

            /// double
            public double StartOffset;

            /// double
            public double Gain;

            /// double
            public double Offset;

            /// unsigned int
            public uint ExternalDecimation;

            /// unsigned int
            public uint HardwareUnderrunValue;

            /// unsigned int
            public uint Reserved2;

            /// unsigned int
            public uint Reserved3;
        }
    }
}
