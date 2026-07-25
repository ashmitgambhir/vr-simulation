using System;
using System.Globalization;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Single place where the experience converts between <see cref="DateTime"/> and the ISO-8601
    /// strings used by the save file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UnityEngine.JsonUtility"/> cannot serialise <see cref="DateTime"/>, so every
    /// timestamp in the backend schema is a string. Centralising the conversion prevents the two
    /// classic defects that follow from scattering <c>DateTime.Parse</c> around a codebase:
    /// culture-sensitive parsing that breaks on non-invariant locales, and silent
    /// <see cref="FormatException"/>s thrown while loading a save.
    /// </para>
    /// <para>
    /// Parsing never throws. A malformed timestamp yields <see cref="DateTime.MinValue"/>, which
    /// callers treat as "unknown" and repair during sanitisation.
    /// </para>
    /// </remarks>
    public static class TimestampUtility
    {
        /// <summary>
        /// Round-trip format specifier. Produces values such as <c>2026-07-24T18:00:00.0000000Z</c>,
        /// which are unambiguous, sortable as plain strings, and culture invariant.
        /// </summary>
        private const string RoundTripFormat = "o";

        /// <summary>
        /// Formats a UTC instant for storage.
        /// </summary>
        /// <param name="value">The instant to format. Converted to UTC if it is not already.</param>
        /// <returns>An ISO-8601 round-trip string.</returns>
        public static string Format(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime();

            return utc.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses a stored timestamp without throwing.
        /// </summary>
        /// <param name="value">The stored string. May be <c>null</c>, empty or malformed.</param>
        /// <returns>The parsed UTC instant, or <see cref="DateTime.MinValue"/> if it could not be parsed.</returns>
        public static DateTime Parse(string value)
        {
            return TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;
        }

        /// <summary>
        /// Attempts to parse a stored timestamp.
        /// </summary>
        /// <param name="value">The stored string. May be <c>null</c>, empty or malformed.</param>
        /// <param name="result">Receives the parsed UTC instant on success.</param>
        /// <returns><c>true</c> if <paramref name="value"/> was a well formed timestamp.</returns>
        public static bool TryParse(string value, out DateTime result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = DateTime.MinValue;
                return false;
            }

            // RoundtripKind is mutually exclusive with AdjustToUniversal and AssumeUniversal;
            // combining them raises ArgumentException rather than being ignored. RoundtripKind
            // alone is correct here, because the "o" format this class writes always carries an
            // explicit 'Z' and therefore already round-trips as UTC.
            if (!DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out result))
            {
                result = DateTime.MinValue;
                return false;
            }

            // A hand-edited value that omits the offset parses as Unspecified. Leaving it that way
            // would let Format treat it as local time and silently shift every subsequent write by
            // the machine's timezone offset, so the only kind this project stores is asserted here.
            if (result.Kind == DateTimeKind.Unspecified)
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            }
            else if (result.Kind == DateTimeKind.Local)
            {
                result = result.ToUniversalTime();
            }

            return true;
        }

        /// <summary>
        /// Determines whether a stored timestamp can be parsed.
        /// </summary>
        /// <param name="value">The stored string.</param>
        /// <returns><c>true</c> if the value is a well formed timestamp.</returns>
        public static bool IsValid(string value) => TryParse(value, out _);
    }
}
