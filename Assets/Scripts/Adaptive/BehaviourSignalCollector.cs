using System;
using System.Collections.Generic;
using UnityEngine;
using AdaptiveVR.PlanetPuzzle;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Accumulates raw VR interaction signals per prediction window and fires
    /// OnPredictionWindowReady when the window closes (correct placement,
    /// consecutive error threshold reached, or prolonged inactivity).
    ///
    /// Hooks: PlanetSlot.OnPlanetStateChanged, SolarSystemPuzzleManager orbit-unlock event.
    ///
    /// Attach to the AdaptiveSystem GameObject.
    /// </summary>
    public class BehaviourSignalCollector : MonoBehaviour
    {
        [Header("Scene References (auto-found if empty)")]
        [Tooltip("All PlanetSlots in the scene.")]
        public List<PlanetSlot> planetSlots = new List<PlanetSlot>();

        [Tooltip("The SolarSystemPuzzleManager for orbit-unlock events.")]
        public SolarSystemPuzzleManager puzzleManager;

        [Header("Window Trigger Settings")]
        [Tooltip("Seconds of inactivity after which the current window is flushed.")]
        [Range(10f, 60f)]
        public float idleWindowTriggerSeconds = 25f;

        [Tooltip("Consecutive wrong snaps before flushing a window.")]
        [Range(1, 5)]
        public int consecutiveErrorWindowThreshold = 2;

        [Tooltip("Min angular delta (degrees) per 0.5 s sample to count as a head direction change.")]
        [Range(10f, 60f)]
        public float significantHeadAngle = 30f;

        // ── Event ─────────────────────────────────────────────────────────────
        /// <summary>Fired when a prediction window has been completed and is ready for inference.</summary>
        public event Action<LearnerFeatureVector> OnPredictionWindowReady;

        // ── Accumulated window state ───────────────────────────────────────────
        private float _windowStartTime;
        private float _lastInteractionTime;
        private float _hesitationStartTime;
        private bool  _hesitationActive;

        private int   _wrongSnaps;
        private int   _correctSnaps;
        private int   _hintRequests;
        private int   _totalInteractions;
        private int   _headDirectionChanges;
        private int   _backtrackingCount;
        private int   _consecutiveErrors;
        private float _idleAccumulator;

        private readonly List<float> _responseTimes = new List<float>();

        // ── Head tracking ─────────────────────────────────────────────────────
        private Quaternion _lastHeadRot;
        private float      _headSampleTimer;
        private const float HEAD_SAMPLE_INTERVAL = 0.5f;

        // ── Backtrack detection ───────────────────────────────────────────────
        private readonly Dictionary<GrabbablePlanet, PlanetSlot> _lastKnownSlot
            = new Dictionary<GrabbablePlanet, PlanetSlot>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            ResetWindow();
        }

        private void Start()
        {
            // Auto-find PlanetSlots
            if (planetSlots == null || planetSlots.Count == 0)
            {
#if UNITY_2023_1_OR_NEWER
                var found = FindObjectsByType<PlanetSlot>(FindObjectsSortMode.None);
#else
                var found = FindObjectsOfType<PlanetSlot>();
#endif
                foreach (var s in found) if (s != null) planetSlots.Add(s);
            }

            // Subscribe to slot events
            foreach (var slot in planetSlots)
                if (slot != null) slot.OnPlanetStateChanged += HandleSlotChanged;

            // Auto-find puzzle manager
            if (puzzleManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                puzzleManager = FindFirstObjectByType<SolarSystemPuzzleManager>();
#else
                puzzleManager = FindObjectOfType<SolarSystemPuzzleManager>();
#endif
            }

            if (puzzleManager != null)
                puzzleManager.onNextOrbitUnlocked.AddListener(HandleOrbitUnlocked);

            // Initialise head tracking
            if (Camera.main != null)
                _lastHeadRot = Camera.main.transform.rotation;

            // Start first hesitation timer at session start
            _hesitationStartTime = Time.time;
            _hesitationActive    = true;
        }

        private void OnDestroy()
        {
            foreach (var slot in planetSlots)
                if (slot != null) slot.OnPlanetStateChanged -= HandleSlotChanged;

            if (puzzleManager != null)
                puzzleManager.onNextOrbitUnlocked.RemoveListener(HandleOrbitUnlocked);
        }

        private void Update()
        {
            // ── Head direction change sampling ────────────────────────────────
            _headSampleTimer += Time.deltaTime;
            if (_headSampleTimer >= HEAD_SAMPLE_INTERVAL && Camera.main != null)
            {
                _headSampleTimer = 0f;
                float angle = Quaternion.Angle(_lastHeadRot, Camera.main.transform.rotation);
                if (angle > significantHeadAngle) _headDirectionChanges++;
                _lastHeadRot = Camera.main.transform.rotation;
            }

            // ── Idle accumulation (3 s grace before counting as idle) ─────────
            if (Time.time - _lastInteractionTime > 3f)
                _idleAccumulator += Time.deltaTime;

            // ── Inactivity window flush ───────────────────────────────────────
            if (_totalInteractions > 0 &&
                Time.time - _lastInteractionTime >= idleWindowTriggerSeconds)
            {
                FlushWindow();
            }
        }

        // ── Slot event ────────────────────────────────────────────────────────

        private void HandleSlotChanged(PlanetSlot slot, GrabbablePlanet planet, bool isCorrect)
        {
            if (planet == null) return;  // Planet cleared from slot — ignore

            float now = Time.time;
            _totalInteractions++;
            _lastInteractionTime = now;

            // Response time since last hesitation start
            if (_hesitationActive)
            {
                _responseTimes.Add(now - _hesitationStartTime);
                _hesitationActive = false;
            }

            if (isCorrect)
            {
                _correctSnaps++;
                _consecutiveErrors = 0;

                // Backtrack: was this planet's last known position a correct slot?
                if (_lastKnownSlot.TryGetValue(planet, out PlanetSlot prev) && prev != slot)
                    if (prev.currentState == SlotState.Correct) _backtrackingCount++;

                _lastKnownSlot[planet] = slot;
                FlushWindow();  // Correct placement → close window
            }
            else
            {
                _wrongSnaps++;
                _consecutiveErrors++;
                _lastKnownSlot[planet] = slot;

                if (_consecutiveErrors >= consecutiveErrorWindowThreshold)
                    FlushWindow();  // Too many errors → close window
            }
        }

        private void HandleOrbitUnlocked()
        {
            // New orbit = new hesitation clock
            _hesitationStartTime = Time.time;
            _hesitationActive    = true;
            _lastInteractionTime = Time.time;
        }

        // ── Hint hook (called by CompanionPuzzleBridge) ───────────────────────

        /// <summary>Register that a hint was shown this window.</summary>
        public void RegisterHintShown()
        {
            _hintRequests++;
            _lastInteractionTime = Time.time;
        }

        // ── Window management ─────────────────────────────────────────────────

        private void FlushWindow()
        {
            float now      = Time.time;
            float duration = Mathf.Max(now - _windowStartTime, 0.1f);

            float avgResponse = _responseTimes.Count > 0
                ? ComputeAverage(_responseTimes) : 0f;

            float hesitationTime = _hesitationActive
                ? (now - _hesitationStartTime)
                : (avgResponse > 0 ? avgResponse : 0f);

            float successRate = _totalInteractions > 0
                ? (float)_correctSnaps / _totalInteractions : 0f;

            var vec = new LearnerFeatureVector
            {
                hesitationTime       = (float)Math.Round(hesitationTime,  3),
                wrongSnapCount       = _wrongSnaps,
                correctSnapCount     = _correctSnaps,
                hintRequestCount     = _hintRequests,
                totalInteractions    = _totalInteractions,
                averageResponseTime  = (float)Math.Round(avgResponse,     3),
                idleSeconds          = (float)Math.Round(_idleAccumulator,3),
                headDirectionChanges = _headDirectionChanges,
                backtrackingCount    = _backtrackingCount,
                successRate          = (float)Math.Round(successRate,     4),
                taskDuration         = (float)Math.Round(duration,        3),
                consecutiveErrors    = _consecutiveErrors,
                windowStartTime      = _windowStartTime,
                windowEndTime        = now
            };

            OnPredictionWindowReady?.Invoke(vec);
            ResetWindow();
        }

        private void ResetWindow()
        {
            _windowStartTime      = Time.time;
            _lastInteractionTime  = Time.time;
            _wrongSnaps           = 0;
            _correctSnaps         = 0;
            _hintRequests         = 0;
            _totalInteractions    = 0;
            _headDirectionChanges = 0;
            _backtrackingCount    = 0;
            _consecutiveErrors    = 0;
            _idleAccumulator      = 0f;
            _responseTimes.Clear();
        }

        private float ComputeAverage(List<float> values)
        {
            float sum = 0f;
            foreach (float v in values) sum += v;
            return sum / values.Count;
        }
    }
}
