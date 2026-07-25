using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using VRSimulation.Bootstrap;
using VRSimulation.Core.Data;
using VRSimulation.Core.Diagnostics;
using VRSimulation.Utilities;

namespace VRSimulation.Player
{
    /// <summary>
    /// Supplies a plausible eye height when the tracking system cannot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <see cref="XROrigin.TrackingOriginMode.Floor"/> the runtime is responsible for placing
    /// the camera at the player's real height, which is what makes room-scale correct on a headset.
    /// When no XR device is present the runtime supplies nothing and the camera rests at the origin,
    /// on the floor. Anyone previewing on a desktop is then looking at the underside of the
    /// geometry, and the obvious remedy — switching to
    /// <see cref="XROrigin.TrackingOriginMode.Device"/> — would sacrifice correct standing height on
    /// the actual target hardware.
    /// </para>
    /// <para>
    /// This component resolves that by leaving the tracking mode alone and offsetting only when
    /// there is demonstrably no tracking data. On a Quest it does nothing at all. On a desktop it
    /// makes the preview usable, and it doubles as the recovery path for the PRD's tracking-loss
    /// case, where the height would otherwise collapse to the floor mid-session.
    /// </para>
    /// <para>
    /// A calibrated height from the player's settings takes precedence over the generic fallback,
    /// so a player who has told the experience how tall they are gets their own value rather than
    /// an average one (TRD 9).
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder.PlayerRig)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    public sealed class TrackingHeightFallback : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Eye height applied when no tracking data is available and the player has not " +
                 "calibrated. Overridden by a calibrated height from settings when one exists.")]
        [Range(SettingsDefaults.MinCalibratedHeightMeters, SettingsDefaults.MaxCalibratedHeightMeters)]
        private float fallbackEyeHeight = SettingsDefaults.FallbackEyeHeightMeters;

        [SerializeField]
        [Tooltip("How often to re-check for tracking, in seconds. Allows the height to correct " +
                 "itself if a headset connects, or tracking recovers, after startup.")]
        [Range(0.25f, 5f)]
        private float recheckInterval = 1f;

        /// <summary>The origin whose camera offset is adjusted.</summary>
        private XROrigin origin;

        /// <summary>Seconds until the next tracking check.</summary>
        private float timeUntilRecheck;

        /// <summary>Whether the fallback offset is currently applied.</summary>
        private bool fallbackApplied;

        /// <summary>Caches the origin reference.</summary>
        private void Awake()
        {
            origin = GetComponent<XROrigin>();
        }

        /// <summary>Applies the fallback immediately so the very first rendered frame is correct.</summary>
        private void Start()
        {
            Evaluate();
        }

        /// <summary>
        /// Periodically re-evaluates, so a headset connecting later is honoured.
        /// </summary>
        /// <remarks>
        /// Polled on an interval rather than every frame. There is no allocation-free event for
        /// "tracking became available", and checking sixty times a second to observe something that
        /// changes at most a handful of times per session would be wasteful on a mobile CPU.
        /// </remarks>
        private void Update()
        {
            timeUntilRecheck -= Time.unscaledDeltaTime;
            if (timeUntilRecheck > 0f)
            {
                return;
            }

            timeUntilRecheck = recheckInterval;
            Evaluate();
        }

        /// <summary>
        /// Applies or removes the fallback offset according to tracking availability.
        /// </summary>
        private void Evaluate()
        {
            if (origin == null || origin.CameraFloorOffsetObject == null)
            {
                return;
            }

            bool trackingAvailable = IsTrackingAvailable();

            if (trackingAvailable)
            {
                if (fallbackApplied)
                {
                    // Tracking arrived. Hand height back to the runtime rather than leaving a
                    // synthetic offset stacked on top of a real one, which would place the player a
                    // metre and a half above their own head.
                    origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
                    fallbackApplied = false;

                    Log(LogSeverity.Info, "Tracking became available; synthetic eye height removed.");
                }

                return;
            }

            float height = ResolveEyeHeight();
            var offset = new Vector3(0f, height, 0f);

            if (fallbackApplied &&
                Mathf.Approximately(origin.CameraFloorOffsetObject.transform.localPosition.y, height))
            {
                return;
            }

            origin.CameraFloorOffsetObject.transform.localPosition = offset;

            if (!fallbackApplied)
            {
                fallbackApplied = true;
                Log(LogSeverity.Info,
                    $"No tracking data available; using a synthetic eye height of {height:F2}m. " +
                    "This is expected when previewing without a headset.");
            }
        }

        /// <summary>
        /// Determines whether the XR runtime is supplying tracking data.
        /// </summary>
        /// <returns><c>true</c> when a device is active and presenting.</returns>
        private static bool IsTrackingAvailable()
        {
            // isDeviceActive covers both "no headset attached" and "XR failed to initialise",
            // which are the two cases that leave the camera unpositioned.
            return XRSettings.isDeviceActive;
        }

        /// <summary>
        /// Chooses the eye height to apply, preferring the player's calibrated value.
        /// </summary>
        /// <returns>An eye height in metres, within the accepted range.</returns>
        private float ResolveEyeHeight()
        {
            ExperienceRoot root = ExperienceRoot.Instance;

            if (root != null && root.IsReady && root.Settings != null)
            {
                float calibrated = root.Settings.Current.calibratedHeightMeters;

                // Zero is the documented "not calibrated" sentinel rather than a real height.
                if (calibrated > 0f)
                {
                    return Mathf.Clamp(
                        calibrated,
                        SettingsDefaults.MinCalibratedHeightMeters,
                        SettingsDefaults.MaxCalibratedHeightMeters);
                }
            }

            return Mathf.Clamp(
                fallbackEyeHeight,
                SettingsDefaults.MinCalibratedHeightMeters,
                SettingsDefaults.MaxCalibratedHeightMeters);
        }

        /// <summary>
        /// Writes a diagnostic entry, tolerating a root that has not started yet.
        /// </summary>
        /// <param name="severity">Entry severity.</param>
        /// <param name="message">Entry text.</param>
        private static void Log(LogSeverity severity, string message)
        {
            ExperienceRoot root = ExperienceRoot.Instance;
            root?.Logger?.Log(severity, LogCategory.Player, message);
        }
    }
}
