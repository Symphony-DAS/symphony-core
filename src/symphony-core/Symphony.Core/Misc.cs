/* Copyright (c) 2012 Physion Consulting, LLC */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

/*
 * These are "utility" constructs used elsewhere throughout the
 * Symphony codebase. They don't really map directly to the problem
 * domain, so they don't really deserve to be in domain-named files.
 * I'm kinda harsh that way. :-)
 */

namespace Symphony.Core
{
    /// <summary>
    /// Helpful "cheat" for the Validate() method; it wants to return both
    /// a bool and a reason-phrase (string), so have it return this Maybe<>
    /// type, with some additional help to make it easy to use the Maybe<>
    /// in classic O-O constructs (via implicit conversion to bool) or for
    /// use in code that needs the "other" part (via implicit conversion to T).
    /// (Compare this to the Option[T] type from F# or Scala.)
    /// </summary>
    /// <typeparam name="T">The actual value we are returning</typeparam>
    public class Maybe<T> : Tuple<bool, T>
    {
        /// <summary>
        /// Create a special-case Maybe with no value
        /// </summary>
        /// <returns></returns>
        public static Maybe<T> Yes()
        {
            return new Maybe<T>(true);
        }
        /// <summary>
        /// Create a special-case Maybe with the wrapped value
        /// </summary>
        /// <returns></returns>
        public static Maybe<T> Yes(T val)
        {
            return new Maybe<T>(true, val);
        }
        public static Maybe<T> Some()
        {
            return new Maybe<T>(true);
        }
        public static Maybe<T> Some(T val)
        {
            return new Maybe<T>(true, val);
        }
        public static Maybe<T> No()
        {
            return new Maybe<T>(false);
        }
        public static Maybe<T> No(T val)
        {
            return new Maybe<T>(false, val);
        }
        public static Maybe<T> None()
        {
            return new Maybe<T>(false);
        }
        public static Maybe<T> None(T val)
        {
            return new Maybe<T>(false, val);
        }

        internal Maybe(bool b) : base(b, default(T)) { }
        private Maybe(bool b, T val) : base(b, val) { }

        public static implicit operator bool(Maybe<T> that) { return that.Item1; }
        public static implicit operator T(Maybe<T> that) { return that.Item2; }
    }


    /// <summary>
    /// A type that answers the "does the value exist" and "what is that value"
    /// question simultaneously. Compare this to the Option[T] type from F#.
    /// </summary>
    /// <typeparam name="T">The actual value we are returning</typeparam>
    public class Option<T> : Tuple<bool, T>, IEnumerable<T>
    {
        public static Option<T> Some()
        {
            return new Option<T>(true);
        }
        public static Option<T> Some(T val)
        {
            return new Option<T>(true, val);
        }
        public static Option<T> None()
        {
            return new Option<T>(false);
        }
        public static Option<T> None(T val)
        {
            return new Option<T>(false, val);
        }

        internal Option(bool b) : base(b, default(T)) { }
        internal Option(bool b, T val) : base(b, val) { }

        public static implicit operator bool(Option<T> that) { return that.Item1; }
        public static implicit operator T(Option<T> that) { return that.Item2; }

        public bool IsSome() { return Item1; }
        public bool IsNone() { return !Item1; }

        public T Get()
        {
            if (IsSome())
                return Item2;
            else
                throw new NullReferenceException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            yield return Item2;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            yield return Item2;
        }
    }

    /// <summary>
    /// This is the conversion function that Converters must provide so as to
    /// convert from one measurement to another (usually in a different unit
    /// of measure).
    /// </summary>
    /// <param name="input">The Measurement to convert</param>
    /// <returns>The result of the conversion</returns>
    public delegate IMeasurement ConvertProc(IMeasurement input);


    /// <summary>
    /// Convenient extensions for the TimeSpan class to convert between TimeSpan and number of samples
    /// at a given sampling rate.
    /// </summary>
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Gives a TimeSpan equivalent to the given number of samples at the given sampling rate.
        /// </summary>
        /// <param name="sampleCount">Number of samples</param>
        /// <param name="sampleRate">Sampling rate</param>
        /// <returns>TimeSpan equal to sampleCount / sampleRate</returns>
        /// <exception cref="ArgumentException">If sampleRate is not in Hz</exception>
        public static TimeSpan FromSamples(uint sampleCount, IMeasurement sampleRate)
        {
            Contract.Assert(sampleRate != null, "sampleRate is null.");

            if (sampleRate.BaseUnit.ToLower() != "hz")
            {
                throw new ArgumentException(String.Format("Sample Rate has unexpected units: {0}", sampleRate.BaseUnit));
            }

            double seconds = sampleCount / (double)sampleRate.QuantityInBaseUnit;
            return new TimeSpan((long)Math.Ceiling(seconds * TimeSpan.TicksPerSecond));
        }

        /// <summary>
        /// Calculates number of samples in given this TimeSpan at the given sampleRate 
        /// </summary>
        /// <param name="timeSpan">this TimeSpan</param>
        /// <param name="sampleRate">Sampling rate</param>
        /// <returns>Ceiling of timeSpan (seconds) * sampleRate</returns>
        /// <exception cref="ArgumentException">If samplieRate is not in Hz or has rate less than or equal to 0</exception>
        public static ulong Samples(this TimeSpan timeSpan, IMeasurement sampleRate)
        {
            if (sampleRate.BaseUnit.ToLower() != "hz")
            {
                throw new ArgumentException(String.Format("Sample Rate has unexpected units: {0}", sampleRate.BaseUnit));
            }

            if (sampleRate.QuantityInBaseUnit <= 0)
            {
                throw new ArgumentException("Sample rate must be greater than 0.", "sampleRate");
            }

            checked
            {
                return (ulong)Math.Ceiling(timeSpan.TotalSeconds * (double)sampleRate.QuantityInBaseUnit);
            }
        }
    }


    /// <summary>
    /// Collection of conversion routines from one Measurement type to another.
    /// Be sure to register (from,to)/ConvertProc instances before use
    /// </summary>
    public static class Converters
    {
        /// <summary>
        /// Register a ConvertProc routine as the code to use when converting
        /// a Measurement from the "from" units to the "to" units
        /// </summary>
        /// <param name="from">The unit type converting from</param>
        /// <param name="to">The unit type to conver to</param>
        /// <param name="proc">The code to do the conversion</param>
        public static void Register(string from, string to, ConvertProc proc)
        {
            // Overwrite on duplication
            ConvertProc obj;
            if (converters.TryGetValue(new Tuple<string, string>(from, to), out obj))
                converters.Remove(new Tuple<string, string>(from, to));

            converters.Add(new Tuple<string, string>(from, to), proc);
        }

        /// <summary>
        /// Dump all the registered conversions. (Generally this is only used during
        /// unit testing.)
        /// </summary>
        public static void Clear()
        {
            converters.Clear();
        }

        /// <summary>
        /// Test to see if this conversion is in the dictionary of registered conversions
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool Test(string from, string to)
        {
            ICollection<Tuple<string, string>> pairs = converters.Keys;
            foreach (var p in pairs)
                if (p.Item1 == from && p.Item2 == to)
                    return true;
            return false;
        }

        /// <summary>
        /// Test to see if the dictionary of registered conversions has this unit type
        /// as a "to" unit type
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        public static bool TestTo(string to)
        {
            ICollection<Tuple<string, string>> pairs = converters.Keys;
            foreach (var p in pairs)
                if (p.Item2 == to)
                    return true;
            return false;
        }

        /// <summary>
        /// Test to see if the dictionary of registered conversions has this unit type
        /// as either a "from" or "to" unit type
        /// </summary>
        /// <param name="eitherFromOrTo"></param>
        /// <returns></returns>
        public static bool TestEither(string eitherFromOrTo)
        {
            ICollection<Tuple<string, string>> pairs = converters.Keys;
            foreach (var p in pairs)
                if (p.Item1 == eitherFromOrTo || p.Item2 == eitherFromOrTo)
                    return true;
            return false;
        }

        private static readonly IUnitSystem _SIUnits = new InternationalSystem();

        /// <summary>
        /// Convert from one Measurement to another. This takes care of finding
        /// a ConvertProc out of the converters dictionary and applies it; throws
        /// an Exception if the (incoming.BaseUnit -> outgoingUnits) converter
        /// function cannot be found.
        /// </summary>
        /// <param name="from">The Measurement to convert</param>
        /// <param name="to">The units of measure to convert to</param>
        /// <returns>Converted Measurement</returns>
        public static IMeasurement Convert(IMeasurement from, string to)
        {
            // Identity transformation always works. We do handle 
            // differences in exponent
            
            if (_SIUnits.BaseUnit(to) == from.BaseUnit)
            {
                if(_SIUnits.Exponent(to) == from.Exponent)
                    return from;

                var toExp = _SIUnits.Exponent(to);
                var expDiff = from.Exponent - toExp;
                return new Measurement(from.Quantity * (decimal)Math.Pow(10, expDiff), toExp, _SIUnits.BaseUnit(to));
            }

            // Can we find a converter for these two units? Use TryGetValue()
            // to avoid the KeyNotFoundException that gets thrown using the
            // traditional [] lookup--we want to throw our own exception.
            //
            ConvertProc converter = null;
            if (converters.TryGetValue(new Tuple<string, string>(from.BaseUnit, to), out converter))
                return converter(from);

            // I dunno what the hell you're trying to convert
            //
            throw new Exception(
                String.Format(
                    "Unrecognized Measurement conversion: {0} to {1}",
                    from.BaseUnit, to));
        }


        /// <summary>
        /// The Dictionary that holds converters from "unit-type" to "unit-type"
        /// (hence the Tuple(string,string) key to the delegate value). Currently 
        /// assumes that it will be populated by something outside of the 
        /// Controller itself.
        /// </summary>
        static IDictionary<Tuple<string, string>, ConvertProc> converters =
            new Dictionary<Tuple<string, string>, ConvertProc>();
    }
}