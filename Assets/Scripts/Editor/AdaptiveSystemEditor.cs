#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AdaptiveVR.Adaptive;
using AdaptiveVR.Companion;

namespace AdaptiveVR.Editor
{
    /// <summary>
    /// Adds a "Force State" testing panel to the LearnerStateManager inspector.
    /// Coloured buttons let the researcher manually drive Low / Medium / High
    /// states in Play Mode without needing real VR interaction data.
    ///
    /// Also provides a one-click scene setup menu item that:
    ///   - Creates and wires the AdaptiveSystem GameObject
    ///   - Attaches PuzzleAdapter, CompanionPuzzleBridge, SpatialGuidanceBeacon
    ///   - Creates the AdaptiveGuidanceHUD World-Space Canvas
    ///   - Wires SolarSystemPuzzleManager events to CompanionPuzzleBridge
    ///   - Links all adapter references into AdaptationEngine and GuidanceAdapter
    /// </summary>
    [CustomEditor(typeof(LearnerStateManager))]
    public class AdaptiveSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var mgr = (LearnerStateManager)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Force State  (Play Mode Testing)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(1.0f, 0.35f, 0.25f);
                if (GUILayout.Button("⬇  LOW\n(Struggling)", GUILayout.Height(42)))
                    mgr.ForceStateLow();

                GUI.backgroundColor = new Color(1.0f, 0.80f, 0.15f);
                if (GUILayout.Button("◼  MEDIUM\n(Balanced)", GUILayout.Height(42)))
                    mgr.ForceStateMedium();

                GUI.backgroundColor = new Color(0.2f, 0.95f, 0.45f);
                if (GUILayout.Button("⬆  HIGH\n(Mastery)", GUILayout.Height(42)))
                    mgr.ForceStateHigh();

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play Mode to force states.", MessageType.Info);
            }

            EditorGUILayout.Space(8);

            // Live state readout
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Current State",
                mgr.currentState.ToString(),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Confidence",     mgr.lastConfidence.ToString("P1"));
            EditorGUILayout.LabelField("Model Version",  mgr.activeModelVersion);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Expected adaptation effects by state:\n" +
                "• LOW (Struggling) → Companion: Thinking + Tier-3 Hint bubble | Beacon: ON | " +
                "HUD: Full scaffolding | Snap radius: 0.55m | Orbit rings: bright | Planet labels: ON\n" +
                "• MED (Learning)   → Companion: Idle | Beacon: OFF | HUD: Standard | Snap radius: 0.35m | Planet labels: ON\n" +
                "• HIGH (Mastery)   → Companion: Happy smile | Beacon: OFF | HUD: Minimal | Snap radius: 0.22m tight | Planet labels: OFF",
                MessageType.None);
        }

        // ── Scene Setup Menu ──────────────────────────────────────────────────

        [MenuItem("Tools/AdaptVR/Setup Adaptive System in Current Scene")]
        public static void SetupAdaptiveSystemInScene()
        {
            // 1. Locate or create AdaptiveSystem root GameObject
            GameObject adaptiveRoot = GameObject.Find("AdaptiveSystem");
            if (adaptiveRoot == null)
            {
                adaptiveRoot = new GameObject("AdaptiveSystem");
                Undo.RegisterCreatedObjectUndo(adaptiveRoot, "Create AdaptiveSystem");
            }

            // Ensure core components on AdaptiveSystem
            var sessionMgr      = EnsureComponent<AdaptiveVR.DataCollection.Core.SessionManager>(adaptiveRoot);
            var stateMgr        = EnsureComponent<LearnerStateManager>(adaptiveRoot);
            var signalCollector = EnsureComponent<BehaviourSignalCollector>(adaptiveRoot);
            var loadBridge      = EnsureComponent<CognitiveLoadBridge>(adaptiveRoot);
            var engine          = EnsureComponent<AdaptationEngine>(adaptiveRoot);
            var updateMgr       = EnsureComponent<ModelUpdateManager>(adaptiveRoot);
            var dataCollector   = EnsureComponent<TrainingDataCollector>(adaptiveRoot);
            var guidanceAdapter = EnsureComponent<GuidanceAdapter>(adaptiveRoot);
            var beaconComp      = EnsureComponent<SpatialGuidanceBeacon>(adaptiveRoot);

            // Wire defaultModelAsset on CognitiveLoadBridge
            if (loadBridge != null && loadBridge.defaultModelAsset == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(
                    "Assets/AdaptVR/Models/cognitive_load_v1.onnx");
                if (asset != null)
                {
                    loadBridge.defaultModelAsset = asset;
                    EditorUtility.SetDirty(loadBridge);
                    Debug.Log("[AdaptVR] Assigned cognitive_load_v1 ModelAsset to CognitiveLoadBridge.");
                }
            }

            // Export .sentis model to StreamingAssets if needed
            if (loadBridge != null && loadBridge.defaultModelAsset != null)
            {
                string sentisPath = System.IO.Path.Combine(Application.streamingAssetsPath, "AdaptVR/cognitive_load_v1.sentis");
                if (!System.IO.File.Exists(sentisPath))
                {
                    try
                    {
                        var model = Unity.InferenceEngine.ModelLoader.Load(loadBridge.defaultModelAsset);
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sentisPath));
                        Unity.InferenceEngine.ModelWriter.Save(sentisPath, model);
                        Debug.Log("[AdaptVR] Exported cognitive_load_v1.sentis to StreamingAssets!");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[AdaptVR] Could not export .sentis model: " + ex.Message);
                    }
                }
            }

            // 2. Find SolarSystemPuzzleManager and CompanionBot
#if UNITY_2023_1_OR_NEWER
            var puzzleMgr = Object.FindFirstObjectByType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
            var bot       = Object.FindFirstObjectByType<CompanionBot>();
#else
            var puzzleMgr = Object.FindObjectOfType<AdaptiveVR.PlanetPuzzle.SolarSystemPuzzleManager>();
            var bot       = Object.FindObjectOfType<CompanionBot>();
#endif

            // 3. Setup PuzzleAdapter on SolarSystemPuzzleManager
            PuzzleAdapter puzzleAdapter = null;
            if (puzzleMgr != null)
            {
                puzzleAdapter = EnsureComponent<PuzzleAdapter>(puzzleMgr.gameObject);
                puzzleAdapter.puzzleManager = puzzleMgr;
                EditorUtility.SetDirty(puzzleAdapter);
            }

            // 4. Setup CompanionPuzzleBridge on CompanionBot
            CompanionPuzzleBridge companionBridge = null;
            if (bot != null)
            {
                companionBridge = EnsureComponent<CompanionPuzzleBridge>(bot.gameObject);

                if (puzzleMgr != null)
                {
                    companionBridge.puzzleManager = puzzleMgr;
                    if (companionBridge.hintPointTarget == null)
                    {
                        companionBridge.hintPointTarget = puzzleMgr.centerReference != null
                            ? puzzleMgr.centerReference
                            : puzzleMgr.transform;
                    }
                }
                EditorUtility.SetDirty(companionBridge);
            }

            // 5. Create AdaptiveGuidanceHUD if not already in scene
#if UNITY_2023_1_OR_NEWER
            var existingHUD = Object.FindFirstObjectByType<AdaptiveVR.UI.AdaptiveGuidanceHUD>();
#else
            var existingHUD = Object.FindObjectOfType<AdaptiveVR.UI.AdaptiveGuidanceHUD>();
#endif
            AdaptiveVR.UI.AdaptiveGuidanceHUD hud = existingHUD;
            if (hud == null)
            {
                GameObject hudObj = new GameObject("AdaptiveGuidanceHUD");
                Undo.RegisterCreatedObjectUndo(hudObj, "Create AdaptiveGuidanceHUD");
                hud = hudObj.AddComponent<AdaptiveVR.UI.AdaptiveGuidanceHUD>();
                if (puzzleMgr != null) hud.puzzleManager = puzzleMgr;
                EditorUtility.SetDirty(hud);
                Debug.Log("[AdaptVR] Created AdaptiveGuidanceHUD in scene.");
            }

            // 6. Wire SpatialGuidanceBeacon
            if (puzzleMgr != null) beaconComp.puzzleManager = puzzleMgr;
            EditorUtility.SetDirty(beaconComp);

            // 7. Wire GuidanceAdapter → Beacon + HUD (HUD & Beacon disabled to prevent VR occlusion)
            guidanceAdapter.spatialBeacon       = beaconComp;
            guidanceAdapter.enableSpatialBeacon = false;
            guidanceAdapter.adaptiveHUD         = hud;
            guidanceAdapter.enableAdaptiveHUD   = false;
            if (hud != null) hud.gameObject.SetActive(false);
            if (beaconComp != null) beaconComp.enabled = false;
            EditorUtility.SetDirty(guidanceAdapter);

            // 8. Wire adapters to AdaptationEngine
            engine.puzzleAdapter   = puzzleAdapter;
            engine.guidanceAdapter = guidanceAdapter;
            engine.companionBridge = companionBridge;
            EditorUtility.SetDirty(engine);

            // 9. Connect SignalCollector to PuzzleManager
            if (signalCollector != null && puzzleMgr != null)
            {
                signalCollector.puzzleManager = puzzleMgr;
                EditorUtility.SetDirty(signalCollector);
            }

            // 10. Wire SolarSystemPuzzleManager UnityEvents → CompanionPuzzleBridge
            if (puzzleMgr != null && companionBridge != null)
            {
                WireUnityEvent(puzzleMgr.onPlanetPlacedCorrectly,  companionBridge,
                    "CelebrateSuccess",       puzzleMgr);
                WireUnityEvent(puzzleMgr.onPlanetPlacedIncorrectly, companionBridge,
                    "EncourageOnError",       puzzleMgr);
                WireUnityEvent(puzzleMgr.onPuzzleCompleted,         companionBridge,
                    "CelebratePuzzleComplete", puzzleMgr);

                // Also notify HUD on correct placement
                if (hud != null)
                    WireUnityEvent(puzzleMgr.onPlanetPlacedCorrectly, hud,
                        "OnPlanetPlaced", puzzleMgr);

                EditorUtility.SetDirty(puzzleMgr);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("<color=#00FF66><b>[AdaptVR]</b> Full Adaptive System wired successfully! Press Ctrl+S to save.</color>");
            EditorUtility.DisplayDialog("AdaptVR Setup Complete",
                "Adaptive System fully configured!\n\n" +
                "✅ AdaptiveSystem GameObject created\n" +
                "✅ PuzzleAdapter — snap radius + orbit ring opacity\n" +
                "✅ CompanionPuzzleBridge — speech bubble + proactive hints\n" +
                "✅ SpatialGuidanceBeacon — glowing orbit beacon\n" +
                "✅ AdaptiveGuidanceHUD — floating task board\n" +
                "✅ GuidanceAdapter — beacon + HUD wired\n" +
                "✅ Puzzle events → Companion (celebrate / encourage)\n\n" +
                "Test with: Inspector → LearnerStateManager → Force State buttons\n" +
                "Remember to press Ctrl+S to save your scene.", "OK");
        }

        // ── Helper: Wire UnityEvent (avoids duplicate entries) ────────────────

        private static void WireUnityEvent(UnityEngine.Events.UnityEvent evt,
            object target, string methodName, Object dirtyObj)
        {
            int count = evt.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                if (evt.GetPersistentMethodName(i) == methodName &&
                    evt.GetPersistentTarget(i) == target as UnityEngine.Object)
                {
                    return; // Already wired
                }
            }

            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method == null)
            {
                Debug.LogWarning($"[AdaptVR] Could not find method '{methodName}' on {target.GetType().Name}");
                return;
            }

            var action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), target, method) as UnityEngine.Events.UnityAction;
            if (action != null)
            {
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(evt, action);
                Debug.Log($"[AdaptVR] Wired {target.GetType().Name}.{methodName} to {dirtyObj.name}");
            }
        }

        // ── Helper: Ensure Component ──────────────────────────────────────────

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null)
                comp = Undo.AddComponent<T>(go);
            return comp;
        }
    }
}
#endif
