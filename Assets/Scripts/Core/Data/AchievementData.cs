using System;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Optional recognitions the player can earn (backend schema, "Achievement System").
    /// </summary>
    /// <remarks>
    /// Values are persisted and must never be renumbered. Achievements are deliberately tied to
    /// acts of understanding rather than time spent, so that they reinforce the PRD's learning
    /// objectives instead of rewarding grinding.
    /// </remarks>
    public enum AchievementId
    {
        /// <summary>Sentinel used for "no achievement".</summary>
        None = 0,

        /// <summary>Completed the introduction.</summary>
        FirstSteps = 1,

        /// <summary>Disabled and restored every sensory pathway in the presence module.</summary>
        SensoryScientist = 2,

        /// <summary>Examined all three hardware components.</summary>
        HardwareInspector = 3,

        /// <summary>Drove the latency slider to its maximum and back to zero.</summary>
        LatencyExpert = 4,

        /// <summary>Deliberately induced a vestibular conflict, then resolved it.</summary>
        ConflictResolver = 5,

        /// <summary>Compared interaction with haptics enabled and disabled.</summary>
        HapticsBeliever = 6,

        /// <summary>Visited every portal in the applications gallery.</summary>
        WorldTraveller = 7,

        /// <summary>Completed every module.</summary>
        PresenceUnderstood = 8,

        /// <summary>Answered every knowledge check correctly on the first attempt.</summary>
        PerfectRecall = 9
    }

    /// <summary>
    /// Earned state of a single achievement (backend schema, "Achievement System").
    /// </summary>
    [Serializable]
    public sealed class AchievementData
    {
        /// <summary>Integer value of the <see cref="AchievementId"/>.</summary>
        public int achievementId = (int)AchievementId.None;

        /// <summary>
        /// Display name, denormalised so an exported save is legible without the application.
        /// </summary>
        public string name = string.Empty;

        /// <summary>Whether the achievement has been earned.</summary>
        public bool earned;

        /// <summary>ISO-8601 UTC timestamp of the moment it was earned, or empty if unearned.</summary>
        public string earnedDate = string.Empty;

        /// <summary>Gets or sets <see cref="achievementId"/> as a strongly typed value.</summary>
        public AchievementId Achievement
        {
            get => EnumGuard.ToEnum(achievementId, AchievementId.None);
            set => achievementId = (int)value;
        }

        /// <summary>Gets the moment it was earned, or <see cref="DateTime.MinValue"/> if unearned.</summary>
        public DateTime EarnedDateUtc => TimestampUtility.Parse(earnedDate);

        /// <summary>
        /// Creates an earned record.
        /// </summary>
        /// <param name="achievement">The achievement earned.</param>
        /// <param name="displayName">Display name for export.</param>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns>A populated, earned record.</returns>
        public static AchievementData CreateEarned(AchievementId achievement, string displayName, DateTime utcNow)
        {
            return new AchievementData
            {
                achievementId = (int)achievement,
                name = displayName ?? string.Empty,
                earned = true,
                earnedDate = TimestampUtility.Format(utcNow)
            };
        }

        /// <summary>
        /// Forces the record into a self-consistent state.
        /// </summary>
        /// <returns><c>true</c> if anything had to be repaired.</returns>
        public bool Sanitize()
        {
            bool repaired = false;

            if (!EnumGuard.IsDefined<AchievementId>(achievementId))
            {
                achievementId = (int)AchievementId.None;
                repaired = true;
            }

            name ??= string.Empty;

            if (earned && !TimestampUtility.IsValid(earnedDate))
            {
                // Earned but undated. The achievement is real, so the flag is kept and the date is
                // cleared rather than revoking something the player actually did.
                earnedDate = string.Empty;
                repaired = true;
            }

            if (!earned && !string.IsNullOrEmpty(earnedDate))
            {
                earnedDate = string.Empty;
                repaired = true;
            }

            return repaired;
        }
    }
}
