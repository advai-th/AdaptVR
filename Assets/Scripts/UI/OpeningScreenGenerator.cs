using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveVR.UI
{
    /// <summary>
    /// Standalone generator script that automatically builds the entire Opening Screen World-Space UI
    /// hierarchy with one click or on demand.
    /// Completely separated from Observatory / Environment generation.
    /// </summary>
    [ExecuteAlways]
    public class OpeningScreenGenerator : MonoBehaviour
    {
        [Header("Visual Customization")]
        [Tooltip("Optional Logo Sprite (e.g. drag vr-logo-dark from your Assets).")]
        public Sprite logoSprite;

        [Tooltip("Size of the World-Space canvas in virtual pixels (Width, Height).")]
        public Vector2 canvasPixelSize = new Vector2(800f, 600f);

        [Tooltip("VR World scale multiplier (0.002 creates an approx 1.6m x 1.2m comfortable board).")]
        public float canvasWorldScale = 0.002f;

        [Tooltip("Initial placement offset relative to this GameObject (in meters).")]
        public Vector3 localPositionOffset = new Vector3(0f, 1.5f, 2.0f);

        [Header("Colors (Sleek Dark Glass Theme)")]
        public Color panelBackgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.92f);
        public Color panelBorderColor = new Color(0.0f, 0.85f, 1.0f, 0.35f);
        public Color titleTextColor = new Color(0.6f, 0.75f, 0.88f);
        public Color codeTextColor = new Color(0.0f, 0.95f, 1.0f);
        public Color statusTextColor = new Color(0.7f, 0.75f, 0.8f);
        public Color welcomeTextColor = new Color(0.1f, 0.95f, 0.5f);

        [Header("Generated References (Auto-Populated)")]
        public GameObject generatedCanvasObject;
        public OpeningScreenManager generatedScreenManager;

        /// <summary>
        /// One-click generator accessible via right-click in Inspector or component menu.
        /// </summary>
        [ContextMenu("Generate Opening Screen UI")]
        public void GenerateOpeningScreen()
        {
            ClearExistingGeneratedUI();

            // 1. Root World-Space Canvas
            GameObject canvasObj = new GameObject("OpeningScreenCanvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = localPositionOffset;
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * canvasWorldScale;

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
            }

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;

            canvasObj.AddComponent<GraphicRaycaster>();
            CanvasGroup rootCanvasGroup = canvasObj.AddComponent<CanvasGroup>();

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = canvasPixelSize;

            // 2. Outer Border Frame (Sleek Glassmorphism border)
            GameObject borderObj = new GameObject("BorderFrame");
            borderObj.transform.SetParent(canvasObj.transform, false);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = panelBorderColor;
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;

            // 3. Dark Background Panel
            GameObject panelObj = new GameObject("BackgroundPanel");
            panelObj.transform.SetParent(borderObj.transform, false);
            Image panelImg = panelObj.AddComponent<Image>();
            panelImg.color = panelBackgroundColor;
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(4, 4);
            panelRect.offsetMax = new Vector2(-4, -4);

            // 4. Logo Image
            GameObject logoObj = new GameObject("LogoImage");
            logoObj.transform.SetParent(panelObj.transform, false);
            Image logoImg = logoObj.AddComponent<Image>();
            if (logoSprite != null)
            {
                logoImg.sprite = logoSprite;
                logoImg.preserveAspect = true;
            }
            else
            {
                logoImg.color = new Color(0.0f, 0.85f, 1.0f, 0.15f); // Subtle placeholder glow if no sprite yet
            }
            RectTransform logoRect = logoObj.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 0.75f);
            logoRect.anchorMax = new Vector2(0.5f, 0.75f);
            logoRect.sizeDelta = new Vector2(360f, 120f);
            logoRect.anchoredPosition = Vector2.zero;

            // Optional Fallback Text under/over logo if sprite is empty
            if (logoSprite == null)
            {
                GameObject logoTextObj = new GameObject("LogoFallbackText");
                logoTextObj.transform.SetParent(logoObj.transform, false);
                Text logoText = logoTextObj.AddComponent<Text>();
                logoText.text = "ADAPT VR";
                logoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                logoText.fontSize = 44;
                logoText.fontStyle = FontStyle.Bold;
                logoText.alignment = TextAnchor.MiddleCenter;
                logoText.color = codeTextColor;
                RectTransform logoTextRect = logoTextObj.GetComponent<RectTransform>();
                logoTextRect.anchorMin = Vector2.zero;
                logoTextRect.anchorMax = Vector2.one;
                logoTextRect.sizeDelta = Vector2.zero;
            }

            // 5. Pairing Container (Empty GameObject holding pairing UI)
            GameObject pairingContainer = new GameObject("PairingContainer");
            pairingContainer.transform.SetParent(panelObj.transform, false);
            RectTransform pairingRect = pairingContainer.AddComponent<RectTransform>();
            pairingRect.anchorMin = new Vector2(0f, 0f);
            pairingRect.anchorMax = new Vector2(1f, 0.6f);
            pairingRect.offsetMin = Vector2.zero;
            pairingRect.offsetMax = Vector2.zero;

            // 5a. "HEADSET PAIRING CODE" Label
            GameObject titleObj = new GameObject("PairingTitle");
            titleObj.transform.SetParent(pairingContainer.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "HEADSET PAIRING CODE";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = titleTextColor;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.82f);
            titleRect.anchorMax = new Vector2(0.5f, 0.82f);
            titleRect.sizeDelta = new Vector2(600f, 32f);

            // 5b. Pairing Code Display Pad
            GameObject codePadObj = new GameObject("CodePad");
            codePadObj.transform.SetParent(pairingContainer.transform, false);
            Image codePadImg = codePadObj.AddComponent<Image>();
            codePadImg.color = new Color(0.0f, 0.4f, 0.6f, 0.18f);
            RectTransform codePadRect = codePadObj.GetComponent<RectTransform>();
            codePadRect.anchorMin = new Vector2(0.5f, 0.52f);
            codePadRect.anchorMax = new Vector2(0.5f, 0.52f);
            codePadRect.sizeDelta = new Vector2(340f, 95f);

            // 5c. Large 4-character Code Text
            GameObject codeTextObj = new GameObject("PairingCodeText");
            codeTextObj.transform.SetParent(codePadObj.transform, false);
            Text codeText = codeTextObj.AddComponent<Text>();
            codeText.text = "----";
            codeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            codeText.fontSize = 64;
            codeText.fontStyle = FontStyle.Bold;
            codeText.alignment = TextAnchor.MiddleCenter;
            codeText.color = codeTextColor;
            RectTransform codeTextRect = codeTextObj.GetComponent<RectTransform>();
            codeTextRect.anchorMin = Vector2.zero;
            codeTextRect.anchorMax = Vector2.one;
            codeTextRect.sizeDelta = Vector2.zero;

            // 5d. Status / Instruction Text
            GameObject statusObj = new GameObject("StatusMessageText");
            statusObj.transform.SetParent(pairingContainer.transform, false);
            Text statusText = statusObj.AddComponent<Text>();
            statusText.text = "Connecting to AdaptVR backend...";
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.fontSize = 18;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = statusTextColor;
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.18f);
            statusRect.anchorMax = new Vector2(0.5f, 0.18f);
            statusRect.sizeDelta = new Vector2(650f, 40f);

            // 6. Welcome Message Text (Centered, shown when teacher dispatches session)
            GameObject welcomeObj = new GameObject("WelcomeMessageText");
            welcomeObj.transform.SetParent(panelObj.transform, false);
            Text welcomeText = welcomeObj.AddComponent<Text>();
            welcomeText.text = "WELCOME\nStudent Name\n\nStarting Session...";
            welcomeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            welcomeText.fontSize = 24;
            welcomeText.alignment = TextAnchor.MiddleCenter;
            welcomeText.color = welcomeTextColor;
            welcomeText.lineSpacing = 1.2f;
            RectTransform welcomeRect = welcomeObj.GetComponent<RectTransform>();
            welcomeRect.anchorMin = new Vector2(0f, 0f);
            welcomeRect.anchorMax = new Vector2(1f, 0.6f);
            welcomeRect.offsetMin = Vector2.zero;
            welcomeRect.offsetMax = Vector2.zero;
            welcomeObj.SetActive(false); // Initially hidden

            // 7. Attach & Wire OpeningScreenManager Component
            OpeningScreenManager manager = canvasObj.AddComponent<OpeningScreenManager>();
            manager.mainCanvasGroup = rootCanvasGroup;
            manager.logoImage = logoImg;
            manager.pairingContainer = pairingContainer;
            manager.pairingCodeText = codeText;
            manager.statusMessageText = statusText;
            manager.welcomeMessageText = welcomeText;
            manager.faceCameraOnStart = true;
            manager.spawnDistance = 2.0f;

            generatedCanvasObject = canvasObj;
            generatedScreenManager = manager;

            Debug.Log("<color=#00FF88><b>[OpeningScreenGenerator]</b> Generated Opening Screen World-Space UI hierarchy successfully!</color>");
        }

        /// <summary>
        /// Clears any previously generated OpeningScreenCanvas child objects.
        /// </summary>
        [ContextMenu("Clear Opening Screen UI")]
        public void ClearExistingGeneratedUI()
        {
            Transform existing = transform.Find("OpeningScreenCanvas");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
            generatedCanvasObject = null;
            generatedScreenManager = null;
        }
    }
}
