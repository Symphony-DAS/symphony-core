using System.Runtime.InteropServices;
using System;

namespace Heka.NativeInterop
{


    public partial class ITCMM
    {

        /// Return Type: unsigned int
        ///GlobalConfig: ITCGlobalConfig*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GlobalConfig", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GlobalConfig(ref ITCGlobalConfig GlobalConfig);


        /// Return Type: unsigned int
        ///DeviceType: unsigned int
        ///DeviceNumber: unsigned int*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_Devices", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_Devices(uint DeviceType, ref uint DeviceNumber);


        /// Return Type: unsigned int
        ///DeviceType: unsigned int
        ///DeviceNumber: unsigned int
        ///DeviceHandle: HANDLE*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetDeviceHandle", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetDeviceHandle(uint DeviceType, uint DeviceNumber, out System.IntPtr DeviceHandle);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///DeviceType: unsigned int*
        ///DeviceNumber: unsigned int*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetDeviceType", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetDeviceType(System.IntPtr DeviceHandle, ref uint DeviceType, ref uint DeviceNumber);


        /// Return Type: unsigned int
        ///DeviceType: unsigned int
        ///DeviceNumber: unsigned int
        ///Mode: unsigned int
        ///DeviceHandle: HANDLE*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_OpenDevice", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_OpenDevice(uint DeviceType, uint DeviceNumber, uint Mode, out System.IntPtr DeviceHandle);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_CloseDevice", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_CloseDevice(System.IntPtr DeviceHandle);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sHWFunction: HWFunction*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_InitDevice", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint ITC_InitDevice(System.IntPtr DeviceHandle, [In]System.IntPtr hwFunction);//[In]ref HWFunction sHWFunction);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///file_path: char*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_LogOpen", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_LogOpen(System.IntPtr DeviceHandle, [System.Runtime.InteropServices.InAttribute()] [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPStr)] string file_path);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///mask: int
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_LogEnable", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_LogEnable(System.IntPtr DeviceHandle, int mask);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_LogDisable", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_LogDisable(System.IntPtr DeviceHandle);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_LogClose", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_LogClose(System.IntPtr DeviceHandle);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sDeviceInfo: GlobalDeviceInfo*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetDeviceInfo", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetDeviceInfo(System.IntPtr DeviceHandle, ref GlobalDeviceInfo sDeviceInfo);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sDeviceInfoEx: ExtendedGlobalDeviceInfo*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetDeviceInfoEx", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetDeviceInfoEx(System.IntPtr DeviceHandle, ref ExtendedGlobalDeviceInfo sDeviceInfoEx);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///ThisDriverVersion: VersionInfo*
        ///KernelLevelDriverVersion: VersionInfo*
        ///HardwareVersion: VersionInfo*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetVersions", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetVersions(System.IntPtr DeviceHandle, ref VersionInfo ThisDriverVersion, ref VersionInfo KernelLevelDriverVersion, ref VersionInfo HardwareVersion);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///HostSerialNumber: unsigned int*
        ///MasterBoxSerialNumber: unsigned int*
        ///SlaveBoxSerialNumber: unsigned int*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetSerialNumbers", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetSerialNumbers(System.IntPtr DeviceHandle, ref uint HostSerialNumber, ref uint MasterBoxSerialNumber, ref uint SlaveBoxSerialNumber);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///Status: int
        ///Text: char*
        ///MaxCharacters: unsigned int
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetStatusText", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetStatusText(System.IntPtr DeviceHandle, int Status, System.IntPtr Text, uint MaxCharacters);


        /// Return Type: unsigned int
        ///Status: int
        ///Text: char*
        ///MaxCharacters: unsigned int
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_AnalyzeError", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_AnalyzeError(int Status, System.IntPtr Text, uint MaxCharacters);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///SoftKey: unsigned int
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_SetSoftKey", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_SetSoftKey(System.IntPtr DeviceHandle, uint SoftKey);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sParam: ITCStatus*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetState", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetState(System.IntPtr DeviceHandle, ref ITCStatus sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sParam: ITCStatus*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_SetState", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_SetState(System.IntPtr DeviceHandle, ref ITCStatus sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///sParam: ITCChannelDataEx*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetFIFOInformation", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetFIFOInformation(System.IntPtr DeviceHandle,
            uint NumberOfChannels,
            [In,Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)]
            ITCChannelDataEx[] Channels);//[In,Out,MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ITCChannelInfo[] Channels);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///Seconds: double*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetTime", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetTime(System.IntPtr DeviceHandle, out double Seconds);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_ResetChannels", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_ResetChannels(System.IntPtr DeviceHandle);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///Channels: ITCChannelInfo*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_SetChannels", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_SetChannels(System.IntPtr DeviceHandle, 
            uint NumberOfChannels, 
            [In,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)] 
            ITCChannelInfo[] Channels);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_UpdateChannels", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_UpdateChannels(System.IntPtr DeviceHandle);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///Channels: ITCChannelInfo*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetChannels", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetChannels(System.IntPtr DeviceHandle, 
            uint NumberOfChannels,
            [In,Out,MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] 
            ITCChannelInfo[] Channels);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sITCConfig: ITCPublicConfig*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_ConfigDevice", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_ConfigDevice(System.IntPtr DeviceHandle, ref ITCPublicConfig sITCConfig);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sParam: ITCStartInfo*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_Start", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_Start(System.IntPtr DeviceHandle, ref ITCStartInfo sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sParam: void*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_Stop", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_Stop(System.IntPtr DeviceHandle, System.IntPtr sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///sParam: void*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_UpdateNow", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_UpdateNow(System.IntPtr DeviceHandle, System.IntPtr sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///sParam: ITCLimited*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_SingleScan", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_SingleScan(System.IntPtr DeviceHandle, 
            uint NumberOfChannels, 
            [In,Out,MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
            ITCLimited[] sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///sParam: ITCChannelDataEx*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_AsyncIO", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_AsyncIO(System.IntPtr DeviceHandle, 
            uint NumberOfChannels,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ITCChannelDataEx[] sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///sParam: ITCChannelDataEx*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetDataAvailable", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetDataAvailable(System.IntPtr DeviceHandle, 
            uint NumberOfChannels, 
            [In,Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)] 
            ITCChannelDataEx[] sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///sParam: ITCChannelDataEx*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_UpdateFIFOPosition", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_UpdateFIFOPosition(System.IntPtr DeviceHandle, 
            uint NumberOfChannels,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ITCChannelDataEx[] sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///sParam: ITCChannelDataEx*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_ReadWriteFIFO", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_ReadWriteFIFO(System.IntPtr DeviceHandle, 
            uint NumberOfChannels,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ITCChannelDataEx[] sParam);


        /// Return Type: unsigned int
        ///DeviceHandle: HANDLE->void*
        ///NumberOfChannels: unsigned int
        ///sParam: ITCChannelDataEx*
        [System.Runtime.InteropServices.DllImportAttribute("ITCMM.dll", EntryPoint = "ITC_GetFIFOPointers", CallingConvention=CallingConvention.Cdecl)]
        public static extern uint ITC_GetFIFOPointers(System.IntPtr DeviceHandle, 
            uint NumberOfChannels,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
            ITCChannelDataEx[] sParam);
    }
}
