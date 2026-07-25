namespace VRSimulation.Core.Diagnostics
{
    /// <summary>
    /// Subsystem a log entry originated from (TRD 22).
    /// </summary>
    /// <remarks>
    /// Categories exist so that a developer chasing a save bug can filter to
    /// <see cref="Persistence"/> without reading several hundred locomotion messages. They are an
    /// enum rather than free-text tags because a mistyped tag silently creates a category nobody
    /// filters on.
    /// </remarks>
    public enum LogCategory
    {
        /// <summary>Application startup, composition and shutdown.</summary>
        Bootstrap = 0,

        /// <summary>Save and settings reading, writing, migration and recovery.</summary>
        Persistence = 1,

        /// <summary>Scene loading, unloading and transitions.</summary>
        SceneFlow = 2,

        /// <summary>Module lifecycle and progression.</summary>
        Module = 3,

        /// <summary>Narration, music and sound effects.</summary>
        Audio = 4,

        /// <summary>Grab, press, pointer and teleport interactions.</summary>
        Interaction = 5,

        /// <summary>Locomotion, turning, calibration and comfort.</summary>
        Player = 6,

        /// <summary>Diegetic interface and subtitles.</summary>
        UserInterface = 7,

        /// <summary>Headset and controller tracking, connection and presence.</summary>
        Xr = 8,

        /// <summary>Frame timing, memory and performance budget warnings.</summary>
        Performance = 9,

        /// <summary>Configuration asset validation.</summary>
        Configuration = 10
    }

    /// <summary>
    /// Importance of a log entry, used to decide what survives into a release build (TRD 22).
    /// </summary>
    public enum LogSeverity
    {
        /// <summary>
        /// Routine progress information. Stripped from release builds entirely, because writing to
        /// the Android log on a Quest costs main-thread time inside the frame budget.
        /// </summary>
        Debug = 0,

        /// <summary>Notable but expected events, such as a module completing. Stripped from release builds.</summary>
        Info = 1,

        /// <summary>
        /// Something recoverable went wrong: a missing optional asset, a repaired save field, a
        /// frame budget overrun. Retained in release builds.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Something failed that the player may notice. Retained in release builds and always
        /// accompanied by a recovery action.
        /// </summary>
        Error = 3
    }
}
