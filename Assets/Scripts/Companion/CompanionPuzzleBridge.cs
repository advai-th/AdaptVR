using System.Collections;
using UnityEngine;
using AdaptiveVR.Companion;
using AdaptiveVR.Networking;
using AdaptiveVR.DataCollection.Core;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Bridges CompanionBot's expression and hint system with the adaptive state machine.
    ///
    /// State → expression mapping:
    ///   Low    → Thinking (bot looks worried / pondering) + proactive scaffolding hint
    ///   Medium → Idle    (default neutral)
    ///   High   → Happy   (subtle positive acknowledgement)
    ///
    /// State → hint tier mapping (when ShowContextualHint() is called):
    ///   Low    → Tier 3 full walkthrough hint
    ///   Medium → Tier 2 partial hint
    ///   High   → Tier 1 minimal nudge
    ///
    /// Also notifies BehaviourSignalCollector so hint counts feed into ML features.
    ///
    /// Attach to the same GameObject as CompanionBot.
    /// </summary>
    [RequireComponent(typeof(CompanionBot))]
    public class CompanionPuzzleBridge : MonoBehaviour
    {
        [Header("Puzzle References (auto-found if empty)")]
        [Tooltip("SolarSystemPuzzleManager to query active orbit and placed planet info.")]
        public AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager puzzleManager;

        // ── Dynamic Astronomical Knowledge Bank ──────────────────────────────
        private static readonly string[] PlanetDisplayNames = new string[]
        {
            "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune"
        };

        // Tier 3 hints for Low state / High Cognitive Load (struggling learner):
        // Specifically names the target planet, its physical characteristics, and orbit number!
        private static readonly string[] DynamicStruggleHintsTier3 = new string[]
        {
            "Let me help! Look for Mercury ☿ — the smallest rocky planet, closest to the Sun! Place it in Orbit 1.",
            "Let me help! Find Venus ♀ next — covered in thick yellowish clouds, second from the Sun! Snap it into Orbit 2.",
            "Let me help! Find Earth 🌍 — our blue and green ocean world! It belongs in Orbit 3 from the Sun.",
            "Let me help! Look for Mars ♂ — the rusty reddish-orange Red Planet! It belongs in Orbit 4.",
            "Let me help! Grab Jupiter ♃ — the massive giant with swirling cloud stripes! Snap it into Orbit 5.",
            "Let me help! Look for Saturn ♄ — the golden planet with magnificent bright rings! Place it in Orbit 6.",
            "Let me help! Find Uranus ⛢ — the pale cyan ice giant tilted on its side! Snap it into Orbit 7.",
            "Let me help! Grab Neptune ♆ — the deep azure-blue planet farthest from the Sun! Place it in Orbit 8."
        };

        // Tier 2 hints for Medium state (optimal learning):
        private static readonly string[] DynamicGuidanceHintsTier2 = new string[]
        {
            "We're on Orbit 1: find Mercury ☿, the fastest planet right next to the Sun!",
            "We need Venus ♀ for Orbit 2! Remember, it's the hottest planet, second from the Sun.",
            "Next is Orbit 3: find Earth 🌍! Third rock from the Sun in the habitable zone.",
            "Orbit 4 is next: find Mars ♂, the Red Planet just outside Earth's orbit!",
            "Heading to Orbit 5: find Jupiter ♃, the largest planet after the asteroid belt!",
            "Orbit 6 is ready: find Saturn ♄, famous for its majestic planetary rings!",
            "Nearly there! Orbit 7 needs Uranus ⛢, the icy giant planet rolling on its axis.",
            "Final orbit! Orbit 8 needs Neptune ♆, the outermost giant in our solar system."
        };

        // Tier 1 hints for High state (Mastery):
        private static readonly string[] DynamicMasteryHintsTier1 = new string[]
        {
            "You're doing great! First orbit (Mercury) awaits.",
            "Awesome flow! Focus on Orbit 2 (Venus).",
            "Great pace! Orbit 3 (Earth) is next.",
            "Smooth work! Look for Orbit 4 (Mars).",
            "Masterful! Onto the outer gas giants: Orbit 5 (Jupiter).",
            "Excellent astronomy! Orbit 6 (Saturn) is next.",
            "Almost complete! Orbit 7 (Uranus) awaits.",
            "Final step for mastery! Finish Orbit 8 (Neptune)!"
        };

        // Dynamic celebratory congratulations for each planet snapped into place:
        private static readonly string[] PlanetCongratsMessages = new string[]
        {
            "🌟 Spot on! Mercury is locked into Orbit 1! Closest to the Sun and super speedy! Next up: Venus!",
            "🌟 Fantastic! Venus is secured in Orbit 2! Second from the Sun and sizzling hot! Next up: Earth 🌍!",
            "🌟 Perfect! Earth is in Orbit 3! 🌍 Our home sweet home in the habitable zone! Next up: Mars ♂!",
            "🌟 Brilliant! Mars is in Orbit 4! 🔴 The Red Planet and home of rovers! Next up: giant Jupiter ♃!",
            "🌟 Whoa! Jupiter is secured in Orbit 5! ♃ The mighty king of the planets! Next up: ringed Saturn 🪐!",
            "🌟 Magnificent! Saturn and its beautiful rings are in Orbit 6! 🪐 Next up: ice giant Uranus ⛢!",
            "🌟 Wonderful! Uranus is resting in Orbit 7! ❄️ Tilted on its side! Final challenge: Neptune ♆!",
            "🌟 Bullseye! Neptune is secured in Orbit 8! 🌊 The outermost planet of the Solar System! Complete!"
        };

        [Header("Hint Texts (Fallback Static Defaults)")]
        [TextArea(2, 3)]
        [Tooltip("Tier 1 — minimal nudge shown to High-state learners.")]
        public string tier1Hint = "You're doing great! Keep going!";

        [TextArea(2, 3)]
        [Tooltip("Tier 2 — partial hint for Medium-state learners.")]
        public string tier2Hint = "Remember: planets get farther from the Sun as you go outward!";

        [TextArea(2, 3)]
        [Tooltip("Tier 3 — full walkthrough for Low-state (struggling) learners.")]
        public string tier3Hint = "Let me help! Grab the next planet and place it in the glowing orbit ring!";

        [Header("Timing")]
        [Tooltip("How long the talking expression and hint bubble lasts.")]
        [Range(2f, 12f)]
        public float hintExpressionDuration = 6f;

        [Tooltip("Seconds of cooldown between automatic proactive hints to avoid spamming.")]
        [Range(5f, 60f)]
        public float proactiveCooldownSeconds = 15f;

        [Tooltip("Seconds the learner must remain in Low state before an automatic hint fires.")]
        [Range(0f, 20f)]
        public float proactiveDelaySeconds = 5f;

        [Header("Celebration")]
        [Tooltip("Duration of the celebration expression after a correct planet snap.")]
        [Range(1f, 6f)]
        public float celebrationDuration = 3.0f;

        [Header("Hint In-Front Motion")]
        [Tooltip("If true, resets the bot's first-hint state when a planet is placed correctly or puzzle completes, so the bot comes in front for the first hint of each new orbit.")]
        public bool resetFirstHintPerOrbit = true;

        [Header("Pointing Target (Optional)")]
        [Tooltip("World-space target for the bot to look toward when giving guidance (e.g. the active orbit slot). " +
                 "Leave null — AdaptiveSystemEditor sets this to the SolarSystemPuzzleManager.")]
        public Transform hintPointTarget;

        // ── Internal ──────────────────────────────────────────────────────────

        private CompanionBot             _bot;
        private BehaviourSignalCollector _collector;
        private LearnerState             _currentState = LearnerState.Medium;
        private float                    _lastProactiveHintTime = -999f;
        private Coroutine                _proactiveRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _bot = GetComponent<CompanionBot>();
        }

        private void Start()
        {
#if UNITY_2023_1_OR_NEWER
            _collector = FindFirstObjectByType<BehaviourSignalCollector>();
            if (puzzleManager == null)
                puzzleManager = FindFirstObjectByType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
#else
            _collector = FindObjectOfType<BehaviourSignalCollector>();
            if (puzzleManager == null)
                puzzleManager = FindObjectOfType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
#endif

            if (puzzleManager != null)
            {
                puzzleManager.onPlanetPlacedCorrectly.AddListener(CelebrateSuccess);
                puzzleManager.onPlanetPlacedIncorrectly.AddListener(EncourageOnError);
                puzzleManager.onPuzzleCompleted.AddListener(CelebratePuzzleComplete);
            }
        }

        private void OnDestroy()
        {
            if (puzzleManager != null)
            {
                puzzleManager.onPlanetPlacedCorrectly.RemoveListener(CelebrateSuccess);
                puzzleManager.onPlanetPlacedIncorrectly.RemoveListener(EncourageOnError);
                puzzleManager.onPuzzleCompleted.RemoveListener(CelebratePuzzleComplete);
            }
        }

        // ── Called by AdaptationEngine ────────────────────────────────────────

        public void OnLearnerStateChanged(LearnerState state)
        {
            _currentState = state;

            switch (state)
            {
                case LearnerState.Low:
                    // Learner is STRUGGLING: thinking face + proactive scaffolding
                    _bot.SetExpression(BotExpression.Thinking);

                    // Cancel any pending proactive coroutine and restart with delay
                    if (_proactiveRoutine != null) StopCoroutine(_proactiveRoutine);
                    _proactiveRoutine = StartCoroutine(ProactiveHintAfterDelay());
                    break;

                case LearnerState.High:
                    // Learner is CONFIDENT / mastery state: soft positive acknowledgement
                    _bot.SetExpression(BotExpression.Happy, 2.5f);

                    // Cancel any pending proactive hint
                    if (_proactiveRoutine != null)
                    {
                        StopCoroutine(_proactiveRoutine);
                        _proactiveRoutine = null;
                    }
                    break;

                default: // Medium
                    _bot.SetExpression(BotExpression.Idle);

                    // Cancel any pending proactive hint
                    if (_proactiveRoutine != null)
                    {
                        StopCoroutine(_proactiveRoutine);
                        _proactiveRoutine = null;
                    }
                    break;
            }
        }

        // ── Proactive Scaffolding Coroutine ───────────────────────────────────

        private IEnumerator ProactiveHintAfterDelay()
        {
            // Wait before auto-firing so it doesn't interrupt during active manipulation
            yield return new WaitForSeconds(proactiveDelaySeconds);

            // Only fire if still in Low state and cooldown has elapsed
            if (_currentState == LearnerState.Low
                && Time.time - _lastProactiveHintTime >= proactiveCooldownSeconds)
            {
                TriggerProactiveScaffolding();
            }
            _proactiveRoutine = null;
        }

        private void TriggerProactiveScaffolding()
        {
            _lastProactiveHintTime = Time.time;

            // Dynamically lookup the active target planet for struggling learner (Tier 3)
            string hint = GetDynamicTierHint(LearnerState.Low);

            // Smoothly swoops in front of the camera, presents the hint, then returns to the side
            _bot.ShowHint(hint, hintExpressionDuration, null, forceInFront: true);

            // If we have a hint point target, briefly turn bot to face it
            if (hintPointTarget != null)
            {
                StartCoroutine(GlanceAtTargetRoutine(hintPointTarget.position, 1.5f));
            }

            // Notify collector so hint count feeds into ML features
            _collector?.RegisterHintShown();

            if (AdaptVRBackendClient.Instance != null)
            {
                int orbitNum = puzzleManager != null ? puzzleManager.currentUnlockedOrbit : 1;
                string planetName = (orbitNum >= 1 && orbitNum <= PlanetDisplayNames.Length) ? PlanetDisplayNames[orbitNum - 1] : "Planet";
                AdaptVRBackendClient.Instance.SendGameplayEvent(
                    eventType: "hint_requested",
                    pieceId: planetName,
                    orbitIndex: orbitNum,
                    isCorrect: false,
                    action: "proactive_scaffolding_hint",
                    extraNotes: hint
                );
            }

            if (SessionManager.HasInstance && SessionManager.Instance != null)
            {
                SessionManager.Instance.RecordLearningEvent("proactive_hint_shown", "CompanionBot", hint);
            }

            Debug.Log($"<color=#00E5FF>[CompanionPuzzleBridge] Proactive dynamic Tier-3 hint fired (state=Low): {hint}</color>");
        }

        private IEnumerator GlanceAtTargetRoutine(Vector3 worldTarget, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Gently rotate bot to look toward the orbit slot
                Vector3 dir = (worldTarget - _bot.transform.position).normalized;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
                    _bot.transform.rotation = Quaternion.Slerp(
                        _bot.transform.rotation, lookRot, Time.deltaTime * 5f);
                }
                yield return null;
            }
        }

        // ── Called from a UI Button, scene trigger, or VR controller tap ──────

        /// <summary>
        /// Shows a context-aware hint. Tier is auto-selected based on current state,
        /// and dynamically addresses the exact unlocked orbit and target planet!
        /// Swoops smoothly in front of the camera, speaks, then returns to the shoulder.
        /// </summary>
        public void ShowContextualHint()
        {
            string hint = GetDynamicTierHint(_currentState);

            // Smoothly swoop in front of the player, type hint, then return to side
            _bot.ShowHint(hint, hintExpressionDuration, null, forceInFront: true);

            Debug.Log($"<color=#00E5FF>[CompanionPuzzleBridge] Contextual dynamic hint requested (state={_currentState}): {hint}</color>");

            // Notify signal collector so hint count feeds into ML features
            _collector?.RegisterHintShown();

            if (AdaptVRBackendClient.Instance != null)
            {
                int orbitNum = puzzleManager != null ? puzzleManager.currentUnlockedOrbit : 1;
                string planetName = (orbitNum >= 1 && orbitNum <= PlanetDisplayNames.Length) ? PlanetDisplayNames[orbitNum - 1] : "Planet";
                AdaptVRBackendClient.Instance.SendGameplayEvent(
                    eventType: "hint_requested",
                    pieceId: planetName,
                    orbitIndex: orbitNum,
                    isCorrect: false,
                    action: "contextual_hint",
                    extraNotes: hint
                );
            }

            if (SessionManager.HasInstance && SessionManager.Instance != null)
            {
                SessionManager.Instance.RecordLearningEvent("contextual_hint_shown", "CompanionBot", hint);
            }

            // Update last proactive time so we don't double-fire
            _lastProactiveHintTime = Time.time;
        }

        // ── Called from SolarSystemPuzzleManager events ───────────────────────

        /// <summary>
        /// Called when any planet is placed correctly. Triggers joyous celebration
        /// and dynamically displays a congratulatory message naming the placed planet!
        /// Wire this to SolarSystemPuzzleManager.onPlanetPlacedCorrectly.
        /// </summary>
        public void CelebrateSuccess()
        {
            // Joyous physical animation (happy eyes, cheerful chirp, celebratory bounce)
            _bot.Celebrate(celebrationDuration);

            // Display dynamic congratulations from the shoulder so it doesn't block table view
            string congrats = GetDynamicCongratsMessage();
            if (!string.IsNullOrEmpty(congrats))
            {
                _bot.ShowHint(congrats, celebrationDuration + 2.0f, null, forceInFront: false);
            }

            // Arm the first-hint state so the next orbit's hint will swoop in front
            if (resetFirstHintPerOrbit && _bot != null)
            {
                _bot.ResetFirstHintState();
            }

            Debug.Log($"<color=#00FF66>[CompanionPuzzleBridge] Celebrating correct planet placement: {congrats}</color>");
        }

        /// <summary>
        /// Called when the entire puzzle is complete. Big celebration in front of player!
        /// Wire this to SolarSystemPuzzleManager.onPuzzleCompleted.
        /// </summary>
        public void CelebratePuzzleComplete()
        {
            if (_bot != null)
            {
                _bot.ResetFirstHintState();
            }

            // Longer, bigger celebration swooping in front for the grand finale
            _bot.Celebrate(celebrationDuration * 1.8f);
            _bot.ShowHint("🌟 INCREDIBLE! You solved the entire Solar System in perfect order! All 8 planets are home! 🪐", 7f, null, forceInFront: true);
            Debug.Log("<color=#00FF66>[CompanionPuzzleBridge] Celebrating FULL puzzle completion!</color>");
        }

        /// <summary>
        /// Resets the bot's first-hint state so that the next hint will again come in front of the camera.
        /// </summary>
        public void ResetFirstHintState()
        {
            _bot?.ResetFirstHintState();
        }

        /// <summary>
        /// Called when a planet is placed incorrectly. Offers gentle encouragement
        /// dynamically naming the target planet needed for this orbit.
        /// Wire this to SolarSystemPuzzleManager.onPlanetPlacedIncorrectly.
        /// </summary>
        public void EncourageOnError()
        {
            // Only intervene if cooldown allows
            if (_currentState == LearnerState.Low) return; // Handled by proactive scaffolding
            if (Time.time - _lastProactiveHintTime < 8f) return; // Respect short cooldown

            _lastProactiveHintTime = Time.time;
            string errorMsg = GetDynamicEncouragementMessage();
            _bot.ShowHint(errorMsg, 5f);
            Debug.Log($"<color=#FFAA00>[CompanionPuzzleBridge] Encouragement on error: {errorMsg}</color>");
        }

        // ── Dynamic Astronomical Hint Helpers ─────────────────────────────────

        /// <summary>
        /// Returns the 1-based index (1..8) of the currently active/unlocked orbit slot.
        /// </summary>
        public int GetCurrentTargetOrbitIndex()
        {
            if (puzzleManager != null)
            {
                return Mathf.Clamp(puzzleManager.currentUnlockedOrbit, 1, 8);
            }
            return 1;
        }

        /// <summary>
        /// Returns the human-readable display name for the planet in the given 1-based orbit.
        /// </summary>
        public string GetPlanetDisplayName(int orbitIndex)
        {
            int idx = Mathf.Clamp(orbitIndex - 1, 0, PlanetDisplayNames.Length - 1);
            return PlanetDisplayNames[idx];
        }

        /// <summary>
        /// Generates a dynamic congratulatory message tailored to the planet just placed.
        /// </summary>
        public string GetDynamicCongratsMessage()
        {
            if (puzzleManager != null && puzzleManager.lastPlacedPlanet != null)
            {
                int orbit = puzzleManager.lastPlacedSlot != null
                    ? puzzleManager.lastPlacedSlot.orbitIndex
                    : puzzleManager.correctPlacementsCount;

                int idx = Mathf.Clamp(orbit - 1, 0, PlanetCongratsMessages.Length - 1);
                return PlanetCongratsMessages[idx];
            }

            // Fallback based on correct placement count
            int placedCount = puzzleManager != null ? puzzleManager.correctPlacementsCount : 1;
            int fallbackIdx = Mathf.Clamp(placedCount - 1, 0, PlanetCongratsMessages.Length - 1);
            return PlanetCongratsMessages[fallbackIdx];
        }

        /// <summary>
        /// Generates dynamic contextual hints for the current learner state and active target orbit.
        /// On High Cognitive Load (LearnerState.Low), dynamically names the planet and gives clear visual clues.
        /// </summary>
        public string GetDynamicTierHint(LearnerState state)
        {
            int targetOrbit = GetCurrentTargetOrbitIndex();
            int idx = Mathf.Clamp(targetOrbit - 1, 0, 7);

            return state switch
            {
                LearnerState.Low  => DynamicStruggleHintsTier3[idx],
                LearnerState.High => DynamicMasteryHintsTier1[idx],
                _                 => DynamicGuidanceHintsTier2[idx]
            };
        }

        /// <summary>
        /// Generates dynamic encouragement mentioning the target planet needed.
        /// </summary>
        public string GetDynamicEncouragementMessage()
        {
            int targetOrbit = GetCurrentTargetOrbitIndex();
            string planetName = GetPlanetDisplayName(targetOrbit);
            return $"Not quite! Orbit {targetOrbit} needs {planetName}. Think about the order from the Sun!";
        }
    }
}
