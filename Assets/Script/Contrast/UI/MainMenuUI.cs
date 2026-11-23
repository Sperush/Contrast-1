namespace Contrast.UI
{
    using Contrast.Audio;
    using Contrast.Color;
    using Contrast.Core;
    using Contrast.Data;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif
    using Color = UnityEngine.Color;
    using UnityEngine.SceneManagement;


    /// <summary>
    /// Orchestrator for CONTRAST's Main Menu UI, organized into modular functional clusters:
    /// <list type="bullet">
    ///   <item><see cref="MainMenuUIBuilder"/> — Shared UI factory, Virtual-Lab buttons, and procedural sprites</item>
    ///   <item><see cref="MainMenuBackdropCluster"/> — Moonlit sky &amp; docked Virtual-Lab header bar</item>
    ///   <item><see cref="MainMenuPlayPanelCluster"/> — Left-column campaign CTA, L01–L03 cards, Settings &amp; Exit</item>
    ///   <item><see cref="MainMenuSettingsCluster"/> — Audio Settings modal (Music &amp; SFX sliders)</item>
    ///   <item><see cref="MainMenuDemoSimulation"/> — Mini-game bot state machine, AABB physics &amp; 2D Camera Follow</item>
    ///   <item><see cref="MainMenuDemoShowcaseCluster"/> — Right-column live gameplay showcase stage</item>
    /// </list>
    /// </summary>
    [ExecuteAlways]
    public sealed class MainMenuUI : MonoBehaviour
    {
        public const string DemoLobbyLevelResourcePath = "Levels/DemoLobbyLevel";
        public const float DemoViewportWidthPx = 796f;
        public const float DemoViewportHeightPx = 458f;
        public const float DemoPixelsPerUnit = 160f;
        public const float DemoCharacterHeightPx = 164f;
        public const float DemoCharacterWidthPx = 134f;

        public enum DemoBotStage
        {
            RunInBlackToWall = 0,
            ShiftWhitePassThroughWall = 1,
            JumpOntoGrayBridge = 2,
            ShiftGrayDropThroughBridge = 3,
            ShiftBlackJumpToNewtonStage = 4,
            ReachAndPickThreeHorizontalPoints = 5,
            AnimateConnectingLines = 6,
            CelebrateCompletion = 7,
            SuckedIntoNewtonCenter = 8,
            CircularIrisCloseAndReset = 9
        }

        public struct DemoBotInput
        {
            public float MoveAxisX;
            public bool JumpPressed;
            public bool HasColorChange;
            public LogicalColor RequestedColor;
            public bool NewtonMarkPressed;
            public string KeyBadgeText;
        }

        private GameObject panel;
        private GameObject cardsRoot;
        private bool boundInPlayMode;

        private readonly MainMenuBackdropCluster backdropCluster = new MainMenuBackdropCluster();
        private readonly MainMenuPlayPanelCluster playPanelCluster = new MainMenuPlayPanelCluster();
        private readonly MainMenuSettingsCluster settingsCluster = new MainMenuSettingsCluster();
        private readonly MainMenuDemoShowcaseCluster demoShowcaseCluster = new MainMenuDemoShowcaseCluster();

        public LevelData LoadedDemoLevelData => demoShowcaseCluster.Simulation.LevelData;
        public DemoBotStage CurrentDemoStage => demoShowcaseCluster.Simulation.BotStage;
        public Vector2 CurrentDemoPlayerWorldPos => demoShowcaseCluster.Simulation.PlayerWorldPos;
        public Vector2 CurrentDemoCameraPosPx => demoShowcaseCluster.Simulation.CameraPosPx;
        public RectTransform DemoCharacterRectTransform => demoShowcaseCluster.CharacterRectTransform;
        public LogicalColor CurrentDemoPlayerColor => demoShowcaseCluster.Simulation.PlayerColor;
        public bool CurrentDemoPlayerGrounded => demoShowcaseCluster.Simulation.PlayerGrounded;
        public bool CurrentDemoQuangDiemCollected => demoShowcaseCluster.Simulation.QuangDiemCollected;
        public int CurrentDemoNewtonMarksCount => demoShowcaseCluster.Simulation.NewtonMarksCount;
        public float CurrentDemoConnectProgress => demoShowcaseCluster.Simulation.ConnectProgress;
        public float CurrentDemoIrisNormalized => demoShowcaseCluster.Simulation.IrisNormalized;

        public bool IsPlaySubMenuOpen => playPanelCluster.IsOpen;
        public bool IsSettingsOpen => settingsCluster.IsOpen;

        public static LevelData LoadDemoLobbyLevelData() => MainMenuDemoSimulation.LoadDemoLobbyLevelData();

        private void Awake()
        {
            if (Application.isPlaying)
            {
                boundInPlayMode = false;
                panel = null;
            }
            EnsureInitialized();
            bool isMainMenuScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameManager.MainMenuSceneName;
            if (isMainMenuScene && GameManager.Instance == null)
                Show(true);
            else
                Show(false);
        }
        public void BackToVatLy(){
            ContrastAudioManager.Instance?.StopAllAudio();
            SceneManager.LoadScene("B1Scene");
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                EnsureEventSystem();
                if (!boundInPlayMode)
                {
                    panel = null;
                    EnsureInitialized();
                }
                if (GameManager.Instance == null)
                {
                    Show(true);
                }
            }
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameManager.MainMenuSceneName &&
                gameObject.name == "MainMenuUI")
            {
                panel = null;
                EnsureInitialized();
                Show(true);
            }
#endif
        }

        private void OnDestroy()
        {
            settingsCluster.UnbindAudioSettings();
        }

        [ContextMenu("Build / Refresh Main Menu UI In Scene")]
        public void RebuildMenuUIInScene()
        {
            boundInPlayMode = false;
            panel = null;
            EnsureInitialized();
            Show(true);
        }

        public void EnsureInitialized()
        {
            EnsureEventSystem();

            if (panel != null && (!Application.isPlaying || boundInPlayMode))
                return;

            Transform existingPanel = transform.Find("Canvas/Panel");
            if (existingPanel != null && TryBindExistingUI(existingPanel))
            {
                if (Application.isPlaying)
                {
                    boundInPlayMode = true;
                    settingsCluster.BindAudioSettings();
                }
                UpdateAnimatedBackdropAndDemo(0.8f, 0.016f);
                if (panel != null)
                    LabStyleUI.CrispenTextHierarchy(panel.transform);
                return;
            }

            Transform staleCanvas = transform.Find("Canvas");
            if (staleCanvas != null)
            {
                if (Application.isPlaying)
                    Destroy(staleCanvas.gameObject);
                else
                    DestroyImmediate(staleCanvas.gameObject);
            }

            BuildUI();
            if (panel != null)
                LabStyleUI.CrispenTextHierarchy(panel.transform);
            if (Application.isPlaying)
            {
                boundInPlayMode = true;
                settingsCluster.BindAudioSettings();
            }
        }

        private bool TryBindExistingUI(Transform existingPanel)
        {
            panel = existingPanel.gameObject;
            Transform canvasTr = existingPanel.parent;
            if (canvasTr != null)
            {
                Canvas c = canvasTr.GetComponent<Canvas>();
                if (c != null)
                {
                    c.enabled = true;
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.pixelPerfect = true;
                    c.sortingOrder = 200;
                }
                GraphicRaycaster gr = canvasTr.GetComponent<GraphicRaycaster>();
                if (gr == null)
                    gr = canvasTr.gameObject.AddComponent<GraphicRaycaster>();
                gr.enabled = true;
            }

            Image bg = panel.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = MainMenuUIBuilder.GetSkyGradientSprite();
                bg.color = Color.white;
            }

            Transform existingCards = existingPanel.Find("MainCardsRoot");
            if (existingCards != null)
                cardsRoot = existingCards.gameObject;

            bool okBackdrop = backdropCluster.BindExisting(existingPanel);
            bool okShowcase = demoShowcaseCluster.BindExisting(existingPanel);
            bool okPlay = playPanelCluster.BindExisting(
                existingPanel,
                SelectPlayFromBeginning,
                SelectDirectLevel,
                OpenSettings,
                QuitApplication);
            bool okSettings = settingsCluster.BindExisting(existingPanel, CloseSettings);

            return okBackdrop && okShowcase && okPlay && okSettings;
        }

        public void Show(bool visible)
        {
            EnsureInitialized();
            if (panel != null)
                panel.SetActive(visible);

            if (visible)
            {
                settingsCluster.SetActive(false);
                OpenPlaySubMenu();
                if (Application.isPlaying)
                    ContrastAudioManager.EnsureInstance()?.PlayMenuMusic();
            }
        }

        public void OpenPlaySubMenu()
        {
            EnsureInitialized();
            if (cardsRoot != null)
                cardsRoot.SetActive(false);
            playPanelCluster.SetActive(true);
        }

        public void ClosePlaySubMenu()
        {
            EnsureInitialized();
            if (cardsRoot != null)
                cardsRoot.SetActive(false);
            playPanelCluster.SetActive(true);
        }

        public void OpenSettings()
        {
            Debug.Log("[UI_BUTTON][MainMenu] Clicked SettingsButton -> OpenSettings");
            EnsureInitialized();
            settingsCluster.Open();
        }

        public void CloseSettings()
        {
            Debug.Log("[UI_BUTTON][MainMenuSettings] Clicked CloseSettingsButton -> CloseSettings");
            settingsCluster.Close();
        }

        public void QuitApplication()
        {
            Debug.Log("[UI_BUTTON][MainMenu] Clicked ExitButton -> QuitApplication");
            ContrastAudioManager.Instance?.PlayButtonClick();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitGame();
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        public void SelectPlayFromBeginning()
        {
            Debug.Log("[UI_BUTTON][MainMenu] Clicked PlayFromStartButton -> LaunchCampaignFromMainMenu(0)");
            ContrastAudioManager.Instance?.PlayButtonClick();
            GameManager.LaunchCampaignFromMainMenu(0);
        }

        public void SelectDirectLevel(int levelIndex)
        {
            Debug.Log($"[UI_BUTTON][MainMenu] Clicked SelectLevel_L0{levelIndex + 1} -> LaunchCampaignFromMainMenu({levelIndex})");
            ContrastAudioManager.Instance?.PlayButtonClick();
            GameManager.LaunchCampaignFromMainMenu(levelIndex);
        }

        public void OnPlay() => OpenPlaySubMenu();

        public void ResetDemoMiniGame()
        {
            EnsureInitialized();
            demoShowcaseCluster.ResetDemoMiniGame();
        }

        public void StepDemoSimulationForTest(float dt)
        {
            EnsureInitialized();
            float nextTime = demoShowcaseCluster.TotalTime + dt;
            UpdateAnimatedBackdropAndDemo(nextTime, dt);
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            if (panel == null || !panel.activeInHierarchy)
                return;

            if (!playPanelCluster.IsOpen)
                playPanelCluster.SetActive(true);

            UpdateAnimatedBackdropAndDemo(Time.unscaledTime, Time.unscaledDeltaTime);

            if (IsSettingsOpen)
            {
                if (IsEscapePressed())
                    CloseSettings();
                return;
            }

            if (Config.Instance.enableTest)
            {
                if (IsKey1Pressed())
                {
                    SelectDirectLevel(0);
                    return;
                }

                if (IsKey2Pressed())
                {
                    SelectDirectLevel(1);
                    return;
                }

                if (IsKey3Pressed())
                {
                    SelectDirectLevel(2);
                    return;
                }
            }

            if (IsConfirmPressed())
            {
                SelectPlayFromBeginning();
                return;
            }
        }

        private void BuildUI()
        {
            GameObject canvasGo = MainMenuUIBuilder.NewUI("Canvas", transform);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            panel = MainMenuUIBuilder.NewUI("Panel", canvasGo.transform);
            Image bg = panel.AddComponent<Image>();
            bg.sprite = MainMenuUIBuilder.GetSkyGradientSprite();
            bg.color = Color.white;
            MainMenuUIBuilder.Stretch(panel.GetComponent<RectTransform>());

            // Cluster 1: Atmospheric Moonlit Sky & Top Virtual-Lab Header Bar
            backdropCluster.Build(panel.transform);

            // Legacy root preserved for hierarchy compatibility
            cardsRoot = MainMenuUIBuilder.NewUI("MainCardsRoot", panel.transform);
            cardsRoot.SetActive(false);

            // Cluster 4: Right Column Live Animated Gameplay Showcase Stage
            demoShowcaseCluster.Build(panel.transform);

            // Cluster 2: Left Column Play & Level Selection Sub-Menu Panel
            playPanelCluster.Build(
                panel.transform,
                SelectPlayFromBeginning,
                SelectDirectLevel,
                OpenSettings,
                QuitApplication);

            // Cluster 3: Audio Settings Modal
            settingsCluster.Build(panel.transform, CloseSettings);

            UpdateAnimatedBackdropAndDemo(0.8f, 0.016f);
        }

        private void UpdateAnimatedBackdropAndDemo(float time, float dt)
        {
            backdropCluster.UpdateSky(time);
            demoShowcaseCluster.UpdateShowcase(time, dt);
        }

        private static void EnsureEventSystem()
        {
            LabStyleUI.EnsureEventSystem();
        }

        private static bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static bool IsKey1Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame);
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1);
#endif
        }

        private static bool IsKey2Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame);
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2);
#endif
        }

        private static bool IsKey3Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame);
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3);
#endif
        }

        private static bool IsConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }
    }
}
