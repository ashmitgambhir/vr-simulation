using System;

namespace VRSimulation.Core.Data
{
    /// <summary>
    /// Converts untrusted integers into enum values without ever producing an undefined one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// C# permits <c>(SomeEnum)9999</c>. The cast succeeds, no exception is raised, and the result
    /// matches no <c>case</c> label and no <c>if</c> branch. When the integer came from a save file
    /// the resulting bug surfaces far from its cause, usually as a system silently doing nothing.
    /// Every integer that crosses the persistence boundary is funnelled through here instead.
    /// </para>
    /// <para>
    /// <see cref="Enum.IsDefined(Type, object)"/> boxes its argument and performs a linear scan, so
    /// it is called on load and on sanitise rather than per frame. None of these call sites are in
    /// a hot path.
    /// </para>
    /// </remarks>
    public static class EnumGuard
    {
        /// <summary>
        /// Determines whether an integer maps to a declared member of <typeparamref name="TEnum"/>.
        /// </summary>
        /// <typeparam name="TEnum">The enum type to test against.</typeparam>
        /// <param name="value">The raw persisted value.</param>
        /// <returns><c>true</c> if the value is declared.</returns>
        public static bool IsDefined<TEnum>(int value) where TEnum : struct, Enum =>
            Enum.IsDefined(typeof(TEnum), value);

        /// <summary>
        /// Converts an integer to an enum value, substituting a fallback when it is not declared.
        /// </summary>
        /// <typeparam name="TEnum">The enum type to convert to.</typeparam>
        /// <param name="value">The raw persisted value.</param>
        /// <param name="fallback">Value returned when <paramref name="value"/> is not declared.</param>
        /// <returns>A declared enum value.</returns>
        public static TEnum ToEnum<TEnum>(int value, TEnum fallback) where TEnum : struct, Enum =>
            IsDefined<TEnum>(value) ? (TEnum)(object)value : fallback;
    }
}
