using VRSimulation.Core.Data;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Core.Services
{
    /// <summary>
    /// Brings a save written by an older build up to the current format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only one format version exists today, so this class currently has nothing to transform. It
    /// exists anyway because the alternative — adding migration once a second version is needed —
    /// arrives too late. By then there are saves in the field written by a build that had no
    /// version stamp and no migration path, and the only available remedy is to discard them.
    /// </para>
    /// <para>
    /// Most schema changes will not need a step here at all. Adding a field is handled by the
    /// deserialiser leaving it at its default and <c>Sanitize</c> repairing it, which is why
    /// additive change is strongly preferred. A step is required only when the *meaning* of
    /// existing data changes, for example if module identifiers were ever renumbered.
    /// </para>
    /// </remarks>
    public sealed class SaveMigrator
    {
        private readonly IExperienceLogger logger;

        /// <summary>
        /// Creates a migrator.
        /// </summary>
        /// <param name="logger">Destination for migration reporting. Must not be <c>null</c>.</param>
        public SaveMigrator(IExperienceLogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Migrates a save in place to <see cref="SaveConstants.CurrentSaveVersion"/>.
        /// </summary>
        /// <remarks>
        /// This method owns the version range check as well as the transformation. An earlier
        /// revision exposed a separate <c>IsSupported</c> predicate which callers used to guard
        /// this one; because that guard short-circuited, an out-of-range version was rejected
        /// without the reason ever reaching the log. Keeping both responsibilities here means the
        /// refusal and its explanation cannot become separated again.
        /// </remarks>
        /// <param name="data">The freshly deserialised save. Must not be <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> if the save is now at the current version, <c>false</c> if it came from a
        /// build newer than this one and cannot be safely downgraded.
        /// </returns>
        public bool TryMigrate(SaveData data)
        {
            if (data == null)
            {
                return false;
            }

            if (data.saveVersion == SaveConstants.CurrentSaveVersion)
            {
                return true;
            }

            if (data.saveVersion > SaveConstants.CurrentSaveVersion)
            {
                // A file from a newer build may contain fields whose meaning this build does not
                // know. Guessing risks corrupting real progress, so it is refused and the caller
                // falls back rather than writing over it.
                logger?.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    $"Save version {data.saveVersion} is newer than this build supports " +
                    $"({SaveConstants.CurrentSaveVersion}). Refusing to downgrade.");
                return false;
            }

            if (data.saveVersion < SaveConstants.MinimumSupportedSaveVersion)
            {
                logger?.Log(
                    LogSeverity.Error,
                    LogCategory.Persistence,
                    $"Save version {data.saveVersion} is older than the minimum supported version " +
                    $"({SaveConstants.MinimumSupportedSaveVersion}).");
                return false;
            }

            // Reserved for future steps. Each will take the form:
            //
            //   if (data.saveVersion == 1) { MigrateV1ToV2(data); data.saveVersion = 2; }
            //
            // applied in ascending order so that a save can advance across several versions in one
            // pass. Every step must be covered by a test that starts from a real file captured from
            // the shipping build it belongs to.

            logger?.Log(
                LogSeverity.Info,
                LogCategory.Persistence,
                $"Migrated save from version {data.saveVersion} to {SaveConstants.CurrentSaveVersion}.");

            data.saveVersion = SaveConstants.CurrentSaveVersion;
            return true;
        }
    }
}
