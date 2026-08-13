using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AdaptiveVR.Environment
{
    /// <summary>
    /// Custom Inspector Editor for ObservatoryGenerator.
    /// Provides scene generation controls, manual edit extraction, and prefab exporting directly in Unity.
    /// </summary>
    [CustomEditor(typeof(ObservatoryGenerator))]
    public class ObservatoryGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ObservatoryGenerator generator = (ObservatoryGenerator)target;

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
            GUILayout.Label("ADAPTIVE SOLAR SYSTEM OBSERVATORY", headerStyle);
            GUILayout.Label("Meta Quest 2 Standalone VR 3D Environment Generator", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(8);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Primary Generation Buttons
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.15f, 0.75f, 0.5f);
            if (GUILayout.Button("Generate Observatory", GUILayout.Height(36)))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Observatory Environment");
                generator.GenerateObservatory();
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
            if (GUILayout.Button("Clear Observatory", GUILayout.Height(36)))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Observatory Environment");
                generator.ClearObservatory();
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Secondary Management Buttons (Save Edits to Script & Export to Prefab)
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.25f, 0.55f, 0.85f);
            if (GUILayout.Button("Save Manual Edits To Script", GUILayout.Height(30)))
            {
                Undo.RecordObject(generator, "Save Manual Scene Edits");
                generator.SaveManualEditsToScript();
                EditorUtility.SetDirty(generator);
            }

            GUI.backgroundColor = new Color(0.9f, 0.65f, 0.2f);
            if (GUILayout.Button("Save Environment as Prefab", GUILayout.Height(30)))
            {
                generator.SaveEnvironmentAsPrefab();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Draw Default Inspector Properties
            DrawDefaultInspector();

            GUILayout.Space(15);
            EditorGUILayout.HelpBox(
                "How to preserve manual Unity scene changes:\n" +
                "1. If you manually moved/scaled objects in Unity, click 'Save Manual Edits To Script' to update your Inspector values.\n" +
                "2. Click 'Save Environment as Prefab' to save your customized room into 'Assets/Prefabs/AdaptiveSolarSystemObservatory.prefab'.",
                MessageType.Info);
        }
    }
}
