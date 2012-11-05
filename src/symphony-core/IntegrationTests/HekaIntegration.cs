using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Heka;
using Symphony.Core;

namespace IntegrationTests
{
    using NUnit.Framework;

    class HekaIntegration
    {

        const double MAX_VOLTAGE_DIFF = 0.1; //Volts. This is completely arbitrary and dependent on the quality of the patch cable. Just something "close" to 0.

        /// <summary>
        /// The Symphony.Core pipeline must handle max usage (4 Analog Out, 8 Analog in on the Heka device at max 50k sampling rate.
        /// </summary>
        /// <param name="sampleRate"></param>
        /// <param name="nEpochs"></param>
        /// <param name="nOut"></param>
        /// <param name="nIn"></param>
        [Test]
        [Timeout(30*1000)]
        public void MaxBandwidth(
            [Values(20000)] decimal sampleRate,
            [Values(1)] int nEpochs,
            [Values(4)] int nOut,
            [Values(8)] int nIn
            )
        {
            Logging.ConfigureConsole();

            Converters.Clear();
            HekaDAQInputStream.RegisterConverters();
            HekaDAQOutputStream.RegisterConverters();

            Assert.That(HekaDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            foreach (var daq in HekaDAQController.AvailableControllers())
            {

                const double epochDuration = 10; //s
                
                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement(sampleRate, "Hz");

                    var controller = new Controller { Clock = daq, DAQController = daq };

                    HekaDAQController daq1 = daq;
                    var outDevices = Enumerable.Range(0, nOut)
                        .Select(i =>
                                    {
                                        var dev0 = new UnitConvertingExternalDevice("Device_OUT_" + i, "Manufacturer", controller,
                                                                                    new Measurement(0, "V"))
                                                       {
                                                           MeasurementConversionTarget = "V",
                                                           Clock = daq1
                                                       };
                                        dev0.BindStream((IDAQOutputStream)daq1.GetStreams("ANALOG_OUT." + i).First());

                                        return dev0;
                                    })
                                    .ToList();


                    var inDevices = Enumerable.Range(0, nIn)
                        .Select(i =>
                        {
                            var dev0 = new UnitConvertingExternalDevice("Device_IN_" + i, "Manufacturer", controller,
                                                                        new Measurement(0, "V"))
                            {
                                MeasurementConversionTarget = "V",
                                Clock = daq1
                            };
                            dev0.BindStream((IDAQInputStream)daq1.GetStreams("ANALOG_IN." + i).First());

                            return dev0;
                        })
                                    .ToList();

                    for (int j = 0; j < nEpochs; j++)
                    {
                        // Setup Epoch
                        var e = new Epoch("HekaIntegration");

                        var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);
                        IList<IMeasurement> stimData = Enumerable.Range(0, nSamples)
                                                                   .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                                   .ToList();

                        foreach (var outDev in outDevices)
                        {
                            e.Stimuli[outDev] = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) new Dictionary<string, object>(),
                                                        (IOutputData) new OutputData(stimData, daq.SampleRate));

                            e.Background[outDev] = new Epoch.EpochBackground(new Measurement(0, "V"), daq.SampleRate);
                        }

                        foreach (var inDev in inDevices)
                        {
                            e.Responses[inDev] = new Response();
                        }


                        //Run single epoch
                        var fakeEpochPersistor = new FakeEpochPersistor();

                        controller.RunEpoch(e, fakeEpochPersistor);



                        Assert.That((bool)e.StartTime, Is.True);
                        Assert.That((DateTimeOffset)e.StartTime, Is.LessThanOrEqualTo(controller.Clock.Now));
                        Assert.That(fakeEpochPersistor.PersistedEpochs, Contains.Item(e));

                    }

                }
                finally
                {
                    if (daq.HardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        [Timeout(30 * 1000)]
        public void RenderedStimulus(
            [Values(10000, 20000, 50000)] double sampleRate,
            [Values(2)] int nEpochs
            )
        {
            Logging.ConfigureConsole();

            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);
            HekaDAQInputStream.RegisterConverters();
            HekaDAQOutputStream.RegisterConverters();

            Assert.That(HekaDAQController.AvailableControllers().Count(), Is.GreaterThan(0));
            foreach (var daq in HekaDAQController.AvailableControllers())
            {

                const double epochDuration = 5; //s

                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller { Clock = daq, DAQController = daq };

                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, new Measurement(0, "V"))
                                   {
                                       MeasurementConversionTarget = "V",
                                       Clock = daq
                                   };
                    dev0.BindStream((IDAQOutputStream)daq.GetStreams("ANALOG_OUT.0").First());
                    dev0.BindStream((IDAQInputStream)daq.GetStreams("ANALOG_IN.0").First());

                    var dev1 = new UnitConvertingExternalDevice("Device1", "Manufacturer", controller, new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq
                    };
                    dev1.BindStream((IDAQOutputStream)daq.GetStreams("ANALOG_OUT.1").First());
                    dev1.BindStream((IDAQInputStream)daq.GetStreams("ANALOG_IN.1").First());

                    for (int j = 0; j < nEpochs; j++)
                    {
                        // Setup Epoch
                        var e = new Epoch("HekaIntegration");

                        var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);
                        IList<IMeasurement> stimData = (IList<IMeasurement>)Enumerable.Range(0, nSamples)
                                                                   .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                                   .ToList();

                        e.Stimuli[dev0] = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) new Dictionary<string, object>(),
                                                        (IOutputData) new OutputData(stimData, daq.SampleRate));
                        e.Responses[dev0] = new Response();
                        e.Background[dev0] = new Epoch.EpochBackground(new Measurement(0, "V"), daq.SampleRate);

                        e.Stimuli[dev1] = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) new Dictionary<string, object>(),
                                                       (IOutputData) new OutputData(Enumerable.Range(0, nSamples)
                                                                                        .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V"))
                                                                                        .ToList(),
                                                                                    daq.SampleRate));
                        e.Responses[dev1] = new Response();
                        e.Background[dev1] = new Epoch.EpochBackground(new Measurement(0, "V"), daq.SampleRate);


                        //Run single epoch
                        var fakeEpochPersistor = new FakeEpochPersistor();

                        controller.RunEpoch(e, fakeEpochPersistor);



                        Assert.That((bool)e.StartTime, Is.True);
                        Assert.That((DateTimeOffset)e.StartTime, Is.LessThanOrEqualTo(controller.Clock.Now));
                        Assert.That(e.Responses[dev0].Duration, Is.EqualTo(((TimeSpan)e.Duration))
                                                                      .Within(TimeSpanExtensions.FromSamples(1,
                                                                                                             daq.
                                                                                                                 SampleRate)));
                        Assert.That(e.Responses[dev1].Duration, Is.EqualTo(((TimeSpan)e.Duration))
                                                                      .Within(TimeSpanExtensions.FromSamples(1,
                                                                                                             daq.
                                                                                                                 SampleRate)));
                        Assert.That(fakeEpochPersistor.PersistedEpochs, Contains.Item(e));

                        var failures0 =
                            e.Responses[dev0].Data.Select(
                                (t, i) => new { index = i, diff = t.QuantityInBaseUnit - stimData[i].QuantityInBaseUnit })
                                .Where(dif => Math.Abs(dif.diff) > (decimal)MAX_VOLTAGE_DIFF);

                        foreach (var failure in failures0.Take(10))
                            Console.WriteLine("{0}: {1}", failure.index, failure.diff);


                        /*
                         * According to Telly @ Heka, a patch cable may introduce 3-4 offset points
                         */
                        Assert.That(failures0.Count(), Is.LessThanOrEqualTo(4));


                        /*
                        //Since we only have one patch cable on the test rig, 
                        //we're not checking second device response values
                        
                        var failures1 =
                            e.Responses[dev1].Data.Data.Select(
                                (t, i) => new { index = i, diff = t.QuantityInBaseUnit - stimData[i].QuantityInBaseUnit })
                                .Where(dif => Math.Abs(dif.diff) > MAX_VOLTAGE_DIFF);

                        foreach (var failure in failures1.Take(10))
                            Console.WriteLine("{0}: {1}", failure.index, failure.diff);

                        Assert.That(failures1.Count(), Is.EqualTo(0));
                        */
                    }

                }
                finally
                {
                    if (daq.HardwareReady)
                        daq.CloseHardware();
                }

            }
        }


        [Test]
        [Timeout(10 * 1000)]
        public void ShouldSetStreamBackgroundOnStop(
            [Values(10000, 20000)] double sampleRate
            )
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);
            HekaDAQInputStream.RegisterConverters();
            HekaDAQOutputStream.RegisterConverters();

            Assert.That(HekaDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            foreach (var daq in HekaDAQController.AvailableControllers())
            {

                const double epochDuration = 1; //s
                //Configure DAQ
                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller();
                    controller.Clock = daq;
                    controller.DAQController = daq;


                    const decimal expectedBackgroundVoltage = -3.2m;
                    var expectedBackground = new Measurement(expectedBackgroundVoltage, "V");
                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, expectedBackground) { MeasurementConversionTarget = "V" };
                    dev0.BindStream(daq.GetStreams("ANALOG_OUT.0").First() as IDAQOutputStream);
                    dev0.BindStream(daq.GetStreams("ANALOG_IN.0").First() as IDAQInputStream);
                    dev0.Clock = daq;

                    controller.DiscardedEpoch += (c, args) => Console.WriteLine("Discarded epoch: " + args.Epoch);

                    // Setup Epoch
                    var e = new Epoch("HekaIntegration");

                    var nSamples = (int)TimeSpanExtensions.Samples(TimeSpan.FromSeconds(epochDuration), daq.SampleRate);
                    IList<IMeasurement> stimData = (IList<IMeasurement>)Enumerable.Range(0, nSamples)
                                                               .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                               .ToList();

                    var stim = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) new Dictionary<string, object>(),
                                                    (IOutputData) new OutputData(stimData, daq.SampleRate, false));
                    e.Stimuli[dev0] = stim;
                    e.Responses[dev0] = new Response();
                    e.Background[dev0] = new Epoch.EpochBackground(expectedBackground, daq.SampleRate);


                    //Run single epoch
                    var fakeEpochPersistor = new FakeEpochPersistor();

                    controller.RunEpoch(e, fakeEpochPersistor);

                    Thread.Sleep(TimeSpan.FromMilliseconds(100)); //allow DAC to settle


                    var actual = ((HekaDAQController)controller.DAQController).ReadStreamAsyncIO(
                        daq.GetStreams("ANALOG_IN.0").First() as IDAQInputStream);

                    //Should be within +/- 0.025 volts
                    Assert.That(actual.Data.First().QuantityInBaseUnit, Is.InRange(expectedBackground.QuantityInBaseUnit - (decimal)0.025,
                        expectedBackground.QuantityInBaseUnit + (decimal)0.025));
                }
                finally
                {
                    if (daq.HardwareReady)
                        daq.CloseHardware();
                }

            }
        }

        [Test]
        [Timeout(12 * 1000)]
        public void SealLeak(
            [Values(10000, 20000, 50000)] double sampleRate
            )
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);
            HekaDAQInputStream.RegisterConverters();
            HekaDAQOutputStream.RegisterConverters();

            Assert.That(HekaDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            foreach (var daq in HekaDAQController.AvailableControllers())
            {
                const double epochDuration = 10; //s

                //Configure DAQ
                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller();
                    controller.Clock = daq;
                    controller.DAQController = daq;


                    const decimal expectedBackgroundVoltage = 3.2m;
                    var expectedBackground = new Measurement(expectedBackgroundVoltage, "V");
                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, expectedBackground)
                                   {
                                       MeasurementConversionTarget = "V",
                                       Clock = daq
                                   };
                    dev0.BindStream(daq.GetStreams("ANALOG_OUT.0").First() as IDAQOutputStream);
                    dev0.BindStream(daq.GetStreams("ANALOG_IN.0").First() as IDAQInputStream);

                    controller.DiscardedEpoch += (c, args) => Console.WriteLine("Discarded epoch: " + args.Epoch);

                    // Setup Epoch
                    var e = new Epoch("HekaIntegration");

                    HekaDAQController cDAQ = daq;
                    var stim = new DelegatedStimulus("TEST_ID", "V", new Dictionary<string, object>(),
                                                     (parameters, duration) =>
                                                     DataForDuration(duration, cDAQ.SampleRate),
                                                     parameters => Option<TimeSpan>.None()
                        );

                    e.Stimuli[dev0] = stim;
                    e.Background[dev0] = new Epoch.EpochBackground(expectedBackground, daq.SampleRate);


                    //Run single epoch
                    var fakeEpochPersistor = new FakeEpochPersistor();

                    new TaskFactory().StartNew(() =>
                                                   {
                                                       Thread.Sleep(TimeSpan.FromSeconds(epochDuration));
                                                       controller.CancelEpoch();
                                                   },
                                               TaskCreationOptions.LongRunning
                        );

                    controller.RunEpoch(e, fakeEpochPersistor);
                }
                finally
                {
                    if (daq.HardwareReady)
                        daq.CloseHardware();
                }
            }
        }

        private static IOutputData DataForDuration(TimeSpan blockDuration, IMeasurement sampleRate)
        {
            ulong nSamples = blockDuration.Samples(sampleRate);

            var samples = Enumerable.Range(0, (int)nSamples).Select(i => new Measurement(1, "V")).ToList();

            return new OutputData(samples, sampleRate, false);
        }

        [Test]
        [Timeout((5*60) * 1000)]
        public void LongEpochPersistence(
            [Values(5,60)] double epochDuration, //seconds
            [Values(2)] int nEpochs
            )
        {
            const decimal sampleRate = 10000m;

            const string h5Path = "..\\..\\..\\LongEpochPersistence.h5";
            if (File.Exists(h5Path))
                File.Delete(h5Path);

            Logging.ConfigureConsole();

            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);
            HekaDAQInputStream.RegisterConverters();
            HekaDAQOutputStream.RegisterConverters();

            Assert.That(HekaDAQController.AvailableControllers().Count(), Is.GreaterThan(0));

            foreach (var daq in HekaDAQController.AvailableControllers())
            {

                try
                {

                    daq.InitHardware();
                    daq.SampleRate = new Measurement(sampleRate, "Hz");

                    var controller = new Controller {Clock = daq, DAQController = daq};

                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller,
                                                                new Measurement(0, "V"))
                                   {
                                       MeasurementConversionTarget = "V",
                                       Clock = daq
                                   };
                    dev0.BindStream((IDAQOutputStream) daq.GetStreams("ANALOG_OUT.0").First());
                    dev0.BindStream((IDAQInputStream) daq.GetStreams("ANALOG_IN.0").First());


                    for (int j = 0; j < nEpochs; j++)
                    {
                        // Setup Epoch
                        var e = new Epoch("HekaIntegration");

                        var nSamples = (int) TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);
                        IList<IMeasurement> stimData = (IList<IMeasurement>) Enumerable.Range(0, nSamples)
                                                                                 .Select(
                                                                                     i =>
                                                                                     new Measurement(
                                                                                         (decimal)
                                                                                         (8*
                                                                                          Math.Sin(((double) i)/
                                                                                                   (nSamples/10.0))),
                                                                                         "V") as IMeasurement)
                                                                                 .ToList();

                        e.Stimuli[dev0] = new RenderedStimulus((string) "RenderedStimulus", (IDictionary<string, object>) new Dictionary<string, object>(),
                                                               (IOutputData) new OutputData(stimData, daq.SampleRate));
                        e.Responses[dev0] = new Response();
                        e.Background[dev0] = new Epoch.EpochBackground(new Measurement(0, "V"), daq.SampleRate);



                        //Run single epoch
                        using (var persistor = new EpochHDF5Persistor(h5Path, null, 9))
                        {
                            persistor.BeginEpochGroup("label", "source", new string[0], new Dictionary<string, object>(),
                                                      Guid.NewGuid(), DateTimeOffset.Now);

                            controller.RunEpoch(e, persistor);

                            persistor.EndEpochGroup();
                        }


                        Assert.That((bool) e.StartTime, Is.True);
                        Assert.That((DateTimeOffset) e.StartTime, Is.LessThanOrEqualTo(controller.Clock.Now));
                        Assert.That(e.Responses[dev0].Duration, Is.EqualTo(((TimeSpan) e.Duration))
                                                                    .Within(TimeSpanExtensions.FromSamples(1,
                                                                                                           daq.
                                                                                                               SampleRate)));
                        //Assert.That(e.Responses[dev1].Duration, Is.EqualTo(((TimeSpan) e.Duration))
                        //                                            .Within(TimeSpanExtensions.FromSamples(1,
                        //                                                                                   daq.
                        //                                                                                       SampleRate)));
                    }
                }
                finally
                {
                    if (File.Exists(h5Path))
                        File.Delete(h5Path);

                    if (daq.HardwareReady)
                        daq.CloseHardware();
                
                }
            }
        }
    }
}
