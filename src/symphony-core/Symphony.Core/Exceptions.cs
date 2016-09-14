using System;

namespace Symphony.Core
{
    /// <summary>
    /// Base class for Exceptions in the Symphony framework.
    /// </summary>
    public abstract class SymphonyException : Exception
    {
        public SymphonyException(string message)
            : base(message)
        {
        }

        public SymphonyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
