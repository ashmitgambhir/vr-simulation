using System;
using VRSimulation.Core.Diagnostics;

namespace VRSimulation.Core.Interfaces
{
    /// <summary>
    /// Structured logging for every subsystem (TRD 22).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing in the codebase calls <c>UnityEngine.Debug.Log</c> directly. Going through this
    /// interface buys three things the raw API cannot: category filtering, the ability to strip
    /// verbose severities from release builds as TRD 22 requires, and a seam that lets tests assert
    /// a failure path actually reported itself rather than passing silently.
    /// </para>
    /// <para>
    /// That last point is what enforces "never silently fail". A recovery path that swallows its
    /// cause is indistinguishable from one that was never exercised; the tests assert on the log.
    /// </para>
    /// </remarks>
    public interface IExperienceLogger
    {
        /// <summary>
        /// Writes an entry.
        /// </summary>
        /// <param name="severity">Importance of the entry.</param>
        /// <param name="category">Originating subsystem.</param>
        /// <param name="message">Human readable description. Must not be interpolated by the
        /// caller when the severity may be stripped; use the overloads that defer formatting.</param>
        void Log(LogSeverity severity, LogCategory category, string message);

        /// <summary>
        /// Writes an entry describing a caught exception.
        /// </summary>
        /// <param name="category">Originating subsystem.</param>
        /// <param name="message">What the code was attempting when the exception was raised.</param>
        /// <param name="exception">The exception. May be <c>null</c>.</param>
        void LogException(LogCategory category, string message, Exception exception);

        /// <summary>
        /// Determines whether entries of a given severity are currently retained.
        /// </summary>
        /// <remarks>
        /// Call this to guard the construction of an expensive message. Building a string that is
        /// then discarded still allocates, and allocation on a Quest means garbage collection
        /// inside a frame the compositor expected to finish in eleven milliseconds.
        /// </remarks>
        /// <param name="severity">The severity to test.</param>
        /// <returns><c>true</c> if an entry at this severity would be recorded.</returns>
        bool IsEnabled(LogSeverity severity);
    }
}
