using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using NUnit.Framework;
using Symphony.ExternalDevices;

namespace Symphony.Core
{
    [TestFixture]
    class ExternalDeviceTests
    {
        private const string UNUSED_NAME = "UNUSED";

        [Test]
        public void ImplementsTimelineProducer()
        {
            Assert.True(typeof(ExternalDeviceBase).FindInterfaces((t, criteria) => { return true; }, null).Contains(typeof(ITimelineProducer)));
        }

        [Test]
        public void ShouldRaiseExceptionIfPullingLessThanOneSample()
        {
            var e = new UnitConvertingExternalDevice(UNUSED_NAME, null, new Measurement(0, "V"));
            var stream = new DAQOutputStream(UNUSED_NAME);
            e.BindStream(stream);

            stream.SampleRate = new Measurement(1, "Hz");

            Assert.Throws<ExternalDeviceException>(() => e.PullOutputData(stream, TimeSpan.FromMilliseconds(0.1)));
        }

        [Test]
        public void EmptyNameShouldFailValidation()
        {
            var e = new UnitConvertingExternalDevice("", UNUSED_NAME, new Measurement(0, "V"));
            Assert.That((bool)e.Validate(), Is.False);

            e = new UnitConvertingExternalDevice(null, UNUSED_NAME, new Measurement(0, "V"));
            Assert.That((bool)e.Validate(), Is.False);
        }

        [Test]
        public void EmptyManufacturerShouldFailValidation()
        {
            var e = new UnitConvertingExternalDevice(UNUSED_NAME, "", new Measurement(0, "V"));
            Assert.That((bool)e.Validate(), Is.False);

            e = new UnitConvertingExternalDevice(UNUSED_NAME, null, new Measurement(0, "V"));
            Assert.That((bool)e.Validate(), Is.False);
        }

        [Test]
        public void ShouldConvertBackgoundUnits()
        {
            Converters.Register("xfromUnits", "toUnits", m => new Measurement(2 * m.Quantity, m.Exponent, m.BaseUnit));

            var bg = new Measurement(1, "xfromUnits");
            var e = new UnitConvertingExternalDevice(UNUSED_NAME,
                                                     null,
                                                     bg)
                        {
                            MeasurementConversionTarget = "toUnits"
                        };

            var stream = new DAQOutputStream(UNUSED_NAME);
            e.BindStream(stream);

            var expected = new Measurement(bg.Quantity * 2,
                                           bg.Exponent,
                                           bg.BaseUnit);

            Assert.That(e.OutputBackground, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldPropagateOutputDataEvents()
        {
            var controllerMock = new Mock<Controller>();

            var device = new TestDevice(controllerMock.Object);


            DateTimeOffset time = DateTime.Now;
            var config = new List<IPipelineNodeConfiguration>();
            controllerMock.Setup(c => c.DidOutputData(device, time, TimeSpan.FromSeconds(0.1), config));

            device.DidOutputData(new DAQOutputStream("test"), time, TimeSpan.FromSeconds(0.1), config);

            controllerMock.VerifyAll();
        }

        [Test]
        public void ShouldAllowBackgroundChanagesAfterConstruction()
        {
            const string units = "xyz";

            var device = new UnitConvertingExternalDevice("dev", "manufacturer", new Measurement(0, units)) { MeasurementConversionTarget = units };

            var expected = new Measurement(123, units);

            device.Background = expected;

            Assert.That(device.Background, Is.EqualTo(expected));
            Assert.That(device.OutputBackground, Is.EqualTo(expected));
        }
    }

}
