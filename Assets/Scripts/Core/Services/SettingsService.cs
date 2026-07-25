using System;
using VRSimulation.Core.Data;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Core.Services
{
    /// <summary>
    /// Save-backed implementation of <see cref="ISettingsService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Holds no state of its own. The authoritative values live in
    /// <see cref="ISaveService.Data"/>, and this class exists to guard access to them: validating
    /// every write, handing out copies rather than references, persisting changes, and telling
    /// interested systems that something moved.
    /// </para>
    /// <para>
    /// Every change is written with <c>force</c> set, bypassing the save service's coalescing
    /// window. Two reasons. First, honesty: a coalesced write reports success without touching the
    /// disk, so an unforced <see cref="Apply"/> would return <c>true</c> for a change that is not
    /// yet persisted and would be lost if the process were killed moments later. Second, weight:
    /// these are accessibility and comfort choices, and a player who calibrates their height and
    /// enables subtitles must not have to do it twice because the headset slept before the next
    /// autosave.
    /// </para>
    /// <para>
    /// The cost is one disk write per change, which is why continuous controls must debounce.
    /// A volume slider is expected to call <see cref="Apply"/> when the player releases it, not on
    /// every frame of the drag. That is a user interface concern and belongs there rather than
    /// being papered over by a persistence layer that quietly declines to persist.
    /// </para>
    /// </remarks>
    public sealed class SettingsService : ISettingsService
    {
        private readonly ISaveService saveService;
        private readonly IExperienceLogger logger;

        /// <inheritdoc />
        public event Action<UserSettingsData> SettingsChanged;

        /// <summary>
        /// Creates a settings service.
        /// </summary>
        /// <param name="saveService">Owner of the persisted settings. Must not be <c>null</c>.</param>
        /// <param name="logger">Diagnostics destination. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Any dependency is <c>null</c>.</exception>
        public SettingsService(ISaveService saveService, IExperienceLogger logger)
        {
            this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public UserSettingsData Current
        {
            get
            {
                UserSettingsData stored = saveService.Data?.settings;

                // A save that has not loaded yet, or one whose settings block was lost, still has
                // to yield something usable: a system asking for the comfort mode during startup
                // must not receive null and must not receive the least comfortable option.
                return stored == null ? UserSettingsData.CreateDefault() : stored.Clone();
            }
        }

        /// <inheritdoc />
        public void Initialize()
        {
            if (saveService.Data == null)
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    "Settings were initialised before the save loaded; falling back to defaults.");
                return;
            }

            if (saveService.Data.settings == null)
            {
                saveService.Data.settings = UserSettingsData.CreateDefault();
                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    "The save contained no settings block; comfort-first defaults were substituted.");
                return;
            }

            // The save service sanitises on load, so this is normally a no-op. It is repeated here
            // because Initialize is also the path taken when a test or tool constructs the service
            // over hand-built data that never went through a load.
            if (saveService.Data.settings.Sanitize())
            {
                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    "Stored settings contained invalid values which were repaired.");
            }
        }

        /// <inheritdoc />
        public bool Apply(UserSettingsData settings)
        {
            if (settings == null)
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    "Apply was called with no settings. Ignoring.");
                return false;
            }

            if (saveService.Data == null)
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    "Settings cannot be applied before the save has loaded.");
                return false;
            }

            // Copy first, so the caller keeps ownership of their instance and cannot mutate live
            // settings afterwards by holding on to the reference they passed in.
            UserSettingsData adopted = settings.Clone();

            if (adopted.Sanitize())
            {
                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    "Applied settings contained invalid values which were repaired before use.");
            }

            saveService.Data.settings = adopted;

            // Forced: see the class remarks. An unforced save may be coalesced and would report
            // success for a change that never reached the disk.
            bool persisted = saveService.Save(force: true);
            if (!persisted)
            {
                // The change is live in memory regardless. Reverting would be worse: the player
                // would see their accessibility choice silently undo itself.
                logger.Log(
                    LogSeverity.Warning,
                    LogCategory.Persistence,
                    "Settings changed but could not be written. They apply to this session only.");
            }

            SettingsChanged?.Invoke(adopted.Clone());
            return persisted;
        }

        /// <inheritdoc />
        public bool Modify(Action<UserSettingsData> mutate)
        {
            if (mutate == null)
            {
                logger.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    "Modify was called with no mutation. Ignoring.");
                return false;
            }

            UserSettingsData draft = Current;

            try
            {
                mutate(draft);
            }
            catch (Exception exception)
            {
                // A caller's callback must not be able to leave settings half-applied. The draft is
                // discarded and the live values are untouched.
                logger.LogException(
                    LogCategory.Persistence,
                    "A settings mutation threw; the change was discarded.",
                    exception);
                return false;
            }

            return Apply(draft);
        }

        /// <inheritdoc />
        public bool ResetToDefaults()
        {
            logger.Log(LogSeverity.Info, LogCategory.Persistence, "Settings reset to defaults.");
            return Apply(UserSettingsData.CreateDefault());
        }
    }
}
