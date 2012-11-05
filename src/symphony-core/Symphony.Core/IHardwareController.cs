using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    /// <summary>
    /// Base interface for hardware controllers in the Symphony pipeline. Common
    /// hardware devices are DAQ interfaces, video output devices, and microscope
    /// controllers.
    /// </summary>
    public interface IHardwareController
    {
        /// <summary>
        /// Indicates if the hardware is running (i.e. DAQ running, video output running)
        /// </summary>
        bool Running { get; }

        /// <summary>
        /// Start this hardware controller and associated device. Optinally holds hardware start for a hardware
        /// triggered.
        /// </summary>
        /// <param name="waitForTrigger">True to wait for hardware trigger</param>
        /// <exception cref="InvalidOperationException">If waitForTrigger is true and this device does not support hardware-triggered start</exception>
        void Start(bool waitForTrigger);

        /// <summary>
        /// Stops this hardware controller and associated device.
        /// </summary>
        void Stop();

        /// <summary>
        /// Configuration for the hardware device.
        /// </summary>
        IDictionary<string, object> Configuration { get; }

        /// <summary>
        /// Give the hardware contorller a chance to configure itself for the present hardware. Called before the pipeline
        /// is fully configured; external devices may not be configured yet.
        /// </summary>
        void BeginSetup();


        /// <summary>
        /// Fired when hardware started.
        /// </summary>
        event EventHandler<TimeStampedEventArgs> Started;

        /// <summary>
        /// Fired when hardware stopped.
        /// </summary>
        event EventHandler<TimeStampedEventArgs> Stopped;

        /// <summary>
        /// Fired when hardware stopped due to hardware or software exception.
        /// </summary>
        event EventHandler<TimeStampedExceptionEventArgs> ExceptionalStop;

    }
}
