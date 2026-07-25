using System;
using System.Collections.Generic;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Tests.EditMode.Fakes
{
    /// <summary>
    /// <see cref="IExperienceLogger"/> that records entries so tests can assert on them.
    /// </summary>
    /// <remarks>
    /// This is how the suite enforces "never silently fail". Asserting that a recovery path
    /// returned the right value proves it recovered; asserting that it also logged proves it told
    /// somebody. Without the second check, a path that swallows its cause passes every test and
    /// then costs an engineer a day of confusion in the field.
    /// </remarks>
    public sealed class RecordingLogger : IExperienceLogger
    {
        /// <summary>One recorded entry.</summary>
        public readonly struct Entry
        {
            /// <summary>Severity the entry was written at.</summary>
            public LogSeverity Severity { get; }

            /// <summary>Originating subsystem.</summary>
            public LogCategory Category { get; }

            /// <summary>Rendered message.</summary>
            public string Message { get; }

            /// <summary>
            /// Creates an entry.
            /// </summary>
            /// <param name="severity">Severity the entry was written at.</param>
            /// <param name="category">Originating subsystem.</param>
            /// <param name="message">Rendered message.</param>
            public Entry(LogSeverity severity, LogCategory category, string message)
            {
                Severity = severity;
                Category = category;
                Message = message;
            }
        }

        /// <summary>Every entry recorded, in order.</summary>
        public List<Entry> Entries { get; } = new List<Entry>();

        /// <inheritdoc />
        public bool IsEnabled(LogSeverity severity) => true;

        /// <inheritdoc />
        public void Log(LogSeverity severity, LogCategory category, string message) =>
            Entries.Add(new Entry(severity, category, message));

        /// <inheritdoc />
        public void LogException(LogCategory category, string message, Exception exception)
        {
            string detail = exception == null ? message : $"{message} ({exception.GetType().Name})";
            Entries.Add(new Entry(LogSeverity.Error, category, detail));
        }

        /// <summary>
        /// Determines whether anything was recorded at a given severity.
        /// </summary>
        /// <param name="severity">Severity to look for.</param>
        /// <returns><c>true</c> if at least one entry matches.</returns>
        public bool HasEntryAt(LogSeverity severity) => Entries.Exists(entry => entry.Severity == severity);

        /// <summary>
        /// Determines whether anything at or above a given severity was recorded.
        /// </summary>
        /// <param name="severity">Minimum severity to look for.</param>
        /// <returns><c>true</c> if at least one entry matches.</returns>
        public bool HasEntryAtLeast(LogSeverity severity) => Entries.Exists(entry => entry.Severity >= severity);

        /// <summary>Removes all recorded entries.</summary>
        public void Clear() => Entries.Clear();
    }
}
