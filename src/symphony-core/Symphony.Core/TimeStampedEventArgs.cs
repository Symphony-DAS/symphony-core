using System;
using log4net;

namespace Symphony.Core
{
    /// <summary>
    /// .Net event EventArgs subclass that has a timestamp associated with the event.
    /// 
    /// The event timestamp is the timestamp of the event args creation, not the firing of
    /// the event, which may happen at a later time.
    /// </summary>
    public class TimeStampedEventArgs : EventArgs
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Controller));

        public TimeStampedEventArgs(IClock clock)
        {
            try
            {
                TimeStamp = clock.Now;
            }
            catch(Exception e)
            {
                log.ErrorFormat("Exception retrieving Clock value. Falling back to system time. {0}", e);
                TimeStamp = DateTimeOffset.Now;
            }
        }

        public TimeStampedEventArgs(DateTimeOffset time)
        {
            TimeStamp = time;
        }

        public DateTimeOffset TimeStamp { get; private set; }
    }

    /// <summary>
    /// .Net event TimeStampedEventArgs subclass that describes an Exception and the time of its occurence
    /// </summary>
    public class TimeStampedExceptionEventArgs : TimeStampedEventArgs
    {
        public TimeStampedExceptionEventArgs(IClock clock, Exception ex)
            : base(clock)
        {
            Exception = ex;
        }

        public Exception Exception { get; private set; }
    }

    /// <summary>
    /// .Net event TimeStampedEventArgs subclass that describes an event
    /// related to the references Epoch at a particular time stamp. 
    /// </summary>
    public class TimeStampedEpochEventArgs : TimeStampedEventArgs
    {
        public TimeStampedEpochEventArgs(IClock clock, Epoch epoch)
            : base(clock)
        {
            Epoch = epoch;
        }

        public Epoch Epoch { get; private set; }
    }

    /// <summary>
    /// .Net event TimeStampedEventArgs subclass that describes output
    /// of a single IOutputData (span) of data for a particular device.
    /// </summary>
    public class TimeStampedStimulusOutputEventArgs : TimeStampedEventArgs
    {
        public TimeStampedStimulusOutputEventArgs(IClock clock, IDAQOutputStream stream, IIOData data)
            : base(clock)
        {
            Stream = stream;
            Data = data;
        }

        public TimeStampedStimulusOutputEventArgs(DateTimeOffset time, IDAQOutputStream stream, IIOData data)
            : base(time)
        {
            Stream = stream;
            Data = data;
        }

        public IDAQOutputStream Stream { get; private set; }
        public IIOData Data { get; private set; }
    }
}
