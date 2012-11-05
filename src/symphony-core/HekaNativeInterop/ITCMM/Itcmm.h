//Main header file for ITC software development
//Latest revision: 6.14.2005
//Copyright 2005, Instrutech Corp.

#ifndef __0acq_h_	//ignore whole file, if already processed
#define __0acq_h_

#ifdef __cplusplus
extern "C" {  // for export C function in C++ source code
#endif

//#ifdef _WINDOWS
//   //For GCC 5.11.2005
//   #define __WIN32
#ifdef _WINDOWS
	#include <windows.h>
#else
   #ifndef ITC_NO_COMPATIBILITY_INCLUDE
	  #include "Compatibility.h"
   #else
	  typedef void* HANDLE;
   #endif
#endif

#include "ExportDefs.h"

#define ITCmm_MAJOR_VERSION                     // compared with MAJOR_VERSION in 0acqGlobal.h
#define ITC_CURRENT_DRIVER_VERSION  (ITCmm_MAJOR_VERSION << 19) | 14

#define USB16_CURRENT_FIRMWARE		(6 << 16) | 0
#define USB18_CURRENT_FIRMWARE		(6 << 16) | 0

//***************************************************************************

//Device Types and Limitations
#define	MAX_DEVICE_TYPE_NUMBER		6
#define	ITC16_ID					0
#define	ITC16_MAX_DEVICE_NUMBER		16
#define	ITC18_ID					1
#define	ITC18_MAX_DEVICE_NUMBER		16
#define	ITC1600_ID				2
#define	ITC1600_MAX_DEVICE_NUMBER	16
#define	ITC00_ID					3
#define	ITC00_MAX_DEVICE_NUMBER		16

#define	USB16_ID					4
#define	USB16_MAX_DEVICE_NUMBER		16
#define	USB18_ID					5
#define	USB18_MAX_DEVICE_NUMBER		16


#define	ITC_MAX_DEVICE_NUMBER		16

//Additional flag to check for all supported devices in ITC_Devices and ITC_OpenDevice
#define	ITC_ALL_DEVICES_ID			-1


//***************************************************************************
#define	NORMAL_MODE					0		//For backward compatibility
#define	SMART_MODE					1		//Main mode for new development
//***************************************************************************

//Channel Types and Limitations
#define NUMBER_OF_CHANNEL_GROUPS	6			//Currently 6 are implemented
#define MAX_NUMBER_OF_CHANNELS_IN_GROUP	32		//Maximum number

#define D2H							0x00		//Input
#define H2D							0x01		//Output
#define INPUT_GROUP					D2H			//Input
#define OUTPUT_GROUP				H2D			//Input

#define DIGITAL_INPUT				0x02		//Digital Input
#define DIGITAL_OUTPUT				0x03		//Digital Output

#define AUX_INPUT					0x04		//Aux Input
#define AUX_OUTPUT					0x05		//Aux Output

#define TEMP_INPUT					0x06		//Temp Input

//STUB -> check the correct number
#define NUMBER_OF_D2H_CHANNELS		32			//ITC1600: 8+F+S0+S1+4(AUX)+T == 16 * 2 = 32
#define NUMBER_OF_H2D_CHANNELS		16			//ITC1600: 4+F+S0+S1 == 7 * 2 = 14 + 1-Host-Aux

//STUB -> Move this object to the Registry
#define	ITC18_SOFTWARE_SEQUENCE_SIZE	4096
//STUB ->Verify
#define	ITC16_SOFTWARE_SEQUENCE_SIZE	256
#define	ITC1600_SOFTWARE_SEQUENCE_SIZE	16
#define	ITC00_SOFTWARE_SEQUENCE_SIZE	16

#define	ITC18_NUMBEROFCHANNELS		16			//4 + 8 + 2 + 1 + 1
#define	ITC18_NUMBEROFOUTPUTS		 7			//4 + 2 + 1
#define	ITC18_NUMBEROFINPUTS		 9			//8 + 1

#define	USB18_NUMBEROFCHANNELS		18			//4 + 8 + 2 + 1 + 1 + 2
#define	USB18_NUMBEROFOUTPUTS		 8			//4 + 2 + 1 + 1
#define	USB18_NUMBEROFINPUTS		10			//8 + 1 + 1

#define	ITC18_NUMBEROFADCINPUTS		 8
#define	ITC18_NUMBEROFDACOUTPUTS	 4
#define	ITC18_NUMBEROFDIGINPUTS		 1
#define	ITC18_NUMBEROFDIGOUTPUTS	 2
#define	ITC18_NUMBEROFAUXINPUTS		 0
#define	ITC18_NUMBEROFAUXOUTPUTS	 1

#define	USB18_NUMBEROFAUXINPUTS		 1
#define	USB18_NUMBEROFAUXOUTPUTS	 2

#define ITC18_DA_CH_MASK			0x3			//4 DA Channels
#define ITC18_DO0_CH				0x4			//DO0
#define ITC18_DO1_CH				0x5			//DO1
#define ITC18_AUX_CH				0x6			//AUX

#define USB18_AUX_CH_IN				0x9			//USB IN 0
#define USB18_AUX_CH_OUT			0x7			//USB OUT 0

#define	ITC16_NUMBEROFCHANNELS		14			//4 + 8 + 1 + 1
#define	ITC16_NUMBEROFOUTPUTS		 5			//4 + 1
#define	ITC16_NUMBEROFINPUTS		 9			//8 + 1
#define	ITC16_DO_CH					 4

#define	USB16_NUMBEROFCHANNELS		16
#define	USB16_NUMBEROFOUTPUTS		 6
#define	USB16_NUMBEROFINPUTS		10

#define	ITC16_NUMBEROFADCINPUTS		 8
#define	ITC16_NUMBEROFDACOUTPUTS	 4
#define	ITC16_NUMBEROFDIGINPUTS		 1
#define	ITC16_NUMBEROFDIGOUTPUTS	 1
#define	ITC16_NUMBEROFAUXINPUTS		 0
#define	ITC16_NUMBEROFAUXOUTPUTS	 0

#define	USB16_NUMBEROFAUXINPUTS		 1
#define	USB16_NUMBEROFAUXOUTPUTS	 1

#define USB16_AUX_CH_IN				0x9			//USB IN 0
#define USB16_AUX_CH_OUT			0x5			//USB OUT 0

//STUB: Check the numbers
#define	ITC1600_NUMBEROFCHANNELS	47			//15 + 32
#define	ITC1600_NUMBEROFINPUTS		32			//(8 AD + 1 Temp + 4 Aux + 3 Dig) * 2
#define	ITC1600_NUMBEROFOUTPUTS		15			//(4 + 3) * 2 + 1

#define	ITC1600_NUMBEROFADCINPUTS	16			//8+8
#define	ITC1600_NUMBEROFDACOUTPUTS	8			//4+4
#define	ITC1600_NUMBEROFDIGINPUTS	6			//F+S+S * 2
#define	ITC1600_NUMBEROFDIGOUTPUTS	6			//F+S+S * 2
#define	ITC1600_NUMBEROFAUXINPUTS	8			//4+4
#define	ITC1600_NUMBEROFAUXOUTPUTS	1			//On Host
#define	ITC1600_NUMBEROFTEMPINPUTS	2			//1+1
#define	ITC1600_NUMBEROFINPUTGROUPS		11		//
#define	ITC1600_NUMBEROFOUTPUTGROUPS	5		//(DAC, SD) + (DAC, SD) + FD + FD + HOST

#define	ITC00_NUMBEROFCHANNELS		48			//16 + 32
#define	ITC00_NUMBEROFINPUTS		32			//(8 AD + 1 Temp + 4 Aux + 3 Dig) * 2
#define	ITC00_NUMBEROFOUTPUTS		16			//(4 + 3) * 2 + 2

#define	ITC00_NUMBEROFADCINPUTS		16			//8+8
#define	ITC00_NUMBEROFDACOUTPUTS	8			//4+4
#define	ITC00_NUMBEROFDIGINPUTS		6			//F+S+S * 2
#define	ITC00_NUMBEROFDIGOUTPUTS	6			//F+S+S * 2
#define	ITC00_NUMBEROFAUXINPUTS		8			//4+4
#define	ITC00_NUMBEROFAUXOUTPUTS	2			//On Host
#define	ITC00_NUMBEROFTEMPINPUTS	2			//1+1
#define	ITC00_NUMBEROFINPUTGROUPS	11		//
#define	ITC00_NUMBEROFOUTPUTGROUPS	5		//(DAC, SD) + (DAC, SD) + FD + FD + HOST

//***************************************************************************
//ITC16 CHANNELS
#define	ITC16_DA0						0
#define	ITC16_DA1						1
#define	ITC16_DA2						2
#define	ITC16_DA3						3
#define	ITC16_DO						4

#define	ITC16_AD0						0
#define	ITC16_AD1						1
#define	ITC16_AD2						2
#define	ITC16_AD3						3
#define	ITC16_AD4						4
#define	ITC16_AD5						5
#define	ITC16_AD6						6
#define	ITC16_AD7						7
#define	ITC16_DI						8

//***************************************************************************
//ITC18 CHANNELS
#define	ITC18_DA0						0
#define	ITC18_DA1						1
#define	ITC18_DA2						2
#define	ITC18_DA3						3
#define	ITC18_DO0						4
#define	ITC18_DO1						5
#define	ITC18_AUX						6

#define	ITC18_AD0						0
#define	ITC18_AD1						1
#define	ITC18_AD2						2
#define	ITC18_AD3						3
#define	ITC18_AD4						4
#define	ITC18_AD5						5
#define	ITC18_AD6						6
#define	ITC18_AD7						7
#define	ITC18_DI						8

//***************************************************************************
//ITC1600 CHANNELS

//DACs
#define	ITC1600_DA0						0		//RACK0
#define	ITC1600_DA1						1
#define	ITC1600_DA2						2
#define	ITC1600_DA3						3
#define	ITC1600_DA4						4		//RACK1
#define	ITC1600_DA5						5
#define	ITC1600_DA6						6
#define	ITC1600_DA7						7

//Digital outputs
#define	ITC1600_DOF0					8		//RACK0
#define	ITC1600_DOS00					9
#define	ITC1600_DOS01					10
#define	ITC1600_DOF1					11		//RACK1
#define	ITC1600_DOS10					12
#define	ITC1600_DOS11					13
#define	ITC1600_HOST					14

//ADCs
#define	ITC1600_AD0						0		//RACK0
#define	ITC1600_AD1						1
#define	ITC1600_AD2						2
#define	ITC1600_AD3						3
#define	ITC1600_AD4						4
#define	ITC1600_AD5						5
#define	ITC1600_AD6						6
#define	ITC1600_AD7						7

#define	ITC1600_AD8						8		//RACK1
#define	ITC1600_AD9						9
#define	ITC1600_AD10					10
#define	ITC1600_AD11					11
#define	ITC1600_AD12					12
#define	ITC1600_AD13					13
#define	ITC1600_AD14					14
#define	ITC1600_AD15					15

//Slow ADCs
#define	ITC1600_SAD0					16		//RACK0
#define	ITC1600_SAD1					17
#define	ITC1600_SAD2					18
#define	ITC1600_SAD3					19
#define	ITC1600_SAD4					20		//RACK1
#define	ITC1600_SAD5					21
#define	ITC1600_SAD6					22
#define	ITC1600_SAD7					23

//Temperature
#define	ITC1600_TEM0					24		//RACK0
#define	ITC1600_TEM1					25		//RACK1

//Digital Inputs
#define	ITC1600_DIF0					26		//RACK0
#define	ITC1600_DIS00					27
#define	ITC1600_DIS01					28
#define	ITC1600_DIF1					29		//RACK1
#define	ITC1600_DIS10					30
#define	ITC1600_DIS11					31

//***************************************************************************
//ITC00 CHANNELS

//DACs
#define	ITC00_DA0						0		//RACK0
#define	ITC00_DA1						1
#define	ITC00_DA2						2
#define	ITC00_DA3						3
#define	ITC00_DA4						4		//RACK1
#define	ITC00_DA5						5
#define	ITC00_DA6						6
#define	ITC00_DA7						7

//Digital outputs
#define	ITC00_DOF0					8		//RACK0
#define	ITC00_DOS00					9
#define	ITC00_DOS01					10
#define	ITC00_DOF1					11		//RACK1
#define	ITC00_DOS10					12
#define	ITC00_DOS11					13
#define	ITC00_HOST0					14
#define	ITC00_HOST1					15

//ADCs
#define	ITC00_AD0						0		//RACK0
#define	ITC00_AD1						1
#define	ITC00_AD2						2
#define	ITC00_AD3						3
#define	ITC00_AD4						4
#define	ITC00_AD5						5
#define	ITC00_AD6						6
#define	ITC00_AD7						7

#define	ITC00_AD8						8		//RACK1
#define	ITC00_AD9						9
#define	ITC00_AD10					10
#define	ITC00_AD11					11
#define	ITC00_AD12					12
#define	ITC00_AD13					13
#define	ITC00_AD14					14
#define	ITC00_AD15					15

//Slow ADCs
#define	ITC00_SAD0					16		//RACK0
#define	ITC00_SAD1					17
#define	ITC00_SAD2					18
#define	ITC00_SAD3					19
#define	ITC00_SAD4					20		//RACK1
#define	ITC00_SAD5					21
#define	ITC00_SAD6					22
#define	ITC00_SAD7					23

//Temperature
#define	ITC00_TEM0					24		//RACK0
#define	ITC00_TEM1					25		//RACK1

//Digital Inputs
#define	ITC00_DIF0					26		//RACK0
#define	ITC00_DIS00					27
#define	ITC00_DIS01					28
#define	ITC00_DIF1					29		//RACK1
#define	ITC00_DIS10					30
#define	ITC00_DIS11					31

//***************************************************************************
//Sampling Rate
#define ITC16_MINIMUM_SAMPLING_INTERVAL	5000	//ns
#define ITC16_MINIMUM_SAMPLING_STEP		1000	//ns
#define ITC18_MINIMUM_SAMPLING_INTERVAL	5000	//ns
#define ITC18_MINIMUM_SAMPLING_STEP		1250	//ns
#define ITC1600_MINIMUM_SAMPLING_INTERVAL	5000	//ns
#define ITC1600_MINIMUM_SAMPLING_STEP		5000	//ns

//***************************************************************************

//ITC18 Available functions
#define	ITC18_STANDARD_FUNCTION 		0
#define	ITC18_PHASESHIFT_FUNCTION 		1
#define	ITC18_DYNAMICCLAMP_FUNCTION		2
#define	ITC18_SPECIAL_FUNCTION			3

//ITC1600 Available functions
#define	ITC1600_STANDARD_FUNCTION 		0
#define	ITC1600_TEST_FUNCTION	 		0x10
#define	ITC1600_S_OUTPUT_FUNCTION	 	0x11
#define	ITC1600_STANDARD_DSP			0
#define	ITC1600_TEST_DSP				4
#define	ITC1600_S_OUTPUT_DSP			6
#define	ITC1600_STANDARD_HOST 			0
#define	ITC1600_STANDARD_RACK	 		0

//ITC00 Available functions
#define	ITC00_STANDARD_FUNCTION 		0
#define	ITC00_TEST_FUNCTION	 			0x10
#define	ITC00_S_OUTPUT_FUNCTION			0x11
#define	ITC00_STANDARD_DSP				0
#define	ITC00_TEST_DSP					4
#define	ITC00_S_OUTPUT_DSP				6
#define	ITC00_STANDARD_HOST 			0
#define	ITC00_STANDARD_RACK		 		0

//ITC1600 Modes
#define	ITC1600_INTERNAL_CLOCK 			0x0
#define	ITC1600_INTRABOX_CLOCK 			0x1
#define	ITC1600_EXTERNAL_CLOCK 			0x2
#define	ITC1600_CLOCKMODE_MASK 			0x3
#define	ITC1600_PCI1600_RACK 			0x8

#define	ITC1600_RACK_RELOAD 			0x10

//ITC00 Modes
#define	ITC00_INTERNAL_CLOCK 			0x0
#define	ITC00_EXTERNAL_CLOCK 			0x1
#define	ITC00_INTRABOX_CLOCK 			0x2
#define	ITC00_CLOCKMODE_MASK 			0x3
#define	ITC00_PCI1600_RACK 				0x8

#define	ITC00_RACK_RELOAD	 			0x10

//ITC1600 Digital Input Modes Bit definition
#define	DI_HEKA_ACTIVE_LOW				0x8000
#define	DI_HEKA_LATCHING_MODE			0x4000
#define	DI_TRIGIN_ACTIVE_LOW			0x2000
#define	DI_TRIGIN_LATCHING_MODE			0x1000
#define	DI_FRONT_3_2_ACTIVE_LOW			0x0800
#define	DI_FRONT_3_2_LATCHING_MODE		0x0400
#define	DI_FRONT_1_0_ACTIVE_LOW			0x0200
#define	DI_FRONT_1_0_LATCHING_MODE		0x0100
#define	DI_15_12_ACTIVE_LOW				0x0080
#define	DI_15_12_LATCHING_MODE			0x0040
#define	DI_11_08_ACTIVE_LOW				0x0020
#define	DI_11_08_LATCHING_MODE			0x0010
#define	DI_07_04_ACTIVE_LOW				0x0008
#define	DI_07_04_LATCHING_MODE			0x0004
#define	DI_03_00_ACTIVE_LOW				0x0002
#define	DI_03_00_LATCHING_MODE			0x0001

//***************************************************************************
//Overflow/Underrun Codes
#define	ITC_READ_OVERFLOW_H			0x00010000
#define	ITC_WRITE_UNDERRUN_H		0x00020000
#define	ITC_READ_OVERFLOW_S			0x00100000
#define	ITC_WRITE_UNDERRUN_S		0x00200000

#define	ITC_STOP_CH_ON_OVERFLOW		0x00000001	//Stop One Channel
#define	ITC_STOP_CH_ON_UNDERRUN		0x00000002

#define	ITC_STOP_CH_ON_COUNT		0x00000010
#define	ITC_STOP_PR_ON_COUNT		0x00000020

#define	ITC_STOP_DR_ON_OVERFLOW		0x00000100	//Stop One Direction
#define	ITC_STOP_DR_ON_UNDERRUN		0x00000200

#define	ITC_STOP_ALL_ON_OVERFLOW	0x00001000	//Stop System (Hardware STOP)
#define	ITC_STOP_ALL_ON_UNDERRUN	0x00002000

//***************************************************************************
//Predefined Vendor IDs (Software Keys MSW)
#define PaulKey						0x5053
#define HekaKey						0x4845
#define UicKey						0x5543
#define InstruKey					0x4954
#define AlexKey						0x4142
#define UWKey						0x5557

//Predefined Program IDs (Software Keys LSW)
#define EcellKey					0x4142
#define SampleKey					0x5470
#define TestKey						0x4444
#define TestSuiteKey				0x5453
#define DemoKey						0x4445
#define IgorKey						0x4947
#define CalibrationKey				0x4341
#define AcqUIKey					0x4151
//***************************************************************************

//Definitions for some bits "LoadedFunction" of GlobalDeviceInfo;
#define ITC_EMPTY					0
#define ITC_RESERVE					0x80000000
#define ITC_INIT_FLAG				0x00008000
#define ITC_RACK_FLAG				0x00004000
#define ITC_FUNCTION_MASK			0x00000FFF

//Definitions for some bits "RunningMode" of GlobalDeviceInfo;
#define	RUN_STATE					0x10
#define	ERROR_STATE					0x80000000
#define	DEAD_STATE					0x00
#define	EMPTY_INPUT					0x01
#define	EMPTY_OUTPUT				0x02
//***************************************************************************

//Specification for Hardware Configuration
typedef struct
{
unsigned long Mode;						//Mode: 0 - Internal Clock; 1 - Intrabox Clock; 2 - External Clock
										//Mode -1; - Do not reload
										//For ITC18 - LCA_MODE
void* U2F_File;							//U2F File name -> may be NULL
unsigned long SizeOfSpecialFunction;	//Sizeof SpecialFunction
void* SpecialFunction;					//different for each device

unsigned long Reserved;
unsigned long id;						//Rack ID for ITC1600
}
HWFunction;

//Special Functions for ITC1600
typedef struct
{
unsigned long Function;					//HWFunction
unsigned long DSPType;					//LCA for Interface side
unsigned long HOSTType;					//LCA for Interface side
unsigned long RACKType;					//LCA for Interface side
}
ITC1600_Special_HWFunction;

//Special Functions for ITC00
typedef struct
{
unsigned long Function;					//HWFunction
unsigned long DSPType;					//LCA for Interface side
unsigned long HOSTType;					//LCA for Interface side
unsigned long RACKType;					//LCA for Interface side
}
ITC00_Special_HWFunction;

//Special Functions for ITC18
typedef struct
{
unsigned long Function;					//HWFunction
void* InterfaceData;					//LCA for Interface side
void* IsolatedData;						//LCA for Isolated side
unsigned long Reserved;
}
ITC18_Special_HWFunction;

//***************************************************************************

//SamplingIntervalFlag / StartOffset:
#define SAMPLING_MASK	0x03
#define USE_FREQUENCY	0x00
#define USE_TIME		0x01
#define USE_TICKS		0x02

#define SCALE_MASK		0x0C
#define NO_SCALE		0x00
#define MS_SCALE		0x04	//Milliseconds
#define US_SCALE		0x08	//Microseconds
#define NS_SCALE		0x0C	//Nanoseconds

#define ADJUST_RATE		0x10	//Adjust to closest available
#define DONTIGNORE_SCAN	0x20	//Use StartOffset to set Scan Number

//Conversion values
#define ANALOGVOLT				3200.
#define SLOWANALOGVOLT			3276.7
#define OFFSETINVOLTS			819200.		//3200*256.
#define SLOWOFFSETINVOLTS		838835.2	//838860.8	//3276.8*256 838835.2
#define POSITIVEVOLT			10.2396875
#define SLOWPOSITIVEVOLT		10.

//Gain definition for ITC-18 ADCs
#define ITC18_GAIN_1	1
#define ITC18_GAIN_2	2
#define ITC18_GAIN_5	5
#define ITC18_GAIN_10	10

//***************************************************************************

//Channel Info structure
typedef struct
{
unsigned long ModeNumberOfPoints;
unsigned long ChannelType;
unsigned long ChannelNumber;
unsigned long Reserved0;					

unsigned long ErrorMode;					//See ITC_STOP_XX..XX definition for Error Modes
unsigned long ErrorState;
void* FIFOPointer;
unsigned long FIFONumberOfPoints;			//In Points

unsigned long ModeOfOperation;
unsigned long SizeOfModeParameters;
void* ModeParameters;
unsigned long SamplingIntervalFlag;			//See flags above

double SamplingRate;						//See flags above
double StartOffset;							//See flags above
double Gain;								//Times
double Offset;								//Volts

unsigned long ExternalDecimation;
unsigned long HardwareUnderrunValue;
unsigned long Reserved2;
unsigned long Reserved3;
}
ITCChannelInfo;

//Defines for ModeOfOperation
#define	ITC_CH_STANDARD_MODE	0
#define	ITC_S_OUTPUT_MODE		1
#define	ITC_S_OUTPUT_TABLE		2		
#define	ITC_S_OUTPUT_TABLE_MAX_SIZE		1024	//Max size for 2 slots

//Special Output DSP Code Parameters:
typedef struct
{
long  Mode;						//Mode for S_OUTPUT
long  Coeff0;					//Phase In for S_OUTPUT
long  Coeff1;					//Freq/Sampling * 360
long  Coeff2;					//Amplit * ANALOGVOLT
}
ITCSpecialOutput;

//Special Output DSP Code Parameters:
typedef struct
{
unsigned long  StartAddress;			//Note: Second slot should have offset
unsigned long  TableSize;				
long	*Data;
}
ITCSpecialOutputTable;					

//Modes for S_OUTPUT
#define	S_OUT_MAX_MODES		6
#define	S_OUT_EMPTY			0
#define	S_OUT_CONSTANT		1
#define	S_OUT_TABLE			2			//Do not use
#define	S_OUT_RAMP			3
#define	S_OUT_SQUARE		4
#define	S_OUT_SAWTOOTH		5
//***************************************************************************

//Defines for CommandStatus (ITC_GetState function)
#define	READ_TOTALTIME				0x01
#define	READ_RUNTIME				0x02
#define	READ_ERRORS					0x04
#define	READ_RUNNINGMODE			0x08
#define	READ_OVERFLOW				0x10
#define	READ_CLIPPING				0x20

#define	READ_ASYN_ADC				0x40

//Defines for return DSP State
#define	RACKLCAISALIVE				0x80000000
#define	PLLERRORINDICATOR			0x08000000
#define	RACK0MODEMASK				0x70000000
#define	RACK1MODEMASK				0x07000000
#define	RACK0IDERROR				0x00020000
#define	RACK1IDERROR				0x00010000
#define	RACK0CRCERRORMASK			0x0000FF00
#define	RACK1CRCERRORMASK			0x000000FF

//Commands for special ITC1600 Mode. Switch output to internal data and back
#define	SWITCH_OUTPUT_LINE0			1
#define	SWITCH_OUTPUT_LINE1			2
#define	SWITCH_OUTPUT_LINE0_LINE1	3
#define	SWITCH_OUTPUT_MASK			3

//Status structure for use in ITC_GetState & ITC_SetState functions
typedef struct 
{
unsigned long CommandStatus;
unsigned long RunningMode;
unsigned long Overflow;
unsigned long Clipping;

unsigned long State;
unsigned long Reserved0;
unsigned long Reserved1;
unsigned long Reserved2;

double TotalSeconds;
double RunSeconds;
}
ITCStatus;

//***************************************************************************

//Trigger definition modes
#define USE_TRIG_IN			0x01
//Additional trigger sources. To combine with "USE_TRIG_IN". For ITC1600 only
#define USE_TRIG_IN_HOST	0x02	//on PCI1600 connector
#define USE_TRIG_IN_TIMER	0x04	//Start by timer
#define USE_TRIG_IN_RACK	0x08	//Start by "Trig In" on the front panel
#define USE_TRIG_IN_FDI0	0x10	//Start by Digital In 0 on the front panel
#define USE_TRIG_IN_FDI1	0x20	//Start by Digital In 1 on the front panel
#define USE_TRIG_IN_FDI2	0x40	//Start by Digital In 2 on the front panel
#define USE_TRIG_IN_FDI3	0x80	//Start by Digital In 3 on the front panel
#define TRIG_IN_MASK		0xFF

//For ITC1600 only. Do not combine with other bits
#define USE_HARD_TRIG_IN	0x100	//on PCI1600 connector

//***************************************************************************

//Specification for Acquisition Configuration
typedef struct 
{
unsigned long DigitalInputMode;		//Bit 0: Latch Enable, Bit 1: Invert. For ITC1600; See AES doc.
unsigned long ExternalTriggerMode;	//Bit 0: Transition, Bit 1: Invert
unsigned long ExternalTrigger;		//Enable
unsigned long EnableExternalClock;	//Enable

unsigned long DACShiftValue;		//For ITC18 Only. Needs special LCA
unsigned long InputRange;			//AD0.. AD7
unsigned long TriggerOutPosition;
unsigned long OutputEnable;			

unsigned long SequenceLength;		//In/Out for ITC16/18; Out for ITC1600
unsigned long *Sequence;			//In/Out for ITC16/18; Out for ITC1600
unsigned long SequenceLengthIn;		//For ITC1600 only
unsigned long *SequenceIn;			//For ITC1600 only

unsigned long ResetFIFOFlag;		//Reset FIFO Pointers / Total Number of points in NORMALMODE
unsigned long ControlLight;
double SamplingInterval;			//In Seconds. Note: may be calculated from channel setting
}
ITCPublicConfig;

//***************************************************************************

/*Not implemented
typedef struct
{
unsigned long ChannelNumber;
double IntegrationPeriod; //in seconds
unsigned long DecimateMode;
void* Result;
}
ITCLimitedAcquire;
*/

//***************************************************************************

//Channel manipulation commands

#define RESET_FIFO_COMMAND			0x00010000
#define PRELOAD_FIFO_COMMAND		0x00020000
#define LAST_FIFO_COMMAND			0x00040000
#define FLUSH_FIFO_COMMAND			0x00080000
#define ITC_SET_SHORT_ACQUISITION	0x00100000
#define READ_OUTPUT_ONLY			0x00200000
#define DISABLE_CALIBRATION			0x00400000

#define RESET_FIFO_COMMAND_EX			0x0001
#define PRELOAD_FIFO_COMMAND_EX			0x0002
#define LAST_FIFO_COMMAND_EX			0x0004
#define FLUSH_FIFO_COMMAND_EX			0x0008
#define ITC_SET_SHORT_ACQUISITION_EX	0x0010
#define READ_OUTPUT_ONLY_EX				0x0020
#define DISABLE_CALIBRATION_EX			0x0040
#define SKIP_INSERT_POINTS_EX			0x0080

//GetFIFOInformation flags
#define READ_FIFO_INFO						0
#define READ_FIFO_READ_POINTER_COUNTER		1
#define READ_FIFO_WRITE_POINTER_COUNTER		2

typedef struct
{
unsigned short ChannelType;		//Channel Type
unsigned short Command;			//Command
unsigned short ChannelNumber;	//Channel Number
unsigned short Status;			//Status
unsigned long  Value;			//Number of points OR Data Value
void* DataPointer;				//Data
}
ITCChannelDataEx;

//***************************************************************************

//Version for driver parts
typedef struct
	{
	long Major;
	long Minor;
	char description[80];
	char date[80];
	}
VersionInfo;

//***************************************************************************

//Device Information structure
typedef struct
	{
	unsigned long DeviceType;
	unsigned long DeviceNumber;		
	unsigned long PrimaryFIFOSize;		//In Points
	unsigned long SecondaryFIFOSize;	//In Points

	unsigned long LoadedFunction;
	unsigned long SoftKey;
	unsigned long Mode;
	unsigned long MasterSerialNumber;

	unsigned long SecondarySerialNumber;
	unsigned long HostSerialNumber;
	unsigned long NumberOfDACs;
	unsigned long NumberOfADCs;

	unsigned long NumberOfDOs;
	unsigned long NumberOfDIs;
	unsigned long NumberOfAUXOs;
	unsigned long NumberOfAUXIs;

	unsigned long MinimumSamplingInterval;		//in ns
	unsigned long MinimumSamplingStep;			//in ns
	unsigned long FirmwareVersion0;
	unsigned long Reserved1;
	}
GlobalDeviceInfo;

//Additional Device Information. Use to get "U2F" file used to load the device.
//Also has current device functionality
typedef struct
	{
	char	*u2ffile;		//may be up to "MAX_PATH + 16". Set to NULL, if not needed
	HANDLE	DriverHandle;
	long	Reserved1;
	long	Reserved2;

	long	DSP_Type;
	long	HOSTLCA_Type;
	long	RACKLCA_Type;
	long	LoadMode;
	}
ExtendedGlobalDeviceInfo;

//***************************************************************************

//RunningOption. Use to improve specific application performance.
#define DontUseTimerThread		1
#define FastPointerUpdate		2
#define ShortDataAcquisition	4
#define TimerResolutionMask	0x00FF0000
#define TimerIntervalMask	0xFF000000
#define TimerResolutionShift	16
#define TimerIntervalShift		24

//How to Start.
typedef struct
	{
	unsigned long ExternalTrigger;		//-1 - do not change
	unsigned long OutputEnable;			//-1 - do not change
	unsigned long StopOnOverflow;		//-1 - do not change
	unsigned long StopOnUnderrun;		//-1 - do not change

	unsigned long RunningOption;		//-1 - do not change
	unsigned long ResetFIFOs;

	unsigned long NumberOf640usToRun;
	unsigned long Reserved3;

	double StartTime;		
	double StopTime;		
	}
ITCStartInfo;

//***************************************************************************

//SINGLE SCAN Limitations
#define ITC16_MaximumSingleScan			16*1024
#define ITC18_MaximumSingleScan			256*1024
#define ITC1600_MaximumSingleScan		1024
#define ITC00_MaximumSingleScan			1024

#define	ITC16_MAX_SEQUENCE_LENGTH		16
#define	ITC18_MAX_SEQUENCE_LENGTH		16
#define	ITC1600_MAX_SEQUENCE_LENGTH		16
#define	ITC00_MAX_SEQUENCE_LENGTH		16

#define	ITC18_NOP_CHANNEL				0x80000000
#define	ITC1600_NOP_CHANNEL_RACK0		0x80000000
#define	ITC1600_NOP_CHANNEL_RACK1		0x80000001
#define	ITC00_NOP_CHANNEL_RACK0			0x80000000
#define	ITC00_NOP_CHANNEL_RACK1			0x80000001

typedef struct
	{
	unsigned long ChannelType;
	unsigned long ChannelNumber;
	unsigned long Reserved0;
	unsigned long Reserved1;

	unsigned long Reserved2;
	unsigned long NumberOfPoints;
	unsigned long DecimateMode;
	void* Data;
	}
ITCLimited;

//***************************************************************************

//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
//M					GLOBAL MANAGEMENT FUNCTIONs
//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
typedef struct
	{
	long SoftwareFIFOSize;
	long HardwareFIFOSize_A;
	long HardwareFIFOSize_B;
	long TransferSizeLimitation;	//Limit transfer to/from FIFO size for ITC-16/18. 0 - no limit

	long Reserved0;
	long Reserved1;
	long Reserved2;
	long Reserved3;
	}
ITCGlobalConfig;

ITC_Export unsigned long ITC_GlobalConfig(ITCGlobalConfig* GlobalConfig) ITC_Import;

//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
//M					
//M					POWER MANAGEMENT FUNCTIONs
//M					
//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
ITC_Export unsigned long ITC_Devices(unsigned long DeviceType, unsigned long* DeviceNumber) ITC_Import;

ITC_Export unsigned long ITC_GetDeviceHandle(	unsigned long DeviceType, 
										unsigned long DeviceNumber,
										HANDLE* DeviceHandle) ITC_Import;

ITC_Export unsigned long ITC_GetDeviceType(	HANDLE DeviceHandle,
										unsigned long* DeviceType, 
										unsigned long* DeviceNumber) ITC_Import;

ITC_Export unsigned long ITC_OpenDevice(unsigned long DeviceType, 
								 unsigned long DeviceNumber, 
								 unsigned long Mode,
								 HANDLE* DeviceHandle) ITC_Import;

ITC_Export unsigned long ITC_CloseDevice(HANDLE DeviceHandle) ITC_Import;

ITC_Export unsigned long ITC_InitDevice(	HANDLE DeviceHandle, 
									HWFunction* sHWFunction) ITC_Import;

ITC_Export unsigned long ITC_LogOpen(HANDLE DeviceHandle, const char* file_path) ITC_Import;
ITC_Export unsigned long ITC_LogEnable(HANDLE DeviceHandle, long mask) ITC_Import;
ITC_Export unsigned long ITC_LogDisable(HANDLE DeviceHandle) ITC_Import;
ITC_Export unsigned long ITC_LogClose(HANDLE DeviceHandle) ITC_Import;

//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
//M					
//M					STATIC INFORMATION FUNCTIONs
//M					
//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM

ITC_Export unsigned long ITC_GetDeviceInfo(HANDLE DeviceHandle, GlobalDeviceInfo* sDeviceInfo) ITC_Import;
ITC_Export unsigned long ITC_GetDeviceInfoEx(HANDLE DeviceHandle, ExtendedGlobalDeviceInfo* sDeviceInfoEx) ITC_Import;

ITC_Export unsigned long ITC_GetVersions(	HANDLE DeviceHandle,
									VersionInfo* ThisDriverVersion,
									VersionInfo* KernelLevelDriverVersion,
									VersionInfo* HardwareVersion) ITC_Import;

ITC_Export unsigned long ITC_GetSerialNumbers(	HANDLE DeviceHandle,
										unsigned long* HostSerialNumber,
										unsigned long* MasterBoxSerialNumber,
										unsigned long* SlaveBoxSerialNumber) ITC_Import;

ITC_Export unsigned long ITC_GetStatusText(HANDLE DeviceHandle,
									long Status,
									char* Text,
									unsigned long MaxCharacters) ITC_Import;

ITC_Export unsigned long ITC_AnalyzeError(	long Status,
									char* Text,
									unsigned long MaxCharacters) ITC_Import;

ITC_Export unsigned long ITC_SetSoftKey(HANDLE DeviceHandle, unsigned long SoftKey) ITC_Import;

//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
//M					
//M					DYNAMIC INFORMATION FUNCTIONs
//M					
//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM

ITC_Export unsigned long ITC_GetState(HANDLE DeviceHandle, ITCStatus* sParam) ITC_Import;
ITC_Export unsigned long ITC_SetState(HANDLE DeviceHandle, ITCStatus* sParam) ITC_Import;
ITC_Export unsigned long ITC_GetFIFOInformation(	HANDLE DeviceHandle,
											unsigned long NumberOfChannels,
											ITCChannelDataEx* sParam) ITC_Import;
ITC_Export unsigned long ITC_GetTime(	HANDLE DeviceHandle,
								double* Seconds) ITC_Import;

//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM
//M					
//M					CONFIGURATION FUNCTIONs
//M					
//MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM

ITC_Export unsigned long ITC_ResetChannels(HANDLE DeviceHandle) ITC_Import;

ITC_Export unsigned long ITC_SetChannels(	HANDLE DeviceHandle, 
									unsigned long NumberOfChannels,
									ITCChannelInfo* Channels) ITC_Import;

ITC_Export unsigned long ITC_UpdateChannels(	HANDLE DeviceHandle) ITC_Import;

ITC_Export unsigned long ITC_GetChannels(	HANDLE DeviceHandle, 
									unsigned long NumberOfChannels,
									ITCChannelInfo* Channels) ITC_Import;

ITC_Export unsigned long ITC_ConfigDevice(	HANDLE DeviceHandle, 
									ITCPublicConfig* sITCConfig) ITC_Import;


ITC_Export unsigned long ITC_Start(HANDLE DeviceHandle, ITCStartInfo* sParam) ITC_Import;
ITC_Export unsigned long ITC_Stop( HANDLE DeviceHandle, void* sParam) ITC_Import;

ITC_Export unsigned long ITC_UpdateNow(HANDLE DeviceHandle, void* sParam) ITC_Import;

ITC_Export unsigned long ITC_SingleScan(	HANDLE DeviceHandle, 
									unsigned long NumberOfChannels,
									ITCLimited* sParam) ITC_Import;

ITC_Export unsigned long ITC_AsyncIO(	HANDLE DeviceHandle,
								unsigned long NumberOfChannels,
								ITCChannelDataEx* sParam) ITC_Import;

//***************************************************************************

ITC_Export unsigned long ITC_GetDataAvailable(	HANDLE DeviceHandle, 
										unsigned long NumberOfChannels, 
										ITCChannelDataEx* sParam) ITC_Import;
ITC_Export unsigned long ITC_UpdateFIFOPosition(	HANDLE DeviceHandle, 
											unsigned long NumberOfChannels, 
											ITCChannelDataEx* sParam) ITC_Import;

ITC_Export unsigned long ITC_ReadWriteFIFO(HANDLE DeviceHandle, 
									unsigned long NumberOfChannels, 
									ITCChannelDataEx* sParam) ITC_Import;

ITC_Export unsigned long ITC_GetFIFOPointers(	HANDLE DeviceHandle,
										unsigned long NumberOfChannels,
										ITCChannelDataEx* sParam) ITC_Import;
//***************************************************************************

#ifdef __cplusplus
}
#endif


#endif
