using System;
using System.Collections.Generic;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Root aggregate written to <c>SaveData.json</c> (backend schema, "Save File Structure").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field names and nesting reproduce the schema's documented JSON shape exactly, so a file
    /// written by this build satisfies the contract as published. <see cref="saveVersion"/> is the
    /// one addition: without a version stamp, a future format change has no safe migration path
    /// and the only remaining option is to discard player progress.
    /// </para>
    /// <para>
    /// This type owns the aggregate's invariants rather than exposing raw lists for callers to
    /// mutate. In particular the schema requires that <c>moduleId</c> be unique across module
    /// records; <see cref="UpsertModule"/> is the only supported way to write one, so that rule
    /// cannot be violated from a call site that forgot about it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// Version of the save format. Compared against
        /// <see cref="SaveConstants.CurrentSaveVersion"/> on load.
        /// </summary>
        public int saveVersion = SaveConstants.CurrentSaveVersion;

        /// <summary>Player identity and resume point.</summary>
        public UserProfile user = new UserProfile();

        /// <summary>Audio, comfort and accessibility preferences.</summary>
        public UserSettingsData settings = new UserSettingsData();

        /// <summary>Overall completion state.</summary>
        public ProgressData progress = new ProgressData();

        /// <summary>One record per module started. Keyed by <see cref="ModuleProgressData.moduleId"/>.</summary>
        public List<ModuleProgressData> modules = new List<ModuleProgressData>();

        /// <summary>Every knowledge check attempt, newest last.</summary>
        public List<QuizResultData> quizzes = new List<QuizResultData>();

        /// <summary>Resume point for narration in the module currently in progress.</summary>
        public NarrationProgressData narration = new NarrationProgressData();

        /// <summary>Earned achievements.</summary>
        public List<AchievementData> achievements = new List<AchievementData>();

        /// <summary>
        /// Creates the save a first-time player receives.
        /// </summary>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns>A valid save with default settings and no progress.</returns>
        public static SaveData CreateDefault(DateTime utcNow)
        {
            return new SaveData
            {
                saveVersion = SaveConstants.CurrentSaveVersion,
                user = UserProfile.CreateNew(utcNow),
                settings = UserSettingsData.CreateDefault(),
                progress = new ProgressData(),
                modules = new List<ModuleProgressData>(),
                quizzes = new List<QuizResultData>(),
                narration = new NarrationProgressData(),
                achievements = new List<AchievementData>()
            };
        }

        /// <summary>
        /// Finds the progress record for a module.
        /// </summary>
        /// <param name="module">The module to look up.</param>
        /// <returns>The record, or <c>null</c> if the module has never been entered.</returns>
        public ModuleProgressData FindModule(ModuleId module)
        {
            if (module == ModuleId.None || modules == null)
            {
                return null;
            }

            int target = (int)module;
            for (int i = 0; i < modules.Count; i++)
            {
                ModuleProgressData candidate = modules[i];
                if (candidate != null && candidate.moduleId == target)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Inserts or updates the progress record for a module.
        /// </summary>
        /// <remarks>
        /// This is the only supported way to write a module record. It enforces the schema's
        /// uniqueness rule by mutating the existing record in place when one is present, rather
        /// than appending a second record for the same module.
        /// </remarks>
        /// <param name="record">The record to store. Ignored if <c>null</c> or unidentified.</param>
        /// <returns><c>true</c> if a new record was inserted, <c>false</c> if an existing one was updated.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="record"/> is <c>null</c>.</exception>
        public bool UpsertModule(ModuleProgressData record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            modules ??= new List<ModuleProgressData>();

            ModuleProgressData existing = FindModule(record.Module);
            if (existing == null)
            {
                modules.Add(record);
                return true;
            }

            if (ReferenceEquals(existing, record))
            {
                // The caller mutated the stored instance directly and handed it back. Nothing to
                // copy, and copying field by field onto itself would be wasted work.
                return false;
            }

            existing.moduleName = record.moduleName;
            existing.completed = record.completed;
            existing.score = record.score;
            existing.attempts = record.attempts;
            existing.completionTimeSeconds = record.completionTimeSeconds;
            existing.lastPlayed = record.lastPlayed;
            return false;
        }

        /// <summary>
        /// Records a module as complete, updating both places that fact is stored.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the only supported way to complete a module. Completion is represented twice in
        /// the backend schema — as <see cref="ModuleProgressData.completed"/> on the module's own
        /// record, and as an entry in <see cref="ProgressData.completedModules"/> — and
        /// <see cref="Sanitize"/> treats the per-module record as authoritative when the two
        /// disagree.
        /// </para>
        /// <para>
        /// The consequence is that calling <see cref="ProgressData.MarkCompleted"/> on its own does
        /// not durably complete anything: with no matching record to corroborate it, the entry is
        /// demoted by the next sanitise and the progress silently disappears on the following load.
        /// Routing completion through the aggregate keeps the two representations in step by
        /// construction, so that failure cannot be reintroduced by a call site that only knew about
        /// one of them.
        /// </para>
        /// </remarks>
        /// <param name="module">The module completed.</param>
        /// <param name="displayName">Human readable name, from the module's definition asset.</param>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns><c>true</c> if this was the first completion of that module.</returns>
        public bool CompleteModule(ModuleId module, string displayName, DateTime utcNow)
        {
            if (module == ModuleId.None)
            {
                return false;
            }

            modules ??= new List<ModuleProgressData>();

            ModuleProgressData record = FindModule(module);
            if (record == null)
            {
                record = ModuleProgressData.CreateNew(module, displayName, utcNow);
                modules.Add(record);
            }
            else if (!string.IsNullOrEmpty(displayName))
            {
                record.moduleName = displayName;
            }

            record.completed = true;
            record.lastPlayed = TimestampUtility.Format(utcNow);

            progress ??= new ProgressData();
            return progress.MarkCompleted(module);
        }

        /// <summary>
        /// Appends a knowledge check result, trimming the oldest entries past the retention cap.
        /// </summary>
        /// <param name="result">The result to append. Ignored if <c>null</c>.</param>
        public void AddQuizResult(QuizResultData result)
        {
            if (result == null)
            {
                return;
            }

            quizzes ??= new List<QuizResultData>();
            quizzes.Add(result);

            TrimToCapacity(quizzes, SaveConstants.MaxRetainedQuizResults);
        }

        /// <summary>
        /// Determines whether an achievement has already been earned.
        /// </summary>
        /// <param name="achievement">The achievement to test.</param>
        /// <returns><c>true</c> if it is recorded as earned.</returns>
        public bool HasAchievement(AchievementId achievement)
        {
            if (achievement == AchievementId.None || achievements == null)
            {
                return false;
            }

            int target = (int)achievement;
            for (int i = 0; i < achievements.Count; i++)
            {
                AchievementData candidate = achievements[i];
                if (candidate != null && candidate.achievementId == target && candidate.earned)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Records an achievement, ignoring repeat awards.
        /// </summary>
        /// <param name="achievement">The achievement earned.</param>
        /// <param name="displayName">Display name for export.</param>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns><c>true</c> if this was the first time it was earned.</returns>
        public bool GrantAchievement(AchievementId achievement, string displayName, DateTime utcNow)
        {
            if (achievement == AchievementId.None || HasAchievement(achievement))
            {
                return false;
            }

            achievements ??= new List<AchievementData>();
            achievements.Add(AchievementData.CreateEarned(achievement, displayName, utcNow));
            return true;
        }

        /// <summary>
        /// Clears all progress while preserving identity and preferences.
        /// </summary>
        /// <remarks>
        /// Settings deliberately survive a progress reset. A player who has calibrated their height
        /// and chosen their comfort options should not have to do it again to replay the
        /// experience, and silently discarding an accessibility choice is a genuine harm.
        /// </remarks>
        /// <param name="utcNow">Current UTC time.</param>
        public void ResetProgress(DateTime utcNow)
        {
            progress = new ProgressData();
            modules = new List<ModuleProgressData>();
            quizzes = new List<QuizResultData>();
            achievements = new List<AchievementData>();
            narration = new NarrationProgressData();

            user ??= UserProfile.CreateNew(utcNow);
            user.CurrentModule = ModuleId.None;
            user.lastPlayed = TimestampUtility.Format(utcNow);
        }

        /// <summary>
        /// Repairs every part of the aggregate that is missing, out of range or contradictory.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Called after every load, before the data is allowed to reach any system. The design goal
        /// is that a partially damaged save is repaired into a playable one rather than discarded:
        /// losing a player's progress is a far worse outcome than losing one malformed field, and
        /// the backend schema's error recovery section calls for exactly this.
        /// </para>
        /// <para>
        /// Damage severe enough that the JSON will not parse at all is handled a level up, by the
        /// save service falling back to the backup file.
        /// </para>
        /// </remarks>
        /// <param name="utcNow">Current UTC time, used to backfill absent timestamps.</param>
        /// <returns><c>true</c> if anything had to be repaired, which the caller should log and re-save.</returns>
        public bool Sanitize(DateTime utcNow)
        {
            bool repaired = false;

            if (user == null)
            {
                user = UserProfile.CreateNew(utcNow);
                repaired = true;
            }
            else
            {
                repaired |= user.Sanitize(utcNow);
            }

            if (settings == null)
            {
                settings = UserSettingsData.CreateDefault();
                repaired = true;
            }
            else
            {
                repaired |= settings.Sanitize();
            }

            if (progress == null)
            {
                progress = new ProgressData();
                repaired = true;
            }
            else
            {
                repaired |= progress.Sanitize();
            }

            if (narration == null)
            {
                narration = new NarrationProgressData();
                repaired = true;
            }
            else
            {
                repaired |= narration.Sanitize();
            }

            repaired |= SanitizeModules(utcNow);
            repaired |= SanitizeQuizzes(utcNow);
            repaired |= SanitizeAchievements();
            repaired |= ReconcileProgressWithModules();

            return repaired;
        }

        /// <summary>
        /// Repairs module records, dropping unidentifiable and duplicate entries.
        /// </summary>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns><c>true</c> if anything changed.</returns>
        private bool SanitizeModules(DateTime utcNow)
        {
            bool repaired = false;

            if (modules == null)
            {
                modules = new List<ModuleProgressData>();
                return true;
            }

            var seen = new HashSet<int>();

            for (int i = modules.Count - 1; i >= 0; i--)
            {
                ModuleProgressData record = modules[i];

                if (record == null)
                {
                    modules.RemoveAt(i);
                    repaired = true;
                    continue;
                }

                repaired |= record.Sanitize(utcNow);

                // A record that no longer identifies a real module, or that duplicates one already
                // seen, cannot be merged safely. Later entries are dropped in favour of earlier
                // ones, which are scanned last because this loop runs backwards.
                if (record.Module == ModuleId.None || !seen.Add(record.moduleId))
                {
                    modules.RemoveAt(i);
                    repaired = true;
                }
            }

            return repaired;
        }

        /// <summary>
        /// Repairs quiz records and enforces the retention cap.
        /// </summary>
        /// <param name="utcNow">Current UTC time.</param>
        /// <returns><c>true</c> if anything changed.</returns>
        private bool SanitizeQuizzes(DateTime utcNow)
        {
            bool repaired = false;

            if (quizzes == null)
            {
                quizzes = new List<QuizResultData>();
                return true;
            }

            for (int i = quizzes.Count - 1; i >= 0; i--)
            {
                QuizResultData record = quizzes[i];

                if (record == null)
                {
                    quizzes.RemoveAt(i);
                    repaired = true;
                    continue;
                }

                repaired |= record.Sanitize(utcNow);
            }

            repaired |= TrimToCapacity(quizzes, SaveConstants.MaxRetainedQuizResults);
            return repaired;
        }

        /// <summary>
        /// Repairs achievement records, dropping unidentifiable and duplicate entries.
        /// </summary>
        /// <returns><c>true</c> if anything changed.</returns>
        private bool SanitizeAchievements()
        {
            bool repaired = false;

            if (achievements == null)
            {
                achievements = new List<AchievementData>();
                return true;
            }

            var seen = new HashSet<int>();

            for (int i = achievements.Count - 1; i >= 0; i--)
            {
                AchievementData record = achievements[i];

                if (record == null)
                {
                    achievements.RemoveAt(i);
                    repaired = true;
                    continue;
                }

                repaired |= record.Sanitize();

                if (record.Achievement == AchievementId.None || !seen.Add(record.achievementId))
                {
                    achievements.RemoveAt(i);
                    repaired = true;
                }
            }

            return repaired;
        }

        /// <summary>
        /// Makes the completed-module set agree with the per-module records.
        /// </summary>
        /// <remarks>
        /// The same fact is stored twice by the schema: as a flag on each module record and as a
        /// list on the progress object. They can disagree if a write was interrupted between the
        /// two updates. The per-module record wins, because it is the one written at the moment the
        /// player actually finished something.
        /// </remarks>
        /// <returns><c>true</c> if the progress set had to be corrected.</returns>
        private bool ReconcileProgressWithModules()
        {
            bool repaired = false;

            for (int i = 0; i < modules.Count; i++)
            {
                ModuleProgressData record = modules[i];
                if (record.completed && !progress.IsCompleted(record.Module))
                {
                    progress.MarkCompleted(record.Module);
                    repaired = true;
                }
            }

            // The reverse direction: a module listed as complete with no record, or with a record
            // that says otherwise, is demoted rather than trusted.
            for (int i = progress.completedModules.Count - 1; i >= 0; i--)
            {
                ModuleId module = ModuleIdExtensions.FromInt(progress.completedModules[i]);
                ModuleProgressData record = FindModule(module);

                if (record == null || !record.completed)
                {
                    progress.completedModules.RemoveAt(i);
                    repaired = true;
                }
            }

            if (repaired)
            {
                progress.Recalculate();
            }

            return repaired;
        }

        /// <summary>
        /// Drops the oldest entries until a list fits its retention cap.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="list">The list to trim in place.</param>
        /// <param name="capacity">Maximum number of entries to retain.</param>
        /// <returns><c>true</c> if any entry was removed.</returns>
        private static bool TrimToCapacity<T>(List<T> list, int capacity)
        {
            if (list == null || list.Count <= capacity)
            {
                return false;
            }

            list.RemoveRange(0, list.Count - capacity);
            return true;
        }
    }
}
