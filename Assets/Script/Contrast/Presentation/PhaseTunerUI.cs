namespace Contrast.Presentation
{
    using UnityEngine;
    using UnityEngine.UI;
    using Contrast.Color;
    using Contrast.Core;
    using Contrast.Player;
    using Contrast.UI;
    using Color = UnityEngine.Color;
    /// <summary>
    /// Presentation-only UI reproducing the Wave-First Cyclic Phase Tuner HUD,
    /// animated dual-waveform interference graph (Wave A fixed vs Wave B phase-shifted),
    /// cyclic ombre strip, status badge, control hint card, and checkpoint toast
    /// from demo_quangbai1.html.
    ///
    /// Does NOT modify any player state or gameplay rules.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhaseTunerUI : MonoBehaviour
    {
        private static PhaseTunerUI instance;
        public static PhaseTunerUI Instance => instance;

        private GameObject tunerPanel;
        private RawImage waveGraphImage;
        private Texture2D waveTexture;
        private Color32[] wavePixels;
        private const int WaveTexWidth = 280;
        private const int WaveTexHeight = 80;

        private RawImage ombreStripImage;
        private Texture2D ombreTexture;
        private RectTransform cursorMarker;
        private readonly RectTransform[] phaseDividers = new RectTransform[4];

        public RectTransform[] PhaseDividers => phaseDividers;
        public RectTransform CursorMarker => cursorMarker;
        public RectTransform TunerPanelRect => tunerPanel != null ? tunerPanel.GetComponent<RectTransform>() : null;
        public GameObject HintPanelObject => hintPanel;

        private static readonly Vector2 NormalTunerAnchoredPos = new Vector2(0f, 18f);
        private static readonly Vector2 NewtonTunerAnchoredPos = new Vector2(-680f, 14f);
        private const float NormalTunerScale = 1.0f;
        private const float NewtonTunerScale = 0.82f;

        private Text stateBadgeTitle;
        private Text stateBadgeRelation;
        private Image stateBadgeBg;
        private Image stateBadgeColorIcon;
        private Outline stateBadgeColorOutline;

        // Control hint card
        private GameObject hintPanel;
        private bool userGuideVisible = true;
        public bool IsUserGuideVisible => userGuideVisible;

        public void ToggleUserGuide()
        {
            EnsureInitialized();
            userGuideVisible = !userGuideVisible;
            if (hintPanel != null)
            {
                GameManager gm = GameManager.Instance;
                bool isPlaying = gm == null || gm.State == GameState.Playing;
                hintPanel.SetActive(isPlaying && userGuideVisible);
            }
        }

        // Checkpoint toast
        private GameObject checkpointToast;
        private Text checkpointToastText;
        private CanvasGroup checkpointToastGroup;
        private float checkpointFlashTimer = 0f;

        // Quang Điểm HUD counter card
        private GameObject quangHudCard;
        private Text quangHudValueText;

        private PlayerController player;
        private Contrast.Camera.CameraFollow cachedCameraFollow;
        private float simulatedPhase = 0f;

        private void Awake()
        {
            instance = this;
            EnsureInitialized();
        }

        private void OnEnable()
        {
            instance = this;
        }

        public void EnsureInitialized()
        {
            if (tunerPanel != null && waveTexture != null && ombreTexture != null)
                return;

            Transform staleCanvas = transform.Find("ContrastPresentationCanvas");
            if (staleCanvas != null)
            {
                if (Application.isPlaying)
                    Destroy(staleCanvas.gameObject);
                else
                    DestroyImmediate(staleCanvas.gameObject);
            }

            BuildUI();
        }

        private void Start()
        {
            player = Contrast.Core.UnityCompat.FindAny<PlayerController>();
        }

        public void TriggerCheckpointFlash()
        {
            checkpointFlashTimer = 2.0f;
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject("ContrastPresentationCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80; // Above gameplay, below menus

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            BuildTunerPanel(canvasGo.transform);
            BuildControlHint(canvasGo.transform);
            BuildCheckpointToast(canvasGo.transform);
            BuildQuangDiemHud(canvasGo.transform);
        }

        private void BuildQuangDiemHud(Transform parent)
        {
            quangHudCard = new GameObject("QuangDiemHUDCard", typeof(RectTransform));
            quangHudCard.transform.SetParent(parent, false);

            Image bg = quangHudCard.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(bg, LabStyleUI.GetPillSprite(), true, -4f);

            // Docked cleanly next to top-left "< Quay lại" pill so right toolbar stays 100% unobstructed
            RectTransform rt = quangHudCard.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(190f, -22f);
            rt.sizeDelta = new Vector2(204f, 44f);

            GameObject starGo = new GameObject("StarLabel", typeof(RectTransform));
            starGo.transform.SetParent(quangHudCard.transform, false);
            Text starText = starGo.AddComponent<Text>();
            starText.font = GetFont();
            starText.text = "✦  Quang Điểm";
            starText.fontSize = 13;
            starText.fontStyle = FontStyle.Bold;
            starText.color = new Color(1.000f, 0.875f, 0.560f, 0.98f);
            starText.alignment = TextAnchor.MiddleLeft;
            RectTransform starRt = starGo.GetComponent<RectTransform>();
            starRt.anchorMin = new Vector2(0f, 0f);
            starRt.anchorMax = new Vector2(0.68f, 1f);
            starRt.offsetMin = new Vector2(16f, 0f);
            starRt.offsetMax = Vector2.zero;

            GameObject valGo = new GameObject("CountValue", typeof(RectTransform));
            valGo.transform.SetParent(quangHudCard.transform, false);
            quangHudValueText = valGo.AddComponent<Text>();
            quangHudValueText.font = GetFont();
            quangHudValueText.text = "0 / 0";
            quangHudValueText.fontSize = 15;
            quangHudValueText.fontStyle = FontStyle.Bold;
            quangHudValueText.color = new Color(1.000f, 0.965f, 0.860f, 1.00f);
            quangHudValueText.alignment = TextAnchor.MiddleRight;
            RectTransform valRt = valGo.GetComponent<RectTransform>();
            valRt.anchorMin = new Vector2(0.55f, 0f);
            valRt.anchorMax = new Vector2(1f, 1f);
            valRt.offsetMin = Vector2.zero;
            valRt.offsetMax = new Vector2(-16f, 0f);
        }

        private void BuildTunerPanel(Transform parent)
        {
            // Tuner Card: Bottom Center (560 x 158) styled with Virtual Lab Frosted Slate Card
            tunerPanel = new GameObject("PhaseTunerPanel", typeof(RectTransform));
            tunerPanel.transform.SetParent(parent, false);

            Image panelBg = tunerPanel.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(panelBg, LabStyleUI.GetCardSprite(), true, -5f);

            RectTransform rt = tunerPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(596f, 162f);
            rt.anchoredPosition = new Vector2(0f, 18f);

            // Header Title
            GameObject titleGo = new GameObject("HeaderLabel", typeof(RectTransform));
            titleGo.transform.SetParent(tunerPanel.transform, false);
            Text titleText = titleGo.AddComponent<Text>();
            titleText.font = GetFont();
            titleText.text = "Bộ điều pha giao thoa sóng (Cực đại · Trung gian · Cực tiểu)";
            titleText.fontSize = 13;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = LabStyleUI.TextSecondary;
            titleText.alignment = TextAnchor.MiddleLeft;

            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(18f, -11f);
            titleRt.sizeDelta = new Vector2(-36f, 20f);

            // Recessed Frame around Wave Graph
            GameObject waveSlotGo = new GameObject("WaveSlotBg", typeof(RectTransform));
            waveSlotGo.transform.SetParent(tunerPanel.transform, false);
            Image waveSlotImg = waveSlotGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(waveSlotImg, LabStyleUI.GetRecessedSlotSprite(), false);
            waveSlotImg.color = new Color(0.58f, 0.66f, 0.78f, 0.92f);
            RectTransform waveSlotRt = waveSlotGo.GetComponent<RectTransform>();
            waveSlotRt.anchorMin = new Vector2(0f, 0f);
            waveSlotRt.anchorMax = new Vector2(0f, 0f);
            waveSlotRt.pivot = new Vector2(0f, 0f);
            waveSlotRt.anchoredPosition = new Vector2(66f, 38f);
            waveSlotRt.sizeDelta = new Vector2(WaveTexWidth + 8f, WaveTexHeight + 8f);

            // Dual Wave Graph Image
            GameObject waveGo = new GameObject("WaveGraph", typeof(RectTransform));
            waveGo.transform.SetParent(tunerPanel.transform, false);
            waveGraphImage = waveGo.AddComponent<RawImage>();

            waveTexture = new Texture2D(WaveTexWidth, WaveTexHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            wavePixels = new Color32[WaveTexWidth * WaveTexHeight];
            waveGraphImage.texture = waveTexture;

            RectTransform waveRt = waveGo.GetComponent<RectTransform>();
            waveRt.anchorMin = new Vector2(0f, 0f);
            waveRt.anchorMax = new Vector2(0f, 0f);
            waveRt.pivot = new Vector2(0f, 0f);
            waveRt.anchoredPosition = new Vector2(70f, 42f);
            waveRt.sizeDelta = new Vector2(WaveTexWidth, WaveTexHeight);

            // Wave A and B Labels matching reference image ("A CỐ ĐỊNH", "B DỊCH PHA")
            CreateLabel(tunerPanel.transform, "A", new Vector2(16f, 95f), 12, FontStyle.Bold, LabStyleUI.TextPrimary);
            CreateLabel(tunerPanel.transform, "CỐ ĐỊNH", new Vector2(10f, 80f), 9, FontStyle.Bold, LabStyleUI.TextMuted);
            CreateLabel(tunerPanel.transform, "B", new Vector2(16f, 61f), 12, FontStyle.Bold, LabStyleUI.TextSecondary);
            CreateLabel(tunerPanel.transform, "DỊCH PHA", new Vector2(10f, 46f), 9, FontStyle.Bold, LabStyleUI.TextMuted);

            // Status Badge (Right side — Royal Sapphire / Slate rounded slot with Color Swatch Icon)
            GameObject badgeGo = new GameObject("StateBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(tunerPanel.transform, false);
            stateBadgeBg = badgeGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(stateBadgeBg, LabStyleUI.GetPrimaryButtonSprite(), false);

            RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 0.5f);
            badgeRt.anchorMax = new Vector2(1f, 0.5f);
            badgeRt.pivot = new Vector2(1f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(-14f, 12f);
            badgeRt.sizeDelta = new Vector2(212f, 68f);

            // Circular Color Swatch Icon on Left of StateBadge showing exact in-game color
            GameObject colorIconGo = new GameObject("StateColorIcon", typeof(RectTransform));
            colorIconGo.transform.SetParent(badgeGo.transform, false);
            stateBadgeColorIcon = colorIconGo.AddComponent<Image>();
            stateBadgeColorIcon.sprite = LabStyleUI.GetSoftCircleSprite();
            stateBadgeColorIcon.color = new Color(0.98f, 0.99f, 1.00f, 1f);
            stateBadgeColorIcon.raycastTarget = false;
            stateBadgeColorOutline = colorIconGo.AddComponent<Outline>();
            stateBadgeColorOutline.effectColor = new Color(0.12f, 0.22f, 0.36f, 0.90f);
            stateBadgeColorOutline.effectDistance = new Vector2(1.5f, -1.5f);

            RectTransform ciRt = colorIconGo.GetComponent<RectTransform>();
            ciRt.anchorMin = new Vector2(0f, 0.5f);
            ciRt.anchorMax = new Vector2(0f, 0.5f);
            ciRt.pivot = new Vector2(0.5f, 0.5f);
            ciRt.anchoredPosition = new Vector2(22f, 0f);
            ciRt.sizeDelta = new Vector2(24f, 24f);

            // Badge Title Text ("GIAO THOA CỰC ĐẠI / TRUNG GIAN / CỰC TIỂU")
            GameObject badgeTitleGo = new GameObject("Title", typeof(RectTransform));
            badgeTitleGo.transform.SetParent(badgeGo.transform, false);
            stateBadgeTitle = badgeTitleGo.AddComponent<Text>();
            stateBadgeTitle.font = GetFont();
            stateBadgeTitle.fontSize = 13;
            stateBadgeTitle.fontStyle = FontStyle.Bold;
            stateBadgeTitle.alignment = TextAnchor.MiddleLeft;
            stateBadgeTitle.color = Color.white;
            stateBadgeTitle.text = "GIAO THOA CỰC ĐẠI";

            RectTransform btRt = badgeTitleGo.GetComponent<RectTransform>();
            btRt.anchorMin = new Vector2(0f, 0.48f);
            btRt.anchorMax = new Vector2(1f, 1f);
            btRt.offsetMin = new Vector2(40f, 0f);
            btRt.offsetMax = new Vector2(-6f, -2f);

            // Badge Relation & Color Nature Text ("Màu Trắng · Cùng pha (Δφ = 0°)")
            GameObject badgeRelGo = new GameObject("Relation", typeof(RectTransform));
            badgeRelGo.transform.SetParent(badgeGo.transform, false);
            stateBadgeRelation = badgeRelGo.AddComponent<Text>();
            stateBadgeRelation.font = GetFont();
            stateBadgeRelation.fontSize = 10;
            stateBadgeRelation.fontStyle = FontStyle.Bold;
            stateBadgeRelation.alignment = TextAnchor.MiddleLeft;
            stateBadgeRelation.color = new Color(0.88f, 0.94f, 1f, 0.94f);
            stateBadgeRelation.text = "Màu Trắng · Cùng pha (Δφ = 0°)";

            RectTransform brRt = badgeRelGo.GetComponent<RectTransform>();
            brRt.anchorMin = new Vector2(0f, 0f);
            brRt.anchorMax = new Vector2(1f, 0.50f);
            brRt.offsetMin = new Vector2(40f, 2f);
            brRt.offsetMax = new Vector2(-6f, 0f);

            // Cyclic Ombre Strip (Bottom of card)
            GameObject ombreGo = new GameObject("OmbreStrip", typeof(RectTransform));
            ombreGo.transform.SetParent(tunerPanel.transform, false);
            ombreStripImage = ombreGo.AddComponent<RawImage>();

            const int ombreW = 240;
            ombreTexture = new Texture2D(ombreW, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave
            };

            // Fill cyclic ombre gradient matching HTML stripColorForPhase(localPhase)
            Color[] ombreColors = new Color[ombreW];
            for (int i = 0; i < ombreW; i++)
            {
                float t = (i + 0.5f) / ombreW;
                float angle = t * Mathf.PI * 2f;
                ombreColors[i] = EvaluateHtmlStripColor(angle);
            }
            ombreTexture.SetPixels(ombreColors);
            ombreTexture.Apply();
            ombreStripImage.texture = ombreTexture;

            Outline ombreOutline = ombreGo.AddComponent<Outline>();
            ombreOutline.effectColor = new Color(0.75f, 0.84f, 0.94f, 0.45f);
            ombreOutline.effectDistance = new Vector2(1f, -1f);

            RectTransform ombreRt = ombreGo.GetComponent<RectTransform>();
            ombreRt.anchorMin = new Vector2(0f, 0f);
            ombreRt.anchorMax = new Vector2(0f, 0f);
            ombreRt.pivot = new Vector2(0f, 0f);
            ombreRt.anchoredPosition = new Vector2(106f, 20f);
            ombreRt.sizeDelta = new Vector2(240f, 12f);

            // Phase zone boundary divider lines (BRIGHT / TUNING / DARK / TUNING)
            float t1 = Mathf.Acos(0.56f) / (Mathf.PI * 2f);
            float t2 = Mathf.Acos(-0.56f) / (Mathf.PI * 2f);
            float t3 = 1f - t2;
            float t4 = 1f - t1;
            float[] dividerNormalized = { t1, t2, t3, t4 };

            for (int d = 0; d < dividerNormalized.Length; d++)
            {
                GameObject divGo = new GameObject($"PhaseDivider_{d}", typeof(RectTransform));
                divGo.transform.SetParent(ombreGo.transform, false);
                Image divImg = divGo.AddComponent<Image>();
                divImg.color = new Color(0.961f, 0.973f, 0.980f, 0.55f);

                RectTransform divRt = divGo.GetComponent<RectTransform>();
                divRt.anchorMin = new Vector2(0f, 0.5f);
                divRt.anchorMax = new Vector2(0f, 0.5f);
                divRt.pivot = new Vector2(0.5f, 0.5f);
                divRt.sizeDelta = new Vector2(1.5f, 14f);
                divRt.anchoredPosition = new Vector2(dividerNormalized[d] * ombreW, 0f);
                phaseDividers[d] = divRt;
            }

            // Cursor on ombre strip
            GameObject cursorGo = new GameObject("CursorMarker", typeof(RectTransform));
            cursorGo.transform.SetParent(ombreGo.transform, false);
            Image cursorImg = cursorGo.AddComponent<Image>();
            cursorImg.color = new Color(0.969f, 0.980f, 0.984f, 1f);

            cursorMarker = cursorGo.GetComponent<RectTransform>();
            cursorMarker.anchorMin = new Vector2(0f, 0.5f);
            cursorMarker.anchorMax = new Vector2(0f, 0.5f);
            cursorMarker.pivot = new Vector2(0.5f, 0.5f);
            cursorMarker.sizeDelta = new Vector2(3f, 20f);

            CreateLabel(tunerPanel.transform, "CỰC ĐẠI · TRUNG GIAN · CỰC TIỂU · TRUNG GIAN · LẶP", new Vector2(88f, 3f), 9, FontStyle.Bold, LabStyleUI.TextMuted);
            CreateLabel(tunerPanel.transform, "← / →  DỊCH SÓNG B · PHA TUẦN HOÀN", new Vector2(362f, 16f), 10, FontStyle.Bold, LabStyleUI.TextSecondary);
        }

        public static Color EvaluateHtmlStripColor(float localPhase)
        {
            float I = Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos(localPhase));
            float v = Mathf.Round(18f + I * 226f);
            float r = v, g = v, b = v;
            if (I >= 0.78f)
            {
                r = Mathf.Min(245f, r + 6f);
                g = Mathf.Min(247f, g + 6f);
                b = Mathf.Min(248f, b + 6f);
            }
            else if (I <= 0.22f)
            {
                r = Mathf.Max(10f, r - 6f);
                g = Mathf.Max(12f, g - 6f);
                b = Mathf.Max(14f, b - 6f);
            }
            else
            {
                r = Mathf.Round(v * 0.92f + 8f);
                g = Mathf.Round(v * 0.95f + 10f);
                b = Mathf.Round(v * 1.02f + 18f);
            }
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private void BuildControlHint(Transform parent)
        {
            // Control Guide Card positioned cleanly below "< Quay lại" pill on Top-Left
            hintPanel = new GameObject("ControlHintCard", typeof(RectTransform));
            hintPanel.transform.SetParent(parent, false);

            Image panelBg = hintPanel.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(panelBg, LabStyleUI.GetCardSprite(), true, -4f);
            panelBg.raycastTarget = false;

            RectTransform rt = hintPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(22f, -80f);
            rt.sizeDelta = new Vector2(384f, 184f);

            // Header Title
            CreateLabel(hintPanel.transform, "HƯỚNG DẪN ĐIỀU KHIỂN", new Vector2(18f, 148f), 13, FontStyle.Bold, LabStyleUI.TextPrimary);

            // Clean 2-column rows (Key badge on left, action description on right — never wraps or overlaps)
            CreateHintRow(hintPanel.transform, "Row_Move", "A / D", "Di chuyển trái / phải", 118f, LabStyleUI.TextSecondary);
            CreateHintRow(hintPanel.transform, "Row_Jump", "SPACE / W", "Nhảy lên", 95f, LabStyleUI.TextSecondary);
            CreateHintRow(hintPanel.transform, "Row_Fall", "S", "Rơi nhanh xuống", 72f, LabStyleUI.TextSecondary);
            CreateHintRow(hintPanel.transform, "Row_Phase", "← / →", "Đổi giao thoa (Cực đại / Trung gian / Cực tiểu)", 49f, LabStyleUI.TextSecondary);
            CreateHintRow(hintPanel.transform, "Row_Newton", "X", "Ghi toạ độ vân tròn Newton", 26f, new Color(1f, 0.89f, 0.50f, 0.96f));
            CreateHintRow(hintPanel.transform, "Row_Restart", "R", "Chơi lại màn hiện tại", 5f, LabStyleUI.TextMuted);
        }

        private void CreateHintRow(Transform parent, string name, string keyStr, string descStr, float yPos, Color descColor)
        {
            GameObject keyGo = new GameObject(name + "_Key", typeof(RectTransform));
            keyGo.transform.SetParent(parent, false);
            Text kTxt = keyGo.AddComponent<Text>();
            kTxt.font = GetFont();
            kTxt.text = keyStr;
            kTxt.fontSize = 11;
            kTxt.fontStyle = FontStyle.Bold;
            kTxt.color = new Color(0.52f, 0.88f, 1.00f, 0.98f);
            kTxt.alignment = TextAnchor.MiddleLeft;
            kTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            kTxt.raycastTarget = false;
            RectTransform kRt = keyGo.GetComponent<RectTransform>();
            kRt.anchorMin = Vector2.zero;
            kRt.anchorMax = Vector2.zero;
            kRt.pivot = Vector2.zero;
            kRt.anchoredPosition = new Vector2(18f, yPos);
            kRt.sizeDelta = new Vector2(82f, 20f);

            GameObject descGo = new GameObject(name + "_Desc", typeof(RectTransform));
            descGo.transform.SetParent(parent, false);
            Text dTxt = descGo.AddComponent<Text>();
            dTxt.font = GetFont();
            dTxt.text = descStr;
            dTxt.fontSize = 11;
            dTxt.fontStyle = FontStyle.Normal;
            dTxt.color = descColor;
            dTxt.alignment = TextAnchor.MiddleLeft;
            dTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            dTxt.raycastTarget = false;
            RectTransform dRt = descGo.GetComponent<RectTransform>();
            dRt.anchorMin = Vector2.zero;
            dRt.anchorMax = Vector2.zero;
            dRt.pivot = Vector2.zero;
            dRt.anchoredPosition = new Vector2(104f, yPos);
            dRt.sizeDelta = new Vector2(268f, 20f);
        }

        private void BuildCheckpointToast(Transform parent)
        {
            checkpointToast = new GameObject("CheckpointToast", typeof(RectTransform));
            checkpointToast.transform.SetParent(parent, false);

            checkpointToastGroup = checkpointToast.AddComponent<CanvasGroup>();
            checkpointToastGroup.alpha = 0f;

            Image bg = checkpointToast.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(bg, LabStyleUI.GetPillSprite(), true, -4f);

            RectTransform rt = checkpointToast.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -66f);
            rt.sizeDelta = new Vector2(360f, 44f);

            GameObject textGo = new GameObject("ToastText", typeof(RectTransform));
            textGo.transform.SetParent(checkpointToast.transform, false);
            checkpointToastText = textGo.AddComponent<Text>();
            checkpointToastText.font = GetFont();
            checkpointToastText.fontSize = 13;
            checkpointToastText.fontStyle = FontStyle.Bold;
            checkpointToastText.alignment = TextAnchor.MiddleCenter;
            checkpointToastText.color = LabStyleUI.TextPrimary;
            checkpointToastText.text = "✦  Đã lưu điểm hồi sinh  ✦";

            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }

        private void CreateLabel(Transform parent, string text, Vector2 pos, int size, FontStyle style, Color col)
        {
            GameObject go = new GameObject("Label_" + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = GetFont();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = col;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(320f, 22f);
        }

        public void UpdateNewtonFramingLayout(bool isNewtonFramingActive, float dt, bool instant = false)
        {
            if (tunerPanel != null)
            {
                RectTransform rt = tunerPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Vector2 targetPos = isNewtonFramingActive ? NewtonTunerAnchoredPos : NormalTunerAnchoredPos;
                    float targetScale = isNewtonFramingActive ? NewtonTunerScale : NormalTunerScale;
                    if (instant || dt <= 0f)
                    {
                        rt.anchoredPosition = targetPos;
                        rt.localScale = Vector3.one * targetScale;
                    }
                    else
                    {
                        float alpha = 1f - Mathf.Exp(-7.5f * dt); // Matches HTML smooth(7.5, dt)
                        rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, alpha);
                        float s = Mathf.Lerp(rt.localScale.x, targetScale, alpha);
                        rt.localScale = Vector3.one * s;
                    }
                }
            }

            if (hintPanel != null)
            {
                GameManager gm = GameManager.Instance;
                bool isPlaying = gm == null || gm.State == GameState.Playing;
                hintPanel.SetActive(isPlaying && !isNewtonFramingActive && userGuideVisible);
            }
        }

        private void Update()
        {
            if (player == null)
                player = Contrast.Core.UnityCompat.FindAny<PlayerController>();

            // Only show tuner and hints during Gameplay (Playing state)
            GameManager gm = GameManager.Instance;
            bool isPlaying = gm != null && gm.State == GameState.Playing;

            if (cachedCameraFollow == null)
                cachedCameraFollow = Contrast.Core.UnityCompat.FindAny<Contrast.Camera.CameraFollow>();
            var newtonCtrl = Contrast.Level.EasyNewtonFinaleController.Instance;
            var camFollow = cachedCameraFollow;
            bool isNewtonFramingActive =
                (newtonCtrl != null && newtonCtrl.IsZoneActive) ||
                (camFollow != null &&
                 (camFollow.CurrentMode == Contrast.Camera.CameraMode.NewtonFixed ||
                  camFollow.CurrentMode == Contrast.Camera.CameraMode.NewtonResultReveal));

            if (tunerPanel != null)
                tunerPanel.SetActive(isPlaying);
            if (hintPanel != null)
                hintPanel.SetActive(isPlaying && !isNewtonFramingActive && userGuideVisible);
            if (quangHudCard != null)
            {
                bool showQuang = isPlaying && Contrast.Level.QuangDiemItem.TotalCount > 0;
                quangHudCard.SetActive(showQuang);
                if (showQuang && quangHudValueText != null)
                {
                    quangHudValueText.text = $"{Contrast.Level.QuangDiemItem.CollectedCount} / {Contrast.Level.QuangDiemItem.TotalCount}";
                }
            }

            if (!isPlaying)
                return;

            UpdateNewtonFramingLayout(isNewtonFramingActive, Time.unscaledDeltaTime, false);

            if (player == null)
                player = Contrast.Core.UnityCompat.FindAny<PlayerController>();

            float currentPhase = player != null ? player.CurrentPhase : simulatedPhase;
            LogicalColor color = player != null ? player.CurrentColor : LogicalColor.White;

            UpdateWaveTexture(currentPhase);
            UpdateStatusBadge(color, currentPhase);
            UpdateOmbreCursor(currentPhase);
            UpdateCheckpointToast();
        }

        private void UpdateWaveTexture(float phaseB)
        {
            if (wavePixels == null || waveTexture == null)
                return;

            // Clear background (deep slate-navy inside recessed slot)
            Color32 clearCol = new Color32(18, 26, 39, 255);
            for (int i = 0; i < wavePixels.Length; i++)
                wavePixels[i] = clearCol;

            // Vertical guides (faint alignment lines)
            for (int gx = 20; gx < WaveTexWidth; gx += 48)
            {
                for (int gy = 4; gy < WaveTexHeight - 4; gy++)
                {
                    wavePixels[gy * WaveTexWidth + gx] = new Color32(38, 54, 76, 255);
                }
            }

            // Draw Wave A (Fixed reference wave, yCenter = 54)
            int yCenterA = 54;
            int yCenterB = 26;
            const float spatialFreq = 0.075f;
            const float amplitude = 12f;

            Color32 waveACol = new Color32(245, 249, 255, 255);
            Color32 waveBCol = new Color32(142, 186, 245, 255);

            for (int x = 0; x < WaveTexWidth; x++)
            {
                // Wave A
                float yA = yCenterA + Mathf.Sin(x * spatialFreq) * amplitude;
                int pyA = Mathf.Clamp(Mathf.RoundToInt(yA), 0, WaveTexHeight - 1);
                wavePixels[pyA * WaveTexWidth + x] = waveACol;
                if (pyA + 1 < WaveTexHeight) wavePixels[(pyA + 1) * WaveTexWidth + x] = waveACol;

                // Wave B
                float yB = yCenterB + Mathf.Sin(x * spatialFreq + phaseB) * amplitude;
                int pyB = Mathf.Clamp(Mathf.RoundToInt(yB), 0, WaveTexHeight - 1);
                wavePixels[pyB * WaveTexWidth + x] = waveBCol;
                if (pyB + 1 < WaveTexHeight) wavePixels[(pyB + 1) * WaveTexWidth + x] = waveBCol;
            }

            waveTexture.SetPixels32(wavePixels);
            waveTexture.Apply();
        }

        private void UpdateStatusBadge(LogicalColor color, float phase)
        {
            if (stateBadgeTitle == null || stateBadgeRelation == null || stateBadgeBg == null)
                return;

            switch (color)
            {
                case LogicalColor.White:
                    stateBadgeTitle.text = "GIAO THOA CỰC ĐẠI";
                    stateBadgeTitle.color = Color.white;
                    stateBadgeRelation.text = "Màu Trắng · Cùng pha (Δφ ≈ 0°)";
                    stateBadgeBg.sprite = LabStyleUI.GetPrimaryButtonSprite();
                    stateBadgeBg.color = Color.white;
                    if (stateBadgeColorIcon != null)
                    {
                        stateBadgeColorIcon.sprite = LabStyleUI.GetSoftCircleSprite();
                        stateBadgeColorIcon.color = new Color(0.98f, 0.99f, 1.00f, 1f);
                    }
                    if (stateBadgeColorOutline != null)
                        stateBadgeColorOutline.effectColor = new Color(0.12f, 0.22f, 0.36f, 0.90f);
                    break;

                case LogicalColor.Gray:
                    stateBadgeTitle.text = "GIAO THOA TRUNG GIAN";
                    stateBadgeTitle.color = new Color(0.94f, 0.97f, 1f, 1f);
                    stateBadgeRelation.text = "Màu Xám · Lệch pha (Δφ ≈ 90°)";
                    stateBadgeBg.sprite = LabStyleUI.GetSecondaryButtonSprite();
                    stateBadgeBg.color = Color.white;
                    if (stateBadgeColorIcon != null)
                    {
                        stateBadgeColorIcon.sprite = LabStyleUI.GetSoftCircleSprite();
                        stateBadgeColorIcon.color = new Color(0.60f, 0.66f, 0.74f, 1f);
                    }
                    if (stateBadgeColorOutline != null)
                        stateBadgeColorOutline.effectColor = new Color(0.92f, 0.96f, 1.00f, 0.90f);
                    break;

                case LogicalColor.Black:
                default:
                    stateBadgeTitle.text = "GIAO THOA CỰC TIỂU";
                    stateBadgeTitle.color = new Color(0.86f, 0.92f, 1f, 1f);
                    stateBadgeRelation.text = "Màu Đen · Ngược pha (Δφ ≈ 180°)";
                    stateBadgeBg.sprite = LabStyleUI.GetSecondaryButtonSprite();
                    stateBadgeBg.color = new Color(0.58f, 0.64f, 0.74f, 1f);
                    if (stateBadgeColorIcon != null)
                    {
                        stateBadgeColorIcon.sprite = LabStyleUI.GetSoftCircleSprite();
                        stateBadgeColorIcon.color = new Color(0.06f, 0.08f, 0.13f, 1f);
                    }
                    if (stateBadgeColorOutline != null)
                        stateBadgeColorOutline.effectColor = new Color(0.55f, 0.82f, 1.00f, 0.90f);
                    break;
            }
        }

        public void SyncPhaseNow(float phase, LogicalColor color)
        {
            UpdateWaveTexture(phase);
            UpdateStatusBadge(color, phase);
            UpdateOmbreCursor(phase);
        }

        private void UpdateOmbreCursor(float phase)
        {
            if (cursorMarker == null)
                return;

            float normalized = Mathf.Repeat(phase / (Mathf.PI * 2f), 1f);
            float posX = normalized * 240f;
            cursorMarker.anchoredPosition = new Vector2(posX, 0f);
        }

        private void UpdateCheckpointToast()
        {
            if (checkpointToastGroup == null)
                return;

            if (checkpointFlashTimer > 0f)
            {
                checkpointFlashTimer -= Time.deltaTime;
                checkpointToastGroup.alpha = Mathf.Clamp01(checkpointFlashTimer * 1.5f);
            }
            else
            {
                checkpointToastGroup.alpha = 0f;
            }
        }

        private static Font GetFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Contrast.Core.UnityCompat.GetBuiltinFont();
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private void OnDestroy()
        {
            if (waveTexture != null)
                DestroyImmediate(waveTexture);
            if (ombreTexture != null)
                DestroyImmediate(ombreTexture);
        }
    }
}
