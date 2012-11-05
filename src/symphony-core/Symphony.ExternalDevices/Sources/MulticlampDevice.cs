using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Symphony.Core;

namespace Symphony.ExternalDevices
{

    public interface IMultiClampCommander : IDisposable
    {
        event EventHandler<MulticlampParametersChangedArgs> ParametersChanged;
    }

    public sealed class MultiClampCommander : IMultiClampCommander
    {
        private readonly object _eventLock = new object();

        private uint SerialNumber { get; set; }
        private uint Channel { get; set; }

        public event EventHandler<MulticlampParametersChangedArgs> ParametersChanged;

        private IClock Clock { get; set; }

        public MultiClampCommander(uint serialNumber, uint channel, IClock clock)
        {
            SerialNumber = serialNumber;
            Channel = channel;
            Clock = clock;

            UInt32 lParam = MulticlampInterop.MCTG_Pack700BSignalIDs(this.SerialNumber, this.Channel); // Pack the above two into an UInt32
            int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MulticlampInterop.MCTG_OPEN_MESSAGE, (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);

            Win32Interop.MessageEvents.WatchMessage(Win32Interop.WM_COPYDATA, (sender, evtArgs) =>
            {
                // WM_COPYDATA LPARAM is a pointer to a COPYDATASTRUCT structure
                Win32Interop.COPYDATASTRUCT cds;
                cds = (Win32Interop.COPYDATASTRUCT)
                    Marshal.PtrToStructure(evtArgs.Message.LParam, typeof(Win32Interop.COPYDATASTRUCT));

                // WM_COPYDATA structure (COPYDATASTRUCT)
                // dwData -- RegisterWindowMessage(MCTG_REQUEST_MESSAGE_STR)
                // cbData -- size (in bytes) of the MC_TELEGRAPH_DATA structure being sent
                // lpData -- MC_TELEGRAPH_DATA*
                MulticlampInterop.MC_TELEGRAPH_DATA mtd;
                mtd = (MulticlampInterop.MC_TELEGRAPH_DATA)Marshal.PtrToStructure(cds.lpData, typeof(MulticlampInterop.MC_TELEGRAPH_DATA));
                var md = new MulticlampInterop.MulticlampData(mtd);

                OnParametersChanged(md);
            });
        }


        private void OnParametersChanged(MulticlampInterop.MulticlampData data)
        {
            lock (_eventLock)
            {
                if (ParametersChanged != null)
                {
                    ParametersChanged(this, new MulticlampParametersChangedArgs(Clock, data));
                }
            }
        }

        // Indirect Dispose method from http://msdn.microsoft.com/en-us/library/system.idisposable.aspx
        private bool _disposed = false;

        void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                UInt32 lParam = MulticlampInterop.MCTG_Pack700BSignalIDs(this.SerialNumber, this.Channel); // Pack the above two into an UInt32
                int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MulticlampInterop.MCTG_CLOSE_MESSAGE, (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);

                // Note disposing has been done.
                _disposed = true;

            }

        }

        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        ~MultiClampCommander()
        {
            Dispose(false);
        }
    }

    public class MulticlampParametersChangedArgs : TimeStampedEventArgs
    {
        public MulticlampParametersChangedArgs(IClock clock, MulticlampInterop.MulticlampData data)
            : base(clock)
        {
            this.Data = data;
        }

        public MulticlampInterop.MulticlampData Data { get; private set; }
    }

    public sealed class MulticlampDevice : ExternalDevice, IDisposable
    {


        public ConcurrentQueue<MulticlampParametersChangedArgs> DeviceParameters { get; set; }

        /// <summary>
        /// ExternalDevice implementation for MultiClamp 700[A,B] device.
        /// 
        /// Spec:
        /// Should register a unit conversion proc that uses device parameters to convert incoming/outgoing units
        /// Given a queue of parameters updates
        ///     Should use the most recent parameter update to calculate unit conversion for IOutputData
        /// Given a queue of paramteter updates
        ///     Should use the parameter update most recent but preceeding (in time) the input data InputTime to calculate unit conversion of IInputData
        ///     Should discard parameter updates older than the used parameter update
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="channel"></param>
        /// <param name="background"></param>
        public MulticlampDevice(uint serialNumber, uint channel, IMultiClampCommander commander, Controller c, Measurement background)
            : base("Multiclamp-" + serialNumber + "-" + channel, c, background)
        {
            DeviceParameters = new ConcurrentQueue<MulticlampParametersChangedArgs>();

            Commander = commander;
            Commander.ParametersChanged += (sender, mdArgs) => DeviceParameters.Enqueue(mdArgs);
        }

        private IMultiClampCommander Commander { get; set; }


        public Maybe<MulticlampParametersChangedArgs> DeviceParametersForOutput(DateTimeOffset time)
        {
            var result = GetFor(time);
            if (this.HasBoundInputStreams)
            {
                RemoveDeviceParametersPreceeding(time);
            }

            return result;
        }

        private void RemoveDeviceParametersPreceeding(DateTimeOffset time)
        {
            if(DeviceParameters.IsEmpty)
                return;

            MulticlampParametersChangedArgs cParams;
            if(DeviceParameters.TryPeek(out cParams))
            {
                   
            }

        }

        protected bool HasBoundInputStreams
        {
            get { return Streams.Keys.OfType<IDAQInputStream>().Count() > 0; }
        }

        private Maybe<MulticlampParametersChangedArgs> GetFor(DateTimeOffset dto)
        {
            // Open questions:
            // (*) Should I be ordering (via OrderBy) the queue results, or is it safe to assume they're already in order?
            // (*) Should I be removing any elements we don't use, and if so, should it be earlier or later ones?

            IEnumerable<MulticlampParametersChangedArgs> e = DeviceParameters.Where((a) => a.TimeStamp.Ticks < dto.UtcTicks).OrderBy((a) => a.TimeStamp);
            if (e.Count() > 0)
                return Maybe<MulticlampParametersChangedArgs>.Some(e.Last());
            else
                return Maybe<MulticlampParametersChangedArgs>.None();
        }


        // needs a shot at processing the OutputData on its way out to the board
        public override Measurement Convert(Measurement incoming, string outgoingUnits)
        {
            throw new NotImplementedException();
        }

        public Measurement Convert(Measurement incoming, string outgoingUnits, MulticlampInterop.MulticlampData deviceParams)
        {
            MulticlampInterop.ScaleFactorUnits[] allowedScaleFactorUnits;

            switch (deviceParams.ScaledOutputSignal)
            {
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_I_MEMB:
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_MEMB:
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_I_CMD_SUMMED:

                    allowedScaleFactorUnits = new[] {
                            MulticlampInterop.ScaleFactorUnits.V_A,
                            MulticlampInterop.ScaleFactorUnits.V_mA,
                            MulticlampInterop.ScaleFactorUnits.V_nA,
                            MulticlampInterop.ScaleFactorUnits.V_uA,
                            MulticlampInterop.ScaleFactorUnits.V_pA
                        };

                    CheckScaleFactorUnits(deviceParams, allowedScaleFactorUnits);

                    return null;
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx10:
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_IC_GLDR_V_MEMBx100:
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_EXT:
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_MEMB:
                case MulticlampInterop.SignalIdentifier.AXMCD_OUT_PRI_VC_GLDR_V_CMD_SUMMED:

                    allowedScaleFactorUnits = new[] {
                        MulticlampInterop.ScaleFactorUnits.V_mV,
                        MulticlampInterop.ScaleFactorUnits.V_uV,
                        MulticlampInterop.ScaleFactorUnits.V_V
                    };

                    CheckScaleFactorUnits(deviceParams, allowedScaleFactorUnits);

                    return null;
                default:
                    throw new ArgumentOutOfRangeException("Unsupported ScaledOutputSignal mode: " + deviceParams.ScaledOutputSignal);
            }
        }

        private static void CheckScaleFactorUnits(MulticlampInterop.MulticlampData deviceParams, MulticlampInterop.ScaleFactorUnits[] allowedScaleFactorUnits)
        {
            if (!allowedScaleFactorUnits.Contains(deviceParams.ScaleFactorUnits))
                throw new MulticlampDeviceException(deviceParams.ScaleFactorUnits + " is not an allowed unit conversion for scaled output mode.");
        }

        public override IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration)
        {
            /* 
             * IOuputData will be directed to a device (not an DAQStream) by the controller.
             * Controller should get mapping (device=>data) from the current Epoch instance.
             * 
             * Thus the standard PullOuputData will pull from the controller's queue for this
             * device.
             */

            //TODO should raise exception if duration is less than one sample
            IOutputData data = this.Controller.PullOutputData(this, duration); //TODO

            return new OutputData(data,
                data.DataWithUnits(MeasurementConversionTarget).Data).DataWithExternalDeviceConfiguration(Configuration);
        }

        // needs a shot at processing the InputData on its way back from the board
        public override void PushInputData(IDAQInputStream stream, IInputData inData)
        {
            IInputData convertedData = new InputData(inData,
                inData.DataWithUnits(MeasurementConversionTarget).Data); //TODO

            this.Controller.PushInputData(this, convertedData.DataWithExternalDeviceConfiguration(Configuration));
        }

        /// <summary>
        /// Verify that everything is hooked up correctly
        /// </summary>
        /// <returns></returns>
        public override Maybe<string> Validate()
        {
            Maybe<string> v = base.Validate();
            if (v)
            {
                //Specific validation here
            }

            return v;
        }


        // Indirect Dispose method from http://msdn.microsoft.com/en-us/library/system.idisposable.aspx
        private bool _disposed = false;

        void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                    Commander.Dispose();

            }

        }

        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        ~MulticlampDevice()
        {
            Dispose(false);
        }

    }


    public class MulticlampDeviceException : SymphonyException
    {
        public MulticlampDeviceException(string message)
            : base(message)
        {
        }

        public MulticlampDeviceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
