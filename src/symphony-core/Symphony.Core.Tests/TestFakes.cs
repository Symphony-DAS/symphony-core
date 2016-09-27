using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Symphony.Core
{
    public class FakeEpochPersistor : IEpochPersistor
    {
        public IEnumerable<Epoch> PersistedEpochs { get; private set; }

        public FakeEpochPersistor()
        {
            PersistedEpochs = new LinkedList<Epoch>();
        }

        public virtual IPersistentEpoch Serialize(Epoch e)
        {
            ((LinkedList<Epoch>)PersistedEpochs).AddLast(e);
            return null;
        }

        public void Close()
        {
        }

        public bool IsClosed { get; private set; }

        public IPersistentExperiment Experiment { get; private set; }

        public IPersistentDevice AddDevice(string name, string manufacturer)
        {
            return null;
        }

        public IPersistentDevice Device(string name, string manufacturer)
        {
            return new FakePersistentDevice(name, manufacturer);
        }

        public IPersistentSource AddSource(string label, IPersistentSource parent)
        {
            return null;
        }

        public IPersistentEpochGroup BeginEpochGroup(string label, IPersistentSource source)
        {
            return null;
        }

        public IPersistentEpochGroup EndEpochGroup()
        {
            return null;
        }

        public IPersistentEpochGroup SplitEpochGroup(IPersistentEpochGroup group, IPersistentEpochBlock block)
        {
            return null;
        }

        public IPersistentEpochGroup MergeEpochGroups(IPersistentEpochGroup group1, IPersistentEpochGroup group2)
        {
            return null;
        }

        public IPersistentEpochGroup CurrentEpochGroup { get; private set; }

        public IPersistentEpochBlock BeginEpochBlock(string protocolID, IDictionary<string, object> parameters)
        {
            return null;
        }

        public IPersistentEpochBlock EndEpochBlock()
        {
            return null;
        }

        public IPersistentEpochBlock CurrentEpochBlock { get; private set; }

        public void Delete(IPersistentEntity entity)
        {
        }
    }

    public class FakePersistentDevice : IPersistentDevice
    {
        public FakePersistentDevice(string name, string manufacturer)
        {
            Name = name;
            Manufacturer = manufacturer;
        }

        public Guid UUID { get; private set; }
        public IEnumerable<KeyValuePair<string, object>> Properties { get; private set; }
        public void AddProperty(string key, object value)
        {
            throw new NotImplementedException();
        }

        public bool RemoveProperty(string key)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> Keywords { get; private set; }
        public bool AddKeyword(string keyword)
        {
            throw new NotImplementedException();
        }

        public bool RemoveKeyword(string keyword)
        {
            throw new NotImplementedException();
        }

        public IPersistentResource AddResource(string uti, string name, byte[] data)
        {
            throw new NotImplementedException();
        }

        public bool RemoveResource(string name)
        {
            throw new NotImplementedException();
        }

        public IPersistentResource GetResource(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetResourceNames()
        {
            return Enumerable.Empty<string>();
        }

        public IEnumerable<IPersistentNote> Notes { get; private set; }
        public IPersistentNote AddNote(DateTimeOffset time, string text)
        {
            throw new NotImplementedException();
        }

        public string Name { get; private set; }
        public string Manufacturer { get; private set; }
        public IPersistentExperiment Experiment { get; private set; }
    }

    public class AggregateExceptionThrowingEpochPersistor : FakeEpochPersistor
    {
        public override IPersistentEpoch Serialize(Epoch e)
        {
            throw new AggregateException();
        }
    }

    public class TestDevice : UnitConvertingExternalDevice
    {
        public IDictionary<IDAQOutputStream, Queue<IOutputData>> OutputData { get; private set; }
        public IDictionary<IDAQInputStream, IList<IInputData>> InputData { get; private set; }

        public TestDevice()
            : this("", new Dictionary<IDAQOutputStream, Queue<IOutputData>>())
        {
        }

        public TestDevice(Controller c)
            : base("", "manufacturer", c, new Measurement(0, "V"))
        {
            
        }

        public TestDevice(string name, IDictionary<IDAQOutputStream, Queue<IOutputData>> outData)
            : base(name, "manufactuer", new Measurement(0, "V"))
        {
            this.OutputData = outData;
            this.InputData = new Dictionary<IDAQInputStream, IList<IInputData>>();
        }

        public override IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration)
        {
            if (this.OutputData[stream].Count > 0)
            {
                return OutputData[stream].Dequeue().SplitData(duration).Head;
            }
            else
            {
                return new OutputData(new List<IMeasurement>(), stream.SampleRate, true);
            }
        }

        public override void PushInputData(IDAQInputStream stream, IInputData inData)
        {
            if (!InputData.ContainsKey(stream))
            {
                InputData[stream] = new List<IInputData>();
            }

            InputData[stream].Add(inData);
        }

        public override void DidOutputData(IDAQOutputStream stream, DateTimeOffset outputTime, TimeSpan duration, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            if (Controller != null)
            {
                base.DidOutputData(stream, outputTime, duration, configuration);
            }
        }
    }


    public class FakeClock : IClock
    {
        public DateTimeOffset Now { get { return DateTimeOffset.Now; } }
    }

    public class IncrementingClock : IClock
    {
        private DateTimeOffset _time;
        
        public IncrementingClock()
        {
            _time = DateTimeOffset.MinValue;
        }

        public DateTimeOffset Now
        {
            get
            {
                _time = _time.AddTicks(1);
                return _time;
            }
        }
    }

    /// <summary>
    /// This DAQController fake provides a rudimentary loop-back style controller.
    /// Test setup must explictly create stream mappings by calling AddStreamMapping().
    /// 
    /// Each iteration of the process loop pulls PollInterval duration of samples from each input
    /// stream in the stream mapping and pushes data to the associated output stream.
    /// 
    /// For example, a stream mapping of
    /// {outStream => inStream}
    /// will push data from outStream to inStream, of duration PollInterval, on each iteration of the process loop.
    /// </summary>
    public class TestDAQController : DAQControllerBase, IClock, IMutableDAQController
    {
        IDictionary<IDAQOutputStream, IDAQInputStream> StreamMapping { get; set; }

        public TestDAQController()
            : this(new TimeSpan(0, 0, 1)) // 1 second
        {
        }
        public TestDAQController(TimeSpan pollInterval)
        {
            StreamMapping = new Dictionary<IDAQOutputStream, IDAQInputStream>();
            ProcessInterval = pollInterval;
            Clock = this;
        }

        public void AddStreamMapping(IDAQOutputStream outStream, IDAQInputStream inStream)
        {
            StreamMapping[outStream] = inStream;
        }

        public void RemoveStreamMapping(IDAQOutputStream outStream)
        {
            StreamMapping.Remove(outStream);
        }

        protected override void StartHardware(bool wait)
        {
            //no op
        }

        protected override bool ShouldStop()
        {
            return IsStopRequested;
        }

        public override IInputData ReadStreamAsync(IDAQInputStream s)
        {
            throw new NotImplementedException();
        }

        public override void ApplyStreamBackgroundAsync(IDAQOutputStream s, IMeasurement background)
        {
            throw new NotImplementedException();
        }

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit, CancellationToken token)
        {
            var result = new Dictionary<IDAQInputStream, IInputData>();

            foreach (var kvp in outData)
            {
                result[StreamMapping[kvp.Key]] = new InputData(kvp.Value.Data,
                                                               kvp.Value.SampleRate,
                                                               DateTimeOffset.Now);
            }

            return result;
        }

        public override void SetStreamsBackground()
        {
            //pass
        }

        public override IEnumerable<IDAQStream> Streams
        {
            get
            {
                return StreamMapping.Keys
                    .Cast<IDAQStream>()
                    .Concat(StreamMapping.Values
                                        .Cast<IDAQStream>()
                            );
            }
        }

        public void AddStream(IDAQStream stream)
        {
            DAQStreams.Add(stream);
        }

        public DateTimeOffset Now
        {
            get { return DateTimeOffset.Now; }
        }

    }
}
