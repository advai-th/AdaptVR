using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using AdaptiveVR.Networking;

namespace AdaptiveVR.Adaptive
{
    /// <summary>
    /// Checks GET /api/model/version at app startup.
    /// If the server version is newer than the local cached version,
    /// downloads the ONNX binary, verifies its SHA-256 integrity,
    /// and saves it to Application.persistentDataPath/AdaptVR/Models/.
    ///
    /// IMPORTANT: No model swap happens during an active session.
    /// CognitiveLoadBridge will pick up the new file on the next app launch.
    ///
    /// Attach to the AdaptiveSystem GameObject.
    /// </summary>
    public class ModelUpdateManager : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The model version currently cached on this device.")]
        public string localModelVersion = "v1";

        [Tooltip("Check for newer models automatically on Start().")]
        public bool checkOnStart = true;

        [Tooltip("Seconds to wait after Start() before checking (let backend client connect first).")]
        [Range(0f, 10f)]
        public float checkDelaySeconds = 3f;

        [Header("Runtime State (Read-Only)")]
        public string serverVersion  = "";
        public bool   updateAvailable= false;
        public bool   isDownloading  = false;

        private const string MODELS_SUBDIR = "AdaptVR/Models";

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (checkOnStart)
                StartCoroutine(CheckAfterDelay());
        }

        private IEnumerator CheckAfterDelay()
        {
            yield return new WaitForSeconds(checkDelaySeconds);
            yield return StartCoroutine(CheckForUpdate());
        }

        // ── Version check ─────────────────────────────────────────────────────

        private IEnumerator CheckForUpdate()
        {
            if (AdaptVRBackendClient.Instance == null)
            {
                Debug.Log("[ModelUpdateManager] AdaptVRBackendClient not found — skipping update check.");
                yield break;
            }

            string url = $"{AdaptVRBackendClient.Instance.GetRestApiBaseUrl()}/model/version";
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"[ModelUpdateManager] Could not reach /api/model/version: {req.error}");
                yield break;
            }

            var payload = JsonUtility.FromJson<ModelVersionResponse>(req.downloadHandler.text);
            if (payload == null || string.IsNullOrEmpty(payload.version))
            {
                Debug.Log("[ModelUpdateManager] No model version data returned.");
                yield break;
            }

            serverVersion = payload.version;
            int local  = ParseVersion(localModelVersion);
            int remote = ParseVersion(payload.version);

            if (remote <= local)
            {
                Debug.Log($"[ModelUpdateManager] Model up-to-date ({localModelVersion}).");
                yield break;
            }

            Debug.Log($"<color=#FFD700>[ModelUpdateManager] Newer model available: " +
                      $"{payload.version}  (local: {localModelVersion})</color>");
            updateAvailable = true;

            yield return StartCoroutine(DownloadModel(payload.version, payload.sha256));
        }

        // ── Download ──────────────────────────────────────────────────────────

        private IEnumerator DownloadModel(string version, string expectedSha256)
        {
            if (AdaptVRBackendClient.Instance == null) yield break;

            isDownloading = true;
            string url = $"{AdaptVRBackendClient.Instance.GetRestApiBaseUrl()}/model/download?version={version}";

            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();
            isDownloading = false;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ModelUpdateManager] Download failed: {req.error}");
                yield break;
            }

            byte[] data = req.downloadHandler.data;

            // SHA-256 integrity verification
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                string actualSha256 = ComputeSha256Hex(data);
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"[ModelUpdateManager] SHA-256 mismatch! " +
                                   $"Expected: {expectedSha256}  Got: {actualSha256}  Discarding.");
                    yield break;
                }
                Debug.Log("[ModelUpdateManager] SHA-256 verified ✓");
            }

            // Save to persistent storage
            string dir  = Path.Combine(Application.persistentDataPath, MODELS_SUBDIR);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"cognitive_load_{version}.onnx");
            File.WriteAllBytes(path, data);
            localModelVersion = version;

            Debug.Log($"<color=#00FF88>[ModelUpdateManager] Model {version} saved to: {path}" +
                      $"  ({data.Length / 1024f:F0} KB)</color>");
            Debug.Log("[ModelUpdateManager] New model will be loaded on the next app launch.");

            // Update local version display
            updateAvailable = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ParseVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return 0;
            int idx = v.LastIndexOf('v');
            if (idx >= 0 && int.TryParse(v.Substring(idx + 1), out int n)) return n;
            return 0;
        }

        private static string ComputeSha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash   = sha.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // ── Serialisation helper ──────────────────────────────────────────────

        [Serializable]
        private class ModelVersionResponse
        {
            public string version;
            public string sha256;
            public string releaseDate;
            public int    trainingSamples;
            public float  validationAccuracy;
        }
    }
}
