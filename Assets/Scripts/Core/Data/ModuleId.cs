namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Stable identifier for every educational module defined in the PRD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These values are persisted inside the save file as <see cref="ModuleProgressData.moduleId"/>
    /// and are therefore part of the on-disk contract. Existing values must never be renumbered;
    /// new modules append to the end of the enum. See the backend schema, "Module Progress".
    /// </para>
    /// <para>
    /// The PRD defines an introduction plus twelve modules, while the TRD organises the experience
    /// into ten scenes. The two are deliberately decoupled: a module is a unit of *teaching*, a
    /// scene is a unit of *loading*. Several modules may share one scene. The mapping lives in
    /// <see cref="VRSimulation.Configuration.ModuleDefinition"/> assets rather than in code, so
    /// that new modules can be added without modifying existing systems (TRD 26).
    /// </para>
    /// </remarks>
    public enum ModuleId
    {
        /// <summary>Sentinel used for "no module"; never persisted as a completed module.</summary>
        None = 0,

        /// <summary>PRD "Introduction" — the spider recoil that motivates the whole experience.</summary>
        Introduction = 1,

        /// <summary>PRD Module 1 — presence emerges when the senses agree.</summary>
        Presence = 2,

        /// <summary>PRD Module 2 — display, sensors and controllers.</summary>
        Hardware = 3,

        /// <summary>PRD Module 3 — the brain checks consistency, not reality.</summary>
        BrainBuildsReality = 4,

        /// <summary>PRD Module 4 — stereoscopic vision and binocular depth.</summary>
        StereoscopicVision = 5,

        /// <summary>PRD Module 5 — the vestibular system and sensory conflict.</summary>
        VestibularSystem = 6,

        /// <summary>PRD Module 6 — the motion sickness lab and brain confidence meter.</summary>
        MotionSicknessLab = 7,

        /// <summary>PRD Module 7 — interaction, haptics and why timing beats realism.</summary>
        Interaction = 8,

        /// <summary>PRD Module 8 — the motion-to-photon latency pipeline.</summary>
        LatencyChallenge = 9,

        /// <summary>PRD Module 9 — reprojection and head-pose prediction.</summary>
        Prediction = 10,

        /// <summary>PRD Module 10 — the user takes the developer's seat.</summary>
        BuildingVR = 11,

        /// <summary>PRD Module 11 — real world applications gallery.</summary>
        RealWorldApplications = 12,

        /// <summary>PRD Module 12 — final summary; eyes, balance and hands merge into presence.</summary>
        FinalSummary = 13
    }
}
