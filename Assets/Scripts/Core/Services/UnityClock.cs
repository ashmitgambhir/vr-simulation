using System;
using UnityEngine;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Core.Services
{
    /// <summary>
    /// <see cref="IClock"/> backed by the system clock and Unity's unscaled timeline.
    /// </summary>
    /// <remarks>
    /// <see cref="Time.unscaledTime"/> rather than <see cref="Time.time"/>, because the experience
    /// sets <see cref="Time.timeScale"/> to zero while paused — for the guardian boundary, for
    /// headset removal, and for the pause menu — and durations measured against scaled time would
    /// stop advancing exactly when the code most needs to know how long something has been waiting.
    /// </remarks>
    public sealed class UnityClock : IClock
    {
        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;

        /// <inheritdoc />
        public float UnscaledTimeSeconds => Time.unscaledTime;
    }
}
