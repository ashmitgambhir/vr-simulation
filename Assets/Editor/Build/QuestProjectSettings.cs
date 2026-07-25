using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRSimulation.EditorTools.Build
{
    /// <summary>
    /// The required player and quality configuration for the Meta Quest target, stated as data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity stores these values in <c>ProjectSettings/*.asset</c>, which are large, version
    /// specific, and effectively unreviewable in a pull request: a diff showing
    /// <c>m_ApiCompatibilityLevel: 6</c> tells a reviewer nothing. Declaring the intended values
    /// here instead means the configuration is readable, diffable, unit-testable and reproducible,
    /// and <see cref="ProjectConfigurator"/> can both apply it and detect drift.
    /// </para>
    /// <para>
    /// Every value below traces to a requirement. The reasoning is recorded next to the value
    /// rather than in a separate document, because a setting whose justification has been lost is a
    /// setting nobody will dare change later.
    /// </para>
    /// </remarks>
    public static class QuestProjectSettings
    {
        /// <summary>
        /// Minimum Android API level. Quest 2 and later run Android 10 (API 29); targeting lower
        /// buys nothing because no Quest device runs it.
        /// </summary>
        public const AndroidSdkVersions MinimumSdkVersion = AndroidSdkVersions.AndroidApiLevel29;

        /// <summary>
        /// Colour space. Linear is required for physically plausible lighting and for the soft
        /// gradients and bloom the PRD's visual style depends on; gamma space makes the dark
        /// environment band visibly.
        /// </summary>
        public const ColorSpace RequiredColorSpace = ColorSpace.Linear;

        /// <summary>
        /// Scripting backend. IL2CPP is mandatory: Meta rejects Mono builds from the Horizon Store,
        /// and ahead-of-time compilation materially improves frame time on mobile hardware.
        /// </summary>
        public const ScriptingImplementation RequiredScriptingBackend = ScriptingImplementation.IL2CPP;

        /// <summary>
        /// CPU architecture. ARM64 only. Quest has no 32-bit devices, and shipping ARMv7 alongside
        /// would roughly double build time and package size for nothing.
        /// </summary>
        public const AndroidArchitecture RequiredArchitectures = AndroidArchitecture.ARM64;

        /// <summary>
        /// Texture compression. ASTC is the only format offering good quality per byte across the
        /// whole Adreno range used by Quest, and directly serves TRD 18's compressed texture rule.
        /// </summary>
        public const MobileTextureSubtarget RequiredTextureCompression = MobileTextureSubtarget.ASTC;

        /// <summary>
        /// Graphics API preference order. Vulkan first: it is the only API on Quest 3 that supports
        /// Application SpaceWarp and it has materially lower driver overhead. OpenGL ES 3 is
        /// retained as a fallback so a device or driver that fails to initialise Vulkan still runs
        /// rather than showing a black screen.
        /// </summary>
        public static readonly GraphicsDeviceType[] RequiredGraphicsApis =
        {
            GraphicsDeviceType.Vulkan,
            GraphicsDeviceType.OpenGLES3
        };

        /// <summary>
        /// Target frame rate. The PRD requires 90 FPS and the TRD accepts 72 FPS as a floor. The
        /// display refresh rate is ultimately chosen by the runtime per device, so this is the
        /// budget the content is authored against rather than a guarantee.
        /// </summary>
        public const int TargetFrameRate = 90;

        /// <summary>
        /// Frame budget in milliseconds implied by <see cref="TargetFrameRate"/>. Anything the main
        /// thread does per frame must fit inside this, which is the number the performance
        /// monitor warns against.
        /// </summary>
        public const float FrameBudgetMilliseconds = 1000f / TargetFrameRate;

        /// <summary>
        /// Whether the graphics jobs and multithreaded rendering paths are required. Both move
        /// command buffer construction off the main thread, which is where the frame budget is
        /// usually lost on Quest.
        /// </summary>
        public const bool RequireMultithreadedRendering = true;

        /// <summary>
        /// Whether GPU skinning is required. Skinned meshes appear throughout the experience — the
        /// brain, the inner ear fluid, the spider — and skinning them on the CPU competes directly
        /// with the simulation for main-thread time.
        /// </summary>
        public const bool RequireGpuSkinning = true;

        /// <summary>
        /// Whether static batching is required. Reduces draw calls for the immobile environment
        /// geometry that makes up most of every scene (TRD 5, PRD performance requirements).
        /// </summary>
        public const bool RequireStaticBatching = true;

        /// <summary>
        /// Product name shown beneath the application icon in the Quest library.
        /// </summary>
        public const string ProductName = "How Virtual Reality Tricks Your Brain";

        /// <summary>
        /// Reverse-DNS application identifier. Must be stable: changing it after release makes the
        /// store treat the build as a different application and orphans installed users' save data.
        /// </summary>
        public const string ApplicationIdentifier = "com.synchrony.vrsimulation";
    }
}
