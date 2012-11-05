#ifndef Compatibility_h
#define Compatibility_h

typedef		unsigned long long	ULONGLONG;

#ifdef _WINDOWS // __MWERKS__ 
#define noErr 0
#endif

#ifndef _WINDOWS // __MWERKS__ 

typedef void* HANDLE;
#define INVALID_HANDLE_VALUE NULL
typedef		unsigned long	DWORD;
typedef		unsigned short	WORD;
typedef		short           SHORT;
typedef		unsigned short  USHORT;
typedef 	unsigned char	UCHAR;
#ifndef __OBJC__
typedef char		BOOL;
#endif
typedef 	unsigned char	BYTE;
typedef		signed char	CHAR;
#ifndef __COREFOUNDATION_CFPLUGINCOM__
typedef		unsigned long	ULONG;
typedef		void*			LPVOID;
#endif
typedef		long			LONG;
typedef		unsigned int	UINT;

typedef		long long		LONGLONG;
typedef		long long 		__int64;
typedef		long long 		LARGE_INTEGER;

#define CALLBACK



#define ERROR_SUCCESS 0




#ifndef HIWORD
#define HIWORD(w) (((w)&0xffff0000)>>16)
#endif
#ifndef LOWORD
#define LOWORD(w) ((w)&0xffff)
#endif
#ifndef min
#define min(a,b) ((a) < (b) ? (a) : (b))
#endif
#ifndef max
#define max(a,b) ((a) > (b) ? (a) : (b))
#endif

#define __try
#define __except(qqq) if (0)

#define _timeb timeb
#define _ftime ftime

#ifdef __cplusplus
extern "C" {
#endif //__cplusplus
void SwapBytes(SHORT* p, int n);
DWORD GetTickCount();
#ifdef __cplusplus
}
#endif //__cplusplus

#define ConfigOffset(Reg) \
((unsigned char)&(((PCI_CONFIG_HEADER_0*)NULL)->Reg))

#ifdef __APPLE__
#include <sys/time.h>
//Intel does not have it. if you build the kernel
#define _MAZ_INCLUDE_TIMEB_
#ifdef _MAZ_BUILD_DRIVER_
   #undef _MAZ_INCLUDE_TIMEB_
#endif

#ifdef _MAZ_INCLUDE_TIMEB_
#include <sys/timeb.h>
#endif

#ifdef __cplusplus
extern "C" {
#endif //__cplusplus
int ftime(struct timeb *tp);
int getch(void); 
int kbhit(void);
#ifdef __cplusplus
}
#endif //__cplusplus

#ifndef NULL
#define NULL 0
#endif

//#define MAX_PATH MAXPATHLEN
#ifndef MAX_PATH
#define MAX_PATH PATH_MAX
#endif

#define ZeroMemory(p,s) bzero(p,s)
#define FillMemory(p,s,c) memset(p,c,s)
#define CopyMemory(d,s,size) bcopy((s),(d),(size))

#endif //__APPLE__

#ifndef DWORD_PTR
#define DWORD_PTR DWORD
#endif

#endif //WINDOW


#endif