namespace Contrast.UI
{
    using System.Collections.Generic;
    using Contrast.Audio;
    using UnityEngine;
    using UnityEngine.UI;
    using Color = UnityEngine.Color;

    /// <summary>
    /// Cluster 1: Atmospheric Moonlit Sky &amp; Top Virtual-Lab Brand Header Bar.
    /// </summary>
    internal sealed class MainMenuBackdropCluster
    {
        private RectTransform moonHaloOuter;
        private RectTransform moonHaloMid;
        private readonly List<RectTransform> skyMotes = new List<RectTransform>();
        private readonly List<Vector4> skyMoteParams = new List<Vector4>(); // x0, y0, speed, phase

        public void Build(Transform panelTransform)
        {
            skyMotes.Clear();
            skyMoteParams.Clear();

            BuildMoonlitSkyLayer(panelTransform);
            BuildTopHeaderBar(panelTransform);
        }

        public bool BindExisting(Transform panelTransform)
        {
            Transform skyLayer = panelTransform.Find("AnimatedSkyLayer");
            Transform headerRoot = panelTransform.Find("BrandHeader");
            if (skyLayer == null || headerRoot == null)
                return false;

            skyMotes.Clear();
            skyMoteParams.Clear();

            Sprite circle = MainMenuUIBuilder.GetCircleSprite();
            Sprite ring = MainMenuUIBuilder.GetRingSprite();

            Transform outerGlow = skyLayer.Find("MoonAtmosphereGlow");
            if (outerGlow != null)
            {
                moonHaloOuter = outerGlow.GetComponent<RectTransform>();
                Image img = outerGlow.GetComponent<Image>();
                if (img != null) img.sprite = circle;
            }

            Transform midRing = skyLayer.Find("MoonInterferenceRing");
            if (midRing != null)
            {
                moonHaloMid = midRing.GetComponent<RectTransform>();
                Image img = midRing.GetComponent<Image>();
                if (img != null) img.sprite = ring;
            }

            Transform innerGlow = skyLayer.Find("MoonInnerHalo");
            if (innerGlow != null)
            {
                Image img = innerGlow.GetComponent<Image>();
                if (img != null) img.sprite = circle;
            }

            Transform moonCore = skyLayer.Find("FullMoonCore");
            if (moonCore != null)
            {
                Image img = moonCore.GetComponent<Image>();
                if (img != null) img.sprite = circle;
            }

            for (int i = 0; i < 24; i++)
            {
                Transform mote = skyLayer.Find("OpticalMote_" + i);
                if (mote == null) continue;
                Image mImg = mote.GetComponent<Image>();
                if (mImg != null) mImg.sprite = circle;
                RectTransform mRt = mote.GetComponent<RectTransform>();
                float x0 = -900f + ((i * 173) % 1800);
                float y0 = -480f + ((i * 239) % 960);
                float spd = 12f + (i % 5) * 6f;
                float ph = i * 0.77f;
                skyMotes.Add(mRt);
                skyMoteParams.Add(new Vector4(x0, y0, spd, ph));
            }

            Transform bgmPill = headerRoot.Find("NowPlayingBadge");
            if (bgmPill != null)
            {
                Image pillBg = bgmPill.GetComponent<Image>();
                if (pillBg != null)
                    LabStyleUI.ApplySlicedSprite(pillBg, LabStyleUI.GetPillSprite(), false);
                Transform spk = bgmPill.Find("SpeakerIcon");
                if (spk != null)
                {
                    Image spkImg = spk.GetComponent<Image>();
                    if (spkImg != null)
                        spkImg.sprite = LabStyleUI.GetIconSprite(LabStyleUI.IconKind.Speaker);
                }
            }

            Transform topDock = headerRoot.Find("TopLabDock");
            if (topDock != null)
            {
                Image dockBg = topDock.GetComponent<Image>();
                if (dockBg != null)
                    LabStyleUI.ApplySlicedSprite(dockBg, LabStyleUI.GetTopDockSprite(), false);
                Transform groove = topDock.Find("HeaderGroove");
                if (groove != null)
                {
                    Image grooveBg = groove.GetComponent<Image>();
                    if (grooveBg != null)
                    {
                        Color col = grooveBg.color;
                        LabStyleUI.ApplySlicedSprite(grooveBg, LabStyleUI.GetRecessedSlotSprite(), false);
                        grooveBg.color = col;
                    }
                }
            }

            Transform fsBtnTr = headerRoot.Find("FullscreenButton");
            if (fsBtnTr != null)
            {
                Transform circleBg = fsBtnTr.Find("CircleBg");
                if (circleBg != null)
                {
                    Image fsImg = circleBg.GetComponent<Image>();
                    if (fsImg != null)
                        fsImg.sprite = LabStyleUI.GetCircleButtonSprite();
                    Transform fsIcon = circleBg.Find("Icon");
                    if (fsIcon != null)
                    {
                        Image icImg = fsIcon.GetComponent<Image>();
                        if (icImg != null)
                            icImg.sprite = LabStyleUI.GetIconSprite(LabStyleUI.IconKind.Fullscreen);
                    }
                }
                Button fsBtn = fsBtnTr.GetComponent<Button>();
                if (fsBtn != null)
                {
                    fsBtn.onClick.RemoveAllListeners();
                    fsBtn.onClick.AddListener(() =>
                    {
                        ContrastAudioManager.Instance?.PlayButtonClick();
                        Screen.fullScreen = !Screen.fullScreen;
                    });
                }
            }

            return true;
        }

        public void UpdateSky(float time)
        {
            if (moonHaloOuter != null)
            {
                float s = 540f + Mathf.Sin(time * 0.9f) * 28f;
                moonHaloOuter.sizeDelta = new Vector2(s, s);
            }
            if (moonHaloMid != null)
            {
                float s = 330f + Mathf.Cos(time * 1.3f) * 16f;
                moonHaloMid.sizeDelta = new Vector2(s, s);
            }

            for (int i = 0; i < skyMotes.Count; i++)
            {
                RectTransform mRt = skyMotes[i];
                if (mRt == null) continue;
                Vector4 p = skyMoteParams[i];
                float y = p.y + ((time * p.z) % 980f);
                if (y > 500f) y -= 980f;
                float x = p.x + Mathf.Sin(time * 0.8f + p.w) * 26f;
                mRt.anchoredPosition = new Vector2(x, y);
            }
        }

        private void BuildMoonlitSkyLayer(Transform parent)
        {
            GameObject skyLayer = MainMenuUIBuilder.NewUI("AnimatedSkyLayer", parent);
            MainMenuUIBuilder.Stretch(skyLayer.GetComponent<RectTransform>());

            Sprite circle = MainMenuUIBuilder.GetCircleSprite();
            Sprite ring = MainMenuUIBuilder.GetRingSprite();

            Vector2 moonPos = new Vector2(590f, 340f);

            GameObject outerGlow = MainMenuUIBuilder.NewUI("MoonAtmosphereGlow", skyLayer.transform);
            Image ogImg = outerGlow.AddComponent<Image>();
            ogImg.sprite = circle;
            ogImg.color = new Color(0.22f, 0.52f, 0.85f, 0.14f);
            ogImg.raycastTarget = false;
            moonHaloOuter = outerGlow.GetComponent<RectTransform>();
            moonHaloOuter.anchoredPosition = moonPos;
            moonHaloOuter.sizeDelta = new Vector2(540f, 540f);

            GameObject midRing = MainMenuUIBuilder.NewUI("MoonInterferenceRing", skyLayer.transform);
            Image mrImg = midRing.AddComponent<Image>();
            mrImg.sprite = ring;
            mrImg.color = new Color(0.48f, 0.84f, 1f, 0.16f);
            mrImg.raycastTarget = false;
            moonHaloMid = midRing.GetComponent<RectTransform>();
            moonHaloMid.anchoredPosition = moonPos;
            moonHaloMid.sizeDelta = new Vector2(330f, 330f);

            GameObject innerGlow = MainMenuUIBuilder.NewUI("MoonInnerHalo", skyLayer.transform);
            Image igImg = innerGlow.AddComponent<Image>();
            igImg.sprite = circle;
            igImg.color = new Color(0.70f, 0.90f, 1f, 0.22f);
            igImg.raycastTarget = false;
            RectTransform igRt = innerGlow.GetComponent<RectTransform>();
            igRt.anchoredPosition = moonPos;
            igRt.sizeDelta = new Vector2(190f, 190f);

            GameObject moonCore = MainMenuUIBuilder.NewUI("FullMoonCore", skyLayer.transform);
            Image mcImg = moonCore.AddComponent<Image>();
            mcImg.sprite = circle;
            mcImg.color = new Color(0.95f, 0.98f, 1f, 0.92f);
            mcImg.raycastTarget = false;
            RectTransform mcRt = moonCore.GetComponent<RectTransform>();
            mcRt.anchoredPosition = moonPos;
            mcRt.sizeDelta = new Vector2(104f, 104f);

            for (int i = 0; i < 24; i++)
            {
                GameObject mote = MainMenuUIBuilder.NewUI("OpticalMote_" + i, skyLayer.transform);
                Image mImg = mote.AddComponent<Image>();
                mImg.sprite = circle;
                float hue = (i % 3 == 0) ? 0.55f : (i % 3 == 1 ? 0.38f : 0.62f);
                Color mCol = Color.HSVToRGB(hue, 0.35f, 1f);
                mCol.a = 0.16f + (i % 4) * 0.06f;
                mImg.color = mCol;
                mImg.raycastTarget = false;

                RectTransform mRt = mote.GetComponent<RectTransform>();
                float sz = 6f + (i % 5) * 3.5f;
                mRt.sizeDelta = new Vector2(sz, sz);

                float x0 = -900f + ((i * 173) % 1800);
                float y0 = -480f + ((i * 239) % 960);
                float spd = 12f + (i % 5) * 6f;
                float ph = i * 0.77f;
                mRt.anchoredPosition = new Vector2(x0, y0);

                skyMotes.Add(mRt);
                skyMoteParams.Add(new Vector4(x0, y0, spd, ph));
            }
        }

        private void BuildTopHeaderBar(Transform parent)
        {
            GameObject headerRoot = MainMenuUIBuilder.NewUI("BrandHeader", parent);
            MainMenuUIBuilder.Stretch(headerRoot.GetComponent<RectTransform>());

            GameObject bgmPill = MainMenuUIBuilder.NewUI("NowPlayingBadge", headerRoot.transform);
            Image pillBg = bgmPill.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(pillBg, LabStyleUI.GetPillSprite(), true, -4f);
            RectTransform pillRt = bgmPill.GetComponent<RectTransform>();
            pillRt.anchorMin = new Vector2(0f, 1f);
            pillRt.anchorMax = new Vector2(0f, 1f);
            pillRt.pivot = new Vector2(0f, 1f);
            pillRt.anchoredPosition = new Vector2(44f, -22f);
            pillRt.sizeDelta = new Vector2(286f, 46f);

            GameObject spkGo = MainMenuUIBuilder.NewUI("SpeakerIcon", bgmPill.transform);
            Image spkImg = spkGo.AddComponent<Image>();
            spkImg.sprite = LabStyleUI.GetIconSprite(LabStyleUI.IconKind.Speaker);
            spkImg.color = LabStyleUI.TextPrimary;
            spkImg.raycastTarget = false;
            RectTransform spkRt = spkGo.GetComponent<RectTransform>();
            spkRt.anchorMin = new Vector2(0f, 0.5f);
            spkRt.anchorMax = new Vector2(0f, 0.5f);
            spkRt.pivot = new Vector2(0.5f, 0.5f);
            spkRt.anchoredPosition = new Vector2(28f, 0f);
            spkRt.sizeDelta = new Vector2(20f, 20f);

            MainMenuUIBuilder.CreateSubLabel(
                bgmPill.transform,
                "NowPlayingText",
                "Calima — Full Moon",
                new Vector2(14f, 0f),
                new Vector2(226f, 32f),
                14,
                FontStyle.Bold,
                LabStyleUI.TextSecondary,
                TextAnchor.MiddleLeft);

            GameObject topDock = MainMenuUIBuilder.NewUI("TopLabDock", headerRoot.transform);
            Image dockBg = topDock.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(dockBg, LabStyleUI.GetTopDockSprite(), true, -5f);
            RectTransform dockRt = topDock.GetComponent<RectTransform>();
            dockRt.anchorMin = new Vector2(0.5f, 1f);
            dockRt.anchorMax = new Vector2(0.5f, 1f);
            dockRt.pivot = new Vector2(0.5f, 1f);
            dockRt.anchoredPosition = new Vector2(0f, 6f);
            dockRt.sizeDelta = new Vector2(780f, 64f);

            GameObject groove = MainMenuUIBuilder.NewUI("HeaderGroove", topDock.transform);
            Image grooveBg = groove.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(grooveBg, LabStyleUI.GetRecessedSlotSprite(), false);
            grooveBg.color = new Color(0.68f, 0.77f, 0.88f, 0.90f);
            RectTransform gRt = groove.GetComponent<RectTransform>();
            gRt.anchoredPosition = new Vector2(0f, -4f);
            gRt.sizeDelta = new Vector2(660f, 34f);

            MainMenuUIBuilder.CreateSubLabel(
                groove.transform,
                "LabSubHeader",
                "PHÒNG THÍ NGHIỆM QUANG HỌC ẢO  ·  GIAO THOA & HỆ VÂN TRÒN NEWTON",
                Vector2.zero,
                new Vector2(640f, 30f),
                13,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleCenter);

            GameObject title = MainMenuUIBuilder.NewUI("Title", headerRoot.transform);
            Text titleText = title.AddComponent<Text>();
            titleText.font = MainMenuUIBuilder.GetFont();
            titleText.text = "CONTRAST";
            titleText.fontSize = 58;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = LabStyleUI.TextPrimary;
            Shadow tShadow = title.AddComponent<Shadow>();
            tShadow.effectColor = LabStyleUI.DropShadowColor;
            tShadow.effectDistance = new Vector2(0f, -4f);
            RectTransform tRt = title.GetComponent<RectTransform>();
            tRt.sizeDelta = new Vector2(620f, 72f);
            tRt.anchoredPosition = new Vector2(-566f, 404f);

            GameObject subTitle = MainMenuUIBuilder.NewUI("Subtitle", headerRoot.transform);
            Text subText = subTitle.AddComponent<Text>();
            subText.font = MainMenuUIBuilder.GetFont();
            subText.text = "Mô phỏng giao thoa sóng & đo bước sóng vân tròn Newton";
            subText.fontSize = 15;
            subText.fontStyle = FontStyle.Bold;
            subText.alignment = TextAnchor.MiddleLeft;
            subText.color = LabStyleUI.TextSecondary;
            RectTransform sRt = subTitle.GetComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(620f, 26f);
            sRt.anchoredPosition = new Vector2(-566f, 362f);

            LabStyleUI.CreateCircularIconAction(
                headerRoot.transform,
                "FullscreenButton",
                LabStyleUI.IconKind.Fullscreen,
                "Toàn màn hình",
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-64f, -20f),
                () =>
                {
                    ContrastAudioManager.Instance?.PlayButtonClick();
                    Screen.fullScreen = !Screen.fullScreen;
                },
                54f);
        }
    }
}

