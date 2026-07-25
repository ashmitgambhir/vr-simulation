namespace VRSimulation.Utilities
{
    /// <summary>
    /// Script execution order values for systems whose initialisation sequence matters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's default execution order is unspecified, so two <c>Awake</c> methods that must run in
    /// a particular sequence will work by luck until a scene is re-saved or an asset is reimported,
    /// at which point the order silently changes. The classic symptom is a null reference on
    /// startup that appears only on the build machine.
    /// </para>
    /// <para>
    /// Gathering the values here rather than scattering magic numbers across
    /// <c>[DefaultExecutionOrder]</c> attributes makes the dependency ordering readable in one
    /// place, and makes it obvious when a new system needs a slot between two existing ones. The
    /// gaps between values are deliberate, so later insertions do not require renumbering.
    /// </para>
    /// </remarks>
    public static class ExecutionOrder
    {
        /// <summary>
        /// The composition root. Runs before everything else, because it constructs the services
        /// every other system resolves during its own initialisation.
        /// </summary>
        public const int Bootstrap = -1000;

        /// <summary>
        /// Systems that apply loaded settings to the runtime, such as audio mixing and comfort
        /// options. After the root, before anything that reads their effects.
        /// </summary>
        public const int SettingsConsumers = -900;

        /// <summary>
        /// The player rig: origin, locomotion and calibration. Must be positioned before module
        /// content places anything relative to the player.
        /// </summary>
        public const int PlayerRig = -800;

        /// <summary>
        /// Scene flow and module lifecycle. After the rig exists, so a module can safely reference
        /// the player on entry.
        /// </summary>
        public const int ModuleLifecycle = -700;

        /// <summary>
        /// Diegetic interface and subtitles. After module lifecycle, so the first objective is
        /// already known when the interface first draws.
        /// </summary>
        public const int UserInterface = -600;

        /// <summary>
        /// Diagnostics that observe the frame. Last, so a frame time measurement includes the cost
        /// of everything above it rather than a fraction of it.
        /// </summary>
        public const int Diagnostics = 1000;
    }
}
