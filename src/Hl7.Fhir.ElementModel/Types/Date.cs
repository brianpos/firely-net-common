﻿/* 
 * Copyright (c) 2020, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-net-sdk/master/LICENSE
 */

#nullable enable

using Hl7.Fhir.Utility;
using System;
using System.Text.RegularExpressions;
using static Hl7.Fhir.Utility.Result;

namespace Hl7.Fhir.ElementModel.Types
{
    public class Date : Any, IComparable, ICqlEquatable, ICqlOrderable, ICqlConvertible
    {
        private Date(string original, DateTimeOffset parsedValue, DateTimePrecision precision, bool hasOffset)
        {
            _original = original;
            _parsedValue = parsedValue;
            Precision = precision;
            HasOffset = hasOffset;
        }

        public static Date Parse(string representation) =>
            TryParse(representation, out var result) ? result : throw new FormatException($"String '{representation}' was not recognized as a valid date.");

        public static bool TryParse(string representation, out Date value) => tryParse(representation, out value);

        public static Date FromDateTimeOffset(DateTimeOffset dto, DateTimePrecision prec = DateTimePrecision.Day,
        bool includeOffset = false)
        {
            string formatString = prec switch
            {
                DateTimePrecision.Year => "yyyy",
                DateTimePrecision.Month => "yyyy-MM",
                _ => "yyyy-MM-dd",
            };
            if (includeOffset) formatString += "K";

            var representation = dto.ToString(formatString);
            return Parse(representation);
        }

        public DateTime ToDateTime() => new DateTime(_original, _parsedValue, Precision, HasOffset);


        public static Date Today(bool includeOffset = false) => FromDateTimeOffset(DateTimeOffset.Now, includeOffset: includeOffset);

        /// <summary>
        /// The precision of the date available. 
        /// </summary>
        public DateTimePrecision Precision { get; private set; }

        public int? Years => Precision >= DateTimePrecision.Year ? _parsedValue.Year : (int?)null;
        public int? Months => Precision >= DateTimePrecision.Month ? _parsedValue.Month : (int?)null;
        public int? Days => Precision >= DateTimePrecision.Day ? _parsedValue.Day : (int?)null;

        /// <summary>
        /// The span of time ahead/behind UTC
        /// </summary>
        public TimeSpan? Offset => HasOffset ? _parsedValue.Offset : (TimeSpan?)null;

        private readonly string _original;
        private readonly DateTimeOffset _parsedValue;

        /// <summary>
        /// Whether the time specifies an offset to UTC
        /// </summary>
        public bool HasOffset { get; private set; }

        private static readonly string DATEFORMAT =
            $"(?<year>[0-9]{{4}}) ((?<month>-[0-9][0-9]) ((?<day>-[0-9][0-9]) )?)? {Time.OFFSETFORMAT}?";
        public static readonly Regex PARTIALDATEREGEX = new Regex("^" + DATEFORMAT + "$",
                RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Converts the date to a full DateTimeOffset instance.
        /// </summary>
        /// <param name="hours"></param>
        /// <param name="minutes"></param>
        /// <param name="seconds"></param>
        /// <param name="defaultOffset">Offset used when the datetime does not specify one.</param>
        /// <returns></returns>
        public DateTimeOffset ToDateTimeOffset(int hours, int minutes, int seconds, TimeSpan defaultOffset) =>
            ToDateTimeOffset(hours, minutes, seconds, 0, defaultOffset);

        /// <summary>
        /// Converts the date to a full DateTimeOffset instance.
        /// </summary>
        /// <param name="hours"></param>
        /// <param name="minutes"></param>
        /// <param name="seconds"></param>
        /// <param name="milliseconds"></param>
        /// <param name="defaultOffset">Offset used when the datetime does not specify one.</param>
        /// <returns></returns>
        public DateTimeOffset ToDateTimeOffset(int hours, int minutes, int seconds, int milliseconds, TimeSpan defaultOffset) =>
                new DateTimeOffset(_parsedValue.Year, _parsedValue.Month, _parsedValue.Day, hours, minutes, seconds, milliseconds,
                        HasOffset ? _parsedValue.Offset : defaultOffset);

        /// <summary>
        /// Converts the date to a full DateTimeOffset instance.
        /// </summary>
        /// <returns></returns>
        private static bool tryParse(string representation, out Date value)
        {
            if (representation is null) throw new ArgumentNullException(nameof(representation));

            var matches = PARTIALDATEREGEX.Match(representation);
            if (!matches.Success)
            {
                value = new Date(representation, default, default, default);
                return false;
            }

            var y = matches.Groups["year"];
            var m = matches.Groups["month"];
            var d = matches.Groups["day"];
            var offset = matches.Groups["offset"];

            var prec =
                d.Success ? DateTimePrecision.Day :
                m.Success ? DateTimePrecision.Month :
                DateTimePrecision.Year;

            var parseableDT = y.Value +
                (m.Success ? m.Value : "-01") +
                (d.Success ? d.Value : "-01") +
                "T" + "00:00:00" +
                (offset.Success ? offset.Value : "Z");

            var success = DateTimeOffset.TryParse(parseableDT, out var parsedValue);
            value = new Date(representation, parsedValue, prec, offset.Success);
            return success;
        }

        /// <summary>
        /// Determines if two dates are equal according to CQL equality rules.
        /// </summary>
        /// <returns>returns true if the values are both dates, have the same precision and each date component is exactly the same. 
        /// Dates with timezones are normalized to zulu before comparison is done.</returns>
        /// <remarks>See <see cref="TryCompareTo(Any)"/> for more details.</remarks>
        public override bool Equals(object? obj) => obj is Any other && TryEquals(other).ValueOrDefault(false);

        public Result<bool> TryEquals(Any other) => other is Date ? TryCompareTo(other).Select(i => i == 0) : false;

        public static bool operator ==(Date a, Date b) => Equals(a, b);
        public static bool operator !=(Date a, Date b) => !Equals(a, b);

        /// <summary>
        /// Compare two dates according to CQL equality rules
        /// </summary>
        /// <remarks>See <see cref="TryCompareTo(Any)"/> for more details.</remarks>
        public int CompareTo(object? obj) => obj is Date p ?
            TryCompareTo(p).ValueOrThrow() : throw NotSameTypeComparison(this, obj);

        /// <summary>
        /// Compares two dates according to CQL ordering rules.
        /// </summary> 
        /// <param name="other"></param>
        /// <returns>An <see cref="Result.Ok{T}"/> with an integer value representing the reseult of the comparison: 0 if this and other are equal, 
        /// -1 if this is smaller than other and +1 if this is bigger than other, or the other is null. If the values are incomparable
        /// this function returns a <see cref="Result.Fail{T}"/> with the reason why the comparison between the two values was impossible.
        /// </returns>
        /// <remarks>The comparison is performed by considering each precision in order, beginning with years. 
        /// If the values are the same, comparison proceeds to the next precision; 
        /// if the values are different, the comparison stops and the result is false. If one input has a value 
        /// for the precision and the other does not, the comparison stops and the values cannot be compared; if neither
        /// input has a value for the precision, or the last precision has been reached, the comparison stops
        /// and the result is true.</remarks>
        public Result<int> TryCompareTo(Any other)
        {
            return other switch
            {
                null => 1,
                Date p => DateTime.CompareDateTimeParts(_parsedValue, Precision, HasOffset, p._parsedValue, p.Precision, p.HasOffset),
                _ => throw NotSameTypeComparison(this, other)
            };
        }

        public static bool operator <(Date a, Date b) => a.CompareTo(b) < 0;
        public static bool operator <=(Date a, Date b) => a.CompareTo(b) <= 0;
        public static bool operator >(Date a, Date b) => a.CompareTo(b) > 0;
        public static bool operator >=(Date a, Date b) => a.CompareTo(b) >= 0;


        public override int GetHashCode() => _original.GetHashCode();
        public override string ToString() => _original;

        public static implicit operator DateTime(Date pd) => pd.ToDateTime();
        public static explicit operator Date(DateTimeOffset dto) => FromDateTimeOffset(dto);
        public static explicit operator String(Date d) => ((ICqlConvertible)d).TryConvertToString().ValueOrThrow();

        bool? ICqlEquatable.IsEqualTo(Any other) => other is { } && TryEquals(other) is Ok<bool> ok ? ok.Value : (bool?)null;

        // Note that, in contrast to equals, this will return false if operators cannot be compared (as described by the spec)
        bool ICqlEquatable.IsEquivalentTo(Any other) => other is { } pd && TryEquals(pd).ValueOrDefault(false);

        int? ICqlOrderable.CompareTo(Any other) => other is { } && TryCompareTo(other) is Ok<int> ok ? ok.Value : (int?)null;

        Result<DateTime> ICqlConvertible.TryConvertToDateTime() => Ok(ToDateTime());

        Result<Date> ICqlConvertible.TryConvertToDate() => Ok(this);

        Result<String> ICqlConvertible.TryConvertToString() => Ok(new String(ToString()));

        Result<Boolean> ICqlConvertible.TryConvertToBoolean() => CannotCastTo<Boolean>(this);
        Result<Decimal> ICqlConvertible.TryConvertToDecimal() => CannotCastTo<Decimal>(this);
        Result<Integer> ICqlConvertible.TryConvertToInteger() => CannotCastTo<Integer>(this);
        Result<Long> ICqlConvertible.TryConvertToLong() => CannotCastTo<Long>(this);
        Result<Quantity> ICqlConvertible.TryConvertToQuantity() => CannotCastTo<Quantity>(this);
        Result<Ratio> ICqlConvertible.TryConvertToRatio() => CannotCastTo<Ratio>(this);
        Result<Time> ICqlConvertible.TryConvertToTime() => CannotCastTo<Time>(this);
        Result<Code> ICqlConvertible.TryConvertToCode() => CannotCastTo<Code>(this);
        Result<Concept> ICqlConvertible.TryConvertToConcept() => CannotCastTo<Concept>(this);
    }
}
