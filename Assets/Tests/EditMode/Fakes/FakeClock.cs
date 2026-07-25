using System;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Tests.EditMode.Fakes
{
    /// <summary>
    /// <see cref="IClock"/> whose time only advances when a test says so.
    /// </summary>
    /// <remarks>
    /// Lets the suite assert on exact persisted timestamps, and lets it verify time-dependent
    /// behaviour such as save coalescing without the test actually sleeping. A suite that waits on
    /// real time is a suite developers eventually stop running.
    /// </remarks>
    public sealed class FakeClock : IClock
    {
        /// <summary>
        /// A fixed, arbitrary starting instant. Chosen to be unambiguous when it appears in a
        /// failure message.
        /// </summary>
        public static readonly DateTime DefaultStart = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        public DateTime UtcNow { get; private set; } = DefaultStart;

        /// <inheritdoc />
        public float UnscaledTimeSeconds { get; private set; }

        /// <summary>
        /// Advances both timelines.
        /// </summary>
        /// <param name="seconds">Seconds to advance by.</param>
        public void Advance(float seconds)
        {
            UnscaledTimeSeconds += seconds;
            UtcNow = UtcNow.AddSeconds(seconds);
        }

        /// <summary>
        /// Sets the wall clock without moving the unscaled timeline, for testing timestamp
        /// handling in isolation.
        /// </summary>
        /// <param name="utcNow">The instant to report.</param>
        public void SetUtcNow(DateTime utcNow) => UtcNow = utcNow;
    }
}
