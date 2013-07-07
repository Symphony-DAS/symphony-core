using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    /// <summary>
    /// IClock implementation that uses the system (CPU) clock. Although a
    /// SystemClock instance may be used as the connonical pipeline clock, 
    /// it is probably more appropriate to use a hardware-based clock such
    /// as a DAQ device clock if one is present in the configuration.
    /// </summary>
    public class SystemClock : IClock
    {
        public DateTimeOffset Now
        {
            get { return DateTimeOffset.Now; }
        }
    }
}
