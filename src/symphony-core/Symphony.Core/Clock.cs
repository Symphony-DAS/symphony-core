using System;

namespace Symphony.Core
{
    /// <summary>
    /// Interface for objects that can serve as the cononical clock for the Symphony pipeline.
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// The DateTimeOffset (in local time) of the current instance.
        /// </summary>
        DateTimeOffset Now { get; }
    }
}
