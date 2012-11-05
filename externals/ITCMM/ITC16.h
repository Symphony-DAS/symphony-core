// Interface procedures for the Instrutech ITC16.
//
// Copyright (c) 1991, 1996 Instrutech Corporation, Great Neck, NY, USA
//
// Created by SKALAR Instruments, Seattle, WA, USA

#ifndef ITC16_H
#define ITC16_H

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
// Functions that return a status return 0 if the function succeeds,
// and a non-zero value if the function fails. The status values
// can be interpreted by ITC16_GetStatusText.

// Function ITC16_GetStatusText
//
// Translate a status value to a text string. The translated string
// is written into "text" with maximum length "length" characters.

ITC_Export int ITC16_GetStatusText(void* device, int status, char* text, int length) ITC_Import;


// Function ITC16_GetStructureSize
//
// Return the size of the structure that defines the device.

ITC_Export int ITC16_GetStructureSize() ITC_Import;

//Mike: Add Version

ITC_Export int ITC16_GetVersion(void* device, void* UserVersion, void* KernelVersion) ITC_Import;

// ITC16_Open
//
// Open the device driver.

ITC_Export int ITC16_Open(void* device, int device_number) ITC_Import;


// ITC16_Close
//
// Close the device and free resources associated with it.

ITC_Export int ITC16_Close(void* device) ITC_Import;


// ITC16_Initialize
//
// Initialize the device hardware.

ITC_Export int ITC16_Initialize(void* device) ITC_Import;


// Function ITC16_GetFIFOSize
//
// Return the size of the  FIFO memory, measured in samples.

ITC_Export int ITC16_GetFIFOSize(void* device) ITC_Import;


// ITC16_SetSamplingInterval
//
// Set the sampling rate.

#define ITC16_MINIMUM_TICKS 5
#define ITC16_MAXIMUM_TICKS 65535

ITC_Export int ITC16_SetSamplingInterval(void* device, int interval) ITC_Import;


// ITC16_SetSequence
//
// An array of length instructions is written out to the sequence RAM.
// Acquisition must be stopped to write to the sequence RAM.
//
// length - The number of entries for the sequence memory.
#define ITC16_SEQUENCE_RAM_SIZE		256

#define ITC16_OUTPUT_DA0 0x18
#define ITC16_OUTPUT_DA1 0x10
#define ITC16_OUTPUT_DA2 0x08
#define ITC16_OUTPUT_DA3 0x00
#define ITC16_OUTPUT_DIGITAL 0x40

#define ITC16_INPUT_AD0 0x07
#define ITC16_INPUT_AD1 0x06
#define ITC16_INPUT_AD2 0x05
#define ITC16_INPUT_AD3 0x04
#define ITC16_INPUT_AD4 0x03
#define ITC16_INPUT_AD5 0x02
#define ITC16_INPUT_AD6 0x01
#define ITC16_INPUT_AD7 0x00
#define ITC16_INPUT_DIGITAL 0x20

ITC_Export int ITC16_SetSequence(void* device, int length, int* instructions) ITC_Import;


// ITC16_InitializeAcquisition
//
// This routine must be called before acquisition can be performed after power
// up, and after each call to Stop.  The FIFOs are reset and enabled.

ITC_Export int ITC16_InitializeAcquisition(void* device) ITC_Import;


// ITC16_Start
//
// Initiate acquisition.  Data acquisition will stop on
// A/D FIFO overflow. D/A output will stop on D/A FIFO underflow.
//
// D/A output will be enabled only if 'output_enable' is non-zero.
//
// External triggering may be specified with a non-zero value in 
// 'external_trigger'. Otherwise acquisition will start immediately.

ITC_Export int ITC16_Start(void* device, 
						 int external_trigger, 
						 int output_enable,
						 int stoponoverflow,
						 int stoponunderrun) ITC_Import;

// ITC16_GetFIFOWriteAvailable
//
// Return the number of FIFO entries available for writing.

ITC_Export int ITC16_GetFIFOWriteAvailable(void* itc, int* available) ITC_Import;


// ITC16_WriteFIFO
//
// The buffer of(length) entries is written to the ITC16.
//
// Any value from 1 to the value returned by ITC16_GetFIFOWriteAvailable may
// be used as the length argument.

ITC_Export int ITC16_WriteFIFO(void* device, int length, short* data) ITC_Import;

// Function ITC16_GetFIFOReadAvailableAndOverflow
//
// 12.20.02 -> combine 2 function in one

ITC_Export int ITC16_GetFIFOReadAvailableOverflow(void* device, int* available, int* overflow) ITC_Import;

// ITC16_GetFIFOReadAvailable
//
// Return the number of acquired samples not yet read out of the FIFO.
// The "overflow" value is set to zero if FIFO overflow has not occurred,
// and a non-zero value if input FIFO overflow has occurred.

ITC_Export int ITC16_GetFIFOReadAvailable(void* itc, int* available) ITC_Import;


// ITC16_ReadFIFO
//
// The buffer is filled with length entries from the ITC16.
//
// Any value from 1 to the value returned by ITC16_GetFIFOReadAvailable may
// be used as the length argument.

ITC_Export int ITC16_ReadFIFO(void* device, int length, short* data) ITC_Import;


// Function ITC16_IsClipping
//
// Return the state of the clipping bit and clear the latch.

ITC_Export int ITC16_IsClipping(void* device, int* clipping) ITC_Import;


// ITC16_GetFIFOOverflow
//
// The 'overflow' parameter is set to zero if input FIFO overflow has
// not occurred, and non-zero if input FIFO overflow has occurred.

ITC_Export int ITC16_GetFIFOOverflow(void* device, int* overflow) ITC_Import;


// ITC16_Stop
//
// End acquisition immediately.

ITC_Export int ITC16_Stop(void* device) ITC_Import;


// ITC16_GetStatusOverflow
//
// Return a status value that corresponds to "FIFO Overflow".

ITC_Export int ITC16_GetStatusOverflow(void* device) ITC_Import;

// Function ITC16_Release
//
// Release the driver.
// Only of use under Microsoft Windows.

ITC_Export int ITC16_Release(void* device) ITC_Import;

// Function ITC16_Reserve
//
// Reserve the driver.
// Only of use under Microsoft Windows.

ITC_Export int ITC16_Reserve(void* device, int* busy) ITC_Import;

//Mike: Add allocation function to kernel level
ITC_Export int ITC16_AllocateMemory(void* device, int SizeInBytes, void** LinAddress) ITC_Import;
ITC_Export int ITC16_FreeMemory(void* device) ITC_Import;

ITC_Export int ITC16_ReadPCIConfig(	void* device,
									void* pciconfig) ITC_Import;
ITC_Export int ITC16_WritePCIConfig(	void* device,
									void* pciconfig) ITC_Import;

#define	PCI_ROM_SIZE (32 * 1024)
//Version 4.0: new sector size 128 -> 4096
#define PCI_OLD_ROM_SECTOR_SIZE 128
#define PCI_ROM_SECTOR_SIZE 4096
ITC_Export int ITC16_ReadPCIROMSector(void* device, int Address, unsigned char* PCIData) ITC_Import;
ITC_Export int ITC16_WritePCIROMSector(void* device, int Address, unsigned char* PCIData) ITC_Import;
ITC_Export int ITC16_GetPCISerialNumber(void* device, int* SerialNumber) ITC_Import;

ITC_Export int ITC16_GetDeviceInfo(void* device, int* DeviceType, int* DeviceNumber) ITC_Import;
ITC_Export int ITC16_GetUSBInfo(void* device, long* REVCTL, long* Speed, long* Version, long* SerialNumber) ITC_Import;

ITC_Export int ITC16_SetUSBDigitalOutput(void* device, int DigitalOutput) ITC_Import;
ITC_Export int ITC16_GetUSBDigitalInput(void* device, int* DigitalInput) ITC_Import;
ITC_Export int ITC16_SetUSBUserBit(void* device, int Value) ITC_Import;
#ifdef _WINDOWS 
ITC_Export int ITC16_GetTimer(void* device, LONGLONG* Timer) ITC_Import;
ITC_Export int ITC16_GetAcqTimer(void* device, LONGLONG* StartTimer, LONGLONG* StopTimer) ITC_Import;
ITC_Export int ITC16_StartByTimer(void* device, 
								int external_trigger,
								int output_enable, 
								int stoponoverflow, 
								int stoponunderrun,
								LONGLONG Timer) ITC_Import;
#else
ITC_Export int ITC16_GetTimer(void* device, long long* Timer) ITC_Import;
ITC_Export int ITC16_GetAcqTimer(void* device, long long* StartTimer, long long* StopTimer) ITC_Import;
ITC_Export int ITC16_StartByTimer(void* device, 
								int external_trigger,
								int output_enable, 
								int stoponoverflow, 
								int stoponunderrun,
								long long Timer) ITC_Import;
#endif
ITC_Export int ITC16_ControlTimers(	void* device, 
									long Control1, 
									long* Counter1, 
									long Control2, 
									long* Counter2);


//USB Stuff
#define USB16_CL	0x10000

ITC_Export int ITC16_LoadUSB(	void* device, 
							int datasize,
							char* data) ITC_Import;

#ifdef __APPLE__
ITC_Export unsigned long ITC16_ReadRegistry(void* device, unsigned long RegID, unsigned long* Value) ITC_Import;
ITC_Export unsigned long ITC16_WriteRegistry(void* device, unsigned long RegID, unsigned long Value) ITC_Import;
#endif

ITC_Export unsigned long ITC16_GetDriverHandle(void* device, void** hDevice) ITC_Import;

ITC_Export int ITC16_StopAndInitialize(void* device, int stop, int initialize) ITC_Import;
ITC_Export int ITC16_SetupAcquisition(void* device,
									int stop,
									int initialize, 
									int length, int* instructions,
									int timer_ticks, int reserved,
									int start,
									int external_trigger, 
									int output_enable,
									int stoponoverflow,
									int stoponunderrun) ITC_Import;

ITC_Export int ITC16_SmallRun(void* device,
							int length, int* instructions,
							int timer_ticks, int reserved,
							int OutputDataSize, short* OutputData,
							int InputDataSize, short* InputData,
							int external_trigger, int output_enable) ITC_Import;

ITC_Export int ITC16_SetUSBMode(void* device, int Mode, int Resereved) ITC_Import;

ITC_Export int ITC16_SingleWriteROM34(void* device, int Port, int Value) ITC_Import;
ITC_Export int ITC16_SingleReadROM4(void* device, short* Value) ITC_Import;

#ifdef __cplusplus
}
#endif

#endif
