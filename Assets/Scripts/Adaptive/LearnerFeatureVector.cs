using System;
using UnityEngine;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// The 12 canonical VR behavioural features computed by BehaviourSignalCollector
    /// at the end of each prediction window.  This is the sole input to the ONNX
    /// cognitive-load model and the payload uploaded to the backend.
    ///
    /// No PII — only aggregated numerical values.
    /// Must stay in sync with VR_FEATURE_COLUMNS in ml/retrain.py.
    /// </summary>
    [Serializable]
    public class LearnerFeatureVector
    {
        // ── Interaction quality ──────────────────────────────────────────────
        /// <summary>Seconds from orbit-unlock to first grab attempt.</summary>
        public float hesitationTime;

        /// <summary>Wrong-planet placements this window.</summary>
        public int   wrongSnapCount;

        /// <summary>Correct placements this window.</summary>
        public int   correctSnapCount;

        /// <summary>Hint requests (any tier) received this window.</summary>
        public int   hintRequestCount;

        /// <summary>Total grab / release / snap events.</summary>
        public int   totalInteractions;

        // ── Timing ──────────────────────────────────────────────────────────
        /// <summary>Mean seconds between orbit-unlock and placement attempt.</summary>
        public float averageResponseTime;

        /// <summary>Total seconds with no grab or head movement.</summary>
        public float idleSeconds;

        /// <summary>Window length in seconds.</summary>
        public float taskDuration;

        // ── Spatial behaviour ────────────────────────────────────────────────
        /// <summary>Significant yaw/pitch direction changes (> 30 degrees).</summary>
        public int   headDirectionChanges;

        /// <summary>Planet picked up and returned to a previous slot.</summary>
        public int   backtrackingCount;

        // ── Derived performance ──────────────────────────────────────────────
        /// <summary>correctSnapCount / max(totalInteractions, 1).</summary>
        public float successRate;

        /// <summary>Running streak of consecutive wrong placements.</summary>
        public int   consecutiveErrors;

        // ── Label (set by TrainingDataCollector, not by inference) ───────────
        [NonSerialized] public string derivedLabel     = "Medium"; // Low | Medium | High
        [NonSerialized] public string labelSource      = "auto";   // auto | teacher
        [NonSerialized] public float  labelConfidence  = 0.70f;

        // ── Context ──────────────────────────────────────────────────────────
        [NonSerialized] public float  windowStartTime;
        [NonSerialized] public float  windowEndTime;
        [NonSerialized] public string modelVersionUsed = "v1";

        /// <summary>
        /// Returns the 12 features as a float array in the exact order
        /// expected by the ONNX model (must match VR_FEATURE_COLUMNS in retrain.py).
        /// </summary>
        public float[] ToArray()
        {
            return new float[]
            {
                hesitationTime,
                wrongSnapCount,
                correctSnapCount,
                hintRequestCount,
                totalInteractions,
                averageResponseTime,
                idleSeconds,
                headDirectionChanges,
                backtrackingCount,
                successRate,
                taskDuration,
                consecutiveErrors
            };
        }
    }
}
