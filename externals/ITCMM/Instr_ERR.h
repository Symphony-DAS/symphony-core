#ifndef _INSTR_ERR_h
#define _INSTR_ERR_h

#ifdef _WINDOWS
	#pragma warning(disable:4996)       // no more string function warnings: ulix 11.10.2009
#endif

#ifdef __cplusplus
extern "C" {  // only need to export C interface if
              // used by C++ source code
#endif

//****************************************************************
#define INSTR_ERR __declspec(dllexport)

//****************************************************************
#define DEVICE		"Device - "
#define ERRTYPE		"; Type - "
#define ERRGNUMBER	"; GN - "
#define ERRLNUMBER	"; LN - "
#define PLACE		"; Loc - "
#define CONN		"; Rel - "
#define SUCCESS		"Success"
#define UNKNOWN		"Unknown"
#define SPECIFIC	"Specific"
#define USER		"User layer"
#define KERNEL		"Kernel layer"
#define DSP			"DSP"
#define VDFS		"mVDFS"
#define DMA			"DMA"
#define TABLES		"Tables"
#define OVERLAYS	"Overlays"
#define DPR			"DPR"
#define INTERFACE_ITC	"Interface card"
#define U2FT		"U2F"
#define LCA			"LCA"
#define FRAME		"Frame"
#define TIMER		"Timer"
#define REGISTRY	"Registry"
#define TESTS		"Tests"
#define DVP32		"Dvp32"
#define ITC			"ITC1XXX"
#define VERSION		"Version"
#define OPEN		"Open"
#define CLOSE		"Close"
#define POWER		"Power / Timeout"
#define ALLOCATION	"Allocation"
#define READY		"Ready"
#define COMMAND		"Wrong Command / Mode"
#define PARAMETER	"Wrong Parameter"
#define MEMORY		"Wrong Memory"
#define ADDRESS		"Wrong Address"
#define SIZE		"Wrong sIZE"
#define READ		"Read"
#define WRITE		"Write"
#define STATE		"Wrong State"

#define DVP32		"Dvp32"
#define ITC1XXX		"ITC16/18/1600"

//****************************************************************

INSTR_ERR LONG GetErrorType(LONG ErrorCode,
							LONG MaxSize,
							char* info);

//****************************************************************

#ifdef __cplusplus
}
#endif

#endif