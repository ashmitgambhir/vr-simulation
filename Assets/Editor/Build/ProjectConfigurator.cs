using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRSimulation.EditorTools.Build
{
    /// <summary>
    /// Applies and verifies the Meta Quest project configuration described by
    /// <see cref="QuestProjectSettings"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is applied by code rather than committed as serialised YAML for three reasons.
    /// It is reviewable, because the intent and its justification are readable in a diff. It is
    /// verifiable, because <see cref="Verify"/> can detect drift and fail a build rather than
    /// silently shipping the wrong colour space. And it is reproducible, because both entry points
    /// run headlessly through <c>-executeMethod</c> on a machine with no Unity window open.
    /// </para>
    /// <para>
    /// The two entry points are deliberately separate. <see cref="Apply"/> mutates project settings
    /// and is run by a developer or a provisioning step; <see cref="Verify"/> never mutates
    /// anything and is safe to run in continuous integration on every commit.
    /// </para>
    /// </remarks>
    public static class ProjectConfigurator
    {
        /// <summary>Menu path for the apply command.</summary>
        private const string ApplyMenuPath = "VR Simulation/Configure Project for Meta Quest";

        /// <summary>Menu path for the verification command.</summary>
        private const string VerifyMenuPath = "VR Simulation/Verify Quest Configuration";

        /// <summary>
        /// Applies every required setting for the Quest target.
        /// </summary>
        /// <remarks>
        /// Run headlessly with:
        /// <code>
        /// Unity -batchmode -quit -projectPath . \
        ///       -executeMethod VRSimulation.EditorTools.Build.ProjectConfigurator.Apply
        /// </code>
        /// Safe to run repeatedly; every operation is idempotent.
        /// </remarks>
        [MenuItem(ApplyMenuPath)]
        public static void Apply()
        {
            var namedTarget = NamedBuildTarget.Android;
            var changes = new List<string>();

            // -- Identity ------------------------------------------------------------------
            if (PlayerSettings.productName != QuestProjectSettings.ProductName)
            {
                PlayerSettings.productName = QuestProjectSettings.ProductName;
                changes.Add($"productName -> {QuestProjectSettings.ProductName}");
            }

            if (PlayerSettings.GetApplicationIdentifier(namedTarget) != QuestProjectSettings.ApplicationIdentifier)
            {
                PlayerSettings.SetApplicationIdentifier(namedTarget, QuestProjectSettings.ApplicationIdentifier);
                changes.Add($"applicationIdentifier -> {QuestProjectSettings.ApplicationIdentifier}");
            }

            // -- Rendering -----------------------------------------------------------------
            if (PlayerSettings.colorSpace != QuestProjectSettings.RequiredColorSpace)
            {
                // Changing colour space forces a full reimport of every texture, which is slow but
                // unavoidable and far better discovered now than after the art is authored.
                PlayerSettings.colorSpace = QuestProjectSettings.RequiredColorSpace;
                changes.Add($"colorSpace -> {QuestProjectSettings.RequiredColorSpace}");
            }

            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                changes.Add("useDefaultGraphicsAPIs -> false");
            }

            GraphicsDeviceType[] currentApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (!SequenceMatches(currentApis, QuestProjectSettings.RequiredGraphicsApis))
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, QuestProjectSettings.RequiredGraphicsApis);
                changes.Add("graphicsAPIs -> Vulkan, OpenGLES3");
            }

            if (PlayerSettings.GetMobileMTRendering(namedTarget) != QuestProjectSettings.RequireMultithreadedRendering)
            {
                PlayerSettings.SetMobileMTRendering(namedTarget, QuestProjectSettings.RequireMultithreadedRendering);
                changes.Add($"multithreadedRendering -> {QuestProjectSettings.RequireMultithreadedRendering}");
            }

            if (PlayerSettings.gpuSkinning != QuestProjectSettings.RequireGpuSkinning)
            {
                PlayerSettings.gpuSkinning = QuestProjectSettings.RequireGpuSkinning;
                changes.Add($"gpuSkinning -> {QuestProjectSettings.RequireGpuSkinning}");
            }

            // -- Scripting and architecture -------------------------------------------------
            if (PlayerSettings.GetScriptingBackend(namedTarget) != QuestProjectSettings.RequiredScriptingBackend)
            {
                PlayerSettings.SetScriptingBackend(namedTarget, QuestProjectSettings.RequiredScriptingBackend);
                changes.Add($"scriptingBackend -> {QuestProjectSettings.RequiredScriptingBackend}");
            }

            if (PlayerSettings.Android.targetArchitectures != QuestProjectSettings.RequiredArchitectures)
            {
                PlayerSettings.Android.targetArchitectures = QuestProjectSettings.RequiredArchitectures;
                changes.Add($"targetArchitectures -> {QuestProjectSettings.RequiredArchitectures}");
            }

            if (PlayerSettings.Android.minSdkVersion != QuestProjectSettings.MinimumSdkVersion)
            {
                PlayerSettings.Android.minSdkVersion = QuestProjectSettings.MinimumSdkVersion;
                changes.Add($"minSdkVersion -> {QuestProjectSettings.MinimumSdkVersion}");
            }

            // -- Batching and texture format -------------------------------------------------
            if (EditorUserBuildSettings.androidBuildSubtarget != QuestProjectSettings.RequiredTextureCompression)
            {
                EditorUserBuildSettings.androidBuildSubtarget = QuestProjectSettings.RequiredTextureCompression;
                changes.Add($"textureCompression -> {QuestProjectSettings.RequiredTextureCompression}");
            }

            if (changes.Count == 0)
            {
                Debug.Log("[ProjectConfigurator] Configuration already correct; nothing changed.");
            }
            else
            {
                var builder = new StringBuilder("[ProjectConfigurator] Applied Quest configuration:");
                foreach (string change in changes)
                {
                    builder.Append("\n  - ").Append(change);
                }

                Debug.Log(builder.ToString());
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Checks every required setting without modifying anything.
        /// </summary>
        /// <remarks>
        /// Run in continuous integration with:
        /// <code>
        /// Unity -batchmode -quit -projectPath . \
        ///       -executeMethod VRSimulation.EditorTools.Build.ProjectConfigurator.Verify
        /// </code>
        /// Exits with a non-zero status when the project has drifted, so a pull request that
        /// changes the colour space or drops ARM64 fails rather than reaching a headset.
        /// </remarks>
        [MenuItem(VerifyMenuPath)]
        public static void Verify()
        {
            var namedTarget = NamedBuildTarget.Android;
            var problems = new List<string>();

            Check(problems,
                PlayerSettings.colorSpace == QuestProjectSettings.RequiredColorSpace,
                $"Colour space must be {QuestProjectSettings.RequiredColorSpace}, found {PlayerSettings.colorSpace}.");

            Check(problems,
                PlayerSettings.GetScriptingBackend(namedTarget) == QuestProjectSettings.RequiredScriptingBackend,
                $"Scripting backend must be {QuestProjectSettings.RequiredScriptingBackend}, " +
                $"found {PlayerSettings.GetScriptingBackend(namedTarget)}. " +
                "Mono builds are rejected by the Horizon Store.");

            Check(problems,
                PlayerSettings.Android.targetArchitectures == QuestProjectSettings.RequiredArchitectures,
                $"Target architectures must be {QuestProjectSettings.RequiredArchitectures}, " +
                $"found {PlayerSettings.Android.targetArchitectures}.");

            Check(problems,
                PlayerSettings.Android.minSdkVersion == QuestProjectSettings.MinimumSdkVersion,
                $"Minimum SDK must be {QuestProjectSettings.MinimumSdkVersion}, " +
                $"found {PlayerSettings.Android.minSdkVersion}.");

            Check(problems,
                SequenceMatches(
                    PlayerSettings.GetGraphicsAPIs(BuildTarget.Android),
                    QuestProjectSettings.RequiredGraphicsApis),
                "Graphics APIs must be exactly Vulkan then OpenGLES3, in that order.");

            Check(problems,
                PlayerSettings.GetMobileMTRendering(namedTarget) == QuestProjectSettings.RequireMultithreadedRendering,
                "Multithreaded rendering must be enabled to keep command buffer construction off the main thread.");

            Check(problems,
                PlayerSettings.gpuSkinning == QuestProjectSettings.RequireGpuSkinning,
                "GPU skinning must be enabled; CPU skinning competes with the simulation for main-thread time.");

            Check(problems,
                EditorUserBuildSettings.androidBuildSubtarget == QuestProjectSettings.RequiredTextureCompression,
                $"Texture compression must be {QuestProjectSettings.RequiredTextureCompression}.");

            if (problems.Count == 0)
            {
                Debug.Log("[ProjectConfigurator] Quest configuration verified; all settings correct.");
                return;
            }

            var builder = new StringBuilder(
                $"[ProjectConfigurator] Quest configuration has drifted ({problems.Count} problem(s)):");

            foreach (string problem in problems)
            {
                builder.Append("\n  - ").Append(problem);
            }

            builder.Append($"\nRun '{ApplyMenuPath}' to correct.");

            // LogError rather than an exception: in batch mode this marks the run as failed and
            // reports every problem at once, where an exception would stop at the first.
            Debug.LogError(builder.ToString());

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Records a description when a condition does not hold.
        /// </summary>
        /// <param name="problems">Accumulator for failure descriptions.</param>
        /// <param name="condition">The condition that must hold.</param>
        /// <param name="description">What is wrong, and why it matters, when it does not.</param>
        private static void Check(ICollection<string> problems, bool condition, string description)
        {
            if (!condition)
            {
                problems.Add(description);
            }
        }

        /// <summary>
        /// Compares two sequences for equal length, contents and order.
        /// </summary>
        /// <remarks>
        /// Order matters for graphics APIs: the first entry is the one Unity attempts first, so
        /// OpenGLES3 ahead of Vulkan is a materially different and worse configuration despite
        /// containing the same members.
        /// </remarks>
        /// <param name="actual">The current value.</param>
        /// <param name="expected">The required value.</param>
        /// <returns><c>true</c> if the sequences match exactly.</returns>
        private static bool SequenceMatches(GraphicsDeviceType[] actual, GraphicsDeviceType[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
            {
                return false;
            }

            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
