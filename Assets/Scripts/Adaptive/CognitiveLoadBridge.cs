using System;
using System.IO;
using UnityEngine;
using AdaptiveVR.Networking;

// Unity AI Inference Engine (formerly Sentis — package: com.unity.ai.inference)
using Unity.InferenceEngine;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Loads the ONNX cognitive-load classifier and runs synchronous inference
    /// (~1–3 ms CPU) on each LearnerFeatureVector emitted by BehaviourSignalCollector.
    /// Reports the result to LearnerStateManager and logs it to the backend.
    ///
    /// Model selection priority:
    ///   1. Highest-versioned .onnx in Application.persistentDataPath/AdaptVR/Models/
    ///   2. Bundled baseline: StreamingAssets/AdaptVR/cognitive_load_v1.onnx
    /// </summary>
    public class CognitiveLoadBridge : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────

        /// Number of input features — must match LearnerFeatureVector.ToArray() and retrain.py
        private const int NUM_FEATURES = 12;

        /// Output names (support both standard scikit-learn ONNX and skl2onnx custom names)
        private const string OUT_LABEL_PRIMARY   = "label";
        private const string OUT_LABEL_FALLBACK  = "output_label";
        private const string OUT_PROBA_PRIMARY   = "probabilities";
        private const string OUT_PROBA_FALLBACK  = "output_probability";

        private const string STREAMING_ASSETS_RELATIVE = "AdaptVR/cognitive_load_v1.onnx";
        private const string PERSISTENT_MODELS_SUBDIR  = "AdaptVR/Models";

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Model Configuration")]
        [Tooltip("Direct reference to the imported ModelAsset (recommended). Auto-wired if cognitive_load_v1.onnx is in Assets.")]
        public ModelAsset defaultModelAsset;

        [Header("Runtime State (Read-Only)")]
        public string loadedModelPath = "";
        public string loadedVersion   = "v1";
        public bool   isModelLoaded   = false;

        // ── Internal ──────────────────────────────────────────────────────────

        private Model  _model;
        private Worker _worker;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            LoadBestModel();
            HookSignalCollector();
        }

        private void OnDestroy()
        {
            _worker?.Dispose();
        }

        // ── Model loading ─────────────────────────────────────────────────────

        private void LoadBestModel()
        {
            // 1. Persistent path — downloaded updates (.sentis or .onnx models) live here
            string persistentDir = Path.Combine(Application.persistentDataPath, PERSISTENT_MODELS_SUBDIR);
            string bestPath      = null;
            int    bestVer       = 0;

            if (Directory.Exists(persistentDir))
            {
                foreach (string path in Directory.GetFiles(persistentDir, "*.*"))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".sentis" || ext == ".onnx")
                    {
                        int v = ExtractVersionNumber(Path.GetFileNameWithoutExtension(path));
                        if (v > bestVer) { bestVer = v; bestPath = path; }
                    }
                }
            }

            if (bestPath != null)
            {
                LoadModelFromFile(bestPath, $"v{bestVer}");
                if (isModelLoaded) return;
            }

            // 2. Direct ModelAsset reference
            if (defaultModelAsset != null)
            {
                LoadModelAsset(defaultModelAsset, "v1");
                if (isModelLoaded) return;
            }

            // 3. Unity Resources folder — reliably bundles across PC and Android Meta Quest APKs
            var resAsset = Resources.Load<ModelAsset>("AdaptVR/cognitive_load_v1");
            if (resAsset == null)
                resAsset = Resources.Load<ModelAsset>("cognitive_load_v1");

            if (resAsset != null)
            {
                defaultModelAsset = resAsset;
                LoadModelAsset(resAsset, "v1");
                if (isModelLoaded) return;
            }

            // 4. Bundled StreamingAssets (.sentis or .onnx file)
            string sentisStreamingPath = Path.Combine(Application.streamingAssetsPath, "AdaptVR/cognitive_load_v1.sentis");
            if (File.Exists(sentisStreamingPath))
            {
                LoadModelFromFile(sentisStreamingPath, "v1");
                if (isModelLoaded) return;
            }

            string onnxStreamingPath = Path.Combine(Application.streamingAssetsPath, "AdaptVR/cognitive_load_v1.onnx");
            if (File.Exists(onnxStreamingPath))
            {
                LoadModelFromFile(onnxStreamingPath, "v1");
                if (isModelLoaded) return;
            }

            // 5. In Editor: try loading ModelAsset from Assets/AdaptVR/Models/
#if UNITY_EDITOR
            var editorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/AdaptVR/Models/cognitive_load_v1.onnx");
            if (editorAsset != null)
            {
                defaultModelAsset = editorAsset;
                LoadModelAsset(editorAsset, "v1");
                if (isModelLoaded) return;
            }
#endif

            Debug.LogWarning("[CognitiveLoadBridge] No precompiled ModelAsset or .sentis model could be initialized directly. " +
                             "The resilient rule-based heuristic inference engine will operate as an active standby.");
        }

        public void LoadModelAsset(ModelAsset asset, string version)
        {
            if (asset == null) return;
            try
            {
                _worker?.Dispose();
                _model          = ModelLoader.Load(asset);
                _worker         = new Worker(_model, BackendType.CPU);
                loadedModelPath = "ModelAsset: " + asset.name;
                loadedVersion   = version;
                isModelLoaded   = true;
                Debug.Log($"<color=#00FF88>[CognitiveLoadBridge] Loaded ModelAsset " +
                          $"<b>{version}</b> ({asset.name})</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CognitiveLoadBridge] Failed to load ModelAsset: {ex.Message}");
                isModelLoaded = false;
            }
        }

        public void LoadModelFromFile(string path, string version)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                _worker?.Dispose();
                _model          = ModelLoader.Load(path);
                _worker         = new Worker(_model, BackendType.CPU);
                loadedModelPath = path;
                loadedVersion   = version;
                isModelLoaded   = true;
                Debug.Log($"<color=#00FF88>[CognitiveLoadBridge] Loaded model " +
                          $"<b>{version}</b> from: {path}</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CognitiveLoadBridge] Failed to load model file '{path}': {ex.Message}");
                isModelLoaded = false;
            }
        }

        // ── Signal collector hook ─────────────────────────────────────────────

        private void HookSignalCollector()
        {
#if UNITY_2023_1_OR_NEWER
            var collector = FindFirstObjectByType<BehaviourSignalCollector>();
#else
            var collector = FindObjectOfType<BehaviourSignalCollector>();
#endif
            if (collector != null)
                collector.OnPredictionWindowReady += RunInference;
            else
                Debug.LogWarning("[CognitiveLoadBridge] BehaviourSignalCollector not found in scene.");
        }

        // ── Inference ─────────────────────────────────────────────────────────

        private void RunInference(LearnerFeatureVector features)
        {
            if (features == null) return;

            try
            {
                int classIdx = 1; // Default: Low learner state (struggling)
            float confidence = 0.85f;
            bool inferenceRanOnnx = false;

            if (isModelLoaded && _worker != null)
            {
                try
                {
                    float[] inputData = features.ToArray();

                    using var inputTensor = new Tensor<float>(new TensorShape(1, NUM_FEATURES), inputData);

                    // Schedule inference (synchronous on CPU backend)
                    _worker.Schedule(inputTensor);

                    // ── Read label ────────────────────────────────────────────────
                    Tensor rawLabel = null;
                    try { rawLabel = _worker.PeekOutput(OUT_LABEL_PRIMARY); } catch { }
                    if (rawLabel == null)
                    {
                        try { rawLabel = _worker.PeekOutput(OUT_LABEL_FALLBACK); } catch { }
                    }
                    if (rawLabel == null && _model != null && _model.outputs.Count > 0)
                    {
                        try { rawLabel = _worker.PeekOutput(0); } catch { }
                    }

                    if (rawLabel != null)
                    {
                        using var cpuLabel = rawLabel.ReadbackAndClone();
                        if (cpuLabel is Tensor<long> longTensor)
                            classIdx = (int)longTensor[0];
                        else if (cpuLabel is Tensor<int> intTensor)
                            classIdx = intTensor[0];
                    }

                    // ── Read probability ──────────────────────────────────────────
                    Tensor rawProb = null;
                    try { rawProb = _worker.PeekOutput(OUT_PROBA_PRIMARY); } catch { }
                    if (rawProb == null)
                    {
                        try { rawProb = _worker.PeekOutput(OUT_PROBA_FALLBACK); } catch { }
                    }
                    if (rawProb == null && _model != null && _model.outputs.Count > 1)
                    {
                        try { rawProb = _worker.PeekOutput(1); } catch { }
                    }

                    if (rawProb != null)
                    {
                        using var cpuProb = rawProb.ReadbackAndClone() as Tensor<float>;
                        if (cpuProb != null)
                        {
                            if (cpuProb.shape.rank >= 2 && classIdx < cpuProb.shape[1])
                                confidence = cpuProb[0, classIdx];
                            else if (classIdx < cpuProb.shape.length)
                                confidence = cpuProb[classIdx];
                        }
                    }

                    inferenceRanOnnx = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CognitiveLoadBridge] ONNX Sentis inference failed ({ex.Message}), falling back to heuristic engine.");
                    inferenceRanOnnx = false;
                }
            }

            // Fallback heuristic classification if ONNX model is missing or failed
            if (!inferenceRanOnnx)
            {
                bool isStruggling = (features.consecutiveErrors >= 2 ||
                                     features.wrongSnapCount >= 2 ||
                                     (features.totalInteractions > 0 && features.successRate < 0.40f) ||
                                     features.hintRequestCount >= 2 ||
                                     features.hesitationTime > 10f ||
                                     features.idleSeconds > 15f);

                bool isMastery = !isStruggling &&
                                 (features.correctSnapCount > 0 &&
                                  features.wrongSnapCount == 0 &&
                                  features.consecutiveErrors == 0 &&
                                  features.hintRequestCount == 0 &&
                                  features.successRate >= 0.80f);

                classIdx = isStruggling ? 1 : (isMastery ? 0 : 2); // 0=High, 1=Low, 2=Medium
                confidence = 0.85f;
            }

            // Map class index → LearnerState enum
            // (High=0, Low=1, Medium=2 — alphabetical LabelEncoder order)
            LearnerState state = (LearnerState)Mathf.Clamp(classIdx, 0, 2);
            features.modelVersionUsed = inferenceRanOnnx ? loadedVersion : "heuristic_fallback";

            // Map LearnerState (Mastery) to Cognitive Load (Mental Effort):
            // - LearnerState.High (Mastery / Confident) -> Cognitive Load "low" (minimal scaffolding)
            // - LearnerState.Low  (Struggling / Novice)  -> Cognitive Load "high" (full scaffolding)
            // - LearnerState.Medium (Intermediate)       -> Cognitive Load "medium" (standard guidance)
            string cognitiveLoad = state switch
            {
                LearnerState.High => "low",
                LearnerState.Low  => "high",
                _                 => "medium"
            };

            Debug.Log($"<color=#00E5FF>[CognitiveLoadBridge] → LearnerState: <b>{state}</b> | CognitiveLoad: <b>{cognitiveLoad}</b> " +
                      $"({confidence:P1}, engine={(inferenceRanOnnx ? "Sentis_ONNX" : "Heuristic")})  " +
                      $"[h={features.hesitationTime:F1}s err={features.wrongSnapCount} " +
                      $"ok={features.correctSnapCount} idle={features.idleSeconds:F1}s]</color>");

            // Report to state manager
            if (LearnerStateManager.Instance != null)
                LearnerStateManager.Instance.ReportState(state, confidence, features.modelVersionUsed);

            // Log prediction to backend
            if (AdaptVRBackendClient.Instance != null)
                AdaptVRBackendClient.Instance.SendCognitiveLoadPrediction(cognitiveLoad, confidence, features);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CognitiveLoadBridge] Inference error: {ex.Message}");
            }
        }

        // ── Hot-reload (called by ModelUpdateManager after a download) ────────

        /// <summary>
        /// Reloads from disk. Safe to call mid-session — new model takes effect
        /// on the NEXT window (existing inference call finishes first).
        /// </summary>
        public void ReloadModel()
        {
            isModelLoaded = false;
            LoadBestModel();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ExtractVersionNumber(string filename)
        {
            // "cognitive_load_v3" → 3
            int idx = filename.LastIndexOf('v');
            if (idx >= 0 && int.TryParse(filename.Substring(idx + 1), out int v))
                return v;
            return 0;
        }
    }
}
