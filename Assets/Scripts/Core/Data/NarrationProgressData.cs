using System;
using UnityEngine;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Where narration had reached when the session ended (backend schema, "Narration Progress").
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes the PRD's "User removes headset — resume exactly where left off" edge
    /// case work. Without it, a player who takes the headset off mid-explanation returns to the
    /// start of the module and hears the same two minutes again, which is the fastest way to lose
    /// them.
    /// </para>
    /// <para>
    /// Only one record is kept, for the module in progress. Narration position in a module the
    /// player already finished is not interesting, and keeping a record per module would grow the
    /// save for no benefit.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class NarrationProgressData
    {
        /// <summary>
        /// Grace period in seconds subtracted from the resume point, so that playback restarts
        /// slightly before the interruption and the player hears the tail of the last sentence
        /// rather than being dropped into the middle of the next word.
        /// </summary>
        public const float ResumeRewindSeconds = 1.5f;

        /// <summary>
        /// Position within a clip below which resuming is pointless and playback simply restarts
        /// the clip from the beginning.
        /// </summary>
        public const float MinimumResumeOffsetSeconds = 0.5f;

        /// <summary>Integer value of the <see cref="ModuleId"/> the narration belongs to.</summary>
        public int moduleId = (int)ModuleId.None;

        /// <summary>Zero-based index of the clip within the module's narration sequence.</summary>
        public int currentNarrationClip;

        /// <summary>Playback position within that clip, in seconds.</summary>
        public float currentTimestamp;

        /// <summary>Gets or sets <see cref="moduleId"/> as a strongly typed value.</summary>
        public ModuleId Module
        {
            get => ModuleIdExtensions.FromInt(moduleId);
            set => moduleId = (int)value;
        }

        /// <summary>Gets whether this record points at a real module and can be resumed from.</summary>
        public bool HasResumePoint => Module != ModuleId.None && currentNarrationClip >= 0;

        /// <summary>
        /// Gets the position playback should actually restart from, rewound slightly for context
        /// and never negative.
        /// </summary>
        public float ResumeTimestamp
        {
            get
            {
                float rewound = currentTimestamp - ResumeRewindSeconds;
                return rewound < MinimumResumeOffsetSeconds ? 0f : rewound;
            }
        }

        /// <summary>
        /// Records the current playback position.
        /// </summary>
        /// <param name="module">Module being narrated.</param>
        /// <param name="clipIndex">Zero-based clip index.</param>
        /// <param name="timestampSeconds">Playback position within the clip.</param>
        public void Set(ModuleId module, int clipIndex, float timestampSeconds)
        {
            moduleId = (int)module;
            currentNarrationClip = Mathf.Max(0, clipIndex);
            currentTimestamp = SanitizeTimestamp(timestampSeconds);
        }

        /// <summary>
        /// Discards the resume point, used once a module completes so a replay starts cleanly.
        /// </summary>
        public void Clear()
        {
            moduleId = (int)ModuleId.None;
            currentNarrationClip = 0;
            currentTimestamp = 0f;
        }

        /// <summary>
        /// Forces every field into its legal range.
        /// </summary>
        /// <returns><c>true</c> if anything had to be repaired.</returns>
        public bool Sanitize()
        {
            bool repaired = false;

            if (!ModuleIdExtensions.IsDefined(moduleId))
            {
                moduleId = (int)ModuleId.None;
                repaired = true;
            }

            if (currentNarrationClip < 0)
            {
                currentNarrationClip = 0;
                repaired = true;
            }

            float sanitized = SanitizeTimestamp(currentTimestamp);
            if (!Mathf.Approximately(sanitized, currentTimestamp))
            {
                currentTimestamp = sanitized;
                repaired = true;
            }

            return repaired;
        }

        /// <summary>
        /// Rejects negative and non-finite playback positions.
        /// </summary>
        /// <param name="value">The candidate position.</param>
        /// <returns>A finite, non-negative position.</returns>
        private static float SanitizeTimestamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return 0f;
            }

            return value;
        }
    }
}
