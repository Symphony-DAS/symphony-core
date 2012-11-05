//////////////////////////////////////////////////////////////////////
//	7.7.2004
// Copyright 2005-2010 HEKA Electronik GmbH
//	Control Codes
//////////////////////////////////////////////////////////////////////

#ifndef __itcusb_h_	//ignore whole file, if already processed
#define __itcusb_h_

#define _USB_REINITIALIZE		0xB7			// re-initialize, call TD_Init( );

#define _USB_GETBOX_INFO		0xC5			// read REVCTL-Version-Speed-SERIALNUMBER register

#define _USB_READ_ROM4_N_TIMES	0xD0
#define _USB_GET_FIFO_STATUS	0xD1
#define _USB_WAIT_TXHEM			0xD2

#define _USB_START				0xD3			// ITC Start
#define _USB_STOP				0xD4			// ITC Stop
#define _USB_GETFIFOOVERFLOW	0xD5			// ITC GetFIFOOverflow
#define _USB_SETSAMPLING		0xD6			// ITC SetSampling
#define _USB_SETSSEQUENCE		0xD7			// ITC SetSequence
#define _USB_GETFIFOPOINTER		0xD9			// ITC Get FIFO Pointer
#define _USB_READ_SCSI_PORTC	0xDA			// Read SCSI Port "C"

#define _USB_WRITE_SCSI_PORTC	0xDB			// Write SCSI Port "C"
#define _USB_SET_RESET_USER_LINE 0xDC			// Set/Reset User Line

#define _USB_READ_EEPROM		0xDD			// Read One EPROM location

#define _USB_INITIALIZE_ACQ		0xDE			// Initialize Acquisition
#define _USB_READ_LIFETIMER		0xDF			// Read LifeTimer

#define _USB_READ_S_TIMER		0xE0			// Read Start/Stop Timers
#define _USB_CONTROL_TIMERS		0xE1			// Control Timer 1/2
#define _USB_EXTERNAL_TRIGGER	0xE2			// Start by external trigger
#define _USB_START_BY_TIMER		0xE3			// Start by timer
#define _USB_IS_CLIPPING		0xE4			// Is Clipping?
#define _USB_STOP_INITIALIZE	0xE5			// StopAndInitialize
#define _USB_SETUP_ACQUISITON	0xE6			// SetupAcquisition
#define _USB_SMALL_RUN			0xE7			// SmallRun
#define _USB_SET_MODE			0xE8			// Set Mode
#define _USB_WRITE_ROM3			0xE9			// Setup to "Write ROM3"
#define _USB_A_WRITE_ROM3		0xEA			// Actual Single Write ROM3 or ROM4
#define _USB_GET_SIGNATURE		0xEB			// Get ITC18 Signature
#define _USB_A_READ_ROM4		0xEC			// Actual Single Read ROM4
#define _USB_READ_FIFO_S		0xED			// ReadFIFO (small amount)
#define _USB_WRITE_AUX_OUT		0xEE			// ITC18_WriteAuxiliaryDigitalOutput

#ifdef _WINDOWS

typedef struct _VENDOR_REQUEST_IN
{
    BYTE    bRequest;
    WORD    wValue;
    WORD    wIndex;
    WORD    wLength;
    BYTE    direction;
    BYTE    bData;
} VENDOR_REQUEST_IN, *PVENDOR_REQUEST_IN;

///////////////////////////////////////////////////////////
//
// control structure for bulk and interrupt data transfers
//
///////////////////////////////////////////////////////////
typedef struct _BULK_TRANSFER_CONTROL
{
   ULONG pipeNum;
} BULK_TRANSFER_CONTROL, *PBULK_TRANSFER_CONTROL;

typedef struct _BULK_LATENCY_CONTROL
{
   ULONG bulkPipeNum;
   ULONG intPipeNum;
   ULONG loops;
} BULK_LATENCY_CONTROL, *PBULK_LATENCY_CONTROL;


///////////////////////////////////////////////////////////
//
// control structure isochronous loopback test
//
///////////////////////////////////////////////////////////
typedef struct _ISO_LOOPBACK_CONTROL
{
   // iso pipe to write to
   ULONG outPipeNum;

   // iso pipe to read from
   ULONG inPipeNum;

   // amount of data to read/write from/to the pipe each frame.  If not
   // specified, the MaxPacketSize of the out pipe is used.
   ULONG packetSize;

} ISO_LOOPBACK_CONTROL, *PISO_LOOPBACK_CONTROL;

///////////////////////////////////////////////////////////
//
// control structure for isochronous data transfers
//
///////////////////////////////////////////////////////////
typedef struct _ISO_TRANSFER_CONTROL
{
   //
   // pipe number to perform the ISO transfer to/from.  Direction is
   // implied by the pipe number.
   //
   ULONG PipeNum;
   //
   // ISO packet size.  Determines how much data is transferred each
   // frame.  Should be less than or equal to the maxpacketsize for
   // the endpoint.
   //
   ULONG PacketSize;
   //
   // Total number of ISO packets to transfer.
   //
   ULONG PacketCount;
   //
   // The following two parameters detmine how buffers are managed for
   // an ISO transfer.  In order to maintain an ISO stream, the driver
   // must create at least 2 transfer buffers and ping pong between them.
   // BufferCount determines how many buffers the driver creates to ping
   // pong between.  FramesPerBuffer specifies how many USB frames of data
   // are transferred by each buffer.
   //
   ULONG FramesPerBuffer;     // 10 is a good value
   ULONG BufferCount;         // 2 is a good value
} ISO_TRANSFER_CONTROL, *PISO_TRANSFER_CONTROL;


///////////////////////////////////////////////////////////
//
// control structure for sending vendor or class specific requests
// to the control endpoint.
//
///////////////////////////////////////////////////////////
typedef struct _VENDOR_OR_CLASS_REQUEST_CONTROL
{
   // transfer direction (0=host to device, 1=device to host)
   UCHAR direction;

   // request type (1=class, 2=vendor)
   UCHAR requestType;

   // recipient (0=device,1=interface,2=endpoint,3=other)
   UCHAR recepient;
   //
   // see the USB Specification for an explanation of the
   // following paramaters.
   //
   UCHAR requestTypeReservedBits;
   UCHAR request;
   USHORT value;
   USHORT index;
} VENDOR_OR_CLASS_REQUEST_CONTROL, *PVENDOR_OR_CLASS_REQUEST_CONTROL;

#define GET_CONFIG_DESCRIPTOR_LENGTH(pv) \
    ((pUsb_Configuration_Descriptor)pv)->wTotalLength

typedef struct __usb_Dev_Descriptor__ {
    UCHAR bLength;
    UCHAR bDescriptorType;
    USHORT bcdUSB;
    UCHAR bDeviceClass;
    UCHAR bDeviceSubClass;
    UCHAR bDeviceProtocol;
    UCHAR bMaxPacketSize0;
    USHORT idVendor;
    USHORT idProduct;
    USHORT bcdDevice;
    UCHAR iManufacturer;
    UCHAR iProduct;
    UCHAR iSerialNumber;
    UCHAR bNumConfigurations;
} Usb_Device_Descriptor, *pUsb_Device_Descriptor;

typedef struct __usb_Config_Descriptor__ {
    UCHAR bLength;
    UCHAR bDescriptorType;
    USHORT wTotalLength;
    UCHAR bNumInterfaces;
    UCHAR bConfigurationValue;
    UCHAR iConfiguration;
    UCHAR bmAttributes;
    UCHAR MaxPower;
} Usb_Configuration_Descriptor, *pUsb_Configuration_Descriptor;

typedef struct __usb_String_Descriptor__ { //TPM added the usb_String_Descriptor
    UCHAR bLength;
    UCHAR bDescriptorType;
    WCHAR bString[1];
} Usb_String_Descriptor, *pUsb_String_Descriptor;

typedef struct _GET_STRING_DESCRIPTOR_IN
{
   UCHAR    Index;
   USHORT   LanguageId;
} GET_STRING_DESCRIPTOR_IN, *PGET_STRING_DESCRIPTOR_IN;

#define GET_STRING_DESCRIPTOR_LENGTH(pv) \
    ((pUsb_String_Descriptor)pv)->bLength

///////////////////////////////////////////////////////
//
//              IOCTL Definitions
//
///////////////////////////////////////////////////////

#define IOCTL_INDEX  0x0800

#define IOCTL_GET_PIPE_INFO				CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+0,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_GET_DEVICE_DESCRIPTOR		CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+1,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_GET_CONFIGURATION_DESCRIPTOR CTL_CODE(FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+2,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_BULK_OR_INTERRUPT_WRITE	CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+3,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_BULK_OR_INTERRUPT_READ	CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+4,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_VENDOR_REQUEST			CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+5,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_GET_CURRENT_CONFIG		CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+6,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_RESET						CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+12,\
													METHOD_IN_DIRECT,  \
													FILE_ANY_ACCESS)

#define IOCTL_RESETPIPE					CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+13,\
													METHOD_IN_DIRECT,  \
													FILE_ANY_ACCESS)

#define IOCTL_ABORTPIPE					CTL_CODE(FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+15,\
													METHOD_IN_DIRECT,  \
													FILE_ANY_ACCESS)

#define IOCTL_SETINTERFACE				CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+16,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_GET_STRING_DESCRIPTOR		CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+17,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

// Perform an IN transfer over the specified bulk or interrupt pipe.
//
// lpInBuffer: BULK_TRANSFER_CONTROL stucture specifying the pipe number to read from
// nInBufferSize: sizeof(BULK_TRANSFER_CONTROL)
// lpOutBuffer: Buffer to hold data read from the device.  
// nOutputBufferSize: size of lpOutBuffer.  This parameter determines
//    the size of the USB transfer.
// lpBytesReturned: actual number of bytes read
#define IOCTL_BULK_READ					CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+19,\
													METHOD_OUT_DIRECT,  \
													FILE_ANY_ACCESS)

// Perform an OUT transfer over the specified bulk or interrupt pipe.
//
// lpInBuffer: BULK_TRANSFER_CONTROL stucture specifying the pipe number to write to
// nInBufferSize: sizeof(BULK_TRANSFER_CONTROL)
// lpOutBuffer: Buffer of data to write to the device
// nOutputBufferSize: size of lpOutBuffer.  This parameter determines
//    the size of the USB transfer.
// lpBytesReturned: actual number of bytes written
#define IOCTL_BULK_WRITE				CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+20,\
													METHOD_IN_DIRECT,  \
													FILE_ANY_ACCESS)

// The following IOCTL's are defined as using METHOD_DIRECT_IN buffering.
// This means that the output buffer is directly mapped into system
// space and probed for read access by the driver.  This means that it is
// brought into memory if it happens to be paged out to disk.  Even though
// the buffer is only probed for read access, it is safe (probably) to
// write to it as well.  This read/write capability is used for the loopback
// IOCTL's

// Retrieve the current USB frame number from the Host Controller
//
// lpInBuffer: NULL
// nInBufferSize: 0
// lpOutBuffer: PULONG to hold current frame number
// nOutputBufferSize: sizeof(PULONG)
#define IOCTL_GET_CURRENT_FRAME_NUMBER	CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+21,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

// Performs a vendor or class specific control transfer to EP0.  The contents of
// the input parameter determine the type of request.  See the USB spec
// for more information on class and vendor control transfers.
//
// lpInBuffer: PVENDOR_OR_CLASS_REQUEST_CONTROL
// nInBufferSize: sizeof(VENDOR_OR_CLASS_REQUEST_CONTROL)
// lpOutBuffer: pointer to a buffer if the request involves a data transfer
// nOutputBufferSize: size of the transfer buffer (corresponds to the wLength
//    field of the USB setup packet)
#define IOCTL_VENDOR_OR_CLASS_REQUEST	CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+22,\
													METHOD_IN_DIRECT,  \
													FILE_ANY_ACCESS)

// Retrieves the actual USBD_STATUS code for the most recently failed
// URB.
//
// lpInBuffer: NULL
// nInBufferSize: 0
// lpOutBuffer: PULONG to hold the URB status
// nOutputBufferSize: sizeof(ULONG)
#define IOCTL_GET_LAST_ERROR			CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+23,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

// Reads from the specified ISO endpoint. (USB IN Transfer)
//
// lpInBuffer: ISO_TRANSFER_CONTROL
// nInBufferSize: sizeof(ISO_TRANSFER_CONTROL)
// lpOutBuffer: buffer to hold data read from the device
// nOutputBufferSize: size of the read buffer.
#define IOCTL_ISO_READ					CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+25,\
													METHOD_OUT_DIRECT,  \
													FILE_ANY_ACCESS)

// Writes to the specified ISO endpoint. (USB OUT Transfer)
//
// lpInBuffer: ISO_TRANSFER_CONTROL
// nInBufferSize: sizeof(ISO_TRANSFER_CONTROL)
// lpOutBuffer: buffer to hold data to write to the device
// nOutputBufferSize: size of the write buffer.
#define IOCTL_ISO_WRITE					CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+26,\
													METHOD_IN_DIRECT,  \
													FILE_ANY_ACCESS)

#define IOCTL_DEVELOPMENT_DOWNLOAD		CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+27,\
													METHOD_IN_DIRECT,  \
													FILE_ANY_ACCESS)

#define IOCTL_GET_DRIVER_VERSION		CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+29,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_START_ISO_STREAM			CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+30,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_STOP_ISO_STREAM			CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+31,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_READ_ISO_BUFFER			CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+32,\
													METHOD_OUT_DIRECT,  \
													FILE_ANY_ACCESS)

#define IOCTL_SET_FEATURE				CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+33,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_USB_RESERVE				CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+34,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_USB_RELEASE				CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+35,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_ALLOCATE_MEMORY			CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+36,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)

#define IOCTL_FREEMEMORY				CTL_CODE(	FILE_DEVICE_UNKNOWN,  \
													IOCTL_INDEX+37,\
													METHOD_BUFFERED,  \
													FILE_ANY_ACCESS)



#endif //_WINDOWS

#endif //__itcusb_h_