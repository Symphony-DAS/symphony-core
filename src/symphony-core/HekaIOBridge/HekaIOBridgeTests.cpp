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

	const int ITC18_PIPELINE_SAMPLES = 3;

	array<itcsample_t>^ IOBridge::RunTestMain(array<itcsample_t>^ managedOut, int nsamples)
	{
		HANDLE dev = NULL;
		unsigned long num;

		unsigned long devices[1] = {USB18_ID};
		int ndevices = 1;
		
		array<int16_t>^ managedIn = nullptr;

		for(int d=0; d < ndevices; d++) {
			unsigned long deviceID = devices[d];
			cout << "Device ID: " << devices[d] << endl;

			long err = ITC_Devices(deviceID, &num);
			if(err != ACQ_SUCCESS)
			{
				cout << "ITC_Devices Error: 0x" << hex << err << dec << "(" << err << ")" << endl;
				cout << endl << "Press any key to terminate this program. " ; cin.ignore();
				exit(1);
			}
			else
			{
				cout << num << " device(s)." << endl;
			}


			num = 1;

			for(unsigned c=0; c<num; c++) 
			{
				cout << "Device " << c << "..." << endl;

				err = ITC_OpenDevice(deviceID, c, SMART_MODE, &dev);
				if(err != ACQ_SUCCESS)
				{
					cout << "ITC_OpenDevice Error: 0x" << hex << err << dec << " (" << err << ")" << endl;
					cout << endl << "Press any key to terminate this program. " ; cin.ignore();
					exit(1);
				}

				assert(dev != NULL);

				err = ITC_InitDevice(dev, NULL);
				if(err != ACQ_SUCCESS)
				{
					cout << "ITC_InitDevice Error: 0x" << hex << err << endl;
					cout << endl << "Press any key to terminate this program. " ; cin.ignore();
					exit(1);
				}


				ITCPublicConfig config;
				ZeroMemory(&config, sizeof(config));
				config.OutputEnable = 1;
				err = ITC_ConfigDevice(dev, &config);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_ConfigDevice : " << hex << err << endl;
				}

				err = ITC_ResetChannels(dev);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_ResetChannels Error: " << hex << err << endl;
				}


				ITCChannelInfo info[4];
				ZeroMemory(info, sizeof(info));

				info[0].ChannelNumber = 0;
				info[0].ChannelType = OUTPUT_GROUP;
				info[0].ErrorMode = ITC_STOP_ALL_ON_UNDERRUN;

				info[1].ChannelNumber = 0;
				info[1].ChannelType = INPUT_GROUP;

				info[0].HardwareUnderrunValue = 1;

				double srate = 10000.0;
				info[0].SamplingRate = srate;
				info[1].SamplingRate = srate;

				err = ITC_SetChannels(dev, 2, info);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_SetChannels Error: " << hex << err << endl;
				}

				err = ITC_UpdateChannels(dev);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_UpdateChannels Error: " << hex << err << endl;
				}

				if(managedOut == nullptr || managedOut->Length != nsamples) {
					managedOut = gcnew array<int16_t>(nsamples);
					for(int i=0; i< nsamples; i++) 
					{
						managedOut[i] = (i % 1000 * 100);
					}
				}

				pin_ptr<int16_t> outputPtr = &managedOut[0];
				int16_t *out = outputPtr;//[nsamples];


				ITCChannelDataEx channelData[4];
				ZeroMemory(channelData, sizeof(channelData));

				channelData[0].ChannelNumber = 0;
				channelData[0].ChannelType = OUTPUT_GROUP;
				channelData[0].Command = ITC_STOP_ALL_ON_UNDERRUN;


				channelData[1].ChannelNumber = 0;
				channelData[1].ChannelType = INPUT_GROUP;
				channelData[1].Command = ITC_STOP_ALL_ON_OVERFLOW;


				int nOut = 0;
				int nIn = 0;
				channelData[0].Value = 2048;
				channelData[0].DataPointer = out;
				channelData[0].Command = PRELOAD_FIFO_COMMAND_EX;


				channelData[1].Value = 0;
				channelData[1].DataPointer = NULL;

				nOut += channelData[0].Value;

				cout << "ITC_ReadWriteFIFO" << endl;

				err = ITC_ReadWriteFIFO(dev, 1, &channelData[0]);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_ReadWriteFIFO preload error: " << hex << err << endl;
				}



				ITCStatus status;
				ZeroMemory(&status, sizeof(status));
				status.CommandStatus = READ_ERRORS | READ_OVERFLOW | READ_RUNNINGMODE;

				err = ITC_GetState(dev, &status);

				channelData[0].Command = 0;
				channelData[2].Command = 0;

				err = ITC_Start(dev, NULL);


				this->device = dev;
				int nPreload = nOut; //TODO for skipping in input
				array<int16_t>^ remainingOut = gcnew array<int16_t>(managedOut->Length - nOut);
				Array::Copy(managedOut, nOut, remainingOut, 0, remainingOut->Length);

				ChannelIdentifier cOut;
				cOut.ChannelNumber = 0;
				cOut.ChannelType = OUTPUT_GROUP;

				ChannelIdentifier cIn;
				cIn.ChannelNumber = 0;
				cIn.ChannelType = INPUT_GROUP;


				IDictionary<ChannelIdentifier, array<itcsample_t>^>^ outputDict = gcnew Dictionary<ChannelIdentifier, array<itcsample_t>^>();
				outputDict[cOut] = remainingOut;

				//managedIn = ReadWriteTest(remainingOut, nsamples);
				IDictionary<ChannelIdentifier, array<itcsample_t>^>^ inputDict;
				List<Heka::ChannelIdentifier>^ inputList = gcnew List<ChannelIdentifier>();
				inputList->Add(cIn);
				inputDict = ReadWrite(outputDict,
					inputList,
					managedOut->Length - nOut);
				managedIn = inputDict[cIn];

				

				for(int z = 0; z < 3; z++) {
					cout << "Still running..." << endl;

					List<Heka::ChannelIdentifier>^ inputList = gcnew List<ChannelIdentifier>();
					inputList->Add(cIn);

					ReadWrite(outputDict,
						inputList,
						managedOut->Length - nOut);
				}

				err = ITC_Stop(dev, NULL);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_Stop : " << hex << err << endl;
				}

				int failures = 0;
				int dif;
				const double MAX_VOLTAGE_DIFF = 0.1;
				const int ITC18_PIPELINE_SAMPLES = 3;
				for(int i=0; i<managedIn->Length-ITC18_PIPELINE_SAMPLES; i++) {
					dif = managedIn[i + ITC18_PIPELINE_SAMPLES] - managedOut[i];
					if(abs(dif) > MAX_VOLTAGE_DIFF*ANALOGVOLT) {
						System::Console::WriteLine("Out {0} => In {1}...", managedOut[i], managedIn[i + ITC18_PIPELINE_SAMPLES]);
						failures++;
					}

				}

				if(failures == 0) {
					cout << "  PASS: Loopback input matches output!" << endl;
				} else {
					cout << "  FAIL: " << failures << " samples do not match output!" << endl;
				}



				err = ITC_CloseDevice(dev);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_CloseDevice : " << hex << err << endl;
				}
			}


		}

		return managedIn;
	}


}
