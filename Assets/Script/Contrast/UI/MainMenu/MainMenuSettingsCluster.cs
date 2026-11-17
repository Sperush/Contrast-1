namespace Contrast.UI
{
    using Contrast.Audio;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;
    using Color = UnityEngine.Color;

    /// <summary>
    /// Cluster 3: Audio Settings Modal (Music &amp; SFX volume sliders, SFX preview,
    /// reset defaults, and audio event synchronization).
    /// </summary>
    internal sealed class MainMenuSettingsCluster
    {
        public GameObject ModalPanel { get; private set; }

        private Slider musicSlider;
        private Slider sfxSlider;
        private Text musicPercentText;
        private Text sfxPercentText;
        private bool suppressSliderCallback;

        public bool IsOpen => ModalPanel != null && ModalPanel.activeSelf;

        public void SetActive(bool active)
        {
            if (ModalPanel != null)
                ModalPanel.SetActive(active);
        }

        public void BindAudioSettings()
        {
            var audio = ContrastAudioManager.EnsureInstance();
            if (audio != null)
            {
                audio.OnVolumeChanged -= HandleAudioVolumeChanged;
                audio.OnVolumeChanged += HandleAudioVolumeChanged;
                SyncSettingsUI(audio.MusicVolume, audio.SfxVolume);
            }
        }

        public void UnbindAudioSettings()
        {
            if (ContrastAudioManager.Instance != null)
                ContrastAudioManager.Instance.OnVolumeChanged -= HandleAudioVolumeChanged;
        }

        private void HandleAudioVolumeChanged(float musicVol, float sfxVol)
        {
            SyncSettingsUI(musicVol, sfxVol);
        }

        public void SyncSettingsUI(float musicVol, float sfxVol)
        {
            suppressSliderCallback = true;
            if (musicSlider != null)
                musicSlider.value = musicVol;
            if (sfxSlider != null)
                sfxSlider.value = sfxVol;
            if (musicPercentText != null)
                musicPercentText.text = $"{Mathf.RoundToInt(musicVol * 100f)}%";
            if (sfxPercentText != null)
                sfxPercentText.text = $"{Mathf.RoundToInt(sfxVol * 100f)}%";
            suppressSliderCallback = false;
        }

        public void Open()
        {
            ContrastAudioManager.Instance?.PlayButtonClick();
            var audio = ContrastAudioManager.EnsureInstance();
            if (audio != null)
                SyncSettingsUI(audio.MusicVolume, audio.SfxVolume);
            if (ModalPanel != null)
                ModalPanel.SetActive(true);
        }

        public void Close()
        {
            ContrastAudioManager.Instance?.PlayButtonClick();
            if (ModalPanel != null)
                ModalPanel.SetActive(false);
        }

        public bool BindExisting(Transform parent, UnityAction onCloseSettings)
        {
            Transform existing = parent.Find("SettingsModalPanel");
            if (existing == null)
                return false;

            ModalPanel = existing.gameObject;
            Transform card = existing.Find("SettingsCard");
            if (card == null)
                return false;

            Image cardBg = card.GetComponent<Image>();
            if (cardBg != null)
                LabStyleUI.ApplySlicedSprite(cardBg, LabStyleUI.GetCardSprite(), false);

            musicSlider = BindExistingSliderRow(
                card,
                "MusicSliderRow",
                out musicPercentText,
                val =>
                {
                    if (suppressSliderCallback) return;
                    ContrastAudioManager.EnsureInstance().SetMusicVolume(val);
                    if (musicPercentText != null)
                        musicPercentText.text = $"{Mathf.RoundToInt(val * 100f)}%";
                });

            sfxSlider = BindExistingSliderRow(
                card,
                "SfxSliderRow",
                out sfxPercentText,
                val =>
                {
                    if (suppressSliderCallback) return;
                    ContrastAudioManager.EnsureInstance().SetSfxVolume(val);
                    if (sfxPercentText != null)
                        sfxPercentText.text = $"{Mathf.RoundToInt(val * 100f)}%";
                });

            BindButton(card, "TestSfxButton", LabStyleUI.GetSecondaryButtonSprite(), LabStyleUI.IconKind.Speaker, () =>
            {
                Debug.Log("[UI_BUTTON][MainMenuSettings] Clicked TestSfxButton -> PlayCheckpoint");
                ContrastAudioManager.EnsureInstance().PlayCheckpoint();
            });
            BindButton(card, "ResetAudioButton", LabStyleUI.GetSecondaryButtonSprite(), LabStyleUI.IconKind.Restart, () =>
            {
                Debug.Log("[UI_BUTTON][MainMenuSettings] Clicked ResetAudioButton -> ResetDefaultVolumes");
                ContrastAudioManager.EnsureInstance().ResetDefaultVolumes();
                ContrastAudioManager.EnsureInstance().PlayButtonClick();
            });
            BindButton(card, "CloseSettingsButton", LabStyleUI.GetPrimaryButtonSprite(), LabStyleUI.IconKind.PlayTriangle, onCloseSettings, false);

            return true;
        }

        private static Slider BindExistingSliderRow(Transform card, string rowName, out Text percentText, UnityAction<float> onValueChanged)
        {
            percentText = null;
            Transform row = card.Find(rowName);
            if (row == null) return null;

            Image rowBg = row.GetComponent<Image>();
            if (rowBg != null)
                LabStyleUI.ApplySlicedSprite(rowBg, LabStyleUI.GetRecessedSlotSprite(), false);

            Transform pctTr = row.Find("PercentText");
            if (pctTr != null)
                percentText = pctTr.GetComponent<Text>();

            Transform sliderTr = row.Find("Slider");
            if (sliderTr == null) return null;

            Image bgImg = sliderTr.Find("Background")?.GetComponent<Image>();
            if (bgImg != null)
            {
                Color c = bgImg.color;
                LabStyleUI.ApplySlicedSprite(bgImg, LabStyleUI.GetPillSprite(), false);
                bgImg.color = c;
            }

            Image fillImg = sliderTr.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fillImg != null)
                LabStyleUI.ApplySlicedSprite(fillImg, LabStyleUI.GetPrimaryButtonSprite(), false);

            Image handleImg = sliderTr.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
            if (handleImg != null)
            {
                handleImg.sprite = LabStyleUI.GetCircleButtonSprite();
                handleImg.type = Image.Type.Simple;
            }

            Slider slider = sliderTr.GetComponent<Slider>();
            if (slider != null)
            {
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(onValueChanged);
            }
            return slider;
        }

        private static void BindButton(Transform parent, string name, Sprite bgSprite, LabStyleUI.IconKind iconKind, UnityAction onClick, bool hasIcon = true)
        {
            Transform tr = parent.Find(name);
            if (tr == null) return;
            Image img = tr.GetComponent<Image>();
            if (img != null)
                LabStyleUI.ApplySlicedSprite(img, bgSprite, false);
            if (hasIcon)
            {
                Image icImg = tr.Find("Icon")?.GetComponent<Image>();
                if (icImg != null)
                    icImg.sprite = LabStyleUI.GetIconSprite(iconKind);
            }
            Button btn = tr.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onClick);
            }
        }

        public void Build(Transform parent, UnityAction onCloseSettings)
        {
            ModalPanel = MainMenuUIBuilder.NewUI("SettingsModalPanel", parent);
            Image backdrop = ModalPanel.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.05f, 0.09f, 0.78f);
            MainMenuUIBuilder.Stretch(ModalPanel.GetComponent<RectTransform>());

            GameObject card = MainMenuUIBuilder.NewUI("SettingsCard", ModalPanel.transform);
            Image cardBg = card.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(cardBg, LabStyleUI.GetCardSprite(), addDropShadow: true, shadowOffsetY: -8f);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(720f, 470f);
            cardRt.anchoredPosition = Vector2.zero;

            MainMenuUIBuilder.CreateSubLabel(
                card.transform,
                "SettingsTitle",
                "Cài đặt âm thanh",
                new Vector2(0f, 178f),
                new Vector2(640f, 42f),
                26,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleCenter);

            MainMenuUIBuilder.CreateSubLabel(
                card.transform,
                "SettingsSub",
                "Điều chỉnh độ lớn Nhạc nền (Full Moon / Evening Tone) và Hiệu ứng âm thanh",
                new Vector2(0f, 138f),
                new Vector2(650f, 24f),
                13,
                FontStyle.Normal,
                LabStyleUI.TextSecondary,
                TextAnchor.MiddleCenter);

            musicSlider = BuildSettingsSliderRow(
                card.transform,
                "MusicSliderRow",
                "Âm lượng Nhạc nền",
                new Vector2(0f, 54f),
                ContrastAudioManager.DefaultMusicVolume,
                out musicPercentText,
                val =>
                {
                    if (suppressSliderCallback) return;
                    ContrastAudioManager.EnsureInstance().SetMusicVolume(val);
                    if (musicPercentText != null)
                        musicPercentText.text = $"{Mathf.RoundToInt(val * 100f)}%";
                });

            sfxSlider = BuildSettingsSliderRow(
                card.transform,
                "SfxSliderRow",
                "Âm lượng Hiệu ứng",
                new Vector2(0f, -38f),
                ContrastAudioManager.DefaultSfxVolume,
                out sfxPercentText,
                val =>
                {
                    if (suppressSliderCallback) return;
                    ContrastAudioManager.EnsureInstance().SetSfxVolume(val);
                    if (sfxPercentText != null)
                        sfxPercentText.text = $"{Mathf.RoundToInt(val * 100f)}%";
                });

            MainMenuUIBuilder.CreatePillUtilityButton(
                card.transform,
                "TestSfxButton",
                "Nghe thử SFX",
                LabStyleUI.IconKind.Speaker,
                LabStyleUI.GetSecondaryButtonSprite(),
                new Vector2(-216f, -158f),
                new Vector2(194f, 58f),
                () =>
                {
                    Debug.Log("[UI_BUTTON][MainMenuSettings] Clicked TestSfxButton -> PlayCheckpoint");
                    ContrastAudioManager.EnsureInstance().PlayCheckpoint();
                });

            MainMenuUIBuilder.CreatePillUtilityButton(
                card.transform,
                "ResetAudioButton",
                "Mặc định",
                LabStyleUI.IconKind.Restart,
                LabStyleUI.GetSecondaryButtonSprite(),
                new Vector2(0f, -158f),
                new Vector2(194f, 58f),
                () =>
                {
                    Debug.Log("[UI_BUTTON][MainMenuSettings] Clicked ResetAudioButton -> ResetDefaultVolumes");
                    ContrastAudioManager.EnsureInstance().ResetDefaultVolumes();
                    ContrastAudioManager.EnsureInstance().PlayButtonClick();
                });

            MainMenuUIBuilder.CreatePrimaryModalButton(
                card.transform,
                "CloseSettingsButton",
                "XÁC NHẬN",
                new Vector2(216f, -158f),
                new Vector2(194f, 58f),
                onCloseSettings);

            ModalPanel.SetActive(false);
        }

        private static Slider BuildSettingsSliderRow(
            Transform parent,
            string name,
            string titleStr,
            Vector2 pos,
            float defaultVal,
            out Text percentText,
            UnityAction<float> onValueChanged)
        {
            GameObject row = MainMenuUIBuilder.NewUI(name, parent);
            Image rowBg = row.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(rowBg, LabStyleUI.GetRecessedSlotSprite(), addDropShadow: false);
            RectTransform rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(632f, 74f);
            rowRt.anchoredPosition = pos;

            GameObject lblGo = MainMenuUIBuilder.NewUI("Label", row.transform);
            Text lbl = lblGo.AddComponent<Text>();
            lbl.font = MainMenuUIBuilder.GetFont();
            lbl.text = titleStr;
            lbl.fontSize = 15;
            lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.color = LabStyleUI.TextPrimary;
            RectTransform lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.sizeDelta = new Vector2(210f, 36f);
            lblRt.anchoredPosition = new Vector2(-188f, 0f);

            GameObject sliderGo = MainMenuUIBuilder.NewUI("Slider", row.transform);
            RectTransform sRt = sliderGo.GetComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(290f, 28f);
            sRt.anchoredPosition = new Vector2(82f, 0f);

            GameObject bgGo = MainMenuUIBuilder.NewUI("Background", sliderGo.transform);
            Image bgImg = bgGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(bgImg, LabStyleUI.GetPillSprite(), addDropShadow: false);
            bgImg.color = new Color(0.75f, 0.82f, 0.92f, 0.95f);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 14f);

            GameObject fillArea = MainMenuUIBuilder.NewUI("Fill Area", sliderGo.transform);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.5f);
            faRt.anchorMax = new Vector2(1f, 0.5f);
            faRt.offsetMin = new Vector2(2f, -6f);
            faRt.offsetMax = new Vector2(-2f, 6f);

            GameObject fillGo = MainMenuUIBuilder.NewUI("Fill", fillArea.transform);
            Image fillImg = fillGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(fillImg, LabStyleUI.GetPrimaryButtonSprite(), addDropShadow: false);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            MainMenuUIBuilder.Stretch(fillRt);

            GameObject handleArea = MainMenuUIBuilder.NewUI("Handle Slide Area", sliderGo.transform);
            MainMenuUIBuilder.Stretch(handleArea.GetComponent<RectTransform>());

            GameObject handleGo = MainMenuUIBuilder.NewUI("Handle", handleArea.transform);
            Image handleImg = handleGo.AddComponent<Image>();
            handleImg.sprite = LabStyleUI.GetCircleButtonSprite();
            handleImg.type = Image.Type.Simple;
            handleImg.color = Color.white;
            RectTransform hRt = handleGo.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(26f, 26f);

            Slider slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.value = defaultVal;
            slider.onValueChanged.AddListener(onValueChanged);

            GameObject pctGo = MainMenuUIBuilder.NewUI("PercentText", row.transform);
            percentText = pctGo.AddComponent<Text>();
            percentText.font = MainMenuUIBuilder.GetFont();
            percentText.text = $"{Mathf.RoundToInt(defaultVal * 100f)}%";
            percentText.fontSize = 15;
            percentText.fontStyle = FontStyle.Bold;
            percentText.alignment = TextAnchor.MiddleRight;
            percentText.color = LabStyleUI.TextPrimary;
            RectTransform pctRt = pctGo.GetComponent<RectTransform>();
            pctRt.sizeDelta = new Vector2(60f, 36f);
            pctRt.anchoredPosition = new Vector2(268f, 0f);

            return slider;
        }
    }
}

