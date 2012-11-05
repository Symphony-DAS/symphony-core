// Copyright 2005-2010 HEKA Electronik GmbH

//0acqErrors.h

#ifndef __0acqErrors_h_
#define __0acqErrors_h_

#define	ACQ_SUCCESS						0

#define	ACQ_SYSTEM_ERROR				0xE0001000
#define	ACQ_LIBRARY_ERROR				0xE0011000

#define	Error_DeviceIsNotSupported		0xF0001000	//1000 0000 0000 0001 0000 0000 0000
#define	Error_UserVersionID				0x80001000
#define	Error_KernelVersionID			0x81001000
#define	Error_DSPVersionID				0x82001000

#define	Error_TimerIsRunning			0x8CD01000
#define	Error_TimerIsDead				0x8CD11000
#define	Error_TimerIsWeak				0x8CD21000

#define	Error_MemoryAllocation			0x80401000
#define	Error_MemoryFree				0x80411000
#define	Error_MemoryError				0x80421000
#define	Error_MemoryExist				0x80431000

#define	Warning_AcqIsRunning			0x80601000
#define	Warning_NotAvailable			0x80671000
#define	Error_TIMEOUT					0x80301000

#define	Error_OpenRegistry				0x8D101000
#define	Error_WriteRegistry				0x8DC01000
#define	Error_ReadRegistry				0x8DB01000
#define	Error_ParamRegistry				0x8D701000	
#define	Error_CloseRegistry				0x8D201000

#define	Error_Open						0x80101000
#define	Error_Close						0x80201000

#define	Error_WrongMode					0x80061000

#define	Error_DeviceIsBusy				0x82601000

#define	Error_AreadyOpen				0x80111000

#define	Error_NotOpen					0x80121000
#define	Error_NotInitialized			0x80D01000

#define	Error_Parameter					0x80701000
#define	Error_ParameterSize				0x80A01000

#define	Error_Config					0x89001000

#define	Error_InputMode					0x80611000
#define	Error_OutputMode				0x80621000
#define	Error_Direction					0x80631000
#define	Error_ChannelNumber				0x80641000
#define	Error_SamplingRate				0x80651000
#define	Error_StartOffset				0x80661000

#define	Error_Software					0x8FF01000

#define	Error_Function_Mask				0xFFFFFF00

//MATLAB ERRORs
#define	Error_Matlab_FunctionNotImplemented		0xA0001300
#define	Error_Matlab_MemoryAllocation			0xA0401300
#define	Error_Matlab_NeedParameters_2			0xA0701300
#define	Error_Matlab_NeedParameters_3			0xA0711300
#define	Error_Matlab_NeedParameters_4			0xA0721300
#define	Error_Matlab_NeedReturnValue			0xA0731300
#define	Error_Matlab_NeedStructure				0xA0741300
#define	Error_Matlab_WrongSize					0xA0601300


#endif
