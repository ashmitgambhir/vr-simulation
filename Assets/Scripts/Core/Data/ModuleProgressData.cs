using System;
using UnityEngine;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// One record per module the player has started (backend schema, "Module Progress").
    /// </summary>
    /// <remarks>
    /// The schema names <c>moduleId</c> as the unique key and requires that duplicate records be
    /// prevented. That invariant is enforced by <see cref="SaveData.UpsertModule"/> rather than by
    /// callers, so there is exactly one place it can be got wrong.
    /// </remarks>
    [Serializable]
    public sealed class ModuleProgressData
    {
        /// <summary>Integer value of the <see cref="ModuleId"/> this record describes.</summary>
        public int moduleId = (int)ModuleId.None;

        /// <summary>
        /// Human readable module name, denormalised into the record so that an exported save is
        /// legible without the application that wrote it.
        /// </summary>
        public string moduleName = string.Empty;

        /// <summary>Whether the player reached the module's completion confirmation.</summary>
        public bool completed;

        /// <summary>Knowledge check score as a percentage, 0 to 100.</summary>
        public int score;

        /// <summary>
        /// Number of times the module has been entered, including the attempt in progress.
        /// Incremented on entry rather than on completion so that abandoned attempts are counted.
        /// </summary>
        public int attempts;

        /// <summary>Total time spent inside the module across all attempts, in seconds.</summary>
        public float completionTimeSeconds;

        /// <summary>ISO-8601 UTC timestamp of the most recent visit.</summary>
        public string lastPlayed = string.Empty;

        /// <summary>Gets or sets <see cref="moduleId"/> as a strongly typed value.</summary>
        public ModuleId Module
        {
            get => ModuleIdExtensions.FromInt(moduleId);
            set => moduleId = (int)value;
        }

        /// <summary>Gets the most recent visit, or <see cref="DateTime.MinValue"/> if unset.</summary>
        public DateTime LastPlayedUtc => TimestampUtility.Parse(lastPlayed);

        /// <summary>
        /// Creates a record for a module the player is entering for the first time.
        /// </summary>
        /// <param name="module">The module being entered.</param>
        /// <param name="displayName">Human readable name, from the module's definition asset.</param>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns>A record with one recorded attempt and no progress.</returns>
        public static ModuleProgressData CreateNew(ModuleId module, string displayName, DateTime utcNow)
        {
            return new ModuleProgressData
            {
                moduleId = (int)module,
                moduleName = displayName ?? string.Empty,
                completed = false,
                score = 0,
                attempts = 1,
                completionTimeSeconds = 0f,
                lastPlayed = TimestampUtility.Format(utcNow)
            };
        }

        /// <summary>
        /// Forces every field into its legal range.
        /// </summary>
        /// <param name="utcNow">Current UTC time, used to backfill an absent timestamp.</param>
        /// <returns><c>true</c> if any field had to be repaired.</returns>
        public bool Sanitize(DateTime utcNow)
        {
            bool repaired = false;

            if (!ModuleIdExtensions.IsDefined(moduleId))
            {
                moduleId = (int)ModuleId.None;
                repaired = true;
            }

            moduleName ??= string.Empty;

            int clampedScore = Mathf.Clamp(score, 0, 100);
            if (clampedScore != score)
            {
                score = clampedScore;
                repaired = true;
            }

            if (attempts < 0)
            {
                attempts = 0;
                repaired = true;
            }

            // A completed module must have been attempted at least once, or the analytics derived
            // from these records would report a completion rate above one.
            if (completed && attempts == 0)
            {
                attempts = 1;
                repaired = true;
            }

            if (float.IsNaN(completionTimeSeconds) || float.IsInfinity(completionTimeSeconds) || completionTimeSeconds < 0f)
            {
                completionTimeSeconds = 0f;
                repaired = true;
            }

            if (!TimestampUtility.IsValid(lastPlayed))
            {
                lastPlayed = TimestampUtility.Format(utcNow);
                repaired = true;
            }

            return repaired;
        }
    }
}
