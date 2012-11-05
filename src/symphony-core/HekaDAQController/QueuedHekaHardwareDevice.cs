using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Heka.NativeInterop;
using Symphony.Core;
using log4net;

namespace Heka
{
    sealed class QueuedHekaHardwareDevice : IHekaDevice
    {
        private IntPtr DevicePtr { get; set; }
        private IOBridge Bridge { get; set; }
        DateTimeOffset StartupTime { get; set; }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly TaskScheduler _scheduler = new LimitedConcurrencyLevelTaskScheduler(1);
        private TaskFactory<uint> ItcmmReturnCodeTaskFactory { get; set; }
        private TaskFactory<IEnumerable<KeyValuePair<ChannelIdentifier, short[]>>> ItcmmReadWriteTaskFactory { get; set; }

        
        public QueuedHekaHardwareDevice(IntPtr dev, uint maxInputStreams, uint maxOutputStreams)
        {
            ItcmmReturnCodeTaskFactory = new TaskFactory<uint>(_cts.Token, TaskCreationOptions.None, TaskContinuationOptions.None, _scheduler);
            ItcmmReadWriteTaskFactory = new TaskFactory<IEnumerable<KeyValuePair<ChannelIdentifier, short[]>>>(_cts.Token, TaskCreationOptions.None, TaskContinuationOptions.None, _scheduler);

            DevicePtr = dev;
            Bridge = new IOBridge(DevicePtr, maxInputStreams, maxOutputStreams);
            StartupTime = DateTimeOffset.Now - new TimeSpan((long)Math.Floor(ITCClock * TimeSpan.TicksPerSecond));

        }


        private void ItcmmCall(Action fn)
        {
            var task = ItcmmReturnCodeTaskFactory.StartNew(() =>
                                                               {
                                                                   fn();
                                                                   return 0;
                                                               });

            task.Wait();
        }

        private uint ItcmmCall(Func<uint> fn)
        {
            var task = ItcmmReturnCodeTaskFactory.StartNew(fn);

            return task.Result;
        }

        private IEnumerable<KeyValuePair<ChannelIdentifier, short[]>> ItcmmCall(Func<IEnumerable<KeyValuePair<ChannelIdentifier, short[]>>> fn)
        {
            var task = ItcmmReadWriteTaskFactory.StartNew(fn);
            return task.Result;
        }


        public IEnumerable<KeyValuePair<ChannelIdentifier, short[]>>
            ReadWrite(IDictionary<ChannelIdentifier, short[]> output,
                        IList<ChannelIdentifier> input,
                        int nsamples)
        {
            return ItcmmCall(() => Bridge.ReadWrite(output, input, nsamples));
        }

        public DateTimeOffset Now
        {
            get
            {
                return StartupTime + new TimeSpan((long) Math.Floor(ITCClock*TimeSpan.TicksPerSecond));
            }
        }

        /// <summary>
        /// Note: Async in the ITC sense, not the .Net sense.
        /// </summary>
        public void SetStreamBackgroundAsyncIO(HekaDAQOutputStream stream)
        {
            if(stream != null)
            {
                WriteAsyncIO(stream, stream.Background);                
            }
        }

        private void WriteAsyncIO(HekaDAQOutputStream stream, IMeasurement streamValue)
        {
            lock(this)
            {
                var channelData = new[]
                                      {
                                          new ITCMM.ITCChannelDataEx
                                              {
                                                  ChannelNumber = stream.ChannelNumber,
                                                  ChannelType = (ushort) stream.ChannelType,
                                                  Value =
                                                      (short)
                                                      Converters.Convert(streamValue, HekaDAQOutputStream.DAQCountUnits)
                                                          .QuantityInBaseUnit
                                              }
                                      };

                uint err = ItcmmCall(() => ITCMM.ITC_AsyncIO(DevicePtr, 1, channelData));
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to write AsyncIO", err);
                }
            }
        }

        public IInputData ReadStreamAsyncIO(HekaDAQInputStream stream)
        {
            lock(this)
            {
                var channelData = new ITCMM.ITCChannelDataEx[]
                                      {
                                          new ITCMM.ITCChannelDataEx
                                              {
                                                  ChannelNumber = stream.ChannelNumber,
                                                  ChannelType = (ushort) stream.ChannelType
                                              }
                                      };

                uint err = ItcmmCall(() => ITCMM.ITC_AsyncIO(DevicePtr, 1, channelData));
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to read AsyncIO", err);
                }

                var inData =
                    new InputData(
                        new List<IMeasurement> {new Measurement(channelData[0].Value, HekaDAQInputStream.DAQCountUnits)},
                        new Measurement(0, "Hz"),
                        DateTimeOffset.Now)
                        .DataWithStreamConfiguration(stream, stream.Configuration);

                return inData.DataWithUnits(stream.MeasurementConversionTarget);
            }
        }

        public void Preload(IDictionary<ChannelIdentifier, short[]> output)
        {
            ItcmmCall(() => Bridge.Preload(output));
        }

        public void Write(IDictionary<ChannelIdentifier, short[]> output)
        {

            ItcmmCall(() => Bridge.Write(output));
        }

        private double ITCClock
        {
            get
            {
                double seconds = 0;
                uint err = ItcmmCall(() => ITCMM.ITC_GetTime(DevicePtr, out seconds));
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to get device time", err);
                }
                return seconds;
            }
        }

        private ITCMM.ITCStatus Status
        {
            get
            {

                var status = new ITCMM.ITCStatus
                                 {
                                     CommandStatus = ITCMM.READ_ERRORS |
                                                     ITCMM.READ_RUNNINGMODE |
                                                     ITCMM.READ_OVERFLOW
                                 };

                uint err = ItcmmCall(() => ITCMM.ITC_GetState(DevicePtr, ref status));
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to get device status", err);
                }

                return status;
            }
        }

        public bool Running
        {
            get
            {
                return (Status.RunningMode & ITCMM.RUN_STATE) > 0;
            }
        }

        public bool Overflow
        {
            get
            {
                return (Status.Overflow & (ITCMM.ITC_READ_OVERFLOW_H)) > 0;
            }
        }

        public bool Underrun
        {
            get
            {
                return (Status.Overflow & (ITCMM.ITC_WRITE_UNDERRUN_H)) > 0;
            }
        }

        public void PreloadSamples(StreamType channelType, ushort channelNumber, IList<short> samples)
        {

            ITCMM.ITCChannelDataEx[] channelData = new ITCMM.ITCChannelDataEx[1];

            channelData[0].ChannelType = (ushort)channelType;
            channelData[0].ChannelNumber = channelNumber;
            channelData[0].Value = (short)samples.Count;

            channelData[0].Command = ITCMM.PRELOAD_FIFO_COMMAND_EX;


            IntPtr samplesPtr = Marshal.AllocHGlobal(samples.Count * sizeof(short));

            try
            {

                Marshal.Copy(samples.ToArray(), 0, samplesPtr, samples.Count);
                channelData[0].DataPointer = samplesPtr;

                uint err = ItcmmCall(() => ITCMM.ITC_ReadWriteFIFO(DevicePtr, 1, channelData));
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to push data", err);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(samplesPtr);
            }
        }


        public int AvailableSamples(StreamType channelType, ushort channelNumber)
        {

            ItcmmCall(() => ITCMM.ITC_UpdateNow(DevicePtr, System.IntPtr.Zero));

            ITCMM.ITCChannelDataEx[] channelData = new ITCMM.ITCChannelDataEx[1];

            channelData[0].ChannelType = (ushort)channelType;
            channelData[0].ChannelNumber = channelNumber;

            uint err = ItcmmCall(() => ITCMM.ITC_GetDataAvailable(DevicePtr, 1, channelData));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Unable to get available FIFO points", err);
            }

            return channelData[0].Value;
        }

        public int MaxAvailableSamples(StreamType channelType, ushort channelNumber)
        {

            ITCMM.ITCChannelDataEx info = new ITCMM.ITCChannelDataEx();

            info.ChannelType = (ushort)channelType;
            info.ChannelNumber = channelNumber;

            var infoArr = new ITCMM.ITCChannelDataEx[1];
            infoArr[0] = info;
            uint err = ItcmmCall(() => ITCMM.ITC_GetFIFOInformation(DevicePtr, 1, infoArr));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Unable to get FIFO information", err);
            }

            info = infoArr[0];

            return info.Value;
        }

        public static IEnumerable<HekaDAQController> AvailableControllers()
        {
            var result = new List<HekaDAQController>();

            for (uint deviceType = 0; deviceType < ITCMM.MAX_DEVICE_TYPE_NUMBER; deviceType++)
            {
                if (deviceType == ITCMM.ITC00_ID)
                    continue;

                uint numDevices = 0;
                uint err = ITCMM.ITC_Devices(deviceType, ref numDevices);
                if (err != ITCMM.ACQ_SUCCESS)
                {
                    throw new HekaDAQException("Unable to find devices", err);
                }

                for (uint deviceNumber = 0; deviceNumber < numDevices; deviceNumber++)
                {
                    result.Add(new HekaDAQController(deviceType, deviceNumber));
                }

            }

            return result;
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(QueuedHekaHardwareDevice));

        internal static IHekaDevice OpenDevice(uint deviceType, uint deviceNumber, out ITCMM.GlobalDeviceInfo deviceInfo)
        {
            IntPtr dev;
            uint err = ITCMM.ITC_OpenDevice(deviceType, deviceNumber, ITCMM.SMART_MODE, out dev);
            if (err != ITCMM.ACQ_SUCCESS)
            {
                log.Error("Unable to open ITC device");
                throw new HekaDAQException("Unable to get device handle", err);
            }

            //ITCMM.HWFunction sHWFunction = new ITCMM.HWFunction();
            err = ITCMM.ITC_InitDevice(dev, IntPtr.Zero); //ref sHWFunction);
            //ITC_SetSoftKey

            // Configure device
            ITCMM.ITCPublicConfig config = new ITCMM.ITCPublicConfig();
            config.OutputEnable = 1;
            config.ControlLight = 1;

            err = ITCMM.ITC_ConfigDevice(dev, ref config);
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Unable to configure device", err);
            }

            deviceInfo = new ITCMM.GlobalDeviceInfo();
            err = ITCMM.ITC_GetDeviceInfo(dev, ref deviceInfo);
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Unable to get device info", err);
            }
            return new QueuedHekaHardwareDevice(dev,
                                          deviceInfo.NumberOfADCs + deviceInfo.NumberOfDIs +
                                          deviceInfo.NumberOfAUXIs,
                                          deviceInfo.NumberOfDACs + deviceInfo.NumberOfDOs +
                                          deviceInfo.NumberOfAUXOs
                );
        }

        public void CloseDevice()
        {

            uint err = ItcmmCall(() => ITCMM.ITC_CloseDevice(DevicePtr));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Unable to close device", err);
            }

            _cts.Cancel();
        }

        public void ConfigureChannels(IEnumerable<HekaDAQStream> streams)
        {

            uint err = ItcmmCall(() => ITCMM.ITC_ResetChannels(DevicePtr));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Channel Reset", err);
            }

            var infoList = streams
                .Select((s) => s.ChannelInfo);

            err = ItcmmCall(() => ITCMM.ITC_SetChannels(DevicePtr, (uint)infoList.Count(), infoList.ToArray()));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Set Channels", err);
            }

            err = ItcmmCall(() => ITCMM.ITC_UpdateChannels(DevicePtr));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Update Channels", err);
            }
        }

        public void StartHardware(bool waitForTrigger)
        {

            var startInfo = new ITCMM.ITCStartInfo
                                {
                                    ExternalTrigger = (uint)(waitForTrigger ? 1 : 0),
                                    OutputEnable = 1,
                                    StopOnOverflow = 1,
                                    StopOnUnderrun = 1,
                                    ResetFIFOs = 1,
                                };
            //TODO test waitForTrigger set

            uint err = ItcmmCall(() => ITCMM.ITC_Start(DevicePtr, ref startInfo));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Unable to start device", err);
            }
        }

        public void StopHardware()
        {
            uint err = ItcmmCall(() => ITCMM.ITC_Stop(DevicePtr, IntPtr.Zero));
            if (err != ITCMM.ACQ_SUCCESS)
            {
                throw new HekaDAQException("Unable to stop device", err);
            }
        }

        public ITCMM.ITCChannelInfo ChannelInfo(StreamType channelType, ushort channelNumber)
        {

            ITCMM.ITCChannelInfo[] info = new ITCMM.ITCChannelInfo[1];
            info[0] = new ITCMM.ITCChannelInfo() { ChannelNumber = channelNumber, ChannelType = (uint)channelType };

            uint err = ItcmmCall(() => ITCMM.ITC_GetChannels(DevicePtr, 1, info));
            if (err != ITCMM.ACQ_SUCCESS)
                throw new HekaDAQException("Unable to retrieve channel info struct.", err);

            return info[0];
        }

    }
}