using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VRSimulation.Bootstrap;
using VRSimulation.Core.Data;
using VRSimulation.Core.Interfaces;

namespace VRSimulation.Tests.PlayMode
{
    /// <summary>
    /// Verifies that the bootstrap scene actually starts up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The EditMode suite proves the services behave correctly in isolation. It cannot prove that
    /// the scene is wired to use them: a missing component, a broken reference or an exception in
    /// <c>Awake</c> would leave every EditMode test green while the application fails on launch.
    /// </para>
    /// <para>
    /// These tests enter play mode against the real scene, which is the only way to catch that
    /// class of failure without a person putting on a headset.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class BootstrapSceneTests
    {
        /// <summary>Name of the scene under test, resolved from the shared scene identifier.</summary>
        private static readonly string BootstrapSceneName = SceneId.Bootstrap.ToString();

        /// <summary>
        /// Loads the bootstrap scene and yields until its first frame has run.
        /// </summary>
        /// <returns>Coroutine enumerator for the test runner.</returns>
        private static IEnumerator LoadBootstrapScene()
        {
            yield return SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);

            // Awake and Start have run by the end of the load, but a frame is yielded anyway so
            // that anything deferred to the first Update has also happened before assertions.
            yield return null;
        }

        /// <summary>The composition root exists and finished startup.</summary>
        [UnityTest]
        public IEnumerator BootstrapScene_StartsUp_RootIsReady()
        {
            yield return LoadBootstrapScene();

            Assert.That(ExperienceRoot.Instance, Is.Not.Null,
                "The bootstrap scene must contain an ExperienceRoot.");
            Assert.That(ExperienceRoot.Instance.IsReady, Is.True,
                "Startup did not complete; an exception during Awake is the usual cause.");
        }

        /// <summary>Every service the rest of the application resolves is constructed.</summary>
        [UnityTest]
        public IEnumerator BootstrapScene_ComposesAllServices()
        {
            yield return LoadBootstrapScene();

            ExperienceRoot root = ExperienceRoot.Instance;

            Assert.That(root.Logger, Is.Not.Null);
            Assert.That(root.SaveService, Is.Not.Null);
            Assert.That(root.Settings, Is.Not.Null);
        }

        /// <summary>The save loads, and the outcome is one the interface can report.</summary>
        [UnityTest]
        public IEnumerator BootstrapScene_LoadsSave()
        {
            yield return LoadBootstrapScene();

            ISaveService save = ExperienceRoot.Instance.SaveService;

            Assert.That(save.IsLoaded, Is.True, "The save must be loaded before the first module runs.");
            Assert.That(save.Data, Is.Not.Null);
            Assert.That(save.Data.settings, Is.Not.Null);
        }

        /// <summary>Settings are available and default to the comfort-first configuration.</summary>
        [UnityTest]
        public IEnumerator BootstrapScene_SettingsDefaultToComfortFirst()
        {
            yield return LoadBootstrapScene();

            UserSettingsData settings = ExperienceRoot.Instance.Settings.Current;

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.EffectiveLocomotion, Is.EqualTo(LocomotionMode.Teleport),
                "A first-time player must not be given smooth locomotion.");
            Assert.That(settings.EffectiveTurning, Is.EqualTo(TurnMode.Snap));
        }

        /// <summary>The player rig exists with a camera, so the scene renders from the head pose.</summary>
        [UnityTest]
        public IEnumerator BootstrapScene_HasPlayerRigWithCamera()
        {
            yield return LoadBootstrapScene();

            var origin = Object.FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();

            Assert.That(origin, Is.Not.Null, "The scene must contain an XR Origin.");
            Assert.That(origin.Camera, Is.Not.Null, "The XR Origin must have a camera assigned.");
            Assert.That(Camera.main, Is.Not.Null, "The head camera must be tagged MainCamera.");
        }

        /// <summary>
        /// The camera sits at a plausible eye height even with no headset attached.
        /// </summary>
        /// <remarks>
        /// Without a tracking origin fallback the camera rests at the floor, and the first thing
        /// anyone previewing on a desktop sees is the underside of the platform. This is the test
        /// that keeps desktop preview usable.
        /// </remarks>
        [UnityTest]
        public IEnumerator BootstrapScene_CameraIsAtEyeHeightWithoutHeadset()
        {
            yield return LoadBootstrapScene();

            float cameraHeight = Camera.main.transform.position.y;

            Assert.That(cameraHeight, Is.GreaterThan(0.5f),
                $"Camera is at y={cameraHeight:F2}, which is at or below the floor.");
        }

        /// <summary>The scene renders something; an empty room would mean the build produced nothing.</summary>
        [UnityTest]
        public IEnumerator BootstrapScene_ContainsVisibleGeometryAndLighting()
        {
            yield return LoadBootstrapScene();

            var renderers = Object.FindObjectsOfType<MeshRenderer>();
            var lights = Object.FindObjectsOfType<Light>();

            Assert.That(renderers.Length, Is.GreaterThan(0), "The scene has no visible geometry.");
            Assert.That(lights.Length, Is.GreaterThan(0), "The scene has no lighting.");

            foreach (MeshRenderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null,
                    $"'{renderer.name}' has no material and would render as magenta.");
            }
        }

        /// <summary>
        /// A second root loaded on top of the first removes itself rather than replacing it.
        /// </summary>
        /// <remarks>
        /// Reloading the bootstrap scene is the realistic way this happens — returning to the main
        /// menu, or an additive load that includes it by mistake. Replacing the live root would
        /// orphan every reference already handed out and discard unsaved progress.
        /// </remarks>
        [UnityTest]
        public IEnumerator BootstrapScene_SecondRootIsDiscarded()
        {
            yield return LoadBootstrapScene();

            ExperienceRoot first = ExperienceRoot.Instance;

            var intruder = new GameObject("DuplicateRoot");
            intruder.AddComponent<ExperienceRoot>();
            yield return null;

            Assert.That(ExperienceRoot.Instance, Is.SameAs(first),
                "The original root must remain the live one.");
        }
    }
}
