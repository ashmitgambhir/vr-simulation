using System;
using UnityEngine;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Every player-configurable audio, comfort and accessibility preference
    /// (backend schema "User Settings"; PRD "Accessibility"; TRD 20).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The public fields reproduce the backend schema exactly, because that document defines the
    /// on-disk contract and a file written by this build must remain readable by any other tool
    /// that implements the schema. The schema does not, however, cover everything the PRD requires
    /// — narration speed, colour vision mode, one-handed mode, vignette strength and calibrated
    /// height are all mandated by the PRD, which outranks the schema. Those are added as new
    /// fields, which is a backward-compatible extension: an older reader ignores them, and this
    /// reader supplies defaults when they are absent.
    /// </para>
    /// <para>
    /// The schema models turning as two independent booleans, <c>snapTurning</c> and
    /// <c>smoothTurning</c>, which admits two nonsensical states: both on, and both off. The fields
    /// are kept for wire compatibility but nothing in the codebase reads them directly. All code
    /// goes through <see cref="Turning"/>, which cannot express an invalid state and which repairs
    /// a contradictory file on load.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class UserSettingsData
    {
        // -- Audio (backend schema) --------------------------------------------------------

        /// <summary>Master output level, 0 to 1.</summary>
        public float masterVolume = SettingsDefaults.MasterVolume;

        /// <summary>Ambient soundtrack level, 0 to 1.</summary>
        public float musicVolume = SettingsDefaults.MusicVolume;

        /// <summary>Narration level, 0 to 1. Defaults highest because narration carries the teaching.</summary>
        public float voiceVolume = SettingsDefaults.VoiceVolume;

        /// <summary>Interface and interaction sound effect level, 0 to 1.</summary>
        public float effectsVolume = SettingsDefaults.EffectsVolume;

        // -- Comfort (backend schema) ------------------------------------------------------

        /// <summary>
        /// Master comfort switch. When enabled the experience forces teleport locomotion, snap
        /// turning and fade transitions regardless of the individual preferences below
        /// (PRD edge case, "User has motion sensitivity").
        /// </summary>
        public bool comfortMode = SettingsDefaults.ComfortMode;

        /// <summary>
        /// Wire-compatible mirror of <see cref="Turning"/>. Do not read directly.
        /// </summary>
        public bool snapTurning = true;

        /// <summary>
        /// Wire-compatible mirror of <see cref="Turning"/>. Do not read directly.
        /// </summary>
        public bool smoothTurning = false;

        /// <summary>Whether the player has chosen the left hand as dominant.</summary>
        public bool leftHanded = false;

        /// <summary>Whether subtitles accompany narration.</summary>
        public bool subtitles = SettingsDefaults.Subtitles;

        /// <summary>Whether the play space is calibrated standing rather than seated.</summary>
        public bool standingMode = true;

        // -- PRD accessibility extensions --------------------------------------------------

        /// <summary>
        /// Narration playback rate multiplier (PRD "Adjustable narration speed"). Clamped to
        /// <see cref="SettingsDefaults.MinNarrationSpeed"/>..<see cref="SettingsDefaults.MaxNarrationSpeed"/>.
        /// </summary>
        public float narrationSpeed = SettingsDefaults.NarrationSpeed;

        /// <summary>
        /// Locomotion style, stored as the integer value of <see cref="LocomotionMode"/>.
        /// </summary>
        public int locomotionMode = (int)LocomotionMode.Teleport;

        /// <summary>
        /// Colour vision accommodation, stored as the integer value of <see cref="ColorVisionMode"/>.
        /// </summary>
        public int colorVisionMode = (int)ColorVisionMode.Standard;

        /// <summary>
        /// Comfort vignette strength, stored as the integer value of <see cref="VignetteStrength"/>.
        /// </summary>
        public int vignetteStrength = (int)VignetteStrength.Medium;

        /// <summary>
        /// Whether every interaction must be completable with a single controller
        /// (PRD "One-handed mode").
        /// </summary>
        public bool oneHandedMode = false;

        /// <summary>
        /// Calibrated eye height in metres, or <c>0</c> when the player has not calibrated and the
        /// runtime should use the tracked height (TRD 9, "Automatic calibration").
        /// </summary>
        public float calibratedHeightMeters = 0f;

        /// <summary>
        /// Snap turn increment in degrees. Exposed because the comfortable increment varies widely
        /// between players; 30 and 45 are both common.
        /// </summary>
        public float snapTurnDegrees = SettingsDefaults.SnapTurnDegrees;

        /// <summary>Continuous turn rate in degrees per second, used only in <see cref="TurnMode.Smooth"/>.</summary>
        public float smoothTurnDegreesPerSecond = SettingsDefaults.SmoothTurnDegreesPerSecond;

        /// <summary>Continuous movement speed in metres per second, used only in <see cref="LocomotionMode.Smooth"/>.</summary>
        public float smoothMoveSpeed = SettingsDefaults.SmoothMoveSpeed;

        /// <summary>Whether controller haptics are enabled. Some players find sustained haptics unpleasant.</summary>
        public bool hapticsEnabled = true;

        // -- Typed accessors ---------------------------------------------------------------

        /// <summary>
        /// Gets or sets the turning style. Writing this keeps the two schema booleans consistent;
        /// reading it resolves a contradictory file deterministically in favour of the comfort
        /// option.
        /// </summary>
        public TurnMode Turning
        {
            get
            {
                // A file that says "both" or "neither" is contradictory. Snap is the comfort
                // choice, so it wins in both cases rather than leaving the player in smooth
                // rotation they did not ask for.
                if (snapTurning == smoothTurning)
                {
                    return TurnMode.Snap;
                }

                return smoothTurning ? TurnMode.Smooth : TurnMode.Snap;
            }
            set
            {
                snapTurning = value == TurnMode.Snap;
                smoothTurning = value == TurnMode.Smooth;
            }
        }

        /// <summary>Gets or sets the locomotion style.</summary>
        public LocomotionMode Locomotion
        {
            get => EnumGuard.ToEnum(locomotionMode, LocomotionMode.Teleport);
            set => locomotionMode = (int)value;
        }

        /// <summary>Gets or sets the play space calibration.</summary>
        public TrackingMode Tracking
        {
            get => standingMode ? TrackingMode.Standing : TrackingMode.Seated;
            set => standingMode = value == TrackingMode.Standing;
        }

        /// <summary>Gets or sets the dominant hand.</summary>
        public Handedness DominantHand
        {
            get => leftHanded ? Handedness.Left : Handedness.Right;
            set => leftHanded = value == Handedness.Left;
        }

        /// <summary>Gets or sets the colour vision accommodation.</summary>
        public ColorVisionMode ColorVision
        {
            get => EnumGuard.ToEnum(colorVisionMode, ColorVisionMode.Standard);
            set => colorVisionMode = (int)value;
        }

        /// <summary>Gets or sets the comfort vignette strength.</summary>
        public VignetteStrength Vignette
        {
            get => EnumGuard.ToEnum(vignetteStrength, VignetteStrength.Medium);
            set => vignetteStrength = (int)value;
        }

        /// <summary>
        /// Gets the locomotion style actually in force, accounting for <see cref="comfortMode"/>.
        /// </summary>
        /// <remarks>
        /// Comfort mode is a hard override rather than a preset that merely changes the other
        /// values, so that turning it on cannot silently discard a player's individual choices and
        /// turning it off restores them exactly.
        /// </remarks>
        public LocomotionMode EffectiveLocomotion => comfortMode ? LocomotionMode.Teleport : Locomotion;

        /// <summary>Gets the turning style actually in force, accounting for <see cref="comfortMode"/>.</summary>
        public TurnMode EffectiveTurning => comfortMode ? TurnMode.Snap : Turning;

        /// <summary>Gets the vignette strength actually in force, accounting for <see cref="comfortMode"/>.</summary>
        public VignetteStrength EffectiveVignette
        {
            get
            {
                if (!comfortMode)
                {
                    return Vignette;
                }

                // Comfort mode raises a weak or absent vignette to the default, but never lowers a
                // player who deliberately chose High.
                return Vignette == VignetteStrength.High ? VignetteStrength.High : VignetteStrength.Medium;
            }
        }

        /// <summary>
        /// Creates a settings object populated with the comfort-first defaults a first-time player
        /// receives.
        /// </summary>
        /// <returns>A new, valid settings instance.</returns>
        public static UserSettingsData CreateDefault() => new UserSettingsData();

        /// <summary>
        /// Creates an independent copy.
        /// </summary>
        /// <remarks>
        /// The settings service hands copies to callers so that a screen editing a draft cannot
        /// mutate live state before the player confirms, and so that a cancelled edit needs no
        /// undo logic.
        /// </remarks>
        /// <returns>A deep copy of this instance.</returns>
        public UserSettingsData Clone() => (UserSettingsData)MemberwiseClone();

        /// <summary>
        /// Forces every field into its legal range, repairing values that were absent, out of
        /// range, or contradictory.
        /// </summary>
        /// <remarks>
        /// Called on load and again before every write. A settings file is the most likely thing a
        /// curious player edits by hand, and a NaN volume or a negative turn rate must degrade to a
        /// sane default rather than propagate into the audio mixer or the locomotion provider.
        /// </remarks>
        /// <returns><c>true</c> if any value had to be repaired.</returns>
        public bool Sanitize()
        {
            bool repaired = false;

            repaired |= ClampVolume(ref masterVolume, SettingsDefaults.MasterVolume);
            repaired |= ClampVolume(ref musicVolume, SettingsDefaults.MusicVolume);
            repaired |= ClampVolume(ref voiceVolume, SettingsDefaults.VoiceVolume);
            repaired |= ClampVolume(ref effectsVolume, SettingsDefaults.EffectsVolume);

            repaired |= ClampRange(
                ref narrationSpeed,
                SettingsDefaults.MinNarrationSpeed,
                SettingsDefaults.MaxNarrationSpeed,
                SettingsDefaults.NarrationSpeed);

            repaired |= ClampRange(
                ref snapTurnDegrees,
                SettingsDefaults.MinSnapTurnDegrees,
                SettingsDefaults.MaxSnapTurnDegrees,
                SettingsDefaults.SnapTurnDegrees);

            repaired |= ClampRange(
                ref smoothTurnDegreesPerSecond,
                SettingsDefaults.MinSmoothTurnDegreesPerSecond,
                SettingsDefaults.MaxSmoothTurnDegreesPerSecond,
                SettingsDefaults.SmoothTurnDegreesPerSecond);

            repaired |= ClampRange(
                ref smoothMoveSpeed,
                SettingsDefaults.MinSmoothMoveSpeed,
                SettingsDefaults.MaxSmoothMoveSpeed,
                SettingsDefaults.SmoothMoveSpeed);

            // Height of exactly zero is the documented "not calibrated" sentinel and is legal.
            if (calibratedHeightMeters != 0f)
            {
                repaired |= ClampRange(
                    ref calibratedHeightMeters,
                    SettingsDefaults.MinCalibratedHeightMeters,
                    SettingsDefaults.MaxCalibratedHeightMeters,
                    0f);
            }

            if (!EnumGuard.IsDefined<LocomotionMode>(locomotionMode))
            {
                locomotionMode = (int)LocomotionMode.Teleport;
                repaired = true;
            }

            if (!EnumGuard.IsDefined<ColorVisionMode>(colorVisionMode))
            {
                colorVisionMode = (int)ColorVisionMode.Standard;
                repaired = true;
            }

            if (!EnumGuard.IsDefined<VignetteStrength>(vignetteStrength))
            {
                vignetteStrength = (int)VignetteStrength.Medium;
                repaired = true;
            }

            // Collapse the contradictory turning states the schema permits.
            if (snapTurning == smoothTurning)
            {
                Turning = TurnMode.Snap;
                repaired = true;
            }

            return repaired;
        }

        /// <summary>
        /// Clamps a normalised volume, replacing non-finite values with a default.
        /// </summary>
        /// <param name="value">The field to repair.</param>
        /// <param name="fallback">Value substituted when <paramref name="value"/> is not finite.</param>
        /// <returns><c>true</c> if the value changed.</returns>
        private static bool ClampVolume(ref float value, float fallback) =>
            ClampRange(ref value, 0f, 1f, fallback);

        /// <summary>
        /// Clamps a float into an inclusive range, replacing non-finite values with a default.
        /// </summary>
        /// <param name="value">The field to repair.</param>
        /// <param name="min">Inclusive lower bound.</param>
        /// <param name="max">Inclusive upper bound.</param>
        /// <param name="fallback">Value substituted when <paramref name="value"/> is NaN or infinite.</param>
        /// <returns><c>true</c> if the value changed.</returns>
        private static bool ClampRange(ref float value, float min, float max, float fallback)
        {
            // NaN fails every comparison, so it must be tested explicitly rather than relying on
            // Mathf.Clamp, which would propagate it.
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
                return true;
            }

            float clamped = Mathf.Clamp(value, min, max);
            if (!Mathf.Approximately(clamped, value))
            {
                value = clamped;
                return true;
            }

            return false;
        }
    }
}
