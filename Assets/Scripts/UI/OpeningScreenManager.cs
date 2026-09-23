using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AdaptiveVR.Networking;

namespace AdaptiveVR.UI
{
    /// <summary>
    /// Manages the opening sequence in VR:
    /// 1. Displays the AdaptVR Logo with a smooth fade-in.
    /// 2. Transitions to the Pairing Code screen with real-time connection status.
    /// 3. Upon receiving session assignment from the teacher dashboard, displays welcome message
    ///    and smoothly fades out the screen to reveal the observatory environment.
    /// 
    /// Place on a World-Space Canvas positioned in front of the XR Origin / Main Camera.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OpeningScreenManager : MonoBehaviour
    {
        [Header("Sequence Timings (Seconds)")]
        [Tooltip("How long the logo remains prominently displayed before transitioning to the pairing code.")]
        public float logoDisplayDuration = 2.5f;

        [Tooltip("Duration of cross-fades between states.")]
        public float fadeDuration = 0.8f;

        [Tooltip("How long the welcome confirmation is shown before dismissing the canvas.")]
        public float welcomeDuration = 2.5f;

        [Header("UI Panels & Visual Components")]
        [Tooltip("CanvasGroup of the main opening screen root (used to fade out the entire screen).")]
        public CanvasGroup mainCanvasGroup;

        [Tooltip("The Logo UI Image or container.")]
        public Image logoImage;

        [Tooltip("Container holding the pairing UI elements (code, instructions, status).")]
        public GameObject pairingContainer;

        [Tooltip("Large text display for the 4-character pairing code (e.g. 'A3X9').")]
        public Text pairingCodeText;

        [Tooltip("Sub-text displaying real-time instructions or connection status.")]
        public Text statusMessageText;

        [Tooltip("Welcome banner text shown when a student is paired.")]
        public Text welcomeMessageText;

        [Header("VR Comfort & Positioning")]
        [Header("VR Comfort & Head Following")]
        [Tooltip("If enabled, faces the user's camera once on startup so it appears directly in front of their eyes.")]
        public bool faceCameraOnStart = true;

        [Tooltip("Smoothly floats and follows the user's head gaze so it is always accessible even if they turn around.")]
        public bool smoothFollowHead = true;

        [Tooltip("Distance in meters to place the opening screen in front of the user camera.")]
        public float spawnDistance = 2.0f;

        [Tooltip("Eye-level height offset relative to headset.")]
        public float spawnHeightOffset = 0.0f;

        [Tooltip("Speed of smooth follow motion. Lower is gentler/floatier, higher is snappier.")]
        [Range(1f, 10f)]
        public float followSpeed = 3.5f;

        [Tooltip("Angle tolerance in degrees before the UI starts following. Prevents micro-jitter for small head turns.")]
        [Range(5f, 60f)]
        public float deadzoneAngle = 25f;

        [Tooltip("Keep the board upright with the horizon (locks pitch and roll to prevent motion sickness).")]
        public bool keepUpright = true;

        [Header("Environment Reveal")]
        [Tooltip("The root GameObject of the environment to hide until session starts. (Assign in Inspector)")]
        public GameObject environmentRoot;

        [Tooltip("The root GameObject of the solar system puzzle to hide until session starts. (Assign in Inspector)")]
        public GameObject puzzleRoot;

        [Tooltip("If true, changes the camera background to a neutral color while waiting.")]
        public bool useNeutralBackground = true;

        [Tooltip("The neutral background color to show while the environment is hidden.")]
        public Color neutralBackgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        private Camera _mainCamera;
        private bool _isDismissed = false;
        private Vector3 _targetFollowPosition;
        private Quaternion _targetFollowRotation;

        private CameraClearFlags _originalClearFlags;
        private Color _originalBackgroundColor;
        private GameObject _foundEnvironmentRoot;
        private GameObject _foundPuzzleRoot;

        private void Awake()
        {
            if (mainCanvasGroup == null)
            {
                mainCanvasGroup = GetComponent<CanvasGroup>();
            }

            _mainCamera = Camera.main;
        }

        private void Start()
        {
            PositionInFrontOfPlayer();

            // Setup Environment Reveal
            _foundEnvironmentRoot = environmentRoot;
            _foundPuzzleRoot = puzzleRoot;

            if (_foundEnvironmentRoot != null)
            {
                _foundEnvironmentRoot.SetActive(false);
            }
            if (_foundPuzzleRoot != null)
            {
                _foundPuzzleRoot.SetActive(false);
            }

            if (useNeutralBackground && _mainCamera != null)
            {
                _originalClearFlags = _mainCamera.clearFlags;
                _originalBackgroundColor = _mainCamera.backgroundColor;
                _mainCamera.clearFlags = CameraClearFlags.SolidColor;
                _mainCamera.backgroundColor = neutralBackgroundColor;
            }

            // Set initial UI state
            if (pairingContainer != null) pairingContainer.SetActive(false);
            if (welcomeMessageText != null) welcomeMessageText.gameObject.SetActive(false);

            // Hook into AdaptVRBackendClient events
            HookBackendClient();

            // Start opening sequence coroutine
            StartCoroutine(OpeningSequenceCoroutine());
        }

        private void LateUpdate()
        {
            if (!smoothFollowHead || _isDismissed) return;
            UpdateHeadFollow();
        }

        private void OnDestroy()
        {
            UnhookBackendClient();
        }

        #region VR Alignment & Lazy Follow

        /// <summary>
        /// Positions and orients this World Space Canvas comfortably in front of the user's current gaze direction.
        /// </summary>
        public void PositionInFrontOfPlayer()
        {
            if (!faceCameraOnStart) return;

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector3 forward = _mainCamera.transform.forward;
            if (keepUpright)
            {
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
            }

            Vector3 targetPos = _mainCamera.transform.position + (forward * spawnDistance);
            targetPos.y = _mainCamera.transform.position.y + spawnHeightOffset;

            transform.position = targetPos;
            _targetFollowPosition = targetPos;

            Vector3 lookDir = transform.position - _mainCamera.transform.position;
            if (keepUpright) lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
                _targetFollowRotation = transform.rotation;
            }
        }

        private void UpdateHeadFollow()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // Calculate current gaze direction from camera to canvas
            Vector3 camPos = _mainCamera.transform.position;
            Vector3 camForward = _mainCamera.transform.forward;

            if (keepUpright)
            {
                camForward.y = 0f;
                if (camForward.sqrMagnitude < 0.001f) camForward = Vector3.forward;
                camForward.Normalize();
            }

            Vector3 toCanvas = transform.position - camPos;
            if (keepUpright) toCanvas.y = 0f;
            toCanvas.Normalize();

            float angleDiff = Vector3.Angle(camForward, toCanvas);

            // If the user's head turned past the deadzone threshold, recompute target position
            if (angleDiff > deadzoneAngle)
            {
                Vector3 desiredPos = camPos + (camForward * spawnDistance);
                desiredPos.y = camPos.y + spawnHeightOffset;
                _targetFollowPosition = desiredPos;

                Vector3 desiredLookDir = _targetFollowPosition - camPos;
                if (keepUpright) desiredLookDir.y = 0f;
                if (desiredLookDir.sqrMagnitude > 0.001f)
                {
                    _targetFollowRotation = Quaternion.LookRotation(desiredLookDir);
                }
            }

            // Smoothly interpolate position and rotation towards target
            if ((transform.position - _targetFollowPosition).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.Lerp(transform.position, _targetFollowPosition, Time.deltaTime * followSpeed);
            }

            if (Quaternion.Angle(transform.rotation, _targetFollowRotation) > 0.1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, _targetFollowRotation, Time.deltaTime * followSpeed);
            }
        }

        #endregion

        #region Sequence Coroutine

        private IEnumerator OpeningSequenceCoroutine()
        {
            // 1. Initial State: Fade in the Logo
            if (mainCanvasGroup != null) mainCanvasGroup.alpha = 0f;
            yield return StartCoroutine(FadeCanvasGroup(mainCanvasGroup, 0f, 1f, fadeDuration));

            // Wait for logo display duration
            yield return new WaitForSeconds(logoDisplayDuration);

            // 2. Transition: Reveal Pairing Code & Status
            if (pairingContainer != null)
            {
                pairingContainer.SetActive(true);
            }

            RefreshStatusUI();
        }

        #endregion

        #region Backend Integration Callbacks

        private void HookBackendClient()
        {
            if (AdaptVRBackendClient.Instance == null) return;

            AdaptVRBackendClient.Instance.onConnected.AddListener(HandleConnected);
            AdaptVRBackendClient.Instance.onDeviceRegistered.AddListener(HandleDeviceRegistered);
            AdaptVRBackendClient.Instance.onDeviceLinked.AddListener(HandleDeviceLinked);
            AdaptVRBackendClient.Instance.onSessionAssigned.AddListener(HandleSessionAssigned);
            AdaptVRBackendClient.Instance.onDisconnected.AddListener(HandleDisconnected);
        }

        private void UnhookBackendClient()
        {
            if (AdaptVRBackendClient.Instance == null) return;

            AdaptVRBackendClient.Instance.onConnected.RemoveListener(HandleConnected);
            AdaptVRBackendClient.Instance.onDeviceRegistered.RemoveListener(HandleDeviceRegistered);
            AdaptVRBackendClient.Instance.onDeviceLinked.RemoveListener(HandleDeviceLinked);
            AdaptVRBackendClient.Instance.onSessionAssigned.RemoveListener(HandleSessionAssigned);
            AdaptVRBackendClient.Instance.onDisconnected.RemoveListener(HandleDisconnected);
        }

        private void HandleConnected()
        {
            RefreshStatusUI();
        }

        private void HandleDeviceRegistered(string code)
        {
            RefreshStatusUI();
        }

        private void HandleDeviceLinked(string deviceLabel)
        {
            RefreshStatusUI();
        }

        private void HandleDisconnected()
        {
            RefreshStatusUI();
        }

        public void RefreshStatusUI()
        {
            if (_isDismissed) return;

            UpdatePairingCodeDisplay();

            if (AdaptVRBackendClient.Instance == null)
            {
                SetStatusMessage("<color=#FF6666>AdaptVR Backend Client not found in scene.</color>");
                return;
            }

            var client = AdaptVRBackendClient.Instance;

            if (client.isLinked)
            {
                string labelText = !string.IsNullOrEmpty(client.deviceLabel) ? client.deviceLabel : "Teacher Dashboard";
                SetStatusMessage($"<color=#00FF88><b>● Connected to Dashboard ({labelText})</b></color>\n<size=16><color=#E0E0E0>Waiting for instructor to launch module...</color></size>");
            }
            else if (client.connectionState == ConnectionState.Registered)
            {
                SetStatusMessage("Ready to pair! Enter this code on the Teacher Dashboard.");
            }
            else if (client.connectionState == ConnectionState.Connected)
            {
                SetStatusMessage("<color=#00E5FF>Connected to backend. Registering headset...</color>");
            }
            else if (client.connectionState == ConnectionState.Connecting)
            {
                SetStatusMessage("Connecting to AdaptVR server...");
            }
            else if (client.connectionState == ConnectionState.Disconnected)
            {
                SetStatusMessage("<color=#FF6666>Connection lost. Reconnecting to server...</color>");
            }
        }

        private void HandleSessionAssigned(AssignedSessionData session)
        {
            if (_isDismissed) return;
            StartCoroutine(SessionAssignedTransitionCoroutine(session));
        }

        private IEnumerator SessionAssignedTransitionCoroutine(AssignedSessionData session)
        {
            _isDismissed = true;

            // Hide pairing container
            if (pairingContainer != null) pairingContainer.SetActive(false);

            // Display welcome message
            if (welcomeMessageText != null)
            {
                welcomeMessageText.gameObject.SetActive(true);
                string student = !string.IsNullOrEmpty(session.studentName) ? session.studentName : "Student";
                string grade = !string.IsNullOrEmpty(session.studentGrade) ? $" (Grade {session.studentGrade})" : "";
                string module = !string.IsNullOrEmpty(session.moduleName) ? session.moduleName : "Solar System";

                welcomeMessageText.text = $"<size=28>WELCOME</size>\n<b>{student}</b>{grade}\n\n<size=22>Module: {module}</size>\n<color=#00FFCC>Starting experience...</color>";
            }

            yield return new WaitForSeconds(welcomeDuration);

            // Reveal Environment
            if (_foundEnvironmentRoot != null)
            {
                _foundEnvironmentRoot.SetActive(true);
            }
            if (_foundPuzzleRoot != null)
            {
                _foundPuzzleRoot.SetActive(true);
            }

            if (useNeutralBackground && _mainCamera != null)
            {
                _mainCamera.clearFlags = _originalClearFlags;
                _mainCamera.backgroundColor = _originalBackgroundColor;
            }

            // Smoothly fade out the entire opening screen
            yield return StartCoroutine(FadeCanvasGroup(mainCanvasGroup, 1f, 0f, fadeDuration));

            // Deactivate canvas GameObject so it doesn't block interaction
            gameObject.SetActive(false);
        }

        #endregion

        #region Visual Helpers

        private void UpdatePairingCodeDisplay()
        {
            if (pairingCodeText != null && AdaptVRBackendClient.Instance != null)
            {
                pairingCodeText.text = AdaptVRBackendClient.Instance.pairingCode;
            }
        }

        private void SetStatusMessage(string message)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = message;
            }
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;

            float elapsed = 0f;
            cg.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            cg.alpha = to;
        }

        #endregion
    }
}
