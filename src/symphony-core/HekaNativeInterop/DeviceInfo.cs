
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        public struct VersionInfo
        {

            /// int
            public int Major;

            /// int
            public int Minor;

            /// char[80]
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
            public string description;

            /// char[80]
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
            public string date;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct GlobalDeviceInfo
        {

            /// unsigned int
            public uint DeviceType;

            /// unsigned int
            public uint DeviceNumber;

            /// unsigned int
            public uint PrimaryFIFOSize;

            /// unsigned int
            public uint SecondaryFIFOSize;

            /// unsigned int
            public uint LoadedFunction;

            /// unsigned int
            public uint SoftKey;

            /// unsigned int
            public uint Mode;

            /// unsigned int
            public uint MasterSerialNumber;

            /// unsigned int
            public uint SecondarySerialNumber;

            /// unsigned int
            public uint HostSerialNumber;

            /// unsigned int
            public uint NumberOfDACs;

            /// unsigned int
            public uint NumberOfADCs;

            /// unsigned int
            public uint NumberOfDOs;

            /// unsigned int
            public uint NumberOfDIs;

            /// unsigned int
            public uint NumberOfAUXOs;

            /// unsigned int
            public uint NumberOfAUXIs;

            /// unsigned int
            public uint MinimumSamplingInterval;

            /// unsigned int
            public uint MinimumSamplingStep;

            /// unsigned int
            public uint FirmwareVersion0;

            /// unsigned int
            public uint Reserved1;
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct ExtendedGlobalDeviceInfo
        {

            /// char*
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPStr)]
            public string u2ffile;

            /// HANDLE->void*
            public System.IntPtr DriverHandle;

            /// int
            public int Reserved1;

            /// int
            public int Reserved2;

            /// int
            public int DSP_Type;

            /// int
            public int HOSTLCA_Type;

            /// int
            public int RACKLCA_Type;

            /// int
            public int LoadMode;
        }

    }
}
