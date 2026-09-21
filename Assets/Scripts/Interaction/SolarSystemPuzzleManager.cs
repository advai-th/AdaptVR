using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AdaptiveVR.Environment;

namespace AdaptiveVR.PlanetPuzzle
{
    /// <summary>
    /// Clean, standalone manager for the Solar System Planet Ordering Puzzle.
    /// Completely independent of room or observatory generators.
    /// Supports Sequential Orbit Progression (Mercury -> Venus -> Earth...)
    /// and generates faint, realistic 360-degree celestial orbit tracks with configurable opacity.
    /// </summary>
    public class SolarSystemPuzzleManager : MonoBehaviour
    {
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
            FindReferencesIfEmpty();
            SubscribeToSlotEvents();
            EvaluatePuzzleState();
            UpdateSequentialProgression();
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
                if (isCorrect)
                {
                    onPlanetPlacedCorrectly?.Invoke();
                }
                else
                {
                    onPlanetPlacedIncorrectly?.Invoke();
                }
            }

            EvaluatePuzzleState();
            UpdateSequentialProgression();
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
            }
        }

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

            var allObjects = FindObjectsOfType<Transform>();
            foreach (var t in allObjects)
            {
                string lowerName = t.name.ToLower();

                foreach (PlanetPreset p in PlanetOrder)
                {
                    string pName = p.ToString().ToLower();

                    // Check if name matches (excluding slots, indicators, and UI)
                    if (lowerName.Contains(pName) &&
                        !lowerName.Contains("slot") &&
                        !lowerName.Contains("orbit") &&
                        !lowerName.Contains("socket") &&
                        !lowerName.Contains("ring") &&
                        !lowerName.Contains("track") &&
                        !lowerName.Contains("label") &&
                        !lowerName.Contains("panel"))
                    {
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
                                t.gameObject.AddComponent(xrType);
                            }
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
