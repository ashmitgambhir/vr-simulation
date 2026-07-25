using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Overall completion state for the experience (backend schema, "Progress").
    /// </summary>
    /// <remarks>
    /// <see cref="percentComplete"/> and <see cref="experienceCompleted"/> are derived from
    /// <see cref="completedModules"/> and are stored only so that an exported save is
    /// self-describing. They are recomputed by <see cref="Recalculate"/> on every change, so a
    /// hand-edited or stale value never becomes authoritative.
    /// </remarks>
    [Serializable]
    public sealed class ProgressData
    {
        /// <summary>Whether every teaching module has been completed.</summary>
        public bool experienceCompleted;

        /// <summary>Completion percentage, 0 to 100. Derived; see <see cref="Recalculate"/>.</summary>
        public int percentComplete;

        /// <summary>
        /// Integer <see cref="ModuleId"/> values the player has completed. Treated as a set: no
        /// duplicates, ascending order.
        /// </summary>
        public List<int> completedModules = new List<int>();

        /// <summary>
        /// Records a module as complete.
        /// </summary>
        /// <param name="module">The completed module.</param>
        /// <returns><c>true</c> if this was the first time the module was completed.</returns>
        public bool MarkCompleted(ModuleId module)
        {
            if (module == ModuleId.None)
            {
                return false;
            }

            completedModules ??= new List<int>();

            int value = (int)module;
            if (completedModules.Contains(value))
            {
                // Replaying a finished module is expected and must not inflate progress
                // (PRD edge case, "Player restarts module").
                return false;
            }

            completedModules.Add(value);
            completedModules.Sort();
            Recalculate();
            return true;
        }

        /// <summary>
        /// Determines whether a module has been completed.
        /// </summary>
        /// <param name="module">The module to test.</param>
        /// <returns><c>true</c> if the module is recorded as complete.</returns>
        public bool IsCompleted(ModuleId module) =>
            module != ModuleId.None && completedModules != null && completedModules.Contains((int)module);

        /// <summary>
        /// Recomputes <see cref="percentComplete"/> and <see cref="experienceCompleted"/> from the
        /// completed set.
        /// </summary>
        public void Recalculate()
        {
            completedModules ??= new List<int>();

            int total = ModuleIdExtensions.TeachingModuleCount;
            if (total <= 0)
            {
                percentComplete = 0;
                experienceCompleted = false;
                return;
            }

            int completedCount = Mathf.Min(completedModules.Count, total);

            // Rounded rather than truncated so that finishing eleven of twelve modules reads as
            // 92 percent and not 91, and so the value only reaches 100 at genuine completion.
            percentComplete = Mathf.Clamp(Mathf.RoundToInt(completedCount * 100f / total), 0, 100);
            experienceCompleted = completedCount >= total;

            // Guard the rounding edge: 100 percent must mean every module, never "almost every".
            if (percentComplete == 100 && !experienceCompleted)
            {
                percentComplete = 99;
            }
        }

        /// <summary>
        /// Removes unrecognised and duplicate entries, then recomputes the derived fields.
        /// </summary>
        /// <returns><c>true</c> if anything had to be repaired.</returns>
        public bool Sanitize()
        {
            bool repaired = false;

            if (completedModules == null)
            {
                completedModules = new List<int>();
                repaired = true;
            }

            var seen = new HashSet<int>();
            for (int i = completedModules.Count - 1; i >= 0; i--)
            {
                int value = completedModules[i];
                bool unknown = !ModuleIdExtensions.IsDefined(value) || value == (int)ModuleId.None;

                if (unknown || !seen.Add(value))
                {
                    completedModules.RemoveAt(i);
                    repaired = true;
                }
            }

            completedModules.Sort();

            int previousPercent = percentComplete;
            bool previousCompleted = experienceCompleted;
            Recalculate();

            return repaired || percentComplete != previousPercent || experienceCompleted != previousCompleted;
        }
    }
}
