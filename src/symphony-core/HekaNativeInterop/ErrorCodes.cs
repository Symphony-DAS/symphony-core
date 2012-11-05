
namespace Heka.NativeInterop
{
    public partial class ITCMM
    {
        /// ACQ_SUCCESS -> 0
        public const int ACQ_SUCCESS = 0;

        /// ACQ_SYSTEM_ERROR -> 0xE0001000
        public const int ACQ_SYSTEM_ERROR = -536866816;

        /// ACQ_LIBRARY_ERROR -> 0xE0011000
        public const int ACQ_LIBRARY_ERROR = -536801280;

        /// Error_DeviceIsNotSupported -> 0xF0001000
        public const int Error_DeviceIsNotSupported = -268431360;

        /// Error_UserVersionID -> 0x80001000
        public const int Error_UserVersionID = -2147479552;

        /// Error_KernelVersionID -> 0x81001000
        public const int Error_KernelVersionID = -2130702336;

        /// Error_DSPVersionID -> 0x82001000
        public const int Error_DSPVersionID = -2113925120;

        /// Error_TimerIsRunning -> 0x8CD01000
        public const int Error_TimerIsRunning = -1932521472;

        /// Error_TimerIsDead -> 0x8CD11000
        public const int Error_TimerIsDead = -1932455936;

        /// Error_TimerIsWeak -> 0x8CD21000
        public const int Error_TimerIsWeak = -1932390400;

        /// Error_MemoryAllocation -> 0x80401000
        public const int Error_MemoryAllocation = -2143285248;

        /// Error_MemoryFree -> 0x80411000
        public const int Error_MemoryFree = -2143219712;

        /// Error_MemoryError -> 0x80421000
        public const int Error_MemoryError = -2143154176;

        /// Error_MemoryExist -> 0x80431000
        public const int Error_MemoryExist = -2143088640;

        /// Warning_AcqIsRunning -> 0x80601000
        public const int Warning_AcqIsRunning = -2141188096;

        /// Warning_NotAvailable -> 0x80671000
        public const int Warning_NotAvailable = -2140729344;

        /// Error_TIMEOUT -> 0x80301000
        public const int Error_TIMEOUT = -2144333824;

        /// Error_OpenRegistry -> 0x8D101000
        public const int Error_OpenRegistry = -1928327168;

        /// Error_WriteRegistry -> 0x8DC01000
        public const int Error_WriteRegistry = -1916792832;

        /// Error_ReadRegistry -> 0x8DB01000
        public const int Error_ReadRegistry = -1917841408;

        /// Error_ParamRegistry -> 0x8D701000
        public const int Error_ParamRegistry = -1922035712;

        /// Error_CloseRegistry -> 0x8D201000
        public const int Error_CloseRegistry = -1927278592;

        /// Error_Open -> 0x80101000
        public const int Error_Open = -2146430976;

        /// Error_Close -> 0x80201000
        public const int Error_Close = -2145382400;

        /// Error_WrongMode -> 0x80061000
        public const int Error_WrongMode = -2147086336;

        /// Error_DeviceIsBusy -> 0x82601000
        public const int Error_DeviceIsBusy = -2107633664;

        /// Error_AreadyOpen -> 0x80111000
        public const int Error_AreadyOpen = -2146365440;

        /// Error_NotOpen -> 0x80121000
        public const int Error_NotOpen = -2146299904;

        /// Error_NotInitialized -> 0x80D01000
        public const int Error_NotInitialized = -2133848064;

        /// Error_Parameter -> 0x80701000
        public const int Error_Parameter = -2140139520;

        /// Error_ParameterSize -> 0x80A01000
        public const int Error_ParameterSize = -2136993792;

        /// Error_Config -> 0x89001000
        public const int Error_Config = -1996484608;

        /// Error_InputMode -> 0x80611000
        public const int Error_InputMode = -2141122560;

        /// Error_OutputMode -> 0x80621000
        public const int Error_OutputMode = -2141057024;

        /// Error_Direction -> 0x80631000
        public const int Error_Direction = -2140991488;

        /// Error_ChannelNumber -> 0x80641000
        public const int Error_ChannelNumber = -2140925952;

        /// Error_SamplingRate -> 0x80651000
        public const int Error_SamplingRate = -2140860416;

        /// Error_StartOffset -> 0x80661000
        public const int Error_StartOffset = -2140794880;

        /// Error_Software -> 0x8FF01000
        public const int Error_Software = -1880092672;

        /// Error_Function_Mask -> 0xFFFFFF00
        public const int Error_Function_Mask = -256;

    }
}
