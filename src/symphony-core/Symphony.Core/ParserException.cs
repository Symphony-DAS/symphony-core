using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    /// <summary>
    /// Exception indicating an error in parsing rig configuration file.
    /// </summary>
    class ParserException : SymphonyException
    {
        public ParserException(string msg) : base(msg) {}
    }
}
