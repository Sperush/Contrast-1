using System;
using System.Collections.Generic;
using UnityEngine;

namespace Contrast.Telemetry
{
    /// <summary>
    /// Inspector-editable ScriptableObject containing all configurable telemetry settings,
    /// endpoint parameters, thresholds, event toggles, field filters, and custom global fields.
    /// Asset path: Assets/Resources/TelemetrySettings.asset
    /// </summary>
    [CreateAssetMenu(fileName = "TelemetrySettings", menuName = "Contrast/Telemetry Settings")]
    public sealed class TelemetrySettings : ScriptableObject
    {
        public const string ResourcesPath = "TelemetrySettings";

        [Serializable]
        public struct CustomField
        {
            public string key;
            public string value;
        }

        [Header("API & Identity")]
        public string endpointUrl = "https://bigame.ezwork.vn/logs";
        public string gameId = "physic_game";
        public string collection = "player_logs";
        public string buildId = "build_01";
        public string levelVersion = "1.0";
        public string country = "VN";
        [Tooltip("Nếu điền giá trị này, hệ thống sẽ gửi user này thay cho ID tự sinh lưu trong PlayerPrefs.")]
        public string overrideUserId = "";
        public string userIdPrefix = "player_";
        public string defaultUserFallback = "player1";
        public string playerPrefsUserKey = "Contrast_Telemetry_UserId";
        [Tooltip("Nếu điền giá trị này, hệ thống sẽ gửi tên người chơi này trong session_track thay cho tên lưu trong PlayerPrefs.")]
        public string overrideUserName = "";
        public string defaultUserName = "Admin";
        public string playerPrefsUserNameKey = "Contrast_Telemetry_UserName";

        [Header("General & Network")]
        public bool enabled = true;
        public bool logToConsole = false;
        [Min(1)] public int requestTimeoutSec = 8;
        [Min(1)] public int quitFlushTimeoutSec = 4;
        [Min(8)] public int maxPendingQueue = 512;
        [Min(8)] public int maxRecentPayloads = 100;

        [Header("Payload Optimization")]
        public bool omitZeroOptionalFields = true;
        [Tooltip("Nếu bật, metadata thiết bị (game_version, build_id, platform, device_model, os_version, country) chỉ gửi ở session_track start; các session_track sau vẫn giữ user_id, user_name, session_id, timestamp.")]
        public bool sendDeviceMetadataOnlyOnSessionStart = false;
        [Tooltip("Nếu bật, movement/jump/obstacle gộp vào level_track, level_finish và challenge_track để giảm HTTP request; tắt đi nếu muốn gửi movement_track, jump_track, obstacle_track riêng lẻ.")]
        public bool aggregateHighFrequencyTracks = false;
        [Min(0f)] public float sendDelaySeconds = 0.05f;

        [Header("Sampling & Detection Thresholds")]
        [Min(0f)] public float stuckCooldownSec = 2.0f;
        [Min(0.01f)] public float maxFrameDeltaTime = 0.25f;
        [Min(0.1f)] public float teleportDeltaXThreshold = 3.0f;
        [Min(0.05f)] public float checkpointMatchRadius = 0.5f;
        [Min(0f)] public float phaseTuneEpsilon = 0.0005f;
        [Min(1f)] public float obstacleApproachRadius = 3.5f;

        [Header("Action Toggles (Spec FormLog & Mapping Checkpoint)")]
        public bool trackSession = true;
        public bool trackLevel = true;
        public bool trackCheckpoint = true;
        public bool trackChallenge = true;
        public bool trackPlayerDeath = true;
        public bool trackRespawn = true;
        public bool trackLevelFinish = true;
        public bool trackPause = true;
        public bool trackCollectible = false;
        public bool trackNewton = false;
        public bool trackPlayerStuck = false;
        [Header("High-Frequency Granular Tracks (Active when Aggregate = false)")]
        public bool trackMovement = true;
        public bool trackJump = true;
        public bool trackObstacle = true;

        [Header("Filtering & Custom Global Fields")]
        [Tooltip("Danh sách event_name bổ sung muốn tắt (VD: player_stuck).")]
        public string[] disabledActions = new string[0];
        [Tooltip("Danh sách tên trường JSON muốn ẩn khỏi mọi payload (VD: platform, app_version).")]
        public string[] disabledFields = new string[0];
        [Tooltip("Các cặp key-value tùy chỉnh sẽ tự động đính kèm vào mọi log gửi lên.")]
        public CustomField[] extraGlobalFields = new CustomField[0];

        private static TelemetrySettings cachedDefault;

        private readonly HashSet<string> disabledActionsSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> disabledFieldsSet = new HashSet<string>(StringComparer.Ordinal);
        private bool cachesBuilt;

        public static TelemetrySettings LoadOrDefault()
        {
            if (cachedDefault != null)
                return cachedDefault;

            cachedDefault = Resources.Load<TelemetrySettings>(ResourcesPath);
            if (cachedDefault == null)
            {
                cachedDefault = CreateInstance<TelemetrySettings>();
                cachedDefault.hideFlags = HideFlags.DontSave;
            }
            cachedDefault.RebuildCaches();
            return cachedDefault;
        }

        private void OnEnable()
        {
            RebuildCaches();
        }

        private void OnValidate()
        {
            requestTimeoutSec = Mathf.Max(1, requestTimeoutSec);
            quitFlushTimeoutSec = Mathf.Max(1, quitFlushTimeoutSec);
            maxPendingQueue = Mathf.Max(8, maxPendingQueue);
            maxRecentPayloads = Mathf.Max(8, maxRecentPayloads);
            sendDelaySeconds = Mathf.Max(0f, sendDelaySeconds);
            stuckCooldownSec = Mathf.Max(0f, stuckCooldownSec);
            maxFrameDeltaTime = Mathf.Max(0.01f, maxFrameDeltaTime);
            teleportDeltaXThreshold = Mathf.Max(0.1f, teleportDeltaXThreshold);
            checkpointMatchRadius = Mathf.Max(0.05f, checkpointMatchRadius);
            phaseTuneEpsilon = Mathf.Max(0f, phaseTuneEpsilon);
            obstacleApproachRadius = Mathf.Max(1f, obstacleApproachRadius);
            RebuildCaches();
        }

        public void RebuildCaches()
        {
            disabledActionsSet.Clear();
            if (disabledActions != null)
            {
                for (int i = 0; i < disabledActions.Length; i++)
                {
                    if (!string.IsNullOrEmpty(disabledActions[i]))
                        disabledActionsSet.Add(disabledActions[i].Trim());
                }
            }

            disabledFieldsSet.Clear();
            if (disabledFields != null)
            {
                for (int i = 0; i < disabledFields.Length; i++)
                {
                    if (!string.IsNullOrEmpty(disabledFields[i]))
                        disabledFieldsSet.Add(disabledFields[i].Trim());
                }
            }

            cachesBuilt = true;
        }

        public bool IsActionAllowed(string action)
        {
            if (!enabled || string.IsNullOrEmpty(action))
                return false;

            if (!cachesBuilt)
                RebuildCaches();

            if (disabledActionsSet.Contains(action))
                return false;

            switch (action)
            {
                case "session_track": return trackSession;
                case "level_track": return trackLevel;
                case "checkpoint_track": return trackCheckpoint;
                case "movement_track": return !aggregateHighFrequencyTracks && trackMovement;
                case "jump_track": return !aggregateHighFrequencyTracks && trackJump;
                case "obstacle_track": return !aggregateHighFrequencyTracks && trackObstacle;
                case "challenge_track": return trackChallenge;
                case "player_death": return trackPlayerDeath;
                case "respawn_track": return trackRespawn;
                case "level_finish": return trackLevelFinish;
                case "pause_track": return trackPause;
                case "collectible_seen":
                case "route_enter":
                case "collectible_pickup":
                    return trackCollectible;
                case "newton_shot":
                case "newton_lock":
                    return trackNewton;
                case "player_stuck":
                    return trackPlayerStuck;
                default:
                    return true;
            }
        }

        public bool IsFieldAllowed(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (!cachesBuilt)
                RebuildCaches();

            return !disabledFieldsSet.Contains(key);
        }

#if UNITY_EDITOR
        private const string EditorAssetPath = "Assets/Resources/TelemetrySettings.asset";

        [UnityEditor.InitializeOnLoadMethod]
        private static void EnsureEditorAssetOnLoad()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (UnityEditor.EditorApplication.isCompiling ||
                    UnityEditor.EditorApplication.isUpdating ||
                    UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                EnsureAssetExists();
            };
        }

        [UnityEditor.MenuItem("Contrast/Telemetry/Select TelemetrySettings")]
        public static void SelectOrCreateAsset()
        {
            TelemetrySettings asset = EnsureAssetExists();
            if (asset != null)
            {
                UnityEditor.EditorUtility.FocusProjectWindow();
                UnityEditor.Selection.activeObject = asset;
                UnityEditor.EditorGUIUtility.PingObject(asset);
            }
        }

        public static TelemetrySettings EnsureAssetExists()
        {
            TelemetrySettings asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TelemetrySettings>(EditorAssetPath);
            if (asset != null)
                return asset;

            asset = Resources.Load<TelemetrySettings>(ResourcesPath);
            if (asset != null)
                return asset;

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TelemetrySettings");
            if (guids != null && guids.Length > 0)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string existingPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TelemetrySettings>(existingPath);
                    if (asset != null)
                        return asset;
                }
            }

            if (!System.IO.Directory.Exists("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            asset = CreateInstance<TelemetrySettings>();
            UnityEditor.AssetDatabase.CreateAsset(asset, EditorAssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            cachedDefault = asset;
            return asset;
        }
#endif
    }
}
