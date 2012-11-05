using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using log4net;


namespace Symphony.Core
{
    /// <summary>
    /// Interface for DAQ Controllers  in the Symphony.Core pipeline.
    /// 
    /// A DAQ Controller manages a single A/D device such as an ITC-18.
    /// 
    /// The DAQ Controller is responsible for:
    ///     - initializing the A/D hardware
    ///     - starting the A/D hardware, optionally waiting for a trigger
    ///     - pulling IOutputData from devices associated with the active output streams (IDAQOutputStream)
    ///     - pushing IInputData from active IDAQInputStreams to the appropriate devices
    /// </summary>
    public interface IDAQController : ITimelineProducer, IHardwareController
    {

        /// <summary>
        /// The DAQController has one DAQStream for each of its channels. Although
        /// and these are typically identified by a unique channel number in the hardware,
        /// this number has no purpose other than as an identifier, so we model it
        /// as a Name attribute on the DAQStream, lest we start doing funny 
        /// offset-games with the identifiers.
        /// </summary>
        IEnumerable<IDAQStream> Streams { get; }

        //TODO this method should go away (streams unique by name)
        /// <summary>
        /// Gets an enumerable list of all IDAQStreams belonging to this controller with the given name.
        /// </summary>
        /// <param name="name">Stream name</param>
        /// <returns>Enumerable of IDAQStreams with the given name</returns>
        IEnumerable<IDAQStream> GetStreams(string name);

        /// <summary>
        /// Gets thes single stream of belonging to this controller with the given name.
        /// </summary>
        /// <param name="name">Stream name</param>
        /// <returns>Stream with the given name or null if none exists</returns>
        /// <exception cref="InvalidOperationException">More than one stream exists with given name</exception>
        IDAQStream GetStream(string name); //throws if more than one

        /// <summary>
        /// Gets the stream of the given type with given name.
        /// </summary>
        /// <typeparam name="T">IDAQStream type</typeparam>
        /// <param name="name">Stream name</param>
        /// <returns>Stream of given type and name or null if none exists</returns>
        /// <exception cref="InvalidOperationException">More than one stream exists with given name</exception>
        T GetStream<T>(string name) where T : class, IDAQStream;


        /// <summary>
        /// Of all Streams assoiated with this DAQController, the IDAQInputStreams
        /// </summary>
        IEnumerable<IDAQInputStream> InputStreams { get; }

        /// <summary>
        /// Of all Streams assoiated with this DAQController, the IDAQOutputStreams
        /// </summary>
        IEnumerable<IDAQOutputStream> OutputStreams { get; }


        /// <summary>
        /// Validates the configuration of this IDAQController
        /// </summary>
        /// <returns>A Maybe monad indicating validation (bool) or error (string)</returns>
        Maybe<string> Validate();

        /// <summary>
        /// Event indicating that the DAQController processed a single iteration (typically ProcessInterval in duration)
        /// of data acquisition.
        /// </summary>
        event EventHandler<TimeStampedEventArgs> ProcessIteration;

        /// <summary>
        /// Event indicating that a Stimulus IIOData span was pushed "to the wire". Upstream elements
        /// may wish to record the pipeline configuration of these data spans.
        /// </summary>
        event EventHandler<TimeStampedStimulusOutputEventArgs> StimulusOutput;

        /// <summary>
        /// Request that this IDAQController stop its associated hardware device.
        /// </summary>
        void RequestStop();

        /// <summary>
        /// The name of this controller.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Blocks for completion of all asynchronous input tasks (incoming data).
        /// </summary>
        void WaitForInputTasks();

        /// <summary>
        /// Asynchronously sets the background for the given stream. Stream background given by s.Background.
        /// </summary>
        /// <param name="s">Output stream</param>
        void ApplyStreamBackground(IDAQOutputStream s);
    }

    public interface IMutableDAQController : IDAQController
    {
        /// <summary>
        /// Add a new IDAQStream to this DAQController. Some DAQControllers
        /// have a fixed set of streams, defined by their hardware. These
        /// controllers should create those streams at construction and should
        /// not allow addition of new streams.
        /// 
        /// </summary>
        /// <exception cref="InvalidOperationException">if this method is not supported by the DAQController instance</exception>
        /// <param name="stream">New stream</param>
        void AddStream(IDAQStream stream);
    }


    /// <summary>
    /// This class provides base (abstract) implementation for a DAQController
    /// that operates on the main (or calling) thread. This base class is
    /// appropriate for implementations whose driver manages its own threads
    /// and provides asynchronous callbacks.
    /// 
    /// <para>The core of the DAQController functionality is the Process() loop.
    /// The controller runs the process loop once per ProcessInterval. On each
    /// iteration, the next IOutputData are pulled asynchronously from the active IOutputStreams.
    /// ProcessLoopIteration() is called to send this IOutputData to the device and to collect
    /// available IInputData. Returned IInputData are pushed asynchronously to the appropriate
    /// IOutputStreams.</para>
    /// 
    /// <para>Most subclasses will simply override the ProcessLoopIteration() method
    /// to do the actual work of pushing the IOutputData out to the
    /// hardware and getting back IInputData from the hardware to be
    /// sent back up the pipeline.</para>
    /// </summary>
    /// <see cref="Process"/>
    /// <see cref="ProcessLoopIteration"/>
    public abstract class DAQControllerBase : IDAQController
    {
        private object eventLock = new Object();

        /// <summary>
        /// The name of this controller
        /// </summary>
        public virtual string Name
        {
            get { return GetType().Name; }
        }

        /// <summary>
        /// The clock used to capture timestamps for data
        /// </summary>
        public IClock Clock { get; set; }

        /// <summary>
        /// Interval for running the Process loop.
        /// </summary>
        public TimeSpan ProcessInterval { get; protected set; }


        protected DAQControllerBase()
        {
            this.DAQStreams = new HashSet<IDAQStream>();
            this.Configuration = new Dictionary<string, object>();
            this.InputTasks = new List<Task>();

        }


        /// <summary>
        /// Flag indicating whether or not the represented hardware device is running
        /// </summary>
        public virtual bool Running { get; protected set; }

        protected IEnumerable<IDAQOutputStream> ActiveOutputStreamsWithData
        {
            get { return ActiveOutputStreams.Where((s) => s.HasMoreData); }
        }

        protected IEnumerable<IDAQOutputStream> ActiveOutputStreams
        {
            get
            {
                return OutputStreams.Where((s) => s.Active);
            }
        }

        protected IEnumerable<IDAQInputStream> ActiveInputStreams { get { return InputStreams.Where(s => s.Active); } }

        /// <summary>
        /// Validates the configuration of this DAQController.
        /// </summary>
        /// <returns>A monad indicating validation success (as a bool) or the failure message (as a string)</returns>
        public virtual Maybe<string> Validate()
        {
            return new Maybe<string>(true);
        }

        /// <summary>
        /// (Added because I can't figure out how else we should add
        /// streams after construction, which is what the configuration
        /// system requires us to do right now. --TKN)
        /// </summary>
        /// <param name="stream">IDAQStream to add to this DAQController</param>
        /// <exception cref="InvalidOperationException">If this DAQController does not support adding streams</exception>
        //protected virtual void AddStream(IDAQStream stream)
        //{
        //    DAQStreams.Add(stream);
        //}

        /// <summary>
        /// Calls Process() to start the acquisition loop.
        /// </summary>
        /// <param name="waitForTrigger">Indicates whether the hardware device should wait for a trigger to start</param>
        /// <remarks>Calling Start() when the DAQController is already Running
        /// is a NOP.</remarks>
        public virtual void Start(bool waitForTrigger)
        {
            if (!Running)
            {
                Running = true;
                StopRequested = false;
                OnStarted();

                Process(waitForTrigger);
            }
        }

        /// <summary>
        /// Called to start hardware device in subclasses.
        /// 
        /// Will be called before Process() loop begins.
        /// 
        /// Subclasses should immediately (synchronously) start the hardware device or block,
        /// waiting for trigger if waitForTrigger is true.
        /// </summary>
        /// <param name="waitForTrigger">Indicates whether the hardware device should wait for a trigger to start</param>
        protected abstract void StartHardware(bool waitForTrigger);

        /// <summary>
        /// Spins in a loop pulling outbound data from the output pipeline and pushing
        /// inbound data back up the input pipeline. The "actual work" of what to
        /// do with the data elements is done in the ProcessLoopIteration()
        /// method, and as a result this method generally won't need to be
        /// overridden.
        /// <para>The Process loop continues while the hardware device is Running or until ShouldStop() returns true.</para>
        /// </summary>
        /// <see cref="ShouldStop"/>
        protected void Process(bool waitForTrigger)
        {
            // Spin in a loop, pulling from the output pipeline and 
            // pushing the results back through the input pipeline
            try
            {
                WillBeginProcessLoop();
                ProcessLoop(waitForTrigger);
            }
            catch (Exception x)
            {
                log.ErrorFormat("Exception during DAQController.ProcessLoop: {0}", x);
                StopWithException(x);
            }
            finally
            {
                if (Running)
                    Stop();

                DidEndProcessLoop();
            }
        }

        protected virtual void WillBeginProcessLoop()
        {
        }

        protected virtual void DidEndProcessLoop()
        {
        }


        private void ProcessLoop(bool waitForTrigger)
        {

            TimeSpan deficit = TimeSpan.Zero;

            // Pull outgoing data
            var outgoingDataTasks = NextOutgoingData();
            bool start = true;


            var iterationStart = DateTimeOffset.Now;

            while (Running && !ShouldStop())
            {
                
                // Collect outgoing data task results
                var outgoingData = new Dictionary<IDAQOutputStream, IOutputData>();
                bool warningShown = false;
                foreach (KeyValuePair<IDAQOutputStream, Task<IOutputData>> task in outgoingDataTasks)
                {

                    if (task.Value.IsFaulted)
                    {
                        if (task.Value.Exception != null)
                        {
                            log.ErrorFormat("An error occurred pulling output data: {0}", task.Value.Exception);
                            throw task.Value.Exception;
                        }
                    }  

                    if (!task.Value.IsCompleted && !warningShown)
                    {
                        log.Debug("At least one DAQ output task has not completed. This may cause an output underrun.");
                        warningShown = true;
                    }
 
                    outgoingData.Add(task.Key, task.Value.Result);
                }


                // Pull next outgoing data
                outgoingDataTasks = NextOutgoingData();

                // Start hardware on first iteration
                if (start)
                {
                    StartHardware(waitForTrigger);
                    start = false;
                }

                // Run iteration
                var outputTime = Clock.Now;
                var incomingData = ProcessLoopIteration(outgoingData, deficit);

                // Push Data
                PushIncomingData(incomingData);

                // Push Output events
                PushOutputDataEvents(outputTime, outgoingData);


                OnProcessIteration();

                //Wait for rest of the process interval
                deficit = SleepForRestOfIteration(iterationStart, ProcessInterval);

                iterationStart += ProcessInterval;
            }
        }

        protected virtual TimeSpan SleepForRestOfIteration(DateTimeOffset iterationStart, TimeSpan iterationDuration)
        {
            if (DateTimeOffset.Now < (iterationStart + iterationDuration))
            {
                while (DateTimeOffset.Now < (iterationStart + iterationDuration)) { }
            }

            return DateTimeOffset.Now - (iterationStart + iterationDuration);
        }

        private IList<Task> InputTasks { get; set; }

        private void PushIncomingData(IEnumerable<KeyValuePair<IDAQInputStream, IInputData>> incomingData)
        {
            // Throw if any previous tasks faulted
            if (InputTasks.Any(t => t.IsFaulted))
            {
                throw new AggregateException(InputTasks.Where(t => t.IsFaulted).Select(t => t.Exception));
            }


            var newTask = InputTasks.Any()
                              ? Task.Factory.ContinueWhenAll(InputTasks.ToArray(),
                                                             (tasks) =>
                                                                 {
                                                                     foreach (var kvp in incomingData)
                                                                     {
                                                                         kvp.Key.PushInputData(kvp.Value);
                                                                     }
                                                                 })
                              : Task.Factory.StartNew(() =>
                                                          {
                                                              foreach (var kvp in incomingData)
                                                              {
                                                                  kvp.Key.PushInputData(kvp.Value);
                                                              }
                                                          });



            InputTasks = InputTasks.Where(t => !t.IsCompleted).ToList();
            InputTasks.Add(newTask);
        }

        public void WaitForInputTasks()
        {
            Task.WaitAll(InputTasks.ToArray());
        }

        public void ApplyStreamBackground(IDAQOutputStream s)
        {
            if(s.DAQ != this)
            {
                throw new DAQException("Output stream does not use this DAQ controller.");
            }

            if(Running && !StopRequested)
            {
                throw new DAQException("Attempted to set background on running stream");
            }

            ApplyStreamBackgroundAsync(s, s.Background);
        }

        public abstract void ApplyStreamBackgroundAsync(IDAQOutputStream s, IMeasurement background);

        private void PushOutputDataEvents(DateTimeOffset outputTime, 
            IEnumerable<KeyValuePair<IDAQOutputStream, IOutputData>> outgoingData)
        {
            var exceptions = new ConcurrentQueue<Exception>();

            Parallel.ForEach(outgoingData,
                             kvp =>
                                 {
                                     try
                                     {
                                         var outputStream = kvp.Key;
                                         var data = (kvp.Value as IOutputData);

                                         if (data != null)
                                         {
                                             data = data.DataWithNodeConfiguration(this.Name, this.Configuration);

                                             Task.Factory.StartNew(
                                                 () => OnStimulusOutput(outputTime, outputStream, data));

                                             outputStream.DidOutputData(outputTime, data.Duration, data.Configuration);
                                         }
                                     }
                                     catch(Exception e)
                                     {
                                         exceptions.Enqueue(e);
                                     }
                                 });

            if (exceptions.Count() > 0) throw new AggregateException(exceptions);
        }

        private IEnumerable<KeyValuePair<IDAQOutputStream, Task<IOutputData>>> NextOutgoingData()
        {
            var outgoingData = ActiveOutputStreams.ToDictionary(
                s => s,
                s => Task.Factory.StartNew(
                    () => NextOutputDataForStream(s),
                    TaskCreationOptions.PreferFairness
                    )
                );

            return outgoingData;
        }

        protected IOutputData NextOutputDataForStream(IDAQOutputStream os)
        {
            return os.PullOutputData(ProcessInterval);
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(DAQControllerBase));

        /// <summary>
        /// Subclasses can override this to continue running, e.g. while input data remains in the DAQ hardware.
        /// </summary>
        /// <returns>true if the DAQController should stop (e.g. data exausted)</returns>
        protected virtual bool ShouldStop()
        {
            return (ActiveOutputStreamsWithData.Count() == 0 || StopRequested);
        }

        protected bool StopRequested { get; private set; }

        private void OnProcessIteration()
        {
            lock (eventLock)
            {
                var evt = ProcessIteration;
                if (evt != null)
                    evt(this, new TimeStampedEventArgs(Clock));
            }
        }

        private void OnStarted()
        {
            lock (eventLock)
            {
                var evt = Started;
                if (evt != null)
                    evt(this, new TimeStampedEventArgs(Clock));
            }
        }

        private void OnStimulusOutput(DateTimeOffset time, IDAQOutputStream stream, IIOData data)
        {
            lock(eventLock)
            {
                var evt = StimulusOutput;
                if(evt != null)
                    evt(this, new TimeStampedStimulusOutputEventArgs(time, stream, data));
            }
        }

        protected abstract IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit);

        /// <summary>
        /// Stops this DAQController. This method blocks until the DAQ hardware stops.
        /// </summary>
        /// <remarks>Calling Stop() on a stopped DAQController is a NOP</remarks>
        public virtual void Stop()
        {
            OnStopped();
            CommonStop();

            Running = false;
        }

        public event EventHandler<TimeStampedStimulusOutputEventArgs> StimulusOutput;

        /// <summary>
        /// Asynchronously stops the DAQ hardware. This method does not block.
        /// </summary>
        public void RequestStop()
        {
            StopRequested = true;
        }


        protected virtual void CommonStop()
        {
            //OutputTaskCTS.Cancel();
            RequestStop();
            SetStreamsBackground();
        }

        /// <summary>
        /// Asynchronously sets each active output stream to the constant value defined by the stream's
        /// associated ExternalDevice.
        /// 
        /// This method may be called after the output pipeline is configured. It will not have any effect
        /// until output streams are activated by connecting an ExternalDevice.
        /// </summary>
        public virtual void SetStreamsBackground()
        {
            foreach(var outputStream in ActiveOutputStreams)
            {
                ApplyStreamBackground(outputStream);
            }
        }

        protected virtual void StopWithException(Exception e)
        {
            Running = false;
            OnExceptionalStop(e);
            CommonStop();
        }

        private void OnStopped()
        {
            lock (eventLock)
            {
                var evt = Stopped;
                if (evt != null)
                    evt(this, new TimeStampedEventArgs(Clock));
            }
        }

        private void OnExceptionalStop(Exception e)
        {
            lock (eventLock)
            {
                log.ErrorFormat("DAQController.ExceptionalStop: {0}", e);
                var evt = ExceptionalStop;
                if (evt != null)
                    evt(this, new TimeStampedExceptionEventArgs(Clock, e));
            }
        }

        /// <summary>
        /// Find the streams identified by the name passed in.
        /// </summary>
        /// <param name="name">The stream name to find</param>
        /// <returns>A list of the DAQStream instances identified</returns>
        public IEnumerable<IDAQStream> GetStreams(string name)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException("name is null or empty.", "name");

            return DAQStreams.Where((s) => s.Name == name);
        }

        /// <summary>
        /// Gets the IDAQStream with the given name.
        /// </summary>
        /// <param name="name">The stream name to find</param>
        /// <returns>The IDAQStream with the given name or null if none exists</returns>
        /// <exception cref="InvalidOperationException">If more than one stream exists with the given name</exception>
        public IDAQStream GetStream(string name)
        {
            if (GetStreams(name).Count() > 1)
            {
                throw new InvalidOperationException("More than one stream with name: " + name);
            }

            return GetStreams(name).FirstOrDefault();
        }

        /// <summary>
        /// Gets the IDAQStream of a particular type with the given name.
        /// </summary>
        /// <typeparam name="T">IDAQStream type to find</typeparam>
        /// <param name="name">The stream name to find</param>
        /// <returns>The IDAQStream with the given name or null if none exists</returns>
        /// <exception cref="InvalidOperationException">If more than one stream of type T exists with the given name</exception>
        public T GetStream<T>(string name) where T : class, IDAQStream
        {
            var streams = StreamsOfType<T>().Where(s => s.Name == name);
            if (streams.Count() > 1)
                throw new InvalidOperationException("More than one stream with name: " + name);

            if (streams.Count() == 0)
                return null;

            return streams.First();
        }

        /// <summary>
        /// The DAQController has one DAQStream for each of its channels,
        /// and these are typically(?) identified by a unique channel number
        /// (which has no purpose other than as an identifier, so we model it
        /// as a string lest we start doing funny offset-games with the identifiers)
        /// </summary>
        public virtual IEnumerable<IDAQStream> Streams
        {
            get
            {
                return this.DAQStreams;
            }
        }

        protected IEnumerable<IDAQStream> ActiveStreams
        {
            get { return Streams.Where(s => s.Active); }
        }

        /// <summary>
        /// Subclass will need to add/remove streams from the Streams property. So we back this.Streams
        /// with an ISet, DAQStreams.
        /// </summary>
        protected ISet<IDAQStream> DAQStreams { get; private set; }


        public virtual IEnumerable<IDAQInputStream> InputStreams
        {
            get
            {
                return StreamsOfType<IDAQInputStream>().OrderBy(s => s.Name);
            }
        }

        private IEnumerable<T> StreamsOfType<T>() where T : IDAQStream
        {
            return Streams.OfType<T>();
        }

        public virtual IEnumerable<IDAQOutputStream> OutputStreams
        {
            get
            {
                return StreamsOfType<IDAQOutputStream>().OrderBy(s => s.Name);
            }
        }

        /// <summary>
        /// Series of name-value pairs used to configure the DAQController
        /// part of the rig.
        /// </summary>
        public virtual IDictionary<string, object> Configuration { get; protected set; }

        /// <summary>
        /// Performs any necessary setup for this DAQController. This should be
        /// called before Start().
        /// </summary>
        public virtual void BeginSetup()
        {
        }

        /// <summary>
        /// Fired when the ThreadedDAQController is started up successfully (meaning this event
        /// gets fired after the Thread has started)
        /// </summary>
        public virtual event EventHandler<TimeStampedEventArgs> Started;
        /// <summary>
        /// Fires when the ThreadedDAQController is told to stop (meaning this event is fired
        /// after the Thread has been successfully stopped, and is NOT fired if the thread is not
        /// successfully stopped despite the request to do so)
        /// </summary>
        public virtual event EventHandler<TimeStampedEventArgs> Stopped;
        /// <summary>
        /// Fires when the ThreadDAQController experiences an exception during the execution
        /// of the Thread (which dies due to the Exception reaching this far).
        /// </summary>
        public virtual event EventHandler<TimeStampedExceptionEventArgs> ExceptionalStop;
        /// <summary>
        /// Fires at the completion of each iteration of the Process loop (while Running).
        /// </summary>
        public virtual event EventHandler<TimeStampedEventArgs> ProcessIteration;
    }

    /// <summary>
    /// Exception indicating an exception or failure in the DAQ controller or associated
    /// A/D hardware.
    /// </summary>
    public class DAQException : Exception
    {
        public DAQException(string msg) : base(msg) { }
    }
}