using System;

namespace VRSimulation.Core.Interfaces
{
    /// <summary>
    /// Supplies the current time to systems that record it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every timestamp written to the save file comes from here rather than from
    /// <see cref="DateTime.UtcNow"/> directly. That makes the persistence tests deterministic: a
    /// test can assert on an exact stored timestamp, and can advance time to verify behaviour such
    /// as the minimum interval between automatic writes without actually waiting.
    /// </para>
    /// <para>
    /// Implementations must return UTC. Local time is never persisted: a player who travels across
    /// a timezone, or whose headset clock adjusts for daylight saving, would otherwise produce a
    /// save whose timestamps run backwards.
    /// </para>
    /// </remarks>
    public interface IClock
    {
        /// <summary>Gets the current UTC time.</summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// Gets seconds elapsed since application start, unaffected by timescale changes.
        /// </summary>
        /// <remarks>
        /// Used for measuring durations such as time spent in a module. It must not be derived from
        /// scaled time, because the experience sets the timescale to zero while paused and a paused
        /// player is not studying the module.
        /// </remarks>
        float UnscaledTimeSeconds { get; }
    }
}
