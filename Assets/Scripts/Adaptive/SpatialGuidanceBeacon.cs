using System.Collections;
using UnityEngine;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Attaches a 3D glowing spatial beacon above the currently active orbit slot.
    /// Activates when the learner state is Low (struggling) to guide attention.
    /// Smoothly fades out in Medium/High states to eliminate visual noise.
    ///
    /// Attach to the AdaptiveSystem GameObject or any scene object.
    /// Drag the SolarSystemPuzzleManager reference in the Inspector —
    /// AdaptiveSystemEditor will wire this automatically.
    /// </summary>
    public class SpatialGuidanceBeacon : MonoBehaviour
    {
        [Header("References (auto-wired by AdaptiveSystemEditor)")]
        [Tooltip("The SolarSystemPuzzleManager to track which orbit slot is currently active.")]
        public AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager puzzleManager;

        [Header("Beacon Appearance")]
        [Tooltip("Height above the orbit slot center for the beacon glow ring.")]
        [Range(0.1f, 1.5f)]
        public float beaconHeight = 0.35f;

        [Tooltip("Diameter of the pulsing indicator ring.")]
        [Range(0.1f, 0.7f)]
        public float beaconDiameter = 0.22f;

        [Tooltip("Color of the beacon ring (matching companion's cyan theme).")]
        public Color beaconColor = new Color(0.0f, 0.88f, 1.0f, 1.0f);

        [Tooltip("Max glow intensity of the beacon point light.")]
        [Range(0.5f, 4f)]
        public float beaconLightIntensity = 2.5f;

        [Tooltip("Pulse speed of the beacon ring.")]
        [Range(0.5f, 5f)]
        public float pulseSpeed = 2.2f;

        [Header("Fade Settings")]
        [Tooltip("How fast the beacon fades in or out when state changes (seconds).")]
        [Range(0.2f, 3f)]
        public float fadeDuration = 0.6f;

        // ── Internal ──────────────────────────────────────────────────────────

        private GameObject _beaconRoot;
        private Renderer   _ringRenderer;
        private Light      _beaconLight;
        private Material   _beaconMaterial;
        private float      _targetAlpha  = 0f;
        private float      _currentAlpha = 0f;
        private int        _lastTrackedSlotIndex = -1;
        private bool       _beaconVisible = false;

        private static readonly int BaseColorId      = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId  = Shader.PropertyToID("_EmissionColor");
        private static readonly int ColorId          = Shader.PropertyToID("_Color");

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildBeaconGeometry();
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
            SetBeaconVisible(false);
        }

        private void Update()
        {
            // Smooth fade
            if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
            {
                float step = Time.deltaTime / Mathf.Max(fadeDuration, 0.05f);
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, step);
                ApplyAlpha(_currentAlpha);
            }

            // Only animate if visible
            if (_currentAlpha > 0.01f && _beaconRoot != null)
            {
                AnimateBeacon();
                TrackActiveSlot();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by GuidanceAdapter when the learner state changes.
        /// </summary>
        public void Apply(LearnerState state)
        {
            bool shouldShow = (state == LearnerState.Low);
            SetBeaconVisible(shouldShow);

            if (shouldShow)
            {
                TrackActiveSlot(); // immediately position beacon
                Debug.Log("[SpatialGuidanceBeacon] Beacon ON — guiding struggling learner.");
            }
            else
            {
                Debug.Log($"[SpatialGuidanceBeacon] Beacon OFF — state={state}.");
            }
        }

        // ── Beacon Positioning ────────────────────────────────────────────────

        private void TrackActiveSlot()
        {
            if (puzzleManager == null || _beaconRoot == null) return;

            int activeIndex = puzzleManager.currentUnlockedOrbit - 1;
            if (activeIndex < 0 || activeIndex >= puzzleManager.orbitSlots.Count) return;

            // Avoid redundant updates when slot hasn't changed
            if (activeIndex == _lastTrackedSlotIndex) return;
            _lastTrackedSlotIndex = activeIndex;

            var targetSlot = puzzleManager.orbitSlots[activeIndex];
            if (targetSlot == null) return;

            Vector3 slotPos = targetSlot.transform.position;
            _beaconRoot.transform.position = slotPos + Vector3.up * beaconHeight;

            Debug.Log($"[SpatialGuidanceBeacon] Tracking slot [{activeIndex + 1}] at {slotPos}.");
        }

        // ── Beacon Animation ──────────────────────────────────────────────────

        private void AnimateBeacon()
        {
            if (_ringRenderer == null) return;

            float pulse = 0.75f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * 0.25f;
            float scalePulse = 1f + Mathf.Sin(Time.time * pulseSpeed * 1.3f * Mathf.PI * 2f) * 0.18f;

            _beaconRoot.transform.localScale = Vector3.one * (beaconDiameter * scalePulse);

            Color emissive = beaconColor * (pulse * 3.5f * _currentAlpha);
            Color baseCol  = new Color(beaconColor.r, beaconColor.g, beaconColor.b, pulse * _currentAlpha);

            if (_beaconMaterial != null)
            {
                if (_beaconMaterial.HasProperty(BaseColorId))   _beaconMaterial.SetColor(BaseColorId, baseCol);
                if (_beaconMaterial.HasProperty(ColorId))       _beaconMaterial.SetColor(ColorId, baseCol);
                if (_beaconMaterial.HasProperty(EmissionColorId)) _beaconMaterial.SetColor(EmissionColorId, emissive);
            }

            if (_beaconLight != null)
            {
                _beaconLight.intensity = beaconLightIntensity * pulse * _currentAlpha;
                _beaconLight.color = beaconColor;
            }

            // Slowly spin the ring
            _beaconRoot.transform.Rotate(0f, Time.deltaTime * 60f, 0f, Space.Self);
        }

        // ── Geometry ──────────────────────────────────────────────────────────

        private void BuildBeaconGeometry()
        {
            _beaconRoot = new GameObject("AdaptiveBeacon");
            _beaconRoot.transform.SetParent(transform, false);

            // Horizontal ring quad
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BeaconRing";
            quad.transform.SetParent(_beaconRoot.transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale    = Vector3.one;

            Collider col = quad.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            _ringRenderer = quad.GetComponent<Renderer>();

            // Build additive glow material
            _beaconMaterial = BuildAdditiveMaterial();
            if (Application.isPlaying)
                _ringRenderer.material = _beaconMaterial;
            else
                _ringRenderer.sharedMaterial = _beaconMaterial;
            _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringRenderer.receiveShadows    = false;

            // Point light
            GameObject lightObj = new GameObject("BeaconLight");
            lightObj.transform.SetParent(_beaconRoot.transform, false);
            lightObj.transform.localPosition = Vector3.up * 0.05f;
            _beaconLight = lightObj.AddComponent<Light>();
            _beaconLight.type    = LightType.Point;
            _beaconLight.color   = beaconColor;
            _beaconLight.range   = 0.9f;
            _beaconLight.shadows = LightShadows.None;

            _beaconRoot.SetActive(false);
        }

        private Material BuildAdditiveMaterial()
        {
            // Try URP first, then fallback to built-in
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Additive")
                         ?? Shader.Find("Mobile/Particles/Additive")
                         ?? Shader.Find("Unlit/Color");

            Material mat = new Material(shader ?? Shader.Find("Standard"));
            mat.name = "BeaconGlowMat";

            // URP transparent additive blend
            if (mat.HasProperty("_Surface"))  mat.SetFloat("_Surface", 1); // Transparent
            if (mat.HasProperty("_Blend"))    mat.SetFloat("_Blend", 0);   // Alpha
            mat.renderQueue = 3100;

            Color c = beaconColor;
            if (mat.HasProperty(BaseColorId))     mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId))         mat.SetColor(ColorId, c);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", c * 3f);
                mat.EnableKeyword("_EMISSION");
            }

            return mat;
        }

        // ── Visibility & Alpha ────────────────────────────────────────────────

        private void SetBeaconVisible(bool visible)
        {
            _beaconVisible = visible;
            _targetAlpha   = visible ? 1f : 0f;

            if (visible && _beaconRoot != null)
            {
                _beaconRoot.SetActive(true);
                _lastTrackedSlotIndex = -1; // Force re-position
            }
        }

        private void ApplyAlpha(float alpha)
        {
            // When fully invisible, deactivate root to save GPU
            if (_beaconRoot == null) return;
            if (alpha <= 0.001f)
            {
                _beaconRoot.SetActive(false);
                return;
            }

            if (!_beaconRoot.activeSelf) _beaconRoot.SetActive(true);

            Color c = new Color(beaconColor.r, beaconColor.g, beaconColor.b, alpha);
            if (_beaconMaterial != null)
            {
                if (_beaconMaterial.HasProperty(BaseColorId))    _beaconMaterial.SetColor(BaseColorId, c);
                if (_beaconMaterial.HasProperty(ColorId))        _beaconMaterial.SetColor(ColorId, c);
                if (_beaconMaterial.HasProperty(EmissionColorId)) _beaconMaterial.SetColor(EmissionColorId, beaconColor * (alpha * 3f));
            }

            if (_beaconLight != null)
                _beaconLight.intensity = beaconLightIntensity * alpha;
        }

        // ── Editor Gizmos ─────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!_beaconVisible) return;
            Gizmos.color = new Color(0f, 0.88f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * beaconHeight, beaconDiameter * 0.5f);
        }
    }
}
