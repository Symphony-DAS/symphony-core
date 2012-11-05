using System;
using System.Collections.Generic;
using System.Linq;

namespace Symphony.Core
{
    public interface IMeasurement : IEquatable<IMeasurement>
    {
        /// <summary>
        /// Measurement quantity with double precision
        /// </summary>
        decimal Quantity { get; }

        /// <summary>
        /// Base-10 exponent of the measurement relative to base unit
        /// </summary>
        int Exponent { get; }

        /// <summary>
        /// SI (or other unit system) base unit of the measurement
        /// </summary>
        string BaseUnit { get; }

        /// <summary>
        /// Display units, accounting for exponent. For example 1 x 10^-3 V
        /// has a base Unit of "V", but a display unit of "mV"
        /// </summary>
        string DisplayUnit { get; }

        /// <summary>
        /// Measurement quantity expressed in base units. For example,
        /// 1 mV would be 1*10^-3 V.
        /// 
        /// Equal to Quantity * 10^(Exponent)
        /// </summary>
        decimal QuantityInBaseUnit { get; }
    }

    /// <summary>
    /// A value type representing a quantity and a unit of measure. Measurement
    /// is not intended to implement a full unit calculus system, but rather to support
    /// the limited unit consistency and conversion required by the Symphony
    /// input/output pipelines.
    /// 
    /// <para>Measurements are represented as a quanity, an exponent and a unit string:</para>
    /// 
    /// <para><example>1 mV = 1.0 x 10^-3 V =  Measurement { Quantity = 1.0, Exponent = -3, BaseUnit = "V" }</example></para>
    /// 
    /// <para>Unit checks are performed at runtime.</para> 
    /// 
    /// </summary>
    public class Measurement : IMeasurement
    {
        /// <summary>
        /// Measurement quantity with double precision
        /// </summary>
        public decimal Quantity { get; private set; }

        /// <summary>
        /// Base-10 exponent of the measurement relative to base unit
        /// </summary>
        public int Exponent { get; private set; }

        /// <summary>
        /// SI (or other unit system) unit of the measurement
        /// </summary>
        public string BaseUnit { get; private set; }

        public string DisplayUnit
        {
            get
            {
                var siUnits = new InternationalSystem();
                return string.Format("{0}{1}", siUnits.ToPrefix(Exponent), BaseUnit);
            }
        }

        /// <summary>
        /// Contruct a Measurement
        /// </summary>
        /// <param name="q">The (howevermany)s of (whatever)s we have</param>
        /// <param name="u">The (whatever)s we have (howevermany)s of</param>
        public Measurement(decimal q, string u)
        {
            var siUnits = new InternationalSystem();

            Quantity = q;
            Exponent = siUnits.Exponent(u);
            BaseUnit = siUnits.BaseUnit(u);
        }

        /// <summary>
        /// Contruct a Measurement
        /// </summary>
        /// <param name="q">The (howevermany)s of (whatever)s we have</param>
        /// <param name="e">The exponent of the measurement relative to base units (e.g. -3 for mV) </param>
        /// <param name="u">The (whatever)s we have (howevermany)s of</param>
        public Measurement(decimal q, int e, string u) { Quantity = q; Exponent = e; BaseUnit = u; }

        public Measurement(double q, string u) : this((decimal)q, u)
        {
        }

        public Measurement(double q, int e, string u) : this((decimal)q, e, u)
        {
        }

        public Measurement(long q, string u) : this((decimal)q, u)
        {
        }

        public Measurement(long q, int e, string u) : this((decimal)q, e, u)
        {
        }

        /// <summary>
        /// Construct a list of Measurements from an array of quantities and unit
        /// </summary>
        /// <param name="quantities">array of measurement quantities</param>
        /// <param name="unit">units for each measurement</param>
        /// <returns>IList of Measurements</returns>
        public static IList<IMeasurement> FromArray(double[] quantities, string unit)
        {
            return FromArray(quantities.AsEnumerable().Select(q => (decimal)q).ToArray(), unit);
        }


        /// <summary>
        /// Construct a list of Measurements from an array of quantities and unit
        /// </summary>
        /// <param name="quantities">array of measurement quantities</param>
        /// <param name="unit">units for each measurement</param>
        /// <returns>IList of Measurements</returns>
        public static IList<IMeasurement> FromArray(decimal[] quantities, string unit)
        {
            return quantities.AsEnumerable().Select(q => new Measurement(q, unit) as IMeasurement)
                .ToList();
        }

        public bool Equals(IMeasurement other)
        {
            if (other == null)
                return false;

            if(this.BaseUnit != other.BaseUnit)
                return false;

            // .Net decimals may be compared exactly
            var difference = Math.Abs(QuantityInBaseUnit - other.QuantityInBaseUnit);
            
            return Decimal.ToDouble(difference) < Double.Epsilon;
        }

        /// <summary>
        /// Construct a string representation of the Measurement.
        /// </summary>
        /// <returns>A string representation of the Measurement.</returns>
        public override string ToString()
        {
            return String.Format("{0} x 10^{1} {2}", Quantity, Exponent, BaseUnit);
        }

        /// <summary>
        /// Measurement quantity expressed in base units. For example,
        /// 1 mV would be 1*10^-3 V.
        /// </summary>
        public decimal QuantityInBaseUnit
        {
            get
            {
                return Quantity * (decimal)Math.Pow(10, Exponent);
            }
        }

        public const string UNITLESS = "_unitless_";

        /// <summary>
        /// Return true if these two Measurements are equivalent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is IMeasurement)
            {
                return Equals((IMeasurement) obj);
            }

            return false;
        }

        /// <summary>
        /// Overridden == operator for Measurements; being value types, we don't care
        /// so much about identity for these guys.
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static bool operator ==(Measurement lhs, IMeasurement rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Overridden != operator for Measurements; being value types, we don't care
        /// so much about identity for these guys.
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static bool operator !=(Measurement lhs, IMeasurement rhs)
        {
            return !(lhs.Equals(rhs));
        }

        /// <summary>
        /// Return a hash of this Quantity/BaseUnit pair.
        /// 
        /// <remarks>No clue if this is a good hash. Probably isn't.</remarks>
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return BaseUnit.GetHashCode();
        }

        /// <summary>
        /// Collect the quantities (in base units) from an IEnumerable of Measurements
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if BaseUnits are not homogenous</exception>
        /// <returns>array of quantities in base units</returns>
        public static double[] ToBaseUnitQuantityArray(IEnumerable<IMeasurement> measurements)
        {
            return measurements.ToBaseUnitQuantityArray();
        }

        /// <summary>
        /// Collect the quantities from an IEnumerable of Measurements
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if DisplayUnits are not homogenous</exception>
        /// <returns>array of quantities in intrinsic units</returns>
        public static double[] ToQuantityArray(IEnumerable<IMeasurement> measurements)
        {
            return measurements.ToQuantityArray();
        }

        /// <summary>
        /// The (homogenous) base units for an IEnumerable of Measurements.
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if units are not homogneous</exception>
        /// <returns>The BaseUnit for all Measurements in the enumeration</returns>
        public static string HomogenousBaseUnits(IEnumerable<IMeasurement> measurements)
        {
            return measurements.BaseUnits();
        }

        /// <summary>
        /// The (homogenous) display units for an IEnumerable of Measurements.
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if units are not homogneous</exception>
        /// <returns>The DisplayUnit for all Measurements in the enumeration</returns>
        public static string HomogenousDisplayUnits(IEnumerable<IMeasurement> measurements)
        {
            return measurements.DisplayUnits();
        }


    }

    /// <summary>
    /// Exception indicating incompatibility of Measurements in a calculation
    /// (likely a unit mismatch).
    /// </summary>
    public class MeasurementIncompatibilityException : SymphonyException
    {
        public MeasurementIncompatibilityException(string unit1, string unit2)
            : base(unit1 + " is incompatible with " + unit2)
        { }

        public MeasurementIncompatibilityException(string msg) : base(msg) { }

        public MeasurementIncompatibilityException(string msg, Exception innerException) : base(msg, innerException)
        { }
    }

    /*
     * These should eventually be provided via MEF
     */

    public interface IUnitSystem
    {
        int ToExponent(string prefix);
        string ToPrefix(int exponent);

        string BaseUnit(string units);
        int Exponent(string units);
    }

    public class InternationalSystem : IUnitSystem
    {
        public int ToExponent(string prefix)
        {
            if (!_prefixToExponent.ContainsKey(prefix))
                throw new ArgumentException(string.Format("Unable to determine SI exponent for prefix {0}", prefix));

            return _prefixToExponent[prefix];
        }

        public string ToPrefix(int exponent)
        {
            if (!_exponentToPrefix.ContainsKey(exponent))
                throw new ArgumentException(string.Format("Unable to determine SI prefix for exponent {0}", exponent), "exponent");

            return _exponentToPrefix[exponent];
        }

        private IDictionary<string, string> _baseUnitsCache = new Dictionary<string, string>(); 
        public string BaseUnit(string units)
        {
            if(_baseUnitsCache.ContainsKey(units))
            {
                return _baseUnitsCache[units];
            }

            var prefix = UnitPrefix(units);
            var result = HasPrefix(units, prefix) ? units.Substring(prefix.Length) : units;

            _baseUnitsCache[units] = result;

            return result;
        }

        private static bool HasPrefix(string units, string prefix)
        {
            return !(prefix == null || prefix == units);
        }

        public int Exponent(string units)
        {
            var prefix = UnitPrefix(units);

            return HasPrefix(units, prefix) ? ToExponent(prefix) : 0;
        }


        private readonly IDictionary<string,string> _unitPrefixCache = new Dictionary<string, string>(); 
        private string UnitPrefix(string units)
        {
            if(_unitPrefixCache.ContainsKey(units))
            {
                return _unitPrefixCache[units];
            }

            var result = _prefixToExponent.Keys.Where(k => !string.IsNullOrEmpty(k)).FirstOrDefault(units.StartsWith);
            _unitPrefixCache[units] = result;

            return result;
        }

        private readonly IDictionary<string, int> _prefixToExponent = new Dictionary<string, int> {
                                                                         {"Y", 24},
                                                                         {"Z", 21},
                                                                         {"E", 18},
                                                                         {"P", 15},
                                                                         {"T", 12},
                                                                         {"G", 9},
                                                                         {"M", 6},
                                                                         {"k", 3},
                                                                         {"h", 2},
                                                                         {"da", 1},
                                                                         {"", 0},
                                                                         {"d", -1},
                                                                         {"c", -2},
                                                                         {"m", -3},
                                                                         {"µ", -6},
                                                                         {"n", -9},
                                                                         {"p", -12},
                                                                         {"f", -15},
                                                                         {"a", -18},
                                                                         {"z", -21},
                                                                         {"y", -24}
                                                                     };

        private readonly IDictionary<int, string> _exponentToPrefix = new Dictionary<int, string> {
                                                                         {24, "Y"},
                                                                         {21, "Z"},
                                                                         {18, "E"},
                                                                         {15, "P"},
                                                                         {12, "T"},
                                                                         {9, "G"},
                                                                         {6, "M"},
                                                                         {3, "k"},
                                                                         {2, "h"},
                                                                         {1, "da"},
                                                                         {0, ""},
                                                                         {-1, "d"},
                                                                         {-2, "c"},
                                                                         {-3, "m"},
                                                                         {-6, "µ"},
                                                                         {-9, "n"},
                                                                         {-12, "p"},
                                                                         {-15, "f"},
                                                                         {-18, "a"},
                                                                         {-21, "z"},
                                                                         {-24, "y"}
                                                                     };

    }
}
