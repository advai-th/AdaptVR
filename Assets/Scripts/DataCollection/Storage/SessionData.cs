using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveVR.DataCollection.Storage
{
    /// <summary>
    /// Serializable representation of 3D vectors for JSON export.
    /// Compatible with Unity's JsonUtility and standard JSON parsers.
    /// </summary>
    [Serializable]
    public struct Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data(float x, float y, float z)
        {
            this.x = SanitizeFloat(x);
            this.y = SanitizeFloat(y);
            this.z = SanitizeFloat(z);
        }

        public Vector3Data(Vector3 v)
        {
            this.x = SanitizeFloat(v.x);
            this.y = SanitizeFloat(v.y);
            this.z = SanitizeFloat(v.z);
        }

        public static float SanitizeFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return (float)Math.Round(value, 3);
        }
    }

    /// <summary>
    /// Level 1 Telemetry: Single head movement sample from XR Origin Main Camera.
    /// </summary>
    [Serializable]
    public class HeadMovementSample
    {
        public float timestamp;
        public Vector3Data position;
        public Vector3Data rotation;

        public HeadMovementSample() { }

        public HeadMovementSample(float time, Vector3 pos, Vector3 rotEuler)
        {
            timestamp = (float)Math.Round(time, 3);
            position = new Vector3Data(pos);
            rotation = new Vector3Data(rotEuler);
        }
    }

    /// <summary>
    /// Level 1 Telemetry: Single player navigation sample from XR Origin root.
    /// </summary>
    [Serializable]
    public class NavigationSample
    {
        public float timestamp;
        public Vector3Data position;
        public bool isTeleport;

        public NavigationSample() { }

        public NavigationSample(float time, Vector3 pos, bool teleport = false)
        {
            timestamp = (float)Math.Round(time, 3);
            position = new Vector3Data(pos);
            isTeleport = teleport;
        }
    }

    /// <summary>
    /// Level 1 Telemetry: Single object interaction record.
    /// </summary>
    [Serializable]
    public class InteractionRecord
    {
        public float timestamp;
        public string objectId;
        public string interactionType; // grab, release, select, activate, place, manipulate, inspect
        public bool successful;
        public float duration;

        public InteractionRecord()
        {
            objectId = string.Empty;
            interactionType = string.Empty;
        }

        public InteractionRecord(float time, string objId, string type, bool success, float dur = 0f)
        {
            timestamp = (float)Math.Round(time, 3);
            objectId = objId ?? string.Empty;
            interactionType = type ?? string.Empty;
            successful = success;
            duration = (float)Math.Round(dur, 3);
        }
    }

    /// <summary>
    /// A single cognitive-load prediction result recorded during a task.
    /// Written by SessionManager.RecordLearnerState() and read by retrain.py
    /// for session-level analysis.
    /// </summary>
    [Serializable]
    public class LearnerStateRecord
    {
        /// <summary>Seconds since task start when the inference fired.</summary>
        public float  timestamp;

        /// <summary>Predicted state: "Low" | "Medium" | "High".</summary>
        public string predictedState;

        /// <summary>Model confidence (0–1).</summary>
        public float  confidence;

        /// <summary>ONNX model version that produced this prediction (e.g. "v2").</summary>
        public string modelVersion;

        /// <summary>Prediction index within this task (1-based).</summary>
        public int    predictionIndex;
    }

    /// <summary>
    /// Level 1 Telemetry: Discrete learning behavior event record.
    /// </summary>
    [Serializable]
    public class LearningEventRecord
    {
        public float timestamp;
        public string eventType;
        public string taskId;
        public string objectId;
        public string metadata;

        public LearningEventRecord()
        {
            eventType = string.Empty;
            taskId = string.Empty;
            objectId = string.Empty;
            metadata = string.Empty;
        }

        public LearningEventRecord(float time, string evtType, string tId, string objId = "", string meta = "")
        {
            timestamp = (float)Math.Round(time, 3);
            eventType = evtType ?? string.Empty;
            taskId = tId ?? string.Empty;
            objectId = objId ?? string.Empty;
            metadata = meta ?? string.Empty;
        }
    }

    /// <summary>
    /// Recorded idle period where player showed no significant movement or interaction.
    /// </summary>
    [Serializable]
    public class IdlePeriodRecord
    {
        public float start;
        public float end;
        public float duration;

        public IdlePeriodRecord() { }

        public IdlePeriodRecord(float start, float end, float duration)
        {
            this.start = (float)Math.Round(start, 3);
            this.end = (float)Math.Round(end, 3);
            this.duration = (float)Math.Round(duration, 3);
        }
    }

    /// <summary>
    /// Head-directed visual focus duration on learning objects.
    /// </summary>
    [Serializable]
    public class FocusRecord
    {
        public string objectId;
        public float startTime;
        public float endTime;
        public float focusDuration;

        public FocusRecord()
        {
            objectId = string.Empty;
        }

        public FocusRecord(string objId, float start, float end, float duration)
        {
            objectId = objId ?? string.Empty;
            startTime = (float)Math.Round(start, 3);
            endTime = (float)Math.Round(end, 3);
            focusDuration = (float)Math.Round(duration, 3);
        }
    }

    /// <summary>
    /// Level 2 Aggregated Metrics: Task performance statistics.
    /// </summary>
    [Serializable]
    public class TaskPerformance
    {
        public float completionTime;
        public int errors;
        public int retries;
        public int hints;
        public int score;
        public int correctActions;
        public int incorrectActions;
        public float responseTime;
        public int attemptCount;

        public TaskPerformance()
        {
            score = 100;
            attemptCount = 1;
        }
    }

    /// <summary>
    /// Level 2 Aggregated Metrics: Behavioral features extracted per task for ML.
    /// </summary>
    [Serializable]
    public class AggregatedFeatures
    {
        public float averageHeadSpeed;
        public float maxHeadSpeed;
        public int headDirectionChanges;
        public float totalRotation;
        public float averageRotationSpeed;
        public int largeRotationsCount;

        public float distanceTravelled;
        public float averageMovementSpeed;
        public float maxMovementSpeed;
        public float idleTime;
        public int movementStopsCount;
        public int backtrackingCount;

        public int totalInteractions;
        public int successfulInteractions;
        public int failedInteractions;
        public float averageInteractionDuration;

        public float totalFocusDuration;
    }

    /// <summary>
    /// Single learning task record containing both Level 1 telemetry and Level 2 features.
    /// </summary>
    [Serializable]
    public class TaskData
    {
        public string taskId = string.Empty;
        public string taskName = string.Empty;
        public string startTime = string.Empty;
        public string endTime = string.Empty;
        public float durationSeconds;
        public bool completed;

        public TaskPerformance performance = new TaskPerformance();
        public List<InteractionRecord>   interactions   = new List<InteractionRecord>();
        public List<LearningEventRecord> learningEvents = new List<LearningEventRecord>();
        public List<HeadMovementSample>  headMovement   = new List<HeadMovementSample>();
        public List<NavigationSample>    navigation     = new List<NavigationSample>();
        public List<IdlePeriodRecord>    idlePeriods    = new List<IdlePeriodRecord>();
        public List<FocusRecord>         focusRecords   = new List<FocusRecord>();
        public AggregatedFeatures        aggregatedFeatures = new AggregatedFeatures();

        /// <summary>Cognitive-load inference results recorded during this task.</summary>
        public List<LearnerStateRecord>  stateHistory   = new List<LearnerStateRecord>();


        public TaskData() { }

        public TaskData(string id, string name)
        {
            taskId = id ?? string.Empty;
            taskName = name ?? string.Empty;
            startTime = DateTime.UtcNow.ToString("o");
        }
    }

    /// <summary>
    /// Session metadata recorded at the start and end of a session.
    /// </summary>
    [Serializable]
    public class SessionMetadata
    {
        public string sessionId = string.Empty;
        public string learnerId = "L001";
        public string moduleId = "MODULE_001";
        public string appVersion = "1.0.0";
        public string unityVersion = string.Empty;
        public string platform = string.Empty;
        public string startTime = string.Empty;
        public string endTime = string.Empty;
        public float durationSeconds;
        public int tasksCompleted;
        public int tasksFailed;
        public int finalScore;
    }

    /// <summary>
    /// Summary totals across all tasks in the session.
    /// </summary>
    [Serializable]
    public class SessionSummary
    {
        public int totalTasks;
        public int tasksCompleted;
        public int tasksFailed;
        public float totalDurationSeconds;
        public float averageTaskDurationSeconds;
        public int totalErrors;
        public int totalRetries;
        public int totalHints;
        public int totalInteractions;
        public float finalScore;
    }

    /// <summary>
    /// Complete Root Session Data Structure for serialization.
    /// </summary>
    [Serializable]
    public class SessionData
    {
        public SessionMetadata session = new SessionMetadata();
        public List<TaskData> tasks = new List<TaskData>();
        public SessionSummary summary = new SessionSummary();

        /// <summary>
        /// Validates that all fields contain valid, non-null, finite data before saving to disk.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (session == null)
            {
                errorMessage = "Session metadata is null.";
                return false;
            }

            if (string.IsNullOrEmpty(session.sessionId))
            {
                errorMessage = "Session ID is missing or empty.";
                return false;
            }

            if (string.IsNullOrEmpty(session.learnerId))
            {
                errorMessage = "Learner ID is missing or empty.";
                return false;
            }

            if (float.IsNaN(session.durationSeconds) || float.IsInfinity(session.durationSeconds))
            {
                session.durationSeconds = 0f;
            }

            if (tasks == null)
            {
                tasks = new List<TaskData>();
            }

            foreach (var task in tasks)
            {
                if (task == null) continue;
                if (string.IsNullOrEmpty(task.taskId)) task.taskId = "TASK_UNKNOWN";
                if (string.IsNullOrEmpty(task.taskName)) task.taskName = "Unnamed Task";

                if (float.IsNaN(task.durationSeconds) || float.IsInfinity(task.durationSeconds))
                {
                    task.durationSeconds = 0f;
                }

                if (task.performance == null) task.performance = new TaskPerformance();
                if (task.aggregatedFeatures == null) task.aggregatedFeatures = new AggregatedFeatures();
            }

            return true;
        }
    }
}
