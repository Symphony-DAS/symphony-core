using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    /// <summary>
    /// Indicates an exception in Epoch persistence. In other words, things have gone very
    /// badly.
    /// </summary>
    public class PersistanceException : SymphonyException
    {
        public PersistanceException(string msg) : base(msg) { }
    }
}
