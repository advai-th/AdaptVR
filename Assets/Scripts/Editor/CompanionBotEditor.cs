using UnityEngine;
using UnityEditor;
using AdaptiveVR.Companion;

namespace AdaptiveVR.Companion.Editor
{
    /// <summary>
    /// Custom Inspector Editor for CompanionBot.
    /// Provides one-click setup for the bot's glowing blue eyes and mouth directly on the face (NO visor/background),
    /// plus live runtime test buttons for facial expressions, speech bubble hints, and shoulder side switching.
    /// </summary>
    [CustomEditor(typeof(CompanionBot))]
    public class CompanionBotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            CompanionBot bot = (CompanionBot)target;

            // Header Banner
            GUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(6);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.0f, 0.88f, 1.0f) }
            };
            GUILayout.Label("CUTE FLOATING COMPANION BOT", headerStyle);
            GUILayout.Label("Gentle Over-Shoulder Follower • Blue Eyes & Mouth • No Visor", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            // One-Click Setup Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Setup & Scene Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click below to attach the face features directly onto your model, setup the soft floating halo, or preview the speech bubble in Scene view for easy positioning.", MessageType.Info);

            GUI.backgroundColor = new Color(0.1f, 0.75f, 0.95f);
            if (GUILayout.Button("Auto-Setup Eyes & Mouth on Face", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(bot.gameObject, "Setup Bot Face Features");
                bot.EnsureFaceRenderers();
                EditorUtility.SetDirty(bot);
            }

            GUILayout.Space(2);
            bool hasHalo = bot.transform.Find("FloatingGlow") != null;
            if (hasHalo)
            {
                GUI.backgroundColor = new Color(1.0f, 0.42f, 0.42f);
                if (GUILayout.Button("Delete / Remove Floating Halo", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(bot.gameObject, "Remove Floating Halo");
                    bot.enableFloatingGlow = false;
                    bot.RemoveFloatingGlow();
                    EditorUtility.SetDirty(bot);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.0f, 0.85f, 0.75f);
                if (GUILayout.Button("Auto-Setup Soft Floating Halo at Bottom", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(bot.gameObject, "Setup Soft Floating Halo");
                    bot.enableFloatingGlow = true;
                    bot.EnsureFloatingGlow();
                    EditorUtility.SetDirty(bot);
                }
            }

            GUILayout.Space(2);
            bool hasBubble = bot.transform.Find("BotSpeechBubble") != null;
            if (hasBubble)
            {
                GUI.backgroundColor = bot.previewBubbleInEditor ? new Color(1.0f, 0.65f, 0.2f) : new Color(0.2f, 0.65f, 1.0f);
                string bubbleBtnText = bot.previewBubbleInEditor ? "Hide Speech Bubble in Scene" : "Show Speech Bubble in Scene (For Positioning)";
                if (GUILayout.Button(bubbleBtnText, GUILayout.Height(28)))
                {
                    Undo.RecordObject(bot, "Toggle Speech Bubble Preview");
                    bot.previewBubbleInEditor = !bot.previewBubbleInEditor;
                    bot.EnsureSpeechBubble();
                    EditorUtility.SetDirty(bot);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.2f, 0.65f, 1.0f);
                if (GUILayout.Button("Auto-Setup Speech Bubble in Scene", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(bot.gameObject, "Setup Speech Bubble");
                    bot.enableSpeechBubble = true;
                    bot.previewBubbleInEditor = true;
                    bot.EnsureSpeechBubble();
                    EditorUtility.SetDirty(bot);
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Live Testing Actions (Play Mode)
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Live Runtime Testing (Face & Hands)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Testing an expression dynamically animates both facial features and the hands.", MessageType.None);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Celebrate (^ ^)"))
                {
                    bot.Celebrate();
                }
                if (GUILayout.Button("Thinking (?)"))
                {
                    bot.ExpressThinking(3f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Talk / Mouth Flap"))
                {
                    bot.SetExpression(BotExpression.Talking, 3f);
                }
                if (GUILayout.Button("Reset to Idle"))
                {
                    bot.SetExpression(BotExpression.Idle);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
                if (GUILayout.Button("Show Test Hint Bubble"))
                {
                    bot.ShowHint("Hello! Try placing Mercury in the first orbit close to the Sun.", 4.5f);
                }

                GUILayout.Space(4);
                string shoulderText = bot.followRightShoulder ? "Switch to Left Shoulder" : "Switch to Right Shoulder";
                if (GUILayout.Button(shoulderText))
                {
                    bot.SetShoulder(!bot.followRightShoulder);
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }

            // Draw Default Inspector properties
            DrawDefaultInspector();
        }
    }
}
