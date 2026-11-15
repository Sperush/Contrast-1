namespace Contrast.UI
{
    using UnityEngine;
    using UnityEngine.UI;
    using Color = UnityEngine.Color;

    /// <summary>
    /// Shared UI factory, layout helpers, Virtual-Lab button builders, and procedural
    /// sprites used across all Main Menu clusters.
    /// </summary>
    internal static class MainMenuUIBuilder
    {
        private static Sprite cachedCircleSprite;
        private static Sprite cachedRingSprite;
        private static Sprite cachedSkySprite;
        private static Sprite cachedIrisApertureSprite;

        public static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return go;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void AnchorToBottomCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
        }

        public static Font GetFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        public static Text CreateSubLabel(
            Transform parent,
            string name,
            string content,
            Vector2 pos,
            Vector2 size,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor anchor)
        {
            GameObject go = NewUI(name, parent);
            Text txt = go.AddComponent<Text>();
            txt.font = GetFont();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = anchor;
            txt.raycastTarget = false;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return txt;
        }

        public static void CreatePrimaryStartButton(
            Transform parent,
            string name,
            string label,
            Vector2 pos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = NewUI(name, parent);
            Image img = btnGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(img, LabStyleUI.GetPrimaryButtonSprite(), addDropShadow: true, shadowOffsetY: -5f);

            Button btn = btnGo.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.targetGraphic = img;
            btn.colors = LabStyleUI.MakeButtonColorBlock();
            btn.onClick.AddListener(onClick);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            GameObject iconGo = NewUI("Icon", btnGo.transform);
            Image iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = LabStyleUI.GetIconSprite(LabStyleUI.IconKind.PlayTriangle);
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;
            RectTransform iRt = iconGo.GetComponent<RectTransform>();
            iRt.sizeDelta = new Vector2(24f, 24f);
            iRt.anchoredPosition = new Vector2(-218f, 0f);

            CreateSubLabel(
                btnGo.transform,
                "Title",
                label,
                new Vector2(14f, 0f),
                new Vector2(size.x - 96f, size.y),
                19,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleCenter);
        }

        public static void CreateLabLevelButton(
            Transform parent,
            string name,
            string indexStr,
            string mainText,
            string subText,
            string wavelengthBadge,
            Color badgeColor,
            Vector2 pos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = NewUI(name, parent);
            Image img = btnGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(img, LabStyleUI.GetSecondaryButtonSprite(), addDropShadow: true, shadowOffsetY: -4f);

            Button btn = btnGo.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.targetGraphic = img;
            btn.colors = LabStyleUI.MakeButtonColorBlock();
            btn.onClick.AddListener(onClick);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            GameObject numCircle = NewUI("OrderCircle", btnGo.transform);
            Image numImg = numCircle.AddComponent<Image>();
            numImg.sprite = LabStyleUI.GetSoftCircleSprite();
            numImg.color = badgeColor;
            numImg.raycastTarget = false;
            RectTransform numRt = numCircle.GetComponent<RectTransform>();
            numRt.sizeDelta = new Vector2(44f, 44f);
            numRt.anchoredPosition = new Vector2(-size.x * 0.5f + 42f, 0f);

            CreateSubLabel(
                numCircle.transform,
                "OrderText",
                indexStr,
                Vector2.zero,
                new Vector2(44f, 44f),
                18,
                FontStyle.Bold,
                new Color(0.06f, 0.10f, 0.16f, 1f),
                TextAnchor.MiddleCenter);

            CreateSubLabel(
                btnGo.transform,
                "Title",
                mainText,
                new Vector2(-46f, 13f),
                new Vector2(490f, 28f),
                17,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleLeft);

            CreateSubLabel(
                btnGo.transform,
                "Sub",
                subText,
                new Vector2(-46f, -14f),
                new Vector2(490f, 22f),
                12,
                FontStyle.Normal,
                LabStyleUI.TextSecondary,
                TextAnchor.MiddleLeft);

            GameObject waveSlot = NewUI("WavelengthSlot", btnGo.transform);
            Image slotImg = waveSlot.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(slotImg, LabStyleUI.GetRecessedSlotSprite(), addDropShadow: false);
            slotImg.raycastTarget = false;
            RectTransform slotRt = waveSlot.GetComponent<RectTransform>();
            slotRt.sizeDelta = new Vector2(148f, 42f);
            slotRt.anchoredPosition = new Vector2(size.x * 0.5f - 94f, 0f);

            GameObject dotGo = NewUI("SpectrumDot", waveSlot.transform);
            Image dotImg = dotGo.AddComponent<Image>();
            dotImg.sprite = LabStyleUI.GetSoftCircleSprite();
            dotImg.color = badgeColor;
            dotImg.raycastTarget = false;
            RectTransform dotRt = dotGo.GetComponent<RectTransform>();
            dotRt.sizeDelta = new Vector2(10f, 10f);
            dotRt.anchoredPosition = new Vector2(-54f, 0f);

            CreateSubLabel(
                waveSlot.transform,
                "WaveText",
                wavelengthBadge,
                new Vector2(8f, 0f),
                new Vector2(118f, 36f),
                13,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleCenter);
        }

        public static void CreatePillUtilityButton(
            Transform parent,
            string name,
            string label,
            LabStyleUI.IconKind iconKind,
            Sprite sprite,
            Vector2 pos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = NewUI(name, parent);
            Image img = btnGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(img, sprite, addDropShadow: true, shadowOffsetY: -4f);

            Button btn = btnGo.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.targetGraphic = img;
            btn.colors = LabStyleUI.MakeButtonColorBlock();
            btn.onClick.AddListener(onClick);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            GameObject iconGo = NewUI("Icon", btnGo.transform);
            Image iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = LabStyleUI.GetIconSprite(iconKind);
            iconImg.color = LabStyleUI.TextPrimary;
            iconImg.raycastTarget = false;
            RectTransform iRt = iconGo.GetComponent<RectTransform>();
            iRt.sizeDelta = new Vector2(22f, 22f);
            iRt.anchoredPosition = new Vector2(-size.x * 0.5f + 34f, 0f);

            CreateSubLabel(
                btnGo.transform,
                "Title",
                label,
                new Vector2(14f, 0f),
                new Vector2(size.x - 68f, size.y),
                16,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleCenter);
        }

        public static void CreatePrimaryModalButton(
            Transform parent,
            string name,
            string label,
            Vector2 pos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = NewUI(name, parent);
            Image img = btnGo.AddComponent<Image>();
            LabStyleUI.ApplySlicedSprite(img, LabStyleUI.GetPrimaryButtonSprite(), addDropShadow: true, shadowOffsetY: -4f);

            Button btn = btnGo.AddComponent<Button>();
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.targetGraphic = img;
            btn.colors = LabStyleUI.MakeButtonColorBlock();
            btn.onClick.AddListener(onClick);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            CreateSubLabel(
                btnGo.transform,
                "Title",
                label,
                Vector2.zero,
                new Vector2(size.x - 24f, size.y),
                16,
                FontStyle.Bold,
                LabStyleUI.TextPrimary,
                TextAnchor.MiddleCenter);
        }

        public static Sprite GetSkyGradientSprite()
        {
            if (cachedSkySprite != null)
                return cachedSkySprite;

            const int w = 64, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color bottom = new Color(0.018f, 0.034f, 0.068f, 1f);
            Color mid = new Color(0.042f, 0.095f, 0.178f, 1f);
            Color top = new Color(0.022f, 0.052f, 0.112f, 1f);
            Color moonGlowTint = new Color(0.10f, 0.24f, 0.42f, 1f);

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                Color rowCol = v < 0.52f
                    ? Color.Lerp(bottom, mid, v / 0.52f)
                    : Color.Lerp(mid, top, (v - 0.52f) / 0.48f);

                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float distMoon = Vector2.Distance(new Vector2(u, v), new Vector2(0.80f, 0.82f));
                    float glow = Mathf.Clamp01(1f - distMoon * 1.65f);
                    Color px = Color.Lerp(rowCol, moonGlowTint, glow * glow * 0.65f);
                    tex.SetPixel(x, y, px);
                }
            }
            tex.Apply();
            cachedSkySprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return cachedSkySprite;
        }

        public static Sprite GetCircleSprite()
        {
            if (cachedCircleSprite != null)
                return cachedCircleSprite;

            const int d = 64;
            const float r = d * 0.5f;
            Texture2D tex = new Texture2D(d, d, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            for (int y = 0; y < d; y++)
            {
                for (int x = 0; x < d; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01((r - 0.5f - dist) * 0.85f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), 64f);
            return cachedCircleSprite;
        }

        public static Sprite GetRingSprite()
        {
            if (cachedRingSprite != null)
                return cachedRingSprite;

            const int d = 96;
            const float r = d * 0.5f;
            Texture2D tex = new Texture2D(d, d, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            for (int y = 0; y < d; y++)
            {
                for (int x = 0; x < d; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float ringDist = Mathf.Abs(dist - (r - 4.5f));
                    float a = Mathf.Clamp01(1f - ringDist / 2.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            cachedRingSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), 96f);
            return cachedRingSprite;
        }

        public static Sprite GetIrisApertureSprite()
        {
            if (cachedIrisApertureSprite != null)
                return cachedIrisApertureSprite;

            const int d = 192;
            const float r = d * 0.5f;
            Color voidCol = new Color(0.02f, 0.035f, 0.065f, 1f);
            Color rimCol = new Color(0.38f, 0.94f, 1f, 0.92f);

            Texture2D tex = new Texture2D(d, d, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            float holeRadius = r * 0.94f;
            float rimOuter = r * 0.97f;

            for (int y = 0; y < d; y++)
            {
                for (int x = 0; x < d; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    if (dist <= holeRadius - 1.5f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist <= holeRadius)
                    {
                        float t = (dist - (holeRadius - 1.5f)) / 1.5f;
                        tex.SetPixel(x, y, new Color(rimCol.r, rimCol.g, rimCol.b, t * 0.85f));
                    }
                    else if (dist <= rimOuter)
                    {
                        float t = (dist - holeRadius) / (rimOuter - holeRadius);
                        tex.SetPixel(x, y, Color.Lerp(rimCol, voidCol, t));
                    }
                    else
                    {
                        tex.SetPixel(x, y, voidCol);
                    }
                }
            }

            tex.Apply();
            cachedIrisApertureSprite = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), 100f);
            return cachedIrisApertureSprite;
        }
    }
}

