using System;
using System.Collections.Generic;
using System.Linq;

namespace Symphony.Core
{
    public static class MeasurementExtensions
    {
        /// <summary>
        /// Collect the quantities (in base units) from an IEnumerable of Measurements
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if units are not homogenous</exception>
        /// <returns>array of quantities in base units</returns>
        public static double[] ToBaseUnitQuantityArray(this IEnumerable<IMeasurement> measurements)
        {
            if (!CheckUnitsHomogenous(measurements))
            {
                throw new MeasurementIncompatibilityException("Measurement units are not homgenous in list.");
            }

            return measurements.Select((m) => (double)m.QuantityInBaseUnit).ToArray();
        }

        /// <summary>
        /// Collect the quantities from an IEnumerable of Measurements
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if DisplayUnits are not homogenous</exception>
        /// <returns>array of quantities in intrinsic units</returns>
        public static double[] ToQuantityArray(this IEnumerable<IMeasurement> measurements)
        {
            if (!CheckDisplayUnitsHomogenous(measurements))
            {
                throw new MeasurementIncompatibilityException("Measurement units are not homgenous in list.");
            }

            return measurements.Select((m) => (double)m.Quantity).ToArray();
        }

        /// <summary>
        /// The (homogenous) base units for an IEnumerable of IMeasurements.
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if units are not homogneous</exception>
        /// <returns>The BaseUnit for all Measurements in the enumeration or the empty string if there are no measurements in the enumeration.</returns>
        public static string BaseUnits(this IEnumerable<IMeasurement> measurements)
        {
            try
            {
                return measurements.Select(m => m.BaseUnit).Distinct().Single();
            }
            catch(InvalidOperationException e)
            {
                throw new MeasurementIncompatibilityException("Measurement units are not homogenous", e);
            }
        }

        /// <summary>
        /// The (homogenous) display units for an IEnumerable of IMeasurements.
        /// </summary>
        /// <param name="measurements">Enumerable of Measurements</param>
        /// <exception cref="MeasurementIncompatibilityException">if units are not homogneous</exception>
        /// <returns>The DisplayUnit for all Measurements in the enumeration or the empty string if there are no measurements in the enumeration.</returns>
        public static string DisplayUnits(this IEnumerable<IMeasurement> measurements)
        {
            try
            {
                return measurements.Select(m => m.DisplayUnit).Distinct().Single();
            }
            catch (InvalidOperationException e)
            {
                throw new MeasurementIncompatibilityException("Measurement units are not homogenous", e);
            }
        }

        private static bool CheckUnitsHomogenous(IEnumerable<IMeasurement> measurements)
        {
            return measurements.Select(m => m.BaseUnit).Distinct().Count() == 1;
        }

        private static bool CheckDisplayUnitsHomogenous(IEnumerable<IMeasurement> measurements)
        {
            return measurements.Select(m => m.DisplayUnit).Distinct().Count() == 1;
        }


        /// <summary>
        /// Convert an enumerable of raw quantities to an enumberable of Measurement with the given units
        /// </summary>
        /// <param name="quantities">IEnumerable of quantities</param>
        /// <param name="units">BaseUnits for the resulting Measurements</param>
        /// <returns>Enumerable of Measurements with the given quantities and units</returns>
        public static IEnumerable<IMeasurement> ToMeasurements(this IEnumerable<double> quantities, string units)
        {
            return quantities.Select(q => (decimal)q).ToMeasurements(units);
        }

        public static IEnumerable<IMeasurement> ToMeasurements(this IEnumerable<int> quantities, string units)
        {
            return quantities.Select(q => (decimal)q).ToMeasurements(units);
        }

        public static IEnumerable<IMeasurement> ToMeasurements(this IEnumerable<decimal> quantities, string units)
        {
            return quantities.Select(q => new Measurement(q, units));
        }
    }
}