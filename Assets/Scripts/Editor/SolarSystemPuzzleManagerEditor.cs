using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AdaptiveVR.Editor;

namespace AdaptiveVR.PlanetPuzzle
{
    /// <summary>
    /// Custom Inspector Editor for SolarSystemPuzzleManager.
    /// Provides one-click controls for Sequential Orbit Progression (Mercury -> Venus -> Earth...),
    /// faint celestial orbit appearance tuning, and scene generation.
    /// </summary>
    [CustomEditor(typeof(SolarSystemPuzzleManager))]
    public class SolarSystemPuzzleManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SolarSystemPuzzleManager manager = (SolarSystemPuzzleManager)target;

            // Header Banner
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(8);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = new Color(0.0f, 0.85f, 1.0f) }
            };
            GUILayout.Label("SOLAR SYSTEM PUZZLE MANAGER", headerStyle);
            GUILayout.Label("Sequential Orbit Progression & Faint Ring Appearance", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(8);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // Progress Banner
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            int total = manager.orbitSlots.Count > 0 ? manager.orbitSlots.Count : 8;
            string status = manager.isPuzzleComplete ? "PUZZLE COMPLETE! (ALL 8 PLACED)" : $"Progress: {manager.correctPlacementsCount} / {total} Correct";
            Color statusColor = manager.isPuzzleComplete ? new Color(0.1f, 0.85f, 0.4f) : new Color(1.0f, 0.75f, 0.2f);

            GUIStyle progressStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = statusColor }
            };
            GUILayout.Label(status, progressStyle);

            if (manager.sequentialProgression && !manager.isPuzzleComplete && manager.orbitSlots.Count > 0)
            {
                string targetPlanet = (manager.currentUnlockedOrbit <= 8)
                    ? manager.orbitSlots[Mathf.Clamp(manager.currentUnlockedOrbit - 1, 0, manager.orbitSlots.Count - 1)].expectedPreset.ToString()
                    : "Complete";

                GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.0f, 0.9f, 1.0f) }
                };
                GUILayout.Label($"Currently Active Target: Orbit {manager.currentUnlockedOrbit} ({targetPlanet})", subStyle);
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Sequential Progression Toggle
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("PROGRESSION SETTINGS", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool newSeq = EditorGUILayout.Toggle("Sequential Progression", manager.sequentialProgression);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Toggle Sequential Progression");
                manager.sequentialProgression = newSeq;
                manager.UpdateSequentialProgression();
                EditorUtility.SetDirty(manager);
            }
            EditorGUILayout.HelpBox("When enabled, Orbit 1 (Mercury) appears first. Orbit 2 (Venus) only unlocks once Mercury is placed correctly!", MessageType.None);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // Faint Ring Visual Tuning
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("FAINT ORBIT APPEARANCE", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float newScale = EditorGUILayout.Slider("Overall Table Scale", manager.scaleMultiplier, 0.2f, 2.5f);
            float newOpacity = EditorGUILayout.Slider("Ring Opacity (Faintness)", manager.orbitRingOpacity, 0.02f, 0.6f);
            float newEmission = EditorGUILayout.Slider("Glow Emission", manager.orbitRingEmission, 0.0f, 1.0f);
            float newWidth = EditorGUILayout.Slider("Ring Line Width", manager.orbitLineWidth, 0.002f, 0.015f);
            Color newColor = EditorGUILayout.ColorField("Ring Cosmic Tint", manager.orbitRingColor);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Change Orbit Appearance");
                manager.scaleMultiplier = newScale;
                manager.orbitRingOpacity = newOpacity;
                manager.orbitRingEmission = newEmission;
                manager.orbitLineWidth = newWidth;
                manager.orbitRingColor = newColor;
                EditorUtility.SetDirty(manager);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // Cognitive Load Visual Scaffolding (Planet Greying)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("COGNITIVE LOAD VISUAL SCAFFOLDING", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool newDimming = EditorGUILayout.Toggle("Planet Greying Active", manager.distractorDimmingActive);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Toggle Distractor Dimming");
                manager.SetDistractorDimming(newDimming);
                EditorUtility.SetDirty(manager);
            }

            GUI.backgroundColor = manager.distractorDimmingActive ? new Color(1.0f, 0.45f, 0.45f) : new Color(0.2f, 0.85f, 0.95f);
            if (GUILayout.Button(manager.distractorDimmingActive ? "Deactivate Planet Greying (Normal)" : "Test Planet Greying (Grey Others / Bright Target)", GUILayout.Height(30)))
            {
                Undo.RecordObject(manager, "Toggle Distractor Dimming");
                manager.SetDistractorDimming(!manager.distractorDimmingActive);
                EditorUtility.SetDirty(manager);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // Cognitive Scaffolding: Floating Planet Name Labels
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("PLANET NAME LABELS (FLOATING HINTS)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool newLabelsActive = EditorGUILayout.Toggle("Planet Labels Active", manager.planetLabelsActive);
            PlanetLabelMode newMode = (PlanetLabelMode)EditorGUILayout.EnumPopup("Label Mode", manager.currentPlanetLabelMode);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Toggle Planet Name Labels");
                manager.SetPlanetLabelsActive(newLabelsActive, newMode);
                EditorUtility.SetDirty(manager);
            }

            GUI.backgroundColor = manager.planetLabelsActive ? new Color(1.0f, 0.45f, 0.45f) : new Color(0.2f, 0.85f, 0.95f);
            if (GUILayout.Button(manager.planetLabelsActive ? "Deactivate Planet Labels (Hide)" : "Test Floating Planet Labels (Show)", GUILayout.Height(30)))
            {
                Undo.RecordObject(manager, "Toggle Planet Name Labels");
                manager.SetPlanetLabelsActive(!manager.planetLabelsActive, manager.currentPlanetLabelMode);
                EditorUtility.SetDirty(manager);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Displays subtle floating name tags directly above unplaced planets on the table. Automatically active in Low (Struggling) & Medium (Learning) states.", MessageType.None);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // OPTION A: ONE-CLICK STATIONARY VR SETUP
            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.95f);
            if (GUILayout.Button("⭐ OPTIMIZE FOR STATIONARY VR (OPTION A)", GUILayout.Height(38)))
            {
                StationaryScaleOptimizer.OptimizeForStationaryVR();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(6);

            // Primary Setup Action 1: Auto-Detect Custom Prefabs
            GUI.backgroundColor = new Color(0.2f, 0.65f, 0.95f);
            if (GUILayout.Button("1. Auto-Detect Scene Planets (Attach Grabbable)", GUILayout.Height(34)))
            {
                Undo.RecordObject(manager, "Auto-Detect Scene Planets");
                manager.AutoDetectScenePlanets();
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }

            GUILayout.Space(4);

            // Primary Setup Action 2: Generate 8 Orbit Rings & Sockets
            GUI.backgroundColor = new Color(0.15f, 0.8f, 0.45f);
            if (GUILayout.Button("2. Create 8 Orbit Rings & Sockets Around Sun", GUILayout.Height(36)))
            {
                Undo.RegisterFullObjectHierarchyUndo(manager.gameObject, "Create 8 Orbit Rings & Sockets");
                manager.CreateOrbitSocketsAroundSun();
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // Testing Actions: Scramble / Auto-Solve
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.5f, 0.75f);
            if (GUILayout.Button("Scramble Planets", GUILayout.Height(28)))
            {
                Undo.RecordObject(manager, "Scramble Planets");
                manager.ScramblePlanets();
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }

            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.2f);
            if (GUILayout.Button("Auto-Solve Puzzle", GUILayout.Height(28)))
            {
                Undo.RecordObject(manager, "Auto-Solve Puzzle");
                manager.AutoSolve();
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Default Inspector Fields
            DrawDefaultInspector();
        }
    }
}
