namespace Contrast.UI
{
    using System;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;
    using Color = UnityEngine.Color;

    /// <summary>
    /// Cluster 2: Left Column Play &amp; Level Selection Sub-Menu Panel (Campaign CTA,
    /// L01/L02/L03 direct level cards, Audio Settings button, and Exit button).
    /// </summary>
    internal sealed class MainMenuPlayPanelCluster
    {
        public GameObject RootPanel { get; private set; }

        public bool IsOpen => RootPanel != null && RootPanel.activeSelf;

        public void SetActive(bool active)
        {
            if (RootPanel != null)
                RootPanel.SetActive(active);
        }

        public void Build(
            Transform parent,
            UnityAction onPlayFromStart,
            Action<int> onSelectDirectLevel,
            UnityAction onOpenSettings,
            UnityAction onQuitApplication)
        {
            RootPanel = MainMenuUIBuilder.NewUI("PlaySubMenuPanel", parent);
            Image cardBg = RootPanel.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(cardBg, LabStyleUI.GetCardSprite(), addDropShadow: true, shadowOffsetY: -7f);

            RectTransform subRt = RootPanel.GetComponent<RectTransform>();
            subRt.sizeDelta = new Vector2(856f, 736f);
            subRt.anchoredPosition = new Vector2(-452f, -64f);

            MainMenuUIBuilder.CreateSubLabel(
                RootPanel.transform,
                "Header",
                "Phòng thí nghiệm giao thoa  ·  Chọn bài thực hành",
                new Vector2(0f, 322f),
                new Vector2(780f, 34f),
                19,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleLeft);

            MainMenuUIBuilder.CreatePrimaryStartButton(
                RootPanel.transform,
                "PlayFromStartButton",
                "BẮT ĐẦU THÍ NGHIỆM   (MÀN 1 → MÀN 3)",
                new Vector2(0f, 246f),
                new Vector2(780f, 76f),
                onPlayFromStart);

            MainMenuUIBuilder.CreateSubLabel(
                RootPanel.transform,
                "DirectSelectHeader",
                "Danh sách bài đo trực tiếp  (k · i · bước sóng λ)",
                new Vector2(0f, 174f),
                new Vector2(780f, 26f),
                14,
                FontStyle.Bold,
                LabStyleUI.TextSecondary,
                TextAnchor.MiddleLeft);

            MainMenuUIBuilder.CreateLabLevelButton(
                RootPanel.transform,
                "SelectLevel_L01",
                "1",
                "Bài 01  ·  Cánh cửa không phải vật cản",
                "Vật cản pha tĩnh  ·  2 Quang điểm  ·  k = 6,  i = 3",
                "550 nm · Lục",
                new Color(0.28f, 0.92f, 0.52f, 1f),
                new Vector2(0f, 104f),
                new Vector2(780f, 82f),
                () => onSelectDirectLevel(0));

            MainMenuUIBuilder.CreateLabLevelButton(
                RootPanel.transform,
                "SelectLevel_L02",
                "2",
                "Bài 02  ·  Cầu thang ma",
                "Cầu thang lệch pha  ·  2 Quang điểm  ·  k = 4,  i = 2",
                "650 nm · Đỏ",
                new Color(0.98f, 0.36f, 0.38f, 1f),
                new Vector2(0f, 10f),
                new Vector2(780f, 82f),
                () => onSelectDirectLevel(1));

            MainMenuUIBuilder.CreateLabLevelButton(
                RootPanel.transform,
                "SelectLevel_L03",
                "3",
                "Bài 03  ·  Giếng ba tầng",
                "Giếng ba tầng & bệ động  ·  3 Quang điểm  ·  k = 7,  i = 2",
                "470 nm · Lam",
                new Color(0.38f, 0.68f, 1f, 1f),
                new Vector2(0f, -84f),
                new Vector2(780f, 82f),
                () => onSelectDirectLevel(2));

            GameObject div = MainMenuUIBuilder.NewUI("BottomDivider", RootPanel.transform);
            Image divImg = div.AddComponent<Image>();
            divImg.color = new Color(0.68f, 0.80f, 0.92f, 0.22f);
            divImg.raycastTarget = false;
            RectTransform divRt = div.GetComponent<RectTransform>();
            divRt.sizeDelta = new Vector2(780f, 1.5f);
            divRt.anchoredPosition = new Vector2(0f, -156f);

            MainMenuUIBuilder.CreatePillUtilityButton(
                RootPanel.transform,
                "SettingsButton",
                "Cài đặt âm thanh",
                LabStyleUI.IconKind.SettingsSliders,
                LabStyleUI.GetPillSprite(),
                new Vector2(-202f, -218f),
                new Vector2(376f, 62f),
                onOpenSettings);

            MainMenuUIBuilder.CreatePillUtilityButton(
                RootPanel.transform,
                "ExitButton",
                "Thoát trò chơi",
                LabStyleUI.IconKind.PowerExit,
                LabStyleUI.GetDangerButtonSprite(),
                new Vector2(202f, -218f),
                new Vector2(376f, 62f),
                onQuitApplication);

            GameObject backGo = MainMenuUIBuilder.NewUI("BackToMainMenuButton", RootPanel.transform);
            backGo.SetActive(false);

            MainMenuUIBuilder.CreateSubLabel(
                RootPanel.transform,
                "GameplayFooter",
                "Phím tắt:  ENTER / SPACE để bắt đầu   ·   Phím 1 / 2 / 3 mở nhanh Bài 01 / 02 / 03",
                new Vector2(0f, -314f),
                new Vector2(780f, 28f),
                13,
                FontStyle.Bold,
                LabStyleUI.TextMuted,
                TextAnchor.MiddleCenter);

            RootPanel.SetActive(true);
        }
    }
}

