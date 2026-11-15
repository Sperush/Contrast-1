namespace Contrast.UI
{
    using System.Collections.Generic;
    using System.IO;
    using Contrast.Color;
    using Contrast.Data;
    using UnityEngine;
    using UnityEngine.UI;

    internal sealed class DemoPlatformActor
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 WorldPos;
        public Vector2 WorldSize;
        public float RawGrayscale;
        public LogicalColor Logical;
        public bool IsUniversal;
    }

    /// <summary>
    /// Cluster 4a: Automated Mini-Game Simulation Engine for the Main Menu Showcase.
    /// Loads <c>DemoLobbyLevel.json</c>, runs the 10-stage bot input state machine,
    /// steps 2D AABB platform &amp; phase collision physics, and computes smooth 2D Camera Follow.
    /// </summary>
    internal sealed class MainMenuDemoSimulation
    {
        private const float DemoMoveSpeed = 5.0f;
        private const float DemoGravity = 28.0f;
        private const float DemoJumpSpeed = 10.8f;

        private readonly List<DemoPlatformActor> demoPlatforms = new List<DemoPlatformActor>();

        public LevelData LevelData { get; private set; }
        public List<DemoPlatformActor> Platforms => demoPlatforms;

        public MainMenuUI.DemoBotStage BotStage { get; private set; } = MainMenuUI.DemoBotStage.RunInBlackToWall;
        public float StageTimer { get; private set; }
        public bool StageActionTriggered { get; private set; }
        public int CelebrateHopCount { get; private set; }

        public Vector2 PlayerWorldPos { get; private set; }
        public Vector2 PlayerSize { get; private set; } = new Vector2(0.625f, 1.0f);
        public float PlayerVelY { get; private set; }
        public bool PlayerGrounded { get; private set; }
        public DemoPlatformActor GroundedPlatform { get; private set; }
        public LogicalColor PlayerColor { get; private set; } = LogicalColor.Black;

        public bool QuangDiemCollected { get; private set; }
        public int NewtonMarksCount { get; private set; }
        public float ConnectProgress { get; private set; }
        public float IrisNormalized { get; private set; } = 1f;
        public bool ResetDoneInIris { get; private set; }
        public Vector2 SuckStartPosPx { get; private set; }

        public Vector2 CameraPosPx { get; private set; }
        private Vector2 cameraVelPx;
        public Vector2 NewtonCenterPx { get; set; } = new Vector2(1048f, -428.8f);

        public static LevelData LoadDemoLobbyLevelData()
        {
            string rawJson = null;

            TextAsset asset = Resources.Load<TextAsset>(MainMenuUI.DemoLobbyLevelResourcePath);
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                rawJson = asset.text;
            }
            else
            {
                string[] candidatePaths =
                {
                    Path.Combine("Assets", "Resources", "Levels", "DemoLobbyLevel.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", "Levels", "DemoLobbyLevel.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Assets", "Resources", "Levels", "DemoLobbyLevel.json")
                };

                for (int i = 0; i < candidatePaths.Length; i++)
                {
                    if (File.Exists(candidatePaths[i]))
                    {
                        rawJson = File.ReadAllText(candidatePaths[i]);
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                LevelData parsed = JsonUtility.FromJson<LevelData>(rawJson);
                if (parsed != null && parsed.platforms != null && parsed.platforms.Length > 0)
                    return parsed;
            }

            return new LevelData
            {
                levelId = "DemoLobbyLevel",
                levelName = "Mô phỏng hướng dẫn sảnh chờ",
                startPos = new StartPosData
                {
                    position = new Vector2Data(-10.25f, -4.45f),
                    size = new Vector2Data(0.625f, 1.0f),
                    color = 212f
                },
                cameraBoundary = new CameraBoundaryData
                {
                    position = new Vector2Data(0f, -3.5f),
                    size = new Vector2Data(24.0f, 6.0f)
                },
                platforms = new[]
                {
                    new ColorPlatformData { position = new Vector2Data(0.0f, -5.125f), size = new Vector2Data(24.0f, 0.35f), rawGrayscaleColor = 255f },
                    new ColorPlatformData { position = new Vector2Data(-6.0f, -3.70f), size = new Vector2Data(0.45f, 2.50f), rawGrayscaleColor = 0f },
                    new ColorPlatformData { position = new Vector2Data(-1.0f, -3.55f), size = new Vector2Data(4.8f, 0.25f), rawGrayscaleColor = 127f },
                    new ColorPlatformData { position = new Vector2Data(-0.2f, -4.55f), size = new Vector2Data(4.2f, 0.25f), rawGrayscaleColor = 212f },
                    new ColorPlatformData { position = new Vector2Data(5.8f, -3.35f), size = new Vector2Data(6.0f, 0.25f), rawGrayscaleColor = 0f }
                },
                newton = new NewtonChamberData
                {
                    enabled = true,
                    cx = 1048.0f,
                    cy = -428.8f,
                    i = 2,
                    k = 4,
                    lambda = 550f,
                    orbitStart = "auto"
                },
                collectibles = new[]
                {
                    new CollectibleData
                    {
                        id = "demo_lobby_quang_diem_1",
                        kind = "collectible",
                        position = new Vector2Data(-0.25f, -3.95f),
                        size = new Vector2Data(0.35f, 0.35f)
                    }
                },
                deathY = -12f
            };
        }

        public void InitializeLevelData()
        {
            LevelData = LoadDemoLobbyLevelData();
            if (LevelData != null && LevelData.newton != null)
            {
                NewtonCenterPx = new Vector2(LevelData.newton.cx, LevelData.newton.cy);
            }
            else
            {
                NewtonCenterPx = new Vector2(1048f, -428.8f);
            }
        }

        public void ResetFull(out bool resetVisualMarkers)
        {
            if (LevelData == null)
                InitializeLevelData();

            Vector2 spawnPos = new Vector2(-10.25f, -4.45f);
            float rawCol = 212f;
            if (LevelData != null && LevelData.startPos != null)
            {
                if (LevelData.startPos.position != null)
                    spawnPos = LevelData.startPos.position.ToVector2();
                if (LevelData.startPos.size != null)
                    PlayerSize = LevelData.startPos.size.ToVector2();
                rawCol = LevelData.startPos.color;
            }

            GroundedPlatform = demoPlatforms.Count > 0 ? demoPlatforms[0] : null;
            if (GroundedPlatform != null)
            {
                float floorTop = GroundedPlatform.WorldPos.y + GroundedPlatform.WorldSize.y * 0.5f;
                spawnPos.y = floorTop + PlayerSize.y * 0.5f;
            }

            PlayerWorldPos = spawnPos;
            PlayerVelY = 0f;
            PlayerGrounded = true;
            PlayerColor = ColorClassifier.Classify(rawCol);
            if (PlayerColor == LogicalColor.Universal)
                PlayerColor = LogicalColor.Black;

            BotStage = MainMenuUI.DemoBotStage.RunInBlackToWall;
            StageTimer = 0f;
            StageActionTriggered = false;
            CelebrateHopCount = 0;
            QuangDiemCollected = false;
            NewtonMarksCount = 0;
            ConnectProgress = 0f;
            ResetDoneInIris = false;

            CameraPosPx = ComputeDesiredCameraPosPx();
            cameraVelPx = Vector2.zero;
            resetVisualMarkers = true;
        }

        public void ResetPositionAndMarkersInsideIris(out bool resetVisualMarkers)
        {
            Vector2 spawnPos = new Vector2(-10.25f, -4.45f);
            float rawCol = 212f;
            if (LevelData != null && LevelData.startPos != null)
            {
                if (LevelData.startPos.position != null)
                    spawnPos = LevelData.startPos.position.ToVector2();
                rawCol = LevelData.startPos.color;
            }

            GroundedPlatform = demoPlatforms.Count > 0 ? demoPlatforms[0] : null;
            if (GroundedPlatform != null)
            {
                float floorTop = GroundedPlatform.WorldPos.y + GroundedPlatform.WorldSize.y * 0.5f;
                spawnPos.y = floorTop + PlayerSize.y * 0.5f;
            }

            PlayerWorldPos = spawnPos;
            PlayerVelY = 0f;
            PlayerGrounded = true;
            PlayerColor = ColorClassifier.Classify(rawCol);
            if (PlayerColor == LogicalColor.Universal)
                PlayerColor = LogicalColor.Black;

            QuangDiemCollected = false;
            NewtonMarksCount = 0;
            ConnectProgress = 0f;

            CameraPosPx = new Vector2(
                Mathf.Clamp(spawnPos.x * MainMenuUI.DemoPixelsPerUnit + 96f, -1360f, 1360f),
                Mathf.Clamp((spawnPos.y - PlayerSize.y * 0.5f) * MainMenuUI.DemoPixelsPerUnit + 96f, -700f, -280f));
            cameraVelPx = Vector2.zero;
            resetVisualMarkers = true;
        }

        public Vector2 ComputeDesiredCameraPosPx()
        {
            Vector2 feetPosPx = new Vector2(
                PlayerWorldPos.x * MainMenuUI.DemoPixelsPerUnit,
                (PlayerWorldPos.y - PlayerSize.y * 0.5f) * MainMenuUI.DemoPixelsPerUnit);

            Vector2 target;
            if (BotStage >= MainMenuUI.DemoBotStage.ReachAndPickThreeHorizontalPoints)
            {
                target = new Vector2(NewtonCenterPx.x - 64f, -456f);
            }
            else
            {
                target = new Vector2(feetPosPx.x + 96f, feetPosPx.y + 96f);
            }

            target.x = Mathf.Clamp(target.x, -1360f, 1360f);
            target.y = Mathf.Clamp(target.y, -700f, -280f);
            return target;
        }

        public void StepSimulation(float safeDt, out MainMenuUI.DemoBotInput lastBotInput, out string ruleText, out bool didResetVisualMarkers)
        {
            ruleText = "Cùng màu → đi xuyên qua vật cản   ·   Khác màu → đứng vững trên bệ đỡ";
            lastBotInput = default;
            didResetVisualMarkers = false;

            float remaining = safeDt;
            const float maxStep = 1f / 120f;
            while (remaining > 0.00001f)
            {
                float step = Mathf.Min(remaining, maxStep);
                remaining -= step;
                StageTimer += step;

                lastBotInput = EvaluateBotInputForCurrentStage(out ruleText, out bool stepResetMarkers);
                if (stepResetMarkers)
                    didResetVisualMarkers = true;

                if (BotStage != MainMenuUI.DemoBotStage.SuckedIntoNewtonCenter &&
                    BotStage != MainMenuUI.DemoBotStage.CircularIrisCloseAndReset)
                {
                    StepDemoPhysicsWithInput(lastBotInput, step);
                }
            }

            Vector2 desiredCamPx = ComputeDesiredCameraPosPx();
            if (BotStage == MainMenuUI.DemoBotStage.CircularIrisCloseAndReset && ResetDoneInIris)
            {
                CameraPosPx = desiredCamPx;
                cameraVelPx = Vector2.zero;
            }
            else
            {
                CameraPosPx = Vector2.SmoothDamp(CameraPosPx, desiredCamPx, ref cameraVelPx, 0.22f, 2600f, safeDt);
            }
        }

        private void TransitionToDemoStage(MainMenuUI.DemoBotStage nextStage)
        {
            BotStage = nextStage;
            StageTimer = 0f;
            StageActionTriggered = false;
            CelebrateHopCount = 0;
        }

        private MainMenuUI.DemoBotInput EvaluateBotInputForCurrentStage(out string ruleCaption, out bool didResetVisualMarkers)
        {
            MainMenuUI.DemoBotInput input = default;
            ruleCaption = "Cùng màu → đi xuyên qua vật cản   ·   Khác màu → đứng vững trên bệ đỡ";
            didResetVisualMarkers = false;

            switch (BotStage)
            {
                case MainMenuUI.DemoBotStage.RunInBlackToWall:
                    input.MoveAxisX = 1f;
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  INPUT: [D] DI CHUYỂN TỚI TƯỜNG TRẮNG";
                    ruleCaption = "PHA ĐEN TIẾN ĐẾN TƯỜNG TRẮNG  ·  CHUẨN BỊ ĐỔI PHA MÀU ĐỂ ĐI XUYÊN QUA...";
                    if (PlayerWorldPos.x >= -7.40f || StageTimer > 1.2f)
                    {
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.ShiftWhitePassThroughWall);
                    }
                    break;

                case MainMenuUI.DemoBotStage.ShiftWhitePassThroughWall:
                    input.MoveAxisX = 1f;
                    if (!StageActionTriggered)
                    {
                        StageActionTriggered = true;
                        input.HasColorChange = true;
                        input.RequestedColor = LogicalColor.White;
                    }
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  INPUT: [←/→] PHA TRẮNG + [D] ĐI XUYÊN";
                    ruleCaption = "ĐỔI SANG PHA TRẮNG (CÙNG MÀU TƯỜNG TRẮNG)  →  ĐI XUYÊN QUA VẬT CẢN!";
                    if (PlayerWorldPos.x >= -4.45f || StageTimer > 1.2f)
                    {
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.JumpOntoGrayBridge);
                    }
                    break;

                case MainMenuUI.DemoBotStage.JumpOntoGrayBridge:
                    input.MoveAxisX = 1f;
                    if (!StageActionTriggered && PlayerGrounded)
                    {
                        StageActionTriggered = true;
                        input.JumpPressed = true;
                    }
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  INPUT: [SPACE] NHẢY LÊN BỆ XÁM";
                    ruleCaption = "KHÁC MÀU (TRẮNG ≠ XÁM)  →  NHẢY LÊN & ĐỨNG VỮNG TRÊN BỆ XÁM!";
                    if (StageActionTriggered && PlayerGrounded && PlayerWorldPos.x >= -0.25f)
                    {
                        PlayerWorldPos = new Vector2(-0.25f, PlayerWorldPos.y);
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.ShiftGrayDropThroughBridge);
                    }
                    else if (StageTimer > 2.0f)
                    {
                        GroundedPlatform = demoPlatforms.Count > 2 ? demoPlatforms[2] : null;
                        float bridgeTop = GroundedPlatform != null
                            ? (GroundedPlatform.WorldPos.y + GroundedPlatform.WorldSize.y * 0.5f)
                            : -3.425f;
                        PlayerWorldPos = new Vector2(-0.25f, bridgeTop + PlayerSize.y * 0.5f);
                        PlayerVelY = 0f;
                        PlayerGrounded = true;
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.ShiftGrayDropThroughBridge);
                    }
                    break;

                case MainMenuUI.DemoBotStage.ShiftGrayDropThroughBridge:
                    input.MoveAxisX = 0f;
                    if (StageTimer >= 0.24f && !StageActionTriggered)
                    {
                        StageActionTriggered = true;
                        input.HasColorChange = true;
                        input.RequestedColor = LogicalColor.Gray;
                    }
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  INPUT: [←/→] PHA XÁM (RƠI XUYÊN BỆ)";
                    ruleCaption = "ĐỔI SANG PHA XÁM (CÙNG MÀU BỆ XÁM)  →  RƠI XUYÊN XUỐNG & THU QUANG ĐIỂM!";
                    if (StageActionTriggered && PlayerGrounded && StageTimer >= 0.72f)
                    {
                        QuangDiemCollected = true;
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.ShiftBlackJumpToNewtonStage);
                    }
                    else if (StageTimer > 1.8f)
                    {
                        PlayerColor = LogicalColor.Gray;
                        QuangDiemCollected = true;
                        GroundedPlatform = demoPlatforms.Count > 3 ? demoPlatforms[3] : null;
                        float pedTop = GroundedPlatform != null
                            ? (GroundedPlatform.WorldPos.y + GroundedPlatform.WorldSize.y * 0.5f)
                            : -4.425f;
                        PlayerWorldPos = new Vector2(-0.25f, pedTop + PlayerSize.y * 0.5f);
                        PlayerVelY = 0f;
                        PlayerGrounded = true;
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.ShiftBlackJumpToNewtonStage);
                    }
                    break;

                case MainMenuUI.DemoBotStage.ShiftBlackJumpToNewtonStage:
                    input.MoveAxisX = 1f;
                    if (!StageActionTriggered && PlayerWorldPos.x >= 1.15f && PlayerGrounded)
                    {
                        StageActionTriggered = true;
                        input.JumpPressed = true;
                    }
                    if (StageActionTriggered && PlayerColor != LogicalColor.Black && PlayerVelY > 0f)
                    {
                        input.HasColorChange = true;
                        input.RequestedColor = LogicalColor.Black;
                    }
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  INPUT: [SPACE] NHẢY + [←/→] PHA ĐEN";
                    ruleCaption = "ĐỔI SANG PHA ĐEN (KHÁC MÀU BỆ TRẮNG)  →  TIẾN RA BÊN VÂN TRÒN NEWTON!";
                    if (PlayerWorldPos.x >= 5.40f && PlayerGrounded && PlayerWorldPos.y > -3.0f)
                    {
                        PlayerWorldPos = new Vector2(5.40f, PlayerWorldPos.y);
                        PlayerColor = LogicalColor.Black;
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.ReachAndPickThreeHorizontalPoints);
                    }
                    else if (StageTimer > 2.4f)
                    {
                        PlayerColor = LogicalColor.Black;
                        GroundedPlatform = demoPlatforms.Count > 4 ? demoPlatforms[4] : null;
                        float stepTop = GroundedPlatform != null
                            ? (GroundedPlatform.WorldPos.y + GroundedPlatform.WorldSize.y * 0.5f)
                            : -3.225f;
                        PlayerWorldPos = new Vector2(5.40f, stepTop + PlayerSize.y * 0.5f);
                        PlayerVelY = 0f;
                        PlayerGrounded = true;
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.ReachAndPickThreeHorizontalPoints);
                    }
                    break;

                case MainMenuUI.DemoBotStage.ReachAndPickThreeHorizontalPoints:
                    input.MoveAxisX = 0f;
                    if (StageTimer >= 0.40f && NewtonMarksCount == 0)
                    {
                        input.NewtonMarkPressed = true;
                        NewtonMarksCount = 1;
                    }
                    else if (StageTimer >= 1.00f && NewtonMarksCount == 1)
                    {
                        input.NewtonMarkPressed = true;
                        NewtonMarksCount = 2;
                    }
                    else if (StageTimer >= 1.60f && NewtonMarksCount == 2)
                    {
                        input.NewtonMarkPressed = true;
                        NewtonMarksCount = 3;
                    }
                    input.KeyBadgeText = $"MÁY TỰ CHƠI  ·  INPUT: [X] CHỌN 3 ĐIỂM NGANG ({NewtonMarksCount}/3: K, I, K')";
                    ruleCaption = "NHÂN VẬT DƠ TAY CHỌN 3 ĐIỂM NGANG (K, I, K') TRÊN CẶP VÂN (i = 2, k = 4)!";
                    if (StageTimer >= 2.10f)
                    {
                        NewtonMarksCount = 3;
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.AnimateConnectingLines);
                    }
                    break;

                case MainMenuUI.DemoBotStage.AnimateConnectingLines:
                    input.MoveAxisX = 0f;
                    ConnectProgress = Mathf.Clamp01(StageTimer / 0.90f);
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  HIỂN THỊ ANIMATION NỐI ĐOẠN B = KI & b = IK'";
                    ruleCaption = "HIỂN THỊ ANIMATION NỐI 3 ĐIỂM NGANG (B = KI, b = IK') TRÊN 5 VÒNG TRÒN NEWTON!";
                    if (StageTimer >= 1.20f)
                    {
                        ConnectProgress = 1f;
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.CelebrateCompletion);
                    }
                    break;

                case MainMenuUI.DemoBotStage.CelebrateCompletion:
                    input.MoveAxisX = 0f;
                    if (PlayerGrounded && CelebrateHopCount == 0 && StageTimer >= 0.04f)
                    {
                        CelebrateHopCount = 1;
                        PlayerVelY = 6.8f;
                        PlayerGrounded = false;
                    }
                    else if (PlayerGrounded && CelebrateHopCount == 1 && StageTimer >= 0.55f)
                    {
                        CelebrateHopCount = 2;
                        PlayerVelY = 6.8f;
                        PlayerGrounded = false;
                    }
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  HOÀN THÀNH ĐO VÂN NEWTON!";
                    ruleCaption = "NHÂN VẬT DƠ HAI TAY VUI MỪNG HOÀN THÀNH PHÉP ĐO VÂN TRÒN NEWTON!";
                    if (StageTimer >= 1.15f)
                    {
                        SuckStartPosPx = new Vector2(
                            PlayerWorldPos.x * MainMenuUI.DemoPixelsPerUnit,
                            (PlayerWorldPos.y - PlayerSize.y * 0.5f) * MainMenuUI.DemoPixelsPerUnit);
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.SuckedIntoNewtonCenter);
                    }
                    break;

                case MainMenuUI.DemoBotStage.SuckedIntoNewtonCenter:
                    input.MoveAxisX = 0f;
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  HÚT VÀO TÂM VÂN TRÒN NEWTON...";
                    ruleCaption = "NHÂN VẬT BỊ HÚT VÀO TÂM VÂN TRÒN NEWTON  ·  CHUẨN BỊ CHUYỂN CẢNH...";
                    if (StageTimer >= 0.95f)
                    {
                        TransitionToDemoStage(MainMenuUI.DemoBotStage.CircularIrisCloseAndReset);
                    }
                    break;

                case MainMenuUI.DemoBotStage.CircularIrisCloseAndReset:
                    input.MoveAxisX = 0f;
                    input.KeyBadgeText = "MÁY TỰ CHƠI  ·  THU NHỎ TRÒN & CHƠI LẠI TỪ ĐẦU...";
                    ruleCaption = "MÀN HÌNH THU NHỎ TRÒN TỪ NGOÀI VÀO  →  TỰ ĐỘNG CHƠI LẠI VÒNG LẶP MỚI!";

                    if (StageTimer < 0.70f)
                    {
                        float u = StageTimer / 0.70f;
                        IrisNormalized = 1f - (u * u * (3f - 2f * u));
                    }
                    else if (StageTimer < 0.92f)
                    {
                        IrisNormalized = 0f;
                        if (!ResetDoneInIris)
                        {
                            ResetDoneInIris = true;
                            ResetPositionAndMarkersInsideIris(out didResetVisualMarkers);
                        }
                    }
                    else if (StageTimer < 1.40f)
                    {
                        if (!ResetDoneInIris)
                        {
                            ResetDoneInIris = true;
                            ResetPositionAndMarkersInsideIris(out didResetVisualMarkers);
                        }
                        float u = (StageTimer - 0.92f) / 0.48f;
                        IrisNormalized = u * u * (3f - 2f * u);
                    }
                    else
                    {
                        IrisNormalized = 1f;
                        ResetFull(out didResetVisualMarkers);
                    }
                    break;
            }

            return input;
        }

        private void StepDemoPhysicsWithInput(MainMenuUI.DemoBotInput input, float dt)
        {
            if (input.HasColorChange && input.RequestedColor != PlayerColor)
            {
                PlayerColor = input.RequestedColor;
            }

            float halfW = PlayerSize.x * 0.5f;
            float halfH = PlayerSize.y * 0.5f;

            if (PlayerGrounded && GroundedPlatform != null)
            {
                bool stillSolid = GroundedPlatform.IsUniversal || GroundedPlatform.Logical != PlayerColor;
                float pLeft = GroundedPlatform.WorldPos.x - GroundedPlatform.WorldSize.x * 0.5f;
                float pRight = GroundedPlatform.WorldPos.x + GroundedPlatform.WorldSize.x * 0.5f;
                bool overlapsX = (PlayerWorldPos.x + halfW > pLeft + 0.02f) &&
                                 (PlayerWorldPos.x - halfW < pRight - 0.02f);
                if (!stillSolid || !overlapsX)
                {
                    PlayerGrounded = false;
                    GroundedPlatform = null;
                }
                else
                {
                    float platTop = GroundedPlatform.WorldPos.y + GroundedPlatform.WorldSize.y * 0.5f;
                    PlayerWorldPos = new Vector2(PlayerWorldPos.x, platTop + halfH);
                }
            }

            if (input.JumpPressed && PlayerGrounded)
            {
                PlayerVelY = DemoJumpSpeed;
                PlayerGrounded = false;
                GroundedPlatform = null;
            }

            float prevX = PlayerWorldPos.x;
            float nextX = prevX + input.MoveAxisX * DemoMoveSpeed * dt;
            float pBottom = PlayerWorldPos.y - halfH + 0.05f;
            float pTop = PlayerWorldPos.y + halfH - 0.05f;

            for (int i = 0; i < demoPlatforms.Count; i++)
            {
                DemoPlatformActor plat = demoPlatforms[i];
                bool isSolid = plat.IsUniversal || plat.Logical != PlayerColor;
                if (!isSolid)
                    continue;

                float platLeft = plat.WorldPos.x - plat.WorldSize.x * 0.5f;
                float platRight = plat.WorldPos.x + plat.WorldSize.x * 0.5f;
                float platBottom = plat.WorldPos.y - plat.WorldSize.y * 0.5f;
                float platTop = plat.WorldPos.y + plat.WorldSize.y * 0.5f;

                if (pTop > platBottom && pBottom < platTop - 0.08f)
                {
                    if (input.MoveAxisX > 0f && prevX + halfW <= platLeft + 0.06f && nextX + halfW > platLeft)
                    {
                        nextX = platLeft - halfW;
                    }
                    else if (input.MoveAxisX < 0f && prevX - halfW >= platRight - 0.06f && nextX - halfW < platRight)
                    {
                        nextX = platRight + halfW;
                    }
                }
            }
            PlayerWorldPos = new Vector2(nextX, PlayerWorldPos.y);

            if (!PlayerGrounded)
            {
                float prevY = PlayerWorldPos.y;
                float prevFeetY = prevY - halfH;
                PlayerVelY = Mathf.Max(-18f, PlayerVelY - DemoGravity * dt);
                float nextY = prevY + PlayerVelY * dt;
                float nextFeetY = nextY - halfH;

                if (PlayerVelY <= 0f)
                {
                    DemoPlatformActor bestLanding = null;
                    float bestTopY = float.NegativeInfinity;

                    for (int i = 0; i < demoPlatforms.Count; i++)
                    {
                        DemoPlatformActor plat = demoPlatforms[i];
                        bool isSolid = plat.IsUniversal || plat.Logical != PlayerColor;
                        if (!isSolid)
                            continue;

                        float platLeft = plat.WorldPos.x - plat.WorldSize.x * 0.5f;
                        float platRight = plat.WorldPos.x + plat.WorldSize.x * 0.5f;
                        float platTop = plat.WorldPos.y + plat.WorldSize.y * 0.5f;

                        bool overlapsX = (PlayerWorldPos.x + halfW > platLeft + 0.02f) &&
                                         (PlayerWorldPos.x - halfW < platRight - 0.02f);
                        if (!overlapsX)
                            continue;

                        if (prevFeetY >= platTop - 0.14f && nextFeetY <= platTop + 0.03f)
                        {
                            if (platTop > bestTopY)
                            {
                                bestTopY = platTop;
                                bestLanding = plat;
                            }
                        }
                    }

                    if (bestLanding != null)
                    {
                        nextY = bestTopY + halfH;
                        PlayerVelY = 0f;
                        PlayerGrounded = true;
                        GroundedPlatform = bestLanding;
                    }
                }
                PlayerWorldPos = new Vector2(PlayerWorldPos.x, nextY);
            }

            if (!QuangDiemCollected && LevelData != null && LevelData.collectibles != null && LevelData.collectibles.Length > 0)
            {
                Vector2 cPos = LevelData.collectibles[0].position.ToVector2();
                if (Vector2.Distance(PlayerWorldPos, cPos) <= 0.85f)
                {
                    QuangDiemCollected = true;
                }
            }
        }
    }
}

