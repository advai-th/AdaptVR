using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using AdaptiveVR.Environment;
using AdaptiveVR.Networking;
using AdaptiveVR.DataCollection.Core;

namespace AdaptiveVR.PlanetPuzzle
{
    /// <summary>
    /// Companion component for XRGrabInteractable on solar system planet prefabs.
    /// Connects the grabbable planet with orbital puzzle sockets (PlanetSlot),
    /// solar system progression (SolarSystemPuzzleManager), and educational UI labels.
    /// XRGrabInteractable handles all grabbing and physics movement natively;
    /// GrabbablePlanet handles slot snapping on release, planet identity, and scoring.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabbablePlanet : MonoBehaviour
    {
        [Header("Planetary Identity")]
        [Tooltip("The planet preset assigned to this grabbable model.")]
        public PlanetPreset preset = PlanetPreset.Earth;

        [Tooltip("Human-readable title displayed for this planet.")]
        public string planetName = "Earth";

        [Header("Current Slot Placement")]
        [Tooltip("Current orbital slot where this planet is placed (if any).")]
        public PlanetSlot currentSlot;

        [Header("Grabbing State (Read-Only)")]
        [Tooltip("Is the planet currently held by the player?")]
        public bool isGrabbed = false;

        [Header("Snapping Motion")]
        [Tooltip("Enable smooth snapping motion when released near an orbit slot.")]
        public bool enableSmoothSnap = true;

        [Tooltip("Speed of smooth movement towards snap target slot.")]
        public float snapSpeed = 14f;

        [Header("Initial Spot (Your Choice in Edit Mode)")]
        [Tooltip("World position where this planet originally started. Automatically captured from your Edit-Mode placement on Awake().")]
        public Vector3 initialPosition;

        [Tooltip("World rotation where this planet originally started.")]
        public Quaternion initialRotation;

        [Tooltip("Optional custom anchor Transform. If left null, automatically uses wherever you moved this planet in the Scene editor.")]
        public Transform customHomeAnchor;

        [Header("Wrong Orbit Auto-Return")]
        [Tooltip("Automatically float back to initial spot if placed in the wrong orbital slot.")]
        public bool autoReturnOnWrongOrbit = true;

        [Tooltip("Seconds to pause at the wrong slot showing error feedback before floating back.")]
        [Range(0.0f, 3.0f)]
        public float rejectPauseDuration = 0.8f;

        [Tooltip("Travel time (in seconds) to return to initial spot. Higher values mean slower, gentler motion.")]
        [Range(0.5f, 6.0f)]
        public float returnDuration = 2.0f;

        [Tooltip("Gentle upward parabolic arc height (in meters) during return to avoid obstacles.")]
        [Range(0.0f, 0.4f)]
        public float returnArcHeight = 0.12f;

        [Header("Floating UI Label Tag (Optional)")]
        [Tooltip("Optional Canvas object displaying planet name and facts above model.")]
        public GameObject labelCanvasObject;

        [Tooltip("UI Text element for planet title.")]
        public Text labelTitleText;

        [Tooltip("UI Text element for educational astronomical details.")]
        public Text labelDetailsText;

        [Tooltip("CanvasGroup for smooth alpha fading of the floating label.")]
        public CanvasGroup labelCanvasGroup;

        [Tooltip("Background Image of the label pill.")]
        public Image labelBackgroundImage;

        [Tooltip("Accent line Image at the base of the label pill.")]
        public Image labelAccentLineImage;

        [Tooltip("Additional padding height in meters above the top of the planet sphere.")]
        [Range(0.01f, 0.2f)]
        public float labelHoverPadding = 0.04f;

        [Tooltip("Automatically generate a lightweight World-Space canvas label if one is not assigned.")]
        public bool autoCreateLabelIfMissing = true;

        private float _targetLabelAlpha = 0f;
        private bool _isAdaptiveLabelActive = false;
        private bool _isGrabbedLabelActive = false;

        // Internal references
        private XRGrabInteractable _grabInteractable;
        private PlanetSpinner _spinner;
        private Camera _mainCamera;

        // Snapping interpolation state
        private bool _isSnapping = false;
        private Vector3 _targetSnapPosition;
        private Quaternion _targetSnapRotation;
        public float grabDuration = 0f;
        private float _grabStartTime = 0f;

        // Return-home interpolation state
        private Coroutine _returnRoutine;
        private bool _isReturningHome = false;
        public bool isReturningHome => _isReturningHome;

        private void Awake()
        {
            // Capture the exact position and rotation chosen in the Scene editor before entering Play Mode
            CaptureInitialSpot();

            _grabInteractable = GetComponent<XRGrabInteractable>();
            _spinner = GetComponent<PlanetSpinner>();
            _mainCamera = Camera.main;

            // Auto-detect preset from PlanetSpinner if this is a newly added component
            if (_spinner != null && _spinner.preset != PlanetPreset.Custom && preset == PlanetPreset.Earth && !name.ToLower().Contains("earth"))
            {
                preset = _spinner.preset;
                planetName = preset.ToString();
            }
            else if (_spinner == null && preset != PlanetPreset.Custom)
            {
                // Ensure missing spinners (e.g. Saturn, Uranus) are automatically attached with scientific presets
                _spinner = gameObject.AddComponent<PlanetSpinner>();
                _spinner.ApplyPreset(preset);
            }

            SetupLabelUI();
            ConfigurePhysics();
        }

        private void OnEnable()
        {
            if (_grabInteractable == null)
            {
                _grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
                _grabInteractable.selectEntered.AddListener(HandleSelectEntered);

                _grabInteractable.selectExited.RemoveListener(HandleSelectExited);
                _grabInteractable.selectExited.AddListener(HandleSelectExited);
            }
        }

        private void OnDisable()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
                _grabInteractable.selectExited.RemoveListener(HandleSelectExited);
            }

            if (_returnRoutine != null)
            {
                StopCoroutine(_returnRoutine);
                _returnRoutine = null;
            }
            _isReturningHome = false;
        }

        private void Start()
        {
            UpdateLabelText();
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            OnGrabStart(args?.interactorObject?.transform);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            OnGrabEnd();
        }

        /// <summary>
        /// Inspector hook for XRGrabInteractable -> selectEntered event
        /// </summary>
        public void OnXRSelectEntered()
        {
            OnGrabStart(null);
        }

        /// <summary>
        /// Inspector hook for XRGrabInteractable -> selectExited event
        /// </summary>
        public void OnXRSelectExited()
        {
            OnGrabEnd();
        }

        /// <summary>
        /// Called when the planet is grabbed by the player.
        /// </summary>
        public void OnGrabStart(Transform grabber = null)
        {
            isGrabbed = true;
            _isSnapping = false;
            _grabStartTime = Time.time;

            // Cancel any return flight immediately so the player takes smooth full control
            if (_returnRoutine != null)
            {
                StopCoroutine(_returnRoutine);
                _returnRoutine = null;
            }
            _isReturningHome = false;

            // Pause axial rotation to prevent controller-orientation fighting/jitter
            if (_spinner == null) _spinner = GetComponent<PlanetSpinner>();
            if (_spinner != null)
            {
                _spinner.isPaused = true;
            }

            // Clear previous slot assignment so the slot becomes empty while held
            if (currentSlot != null)
            {
                currentSlot.ClearSlot();
                currentSlot = null;
            }

            SetGrabbedLabelVisible(true);

            if (AdaptVRBackendClient.Instance != null)
            {
                AdaptVRBackendClient.Instance.SendGameplayEvent(
                    eventType: "gameplay",
                    pieceId: preset.ToString(),
                    orbitIndex: 0,
                    isCorrect: false,
                    action: "planet_grabbed",
                    extraNotes: $"Grabbed {preset}"
                );
            }

            if (SessionManager.HasInstance && SessionManager.Instance != null)
            {
                SessionManager.Instance.RecordInteraction(preset.ToString(), "grab", true);
            }
        }

        public void OnGrabStart()
        {
            OnGrabStart(null);
        }

        /// <summary>
        /// Called when the planet is released by the player.
        /// Finds the nearest active PlanetSlot within snapRadius and snaps into it.
        /// </summary>
        public void OnGrabEnd()
        {
            isGrabbed = false;
            grabDuration = Time.time - _grabStartTime;

            // Resume astronomical spin once released
            if (_spinner == null) _spinner = GetComponent<PlanetSpinner>();
            if (_spinner != null)
            {
                _spinner.isPaused = false;
            }

            SetGrabbedLabelVisible(false);

            if (AdaptVRBackendClient.Instance != null)
            {
                AdaptVRBackendClient.Instance.SendGameplayEvent(
                    eventType: "gameplay",
                    pieceId: preset.ToString(),
                    orbitIndex: 0,
                    isCorrect: false,
                    responseTime: grabDuration,
                    action: "planet_released",
                    extraNotes: $"Released {preset} after {grabDuration:F2}s"
                );
            }

            if (SessionManager.HasInstance && SessionManager.Instance != null)
            {
                SessionManager.Instance.RecordInteraction(preset.ToString(), "release", true, grabDuration);
            }

            PlanetSlot nearestSlot = FindNearestSnapSlot();

            if (nearestSlot != null)
            {
                _targetSnapPosition = nearestSlot.snapPoint != null ? nearestSlot.snapPoint.position : nearestSlot.transform.position;
                _targetSnapRotation = nearestSlot.snapPoint != null ? nearestSlot.snapPoint.rotation : nearestSlot.transform.rotation;

                bool isCorrect = nearestSlot.TrySnapPlanet(this);

                if (isCorrect)
                {
                    if (enableSmoothSnap)
                    {
                        _isSnapping = true;
                    }
                    else
                    {
                        transform.position = _targetSnapPosition;
                        transform.rotation = _targetSnapRotation;
                    }
                }
                else
                {
                    if (autoReturnOnWrongOrbit)
                    {
                        if (enableSmoothSnap)
                        {
                            _isSnapping = true;
                        }
                        else
                        {
                            transform.position = _targetSnapPosition;
                            transform.rotation = _targetSnapRotation;
                        }

                        if (_returnRoutine != null)
                        {
                            StopCoroutine(_returnRoutine);
                        }
                        _returnRoutine = StartCoroutine(RejectAndReturnRoutine(nearestSlot));
                    }
                    else
                    {
                        if (enableSmoothSnap)
                        {
                            _isSnapping = true;
                        }
                        else
                        {
                            transform.position = _targetSnapPosition;
                            transform.rotation = _targetSnapRotation;
                        }
                    }
                }
            }
        }

        #region Initial Spot & Rejection Return Logic

        /// <summary>
        /// Captures the current transform as the home spot.
        /// Called automatically in Awake() when entering Play Mode,
        /// and can be invoked from the Unity Inspector context menu in Edit Mode.
        /// </summary>
        [ContextMenu("Capture Current Position as Initial Spot")]
        public void CaptureInitialSpot()
        {
            if (customHomeAnchor != null)
            {
                initialPosition = customHomeAnchor.position;
                initialRotation = customHomeAnchor.rotation;
            }
            else
            {
                initialPosition = transform.position;
                initialRotation = transform.rotation;
            }
        }

        /// <summary>
        /// Explicitly updates the initial home spot to the specified position and rotation.
        /// </summary>
        public void SetInitialSpot(Vector3 pos, Quaternion rot)
        {
            initialPosition = pos;
            initialRotation = rot;
        }

        public Vector3 GetInitialTargetPosition()
        {
            return customHomeAnchor != null ? customHomeAnchor.position : initialPosition;
        }

        public Quaternion GetInitialTargetRotation()
        {
            return customHomeAnchor != null ? customHomeAnchor.rotation : initialRotation;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep inspector coordinates in sync in Edit Mode whenever moved in Scene
            if (!Application.isPlaying && customHomeAnchor == null)
            {
                initialPosition = transform.position;
                initialRotation = transform.rotation;
            }
        }
#endif

        /// <summary>
        /// Coroutine that pauses briefly at the wrong slot showing error feedback,
        /// clears the slot, then smoothly and slowly glides the planet back to its initial spot.
        /// </summary>
        private IEnumerator RejectAndReturnRoutine(PlanetSlot slot)
        {
            _isReturningHome = false;

            // Phase 1: Wait briefly so player registers the wrong slot error feedback (red indicator)
            if (rejectPauseDuration > 0f)
            {
                yield return new WaitForSeconds(rejectPauseDuration);
            }

            // If grabbed during pause, cancel
            if (isGrabbed)
            {
                _returnRoutine = null;
                yield break;
            }

            // Stop snapping to slot
            _isSnapping = false;

            // Phase 2: Clear the slot so it returns to empty state (cyan indicator)
            if (slot != null && slot.currentlyPlacedPlanet == this)
            {
                slot.ClearSlot();
            }
            if (currentSlot == slot)
            {
                currentSlot = null;
            }

            // Phase 3: Smooth slow return to initial spot
            _isReturningHome = true;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 targetPos = GetInitialTargetPosition();
            Quaternion targetRot = GetInitialTargetRotation();

            float duration = Mathf.Max(returnDuration, 0.1f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (isGrabbed)
                {
                    _isReturningHome = false;
                    _returnRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth S-curve easing
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                targetPos = GetInitialTargetPosition();
                targetRot = GetInitialTargetRotation();

                // Gentle parabolic arc in Y to prevent clipping table or other objects
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
                if (returnArcHeight > 0f)
                {
                    currentPos.y += Mathf.Sin(t * Mathf.PI) * returnArcHeight;
                }

                transform.position = currentPos;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
            _isReturningHome = false;
            _returnRoutine = null;
        }

        /// <summary>
        /// Triggers manual return to initial spot. Can be invoked via Inspector Context Menu or Unity Events.
        /// </summary>
        [ContextMenu("Return To Initial Spot")]
        public void ReturnToInitialSpot()
        {
            if (_returnRoutine != null) StopCoroutine(_returnRoutine);
            _returnRoutine = StartCoroutine(RejectAndReturnRoutine(currentSlot));
        }

        #endregion

        private void Update()
        {
            // 1. Smooth snap interpolation towards orbit socket (only when NOT grabbed)
            if (_isSnapping && !isGrabbed && currentSlot != null)
            {
                transform.position = Vector3.Lerp(transform.position, _targetSnapPosition, Time.deltaTime * snapSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, _targetSnapRotation, Time.deltaTime * snapSpeed);

                if (Vector3.Distance(transform.position, _targetSnapPosition) < 0.002f)
                {
                    transform.position = _targetSnapPosition;
                    transform.rotation = _targetSnapRotation;
                    _isSnapping = false;
                }
            }
        }

        private void LateUpdate()
        {
            // 2. Position, billboard, and fade floating label tag (in LateUpdate so it remains rock-steady above the planet after PlanetSpinner axial spin)
            UpdateFloatingLabel();
        }

        /// <summary>
        /// Searches all active PlanetSlots in the scene to find the closest one within its snapRadius.
        /// </summary>
        public PlanetSlot FindNearestSnapSlot()
        {
#if UNITY_2023_1_OR_NEWER
            PlanetSlot[] slots = FindObjectsByType<PlanetSlot>(FindObjectsSortMode.None);
#else
            PlanetSlot[] slots = FindObjectsOfType<PlanetSlot>();
#endif
            PlanetSlot bestSlot = null;
            float minDistance = float.MaxValue;

            foreach (PlanetSlot slot in slots)
            {
                if (slot == null || !slot.isSlotActive) continue;

                Vector3 targetPos = slot.snapPoint != null ? slot.snapPoint.position : slot.transform.position;
                float dist = Vector3.Distance(transform.position, targetPos);
                if (dist <= slot.snapRadius && dist < minDistance)
                {
                    minDistance = dist;
                    bestSlot = slot;
                }
            }

            return bestSlot;
        }

        /// <summary>
        /// Optimizes physics and XR Interaction Toolkit properties for smooth VR handling:
        /// - Sets XRGrabInteractable movementType to Instantaneous (eliminates 50Hz physics lag)
        /// - Enables useDynamicAttach (prevents unnatural center-of-mass snapping)
        /// - Enables RigidbodyInterpolation.Interpolate (smooth render pacing)
        /// - Disables extraneous/duplicate SphereColliders on the root GameObject
        /// </summary>
        public void ConfigurePhysics()
        {
            // 1. Optimize Rigidbody interpolation & physics state
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            // 2. Configure XRGrabInteractable for crisp, jitter-free VR hand tracking
            if (_grabInteractable == null)
            {
                _grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (_grabInteractable != null)
            {
                _grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
                _grabInteractable.useDynamicAttach = true;
                _grabInteractable.throwOnDetach = false;
                _grabInteractable.attachEaseInTime = 0.05f;
            }

            // 3. Deduplicate overlapping root SphereColliders
            SphereCollider[] sphereCols = GetComponents<SphereCollider>();
            if (sphereCols.Length > 1)
            {
                // Prefer the standard normalized radius (closest to 1.0) and disable duplicate colliders
                SphereCollider bestCol = sphereCols[0];
                float bestDiff = Mathf.Abs(bestCol.radius - 1f);
                for (int i = 1; i < sphereCols.Length; i++)
                {
                    float diff = Mathf.Abs(sphereCols[i].radius - 1f);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestCol = sphereCols[i];
                    }
                }

                for (int i = 0; i < sphereCols.Length; i++)
                {
                    if (sphereCols[i] != bestCol)
                    {
                        sphereCols[i].enabled = false;
                    }
                }
            }
        }

        #region Visual Scaffolding (Distractor Dimming & Target Highlighting)

        [Header("Visual Scaffolding (Distractor Dimming)")]
        [Tooltip("Is this planet currently dimmed/greyed out as a distractor?")]
        public bool isDimmed = false;

        [Tooltip("Is this planet currently highlighted as the active target?")]
        public bool isTargetHighlighted = false;

        private Renderer[] _cachedRenderers;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int FresnelColorId = Shader.PropertyToID("Color_B354DC38");

        private void InitRendererCache()
        {
            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
            {
                _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            }
            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }
        }

        /// <summary>
        /// Greys out and dims this planet as an incorrect distractor during high cognitive load.
        /// </summary>
        public void SetDistractorDimmed(bool dimmed)
        {
            isDimmed = dimmed;
            isTargetHighlighted = false;
            InitRendererCache();
            if (_cachedRenderers == null) return;

            foreach (var rend in _cachedRenderers)
            {
                if (rend == null) continue;
                if (rend.GetComponentInParent<Canvas>() != null) continue;

                rend.GetPropertyBlock(_propBlock);
                if (dimmed)
                {
                    Color dimColor = new Color(0.20f, 0.20f, 0.20f, 1.0f);
                    _propBlock.SetColor(BaseColorId, dimColor);
                    _propBlock.SetColor(ColorId, dimColor);
                    _propBlock.SetColor(EmissionColorId, Color.black);
                    _propBlock.SetColor(FresnelColorId, new Color(0f, 0f, 0f, 0f));
                }
                else
                {
                    _propBlock.Clear();
                }
                rend.SetPropertyBlock(_propBlock);
            }
        }

        /// <summary>
        /// Highlights this planet with full vibrant colors and luminous celestial glow as the correct target.
        /// </summary>
        public void SetTargetHighlighted(bool highlighted)
        {
            isTargetHighlighted = highlighted;
            isDimmed = false;
            InitRendererCache();
            if (_cachedRenderers == null) return;

            foreach (var rend in _cachedRenderers)
            {
                if (rend == null) continue;
                if (rend.GetComponentInParent<Canvas>() != null) continue;

                rend.GetPropertyBlock(_propBlock);
                if (highlighted)
                {
                    _propBlock.SetColor(BaseColorId, Color.white);
                    _propBlock.SetColor(ColorId, Color.white);
                    Color glowColor = new Color(0.1f, 0.9f, 1.0f, 1.0f);
                    _propBlock.SetColor(EmissionColorId, glowColor * 0.9f);
                    _propBlock.SetColor(FresnelColorId, glowColor);
                }
                else
                {
                    _propBlock.Clear();
                }
                rend.SetPropertyBlock(_propBlock);
            }
        }

        /// <summary>
        /// Resets this planet to normal pristine appearance.
        /// </summary>
        public void ResetScaffoldingVisuals()
        {
            isDimmed = false;
            isTargetHighlighted = false;
            InitRendererCache();
            if (_cachedRenderers == null) return;

            foreach (var rend in _cachedRenderers)
            {
                if (rend == null) continue;
                if (rend.GetComponentInParent<Canvas>() != null) continue;

                _propBlock.Clear();
                rend.SetPropertyBlock(_propBlock);
            }
        }

        #endregion

        #region UI & Label Helpers

        public string GetFormattedPlanetTitle()
        {
            if (!string.IsNullOrEmpty(planetName) && planetName != "Earth")
            {
                return planetName;
            }
            return preset.ToString();
        }

        public void UpdateLabelText()
        {
            if (string.IsNullOrEmpty(planetName) || planetName == "Earth")
            {
                planetName = preset.ToString();
            }

            if (labelTitleText != null)
            {
                labelTitleText.text = planetName;
            }

            if (labelDetailsText != null && _spinner != null)
            {
                labelDetailsText.text = $"Orbit: {_spinner.realOrbitPeriodYears:F1} Yr | Day: {_spinner.realDayLengthDays:F1} d";
            }
        }

        /// <summary>
        /// Backward-compatible visibility toggle.
        /// </summary>
        public void SetLabelVisibility(bool visible)
        {
            SetAdaptiveLabelVisible(visible);
        }

        /// <summary>
        /// Toggles whether this planet's name label is active from cognitive load scaffolding.
        /// </summary>
        public void SetAdaptiveLabelVisible(bool visible)
        {
            _isAdaptiveLabelActive = visible;
            UpdateTargetLabelVisibility();
        }

        /// <summary>
        /// Toggles whether this planet's name label is active because it is held by the player.
        /// </summary>
        public void SetGrabbedLabelVisible(bool visible)
        {
            _isGrabbedLabelActive = visible;
            UpdateTargetLabelVisibility();
        }

        private void UpdateTargetLabelVisibility()
        {
            bool shouldBeVisible = _isAdaptiveLabelActive || _isGrabbedLabelActive;
            _targetLabelAlpha = shouldBeVisible ? 1.0f : 0.0f;

            if (shouldBeVisible)
            {
                if (labelCanvasObject == null && autoCreateLabelIfMissing)
                {
                    CreateDefaultLabelUI();
                }

                if (labelCanvasObject != null && !labelCanvasObject.activeSelf)
                {
                    labelCanvasObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// Visually accents the label if this planet is the active target needed for the next orbit.
        /// </summary>
        public void SetLabelHighlight(bool isTarget)
        {
            if (labelTitleText != null)
            {
                labelTitleText.color = isTarget
                    ? new Color(0.15f, 1.0f, 0.95f, 1.0f)     // Vibrant celestial cyan for target
                    : new Color(0.85f, 0.92f, 0.98f, 0.88f);  // Soft subtle white/ice-blue for distractors
            }

            if (labelBackgroundImage != null)
            {
                labelBackgroundImage.color = isTarget
                    ? new Color(0.02f, 0.12f, 0.22f, 0.85f)   // Luminous deep cyan glass
                    : new Color(0.04f, 0.07f, 0.14f, 0.65f);  // Translucent dark glass
            }

            if (labelAccentLineImage != null)
            {
                labelAccentLineImage.color = isTarget
                    ? new Color(0.0f, 0.95f, 1.0f, 0.95f)
                    : new Color(0.0f, 0.75f, 0.9f, 0.45f);
            }
        }

        /// <summary>
        /// Computes the optimal hover height in world space above the top of this planet model.
        /// </summary>
        public float GetCalculatedHoverHeight()
        {
            float maxExtents = 0.06f;

            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc != null && sc.enabled)
            {
                float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                maxExtents = Mathf.Max(sc.radius * maxScale, 0.04f);
            }
            else
            {
                Renderer rend = GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    maxExtents = Mathf.Max(rend.bounds.extents.y, 0.04f);
                }
            }

            return maxExtents + labelHoverPadding;
        }

        private void UpdateFloatingLabel()
        {
            if (labelCanvasObject == null) return;

            // 1. Smooth alpha fade transition
            if (labelCanvasGroup != null)
            {
                float currentAlpha = labelCanvasGroup.alpha;
                if (!Mathf.Approximately(currentAlpha, _targetLabelAlpha))
                {
                    labelCanvasGroup.alpha = Mathf.MoveTowards(currentAlpha, _targetLabelAlpha, Time.deltaTime * 4.0f);
                    if (labelCanvasGroup.alpha <= 0.001f && _targetLabelAlpha <= 0.001f && labelCanvasObject.activeSelf)
                    {
                        labelCanvasObject.SetActive(false);
                    }
                }
            }

            // 2. Position and billboard when active
            if (labelCanvasObject.activeSelf)
            {
                // Float directly above the planet in world space (unaffected by planet axial tilt and spin)
                labelCanvasObject.transform.position = transform.position + Vector3.up * GetCalculatedHoverHeight();

                // Billboard towards main camera
                if (_mainCamera == null) _mainCamera = Camera.main;
                if (_mainCamera != null)
                {
                    Vector3 lookDir = labelCanvasObject.transform.position - _mainCamera.transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        labelCanvasObject.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                    }
                }
            }
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

        private void SetupLabelUI()
        {
            if (labelCanvasObject == null)
            {
                Transform childCanvas = transform.Find("LabelCanvas");
                if (childCanvas != null)
                {
                    labelCanvasObject = childCanvas.gameObject;
                    if (labelCanvasObject.GetComponent<Canvas>() == null)
                    {
                        CreateDefaultLabelUI();
                    }
                }
                else if (autoCreateLabelIfMissing)
                {
                    CreateDefaultLabelUI();
                }
            }

            if (labelCanvasObject != null)
            {
                if (labelCanvasGroup == null) labelCanvasGroup = labelCanvasObject.GetComponent<CanvasGroup>();
                if (labelTitleText == null) labelTitleText = labelCanvasObject.GetComponentInChildren<Text>();
                if (labelBackgroundImage == null) labelBackgroundImage = labelCanvasObject.GetComponentInChildren<Image>();
            }
        }

        /// <summary>
        /// Procedurally constructs a clean, lightweight, non-invasive floating World-Space UI label
        /// directly above the planet model if one isn't already assigned in the Inspector.
        /// </summary>
        [ContextMenu("Rebuild Floating Label Tag")]
        public void CreateDefaultLabelUI()
        {
            // 1. Root Canvas GameObject
            Transform existing = transform.Find("LabelCanvas");
            GameObject canvasGo = existing != null ? existing.gameObject : new GameObject("LabelCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = EnsureComponent<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            CanvasGroup cg = EnsureComponent<CanvasGroup>(canvasGo);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            labelCanvasGroup = cg;

            RectTransform canvasRt = EnsureComponent<RectTransform>(canvasGo);
            canvasRt.sizeDelta = new Vector2(110f, 32f);
            canvasRt.localScale = Vector3.one * 0.0009f; // Real-world dimensions: ~10cm wide, ~3cm tall
            canvasRt.pivot = new Vector2(0.5f, 0.5f);

            // 2. Background Pill
            Transform bgTrans = canvasGo.transform.Find("BackgroundPill");
            GameObject bgGo = bgTrans != null ? bgTrans.gameObject : new GameObject("BackgroundPill");
            bgGo.transform.SetParent(canvasGo.transform, false);

            Image bgImg = EnsureComponent<Image>(bgGo);
            bgImg.color = new Color(0.04f, 0.07f, 0.14f, 0.70f); // Translucent cosmic navy
            bgImg.raycastTarget = false;
            labelBackgroundImage = bgImg;

            RectTransform bgRt = EnsureComponent<RectTransform>(bgGo);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            bgRt.anchoredPosition = Vector2.zero;

            // 3. Subtle bottom accent line
            Transform lineTrans = canvasGo.transform.Find("AccentLine");
            GameObject lineGo = lineTrans != null ? lineTrans.gameObject : new GameObject("AccentLine");
            lineGo.transform.SetParent(canvasGo.transform, false);

            Image lineImg = EnsureComponent<Image>(lineGo);
            lineImg.color = new Color(0.0f, 0.85f, 1.0f, 0.65f); // Subtle celestial cyan line
            lineImg.raycastTarget = false;
            labelAccentLineImage = lineImg;

            RectTransform lineRt = EnsureComponent<RectTransform>(lineGo);
            lineRt.anchorMin = new Vector2(0.1f, 0f);
            lineRt.anchorMax = new Vector2(0.9f, 0f);
            lineRt.pivot = new Vector2(0.5f, 0f);
            lineRt.sizeDelta = new Vector2(0f, 1.8f);
            lineRt.anchoredPosition = new Vector2(0f, 1f);

            // 4. Title Text
            Transform textTrans = canvasGo.transform.Find("TitleText");
            GameObject textGo = textTrans != null ? textTrans.gameObject : new GameObject("TitleText");
            textGo.transform.SetParent(canvasGo.transform, false);

            Text txt = EnsureComponent<Text>(textGo);
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = GetFormattedPlanetTitle();
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.88f, 0.95f, 1.0f, 0.95f);
            txt.raycastTarget = false;
            labelTitleText = txt;

            RectTransform textRt = EnsureComponent<RectTransform>(textGo);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = new Vector2(0f, 1f);

            labelCanvasObject = canvasGo;
            labelCanvasObject.SetActive(false); // Hidden by default
        }

        #endregion
    }
}
