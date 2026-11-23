using System;
using System.Collections.Generic;
using Contrast.Color;
using Contrast.Data;
using Contrast.Level;
using Contrast.Player;
using UnityEngine;

namespace Contrast.Telemetry
{
    /// <summary>
    /// Lookup mapping aligned with the official CONTRAST Mapping Checkpoint sheet (L1, L2, L3, Quang Diem, Newton).
    /// </summary>
    public static class ContrastSpecMapping
    {
        public static string NormalizeLevelId(string rawLevelId)
        {
            if (string.IsNullOrEmpty(rawLevelId))
                return "custom_level";

            switch (rawLevelId.Trim().ToUpperInvariant())
            {
                case "L01":
                case "L1":
                    return "L1";
                case "L02":
                case "L2":
                    return "L2";
                case "L03":
                case "L3":
                    return "L3";
                default:
                    return rawLevelId.Trim();
            }
        }

        public static string FormatCheckpointId(string normalizedLevelId, int checkpointIndex)
        {
            if (checkpointIndex <= 0)
                return null;

            switch (normalizedLevelId)
            {
                case "L1": return $"L1-{checkpointIndex:D2}";
                case "L2": return $"L2-{checkpointIndex:D2}";
                case "L3": return $"L3-{checkpointIndex:D2}";
                default: return $"{normalizedLevelId}-CP{checkpointIndex:D2}";
            }
        }

        public static string ResolveActiveSegmentId(string normalizedLevelId, int activeCheckpointIndex, bool inNewtonZone)
        {
            if (inNewtonZone)
                return ResolveNewtonChallengeId(normalizedLevelId);

            switch (normalizedLevelId)
            {
                case "L1":
                    if (activeCheckpointIndex <= 0) return "L1-01";
                    if (activeCheckpointIndex == 1) return "L1-02";
                    if (activeCheckpointIndex == 2) return "L1-03";
                    if (activeCheckpointIndex == 3) return "L1-04";
                    return "L1-05";

                case "L2":
                    if (activeCheckpointIndex <= 0) return "L2-01";
                    if (activeCheckpointIndex == 1) return "L2-02";
                    if (activeCheckpointIndex == 2) return "L2-03";
                    if (activeCheckpointIndex == 3) return "L2-04";
                    return "L2-05";

                case "L3":
                    if (activeCheckpointIndex <= 1) return "L3-01"; // Spawn -> CP2 (crosses CP1)
                    if (activeCheckpointIndex == 2) return "L3-03"; // CP2 -> CP3
                    if (activeCheckpointIndex == 3) return "L3-04"; // CP3 -> CP4
                    if (activeCheckpointIndex <= 5) return "L3-05"; // CP4 -> CP6 (crosses CP5)
                    if (activeCheckpointIndex == 6) return "L3-07"; // CP6 -> CP7
                    return "L3-08";                                 // CP7 -> Newton

                default:
                    return $"{normalizedLevelId}-{(activeCheckpointIndex + 1):D2}";
            }
        }

        public static string ResolveNewtonChallengeId(string normalizedLevelId)
        {
            switch (normalizedLevelId)
            {
                case "L1": return "N-L1";
                case "L2": return "N-L2";
                case "L3": return "N-L3";
                default: return $"N-{normalizedLevelId}";
            }
        }

        public static string ResolveNewtonStepChallengeId(NewtonFinaleStep step)
        {
            switch (step)
            {
                case NewtonFinaleStep.CaptureK: return "N-K";
                case NewtonFinaleStep.CaptureI: return "N-I";
                case NewtonFinaleStep.CaptureKp: return "N-KP";
                default: return "N-KP";
            }
        }

        public static string ResolveCollectibleId(string normalizedLevelId, string rawId, int ordinal1Based)
        {
            if (!string.IsNullOrEmpty(rawId))
            {
                switch (rawId)
                {
                    case "item_mvnivkti_1": return "L01-Q1";
                    case "item_mvnivvcv_2": return "L01-Q2";
                    case "item_mvni3tly_1": return "L02-Q1";
                    case "L02_QD02_SECRET_UNDER_BLACK": return "L02-Q2";
                    case "L03_QD01_NGACH_GRAY": return "L03-Q1";
                    case "item_mvnod5fd_9": return "L03-Q2";
                    case "item_mvnopbeo_e": return "L03-Q3";
                }
            }

            if (normalizedLevelId == "L1") return $"L01-Q{ordinal1Based}";
            if (normalizedLevelId == "L2") return $"L02-Q{ordinal1Based}";
            if (normalizedLevelId == "L3") return $"L03-Q{ordinal1Based}";
            return $"{normalizedLevelId}-Q{ordinal1Based}";
        }

        public static bool IsSecretBranchCollectible(string specCollectibleId)
        {
            return specCollectibleId == "L02-Q2"
                || specCollectibleId == "L03-Q1"
                || specCollectibleId == "L03-Q3";
        }

        public static string ClassifyObstacleType(ColorPlatform plat)
        {
            if (plat == null)
                return "other";

            Trajectory traj = plat.GetTrajectory();
            if (traj != null && traj.IsEnabled)
                return "moving_platform";

            Vector2 size = plat.Size;
            if (size.y > size.x * 1.35f && size.y >= 1.8f)
                return "wall";

            return "other";
        }
    }

    /// <summary>
    /// Isolated per-attempt, per-checkpoint, per-challenge, jump, movement, and death/respawn state.
    /// Aligns runtime accumulators with FormLog Platformer &amp; Mapping Checkpoint.
    /// </summary>
    public sealed class LevelSessionMetrics
    {
        // Level attempt timing (Unix ms + active seconds)
        public long LevelStartUnixMs { get; private set; }
        public float LevelElapsedSec { get; private set; }
        public float AttemptTimeSec { get; private set; }
        public float TimeSincePreviousCpSec { get; private set; }
        public float NewtonElapsedSec { get; private set; }
        public bool NewtonEnterTracked { get; set; }
        public float TotalStuckDurationSec { get; private set; }

        // Color & Phase stats (aggregated summary)
        public LogicalColor LastColor { get; private set; } = LogicalColor.White;
        public float LastPhase { get; private set; }
        public int ColorShiftsTotal { get; private set; }
        public int ColorShiftsWhite { get; private set; }
        public int ColorShiftsGray { get; private set; }
        public int ColorShiftsBlack { get; private set; }
        public float PhaseTuneTimeSec { get; private set; }

        // Spatial & Progress
        public Vector2 SpawnPos { get; private set; }
        public Vector2 LastPos { get; private set; }
        public Vector2 LastSupportedPos { get; private set; }
        public string LastSupportedPlatform { get; private set; }
        public float MaxPlayerX { get; private set; }
        public float TargetEndX { get; private set; }
        public float MaxProgress01 { get; private set; }

        // Level Totals (FormLog: level_track, level_finish)
        public int DeathCount { get; private set; }
        public int RespawnCount { get; private set; }
        public int DistinctCheckpointsReached { get; private set; }
        public int ActiveCheckpointIndex { get; set; }
        public string ActiveCheckpointId { get; set; }
        public string PreviousCheckpointId { get; set; }
        public int TotalJumps { get; private set; }
        public int TotalJumpRequests { get; private set; }
        public int TotalObstaclesPassed { get; private set; }
        public int CollectiblesCollected { get; set; }
        public int StuckEventsCount { get; private set; }
        public bool UsedTestMode { get; set; }

        // Newton Totals
        public bool NewtonZoneEntered { get; set; }
        public bool NewtonDocked { get; set; }
        public bool NewtonCompleted { get; set; }
        public int NewtonMarkAttempts { get; private set; }
        public int NewtonMarkFailures { get; private set; }
        public int NewtonAccuracyI { get; private set; } = -1;
        public int NewtonAccuracyKp { get; private set; } = -1;

        // Challenge Tracking (challenge_track)
        public string ActiveChallengeId { get; private set; }
        public string ActiveChallengeAttemptId { get; private set; }
        public int ActiveChallengeAttemptIndex { get; private set; }
        public long ActiveChallengeStartMs { get; private set; }
        public float ActiveChallengeElapsedSec { get; private set; }
        public int ActiveChallengeDeaths { get; private set; }

        // Death <-> Respawn pairing
        public bool WaitingForRespawn { get; set; }
        public string PendingDeathId { get; set; }
        public float DeathRealtime { get; set; }
        public string DeathCheckpointId { get; set; }

        // Jump Tracking
        public bool HasActiveJump { get; private set; }
        public int ActiveJumpId { get; private set; }
        public Vector2 JumpTakeoffPos { get; private set; }
        public float JumpElapsedSec { get; private set; }

        // Movement Tracking
        public bool IsMoving { get; private set; }
        public string CurrentMoveDirection { get; private set; } = "none";
        public float CurrentMoveDurationSec { get; private set; }

        // Sets for once-per-attempt deduplication
        public readonly HashSet<int> ActivatedCheckpoints = new HashSet<int>();
        public readonly HashSet<string> EnteredChallenges = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> ChallengeAttemptCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly HashSet<string> SeenCollectibles = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> EnteredSecretRoutes = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> ObstacleAttemptCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly HashSet<string> PassedObstacles = new HashSet<string>(StringComparer.Ordinal);
        public string ActiveObstacleId { get; set; }
        public string ActiveObstacleType { get; set; }
        public float ActiveObstacleEnterSec { get; set; }

        private bool hasColorSample;
        private bool hasPosSample;
        private bool wasStuckPrevFrame;
        private int prevJumpRequests;
        private int prevJumpsExecuted;
        private int jumpSequence;

        public void ResetLevel(
            Vector2 spawnPos,
            LogicalColor spawnColor,
            float targetEndX,
            int initialJumpRequests,
            int initialJumpsExecuted,
            long nowMs)
        {
            LevelStartUnixMs = nowMs;
            LevelElapsedSec = 0f;
            AttemptTimeSec = 0f;
            TimeSincePreviousCpSec = 0f;
            NewtonElapsedSec = 0f;
            NewtonEnterTracked = false;
            TotalStuckDurationSec = 0f;

            LastColor = spawnColor;
            LastPhase = 0f;
            hasColorSample = true;
            ColorShiftsTotal = 0;
            ColorShiftsWhite = 0;
            ColorShiftsGray = 0;
            ColorShiftsBlack = 0;
            PhaseTuneTimeSec = 0f;

            SpawnPos = spawnPos;
            LastPos = spawnPos;
            LastSupportedPos = spawnPos;
            LastSupportedPlatform = null;
            MaxPlayerX = spawnPos.x;
            TargetEndX = Mathf.Max(spawnPos.x + 1f, targetEndX);
            MaxProgress01 = 0f;
            hasPosSample = true;

            DeathCount = 0;
            RespawnCount = 0;
            DistinctCheckpointsReached = 0;
            ActiveCheckpointIndex = 0;
            ActiveCheckpointId = null;
            PreviousCheckpointId = null;
            TotalJumps = 0;
            TotalJumpRequests = 0;
            TotalObstaclesPassed = 0;
            CollectiblesCollected = 0;
            StuckEventsCount = 0;
            UsedTestMode = false;

            NewtonZoneEntered = false;
            NewtonDocked = false;
            NewtonCompleted = false;
            NewtonMarkAttempts = 0;
            NewtonMarkFailures = 0;
            NewtonAccuracyI = -1;
            NewtonAccuracyKp = -1;

            ActiveChallengeId = null;
            ActiveChallengeAttemptId = null;
            ActiveChallengeAttemptIndex = 0;
            ActiveChallengeStartMs = nowMs;
            ActiveChallengeElapsedSec = 0f;
            ActiveChallengeDeaths = 0;

            WaitingForRespawn = false;
            PendingDeathId = null;
            DeathRealtime = 0f;
            DeathCheckpointId = null;

            HasActiveJump = false;
            ActiveJumpId = 0;
            jumpSequence = 0;
            JumpElapsedSec = 0f;

            IsMoving = false;
            CurrentMoveDirection = "none";
            CurrentMoveDurationSec = 0f;

            wasStuckPrevFrame = false;
            prevJumpRequests = initialJumpRequests;
            prevJumpsExecuted = initialJumpsExecuted;

            ActivatedCheckpoints.Clear();
            EnteredChallenges.Clear();
            ChallengeAttemptCounts.Clear();
            SeenCollectibles.Clear();
            EnteredSecretRoutes.Clear();
            ObstacleAttemptCounts.Clear();
            PassedObstacles.Clear();
            ActiveObstacleId = null;
            ActiveObstacleType = null;
            ActiveObstacleEnterSec = 0f;
        }

        public void BeginChallenge(string challengeId, long nowMs, out bool isFirstEnterInAttempt)
        {
            ActiveChallengeId = challengeId;
            isFirstEnterInAttempt = EnteredChallenges.Add(challengeId);

            int nextIdx = 1;
            if (ChallengeAttemptCounts.TryGetValue(challengeId, out int prev))
                nextIdx = prev + 1;
            ChallengeAttemptCounts[challengeId] = nextIdx;

            ActiveChallengeAttemptIndex = nextIdx;
            ActiveChallengeAttemptId = $"{challengeId}_{nextIdx}";
            ActiveChallengeStartMs = nowMs;
            ActiveChallengeElapsedSec = 0f;
            if (isFirstEnterInAttempt)
                ActiveChallengeDeaths = 0;
        }

        public void ResetSpawnTracking()
        {
            AttemptTimeSec = 0f;
            HasActiveJump = false;
            IsMoving = false;
            CurrentMoveDirection = "none";
            CurrentMoveDurationSec = 0f;
            wasStuckPrevFrame = false;
            hasPosSample = false;
        }

        public bool TryStopMovement(out string direction, out float duration)
        {
            if (!IsMoving)
            {
                direction = "none";
                duration = 0f;
                return false;
            }

            direction = CurrentMoveDirection;
            duration = CurrentMoveDurationSec;
            IsMoving = false;
            CurrentMoveDirection = "none";
            CurrentMoveDurationSec = 0f;
            return true;
        }

        public struct FrameTransitions
        {
            public bool JumpStarted;
            public bool JumpLanded;
            public string JumpLandingObjectId;
            public float JumpAirTime;
            public float JumpHorizontalDist;
            public Vector2 JumpTakeoff;
            public int JumpId;

            public bool MoveStarted;
            public bool MoveStopped;
            public string MoveStopDirection;
            public string MoveStartDirection;
            public float MoveDuration;
            public float MoveWorldX;

            public bool StuckStarted;
        }

        public FrameTransitions SampleFrame(PlayerController pc, float dt, TelemetrySettings settings)
        {
            FrameTransitions tr = default(FrameTransitions);
            if (pc == null || dt < 0f)
                return tr;

            float maxDt = settings != null ? settings.maxFrameDeltaTime : 0.25f;
            float phaseEps = settings != null ? settings.phaseTuneEpsilon : 0.0005f;
            dt = Mathf.Min(dt, maxDt);

            LevelElapsedSec += dt;
            AttemptTimeSec += dt;
            TimeSincePreviousCpSec += dt;
            ActiveChallengeElapsedSec += dt;
            if (NewtonZoneEntered && !NewtonCompleted)
                NewtonElapsedSec += dt;

            // Color & Phase tracking
            LogicalColor curColor = pc.CurrentColor;
            if (hasColorSample && curColor != LastColor)
            {
                ColorShiftsTotal++;
                switch (curColor)
                {
                    case LogicalColor.White: ColorShiftsWhite++; break;
                    case LogicalColor.Gray: ColorShiftsGray++; break;
                    case LogicalColor.Black: ColorShiftsBlack++; break;
                }
            }
            if (Mathf.Abs(pc.CurrentPhase - LastPhase) > phaseEps)
                PhaseTuneTimeSec += dt;
            LastColor = curColor;
            LastPhase = pc.CurrentPhase;
            hasColorSample = true;

            // Spatial & progress tracking
            Vector2 pos = pc.transform.position;
            if (!hasPosSample)
            {
                LastPos = pos;
                hasPosSample = true;
            }
            else
            {
                LastPos = pos;
            }

            if (pos.x > MaxPlayerX)
                MaxPlayerX = pos.x;

            float span = Mathf.Max(1f, TargetEndX - SpawnPos.x);
            float p01 = Mathf.Clamp01((MaxPlayerX - SpawnPos.x) / span);
            if (p01 > MaxProgress01)
                MaxProgress01 = p01;

            if (pc.IsGrounded && pc.GroundedPlatform != null)
            {
                LastSupportedPos = pos;
                LastSupportedPlatform = pc.GroundedPlatform.name;
            }

            // Movement tracking
            float moveX = pc.LastMoveInputX;
            string dir = moveX > 0.1f ? "right" : (moveX < -0.1f ? "left" : "none");
            if (IsMoving)
            {
                CurrentMoveDurationSec += dt;
                if (dir != CurrentMoveDirection)
                {
                    tr.MoveStopped = true;
                    tr.MoveStopDirection = CurrentMoveDirection;
                    tr.MoveDuration = CurrentMoveDurationSec;
                    tr.MoveWorldX = pos.x;
                    IsMoving = false;
                    CurrentMoveDurationSec = 0f;
                }
            }
            if (!IsMoving && dir != "none")
            {
                IsMoving = true;
                CurrentMoveDirection = dir;
                CurrentMoveDurationSec = 0f;
                tr.MoveStarted = true;
                tr.MoveStartDirection = dir;
                tr.MoveWorldX = pos.x;
            }
            else if (!IsMoving)
            {
                CurrentMoveDirection = "none";
            }

            // Jump tracking
            int reqDelta = Mathf.Max(0, pc.TotalJumpRequests - prevJumpRequests);
            int execDelta = Mathf.Max(0, pc.TotalJumpsExecuted - prevJumpsExecuted);
            prevJumpRequests = pc.TotalJumpRequests;
            prevJumpsExecuted = pc.TotalJumpsExecuted;

            if (reqDelta > 0)
                TotalJumpRequests += reqDelta;

            if (HasActiveJump)
                JumpElapsedSec += dt;

            if (execDelta > 0)
            {
                TotalJumps += execDelta;
                HasActiveJump = true;
                ActiveJumpId = ++jumpSequence;
                JumpTakeoffPos = pos;
                JumpElapsedSec = 0f;
                tr.JumpStarted = true;
                tr.JumpId = ActiveJumpId;
                tr.JumpTakeoff = pos;
            }
            else if (HasActiveJump && pc.IsGrounded && JumpElapsedSec > 0.02f)
            {
                tr.JumpLanded = true;
                tr.JumpId = ActiveJumpId;
                tr.JumpTakeoff = JumpTakeoffPos;
                tr.JumpAirTime = JumpElapsedSec;
                tr.JumpHorizontalDist = Mathf.Abs(pos.x - JumpTakeoffPos.x);
                tr.JumpLandingObjectId = pc.GroundedPlatform != null ? pc.GroundedPlatform.name : "ground";
                HasActiveJump = false;
            }

            // Stuck tracking
            bool isStuck = pc.State == PlayerState.Stuck;
            if (isStuck)
                TotalStuckDurationSec += dt;
            if (isStuck && !wasStuckPrevFrame)
            {
                StuckEventsCount++;
                tr.StuckStarted = true;
            }
            wasStuckPrevFrame = isStuck;

            return tr;
        }

        public bool RecordDeath(out string deathId)
        {
            DeathCount++;
            ActiveChallengeDeaths++;
            deathId = $"D{DeathCount}";
            PendingDeathId = deathId;
            DeathRealtime = Time.realtimeSinceStartup;
            DeathCheckpointId = ActiveCheckpointId;
            WaitingForRespawn = true;

            bool hadOpenJump = HasActiveJump;
            HasActiveJump = false;
            return hadOpenJump;
        }

        public float CompleteRespawn()
        {
            WaitingForRespawn = false;
            RespawnCount++;
            return Mathf.Max(0f, Time.realtimeSinceStartup - DeathRealtime);
        }

        public bool RecordCheckpoint(int checkpointIndex, string checkpointId, out string prevCheckpointId, out float segTimeSec)
        {
            prevCheckpointId = ActiveCheckpointId;
            segTimeSec = TimeSincePreviousCpSec;

            if (!ActivatedCheckpoints.Add(checkpointIndex))
                return false;

            PreviousCheckpointId = ActiveCheckpointId;
            ActiveCheckpointIndex = checkpointIndex;
            ActiveCheckpointId = checkpointId;
            DistinctCheckpointsReached = ActivatedCheckpoints.Count;
            TimeSincePreviousCpSec = 0f;
            return true;
        }

        public int EnterObstacle(string obstacleId, string obstacleType)
        {
            ActiveObstacleId = obstacleId;
            ActiveObstacleType = obstacleType;
            ActiveObstacleEnterSec = LevelElapsedSec;

            int attempt = 1;
            if (ObstacleAttemptCounts.TryGetValue(obstacleId, out int prev))
                attempt = prev + 1;
            ObstacleAttemptCounts[obstacleId] = attempt;
            return attempt;
        }

        public bool PassActiveObstacle(out float timeToPass)
        {
            timeToPass = Mathf.Max(0f, LevelElapsedSec - ActiveObstacleEnterSec);
            if (string.IsNullOrEmpty(ActiveObstacleId))
                return false;

            string id = ActiveObstacleId;
            ActiveObstacleId = null;
            if (PassedObstacles.Add(id))
                TotalObstaclesPassed++;
            return true;
        }

        public void RecordNewtonAttempt(int targetIndex, bool accepted, int accuracy)
        {
            NewtonMarkAttempts++;
            if (!accepted)
            {
                NewtonMarkFailures++;
            }
            else
            {
                if (targetIndex == 2) NewtonAccuracyI = accuracy;
                else if (targetIndex == 3) NewtonAccuracyKp = accuracy;
            }
        }
    }
}
