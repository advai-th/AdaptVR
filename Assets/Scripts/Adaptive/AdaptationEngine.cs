using UnityEngine;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Subscribes to LearnerStateManager.OnStateChanged and routes each
    /// state update to every registered adapter.
    ///
    /// Attach alongside LearnerStateManager on the AdaptiveSystem GameObject.
    /// Drag PuzzleAdapter, GuidanceAdapter, and CompanionPuzzleBridge into the
    /// inspector fields.
    /// </summary>
    public class AdaptationEngine : MonoBehaviour
    {
        [Header("Adapter References (assign in Inspector)")]
        [Tooltip("Adjusts PlanetSlot snap radii and orbit ring opacity.")]
        public PuzzleAdapter   puzzleAdapter;

        [Tooltip("Shows/hides hint arrows and guidance UI.")]
        public GuidanceAdapter guidanceAdapter;

        [Tooltip("Controls the CompanionBot's expression and hint tier.")]
        public CompanionPuzzleBridge companionBridge;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (LearnerStateManager.Instance != null)
            {
                LearnerStateManager.Instance.OnStateChanged += HandleStateChanged;
            }
            else
            {
                Debug.LogWarning("[AdaptationEngine] LearnerStateManager not found. " +
                                 "Ensure it is present and enabled before this script.");
            }
        }

        private void OnDestroy()
        {
            if (LearnerStateManager.Instance != null)
                LearnerStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        // ── State routing ─────────────────────────────────────────────────────

        private void HandleStateChanged(LearnerState state, float confidence)
        {
            Debug.Log($"[AdaptationEngine] Routing adaptation → {state} ({confidence:P1})");

            puzzleAdapter?.Apply(state);
            guidanceAdapter?.Apply(state);
            companionBridge?.OnLearnerStateChanged(state);
        }
    }
}
