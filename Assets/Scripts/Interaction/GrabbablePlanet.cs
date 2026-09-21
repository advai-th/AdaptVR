using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using AdaptiveVR.Environment;

namespace AdaptiveVR.PlanetPuzzle
{
    /// <summary>
    /// Makes any custom planet prefab grabbable by Unity's XR Interaction Toolkit (XRGrabInteractable),
    /// VR direct hand tracking, or desktop mouse.
    /// Automatically connects with XRGrabInteractable so when released near a PlanetSlot,
    /// it smoothly snaps into place and updates the puzzle.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbablePlanet : MonoBehaviour
    {
        [Header("Planetary Identity")]
        [Tooltip("The planet preset assigned to this grabbable model.")]
        public PlanetPreset preset = PlanetPreset.Earth;

        [Tooltip("Human-readable title displayed for this planet.")]
        public string planetName = "Earth";

        [Header("Current Slot Placement")]
        [Tooltip("Current slot where this planet is placed (if any).")]
        public PlanetSlot currentSlot;

        [Header("Grabbing & Movement")]
        [Tooltip("Is the planet currently held by a hand, controller, or mouse?")]
        public bool isGrabbed = false;

        [Tooltip("Enable smooth snapping motion when released near an orbit slot.")]
        public bool enableSmoothSnap = true;

        [Tooltip("Speed of smooth movement towards snap target slot.")]
        public float snapSpeed = 14f;

        [Header("Floating UI Label Tag (Optional)")]
        [Tooltip("Optional Canvas object displaying planet name and facts above model.")]
        public GameObject labelCanvasObject;

        [Tooltip("UI Text element for planet title.")]
        public Text labelTitleText;

        [Tooltip("UI Text element for educational astronomical details.")]
        public Text labelDetailsText;

        // Internal references
        private Rigidbody _rigidbody;
        private SphereCollider _collider;
        private PlanetSpinner _spinner;
        private Camera _mainCamera;

        // XR Interaction Toolkit auto-integration
        private Component _xrInteractable;
        private PropertyInfo _xrIsSelectedProp;
        private bool _wasXrSelected = false;

        // Snapping interpolation state
        private bool _isSnapping = false;
        private Vector3 _targetSnapPosition;
        private Quaternion _targetSnapRotation;

        // Direct Hand / Controller grabbing state
        private Transform _currentGrabber;
        private Vector3 _grabOffset;
        private Quaternion _grabRotationOffset;

        // Desktop Mouse Dragging state (New Input System & Legacy compatible)
        private bool _isMouseDragging = false;
        private float _zCoord;
        private Vector3 _mouseOffset;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<SphereCollider>();
            _spinner = GetComponent<PlanetSpinner>();
            _mainCamera = Camera.main;

            ConfigurePhysics();
            SetupXRInteractableIntegration();

            if (_spinner != null && preset != PlanetPreset.Custom)
            {
                _spinner.ApplyPreset(preset);
            }

            SetupLabelUI();
        }

        private void Start()
        {
            UpdateLabelText();
            // Re-check XR interactable in Start in case it was added at runtime
            if (_xrInteractable == null)
            {
                SetupXRInteractableIntegration();
            }
        }

        private void SetupXRInteractableIntegration()
        {
            // Works seamlessly with XRGrabInteractable from Unity XR Interaction Toolkit (v2 or v3)
            _xrInteractable = GetComponent("XRGrabInteractable")
                           ?? GetComponent("XRBaseInteractable");

            if (_xrInteractable != null)
            {
                _xrIsSelectedProp = _xrInteractable.GetType().GetProperty("isSelected");
            }
        }

        private void Update()
        {
            // 1. Check XR Interaction Toolkit (XRGrabInteractable) selection state
            CheckXRInteractableState();

            // 2. Update billboard label to face VR / Main camera
            if (labelCanvasObject != null && labelCanvasObject.activeSelf)
            {
                if (_mainCamera == null) _mainCamera = Camera.main;
                if (_mainCamera != null)
                {
                    labelCanvasObject.transform.rotation = Quaternion.LookRotation(labelCanvasObject.transform.position - _mainCamera.transform.position);
                }
            }

            // 3. Follow direct hand grabber if not using XRGrabInteractable
            if (isGrabbed && _currentGrabber != null && _xrInteractable == null)
            {
                transform.position = _currentGrabber.TransformPoint(_grabOffset);
                transform.rotation = _currentGrabber.rotation * _grabRotationOffset;
            }

            // 4. Smooth snap interpolation into orbit slot
            if (_isSnapping && currentSlot != null)
            {
                transform.position = Vector3.Lerp(transform.position, _targetSnapPosition, Time.deltaTime * snapSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, _targetSnapRotation, Time.deltaTime * snapSpeed);

                if (Vector3.Distance(transform.position, _targetSnapPosition) < 0.003f)
                {
                    transform.position = _targetSnapPosition;
                    transform.rotation = _targetSnapRotation;
                    _isSnapping = false;
                }
            }

            // 5. Desktop mouse drag updates (compatible with New Input System & Legacy)
            HandleDesktopMouseInput();
        }

        private void CheckXRInteractableState()
        {
            if (_xrInteractable == null && _xrIsSelectedProp == null) return;

            try
            {
                bool isCurrentlySelected = (bool)_xrIsSelectedProp.GetValue(_xrInteractable, null);

                if (isCurrentlySelected && !_wasXrSelected)
                {
                    // Grabbed via XRGrabInteractable!
                    _wasXrSelected = true;
                    OnGrabStart(null);
                }
                else if (!isCurrentlySelected && _wasXrSelected)
                {
                    // Released via XRGrabInteractable!
                    _wasXrSelected = false;
                    OnGrabEnd();
                }
            }
            catch
            {
                // Fallback if property access fails
            }
        }

        /// <summary>
        /// Automatically calculates proper sphere collider bounds and sets up Rigidbody physics.
        /// </summary>
        public void ConfigurePhysics()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_collider == null) _collider = GetComponent<SphereCollider>();

            if (_rigidbody != null)
            {
                // Default to kinematic until interaction toolkit or release needs physics
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            if (_collider != null)
            {
                _collider.isTrigger = false;

                // Compute bounding box covering all child meshes
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        if (!(renderers[i] is LineRenderer) && !(renderers[i] is TrailRenderer))
                        {
                            b.Encapsulate(renderers[i].bounds);
                        }
                    }

                    float maxWorldExtent = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
                    float lossyScale = Mathf.Max(transform.lossyScale.x, 0.0001f);
                    _collider.radius = Mathf.Max(maxWorldExtent / lossyScale, 0.1f);
                    _collider.center = transform.InverseTransformPoint(b.center);
                }
                else
                {
                    if (_collider.radius < 0.1f) _collider.radius = 0.25f;
                }
            }
        }

        #region Grab & Release API (Can also be wired directly in XRGrabInteractable Inspector Events)

        /// <summary>
        /// Called when the planet is grabbed (by XRGrabInteractable, VR Hand, or Mouse).
        /// </summary>
        public void OnGrabStart(Transform grabber)
        {
            isGrabbed = true;
            _isSnapping = false;
            _currentGrabber = grabber;

            if (_currentGrabber != null)
            {
                _grabOffset = _currentGrabber.InverseTransformPoint(transform.position);
                _grabRotationOffset = Quaternion.Inverse(_currentGrabber.rotation) * transform.rotation;
            }

            // Remove from existing slot while dragging so slot becomes Empty
            if (currentSlot != null)
            {
                currentSlot.ClearSlot();
                currentSlot = null;
            }

            SetLabelVisibility(true);
        }

        public void OnGrabStart()
        {
            OnGrabStart(null);
        }

        /// <summary>
        /// Called when the planet is released.
        /// Searches for the closest PlanetSlot within range and snaps into place.
        /// </summary>
        public void OnGrabEnd()
        {
            isGrabbed = false;
            _currentGrabber = null;
            _isMouseDragging = false;

            PlanetSlot nearestSlot = FindNearestSnapSlot();

            if (nearestSlot != null)
            {
                _targetSnapPosition = nearestSlot.snapPoint != null ? nearestSlot.snapPoint.position : nearestSlot.transform.position;
                _targetSnapRotation = nearestSlot.snapPoint != null ? nearestSlot.snapPoint.rotation : nearestSlot.transform.rotation;

                nearestSlot.TrySnapPlanet(this);

                if (_rigidbody != null)
                {
                    _rigidbody.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
                    _rigidbody.linearVelocity = Vector3.zero;
#else
                    _rigidbody.velocity = Vector3.zero;
#endif
                    _rigidbody.angularVelocity = Vector3.zero;
                }

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
                if (_rigidbody != null)
                {
                    _rigidbody.isKinematic = true;
                }
            }
        }

        #endregion

        #region Proximity Slot Search

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

        #endregion

        #region Desktop Mouse Dragging (New Input System & Legacy Compatible)

        private void HandleDesktopMouseInput()
        {
            if (!Application.isPlaying) return;

            bool isDown = false;
            bool isHeld = false;
            bool isUp = false;
            Vector2 screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                isDown = UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
                isHeld = UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
                isUp = UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame;
                screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (!isDown) isDown = Input.GetMouseButtonDown(0);
                if (!isHeld) isHeld = Input.GetMouseButton(0);
                if (!isUp) isUp = Input.GetMouseButtonUp(0);
                if (screenPos == Vector2.zero) screenPos = Input.mousePosition;
            }
            catch { }
#endif

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // Clicked down: Raycast to detect if clicking this planet
            if (isDown && !isGrabbed)
            {
                Ray ray = _mainCamera.ScreenPointToRay(screenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    GrabbablePlanet hitPlanet = hit.collider.GetComponentInParent<GrabbablePlanet>();
                    if (hitPlanet == this)
                    {
                        _isMouseDragging = true;
                        _zCoord = _mainCamera.WorldToScreenPoint(transform.position).z;
                        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, _zCoord));
                        _mouseOffset = transform.position - worldPos;
                        OnGrabStart(null);
                    }
                }
            }

            // Dragging: follow cursor in world space
            if (_isMouseDragging && isGrabbed)
            {
                if (isUp || !isHeld)
                {
                    _isMouseDragging = false;
                    OnGrabEnd();
                }
                else
                {
                    Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, _zCoord));
                    transform.position = worldPos + _mouseOffset;
                }
            }
        }

        #endregion

        #region UI & Label Helpers

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

        public void SetLabelVisibility(bool visible)
        {
            if (labelCanvasObject != null)
            {
                labelCanvasObject.SetActive(visible);
            }
        }

        private void SetupLabelUI()
        {
            if (labelCanvasObject == null)
            {
                Transform childCanvas = transform.Find("LabelCanvas");
                if (childCanvas != null)
                {
                    labelCanvasObject = childCanvas.gameObject;
                }
            }
        }

        #endregion
    }
}
