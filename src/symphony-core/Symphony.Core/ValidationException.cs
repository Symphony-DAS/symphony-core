using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    /// <summary>
    /// Exception indicating a validation failure in a pipeline component's configuration.
    /// </summary>
    class ValidationException : SymphonyException
    {
        public ValidationException(string msg) : base(msg) { }
    }
}
