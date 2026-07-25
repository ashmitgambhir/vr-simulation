using System;
using VRSimulation.Core.Data;

namespace VRSimulation.Core.Interfaces
{
    /// <summary>
    /// Owns the player's audio, comfort and accessibility preferences (TRD 8, "Settings Manager").
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is exactly one source of truth for settings: the save file. The scaffold this project
    /// replaced kept preferences in both <c>PlayerPrefs</c> and the save JSON, which meant the two
    /// could disagree and nothing decided which won. Everything now reads through this service, and
    /// the service reads through <see cref="ISaveService"/>.
    /// </para>
    /// <para>
    /// Callers never receive the live instance. <see cref="Current"/> returns a copy, so a settings
    /// screen can edit a draft and discard it without an undo path, and no system can mutate
    /// another system's view of the settings by accident. Changes take effect only through
    /// <see cref="Apply"/>.
    /// </para>
    /// </remarks>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets a copy of the settings currently in force. Never <c>null</c>.
        /// </summary>
        UserSettingsData Current { get; }

        /// <summary>
        /// Raised after settings change, carrying a copy of the new values.
        /// </summary>
        /// <remarks>
        /// Subscribers should treat this as "something changed, re-read what you care about" rather
        /// than diffing. The event fires on <see cref="Apply"/> and on
        /// <see cref="ResetToDefaults"/>, and is deliberately not raised during
        /// <see cref="Initialize"/>: systems that start up after the settings are loaded should
        /// pull the current value rather than wait for a change that already happened.
        /// </remarks>
        event Action<UserSettingsData> SettingsChanged;

        /// <summary>
        /// Adopts the settings held by the save service. Call once, after the save has loaded.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Validates and stores a new set of settings, then persists them.
        /// </summary>
        /// <remarks>
        /// The supplied object is sanitised before it is adopted, so a caller that assembled it
        /// from slider values cannot introduce a NaN volume or an out-of-range turn rate. The
        /// caller's instance is not retained.
        /// </remarks>
        /// <param name="settings">The desired settings. Ignored if <c>null</c>.</param>
        /// <returns><c>true</c> if the change was applied and persisted.</returns>
        bool Apply(UserSettingsData settings);

        /// <summary>
        /// Mutates the settings through a callback, then validates, stores and persists.
        /// </summary>
        /// <remarks>
        /// The convenience path for a single change, so that a caller toggling one option does not
        /// have to clone, mutate and pass back. The callback receives a private draft.
        /// </remarks>
        /// <param name="mutate">Receives a draft to modify. Ignored if <c>null</c>.</param>
        /// <returns><c>true</c> if the change was applied and persisted.</returns>
        bool Modify(Action<UserSettingsData> mutate);

        /// <summary>
        /// Restores the comfort-first defaults a first-time player receives, then persists.
        /// </summary>
        /// <returns><c>true</c> if the reset was persisted.</returns>
        bool ResetToDefaults();
    }
}
