using System;
using System.Text;
using UnityEngine;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Core.Diagnostics
{
    /// <summary>
    /// <see cref="IExperienceLogger"/> that writes to the Unity console and the device log.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TRD 22 requires that debug logging be removable from release builds. That is enforced here
    /// by the minimum severity defaulting to <see cref="LogSeverity.Warning"/> whenever
    /// <c>DEVELOPMENT_BUILD</c> and <c>UNITY_EDITOR</c> are both absent, which is exactly the
    /// release configuration. Verbose entries are discarded before any string is built.
    /// </para>
    /// <para>
    /// The reason this matters on a Quest is cost rather than tidiness. Each call into the Android
    /// log crosses the JNI boundary and blocks the calling thread; a handful per frame is enough to
    /// push a frame past the compositor deadline and produce a visible stutter, which in a VR
    /// experience about latency would be an unfortunate irony.
    /// </para>
    /// </remarks>
    public sealed class UnityExperienceLogger : IExperienceLogger
    {
        /// <summary>
        /// Reused across calls so that formatting a message does not allocate a new buffer each
        /// time. Safe because Unity calls into gameplay code from a single thread.
        /// </summary>
        private readonly StringBuilder builder = new StringBuilder(160);

        private readonly LogSeverity minimumSeverity;

        /// <summary>
        /// Creates a logger using the default severity floor for the current build configuration.
        /// </summary>
        public UnityExperienceLogger()
            : this(DefaultMinimumSeverity)
        {
        }

        /// <summary>
        /// Creates a logger with an explicit severity floor.
        /// </summary>
        /// <param name="minimumSeverity">Entries below this severity are discarded.</param>
        public UnityExperienceLogger(LogSeverity minimumSeverity)
        {
            this.minimumSeverity = minimumSeverity;
        }

        /// <summary>
        /// Gets the severity floor implied by the build configuration.
        /// </summary>
        /// <value>
        /// <see cref="LogSeverity.Debug"/> in the editor and in development builds, so that
        /// developers see everything; <see cref="LogSeverity.Warning"/> in release builds, so that
        /// players pay no frame-time cost for routine messages while genuine problems are still
        /// captured in the device log for support.
        /// </value>
        public static LogSeverity DefaultMinimumSeverity
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return LogSeverity.Debug;
#else
                return LogSeverity.Warning;
#endif
            }
        }

        /// <inheritdoc />
        public bool IsEnabled(LogSeverity severity) => severity >= minimumSeverity;

        /// <inheritdoc />
        public void Log(LogSeverity severity, LogCategory category, string message)
        {
            if (!IsEnabled(severity))
            {
                return;
            }

            string formatted = Format(category, message);

            switch (severity)
            {
                case LogSeverity.Error:
                    Debug.LogError(formatted);
                    break;

                case LogSeverity.Warning:
                    Debug.LogWarning(formatted);
                    break;

                default:
                    Debug.Log(formatted);
                    break;
            }
        }

        /// <inheritdoc />
        public void LogException(LogCategory category, string message, Exception exception)
        {
            // Exceptions are reported at error severity, which is never stripped: an exception that
            // was caught and hidden is the failure mode this project explicitly forbids.
            if (!IsEnabled(LogSeverity.Error))
            {
                return;
            }

            string detail = exception == null
                ? message
                : $"{message} ({exception.GetType().Name}: {exception.Message})";

            Debug.LogError(Format(category, detail));

            // The full stack trace is only useful to a developer, and Unity renders it as a second
            // console entry, so it is kept out of release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (exception != null)
            {
                Debug.LogException(exception);
            }
#endif
        }

        /// <summary>
        /// Prefixes a message with its category.
        /// </summary>
        /// <param name="category">Originating subsystem.</param>
        /// <param name="message">The message. A <c>null</c> message is rendered explicitly rather
        /// than producing a bare prefix, so an accidental null is visible instead of silent.</param>
        /// <returns>The formatted entry.</returns>
        private string Format(LogCategory category, string message)
        {
            builder.Clear();
            builder.Append('[').Append(category).Append("] ");
            builder.Append(message ?? "<no message>");
            return builder.ToString();
        }
    }
}
