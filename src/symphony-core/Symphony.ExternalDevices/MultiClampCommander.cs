using System;
using System.Runtime.InteropServices;
using Symphony.Core;
using log4net;

namespace Symphony.ExternalDevices
{

    /// <summary>
    /// Event arguments containing a MultiClampInterop.MulticlampData describing
    /// the *new* parameters of the MultiClamp device. The MulticlampData is likely
    /// derived from a MC Commander telegraph Windows event.
    /// </summary>
    public class MultiClampParametersChangedArgs : TimeStampedEventArgs
    {
        public MultiClampParametersChangedArgs(IClock clock, MultiClampInterop.MulticlampData data)
            : base(clock)
        {
            this.Data = data;
        }

        public MultiClampInterop.MulticlampData Data { get; private set; }
    }


    /// <summary>
    /// Implementation of the IMultiClampCommander interface for Windows.
    /// 
    /// Usage:
    /// <example>
    /// <code>
    /// using(var multiClampCommander = new MultiClampCommander(serial, channel, clock)) {
    ///   multiClampCommander.ParametersChanges += (mcc, args) => { //handle device parameters };
    ///   ...
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public sealed class MultiClampCommander : IMultiClampCommander
    {
        private readonly object _eventLock = new object();

        public uint SerialNumber { get; private set; }
        public uint Channel { get; private set; }

        public event EventHandler<MultiClampParametersChangedArgs> ParametersChanged;

        private IClock Clock { get; set; }

        private static readonly ILog log = LogManager.GetLogger(typeof(MultiClampCommander));

        /// <summary>
        /// Constructs a new MultiClampCommander for a given serial number and channel.
        /// </summary>
        /// <param name="serialNumber">MultiClamp device serial number</param>
        /// <param name="channel">MultiClamp channel</param>
        /// <param name="clock">IClock for generating event time stamps</param>
        public MultiClampCommander(uint serialNumber, uint channel, IClock clock)
        {
            SerialNumber = serialNumber;
            Channel = channel;
            Clock = clock;

            var lParam = DeviceLParam();

            RegisterForWmCopyDataEvents();
            RegisterForReconnectEvent(lParam);
            
            OpenMultiClampConversation(lParam);
            RequestTelegraphValue(lParam);
        }

        private uint DeviceLParam()
        {
            UInt32 lParam = MultiClampInterop.MCTG_Pack700BSignalIDs(this.SerialNumber, this.Channel);
                // Pack the above two into an UInt32
            return lParam;
        }

        private void RegisterForReconnectEvent(uint lParam)
        {
            log.Debug("Registering for MCTG_RECONNECT_MESSAGE messages...");
            Win32Interop.MessageEvents.WatchMessage(MultiClampInterop.MCTG_RECONNECT_MESSAGE, (sender, evtArgs) =>
                                                                                                  {
                                                                                                      log.DebugFormat(
                                                                                                          "Received MCTG_RECONNECT_MESSAGE: {0}",
                                                                                                          evtArgs.Message);

                                                                                                      if ((IntPtr) lParam ==
                                                                                                          evtArgs.Message.LParam)
                                                                                                      {
                                                                                                          OpenMultiClampConversation
                                                                                                              (lParam);
                                                                                                          RegisterForWmCopyDataEvents
                                                                                                              ();
                                                                                                          RequestTelegraphValue((uint)lParam);
                                                                                                      }
                                                                                                  });
        }

        public void RequestTelegraphValue()
        {
            RequestTelegraphValue(DeviceLParam());
        }

        private static void RequestTelegraphValue(uint lParam)
        {
            log.Debug("Requesting telegraph data...");
            int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MultiClampInterop.MCTG_REQUEST_MESSAGE,
                                                  (IntPtr) Win32Interop.MessageEvents.WindowHandle, (IntPtr) lParam);
            log.DebugFormat("  result = {0}", result);
        }

        private static void OpenMultiClampConversation(uint lParam)
        {
            log.Debug("Opening MultiClamp conversation...");
            int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MultiClampInterop.MCTG_OPEN_MESSAGE,
                                                  (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);
            log.DebugFormat("  result = {0}", result);
        }

        private void RegisterForWmCopyDataEvents()
        {
            log.Debug("Registering from WM_COPYDATA messages");

            Win32Interop.MessageEvents.WatchMessage(Win32Interop.WM_COPYDATA, (sender, evtArgs) =>
                                                                                  {   
                                                                                      // WM_COPYDATA LPARAM is a pointer to a COPYDATASTRUCT structure
                                                                                      Win32Interop.COPYDATASTRUCT cds;
                                                                                      cds = (Win32Interop.COPYDATASTRUCT)
                                                                                            Marshal.PtrToStructure(
                                                                                                evtArgs.Message.LParam,
                                                                                                typeof(
                                                                                                    Win32Interop.COPYDATASTRUCT));

                                                                                      // WM_COPYDATA structure (COPYDATASTRUCT)
                                                                                      // dwData -- RegisterWindowMessage(MCTG_REQUEST_MESSAGE_STR)
                                                                                      // cbData -- size (in bytes) of the MC_TELEGRAPH_DATA structure being sent
                                                                                      // lpData -- MC_TELEGRAPH_DATA*
                                                                                      try
                                                                                      {
                                                                                          if (cds.lpData != IntPtr.Zero)
                                                                                          {
                                                                                              MultiClampInterop.
                                                                                                  MC_TELEGRAPH_DATA mtd
                                                                                                      =
                                                                                                      (
                                                                                                      MultiClampInterop.
                                                                                                          MC_TELEGRAPH_DATA
                                                                                                      )
                                                                                                      Marshal.
                                                                                                          PtrToStructure
                                                                                                          (cds.lpData,
                                                                                                           typeof (
                                                                                                               MultiClampInterop
                                                                                                               .
                                                                                                               MC_TELEGRAPH_DATA
                                                                                                               ));
                                                                                              var md =
                                                                                                  new MultiClampInterop.
                                                                                                      MulticlampData(
                                                                                                      mtd);

                                                                                              log.Debug(
                                                                                                  "WM_COPYDATA message recieved from MCCommander");
                                                                                              OnParametersChanged(md);
                                                                                          }
                                                                                      }
                                                                                      catch (ArgumentOutOfRangeException)
                                                                                      {
                                                                                          log.ErrorFormat("WM_COPYDATA message received from MCCommander, but operating mode is not valid.");
                                                                                          RequestTelegraphValue((uint)evtArgs.Message.LParam);
                                                                                      }
                                                                                  });
        }


        private void OnParametersChanged(MultiClampInterop.MulticlampData data)
        {
            lock (_eventLock)
            {
                if (ParametersChanged != null)
                {
                    ParametersChanged(this, new MultiClampParametersChangedArgs(Clock, data));
                }
            }
        }

        // Indirect Dispose method from http://msdn.microsoft.com/en-us/library/system.idisposable.aspx
        private bool _disposed = false;

        void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    //Handle manage object disposal

                    // This object will be cleaned up by the Dispose method.
                    // Therefore, you should call GC.SupressFinalize to
                    // take this object off the finalization queue
                    // and prevent finalization code for this object
                    // from executing a second time.
                    GC.SuppressFinalize(this);
                }

                UInt32 lParam = MultiClampInterop.MCTG_Pack700BSignalIDs(this.SerialNumber, this.Channel); // Pack the above two into an UInt32
                int result = Win32Interop.PostMessage(Win32Interop.HWND_BROADCAST, MultiClampInterop.MCTG_CLOSE_MESSAGE, (IntPtr)Win32Interop.MessageEvents.WindowHandle, (IntPtr)lParam);

                // Note disposing has been done.
                _disposed = true;

            }

        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~MultiClampCommander()
        {
            Dispose(false);
        }
    }
}