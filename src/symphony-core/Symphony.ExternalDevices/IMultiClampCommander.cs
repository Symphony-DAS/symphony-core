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
        /// Attached MC device serial number
        /// </summary>
        uint SerialNumber { get; }

        /// <summary>
        /// Listens for parameter changes on this device channel
        /// </summary>
        uint Channel { get; }

        /// <summary>
        /// Requests a new parameter set from the MultiClamp Commander
        /// </summary>
        void RequestTelegraphValue();
    }
}