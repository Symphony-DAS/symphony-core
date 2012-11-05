// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the HEKAIOBRIDGE_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// HEKAIOBRIDGE_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
//#ifdef HEKAIOBRIDGE_EXPORTS
//#define HEKAIOBRIDGE_API __declspec(dllexport)
//#else
//#define HEKAIOBRIDGE_API __declspec(dllimport)
//#endif

#pragma warning (default : 4412)
using namespace System;
using namespace System::Collections::Generic;
using namespace Heka::NativeInterop;

#include <cstdint>

namespace Heka {
	typedef int16_t itcsample_t;


	public value struct ChannelIdentifier
	{
	public:
		property uint16_t ChannelNumber;
		property uint16_t ChannelType;
		property int32_t Samples; //Number of input/output samples


		virtual bool Equals(Object ^obj) override
		{
			if ( dynamic_cast<ChannelIdentifier^>(obj) )
			{
				ChannelIdentifier ^other = dynamic_cast<ChannelIdentifier^>(obj);
				return (other->ChannelNumber == ChannelNumber &&
					other->ChannelType == ChannelType);

			} else {
				return false;
			}
		}

		virtual int GetHashCode() override
		{
			return ChannelNumber.GetHashCode() ^ ChannelType.GetHashCode();
		}

	};

	public ref class IOBridge
	{
	public:
		static const unsigned int TRANSFER_BLOCK_SAMPLES = 512;

		IOBridge(IntPtr^ dev, unsigned int maxInputStreams, unsigned int maxOutputStreams) 
			: device(dev->ToPointer()), maxInputs(maxInputStreams), maxOutputs(maxOutputStreams) 
		{}

		array<itcsample_t>^ RunTestMain(array<itcsample_t>^ managedOut, int nsamples);

		IDictionary<ChannelIdentifier, array<itcsample_t>^>^ 
			ReadWrite(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output,
			IList<ChannelIdentifier>^ input,
			int32_t nsamples);

		void Preload(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output);
		void Write(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output);

	private:
		void *GetDevice() { return device; }

		void WriteOutput(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output,
			bool preload);

		void *device;

		unsigned const int maxInputs;
		unsigned const int maxOutputs;
	};
}