using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using AdaptiveVR.DataCollection.Storage;

namespace AdaptiveVR.DataCollection.Core
{
    /// <summary>
    /// Central Session Manager controlling the lifecycle of a VR learning session.
    /// Coordinates session start/end, task transitions, pseudonymous participant IDs,
    /// and auto-saving telemetry data to JSON.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Global data collection configuration asset. If null, a default config will be instantiated.")]
        public DataCollectionConfig config;

        [Header("Session Identity Settings")]
        [Tooltip("Pseudonymous Learner ID (e.g. L001, P042). Never use real names or PII.")]
        public string learnerId = "L001";

        [Tooltip("Identifier of the current learning module (e.g. SOLAR_SYSTEM_01).")]
        public string moduleId = "SOLAR_SYSTEM_01";

        [Tooltip("Automatically start a session in Start() if not already active.")]
        public bool autoStartSessionOnPlay = false;

        [Header("Runtime State (Read-Only)")]
        [Tooltip("Is a learning session currently in progress?")]
        public bool isSessionActive = false;

        [Tooltip("Is the learning session currently paused?")]
        public bool isSessionPaused = false;

        [Tooltip("Unique ID generated for the active session.")]
        public string activeSessionId = string.Empty;

        [Tooltip("Current session data object containing metadata, tasks, and telemetry.")]
        public SessionData currentSession;

        [Tooltip("Currently active task within this session.")]
        public TaskData currentTask;

        [Header("Session Lifecycle Events")]
        public UnityEvent<string> onSessionStarted;
        public UnityEvent<string> onSessionEnded;
        public UnityEvent onSessionPaused;
        public UnityEvent onSessionResumed;
        public UnityEvent<TaskData> onTaskStarted;
        public UnityEvent<TaskData> onTaskCompleted;
        public UnityEvent<TaskData> onTaskFailed;

        // Internal timing tracking
        private float _sessionStartTime;
        private float _taskStartTime;
        private float _pausedDurationAccumulator;
        private float _pauseStartTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (config == null)
            {
                config = DataCollectionConfig.CreateDefault();
            }
        }

        private void Start()
        {
            if (autoStartSessionOnPlay && !isSessionActive)
            {
                StartSession();
            }
        }

        private void OnDestroy()
        {
            if (isSessionActive)
            {
                EndSession();
            }
        }

        private void OnApplicationQuit()
        {
            if (isSessionActive)
            {
                if (config != null && config.autoSaveOnApplicationQuit)
                {
                    EndSession();
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                PauseSession();
                if (isSessionActive && config != null && config.autoSaveOnApplicationQuit)
                {
                    SaveCurrentSessionToDisk();
                }
            }
            else
            {
                ResumeSession();
            }
        }

        #region Session Lifecycle API

        /// <summary>
        /// Starts a new data-collection learning session with a pseudonymous session ID or assigned backend UUID.
        /// </summary>
        public void StartSession(string customLearnerId = null, string customModuleId = null, string customSessionId = null)
        {
            if (isSessionActive)
            {
                Debug.LogWarning("[SessionManager] A session is already active. Ending current session first.");
                EndSession();
            }

            if (!string.IsNullOrEmpty(customLearnerId)) learnerId = customLearnerId;
            if (!string.IsNullOrEmpty(customModuleId)) moduleId = customModuleId;

            activeSessionId = !string.IsNullOrEmpty(customSessionId) ? customSessionId : GenerateSessionId();
            _sessionStartTime = Time.time;
            _pausedDurationAccumulator = 0f;

            currentSession = new SessionData();
            currentSession.session.sessionId = activeSessionId;
            currentSession.session.learnerId = learnerId;
            currentSession.session.moduleId = moduleId;
            currentSession.session.appVersion = Application.version;
            currentSession.session.unityVersion = Application.unityVersion;
            currentSession.session.platform = Application.platform.ToString();
            currentSession.session.startTime = DateTime.UtcNow.ToString("o");

            isSessionActive = true;
            isSessionPaused = false;

            if (config != null && config.verboseLogging)
            {
                Debug.Log($"<color=#00FFCC><b>[SessionManager]</b> Started Session: <b>{activeSessionId}</b> | Learner: <b>{learnerId}</b> | Module: <b>{moduleId}</b></color>");
            }

            onSessionStarted?.Invoke(activeSessionId);
        }

        /// <summary>
        /// Ends the current learning session, aggregates totals, and saves the final session JSON to local storage.
        /// </summary>
        public void EndSession()
        {
            if (!isSessionActive || currentSession == null) return;

            // Close active task if any
            if (currentTask != null)
            {
                CompleteTask(currentTask.taskId, currentTask.performance != null ? currentTask.performance.score : 100);
            }

            float rawDuration = Time.time - _sessionStartTime - _pausedDurationAccumulator;
            currentSession.session.durationSeconds = (float)Math.Round(Mathf.Max(rawDuration, 0f), 2);
            currentSession.session.endTime = DateTime.UtcNow.ToString("o");

            // Aggregate session summary metrics
            AggregateSessionSummary();

            isSessionActive = false;
            isSessionPaused = false;

            // Save to disk
            SaveCurrentSessionToDisk();

            if (config != null && config.verboseLogging)
            {
                Debug.Log($"<color=#FFD700><b>[SessionManager]</b> Ended Session: <b>{activeSessionId}</b> | Total Duration: {currentSession.session.durationSeconds}s | Tasks: {currentSession.tasks.Count}</color>");
            }

            onSessionEnded?.Invoke(activeSessionId);
            activeSessionId = string.Empty;
        }

        /// <summary>
        /// Pauses session and task timers (e.g. comfort menu opened or headset removed).
        /// </summary>
        public void PauseSession()
        {
            if (!isSessionActive || isSessionPaused) return;

            isSessionPaused = true;
            _pauseStartTime = Time.time;

            if (config != null && config.verboseLogging)
            {
                Debug.Log("[SessionManager] Session Paused.");
            }

            onSessionPaused?.Invoke();
        }

        /// <summary>
        /// Resumes session timers.
        /// </summary>
        public void ResumeSession()
        {
            if (!isSessionActive || !isSessionPaused) return;

            isSessionPaused = false;
            float pauseDuration = Time.time - _pauseStartTime;
            _pausedDurationAccumulator += Mathf.Max(pauseDuration, 0f);

            if (config != null && config.verboseLogging)
            {
                Debug.Log($"[SessionManager] Session Resumed (Paused for {pauseDuration:F2}s).");
            }

            onSessionResumed?.Invoke();
        }

        #endregion

        #region Task Lifecycle API

        /// <summary>
        /// Starts tracking a new discrete task within the session.
        /// </summary>
        public TaskData StartTask(string taskId, string taskName)
        {
            if (!isSessionActive)
            {
                StartSession();
            }

            // Close previous uncompleted task if still open
            if (currentTask != null && !currentTask.completed)
            {
                CompleteTask(currentTask.taskId, currentTask.performance.score);
            }

            currentTask = new TaskData(taskId, taskName);
            _taskStartTime = Time.time;

            currentSession.tasks.Add(currentTask);

            if (config != null && config.verboseLogging)
            {
                Debug.Log($"<color=#33CCFF><b>[SessionManager]</b> Started Task: <b>{taskId}</b> ('{taskName}')</color>");
            }

            onTaskStarted?.Invoke(currentTask);
            return currentTask;
        }

        /// <summary>
        /// Marks the current task as completed and records duration and final score.
        /// </summary>
        public void CompleteTask(string taskId, int score = 100)
        {
            if (currentTask == null) return;

            if (currentTask.taskId == taskId || string.IsNullOrEmpty(taskId))
            {
                float taskDuration = Time.time - _taskStartTime;
                currentTask.durationSeconds = (float)Math.Round(Mathf.Max(taskDuration, 0f), 2);
                currentTask.endTime = DateTime.UtcNow.ToString("o");
                currentTask.completed = true;
                if (currentTask.performance != null)
                {
                    currentTask.performance.completionTime = currentTask.durationSeconds;
                    currentTask.performance.score = score;
                }

                if (config != null && config.verboseLogging)
                {
                    Debug.Log($"<color=#00FF88><b>[SessionManager]</b> Completed Task: <b>{currentTask.taskId}</b> in {currentTask.durationSeconds}s | Score: {score}</color>");
                }

                onTaskCompleted?.Invoke(currentTask);

                if (config != null && config.autoSaveOnTaskComplete)
                {
                    SaveCurrentSessionToDisk();
                }

                currentTask = null;
            }
        }

        /// <summary>
        /// Marks the current task as failed.
        /// </summary>
        public void FailTask(string taskId, int score = 0)
        {
            if (currentTask == null) return;

            if (currentTask.taskId == taskId || string.IsNullOrEmpty(taskId))
            {
                float taskDuration = Time.time - _taskStartTime;
                currentTask.durationSeconds = (float)Math.Round(Mathf.Max(taskDuration, 0f), 2);
                currentTask.endTime = DateTime.UtcNow.ToString("o");
                currentTask.completed = false;
                if (currentTask.performance != null)
                {
                    currentTask.performance.completionTime = currentTask.durationSeconds;
                    currentTask.performance.score = score;
                }

                if (config != null && config.verboseLogging)
                {
                    Debug.Log($"<color=#FF4444><b>[SessionManager]</b> Failed Task: <b>{currentTask.taskId}</b> after {currentTask.durationSeconds}s</color>");
                }

                onTaskFailed?.Invoke(currentTask);

                if (config != null && config.autoSaveOnTaskComplete)
                {
                    SaveCurrentSessionToDisk();
                }

                currentTask = null;
            }
        }

        #endregion

        #region Internal Helpers & Summary Aggregation

        private void AggregateSessionSummary()
        {
            if (currentSession == null) return;

            int completed = 0;
            int failed = 0;
            int totalScore = 0;
            int totalErrors = 0;
            int totalRetries = 0;
            int totalHints = 0;
            int totalInteractions = 0;

            foreach (var task in currentSession.tasks)
            {
                if (task.completed) completed++;
                else failed++;

                if (task.performance != null)
                {
                    totalScore += task.performance.score;
                    totalErrors += task.performance.errors;
                    totalRetries += task.performance.retries;
                    totalHints += task.performance.hints;
                }

                if (task.interactions != null)
                {
                    totalInteractions += task.interactions.Count;
                }
            }

            currentSession.session.tasksCompleted = completed;
            currentSession.session.tasksFailed = failed;
            currentSession.session.finalScore = currentSession.tasks.Count > 0 ? (totalScore / currentSession.tasks.Count) : 0;

            currentSession.summary.totalTasks = currentSession.tasks.Count;
            currentSession.summary.tasksCompleted = completed;
            currentSession.summary.tasksFailed = failed;
            currentSession.summary.totalDurationSeconds = currentSession.session.durationSeconds;
            currentSession.summary.averageTaskDurationSeconds = currentSession.tasks.Count > 0 
                ? (float)Math.Round(currentSession.session.durationSeconds / currentSession.tasks.Count, 2) 
                : 0f;
            currentSession.summary.totalErrors = totalErrors;
            currentSession.summary.totalRetries = totalRetries;
            currentSession.summary.totalHints = totalHints;
            currentSession.summary.totalInteractions = totalInteractions;
            currentSession.summary.finalScore = currentSession.session.finalScore;
        }

        /// <summary>
        /// Flushes the current in-memory session data to disk via JsonDataLogger.
        /// </summary>
        public bool SaveCurrentSessionToDisk()
        {
            if (currentSession == null) return false;
            return JsonDataLogger.SaveSession(currentSession, config, out _);
        }

        /// <summary>
        /// Generates an anonymous, timestamped session identifier.
        /// Format: SESSION_YYYYMMDD_HHMMSS_A1B2
        /// </summary>
        private static string GenerateSessionId()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string randomSuffix = UnityEngine.Random.Range(0x1000, 0xFFFF).ToString("X4");
            return $"SESSION_{timestamp}_{randomSuffix}";
        }

        #endregion

        #region Editor Context Menu Helpers

        [ContextMenu("Test Start & Save Session")]
        public void TestStartAndSaveSession()
        {
            StartSession("TEST_LEARNER_01", "SOLAR_SYSTEM_MODULE");
            StartTask("TASK_01", "Mercury Orbit Placement");
            CompleteTask("TASK_01", 100);
            StartTask("TASK_02", "Venus Orbit Placement");
            CompleteTask("TASK_02", 90);
            EndSession();
        }

        [ContextMenu("Log Current Storage Path")]
        public void LogCurrentStoragePath()
        {
            string path = JsonDataLogger.GetStorageDirectory(config);
            Debug.Log($"<b>[SessionManager] Storage Directory:</b> {path}");
        }

        #endregion
    }
}
