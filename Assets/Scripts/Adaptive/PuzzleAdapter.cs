using System.Collections.Generic;
using UnityEngine;
using AdaptiveVR.PlanetPuzzle;
using AdaptiveVR.Networking;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Adjusts PlanetSlot snap radius and SolarSystemPuzzleManager orbit ring opacity
    /// in response to the current cognitive-load state.
    ///
    ///   Low   → bigger snap zones + brighter rings  (scaffolding for struggling learner)
    ///   Medium → default values
    ///   High  → tighter snap zones + fainter rings  (reduce visual noise for confident learner)
    ///
    /// Attach to the same GameObject as SolarSystemPuzzleManager, or anywhere in the scene.
    /// </summary>
    public class PuzzleAdapter : MonoBehaviour
    {
        [Header("Snap Radius Overrides (metres)")]
        [Tooltip("Snap radius when learner is struggling (Low state).")]
        [Range(0.25f, 0.8f)] public float snapRadiusLow    = 0.55f;

        [Tooltip("Snap radius at baseline difficulty (Medium state).")]
        [Range(0.15f, 0.6f)] public float snapRadiusMedium = 0.35f;

        [Tooltip("Snap radius for confident learners (High state).")]
        [Range(0.10f, 0.4f)] public float snapRadiusHigh   = 0.22f;

        [Header("Orbit Ring Opacity Overrides")]
        [Range(0f, 1f)] public float orbitOpacityLow    = 0.40f;
        [Range(0f, 1f)] public float orbitOpacityMedium = 0.14f;
        [Range(0f, 1f)] public float orbitOpacityHigh   = 0.06f;

        [Header("Cognitive Scaffolding (Planet Distractor Dimming)")]
        [Tooltip("Greys out incorrect distractor planets and illuminates only the target planet for struggling learners (Low state).")]
        public bool enableDimmingOnLowState = true;

        [Tooltip("Enables distractor dimming for High state (Mastery). Off by default for confident learners.")]
        public bool enableDimmingOnHighState = false;

        [Header("Cognitive Scaffolding (Planet Name Labels)")]
        [Tooltip("Master switch to show subtle, non-invasive floating name labels on top of planets.")]
        public bool enablePlanetLabels = true;

        [Tooltip("Enables planet labels when state is Low (struggling learner).")]
        public bool enableLabelsOnLowState = true;

        [Tooltip("Enables planet labels when state is Medium (intermediate learner).")]
        public bool enableLabelsOnMediumState = true;

        [Tooltip("Enables planet labels when state is High (Mastery/confident). Off by default to keep scene clean for expert learners.")]
        public bool enableLabelsOnHighState = false;

        [Tooltip("Display mode for floating name labels: TargetPlanetOnly (minimal), AllUnplacedPlanets (all table planets), or AllPlanets.")]
        public PlanetLabelMode planetLabelMode = PlanetLabelMode.AllUnplacedPlanets;

        [Header("References (auto-found if empty)")]
        [Tooltip("Leave empty — all PlanetSlots will be auto-found in Start().")]
        public List<PlanetSlot> planetSlots = new List<PlanetSlot>();

        public SolarSystemPuzzleManager puzzleManager;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (planetSlots.Count == 0)
            {
#if UNITY_2023_1_OR_NEWER
                var found = FindObjectsByType<PlanetSlot>(FindObjectsSortMode.None);
#else
                var found = FindObjectsOfType<PlanetSlot>();
#endif
                foreach (var s in found) if (s != null) planetSlots.Add(s);
            }

            if (puzzleManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                puzzleManager = FindFirstObjectByType<SolarSystemPuzzleManager>();
#else
                puzzleManager = FindObjectOfType<SolarSystemPuzzleManager>();
#endif
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Apply(LearnerState state)
        {
            float radius  = state == LearnerState.Low  ? snapRadiusLow  :
                            state == LearnerState.High ? snapRadiusHigh  :
                            snapRadiusMedium;

            float opacity = state == LearnerState.Low  ? orbitOpacityLow  :
                            state == LearnerState.High ? orbitOpacityHigh  :
                            orbitOpacityMedium;

            // Apply snap radius to all active slots
            foreach (var slot in planetSlots)
            {
                if (slot != null && slot.isSlotActive)
                    slot.snapRadius = radius;
            }

            // Apply ring opacity through the puzzle manager
            if (puzzleManager != null)
                puzzleManager.orbitRingOpacity = opacity;

            // Planet distractor dimming visual scaffolding:
            // High cognitive load (or Low struggle state) dims distractors and highlights target.
            bool shouldDimDistractors = (state == LearnerState.High && enableDimmingOnHighState) ||
                                       (state == LearnerState.Low && enableDimmingOnLowState);

            if (puzzleManager != null)
            {
                puzzleManager.SetDistractorDimming(shouldDimDistractors);
            }

            // Planet name labels visual scaffolding:
            bool shouldShowLabels = enablePlanetLabels &&
                                   ((state == LearnerState.Low && enableLabelsOnLowState) ||
                                    (state == LearnerState.Medium && enableLabelsOnMediumState) ||
                                    (state == LearnerState.High && enableLabelsOnHighState));

            if (puzzleManager != null)
            {
                puzzleManager.SetPlanetLabelsActive(shouldShowLabels, planetLabelMode);
            }

            Debug.Log($"[PuzzleAdapter] {state}: snapRadius={radius:F2}, orbitOpacity={opacity:F2}, distractorDimming={shouldDimDistractors}, planetLabels={shouldShowLabels}");

            // Log adaptation to backend
            if (AdaptVRBackendClient.Instance != null)
            {
                AdaptVRBackendClient.Instance.SendAdaptationEvent(
                    "puzzle_scaffolding_and_dimming",
                    "Medium",
                    state.ToString(),
                    $"snapRadius={radius:F2} orbitOpacity={opacity:F2} distractorDimming={shouldDimDistractors} planetLabels={shouldShowLabels}"
                );
            }
        }
    }
}
