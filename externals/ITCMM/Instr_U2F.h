#ifndef _U2F_h
#define _U2F_h

#ifdef _WINDOWS
	#pragma warning(disable:4996)       // no more string function warnings: ulix 11.10.2009
#endif

#include "ExportDefs.h"

#ifdef __APPLE__
#include "Compatibility.h"
#endif

#ifdef macintosh
#include "Compatibility.h"
#endif

#ifdef __cplusplus
extern "C" {  // only need to export C interface if
              // used by C++ source code
#endif

//****************************************************************

#define PRODUCT_ID			0
#define FUNCTION_ID			1
#define LOCATION_ID			2
#define DEVICE_ID			3
#define SPEED_ID			4
#define MEMORY_ID			5
#define ALGORITHM_ID		6
#define ERROR_ID			7

//****************************************************************

#define ITC18_PRODUCT		0
#define DVP32_PRODUCT		1
#define ITC1600_PRODUCT		2
#define ITC16_PRODUCT		3
#define PRODUCT_NUMBER		4

//****************************************************************

//U2F LCA's for ITC16 (Function ID)
#define U2F_ITC16_USB_STANDARD				0x00010000
//U2F Location for ITC16 (Location ID)
#define U2F_ITC16_LOCATION_USB				0x00000002

//U2F LCA's for ITC18 (Function ID)
#define U2F_ITC18_LCA_PEROM_SYSTEM			0x00000000
#define U2F_ITC18_LCA_PEROM_USER			0x00000001
#define U2F_ITC18_LCA_BOOTLOADER			0x00000100
#define U2F_ITC18_LCA_ISO_STANDARD			0x00000200
#define U2F_ITC18_LCA_ISO_PHSHIFT			0x00000201
#define U2F_ITC18_LCA_ISO_DYNCLAMP			0x00000202
#define U2F_ITC18_LCA_COMP_CMOS_256KW		0x00000300
#define U2F_ITC18_LCA_COMP_CMOS_1MW			0x00000301
#define U2F_ITC18_LCA_COMP_TTL_1MW			0x00000302
#define U2F_ITC18_LCA_COMP_PCM				0x00000400
#define U2F_ITC18_USB_STANDARD				0x00010000

//U2F Location for ITC18 (Location ID)
#define U2F_ITC18_LOCATION_COMP				0x00000000
#define U2F_ITC18_LOCATION_ISO				0x00000001
#define U2F_ITC18_LOCATION_USB				0x00000002

//U2F LCA's for DVP32 (Function ID)
#define U2F_DVP32_DSP_BOOTLOADER			0x00000000
#define U2F_DVP32_DSP_STANDARD				0x00000100
#define U2F_DVP32_LCA_CABLE_STANDARD_4		0x00000200
#define U2F_DVP32_LCA_DSP_STANDARD_3		0x00000300
#define U2F_DVP32_LCA_SIGNED_STANDARD_1		0x00000400
#define U2F_DVP32_LCA_UNSIGNED_STANDARD_1	0x00000401
#define U2F_DVP32_LCA_OVERLAY_STANDARD_2	0x00000500

//U2F Location for DVP32 (Location ID)
#define U2F_DVP32_LOCATION_DSP				0x00000000
#define U2F_DVP32_LOCATION_LCA1				0x00000001
#define U2F_DVP32_LOCATION_LCA2				0x00000002
#define U2F_DVP32_LOCATION_LCA3				0x00000003
#define U2F_DVP32_LOCATION_LCA4				0x00000004

//U2F LCA's for ITC1600 (Function ID)
#define U2F_ITC1600_DSP_STANDARD			0x00000100
#define U2F_ITC1600_DSP_EEPROMLOADER		0x00000101
#define U2F_ITC1600_DSP_BOOTLOADER			0x00000102
#define U2F_ITC1600_DSP_SYSTEMLOADER		0x00000103
#define U2F_ITC1600_DSP_TESTER				0x00000104
#define U2F_ITC1600_DSP_RACKLOADER			0x00000105
#define U2F_ITC1600_DSP_OUTPUTSPECIAL		0x00000106
#define U2F_ITC1600_LCA_HOST_STANDARD		0x00000200
#define U2F_ITC1600_LCA_RACK_STANDARD		0x00000300

//U2F Location for ITC1600 (Location ID)
#define U2F_ITC1600_LOCATION_DSP			0x00000000
#define U2F_ITC1600_LOCATION_HOST_LCA		0x00000001
#define U2F_ITC1600_LOCATION_RACK_LCA		0x00000002

//U2F Device Type (Device ID)
#define U2F_DEVICE_TYPE_TMS320C32			0x00000000
#define U2F_DEVICE_TYPE_DSP56301			0x00000001
#define U2F_DEVICE_TYPE_FX2LP				0x00000002
#define U2F_DEVICE_TYPE_3190A				0x00010000
#define U2F_DEVICE_TYPE_3195A				0x00010001
#define U2F_DEVICE_TYPE_4005XL				0x00010002
#define U2F_DEVICE_TYPE_4013XL				0x00010003
#define U2F_DEVICE_TYPE_XCS30XL				0x00010004

//U2F Device Speed (Speed ID)
#define U2F_DEVICE_SPEED_40MHZ				0x00000000
#define U2F_DEVICE_SPEED_80MHZ				0x00000001
#define U2F_DEVICE_SPEED_100MHZ				0x00000002
#define U2F_DEVICE_SPEED_48MHZ				0x00000003
#define U2F_DEVICE_XILINX_5					0x00010000
#define U2F_DEVICE_XILINX_4					0x00010001
#define U2F_DEVICE_XILINX_3					0x00010002

//U2F Memory Required (Memory ID)
#define U2F_MEMORY_NO						0x00000000
#define U2F_MEMORY_128Kx32					0x00010000
#define U2F_MEMORY_128Kx24					0x00010001
#define U2F_MEMORY_5V_CMOS_256Kx16			0x00020000
#define U2F_MEMORY_5V_CMOS_1Mx16			0x00020001
#define U2F_MEMORY_3_3V_TTL_1Mx16			0x00020002
#define U2F_MEMORY_4Kx8_INTERNAL			0x00030000
#define U2F_MEMORY_8Kx8_INTERNAL			0x00030001
#define U2F_MEMORY_16Kx8_INTERNAL			0x00030002
#define U2F_MEMORY_32Kx8_INTERNAL			0x00030003

//U2F Program Algorithm (Algorithm ID)
#define U2F_PROGRAM_ALG_NA					0x00000000
#define U2F_PROGRAM_ALG_LCA_STANDARD_SPEED	0x00010000
#define U2F_PROGRAM_ALG_LCA_FAST_SPEED		0x00010001

#define CHECKSUM_ADD						0x12345678

//****************************************************************

typedef struct
	{
	ULONG Version;
	ULONG ProductCode;
	ULONG NumberOfChunks;
	ULONG VersionChecksum;
	} global_u2f_header;

typedef struct
	{
	ULONG Function;		
	ULONG FunctionVersion;		
	ULONG Location;		//Hardware to use	
	ULONG Type;			//Type of hardware
	ULONG Speed;
	ULONG Memory;
	ULONG ByteSize;	
	ULONG Algorithm;	
	ULONG HeaderChecksum;
	} local_u2f_header;

//****************************************************************
ITC_Export LONG ITC_GetInfoU2F (LONG SelectID,
							LONG ProductID,
							LONG id,
							LONG SizeOfInfo,
							char* info);

ITC_Export LONG ITC_GetHeaderU2F (char* filename,
							global_u2f_header* header);

ITC_Export LONG ITC_GetSizeU2F (char* filename, 
							ULONG product,
							ULONG Number,	 //Number LCAs/DSPs to extract
							local_u2f_header needlheader[],
							LONG *gversion,  //U2F gVersion
							LONG lversion[], //U2F lVersion
							LONG psize[]     //Pointers to ByteSize
							);
ITC_Export LONG ITC_DecodeU2F (char* filename, 
							ULONG product,
							ULONG Number,	//Number LCAs/DSPs to extract
							local_u2f_header needlheader[],
							LONG *gversion, //U2F gVersion
							LONG lversion[],//U2F lVersion
							void* pted[]     //Pointers to Data
							);

//****************************************************************

#define U2F_SUCCESS					0
#define U2F_ERROR_OPEN				0x89100000
#define U2F_ERROR_READ				0x89B00000
#define U2F_ERROR_SEEK				0x89B10000
#define U2F_ERROR_CHECKSUM			0x89B20000
#define U2F_ERROR_CHUNK				0x89B30000
#define U2F_ERROR_VERSION			0x89000000
#define U2F_ERROR_PRODUCT			0x89010000
#define U2F_ERROR_MEMORY			0x89400000
#define U2F_ERROR_PARAMETER			0x89700000

//****************************************************************

#ifdef __cplusplus
}
#endif

#endif