using System;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Identity and resume point for a single player (backend schema, "User").
    /// </summary>
    /// <remarks>
    /// Serialised by <see cref="UnityEngine.JsonUtility"/>, which only handles public fields on
    /// <see cref="SerializableAttribute"/> types and has no support for <see cref="DateTime"/>.
    /// Timestamps are therefore stored as ISO-8601 round-trip ("o") strings and exposed through
    /// typed properties so that calling code never parses dates by hand.
    /// </remarks>
    [Serializable]
    public sealed class UserProfile
    {
        /// <summary>Unique identifier. A GUID string, generated once on first launch.</summary>
        public string userId = string.Empty;

        /// <summary>Display name. Defaults to "Guest"; the experience never asks for real names.</summary>
        public string username = SaveConstants.DefaultUsername;

        /// <summary>ISO-8601 UTC timestamp of first launch.</summary>
        public string createdAt = string.Empty;

        /// <summary>ISO-8601 UTC timestamp of the most recent session.</summary>
        public string lastPlayed = string.Empty;

        /// <summary>
        /// The module the player should resume into, stored as the integer value of
        /// <see cref="ModuleId"/> so that the on-disk format stays schema-compatible.
        /// </summary>
        public int currentModule = (int)ModuleId.None;

        /// <summary>Gets or sets <see cref="currentModule"/> as a strongly typed value.</summary>
        public ModuleId CurrentModule
        {
            get => ModuleIdExtensions.FromInt(currentModule);
            set => currentModule = (int)value;
        }

        /// <summary>Gets the first-launch timestamp, or <see cref="DateTime.MinValue"/> if unset or malformed.</summary>
        public DateTime CreatedAtUtc => TimestampUtility.Parse(createdAt);

        /// <summary>Gets the most recent session timestamp, or <see cref="DateTime.MinValue"/> if unset or malformed.</summary>
        public DateTime LastPlayedUtc => TimestampUtility.Parse(lastPlayed);

        /// <summary>
        /// Creates a profile for a brand new player.
        /// </summary>
        /// <param name="utcNow">Current UTC time, injected so the behaviour is deterministic under test.</param>
        /// <returns>A populated profile with a freshly generated identifier.</returns>
        public static UserProfile CreateNew(DateTime utcNow)
        {
            string timestamp = TimestampUtility.Format(utcNow);

            return new UserProfile
            {
                userId = Guid.NewGuid().ToString("D"),
                username = SaveConstants.DefaultUsername,
                createdAt = timestamp,
                lastPlayed = timestamp,
                currentModule = (int)ModuleId.None
            };
        }

        /// <summary>
        /// Repairs any field that is missing or unparseable, so that a partially corrupt save can
        /// still be loaded rather than discarded (backend schema, "Error Recovery").
        /// </summary>
        /// <param name="utcNow">Current UTC time, used to backfill absent timestamps.</param>
        /// <returns><c>true</c> if any field had to be repaired.</returns>
        public bool Sanitize(DateTime utcNow)
        {
            bool repaired = false;
            string timestamp = TimestampUtility.Format(utcNow);

            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = Guid.NewGuid().ToString("D");
                repaired = true;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                username = SaveConstants.DefaultUsername;
                repaired = true;
            }

            if (!TimestampUtility.IsValid(createdAt))
            {
                createdAt = timestamp;
                repaired = true;
            }

            if (!TimestampUtility.IsValid(lastPlayed))
            {
                lastPlayed = timestamp;
                repaired = true;
            }

            if (!ModuleIdExtensions.IsDefined(currentModule))
            {
                currentModule = (int)ModuleId.None;
                repaired = true;
            }

            return repaired;
        }
    }
}
