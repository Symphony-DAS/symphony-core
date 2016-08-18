using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NI;
using NUnit.Framework;
using Symphony.Core;

namespace IntegrationTests
{
    [TestFixture]
    public class NIIntegration
    {
        const double MAX_VOLTAGE_DIFF = 0.1; //Volts. This is completely arbitrary and dependent on the quality of the patch cable. Just something "close" to 0.

        [Test]
        [Timeout(30 * 1000)]
        public void RenderedStimulus(
            [Values(1000, 10000, 20000, 50000)] double sampleRate,
            [Values(2)] int nEpochs
            )
        {
            Converters.Clear();
            Converters.Register("V", "V",
                // just an identity conversion for now, to pass Validate()
                                (IMeasurement m) => m);

            Assert.That(NIDAQController.AvailableControllers().Count(), Is.GreaterThan(0));
            foreach (var daq in NIDAQController.AvailableControllers())
            {

                const double epochDuration = 5; //s

                daq.InitHardware();
                try
                {
                    daq.SampleRate = new Measurement((decimal)sampleRate, "Hz");

                    var controller = new Controller { Clock = daq.Clock, DAQController = daq };

                    var dev0 = new UnitConvertingExternalDevice("Device0", "Manufacturer", controller, new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev0.BindStream((IDAQOutputStream)daq.GetStreams("ao0").First());
                    dev0.BindStream((IDAQInputStream)daq.GetStreams("ai0").First());

                    var dev1 = new UnitConvertingExternalDevice("Device1", "Manufacturer", controller, new Measurement(0, "V"))
                    {
                        MeasurementConversionTarget = "V",
                        Clock = daq.Clock,
                        OutputSampleRate = daq.SampleRate,
                        InputSampleRate = daq.SampleRate
                    };
                    dev1.BindStream((IDAQOutputStream)daq.GetStreams("ao1").First());
                    dev1.BindStream((IDAQInputStream)daq.GetStreams("ai1").First());

                    for (int j = 0; j < nEpochs; j++)
                    {
                        // Setup Epoch
                        var e = new Epoch("NIIntegration" + j);

                        var nSamples = (int)TimeSpan.FromSeconds(epochDuration).Samples(daq.SampleRate);
                        IList<IMeasurement> stimData = (IList<IMeasurement>)Enumerable.Range(0, nSamples)
                                                                   .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V") as IMeasurement)
                                                                   .ToList();

                        e.Stimuli[dev0] = new RenderedStimulus((string)"RenderedStimulus", (IDictionary<string, object>)new Dictionary<string, object>(),
                                                        (IOutputData)new OutputData(stimData, daq.SampleRate));
                        e.Responses[dev0] = new Response();

                        e.Stimuli[dev1] = new RenderedStimulus((string)"RenderedStimulus", (IDictionary<string, object>)new Dictionary<string, object>(),
                                                       (IOutputData)new OutputData(Enumerable.Range(0, nSamples)
                                                                                        .Select(i => new Measurement((decimal)(8 * Math.Sin(((double)i) / (nSamples / 10.0))), "V"))
                                                                                        .ToList(),
                                                                                    daq.SampleRate));
                        e.Responses[dev1] = new Response();


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
                                (t, i) => new { index = i, diff = t.QuantityInBaseUnits - stimData[i].QuantityInBaseUnits })
                                .Where(dif => Math.Abs(dif.diff) > (decimal)MAX_VOLTAGE_DIFF);

                        foreach (var failure in failures0.Take(10))
                            Console.WriteLine("{0}: {1}", failure.index, failure.diff);

                        Assert.That(failures0.Count(), Is.LessThanOrEqualTo(0));
                    }

                }
                finally
                {
                    if (daq.IsHardwareReady)
                        daq.CloseHardware();
                }

            }
        }

    }

}
