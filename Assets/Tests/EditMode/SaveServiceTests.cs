using System.IO;
using NUnit.Framework;
using UnityEngine;
using VRSimulation.Core.Data;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Interfaces;
using VRSimulation.Core.Services;
using VRSimulation.Tests.EditMode.Fakes;

namespace VRSimulation.Tests.EditMode
{
    /// <summary>
    /// Behavioural tests for <see cref="SaveService"/>, concentrating on the failure and recovery
    /// paths required by the backend schema's error recovery section.
    /// </summary>
    /// <remarks>
    /// The happy path is the least interesting thing here. What matters is that a corrupt file
    /// falls back to the backup, that a failed write never destroys a good file, and that none of
    /// it happens silently.
    /// </remarks>
    [TestFixture]
    public sealed class SaveServiceTests
    {
        private const string RootPath = "/fake-root";

        private FakeFileSystem fileSystem;
        private FakeClock clock;
        private RecordingLogger logger;
        private SaveService service;

        private string DirectoryPath => Path.Combine(RootPath, SaveConstants.DataDirectoryName);
        private string SavePath => Path.Combine(DirectoryPath, SaveConstants.SaveFileName);
        private string BackupPath => Path.Combine(DirectoryPath, SaveConstants.BackupFileName);
        private string TempPath => Path.Combine(DirectoryPath, SaveConstants.TempFileName);

        /// <summary>Builds a fresh service and collaborators before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            fileSystem = new FakeFileSystem();
            clock = new FakeClock();
            logger = new RecordingLogger();
            service = new SaveService(fileSystem, clock, logger, RootPath);
        }

        // -- First launch ------------------------------------------------------------------

        /// <summary>With no files present, a default save is created and written.</summary>
        [Test]
        public void Load_WithNoExistingFiles_CreatesAndPersistsDefaultSave()
        {
            SaveLoadOutcome outcome = service.Load();

            Assert.That(outcome, Is.EqualTo(SaveLoadOutcome.CreatedNew));
            Assert.That(service.IsLoaded, Is.True);
            Assert.That(service.IsPersisting, Is.True);
            Assert.That(fileSystem.FileExists(SavePath), Is.True, "A new save should have been written to disk.");
            Assert.That(service.Data.settings.comfortMode, Is.True, "Comfort mode must default on.");
        }

        /// <summary>A newly created profile gets a usable identity rather than empty fields.</summary>
        [Test]
        public void Load_WithNoExistingFiles_ProducesUsableProfile()
        {
            service.Load();

            Assert.That(service.Data.user.userId, Is.Not.Empty);
            Assert.That(service.Data.user.username, Is.EqualTo(SaveConstants.DefaultUsername));
            Assert.That(service.Data.user.CreatedAtUtc, Is.EqualTo(FakeClock.DefaultStart));
        }

        // -- Round trip --------------------------------------------------------------------

        /// <summary>State written by one service instance is read back by the next.</summary>
        [Test]
        public void Save_ThenReload_RestoresProgress()
        {
            service.Load();
            service.Data.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            service.Data.user.CurrentModule = ModuleId.Hardware;
            Assert.That(service.Save(force: true), Is.True);

            var reloaded = new SaveService(fileSystem, clock, logger, RootPath);
            SaveLoadOutcome outcome = reloaded.Load();

            Assert.That(outcome, Is.EqualTo(SaveLoadOutcome.LoadedPrimary));
            Assert.That(reloaded.Data.progress.IsCompleted(ModuleId.Presence), Is.True);
            Assert.That(reloaded.Data.user.CurrentModule, Is.EqualTo(ModuleId.Hardware));
        }

        // -- Corruption recovery -----------------------------------------------------------

        /// <summary>Unparseable primary with a good backup recovers rather than losing progress.</summary>
        [Test]
        public void Load_WithCorruptPrimaryAndValidBackup_RecoversFromBackup()
        {
            SaveData good = SaveData.CreateDefault(clock.UtcNow);
            good.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            fileSystem.SeedFile(BackupPath, JsonUtility.ToJson(good, true));
            fileSystem.SeedFile(SavePath, "{ this is not valid json");

            SaveLoadOutcome outcome = service.Load();

            Assert.That(outcome, Is.EqualTo(SaveLoadOutcome.RecoveredFromBackup));
            Assert.That(service.Data.progress.IsCompleted(ModuleId.Presence), Is.True,
                "Progress from the backup should have survived.");
            Assert.That(logger.HasEntryAtLeast(LogSeverity.Warning), Is.True,
                "Recovery must be reported, never silent.");
        }

        /// <summary>After recovering, the backup is promoted so the next launch is clean.</summary>
        [Test]
        public void Load_AfterRecovery_RewritesPrimaryFromBackup()
        {
            SaveData good = SaveData.CreateDefault(clock.UtcNow);
            good.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            fileSystem.SeedFile(BackupPath, JsonUtility.ToJson(good, true));
            fileSystem.SeedFile(SavePath, "corrupt");

            service.Load();

            var reloaded = new SaveService(fileSystem, clock, logger, RootPath);
            Assert.That(reloaded.Load(), Is.EqualTo(SaveLoadOutcome.LoadedPrimary),
                "The repaired primary should load cleanly on the following launch.");
        }

        /// <summary>
        /// Recovery must not overwrite the good backup with the corrupt primary.
        /// </summary>
        /// <remarks>
        /// Regression test. The ordinary write sequence refreshes the backup from the live primary
        /// before swapping, which is correct during normal play but catastrophic during recovery:
        /// the primary is the file that was just found unreadable. Refreshing from it would leave
        /// both copies corrupt if the process died before the swap completed, losing everything.
        /// </remarks>
        [Test]
        public void Load_WhenRecovering_DoesNotOverwriteBackupWithCorruptPrimary()
        {
            SaveData good = SaveData.CreateDefault(clock.UtcNow);
            good.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            fileSystem.SeedFile(BackupPath, JsonUtility.ToJson(good, true));
            fileSystem.SeedFile(SavePath, "{ corrupt");

            service.Load();

            string backupAfter = fileSystem.PeekFile(BackupPath);
            Assert.That(backupAfter, Is.Not.Null);
            Assert.That(backupAfter.Contains("corrupt"), Is.False,
                "The corrupt primary must never be copied over the good backup.");

            SaveData reparsedBackup = JsonUtility.FromJson<SaveData>(backupAfter);
            Assert.That(reparsedBackup, Is.Not.Null, "The backup must remain parseable after recovery.");
            Assert.That(reparsedBackup.progress.completedModules, Is.Not.Empty,
                "The backup must still hold the recovered progress.");
        }

        /// <summary>Both files unreadable is survivable: defaults, and a clear report.</summary>
        [Test]
        public void Load_WithBothFilesCorrupt_FallsBackToDefaults()
        {
            fileSystem.SeedFile(SavePath, "corrupt");
            fileSystem.SeedFile(BackupPath, "also corrupt");

            SaveLoadOutcome outcome = service.Load();

            Assert.That(outcome, Is.EqualTo(SaveLoadOutcome.CreatedNew));
            Assert.That(service.Data, Is.Not.Null);
            Assert.That(logger.HasEntryAtLeast(LogSeverity.Warning), Is.True);
        }

        /// <summary>A zero-length file is the signature of an interrupted write and is rejected.</summary>
        [Test]
        public void Load_WithEmptyPrimary_IsTreatedAsCorrupt()
        {
            fileSystem.SeedFile(SavePath, string.Empty);

            Assert.That(service.Load(), Is.EqualTo(SaveLoadOutcome.CreatedNew));
        }

        /// <summary>Out-of-range values are repaired rather than reaching the rest of the app.</summary>
        [Test]
        public void Load_WithOutOfRangeValues_RepairsThemAndReportsIt()
        {
            SaveData damaged = SaveData.CreateDefault(clock.UtcNow);
            damaged.settings.masterVolume = 42f;
            damaged.settings.narrationSpeed = float.NaN;
            damaged.settings.snapTurning = true;
            damaged.settings.smoothTurning = true;
            fileSystem.SeedFile(SavePath, JsonUtility.ToJson(damaged, true));

            SaveLoadOutcome outcome = service.Load();

            Assert.That(outcome, Is.EqualTo(SaveLoadOutcome.RepairedPrimary));
            Assert.That(service.Data.settings.masterVolume, Is.InRange(0f, 1f));
            Assert.That(float.IsNaN(service.Data.settings.narrationSpeed), Is.False);
            Assert.That(service.Data.settings.Turning, Is.EqualTo(TurnMode.Snap),
                "A contradictory turning state must resolve to the comfort option.");
        }

        /// <summary>A save from a future build is refused rather than downgraded.</summary>
        [Test]
        public void Load_WithNewerSaveVersion_RefusesRatherThanDowngrading()
        {
            SaveData future = SaveData.CreateDefault(clock.UtcNow);
            future.saveVersion = SaveConstants.CurrentSaveVersion + 1;
            fileSystem.SeedFile(SavePath, JsonUtility.ToJson(future, true));

            SaveLoadOutcome outcome = service.Load();

            Assert.That(outcome, Is.EqualTo(SaveLoadOutcome.CreatedNew));
            Assert.That(logger.HasEntryAt(LogSeverity.Error), Is.True);
        }

        // -- Write durability ---------------------------------------------------------------

        /// <summary>A failed staged write leaves the existing good save untouched.</summary>
        [Test]
        public void Save_WhenStagedWriteFails_LeavesExistingSaveIntact()
        {
            service.Load();
            service.Data.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            service.Save(force: true);
            string before = fileSystem.PeekFile(SavePath);

            fileSystem.WriteFailurePaths.Add(TempPath);
            service.Data.CompleteModule(ModuleId.Hardware, "Hardware", clock.UtcNow);
            bool saved = service.Save(force: true);

            Assert.That(saved, Is.False);
            Assert.That(service.IsPersisting, Is.False, "A failed write must be visible to the interface.");
            Assert.That(fileSystem.PeekFile(SavePath), Is.EqualTo(before),
                "The previously good save must not be damaged by a failed write.");
        }

        /// <summary>A failed swap leaves the old save in place and cleans up the staged file.</summary>
        [Test]
        public void Save_WhenMoveFails_PreservesPreviousSaveAndRemovesTemp()
        {
            service.Load();
            string before = fileSystem.PeekFile(SavePath);

            fileSystem.MoveFailurePaths.Add(TempPath);
            service.Data.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            bool saved = service.Save(force: true);

            Assert.That(saved, Is.False);
            Assert.That(fileSystem.PeekFile(SavePath), Is.EqualTo(before));
            Assert.That(fileSystem.FileExists(TempPath), Is.False, "The staged file must not be left behind.");
        }

        /// <summary>A failed backup refresh is a warning, not a reason to lose the write.</summary>
        [Test]
        public void Save_WhenBackupCopyFails_StillCompletesTheWrite()
        {
            service.Load();
            fileSystem.CopyFailurePaths.Add(SavePath);

            service.Data.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);

            Assert.That(service.Save(force: true), Is.True);
            Assert.That(logger.HasEntryAt(LogSeverity.Warning), Is.True);
        }

        /// <summary>A write raises the failure event so the interface can warn the player.</summary>
        [Test]
        public void Save_OnFailure_RaisesSaveFailedEvent()
        {
            service.Load();

            string reported = null;
            service.SaveFailed += message => reported = message;

            fileSystem.WriteFailurePaths.Add(TempPath);
            service.Save(force: true);

            Assert.That(reported, Is.Not.Null.And.Not.Empty);
        }

        // -- Coalescing ---------------------------------------------------------------------

        /// <summary>Rapid saves are coalesced so a button masher cannot stutter the frame.</summary>
        [Test]
        public void Save_CalledRepeatedlyWithinCooldown_WritesOnlyOnce()
        {
            service.Load();
            int baseline = fileSystem.WriteCount;

            for (int i = 0; i < 20; i++)
            {
                service.Save();
            }

            Assert.That(fileSystem.WriteCount, Is.EqualTo(baseline),
                "Writes inside the cooldown window must be coalesced.");
        }

        /// <summary>A coalesced change is not lost; the flush writes it.</summary>
        [Test]
        public void Flush_AfterCoalescedSaves_PersistsPendingChange()
        {
            service.Load();
            service.Data.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            service.Save();

            Assert.That(service.Flush(), Is.True);

            var reloaded = new SaveService(fileSystem, clock, logger, RootPath);
            reloaded.Load();
            Assert.That(reloaded.Data.progress.IsCompleted(ModuleId.Presence), Is.True,
                "A coalesced change must survive the flush.");
        }

        /// <summary>Once the cooldown elapses, a save writes again.</summary>
        [Test]
        public void Save_AfterCooldownElapses_WritesAgain()
        {
            service.Load();
            int baseline = fileSystem.WriteCount;

            service.Save();
            clock.Advance(SaveConstants.MinimumAutoSaveIntervalSeconds + 0.1f);
            service.Save();

            Assert.That(fileSystem.WriteCount, Is.GreaterThan(baseline));
        }

        // -- Ordering and storage failure ---------------------------------------------------

        /// <summary>Saving before loading must not overwrite an existing file with placeholders.</summary>
        [Test]
        public void Save_BeforeLoad_IsRefused()
        {
            SaveData existing = SaveData.CreateDefault(clock.UtcNow);
            existing.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            string original = JsonUtility.ToJson(existing, true);
            fileSystem.SeedFile(SavePath, original);

            bool saved = service.Save(force: true);

            Assert.That(saved, Is.False);
            Assert.That(fileSystem.PeekFile(SavePath), Is.EqualTo(original),
                "An unloaded service must never overwrite real progress.");
            Assert.That(logger.HasEntryAt(LogSeverity.Error), Is.True);
        }

        /// <summary>Unavailable storage degrades to an in-memory session rather than crashing.</summary>
        [Test]
        public void Load_WhenStorageUnavailable_DegradesToReadOnlySession()
        {
            fileSystem.DirectoryCreationFails = true;

            SaveLoadOutcome outcome = service.Load();

            Assert.That(outcome, Is.EqualTo(SaveLoadOutcome.FailedReadOnly));
            Assert.That(service.Data, Is.Not.Null, "The session must still be playable.");
            Assert.That(service.IsPersisting, Is.False);
            Assert.That(logger.HasEntryAt(LogSeverity.Error), Is.True);
        }

        // -- Reset ---------------------------------------------------------------------------

        /// <summary>Resetting progress must not discard accessibility choices.</summary>
        [Test]
        public void ResetProgress_PreservesSettingsAndIdentity()
        {
            service.Load();
            service.Data.settings.calibratedHeightMeters = 1.42f;
            service.Data.settings.ColorVision = ColorVisionMode.Deuteranopia;
            service.Data.CompleteModule(ModuleId.Presence, "Presence", clock.UtcNow);
            string userId = service.Data.user.userId;

            Assert.That(service.ResetProgress(), Is.True);

            Assert.That(service.Data.progress.completedModules, Is.Empty);
            Assert.That(service.Data.user.userId, Is.EqualTo(userId));
            Assert.That(service.Data.settings.calibratedHeightMeters, Is.EqualTo(1.42f).Within(0.001f),
                "Height calibration must survive a progress reset.");
            Assert.That(service.Data.settings.ColorVision, Is.EqualTo(ColorVisionMode.Deuteranopia),
                "An accessibility choice must never be silently discarded.");
        }

        // -- Guard rails ----------------------------------------------------------------------

        /// <summary>Missing dependencies fail loudly at construction, not later at a call site.</summary>
        [Test]
        public void Constructor_WithMissingDependencies_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new SaveService(null, clock, logger, RootPath));

            Assert.Throws<System.ArgumentNullException>(
                () => new SaveService(fileSystem, null, logger, RootPath));

            Assert.Throws<System.ArgumentNullException>(
                () => new SaveService(fileSystem, clock, null, RootPath));

            Assert.Throws<System.ArgumentException>(
                () => new SaveService(fileSystem, clock, logger, string.Empty));
        }

        /// <summary>Data is available before Load so early systems cannot dereference null.</summary>
        [Test]
        public void Data_BeforeLoad_IsNotNull()
        {
            Assert.That(service.Data, Is.Not.Null);
            Assert.That(service.IsLoaded, Is.False);
        }
    }
}
