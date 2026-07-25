using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRSimulation.EditorTools.Build
{
    /// <summary>
    /// Creates and assigns the Universal Render Pipeline assets the Quest target requires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// URP is a deliberate choice over the built-in pipeline. The built-in pipeline has no SRP
    /// batcher, which matters because the experience is built from many small emissive objects —
    /// neural pathways, floating interface panels, the latency timeline — and each one would
    /// otherwise cost a separate draw call against a mobile GPU budget. URP also provides the
    /// post-processing stack the PRD's visual style depends on, and single-pass instanced stereo
    /// rendering, which roughly halves per-eye CPU cost.
    /// </para>
    /// <para>
    /// The assets are generated rather than committed as binary, so their settings are visible in
    /// this file and reviewable in a diff. Running this is idempotent: existing assets are updated
    /// in place so that references from materials and scenes survive.
    /// </para>
    /// </remarks>
    public static class RenderPipelineConfigurator
    {
        /// <summary>Directory holding generated render pipeline assets.</summary>
        private const string SettingsDirectory = "Assets/Settings";

        /// <summary>Path of the renderer data asset.</summary>
        private const string RendererAssetPath = SettingsDirectory + "/QuestUniversalRenderer.asset";

        /// <summary>Path of the pipeline asset.</summary>
        private const string PipelineAssetPath = SettingsDirectory + "/QuestUniversalRenderPipeline.asset";

        /// <summary>
        /// Shadow distance in metres. Kept short because every metre of shadow distance costs
        /// shadow map resolution, and the experience is set in small rooms where distant shadows
        /// are never visible.
        /// </summary>
        private const float ShadowDistanceMeters = 15f;

        /// <summary>
        /// Render scale. Left at native resolution; the Quest runtime applies its own dynamic
        /// resolution, and scaling here as well compounds into a visibly soft image.
        /// </summary>
        private const float RenderScale = 1.0f;

        /// <summary>
        /// Creates the pipeline assets if absent, applies the Quest-appropriate settings, and makes
        /// the pipeline active.
        /// </summary>
        /// <remarks>
        /// Run headlessly with:
        /// <code>
        /// Unity -batchmode -quit -projectPath . \
        ///       -executeMethod VRSimulation.EditorTools.Build.RenderPipelineConfigurator.Configure
        /// </code>
        /// </remarks>
        [MenuItem("VR Simulation/Configure Render Pipeline")]
        public static void Configure()
        {
            EnsureDirectory();

            UniversalRendererData rendererData = LoadOrCreateRendererData();
            UniversalRenderPipelineAsset pipeline = LoadOrCreatePipeline(rendererData);

            ApplyQuestSettings(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[RenderPipelineConfigurator] URP active.\n" +
                $"  pipeline       : {PipelineAssetPath}\n" +
                $"  renderer       : {RendererAssetPath}\n" +
                $"  shadowDistance : {ShadowDistanceMeters}m\n" +
                $"  renderScale    : {RenderScale}");
        }

        /// <summary>
        /// Ensures the settings directory exists.
        /// </summary>
        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Loads the renderer data asset, creating it on first run.
        /// </summary>
        /// <returns>The renderer data asset.</returns>
        private static UniversalRendererData LoadOrCreateRendererData()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var created = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(created, RendererAssetPath);
            return created;
        }

        /// <summary>
        /// Loads the pipeline asset, creating it on first run.
        /// </summary>
        /// <param name="rendererData">Renderer the pipeline should use.</param>
        /// <returns>The pipeline asset.</returns>
        private static UniversalRenderPipelineAsset LoadOrCreatePipeline(UniversalRendererData rendererData)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (existing != null)
            {
                return existing;
            }

            UniversalRenderPipelineAsset created = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(created, PipelineAssetPath);
            return created;
        }

        /// <summary>
        /// Applies the settings appropriate to a mobile standalone headset.
        /// </summary>
        /// <param name="pipeline">The pipeline asset to configure.</param>
        private static void ApplyQuestSettings(UniversalRenderPipelineAsset pipeline)
        {
            // HDR off: the Quest display is 8 bit per channel, so an HDR buffer costs bandwidth and
            // an extra resolve for a range the panel cannot show.
            pipeline.supportsHDR = false;

            // 4x MSAA. Aliasing on high-contrast edges is far more objectionable in a headset than
            // on a monitor, because head motion makes edge crawl constant rather than occasional,
            // and MSAA is comparatively cheap on tile-based mobile GPUs.
            pipeline.msaaSampleCount = 4;

            pipeline.renderScale = RenderScale;
            pipeline.shadowDistance = ShadowDistanceMeters;

            // A single shadow cascade. Cascades exist for large outdoor scenes; in rooms a few
            // metres across they only divide the shadow map resolution for no visible gain.
            pipeline.shadowCascadeCount = 1;

            // Depth texture is needed by soft particles and by the comfort vignette, both of which
            // the PRD's visual style relies on.
            pipeline.supportsCameraDepthTexture = true;

            // Opaque texture is not: nothing in the experience refracts or distorts the background,
            // and the copy costs bandwidth every frame.
            pipeline.supportsCameraOpaqueTexture = false;
        }
    }
}
