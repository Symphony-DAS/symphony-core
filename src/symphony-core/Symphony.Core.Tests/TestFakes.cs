using System;
using System.Collections.Generic;
using System.Linq;

namespace Symphony.Core
{
    public class FakeEpochPersistor : EpochPersistor
    {
        public IEnumerable<Epoch> PersistedEpochs { get; private set; }
        public FakeEpochPersistor()
        {
            PersistedEpochs = new LinkedList<Epoch>();
        }

        public override void Serialize(Epoch e)
        {
            ((LinkedList<Epoch>)PersistedEpochs).AddLast(e);
        }
    }

    public class AggregateExceptionThrowingEpochPersistor : EpochPersistor
    {
        public override void Serialize(Epoch e)
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
    }


    public class FakeClock : IClock
    {
        public DateTimeOffset Now { get { return DateTimeOffset.Now; } }
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

        public override void ApplyStreamBackgroundAsync(IDAQOutputStream s, IMeasurement background)
        {
            throw new NotImplementedException();
        }

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit)
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
