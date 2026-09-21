using System;
using UnityEngine;
using UnityEngine.Events;
using AdaptiveVR.Environment;

namespace AdaptiveVR.PlanetPuzzle
{
    public enum SlotState
    {
        Empty,
        Hovered,
        Correct,
        Incorrect
    }

    /// <summary>
    /// Orbital target socket for solar system planets.
    /// Detects when a GrabbablePlanet is placed, evaluates whether it matches expectedPreset,
    /// and provides rich, faint visual feedback on both the socket indicator and its orbital ring line.
    /// </summary>
    public class PlanetSlot : MonoBehaviour
    {
        [Header("Slot Identity")]
        [Tooltip("The planet preset that correctly belongs in this orbital slot.")]
        public PlanetPreset expectedPreset = PlanetPreset.Earth;

        [Tooltip("Orbital order position (1 = Mercury, 2 = Venus, ... 8 = Neptune).")]
        [Range(1, 8)]
        public int orbitIndex = 3;

        [Tooltip("Radius of this orbit from the central Sun/origin in meters.")]
        public float orbitRadius = 1.2f;

        [Header("Snap Configuration")]
        [Tooltip("Distance threshold (in meters) within which a planet will snap into this slot.")]
        public float snapRadius = 0.35f;

        [Tooltip("Transform position where the snapped planet will rest. Defaults to this transform.")]
        public Transform snapPoint;

        [Header("Visual Feedback Indicators")]
        [Tooltip("Renderer of the target ring / glow indicator object.")]
        public Renderer indicatorRenderer;

        [Tooltip("Optional LineRenderer of the full 360-degree orbit ring for visual feedback.")]
        public LineRenderer orbitLineRenderer;

        [Tooltip("Optional root GameObject of the entire orbit (containing line and socket) for sequential activation.")]
        public GameObject orbitRootObject;

        [Header("Feedback Colors (Faint & Subtle)")]
        public Color emptyColor = new Color(0.2f, 0.65f, 0.95f, 0.15f);    // Faint Soft Cyan (Lower Opacity)
        public Color hoverColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);       // Subtle Soft White Hover
        public Color correctColor = new Color(0.1f, 0.9f, 0.4f, 0.6f);     // Pleasant Soft Green
        public Color incorrectColor = new Color(0.95f, 0.25f, 0.2f, 0.6f); // Soft Warning Red

        [Header("Current Placement State")]
        public GrabbablePlanet currentlyPlacedPlanet;
        public SlotState currentState = SlotState.Empty;
        public bool isSlotActive = true;

        [Header("Unity Events")]
        public UnityEvent onCorrectPlacement;
        public UnityEvent onIncorrectPlacement;
        public UnityEvent onSlotCleared;

        // C# Action event for puzzle manager
        public event Action<PlanetSlot, GrabbablePlanet, bool> OnPlanetStateChanged;

        private Material _indicatorMaterial;
        private Material _orbitLineMaterial;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (snapPoint == null)
            {
                snapPoint = transform;
            }

            SetupMaterials();
            UpdateVisualFeedback();
        }

        private void SetupMaterials()
        {
            if (indicatorRenderer != null)
            {
                _indicatorMaterial = indicatorRenderer.material;
            }

            if (orbitLineRenderer != null)
            {
                _orbitLineMaterial = orbitLineRenderer.material;
            }
        }

        /// <summary>
        /// Controls whether this entire orbital slot (and its ring line) is active and visible.
        /// Used by SolarSystemPuzzleManager for sequential progression.
        /// </summary>
        public void SetSlotActive(bool active)
        {
            isSlotActive = active;

            if (orbitRootObject != null)
            {
                orbitRootObject.SetActive(active);
            }
            else
            {
                gameObject.SetActive(active);
                if (orbitLineRenderer != null)
                {
                    orbitLineRenderer.gameObject.SetActive(active);
                }
            }
        }

        /// <summary>
        /// Checks if a grabbable planet is within snap distance of this slot.
        /// </summary>
        public bool IsInSnapRange(Vector3 planetPosition)
        {
            if (!isSlotActive) return false;
            Vector3 target = snapPoint != null ? snapPoint.position : transform.position;
            return Vector3.Distance(target, planetPosition) <= snapRadius;
        }

        /// <summary>
        /// Snaps a planet into this slot and evaluates correctness.
        /// </summary>
        public bool TrySnapPlanet(GrabbablePlanet planet)
        {
            if (!isSlotActive || planet == null) return false;

            // If another planet was occupying this slot, disconnect it
            if (currentlyPlacedPlanet != null && currentlyPlacedPlanet != planet)
            {
                GrabbablePlanet prev = currentlyPlacedPlanet;
                currentlyPlacedPlanet = null;
                prev.currentSlot = null;
            }

            currentlyPlacedPlanet = planet;
            planet.currentSlot = this;

            bool isCorrect = (planet.preset == expectedPreset);
            currentState = isCorrect ? SlotState.Correct : SlotState.Incorrect;

            UpdateVisualFeedback();

            if (isCorrect)
            {
                onCorrectPlacement?.Invoke();
            }
            else
            {
                onIncorrectPlacement?.Invoke();
            }

            OnPlanetStateChanged?.Invoke(this, planet, isCorrect);
            return isCorrect;
        }

        /// <summary>
        /// Clears the slot when a planet is picked up or removed.
        /// </summary>
        public void ClearSlot()
        {
            if (currentlyPlacedPlanet != null)
            {
                GrabbablePlanet prev = currentlyPlacedPlanet;
                currentlyPlacedPlanet = null;
                if (prev.currentSlot == this)
                {
                    prev.currentSlot = null;
                }

                currentState = SlotState.Empty;
                UpdateVisualFeedback();

                onSlotCleared?.Invoke();
                OnPlanetStateChanged?.Invoke(this, null, false);
            }
            else
            {
                currentState = SlotState.Empty;
                UpdateVisualFeedback();
            }
        }

        /// <summary>
        /// Updates the visual indicator color and emission based on currentState.
        /// </summary>
        public void UpdateVisualFeedback()
        {
            Color targetColor = emptyColor;
            switch (currentState)
            {
                case SlotState.Hovered:
                    targetColor = hoverColor;
                    break;
                case SlotState.Correct:
                    targetColor = correctColor;
                    break;
                case SlotState.Incorrect:
                    targetColor = incorrectColor;
                    break;
                case SlotState.Empty:
                default:
                    targetColor = emptyColor;
                    break;
            }

            // Update socket indicator pad with faint, non-blinding emission
            if (indicatorRenderer != null)
            {
                if (_indicatorMaterial == null) _indicatorMaterial = indicatorRenderer.material;
                if (_indicatorMaterial != null)
                {
                    float emissionMult = (currentState == SlotState.Correct) ? 0.8f : (currentState == SlotState.Empty ? 0.2f : 0.6f);
                    ApplyColorToMaterial(_indicatorMaterial, targetColor, emissionMult);
                }
            }

            // Update full circular orbit line with faint opacity
            if (orbitLineRenderer != null)
            {
                if (_orbitLineMaterial == null) _orbitLineMaterial = orbitLineRenderer.material;
                if (_orbitLineMaterial != null)
                {
                    Color lineCol = targetColor;
                    float emissionMult = (currentState == SlotState.Correct) ? 0.7f : 0.2f;
                    ApplyColorToMaterial(_orbitLineMaterial, lineCol, emissionMult);
                }
            }
        }

        private void ApplyColorToMaterial(Material mat, Color col, float emissionMult)
        {
            if (mat.HasProperty(BaseColorId))
            {
                mat.SetColor(BaseColorId, col);
            }
            else if (mat.HasProperty(ColorId))
            {
                mat.SetColor(ColorId, col);
            }

            if (mat.HasProperty(EmissionColorId))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, col * emissionMult);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 pos = snapPoint != null ? snapPoint.position : transform.position;
            Gizmos.color = currentState == SlotState.Correct ? Color.green : (currentState == SlotState.Incorrect ? Color.red : Color.cyan);
            Gizmos.DrawWireSphere(pos, snapRadius);
        }
    }
}
