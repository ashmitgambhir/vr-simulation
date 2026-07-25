using System;
using System.IO;
using UnityEngine;
using VRSimulation.Core.Data;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Core.Services
{
    /// <summary>
    /// Durable, corruption-resistant implementation of <see cref="ISaveService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes follow a stage-verify-backup-swap sequence rather than overwriting the save in place.
    /// Overwriting is the single most common cause of total save loss in shipped games: the process
    /// dies partway through the write and the only copy on disk is half a file. The sequence here
    /// guarantees that at every instant at least one complete, parseable file exists.
    /// </para>
    /// <para>
    /// Reads are correspondingly defensive. A file that is absent, oversized, unparseable, of an
    /// unsupported version, or internally inconsistent is handled by falling back to the backup and
    /// then to defaults, and every fallback is logged and reported through
    /// <see cref="SaveLoadOutcome"/>. Nothing is discarded silently.
    /// </para>
    /// <para>
    /// This class is deliberately not a <see cref="MonoBehaviour"/>. It has no per-frame behaviour
    /// and no scene presence, so keeping it a plain object lets it be constructed directly in a
    /// test with fake collaborators (TRD 23).
    /// </para>
    /// </remarks>
    public sealed class SaveService : ISaveService
    {
        private readonly IFileSystem fileSystem;
        private readonly IClock clock;
        private readonly IExperienceLogger logger;
        private readonly SaveMigrator migrator;

        private readonly string directoryPath;
        private readonly string savePath;
        private readonly string backupPath;
        private readonly string tempPath;

        /// <summary>Timestamp of the last completed write, for coalescing rapid saves.</summary>
        private float lastWriteTimeSeconds = float.NegativeInfinity;

        /// <summary>Whether a coalesced write is owed to disk.</summary>
        private bool hasPendingChanges;

        /// <inheritdoc />
        public SaveData Data { get; private set; }

        /// <inheritdoc />
        public bool IsLoaded { get; private set; }

        /// <inheritdoc />
        public bool IsPersisting { get; private set; } = true;

        /// <inheritdoc />
        public event Action Saved;

        /// <inheritdoc />
        public event Action<string> SaveFailed;

        /// <summary>
        /// Creates a save service.
        /// </summary>
        /// <param name="fileSystem">File operations. Must not be <c>null</c>.</param>
        /// <param name="clock">Time source. Must not be <c>null</c>.</param>
        /// <param name="logger">Diagnostics destination. Must not be <c>null</c>.</param>
        /// <param name="rootPath">
        /// Directory that will contain <see cref="SaveConstants.DataDirectoryName"/>. In the
        /// application this is <c>Application.persistentDataPath</c>; tests supply a fake root.
        /// </param>
        /// <exception cref="ArgumentNullException">Any required dependency is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="rootPath"/> is empty.</exception>
        public SaveService(IFileSystem fileSystem, IClock clock, IExperienceLogger logger, string rootPath)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path must be supplied.", nameof(rootPath));
            }

            migrator = new SaveMigrator(logger);

            directoryPath = Path.Combine(rootPath, SaveConstants.DataDirectoryName);
            savePath = Path.Combine(directoryPath, SaveConstants.SaveFileName);
            backupPath = Path.Combine(directoryPath, SaveConstants.BackupFileName);
            tempPath = Path.Combine(directoryPath, SaveConstants.TempFileName);

            // Never null, even before Load, so that a system which starts early cannot dereference
            // a null save while the load is still in flight.
            Data = SaveData.CreateDefault(clock.UtcNow);
        }

        /// <summary>Gets the absolute path of the primary save file. Exposed for diagnostics.</summary>
        public string SavePath => savePath;

        /// <inheritdoc />
        public SaveLoadOutcome Load()
        {
            if (!fileSystem.EnsureDirectory(directoryPath))
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    $"Could not create the save directory at '{directoryPath}'. " +
                    "The session will run without persistence.");

                Data = SaveData.CreateDefault(clock.UtcNow);
                IsLoaded = true;
                IsPersisting = false;
                return SaveLoadOutcome.FailedReadOnly;
            }

            // Attempt the primary file, then the backup. A stale backup is far better than nothing:
            // it costs the player their most recent module, not their whole session.
            if (TryLoadFrom(savePath, out SaveData primary, out bool primaryRepaired))
            {
                Data = primary;
                IsLoaded = true;
                IsPersisting = true;

                if (!primaryRepaired)
                {
                    return SaveLoadOutcome.LoadedPrimary;
                }

                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    "The save file contained invalid values which were repaired. Writing the repaired copy back.");

                // Persist the repair so the same warning is not produced on every launch.
                WriteToDisk();
                return SaveLoadOutcome.RepairedPrimary;
            }

            logger.Log(
                LogSeverity.Warning,
                LogCategory.Persistence,
                $"The primary save at '{savePath}' could not be read. Attempting the backup.");

            if (TryLoadFrom(backupPath, out SaveData backup, out _))
            {
                Data = backup;
                IsLoaded = true;
                IsPersisting = true;

                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    "Recovered progress from the backup save. Recent progress may be missing.");

                // Promote the backup to primary immediately, so the next launch does not repeat
                // this recovery.
                //
                // The backup must NOT be refreshed as part of this write. The normal write sequence
                // copies the live primary over the backup first, but the primary is precisely the
                // file that was just found to be unreadable — copying it would destroy the only
                // good data in the pair. Were the process to die between that copy and the
                // subsequent swap, both files would be corrupt and the player's progress would be
                // gone. The backup already holds exactly the data being written, so there is
                // nothing to refresh.
                WriteToDisk(refreshBackup: false);
                return SaveLoadOutcome.RecoveredFromBackup;
            }

            Data = SaveData.CreateDefault(clock.UtcNow);
            IsLoaded = true;

            // As in the recovery path above, the backup is not refreshed here. Either this is a
            // genuine first launch and there is no primary to copy, or both files were unreadable
            // and copying the bad primary over the bad backup would only discard evidence of what
            // went wrong without helping the player.
            bool written = WriteToDisk(refreshBackup: false);
            IsPersisting = written;

            if (!written)
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    "No save could be read and a new one could not be written. " +
                    "The session will run without persistence.");
                return SaveLoadOutcome.FailedReadOnly;
            }

            logger.Log(LogSeverity.Info, LogCategory.Persistence, "Created a new save file.");
            return SaveLoadOutcome.CreatedNew;
        }

        /// <inheritdoc />
        public bool Save(bool force = false)
        {
            if (!IsLoaded)
            {
                // Writing before loading would overwrite a real save with the placeholder defaults
                // created in the constructor.
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    "Save was requested before Load completed. Ignoring to avoid overwriting existing progress.");
                return false;
            }

            hasPendingChanges = true;

            float now = clock.UnscaledTimeSeconds;
            bool withinCooldown = now - lastWriteTimeSeconds < SaveConstants.MinimumAutoSaveIntervalSeconds;

            if (!force && withinCooldown)
            {
                // Coalesced. The change is still owed to disk and Flush will write it, so this is
                // reported as success: the caller's data is not lost, only deferred.
                return true;
            }

            return WriteToDisk();
        }

        /// <inheritdoc />
        public bool ResetProgress()
        {
            if (!IsLoaded)
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    "Reset was requested before Load completed. Ignoring.");
                return false;
            }

            Data.ResetProgress(clock.UtcNow);
            logger.Log(LogSeverity.Info, LogCategory.Persistence, "Progress reset. Settings preserved.");

            return Save(force: true);
        }

        /// <inheritdoc />
        public bool Flush()
        {
            if (!IsLoaded || !hasPendingChanges)
            {
                return true;
            }

            return WriteToDisk();
        }

        /// <summary>
        /// Reads, validates, migrates and repairs a save file.
        /// </summary>
        /// <param name="path">Absolute path to read.</param>
        /// <param name="result">Receives the loaded save on success.</param>
        /// <param name="repaired">Receives whether sanitisation had to change anything.</param>
        /// <returns><c>true</c> if a usable save was produced.</returns>
        private bool TryLoadFrom(string path, out SaveData result, out bool repaired)
        {
            result = null;
            repaired = false;

            if (!fileSystem.FileExists(path))
            {
                return false;
            }

            long size = fileSystem.GetFileSize(path);
            if (size <= 0)
            {
                // Zero length is the signature of a write that was interrupted before any data
                // reached the disk.
                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    $"'{path}' is empty and cannot be a valid save.");
                return false;
            }

            if (size > SaveConstants.MaxSaveFileBytes)
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    $"'{path}' is {size} bytes, beyond the {SaveConstants.MaxSaveFileBytes} byte limit. " +
                    "Treating it as corrupt rather than loading it.");
                return false;
            }

            if (!fileSystem.TryReadAllText(path, out string json) || string.IsNullOrWhiteSpace(json))
            {
                logger.Log(LogSeverity.Warning, LogCategory.Persistence, $"'{path}' could not be read.");
                return false;
            }

            SaveData parsed = Deserialize(json, path);
            if (parsed == null)
            {
                return false;
            }

            // TryMigrate performs its own range check and reports the specific reason a version was
            // refused. Guarding it with a separate IsSupported call would short-circuit past that
            // reporting, leaving "the save came from a newer build" indistinguishable from an
            // ordinary parse failure in the log.
            if (!migrator.TryMigrate(parsed))
            {
                return false;
            }

            repaired = parsed.Sanitize(clock.UtcNow);
            result = parsed;
            return true;
        }

        /// <summary>
        /// Parses save JSON, converting any parser failure into a <c>null</c> result.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonUtility.FromJson{T}(string)"/> raises <see cref="ArgumentException"/> for
        /// malformed input and returns <c>null</c> for the literal <c>null</c>, so both outcomes
        /// have to be handled to keep a hand-edited file from terminating the load.
        /// </remarks>
        /// <param name="json">Raw file contents.</param>
        /// <param name="path">Path, used only for diagnostics.</param>
        /// <returns>The parsed save, or <c>null</c> if the input was not valid save JSON.</returns>
        private SaveData Deserialize(string json, string path)
        {
            try
            {
                SaveData parsed = JsonUtility.FromJson<SaveData>(json);

                if (parsed == null)
                {
                    logger.Log(
                        LogSeverity.Warning,
                        LogCategory.Persistence,
                        $"'{path}' parsed to nothing and is not a valid save.");
                }

                return parsed;
            }
            catch (Exception exception)
            {
                logger.LogException(
                    LogCategory.Persistence,
                    $"'{path}' is not valid save JSON.",
                    exception);
                return null;
            }
        }

        /// <summary>
        /// Performs the atomic write sequence.
        /// </summary>
        /// <remarks>
        /// <para>The ordering is what provides the durability guarantee:</para>
        /// <list type="number">
        /// <item><description>Serialise in memory, so a serialisation failure never touches disk.</description></item>
        /// <item><description>Write to a temporary file and flush it.</description></item>
        /// <item><description>Read the temporary file back and re-parse it. A write that reported
        /// success but produced unparseable bytes is caught here, before it can replace a good
        /// file.</description></item>
        /// <item><description>Copy the current primary over the backup, so the last known good
        /// state survives whatever happens next.</description></item>
        /// <item><description>Move the temporary file over the primary.</description></item>
        /// </list>
        /// <para>
        /// An interruption before step five leaves the old primary intact; an interruption during
        /// step five leaves the backup intact. There is no instant at which both are incomplete.
        /// </para>
        /// </remarks>
        /// <param name="refreshBackup">
        /// Whether to copy the current primary over the backup before swapping. Pass <c>false</c>
        /// only when the primary is known to be unusable, as during recovery, where copying it
        /// would destroy the good backup.
        /// </param>
        /// <returns><c>true</c> if the state reached disk.</returns>
        private bool WriteToDisk(bool refreshBackup = true)
        {
            string json;

            try
            {
                Data.user.lastPlayed = TimestampUtility.Format(clock.UtcNow);
                json = JsonUtility.ToJson(Data, prettyPrint: true);
            }
            catch (Exception exception)
            {
                return ReportWriteFailure("The save could not be serialised.", exception);
            }

            if (string.IsNullOrEmpty(json))
            {
                return ReportWriteFailure("Serialising the save produced no output.", null);
            }

            if (!fileSystem.EnsureDirectory(directoryPath))
            {
                return ReportWriteFailure($"The save directory '{directoryPath}' is unavailable.", null);
            }

            if (!fileSystem.TryWriteAllText(tempPath, json))
            {
                return ReportWriteFailure("The staged save could not be written. Storage may be full.", null);
            }

            if (!VerifyStagedFile())
            {
                fileSystem.TryDelete(tempPath);
                return ReportWriteFailure("The staged save failed verification and was discarded.", null);
            }

            // Only meaningful once a primary exists; on first launch there is nothing to back up.
            if (refreshBackup && fileSystem.FileExists(savePath) && !fileSystem.TryCopy(savePath, backupPath))
            {
                // Not fatal. Proceeding costs the backup for this one write; refusing would cost
                // the player their progress. The primary is still complete at this instant.
                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    "The backup save could not be refreshed. Continuing with the write.");
            }

            if (!fileSystem.TryMove(tempPath, savePath))
            {
                fileSystem.TryDelete(tempPath);
                return ReportWriteFailure("The staged save could not replace the live save.", null);
            }

            lastWriteTimeSeconds = clock.UnscaledTimeSeconds;
            hasPendingChanges = false;
            IsPersisting = true;

            logger.Log(LogSeverity.Debug, LogCategory.Persistence, "Save written.");
            Saved?.Invoke();
            return true;
        }

        /// <summary>
        /// Confirms the staged file exists and re-parses, so that a silently truncated write is
        /// caught before it replaces a good file.
        /// </summary>
        /// <returns><c>true</c> if the staged file is a complete, parseable save.</returns>
        private bool VerifyStagedFile()
        {
            if (!fileSystem.FileExists(tempPath) || fileSystem.GetFileSize(tempPath) <= 0)
            {
                return false;
            }

            if (!fileSystem.TryReadAllText(tempPath, out string readBack) || string.IsNullOrWhiteSpace(readBack))
            {
                return false;
            }

            return Deserialize(readBack, tempPath) != null;
        }

        /// <summary>
        /// Records a write failure and notifies listeners.
        /// </summary>
        /// <param name="message">Description of what failed, suitable for display.</param>
        /// <param name="exception">Underlying exception, if any.</param>
        /// <returns>Always <c>false</c>, so callers can <c>return ReportWriteFailure(...)</c>.</returns>
        private bool ReportWriteFailure(string message, Exception exception)
        {
            IsPersisting = false;

            if (exception != null)
            {
                logger.LogException(LogCategory.Persistence, message, exception);
            }
            else
            {
                logger.Log(LogSeverity.Error, LogCategory.Persistence, message);
            }

            SaveFailed?.Invoke(message);
            return false;
        }
    }
}
