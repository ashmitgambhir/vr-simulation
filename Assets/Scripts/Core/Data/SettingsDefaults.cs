namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Default values and legal ranges for every entry in <see cref="UserSettingsData"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stated once, here, so that the default a new player receives, the value a corrupt field is
    /// repaired to, and the range a settings slider offers can never disagree (TRD 25).
    /// </para>
    /// <para>
    /// The audio defaults are taken verbatim from the backend schema. The comfort defaults follow
    /// the PRD position that the experience must not induce motion sickness in a first-time user
    /// who changes nothing, so every default is the conservative option.
    /// </para>
    /// </remarks>
    public static class SettingsDefaults
    {
        // -- Audio, per backend schema "User Settings" -------------------------------------

        /// <summary>Default master volume.</summary>
        public const float MasterVolume = 0.8f;

        /// <summary>Default ambient soundtrack volume, kept well below narration.</summary>
        public const float MusicVolume = 0.4f;

        /// <summary>Default narration volume. Full scale, because narration carries the teaching.</summary>
        public const float VoiceVolume = 1.0f;

        /// <summary>Default interface and interaction effect volume.</summary>
        public const float EffectsVolume = 0.7f;

        /// <summary>
        /// Quietest level that is still audible above the headset fan, below which the mixer
        /// switches the channel fully off to save the voice.
        /// </summary>
        public const float SilenceThreshold = 0.001f;

        /// <summary>Attenuation in decibels applied when a channel is fully muted.</summary>
        public const float MutedVolumeDecibels = -80f;

        // -- Comfort -----------------------------------------------------------------------

        /// <summary>Comfort mode starts enabled; the player opts out, never in.</summary>
        public const bool ComfortMode = true;

        /// <summary>Subtitles start enabled, per PRD accessibility.</summary>
        public const bool Subtitles = true;

        /// <summary>Default snap turn increment in degrees.</summary>
        public const float SnapTurnDegrees = 30f;

        /// <summary>Smallest offered snap increment.</summary>
        public const float MinSnapTurnDegrees = 15f;

        /// <summary>Largest offered snap increment.</summary>
        public const float MaxSnapTurnDegrees = 90f;

        /// <summary>Default continuous turn rate, deliberately slow.</summary>
        public const float SmoothTurnDegreesPerSecond = 60f;

        /// <summary>Slowest offered continuous turn rate.</summary>
        public const float MinSmoothTurnDegreesPerSecond = 30f;

        /// <summary>
        /// Fastest offered continuous turn rate. Capped well below what a thumbstick could drive,
        /// because high angular velocity is the single strongest motion sickness trigger in VR.
        /// </summary>
        public const float MaxSmoothTurnDegreesPerSecond = 180f;

        /// <summary>Default continuous movement speed, roughly a slow walk.</summary>
        public const float SmoothMoveSpeed = 1.5f;

        /// <summary>Slowest offered continuous movement speed.</summary>
        public const float MinSmoothMoveSpeed = 0.5f;

        /// <summary>Fastest offered continuous movement speed, kept near a brisk walk.</summary>
        public const float MaxSmoothMoveSpeed = 4f;

        // -- Narration ---------------------------------------------------------------------

        /// <summary>Default narration rate multiplier.</summary>
        public const float NarrationSpeed = 1.0f;

        /// <summary>Slowest narration rate, for players who need more processing time.</summary>
        public const float MinNarrationSpeed = 0.5f;

        /// <summary>
        /// Fastest narration rate. Above roughly 1.5x, pitch-preserved speech becomes hard to
        /// follow for the beginner audience the PRD targets.
        /// </summary>
        public const float MaxNarrationSpeed = 1.5f;

        // -- Height calibration ------------------------------------------------------------

        /// <summary>
        /// Shortest calibrated eye height accepted, in metres. Below this the value is assumed to
        /// be a tracking glitch rather than a real player.
        /// </summary>
        public const float MinCalibratedHeightMeters = 0.8f;

        /// <summary>Tallest calibrated eye height accepted, in metres.</summary>
        public const float MaxCalibratedHeightMeters = 2.2f;

        /// <summary>Assumed eye height when neither calibration nor tracking can supply one.</summary>
        public const float FallbackEyeHeightMeters = 1.6f;
    }
}
