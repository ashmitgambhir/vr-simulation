using System;
using System.IO;
using NUnit.Framework;
using VRSimulation.Core.Data;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Core.Services;
using VRSimulation.Tests.EditMode.Fakes;

namespace VRSimulation.Tests.EditMode
{
    /// <summary>
    /// Behavioural tests for <see cref="SettingsService"/>.
    /// </summary>
    /// <remarks>
    /// These run against a real <see cref="SaveService"/> over a fake file system rather than a
    /// mocked save service, because the behaviour that matters most here is the interaction between
    /// the two: that a setting survives a round trip, and that a failed write still leaves the
    /// player's accessibility choice in force for the session.
    /// </remarks>
    [TestFixture]
    public sealed class SettingsServiceTests
    {
        private const string RootPath = "/fake-root";

        private FakeFileSystem fileSystem;
        private FakeClock clock;
        private RecordingLogger logger;
        private SaveService saveService;
        private SettingsService settings;

        private string TempPath =>
            Path.Combine(Path.Combine(RootPath, SaveConstants.DataDirectoryName), SaveConstants.TempFileName);

        /// <summary>Builds a loaded save service and a settings service over it.</summary>
        [SetUp]
        public void SetUp()
        {
            fileSystem = new FakeFileSystem();
            clock = new FakeClock();
            logger = new RecordingLogger();
            saveService = new SaveService(fileSystem, clock, logger, RootPath);
            saveService.Load();
            settings = new SettingsService(saveService, logger);
            settings.Initialize();
        }

        // -- Defaults -----------------------------------------------------------------------

        /// <summary>A new player receives the comfort-first defaults the PRD requires.</summary>
        [Test]
        public void Current_ForNewPlayer_IsComfortFirst()
        {
            UserSettingsData current = settings.Current;

            Assert.That(current.comfortMode, Is.True);
            Assert.That(current.EffectiveLocomotion, Is.EqualTo(LocomotionMode.Teleport));
            Assert.That(current.EffectiveTurning, Is.EqualTo(TurnMode.Snap));
            Assert.That(current.subtitles, Is.True);
        }

        // -- Copy semantics -----------------------------------------------------------------

        /// <summary>
        /// The returned settings are a copy, so a caller editing a draft cannot alter live state.
        /// </summary>
        [Test]
        public void Current_ReturnsCopy_SoCallerCannotMutateLiveState()
        {
            UserSettingsData draft = settings.Current;
            draft.masterVolume = 0.1f;
            draft.comfortMode = false;

            Assert.That(settings.Current.masterVolume, Is.Not.EqualTo(0.1f).Within(0.0001f),
                "Editing a returned draft must not change the live settings.");
            Assert.That(settings.Current.comfortMode, Is.True);
        }

        /// <summary>The applied instance is copied, so the caller's reference stays inert.</summary>
        [Test]
        public void Apply_CopiesInput_SoLaterCallerMutationIsIgnored()
        {
            UserSettingsData input = settings.Current;
            input.masterVolume = 0.5f;
            settings.Apply(input);

            input.masterVolume = 0.9f;

            Assert.That(settings.Current.masterVolume, Is.EqualTo(0.5f).Within(0.0001f),
                "Mutating the instance after applying it must not reach the live settings.");
        }

        // -- Validation ---------------------------------------------------------------------

        /// <summary>Invalid values are repaired rather than reaching the audio mixer.</summary>
        [Test]
        public void Apply_WithInvalidValues_RepairsThem()
        {
            UserSettingsData draft = settings.Current;
            draft.masterVolume = float.NaN;
            draft.narrationSpeed = 99f;
            draft.snapTurnDegrees = -5f;

            settings.Apply(draft);

            UserSettingsData result = settings.Current;
            Assert.That(float.IsNaN(result.masterVolume), Is.False);
            Assert.That(result.narrationSpeed, Is.InRange(
                SettingsDefaults.MinNarrationSpeed, SettingsDefaults.MaxNarrationSpeed));
            Assert.That(result.snapTurnDegrees, Is.InRange(
                SettingsDefaults.MinSnapTurnDegrees, SettingsDefaults.MaxSnapTurnDegrees));
            Assert.That(logger.HasEntryAtLeast(LogSeverity.Warning), Is.True,
                "Repairing a caller's values must be reported.");
        }

        /// <summary>A null apply is refused loudly rather than wiping settings.</summary>
        [Test]
        public void Apply_WithNull_IsRefusedAndReported()
        {
            Assert.That(settings.Apply(null), Is.False);
            Assert.That(settings.Current, Is.Not.Null);
            Assert.That(logger.HasEntryAt(LogSeverity.Error), Is.True);
        }

        // -- Change notification --------------------------------------------------------------

        /// <summary>Applying raises the change event with the new values.</summary>
        [Test]
        public void Apply_RaisesSettingsChangedWithNewValues()
        {
            UserSettingsData received = null;
            settings.SettingsChanged += value => received = value;

            UserSettingsData draft = settings.Current;
            draft.ColorVision = ColorVisionMode.Deuteranopia;
            settings.Apply(draft);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.ColorVision, Is.EqualTo(ColorVisionMode.Deuteranopia));
        }

        /// <summary>The event payload is a copy, so a subscriber cannot corrupt live settings.</summary>
        [Test]
        public void SettingsChanged_PayloadIsCopy()
        {
            UserSettingsData received = null;
            settings.SettingsChanged += value => received = value;
            settings.Apply(settings.Current);

            received.masterVolume = 0.02f;

            Assert.That(settings.Current.masterVolume, Is.Not.EqualTo(0.02f).Within(0.0001f));
        }

        // -- Modify ----------------------------------------------------------------------------

        /// <summary>The convenience path applies and persists a single change.</summary>
        [Test]
        public void Modify_AppliesAndPersistsChange()
        {
            Assert.That(settings.Modify(s => s.DominantHand = Handedness.Left), Is.True);

            Assert.That(settings.Current.DominantHand, Is.EqualTo(Handedness.Left));

            var reloaded = new SaveService(fileSystem, clock, logger, RootPath);
            reloaded.Load();
            Assert.That(reloaded.Data.settings.DominantHand, Is.EqualTo(Handedness.Left),
                "The change must survive a reload.");
        }

        /// <summary>A throwing callback leaves the live settings untouched.</summary>
        [Test]
        public void Modify_WhenCallbackThrows_DiscardsChangeAndReports()
        {
            float before = settings.Current.masterVolume;

            bool result = settings.Modify(s =>
            {
                s.masterVolume = 0.25f;
                throw new InvalidOperationException("simulated failure");
            });

            Assert.That(result, Is.False);
            Assert.That(settings.Current.masterVolume, Is.EqualTo(before).Within(0.0001f),
                "A failed mutation must not leave settings half-applied.");
            Assert.That(logger.HasEntryAt(LogSeverity.Error), Is.True);
        }

        /// <summary>A null mutation is refused rather than silently doing nothing.</summary>
        [Test]
        public void Modify_WithNullCallback_IsRefusedAndReported()
        {
            Assert.That(settings.Modify(null), Is.False);
            Assert.That(logger.HasEntryAt(LogSeverity.Error), Is.True);
        }

        // -- Persistence failure -----------------------------------------------------------------

        /// <summary>
        /// A failed write must not revert the player's choice; it applies for the session.
        /// </summary>
        [Test]
        public void Apply_WhenWriteFails_KeepsChangeLiveForSession()
        {
            fileSystem.WriteFailurePaths.Add(TempPath);

            bool persisted = settings.Modify(s => s.ColorVision = ColorVisionMode.Tritanopia);

            Assert.That(persisted, Is.False, "The caller must learn the write did not land.");
            Assert.That(settings.Current.ColorVision, Is.EqualTo(ColorVisionMode.Tritanopia),
                "An accessibility choice must never silently undo itself because storage failed.");
            Assert.That(logger.HasEntryAtLeast(LogSeverity.Warning), Is.True);
        }

        // -- Reset -------------------------------------------------------------------------------

        /// <summary>Resetting restores the comfort-first defaults.</summary>
        [Test]
        public void ResetToDefaults_RestoresComfortFirstValues()
        {
            settings.Modify(s =>
            {
                s.comfortMode = false;
                s.Locomotion = LocomotionMode.Smooth;
                s.subtitles = false;
            });

            Assert.That(settings.ResetToDefaults(), Is.True);

            UserSettingsData result = settings.Current;
            Assert.That(result.comfortMode, Is.True);
            Assert.That(result.EffectiveLocomotion, Is.EqualTo(LocomotionMode.Teleport));
            Assert.That(result.subtitles, Is.True);
        }

        // -- Guard rails ---------------------------------------------------------------------------

        /// <summary>Missing dependencies fail at construction rather than at first use.</summary>
        [Test]
        public void Constructor_WithMissingDependencies_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SettingsService(null, logger));
            Assert.Throws<ArgumentNullException>(() => new SettingsService(saveService, null));
        }
    }
}
