using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdaptiveVR.Environment
{
    /// <summary>
    /// Procedural 3D Environment Generator for the Adaptive Solar System Observatory.
    /// Matched precisely to the high-fidelity stationary educational VR observatory concept design.
    /// Includes automatic asset persistence for URP materials & textures to prevent pink Prefabs.
    /// </summary>
    [ExecuteInEditMode]
    public class ObservatoryGenerator : MonoBehaviour
    {
        [Header("1. Room Geometry")]
        [Tooltip("Radius of the circular room in meters.")]
        [Range(3f, 15f)] public float roomRadius = 5.0f;

        [Tooltip("Height of the room from floor to ceiling in meters.")]
        [Range(2.5f, 6f)] public float roomHeight = 3.2f;

        [Tooltip("Number of wall segments forming the circular wall (32 recommended).")]
        [Range(16, 64)] public int wallSegments = 32;

        [Header("2. Observatory Windows")]
        [Tooltip("Number of large observatory windows distributed around perimeter.")]
        [Range(2, 8)] public int windowCount = 4;

        [Tooltip("Width of each big window in meters.")]
        [Range(1f, 5f)] public float windowWidth = 3.2f;

        [Tooltip("Height of each big window in meters.")]
        [Range(1f, 3f)] public float windowHeight = 1.9f;

        [Tooltip("Elevation of window center from floor in meters.")]
        [Range(1f, 2.5f)] public float windowElevation = 1.85f;

        [Tooltip("Y-Axis rotation offset for window quads in degrees.")]
        public float windowRotationY = 90f;

        [Header("3. Central Interactive Table")]
        [Tooltip("Diameter of the central interactive table in meters.")]
        [Range(1.5f, 4f)] public float tableDiameter = 2.8f;

        [Tooltip("Height of the central table in meters.")]
        [Range(0.6f, 1.2f)] public float tableHeight = 0.85f;

        [Tooltip("Y-Axis rotation offset for front table display panel in degrees.")]
        public float tablePanelRotationY = 90f;

        [Header("4. Display Elements & UI Panels")]
        [Tooltip("Number of concentric decorative floor rings.")]
        [Range(1, 5)] public int floorRingCount = 2;

        [Tooltip("Y-Axis rotation offset for back wall UI display panels in degrees.")]
        public float displayRotationY = 90f;

        [Header("5. Lighting Settings")]
        [Tooltip("Intensity of ambient soft directional light.")]
        [Range(0.1f, 2f)] public float ambientIntensity = 0.5f;

        [Tooltip("Intensity of warm ceiling spotlights.")]
        [Range(0.2f, 4f)] public float spotlightIntensity = 1.8f;

        [Tooltip("Intensity of central Sun glowing point light.")]
        [Range(0.5f, 5f)] public float sunLightIntensity = 3.0f;

        [Header("6. Concept Color Palette")]
        public Color floorColor = new Color(0.85f, 0.88f, 0.92f);         // Off-white / light neutral grey
        public Color floorRingColor = new Color(0.26f, 0.32f, 0.40f);     // Dark slate grey concentric rings
        public Color wallColor = new Color(0.12f, 0.15f, 0.20f);          // Dark blue-grey architectural walls
        public Color ceilingColor = new Color(0.09f, 0.11f, 0.16f);       // Dark navy ceiling
        public Color tableBaseColor = new Color(0.10f, 0.13f, 0.18f);      // Dark blue-grey pedestal
        public Color tableSurfaceColor = new Color(0.04f, 0.08f, 0.14f);   // Dark glass blue
        public Color accentBlueColor = new Color(0.0f, 0.82f, 1.0f);       // Bright cyan blue emission
        public Color sunEmissionColor = new Color(1.0f, 0.55f, 0.05f);     // Warm glowing orange/yellow
        public Color warmSpotColor = new Color(1.0f, 0.92f, 0.78f);        // Soft warm spotlighting

        // Texture and Material Caching
        private Texture2D _starfieldTexture;
        private Texture2D _mainDisplayTexture;
        private Texture2D _factsDisplayTexture;
        private Texture2D _beyondDisplayTexture;
        private Texture2D _tableFrontTexture;
        private Dictionary<string, Material> _materialCache = new Dictionary<string, Material>();

        #region Context Menu & Public Methods

        [ContextMenu("Generate Observatory")]
        public void GenerateObservatory()
        {
            ClearObservatory();
            _materialCache.Clear();

            // Generate Procedural Textures for UI Screens & Starfield
            _starfieldTexture = CreateStarfieldTexture(512, 512);
            _mainDisplayTexture = CreateMainDisplayTexture(512, 256);
            _factsDisplayTexture = CreateFactsDisplayTexture(256, 512);
            _beyondDisplayTexture = CreateBeyondDisplayTexture(256, 512);
            _tableFrontTexture = CreateTableFrontTexture(512, 128);

            // Create Required Hierarchy Root Containers
            Transform rootTransform = this.transform;

            Transform envParent = CreateChildContainer(rootTransform, "Environment");
            Transform floorParent = CreateChildContainer(envParent, "Floor");
            Transform wallsParent = CreateChildContainer(envParent, "CircularWalls");
            Transform windowsParent = CreateChildContainer(envParent, "Windows");
            Transform framesParent = CreateChildContainer(envParent, "WindowFrames");
            Transform panelsParent = CreateChildContainer(envParent, "EducationalPanels");
            Transform shelvesParent = CreateChildContainer(envParent, "PlanetShelves");
            Transform ceilingParent = CreateChildContainer(envParent, "Ceiling");

            Transform exhibitParent = CreateChildContainer(rootTransform, "CentralExhibit");
            Transform tableParent = CreateChildContainer(exhibitParent, "InteractiveTable");
            Transform tableSurfaceParent = CreateChildContainer(exhibitParent, "TableSurface");
            Transform tableRingsParent = CreateChildContainer(exhibitParent, "TableGlowRings");
            Transform solarSystemParent = CreateChildContainer(exhibitParent, "SolarSystemPlaceholder");

            Transform lightingParent = CreateChildContainer(rootTransform, "Lighting");
            Transform ambientParent = CreateChildContainer(lightingParent, "Ambient");
            Transform archLightsParent = CreateChildContainer(lightingParent, "ArchitecturalLights");
            Transform sunLightParent = CreateChildContainer(lightingParent, "SunLight");

            Transform detailsParent = CreateChildContainer(rootTransform, "EnvironmentDetails");
            Transform floorRingsParent = CreateChildContainer(detailsParent, "FloorRings");
            Transform ceilingRingsParent = CreateChildContainer(detailsParent, "CeilingRings");

            // 1. Build Floor & Concentric Floor Rings
            BuildFloor(floorParent, floorRingsParent);

            // 2. Build Ceiling & Planetarium Dome with Ceiling Spotlights
            BuildCeiling(ceilingParent, ceilingRingsParent, archLightsParent);

            // 3. Build Perimeter Walls, 4 Big Windows, Back UI Screen, Display Counters, Shelves, Plants & Door
            BuildWallsAndPerimeter(wallsParent, windowsParent, framesParent, panelsParent, shelvesParent, archLightsParent);

            // 4. Build Central Exhibit (Table with Front Panel & Hologram 8-Planet Solar System)
            BuildCentralExhibit(tableParent, tableSurfaceParent, tableRingsParent, solarSystemParent);

            // 5. Build Unified Lighting Setup
            BuildLighting(ambientParent, sunLightParent);

            Debug.Log("<color=#00FFB3><b>[Adaptive Observatory]</b></color> Environment generated successfully!");
        }

        [ContextMenu("Clear Observatory")]
        public void ClearObservatory()
        {
            List<GameObject> childrenToDestroy = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name == "Environment" ||
                    child.name == "CentralExhibit" ||
                    child.name == "Lighting" ||
                    child.name == "EnvironmentDetails")
                {
                    childrenToDestroy.Add(child.gameObject);
                }
            }

            foreach (GameObject child in childrenToDestroy)
            {
#if UNITY_EDITOR
                DestroyImmediate(child);
#else
                Destroy(child);
#endif
            }

            Debug.Log("<b>[Adaptive Observatory]</b> Cleaned up existing environment.");
        }

        [ContextMenu("Save Manual Edits To Script")]
        public void SaveManualEditsToScript()
        {
            int updatedFields = 0;

            // 1. Read Room Radius from FloorBase scale
            Transform floorBase = transform.Find("Environment/Floor/FloorBase");
            if (floorBase != null)
            {
                roomRadius = floorBase.localScale.x / 2f;
                updatedFields++;
            }

            // 2. Read Room Height from CeilingBase position
            Transform ceilingBase = transform.Find("Environment/Ceiling/CeilingBase");
            if (ceilingBase != null)
            {
                roomHeight = ceilingBase.localPosition.y - 0.05f;
                updatedFields++;
            }

            // 3. Read Central Table dimensions & Front Panel Rotation
            Transform tableBase = transform.Find("CentralExhibit/InteractiveTable/TableBasePedestal");
            if (tableBase != null)
            {
                tableDiameter = tableBase.localScale.x;
                tableHeight = tableBase.localScale.y * 2f;
                updatedFields += 2;
            }

            Transform tableFrontPanel = transform.Find("CentralExhibit/InteractiveTable/TableFrontPanel_SolarSystem");
            if (tableFrontPanel != null)
            {
                tablePanelRotationY = tableFrontPanel.localEulerAngles.y;
                updatedFields++;
            }

            // 4. Read Window dimensions, elevation & Window Rotation
            Transform windowsContainer = transform.Find("Environment/Windows");
            if (windowsContainer != null && windowsContainer.childCount > 0)
            {
                Transform firstWin = windowsContainer.GetChild(0);
                windowWidth = firstWin.localScale.x;
                windowHeight = firstWin.localScale.y;
                windowElevation = firstWin.position.y;
                windowCount = windowsContainer.childCount;
                windowRotationY = firstWin.localEulerAngles.y;
                updatedFields += 5;
            }

            // 5. Read Back Wall Display Screen Rotation
            Transform mainDisplay = transform.Find("Environment/EducationalPanels/MainDisplay_OurSolarSystem");
            if (mainDisplay != null)
            {
                displayRotationY = mainDisplay.localEulerAngles.y;
                updatedFields++;
            }

            // 6. Read Lighting Intensities & Colors
            Transform sunLightObj = transform.Find("Lighting/SunLight/SunPointLight");
            if (sunLightObj != null)
            {
                Light l = sunLightObj.GetComponent<Light>();
                if (l != null)
                {
                    sunLightIntensity = l.intensity;
                    sunEmissionColor = l.color;
                    updatedFields += 2;
                }
            }

            Transform ambLightObj = transform.Find("Lighting/Ambient/AmbientDirectionalLight");
            if (ambLightObj != null)
            {
                Light l = ambLightObj.GetComponent<Light>();
                if (l != null)
                {
                    ambientIntensity = l.intensity;
                    updatedFields++;
                }
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            Debug.Log($"<color=#00FFB3><b>[Adaptive Observatory]</b> Successfully extracted {updatedFields} manual scene edits back into script parameters!</color>");
        }

#if UNITY_EDITOR
        [ContextMenu("Save Environment as Prefab")]
        public void SaveEnvironmentAsPrefab()
        {
            string folderPath = "Assets/Prefabs";
            string materialsPath = "Assets/Prefabs/Materials";
            string texturesPath = "Assets/Prefabs/Textures";

            if (!AssetDatabase.IsValidFolder(folderPath)) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(materialsPath)) AssetDatabase.CreateFolder("Assets/Prefabs", "Materials");
            if (!AssetDatabase.IsValidFolder(texturesPath)) AssetDatabase.CreateFolder("Assets/Prefabs", "Textures");

            // 1. Save procedural textures to disk as PNG assets
            SaveTextureToDisk(_starfieldTexture, texturesPath + "/Tex_Starfield.png");
            SaveTextureToDisk(_mainDisplayTexture, texturesPath + "/Tex_MainDisplay.png");
            SaveTextureToDisk(_factsDisplayTexture, texturesPath + "/Tex_FactsDisplay.png");
            SaveTextureToDisk(_beyondDisplayTexture, texturesPath + "/Tex_BeyondDisplay.png");
            SaveTextureToDisk(_tableFrontTexture, texturesPath + "/Tex_TableFront.png");

            // 2. Save all procedural material instances as persistent .mat asset files on disk
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Dictionary<string, Material> savedMaterials = new Dictionary<string, Material>();

            foreach (Renderer r in renderers)
            {
                if (r.sharedMaterial != null)
                {
                    Material mat = r.sharedMaterial;
                    string matName = string.IsNullOrEmpty(mat.name) ? "Mat_Generated" : mat.name.Replace(" (Instance)", "");
                    string matPath = $"{materialsPath}/{matName}.mat";

                    if (!savedMaterials.ContainsKey(matName))
                    {
                        Material existingAsset = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (existingAsset == null)
                        {
                            Material matToSave = new Material(mat);
                            AssetDatabase.CreateAsset(matToSave, matPath);
                            savedMaterials[matName] = matToSave;
                        }
                        else
                        {
                            savedMaterials[matName] = existingAsset;
                        }
                    }

                    r.sharedMaterial = savedMaterials[matName];
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 3. Save GameObject hierarchy as persistent Prefab asset
            string prefabPath = $"{folderPath}/AdaptiveSolarSystemObservatory.prefab";
            PrefabUtility.SaveAsPrefabAsset(this.gameObject, prefabPath);
            Debug.Log($"<color=#00FFB3><b>[Adaptive Observatory]</b> Saved non-pink URP Prefab to: {prefabPath}</color>");
        }

        private void SaveTextureToDisk(Texture2D tex, string path)
        {
            if (tex == null) return;
            try
            {
                byte[] bytes = tex.EncodeToPNG();
                if (bytes != null)
                {
                    System.IO.File.WriteAllBytes(path, bytes);
                    AssetDatabase.ImportAsset(path);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Adaptive Observatory] Could not save texture to {path}: {ex.Message}");
            }
        }
#endif

        #endregion

        #region Procedural Builders

        private void BuildFloor(Transform floorParent, Transform floorRingsParent)
        {
            // Main Light Off-White/Grey Floor Cylinder
            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floorObj.name = "FloorBase";
            floorObj.transform.SetParent(floorParent, false);
            floorObj.transform.localPosition = new Vector3(0, -0.05f, 0);
            floorObj.transform.localScale = new Vector3(roomRadius * 2f, 0.1f, roomRadius * 2f);

            Material floorMat = GetOrCreateMaterial("Mat_Floor", floorColor, 0.15f, 0.0f);
            floorObj.GetComponent<Renderer>().sharedMaterial = floorMat;
            SetStaticFlags(floorObj);

            // 2 Dark Concentric Decorative Floor Rings
            Material darkRingMat = GetOrCreateMaterial("Mat_FloorRingDark", floorRingColor, 0.3f);

            float[] ringRadii = new float[] { 2.2f, 3.6f };
            for (int i = 0; i < ringRadii.Length; i++)
            {
                float radius = ringRadii[i];
                GameObject ringObj = CreateThinRing("FloorRing_" + (i + 1), radius, 0.08f, darkRingMat);
                ringObj.transform.SetParent(floorRingsParent, false);
                ringObj.transform.localPosition = new Vector3(0, 0.002f, 0);
                SetStaticFlags(ringObj);
            }
        }

        private void BuildCeiling(Transform ceilingParent, Transform ceilingRingsParent, Transform archLightsParent)
        {
            // Main Circular Ceiling Disc
            GameObject ceilingObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ceilingObj.name = "CeilingBase";
            ceilingObj.transform.SetParent(ceilingParent, false);
            ceilingObj.transform.localPosition = new Vector3(0, roomHeight + 0.05f, 0);
            ceilingObj.transform.localScale = new Vector3(roomRadius * 2f, 0.1f, roomRadius * 2f);

            Material ceilingMat = GetOrCreateMaterial("Mat_Ceiling", ceilingColor, 0.1f);
            ceilingObj.GetComponent<Renderer>().sharedMaterial = ceilingMat;
            SetStaticFlags(ceilingObj);

            // Central Planetarium Recessed Dome Opening
            float domeRadius = 2.6f;
            GameObject domeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            domeObj.name = "PlanetariumDomeRecess";
            domeObj.transform.SetParent(ceilingParent, false);
            domeObj.transform.localPosition = new Vector3(0, roomHeight + 0.15f, 0);
            domeObj.transform.localScale = new Vector3(domeRadius * 2f, 0.2f, domeRadius * 2f);

            Material starfieldMat = GetOrCreateTextureMaterial("Mat_Starfield", _starfieldTexture, isUnlit: true);
            domeObj.GetComponent<Renderer>().sharedMaterial = starfieldMat;
            SetStaticFlags(domeObj);

            // Ceiling Cyan LED Glowing Accent Ring around Dome Rim
            Material emissiveBlueMat = GetOrCreateEmissiveMaterial("Mat_EmissiveBlue", accentBlueColor, 1.8f);
            GameObject domeEmissiveRing = CreateThinRing("CeilingDome_CyanLED", domeRadius + 0.02f, 0.04f, emissiveBlueMat);
            domeEmissiveRing.transform.SetParent(ceilingRingsParent, false);
            domeEmissiveRing.transform.localPosition = new Vector3(0, roomHeight - 0.005f, 0);
            SetStaticFlags(domeEmissiveRing);

            // 12 Warm Spotlights Encircling the Ceiling Dome Rim
            int spotCount = 12;
            for (int s = 0; s < spotCount; s++)
            {
                float angle = s * (360f / spotCount);
                Vector3 spotPos = Quaternion.Euler(0, angle, 0) * new Vector3(domeRadius + 0.35f, roomHeight - 0.08f, 0);

                // Fixture mesh
                GameObject spotFixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spotFixture.name = $"CeilingSpotlightFixture_{s}";
                spotFixture.transform.SetParent(ceilingRingsParent, false);
                spotFixture.transform.localPosition = spotPos;
                spotFixture.transform.localScale = new Vector3(0.18f, 0.06f, 0.18f);
                spotFixture.GetComponent<Renderer>().sharedMaterial = GetOrCreateEmissiveMaterial("Mat_SpotFixture", warmSpotColor, 1.5f);
                SetStaticFlags(spotFixture);

                // Warm Downlight Light Source
                GameObject spotLightObj = new GameObject($"CeilingSpotlight_{s}");
                spotLightObj.transform.SetParent(archLightsParent, false);
                spotLightObj.transform.localPosition = spotPos - new Vector3(0, 0.05f, 0);
                spotLightObj.transform.rotation = Quaternion.Euler(90f, 0, 0);

                Light spotLight = spotLightObj.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                spotLight.color = warmSpotColor;
                spotLight.intensity = spotlightIntensity * 0.6f;
                spotLight.spotAngle = 65f;
                spotLight.range = roomHeight * 1.2f;
                spotLight.shadows = LightShadows.None;
            }
        }

        private void BuildWallsAndPerimeter(Transform wallsParent, Transform windowsParent, Transform framesParent, Transform panelsParent, Transform shelvesParent, Transform archLightsParent)
        {
            float angleStep = 360f / wallSegments;
            float segmentWidth = 2f * roomRadius * Mathf.Sin(Mathf.Deg2Rad * (angleStep / 2f));

            Material wallMat = GetOrCreateMaterial("Mat_Wall", wallColor, 0.2f);
            Material frameMat = GetOrCreateMaterial("Mat_WindowFrame", wallColor * 0.8f, 0.4f);
            Material starfieldMat = GetOrCreateTextureMaterial("Mat_Starfield", _starfieldTexture, isUnlit: true);
            Material emissiveBlueMat = GetOrCreateEmissiveMaterial("Mat_EmissiveBlue", accentBlueColor, 1.8f);

            // Materials for UI Display Screens
            Material mainDisplayMat = GetOrCreateTextureMaterial("Mat_MainUIDisplay", _mainDisplayTexture, isUnlit: false);
            Material factsDisplayMat = GetOrCreateTextureMaterial("Mat_FactsUIDisplay", _factsDisplayTexture, isUnlit: false);
            Material beyondDisplayMat = GetOrCreateTextureMaterial("Mat_BeyondUIDisplay", _beyondDisplayTexture, isUnlit: false);

            // 4 Big Windows distributed at indices 4, 12, 20, 28 (45°, 135°, 225°, 315°)
            HashSet<int> bigWindowIndices = new HashSet<int>() { 4, 12, 20, 28 };

            // Construct Wall Shell (32 Segments)
            for (int i = 0; i < wallSegments; i++)
            {
                float currentAngle = i * angleStep;
                Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
                Vector3 position = rotation * new Vector3(0, roomHeight / 2f, roomRadius);

                // --- 1. BACK WALL (180°) - "OUR SOLAR SYSTEM" MAIN CURVED DISPLAY & SIDE PANELS ---
                if (i >= 14 && i <= 18)
                {
                    // Wall backdrop
                    GameObject wallSec = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallSec.name = $"Wall_Backdrop_{i}";
                    wallSec.transform.SetParent(wallsParent, false);
                    wallSec.transform.position = position;
                    wallSec.transform.rotation = rotation;
                    wallSec.transform.localScale = new Vector3(segmentWidth + 0.05f, roomHeight, 0.2f);
                    wallSec.GetComponent<Renderer>().sharedMaterial = wallMat;
                    SetStaticFlags(wallSec);

                    if (i == 16)
                    {
                        // Main Display Board: "OUR SOLAR SYSTEM"
                        GameObject mainDisplay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        mainDisplay.name = "MainDisplay_OurSolarSystem";
                        mainDisplay.transform.SetParent(panelsParent, false);
                        mainDisplay.transform.position = rotation * new Vector3(0, roomHeight * 0.58f, roomRadius - 0.15f);
                        mainDisplay.transform.rotation = rotation * Quaternion.Euler(0, displayRotationY, 0);
                        mainDisplay.transform.localScale = new Vector3(3.2f, 1.7f, 1f);
                        mainDisplay.GetComponent<Renderer>().sharedMaterial = mainDisplayMat;
                        SetStaticFlags(mainDisplay);

                        // Cyan LED Frame Border
                        GameObject displayFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        displayFrame.name = "MainDisplay_CyanFrame";
                        displayFrame.transform.SetParent(panelsParent, false);
                        displayFrame.transform.position = rotation * new Vector3(0, roomHeight * 0.58f, roomRadius - 0.16f);
                        displayFrame.transform.rotation = rotation;
                        displayFrame.transform.localScale = new Vector3(3.3f, 1.8f, 0.04f);
                        displayFrame.GetComponent<Renderer>().sharedMaterial = emissiveBlueMat;
                        SetStaticFlags(displayFrame);
                    }
                    else if (i == 14)
                    {
                        // Left Side Display Panel: "FACTS"
                        GameObject factsPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        factsPanel.name = "SidePanel_Facts";
                        factsPanel.transform.SetParent(panelsParent, false);
                        factsPanel.transform.position = rotation * new Vector3(0, roomHeight * 0.56f, roomRadius - 0.15f);
                        factsPanel.transform.rotation = rotation * Quaternion.Euler(0, displayRotationY, 0);
                        factsPanel.transform.localScale = new Vector3(1.1f, 1.6f, 1f);
                        factsPanel.GetComponent<Renderer>().sharedMaterial = factsDisplayMat;
                        SetStaticFlags(factsPanel);

                        BuildWallSconce(archLightsParent, rotation, roomHeight * 0.88f, roomRadius - 0.2f, "Sconce_Facts");
                    }
                    else if (i == 18)
                    {
                        // Right Side Display Panel: "BEYOND NEPTUNE"
                        GameObject beyondPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        beyondPanel.name = "SidePanel_BeyondNeptune";
                        beyondPanel.transform.SetParent(panelsParent, false);
                        beyondPanel.transform.position = rotation * new Vector3(0, roomHeight * 0.56f, roomRadius - 0.15f);
                        beyondPanel.transform.rotation = rotation * Quaternion.Euler(0, displayRotationY, 0);
                        beyondPanel.transform.localScale = new Vector3(1.1f, 1.6f, 1f);
                        beyondPanel.GetComponent<Renderer>().sharedMaterial = beyondDisplayMat;
                        SetStaticFlags(beyondPanel);

                        BuildWallSconce(archLightsParent, rotation, roomHeight * 0.88f, roomRadius - 0.2f, "Sconce_Beyond");
                    }
                    continue;
                }

                // --- 2. DOORWAY EXIT (180° offset / i == 0 at Front) ---
                if (i == 0)
                {
                    GameObject wallSec = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallSec.name = $"Wall_DoorSection_{i}";
                    wallSec.transform.SetParent(wallsParent, false);
                    wallSec.transform.position = position;
                    wallSec.transform.rotation = rotation;
                    wallSec.transform.localScale = new Vector3(segmentWidth + 0.05f, roomHeight, 0.2f);
                    wallSec.GetComponent<Renderer>().sharedMaterial = wallMat;
                    SetStaticFlags(wallSec);

                    GameObject doorFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    doorFrame.name = "ExitDoor_Frame";
                    doorFrame.transform.SetParent(wallsParent, false);
                    doorFrame.transform.position = rotation * new Vector3(0, 1.2f, roomRadius - 0.08f);
                    doorFrame.transform.rotation = rotation;
                    doorFrame.transform.localScale = new Vector3(1.3f, 2.3f, 0.08f);
                    doorFrame.GetComponent<Renderer>().sharedMaterial = frameMat;
                    SetStaticFlags(doorFrame);
                    continue;
                }

                // --- 3. 4 BIG OBSERVATORY WINDOWS ---
                if (bigWindowIndices.Contains(i))
                {
                    // Lower Wall below window
                    float lowerH = windowElevation - (windowHeight / 2f);
                    GameObject lowerW = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lowerW.name = $"Wall_Lower_{i}";
                    lowerW.transform.SetParent(wallsParent, false);
                    lowerW.transform.position = rotation * new Vector3(0, lowerH / 2f, roomRadius);
                    lowerW.transform.rotation = rotation;
                    lowerW.transform.localScale = new Vector3(segmentWidth + 0.05f, lowerH, 0.2f);
                    lowerW.GetComponent<Renderer>().sharedMaterial = wallMat;
                    SetStaticFlags(lowerW);

                    // Upper Wall above window
                    float upperTop = windowElevation + (windowHeight / 2f);
                    float upperH = roomHeight - upperTop;
                    GameObject upperW = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    upperW.name = $"Wall_Upper_{i}";
                    upperW.transform.SetParent(wallsParent, false);
                    upperW.transform.position = rotation * new Vector3(0, upperTop + (upperH / 2f), roomRadius);
                    upperW.transform.rotation = rotation;
                    upperW.transform.localScale = new Vector3(segmentWidth + 0.05f, upperH, 0.2f);
                    upperW.GetComponent<Renderer>().sharedMaterial = wallMat;
                    SetStaticFlags(upperW);

                    // Window Glass Starfield Quad
                    GameObject windowPane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    windowPane.name = $"ObservatoryBigWindow_{i}";
                    windowPane.transform.SetParent(windowsParent, false);
                    windowPane.transform.position = rotation * new Vector3(0, windowElevation, roomRadius + 0.04f);
                    windowPane.transform.rotation = rotation * Quaternion.Euler(0, windowRotationY, 0);
                    windowPane.transform.localScale = new Vector3(windowWidth, windowHeight, 1f);
                    windowPane.GetComponent<Renderer>().sharedMaterial = starfieldMat;
                    SetStaticFlags(windowPane);

                    // Window Frame
                    BuildWindowFrame(framesParent, rotation, windowElevation, roomRadius, windowWidth, windowHeight, frameMat, emissiveBlueMat, i);
                    continue;
                }

                // --- 4. PLANET DISPLAY COUNTER BENCHES & BOOKCASE SHELVES ---
                if (i == 6 || i == 7 || i == 25 || i == 26)
                {
                    GameObject wallSec = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallSec.name = $"Wall_DisplaySection_{i}";
                    wallSec.transform.SetParent(wallsParent, false);
                    wallSec.transform.position = position;
                    wallSec.transform.rotation = rotation;
                    wallSec.transform.localScale = new Vector3(segmentWidth + 0.05f, roomHeight, 0.2f);
                    wallSec.GetComponent<Renderer>().sharedMaterial = wallMat;
                    SetStaticFlags(wallSec);

                    BuildPlanetDisplayCounterBench(shelvesParent, rotation, roomRadius, wallMat, frameMat, emissiveBlueMat, i);
                    continue;
                }

                // --- 5. STANDARD ARCHITECTURAL WALL SEGMENTS & PILLARS ---
                GameObject stdWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stdWall.name = $"Wall_Segment_{i}";
                stdWall.transform.SetParent(wallsParent, false);
                stdWall.transform.position = position;
                stdWall.transform.rotation = rotation;
                stdWall.transform.localScale = new Vector3(segmentWidth + 0.05f, roomHeight, 0.2f);
                stdWall.GetComponent<Renderer>().sharedMaterial = wallMat;
                SetStaticFlags(stdWall);

                if (i % 2 == 0)
                {
                    GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = $"Wall_Pillar_{i}";
                    pillar.transform.SetParent(wallsParent, false);
                    pillar.transform.position = rotation * new Vector3(segmentWidth / 2f, roomHeight / 2f, roomRadius - 0.05f);
                    pillar.transform.rotation = rotation;
                    pillar.transform.localScale = new Vector3(0.14f, roomHeight, 0.14f);
                    pillar.GetComponent<Renderer>().sharedMaterial = frameMat;
                    SetStaticFlags(pillar);
                }
            }
        }

        private void BuildWindowFrame(Transform framesParent, Quaternion rotation, float elevation, float radius, float wWidth, float wHeight, Material frameMat, Material emissiveMat, int index)
        {
            float thickness = 0.08f;
            float depth = 0.15f;

            // Top Frame
            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = $"Frame_Top_{index}";
            top.transform.SetParent(framesParent, false);
            top.transform.position = rotation * new Vector3(0, elevation + (wHeight / 2f) + (thickness / 2f), radius - 0.02f);
            top.transform.rotation = rotation;
            top.transform.localScale = new Vector3(wWidth + (thickness * 2f), thickness, depth);
            top.GetComponent<Renderer>().sharedMaterial = frameMat;
            SetStaticFlags(top);

            // Top Cyan Emissive Strip
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glow.name = $"Frame_Glow_{index}";
            glow.transform.SetParent(framesParent, false);
            glow.transform.position = rotation * new Vector3(0, elevation + (wHeight / 2f) - 0.02f, radius - 0.05f);
            glow.transform.rotation = rotation;
            glow.transform.localScale = new Vector3(wWidth * 0.95f, 0.025f, 0.04f);
            glow.GetComponent<Renderer>().sharedMaterial = emissiveMat;
            SetStaticFlags(glow);

            // Bottom Frame
            GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottom.name = $"Frame_Bottom_{index}";
            bottom.transform.SetParent(framesParent, false);
            bottom.transform.position = rotation * new Vector3(0, elevation - (wHeight / 2f) - (thickness / 2f), radius - 0.02f);
            bottom.transform.rotation = rotation;
            bottom.transform.localScale = new Vector3(wWidth + (thickness * 2f), thickness, depth);
            bottom.GetComponent<Renderer>().sharedMaterial = frameMat;
            SetStaticFlags(bottom);

            // Left & Right Frames
            GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = $"Frame_Left_{index}";
            left.transform.SetParent(framesParent, false);
            left.transform.position = rotation * new Vector3(-(wWidth / 2f) - (thickness / 2f), elevation, radius - 0.02f);
            left.transform.rotation = rotation;
            left.transform.localScale = new Vector3(thickness, wHeight, depth);
            left.GetComponent<Renderer>().sharedMaterial = frameMat;
            SetStaticFlags(left);

            GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = $"Frame_Right_{index}";
            right.transform.SetParent(framesParent, false);
            right.transform.position = rotation * new Vector3((wWidth / 2f) + (thickness / 2f), elevation, radius - 0.02f);
            right.transform.rotation = rotation;
            right.transform.localScale = new Vector3(thickness, wHeight, depth);
            right.GetComponent<Renderer>().sharedMaterial = frameMat;
            SetStaticFlags(right);
        }

        private void BuildPlanetDisplayCounterBench(Transform shelvesParent, Quaternion rotation, float radius, Material wallMat, Material frameMat, Material emissiveMat, int index)
        {
            // Low Pedestal Counter Bench Base
            GameObject benchBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            benchBase.name = $"DisplayBench_Base_{index}";
            benchBase.transform.SetParent(shelvesParent, false);
            benchBase.transform.position = rotation * new Vector3(0, 0.38f, radius - 0.35f);
            benchBase.transform.rotation = rotation;
            benchBase.transform.localScale = new Vector3(1.8f, 0.75f, 0.6f);
            benchBase.GetComponent<Renderer>().sharedMaterial = frameMat;
            SetStaticFlags(benchBase);

            // Illuminated Glass Counter Top Surface
            GameObject benchTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            benchTop.name = $"DisplayBench_Top_{index}";
            benchTop.transform.SetParent(shelvesParent, false);
            benchTop.transform.position = rotation * new Vector3(0, 0.77f, radius - 0.35f);
            benchTop.transform.rotation = rotation;
            benchTop.transform.localScale = new Vector3(1.85f, 0.04f, 0.65f);
            benchTop.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Mat_BenchTopGlass", tableSurfaceColor, 0.8f, 0.1f);
            SetStaticFlags(benchTop);

            // Front Glowing LED Accent Strip along bench
            GameObject benchGlow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            benchGlow.name = $"DisplayBench_Glow_{index}";
            benchGlow.transform.SetParent(shelvesParent, false);
            benchGlow.transform.position = rotation * new Vector3(0, 0.74f, radius - 0.66f);
            benchGlow.transform.rotation = rotation;
            benchGlow.transform.localScale = new Vector3(1.8f, 0.02f, 0.04f);
            benchGlow.GetComponent<Renderer>().sharedMaterial = emissiveMat;
            SetStaticFlags(benchGlow);

            // 4 Planet Display Pedestals
            Color[] planetColors = new Color[]
            {
                new Color(0.72f, 0.72f, 0.75f), // Mercury Grey
                new Color(0.90f, 0.75f, 0.50f), // Venus Sand
                new Color(0.20f, 0.60f, 0.90f), // Earth Blue
                new Color(0.88f, 0.38f, 0.22f), // Mars Terracotta
                new Color(0.82f, 0.68f, 0.42f)  // Saturn Golden
            };

            for (int p = 0; p < 4; p++)
            {
                float xPos = -0.6f + (p * 0.4f);

                GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ped.name = $"PlanetPedestal_{index}_{p}";
                ped.transform.SetParent(shelvesParent, false);
                ped.transform.position = rotation * new Vector3(xPos, 0.82f, radius - 0.35f);
                ped.transform.rotation = rotation;
                ped.transform.localScale = new Vector3(0.18f, 0.05f, 0.18f);
                ped.GetComponent<Renderer>().sharedMaterial = emissiveMat;
                SetStaticFlags(ped);

                float pScale = 0.16f + (p % 2 == 0 ? 0.04f : 0f);
                GameObject planetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                planetObj.name = $"PlanetModel_{index}_{p}";
                planetObj.transform.SetParent(shelvesParent, false);
                planetObj.transform.position = rotation * new Vector3(xPos, 0.85f + (pScale / 2f), radius - 0.35f);
                planetObj.transform.rotation = Quaternion.identity;
                planetObj.transform.localScale = Vector3.one * pScale;

                Color pCol = planetColors[(p + index) % planetColors.Length];
                planetObj.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial($"Mat_DisplayPlanet_{pCol.GetHashCode()}", pCol, 0.5f);
                SetStaticFlags(planetObj);

                if (p == 3)
                {
                    GameObject saturnRing = CreateThinRing($"SaturnRing_Display_{index}", 0.18f, 0.005f, GetOrCreateMaterial("Mat_SaturnRingMat", new Color(0.85f, 0.75f, 0.5f), 0.4f));
                    saturnRing.transform.SetParent(shelvesParent, false);
                    saturnRing.transform.position = planetObj.transform.position;
                    saturnRing.transform.rotation = Quaternion.Euler(20f, 0, 15f);
                    SetStaticFlags(saturnRing);
                }
            }

            if (index == 7 || index == 26)
            {
                BuildPottedPlant(shelvesParent, rotation * new Vector3(0.95f, 0, radius - 0.45f), "PottedPlant_" + index);
            }
        }

        private void BuildPottedPlant(Transform parent, Vector3 position, string name)
        {
            GameObject plantContainer = new GameObject(name);
            plantContainer.transform.SetParent(parent, false);
            plantContainer.transform.position = position;

            GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pot.name = "Pot";
            pot.transform.SetParent(plantContainer.transform, false);
            pot.transform.localPosition = new Vector3(0, 0.25f, 0);
            pot.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
            pot.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Mat_PlantPot", new Color(0.9f, 0.9f, 0.92f), 0.2f);
            SetStaticFlags(pot);

            Color leafColor = new Color(0.18f, 0.45f, 0.22f);
            Material leafMat = GetOrCreateMaterial("Mat_PlantFoliage", leafColor, 0.1f);

            for (int f = 0; f < 3; f++)
            {
                GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foliage.name = $"Foliage_{f}";
                foliage.transform.SetParent(plantContainer.transform, false);
                foliage.transform.localPosition = new Vector3((f - 1) * 0.08f, 0.6f + (f * 0.08f), 0);
                foliage.transform.localScale = new Vector3(0.32f, 0.4f, 0.32f);
                foliage.GetComponent<Renderer>().sharedMaterial = leafMat;
                SetStaticFlags(foliage);
            }
        }

        private void BuildWallSconce(Transform archLightsParent, Quaternion rotation, float height, float radius, string name)
        {
            GameObject sconceObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sconceObj.name = name + "_Fixture";
            sconceObj.transform.SetParent(archLightsParent, false);
            sconceObj.transform.position = rotation * new Vector3(0, height, radius);
            sconceObj.transform.rotation = rotation;
            sconceObj.transform.localScale = new Vector3(0.14f, 0.04f, 0.14f);
            sconceObj.GetComponent<Renderer>().sharedMaterial = GetOrCreateEmissiveMaterial("Mat_SconceGlow", warmSpotColor, 2.0f);
            SetStaticFlags(sconceObj);

            GameObject lightObj = new GameObject(name + "_Light");
            lightObj.transform.SetParent(archLightsParent, false);
            lightObj.transform.position = rotation * new Vector3(0, height - 0.05f, radius - 0.1f);

            Light sconceLight = lightObj.AddComponent<Light>();
            sconceLight.type = LightType.Point;
            sconceLight.color = warmSpotColor;
            sconceLight.intensity = spotlightIntensity * 0.7f;
            sconceLight.range = 3.5f;
            sconceLight.shadows = LightShadows.None;
        }

        private void BuildCentralExhibit(Transform tableParent, Transform tableSurfaceParent, Transform tableRingsParent, Transform solarSystemParent)
        {
            // 1. Central Pedestal Table Base
            GameObject tableBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tableBase.name = "TableBasePedestal";
            tableBase.transform.SetParent(tableParent, false);
            tableBase.transform.localPosition = new Vector3(0, tableHeight / 2f, 0);
            tableBase.transform.localScale = new Vector3(tableDiameter, tableHeight / 2f, tableDiameter);

            Material tableBaseMat = GetOrCreateMaterial("Mat_TableBase", tableBaseColor, 0.4f);
            tableBase.GetComponent<Renderer>().sharedMaterial = tableBaseMat;
            SetStaticFlags(tableBase);

            // Cyan Emissive Ring around Table Base (+0.1m larger)
            Material emissiveBlueMat = GetOrCreateEmissiveMaterial("Mat_EmissiveBlue", accentBlueColor, 1.8f);
            float baseRingRadius = (tableDiameter / 2f) + 0.12f;
            GameObject baseGlowRing = CreateThinRing("TableBase_BottomLED", baseRingRadius, 0.035f, emissiveBlueMat);
            baseGlowRing.transform.SetParent(tableRingsParent, false);
            baseGlowRing.transform.localPosition = new Vector3(0, 0.02f, 0);
            SetStaticFlags(baseGlowRing);

            // 2. Front Illuminated Display Screen Panel ("SOLAR SYSTEM")
            GameObject tableFrontPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tableFrontPanel.name = "TableFrontPanel_SolarSystem";
            tableFrontPanel.transform.SetParent(tableParent, false);
            tableFrontPanel.transform.localPosition = new Vector3(0, tableHeight * 0.45f, (tableDiameter / 2f) + 0.01f);
            tableFrontPanel.transform.localRotation = Quaternion.Euler(0, tablePanelRotationY, 0);
            tableFrontPanel.transform.localScale = new Vector3(1.2f, 0.35f, 1f);

            Material tableFrontMat = GetOrCreateTextureMaterial("Mat_TableFrontUI", _tableFrontTexture, isUnlit: false);
            tableFrontPanel.GetComponent<Renderer>().sharedMaterial = tableFrontMat;
            SetStaticFlags(tableFrontPanel);

            // 3. Dark Blue Glass Table Surface Top
            GameObject tableSurface = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tableSurface.name = "TableGlassSurface";
            tableSurface.transform.SetParent(tableSurfaceParent, false);
            tableSurface.transform.localPosition = new Vector3(0, tableHeight + 0.015f, 0);
            tableSurface.transform.localScale = new Vector3(tableDiameter + 0.08f, 0.03f, tableDiameter + 0.08f);

            Material glassMat = GetOrCreateMaterial("Mat_TableSurface", tableSurfaceColor, 0.88f, 0.2f);
            tableSurface.GetComponent<Renderer>().sharedMaterial = glassMat;
            SetStaticFlags(tableSurface);

            // Glowing Outer & Inner Rim Rings (+0.1m larger)
            float outerTopRadius = (tableDiameter / 2f) + 0.13f;
            GameObject tableOuterRing = CreateThinRing("TableGlowRing_Outer", outerTopRadius, 0.028f, emissiveBlueMat);
            tableOuterRing.transform.SetParent(tableRingsParent, false);
            tableOuterRing.transform.localPosition = new Vector3(0, tableHeight + 0.032f, 0);
            SetStaticFlags(tableOuterRing);

            float innerTopRadius = ((tableDiameter / 2f) * 0.5f) + 0.10f;
            GameObject tableInnerRing = CreateThinRing("TableGlowRing_Inner", innerTopRadius, 0.022f, emissiveBlueMat);
            tableInnerRing.transform.SetParent(tableRingsParent, false);
            tableInnerRing.transform.localPosition = new Vector3(0, tableHeight + 0.032f, 0);
            SetStaticFlags(tableInnerRing);

            // 4. Complete 3D Holographic 8-Planet Solar System Model
            BuildCompleteSolarSystemModel(solarSystemParent);
        }

        private void BuildCompleteSolarSystemModel(Transform solarSystemParent)
        {
            float displayY = tableHeight + 1.45f;
            Vector3 centerPos = new Vector3(0, displayY, 0);

            // Central Glowing Sun Sphere
            GameObject sunObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sunObj.name = "Sun_CentralHologram";
            sunObj.transform.SetParent(solarSystemParent, false);
            sunObj.transform.localPosition = centerPos;
            sunObj.transform.localScale = Vector3.one * 0.55f;

            Material sunMat = GetOrCreateEmissiveMaterial("Mat_Sun", sunEmissionColor, 2.5f);
            sunObj.GetComponent<Renderer>().sharedMaterial = sunMat;
            SetStaticFlags(sunObj);

            // Complete 8 Planets + Saturn Rings
            Material orbitLineMat = GetOrCreateEmissiveMaterial("Mat_OrbitLine", accentBlueColor * 0.7f, 1.0f);

            var planets = new[]
            {
                new { Name = "Mercury", Radius = 0.70f, Scale = 0.09f, Angle = 25f,  Color = new Color(0.72f, 0.72f, 0.75f), HasRings = false },
                new { Name = "Venus",   Radius = 0.95f, Scale = 0.13f, Angle = 115f, Color = new Color(0.90f, 0.75f, 0.50f), HasRings = false },
                new { Name = "Earth",   Radius = 1.20f, Scale = 0.15f, Angle = 210f, Color = new Color(0.20f, 0.60f, 0.90f), HasRings = false },
                new { Name = "Mars",    Radius = 1.45f, Scale = 0.11f, Angle = 305f, Color = new Color(0.88f, 0.38f, 0.22f), HasRings = false },
                new { Name = "Jupiter", Radius = 1.80f, Scale = 0.28f, Angle = 65f,  Color = new Color(0.82f, 0.68f, 0.42f), HasRings = false },
                new { Name = "Saturn",  Radius = 2.15f, Scale = 0.24f, Angle = 160f, Color = new Color(0.88f, 0.78f, 0.52f), HasRings = true  },
                new { Name = "Uranus",  Radius = 2.45f, Scale = 0.18f, Angle = 250f, Color = new Color(0.40f, 0.85f, 0.90f), HasRings = false },
                new { Name = "Neptune", Radius = 2.75f, Scale = 0.18f, Angle = 340f, Color = new Color(0.15f, 0.40f, 0.85f), HasRings = false }
            };

            foreach (var p in planets)
            {
                // Thin Orbit Line Ring
                GameObject orbitRing = CreateThinRing($"OrbitRing_{p.Name}", p.Radius, 0.012f, orbitLineMat);
                orbitRing.transform.SetParent(solarSystemParent, false);
                orbitRing.transform.localPosition = centerPos;
                SetStaticFlags(orbitRing);

                // Planet Sphere Position
                Vector3 offset = Quaternion.Euler(0, p.Angle, 0) * new Vector3(p.Radius, 0, 0);
                GameObject planetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                planetObj.name = $"Hologram_{p.Name}";
                planetObj.transform.SetParent(solarSystemParent, false);
                planetObj.transform.localPosition = centerPos + offset;
                planetObj.transform.localScale = Vector3.one * p.Scale;

                Material pMat = GetOrCreateMaterial($"Mat_Holo_{p.Name}", p.Color, 0.5f);
                planetObj.GetComponent<Renderer>().sharedMaterial = pMat;
                SetStaticFlags(planetObj);

                // Saturn Ring Disc
                if (p.HasRings)
                {
                    GameObject ringDisc = CreateThinRing("Saturn_HologramRings", p.Scale * 1.1f, 0.005f, GetOrCreateMaterial("Mat_SaturnHoloRing", new Color(0.85f, 0.75f, 0.5f), 0.4f));
                    ringDisc.transform.SetParent(solarSystemParent, false);
                    ringDisc.transform.localPosition = centerPos + offset;
                    ringDisc.transform.localRotation = Quaternion.Euler(22f, 0, 15f);
                    SetStaticFlags(ringDisc);
                }

                // If Earth, add tiny Moon sphere
                if (p.Name == "Earth")
                {
                    GameObject moonObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    moonObj.name = "Hologram_Moon";
                    moonObj.transform.SetParent(solarSystemParent, false);
                    moonObj.transform.localPosition = centerPos + offset + new Vector3(0.22f, 0.04f, 0f);
                    moonObj.transform.localScale = Vector3.one * 0.045f;
                    moonObj.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Mat_Moon", new Color(0.8f, 0.8f, 0.82f), 0.2f);
                    SetStaticFlags(moonObj);
                }
            }
        }

        private void BuildLighting(Transform ambientParent, Transform sunLightParent)
        {
            // Soft Cool Blue Directional Ambient Light
            GameObject ambientLightObj = new GameObject("AmbientDirectionalLight");
            ambientLightObj.transform.SetParent(ambientParent, false);
            ambientLightObj.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            Light ambientLight = ambientLightObj.AddComponent<Light>();
            ambientLight.type = LightType.Directional;
            ambientLight.color = new Color(0.75f, 0.85f, 0.98f);
            ambientLight.intensity = ambientIntensity;
            ambientLight.shadows = LightShadows.Soft;

            // Warm Central Sun Point Light
            GameObject sunLightObj = new GameObject("SunPointLight");
            sunLightObj.transform.SetParent(sunLightParent, false);
            sunLightObj.transform.localPosition = new Vector3(0, tableHeight + 1.45f, 0);

            Light sunLight = sunLightObj.AddComponent<Light>();
            sunLight.type = LightType.Point;
            sunLight.color = new Color(1.0f, 0.65f, 0.25f);
            sunLight.intensity = sunLightIntensity;
            sunLight.range = 9.0f;
            sunLight.shadows = LightShadows.Soft;
        }

        #endregion

        #region Render Pipeline Compatibility Helpers

        private Shader FindLitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) return s;
            s = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (s != null) return s;
            s = Shader.Find("Standard");
            if (s != null) return s;
            return Shader.Find("Diffuse");
        }

        private Shader FindUnlitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s != null) return s;
            s = Shader.Find("Unlit/Texture");
            if (s != null) return s;
            s = Shader.Find("Unlit/Color");
            if (s != null) return s;
            return Shader.Find("Standard");
        }

        private void SetMaterialColor(Material mat, Color color)
        {
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        }

        private void SetMaterialTexture(Material mat, Texture2D texture)
        {
            mat.mainTexture = texture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        }

        private void SetMaterialSmoothness(Material mat, float smoothness, float metallic)
        {
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        }

        private Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic = 0.0f)
        {
            if (_materialCache.TryGetValue(name, out Material cached) && cached != null)
                return cached;

            Shader shader = FindLitShader();
            Material mat = new Material(shader);
            mat.name = name;

            SetMaterialColor(mat, color);
            SetMaterialSmoothness(mat, smoothness, metallic);

            _materialCache[name] = mat;
            return mat;
        }

        private Material GetOrCreateEmissiveMaterial(string name, Color emissionColor, float intensity)
        {
            if (_materialCache.TryGetValue(name, out Material cached) && cached != null)
                return cached;

            Shader shader = FindLitShader();
            Material mat = new Material(shader);
            mat.name = name;

            Color baseColor = emissionColor * 0.2f;
            SetMaterialColor(mat, baseColor);

            Color finalEmission = emissionColor * intensity;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", finalEmission);

            _materialCache[name] = mat;
            return mat;
        }

        private Material GetOrCreateTextureMaterial(string name, Texture2D texture, bool isUnlit)
        {
            if (_materialCache.TryGetValue(name, out Material cached) && cached != null)
                return cached;

            Shader shader = isUnlit ? FindUnlitShader() : FindLitShader();
            Material mat = new Material(shader);
            mat.name = name;

            SetMaterialTexture(mat, texture);
            SetMaterialColor(mat, Color.white);

            _materialCache[name] = mat;
            return mat;
        }

        #endregion

        #region Procedural UI & Texture Generation

        private Transform CreateChildContainer(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            return container.transform;
        }

        private GameObject CreateThinRing(string name, float radius, float lineThickness, Material mat)
        {
            GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObj.name = name;
            ringObj.transform.localScale = new Vector3(radius * 2f, 0.005f, radius * 2f);
            ringObj.GetComponent<Renderer>().sharedMaterial = mat;
            return ringObj;
        }

        private Texture2D CreateStarfieldTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color spaceBg = new Color(0.02f, 0.04f, 0.09f, 1.0f);

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = spaceBg;

            int starCount = 650;
            Random.InitState(42);
            for (int s = 0; s < starCount; s++)
            {
                int rx = Random.Range(2, width - 2);
                int ry = Random.Range(2, height - 2);

                float brightness = Random.Range(0.4f, 1.0f);
                Color starCol = (Random.value > 0.3f) ?
                    new Color(0.8f, 0.92f, 1.0f, 1.0f) * brightness :
                    new Color(1.0f, 0.85f, 0.6f, 1.0f) * brightness;

                pixels[ry * width + rx] = starCol;
                pixels[ry * width + (rx + 1)] = starCol * 0.4f;
                pixels[ry * width + (rx - 1)] = starCol * 0.4f;
                pixels[(ry + 1) * width + rx] = starCol * 0.4f;
                pixels[(ry - 1) * width + rx] = starCol * 0.4f;
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private Texture2D CreateMainDisplayTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color bg = new Color(0.06f, 0.10f, 0.16f, 0.95f);
            Color cyan = new Color(0.0f, 0.8f, 1.0f, 1.0f);

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            int centerX = (int)(width * 0.78f);
            int centerY = height / 2;
            int[] radii = new int[] { 25, 45, 65, 85 };

            foreach (int r in radii)
            {
                for (float a = 0; a < 360f; a += 1.5f)
                {
                    int x = centerX + (int)(r * Mathf.Cos(a * Mathf.Deg2Rad));
                    int y = centerY + (int)(r * Mathf.Sin(a * Mathf.Deg2Rad));
                    if (x >= 0 && x < width && y >= 0 && y < height)
                        pixels[y * width + x] = cyan * 0.6f;
                }
            }

            for (int dx = -6; dx <= 6; dx++)
            {
                for (int dy = -6; dy <= 6; dy++)
                {
                    if (dx * dx + dy * dy <= 36)
                    {
                        int x = centerX + dx;
                        int y = centerY + dy;
                        if (x >= 0 && x < width && y >= 0 && y < height)
                            pixels[y * width + x] = new Color(1f, 0.65f, 0.1f, 1f);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private Texture2D CreateFactsDisplayTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color bg = new Color(0.06f, 0.10f, 0.16f, 0.95f);
            Color cyan = new Color(0.0f, 0.8f, 1.0f, 1.0f);

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            for (int x = 20; x < width - 20; x++)
            {
                for (int y = height - 40; y < height - 36; y++)
                    pixels[y * width + x] = cyan;
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private Texture2D CreateBeyondDisplayTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color bg = new Color(0.06f, 0.10f, 0.16f, 0.95f);
            Color cyan = new Color(0.0f, 0.8f, 1.0f, 1.0f);

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            for (int x = 20; x < width - 20; x++)
            {
                for (int y = height - 40; y < height - 36; y++)
                    pixels[y * width + x] = cyan;
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private Texture2D CreateTableFrontTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color bg = new Color(0.04f, 0.08f, 0.14f, 0.95f);
            Color cyan = new Color(0.0f, 0.82f, 1.0f, 1.0f);

            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            for (int x = 10; x < width - 10; x++)
            {
                for (int y = height / 2 - 1; y <= height / 2 + 1; y++)
                    pixels[y * width + x] = cyan * 0.7f;
            }

            int planetCount = 8;
            int step = width / (planetCount + 1);
            for (int p = 1; p <= planetCount; p++)
            {
                int cx = p * step;
                int cy = height / 2;
                for (int dx = -4; dx <= 4; dx++)
                {
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        if (dx * dx + dy * dy <= 16)
                        {
                            int x = cx + dx;
                            int y = cy + dy;
                            if (x >= 0 && x < width && y >= 0 && y < height)
                                pixels[y * width + x] = (p == 5 || p == 6) ? new Color(1f, 0.75f, 0.3f) : cyan;
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private void SetStaticFlags(GameObject obj)
        {
#if UNITY_EDITOR
            GameObjectUtility.SetStaticEditorFlags(obj,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic);
#endif
        }

        #endregion
    }
}
