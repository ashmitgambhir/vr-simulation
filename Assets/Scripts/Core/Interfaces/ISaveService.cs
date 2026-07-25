using System;
using VRSimulation.Core.Data;

namespace VRSimulation.Core.Interfaces
{
    /// <summary>
    /// How a load attempt resolved.
    /// </summary>
    /// <remarks>
    /// The outcome is returned rather than being reduced to a boolean because the caller must be
    /// able to tell the player what happened. "Your progress was restored from a backup" and
    /// "starting fresh" are very different messages, and the backend schema's error recovery
    /// section requires that recovery actually be surfaced rather than performed silently.
    /// </remarks>
    public enum SaveLoadOutcome
    {
        /// <summary>The primary file was read and needed no repair.</summary>
        LoadedPrimary = 0,

        /// <summary>
        /// The primary file was read but contained values that had to be repaired. The repaired
        /// data has been written back.
        /// </summary>
        RepairedPrimary = 1,

        /// <summary>
        /// The primary file was missing or unreadable and the backup was used instead. Some recent
        /// progress may have been lost, and the player should be told.
        /// </summary>
        RecoveredFromBackup = 2,

        /// <summary>No usable file existed, so a default save was created. Expected on first launch.</summary>
        CreatedNew = 3,

        /// <summary>
        /// Neither file could be read and a default could not be written, most likely because
        /// storage is unavailable. The session continues in memory and progress will not persist.
        /// </summary>
        FailedReadOnly = 4
    }

    /// <summary>
    /// Reads and writes the player's save file (backend schema, "Save Manager Responsibilities").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations must never throw from these members. Storage on a standalone headset can be
    /// full, slow or briefly unavailable, and none of those may be allowed to terminate the
    /// experience mid-module. Failures are reported through return values and the logger.
    /// </para>
    /// <para>
    /// Implementations must never lose data on a failed write. The staged-then-moved sequence, plus
    /// a retained backup of the last known good file, means an interruption at any point leaves at
    /// least one complete file on disk.
    /// </para>
    /// </remarks>
    public interface ISaveService
    {
        /// <summary>
        /// Gets the in-memory save state. Never <c>null</c> after <see cref="Load"/> has run.
        /// </summary>
        /// <remarks>
        /// Callers may read this freely and may mutate it through the aggregate's own methods, but
        /// must call <see cref="Save"/> to persist. Mutating it does not write to disk implicitly,
        /// because a write per interaction would stutter the frame on a headset.
        /// </remarks>
        SaveData Data { get; }

        /// <summary>Gets whether <see cref="Load"/> has completed.</summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets whether writes are currently succeeding. <c>false</c> once a write has failed, so
        /// that the interface can warn the player their progress is not being kept.
        /// </summary>
        bool IsPersisting { get; }

        /// <summary>Raised after a successful write. Carries no payload; read <see cref="Data"/>.</summary>
        event Action Saved;

        /// <summary>
        /// Raised when a write fails. The argument describes what went wrong, for display and for
        /// logging.
        /// </summary>
        event Action<string> SaveFailed;

        /// <summary>
        /// Loads the save, recovering from the backup or creating defaults as needed.
        /// </summary>
        /// <remarks>Safe to call more than once; a second call reloads from disk.</remarks>
        /// <returns>How the load resolved.</returns>
        SaveLoadOutcome Load();

        /// <summary>
        /// Writes the current state to disk.
        /// </summary>
        /// <param name="force">
        /// When <c>false</c>, a write occurring sooner than
        /// <see cref="SaveConstants.MinimumAutoSaveIntervalSeconds"/> after the previous one is
        /// coalesced and reported as successful without touching the disk, which protects the frame
        /// budget when a player rapidly toggles a setting. When <c>true</c>, the write always
        /// happens; used at checkpoints and on shutdown where losing the write is unacceptable.
        /// </param>
        /// <returns><c>true</c> if the state is safely on disk when this returns.</returns>
        bool Save(bool force = false);

        /// <summary>
        /// Clears progress while preserving identity and preferences, then writes.
        /// </summary>
        /// <returns><c>true</c> if the reset was persisted.</returns>
        bool ResetProgress();

        /// <summary>
        /// Writes any coalesced pending change immediately.
        /// </summary>
        /// <remarks>
        /// Called when the application loses focus, is paused, or is quitting, and when the headset
        /// is removed. On Android the process can be terminated without further warning after a
        /// pause, so this is the last reliable opportunity to persist
        /// (PRD edge cases, "Player removes headset", "Low battery").
        /// </remarks>
        /// <returns><c>true</c> if nothing remains unwritten.</returns>
        bool Flush();
    }
}
