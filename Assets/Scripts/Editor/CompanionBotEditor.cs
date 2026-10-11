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

            GUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            float newScale = EditorGUILayout.Slider("Bot Model Scale", bot.botModelScale, 2f, 30f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bot, "Change Bot Model Scale");
                bot.botModelScale = newScale;
                bot.ApplyModelScale();
                EditorUtility.SetDirty(bot);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cute Size (8.5)"))
            {
                Undo.RecordObject(bot, "Set Bot Scale to 8.5");
                bot.botModelScale = 8.5f;
                bot.ApplyModelScale();
                EditorUtility.SetDirty(bot);
            }
            if (GUILayout.Button("Align Shoulder Height"))
            {
                Undo.RecordObject(bot, "Align Shoulder Height");
                bot.shoulderOffset = new Vector3(0.42f, -0.18f, 0.38f);
                EditorUtility.SetDirty(bot);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);

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

            GUILayout.Space(8);

            // Shoulder Distance & Positioning Box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Shoulder Distance & Head-Turn Isolation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adjust how far to the side the bot floats, and control head-turn isolation so the bot stays on your shoulder when you turn your head to look at it.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            float newSideDist = EditorGUILayout.Slider("Side Distance (X)", bot.shoulderSideDistance, 0.15f, 1.5f);
            float newFwdDist = EditorGUILayout.Slider("Forward Distance (Z)", bot.shoulderForwardDistance, -0.3f, 1.2f);
            float newHeight = EditorGUILayout.Slider("Height Offset (Y)", bot.shoulderHeight, -0.6f, 0.4f);
            float newMult = EditorGUILayout.Slider("Distance Multiplier", bot.shoulderDistanceMultiplier, 0.3f, 3.0f);
            bool newIsolate = EditorGUILayout.Toggle("Isolate Head Turn", bot.isolateHeadTurn);
            float newDeadzone = bot.isolateHeadTurn ? EditorGUILayout.Slider("Look Deadzone (°)", bot.headTurnDeadzone, 20f, 90f) : bot.headTurnDeadzone;

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bot, "Change Shoulder Distance Settings");
                bot.shoulderSideDistance = newSideDist;
                bot.shoulderForwardDistance = newFwdDist;
                bot.shoulderHeight = newHeight;
                bot.shoulderDistanceMultiplier = newMult;
                bot.isolateHeadTurn = newIsolate;
                bot.headTurnDeadzone = newDeadzone;
                bot.SyncShoulderControls();
                EditorUtility.SetDirty(bot);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Close (0.35m)"))
            {
                Undo.RecordObject(bot, "Set Shoulder Close");
                bot.shoulderSideDistance = 0.35f;
                bot.shoulderDistanceMultiplier = 1.0f;
                bot.SyncShoulderControls();
                EditorUtility.SetDirty(bot);
            }
            if (GUILayout.Button("Default (0.42m)"))
            {
                Undo.RecordObject(bot, "Set Shoulder Default");
                bot.shoulderSideDistance = 0.42f;
                bot.shoulderForwardDistance = 0.38f;
                bot.shoulderHeight = -0.18f;
                bot.shoulderDistanceMultiplier = 1.0f;
                bot.SyncShoulderControls();
                EditorUtility.SetDirty(bot);
            }
            if (GUILayout.Button("Wide (0.60m)"))
            {
                Undo.RecordObject(bot, "Set Shoulder Wide");
                bot.shoulderSideDistance = 0.60f;
                bot.shoulderDistanceMultiplier = 1.0f;
                bot.SyncShoulderControls();
                EditorUtility.SetDirty(bot);
            }
            if (GUILayout.Button("Far (0.85m)"))
            {
                Undo.RecordObject(bot, "Set Shoulder Far");
                bot.shoulderSideDistance = 0.85f;
                bot.shoulderDistanceMultiplier = 1.0f;
                bot.SyncShoulderControls();
                EditorUtility.SetDirty(bot);
            }
            EditorGUILayout.EndHorizontal();

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

                GUILayout.Space(6);
                EditorGUILayout.LabelField("Hint Presentation Testing", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.2f, 0.9f, 0.6f);
                if (GUILayout.Button("Test Hint (In Front -> Side)"))
                {
                    bot.ShowHint("Hello! Try placing Mercury in the first orbit close to the Sun.", 4.5f, forceInFront: true);
                }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Test Hint (At Shoulder)"))
                {
                    bot.ShowHint("Remember: Mercury is closest to the Sun!", 4.0f, forceInFront: false);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                string statusText = bot.HasGivenFirstHint ? "First Hint Given: YES" : "First Hint Given: NO";
                EditorGUILayout.LabelField(statusText, EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Reset First Hint", GUILayout.Width(130)))
                {
                    bot.ResetFirstHintState();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"State: {bot.PresentationState}", EditorStyles.miniLabel);

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
