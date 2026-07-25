using System;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Kinds of analytics event the experience records (backend schema, "Analytics Events").
    /// </summary>
    /// <remarks>
    /// The schema lists these as strings. They are modelled as an enum so that a typo cannot
    /// silently create a new event category that no dashboard is aggregating, and converted to
    /// their documented string form only at the serialisation boundary.
    /// </remarks>
    public enum AnalyticsEventType
    {
        /// <summary>Sentinel; never recorded.</summary>
        None = 0,

        /// <summary>A scene finished loading.</summary>
        SceneLoaded = 1,

        /// <summary>The player entered a module.</summary>
        ModuleStarted = 2,

        /// <summary>The player reached a module's completion confirmation.</summary>
        ModuleCompleted = 3,

        /// <summary>An interactable object was grabbed.</summary>
        ObjectGrabbed = 4,

        /// <summary>A diegetic button was pressed.</summary>
        ButtonPressed = 5,

        /// <summary>The player teleported.</summary>
        Teleport = 6,

        /// <summary>A knowledge check was submitted.</summary>
        QuizCompleted = 7,

        /// <summary>A setting was changed.</summary>
        SettingsChanged = 8,

        /// <summary>The player reached the end of the experience.</summary>
        ExperienceFinished = 9,

        /// <summary>The player skipped a tutorial or narration segment.</summary>
        TutorialSkipped = 10
    }

    /// <summary>
    /// A single recorded interaction (backend schema, "Analytics Events").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Analytics are marked optional by the schema and are stored locally only. Nothing here is
    /// transmitted anywhere: the experience targets a beginner audience that includes minors, so
    /// the safe default is that data never leaves the device. The buffer exists so a facilitator
    /// can export it deliberately, and it is capped at
    /// <see cref="SaveConstants.MaxRetainedAnalyticsEvents"/>.
    /// </para>
    /// <para>
    /// No field here may carry personally identifying information. The event describes what was
    /// done, never who did it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class AnalyticsEvent
    {
        /// <summary>Unique identifier for this event, a GUID string.</summary>
        public string eventId = string.Empty;

        /// <summary>Integer value of the <see cref="AnalyticsEventType"/>.</summary>
        public int eventType = (int)AnalyticsEventType.None;

        /// <summary>Integer value of the <see cref="ModuleId"/> the event occurred in.</summary>
        public int moduleId = (int)ModuleId.None;

        /// <summary>ISO-8601 UTC timestamp.</summary>
        public string timestamp = string.Empty;

        /// <summary>
        /// Name of the object involved, where one applies. Names come from prefabs authored by the
        /// development team, never from player input.
        /// </summary>
        public string objectName = string.Empty;

        /// <summary>
        /// Optional numeric payload, used for values such as a latency slider position so that a
        /// facilitator can see which settings the player explored.
        /// </summary>
        public float numericValue;

        /// <summary>Gets or sets <see cref="eventType"/> as a strongly typed value.</summary>
        public AnalyticsEventType EventType
        {
            get => EnumGuard.ToEnum(eventType, AnalyticsEventType.None);
            set => eventType = (int)value;
        }

        /// <summary>Gets or sets <see cref="moduleId"/> as a strongly typed value.</summary>
        public ModuleId Module
        {
            get => ModuleIdExtensions.FromInt(moduleId);
            set => moduleId = (int)value;
        }

        /// <summary>Gets when the event occurred, or <see cref="DateTime.MinValue"/> if unset.</summary>
        public DateTime TimestampUtc => TimestampUtility.Parse(timestamp);

        /// <summary>
        /// Creates an event.
        /// </summary>
        /// <param name="type">What happened.</param>
        /// <param name="module">Where it happened.</param>
        /// <param name="utcNow">When it happened.</param>
        /// <param name="objectName">Optional name of the object involved.</param>
        /// <param name="numericValue">Optional numeric payload.</param>
        /// <returns>A populated event.</returns>
        public static AnalyticsEvent Create(
            AnalyticsEventType type,
            ModuleId module,
            DateTime utcNow,
            string objectName = null,
            float numericValue = 0f)
        {
            return new AnalyticsEvent
            {
                eventId = Guid.NewGuid().ToString("D"),
                eventType = (int)type,
                moduleId = (int)module,
                timestamp = TimestampUtility.Format(utcNow),
                objectName = objectName ?? string.Empty,
                numericValue = float.IsNaN(numericValue) || float.IsInfinity(numericValue) ? 0f : numericValue
            };
        }
    }
}
