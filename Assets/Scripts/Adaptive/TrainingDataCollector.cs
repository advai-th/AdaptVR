using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using AdaptiveVR.DataCollection.Core;
using AdaptiveVR.Networking;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Collects every LearnerFeatureVector throughout the session.
    /// At session end, auto-labels each window using the same rules as retrain.py,
    /// then uploads the labelled batch to POST /api/training/features.
    ///
    /// Data flywheel: Quest device → this collector → backend DB → retrain.py → new ONNX.
    ///
    /// No PII — only the 12 aggregated feature numbers are sent.
    /// Attach to the AdaptiveSystem GameObject.
    /// </summary>
    public class TrainingDataCollector : MonoBehaviour
    {
        [Header("Auto-Label Thresholds (must match retrain.py)")]
        [Tooltip("successRate below this OR consecutiveErrors >= 3 OR hintCount >= lowHintThreshold → 'Low'")]
        [Range(0f, 0.5f)] public float lowSuccessRateThreshold  = 0.35f;
        [Range(1, 5)]     public int   lowHintCountThreshold    = 3;
        [Range(2, 6)]     public int   lowConsecutiveErrorThreshold = 3;

        [Tooltip("successRate above this AND hintCount == 0 AND fast task → 'High'")]
        [Range(0.6f, 1f)] public float highSuccessRateThreshold  = 0.85f;
        [Range(0.5f, 0.9f)] public float highDurationFastFraction = 0.70f;

        [Header("Runtime (Read-Only)")]
        public int totalWindowsCollected = 0;
        public int lastBatchUploaded     = 0;

        // ── Internal ──────────────────────────────────────────────────────────

        private readonly List<LearnerFeatureVector> _windows = new List<LearnerFeatureVector>();
        private BehaviourSignalCollector _collector;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
#if UNITY_2023_1_OR_NEWER
            _collector = FindFirstObjectByType<BehaviourSignalCollector>();
#else
            _collector = FindObjectOfType<BehaviourSignalCollector>();
#endif
            if (_collector != null)
                _collector.OnPredictionWindowReady += HandleWindow;

            if (SessionManager.HasInstance && SessionManager.Instance != null)
                SessionManager.Instance.onSessionEnded?.AddListener(HandleSessionEnded);
        }

        private void OnDestroy()
        {
            if (_collector != null) _collector.OnPredictionWindowReady -= HandleWindow;
            if (SessionManager.HasInstance && SessionManager.Instance != null)
                SessionManager.Instance.onSessionEnded?.RemoveListener(HandleSessionEnded);
        }

        // ── Window accumulation ───────────────────────────────────────────────

        private void HandleWindow(LearnerFeatureVector vec)
        {
            _windows.Add(vec);
            totalWindowsCollected = _windows.Count;
        }

        // ── Session end → label + upload ──────────────────────────────────────

        private void HandleSessionEnded(string sessionId)
        {
            if (_windows.Count == 0)
            {
                Debug.Log("[TrainingDataCollector] No windows to upload this session.");
                return;
            }

            // Apply auto-labels
            float medianDuration = ComputeMedianDuration();
            foreach (var w in _windows)
                ApplyAutoLabel(w, medianDuration);

            Debug.Log($"[TrainingDataCollector] Labelled {_windows.Count} windows. Uploading...");

            if (AdaptVRBackendClient.Instance != null)
                StartCoroutine(UploadBatch(sessionId));
        }

        // ── Auto-labelling (mirrors retrain.py logic) ─────────────────────────

        private void ApplyAutoLabel(LearnerFeatureVector w, float medianDuration)
        {
            string modelVer = LearnerStateManager.Instance != null
                            ? LearnerStateManager.Instance.activeModelVersion : "v1";
            w.modelVersionUsed = modelVer;

            bool isLow = w.consecutiveErrors >= lowConsecutiveErrorThreshold
                      || w.successRate      <  lowSuccessRateThreshold
                      || w.hintRequestCount >= lowHintCountThreshold;

            bool isHigh = !isLow
                       && w.successRate      >= highSuccessRateThreshold
                       && w.hintRequestCount == 0
                       && w.taskDuration     <  highDurationFastFraction * medianDuration;

            if (isLow)
            {
                w.derivedLabel    = "Low";
                w.labelConfidence = 0.70f;
            }
            else if (isHigh)
            {
                w.derivedLabel    = "High";
                w.labelConfidence = 0.70f;
            }
            else
            {
                w.derivedLabel    = "Medium";
                w.labelConfidence = 0.70f;
            }

            w.labelSource = "auto";
        }

        // ── Upload coroutine ──────────────────────────────────────────────────

        private IEnumerator UploadBatch(string sessionId)
        {
            string json = BuildBatchJson(sessionId);
            AdaptVRBackendClient.Instance.UploadTrainingFeatures(sessionId, json);
            lastBatchUploaded = _windows.Count;
            Debug.Log($"[TrainingDataCollector] Uploaded {lastBatchUploaded} windows for session {sessionId}.");
            _windows.Clear();
            totalWindowsCollected = 0;
            yield return null;
        }

        // ── JSON serialisation (manual — no Newtonsoft dependency) ────────────

        private string BuildBatchJson(string sessionId)
        {
            string modelVer = LearnerStateManager.Instance != null
                            ? LearnerStateManager.Instance.activeModelVersion : "v1";

            var sb = new StringBuilder(512);
            sb.Append("{");
            sb.Append($"\"sessionId\":\"{EscapeJson(sessionId)}\",");
            sb.Append($"\"modelVersionUsed\":\"{EscapeJson(modelVer)}\",");
            sb.Append("\"examples\":[");

            for (int i = 0; i < _windows.Count; i++)
            {
                var w = _windows[i];
                if (i > 0) sb.Append(",");
                sb.Append("{");
                sb.Append($"\"hesitationTime\":{w.hesitationTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"\"wrongSnapCount\":{w.wrongSnapCount},");
                sb.Append($"\"correctSnapCount\":{w.correctSnapCount},");
                sb.Append($"\"hintRequestCount\":{w.hintRequestCount},");
                sb.Append($"\"totalInteractions\":{w.totalInteractions},");
                sb.Append($"\"averageResponseTime\":{w.averageResponseTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"\"idleSeconds\":{w.idleSeconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"\"headDirectionChanges\":{w.headDirectionChanges},");
                sb.Append($"\"backtrackingCount\":{w.backtrackingCount},");
                sb.Append($"\"successRate\":{w.successRate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"\"taskDuration\":{w.taskDuration.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"\"consecutiveErrors\":{w.consecutiveErrors},");
                sb.Append($"\"derivedLabel\":\"{w.derivedLabel}\",");
                sb.Append($"\"labelSource\":\"{w.labelSource}\",");
                sb.Append($"\"labelConfidence\":{w.labelConfidence.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"\"windowStartTime\":{w.windowStartTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},");
                sb.Append($"\"windowEndTime\":{w.windowEndTime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append("}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private float ComputeMedianDuration()
        {
            if (_windows.Count == 0) return 30f;
            var durations = new List<float>();
            foreach (var w in _windows) durations.Add(w.taskDuration);
            durations.Sort();
            return durations[durations.Count / 2];
        }

        private static string EscapeJson(string s)
            => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
    }
}
