using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AdaptiveVR.Environment;
using AdaptiveVR.Networking;
using AdaptiveVR.DataCollection.Core;

namespace AdaptiveVR.PlanetPuzzle
{
    public enum PlanetLabelMode
    {
        TargetPlanetOnly,
        AllUnplacedPlanets,
        AllPlanets
    }

    /// <summary>
    /// Clean, standalone manager for the Solar System Planet Ordering Puzzle.
    /// Completely independent of room or observatory generators.
    /// Supports Sequential Orbit Progression (Mercury -> Venus -> Earth...)
    /// and generates faint, realistic 360-degree celestial orbit tracks with configurable opacity.
    /// </summary>
    public class SolarSystemPuzzleManager : MonoBehaviour
    {
        public static SolarSystemPuzzleManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        [Header("Scene References")]
        [Tooltip("Center reference point (e.g., Sun object or Table center) around which orbits are positioned.")]
        public Transform centerReference;

        [Tooltip("The 8 orbital target slots (Order 1: Mercury to 8: Neptune).")]
        public List<PlanetSlot> orbitSlots = new List<PlanetSlot>();

        [Tooltip("The grabbable planet objects in the scene.")]
        public List<GrabbablePlanet> planets = new List<GrabbablePlanet>();

        [Header("Progression Mode")]
        [Tooltip("When enabled, orbits appear sequentially: Orbit 1 (Mercury) appears first, and each next orbit only unlocks once the previous planet is placed correctly!")]
        public bool sequentialProgression = true;

        [Tooltip("Index of the currently active/unlocked orbit (1 = Mercury, 2 = Venus, etc.). Read-only in play mode.")]
        public int currentUnlockedOrbit = 1;

        [Header("Orbit Visual Appearance (Faint & Low Opacity)")]
        [Tooltip("Opacity of the circular orbit rings. Lower values make faint, ethereal rings.")]
        [Range(0.02f, 0.8f)]
        public float orbitRingOpacity = 0.14f;

        [Tooltip("Glow emission intensity of the faint orbit rings.")]
        [Range(0.0f, 1.5f)]
        public float orbitRingEmission = 0.25f;

        [Tooltip("Cosmic color tint for empty orbit tracks.")]
        public Color orbitRingColor = new Color(0.2f, 0.65f, 0.95f, 0.14f);

        [Header("Realistic Solar System Orbit Settings")]
        [Tooltip("Overall scale multiplier for all orbits to comfortably fit your table or room size.")]
        [Range(0.2f, 3.0f)]
        public float scaleMultiplier = 1.0f;

        [Tooltip("Progressive realistic spacing from Sun in meters: [Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune].")]
        public float[] orbitRadii = new float[]
        {
            0.38f, // 1: Mercury (inner terrestrial)
            0.52f, // 2: Venus
            0.68f, // 3: Earth
            0.86f, // 4: Mars
            1.18f, // 5: Jupiter (larger gap after asteroid belt)
            1.50f, // 6: Saturn
            1.84f, // 7: Uranus
            2.18f  // 8: Neptune
        };

        [Tooltip("Generate visible 360-degree glowing orbit lines around the Sun.")]
        public bool generateOrbitLines = true;

        [Tooltip("Thickness of the orbit line tracks.")]
        [Range(0.002f, 0.03f)]
        public float orbitLineWidth = 0.005f;

        [Tooltip("Height offset of the orbits and sockets relative to the Sun/center.")]
        public float socketsHeightOffset = 0.0f;

        [Tooltip("Arc spread angle in degrees for distributing the 8 sockets towards the player for easy VR reach.")]
        [Range(0f, 360f)]
        public float socketArcSpread = 70f;

        [Tooltip("Starting angle offset in degrees for the socket arc.")]
        [Range(-180f, 180f)]
        public float socketStartAngle = -35f;

        [Header("Puzzle Progress (Read-Only)")]
        [Tooltip("Number of planets currently placed in their correct orbital slot.")]
        public int correctPlacementsCount = 0;

        [Tooltip("Is the entire puzzle correctly completed (all 8 planets placed correctly)?")]
        public bool isPuzzleComplete = false;

        [Tooltip("The planet that was most recently placed into a slot.")]
        public GrabbablePlanet lastPlacedPlanet;

        [Tooltip("The slot that most recently received a planet.")]
        public PlanetSlot lastPlacedSlot;

        [Header("Cognitive Scaffolding (Visual High Load / Struggle)")]
        [Tooltip("When active (e.g. on High Cognitive Load), incorrect distractor planets are greyed/dimmed, and only the target planet remains bright and illuminated.")]
        public bool distractorDimmingActive = false;

        [Tooltip("When active (e.g. on High Cognitive Load), light floating name labels appear on top of planets.")]
        public bool planetLabelsActive = false;

        [Tooltip("Display mode for floating planet name labels.")]
        public PlanetLabelMode currentPlanetLabelMode = PlanetLabelMode.AllUnplacedPlanets;

        [Header("Autonomous Scaffolding Ladder")]
        [Tooltip("Consecutive errors on the current orbit before triggering local distractor dimming.")]
        public int errorThresholdForDistractorDimming = 2;

        [Tooltip("Consecutive incorrect placement attempts on the active orbit.")]
        public int consecutiveErrorsOnCurrentOrbit = 0;

        [Tooltip("Manual instructor override for distractor dimming.")]
        public bool teacherOverrideDistractorDimming = false;

        [Tooltip("Manual instructor override for planet labels.")]
        public bool teacherOverrideLabels = false;

        [Header("Puzzle Events")]
        public UnityEvent onPuzzleCompleted;
        public UnityEvent onPlanetPlacedCorrectly;
        public UnityEvent onPlanetPlacedIncorrectly;
        public UnityEvent onNextOrbitUnlocked;

        private static readonly PlanetPreset[] PlanetOrder = new PlanetPreset[]
        {
            PlanetPreset.Mercury,
            PlanetPreset.Venus,
            PlanetPreset.Earth,
            PlanetPreset.Mars,
            PlanetPreset.Jupiter,
            PlanetPreset.Saturn,
            PlanetPreset.Uranus,
            PlanetPreset.Neptune
        };

        private void Start()
        {
            if (SessionManager.HasInstance && SessionManager.Instance != null && !SessionManager.Instance.isSessionActive)
            {
                SessionManager.Instance.StartSession();
                SessionManager.Instance.StartTask("SOLAR_SYSTEM_PUZZLE", "Solar System Planetary Placement");
            }

            FindReferencesIfEmpty();
            SubscribeToSlotEvents();
            EvaluatePuzzleState();
            UpdateSequentialProgression();
            UpdatePlanetScaffoldingVisuals();
            UpdatePlanetLabelsVisuals();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSlotEvents();
        }

        private void SubscribeToSlotEvents()
        {
            foreach (var slot in orbitSlots)
            {
                if (slot != null)
                {
                    slot.OnPlanetStateChanged += HandleSlotStateChanged;
                }
            }
        }

        private void UnsubscribeFromSlotEvents()
        {
            foreach (var slot in orbitSlots)
            {
                if (slot != null)
                {
                    slot.OnPlanetStateChanged -= HandleSlotStateChanged;
                }
            }
        }

        private void HandleSlotStateChanged(PlanetSlot slot, GrabbablePlanet planet, bool isCorrect)
        {
            if (planet != null)
            {
                lastPlacedPlanet = planet;
                lastPlacedSlot = slot;

                if (isCorrect)
                {
                    consecutiveErrorsOnCurrentOrbit = 0;

                    // Release distractor dimming if it was triggered automatically by errors
                    if (!teacherOverrideDistractorDimming && distractorDimmingActive)
                    {
                        SetDistractorDimming(false);
                    }

                    onPlanetPlacedCorrectly?.Invoke();
                }
                else
                {
                    consecutiveErrorsOnCurrentOrbit++;
                    Debug.Log($"<color=#FF8800>[SolarSystemPuzzleManager] Incorrect placement. Consecutive errors on Orbit {currentUnlockedOrbit}: {consecutiveErrorsOnCurrentOrbit}/{errorThresholdForDistractorDimming}</color>");

                    // Graduated ladder: reaching error threshold automatically engages distractor dimming and labels
                    if (consecutiveErrorsOnCurrentOrbit >= errorThresholdForDistractorDimming)
                    {
                        SetDistractorDimming(true);
                        SetPlanetLabelsActive(true);
                    }

                    onPlanetPlacedIncorrectly?.Invoke();
                }
            }

            EvaluatePuzzleState();
            UpdateSequentialProgression();
            UpdatePlanetScaffoldingVisuals();
            UpdatePlanetLabelsVisuals();
        }

        /// <summary>
        /// Evaluates current puzzle progress across all active slots.
        /// </summary>
        public void EvaluatePuzzleState()
        {
            int correct = 0;
            int total = orbitSlots.Count > 0 ? orbitSlots.Count : 8;

            foreach (var slot in orbitSlots)
            {
                if (slot != null && slot.currentState == SlotState.Correct)
                {
                    correct++;
                }
            }

            correctPlacementsCount = correct;
            bool wasComplete = isPuzzleComplete;
            isPuzzleComplete = (correct >= total && total > 0);

            if (isPuzzleComplete && !wasComplete)
            {
                Debug.Log("<color=#00FF66><b>[SolarSystemPuzzleManager]</b> All 8 planets placed in correct order! Puzzle Complete!</color>");
                onPuzzleCompleted?.Invoke();

                if (AdaptVRBackendClient.Instance != null)
                {
                    AdaptVRBackendClient.Instance.SendGameplayEvent(
                        eventType: "puzzle_completed",
                        pieceId: "SolarSystem",
                        orbitIndex: 8,
                        isCorrect: true,
                        action: "puzzle_completed",
                        extraNotes: "All 8 planets placed in correct order! Puzzle Complete!"
                    );

                    string sId = AdaptVRBackendClient.Instance.GetActiveSessionId();
                    if (!string.IsNullOrEmpty(sId))
                    {
                        AdaptVRBackendClient.Instance.EndSessionOnBackend(sId, 100, "completed");
                    }
                }

                if (SessionManager.HasInstance && SessionManager.Instance != null)
                {
                    SessionManager.Instance.CompleteTask("SOLAR_SYSTEM_PUZZLE", 100);
                }

                StartCoroutine(ConcludeSessionAfterCelebrationCoroutine());
            }
        }

        private IEnumerator ConcludeSessionAfterCelebrationCoroutine()
        {
            // Allow 8 seconds for the player to enjoy the companion congratulatory celebration and full solar system
            yield return new WaitForSeconds(8.0f);

            if (SessionManager.HasInstance && SessionManager.Instance != null && SessionManager.Instance.isSessionActive)
            {
                SessionManager.Instance.EndSession();
            }
        }

        /// <summary>
        /// Controls sequential reveal of orbits:
        /// Orbit 1 (Mercury) is visible first. Orbit 2 (Venus) only unlocks when Mercury is correctly settled!
        /// </summary>
        public void UpdateSequentialProgression()
        {
            if (orbitSlots == null || orbitSlots.Count == 0) return;

            if (!sequentialProgression)
            {
                // All slots visible
                currentUnlockedOrbit = orbitSlots.Count;
                foreach (var slot in orbitSlots)
                {
                    if (slot != null) slot.SetSlotActive(true);
                }
                UpdatePlanetScaffoldingVisuals();
                return;
            }

            // Find how many consecutive orbits are completed starting from Orbit 1 (Mercury)
            int highestConsecutiveSolved = 0;
            for (int i = 0; i < orbitSlots.Count; i++)
            {
                var slot = orbitSlots[i];
                if (slot != null && slot.currentState == SlotState.Correct)
                {
                    highestConsecutiveSolved = i + 1;
                }
                else
                {
                    break; // Stop at first unsolved slot
                }
            }

            int prevUnlocked = currentUnlockedOrbit;
            // The active/unlocked target is all solved ones, plus the next unsolved one
            currentUnlockedOrbit = Mathf.Clamp(highestConsecutiveSolved + 1, 1, orbitSlots.Count);

            for (int i = 0; i < orbitSlots.Count; i++)
            {
                var slot = orbitSlots[i];
                if (slot == null) continue;

                int slotIndex = i + 1;
                bool shouldBeVisible = (slotIndex <= currentUnlockedOrbit);
                slot.SetSlotActive(shouldBeVisible);
            }

            if (currentUnlockedOrbit > prevUnlocked && currentUnlockedOrbit <= orbitSlots.Count)
            {
                Debug.Log($"<color=#00E5FF><b>[SolarSystemPuzzleManager]</b> Next Orbit Unlocked: {PlanetOrder[currentUnlockedOrbit - 1]}!</color>");
                onNextOrbitUnlocked?.Invoke();

                if (AdaptVRBackendClient.Instance != null)
                {
                    AdaptVRBackendClient.Instance.SendGameplayEvent(
                        eventType: "gameplay",
                        pieceId: PlanetOrder[currentUnlockedOrbit - 1].ToString(),
                        orbitIndex: currentUnlockedOrbit,
                        isCorrect: true,
                        action: "orbit_unlocked",
                        extraNotes: $"Orbit {currentUnlockedOrbit} ({PlanetOrder[currentUnlockedOrbit - 1]}) unlocked"
                    );
                }

                if (SessionManager.HasInstance && SessionManager.Instance != null)
                {
                    SessionManager.Instance.RecordLearningEvent("orbit_unlocked", PlanetOrder[currentUnlockedOrbit - 1].ToString(), $"Orbit {currentUnlockedOrbit}");
                }
            }

            UpdatePlanetScaffoldingVisuals();
            UpdatePlanetLabelsVisuals();
        }

        #region Cognitive Scaffolding (Planet Distractor Dimming & Highlighting)

        /// <summary>
        /// Activates or deactivates distractor dimming visual scaffolding.
        /// When active, unplaced incorrect planets are dimmed to grey and the current target planet is illuminated.
        /// </summary>
        public void SetDistractorDimming(bool active)
        {
            distractorDimmingActive = active;
            UpdatePlanetScaffoldingVisuals();
        }

        /// <summary>
        /// Explicit teacher/dashboard override for distractor dimming.
        /// </summary>
        public void TriggerTeacherDistractorDimming(bool enable)
        {
            teacherOverrideDistractorDimming = enable;
            SetDistractorDimming(enable);
        }

        /// <summary>
        /// Explicit teacher/dashboard override for planet labels.
        /// </summary>
        public void TriggerTeacherLabels(bool enable)
        {
            teacherOverrideLabels = enable;
            SetPlanetLabelsActive(enable);
        }

        /// <summary>
        /// Updates the visual scaffolding state on all planets.
        /// If distractor dimming is inactive or puzzle is complete, resets all planets to normal visuals.
        /// If active, the current target planet is highlighted, while other unplaced planets are dimmed/greyed.
        /// Already correctly placed planets in orbits remain in their normal pristine state.
        /// </summary>
        public void UpdatePlanetScaffoldingVisuals()
        {
            if (planets == null || planets.Count == 0)
            {
                FindReferencesIfEmpty();
            }
            if (planets == null) return;

            if (!distractorDimmingActive || isPuzzleComplete)
            {
                foreach (var p in planets)
                {
                    if (p != null) p.ResetScaffoldingVisuals();
                }
                return;
            }

            // Determine target preset for the current unlocked orbit
            PlanetPreset targetPreset;
            if (sequentialProgression)
            {
                int targetIndex = Mathf.Clamp(currentUnlockedOrbit - 1, 0, PlanetOrder.Length - 1);
                targetPreset = PlanetOrder[targetIndex];
            }
            else
            {
                // Find first slot that is not yet correctly placed
                PlanetSlot nextSlot = orbitSlots.Find(s => s != null && s.currentState != SlotState.Correct);
                targetPreset = nextSlot != null ? nextSlot.expectedPreset : PlanetPreset.Mercury;
            }

            foreach (var p in planets)
            {
                if (p == null) continue;

                // If the planet is already correctly placed in its orbital socket, keep it in pristine normal visuals
                if (p.currentSlot != null && p.currentSlot.currentState == SlotState.Correct)
                {
                    p.ResetScaffoldingVisuals();
                    continue;
                }

                // If this is the current target planet, highlight it with luminous celestial glow
                if (p.preset == targetPreset)
                {
                    p.SetTargetHighlighted(true);
                }
                else
                {
                    // Grey out distractor planet
                    p.SetDistractorDimmed(true);
                }
            }
        }

        #endregion

        #region Cognitive Scaffolding (Planet Name Labels)

        /// <summary>
        /// Activates or deactivates floating planet name labels based on cognitive load state.
        /// </summary>
        public void SetPlanetLabelsActive(bool active, PlanetLabelMode mode = PlanetLabelMode.AllUnplacedPlanets)
        {
            planetLabelsActive = active;
            currentPlanetLabelMode = mode;
            UpdatePlanetLabelsVisuals();
        }

        /// <summary>
        /// Updates the floating name label visibility for each planet.
        /// When active, subtle floating name tags appear on top of planets.
        /// Placed planets or complete puzzles hide labels to keep orbits clean.
        /// </summary>
        public void UpdatePlanetLabelsVisuals()
        {
            if (planets == null || planets.Count == 0)
            {
                FindReferencesIfEmpty();
            }
            if (planets == null) return;

            // Determine target preset for the current unlocked orbit
            PlanetPreset targetPreset;
            if (sequentialProgression)
            {
                int targetIndex = Mathf.Clamp(currentUnlockedOrbit - 1, 0, PlanetOrder.Length - 1);
                targetPreset = PlanetOrder[targetIndex];
            }
            else
            {
                PlanetSlot nextSlot = orbitSlots.Find(s => s != null && s.currentState != SlotState.Correct);
                targetPreset = nextSlot != null ? nextSlot.expectedPreset : PlanetPreset.Mercury;
            }

            foreach (var p in planets)
            {
                if (p == null) continue;

                // If puzzle is complete or labels inactive:
                if (!planetLabelsActive || isPuzzleComplete)
                {
                    p.SetAdaptiveLabelVisible(false);
                    continue;
                }

                // If planet is already correctly placed in its orbital socket, hide label
                if (p.currentSlot != null && p.currentSlot.currentState == SlotState.Correct)
                {
                    p.SetAdaptiveLabelVisible(false);
                    continue;
                }

                bool isTarget = (p.preset == targetPreset);

                switch (currentPlanetLabelMode)
                {
                    case PlanetLabelMode.TargetPlanetOnly:
                        p.SetAdaptiveLabelVisible(isTarget);
                        if (isTarget) p.SetLabelHighlight(true);
                        break;

                    case PlanetLabelMode.AllUnplacedPlanets:
                    case PlanetLabelMode.AllPlanets:
                        p.SetAdaptiveLabelVisible(true);
                        p.SetLabelHighlight(isTarget);
                        break;
                }
            }
        }

        #endregion

        #region Automated Scene Setup Helpers (Editor & Runtime)

        /// <summary>
        /// Auto-locates existing PlanetSlots and GrabbablePlanets in the scene if lists are empty.
        /// </summary>
        [ContextMenu("Find References In Scene")]
        public void FindReferencesIfEmpty()
        {
            if (centerReference == null)
            {
                GameObject sunObj = GameObject.Find("Sun") ?? GameObject.Find("sun");
                if (sunObj != null) centerReference = sunObj.transform;
                else centerReference = transform;
            }

            if (orbitSlots == null || orbitSlots.Count == 0)
            {
#if UNITY_2023_1_OR_NEWER
                orbitSlots = new List<PlanetSlot>(FindObjectsByType<PlanetSlot>(FindObjectsSortMode.None));
#else
                orbitSlots = new List<PlanetSlot>(FindObjectsOfType<PlanetSlot>());
#endif
                orbitSlots.Sort((a, b) => a.orbitIndex.CompareTo(b.orbitIndex));
            }

            if (planets == null || planets.Count == 0)
            {
#if UNITY_2023_1_OR_NEWER
                planets = new List<GrabbablePlanet>(FindObjectsByType<GrabbablePlanet>(FindObjectsSortMode.None));
#else
                planets = new List<GrabbablePlanet>(FindObjectsOfType<GrabbablePlanet>());
#endif
            }
        }

        /// <summary>
        /// Searches all GameObjects in the scene for planetary names and attaches GrabbablePlanet to your custom prefabs.
        /// </summary>
        [ContextMenu("Auto-Detect Scene Planets")]
        public void AutoDetectScenePlanets()
        {
            planets.Clear();

#if UNITY_2023_1_OR_NEWER
            var allObjects = FindObjectsByType<Transform>(FindObjectsSortMode.None);
#else
            var allObjects = FindObjectsOfType<Transform>();
#endif
            foreach (var t in allObjects)
            {
                string lowerName = t.name.ToLower();

                // Skip child LOD meshes, light objects, sockets, indicators, UI
                if (lowerName.Contains("lod") ||
                    lowerName.Contains("light") ||
                    lowerName.Contains("glow") ||
                    lowerName.Contains("slot") ||
                    lowerName.Contains("orbit") ||
                    lowerName.Contains("socket") ||
                    lowerName.Contains("ring") ||
                    lowerName.Contains("track") ||
                    lowerName.Contains("label") ||
                    lowerName.Contains("panel"))
                {
                    continue;
                }

                foreach (PlanetPreset p in PlanetOrder)
                {
                    string pName = p.ToString().ToLower();

                    // Check if name matches (excluding slots, indicators, and UI)
                    if (lowerName.Contains(pName))
                    {
                        // Guard: if this transform has a parent that also contains the planet name, it's a child mesh!
                        if (t.parent != null && t.parent.name.ToLower().Contains(pName))
                        {
                            var childGp = t.GetComponent<GrabbablePlanet>();
                            if (childGp != null) DestroyImmediate(childGp);
                            var childRb = t.GetComponent<Rigidbody>();
                            if (childRb != null) DestroyImmediate(childRb);
                            var childCol = t.GetComponent<SphereCollider>();
                            if (childCol != null) DestroyImmediate(childCol);
                            continue;
                        }

                        // Also clean up any accidental components on children of this root
                        foreach (Transform child in t)
                        {
                            var cGps = child.GetComponentsInChildren<GrabbablePlanet>(true);
                            foreach (var c in cGps) DestroyImmediate(c);
                            var cRbs = child.GetComponentsInChildren<Rigidbody>(true);
                            foreach (var c in cRbs) DestroyImmediate(c);
                            var cCols = child.GetComponentsInChildren<SphereCollider>(true);
                            foreach (var c in cCols) DestroyImmediate(c);
                        }

                        GrabbablePlanet grabbable = t.GetComponent<GrabbablePlanet>();
                        if (grabbable == null)
                        {
                            grabbable = t.gameObject.AddComponent<GrabbablePlanet>();
                        }

                        grabbable.preset = p;
                        grabbable.planetName = p.ToString();
                        grabbable.ConfigurePhysics();

                        // Auto-add XRGrabInteractable if XR Interaction Toolkit is present in project
                        Component xrGrab = t.GetComponent("XRGrabInteractable");
                        if (xrGrab == null)
                        {
                            System.Type xrType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit")
                                              ?? System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
                            if (xrType != null)
                            {
                                xrGrab = t.gameObject.AddComponent(xrType);
                            }
                        }

                        if (xrGrab != null)
                        {
                            try
                            {
                                var moveProp = xrGrab.GetType().GetProperty("movementType");
                                if (moveProp != null && moveProp.CanWrite)
                                {
                                    var instantaneousVal = System.Enum.Parse(moveProp.PropertyType, "Instantaneous");
                                    moveProp.SetValue(xrGrab, instantaneousVal, null);
                                }
                                var throwProp = xrGrab.GetType().GetProperty("throwOnDetach");
                                if (throwProp != null && throwProp.CanWrite)
                                {
                                    throwProp.SetValue(xrGrab, false, null);
                                }
                                var snapProp = xrGrab.GetType().GetProperty("snapToColliderVolume");
                                if (snapProp != null && snapProp.CanWrite)
                                {
                                    snapProp.SetValue(xrGrab, false, null);
                                }
                                var dynAttachProp = xrGrab.GetType().GetProperty("useDynamicAttach");
                                if (dynAttachProp != null && dynAttachProp.CanWrite)
                                {
                                    dynAttachProp.SetValue(xrGrab, true, null);
                                }
                            }
                            catch { }
                        }

                        if (!planets.Contains(grabbable))
                        {
                            planets.Add(grabbable);
                        }
                        break;
                    }
                }
            }

            Debug.Log($"[SolarSystemPuzzleManager] Auto-detected {planets.Count} planets in scene.");
        }

        /// <summary>
        /// Generates 8 realistic concentric orbit lines and target sockets with progressive spacing around the Sun.
        /// Configured with low-opacity, faint celestial materials.
        /// </summary>
        [ContextMenu("Create 8 Orbit Sockets Around Sun")]
        public void CreateOrbitSocketsAroundSun()
        {
            FindReferencesIfEmpty();

            // Find or create container for orbits & sockets
            Transform container = transform.Find("OrbitSockets");
            if (container == null)
            {
                GameObject containerObj = new GameObject("OrbitSockets");
                containerObj.transform.SetParent(transform);
                containerObj.transform.localPosition = Vector3.zero;
                containerObj.transform.localRotation = Quaternion.identity;
                container = containerObj.transform;
            }

            // Remove existing generated children in container
            while (container.childCount > 0)
            {
                DestroyImmediate(container.GetChild(0).gameObject);
            }

            orbitSlots.Clear();

            Vector3 centerPos = centerReference != null ? centerReference.position : transform.position;
            centerPos.y += socketsHeightOffset;

            // Shared faint materials
            Material socketMat = CreateSocketIndicatorMaterial();
            Material lineMat = CreateOrbitLineMaterial();

            // Fallback radii if array is improperly sized
            float[] defaultRadii = new float[] { 0.38f, 0.52f, 0.68f, 0.86f, 1.18f, 1.50f, 1.84f, 2.18f };
            if (orbitRadii == null || orbitRadii.Length < 8)
            {
                orbitRadii = defaultRadii;
            }

            for (int i = 0; i < PlanetOrder.Length; i++)
            {
                PlanetPreset preset = PlanetOrder[i];
                int orbitIndex = i + 1;
                float radius = orbitRadii[i] * scaleMultiplier;

                // 1. Create Orbit Root Object
                GameObject orbitRootObj = new GameObject($"Orbit_{orbitIndex}_{preset}");
                orbitRootObj.transform.SetParent(container);
                orbitRootObj.transform.position = centerPos;

                LineRenderer orbitLineRenderer = null;

                // 2. Build 360-degree Continuous Faint Orbit Line Track
                if (generateOrbitLines)
                {
                    GameObject trackObj = new GameObject("OrbitTrackLine");
                    trackObj.transform.SetParent(orbitRootObj.transform);
                    trackObj.transform.localPosition = Vector3.zero;

                    orbitLineRenderer = trackObj.AddComponent<LineRenderer>();
                    orbitLineRenderer.useWorldSpace = false;
                    orbitLineRenderer.loop = true;
                    orbitLineRenderer.positionCount = 96;
                    orbitLineRenderer.startWidth = orbitLineWidth;
                    orbitLineRenderer.endWidth = orbitLineWidth;
                    orbitLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    orbitLineRenderer.receiveShadows = false;
                    orbitLineRenderer.material = new Material(lineMat);

                    for (int s = 0; s < 96; s++)
                    {
                        float rad = (s / 96f) * Mathf.PI * 2f;
                        Vector3 pt = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
                        orbitLineRenderer.SetPosition(s, pt);
                    }
                }

                // 3. Calculate Socket Position along the orbit arc facing the player
                float stepFraction = PlanetOrder.Length > 1 ? (float)i / (PlanetOrder.Length - 1) : 0f;
                float angleDeg = socketStartAngle + (stepFraction * socketArcSpread);
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 socketLocalPos = new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);

                GameObject socketObj = new GameObject($"SlotMarker_{preset}");
                socketObj.transform.SetParent(orbitRootObj.transform);
                socketObj.transform.localPosition = socketLocalPos;

                // 4. Visual Indicator Pad / Disc
                GameObject indicatorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                indicatorObj.name = "SocketIndicatorPad";
                indicatorObj.transform.SetParent(socketObj.transform);
                indicatorObj.transform.localPosition = Vector3.zero;

                float padDiameter = Mathf.Clamp(0.18f + (i * 0.02f), 0.18f, 0.36f);
                indicatorObj.transform.localScale = new Vector3(padDiameter, 0.003f, padDiameter);

                Collider primCollider = indicatorObj.GetComponent<Collider>();
                if (primCollider != null) DestroyImmediate(primCollider);

                Renderer indRenderer = indicatorObj.GetComponent<Renderer>();
                if (indRenderer != null)
                {
                    indRenderer.material = new Material(socketMat);
                }

                // 5. Snap Point Transform (slightly elevated above pad)
                GameObject snapPointObj = new GameObject("SnapPoint");
                snapPointObj.transform.SetParent(socketObj.transform);
                snapPointObj.transform.localPosition = new Vector3(0f, 0.08f, 0f);

                // 6. Attach PlanetSlot Component
                PlanetSlot slot = socketObj.AddComponent<PlanetSlot>();
                slot.expectedPreset = preset;
                slot.orbitIndex = orbitIndex;
                slot.orbitRadius = radius;
                slot.snapRadius = Mathf.Clamp(padDiameter * 1.2f, 0.25f, 0.5f);
                slot.snapPoint = snapPointObj.transform;
                slot.indicatorRenderer = indRenderer;
                slot.orbitLineRenderer = orbitLineRenderer;
                slot.orbitRootObject = orbitRootObj; // Set root object for sequential toggle

                // Apply faint colors
                Color faintSlotColor = orbitRingColor;
                faintSlotColor.a = orbitRingOpacity + 0.05f;
                slot.emptyColor = faintSlotColor;
                slot.UpdateVisualFeedback();

                orbitSlots.Add(slot);
            }

            // Apply sequential visibility immediately
            UpdateSequentialProgression();

            Debug.Log("<color=#00FF66>[SolarSystemPuzzleManager] Generated 8 Realistic Faint Orbit Rings & Sockets successfully.</color>");
        }

        private Material CreateSocketIndicatorMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = "Mat_PlanetSlotIndicator";

            // Configure Transparent blend
            ConfigureTransparentMaterial(mat);

            Color padColor = orbitRingColor;
            padColor.a = Mathf.Clamp01(orbitRingOpacity + 0.1f);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", padColor);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", padColor);

            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", padColor * orbitRingEmission);

            return mat;
        }

        private Material CreateOrbitLineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = "Mat_OrbitTrackLine";

            // Configure Transparent blend
            ConfigureTransparentMaterial(mat);

            Color lineColor = orbitRingColor;
            lineColor.a = Mathf.Clamp01(orbitRingOpacity);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", lineColor);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", lineColor);

            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", lineColor * orbitRingEmission);

            return mat;
        }

        private void ConfigureTransparentMaterial(Material mat)
        {
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1.0f); // Transparent
                mat.SetFloat("_Blend", 0.0f);   // Alpha blend
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>
        /// Snaps each planet directly to its correct slot (useful for testing victory condition).
        /// </summary>
        [ContextMenu("Auto-Solve Puzzle")]
        public void AutoSolve()
        {
            FindReferencesIfEmpty();

            foreach (var slot in orbitSlots)
            {
                if (slot == null) continue;

                slot.SetSlotActive(true);
                GrabbablePlanet matchingPlanet = planets.Find(p => p != null && p.preset == slot.expectedPreset);
                if (matchingPlanet != null)
                {
                    matchingPlanet.transform.position = slot.snapPoint != null ? slot.snapPoint.position : slot.transform.position;
                    matchingPlanet.transform.rotation = slot.snapPoint != null ? slot.snapPoint.rotation : slot.transform.rotation;
                    slot.TrySnapPlanet(matchingPlanet);
                }
            }

            EvaluatePuzzleState();
            UpdateSequentialProgression();
        }

        /// <summary>
        /// Moves planets to scattered starter positions (useful for starting the puzzle over).
        /// </summary>
        [ContextMenu("Scramble Planets")]
        public void ScramblePlanets()
        {
            FindReferencesIfEmpty();

            foreach (var slot in orbitSlots)
            {
                if (slot != null) slot.ClearSlot();
            }

            Vector3 centerPos = centerReference != null ? centerReference.position : transform.position;

            for (int i = 0; i < planets.Count; i++)
            {
                var planet = planets[i];
                if (planet == null) continue;

                float angle = (i * 45f) * Mathf.Deg2Rad;
                float dist = (orbitRadii.Length > 0 ? orbitRadii[0] : 0.4f) * scaleMultiplier + 0.25f;
                planet.transform.position = centerPos + new Vector3(Mathf.Cos(angle) * dist, 0.15f, Mathf.Sin(angle) * dist);
            }

            EvaluatePuzzleState();
            UpdateSequentialProgression();
        }

        #endregion
    }
}
