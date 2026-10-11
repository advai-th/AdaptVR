using System;
using UnityEngine;
using AdaptiveVR.DataCollection.Core;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// One of three cognitive-load states inferred by the ONNX model.
    /// Values are aligned with the LabelEncoder order produced by retrain.py:
    ///   High = 0, Low = 1, Medium = 2  (alphabetical scikit-learn encoding).
    /// </summary>
    public enum LearnerState
    {
        High   = 0,
        Low    = 1,
        Medium = 2
    }

    /// <summary>
    /// Singleton that stores the current cognitive-load state and
    /// fires OnStateChanged so AdaptationEngine and other listeners can react.
    ///
    /// Attach to a DontDestroyOnLoad GameObject in the root scene.
    /// </summary>
    public class LearnerStateManager : MonoBehaviour
    {
        public static LearnerStateManager Instance { get; private set; }

        [Header("Current Cognitive Load State (Read-Only)")]
        public LearnerState currentState       = LearnerState.Medium;
        public float        lastConfidence      = 0f;
        public string       activeModelVersion  = "v1";

        /// <summary>
        /// Fired whenever the inferred state changes.
        /// Parameters: (newState, confidence).
        /// </summary>
        public event Action<LearnerState, float> OnStateChanged;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by CognitiveLoadBridge after each ONNX inference.
        /// Only fires OnStateChanged if the state has actually changed.
        /// Also records the state to SessionManager so it ends up in the session JSON.
        /// </summary>
        public void ReportState(LearnerState newState, float confidence, string modelVersion)
        {
            bool changed = (newState != currentState);

            currentState      = newState;
            lastConfidence    = confidence;
            activeModelVersion= modelVersion;

            // Persist to session log
            if (SessionManager.HasInstance && SessionManager.Instance != null)
                SessionManager.Instance.RecordLearnerState(newState.ToString(), confidence, modelVersion);

            if (changed)
            {
                Debug.Log($"<color=#00E5FF>[LearnerStateManager] State → " +
                          $"<b>{newState}</b>  conf: {confidence:P1}  model: {modelVersion}</color>");
                OnStateChanged?.Invoke(newState, confidence);
            }
        }

        // ── Editor / Testing helpers ──────────────────────────────────────────

        /// <summary>
        /// Translates incoming Cognitive Load terminology (High, Medium, Low) into the internal LearnerState.
        /// - High Cognitive Load = learner struggling -> maps to LearnerState.Low (scaffolding active)
        /// - Medium Cognitive Load = balanced -> maps to LearnerState.Medium
        /// - Low Cognitive Load = learner mastery/confident -> maps to LearnerState.High (independent)
        /// </summary>
        public void SetCognitiveLoad(string cognitiveLoadLevel, float confidence = 1f)
        {
            if (string.IsNullOrEmpty(cognitiveLoadLevel)) return;
            string clean = cognitiveLoadLevel.Trim().ToLowerInvariant();

            if (clean == "high")
            {
                // High Cognitive Load -> Learner struggling -> Trigger LearnerState.Low (Scaffolding ON)
                ReportState(LearnerState.Low, confidence, activeModelVersion);
            }
            else if (clean == "low")
            {
                // Low Cognitive Load -> Learner mastery -> Trigger LearnerState.High (Scaffolding OFF)
                ReportState(LearnerState.High, confidence, activeModelVersion);
            }
            else
            {
                // Medium Cognitive Load -> Baseline
                ReportState(LearnerState.Medium, confidence, activeModelVersion);
            }
        }

        [ContextMenu("Force State: Low (Struggling / Scaffolding ON)")]
        public void ForceStateLow()    => ReportState(LearnerState.Low,    1f, activeModelVersion);

        [ContextMenu("Force State: Medium")]
        public void ForceStateMedium() => ReportState(LearnerState.Medium, 1f, activeModelVersion);

        [ContextMenu("Force State: High (Mastery / Scaffolding OFF)")]
        public void ForceStateHigh()   => ReportState(LearnerState.High,   1f, activeModelVersion);
    }
}
