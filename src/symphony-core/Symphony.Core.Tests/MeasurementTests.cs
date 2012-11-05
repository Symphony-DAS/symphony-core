using System;
using System.Collections.Generic;
using System.Linq;

namespace Symphony.Core
{
    using NUnit.Framework;

    [TestFixture]
    public class MeasurementTests
    {
        [SetUp]
        public void Reset()
        {
            Converters.Clear();
        }

        [Test]
        public void MeasurementBasics()
        {
            IMeasurement tedsHeight = new Measurement(74, "inches");
            Assert.IsTrue((Measurement) tedsHeight == new Measurement(74, "inches"));

            IMeasurement charlottesHeight = new Measurement(62, "inches");
            Assert.IsFalse(tedsHeight == charlottesHeight);

            Assert.IsFalse(tedsHeight == new Measurement(74, "ImpFeet"));
        }
        [Test]
        public void TestIdentityMeasurementConversion()
        {
            Measurement tedsHeight = new Measurement(74, "inches");
            Assert.IsTrue(Converters.Convert(tedsHeight, "inches").Equals(new Measurement(74, "inches")));
        }

        [Test]
        public void ShouldBuildListFromQuantityArray()
        {
            var quantities = new decimal[] { 1.0m, 2.0m, -1.0m, (decimal) Math.PI };
            const string units = "unit";

            var actual = Measurement.FromArray(quantities, units);
            for (var i = 0; i < quantities.Length; i++)
            {
                Assert.That((double)actual[i].QuantityInBaseUnit, Is.EqualTo(quantities[i]));
                Assert.That(actual[i].BaseUnit, Is.EqualTo(units));
            }
        }

        [Test]
        public void ShouldConstructFromInteger()
        {
            const int expected = 1;
            var m = new Measurement(expected, "V");

            Assert.That((int)m.Quantity, Is.EqualTo(expected));
        }

        [Test]
        public void ShouldConstructFromDouble()
        {
            const double expected = Math.PI;
            var m = new Measurement(expected, "V");
            Assert.That((double)m.Quantity, Is.EqualTo(expected).Within(1e-10));
        }

        [Test]
        public void ShouldThrowExceptionInToQuantityArrayIfUnitsNotHomogenous()
        {
            var measurementList = new System.Collections.Generic.List<IMeasurement>()
                                      {
                                          new Measurement(1, "Hz"),
                                          new Measurement(1, "notHz")
                                      };

            Assert.Throws(typeof(MeasurementIncompatibilityException),
                          () => measurementList.ToBaseUnitQuantityArray()
                );
        }

        [Test]
        public void ShouldThrowExceptionInUnitsIfUnitsNotHomogenous()
        {
            var measurementList = new System.Collections.Generic.List<IMeasurement>()
                                      {
                                          new Measurement(1, "Hz"),
                                          new Measurement(1, "notHz")
                                      };

            Assert.Throws(typeof(MeasurementIncompatibilityException),
                          () => measurementList.BaseUnits()
                );
        }

        [Test]
        public void ShouldConvertToQuantityArray()
        {
            var quantities = new decimal[] { 1.0m, 2.0m, -1.0m, (decimal) Math.PI };
            const string units = "unit";

            var measurements = quantities.Select((q) => new Measurement((decimal) q, units));

            var actual = measurements.ToBaseUnitQuantityArray();

            CollectionAssert.AreEqual(Measurement.ToBaseUnitQuantityArray(measurements), quantities);
        }

        [Test]
        public void ShouldGiveHomogenousUnits()
        {
            var quantities = new double[] { 1.0, 2.0, -1.0, Math.PI };
            const string units = "unit";

            var measurements = quantities.Select((q) => new Measurement((decimal) q, units));

            var actual = measurements.BaseUnits();

            Assert.That(actual, Is.EqualTo(units));
            Assert.That(Measurement.HomogenousBaseUnits(measurements), Is.EqualTo(units));
        }

        [Test]
        public void TestSimpleConversion()
        {
            Converters.Register("ImpFeet", "inches",
                delegate(IMeasurement ft)
                {
                    if (ft.BaseUnit == "ImpFeet")
                        return new Measurement(ft.Quantity * 12, "inches");
                    throw new Exception(String.Format("Illegal conversion: {0} to inches", ft.BaseUnit));
                });
            Converters.Register("ImpYards", "inches",
                delegate(IMeasurement yd)
                {
                    if (yd.BaseUnit == "ImpYards")
                        return new Measurement(yd.Quantity * 12 * 3, "inches");
                    throw new Exception(String.Format("Illegal conversion: {0} to inches", yd.BaseUnit));
                });

            Measurement tedsHeight = new Measurement(6, "ImpFeet");
            Assert.That(Converters.Convert(tedsHeight, "inches"), Is.EqualTo(new Measurement(72, "inches")));

            tedsHeight = new Measurement(2, "ImpYards");
            Assert.That(Converters.Convert(tedsHeight, "inches"), Is.EqualTo(new Measurement(72, "inches")));
        }

        [Test]
        public void BaseUnitQuantity()
        {
            IMeasurement m = new Measurement(100, -3, "V"); //100mV
            Assert.AreEqual(100 * Math.Pow(10, -3), m.QuantityInBaseUnit);
        }

        [Test]
        public void TestExponentialConversion()
        {
            Converters.Register("m", "cm",
                delegate(IMeasurement m)
                {
                    if (m.BaseUnit == "m")
                    {
                        // m are 100 cm
                        double q = (double) (m.Quantity * 100);

                        // but only if the exponents match; if they don't, then we need to adjust
                        int exp = m.Exponent;
                        if (exp < 0)
                        {
                            while (exp < 0)
                            {
                                q = q / 10;
                                exp++;
                            }
                        }
                        else if (exp > 0)
                        {
                            while (exp > 0)
                            {
                                q = q * 10;
                                exp--;
                            }
                        }

                        return new Measurement((decimal) q, "cm");
                    }
                    throw new Exception(String.Format("Illegal conversion: {0} to cm", m.BaseUnit));
                });

            Measurement oneMeter = new Measurement(1, "m"); // no exponent (oneMeter.Exponent = 0)
            Assert.That(Converters.Convert(oneMeter, "cm"), Is.EqualTo(new Measurement(100, "cm")));

            Measurement tenMeter = new Measurement(1, 1, "m"); // exponent (tenMeter.Exponent = 1)
            Assert.That(Converters.Convert(tenMeter, "cm"), Is.EqualTo(new Measurement(1000, "cm")));

            // Now go the other way
            Converters.Register("cm", "m",
                delegate(IMeasurement m)
                {
                    if (m.BaseUnit == "cm")
                    {
                        // m are 100 cm
                        double q = (double) (m.Quantity / 100);

                        // but only if the exponents match; if they don't, then we need to adjust
                        int exp = m.Exponent;
                        if (exp < 0)
                        {
                            while (exp < 0)
                            {
                                q = q / 10;
                                exp++;
                            }
                        }
                        else if (exp > 0)
                        {
                            while (exp > 0)
                            {
                                q = q * 10;
                                exp--;
                            }
                        }

                        return new Measurement((decimal) q, "m");
                    }
                    throw new Exception(String.Format("Illegal conversion: {0} cm to m", m.BaseUnit));
                });

            var oneCentimeter = new Measurement(1, "cm"); // no exponent
            Assert.That(Converters.Convert(oneCentimeter, "m"), Is.EqualTo(new Measurement(0.01m, "m")));

            var tenCentimeter = new Measurement(1, 1, "cm"); // no exponent (tenCentimeter.Exponent = 1)
            Assert.That(Converters.Convert(tenCentimeter, "m"), Is.EqualTo(new Measurement(0.1m, "m")));
        }

        [Test]
        public void ShouldHandleScalingInBaseUnitIdentityConversion(
            [Values(-3,-2,-1,0,1,2,3)] int toExp,
            [Values(-3,-2,-1,0,1,2,3)] int fromExp)
        {
            var q = 1.5m;
            var m = new Measurement(q, fromExp, "B");

            var siUnits = new InternationalSystem();
            Assert.That(Converters.Convert(m, string.Format("{0}{1}", siUnits.ToPrefix(toExp), "B")), 
                Is.EqualTo(new Measurement(q * (decimal)Math.Pow(10, fromExp - toExp), toExp, "B")));
        }

        [Test]
        public void ShouldConvertVTomV() // Case 1666
        {
            var m = new Measurement(2.5, "V");
            Assert.That(Converters.Convert(m, "mV"),
                Is.EqualTo(new Measurement(2500, "mV")));
        }

        [Test]
        public void ShouldConvertAtopA() //Case 1666
        {
            var m = new Measurement(2.5, "A");
            Assert.That(Converters.Convert(m, "pA"),
                Is.EqualTo(new Measurement(2.5e12, "pA")));
        }

        [Test]
        public void ShouldConsiderExponentInEquality()
        {
            var m1 = new Measurement(1.0, 0, "u");
            var m2 = new Measurement(100, -2, "u");

            Assert.That(m1.Equals(m2));
            Assert.That(m2.Equals(m1));

            Assert.That(m1, Is.EqualTo(m2));
            Assert.That(m2, Is.EqualTo(m1));
        }

        [Test, Sequential]
        public void ShouldConvertInternationalSystemPrefixToExponent(
            [Values("Y", "Z", "E", "P", "T", "G", "M", "k", "h", "da", "", "d", "c", "m", "µ", "n", "p", "f", "a", "z", "y")]
            string prefix,
            [Values(24,21,18,15,12,9,6,3,2,1,0,-1,-2,-3,-6,-9,-12,-15,-18,-21,-24)] int exponent)
        {
            var siUnits = new InternationalSystem();
            Assert.That(siUnits.ToExponent(prefix), Is.EqualTo(exponent));
        }

        [Test, Sequential]
        public void ShouldConvertInternationalSystemUnitsToExponent(
            [Values("YV", "ZV", "EV", "PV", "TV", "GV", "MV", "kV", "hV", "daV", "V", "dV", "cV", "mV", "µV", "nV", "pV", "fV", "aV", "zV", "yV")]
            string units,
            [Values(24, 21, 18, 15, 12, 9, 6, 3, 2, 1, 0, -1, -2, -3, -6, -9, -12, -15, -18, -21, -24)] int exponent)
        {
            var siUnits = new InternationalSystem();
            Assert.That(siUnits.Exponent(units), Is.EqualTo(exponent));
        }

        [Test, Sequential]
        public void ShouldConvertInternationalSystemUnitsToBaseUnit(
            [Values("YV", "ZV", "EV", "PV", "TV", "GV", "MV", "kV", "hV", "daV", "V", "dV", "cV", "mV", "µV", "nV", "pV", "fV", "aV", "zV", "yV")]
            string units)            
        {
            var siUnits = new InternationalSystem();
            Assert.That(siUnits.BaseUnit(units), Is.EqualTo("V"));
        }

        [Test, Sequential]
        public void ShouldConvertExponentToInternationalSystemPrefix(
            [Values("Y", "Z", "E", "P", "T", "G", "M", "k", "h", "da", "", "d", "c", "m", "µ", "n", "p", "f", "a", "z", "y")]
            string prefix,
            [Values(24, 21, 18, 15, 12, 9, 6, 3, 2, 1, 0, -1, -2, -3, -6, -9, -12, -15, -18, -21, -24)] int exponent)
        {
            var siUnits = new InternationalSystem();
            Assert.That(siUnits.ToPrefix(exponent), Is.EqualTo(prefix));
        }

        [Test]
        public void ShouldThrowExceptionForUnknownPrefix()
        {
            Assert.That(() => new InternationalSystem().ToExponent("x"), Throws.Exception.TypeOf<ArgumentException>());            
        }

        [Test]
        public void ShoudlThrowExceptionForUnknownExponent()
        {
            Assert.That(() => new InternationalSystem().ToPrefix(17), Throws.Exception.TypeOf<ArgumentException>());
        }

        [Test, Sequential]
        public void ShouldShowDisplayUnitsInInternationalSystemPrefix(
            [Values("Y", "Z", "E", "P", "T", "G", "M", "k", "h", "da", "", "d", "c", "m", "µ", "n", "p", "f", "a", "z", "y")]
            string prefix,
            [Values(24, 21, 18, 15, 12, 9, 6, 3, 2, 1, 0, -1, -2, -3, -6, -9, -12, -15, -18, -21, -24)] int exponent)
        {

            const string baseUnit = "B";
            var m = new Measurement(3m, exponent, baseUnit);

            Assert.That(m.DisplayUnit, Is.EqualTo(string.Format("{0}{1}", prefix, baseUnit)));
        }

        [Test, Sequential]
        public void ShouldInterpretInternationalSystemUnitsInConstruction(
             [Values("Y", "Z", "E", "P", "T", "G", "M", "k", "h", "da", "", "d", "c", "m", "µ", "n", "p", "f", "a", "z", "y")]
            string prefix,
            [Values(24, 21, 18, 15, 12, 9, 6, 3, 2, 1, 0, -1, -2, -3, -6, -9, -12, -15, -18, -21, -24)] int exponent)
        {
            
            const string baseUnits = "B";
            const decimal quantity = 1.5m;
            var units = string.Format("{0}{1}", prefix, baseUnits);
            var m = new Measurement(quantity, units);

            Assert.That(m.DisplayUnit, Is.EqualTo(units));
            Assert.That(m.BaseUnit, Is.EqualTo(baseUnits));
            Assert.That(m.Exponent, Is.EqualTo(exponent));
            Assert.That(m.Quantity, Is.EqualTo(quantity));
        }

        [Test]
        public void ShouldDetermineHomogenousDisplayUnits()
        {
            var homogenousDisplayUnits = new List<IMeasurement>() {new Measurement(1, -3, "V"), new Measurement(2, -3, "V")};

            Assert.That(homogenousDisplayUnits.DisplayUnits(), Is.EqualTo("mV"));
            Assert.That(Measurement.HomogenousDisplayUnits(homogenousDisplayUnits), Is.EqualTo(homogenousDisplayUnits.DisplayUnits()));
        }

        [Test]
        public void ShouldThrowExceptionForInhomgenousDisplayUnits()
        {
            var inhomogenousDisplayUnits = new List<IMeasurement>() { new Measurement(1, -3, "V"), new Measurement(2, -2, "V") };

            Assert.That(() => inhomogenousDisplayUnits.DisplayUnits(), Throws.Exception.TypeOf<MeasurementIncompatibilityException>());
            Assert.That(() => Measurement.HomogenousDisplayUnits(inhomogenousDisplayUnits), Throws.Exception.TypeOf<MeasurementIncompatibilityException>());

        }

        [Test]
        public void ShouldRetrieveIntrinsicQuantities()
        {
            var homogenousDisplayUnits = new List<IMeasurement>() { new Measurement(1, -3, "V"), new Measurement(2, -3, "V") };

            Assert.That(homogenousDisplayUnits.ToQuantityArray(), Is.EqualTo(homogenousDisplayUnits.Select(m => (double)m.Quantity).ToArray()));
            Assert.That(homogenousDisplayUnits.ToQuantityArray(), Is.EqualTo(Measurement.ToQuantityArray(homogenousDisplayUnits)));
        }

        [Test]
        public void ShouldThrowExceptionCollectingQuantityArrayForInhomogenousDisplayUnits()
        {
            var inhomogenousDisplayUnits = new List<IMeasurement>() { new Measurement(1, -3, "V"), new Measurement(2, -2, "V") };

            Assert.That(() => inhomogenousDisplayUnits.ToQuantityArray(), Throws.Exception.TypeOf<MeasurementIncompatibilityException>());
            Assert.That(() => Measurement.ToQuantityArray(inhomogenousDisplayUnits), Throws.Exception.TypeOf<MeasurementIncompatibilityException>());
        }

    }
}
