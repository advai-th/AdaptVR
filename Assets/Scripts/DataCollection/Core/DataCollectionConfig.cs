using UnityEngine;

namespace AdaptiveVR.DataCollection.Core
{
    /// <summary>
    /// Centralized configuration for the VR Data Collection and Telemetry system.
    /// Can be created as a ScriptableObject asset or assigned directly in the Unity Inspector.
    /// Avoids scattering magic numbers throughout tracking scripts.
    /// </summary>
    [CreateAssetMenu(fileName = "DataCollectionConfig", menuName = "AdaptiveVR/Data Collection Config", order = 1)]
    public class DataCollectionConfig : ScriptableObject
    {
        [Header("Sampling Intervals (Seconds)")]
        [Tooltip("Sampling interval for HMD head position and rotation. Default is 0.1s (~10 samples/second).")]
        [Range(0.02f, 1.0f)]
        public float headSamplingInterval = 0.10f;

        [Tooltip("Sampling interval for player position/navigation tracking. Default is 0.25s (~4 samples/second).")]
        [Range(0.05f, 2.0f)]
        public float navigationSamplingInterval = 0.25f;

        [Tooltip("Threshold duration of no movement/interaction before an idle state is recorded. Default is 5.0 seconds.")]
        [Range(1.0f, 30.0f)]
        public float idleThresholdSeconds = 5.0f;

        [Tooltip("Movement delta threshold below which player is considered stationary.")]
        [Range(0.001f, 0.1f)]
        public float idleMovementThreshold = 0.02f;

        [Tooltip("Head rotation angular velocity threshold (degrees/sec) for detecting significant direction changes.")]
        [Range(5f, 90f)]
        public float significantHeadRotationThreshold = 30.0f;

        [Header("Head-Directed Focus (Gaze Proxy) Settings")]
        [Tooltip("Maximum raycast distance for detecting objects the learner is looking at.")]
        [Range(0.5f, 20.0f)]
        public float focusRayDistance = 5.0f;

        [Tooltip("LayerMask containing interactive learning objects for head-directed focus tracking.")]
        public LayerMask focusLayerMask = ~0;

        [Header("Subsystem Enable/Disable Flags")]
        [Tooltip("Enable tracking of head position, rotation, and kinematic features.")]
        public bool enableHeadTracking = true;

        [Tooltip("Enable tracking of player navigation, distance travelled, and teleportation.")]
        public bool enableNavigationTracking = true;

        [Tooltip("Enable optional head-directed object focus tracking.")]
        public bool enableFocusTracking = true;

        [Tooltip("If enabled, preserves Level 1 raw telemetry arrays in the exported JSON.")]
        public bool enableRawTelemetry = true;

        [Header("Persistence & Auto-Save Configuration")]
        [Tooltip("Automatically save session JSON whenever a task completes.")]
        public bool autoSaveOnTaskComplete = true;

        [Tooltip("Automatically save current session data when the application quits or loses focus.")]
        public bool autoSaveOnApplicationQuit = true;

        [Tooltip("Subdirectory relative to Application.persistentDataPath where session JSON logs will be stored.")]
        public string storageSubdirectory = "AdaptVR/Sessions";

        [Header("Development / Debug Mode")]
        [Tooltip("Enable verbose diagnostic logging in the Unity Console.")]
        public bool verboseLogging = true;

        /// <summary>
        /// Creates a default runtime instance of configuration if no asset is assigned.
        /// </summary>
        public static DataCollectionConfig CreateDefault()
        {
            DataCollectionConfig config = ScriptableObject.CreateInstance<DataCollectionConfig>();
            return config;
        }
    }
}
