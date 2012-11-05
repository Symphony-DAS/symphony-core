using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using log4net;
using Symphony.Core;

namespace Symphony.Core
{
    /// <summary>
    /// Indicates an exception in an ExternalDevice instance.
    /// </summary>
    public class ExternalDeviceException : SymphonyException
    {
        public ExternalDeviceException(string message)
            : base(message)
        {
        }

        public ExternalDeviceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// IExternalDevice represents a single physical external hardware device
    /// in the Symphony input/output pipeline(s).
    /// 
    /// <para>In a typical physiology rig, each amplifier channel, any stimulation
    /// devices such as an LED, valve, etc., would be represented by a distinct
    /// ExternalDevice within the Symphony.Core pipelines.
    /// </para>
    /// 
    /// <para>Each ExternalDevice has an associated Controller instance. Input data is pushed
    /// to the Controller from the device whereupon the Controller will append that data
    /// to the relevatn Response of the current Epoch.
    /// Similarly, the device pulls data for output from the Controller
    /// which will in turn pull from the appropriate Stimulus for the current Epoch.</para>
    /// 
    /// <para>Each ExternalDevice may have one or more (named) input streams. Data
    /// from these input streams are pushed to the device for interpretation according
    /// to the semantics of the represented device. Similarly each ExternalDevice may have
    /// a one or more associated output streams. These streams pull data from this
    /// ExternalDevice for output to the preparation.</para>
    /// 
    /// <para>If a device has any associated output streams, it must have a valid Backgroud Measurement.
    /// This value is applied to the streams when the associated DAQController is stopped.</para>
    /// </summary>
    public interface IExternalDevice : ITimelineProducer
    {
        /// <summary>
        /// Mapping from device's proprietary stream name to IDAQStreams. Result should be considered immutable.
        /// 
        /// Use BindStream and UnbindStream to modify the collection.
        /// </summary>
        IDictionary<string, IDAQStream> Streams { get; }

        /// <summary>
        /// The value, in device  units, that should
        /// be applied to any IOutputStreams bound to this device when stopped.
        /// </summary>
        IMeasurement Background { get; set; }

        /// <summary>
        /// The current Background, in output units.
        /// </summary>
        IMeasurement OutputBackground { get; }

        /// <summary>
        /// The name of this external device, principally for human-recognition
        /// purposes (log files, etc)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Manufacturer of this device
        /// </summary>
        string Manufacturer
        {
            get;
        }

        /// <summary>
        /// The Controller we're connected up to.
        /// </summary>
        Controller Controller { get; set; }

        /// <summary>
        /// What are the parameters of this device's configuration (i.e. a representation of the hardware settings of the device).
        /// May also include information about conversion proc(s) parameters.
        /// </summary>
        IDictionary<string, object> Configuration { get; }

        /// <summary>
        /// Binds an IDAQInputStream to this device. Adds this device to the stream's
        /// Devices list.
        /// </summary>
        /// <param name="inputStream">Stream to bind</param>
        ExternalDeviceBase BindStream(IDAQInputStream inputStream);

        /// <summary>
        /// Associate an IDAQInputStream with this ExternalDevice with the given name.
        /// Adds this device to the stream's
        /// Devices list.
        /// </summary>
        /// <param name="name">Associatino name</param>
        /// <param name="inputStream">Stream to bind</param>
        ExternalDeviceBase BindStream(string name, IDAQInputStream inputStream);

        /// <summary>
        /// Associate an IDAQOutputStream with this ExternalDevice. Sets this device as the stream's
        /// output Device.
        /// </summary>
        /// <param name="outputStream">Stream to bind</param>
        ExternalDeviceBase BindStream(IDAQOutputStream outputStream);

        /// <summary>
        /// Associate an IDAQOutputStream with this device using the given association name.  Sets this device as the stream's
        /// output Device.
        /// </summary>
        /// <param name="name">Association name</param>
        /// <param name="outputStream">Stream to bind</param>
        ExternalDeviceBase BindStream(string name, IDAQOutputStream outputStream);

        /// <summary>
        /// Remove the Stream associated with this ExternalDevice
        /// </summary>
        /// <param name="name"></param>
        void UnbindStream(string name);

        /// <summary>
        /// Pulls data for output to the given IDAQStream. Default implementation pulls data from
        /// this Device's Controller.
        /// </summary>
        /// <remarks>Appends this Device's Configuration to the IOutputData</remarks>
        /// <param name="stream">Stream for output</param>
        /// <param name="duration">Requested duration</param>
        /// <returns>IOutputData of duration less than or equal to duration</returns>
        /// <exception cref="ExternalDeviceException">Requested duration is less than one sample</exception>
        IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration);

        /// <summary>
        /// Pushes input data to this Device's controller.
        /// </summary>
        /// <param name="stream">Stream supplying the input data</param>
        /// <param name="inData">IInputData to push to the controller</param>
        void PushInputData(IDAQInputStream stream, IInputData inData);

        /// <summary>
        /// Called by bound streams to indicate that output data was pushed "to the wire" (e.g. out the D/A converter of a DAQ hardware device)
        /// </summary>
        /// <param name="stream">Stream that sent the outputdata</param>
        /// <param name="outputTime">Estimated time that the data reached the wire</param>
        /// <param name="units">Output data units</param>
        /// <param name="duration">Configuration span duration (output data duration)</param>
        /// <param name="configuration">Output data configuration</param>
        void DidOutputData(IDAQOutputStream stream, DateTimeOffset outputTime, TimeSpan duration,
                           IEnumerable<IPipelineNodeConfiguration> configuration);

        /// <summary>
        /// Verify that everything is hooked up correctly
        /// </summary>
        /// <returns></returns>
        Maybe<string> Validate();
    }

    /// <summary>
    /// ExternalDevice represents a single physical external hardware device
    /// in the Symphony input/output pipeline(s).
    /// 
    /// <para>In a typical physiology rig, each amplifier channel, any stimulation
    /// devices such as an LED, valve, etc., would be represented by a distinct
    /// ExternalDevice within the Symphony.Core pipelines.
    /// </para>
    /// 
    /// <para>Each ExternalDevice has an associated Controller instance. Input data is pushed
    /// to the Controller from the device whereupon the Controller will append that data
    /// to the relevatn Response of the current Epoch.
    /// Similarly, the device pulls data for output from the Controller
    /// which will in turn pull from the appropriate Stimulus for the current Epoch.</para>
    /// 
    /// <para>Each ExternalDevice may have one or more (named) input streams. Data
    /// from these input streams are pushed to the device for interpretation according
    /// to the semantics of the represented device. Similarly each ExternalDevice may have
    /// a one or more associated output streams. These streams pull data from this
    /// ExternalDevice for output to the preparation.</para>
    /// 
    /// <para>If a device has any associated output streams, it must have a valid Backgroud Measurement.
    /// This value is applied to the streams when the associated DAQController is stopped.</para>
    /// </summary>
    public abstract class ExternalDeviceBase : IExternalDevice
    {
        public IClock Clock { get; set; }

        /// <summary>
        /// Constructs a new ExternalDevice instance with the given name and background.
        /// </summary>
        /// <param name="name">Device name</param>
        /// <param name="manufacturer">Device manufacturer</param>
        /// <param name="background">Device's default output (background) value</param>
        protected ExternalDeviceBase(string name, string manufacturer, Measurement background)
            : this(name, manufacturer, null, background)
        {
        }

        /// <summary>
        /// Constructs an ExternalDevice referencing a Controller
        /// </summary>
        /// <param name="name">Device name</param>
        /// <param name="manufacturer">Device manufacturer</param>
        /// <param name="c">Controller to connect with this Device</param>
        /// <param name="background">Device's default output (background) value</param>
        protected ExternalDeviceBase(string name, string manufacturer, Controller c, Measurement background)
            : this(name, manufacturer, c)
        {
            Background = background;
        }

        protected ExternalDeviceBase(string name, string manufacturer, Controller c)
        {
            this.Name = name;
            this.Manufacturer = manufacturer;
            this.Streams = new Dictionary<string, IDAQStream>();
            Configuration = new Dictionary<string, object>();


            if (c != null)
            {
                Controller = c;
                Controller.AddDevice(this);
            }
        }

        /// <summary>
        /// Mapping from device's proprietary stream name to IDAQStreams. Result should be considered immutable.
        /// 
        /// Use BindStream and UnbindStream to modify the collection.
        /// </summary>
        public virtual IDictionary<string, IDAQStream> Streams { get; private set; }

        /// <summary>
        /// The value, in MeasurementConversionTarget input units, that should
        /// be applied to any IOutputStreams bound to this device when stopped.
        /// </summary>
        public virtual IMeasurement Background { get; set; }

        public virtual IMeasurement OutputBackground
        {
            get { return ConvertOutput(Background); }
        }

        protected virtual IMeasurement ConvertOutput(IMeasurement deviceOutput)
        {
            return deviceOutput;
        }

        /// <summary>
        /// Binds an IDAQInputStream to this device. Adds this device to the stream's
        /// Devices list.
        /// </summary>
        /// <param name="inputStream">Stream to bind</param>
        public ExternalDeviceBase BindStream(IDAQInputStream inputStream)
        {
            Contract.Assert(inputStream != null, "inputStream is null");
            return BindStream(inputStream.Name, inputStream);
        }
        /// <summary>
        /// Associate an IDAQInputStream with this ExternalDevice with the given name.
        /// Adds this device to the stream's
        /// Devices list.
        /// </summary>
        /// <param name="name">Associatino name</param>
        /// <param name="inputStream">Stream to bind</param>
        public ExternalDeviceBase BindStream(string name, IDAQInputStream inputStream)
        {
            Contract.Assert(inputStream != null, "inputStream is null");
            Contract.Assert(name != null && name.Length > 0, "name is null or empty");

            inputStream.Devices.Add(this);
            this.Streams[name] = inputStream;
            return this;
        }
        /// <summary>
        /// Associate an IDAQOutputStream with this ExternalDevice. Sets this device as the stream's
        /// output Device.
        /// </summary>
        /// <param name="outputStream">Stream to bind</param>
        public ExternalDeviceBase BindStream(IDAQOutputStream outputStream)
        {
            Contract.Assert(outputStream != null, "outputStream is null");

            return BindStream(outputStream.Name, outputStream);
        }
        /// <summary>
        /// Associate an IDAQOutputStream with this device using the given association name.  Sets this device as the stream's
        /// output Device.
        /// </summary>
        /// <param name="name">Association name</param>
        /// <param name="outputStream">Stream to bind</param>
        public ExternalDeviceBase BindStream(string name, IDAQOutputStream outputStream)
        {
            Contract.Assert(outputStream != null, "outputStream is null");
            Contract.Assert(name != null && name.Length > 0, "name is null or empty");

            outputStream.Device = this;
            this.Streams[name] = outputStream;
            return this;
        }
        /// <summary>
        /// Remove the Stream associated with this ExternalDevice
        /// </summary>
        /// <param name="name"></param>
        public void UnbindStream(string name)
        {
            if (Streams.ContainsKey(name))
            {
                IDAQStream stream = Streams[name];
                Streams.Remove(name);

                stream.RemoveDevice(this);
            }
        }


        /// <summary>
        /// The name of this external device, principally for human-recognition
        /// purposes (log files, etc)
        /// </summary>
        public string Name { get; set; }

        public string Manufacturer
        {
            get;
            private set;
        }

        /// <summary>
        /// The Controller we're connected up to.
        /// </summary>
        public Controller Controller { get; set; }

        /// <summary>
        /// What are the parameters of this device's configuration (i.e. a representation of the hardware settings of the device).
        /// May also include information about conversion proc(s) parameters.
        /// </summary>
        public virtual IDictionary<string, object> Configuration { get; private set; }

        // needs a shot at processing the OutputData on its way out to the board
        /// <summary>
        /// Pulls data for output to the given IDAQStream. Default implementation pulls data from
        /// this Device's Controller.
        /// </summary>
        /// <remarks>Appends this Device's Configuration to the IOutputData</remarks>
        /// <param name="stream">Stream for output</param>
        /// <param name="duration">Requested duration</param>
        /// <returns>IOutputData of duration less than or equal to duration</returns>
        /// <exception cref="ExternalDeviceException">Requested duration is less than one sample</exception>
        public abstract IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration);

        private static readonly ILog log = LogManager.GetLogger(typeof(ExternalDeviceBase));

        /// <summary>
        /// Pushes input data to this Device's controller.
        /// </summary>
        /// <param name="stream">Stream supplying the input data</param>
        /// <param name="inData">IInputData to push to the controller</param>
        public abstract void PushInputData(IDAQInputStream stream, IInputData inData);

        public void DidOutputData(IDAQOutputStream stream, DateTimeOffset outputTime, TimeSpan duration, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            Controller.DidOutputData(this, outputTime, duration, configuration);
        }

        /// <summary>
        /// Verify that everything is hooked up correctly
        /// </summary>
        /// <returns></returns>
        public virtual Maybe<string> Validate()
        {
            //Validate name and manufacturer
            if (string.IsNullOrEmpty(Name))
                return Maybe<string>.No("Name must not be null or empty");
            if (string.IsNullOrEmpty(Manufacturer))
                return Maybe<string>.No("Manufacturer must not be null or empty");

            // Validate that each of the DAQStreams associated with
            // this ExternalDevice also validates))
            foreach (var stream in Streams.Values)
            {
                Maybe<string> sVal = stream.Validate();
                if (!sVal)
                    return Maybe<string>.No("DAQStream " +
                        stream.Name + " failed validation: " +
                        sVal.Item2);
            }

            if (Clock == null)
                return Maybe<string>.No("Clock must not be null.");

            if (Background == null &&
                Streams.Values.Where(s => s.GetType().IsSubclassOf(typeof(IDAQOutputStream))).Any())
                return Maybe<string>.No("Background value required.");

            return Maybe<string>.Yes();
        }

    }

    public class UnitConvertingExternalDevice : ExternalDeviceBase
    {

        /// <summary>
        /// Constructs a new ExternalDevice instance with the given name and background.
        /// </summary>
        /// <param name="name">Device name</param>
        /// <param name="background">Device's default output (background) value</param>
        public UnitConvertingExternalDevice(string name, string manufacturer, Measurement background)
            : base(name, manufacturer, null, background)
        {
        }

        /// <summary>
        /// Constructs an ExternalDevice referencing a Controller
        /// </summary>
        /// <param name="name">Device name</param>
        /// <param name="c">Controller to connect with this Device</param>
        /// <param name="background">Device's default output (background) value</param>
        public UnitConvertingExternalDevice(string name, string manufacturer, Controller c, Measurement background)
            : base(name, manufacturer, c, background)
        {
        }

        /// <summary>
        /// What are we converting Measurements to? (Volts, ohms, shakes, whatever)
        /// This MUST match something in the Controller's Conversions dictionary,
        /// or exceptions will get thrown (eventually)
        /// </summary>
        public virtual string MeasurementConversionTarget { get; set; }

        private static readonly ILog log = LogManager.GetLogger(typeof(UnitConvertingExternalDevice));

        public override IOutputData PullOutputData(IDAQOutputStream stream, TimeSpan duration)
        {
            /* 
             * IOuputData will be directed to a device (not an DAQStream) by the controller.
             * Controller should get mapping (device=>data) from the current Epoch instance.
             * 
             * Thus the standard PullOuputData will pull from the controller's queue for this
             * device.
             */

            if (duration.Samples(stream.SampleRate) <= 1)
                throw new ExternalDeviceException("Cannot pull less than one sample.");

            try
            {
                IOutputData data = this.Controller.PullOutputData(this, duration);

                return data.DataWithUnits(MeasurementConversionTarget)
                    .DataWithExternalDeviceConfiguration(this, Configuration);
            }
            catch (Exception ex)
            {
                log.DebugFormat("Error pulling data from controller: " + ex.Message);
                throw;
            }

        }

        /// <summary>
        /// Pushes input data to this Device's controller.
        /// </summary>
        /// <param name="stream">Stream supplying the input data</param>
        /// <param name="inData">IInputData to push to the controller</param>
        public override void PushInputData(IDAQInputStream stream, IInputData inData)
        {
            try
            {

                IInputData convertedData = inData.DataWithUnits(MeasurementConversionTarget)
                    .DataWithExternalDeviceConfiguration(this, Configuration);

                this.Controller.PushInputData(this, convertedData);
            }
            catch (Exception ex)
            {
                log.DebugFormat("Error pushing data to controller: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Verify that everything is hooked up correctly
        /// </summary>
        /// <returns></returns>
        public override Maybe<string> Validate()
        {
            // Pull MeasurementConversionTarget out of Configuration, if it's not already provided
            if (MeasurementConversionTarget == null)
            {
                object obj;
                if (!Configuration.TryGetValue("MeasurementConversionTarget", out obj))
                    return Maybe<string>.No("No MeasurementConversionTarget specified in " + Name + "'s Configuration");

                MeasurementConversionTarget = (string)obj;
            }

            // Validate that the conversion targets are in the Controller's 
            // Conversions dictionary
            if (!(MeasurementConversionTarget.Equals(Measurement.UNITLESS) ||
                Converters.TestTo(MeasurementConversionTarget)))
            {
                return Maybe<string>.No(Name + " failed to find conversion target for " + MeasurementConversionTarget);
            }
            
            return base.Validate();
        }


        protected override IMeasurement ConvertOutput(IMeasurement deviceOutput)
        {
            return Converters.Convert(deviceOutput, MeasurementConversionTarget);
        }

    }

    /// <summary>
    /// The CoalescingDevice is a special kind of ExternalDevice that needs
    /// to coalesce (combine) multiple InputData instances into a single
    /// InputData for processing further up the pipeline.
    /// </summary>
    public class CoalescingDevice : UnitConvertingExternalDevice
    {
        public delegate IInputData CoalesceProc(IList<IInputData> inputs); //IDictionary<string, IInputData> inputs);

        /// <summary>
        /// This is a simple CoalescingProc for a CoalescingDevice, for configuration purposes
        /// </summary>
        public static CoalesceProc OneItemCoalesce = data => data[0];

        private IDictionary<IDAQInputStream, IList<IInputData>> queues =
            new Dictionary<IDAQInputStream, IList<IInputData>>();

        public CoalescingDevice(string name, string manufacturer, Measurement background)
            : base(name, manufacturer, background)
        {
        }

        public CoalescingDevice(string name, string manufacturer, Controller controller, Measurement background)
            : base(name, manufacturer, controller, background)
        {
        }

        public CoalesceProc Coalesce { get; set; }

        /// <summary>
        /// These streams must all produce an IInputData before the
        /// CoalescingDevice will push the resulting IInputData onwards
        /// </summary>
        /// <param name="streams">The instances that are connected</param>
        public void Connect(params IDAQInputStream[] streams)
        {
            queues.Clear();

            foreach (var inStream in streams)
                queues.Add(inStream, new List<IInputData>());
        }

        /// <summary>
        /// Validate the configuration
        /// </summary>
        /// <returns></returns>
        public override Maybe<string> Validate()
        {
            Maybe<string> baseValidation = base.Validate();
            if (baseValidation.Item1) // assuming base passed validation....
            {
                // 98% of the time, the CoalesceProc will need to come from configuration,
                // but a user may be wiring up the rig programmatically, and we should
                // be prepared for that
                if (this.Coalesce == null)
                {
                    // Go look in Configuration for it using Try...(), since we don't
                    // want an exception thrown if it fails; we want to handle it ourselves
                    // via the Maybe<> return
                    object obj;
                    if (Configuration.TryGetValue("CoalesceProc", out obj))
                    {
                        // It will be either "Symphony.Core.CoalescingDevice.OneItemCoalesce"
                        // or something like "Custom.Assembly.Name,Custom.ClassName.CoalescingProcInstance"
                        string assmClassPair = (string)obj;

                        string assemblyName = "Symphony.Core";
                        string name = assmClassPair;
                        if (assmClassPair.Contains(','))
                        {
                            assemblyName = assmClassPair.Split(',')[0];
                            name = assmClassPair.Split(',')[1];
                        }

                        Assembly assm = assemblyName != "Symphony.Core" ?
                            Assembly.Load(assemblyName) :
                            Assembly.GetExecutingAssembly();

                        string instName = name.Substring(name.LastIndexOf('.') + 1); // Trim off leading "."
                        string className = name.Substring(0, name.LastIndexOf('.'));

                        Type classType = assm.GetType(className);
                        MemberInfo[] instMembers = classType.GetMember(instName);
                        if (instMembers.Length == 0)
                            return Maybe<string>.No(string.Format("Couldn't find {0}.{1} CoalesceProc", className, instName));
                        if (instMembers.Length > 2)
                            return Maybe<string>.No(
                                string.Format("Too many members for {0}.{1} CoalesceProc",
                                    className, instName));

                        MemberInfo mi = instMembers[0];
                        if (mi is PropertyInfo)
                        {
                            PropertyInfo pi = (PropertyInfo)mi;

                            if (!(pi.CanRead))
                                return Maybe<string>.No(string.Format("{0}.{1} is not readable", className, instName));

                            this.Coalesce = (CoalesceProc)pi.GetValue(null, null);
                        }
                        else if (mi is FieldInfo)
                        {
                            FieldInfo fi = (FieldInfo)mi;

                            if (!fi.Attributes.HasFlag(FieldAttributes.Public))
                                return Maybe<string>.No(string.Format("{0}.{1} must be public", className, instName));

                            if (!fi.Attributes.HasFlag(FieldAttributes.Static))
                                return Maybe<string>.No(string.Format("{0}.{1} must be static", className, instName));

                            this.Coalesce = (CoalesceProc)fi.GetValue(null);
                        }
                        else
                            return Maybe<string>.No(string.Format("{0}.{1} is not a field or property"));

                        return Maybe<string>.Yes();
                    }
                    else
                        return Maybe<string>.No("No CoalesceProc provided in Configuration");
                }
                else
                    return Maybe<string>.Yes();
            }
            else
                return baseValidation;
        }
        /// <summary>
        /// These streams must all produce an IInputData before the
        /// CoalescingDevice will push the resulting IInputData onwards
        /// </summary>
        /// <param name="streams">The instances that are connected</param>
        public void Connect(params string[] streams)
        {
            IList<IDAQInputStream> objs = new List<IDAQInputStream>();
            foreach (var name in streams)
                objs.Add((IDAQInputStream)Streams[name]);
            // Yes, this will throw an exception if the cast fails
            // That's OK, because it means they're trying to wire up
            // an InputStream and an OutputStream, which is nonsensical

            Connect(objs.ToArray());
        }

        // needs a shot at processing the InputData on its way back from the board
        public override void PushInputData(IDAQInputStream stream, IInputData inData)
        {
            // Do these conversions need to be on a per-stream basis?
            // Or is per-device enough?

            // This is the easy part
            queues[stream].Add(inData);

            // Now figure out if we have all the data we need
            bool haveOneOfEverything = true;
            foreach (var dataList in queues.Values)
            {
                if (dataList.Count == 0)
                {
                    haveOneOfEverything = false;
                }
            }

            if (haveOneOfEverything)
            {
                IList<IInputData> data = new List<IInputData>();
                foreach (var dataList in queues.Values)
                {
                    data.Add(dataList[0]); data.RemoveAt(0);
                }

                Controller.PushInputData(this, Coalesce(data));
            }
        }
    }
}