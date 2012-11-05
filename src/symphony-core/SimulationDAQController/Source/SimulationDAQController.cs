using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Symphony.Core;
using System.Collections.Concurrent;

namespace Symphony.SimulationDAQController
{

    /// <summary>
    /// Delegate that implements simulation of the experimental subject. Client code will provide a Simulation delegate
    /// that implements their desired simulation algorithm. This delegate will be called once per simulation timestep.
    /// </summary>
    /// <param name="output">Mapping from output stream to IOutputData for this simulation time step.</param>
    /// <param name="timeStep">Duration of the time step</param>
    /// <returns>A map from IDAQInputStream to IInputData "acquired" during the simulated time step</returns>
    public delegate IDictionary<IDAQInputStream, IInputData> Simulation(IDictionary<IDAQOutputStream, IOutputData> output, TimeSpan timeStep);


    /// <summary>
    /// Provides a simulation controller for the Symphony.Core DAQ system. The purpose of
    /// the simulation controller is twofold:
    /// (1) Allows testing of the Symphony.Core architecture or user protocol or 
    /// stimulus generation codeindependent of DAQ hardware
    /// 
    /// (2) Allows use of Symphony for computational simulation in addition to direct
    /// data acquisition. In this mode, the same application shell, protocol and 
    /// stimulus generation code can be used for both physiology and simulation
    /// experiments, with the resulting output in the common Symphony data format.
    /// </summary>
    public class SimulationDAQController : DAQControllerBase, IClock, IMutableDAQController
    {
        private const int DEFAULT_ITERATION_MILLISECONDS = 500;

        public IMeasurement SampleRate
        {
            get;
            set;
        }

        /// <summary>
        /// The Simulation delegate for this SimulationDAQController
        /// </summary>
        public Simulation SimulationRunner { get; set; }


        /// <summary>
        /// Constructs a new SimulationDAQController with the given simulation time step.
        /// </summary>
        /// <param name="simulationTimeStep">Time step</param>
        public SimulationDAQController(TimeSpan simulationTimeStep)
        {
            ProcessInterval = simulationTimeStep;
            this.Clock = this;
        }

        /// <summary>
        /// Constructs a new SimulationDAQController with the default simulation time step (500 ms)
        /// </summary>
        public SimulationDAQController()
            : this(TimeSpan.FromMilliseconds(DEFAULT_ITERATION_MILLISECONDS))
        { }

        /// <summary>
        /// IClock implementation using system (CPU) clock.
        /// </summary>
        public DateTimeOffset Now
        {
            get
            {
                return DateTimeOffset.Now;
            }
        }

        protected override void StartHardware(bool waitForTrigger)
        {
            if (waitForTrigger)
                throw new DAQException("SimulationDAQController does not support triggered start.");

            Running = true;
        }

        public override void ApplyStreamBackgroundAsync(IDAQOutputStream s, IMeasurement background)
        {
            
        }

        protected override bool ShouldStop()
        {
            return StopRequested;
        }

        protected override IDictionary<IDAQInputStream, IInputData> ProcessLoopIteration(IDictionary<IDAQOutputStream, IOutputData> outData, TimeSpan deficit)
        {
            var result = SimulationRunner(outData, ProcessInterval);

            return result;
        }

        public override void SetStreamsBackground()
        { }

        public void AddStream(IDAQStream stream)
        {
            DAQStreams.Add(stream);
        }
    }
}
