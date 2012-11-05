using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    /// <summary>
    /// Interface for components that can generate TimeStampedEvents (and thus require a Clock)
    /// </summary>
    public interface ITimelineProducer
    {
        /// <summary>
        /// IClock instance that provides the timestamp for TimeStampedEvents
        /// </summary>
        IClock Clock { get; set; }
    }
}
