using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveVR.UI
{
    /// <summary>
    /// World-Space VR HUD that floats near the solar system table.
    /// Displays the current puzzle objective and adapts its content
    /// based on the learner's cognitive-load state:
    ///
    ///   Low    → Full scaffolding: objective + planet hint + encouragement
    ///   Medium → Standard: objective + basic guidance
    ///   High   → Minimal: just progress bar (removes visual noise)
    ///
    /// Attach to a World-Space Canvas GameObject in the scene.
    /// All UI elements are procedurally generated if not assigned in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class AdaptiveGuidanceHUD : MonoBehaviour
    {
        // ── Planet Name Labels ────────────────────────────────────────────────
        private static readonly string[] PlanetNames = new string[]
        {
            "Mercury ☿", "Venus ♀", "Earth 🌍", "Mars ♂",
            "Jupiter ♃", "Saturn ♄", "Uranus ⛢", "Neptune ♆"
        };

        private static readonly string[] PlanetHints = new string[]
        {
            "Mercury is the smallest planet and closest to the Sun.",
            "Venus is the hottest planet — second from the Sun.",
            "Earth is our home — the third planet.",
            "Mars is the Red Planet — fourth from the Sun.",
            "Jupiter is the largest planet — fifth from the Sun.",
            "Saturn has beautiful rings — sixth from the Sun.",
            "Uranus is the ice giant — seventh from the Sun.",
            "Neptune is the outermost planet — farthest from the Sun."
        };

        [Header("References (auto-wired by AdaptiveSystemEditor)")]
        [Tooltip("SolarSystemPuzzleManager to track progress and active orbit.")]
        public AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager puzzleManager;

        [Header("HUD Position & Scale")]
        [Tooltip("Distance from the player head to spawn the HUD (meters).")]
        [Range(0.5f, 3f)]
        public float spawnDistance = 1.4f;

        [Tooltip("Height offset relative to the VR headset position.")]
        [Range(-0.5f, 0.5f)]
        public float heightOffset = -0.2f;

        [Tooltip("World-space scale of the canvas (default 0.002 = 2mm per pixel).")]
        public float canvasWorldScale = 0.0018f;

        [Header("Fade")]
        [Tooltip("Seconds to fade in/out when the HUD becomes visible or hidden.")]
        [Range(0.1f, 2f)]
        public float fadeDuration = 0.5f;

        [Header("Colors")]
        public Color cardBackground    = new Color(0.04f, 0.08f, 0.18f, 0.92f);
        public Color accentCyan        = new Color(0.0f,  0.88f, 1.0f,  1.0f);
        public Color textPrimary       = new Color(0.92f, 0.98f, 1.0f,  1.0f);
        public Color textSecondary     = new Color(0.6f,  0.85f, 0.95f, 1.0f);
        public Color progressBarFull   = new Color(0.1f,  0.92f, 0.45f, 1.0f);
        public Color progressBarEmpty  = new Color(0.15f, 0.22f, 0.32f, 1.0f);
        public Color stateColorLow     = new Color(0.95f, 0.65f, 0.15f, 1.0f);
        public Color stateColorMedium  = new Color(0.2f,  0.7f,  1.0f,  1.0f);
        public Color stateColorHigh    = new Color(0.1f,  0.95f, 0.45f, 1.0f);

        // ── Internal UI Elements ──────────────────────────────────────────────
        private CanvasGroup  _canvasGroup;
        private Text         _objectiveLabel;
        private Text         _hintLabel;
        private Text         _progressLabel;
        private Image[]      _progressPips;
        private Image        _stateIndicator;
        private Text         _stateLabel;
        private GameObject   _hintSection;

        private AdaptiveVR.Adaptive.LearnerState _currentState = AdaptiveVR.Adaptive.LearnerState.Medium;
        private bool _isBuilt = false;
        private Coroutine _followRoutine;

        private const int TOTAL_PLANETS = 8;
        private const int CANVAS_WIDTH  = 380;
        private const int CANVAS_HEIGHT = 210;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetupCanvas();
            BuildHUDLayout();
            _isBuilt = true;
        }

        private void Start()
        {
#if UNITY_2023_1_OR_NEWER
            if (puzzleManager == null)
                puzzleManager = FindFirstObjectByType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
#else
            if (puzzleManager == null)
                puzzleManager = FindObjectOfType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
#endif
            PositionInFrontOfPlayer();
            _followRoutine = StartCoroutine(SoftFollowHeadRoutine());
            Apply(AdaptiveVR.Adaptive.LearnerState.Medium);
        }

        private void OnDestroy()
        {
            if (_followRoutine != null) StopCoroutine(_followRoutine);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by GuidanceAdapter when learner state changes.
        /// Updates HUD content and scaffolding density.
        /// </summary>
        public void Apply(AdaptiveVR.Adaptive.LearnerState state)
        {
            _currentState = state;
            if (!_isBuilt) return;

            RefreshProgress();
            RefreshObjective();
            RefreshHintSection(state);
            RefreshStateIndicator(state);
        }

        /// <summary>
        /// Refresh after a planet is placed (called by SolarSystemPuzzleManager events).
        /// </summary>
        public void OnPlanetPlaced()
        {
            if (!_isBuilt) return;
            RefreshProgress();
            RefreshObjective();
        }

        // ── Content Refresh ───────────────────────────────────────────────────

        private void RefreshProgress()
        {
            int placed = puzzleManager != null ? puzzleManager.correctPlacementsCount : 0;

            if (_progressLabel != null)
                _progressLabel.text = $"{placed} / {TOTAL_PLANETS} Planets";

            if (_progressPips != null)
            {
                for (int i = 0; i < _progressPips.Length && i < TOTAL_PLANETS; i++)
                {
                    if (_progressPips[i] != null)
                        _progressPips[i].color = i < placed ? progressBarFull : progressBarEmpty;
                }
            }
        }

        private void RefreshObjective()
        {
            if (_objectiveLabel == null || puzzleManager == null) return;

            bool complete = puzzleManager.isPuzzleComplete;
            int activeIdx = Mathf.Clamp(puzzleManager.currentUnlockedOrbit - 1, 0, TOTAL_PLANETS - 1);

            if (complete)
            {
                _objectiveLabel.text = "🌟 Solar System Complete!";
                _objectiveLabel.color = progressBarFull;
            }
            else
            {
                _objectiveLabel.text = $"Next: {PlanetNames[activeIdx]}";
                _objectiveLabel.color = textPrimary;
            }
        }

        private void RefreshHintSection(AdaptiveVR.Adaptive.LearnerState state)
        {
            if (_hintLabel == null || _hintSection == null) return;

            switch (state)
            {
                case AdaptiveVR.Adaptive.LearnerState.Low:
                    // Full scaffolding: show planet hint
                    _hintSection.SetActive(true);
                    if (puzzleManager != null)
                    {
                        int idx = Mathf.Clamp(puzzleManager.currentUnlockedOrbit - 1, 0, TOTAL_PLANETS - 1);
                        _hintLabel.text = PlanetHints[idx];
                    }
                    break;

                case AdaptiveVR.Adaptive.LearnerState.Medium:
                    // Partial hint: just remind of order rule
                    _hintSection.SetActive(true);
                    _hintLabel.text = "Planets orbit farther from the Sun as order increases.";
                    _hintLabel.color = textSecondary;
                    break;

                case AdaptiveVR.Adaptive.LearnerState.High:
                    // Minimal: hide hint to reduce visual noise for confident learner
                    _hintSection.SetActive(false);
                    break;
            }
        }

        private void RefreshStateIndicator(AdaptiveVR.Adaptive.LearnerState state)
        {
            if (_stateIndicator == null || _stateLabel == null) return;

            switch (state)
            {
                case AdaptiveVR.Adaptive.LearnerState.Low:
                    _stateIndicator.color = stateColorLow;
                    _stateLabel.text = "Guided";
                    break;
                case AdaptiveVR.Adaptive.LearnerState.Medium:
                    _stateIndicator.color = stateColorMedium;
                    _stateLabel.text = "Learning";
                    break;
                case AdaptiveVR.Adaptive.LearnerState.High:
                    _stateIndicator.color = stateColorHigh;
                    _stateLabel.text = "Mastery";
                    break;
            }
        }

        // ── Canvas Setup & Layout ─────────────────────────────────────────────

        private void SetupCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_WIDTH, CANVAS_HEIGHT);
            rt.localScale = Vector3.one * canvasWorldScale;

            _canvasGroup.alpha = 1f;
        }

        private void BuildHUDLayout()
        {
            // ── Background card ──
            GameObject bgObj = new GameObject("HUD_Background");
            bgObj.transform.SetParent(transform, false);
            Image bg = bgObj.AddComponent<Image>();
            bg.color = cardBackground;
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // ── Top accent bar ──
            GameObject accentObj = new GameObject("HUD_AccentBar");
            accentObj.transform.SetParent(transform, false);
            Image accent = accentObj.AddComponent<Image>();
            accent.color = accentCyan;
            RectTransform accentRT = accentObj.GetComponent<RectTransform>();
            accentRT.anchorMin = new Vector2(0f, 1f);
            accentRT.anchorMax = new Vector2(1f, 1f);
            accentRT.pivot     = new Vector2(0.5f, 1f);
            accentRT.sizeDelta = new Vector2(0f, 4f);
            accentRT.anchoredPosition = Vector2.zero;

            // ── State indicator dot + label (top-right) ──
            GameObject stateContainer = new GameObject("HUD_StateContainer");
            stateContainer.transform.SetParent(transform, false);
            RectTransform stateContRT = stateContainer.AddComponent<RectTransform>();
            stateContRT.anchorMin = new Vector2(1f, 1f);
            stateContRT.anchorMax = new Vector2(1f, 1f);
            stateContRT.pivot     = new Vector2(1f, 1f);
            stateContRT.anchoredPosition = new Vector2(-12f, -10f);
            stateContRT.sizeDelta = new Vector2(90f, 22f);

            GameObject dotObj = new GameObject("HUD_StateDot");
            dotObj.transform.SetParent(stateContainer.transform, false);
            _stateIndicator = dotObj.AddComponent<Image>();
            _stateIndicator.color = stateColorMedium;
            RectTransform dotRT = dotObj.GetComponent<RectTransform>();
            dotRT.anchorMin = new Vector2(0f, 0.5f);
            dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0f, 0.5f);
            dotRT.anchoredPosition = Vector2.zero;
            dotRT.sizeDelta = new Vector2(10f, 10f);

            _stateLabel = CreateText(stateContainer.transform, "HUD_StateLabel", "",
                8, TextAnchor.MiddleLeft, textSecondary,
                new Vector2(14f, 0f), new Vector2(80f, 22f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));

            // ── Objective label ──
            _objectiveLabel = CreateText(transform, "HUD_Objective", "Next: ...",
                15, TextAnchor.UpperLeft, textPrimary,
                new Vector2(14f, -18f), new Vector2(CANVAS_WIDTH - 110f, 28f),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            _objectiveLabel.fontStyle = FontStyle.Bold;

            // ── Progress pips row ──
            GameObject pipsRow = new GameObject("HUD_PipsRow");
            pipsRow.transform.SetParent(transform, false);
            RectTransform pipsRT = pipsRow.AddComponent<RectTransform>();
            pipsRT.anchorMin = new Vector2(0f, 1f);
            pipsRT.anchorMax = new Vector2(1f, 1f);
            pipsRT.pivot     = new Vector2(0f, 1f);
            pipsRT.anchoredPosition = new Vector2(14f, -52f);
            pipsRT.sizeDelta = new Vector2(-(14f * 2), 14f);

            _progressPips = new Image[TOTAL_PLANETS];
            float pipWidth = (CANVAS_WIDTH - 28f) / TOTAL_PLANETS - 3f;
            for (int i = 0; i < TOTAL_PLANETS; i++)
            {
                GameObject pipObj = new GameObject($"Pip_{i}");
                pipObj.transform.SetParent(pipsRow.transform, false);
                Image pip = pipObj.AddComponent<Image>();
                pip.color = progressBarEmpty;
                RectTransform pipRT = pipObj.GetComponent<RectTransform>();
                pipRT.anchorMin = new Vector2(0f, 0f);
                pipRT.anchorMax = new Vector2(0f, 1f);
                pipRT.pivot     = new Vector2(0f, 0f);
                pipRT.anchoredPosition = new Vector2(i * (pipWidth + 3f), 0f);
                pipRT.sizeDelta = new Vector2(pipWidth, 0f);
                _progressPips[i] = pip;
            }

            // ── Progress label ──
            _progressLabel = CreateText(transform, "HUD_ProgressLabel", "0 / 8 Planets",
                9, TextAnchor.UpperLeft, textSecondary,
                new Vector2(14f, -72f), new Vector2(180f, 18f),
                new Vector2(0f, 1f), new Vector2(0f, 1f));

            // ── Divider ──
            GameObject divObj = new GameObject("HUD_Divider");
            divObj.transform.SetParent(transform, false);
            Image div = divObj.AddComponent<Image>();
            div.color = new Color(accentCyan.r, accentCyan.g, accentCyan.b, 0.25f);
            RectTransform divRT = divObj.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0f, 1f);
            divRT.anchorMax = new Vector2(1f, 1f);
            divRT.pivot     = new Vector2(0.5f, 1f);
            divRT.anchoredPosition = new Vector2(0f, -94f);
            divRT.sizeDelta = new Vector2(-28f, 1f);

            // ── Hint section ──
            _hintSection = new GameObject("HUD_HintSection");
            _hintSection.transform.SetParent(transform, false);
            RectTransform hintSectionRT = _hintSection.AddComponent<RectTransform>();
            hintSectionRT.anchorMin = new Vector2(0f, 0f);
            hintSectionRT.anchorMax = new Vector2(1f, 1f);
            hintSectionRT.pivot     = new Vector2(0f, 1f);
            hintSectionRT.anchoredPosition = new Vector2(0f, -98f);
            hintSectionRT.sizeDelta = new Vector2(0f, -98f);

            _hintLabel = CreateText(_hintSection.transform, "HUD_HintLabel",
                "Loading...",
                9, TextAnchor.UpperLeft, textSecondary,
                new Vector2(14f, -4f), new Vector2(-(14f * 2), 90f),
                new Vector2(0f, 1f), new Vector2(1f, 1f));
            _hintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private Text CreateText(Transform parent, string name, string defaultText,
            int fontSize, TextAnchor anchor, Color color,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text       = defaultText;
            t.fontSize   = fontSize;
            t.alignment  = anchor;
            t.color      = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Truncate;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = anchorMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = sizeDelta;

            return t;
        }

        // ── Positioning & Head Follow ─────────────────────────────────────────

        private void PositionInFrontOfPlayer()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            transform.position = cam.transform.position
                                + forward * spawnDistance
                                + Vector3.up * heightOffset;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private IEnumerator SoftFollowHeadRoutine()
        {
            // Gently drift the HUD to stay accessible after large player turns
            const float DEADZONE_ANGLE = 30f;
            const float FOLLOW_SPEED   = 2.5f;

            while (true)
            {
                yield return new WaitForSeconds(0.1f);

                Camera cam = Camera.main;
                if (cam == null) continue;

                Vector3 desiredForward = cam.transform.forward;
                desiredForward.y = 0f;
                if (desiredForward.sqrMagnitude < 0.001f) continue;
                desiredForward.Normalize();

                Vector3 toHUD = (transform.position - cam.transform.position);
                toHUD.y = 0f;
                if (toHUD.sqrMagnitude < 0.001f) continue;
                toHUD.Normalize();

                float angle = Vector3.Angle(desiredForward, toHUD);
                if (angle > DEADZONE_ANGLE)
                {
                    Vector3 targetPos = cam.transform.position
                                      + desiredForward * spawnDistance
                                      + Vector3.up * heightOffset;
                    transform.position = Vector3.Lerp(transform.position, targetPos,
                                                      Time.deltaTime * FOLLOW_SPEED);
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(desiredForward, Vector3.up),
                        Time.deltaTime * FOLLOW_SPEED);
                }
            }
        }
    }
}
