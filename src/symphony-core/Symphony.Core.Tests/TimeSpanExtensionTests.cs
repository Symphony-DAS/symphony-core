using System;

namespace Symphony.Core
{
    using NUnit.Framework;
    using Symphony.Core;

    [TestFixture]
    class TimeSpanExtensionTests
    {
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowsForNonHzSampleRate()
        {
            TimeSpanExtensions.FromSamples(10, new Measurement(1000, "foo"));
        }

        [Test]
        public void ConvertsSamplesToTimeSpan()
        {
            IMeasurement m = new Measurement(723, "Hz");
            uint samples = 1300;
            double expectedSeconds =(double) (samples / m.QuantityInBaseUnit);
            TimeSpan expected = new TimeSpan((long)Math.Ceiling(expectedSeconds*TimeSpan.TicksPerSecond));

            Assert.AreEqual(expected, TimeSpanExtensions.FromSamples(samples, m));
        }

        [Test]
        public void ConvertsZeroSamples()
        {
            IMeasurement m = new Measurement(723, "Hz");

            TimeSpan expected = new TimeSpan(0);

            Assert.AreEqual(expected, TimeSpanExtensions.FromSamples(0, m));
        }

        [Test]
        public void SamplesShouldThrowArgumentExceptionForNonHzSampleRate()
        {
            TimeSpan t = TimeSpan.FromMilliseconds(100);
            ArgumentException caught = null;
            try
            {
                t.Samples(new Measurement(100, "foo"));
            }
            catch (ArgumentException e)
            {
                caught = e;
            }

            Assert.That(caught, Is.Not.Null);
        }

        [Test]
        public void SamplesShouldConvertToSamples()
        {
            TimeSpan t = TimeSpan.FromMilliseconds(100);
            Assert.That(t.Samples(new Measurement(1000, "Hz")), Is.EqualTo(100));
        }

        [Test]
        public void SamplesShouldThrowForZeroSampleRate()
        {
            TimeSpan t = TimeSpan.FromMilliseconds(100);
            ArgumentException caught = null;
            try
            {
                t.Samples(new Measurement(0, "hz"));
            }
            catch (ArgumentException e)
            {
                caught = e;
            }

            Assert.That(caught, Is.Not.Null);
        }

        [Test]
        public void SampleShouldThrowForNegativeSampleRate()
        {

            TimeSpan t = TimeSpan.FromMilliseconds(100);
            ArgumentException caught = null;
            try
            {
                t.Samples(new Measurement(-1, "hz"));
            }
            catch (ArgumentException e)
            {
                caught = e;
            }

            Assert.That(caught, Is.Not.Null);
        }

        [Test]
        public void SamplesShouldRoundUp()
        {
            TimeSpan t = TimeSpan.FromMilliseconds(99.3);
            var sampleRate = new Measurement(1000, "Hz");
            Assert.That(t.Samples(sampleRate), Is.EqualTo(
                (ulong)Math.Ceiling(t.TotalSeconds * (double)sampleRate.QuantityInBaseUnit))
                );
        }
    }
}
