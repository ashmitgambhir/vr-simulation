namespace VRSimulation.Core.Data
{
    /// <summary>
    /// File names, format versions and validation limits for the persistence layer.
    /// </summary>
    /// <remarks>
    /// Every literal that the save system writes to disk lives here so that the on-disk contract
    /// described by the backend schema is stated once and cannot drift between the writer, the
    /// reader and the tests (TRD 25, "avoid magic numbers, hardcoded values").
    /// </remarks>
    public static class SaveConstants
    {
        /// <summary>Directory beneath <c>Application.persistentDataPath</c> holding all player data.</summary>
        public const string DataDirectoryName = "PersistentData";

        /// <summary>Primary save file (backend schema, "Folder Structure").</summary>
        public const string SaveFileName = "SaveData.json";

        /// <summary>
        /// Backup of the last known good save. Written before the primary file is overwritten so
        /// that a crash mid-write can never destroy both copies.
        /// </summary>
        public const string BackupFileName = "SaveData_Backup.json";

        /// <summary>
        /// Temporary file used to stage a write. The staged file is flushed and only then moved
        /// over the primary save, which makes the swap atomic on every platform we target.
        /// </summary>
        public const string TempFileName = "SaveData.tmp";

        /// <summary>Buffered analytics events (backend schema, "Folder Structure").</summary>
        public const string AnalyticsFileName = "Analytics.json";

        /// <summary>Display name used when the player has not chosen one.</summary>
        public const string DefaultUsername = "Guest";

        /// <summary>
        /// Version of the save *format*, not of the application. Incremented only when a change
        /// cannot be handled by additive field defaults, at which point
        /// <see cref="VRSimulation.Core.Services.SaveMigrator"/> gains a step for it.
        /// </summary>
        public const int CurrentSaveVersion = 1;

        /// <summary>
        /// Oldest format version this build can still read. Files older than this are treated as
        /// unreadable and the player is offered a fresh start rather than a corrupt session.
        /// </summary>
        public const int MinimumSupportedSaveVersion = 1;

        /// <summary>
        /// Upper bound on retained analytics events. Analytics are optional (backend schema) and
        /// must never grow without limit on a headset with finite storage; the oldest events are
        /// discarded first.
        /// </summary>
        public const int MaxRetainedAnalyticsEvents = 1000;

        /// <summary>
        /// Upper bound on retained quiz results. Generous enough to keep every attempt from a
        /// realistic session while bounding the file size.
        /// </summary>
        public const int MaxRetainedQuizResults = 500;

        /// <summary>
        /// Largest save file this build will attempt to parse, in bytes. A file larger than this
        /// is treated as corrupt rather than being loaded into memory on a memory constrained
        /// standalone headset.
        /// </summary>
        public const int MaxSaveFileBytes = 4 * 1024 * 1024;

        /// <summary>
        /// Minimum interval between automatic writes, in seconds. Prevents a player who rapidly
        /// toggles settings from issuing a write per frame (PRD edge case, "rapidly presses buttons").
        /// </summary>
        public const float MinimumAutoSaveIntervalSeconds = 2f;
    }
}
