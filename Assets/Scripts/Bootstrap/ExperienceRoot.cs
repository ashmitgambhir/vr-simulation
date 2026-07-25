using UnityEngine;
using VRSimulation.Core.Data;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Interfaces;
using VRSimulation.Core.Services;
using VRSimulation.Utilities;

namespace VRSimulation.Bootstrap
{
    /// <summary>
    /// Composition root: constructs the application's services and owns their lifetime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class does one thing. It builds the object graph and manages the application lifecycle
    /// events that the graph must react to. It contains no gameplay behaviour, no interaction
    /// logic, no audio mixing and no interface code, and it must stay that way — a composition root
    /// that starts doing work is how a project acquires the God Object this one replaced, where a
    /// single component owned save data, settings, audio, interaction, scene flow and the player
    /// all at once.
    /// </para>
    /// <para>
    /// Services are constructed explicitly rather than resolved by a container. The graph is small
    /// and the wiring is readable top to bottom, so a dependency injection framework would add a
    /// dependency, a learning curve and reflection cost for no benefit — which is exactly the sort
    /// of unnecessary framework TRD 2 asks the project to avoid.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder.Bootstrap)]
    [DisallowMultipleComponent]
    public sealed class ExperienceRoot : MonoBehaviour
    {
        /// <summary>
        /// The live root, or <c>null</c> before startup.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A single static entry point is a deliberate exception to this project's preference for
        /// injected dependencies. Unity instantiates components from scene data, so a system loaded
        /// as part of a module scene has no constructor through which anything could be passed to
        /// it; without a known entry point every such system would need a serialised reference
        /// wired by hand in every scene, which is both laborious and easy to leave null.
        /// </para>
        /// <para>
        /// The exposure is kept as narrow as possible: this is the only static in the project, it
        /// is set once, and it hands out interfaces rather than concrete types so that consumers
        /// remain testable against fakes.
        /// </para>
        /// </remarks>
        public static ExperienceRoot Instance { get; private set; }

        [Header("Diagnostics")]
        [SerializeField]
        [Tooltip("Overrides the log severity floor. Leave unchecked to use the build's default: " +
                 "verbose in the editor and development builds, warnings and errors in release.")]
        private bool overrideLogSeverity;

        [SerializeField]
        [Tooltip("Severity floor applied when the override above is enabled.")]
        private LogSeverity logSeverityOverride = LogSeverity.Debug;

        /// <summary>Gets the structured logger. Never <c>null</c> after <c>Awake</c>.</summary>
        public IExperienceLogger Logger { get; private set; }

        /// <summary>Gets the save service. Never <c>null</c> after <c>Awake</c>.</summary>
        public ISaveService SaveService { get; private set; }

        /// <summary>Gets the settings service. Never <c>null</c> after <c>Awake</c>.</summary>
        public ISettingsService Settings { get; private set; }

        /// <summary>Gets how the save resolved on startup, for the interface to report to the player.</summary>
        public SaveLoadOutcome SaveOutcome { get; private set; }

        /// <summary>Gets whether startup completed and the services are usable.</summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Builds the service graph and loads persisted state.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Reached when a scene containing a second root is loaded additively, or when the
                // bootstrap scene is re-entered. The newcomer removes itself rather than replacing
                // the live root, which would orphan every reference already handed out and discard
                // unsaved state.
                Instance.Logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Bootstrap,
                    $"A second {nameof(ExperienceRoot)} was loaded and has been discarded. " +
                    "Only the bootstrap scene should contain one.");

                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Compose();
            LoadPersistentState();

            IsReady = true;
            Logger.Log(LogSeverity.Info, LogCategory.Bootstrap, "Experience root ready.");
        }

        /// <summary>
        /// Constructs the service graph.
        /// </summary>
        /// <remarks>
        /// Ordered by dependency: the logger first because every other service reports through it,
        /// then the platform adapters, then the services built on them.
        /// </remarks>
        private void Compose()
        {
            Logger = overrideLogSeverity
                ? new UnityExperienceLogger(logSeverityOverride)
                : new UnityExperienceLogger();

            IFileSystem fileSystem = new UnityFileSystem(Logger);
            IClock clock = new UnityClock();

            SaveService = new SaveService(fileSystem, clock, Logger, Application.persistentDataPath);
            Settings = new SettingsService(SaveService, Logger);
        }

        /// <summary>
        /// Loads the save and adopts its settings.
        /// </summary>
        private void LoadPersistentState()
        {
            SaveOutcome = SaveService.Load();
            Settings.Initialize();

            // Outcomes that cost the player something are surfaced rather than merely logged, so
            // the interface can tell them what happened. The backend schema's error recovery
            // section requires recovery to be visible, not silent.
            switch (SaveOutcome)
            {
                case SaveLoadOutcome.RecoveredFromBackup:
                    Logger.Log(
                        LogSeverity.Warning,
                        LogCategory.Bootstrap,
                        "Progress was restored from a backup. The player should be told that recent progress may be missing.");
                    break;

                case SaveLoadOutcome.FailedReadOnly:
                    Logger.Log(
                        LogSeverity.Error,
                        LogCategory.Bootstrap,
                        "Storage is unavailable. The experience is playable but progress will not be kept.");
                    break;
            }
        }

        /// <summary>
        /// Persists pending changes when the application is backgrounded.
        /// </summary>
        /// <remarks>
        /// On Android the process may be terminated after a pause without any further callback, so
        /// this is the last reliable opportunity to write. It fires when the player removes the
        /// headset, when the guardian is exited into the passthrough shell, and when the system
        /// dialog appears on low battery — every one of the interruption cases the PRD calls out.
        /// </remarks>
        /// <param name="isPaused"><c>true</c> when the application is losing the foreground.</param>
        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                FlushPendingState("application paused");
            }
        }

        /// <summary>
        /// Persists pending changes when focus is lost.
        /// </summary>
        /// <remarks>
        /// Focus loss and pause do not reliably both fire, and which one arrives varies by platform
        /// and by how the player left. Handling both is redundant by design; the flush is cheap and
        /// does nothing when there is nothing owed to disk.
        /// </remarks>
        /// <param name="hasFocus"><c>false</c> when the application is losing focus.</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                FlushPendingState("focus lost");
            }
        }

        /// <summary>
        /// Persists pending changes during an orderly shutdown.
        /// </summary>
        private void OnApplicationQuit()
        {
            FlushPendingState("application quitting");
        }

        /// <summary>
        /// Writes any coalesced change, reporting the reason for diagnostics.
        /// </summary>
        /// <param name="reason">Why the flush was triggered.</param>
        private void FlushPendingState(string reason)
        {
            if (!IsReady || SaveService == null)
            {
                return;
            }

            if (!SaveService.Flush())
            {
                Logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    $"Failed to persist state on {reason}. Progress since the last successful write may be lost.");
                return;
            }

            Logger.Log(LogSeverity.Debug, LogCategory.Persistence, $"State flushed on {reason}.");
        }

        /// <summary>
        /// Releases the static entry point when the root is torn down.
        /// </summary>
        /// <remarks>
        /// Leaving a stale reference behind would let a subsequent play session in the editor
        /// resolve services belonging to a destroyed object, producing null reference exceptions
        /// that do not occur in a build and are correspondingly hard to reproduce.
        /// </remarks>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
