using System;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Safe conversions between <see cref="ModuleId"/> and the integers stored on disk.
    /// </summary>
    /// <remarks>
    /// A save file is user-writable data on a device we do not control, so any integer read from
    /// it must be treated as untrusted. Casting an arbitrary <see cref="int"/> to an enum succeeds
    /// silently in C# and produces a value that matches no <c>switch</c> arm, which surfaces much
    /// later as an impossible state. These helpers reject unknown values at the boundary instead.
    /// </remarks>
    public static class ModuleIdExtensions
    {
        /// <summary>Cached enum values. <see cref="Enum.GetValues"/> allocates, so it is called once.</summary>
        private static readonly ModuleId[] AllValues = (ModuleId[])Enum.GetValues(typeof(ModuleId));

        /// <summary>
        /// Gets every declared module in ascending order, excluding <see cref="ModuleId.None"/>.
        /// </summary>
        /// <returns>The ordered teaching sequence defined by the PRD.</returns>
        public static ModuleId[] GetOrderedModules()
        {
            var ordered = new ModuleId[AllValues.Length - 1];
            int index = 0;

            foreach (ModuleId value in AllValues)
            {
                if (value != ModuleId.None)
                {
                    ordered[index++] = value;
                }
            }

            return ordered;
        }

        /// <summary>
        /// Gets the number of modules that count toward completion, excluding
        /// <see cref="ModuleId.None"/>.
        /// </summary>
        public static int TeachingModuleCount => AllValues.Length - 1;

        /// <summary>
        /// Determines whether an integer read from disk maps to a declared module.
        /// </summary>
        /// <param name="value">The raw persisted value.</param>
        /// <returns><c>true</c> if the value is a declared <see cref="ModuleId"/>.</returns>
        public static bool IsDefined(int value) => Enum.IsDefined(typeof(ModuleId), value);

        /// <summary>
        /// Converts an integer read from disk into a module, falling back to
        /// <see cref="ModuleId.None"/> when the value is not recognised.
        /// </summary>
        /// <param name="value">The raw persisted value.</param>
        /// <returns>A declared module, never an undefined enum value.</returns>
        public static ModuleId FromInt(int value) => IsDefined(value) ? (ModuleId)value : ModuleId.None;

        /// <summary>
        /// Gets the module that follows <paramref name="current"/> in the teaching sequence.
        /// </summary>
        /// <param name="current">The current module.</param>
        /// <returns>
        /// The next module, or <see cref="ModuleId.None"/> when <paramref name="current"/> is the
        /// final module or is itself unrecognised.
        /// </returns>
        public static ModuleId Next(this ModuleId current)
        {
            if (current == ModuleId.None)
            {
                return ModuleId.Introduction;
            }

            int candidate = (int)current + 1;
            return FromInt(candidate);
        }

        /// <summary>
        /// Gets the module that precedes <paramref name="current"/> in the teaching sequence.
        /// </summary>
        /// <param name="current">The current module.</param>
        /// <returns>
        /// The previous module, or <see cref="ModuleId.None"/> when <paramref name="current"/> is
        /// the first module or is itself unrecognised.
        /// </returns>
        public static ModuleId Previous(this ModuleId current)
        {
            if (current == ModuleId.None || current == ModuleId.Introduction)
            {
                return ModuleId.None;
            }

            return FromInt((int)current - 1);
        }

        /// <summary>
        /// Gets the zero-based position of a module within the teaching sequence.
        /// </summary>
        /// <param name="module">The module to locate.</param>
        /// <returns>The ordinal, or <c>-1</c> for <see cref="ModuleId.None"/>.</returns>
        public static int ToOrdinal(this ModuleId module)
        {
            return module == ModuleId.None ? -1 : (int)module - 1;
        }
    }
}
