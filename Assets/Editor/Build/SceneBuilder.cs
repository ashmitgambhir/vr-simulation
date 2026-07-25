using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRSimulation.Bootstrap;
using VRSimulation.Core.Data;
using VRSimulation.Player;

namespace VRSimulation.EditorTools.Build
{
    /// <summary>
    /// Generates the project's scenes from code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity scenes are serialised YAML full of numeric file identifiers. Two developers editing
    /// the same scene produce a conflict no one can resolve by reading the diff, and a reviewer
    /// cannot tell from a pull request whether the lighting changed or a light was deleted.
    /// Generating scenes from a script instead makes their contents reviewable, reproducible on any
    /// machine, and trivially regenerated after a mistake.
    /// </para>
    /// <para>
    /// The generated scenes are still committed, because the project must open normally for anyone
    /// without running a build step first. The script is the source of truth; the scene is its
    /// output, and regenerating it is always safe.
    /// </para>
    /// </remarks>
    public static class SceneBuilder
    {
        /// <summary>Directory holding generated scenes.</summary>
        private const string SceneDirectory = "Assets/Scenes";

        /// <summary>Directory holding generated materials.</summary>
        private const string MaterialDirectory = "Assets/Materials";

        /// <summary>URP lit shader, used for surfaces that receive light.</summary>
        private const string LitShader = "Universal Render Pipeline/Lit";

        /// <summary>URP unlit shader, used for self-illuminated surfaces.</summary>
        private const string UnlitShader = "Universal Render Pipeline/Unlit";

        // -- Palette, from the PRD visual style: dark environment, soft lighting, blue accents ----

        /// <summary>Near-black environment base, with a slight blue bias so it never reads as dead grey.</summary>
        private static readonly Color EnvironmentBase = new Color(0.031f, 0.035f, 0.047f, 1f);

        /// <summary>Primary blue accent used for glowing surfaces and interface highlights.</summary>
        private static readonly Color AccentBlue = new Color(0.243f, 0.612f, 1f, 1f);

        /// <summary>Emission colour for the platform, brighter than the accent so it blooms.</summary>
        private static readonly Color PlatformGlow = new Color(0.298f, 0.702f, 1f, 1f);

        /// <summary>
        /// Builds every scene the project needs and registers them in build settings.
        /// </summary>
        /// <remarks>
        /// Run headlessly with:
        /// <code>
        /// Unity -batchmode -quit -projectPath . \
        ///       -executeMethod VRSimulation.EditorTools.Build.SceneBuilder.BuildAllScenes
        /// </code>
        /// </remarks>
        [MenuItem("VR Simulation/Rebuild Scenes")]
        public static void BuildAllScenes()
        {
            EnsureDirectories();

            string bootstrapPath = BuildBootstrapScene();

            RegisterBuildScenes(new[] { bootstrapPath });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] Scenes rebuilt.\n  {bootstrapPath}");
        }

        /// <summary>
        /// Builds the persistent bootstrap scene.
        /// </summary>
        /// <remarks>
        /// This scene holds the composition root and the player rig, and is the scene the player
        /// actually launches into. Module content is loaded additively on top of it so that the
        /// services and the rig survive every transition rather than being rebuilt per module,
        /// which is what keeps transitions inside the three second budget in TRD 5.
        /// </remarks>
        /// <returns>The asset path of the saved scene.</returns>
        private static string BuildBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureAmbience();
            CreateExperienceRoot();
            CreatePlayerRig();
            CreateIntroductionEnvironment();
            CreateLighting();

            string path = Path.Combine(SceneDirectory, SceneId.Bootstrap + ".unity");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        /// <summary>
        /// Sets the scene-wide atmosphere described by the PRD: a dark room with soft blue light.
        /// </summary>
        private static void ConfigureAmbience()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            // Ambient light is deliberately not black. A pure black ambient makes unlit faces
            // vanish entirely, which in a headset reads as geometry with holes in it rather than as
            // darkness.
            RenderSettings.ambientLight = new Color(0.09f, 0.11f, 0.16f, 1f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = EnvironmentBase;

            // Light fog, per TRD 17. Enough to fade the floor into the dark at the room's edge so
            // it has no visible boundary, without hazing objects within arm's reach.
            RenderSettings.fogDensity = 0.035f;

            RenderSettings.skybox = null;
        }

        /// <summary>
        /// Creates the composition root object.
        /// </summary>
        private static void CreateExperienceRoot()
        {
            var root = new GameObject("ExperienceRoot");
            root.AddComponent<ExperienceRoot>();
        }

        /// <summary>
        /// Creates the XR rig: origin, camera offset and head camera.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The hierarchy follows the XR Origin convention. The origin is the player's position in
        /// the virtual world; the camera offset applies the tracking-space height; the camera is
        /// driven by the headset pose. Nothing may move the camera directly — locomotion moves the
        /// origin — because a camera whose transform disagrees with the tracked pose produces
        /// exactly the mismatch this experience teaches about.
        /// </para>
        /// <para>
        /// Tracking origin mode is set to Floor so that the player's real height is honoured
        /// (TRD 9, "automatic calibration"). A seated player is accommodated by the calibration
        /// offset rather than by changing this mode, so the same content works for both.
        /// </para>
        /// </remarks>
        private static void CreatePlayerRig()
        {
            var originObject = new GameObject("XR Origin");
            var origin = originObject.AddComponent<XROrigin>();

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = EnvironmentBase;

            // A near plane of 0.01 lets the player bring an object close to their face without it
            // clipping, which they will do the moment they are handed something.
            camera.nearClipPlane = 0.01f;

            // Far plane kept short: the rooms are small, and a shorter range gives the depth buffer
            // more precision, which reduces z-fighting on the coplanar interface panels.
            camera.farClipPlane = 100f;

            cameraObject.AddComponent<AudioListener>();

            origin.Camera = camera;
            origin.CameraFloorOffsetObject = cameraOffset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // CameraYOffset is deliberately not set here. It only takes effect in Device tracking
            // mode; under Floor mode the runtime owns the height, and with no headset attached it
            // supplies none, leaving the camera on the floor. Rather than sacrifice correct
            // room-scale height on the actual target hardware, the desktop case is handled at
            // runtime by TrackingHeightFallback, which offsets only when tracking is genuinely
            // absent and steps aside the moment it appears.
            originObject.AddComponent<TrackingHeightFallback>();
        }

        /// <summary>
        /// Builds the PRD's opening image: a black room containing one glowing platform.
        /// </summary>
        private static void CreateIntroductionEnvironment()
        {
            var environment = new GameObject("Environment");

            Material floorMaterial = CreateMaterial(
                "M_EnvironmentFloor", LitShader, EnvironmentBase, smoothness: 0.65f);

            Material platformMaterial = CreateMaterial(
                "M_PlatformGlow", UnlitShader, PlatformGlow);

            Material ringMaterial = CreateMaterial(
                "M_AccentRing", UnlitShader, AccentBlue);

            GameObject floor = CreatePrimitive(
                PrimitiveType.Plane, "Floor", environment.transform,
                position: Vector3.zero, scale: new Vector3(4f, 1f, 4f), material: floorMaterial);

            // The floor is the one surface the player is always standing on, so it is the one that
            // most needs to receive shadows and least needs to cast them.
            SetShadows(floor, castShadows: false, receiveShadows: true);

            GameObject platform = CreatePrimitive(
                PrimitiveType.Cylinder, "GlowingPlatform", environment.transform,
                position: new Vector3(0f, 0.02f, 0f),
                scale: new Vector3(2.4f, 0.02f, 2.4f),
                material: platformMaterial);

            SetShadows(platform, castShadows: false, receiveShadows: false);

            GameObject ring = CreatePrimitive(
                PrimitiveType.Cylinder, "AccentRing", environment.transform,
                position: new Vector3(0f, 0.015f, 0f),
                scale: new Vector3(2.9f, 0.015f, 2.9f),
                material: ringMaterial);

            SetShadows(ring, castShadows: false, receiveShadows: false);

            // A reference object at eye height, so that pressing Play shows something with
            // recognisable scale and shading rather than a flat disc. It also gives the eventual
            // grab interaction something to act on.
            Material accentMaterial = CreateMaterial(
                "M_AccentSurface", LitShader, AccentBlue, smoothness: 0.8f, metallic: 0.1f);

            GameObject marker = CreatePrimitive(
                PrimitiveType.Cube, "ReferenceCube", environment.transform,
                position: new Vector3(0f, 1.2f, 1.6f),
                scale: new Vector3(0.25f, 0.25f, 0.25f),
                material: accentMaterial);

            SetShadows(marker, castShadows: true, receiveShadows: true);
        }

        /// <summary>
        /// Creates the soft key light and a cool fill.
        /// </summary>
        private static void CreateLighting()
        {
            var lighting = new GameObject("Lighting");

            var keyObject = new GameObject("Key Light");
            keyObject.transform.SetParent(lighting.transform, false);
            keyObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.78f, 0.86f, 1f, 1f);

            // Low intensity: the PRD asks for a dark environment with soft lighting, and a
            // conventionally lit directional light would flatten the glow of the platform, which is
            // meant to be the only real light source in the room.
            key.intensity = 0.55f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.6f;

            // The platform reads as emissive, but an unlit material emits no actual light. This
            // point light supplies the illumination the glow implies, so objects near the platform
            // are lit from below as the player expects.
            var glowObject = new GameObject("Platform Fill");
            glowObject.transform.SetParent(lighting.transform, false);
            glowObject.transform.position = new Vector3(0f, 0.35f, 0f);

            var glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = AccentBlue;
            glow.intensity = 2.2f;
            glow.range = 6f;
            glow.shadows = LightShadows.None;
        }

        // -- Helpers -------------------------------------------------------------------------

        /// <summary>
        /// Creates a primitive with no collider, parented and positioned.
        /// </summary>
        /// <remarks>
        /// Colliders are removed rather than kept. Unity's primitives ship with them, and leaving
        /// them on decorative geometry means the XR interaction ray hits the floor and the walls,
        /// which makes pointing at anything unreliable. Colliders are added deliberately, only to
        /// things meant to be interacted with.
        /// </remarks>
        /// <param name="type">Primitive shape.</param>
        /// <param name="name">Object name.</param>
        /// <param name="parent">Parent transform.</param>
        /// <param name="position">Local position.</param>
        /// <param name="scale">Local scale.</param>
        /// <param name="material">Material to assign.</param>
        /// <returns>The created object.</returns>
        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject created = GameObject.CreatePrimitive(type);
            created.name = name;
            created.transform.SetParent(parent, false);
            created.transform.localPosition = position;
            created.transform.localScale = scale;

            Collider collider = created.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = created.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return created;
        }

        /// <summary>
        /// Sets shadow casting and receiving on a renderer.
        /// </summary>
        /// <param name="target">Object whose renderer to configure.</param>
        /// <param name="castShadows">Whether the object casts shadows.</param>
        /// <param name="receiveShadows">Whether the object receives shadows.</param>
        private static void SetShadows(GameObject target, bool castShadows, bool receiveShadows)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            renderer.receiveShadows = receiveShadows;
        }

        /// <summary>
        /// Creates or updates a shared material asset.
        /// </summary>
        /// <remarks>
        /// Materials are assets rather than instances created at runtime. Assigning
        /// <c>renderer.material</c> silently clones the material per object, which defeats the SRP
        /// batcher and leaks a material per renderer — a classic cause of both draw call inflation
        /// and growing memory on a headset.
        /// </remarks>
        /// <param name="name">Asset name.</param>
        /// <param name="shaderName">Shader to use.</param>
        /// <param name="color">Base colour.</param>
        /// <param name="smoothness">Surface smoothness, where the shader supports it.</param>
        /// <param name="metallic">Metallic value, where the shader supports it.</param>
        /// <returns>The material asset, or <c>null</c> if the shader is missing.</returns>
        private static Material CreateMaterial(
            string name,
            string shaderName,
            Color color,
            float smoothness = 0.5f,
            float metallic = 0f)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[SceneBuilder] Shader '{shaderName}' was not found. " +
                    "The render pipeline may not be configured; run 'VR Simulation/Configure Render Pipeline'.");
                return null;
            }

            string path = Path.Combine(MaterialDirectory, name + ".mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Replaces the build settings scene list.
        /// </summary>
        /// <param name="scenePaths">Scene asset paths, in load order.</param>
        private static void RegisterBuildScenes(IReadOnlyList<string> scenePaths)
        {
            var entries = new EditorBuildSettingsScene[scenePaths.Count];
            for (int i = 0; i < scenePaths.Count; i++)
            {
                entries[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            }

            EditorBuildSettings.scenes = entries;
        }

        /// <summary>
        /// Ensures the output directories exist.
        /// </summary>
        private static void EnsureDirectories()
        {
            foreach (string directory in new[] { SceneDirectory, MaterialDirectory })
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
