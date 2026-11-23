using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Contrast.Color;
using Contrast.Core;
using Contrast.Data;
using Contrast.Level;
using Contrast.Player;
using UnityEngine;
using UnityEngine.Networking;

namespace Contrast.Telemetry
{
    /// <summary>
    /// Zero-coupling gameplay telemetry observer aligned with FormLog Platformer &amp; Mapping Checkpoint.
    /// Automatically attaches at startup and observes GameManager, PlayerController,
    /// LevelLoader, QuangDiemItem, ColorPlatform, and EasyNewtonFinaleController without requiring
    /// any telemetry references in gameplay scripts.
    /// </summary>
    public sealed class ContrastTelemetryObserver : MonoBehaviour
    {
        public static ContrastTelemetryObserver Instance { get; private set; }

        [SerializeField] private TelemetrySettings settings;

        public TelemetrySettings Settings
        {
            get
            {
                if (settings == null)
                    settings = TelemetrySettings.LoadOrDefault();
                return settings;
            }
        }

        private string userId;
        private string sessionId;
        private long sessionStartUnixMs;
        private float lastForegroundMarkRealtime;
        private float activeSessionDurationSec;
        private bool isAppPaused;
        private bool sessionEndSent;

        private readonly Queue<string> pendingPayloads = new Queue<string>();
        private readonly List<string> recentPayloads = new List<string>();
        private readonly Dictionary<string, int> levelAttemptCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool isDispatching;

        private readonly LevelSessionMetrics metrics = new LevelSessionMetrics();
        private readonly TelemetryEvent reusableEvent = new TelemetryEvent();

        // Observed level state
        private LevelData observedLevelData;
        private GameState prevGameState = GameState.MainMenu;
        private bool prevTestMode;
        private Vector2[] levelCheckpoints = Array.Empty<Vector2>();
        private readonly Dictionary<string, int> collectibleOrdinalByRawId = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool hasActiveLevel;
        private bool levelClosed;
        private string activeLevelId = string.Empty;
        private string activeSegmentId = string.Empty;
        private int currentAttempt;
        private int totalCheckpoints;
        private int totalCollectibles;
        private float activeKillY = -50f;
        private float pauseStartRealtime;

        // Death / Stuck deduplication
        private int lastDeathFrame = -1;
        private float lastStuckRealtime = -999f;

        // Newton observed state
        private EasyNewtonFinaleController observedNewton;
        private bool prevNewtonDockActive;
        private int prevNewtonMarkSeq;
        private NewtonFinaleStep prevNewtonStep = NewtonFinaleStep.CaptureK;

        public string UserId => userId;
        public string SessionId => sessionId;
        public IReadOnlyList<string> RecentPayloads => recentPayloads;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isPlaying)
                return;

            EnsureInstance();
        }

        public static ContrastTelemetryObserver EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            Instance = FindObjectOfType<ContrastTelemetryObserver>();
            if (Instance == null)
            {
                GameObject host = new GameObject("ContrastTelemetryObserver");
                DontDestroyOnLoad(host);
                Instance = host.AddComponent<ContrastTelemetryObserver>();
            }
            return Instance;
        }

        public void SetUserId(string newUserId)
        {
            if (string.IsNullOrEmpty(newUserId))
                return;

            userId = newUserId.Trim();
            string prefKey = !string.IsNullOrEmpty(Settings.playerPrefsUserKey)
                ? Settings.playerPrefsUserKey
                : "Contrast_Telemetry_UserId";
            PlayerPrefs.SetString(prefKey, userId);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (settings == null)
                settings = TelemetrySettings.LoadOrDefault();
            TelemetryConfig.ApplySettings(settings);

            InitializeIdentity();
            Application.logMessageReceived += ObserveRuntimeLog;
            QuangDiemItem.OnCollected += HandleCollectibleCollected;

            EmitSessionTrack("start", includeDeviceMetadata: true, ended: false);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= ObserveRuntimeLog;
            QuangDiemItem.OnCollected -= HandleCollectibleCollected;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!Application.isPlaying || sessionEndSent)
                return;

            if (paused && !isAppPaused)
            {
                activeSessionDurationSec += Mathf.Max(0f, Time.realtimeSinceStartup - lastForegroundMarkRealtime);
                isAppPaused = true;
                EmitSessionTrack("pause", includeDeviceMetadata: false, ended: false);
            }
            else if (!paused && isAppPaused)
            {
                lastForegroundMarkRealtime = Time.realtimeSinceStartup;
                isAppPaused = false;
                EmitSessionTrack("resume", includeDeviceMetadata: false, ended: false);
            }
        }

        private void OnApplicationQuit()
        {
            if (sessionEndSent)
                return;

            sessionEndSent = true;
            if (!isAppPaused)
                activeSessionDurationSec += Mathf.Max(0f, Time.realtimeSinceStartup - lastForegroundMarkRealtime);

            if (hasActiveLevel && !levelClosed)
            {
                FlushActiveMovement(GameManager.Instance != null ? GameManager.Instance.Player : null);
                CloseActiveChallenge("fail");
                EmitLevelTrack("end", "quit_level", "app_closed");
                levelClosed = true;
            }

            EmitSessionTrack("end", includeDeviceMetadata: false, ended: true);
            FlushPendingOnQuit();
        }

        private void LateUpdate()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                if (hasActiveLevel && !levelClosed)
                {
                    OnGameStateChanged(prevGameState, GameState.MainMenu);
                    prevGameState = GameState.MainMenu;
                }
                return;
            }

            PlayerController pc = gm.Player;
            LevelLoader loader = gm.LevelLoader;
            LevelData currentData = loader != null ? loader.CurrentLevelData : null;

            // 1. Detect level load / fresh attempt (or restart)
            if (currentData != null && !ReferenceEquals(currentData, observedLevelData))
            {
                observedLevelData = currentData;
                OnNewLevelObserved(gm, pc, currentData);
            }

            // 2. Detect GameState transitions (Pause, Resume, Win, Exit)
            GameState curState = gm.State;
            if (curState != prevGameState)
            {
                OnGameStateChanged(prevGameState, curState);
                prevGameState = curState;
            }

            // 3. Detect Test Mode toggle
            bool curTestMode = pc != null && pc.IsTestModeActive;
            if (curTestMode != prevTestMode)
            {
                prevTestMode = curTestMode;
                if (curTestMode)
                    metrics.UsedTestMode = true;
            }

            // 4. Sample active gameplay frame (Movement, Jump, Stuck, Respawn, Collectibles, Obstacles)
            if (curState == GameState.Playing && pc != null && pc.gameObject.activeInHierarchy && !curTestMode)
            {
                // Detect respawn completion after death
                if (metrics.WaitingForRespawn && pc.State != PlayerState.Dead)
                {
                    float respawnDelay = metrics.CompleteRespawn();
                    EmitRespawnTrack(respawnDelay);
                    BeginSegmentChallenge(activeSegmentId, isRespawnAttempt: true);
                }

                LevelSessionMetrics.FrameTransitions tr = metrics.SampleFrame(pc, Time.deltaTime, Settings);
                ProcessMovementAndJumpTransitions(pc, tr);

                if (tr.StuckStarted)
                {
                    if (!string.IsNullOrEmpty(metrics.ActiveObstacleId))
                    {
                        Vector2 pPos = pc.transform.position;
                        int obsAttempt = metrics.ObstacleAttemptCounts.TryGetValue(metrics.ActiveObstacleId, out int c) ? c : 1;
                        EmitObstacleTrack(
                            metrics.ActiveObstacleId,
                            metrics.ActiveObstacleType ?? "other",
                            "obstacle_hit",
                            obsAttempt,
                            0f,
                            "blocked",
                            pPos.x,
                            pPos.y);
                    }
                    EmitPlayerStuck(pc);
                }

                if (pc.State == PlayerState.Dead && lastDeathFrame != Time.frameCount)
                {
                    lastDeathFrame = Time.frameCount;
                    OnPlayerDiedObserved(pc);
                }

                ObserveCollectibles(pc);
                ObserveObstacles(pc);
            }

            // 5. Observe Newton Finale progression
            ObserveNewtonFinale(pc);
        }

        // ──────────────────────────────────────────────────────────────
        // Runtime Observers
        // ──────────────────────────────────────────────────────────────

        private void OnNewLevelObserved(GameManager gm, PlayerController pc, LevelData data)
        {
            if (hasActiveLevel && !levelClosed)
            {
                FlushActiveMovement(pc);
                CloseActiveChallenge("fail");
                EmitLevelTrack("end", "quit_level", "level_reloaded");
            }

            activeLevelId = ResolveNormalizedLevelId(gm, data);

            int attempt = 1;
            if (levelAttemptCounts.TryGetValue(activeLevelId, out int prev))
                attempt = prev + 1;
            levelAttemptCounts[activeLevelId] = attempt;
            currentAttempt = attempt;

            hasActiveLevel = true;
            levelClosed = false;

            totalCheckpoints = data.checkPoint != null ? data.checkPoint.Length : 0;
            totalCollectibles = data.collectibles != null ? data.collectibles.Length : 0;
            activeKillY = LevelLoader.ComputeKillY(data);

            levelCheckpoints = new Vector2[totalCheckpoints];
            for (int i = 0; i < totalCheckpoints; i++)
            {
                CheckPointData cp = data.checkPoint[i];
                levelCheckpoints[i] = (cp != null && cp.position != null) ? cp.position.ToVector2() : Vector2.zero;
            }

            collectibleOrdinalByRawId.Clear();
            if (data.collectibles != null)
            {
                for (int i = 0; i < data.collectibles.Length; i++)
                {
                    CollectibleData cd = data.collectibles[i];
                    if (cd != null && !string.IsNullOrEmpty(cd.id) && !collectibleOrdinalByRawId.ContainsKey(cd.id))
                        collectibleOrdinalByRawId[cd.id] = i + 1;
                }
            }

            Vector2 spawnPos = (data.startPos != null && data.startPos.position != null)
                ? data.startPos.position.ToVector2()
                : Vector2.zero;
            LogicalColor spawnColor = data.startPos != null
                ? ColorClassifier.Classify(data.startPos.color)
                : LogicalColor.White;
            float endX = ComputeLevelEndX(data, spawnPos);

            metrics.ResetLevel(
                spawnPos,
                spawnColor,
                endX,
                pc != null ? pc.TotalJumpRequests : 0,
                pc != null ? pc.TotalJumpsExecuted : 0,
                NowUnixMs());

            observedNewton = null;
            prevNewtonDockActive = false;
            prevNewtonMarkSeq = 0;
            prevNewtonStep = NewtonFinaleStep.CaptureK;

            EmitLevelTrack("start", null, null);

            activeSegmentId = ContrastSpecMapping.ResolveActiveSegmentId(activeLevelId, 0, false);
            BeginSegmentChallenge(activeSegmentId, isRespawnAttempt: false);
        }

        private void OnGameStateChanged(GameState from, GameState to)
        {
            if (from == GameState.Playing && to == GameState.Paused)
            {
                FlushActiveMovement(GameManager.Instance != null ? GameManager.Instance.Player : null);
                pauseStartRealtime = Time.realtimeSinceStartup;
                EmitPauseTrack(true, 0f);
            }
            else if (from == GameState.Paused && to == GameState.Playing)
            {
                float pauseDur = Mathf.Max(0f, Time.realtimeSinceStartup - pauseStartRealtime);
                EmitPauseTrack(false, pauseDur);
            }
            else if (from == GameState.Playing && to == GameState.Won)
            {
                OnLevelWonObserved();
            }
            else if ((from == GameState.Playing || from == GameState.Paused || from == GameState.GameOver) &&
                     (to == GameState.MainMenu || to == GameState.LevelSelect || to == GameState.Editing))
            {
                if (hasActiveLevel && !levelClosed)
                {
                    FlushActiveMovement(GameManager.Instance != null ? GameManager.Instance.Player : null);
                    CloseActiveChallenge("fail");
                    EmitLevelTrack("end", "quit_level", "quit_by_user");
                    levelClosed = true;
                    hasActiveLevel = false;
                }
            }
        }

        private void ObserveRuntimeLog(string message, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (message.StartsWith("[FLOW][AUTO_RESPAWN]", StringComparison.Ordinal))
            {
                if (lastDeathFrame != Time.frameCount)
                {
                    lastDeathFrame = Time.frameCount;
                    PlayerController pc = GameManager.Instance != null ? GameManager.Instance.Player : null;
                    OnPlayerDiedObserved(pc);
                }
            }
            else if (message.StartsWith("[FLOW][RESTART]", StringComparison.Ordinal))
            {
                if (hasActiveLevel && !levelClosed)
                {
                    FlushActiveMovement(GameManager.Instance != null ? GameManager.Instance.Player : null);
                    CloseActiveChallenge("fail");
                    EmitLevelTrack("retry", "quit_level", "retry");
                    levelClosed = true;
                    observedLevelData = null; // Force fresh level_track start & attempt_id++ on reload
                }
            }
            else if (message.StartsWith("[CHECKPOINT] reached", StringComparison.Ordinal))
            {
                OnCheckpointReachedObserved();
            }
        }

        private void ProcessMovementAndJumpTransitions(PlayerController pc, LevelSessionMetrics.FrameTransitions tr)
        {
            if (tr.MoveStopped)
                EmitMovementTrack("move_stop", tr.MoveStopDirection, tr.MoveDuration, tr.MoveWorldX);
            if (tr.MoveStarted)
                EmitMovementTrack("move_start", tr.MoveStartDirection, 0f, tr.MoveWorldX);

            if (tr.JumpStarted)
                EmitJumpTrack("jump_start", tr.JumpId, tr.JumpTakeoff, null, 0f, 0f, null);
            if (tr.JumpLanded)
                EmitJumpTrack("jump_land", tr.JumpId, tr.JumpTakeoff, tr.JumpLandingObjectId, tr.JumpAirTime, tr.JumpHorizontalDist, 1);
        }

        private void OnPlayerDiedObserved(PlayerController pc)
        {
            if (!hasActiveLevel || levelClosed)
                return;

            if (pc != null)
            {
                LevelSessionMetrics.FrameTransitions tr = metrics.SampleFrame(pc, 0f, Settings);
                if (tr.MoveStopped)
                    EmitMovementTrack("move_stop", tr.MoveStopDirection, tr.MoveDuration, tr.MoveWorldX);
            }
            FlushActiveMovement(pc);

            Vector2 pos = pc != null ? (Vector2)pc.transform.position : metrics.LastPos;
            string cause = pos.y <= activeKillY + 0.5f
                ? "fall"
                : (pc != null && pc.State == PlayerState.Stuck ? "crush" : "other");

            bool hadOpenJump = metrics.RecordDeath(out _);

            if (hadOpenJump)
            {
                EmitJumpTrack(
                    "jump_fail",
                    metrics.ActiveJumpId,
                    metrics.JumpTakeoffPos,
                    null,
                    metrics.JumpElapsedSec,
                    Mathf.Abs(pos.x - metrics.JumpTakeoffPos.x),
                    0);
            }

            if (!string.IsNullOrEmpty(metrics.ActiveObstacleId))
            {
                string failReason = cause == "fall" ? "fall" : (cause == "crush" ? "blocked" : "unknown");
                int obsAttempt = metrics.ObstacleAttemptCounts.TryGetValue(metrics.ActiveObstacleId, out int c) ? c : 1;
                EmitObstacleTrack(
                    metrics.ActiveObstacleId,
                    metrics.ActiveObstacleType ?? "other",
                    "obstacle_fail",
                    obsAttempt,
                    0f,
                    failReason,
                    pos.x,
                    pos.y);
                metrics.ActiveObstacleId = null;
            }

            EmitPlayerDeath(cause, pos);
            CloseActiveChallenge("fail");
            metrics.ResetSpawnTracking();
        }

        private void OnCheckpointReachedObserved()
        {
            if (!hasActiveLevel || levelClosed)
                return;

            GameManager gm = GameManager.Instance;
            LevelLoader loader = gm != null ? gm.LevelLoader : null;
            Vector2 cpPos = loader != null ? loader.CurrentSpawnPosition : metrics.LastPos;

            int cpIndex = ResolveCheckpointIndex(cpPos);
            string cpId = ContrastSpecMapping.FormatCheckpointId(activeLevelId, cpIndex);
            if (!metrics.RecordCheckpoint(cpIndex, cpId, out string prevCpId, out float segTimeSec))
                return;

            float span = Mathf.Max(1f, metrics.TargetEndX - metrics.SpawnPos.x);
            float pct = Mathf.Clamp(Mathf.Round(((cpPos.x - metrics.SpawnPos.x) / span) * 1000f) / 10f, 0f, 100f);

            EmitCheckpointTrack(cpId, cpIndex, prevCpId, pct, segTimeSec);

            string nextSegment = ContrastSpecMapping.ResolveActiveSegmentId(activeLevelId, cpIndex, metrics.NewtonZoneEntered);
            if (!string.Equals(nextSegment, activeSegmentId, StringComparison.Ordinal))
            {
                CloseActiveChallenge("clear");
                activeSegmentId = nextSegment;
                BeginSegmentChallenge(activeSegmentId, isRespawnAttempt: false);
            }
        }

        private void OnLevelWonObserved()
        {
            if (!hasActiveLevel || levelClosed)
                return;

            FlushActiveMovement(GameManager.Instance != null ? GameManager.Instance.Player : null);
            levelClosed = true;
            CloseActiveChallenge("clear");

            EmitLevelFinish();
            EmitLevelTrack("end", "win", "goal_reached");
        }

        private void ObserveCollectibles(PlayerController pc)
        {
            if (!hasActiveLevel || levelClosed || QuangDiemItem.All.Count == 0)
                return;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            Vector2 pPos = pc.transform.position;

            for (int i = 0; i < QuangDiemItem.All.Count; i++)
            {
                QuangDiemItem item = QuangDiemItem.All[i];
                if (item == null || item.IsCollected)
                    continue;

                string specId = ResolveCollectibleSpecId(item, i + 1);
                Vector2 iPos = item.Position;

                if (cam != null && !metrics.SeenCollectibles.Contains(specId))
                {
                    Vector3 vp = cam.WorldToViewportPoint(iPos);
                    if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                    {
                        metrics.SeenCollectibles.Add(specId);
                        EmitCollectibleEvent("collectible_seen", specId, item.ItemId, iPos);
                    }
                }

                if (ContrastSpecMapping.IsSecretBranchCollectible(specId) &&
                    !metrics.EnteredSecretRoutes.Contains(specId) &&
                    Vector2.Distance(pPos, iPos) <= 5.0f)
                {
                    metrics.EnteredSecretRoutes.Add(specId);
                    EmitCollectibleEvent("route_enter", specId, item.ItemId, iPos);
                }
            }
        }

        private void HandleCollectibleCollected(QuangDiemItem item)
        {
            if (item == null || !hasActiveLevel)
                return;

            metrics.CollectiblesCollected = QuangDiemItem.CollectedCount;
            if (QuangDiemItem.TotalCount > totalCollectibles)
                totalCollectibles = QuangDiemItem.TotalCount;

            string specId = ResolveCollectibleSpecId(item, QuangDiemItem.CollectedCount);
            EmitCollectibleEvent("collectible_pickup", specId, item.ItemId, item.BasePosition);
        }

        private void ObserveObstacles(PlayerController pc)
        {
            if (!hasActiveLevel || levelClosed || ColorPlatform.All.Count == 0)
                return;

            Vector2 pPos = pc.transform.position;
            float approach = Settings.obstacleApproachRadius;

            // Check exit/pass of active obstacle
            if (!string.IsNullOrEmpty(metrics.ActiveObstacleId))
            {
                ColorPlatform activePlat = null;
                for (int i = 0; i < ColorPlatform.All.Count; i++)
                {
                    if (ColorPlatform.All[i] != null && ColorPlatform.All[i].name == metrics.ActiveObstacleId)
                    {
                        activePlat = ColorPlatform.All[i];
                        break;
                    }
                }

                if (activePlat != null)
                {
                    float rightEdge = activePlat.Position.x + activePlat.Size.x * 0.5f;
                    if (pPos.x > rightEdge + 0.5f && pc.State != PlayerState.Dead)
                    {
                        string obsId = metrics.ActiveObstacleId;
                        string obsType = metrics.ActiveObstacleType ?? "other";
                        int attemptIdx = metrics.ObstacleAttemptCounts.TryGetValue(obsId, out int c) ? c : 1;
                        if (metrics.PassActiveObstacle(out float timeToPass))
                            EmitObstacleTrack(obsId, obsType, "obstacle_pass", attemptIdx, timeToPass, null, pPos.x, pPos.y);
                    }
                }
                else
                {
                    metrics.ActiveObstacleId = null;
                    metrics.ActiveObstacleType = null;
                }
                return;
            }

            // Detect entering a new obstacle (moving platforms & vertical doors/walls)
            for (int i = 0; i < ColorPlatform.All.Count; i++)
            {
                ColorPlatform plat = ColorPlatform.All[i];
                if (plat == null)
                    continue;

                string obsType = ContrastSpecMapping.ClassifyObstacleType(plat);
                if (obsType == "other")
                    continue;

                string obsId = plat.name;
                if (metrics.PassedObstacles.Contains(obsId))
                    continue;

                Vector2 center = plat.Position;
                Vector2 half = plat.Size * 0.5f;
                if (pPos.x >= center.x - half.x - approach &&
                    pPos.x <= center.x + half.x + 0.25f &&
                    Mathf.Abs(pPos.y - center.y) <= Mathf.Max(2.5f, half.y + 1.5f))
                {
                    int obsAttempt = metrics.EnterObstacle(obsId, obsType);
                    EmitObstacleTrack(obsId, obsType, "obstacle_enter", obsAttempt, 0f, null, pPos.x, pPos.y);
                    break;
                }
            }
        }

        private void ObserveNewtonFinale(PlayerController pc)
        {
            EasyNewtonFinaleController newton = EasyNewtonFinaleController.Instance;
            if (newton == null)
            {
                observedNewton = null;
                return;
            }

            if (!ReferenceEquals(newton, observedNewton))
            {
                observedNewton = newton;
                prevNewtonDockActive = newton.DockActive;
                prevNewtonMarkSeq = newton.MarkEventSequence;
                prevNewtonStep = newton.CurrentStep;
            }

            Vector2 playerPos = pc != null ? (Vector2)pc.transform.position : metrics.LastPos;

            if (newton.IsZoneActive && !metrics.NewtonZoneEntered)
            {
                metrics.NewtonZoneEntered = true;
                metrics.NewtonEnterTracked = true;

                CloseActiveChallenge("clear");
                activeSegmentId = ContrastSpecMapping.ResolveNewtonChallengeId(activeLevelId);
                BeginSegmentChallenge(activeSegmentId, isRespawnAttempt: false);
            }

            if (newton.DockActive && !prevNewtonDockActive)
            {
                prevNewtonDockActive = true;
                metrics.NewtonDocked = true;
            }

            if (newton.MarkEventSequence != prevNewtonMarkSeq)
            {
                prevNewtonMarkSeq = newton.MarkEventSequence;
                bool accepted = newton.CurrentStep != prevNewtonStep;
                int targetIndex = (int)prevNewtonStep + 1;
                string stepChallengeId = ContrastSpecMapping.ResolveNewtonStepChallengeId(prevNewtonStep);
                string reason = accepted ? $"TARGET_{targetIndex}_LOCKED" : newton.LastMarkRejectionReason;

                ComputeNewtonAttemptAccuracy(newton, targetIndex, accepted, out float dist, out float tol, out int accuracy);
                metrics.RecordNewtonAttempt(targetIndex, accepted, accuracy);

                EmitNewtonShot(stepChallengeId, accepted, reason, accuracy, dist, tol, playerPos);
                if (accepted)
                    EmitNewtonLock(newton, stepChallengeId, accuracy);
            }

            if (newton.CurrentStep >= NewtonFinaleStep.Reveal && prevNewtonStep < NewtonFinaleStep.Reveal)
            {
                metrics.NewtonCompleted = true;
                CloseActiveChallenge("clear");
            }

            prevNewtonStep = newton.CurrentStep;
        }

        // ──────────────────────────────────────────────────────────────
        // Spec Event Emitters (FormLog Platformer + Mapping Checkpoint)
        // ──────────────────────────────────────────────────────────────

        private void EmitSessionTrack(string actionName, bool includeDeviceMetadata, bool ended)
        {
            long nowMs = NowUnixMs();
            Send(NewEvent("session_track", includeDeviceMetadata)
                .Set("action_name", actionName)
                .Set("start_time", sessionStartUnixMs)
                .Set("end_time", ended ? (long?)nowMs : null)
                .Set("duration", activeSessionDurationSec));
        }

        private void EmitLevelTrack(string actionName, string actionType, string finishReason)
        {
            bool isStart = actionName == "start";
            long nowMs = NowUnixMs();
            float maxProgress = actionType == "win" ? 1f : metrics.MaxProgress01;

            TelemetryEvent evt = NewEvent("level_track", includeDeviceMetadata: false)
                .Set("attempt_id", currentAttempt)
                .Set("level_id", activeLevelId)
                .Set("level_version", Settings.levelVersion ?? "1.0")
                .Set("action_name", actionName)
                .Set("action_type", actionType)
                .Set("start_time", metrics.LevelStartUnixMs)
                .Set("end_time", isStart ? (long?)null : nowMs)
                .Set("duration", metrics.LevelElapsedSec)
                .Set("death_count", metrics.DeathCount)
                .Set("respawn_count", metrics.RespawnCount)
                .Set("checkpoint_reached", metrics.DistinctCheckpointsReached)
                .Set("last_checkpoint_id", metrics.ActiveCheckpointId)
                .Set("max_progress", maxProgress)
                .Set("finish_reason", finishReason);

            if (!isStart && Settings.aggregateHighFrequencyTracks)
            {
                evt.Set("total_jump", metrics.TotalJumps)
                   .Set("total_obstacle_pass", metrics.TotalObstaclesPassed)
                   .SetOptional("test_mode", metrics.UsedTestMode);
            }

            Send(evt);
        }

        private void EmitCheckpointTrack(
            string cpId,
            int cpIndex,
            string prevCpId,
            float progressPercent,
            float timeFromPreviousCp)
        {
            Send(NewEvent("checkpoint_track", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("checkpoint_id", cpId)
                .Set("checkpoint_index", cpIndex)
                .Set("previous_checkpoint_id", prevCpId)
                .Set("progress_percent", progressPercent)
                .Set("time_from_level_start", metrics.LevelElapsedSec)
                .Set("time_from_previous_cp", timeFromPreviousCp));
        }

        private void EmitMovementTrack(string actionName, string direction, float moveDuration, float worldX)
        {
            TelemetryEvent evt = NewEvent("movement_track", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("action_name", actionName)
                .Set("direction", direction ?? "none");

            if (actionName == "move_stop")
                evt.Set("move_duration", moveDuration);

            evt.Set("world_x", worldX);
            Send(evt);
        }

        private void EmitJumpTrack(
            string actionName,
            int jumpId,
            Vector2 takeoff,
            string landingObjectId,
            float airTime,
            float horizontalDist,
            int? isSuccess)
        {
            Send(NewEvent("jump_track", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("jump_id", jumpId)
                .Set("action_name", actionName)
                .Set("jump_type", "normal")
                .Set("takeoff_x", takeoff.x)
                .Set("takeoff_y", takeoff.y)
                .Set("landing_object_id", landingObjectId)
                .Set("air_time", airTime)
                .Set("horizontal_distance", horizontalDist)
                .Set("is_success", isSuccess));
        }

        private void EmitObstacleTrack(
            string obstacleId,
            string obstacleType,
            string actionName,
            int attemptIndex,
            float timeToPass,
            string failureReason,
            float worldX,
            float worldY)
        {
            Send(NewEvent("obstacle_track", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("obstacle_id", obstacleId)
                .Set("obstacle_type", obstacleType)
                .Set("action_name", actionName)
                .Set("attempt_index", attemptIndex)
                .Set("time_to_pass", timeToPass)
                .Set("failure_reason", failureReason)
                .Set("world_x", worldX)
                .Set("world_y", worldY));
        }

        private void EmitChallengeTrack(string actionName, long endTimeMsOrZero)
        {
            if (string.IsNullOrEmpty(metrics.ActiveChallengeId))
                return;

            Send(NewEvent("challenge_track", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("challenge_id", metrics.ActiveChallengeId)
                .Set("action_name", actionName)
                .Set("challenge_attempt_id", metrics.ActiveChallengeAttemptId)
                .Set("attempt_index", metrics.ActiveChallengeAttemptIndex)
                .Set("start_time", metrics.ActiveChallengeStartMs)
                .Set("end_time", endTimeMsOrZero > 0 ? (long?)endTimeMsOrZero : null)
                .Set("duration", metrics.ActiveChallengeElapsedSec)
                .Set("death_count", metrics.ActiveChallengeDeaths));
        }

        private void EmitPlayerDeath(string cause, Vector2 pos)
        {
            Send(NewEvent("player_death", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("cause", cause)
                .Set("obstacle_id", metrics.ActiveObstacleId)
                .Set("world_x", pos.x)
                .Set("world_y", pos.y)
                .Set("last_safe_object_id", metrics.LastSupportedPlatform)
                .Set("checkpoint_id", metrics.ActiveCheckpointId)
                .Set("time_from_level_start", metrics.LevelElapsedSec));
        }

        private void EmitRespawnTrack(float respawnDelay)
        {
            Send(NewEvent("respawn_track", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("checkpoint_id", metrics.DeathCheckpointId)
                .Set("death_id", metrics.PendingDeathId)
                .Set("respawn_delay", respawnDelay)
                .Set("action_name", "respawn"));
        }

        private void EmitLevelFinish()
        {
            Send(NewEvent("level_finish", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("action_name", "goal_reached")
                .Set("total_duration", metrics.LevelElapsedSec)
                .Set("total_death", metrics.DeathCount)
                .Set("total_jump", metrics.TotalJumps)
                .Set("total_checkpoint", metrics.DistinctCheckpointsReached)
                .Set("total_obstacle_pass", metrics.TotalObstaclesPassed)
                .Set("completion_percent", 100f));
        }

        private void EmitPauseTrack(bool paused, float pauseDuration)
        {
            TelemetryEvent evt = NewEvent("pause_track", includeDeviceMetadata: false)
                .Set("level_id", hasActiveLevel ? activeLevelId : null)
                .Set("attempt_id", hasActiveLevel ? (int?)currentAttempt : null)
                .Set("action_name", paused ? "pause" : "resume");

            if (!paused)
                evt.Set("pause_duration", pauseDuration);

            Send(evt);
        }

        private void EmitCollectibleEvent(string actionName, string specCollectibleId, string rawId, Vector2 pos)
        {
            Send(NewEvent(actionName, includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("collectible_id", specCollectibleId)
                .Set("raw_id", rawId ?? string.Empty)
                .Set("world_x", pos.x)
                .Set("world_y", pos.y)
                .Set("collected_count", metrics.CollectiblesCollected)
                .Set("total_count", totalCollectibles)
                .Set("time_from_level_start", metrics.LevelElapsedSec));
        }

        private void EmitNewtonShot(
            string stepChallengeId,
            bool accepted,
            string reason,
            int accuracy,
            float dist,
            float tol,
            Vector2 pos)
        {
            Send(NewEvent("newton_shot", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("challenge_id", stepChallengeId)
                .Set("accepted", accepted)
                .Set("reason", reason ?? "NONE")
                .Set("accuracy", accuracy)
                .Set("dist", dist)
                .Set("tol", tol)
                .Set("world_x", pos.x)
                .Set("world_y", pos.y));
        }

        private void EmitNewtonLock(EasyNewtonFinaleController newton, string stepChallengeId, int accuracy)
        {
            TelemetryEvent evt = NewEvent("newton_lock", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("challenge_id", stepChallengeId)
                .Set("accuracy", accuracy)
                .Set("time_from_newton_enter", metrics.NewtonElapsedSec);

            if (stepChallengeId == "N-KP" && newton != null)
            {
                evt.Set("calc_lambda_nm", newton.CalculatedLambda_nm)
                   .Set("source_lambda_nm", newton.SourceLambda);
            }

            Send(evt);
        }

        private void EmitPlayerStuck(PlayerController pc)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastStuckRealtime < Settings.stuckCooldownSec)
                return;

            lastStuckRealtime = now;
            Vector2 pos = pc.transform.position;
            Send(NewEvent("player_stuck", includeDeviceMetadata: false)
                .Set("level_id", activeLevelId)
                .Set("attempt_id", currentAttempt)
                .Set("segment_id", activeSegmentId)
                .Set("world_x", pos.x)
                .Set("world_y", pos.y)
                .Set("stuck_duration", metrics.TotalStuckDurationSec)
                .Set("checkpoint_id", metrics.ActiveCheckpointId));
        }

        private void BeginSegmentChallenge(string challengeId, bool isRespawnAttempt)
        {
            if (string.IsNullOrEmpty(challengeId))
                return;

            long nowMs = NowUnixMs();
            metrics.BeginChallenge(challengeId, nowMs, out bool isFirstEnter);
            if (isFirstEnter && !isRespawnAttempt)
                EmitChallengeTrack("enter", 0);
            EmitChallengeTrack("attempt_start", 0);
        }

        private void CloseActiveChallenge(string finalActionName)
        {
            if (string.IsNullOrEmpty(metrics.ActiveChallengeId))
                return;

            EmitChallengeTrack(finalActionName, NowUnixMs());
        }

        private void FlushActiveMovement(PlayerController pc)
        {
            if (!hasActiveLevel || levelClosed)
                return;

            if (metrics.TryStopMovement(out string dir, out float duration))
            {
                float worldX = pc != null ? pc.transform.position.x : metrics.LastPos.x;
                EmitMovementTrack("move_stop", dir, duration, worldX);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Payload Construction & Network Transport
        // ──────────────────────────────────────────────────────────────

        private TelemetryEvent NewEvent(string action, bool includeDeviceMetadata)
        {
            TelemetryConfig.ApplySettings(Settings);
            TelemetrySettings s = Settings;
            long nowMs = NowUnixMs();
            string effectiveUser = !string.IsNullOrEmpty(s.overrideUserId)
                ? s.overrideUserId
                : (!string.IsNullOrEmpty(userId) ? userId : s.defaultUserFallback);

            TelemetryEvent evt = reusableEvent.Clear(action)
                .Set("game_id", s.gameId)
                .Set("collection", action)
                .Set("user", effectiveUser)
                .Set("session_id", sessionId ?? string.Empty)
                .Set("user_id", effectiveUser)
                .Set("timestamp", nowMs);

            if (includeDeviceMetadata || !s.sendDeviceMetadataOnlyOnSessionStart)
            {
                evt.Set("game_version", !string.IsNullOrEmpty(Application.version) ? Application.version : "1.0.0")
                   .Set("build_id", s.buildId ?? "build_01")
                   .Set("platform", Application.platform.ToString())
                   .Set("device_model", SystemInfo.deviceModel ?? string.Empty)
                   .Set("os_version", SystemInfo.operatingSystem ?? string.Empty)
                   .Set("country", s.country ?? "VN");
            }

            if (s.extraGlobalFields != null)
            {
                for (int i = 0; i < s.extraGlobalFields.Length; i++)
                {
                    TelemetrySettings.CustomField cf = s.extraGlobalFields[i];
                    if (!string.IsNullOrEmpty(cf.key))
                        evt.Set(cf.key.Trim(), cf.value ?? string.Empty);
                }
            }

            return evt;
        }

        private void Send(TelemetryEvent evt)
        {
            TelemetryConfig.ApplySettings(Settings);
            if (evt == null || !TelemetryConfig.IsActionEnabled(evt.Action))
                return;

            string json = evt.ToJson();
            recentPayloads.Add(json);
            int maxRecent = Mathf.Max(8, Settings.maxRecentPayloads);
            while (recentPayloads.Count > maxRecent)
                recentPayloads.RemoveAt(0);

            if (TelemetryConfig.LogToConsole)
                Debug.Log($"[TELEMETRY] {json}");

            if (TelemetryConfig.SuppressNetworkInTests || !Application.isPlaying)
                return;

            int maxQueue = Mathf.Max(8, Settings.maxPendingQueue);
            while (pendingPayloads.Count >= maxQueue)
                pendingPayloads.Dequeue();

            pendingPayloads.Enqueue(json);

            if (!isDispatching && gameObject.activeInHierarchy)
                StartCoroutine(DispatchQueueRoutine());
        }

        private IEnumerator DispatchQueueRoutine()
        {
            isDispatching = true;
            try
            {
                while (pendingPayloads.Count > 0)
                {
                    string json = pendingPayloads.Dequeue();
                    byte[] body = Encoding.UTF8.GetBytes(json);

                    using (UnityWebRequest req = new UnityWebRequest(Settings.endpointUrl, UnityWebRequest.kHttpVerbPOST))
                    {
                        req.uploadHandler = new UploadHandlerRaw(body);
                        req.downloadHandler = new DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        req.timeout = Mathf.Max(1, Settings.requestTimeoutSec);

                        yield return req.SendWebRequest();

                        if ((req.isNetworkError || req.isHttpError) && TelemetryConfig.LogToConsole)
                            Debug.LogWarning($"[TELEMETRY] Send failed ({req.responseCode}): {req.error}");
                    }

                    if (pendingPayloads.Count > 0 && Settings.sendDelaySeconds > 0f)
                        yield return new WaitForSecondsRealtime(Settings.sendDelaySeconds);
                }
            }
            finally
            {
                isDispatching = false;
            }
        }

        private void FlushPendingOnQuit()
        {
            if (!TelemetryConfig.Enabled || TelemetryConfig.SuppressNetworkInTests || !Application.isPlaying)
                return;

            while (pendingPayloads.Count > 0)
            {
                string json = pendingPayloads.Dequeue();
                try
                {
                    byte[] body = Encoding.UTF8.GetBytes(json);
                    UnityWebRequest req = new UnityWebRequest(Settings.endpointUrl, UnityWebRequest.kHttpVerbPOST);
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.timeout = Mathf.Max(1, Settings.quitFlushTimeoutSec);
                    req.SendWebRequest();
                }
                catch
                {
                    // Best-effort on shutdown
                }
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────

        private static long NowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private void InitializeIdentity()
        {
            TelemetrySettings s = Settings;
            if (string.IsNullOrEmpty(userId))
            {
                string prefKey = !string.IsNullOrEmpty(s.playerPrefsUserKey)
                    ? s.playerPrefsUserKey
                    : "Contrast_Telemetry_UserId";

                string saved = PlayerPrefs.GetString(prefKey, string.Empty);
                if (string.IsNullOrEmpty(saved))
                {
                    string prefix = s.userIdPrefix ?? "player_";
                    saved = prefix + Guid.NewGuid().ToString("N").Substring(0, 8);
                    PlayerPrefs.SetString(prefKey, saved);
                    PlayerPrefs.Save();
                }
                userId = saved;
            }

            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
                sessionStartUnixMs = NowUnixMs();
                lastForegroundMarkRealtime = Time.realtimeSinceStartup;
            }
        }

        private string ResolveCollectibleSpecId(QuangDiemItem item, int fallbackOrdinal)
        {
            int ord = fallbackOrdinal;
            if (!string.IsNullOrEmpty(item.ItemId) && collectibleOrdinalByRawId.TryGetValue(item.ItemId, out int idx))
                ord = idx;
            return ContrastSpecMapping.ResolveCollectibleId(activeLevelId, item.ItemId, ord);
        }

        private int ResolveCheckpointIndex(Vector2 cpPos)
        {
            float matchRadius = Settings.checkpointMatchRadius;
            int bestIndex = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < levelCheckpoints.Length; i++)
            {
                float d = Vector2.Distance(cpPos, levelCheckpoints[i]);
                if (d < matchRadius && d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i + 1;
                }
            }
            return bestIndex > 0 ? bestIndex : (metrics.ActiveCheckpointIndex + 1);
        }

        private static float ComputeLevelEndX(LevelData data, Vector2 spawnPos)
        {
            float maxX = spawnPos.x + 10f;
            if (data.newton != null && data.newton.k > 0)
                maxX = Mathf.Max(maxX, data.newton.cx / 64f);
            else if (LevelLoader.IsEasyLevel(data))
                maxX = Mathf.Max(maxX, 61.0f);

            if (data.end != null && data.end.position != null)
                maxX = Mathf.Max(maxX, data.end.position.x);

            if (data.checkPoint != null)
            {
                for (int i = 0; i < data.checkPoint.Length; i++)
                {
                    if (data.checkPoint[i] != null && data.checkPoint[i].position != null)
                        maxX = Mathf.Max(maxX, data.checkPoint[i].position.x);
                }
            }

            if (data.platforms != null)
            {
                for (int i = 0; i < data.platforms.Length; i++)
                {
                    if (data.platforms[i] != null && data.platforms[i].position != null)
                        maxX = Mathf.Max(maxX, data.platforms[i].position.x);
                }
            }
            return maxX;
        }

        private static string ResolveNormalizedLevelId(GameManager gm, LevelData data)
        {
            if (data != null && !string.IsNullOrEmpty(data.levelId))
                return ContrastSpecMapping.NormalizeLevelId(data.levelId);

            if (gm.CurrentLevelSource != null)
            {
                string path = gm.CurrentLevelSource.ResourcePath ?? gm.CurrentLevelSource.FilePath;
                if (!string.IsNullOrEmpty(path))
                    return ContrastSpecMapping.NormalizeLevelId(Path.GetFileNameWithoutExtension(path));
            }

            if (gm.CurrentCampaignIndex >= 0)
                return ContrastSpecMapping.NormalizeLevelId($"L{(gm.CurrentCampaignIndex + 1):D2}");

            return "custom_level";
        }

        private static void ComputeNewtonAttemptAccuracy(
            EasyNewtonFinaleController newton,
            int targetIndex,
            bool accepted,
            out float dist,
            out float tol,
            out int accuracy)
        {
            dist = -1f;
            tol = 0f;
            accuracy = accepted ? 100 : 0;

            if (targetIndex == 2)
            {
                tol = newton.ToleranceI;
                if (newton.PlatformI != null && newton.HasK)
                {
                    Vector2 targetPos = accepted ? newton.PointI : (newton.CenterO - newton.AxisU * Vector2.Distance(newton.CenterO, newton.PlatformI.transform.position));
                    dist = Vector2.Distance(newton.PlatformI.transform.position, targetPos);
                    if (accepted && tol > 0f)
                        accuracy = Mathf.Max(0, Mathf.RoundToInt(100f * (1f - Mathf.Clamp01(dist / tol))));
                }
            }
            else if (targetIndex == 3)
            {
                tol = newton.ToleranceKp;
                if (newton.PlatformK != null && newton.HasK)
                {
                    Vector2 targetPos = accepted ? newton.PointKp : (newton.CenterO - newton.AxisU * Vector2.Distance(newton.CenterO, newton.PlatformK.transform.position));
                    dist = Vector2.Distance(newton.PlatformK.transform.position, targetPos);
                    if (accepted && tol > 0f)
                        accuracy = Mathf.Max(0, Mathf.RoundToInt(100f * (1f - Mathf.Clamp01(dist / tol))));
                }
            }
        }
    }
}
