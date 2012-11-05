using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

namespace Symphony.Core
{
    /// <summary>
    /// Interface for data elements in the Symphony input/output pipelines. Encapsulates
    /// a list of Measurement samples as well as structural information about the 
    /// conditions of that data's presentation or acquisition.
    /// </summary>
    public interface IIOData
    {

        /// <summary>
        /// The Data as a list of Measurement samples.
        /// </summary>
        IList<IMeasurement> Data { get; }

        /// <summary>
        /// The sample rate of this data. The Measurement is
        /// expected to be in Hz or some related unit.
        /// </summary>
        IMeasurement SampleRate { get; }

        /// <summary>
        /// Dictionary of IPipelineNodeConfiguration descriptors for this data. Each entry describes
        /// the pipeline node configurations for a node within this data's pipeline.
        /// </summary>
        IEnumerable<IPipelineNodeConfiguration> Configuration { get; }

        IPipelineNodeConfiguration NodeConfigurationWithName(string name);

        /// <summary>
        /// The duration of this data. (samples/sampleRate);
        /// </summary>
        TimeSpan Duration { get; }

        bool HasNodeConfiguration(string name);
    }

    public interface IConfigurationSpan
    {
        TimeSpan Time { get; }
        IEnumerable<IPipelineNodeConfiguration> Nodes { get; }
    }

    public interface IPipelineNodeConfiguration
    {
        string Name { get; }
        IDictionary<string, object> Configuration { get; }
    }

    internal class ConfigurationSpan : IConfigurationSpan
    {
        public TimeSpan Time { get; private set; }
        public IEnumerable<IPipelineNodeConfiguration> Nodes { get; private set; }


        public ConfigurationSpan(TimeSpan time,
            IEnumerable<IPipelineNodeConfiguration> nodes)
        {
            Time = time;
            Nodes = nodes;
        }
    }

    public class PipelineNodeConfiguration : IPipelineNodeConfiguration
    {
        public string Name { get; private set; }
        public IDictionary<string, object> Configuration { get; private set; }

        public PipelineNodeConfiguration(string name,
            IDictionary<string,object> config)
        {
            Name = name;
            Configuration = config;
        }
    }

    /// <summary>
    /// A LISP-style cons that splits a single IIOData into
    /// head and rest components. 
    /// </summary>
    /// <typeparam name="T">IIOData type</typeparam>
    public class DataSplit<T> : Tuple<T, T> where T : IIOData
    {
        internal DataSplit(T head, T rest) : base(head, rest) { }

        /// <summary>
        /// The initial segment of the IIOData
        /// </summary>
        public T Head
        {
            get
            {
                return Item1;
            }
        }

        /// <summary>
        /// The rest of the IIOData (i.e. after Head)
        /// </summary>
        public T Rest
        {
            get
            {
                return Item2;
            }
        }
    }

    /// <summary>
    /// A LISP-style cons that splits a single IOutputData into
    /// head and rest components.
    /// </summary>
    public class OutputDataSplit : DataSplit<IOutputData>
    {
        internal OutputDataSplit(IOutputData head, IOutputData rest) : base(head, rest) { }
    }

    /// <summary>
    /// A LISP-style cons that splits a single IInputData into
    /// head and rest components.
    /// </summary>
    public class InputDataSplit : DataSplit<IInputData>
    {
        internal InputDataSplit(IInputData head, IInputData rest) : base(head, rest) { }
    }

    /// <summary>
    /// Exception indicating calling code attempted to add a configuration (ExternalDevice or Stream)
    /// to an IIOData that already had a configuration for that property.
    ///  </summary>
    public class ExistingConfigurationException : SymphonyException
    {
        public ExistingConfigurationException(string msg) : base(msg) { }
    }


    /// <summary>
    /// Interface for output data (i.e. from the CPU to the external world; stimulus)
    /// </summary>
    public interface IOutputData : IIOData, ICloneable
    {
        /// <summary>
        /// Gives the time that data went to physical wire (out of DAQ). Raises InvalidOperationException if accessed before being set.
        /// </summary>
        /// <see cref="HasOutputTime"/>
        DateTimeOffset OutputTime { get; set; }

        /// <summary>
        /// Indicates if this IIOutputData has an OutputTime (i.e. has gone out the wire).
        /// </summary>
        bool HasOutputTime { get; }

        /// <summary>
        /// IOutputData may represent many seconds or minutes of data for output. However, the output pipeline
        /// may want to deal with that output data in smaller segments. This method splits an IOutputData into
        /// a tuple (head,rest) where head is an IOutputData of the given duration and rest is an IOutputData
        /// of consiting of the rest of the current data.
        /// </summary>
        /// <param name="duration">Desired duration for head data</param>
        /// <returns>OutputDataSplit containing Head and Rest data elements</returns>
        OutputDataSplit SplitData(TimeSpan duration);

        /// <summary>
        /// Convert this IIOData to a new physical unit. An appropriate conversion proc (i.e. from m.BaseUnit to the desired unit) must exist for each Measurment in this.Data.
        /// </summary>
        /// <param name="unit">Unit to convert to</param>
        /// <returns>A new IIOData with converted units.</returns>
        IOutputData DataWithUnits(string unit);

        /// <summary>
        /// Convert this IIOData with a conversion function applied to each Measurement in this.Data.
        /// </summary>
        /// <param name="conversion">Conversion proc Measurement => Measurement</param>
        /// <returns>A new IIOData with converted measurements</returns>
        IOutputData DataWithConversion(Func<IMeasurement, IMeasurement> conversion);

        /// <summary>
        /// Creates a new IOutputData from this IOutputData, adding given ExternalDeviceConfiguration.
        /// </summary>
        /// <param name="device">ExternalDevice for node</param>
        /// <param name="config">ExternalDevice configuration dictionary</param>
        /// <returns>New IInputData with given ExternalDevice configuration</returns>
        IOutputData DataWithExternalDeviceConfiguration(IExternalDevice device, IDictionary<string, object> config);

        /// <summary>
        /// Creates a new IOutputData from this IOutputData, adding given StreamConfiguration.
        /// </summary>
        /// <param name="stream">IDAQStream for node</param>
        /// <param name="config">IDAQStream configuration dictionary</param>
        /// <returns>New IInputData with given IDAQStream configuration</returns>
        IOutputData DataWithStreamConfiguration(IDAQStream stream, IDictionary<string, object> config);


        /// <summary>
        /// Creates a new IOutputData from this IOutputData, adding given node configuraiton.
        /// </summary>
        /// <param name="nodeName">Node name</param>
        /// <param name="config">Configuration of the node</param>
        /// <returns></returns>
        IOutputData DataWithNodeConfiguration(string nodeName, IDictionary<string, object> config);

        /// <summary>
        /// Client flag to indicate whether this IOutputData is the last for a given stream.
        /// </summary>
        bool IsLast { get; }


        /// <summary>
        /// Returns a new IOutputData which is the concatenation of this data and the supplied IOutputData.
        /// </summary>
        /// <param name="o">Data to concatenate</param>
        /// <returns>A new IOuputData instance which is the concatenation {this,o}</returns>
        /// <exception cref="ArgumentException">If either this or o has an existing device configuration</exception>
        IOutputData Concat(IOutputData o);
    }


    /// <summary>
    /// Interface for input data (i.e. from the external world to the CPU; response).
    /// 
    /// Includes a timestamp (InputTime) when the first sample was received at the hardware interface
    /// according to the connonical clock.
    /// </summary>
    public interface IInputData : IIOData
    {
        /// <summary>
        /// Time first sample of this IInputData came off the DAQ wire, according to the connoical pipeline clock.
        /// </summary>
        DateTimeOffset InputTime { get; }

        /// <summary>
        /// Convert this IIOData to a new physical unit. An appropriate conversion proc (i.e. from m.Unit to the desired unit) must exist for each Measurment in this.Data.
        /// </summary>
        /// <param name="unit">Unit to convert to</param>
        /// <returns>A new IIOData with converted units.</returns>
        IInputData DataWithUnits(string unit);

        /// <summary>
        /// Convert this IIOData with a conversion function applied to each Measurement in this.Data.
        /// </summary>
        /// <param name="conversion">Conversion proc Measurement => Measurement</param>
        /// <returns>A new IIOData with converted measurements</returns>
        IInputData DataWithConversion(Func<IMeasurement, IMeasurement> conversion);

        /// <summary>
        /// Creates a new IInputData from this IInputData, adding given ExternalDeviceConfiguration.
        /// </summary>
        /// <param name="device">ExternalDevice for node</param>
        /// <param name="config">ExternalDevice configuration dictionary</param>
        /// <returns>New IInputData with given ExternalDevice configuration</returns>
        IInputData DataWithExternalDeviceConfiguration(IExternalDevice device, IDictionary<string, object> config);

        /// <summary>
        /// Creates a new IInputData from this IInputData, adding given StreamConfiguration.
        /// </summary>
        /// <param name="stream">IDAQStream for node</param>
        /// <param name="config">IDAQStream configuration dictionary</param>
        /// <returns>New IInputData with given IDAQStream configuration</returns>
        IInputData DataWithStreamConfiguration(IDAQStream stream, IDictionary<string, object> config);


        /// <summary>
        /// Splits this IInputData at the given duration.
        /// </summary>
        /// <remarks>The resulting cons.Rest may be empty if duration is greater than or equal 
        /// to the duration of this IInputData.</remarks>
        /// <param name="duration">Duration of the resulting cons.Head</param>
        /// <returns>InputDataSplit splitting this input data at duration.</returns>
        InputDataSplit SplitData(TimeSpan duration);
    }

    /// <summary>
    /// Abstract base class implementation of IIOData.
    /// </summary>
    public abstract class IOData : IIOData
    {
        public const string EXTERNAL_DEVICE_CONFIGURATION_NAME = "EXTERNAL_DEVICE";
        public const string STREAM_CONFIGURATION_NAME = "STREAM";
        public const string HARWARE_CONTROLLER_CONFIGURATION_NAME = "HARDWARE_CONTROLLER";

        public IList<IMeasurement> Data { get; private set; }
        public IMeasurement SampleRate { get; private set; }

        public IEnumerable<IPipelineNodeConfiguration> Configuration { get; private set; }

        protected DateTimeOffset Time { get; set; }


        protected IEnumerable<IPipelineNodeConfiguration> NodeConfigurations(string name)
        {
            return Configuration.Where(config => config.Name == name);
        }

        public IPipelineNodeConfiguration NodeConfigurationWithName(string name)
        {
            if(NodeConfigurations(name).Count() > 1)
                throw new ExistingConfigurationException("More than one configuration exists for " + name);

            return NodeConfigurations(name).FirstOrDefault();
        }

        public TimeSpan Duration
        {
            get
            {
                return TimeSpanExtensions.FromSamples((uint)Data.Count, SampleRate);
            }
        }

        public bool HasNodeConfiguration(string name)
        {
            return NodeConfigurations(name).Count() > 0;
        }


        /// <summary>
        /// Constructor for an IOData derived from an existing IOData
        /// </summary>
        /// <param name="baseData"></param>
        /// <param name="derivedData"></param>
        protected IOData(IIOData baseData,
            IEnumerable<IMeasurement> derivedData)
            : this(derivedData, baseData.SampleRate, baseData.Configuration)
        {
        }

        protected IOData(IIOData baseData)
            : this(baseData.Data, baseData.SampleRate, baseData.Configuration)
        {
        }


        protected IOData(IEnumerable<IMeasurement> data,
            IMeasurement sampleRate,
            IEnumerable<IPipelineNodeConfiguration> configuration
            )
        {
            this.Data = new List<IMeasurement>(data);
            this.SampleRate = sampleRate;
            this.Configuration = configuration ?? new List<IPipelineNodeConfiguration>();
        }

        protected IOData(IIOData baseData,
            IPipelineNodeConfiguration appendedConfiguration)
            : this(baseData)
        {
            if(baseData.HasNodeConfiguration(appendedConfiguration.Name))
            {
                throw new ExistingConfigurationException("Configuration already exists for " + appendedConfiguration.Name);
            }

            this.Configuration =
                baseData.Configuration.Concat(new List<IPipelineNodeConfiguration> {appendedConfiguration});
        }


        protected IOData(IEnumerable<IMeasurement> data,
            IMeasurement sampleRate)
            : this(data,
            sampleRate,
            null
            )
        {
        }
    }


    public class OutputData : IOData, IOutputData
    {
        // Option<T> is a combination of "does it exist" and "what's its value";
        // see Misc.cs for the API
        private Option<DateTimeOffset> outputTime =
            Option<DateTimeOffset>.None();

        public OutputData(IEnumerable<IMeasurement> data,
            IMeasurement sampleRate,
            bool isLast) :
            base(data, sampleRate)
        {
            IsLast = isLast;
        }

        public OutputData(IEnumerable<IMeasurement> data,
            IMeasurement sampleRate) :
            this(data, sampleRate, true)
        {
        }


        protected OutputData(IOutputData data,
            IPipelineNodeConfiguration config)
            : base(data, config)
        {
            IsLast = data.IsLast;
        }

        public OutputData(IOutputData baseData,
                    IEnumerable<IMeasurement> derivedData) :
            base(baseData, derivedData)
        {
            if (baseData.HasOutputTime)
            {
                OutputTime = baseData.OutputTime;
            }

            IsLast = baseData.IsLast;
        }

        public OutputData(IOutputData baseData)
            : base(baseData)
        {
        }

        public OutputData(IOutputData baseData, bool isLast)
            : this(baseData)
        {
            IsLast = isLast;
        }

        public DateTimeOffset OutputTime
        {
            get
            {
                if (outputTime)
                    return outputTime.Item2;

                throw new InvalidOperationException();
            }
            set
            {
                outputTime = Option<DateTimeOffset>.Some(value);
            }
        }

        public bool HasOutputTime
        {
            get
            {
                return outputTime.IsSome();
            }
        }

        public bool IsLast { get; private set; }

        /// <summary>
        /// Returns a new IOutputData which is the concatenation of this data and the supplied IOutputData.
        /// </summary>
        /// <param name="o">Data to concatenate</param>
        /// <returns>A new IOuputData instance which is the concatenation {this,o}</returns>
        /// <exception cref="ExistingConfigurationException">If either this or o has an existing device configuration</exception>
        public IOutputData Concat(IOutputData o)
        {
            if (this.Configuration.Count() > 0 || o.Configuration.Count() > 0)
                throw new ArgumentException("Cannot concatenate OutputData with existing node configurations.");

            
            if (!this.SampleRate.Equals(o.SampleRate))
                throw new ArgumentException("Sample rate mismatch");

            return new OutputData(this.Data.Concat(o.Data).ToList(), this.SampleRate, this.IsLast || o.IsLast);
        }

        public IOutputData OutputDataForRange(int startSample, int numSamples)
        {
            if (startSample <= 0 || numSamples < 0 || startSample + numSamples > Data.Count)
            {
                throw new Exception("Range out-of-bounds.");
            }

            var rangeData = this.Data.Skip(startSample).Take(numSamples);
            IList<IMeasurement> subData = new List<IMeasurement>(rangeData);

            return new OutputData(this, subData);
        }

        public IOutputData DataWithUnits(string unit)
        {
            return DataWithConversion((m) => Converters.Convert(m, unit));
        }

        public IOutputData DataWithConversion(Func<IMeasurement, IMeasurement> conversion)
        {
            return new OutputData(this, Data.Select(conversion).ToList());
        }

        public OutputDataSplit SplitData(TimeSpan duration)
        {
            int requestedSamples = duration.Ticks == 0 ? 0 : (int)Math.Ceiling(duration.TotalSeconds * (double)SampleRate.QuantityInBaseUnit);
            int numSamples = Math.Min(requestedSamples, Data.Count);

            var headData = Data.Take(numSamples).ToList();
            var restData = Data.Skip(numSamples).Take(Data.Count() - numSamples).ToList();

            return new OutputDataSplit(
                new OutputData(this, headData),
                new OutputData(this, restData)
            );
        }

        public IOutputData DataWithNodeConfiguration(string nodeName, IDictionary<string,object> config)
        {
            return new OutputData(this, new PipelineNodeConfiguration(nodeName, config));
        }

        public IOutputData DataWithExternalDeviceConfiguration(IExternalDevice dev, IDictionary<string, object> config)
        {
            return DataWithNodeConfiguration(dev.Name, config);
        }

        public IOutputData DataWithStreamConfiguration(IDAQStream stream, IDictionary<string, object> config)
        {
            return DataWithNodeConfiguration(stream.Name, config);
        }

        public object Clone()
        {
            return new OutputData(this, this.Data);
        }
    }

    /// <summary>
    /// IInputData implementation for the Symphony input/output pipelines
    /// </summary>
    public class InputData : IOData, IInputData
    {
        public DateTimeOffset InputTime { get; private set; }

        public InputData(IEnumerable<IMeasurement> data,
            IMeasurement sampleRate,
            DateTimeOffset time,
            IPipelineNodeConfiguration config)
            : base(data, sampleRate, new List<IPipelineNodeConfiguration> { config } )
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            this.InputTime = time;
        }

        protected InputData(IInputData data,
            IPipelineNodeConfiguration config)
            : base(data, config)
        {
            this.InputTime = data.InputTime;
        }

        public InputData(IEnumerable<IMeasurement> data,
            IMeasurement sampleRate,
            DateTimeOffset time)
            : base(data, sampleRate)
        {
            this.InputTime = time;
        }

        public InputData(IInputData baseData,
            IEnumerable<IMeasurement> derivedData)
            : base(baseData, derivedData)
        {
            this.InputTime = baseData.InputTime;
        }


        public IInputData DataWithUnits(string unit)
        {
            return DataWithConversion(m => Converters.Convert(m, unit));
        }

        public IInputData DataWithConversion(Func<IMeasurement, IMeasurement> conversion)
        {
            return new InputData(this, Data.Select(conversion).ToList());
        }

        public InputDataSplit SplitData(TimeSpan duration)
        {
            int numSamples = duration.Ticks == 0 ? 0 : (int)Math.Ceiling(duration.TotalSeconds * (double)SampleRate.QuantityInBaseUnit);
            numSamples = Math.Min(numSamples, Data.Count);

            var headData = Data.Take(numSamples).ToList();
            var restData = Data.Skip(numSamples).Take(Data.Count() - numSamples).ToList();

            return new InputDataSplit(
                new InputData(this, headData),
                new InputData(this, restData)
            );
        }

        public IInputData DataWithNodeConfiguration(string nodeName, IDictionary<string, object> config)
        {
            return new InputData(this, new PipelineNodeConfiguration(nodeName, config));
        }

        public IInputData DataWithExternalDeviceConfiguration(IExternalDevice dev, IDictionary<string, object> config)
        {
            return DataWithNodeConfiguration(dev.Name, config);
        }

        public IInputData DataWithStreamConfiguration(IDAQStream stream, IDictionary<string, object> config)
        {
            return DataWithNodeConfiguration(stream.Name, config);
        }


    }
}
