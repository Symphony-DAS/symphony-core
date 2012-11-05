using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using log4net;

namespace Symphony.ExternalDevices
{
    /// <summary>
    /// Set of P/Invoke declarations
    /// </summary>
    static class Win32Interop
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static readonly ILog log = LogManager.GetLogger(typeof(Win32Interop));

        /// <summary>
        /// Overload for PostMessage that takes a Message struct instead of the individual params
        /// </summary>
        /// <param name="msg">the HWND, UINT msg, WPARAM, LPARAM to send</param>
        /// <returns>Whatever PostMessage() returns</returns>
        public static int PostMessage(Message msg)
        {
            log.DebugFormat("Post message: {0}", msg);
            return PostMessage(msg.HWnd, msg.Msg, msg.WParam, msg.LParam);
        }


        /// <summary>
        /// The "broadcast-message" window handle
        /// </summary>
        public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;


        /// <summary>
        /// WM_COPYDATA from Windows.h
        /// </summary>
        public const int WM_COPYDATA = 0x004A;

        /// <summary>
        /// Data structure sent by WM_COPYDATA
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        /// <summary>
        /// Event package sent when a Win32 WM_ message is received.
        /// </summary>
        public class MessageReceivedEventArgs : EventArgs
        {
            private readonly Message message;
            public MessageReceivedEventArgs(Message message) { this.message = message; }
            public Message Message { get { return message; } }
        }

        /// <summary>
        /// Static class designed to retrieve Windows messages (WM_*, BN_*, etc) broadcast across
        /// process boundaries. Code (slightly altered) comes from Stephen Toub's article in the
        /// June 2007 MSDN ".NET Matters" column ("Handling Messages in Console Apps").
        /// 
        /// To hook up an event-handler to a message, just do the following:
        /// <pre>
        /// int customMessage =
        ///    Win32Interop.RegisterWindowMessage("com.tedneward.messages.custom");
        ///
        /// MessageEvents.WatchMessage(customMessage,
        ///        delegate(object sender, MessageReceivedEventArgs mreArgs)
        ///        {
        ///            Console.WriteLine("Message received: {0}", mreArgs.Message);
        ///        });
        /// </pre>
        /// Doing so will start firing messages to that event handler as messages come
        /// in to the process.
        /// </summary>
        public static class MessageEvents
        {
            private static object lockObj = new object();
            private static MessageWindow window;
            private static IntPtr hwnd;
            private static SynchronizationContext context;

            /// <summary>
            /// Assign the event handler "handler" to be fired when a Windows message matching the
            /// value passed in "message" is fired.
            /// </summary>
            /// <param name="message"></param>
            /// <param name="handler"></param>
            public static void WatchMessage(int message, EventHandler<MessageReceivedEventArgs> handler)
            {
                EnsureInitialized();
                window.RegisterEventForMessage(message, handler);
            }

            /// <summary>
            /// The HWND for the hidden Form window receiving messages
            /// </summary>
            public static IntPtr WindowHandle
            {
                get
                {
                    EnsureInitialized();
                    return hwnd;
                }
            }

            private static void EnsureInitialized()
            {
                lock (lockObj)
                {
                    if (window == null)
                    {
                        context = AsyncOperationManager.SynchronizationContext;
                        using (ManualResetEvent mre = new ManualResetEvent(false))
                        {
                            Thread t = new Thread((ThreadStart)delegate
                            {
                                window = new MessageWindow();
                                hwnd = window.Handle;
                                mre.Set();
                                Application.Run();
                            });
                            t.Name = "MessageEvents message loop";
                            t.IsBackground = true;
                            t.Start();

                            mre.WaitOne();
                        }
                    }
                }
            }

            private class MessageWindow : Form
            {
                private ReaderWriterLock rwLock = new ReaderWriterLock();
                private Dictionary<int, EventHandler<MessageReceivedEventArgs>> handlers =
                    new Dictionary<int, EventHandler<MessageReceivedEventArgs>>();

                public void RegisterEventForMessage(int messageID, EventHandler<MessageReceivedEventArgs> handler)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    rwLock.AcquireWriterLock(Timeout.Infinite);
                    handlers[messageID] = handler;
                    rwLock.ReleaseWriterLock();
                }

                protected override void WndProc(ref Message m)
                {
                    rwLock.AcquireReaderLock(Timeout.Infinite);
                    bool handleMessage = handlers.ContainsKey(m.Msg);
                    rwLock.ReleaseReaderLock();

                    Message msg = m;

                    if (handleMessage)
                    {
                        log.DebugFormat("Handling message: {0}", m);
                        MessageEvents.context.Post(
                            (object state) =>
                            {
                                log.DebugFormat("Posting message: {0}", state);
                                EventHandler<MessageReceivedEventArgs> handler = handlers[msg.Msg];
                                if (handler != null)
                                    handler(null, new MessageReceivedEventArgs((Message)state));
                            },
                        msg);
                    }

                    base.WndProc(ref m);
                }
            }
        }
    }
}
