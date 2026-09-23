using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using AdaptiveVR.DataCollection.Core;
using AdaptiveVR.DataCollection.Storage;

namespace AdaptiveVR.Networking
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Registered,
        SessionActive
    }

    /// <summary>
    /// Data received from the backend when a teacher dispatches a session via the dashboard.
    /// </summary>
    [Serializable]
    public class AssignedSessionData
    {
        public string type;
        public string sessionId;
        public string studentName;
        public string studentGrade;
        public string moduleName;
        public string moduleCategory;
        public string difficulty;
        public string startTime;
    }

    /// <summary>
    /// Connects the Unity VR client to the AdaptVR Node.js & WebSocket backend.
    /// Manages device registration, pairing codes, session assignment dispatch from the teacher dashboard,
    /// and real-time telemetry streaming over WebSockets and REST.
    /// 
    /// Note: Does not touch or depend on any UI generation, Editor, or Environment scripts.
    /// </summary>
    public class AdaptVRBackendClient : MonoBehaviour
    {
        public static AdaptVRBackendClient Instance { get; private set; }

        [Header("Backend Connection Configuration")]
        [Tooltip("Server host IP or domain (e.g., 'localhost', '127.0.0.1', or LAN IP like '192.168.1.100').")]
        public string serverHost = "localhost";

        [Tooltip("Port where the AdaptVR Node.js server is listening.")]
        public int serverPort = 5000;

        [Tooltip("Use secure WebSockets (wss://) and HTTPS. Keep false for local LAN development.")]
        public bool useSsl = false;

        [Tooltip("Automatically establish WebSocket connection on Start().")]
        public bool autoConnectOnStart = true;

        [Tooltip("Automatically attempt to reconnect if the connection drops.")]
        public bool autoReconnect = true;

        [Tooltip("Seconds to wait between reconnect attempts.")]
        [Range(1f, 15f)]
        public float reconnectIntervalSeconds = 4.0f;

        [Header("Device Identity & Pairing")]
        [Tooltip("6-digit pairing code chunked as XXX-XXX (e.g. '748-291'). If empty, one will be auto-generated.")]
        public string pairingCode = "";

        [Tooltip("Friendly label identifying this headset on the teacher dashboard (e.g. 'Quest-01').")]
        public string deviceLabel = "Quest-01";

        [Tooltip("Hardware model name reported to the backend.")]
        public string deviceModel = "Meta Quest 2";

        [Tooltip("Firmware or build version reported to the backend.")]
        public string firmwareVersion = "1.0.0";

        [Header("Optional VR HUD / In-Headset UI Text")]
        [Tooltip("Optional Text component to display the pairing code and status in-headset for the student.")]
        public Text pairingStatusText;

        [Header("Current Runtime State (Read-Only)")]
        [Tooltip("Current network connection status.")]
        public ConnectionState connectionState = ConnectionState.Disconnected;

        [Tooltip("Whether this device has been paired or linked to an active dashboard record.")]
        public bool isLinked = false;

        [Tooltip("Active session assigned by the dashboard.")]
        public AssignedSessionData currentAssignedSession;

        [Header("Network Events")]
        public UnityEvent onConnected;
        public UnityEvent onDisconnected;
        public UnityEvent<string> onDeviceRegistered;
        public UnityEvent<string> onDeviceLinked;
        public UnityEvent<AssignedSessionData> onSessionAssigned;

        // Internal WebSocket & Threading state
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Queue<Action> _mainThreadExecutionQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        private bool _isReconnecting = false;
        private bool _isQuitting = false;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Ensure pairing code exists
            InitializePairingCode();

            // Auto-populate device label if left as default
            if (string.IsNullOrEmpty(deviceLabel) || deviceLabel == "Quest-01")
            {
                string devName = SystemInfo.deviceName;
                if (!string.IsNullOrEmpty(devName) && devName != "Unknown")
                {
                    deviceLabel = devName;
                }
            }
        }

        private void Start()
        {
            UpdateStatusUI("Initializing...");

            HookSessionManagerEvents();

            if (autoConnectOnStart)
            {
                ConnectToBackend();
            }
        }

        private void Update()
        {
            // Dispatch background WebSocket messages onto Unity's main thread
            lock (_queueLock)
            {
                while (_mainThreadExecutionQueue.Count > 0)
                {
                    try
                    {
                        _mainThreadExecutionQueue.Dequeue()?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AdaptVRBackendClient] Error executing queued action: {ex.Message}");
                    }
                }
            }
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            Disconnect(true);
        }

        private void OnDestroy()
        {
            _isQuitting = true;
            Disconnect(true);
            UnhookSessionManagerEvents();
        }

        #endregion

        #region Pairing Code Generation

        private void InitializePairingCode()
        {
            if (!string.IsNullOrEmpty(pairingCode))
            {
                pairingCode = pairingCode.Trim().ToUpper();
                return;
            }

            // Check PlayerPrefs for a previously generated 6-digit chunked code (format: XXX-XXX)
            string savedCode = PlayerPrefs.GetString("AdaptVR_PairingCode", "");
            if (!string.IsNullOrEmpty(savedCode) && savedCode.Length == 7 && savedCode.Contains("-"))
            {
                pairingCode = savedCode;
                return;
            }

            // Generate a fresh 6-digit code chunked as XXX-XXX (e.g. 748-291)
            int part1 = UnityEngine.Random.Range(100, 1000);
            int part2 = UnityEngine.Random.Range(100, 1000);
            pairingCode = $"{part1}-{part2}";
            PlayerPrefs.SetString("AdaptVR_PairingCode", pairingCode);
            PlayerPrefs.Save();
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Base WebSocket URI constructed from configuration.
        /// </summary>
        public string GetWebSocketUri()
        {
            string scheme = useSsl ? "wss" : "ws";
            return $"{scheme}://{serverHost}:{serverPort}";
        }

        /// <summary>
        /// Base REST HTTP URI constructed from configuration.
        /// </summary>
        public string GetRestApiBaseUrl()
        {
            string scheme = useSsl ? "https" : "http";
            return $"{scheme}://{serverHost}:{serverPort}/api";
        }

        /// <summary>
        /// Connects asynchronously to the AdaptVR backend WebSocket server.
        /// </summary>
        public void ConnectToBackend()
        {
            if (connectionState == ConnectionState.Connecting || connectionState == ConnectionState.Connected || connectionState == ConnectionState.Registered)
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _ = ConnectInternalAsync();
        }

        private async Task ConnectInternalAsync()
        {
            connectionState = ConnectionState.Connecting;
            string wsUri = GetWebSocketUri();

            EnqueueMainThread(() =>
            {
                Debug.Log($"<color=#00E5FF><b>[AdaptVRBackendClient]</b> Connecting to {wsUri}...</color>");
                UpdateStatusUI($"Connecting to {serverHost}:{serverPort}...");
            });

            try
            {
                _webSocket = new ClientWebSocket();
                Uri uri = new Uri(wsUri);

                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

                EnqueueMainThread(() =>
                {
                    connectionState = ConnectionState.Connected;
                    Debug.Log($"<color=#00FF88><b>[AdaptVRBackendClient]</b> Connected successfully to AdaptVR backend!</color>");
                    onConnected?.Invoke();

                    // Immediately send device.register message
                    RegisterDevice();
                });

                // Start listening loop
                _ = ReceiveLoopAsync(_webSocket, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                EnqueueMainThread(() =>
                {
                    connectionState = ConnectionState.Disconnected;
                    Debug.LogWarning($"[AdaptVRBackendClient] Connection failed: {ex.Message}");
                    UpdateStatusUI($"Connection failed. Retrying...");
                    onDisconnected?.Invoke();

                    TriggerReconnect();
                });
            }
        }

        /// <summary>
        /// Disconnects from the backend gracefully.
        /// </summary>
        public void Disconnect(bool immediate = false)
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                if (_webSocket != null)
                {
                    if (!immediate && _webSocket.State == WebSocketState.Open)
                    {
                        _ = _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                    }
                    _webSocket.Dispose();
                    _webSocket = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AdaptVRBackendClient] Exception during disconnect: {ex.Message}");
            }
            finally
            {
                connectionState = ConnectionState.Disconnected;
            }
        }

        private void TriggerReconnect()
        {
            if (!autoReconnect || _isReconnecting || _isQuitting) return;

            StartCoroutine(ReconnectCoroutine());
        }

        private IEnumerator ReconnectCoroutine()
        {
            _isReconnecting = true;
            yield return new WaitForSeconds(reconnectIntervalSeconds);
            _isReconnecting = false;

            if (connectionState == ConnectionState.Disconnected && !_isQuitting)
            {
                ConnectToBackend();
            }
        }

        #endregion

        #region WebSocket Receive & Message Dispatching

        private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (ws != null && ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed connection", ct);
                            break;
                        }

                        string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        sb.Append(chunk);

                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string fullMessage = sb.ToString();
                        EnqueueMainThread(() => HandleIncomingMessage(fullMessage));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                EnqueueMainThread(() =>
                {
                    Debug.LogWarning($"[AdaptVRBackendClient] WebSocket read error: {ex.Message}");
                });
            }
            finally
            {
                EnqueueMainThread(() =>
                {
                    if (connectionState != ConnectionState.Disconnected)
                    {
                        connectionState = ConnectionState.Disconnected;
                        onDisconnected?.Invoke();
                        UpdateStatusUI("Disconnected from backend. Retrying...");
                        TriggerReconnect();
                    }
                });
            }
        }

        private void HandleIncomingMessage(string rawJson)
        {
            try
            {
                // Parse top-level message type
                var baseMsg = JsonUtility.FromJson<BaseWsMessage>(rawJson);
                if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
                {
                    return;
                }

                switch (baseMsg.type)
                {
                    case "connection.established":
                        Debug.Log("<color=#00FFCC>[AdaptVRBackendClient] Connection acknowledged by server.</color>");
                        break;

                    case "device.register.ack":
                        var ack = JsonUtility.FromJson<DeviceRegisterAckPayload>(rawJson);
                        connectionState = ConnectionState.Registered;
                        if (ack != null && !string.IsNullOrEmpty(ack.deviceLabel))
                        {
                            deviceLabel = ack.deviceLabel;
                        }
                        if (ack != null && ack.isLinked)
                        {
                            isLinked = true;
                            Debug.Log($"<color=#00FF66><b>[AdaptVRBackendClient] Device Registered & Linked!</b> ({deviceLabel})</color>");
                            UpdateStatusUI($"CONNECTED: {deviceLabel}\nWaiting for module assignment...");
                            onDeviceLinked?.Invoke(deviceLabel);
                        }
                        else
                        {
                            isLinked = false;
                            Debug.Log($"<color=#00FF66><b>[AdaptVRBackendClient] Device Registered!</b> Code: <b>{pairingCode}</b></color>");
                            UpdateStatusUI($"PAIRING CODE: {pairingCode}\nWaiting for Instructor to link device...");
                            onDeviceRegistered?.Invoke(pairingCode);
                        }
                        break;

                    case "device.linked":
                        var linkedMsg = JsonUtility.FromJson<DeviceLinkedWsMessage>(rawJson);
                        string lbl = (linkedMsg != null && !string.IsNullOrEmpty(linkedMsg.deviceLabel))
                            ? linkedMsg.deviceLabel
                            : (!string.IsNullOrEmpty(deviceLabel) ? deviceLabel : "Teacher Dashboard");
                        deviceLabel = lbl;
                        isLinked = true;
                        Debug.Log($"<color=#00E5FF><b>[AdaptVRBackendClient] Headset Connected to Teacher Dashboard!</b> ({lbl})</color>");
                        UpdateStatusUI($"CONNECTED: {lbl}\nWaiting for module assignment...");
                        onDeviceLinked?.Invoke(lbl);
                        break;

                    case "session.assigned":
                        HandleSessionAssigned(rawJson);
                        break;

                    default:
                        // Other messages (broadcasts, info, etc.)
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdaptVRBackendClient] Error processing message: {ex.Message}\nRaw: {rawJson}");
            }
        }

        private void HandleSessionAssigned(string rawJson)
        {
            try
            {
                AssignedSessionData assigned = JsonUtility.FromJson<AssignedSessionData>(rawJson);
                if (assigned == null || string.IsNullOrEmpty(assigned.sessionId))
                {
                    Debug.LogError("[AdaptVRBackendClient] Received invalid session.assigned payload.");
                    return;
                }

                currentAssignedSession = assigned;
                connectionState = ConnectionState.SessionActive;

                string studentInfo = !string.IsNullOrEmpty(assigned.studentName) ? assigned.studentName : "Student";
                string gradeInfo = !string.IsNullOrEmpty(assigned.studentGrade) ? $" (Grade {assigned.studentGrade})" : "";
                string moduleInfo = !string.IsNullOrEmpty(assigned.moduleName) ? assigned.moduleName : "Solar System";

                Debug.Log($"<color=#FFD700><b>[AdaptVRBackendClient] Session Assigned by Dashboard!</b></color>\n" +
                          $"  - Session ID: {assigned.sessionId}\n" +
                          $"  - Student: {studentInfo}{gradeInfo}\n" +
                          $"  - Module: {moduleInfo}");

                UpdateStatusUI($"SESSION ACTIVE\nStudent: {studentInfo}{gradeInfo}\nModule: {moduleInfo}");

                onSessionAssigned?.Invoke(assigned);

                // Start Session in SessionManager with the assigned backend SessionId
                if (SessionManager.Instance != null)
                {
                    SessionManager.Instance.StartSession(
                        customLearnerId: studentInfo,
                        customModuleId: moduleInfo,
                        customSessionId: assigned.sessionId
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdaptVRBackendClient] Exception in HandleSessionAssigned: {ex.Message}");
            }
        }

        #endregion

        #region Client Transmission & Telemetry (WebSockets)

        /// <summary>
        /// Sends the device.register packet to the backend to register the headset's pairing code.
        /// </summary>
        public void RegisterDevice()
        {
            int battery = Mathf.RoundToInt(SystemInfo.batteryLevel >= 0 ? SystemInfo.batteryLevel * 100 : 100);

            var reg = new DeviceRegisterPayload
            {
                type = "device.register",
                pairingCode = pairingCode,
                deviceLabel = deviceLabel,
                deviceModel = deviceModel,
                battery = battery,
                firmwareVersion = firmwareVersion,
                serialNumber = SystemInfo.deviceUniqueIdentifier
            };

            string json = JsonUtility.ToJson(reg);
            SendWebSocketMessage(json);
            Debug.Log($"[AdaptVRBackendClient] Sent device.register with Code: <b>{pairingCode}</b>");
        }

        /// <summary>
        /// Sends raw JSON over the active WebSocket connection.
        /// </summary>
        public void SendWebSocketMessage(string json)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                return;
            }

            _ = SendWebSocketMessageInternalAsync(json);
        }

        private async Task SendWebSocketMessageInternalAsync(string json)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource != null ? _cancellationTokenSource.Token : CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                EnqueueMainThread(() =>
                {
                    Debug.LogWarning($"[AdaptVRBackendClient] Failed to send WebSocket message: {ex.Message}");
                });
            }
        }

        #endregion

        #region SessionManager Lifecycle Hooking

        private void HookSessionManagerEvents()
        {
            if (SessionManager.Instance == null) return;

            SessionManager.Instance.onSessionStarted.AddListener(HandleSessionManagerStarted);
            SessionManager.Instance.onSessionEnded.AddListener(HandleSessionManagerEnded);
            SessionManager.Instance.onTaskStarted.AddListener(HandleTaskStarted);
            SessionManager.Instance.onTaskCompleted.AddListener(HandleTaskCompleted);
            SessionManager.Instance.onTaskFailed.AddListener(HandleTaskFailed);
        }

        private void UnhookSessionManagerEvents()
        {
            if (SessionManager.Instance == null) return;

            SessionManager.Instance.onSessionStarted.RemoveListener(HandleSessionManagerStarted);
            SessionManager.Instance.onSessionEnded.RemoveListener(HandleSessionManagerEnded);
            SessionManager.Instance.onTaskStarted.RemoveListener(HandleTaskStarted);
            SessionManager.Instance.onTaskCompleted.RemoveListener(HandleTaskCompleted);
            SessionManager.Instance.onTaskFailed.RemoveListener(HandleTaskFailed);
        }

        private void HandleSessionManagerStarted(string sessionId)
        {
            // Broadcast session start message over WebSocket
            var msg = new
            {
                type = "session.client.started",
                sessionId = sessionId,
                pairingCode = pairingCode
            };
            SendWebSocketMessage(FormatSimpleJson("session.client.started", sessionId));
        }

        private void HandleSessionManagerEnded(string sessionId)
        {
            connectionState = ConnectionState.Registered;
            UpdateStatusUI($"PAIRING CODE: {pairingCode}\nSession Completed. Waiting for instructor...");

            int score = SessionManager.Instance.currentSession != null
                ? Mathf.RoundToInt(SessionManager.Instance.currentSession.session.finalScore)
                : 90;

            // Notify backend via REST API to persist completion in PostgreSQL
            EndSessionOnBackend(sessionId, score, "completed");

            // Broadcast over WebSocket for dashboard live update
            SendWebSocketMessage(FormatSimpleJson("session.client.ended", sessionId));
        }

        private void HandleTaskStarted(TaskData task)
        {
            if (task == null) return;

            string sId = SessionManager.Instance != null ? SessionManager.Instance.activeSessionId : "";
            var payload = new TaskTelemetryWsPayload
            {
                type = "task.started",
                sessionId = sId,
                taskId = task.taskId,
                taskName = task.taskName
            };
            SendWebSocketMessage(JsonUtility.ToJson(payload));
        }

        private void HandleTaskCompleted(TaskData task)
        {
            if (task == null) return;

            string sId = SessionManager.Instance != null ? SessionManager.Instance.activeSessionId : "";
            var payload = new TaskTelemetryWsPayload
            {
                type = "task.completed",
                sessionId = sId,
                taskId = task.taskId,
                taskName = task.taskName,
                durationSeconds = task.durationSeconds,
                score = task.performance != null ? task.performance.score : 100,
                errors = task.performance != null ? task.performance.errors : 0,
                hints = task.performance != null ? task.performance.hints : 0
            };
            SendWebSocketMessage(JsonUtility.ToJson(payload));

            // Also mirror as interaction event in DB
            SendInteractionEvent(
                eventType: "task_complete",
                objectName: task.taskName,
                responseTime: task.durationSeconds,
                errorCount: task.performance != null ? task.performance.errors : 0
            );
        }

        private void HandleTaskFailed(TaskData task)
        {
            if (task == null) return;

            string sId = SessionManager.Instance != null ? SessionManager.Instance.activeSessionId : "";
            var payload = new TaskTelemetryWsPayload
            {
                type = "task.failed",
                sessionId = sId,
                taskId = task.taskId,
                taskName = task.taskName,
                durationSeconds = task.durationSeconds,
                errors = task.performance != null ? task.performance.errors : 0
            };
            SendWebSocketMessage(JsonUtility.ToJson(payload));
        }

        #endregion

        #region REST API Client Methods (UnityWebRequest)

        /// <summary>
        /// Sends an interaction event to POST /api/sessions/:id/events for persistent DB analytics.
        /// </summary>
        public void SendInteractionEvent(string eventType, string objectName, float responseTime, int errorCount, string interactionValueJson = null)
        {
            string sessionId = GetActiveSessionId();
            if (string.IsNullOrEmpty(sessionId)) return;

            string url = $"{GetRestApiBaseUrl()}/sessions/{sessionId}/events";

            var payload = new InteractionEventPostPayload
            {
                event_type = eventType,
                object_name = objectName,
                response_time = (float)Math.Round(responseTime, 2),
                error_count = errorCount
            };

            string jsonBody = JsonUtility.ToJson(payload);
            StartCoroutine(PostJsonCoroutine(url, jsonBody));
        }

        /// <summary>
        /// Sends ML Cognitive Load estimate to POST /api/sessions/:id/predictions.
        /// </summary>
        public void SendCognitiveLoadPrediction(string cognitiveLoad, float confidenceScore)
        {
            string sessionId = GetActiveSessionId();
            if (string.IsNullOrEmpty(sessionId)) return;

            string url = $"{GetRestApiBaseUrl()}/sessions/{sessionId}/predictions";

            var payload = new PredictionPostPayload
            {
                predicted_cognitive_load = cognitiveLoad,
                confidence_score = (float)Math.Round(confidenceScore, 3)
            };

            string jsonBody = JsonUtility.ToJson(payload);
            StartCoroutine(PostJsonCoroutine(url, jsonBody));
        }

        /// <summary>
        /// Sends an adaptive intervention record to POST /api/sessions/:id/adaptations.
        /// </summary>
        public void SendAdaptationEvent(string adaptationType, string previousDifficulty, string newDifficulty, string description)
        {
            string sessionId = GetActiveSessionId();
            if (string.IsNullOrEmpty(sessionId)) return;

            string url = $"{GetRestApiBaseUrl()}/sessions/{sessionId}/adaptations";

            var payload = new AdaptationPostPayload
            {
                adaptation_type = adaptationType,
                previous_difficulty = previousDifficulty,
                new_difficulty = newDifficulty,
                description = description
            };

            string jsonBody = JsonUtility.ToJson(payload);
            StartCoroutine(PostJsonCoroutine(url, jsonBody));
        }

        /// <summary>
        /// Marks session finished on backend database via POST /api/sessions/:id/end.
        /// </summary>
        public void EndSessionOnBackend(string sessionId, int finalScore, string completionStatus = "completed")
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            string url = $"{GetRestApiBaseUrl()}/sessions/{sessionId}/end";

            var payload = new SessionEndPostPayload
            {
                final_score = finalScore,
                completion_status = completionStatus
            };

            string jsonBody = JsonUtility.ToJson(payload);
            StartCoroutine(PostJsonCoroutine(url, jsonBody));
        }

        /// <summary>
        /// Diagnostic health check to verify the Node.js backend is accessible.
        /// </summary>
        public void CheckBackendHealth(Action<bool, string> onComplete = null)
        {
            string url = $"{GetRestApiBaseUrl()}/health";
            StartCoroutine(GetHealthCoroutine(url, onComplete));
        }

        private IEnumerator PostJsonCoroutine(string url, string jsonBody)
        {
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[AdaptVRBackendClient] POST {url} failed: {req.error} ({req.responseCode})");
                }
            }
        }

        private IEnumerator GetHealthCoroutine(string url, Action<bool, string> onComplete)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();

                bool ok = (req.result == UnityWebRequest.Result.Success);
                string resp = ok ? req.downloadHandler.text : req.error;
                onComplete?.Invoke(ok, resp);
            }
        }

        private string GetActiveSessionId()
        {
            if (currentAssignedSession != null && !string.IsNullOrEmpty(currentAssignedSession.sessionId))
            {
                return currentAssignedSession.sessionId;
            }
            if (SessionManager.Instance != null && !string.IsNullOrEmpty(SessionManager.Instance.activeSessionId))
            {
                return SessionManager.Instance.activeSessionId;
            }
            return null;
        }

        #endregion

        #region Helpers & UI Updates

        private void UpdateStatusUI(string text)
        {
            if (pairingStatusText != null)
            {
                pairingStatusText.text = text;
            }
        }

        private void EnqueueMainThread(Action action)
        {
            lock (_queueLock)
            {
                _mainThreadExecutionQueue.Enqueue(action);
            }
        }

        private static string FormatSimpleJson(string type, string sessionId)
        {
            return $"{{\"type\":\"{type}\",\"sessionId\":\"{sessionId}\"}}";
        }

        [ContextMenu("Test Health Check")]
        public void TestHealthCheck()
        {
            CheckBackendHealth((success, result) =>
            {
                if (success)
                {
                    Debug.Log($"<color=#00FF66><b>[Backend Health OK]</b></color>\n{result}");
                }
                else
                {
                    Debug.LogError($"[Backend Health FAILED] {result}");
                }
            });
        }

        [ContextMenu("Regenerate Pairing Code")]
        public void RegeneratePairingCode()
        {
            PlayerPrefs.DeleteKey("AdaptVR_PairingCode");
            pairingCode = "";
            InitializePairingCode();
            Debug.Log($"[AdaptVRBackendClient] New Pairing Code generated: {pairingCode}");
            if (connectionState == ConnectionState.Connected || connectionState == ConnectionState.Registered)
            {
                RegisterDevice();
            }
        }

        #endregion
    }

    #region Internal Serialization Data Contracts

    [Serializable]
    internal class BaseWsMessage
    {
        public string type;
    }

    [Serializable]
    internal class DeviceRegisterAckPayload
    {
        public string type;
        public string pairingCode;
        public string deviceLabel;
        public bool isLinked;
        public string message;
    }

    [Serializable]
    internal class DeviceRegisterPayload
    {
        public string type;
        public string pairingCode;
        public string deviceLabel;
        public string deviceModel;
        public int battery;
        public string firmwareVersion;
        public string serialNumber;
    }

    [Serializable]
    internal class TaskTelemetryWsPayload
    {
        public string type;
        public string sessionId;
        public string taskId;
        public string taskName;
        public float durationSeconds;
        public int score;
        public int errors;
        public int hints;
    }

    [Serializable]
    internal class InteractionEventPostPayload
    {
        public string event_type;
        public string object_name;
        public float response_time;
        public int error_count;
    }

    [Serializable]
    internal class PredictionPostPayload
    {
        public string predicted_cognitive_load;
        public float confidence_score;
    }

    [Serializable]
    internal class AdaptationPostPayload
    {
        public string adaptation_type;
        public string previous_difficulty;
        public string new_difficulty;
        public string description;
    }

    [Serializable]
    internal class DeviceLinkedWsMessage
    {
        public string type;
        public string deviceLabel;
        public string message;
    }

    [Serializable]
    internal class SessionEndPostPayload
    {
        public int final_score;
        public string completion_status;
    }

    #endregion
}
// Trigger Unity Recompile
