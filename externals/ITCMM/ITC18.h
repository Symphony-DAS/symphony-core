// Interface procedures for the Instrutech ITC18.
//
// Copyright (c) 1991, 1995 Instrutech Corporation, Port Washington, NY, USA
//
// Created by SKALAR Instruments, Seattle, WA, USA
//
// Modified by Telly Galiatsatos, Instrutech Corporation 6/28/2000
//
// Modified by Michael Mazlin, instrutech Corporation 7/26/2001 


#ifndef ITC18_H
#define ITC18_H

#ifdef __cplusplus
extern "C"
{
#endif

#ifdef _WINDOWS
   #include <windows.h>
#else
   #include "Compatibility.h"
#endif

#include "ExportDefs.h"

// Application interface functions
//
// Functions that return a status value return zero if the function
// succeeds, and non-zero if the function fails. To interpret a
// status value, use ITC18_GetStatusText.


// Function ITC18_GetStatusText
//
// Translate a status value to a text string. The translated string
// is written into "text" with maximum length "length" characters.

ITC_Export int ITC18_GetStatusText(void* device, int status, char* text, int length) ITC_Import;


// Function ITC18_GetStructureSize
//
// Return the size of the structure that defines the device.

ITC_Export int ITC18_GetStructureSize() ITC_Import;

//Mike: Add Version

ITC_Export int ITC18_GetVersion(void* device, void* UserVersion, void* KernelVersion) ITC_Import;

// ITC18_Open
//
// Open the device driver.

ITC_Export int ITC18_Open(void* device, int device_number) ITC_Import;


// ITC18_Close
//
// Close the device and free resources associated with it.

ITC_Export int ITC18_Close(void* device) ITC_Import;

// ITC18_Initialize
//
// Initialize the device hardware.
// TG 6/28/2000
// Added new parameter to specify LCA programming
//	0 - Standard
//	1 - DynamicClamp
//	2 - PhaseShifter

#define ITC18_STANDARD 0
#define ITC18_DYNAMICCLAMP 1
#define ITC18_PHASESHIFTER 2

ITC_Export int ITC18_Initialize(void* device, int LCA_configuration) ITC_Import;

// ITC18_InitializeCustom
//
// Initialize the device hardware.
// 'control_data' and 'isolated_data' are pointers to buffers containing
// LCA data. If a pointer is NULL, the corresponding default LCA is used.

ITC_Export int ITC18_InitializeCustom(
	void* device,
	void* control_data,
	void* isolated_data
) ITC_Import;

// Function ITC18_GetFIFOSize
//
// Return the size of the FIFO in samples.

ITC_Export int ITC18_GetFIFOSize(void* device) ITC_Import;

// The EEPROM total and sector sizes in bytes.

#define	ITC18_ROM_SIZE (32 * 1024)
//Version 4.0: new sector size 128 -> 4096
#define ITC18_OLD_ROM_SECTOR_SIZE 128
#define ITC18_ROM_SECTOR_SIZE 4096

// Function ITC18_ReadROM
//
// Read a ROM data - upto sector size:ITC18_ROM_SECTOR_SIZE
// 'address' is the starting address of the transfer, and must be a
// multiple of ITC18_SECTOR_SIZE in the range
// 0..ITC18_ROM_SIZE-ITC18_SECTOR_SIZE. 'data' is a pointer to a
// buffer of length ITC18_ROM_SECTOR_SIZE. The data in the ROM is
// written into the buffer.

ITC_Export int ITC18_ReadROM(void* device, int address, int size, void* data) ITC_Import;

// Function ITC18_WriteROM
//
// Write a ROM sector.
// 'address' is the starting address of the transfer, and must be a
// multiple of ITC18_SECTOR_SIZE in the range
// 0..ITC18_ROM_SIZE-ITC18_SECTOR_SIZE. 'data' is a pointer to a
// buffer of length ITC18_ROM_SECTOR_SIZE. The data in the buffer
// is written to the ROM.

ITC_Export int ITC18_WriteROM(void* device, int address, void* data) ITC_Import;

// ITC18_SetSamplingInterval
//
// The sampling rate is represented in timer ticks of 1.25 usecs.
// Non zero 'external_clock' causes the ITC-18 to synchronous with
// an external clock.

#define ITC18_MINIMUM_TICKS 4
#define ITC18_MAXIMUM_TICKS 65535

ITC_Export int ITC18_SetSamplingInterval(void* device, int timer_ticks, int external_clock) ITC_Import;

// Function ITC18_WriteAuxiliaryDigitalOutput
//
// Write the auxiliary digital outputs asynchronously.

ITC_Export int ITC18_WriteAuxiliaryDigitalOutput(void* device, int data) ITC_Import;

// Function ITC18_SetDigitalInputMode
//
// Set the latch and inversion mode for the digital inputs.
// The default settings are non-latched and non-inverted.

ITC_Export int ITC18_SetDigitalInputMode(void* device, int latch, int invert) ITC_Import;

// Function ITC18_SetReadyLight
//
// Turn ON / OFF Ready Light
ITC_Export int ITC18_SetReadyLight(void* device, int on) ITC_Import;

// ITC18_SetSequence
//
// An array of(length) 16 bit instructions is written out to the sequence RAM.
// Acquisition must be stopped to write to the sequence RAM.

// ITC18 Sequence memory parameters

#define ITC18_SEQUENCE_RAM_SIZE (4*1024)

#define ITC18_OUTPUT_UPDATE 0x8000
#define ITC18_INPUT_UPDATE 0x4000

//Mike 11.9.2000: Add Trigger Out Bit
#define ITC18_OUTPUT_TRIGGER 0x0001

#define ITC18_OUTPUT_DA0 0x0000
#define ITC18_OUTPUT_DA1 0x0800
#define ITC18_OUTPUT_DA2 0x1000
#define ITC18_OUTPUT_DA3 0x1800

#define ITC18_OUTPUT_DIGITAL0 0x2000
#define ITC18_OUTPUT_DIGITAL1 0x2800
#define ITC18_OUTPUT_SKIP	0x3800

#define ITC18_INPUT_AD0 0x0000
#define ITC18_INPUT_AD1 0x0080
#define ITC18_INPUT_AD2 0x0100
#define ITC18_INPUT_AD3 0x0180
#define ITC18_INPUT_AD4 0x0200
#define ITC18_INPUT_AD5 0x0280
#define ITC18_INPUT_AD6 0x0300
#define ITC18_INPUT_AD7 0x0380

#define ITC18_INPUT_DIGITAL 0x0400
#define ITC18_INPUT_SKIP 0x0780

ITC_Export int ITC18_SetSequence(void* device, int length, int* instructions) ITC_Import;


// ITC18_SetRange
//
// The range vector must be ITC18_AD_CHANNELS entries long.  Each entry
// contains an ITC18_AD_RANGE_ value.  SetRange may be called whether
// acquisition is in progress or not.

#define ITC18_AD_CHANNELS 8

#define ITC18_AD_RANGE_10V 0
#define ITC18_AD_RANGE_5V 1
#define ITC18_AD_RANGE_2V 2
#define ITC18_AD_RANGE_1V 3

ITC_Export int ITC18_SetRange(void* device, int* range) ITC_Import;

// Function ITC18_SetExternalTriggerMode
//
// Non zero 'transition' triggers on a transition only,
// 'invert' triggers low.
// By default the ITC-18 will trigger high without sensing
// transitions.

ITC_Export int ITC18_SetExternalTriggerMode(void* device, int transition, int invert) ITC_Import;

// ITC18_InitializeAcquisition
//
// This routine must be called before acquisition can be performed after power
// up, and after each call to ITC18_Stop.

ITC_Export int ITC18_InitializeAcquisition(void* device) ITC_Import;


// ITC18_Start
//
// Initiate acquisition.
//
// 'external_trigger' non-zero causes data acquisition to begin on an external
// trigger signal. 'output_enable' must be non-zero to activate the outputs,
// including the D/A converters.

ITC_Export int ITC18_Start(void* device, 
						 int external_trigger, 
						 int output_enable,
						 int stoponoverflow,
						 int reserved) ITC_Import;

// ITC18_GetFIFOWriteAvailable
//
// Return the number of FIFO entries available for writing.

ITC_Export int ITC18_GetFIFOWriteAvailable(void* device, int* available) ITC_Import;


// ITC18_WriteFIFO
//
// The buffer of length entries is written to the ITC18.
//
// Any value from 1 to the value returned by ITC18_GetFIFOWriteAvailable may
// be used as the length argument.

ITC_Export int ITC18_WriteFIFO(void* device, int length, short* buffer) ITC_Import;


// Function ITC16_GetFIFOReadAvailableAndOverflow
//
// 07.19.04 -> combine 2 function in one

ITC_Export int ITC18_GetFIFOReadAvailableOverflow(void* device, int* available, int* overflow) ITC_Import;

// ITC18_GetFIFOReadAvailable
//
// Return the number of acquired samples not yet read out of the FIFO.

ITC_Export int ITC18_GetFIFOReadAvailable(void* device, int* available) ITC_Import;


// ITC18_ReadFIFO
//
// The buffer is filled with length entries from the ITC18.
//
// Any value from 1 to the value returned by ITC18_GetFIFOReadAvailable may
// be used as the length argument.

ITC_Export int ITC18_ReadFIFO(void* device, int length, short* buffer) ITC_Import;


// ITC18_IsClipping
//
// Set the "clipping" parameter to the state of the clipping line. If
// the clipping line is inactive, "clipping" is set to zero. If the
// clipping line is active, "clipping" is set to a non-zero value.
// The clipping latch is reset.

ITC_Export int ITC18_IsClipping(void* device, int* clipping) ITC_Import;


// ITC18_GetFIFOOverflow
//
// The 'overflow' parameter is set to zero if input FIFO overflow has
// not occurred, and non-zero if input FIFO overflow has occurred.

ITC_Export int ITC18_GetFIFOOverflow(void* device, int* overflow) ITC_Import;


// ITC18_Stop
//
// End acquisition immediately.

ITC_Export int ITC18_Stop(void* device) ITC_Import;


// ITC18_GetStatusOverflow
//
// Return the status value that indicates FIFO overflow.

ITC_Export int ITC18_GetStatusOverflow(void* device) ITC_Import;

// Function ITC18_Release
//
// Release the driver.
// Only of use under Microsoft Windows.

ITC_Export int ITC18_Release(void* device) ITC_Import;

// Function ITC18_Reserve
//
// Reserve the driver.
// Only of use under Microsoft Windows.

ITC_Export int ITC18_Reserve(void* device, int* busy) ITC_Import;

// Function ITC18_SetBank
//
// Set the bank for the special DAC shift LCA
// Only to be used with the EPC-10


// added for DAC shift LCA TG 8/24/98

ITC_Export int ITC18_SetBank(void* device, unsigned bank) ITC_Import;

// Function ITC18_SetDACShift
//
// Set the DAC shift value, 00 = 0
//							01 = 1.25 microseconds
//							10 = 2.50 microseconds
//							11 = 3.75 microseconds

ITC_Export int ITC18_SetDACShift(void* device, unsigned shift) ITC_Import;

//Mike: New Function -> Read PCI Space

//Mike: Add allocation function to kernel level
ITC_Export int ITC18_AllocateMemory(void* device, int SizeInBytes, void** LinAddress) ITC_Import;
ITC_Export int ITC18_FreeMemory(void* device) ITC_Import;

ITC_Export int ITC18_ReadPCIConfig(	void* device,
									void* pciconfig) ITC_Import;
ITC_Export int ITC18_WritePCIConfig(	void* device,
									void* pciconfig) ITC_Import;

#define	PCI_ROM_SIZE (32 * 1024)
//Version 4.0: new sector size 128 -> 4096
#define PCI_OLD_ROM_SECTOR_SIZE 128
#define PCI_ROM_SECTOR_SIZE 4096
ITC_Export int ITC18_ReadPCIROMSector(void* device, int Address, unsigned char* PCIData) ITC_Import;
ITC_Export int ITC18_WritePCIROMSector(void* device, int Address, unsigned char* PCIData) ITC_Import;
ITC_Export int ITC18_GetPCISerialNumber(void* device, int* SerialNumber) ITC_Import;

ITC_Export int ITC18_GetDeviceInfo(void* device, int* DeviceType, int* DeviceNumber) ITC_Import;
ITC_Export int ITC18_GetUSBInfo(void* device, long* REVCTL, long* Speed, long* Version, long* SerialNumber) ITC_Import;

ITC_Export int ITC18_SetUSBDigitalOutput(void* device, int DigitalOutput) ITC_Import;
ITC_Export int ITC18_GetUSBDigitalInput(void* device, int* DigitalInput) ITC_Import;
ITC_Export int ITC18_SetUSBUserBit(void* device, int Value) ITC_Import;
#ifdef _WINDOWS
ITC_Export int ITC18_GetTimer(void* device, LONGLONG* Timer);
ITC_Export int ITC18_GetAcqTimer(void* device, LONGLONG* StartTimer, LONGLONG* StopTimer) ITC_Import;
ITC_Export int ITC18_StartByTimer(void* device, 
								int external_trigger,
								int output_enable, 
								int stoponoverflow, 
								int reserved,
								LONGLONG Timer) ITC_Import;
#else
ITC_Export int ITC18_GetTimer(void* device, long long* Timer) ITC_Import;
ITC_Export int ITC18_GetAcqTimer(void* device, long long* StartTimer, long long* StopTimer) ITC_Import;
ITC_Export int ITC18_StartByTimer(void* device, 
								int external_trigger,
								int output_enable, 
								int stoponoverflow, 
								int reserved,
								long long Timer) ITC_Import;
#endif
ITC_Export int ITC18_ControlTimers(	void* device, 
									long Control1, 
									long* Counter1, 
									long Control2, 
									long* Counter2) ITC_Import;
//USB Stuff
#define USB18_CL	0x10000

ITC_Export int ITC18_LoadUSB(	void* device, 
							int datasize,
							char* data) ITC_Import;

#ifdef __APPLE__
ITC_Export unsigned long ITC18_ReadRegistry(void* device, unsigned long RegID, unsigned long* Value) ITC_Import;
ITC_Export unsigned long ITC18_WriteRegistry(void* device, unsigned long RegID, unsigned long Value) ITC_Import;
#endif

ITC_Export unsigned long ITC18_GetDriverHandle(void* device, void** hDevice) ITC_Import;

ITC_Export int ITC18_StopAndInitialize(void* device, int stop, int initialize) ITC_Import;
ITC_Export int ITC18_SetupAcquisition(void* device,
									int stop,
									int initialize, 
									int length, int* instructions,
									int timer_ticks, int external_clock,
									int start,
									int external_trigger, 
									int output_enable,
									int stoponoverflow,
									int reserved) ITC_Import;

ITC_Export int ITC18_SmallRun(void* device,
							int length, int* instructions,
							int timer_ticks, int external_clock,
							int OutputDataSize, short* OutputData,
							int InputDataSize, short* InputData,
							int external_trigger, int output_enable) ITC_Import;

//For Testing:
ITC_Export int ITC18_SingleWriteROM34(void* device, int Port, int Value) ITC_Import;
ITC_Export int ITC18_SingleReadROM4(void* device, short* Value) ITC_Import;

#ifdef __cplusplus
}
#endif

#endif
