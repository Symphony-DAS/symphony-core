using System;

using Heka.NativeInterop;
using Symphony.Core;

namespace Heka
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using NUnit.Framework;
    using HekkaDevice = System.IntPtr;
    using System.Collections.Generic;

    [TestFixture]
    public class NativeInteropTests
    {

        [Test]
        public void DeviceDetection()
        {
            uint numDevices = 0;

            uint result = ITCMM.ITC_Devices(ITCMM.USB18_ID, ref numDevices);

            Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                result,
                ErrorDescription.ErrorString(result));
            Assert.GreaterOrEqual(numDevices, 1, "We should find at least one ITC18 device.");
        }

        [Test]
        public void OpenDevice()
        {
            HekkaDevice device = HekkaDevice.Zero;

            uint err = ITCMM.ITC_OpenDevice(ITCMM.USB18_ID, 0, ITCMM.SMART_MODE, out device);

            try
            {
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );

                Assert.NotNull(device);
            }
            finally
            {
                err = ITCMM.ITC_CloseDevice(device);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );
            }

        }


        [Test]
        public void ReadAvailableSamples()
        {
            HekkaDevice device = HekkaDevice.Zero;

            uint err = ITCMM.ITC_OpenDevice(ITCMM.USB18_ID, 0, ITCMM.SMART_MODE, out device);
            if (err != ITCMM.ACQ_SUCCESS)
            {
                Assert.Fail(ErrorDescription.ErrorString(err));
            }

            try
            {
                //ITCMM.HWFunction hwf = new ITCMM.HWFunction();

                err = ITCMM.ITC_InitDevice(device, IntPtr.Zero); // ref hwf);

                ITCMM.ITCPublicConfig config = new ITCMM.ITCPublicConfig();
                config.OutputEnable = 1;

                err = ITCMM.ITC_ConfigDevice(device, ref config);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    Assert.Fail(ErrorDescription.ErrorString(err));
                }


                Assert.NotNull(device);

                ITCMM.ITCChannelInfo channelInfo = new ITCMM.ITCChannelInfo();
                channelInfo.ChannelType = ITCMM.H2D;
                channelInfo.ChannelNumber = 0;
                channelInfo.SamplingRate = 1000.0;
                Assert.AreEqual(System.IntPtr.Zero, channelInfo.FIFOPointer);

                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    ITCMM.ITC_SetChannels(device, 1, new ITCMM.ITCChannelInfo[] { channelInfo })
                    );

                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    (int)ITCMM.ITC_UpdateChannels(device)
                    );

                ITCMM.ITCChannelDataEx info = new ITCMM.ITCChannelDataEx();

                info.ChannelType = ITCMM.H2D;
                info.ChannelNumber = 0;

                ITCMM.ITCChannelDataEx[] arr = new ITCMM.ITCChannelDataEx[] { info };
                err = ITCMM.ITC_GetDataAvailable(device, 1, arr);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    Assert.Fail(ErrorDescription.ErrorString(err));
                }

                info = arr[0];

                Assert.That(info.Value, Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                err = ITCMM.ITC_CloseDevice(device);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );
            }
        }

        [Test]
        public void RoundTripChannelConfig()
        {
            HekkaDevice device = HekkaDevice.Zero;

            uint err = ITCMM.ITC_OpenDevice(ITCMM.USB18_ID, 0, ITCMM.SMART_MODE, out device);
            if (err != ITCMM.ACQ_SUCCESS)
            {
                Assert.Fail(ErrorDescription.ErrorString(err));
            }

            try
            {
                //ITCMM.HWFunction hwf = new ITCMM.HWFunction();

                err = ITCMM.ITC_InitDevice(device, IntPtr.Zero); //ref hwf);

                ITCMM.ITCPublicConfig config = new ITCMM.ITCPublicConfig();
                config.OutputEnable = 1;

                err = ITCMM.ITC_ConfigDevice(device, ref config);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    Assert.Fail(ErrorDescription.ErrorString(err));
                }


                Assert.NotNull(device);



                err = ITCMM.ITC_ResetChannels(device);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    Assert.Fail(ErrorDescription.ErrorString(err));
                }


                ITCMM.ITCChannelInfo[] info = new ITCMM.ITCChannelInfo[2];

                info[0].ChannelNumber = 0;
                info[0].ChannelType = ITCMM.OUTPUT_GROUP;
                info[1].ChannelNumber = 0;
                info[1].ChannelType = ITCMM.INPUT_GROUP;

                const double srate = 8000;
                info[0].SamplingRate = srate;
                info[1].SamplingRate = srate;


                err = ITCMM.ITC_SetChannels(device, 2, info);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    Assert.Fail(ErrorDescription.ErrorString(err));
                }

                err = ITCMM.ITC_UpdateChannels(device);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    Assert.Fail(ErrorDescription.ErrorString(err));
                }


                ITCMM.ITCChannelInfo[] actual = new ITCMM.ITCChannelInfo[2];
                actual[0].ChannelType = info[0].ChannelType;
                actual[0].ChannelNumber = info[0].ChannelNumber;
                actual[1].ChannelType = info[1].ChannelType;
                actual[1].ChannelNumber = info[1].ChannelNumber;


                err = ITCMM.ITC_GetChannels(device, 2, actual);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    Assert.Fail(ErrorDescription.ErrorString(err));
                }


                Assert.AreEqual(info[0].SamplingRate, actual[0].SamplingRate);
                Assert.AreEqual(info[1].SamplingRate, actual[1].SamplingRate);

            }
            finally
            {
                err = ITCMM.ITC_CloseDevice(device);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );
            }
        }

        [Test]
        public void SamplesArrayMarshalRoundTrip()
        {
            const int nsamples = 1000;
            short[] expected = new short[nsamples];
            for (short i = 0; i < nsamples; i++)
            {
                if (i < nsamples / 2)
                {
                    expected[i] = (short)(-i / 2);
                }
                else
                {
                    expected[i] = (short)(i / 2);
                }
            }

            short[] actual = new short[expected.Length];

            System.IntPtr samplePtr = Marshal.AllocHGlobal(nsamples * Marshal.SizeOf(typeof(short)));
            try
            {
                Marshal.Copy(expected, 0, samplePtr, nsamples);
                Marshal.Copy(samplePtr, actual, 0, nsamples);
            }
            finally
            {
                Marshal.FreeHGlobal(samplePtr);
            }

            CollectionAssert.AreEqual(expected, actual);

        }


        [Test]
        [Timeout(20000)]
        public void RoundTripWithContinuousAcquisition()
        {
            var io = new IOBridge(IntPtr.Zero, ITCMM.ITC18_NUMBEROFINPUTS, ITCMM.ITC18_NUMBEROFOUTPUTS);
            const int nsamples = 5000;
            var output = new short[nsamples];
            for (int i = 0; i < nsamples; i++)
            {
                output[i] = (short)(i % 1000 * 100);
            }

            short[] input = io.RunTestMain(output, nsamples);

            int failures = 0;
            const double MAX_VOLTAGE_DIFF = 0.05;
            for (int i = 0; i < input.Length - 3; i++)
            {
                //+ITC18_PIPELINE_SAMPLES
                int dif = input[i + 3] - output[i];

                if (Math.Abs(dif) > MAX_VOLTAGE_DIFF * ITCMM.ANALOGVOLT)
                {
                    failures++;
                }
            }

            Assert.That(failures, Is.LessThanOrEqualTo(4));
        }

        private static void CheckError(uint err, string p)
        {
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new Exception(p + " Error: " + err);
            }
        }

        [Test]
        public void CanReadITCClock()
        {
            HekkaDevice device;

            uint err = ITCMM.ITC_OpenDevice(ITCMM.USB18_ID, 0, ITCMM.SMART_MODE, out device);
            Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );

            try
            {
                //ITCMM.HWFunction sHWFunction = new ITCMM.HWFunction();

                err = ITCMM.ITC_InitDevice(device, IntPtr.Zero); //ref sHWFunction);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );

                double t1;
                err = ITCMM.ITC_GetTime(device, out t1);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );

                double t2 = t1;
                err = ITCMM.ITC_GetTime(device, out t2);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );

                Assert.Greater(t2, t1);
            }
            finally
            {
                err = ITCMM.ITC_CloseDevice(device);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );
            }
        }

        [Test]
        public void AsyncIORoundTrip()
        {
            HekkaDevice device = HekkaDevice.Zero;

            uint err = ITCMM.ITC_OpenDevice(ITCMM.USB18_ID, 0, ITCMM.SMART_MODE, out device);
            Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );

            try
            {
                err = ITCMM.ITC_InitDevice(device, IntPtr.Zero); //ref sHWFunction);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );


                uint expectedValue = 8000;

                var channelData = new ITCMM.ITCChannelDataEx[] { 
                    new ITCMM.ITCChannelDataEx {
                        ChannelNumber = 0, 
                        ChannelType = ITCMM.OUTPUT_GROUP,
                        Value = (short)expectedValue
                    }
                };

                err = ITCMM.ITC_AsyncIO(device, 1, channelData);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to write AsyncIO", err);
                }

                channelData = new ITCMM.ITCChannelDataEx[] { 
                    new ITCMM.ITCChannelDataEx {
                        ChannelNumber = 0, 
                        ChannelType = ITCMM.INPUT_GROUP,
                        Value = (short)expectedValue
                    }
                };

                err = ITCMM.ITC_AsyncIO(device, 1, channelData);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to write AsyncIO", err);
                }

                var actualValue = channelData[0].Value;

                Assert.That(actualValue, Is.InRange(expectedValue - 50, expectedValue + 50));

            }
            finally
            {
                err = ITCMM.ITC_CloseDevice(device);
                Assert.AreEqual(ITCMM.ACQ_SUCCESS,
                    err,
                    ErrorDescription.ErrorString(err)
                    );
            }
        }
    }
}
