namespace VRSimulation.Core.Data
{
    /// <summary>
    /// How the player moves through the world (PRD "Accessibility", TRD 9).
    /// </summary>
    /// <remarks>
    /// Teleportation is the default because it is the only locomotion style that produces no
    /// vestibular conflict, which is the very phenomenon the experience teaches. Smooth locomotion
    /// is offered but never assumed.
    /// </remarks>
    public enum LocomotionMode
    {
        /// <summary>Instant, arc-targeted teleport with a fade. The comfort default.</summary>
        Teleport = 0,

        /// <summary>Continuous thumbstick movement. Opt-in, and paired with a stronger vignette.</summary>
        Smooth = 1
    }

    /// <summary>
    /// How the player rotates (PRD "Accessibility", TRD 9).
    /// </summary>
    public enum TurnMode
    {
        /// <summary>Discrete rotation by a fixed angle. The comfort default.</summary>
        Snap = 0,

        /// <summary>Continuous rotation. Opt-in; a common motion sickness trigger.</summary>
        Smooth = 1
    }

    /// <summary>
    /// Whether the play space is calibrated for a seated or standing player (PRD, TRD 20).
    /// </summary>
    /// <remarks>
    /// This drives both the camera offset mode and whether floor-relative interactions are moved
    /// into comfortable reach, so a seated player is never asked to pick an object off the floor.
    /// </remarks>
    public enum TrackingMode
    {
        /// <summary>Seated. Height is derived from calibration rather than the floor.</summary>
        Seated = 0,

        /// <summary>Standing, room scale. Height comes from the tracked floor.</summary>
        Standing = 1
    }

    /// <summary>
    /// Dominant hand, used to mirror interaction layouts (PRD edge case "User is left-handed").
    /// </summary>
    public enum Handedness
    {
        /// <summary>Right hand drives pointing and primary interaction.</summary>
        Right = 0,

        /// <summary>Left hand drives pointing and primary interaction; layouts mirror.</summary>
        Left = 1
    }

    /// <summary>
    /// Colour vision accommodation for the experience palette (PRD, TRD 20).
    /// </summary>
    /// <remarks>
    /// The experience leans heavily on red-versus-green to mean "sensory conflict" versus
    /// "sensory agreement" (PRD Module 3). That encoding is invisible to the most common forms of
    /// colour blindness, so the palette must be able to switch to a hue pair that survives it, and
    /// every such signal is additionally encoded by shape and motion rather than colour alone.
    /// </remarks>
    public enum ColorVisionMode
    {
        /// <summary>Unmodified palette.</summary>
        Standard = 0,

        /// <summary>Red-green deficiency, red cone. Substitutes a blue and orange pair.</summary>
        Protanopia = 1,

        /// <summary>Red-green deficiency, green cone. Substitutes a blue and orange pair.</summary>
        Deuteranopia = 2,

        /// <summary>Blue-yellow deficiency. Substitutes a red and cyan pair.</summary>
        Tritanopia = 3
    }

    /// <summary>
    /// Strength of the peripheral comfort vignette applied during movement (PRD, TRD 20).
    /// </summary>
    public enum VignetteStrength
    {
        /// <summary>No vignette. For players with no motion sensitivity.</summary>
        Off = 0,

        /// <summary>Subtle aperture reduction.</summary>
        Low = 1,

        /// <summary>The default. Noticeably reduces optical flow without feeling restrictive.</summary>
        Medium = 2,

        /// <summary>Aggressive aperture reduction for highly sensitive players.</summary>
        High = 3
    }
}
