// HekaIOBridge.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "HekaIOBridge.h"
#include "itcmm.h"
#include "0acqerrors.h"

#include <cassert>
#include <iostream>
#include <sstream>
#include <memory>
#include <vector>

//Automatcially link importer's project to ITCMM.lib
#pragma message("Adding automatic link to ITCMM.lib")  
#pragma comment(lib, "ITCMM.lib")

using namespace std;
using namespace System::Collections::Generic;
using namespace System::Linq;

namespace Heka {


	void IOBridge::Preload(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output)
	{
		WriteOutput(output, true);
	}

	void IOBridge::Write(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output)
	{
		WriteOutput(output, false);
	}

	void IOBridge::WriteOutput(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output, bool preload)
	{
		if(output->Count == 0) {
			return;
		}

		ITCChannelDataEx outputData[max(USB18_NUMBEROFOUTPUTS,ITC18_NUMBEROFOUTPUTS)];
		ZeroMemory(outputData, sizeof(outputData));



		IList<ChannelIdentifier>^ outputStreams = Enumerable::ToList(output->Keys);

		for(int i=0; i<outputStreams->Count; i++) {
			outputData[i].ChannelNumber = outputStreams[i].ChannelNumber;
			outputData[i].ChannelType = outputStreams[i].ChannelType;
			if(preload) {
				outputData[i].Command |= PRELOAD_FIFO_COMMAND_EX;
			}
		}

		long err;

		vector<vector<itcsample_t> > outputSamples(output->Count); 

		int32_t nsamples = output[outputStreams[0]]->Length;

		for(int i=0; i < output->Count; i++) {
			if(output[outputStreams[i]]->Length != nsamples) {
				throw gcnew ArgumentException("Preload sample buffers must be homogenous in length", "output.Values");
			}

			outputSamples[i] = vector<itcsample_t>(nsamples); //new itcsample_t[nsamples];
			array<itcsample_t>^ out = output[outputStreams[i]];
			for(int j=0; j<out->Length; j++) {
				outputSamples[i][j] = out[j];
			}
		}

		for(int i=0; i < outputStreams->Count; i++) {
			outputData[i].Value = nsamples;
		}


		for(int i=0; i < outputStreams->Count; i++) {
			outputData[i].DataPointer = outputSamples[i].data();
		}

		err = ITC_ReadWriteFIFO(GetDevice(), outputStreams->Count, outputData);
		if(err != ACQ_SUCCESS) {
			throw gcnew HekaDAQException("ITC_ReadWriteFIFO error", err);
		}
	}


	void CheckStatus(void *device, ITCStatus status)
	{
		long err = ITC_GetState(device, &status);
		if(err != ACQ_SUCCESS) {
			throw gcnew HekaDAQException("ITC_GetState error", err);
		}

		if( !(status.RunningMode & RUN_STATE) ||
			((status.RunningMode & ERROR_STATE) && (status.Overflow & (ITC_WRITE_UNDERRUN_H | ITC_WRITE_UNDERRUN_S))) ||
			((status.RunningMode & ERROR_STATE) && (status.Overflow & (ITC_READ_OVERFLOW_H | ITC_READ_OVERFLOW_S)))
			)
		{

			String ^msg;

			if(status.RunningMode == DEAD_STATE)
				msg = String::Format("ITC not running. State: DEAD (likely due to hardware underrun)");
			else
				msg = String::Format("ITC not running. State: 0x{0:X}, error code: 0x{1:X}", status.RunningMode, status.Overflow);

			throw gcnew HekaDAQException(msg);
		}
	}


	IDictionary<ChannelIdentifier, array<itcsample_t>^>^ 
		IOBridge::ReadWrite(IDictionary<ChannelIdentifier, array<itcsample_t>^>^ output,
		IList<ChannelIdentifier>^ input,
		int32_t nsamples)
	{

		if(nsamples < 0) {
			throw gcnew HekaDAQException("nsamples may not be less than zero.");
		}

		if(output->Keys->Count >= max(USB18_NUMBEROFOUTPUTS,ITC18_NUMBEROFOUTPUTS)) {
			throw gcnew HekaDAQException("Too many output channels");
		}

		if(input->Count >= max(USB18_NUMBEROFINPUTS,ITC18_NUMBEROFINPUTS)) {
			throw gcnew HekaDAQException("Too many input channels");
		}


		long err;

		if(output->Values->Count > maxOutputs) {
			throw gcnew HekaDAQException("Output stream number exceeds output stream availability.");
		}

		if(input->Count > maxInputs) {
			throw gcnew HekaDAQException("Input stream count exceeds input stream availability.");
		}

		ITCStatus status;
		ZeroMemory(&status, sizeof(status));
		status.CommandStatus = READ_ERRORS | READ_OVERFLOW | READ_RUNNINGMODE;

		ITCChannelDataEx outputData[max(USB18_NUMBEROFOUTPUTS,ITC18_NUMBEROFOUTPUTS)];
		ZeroMemory(outputData, sizeof(outputData));
		ITCChannelDataEx inputData[max(USB18_NUMBEROFINPUTS,ITC18_NUMBEROFINPUTS)];
		ZeroMemory(inputData, sizeof(inputData));


		IList<ChannelIdentifier>^ outputStreams = Enumerable::ToList(output->Keys);


		for(int i=0; i<outputStreams->Count; i++) {
			outputData[i].ChannelNumber = outputStreams[i].ChannelNumber;
			outputData[i].ChannelType = outputStreams[i].ChannelType;
		}

		for(int i=0; i<input->Count; i++) {
			inputData[i].ChannelNumber = input[i].ChannelNumber;
			inputData[i].ChannelType = input[i].ChannelType;
		}

		//check all outputs are correct length
		for each(array<itcsample_t>^ a in output->Values) {
			if(a->Length != nsamples) {
				throw gcnew HekaDAQException("Output not correct length");
			}
		}

		int32_t nIn = 0;
		int32_t nOut = 0;

		vector<vector<itcsample_t> > inputSamples(input->Count); 
		vector<vector<itcsample_t> > outputSamples(output->Count);

		for(int i=0; i < input->Count; i++) {
			inputSamples[i] = vector<itcsample_t>(max(2*nsamples, 2*TRANSFER_BLOCK_SAMPLES));
		}

		for(int i=0; i < output->Count; i++) {
			outputSamples[i] = vector<itcsample_t>(nsamples);

			array<itcsample_t>^ out = output[outputStreams[i]];

			for(int j=0; j<out->Length; j++) {
				outputSamples[i][j] = out[j];
			}
		}

		while(nIn < nsamples) {

			CheckStatus(GetDevice(), status);

			ITC_UpdateNow(GetDevice(), NULL);

			err = ITC_GetDataAvailable(GetDevice(), input->Count, inputData);

			bool blockAvailable = false;
			for(int i=0; i < input->Count; i++) {
				if(inputData[i].Value >= TRANSFER_BLOCK_SAMPLES) {
					inputData[i].Value = TRANSFER_BLOCK_SAMPLES;
					blockAvailable = true;
				} else {
					inputData[i].Value = 0;
				}
			}

			if(blockAvailable) {
				for(int i=0; i < outputStreams->Count; i++) {
					outputData[i].Value = TRANSFER_BLOCK_SAMPLES;
					if((int)(nOut + outputData[i].Value) >= output[outputStreams[i]]->Length) {
						outputData[i].Value = output[outputStreams[i]]->Length - nOut;
					}
				}
			} else {

				continue;
			}

			for(int i=0; i < input->Count; i++) {
				inputData[i].DataPointer = inputSamples[i].data() + nIn;
			}

			for(int i=0; i < outputStreams->Count; i++) {
				outputData[i].DataPointer = outputSamples[i].data() + nOut;
			}

			if(nOut < nsamples) {
				err = ITC_ReadWriteFIFO(GetDevice(), outputStreams->Count, outputData);
				if(err != ACQ_SUCCESS) {
					throw gcnew HekaDAQException("ITC_ReadWriteFIFO error", err);
				}

				nOut += TRANSFER_BLOCK_SAMPLES;
			}

			err = ITC_ReadWriteFIFO(GetDevice(), input->Count, inputData);
			if(err != ACQ_SUCCESS) {
				throw gcnew HekaDAQException("ITC_ReadWriteFIFO error", err);
			}

			nIn += TRANSFER_BLOCK_SAMPLES;
			for(int i=0; i < input->Count; i++) {
				ChannelIdentifier c = input[i];
				c.Samples = c.Samples + inputData[i].Value;
				input[i] = c;
			}


		}

		IDictionary<ChannelIdentifier, array<itcsample_t>^>^ result = gcnew Dictionary<ChannelIdentifier, array<itcsample_t>^>();


		for(int i=0; i < input->Count; i++) {
			array<itcsample_t>^ inData = gcnew array<itcsample_t>(input[i].Samples);
			for(unsigned int j=0; j < input[i].Samples; j++) {
				itcsample_t s = inputSamples[i][j];  
				inData[j] = s;
			}

			result[input[i]] = inData;
		}

		return result;
	}
}