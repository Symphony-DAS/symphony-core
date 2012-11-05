// HekkaNative.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include "Windows.h"

#include "itcmm.h"
#include "0acqerrors.h"
#include <iostream>
#include <cassert>

//Automatcially link importer's project to ITCMM.lib
#pragma message("Adding automatic link to ITCMM.lib")  
#pragma comment(lib, "ITCMM.lib")

const int ITC18_PIPELINE_SAMPLES = 3;
using namespace std;

using namespace System;

int _tmain(int argc, _TCHAR* argv[])
{
	HANDLE dev = NULL;
	unsigned long num;
	//unsigned long devices[2] = {ITC00_ID, ITC18_ID};
	unsigned long devices[1] = {ITC18_ID};
	int ndevices = 1;

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
			cout << num << " devices." << endl;
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
			/*
			ITCPublicConfig config;
			ZeroMemory(&config, sizeof(config));
			config.OutputEnable = 1;
			err = ITC_ConfigDevice(dev, &config);
			if(err != ACQ_SUCCESS) {
			cout << "ITC_ConfigDevice : " << hex << err << endl;
			}
			*/
			err = ITC_ResetChannels(dev);
			if(err != ACQ_SUCCESS) {
				cout << "ITC_ResetChannels Error: " << hex << err << endl;
			}


			ITCChannelInfo info[2];
			ZeroMemory(info, sizeof(info));

			info[0].ChannelNumber = 0;
			info[0].ChannelType = OUTPUT_GROUP;

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

			const int nsamples = 100000;
			short in[2*nsamples+ITC18_PIPELINE_SAMPLES];
			short out[nsamples];
			array<short>^ managedOut = gcnew array<short>(nsamples);
			array<short>^ managedIn = gcnew array<short>(2*nsamples+ITC18_PIPELINE_SAMPLES);

			ZeroMemory(out, sizeof(out));
			ZeroMemory(in, sizeof(in));
			for(int i=0; i< nsamples; i++) {
				managedOut[i] = (i % 1000 * 100);
				out[i] = managedOut[i];
			}

			ITCChannelDataEx channelData[2];
			ZeroMemory(channelData, sizeof(channelData));

			channelData[0].ChannelNumber = 0;
			channelData[0].ChannelType = OUTPUT_GROUP; //output
			channelData[0].Command = 0;//ITC_STOP_ALL_ON_UNDERRUN;

			channelData[1].ChannelNumber = 0;
			channelData[1].ChannelType = INPUT_GROUP; //input
			channelData[1].Command = 0;//ITC_STOP_ALL_ON_OVERFLOW;

			int nOut = 0;
			int nIn = 0;
			channelData[0].Value = 2048;
			channelData[0].DataPointer = out;
			channelData[0].Command = PRELOAD_FIFO_COMMAND_EX;

			channelData[1].DataPointer = in;

			nOut += channelData[0].Value;

			err = ITC_ReadWriteFIFO(dev, 1, &channelData[0]);
			if(err != ACQ_SUCCESS) {
				cout << "ITC_ReadWriteFIFO preload error: " << hex << err << endl;
			}

			/*
			ITCStartInfo sParam;
			ZeroMemory(&sParam, sizeof(sParam));
			sParam.OutputEnable = 1;
			sParam.StopOnOverflow = 1;
			sParam.StopOnUnderrun = 1;

			err = ITC_Start(dev, &sParam);
			if(err != ACQ_SUCCESS) {
			cout << "ITC_Start Error: " << hex << err << endl;
			}
			*/
			ITCStatus status;
			ZeroMemory(&status, sizeof(status));
			status.CommandStatus = READ_ERRORS | READ_OVERFLOW | READ_RUNNINGMODE;

			err = ITC_GetState(dev, &status);

			channelData[0].Command = 0;

			err = ITC_Start(dev, NULL);

			while(nIn < nsamples) {

				err = ITC_GetDataAvailable(dev, 2, channelData);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_GetDataAvailableError: " << hex << err << endl;
				}

				if(channelData[1].Value >= 512)
				{
					//Add 512 points read 512 points
					channelData[0].Value = channelData[1].Value = 512;
				}
				else
					continue;


				channelData[0].DataPointer = out + nOut;

				nOut += channelData[0].Value;
				if(nOut == nsamples) {
					channelData[0].Command |= LAST_FIFO_COMMAND_EX;
				}

				//cout << "  Sending " << channelData[0].Value << " samples..." << endl;

				channelData[1].DataPointer = in + nIn;
				
				err = ITC_ReadWriteFIFO(dev, 2, channelData);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_ReadWriteFIFO : " << hex << err << endl;
				}

				for(int i = nIn; i < nIn + channelData[1].Value; i++) {
					managedIn[i] = in[i];
				}
				nIn += channelData[1].Value;

				err = ITC_GetState(dev, &status);
				if(err != ACQ_SUCCESS) {
					cout << "ITC_GetStatus : " << hex << err << endl;
				}
				if(
					!(status.RunningMode & RUN_STATE) ||
					((status.RunningMode & ERROR_STATE) && (status.RunningMode & ITC_WRITE_UNDERRUN_H))
					) 
				{
					cout << "ITC not running. State: 0x" << hex << status.RunningMode << ", error code: 0x" << hex << status.Overflow << endl;
					break;
				}

				ITC_UpdateNow(dev, NULL);


			}

			err = ITC_Stop(dev, NULL);
			if(err != ACQ_SUCCESS) {
				cout << "ITC_Stop : " << hex << err << endl;
			}

			int failures = 0;
			int dif;
			const double MAX_VOLTAGE_DIFF = 0.025;
			for(int i=0; i<nsamples-ITC18_PIPELINE_SAMPLES; i++) {
				dif = managedIn[i+ITC18_PIPELINE_SAMPLES] - managedOut[i];
				
				if(abs(dif) > MAX_VOLTAGE_DIFF*ANALOGVOLT) {
					cout << "Sample " << i+3 << " differs from output by " << ((double)dif)/ANALOGVOLT << "V" << endl;
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


	cout << endl << "Press any key to terminate this program. " ; cin.ignore();

	return 0;

}
