namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Stable identifier for every Unity scene shipped with the experience (TRD 7).
    /// </summary>
    /// <remarks>
    /// Scene *names* are a build-settings concern and are resolved through
    /// <see cref="VRSimulation.Configuration.SceneCatalog"/>; nothing outside that catalog should
    /// reference a scene by string. Keeping the enum separate from <see cref="ModuleId"/> lets
    /// several modules share a single scene without forcing a scene load between them, which is
    /// what keeps transitions under the three second budget in TRD 5.
    /// </remarks>
    public enum SceneId
    {
        /// <summary>Sentinel used for "no scene".</summary>
        None = 0,

        /// <summary>Persistent scene holding the manager rig; loaded once and never unloaded.</summary>
        Bootstrap = 1,

        /// <summary>Diegetic main menu and resume point.</summary>
        MainMenu = 2,

        /// <summary>Black room, glowing platform, the spider.</summary>
        Introduction = 3,

        /// <summary>Floating brain and the three sensory pathways.</summary>
        Presence = 4,

        /// <summary>Exploded headset: display, sensors, controllers.</summary>
        Hardware = 5,

        /// <summary>Vision lab: two cameras, eye separation, field of view.</summary>
        Vision = 6,

        /// <summary>Balance lab: inner ear, sensory conflict, motion sickness.</summary>
        Vestibular = 7,

        /// <summary>Interaction room: grab, throw, press, haptics.</summary>
        Interaction = 8,

        /// <summary>Latency room: the pipeline timeline, prediction, developer tasks.</summary>
        Latency = 9,

        /// <summary>Applications gallery: portals to six real world uses.</summary>
        Applications = 10,

        /// <summary>Conclusion room: the three icons merge into presence.</summary>
        Conclusion = 11
    }
}
