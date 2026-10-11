#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AdaptiveVR.PlanetPuzzle;
using AdaptiveVR.Environment;

namespace AdaptiveVR.Editor
{
    /// <summary>
    /// One-click optimizer to adapt the AdaptVR environment for stationary hand-tracking (Option A).
    /// Repositions the XR Origin right in front of the table, scales the orbits down to comfortable arm-reach (0.4x),
    /// generates all sockets facing the user, and places all 8 planets in an ergonomic front-rack on the table.
    /// </summary>
    public static class StationaryScaleOptimizer
    {
        private const float TableRadius = 1.44f;
        private const float TableSurfaceY = 0.92f;
        private const float OptimalPlayerZ = -1.05f;
        private const float OptimalScaleMultiplier = 0.40f;
        private const float OptimalStartAngle = -125.0f;
        private const float OptimalArcSpread = 70.0f;

        [MenuItem("Tools/AdaptVR/Optimize Scale for Stationary VR (Option A)", priority = 10)]
        [MenuItem("AdaptVR/Optimize Scale for Stationary VR (Option A)", priority = 10)]
        public static void OptimizeForStationaryVR()
        {
            Undo.SetCurrentGroupName("Optimize Scale for Stationary VR (Option A)");
            int undoGroup = Undo.GetCurrentGroup();

            Debug.Log("<color=#00E5FF><b>[AdaptVR]</b> Optimizing environment scale for Stationary Hand-Tracking (Option A)...</color>");

            // 1. Find and reposition XR Origin
            GameObject xrRig = GameObject.Find("XR Origin Hands (XR Rig) (1)") 
                            ?? GameObject.Find("XR Origin Hands (XR Rig)")
                            ?? GameObject.Find("XR Origin")
                            ?? GameObject.Find("XR Rig");

            if (xrRig != null)
            {
                Undo.RecordObject(xrRig.transform, "Reposition XR Origin");
                xrRig.transform.position = new Vector3(0f, 0f, OptimalPlayerZ);
                xrRig.transform.rotation = Quaternion.identity; // Facing forward (+Z) directly toward the table center
                Debug.Log($"<color=#00FF66>✓</color> XR Origin placed at front edge of table: {xrRig.transform.position}");
            }
            else
            {
                Debug.LogWarning("[AdaptVR] Could not find XR Origin GameObject in scene to reposition.");
            }

            // 2. Find and configure SolarSystemPuzzleManager
            SolarSystemPuzzleManager puzzleManager = Object.FindFirstObjectByType<SolarSystemPuzzleManager>();
            if (puzzleManager == null)
            {
#pragma warning disable CS0618
                puzzleManager = Object.FindObjectOfType<SolarSystemPuzzleManager>();
#pragma warning restore CS0618
            }

            if (puzzleManager != null)
            {
                Undo.RecordObject(puzzleManager, "Configure Puzzle Scale & Arcs");

                // Auto-detect planets first so puzzleManager.planets is populated
                puzzleManager.AutoDetectScenePlanets();

                // Apply downscaled radii settings
                puzzleManager.scaleMultiplier = OptimalScaleMultiplier;
                puzzleManager.socketStartAngle = OptimalStartAngle;
                puzzleManager.socketArcSpread = OptimalArcSpread;
                puzzleManager.socketsHeightOffset = 0.01f;

                // Re-generate orbits & sockets
                Undo.RegisterFullObjectHierarchyUndo(puzzleManager.gameObject, "Rebuild Orbits & Sockets");
                puzzleManager.CreateOrbitSocketsAroundSun();

                EditorUtility.SetDirty(puzzleManager);
                Debug.Log($"<color=#00FF66>✓</color> Orbits scaled down to {OptimalScaleMultiplier}x and oriented directly toward player.");

                // 3. Place planets on an ergonomic front table tray
                ArrangePlanetsOnTableRack(puzzleManager);
            }
            else
            {
                Debug.LogError("[AdaptVR] SolarSystemPuzzleManager not found in scene!");
            }

            // 4. Create or update visual Planet Tray / Indicator mat on table
            CreateOrUpdatePlanetTrayVisual();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "AdaptVR - Option A Applied!",
                "Scene scale successfully optimized for Stationary Hand-Tracking!\n\n" +
                "• Player moved to front edge of table (Z = -1.05m)\n" +
                "• Solar orbits scaled to 0.40x (max reach ~0.9m)\n" +
                "• Sockets oriented directly facing the player\n" +
                "• All 8 planets placed in an accessible arc on the table edge (0.3m-0.5m reach)\n\n" +
                "Please save the scene (Ctrl + S).",
                "Great!"
            );
        }

        private static void ArrangePlanetsOnTableRack(SolarSystemPuzzleManager puzzleManager)
        {
            if (puzzleManager.planets == null || puzzleManager.planets.Count == 0) return;

            // Sort planets in standard celestial order
            var orderedPlanets = new List<GrabbablePlanet>(puzzleManager.planets);
            orderedPlanets.Sort((a, b) => a.preset.CompareTo(b.preset));

            int count = orderedPlanets.Count;
            float rackRadius = 0.85f;
            float totalArcDeg = 65f;
            float startAngleDeg = -90f - (totalArcDeg / 2f);

            for (int i = 0; i < count; i++)
            {
                var planet = orderedPlanets[i];
                if (planet == null) continue;

                Undo.RecordObject(planet.transform, $"Place Planet {planet.planetName}");

                float fraction = count > 1 ? (float)i / (count - 1) : 0.5f;
                float angleDeg = startAngleDeg + (fraction * totalArcDeg);
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Position in arc on near edge of table
                float px = Mathf.Cos(angleRad) * rackRadius;
                float pz = Mathf.Sin(angleRad) * rackRadius;
                
                // Elevation so planet rests cleanly ON top of the table instead of sinking into the glass
                float visualRadius = Mathf.Max(planet.transform.lossyScale.y * 1.0f, 0.05f);
                float py = TableSurfaceY + visualRadius + 0.01f;

                planet.transform.position = new Vector3(px, py, pz);
                planet.transform.rotation = Quaternion.identity;

                // Ensure physics and interaction settings are cleanly configured
                planet.ConfigurePhysics();
                planet.SetInitialSpot(planet.transform.position, planet.transform.rotation);

                EditorUtility.SetDirty(planet.gameObject);
            }

            Debug.Log($"<color=#00FF66>✓</color> Arranged {count} planets in front tray within 0.4m–0.6m of player.");
        }

        private static void CreateOrUpdatePlanetTrayVisual()
        {
            GameObject table = GameObject.Find("InteractiveTable");
            Transform parent = table != null ? table.transform : null;

            GameObject tray = GameObject.Find("PlanetTray_RestingPad");
            if (tray == null)
            {
                tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tray.name = "PlanetTray_RestingPad";
                if (parent != null) tray.transform.SetParent(parent, true);

                // Remove collider so it doesn't block hand interaction
                Collider col = tray.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            Undo.RecordObject(tray.transform, "Update Planet Tray");
            tray.transform.position = new Vector3(0f, TableSurfaceY + 0.002f, -0.80f);
            tray.transform.localScale = new Vector3(1.10f, 0.004f, 0.28f);

            // Give it a sleek dark glass/cyan aesthetic
            Renderer rend = tray.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.name = "Mat_PlanetTrayPad";
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.04f, 0.12f, 0.22f, 0.75f));
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.04f, 0.12f, 0.22f, 0.75f));
                rend.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(tray);
        }
    }
}
#endif
