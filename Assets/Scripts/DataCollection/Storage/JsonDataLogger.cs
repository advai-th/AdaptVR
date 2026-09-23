using System;
using System.IO;
using UnityEngine;
using AdaptiveVR.DataCollection.Core;

namespace AdaptiveVR.DataCollection.Storage
{
    /// <summary>
    /// Handles local JSON file persistence and data validation for VR learning sessions.
    /// Works offline on both Windows Editor/PC and Meta Quest 2 / Android (Application.persistentDataPath).
    /// </summary>
    public static class JsonDataLogger
    {
        private static readonly object FileLock = new object();

        /// <summary>
        /// Gets the absolute cross-platform directory where sessions are saved.
        /// </summary>
        public static string GetStorageDirectory(DataCollectionConfig config = null)
        {
            string subDir = config != null && !string.IsNullOrEmpty(config.storageSubdirectory)
                ? config.storageSubdirectory
                : "AdaptVR/Sessions";

            string fullPath = Path.Combine(Application.persistentDataPath, subDir);

            try
            {
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonDataLogger] Failed to create directory '{fullPath}': {ex.Message}");
            }

            return fullPath;
        }

        /// <summary>
        /// Validates session data and writes it to a structured JSON file.
        /// </summary>
        /// <param name="sessionData">Session data object to serialize.</param>
        /// <param name="config">Configuration containing paths and flags.</param>
        /// <param name="savedFilePath">Outputs the final absolute file path if successful.</param>
        /// <returns>True if save succeeded, false otherwise.</returns>
        public static bool SaveSession(SessionData sessionData, DataCollectionConfig config, out string savedFilePath)
        {
            savedFilePath = string.Empty;

            if (sessionData == null)
            {
                Debug.LogError("[JsonDataLogger] Cannot save: sessionData is null.");
                return false;
            }

            // 1. Data Validation
            if (!sessionData.Validate(out string validationError))
            {
                Debug.LogError($"[JsonDataLogger] Data validation failed before saving: {validationError}");
                return false;
            }

            // 2. Prepare file path
            string directory = GetStorageDirectory(config);
            string sanitizedSessionId = SanitizeFileName(sessionData.session.sessionId);
            if (string.IsNullOrEmpty(sanitizedSessionId))
            {
                sanitizedSessionId = $"SESSION_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            }

            string fileName = $"{sanitizedSessionId}.json";
            string filePath = Path.Combine(directory, fileName);

            // 3. Serialize to JSON
            try
            {
                string jsonContent = JsonUtility.ToJson(sessionData, true);

                lock (FileLock)
                {
                    // Write to temporary file first then move to prevent partial writes
                    string tempPath = filePath + ".tmp";
                    File.WriteAllText(tempPath, jsonContent);

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    File.Move(tempPath, filePath);
                }

                savedFilePath = filePath;
                if (config == null || config.verboseLogging)
                {
                    Debug.Log($"<color=#00FF66><b>[JsonDataLogger]</b> Session successfully saved to:</color> {filePath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonDataLogger] Exception while saving session to '{filePath}': {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Loads and deserializes a previously saved SessionData JSON file.
        /// </summary>
        public static SessionData LoadSession(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[JsonDataLogger] File not found: {filePath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<SessionData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonDataLogger] Failed to load session from '{filePath}': {ex.Message}");
                return null;
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
