using System;

namespace Symphony.ExternalDevices
{
    /// <summary>
    /// Interface to the MultiClamp Commander telegraph stream. Implementations
    /// fire a ParametersChanges event when the MC Commander telelgraph stream
    /// indicates a change in device parameters.
    /// 
    /// Implements IDisposable so that implementations may clean up resources
    /// when the connection to the MC Commander is no longer needed.
    /// </summary>
    public interface IMultiClampCommander : IDisposable
    {
        event EventHandler<MultiClampParametersChangedArgs> ParametersChanged;

        /// <summary>
        /// Attached MC device serial number (700B only)
        /// </summary>
        uint SerialNumber { get; }

        /// <summary>
        /// Attached MC COM port (700A only)
        /// </summary>
        uint COMPort { get; }

        /// <summary>
        /// Attached MC device number, AKA AxoBus ID (700A only)
        /// </summary>
        uint DeviceNumber { get; }

        /// <summary>
        /// Listens for parameter changes on this device channel
        /// </summary>
        uint Channel { get; }

        /// <summary>
        /// Attached MC type (e.g. 700A or 700B)
        /// </summary>
        MultiClampInterop.HardwareType HardwareType { get; }

        /// <summary>
        /// Requests a new parameter set from the MultiClamp Commander
        /// </summary>
        void RequestTelegraphValue();
    }
}