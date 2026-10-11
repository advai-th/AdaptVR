using UnityEngine;
using UnityEngine.UI;
using AdaptiveVR.Networking;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Shows/hides 3D spatial beacons, adaptive HUD panels, hint arrows, and
    /// encouragement banners in response to the cognitive-load state.
    ///
    ///   Low   → beacon ON + HUD full scaffolding + arrows + banner
    ///   Medium → beacon OFF + HUD standard
    ///   High  → beacon OFF + HUD minimal (removes all visual noise)
    ///
    /// All fields are optional — this script degrades gracefully if references are missing.
    /// Wire SpatialGuidanceBeacon and AdaptiveGuidanceHUD via the Inspector or
    /// AdaptiveSystemEditor's one-click setup.
    /// </summary>
    public class GuidanceAdapter : MonoBehaviour
    {
        [Header("3D Spatial Guidance")]
        [Tooltip("The SpatialGuidanceBeacon that pulses above the active orbit slot (legacy).")]
        public SpatialGuidanceBeacon spatialBeacon;

        [Tooltip("When disabled (recommended), the floating quad beacon is turned off to prevent visual clutter. The luminous socket pads and Companion Bot provide direct guidance.")]
        public bool enableSpatialBeacon = false;

        [Header("World-Space HUD (Optional)")]
        [Tooltip("The AdaptiveGuidanceHUD that shows objectives, progress, and hint text. " +
                 "Auto-wired by AdaptiveSystemEditor.")]
        public AdaptiveVR.UI.AdaptiveGuidanceHUD adaptiveHUD;

        [Tooltip("When disabled (recommended), the floating 2D HUD is turned off to prevent blocking the 3D VR solar system. The CompanionBot provides all dynamic hints.")]
        public bool enableAdaptiveHUD = false;

        private void Start()
        {
            if (adaptiveHUD != null && !enableAdaptiveHUD)
            {
                adaptiveHUD.gameObject.SetActive(false);
            }

            if (spatialBeacon != null && !enableSpatialBeacon)
            {
                spatialBeacon.gameObject.SetActive(false);
            }
        }

        [Header("Legacy Hint Arrows (optional — shown in Low state)")]
        [Tooltip("Arrow GameObjects pointing at the active orbit slot. Shown only in Low state.")]
        public GameObject[] hintArrows;

        [Header("Legacy Encouragement Banner (optional)")]
        [Tooltip("'You can do it!' style banner. Shown only in Low state.")]
        public GameObject encouragementBanner;

        [Header("Legacy Short Guidance Text (optional)")]
        [Tooltip("TextUI element for a short task hint.")]
        public Text shortHintText;

        [TextArea(2, 4)]
        [Tooltip("Text shown in the Low state hint panel (legacy).")]
        public string lowStateHintMessage = "Try placing the nearest planet in the glowing ring!";

        // ── Public API ────────────────────────────────────────────────────────

        public void Apply(LearnerState state)
        {
            bool showGuidance = (state == LearnerState.Low);

            // ── 3D Spatial Beacon (Suppressed by default) ──────────────────
            if (spatialBeacon != null)
            {
                if (enableSpatialBeacon)
                {
                    spatialBeacon.gameObject.SetActive(true);
                    spatialBeacon.Apply(state);
                }
                else
                {
                    spatialBeacon.gameObject.SetActive(false);
                }
            }

            // ── Active Orbit Pad Adaptive Guidance (Pulsing Glow on Table) ──
#if UNITY_2023_1_OR_NEWER
            var puzzleManager = FindFirstObjectByType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
#else
            var puzzleManager = FindObjectOfType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
#endif
            if (puzzleManager != null && puzzleManager.orbitSlots != null)
            {
                int activeIdx = puzzleManager.currentUnlockedOrbit - 1;
                for (int i = 0; i < puzzleManager.orbitSlots.Count; i++)
                {
                    var slot = puzzleManager.orbitSlots[i];
                    if (slot != null)
                    {
                        slot.SetPulsingGlow(showGuidance && (i == activeIdx));
                    }
                }
            }

            // ── Adaptive HUD (disabled by default to avoid occluding VR interaction) ──
            if (adaptiveHUD != null)
            {
                if (enableAdaptiveHUD)
                {
                    adaptiveHUD.gameObject.SetActive(true);
                    adaptiveHUD.Apply(state);
                }
                else
                {
                    adaptiveHUD.gameObject.SetActive(false);
                }
            }

            // ── Legacy: Toggle arrows ──────────────────────────────────────
            if (hintArrows != null)
                foreach (var arrow in hintArrows)
                    if (arrow != null) arrow.SetActive(showGuidance);

            // ── Legacy: Toggle banner ──────────────────────────────────────
            if (encouragementBanner != null)
                encouragementBanner.SetActive(showGuidance);

            // ── Legacy: Toggle hint text ───────────────────────────────────
            if (shortHintText != null)
            {
                shortHintText.gameObject.SetActive(showGuidance);
                if (showGuidance) shortHintText.text = lowStateHintMessage;
            }

            Debug.Log($"[GuidanceAdapter] State={state}: beacon={showGuidance} hud=updated arrows={showGuidance}");

            // ── Report to backend ──────────────────────────────────────────
            if (AdaptVRBackendClient.Instance != null)
            {
                AdaptVRBackendClient.Instance.SendAdaptationEvent(
                    "guidance_ui_adjust",
                    "Medium",
                    state.ToString(),
                    $"beacon={showGuidance} hud=adaptive arrows={showGuidance}"
                );
            }
        }
    }
}
