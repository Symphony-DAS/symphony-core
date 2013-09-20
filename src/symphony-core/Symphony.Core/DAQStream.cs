using System;
using System.Collections.Generic;
using System.Linq;
using log4net;

namespace Symphony.Core
{
    /// <summary>
    /// Interface for streams representing hardware channels of a DAQ device.
    /// </summary>
    public interface IDAQStream : ITimelineProducer
    {
        /// <summary>
        /// Configuration of this stream
        /// </summary>
        IDictionary<string, object> Configuration { get; set; }

        /// <summary>
        /// The name of this stream. Although DAQ devices typically represent channels
        /// by number, IDAQStreams are identified by name to prevent
        /// tricky indexing tricks.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The sampling rate of this stream.
        /// </summary>
        IMeasurement SampleRate { get; set; }

        /// <summary>
        /// Indicates if the sample rate can be set on this stream.
        /// </summary>
        bool CanSetSampleRate { get; }

        /// <summary>
        /// An output stream without an associated Device or an input stream without devices to which it
        /// is pushing data is "inactive". DAQControllers may wish to not process data for these incative
        /// streams.
        /// </summary>
        bool Active { get; }

        /// <summary>
        /// Do a Measurement conversion on the Measurement passed in, using the
        /// Conversions dictionary stored in the Controller.
        /// </summary>
        /// <param name="incoming">The Measurement to convert</param>
        /// <param name="outgoingUnits">The unit type to conver to; ensure this unit type
        /// is present in the Controller's Conversions dictionary</param>
        /// 
        /// <returns></returns>
        IMeasurement Convert(IMeasurement incoming, string outgoingUnits);

        /// <summary>
        /// What are we converting Measurements to? (Volts, ohms, shakes, whatever)
        /// This MUST match something in the Controller's Conversions dictionary,
        /// or exceptions will get thrown (eventually)
        /// </summary>
        string MeasurementConversionTarget { get; set; }

        /// <summary>
        /// Test this stream for valid configuration.
        /// </summary>
        /// <returns>A Maybe monad indicating validity (bool) or an error message (string)</returns>
        Maybe<string> Validate();

        /// <summary>
        /// Remove a device association from this stream.
        /// </summary>
        /// <param name="device"></param>
        void RemoveDevice(ExternalDeviceBase device);

        /// <summary>
        /// The DAQ hardware controller associated with this stream.
        /// </summary>
        IDAQController DAQ { get; }
    }

    /// <summary>
    /// DAQInputStreams have one or more (inbound) ExternalDevices.
    /// </summary>
    public interface IDAQInputStream : IDAQStream
    {
        /// <summary>
        /// Push input data from the DAQ device to the associated
        /// external devices.
        /// </summary>
        /// <param name="inData">Data to push</param>
        void PushInputData(IInputData inData);

        /// <summary>
        /// The ExternalDevice instances to which we push IInputData
        /// </summary>
        IList<IExternalDevice> Devices { get; }
    }

    /// <summary>
    /// <para>DAQOutputStreams have one (outbound) ExternalDevice</para>
    /// </summary>
    public interface IDAQOutputStream : IDAQStream
    {
        /// <summary>
        /// The ExternalDevice instances with which we pull IOutputData
        /// </summary>
        IList<IExternalDevice> Devices { get; }

        /// <summary>
        /// Indicates if this stream has more output data. Will be false once
        /// this stream has pulled an IOutputData with IsLast = true.
        /// 
        /// <para>DAQControllers may wish to disable output from this stream
        /// once HasMoreData is false.</para>
        /// </summary>
        bool HasMoreData { get; }

        /// <summary>
        /// Reset HasMoreData.
        /// <para>DAQController may wish to re-enable output from this stream once reset.</para>
        /// </summary>
        void Reset();

        IOutputData PullOutputData(TimeSpan duration);

        /// <summary>
        /// The "background" value to be used when no data is available or upon stopping the 
        /// DAQ device.
        /// </summary>
        IMeasurement Background { get; }

        void DidOutputData(DateTimeOffset outputTime, TimeSpan duration, IEnumerable<IPipelineNodeConfiguration> configuration);


        /// <summary>
        /// Applies the stream Background.
        /// </summary>
        void ApplyBackground();

    }

    /// <summary>
    /// Base implementation of IDAQInputStream.
    /// </summary>
    public class DAQInputStream : IDAQInputStream
    {

        public IClock Clock { get; set; }


        public IDAQController DAQ { get; private set; }

        /// <summary>
        /// Constructs a new DAQInputStream with a null DAQ controller.
        /// </summary>
        /// <param name="name"></param>
        public DAQInputStream(string name) : this(name, null)
        {
            
        }   
   
        /// <summary>
        /// Constructs a new DAQInputStream
        /// </summary>
        /// <param name="name">Stream name</param>
        /// <param name="daqController"> </param>
        public DAQInputStream(string name, IDAQController daqController)
        {
            this.Devices = new List<IExternalDevice>();
            this.Configuration = new Dictionary<string, object>();
            this.Name = name;
            this.DAQ = daqController;

        }

        /// <summary>
        /// The sampling rate of this stream.
        /// </summary>
        public virtual IMeasurement SampleRate
        {
            get { return sampleRate; }
            set
            {
                if (value.Quantity < 0 || value.BaseUnit.ToLower() != "hz")
                {
                    throw new ArgumentException("Illegal SampleRate");
                }

                sampleRate = value;
            }
        }
        private IMeasurement sampleRate;

        public virtual bool CanSetSampleRate
        {
            get { return true; }
        }

        /// <summary>
        /// Indicates whether this stream is "active". An input stream is active if it
        /// has at least one attached ExternalDevice.
        /// </summary>
        public bool Active
        {
            get
            {
                return Devices.Any();
            }
        }

        /// <summary>
        /// Configuration for this stream.
        /// </summary>
        public virtual IDictionary<string, object> Configuration { get; set; }
        /// <summary>
        /// This name of this stream.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Removes the given ExternalDevice from this stream's connected devices.
        /// </summary>
        /// <param name="device">Device to remove</param>
        public void RemoveDevice(ExternalDeviceBase device)
        {
            if (device.Streams.ContainsKey(Name))
                device.UnbindStream(Name);

            Devices.Remove(device);
        }

        /// <summary>
        /// Do a Measurement conversion on the Measurement passed in, using the
        /// Conversions dictionary stored in the Controller. Override in derived
        /// classes only if you don't want to use the Conversions dictionary in
        /// the Controller, or if you don't want to use the MeasurementConversionTarget
        /// property to specify the unit type to convert into.
        /// </summary>
        /// <param name="incoming">The Measurement to convert</param>
        /// <param name="outgoingUnits">The unit type to conver to; ensure this unit type
        /// is present in the Controller's Conversions dictionary</param>
        /// <returns></returns>
        public virtual IMeasurement Convert(IMeasurement incoming, string outgoingUnits)
        {
            // Grab the Controller at the end of the pipeline; all of the ExternalDevices
            // attached to this stream have a reference to it, just grab the first so we
            // can get to it
            return Converters.Convert(incoming, outgoingUnits);
        }

        /// <summary>
        /// What are we converting Measurements to? (Volts, ohms, shakes, whatever)
        /// This MUST match something in the Controller's Conversions dictionary,
        /// or exceptions will get thrown (eventually)
        /// </summary>
        public virtual string MeasurementConversionTarget { get; set; }

        /// <summary>
        /// The ExternalDevice instances to which we push IInputData
        /// </summary>
        public virtual IList<IExternalDevice> Devices { get; private set; }

        /// <summary>
        /// Push the input data to the devices associated with this DAQInputStream.
        /// </summary>
        /// <remarks>Appends this stream's Configuration to the data's stream configuration.</remarks>
        /// <param name="inData">Input data</param>
        public virtual void PushInputData(IInputData inData)
        {
            if (MeasurementConversionTarget == null)
            {
                throw new DAQException("Input stream has null MeasurementConversionTarget");
            }

            foreach (ExternalDeviceBase ed in Devices)
            {
                var data = inData.DataWithUnits(MeasurementConversionTarget);

                ed.PushInputData(this, data.DataWithStreamConfiguration(this, this.Configuration));
            }
        }

        /// <summary>
        /// Make sure everything is configured correctly as a sanity-check before we get going
        /// </summary>
        /// <returns></returns>
        public virtual Maybe<string> Validate()
        {
            // We should always have some non-zero number of Devices configured
            if (Devices.Count == 0 && this.Active)
                return Maybe<string>.No("Zero devices configured and/or 'this' not Active");

            if (Name == null)
                return Maybe<string>.No("Name is null");

            // Pull MeasurementConversionTarget out of Configuration, if it's not already provided
            if (MeasurementConversionTarget == null)
            {
                object obj;
                if (!Configuration.TryGetValue("MeasurementConversionTarget", out obj))
                    return Maybe<string>.No("No MeasurementConversionTarget specified in " + Name + "'s Configuration");

                MeasurementConversionTarget = (string)obj;
            }

            // Make sure there is a conversions target in the Controller for the MeasurementConversionTarget
            // unit type specified here; it's not a 100% guarantee that we're covered, since we don't know
            // the incoming unit type yet, but at least it's better than nothing to start
            if (!Converters.TestTo(MeasurementConversionTarget))
                return Maybe<string>.No(Name + " failed to find conversion target for " + MeasurementConversionTarget);

            if (Clock == null)
                return Maybe<string>.No("Clock must not be null.");

            return Maybe<string>.Yes();
        }
    }


    /// <summary>
    /// Base implementation of IDAQOutputStream.
    /// 
    /// </summary>
    public class DAQOutputStream : IDAQOutputStream
    {
        public IClock Clock { get; set; }

        public IDAQController DAQ { get; private set; }

        /// <summary>
        /// Constructs a new DAQOutputStream without a DAQ controller
        /// </summary>
        /// <param name="name"></param>
        public DAQOutputStream(string name) : this(name, null)
        {
        }

        /// <summary>
        /// Constructs a new DAQOutputStream
        /// </summary>
        /// <param name="name">Stream name</param>
        /// <param name="daqController"> </param>
        public DAQOutputStream(string name, IDAQController daqController)
        {
            this.Devices = new List<IExternalDevice>();
            this.Name = name;
            this.Configuration = new Dictionary<string, object>();
            this.LastDataPulled = false;
            this.DAQ = daqController;
        }

        protected bool LastDataPulled { get; set; }


        public bool HasMoreData
        {
            get
            {
                return Active && !LastDataPulled;
            }
        }

        /// <summary>
        /// The "background" value to be used when no data is available or upon stopping the 
        /// DAQ device. The value is the sum of OutputBackgrounds of all associated devices.
        /// </summary>
        public IMeasurement Background
        {
            get
            {
                var backgrounds = Devices.Select(ed => ed.OutputBackground).ToList();

                var units = backgrounds.Select(b => b.BaseUnit).Distinct().ToList();
                if (units.Count() > 1)
                    throw new DAQException("Devices have multiple background base units");

                var quantity = backgrounds.Select(b => b.QuantityInBaseUnit).Sum();

                return new Measurement(quantity, 0, units.First());
            }
        }

        public void DidOutputData(DateTimeOffset outputTime, TimeSpan duration, IEnumerable<IPipelineNodeConfiguration> configuration)
        {
            foreach (var ed in Devices)
            {
                ed.DidOutputData(this, outputTime, duration, configuration);
            }
        }

        public void Reset()
        {
            LastDataPulled = false;
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual IMeasurement SampleRate
        {
            get { return sampleRate; }
            set
            {
                if (value.Quantity < 0 || value.BaseUnit.ToLower() != "hz")
                {
                    throw new ArgumentException("Illegal SampleRate");
                }

                sampleRate = value;
            }
        }
        private IMeasurement sampleRate;

        public virtual bool CanSetSampleRate
        {
            get { return true; }
        }

        /// <summary>
        /// Configuration for this stream.
        /// </summary>
        public virtual IDictionary<string, object> Configuration { get; set; }
        /// <summary>
        /// This stream's name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Indicates whether this stream is "active". An active output stream has at least one
        /// attached ExternalDevice.
        /// </summary>
        public bool Active
        {
            get
            {
                return Devices.Any();
            }
        }

        /// <summary>
        /// The ExternalDevice instances with which we pull IOutputData
        /// </summary>
        public virtual IList<IExternalDevice> Devices { get; private set; }

        /// <summary>
        /// Removes the given ExternalDevice from this stream.
        /// </summary>
        /// <param name="device">Device to remove</param>
        public void RemoveDevice(ExternalDeviceBase device)
        {
            if (device.Streams.ContainsKey(Name))
                device.UnbindStream(Name);

            Devices.Remove(device);
        }

        /// <summary>
        /// Do a Measurement conversion on the Measurement passed in, using the
        /// Conversions dictionary stored in the Controller. Override in derived
        /// classes only if you don't want to use the Conversions dictionary in
        /// the Controller, or if you don't want to use the MeasurementConversionTarget
        /// property to specify the unit type to convert into.
        /// </summary>
        /// <param name="incoming">The Measurement to convert</param>
        /// <param name="outgoingUnits">The unit type to conver to; ensure this unit type
        /// is present in the Controller's Conversions dictionary</param>
        /// <returns></returns>
        public virtual IMeasurement Convert(IMeasurement incoming, string outgoingUnits)
        {
            // Grab the Controller at the end of the pipeline
            return Converters.Convert(incoming, outgoingUnits);
        }

        /// <summary>
        /// What are we converting Measurements to? (Volts, ohms, shakes, whatever)
        /// This MUST match something in the Controller's Conversions dictionary,
        /// or exceptions will get thrown (eventually)
        /// </summary>
        public virtual string MeasurementConversionTarget { get; set; }

        /// <summary>
        /// Pulls the next IOuputData from the devices associated with this DAQOutputStream. If
        /// multiple devices are associated with this stream, the returned output data is the sum
        /// of all device output data.
        /// <para>This method checks the pulled data to see if its IsLast flag is true.
        /// If so, this stream is marked as with LastDataPulled=true.</para>
        /// </summary>
        /// <remarks>Appends this stream's Configuration to the output data.</remarks>
        /// <returns>The output data to be sent down the output pipeline.</returns>
        /// <exception cref="DAQException">If Devices is empty</exception>
        /// <exception cref="DAQException">If the pulled data's SampleRate does match this stream's SampleRate</exception>
        public virtual IOutputData PullOutputData(TimeSpan duration)
        {
            if (!Devices.Any())
                throw new DAQException("No bound external devices (check configuration)");

            IOutputData outData = null;
            foreach (var ed in Devices)
            {
                var pulled = ed.PullOutputData(this, duration).DataWithUnits(MeasurementConversionTarget);

                outData = outData == null 
                    ? pulled
                    : outData.Zip(pulled, (m1, m2) => new Measurement(m1.QuantityInBaseUnit + m2.QuantityInBaseUnit, 0, m1.BaseUnit));
            }

            if (!outData.SampleRate.Equals(this.SampleRate))
                throw new DAQException("Sample rate mismatch.");

            if (outData.IsLast)
                LastDataPulled = true;

            return outData.DataWithStreamConfiguration(this, this.Configuration);
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(DAQOutputStream));

        /// <summary>
        /// Validates the configuration of this stream.
        /// </summary>
        /// <returns>A Maybe monad indicating validation success (bool) or error message (string)</returns>
        public virtual Maybe<string> Validate()
        {
            // We should always have some non-zero number of Devices configured
            if (Devices.Count == 0 && this.Active)
                return Maybe<string>.No("Zero devices configured and/or 'this' not Active");

            if (Name == null)
                return Maybe<string>.No("Name was null");

            // Pull MeasurementConversionTarget out of Configuration, if it's not already provided
            if (MeasurementConversionTarget == null)
            {
                object obj;
                if (!Configuration.TryGetValue("MeasurementConversionTarget", out obj))
                    return Maybe<string>.No("No MeasurementConversionTarget specified in " + Name + "'s Configuration");

                MeasurementConversionTarget = (string)obj;
            }

            // Make sure there is a conversions target in the Controller for the MeasurementConversionTarget
            // unit type specified here; it's not a 100% guarantee that we're covered, since we don't know
            // the incoming unit type yet, but at least it's better than nothing to start
            if (MeasurementConversionTarget != Measurement.UNITLESS &&
                !Converters.TestTo(MeasurementConversionTarget))
                return Maybe<string>.No("Conversion target not found");


            if (Clock == null)
                return Maybe<string>.No("Clock must not be null.");

            return Maybe<string>.Yes();
        }

        public void ApplyBackground()
        {
            DAQ.ApplyStreamBackground(this);
        }
    }
}