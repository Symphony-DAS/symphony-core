using System.Collections.Generic;
using Symphony.Core;

namespace Symphony.ExternalDevices
{
    public interface IAxopatch
    {
        /// <summary>
        /// Interprets the given telegraph reading
        /// </summary>
        AxopatchInterop.AxopatchData ReadTelegraphData(IDictionary<string, IInputData> data);
    }
}
