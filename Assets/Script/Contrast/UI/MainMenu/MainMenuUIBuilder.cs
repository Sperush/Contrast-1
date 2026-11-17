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
        private static readonly Sprite[] cachedPhaseCardSprites = new Sprite[3];
        private static readonly Sprite[] cachedPhaseSwatchSprites = new Sprite[3];
        private static Sprite cachedChamferedTagSprite;
        private static Sprite cachedDoubleSlashSprite;

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

        private static bool IsValid(Sprite s) => s != null && s.texture != null;

        public static Sprite GetSkyGradientSprite()
        {
            if (IsValid(cachedSkySprite))
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
            cachedSkySprite.hideFlags = HideFlags.DontSave;
            return cachedSkySprite;
        }

        public static Sprite GetCircleSprite()
        {
            if (IsValid(cachedCircleSprite))
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
            cachedCircleSprite.hideFlags = HideFlags.DontSave;
            return cachedCircleSprite;
        }

        public static Sprite GetRingSprite()
        {
            if (IsValid(cachedRingSprite))
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
            cachedRingSprite.hideFlags = HideFlags.DontSave;
            return cachedRingSprite;
        }

        public static Sprite GetIrisApertureSprite()
        {
            if (IsValid(cachedIrisApertureSprite))
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
            cachedIrisApertureSprite.hideFlags = HideFlags.DontSave;
            return cachedIrisApertureSprite;
        }

        public static Sprite GetPhaseCardSprite(int phaseIndex)
        {
            int idx = Mathf.Clamp(phaseIndex, 0, 2);
            if (IsValid(cachedPhaseCardSprites[idx]))
                return cachedPhaseCardSprites[idx];

            const int w = 160, h = 88;
            const float radius = 14f;
            const float borderThickness = 2.0f;

            Color fillTop, fillBottom, borderCol, diagAccentCol;
            if (idx == 0)
            {
                // Pha 1: Luminous Pearl-White card with bright ice-white rim
                fillTop = new Color(0.95f, 0.975f, 1.00f, 0.98f);
                fillBottom = new Color(0.86f, 0.91f, 0.97f, 0.98f);
                borderCol = new Color(1.00f, 1.00f, 1.00f, 1.00f);
                diagAccentCol = new Color(0.75f, 0.82f, 0.90f, 0.55f);
            }
            else if (idx == 1)
            {
                // Pha 2: Frosted Slate-Gray card with silver-white rim
                fillTop = new Color(0.50f, 0.56f, 0.64f, 0.96f);
                fillBottom = new Color(0.36f, 0.42f, 0.50f, 0.97f);
                borderCol = new Color(0.84f, 0.90f, 0.96f, 0.88f);
                diagAccentCol = new Color(0.66f, 0.73f, 0.82f, 0.36f);
            }
            else
            {
                // Pha 3: Deep Obsidian-Navy card with glowing electric-cyan rim
                fillTop = new Color(0.055f, 0.105f, 0.180f, 0.98f);
                fillBottom = new Color(0.024f, 0.045f, 0.085f, 0.99f);
                borderCol = new Color(0.24f, 0.82f, 1.00f, 0.96f);
                diagAccentCol = new Color(0.14f, 0.48f, 0.78f, 0.35f);
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
            Vector2 halfSize = new Vector2(w * 0.5f - 1.5f, h * 0.5f - 1.5f);

            for (int y = 0; y < h; y++)
            {
                float tY = (float)y / (h - 1);
                Color gradFill = Color.Lerp(fillBottom, fillTop, tY);

                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (halfSize - new Vector2(radius, radius));
                    float dist = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;

                    float outerAlpha = Mathf.Clamp01(0.5f - dist);
                    if (outerAlpha <= 0.001f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    Color bodyCol = gradFill;
                    // Diagonal tech stripes in bottom-right corner matching reference image
                    float diag = (x - (w - 34f)) - (y * 0.75f);
                    if (y < 28 && ((diag > 0f && diag < 7f) || (diag > 12f && diag < 19f)))
                    {
                        bodyCol = Color.Lerp(bodyCol, diagAccentCol, diagAccentCol.a);
                    }

                    float innerBlend = Mathf.Clamp01(0.5f - (dist + borderThickness));
                    Color px = Color.Lerp(borderCol, bodyCol, innerBlend);
                    px.a *= outerAlpha;
                    tex.SetPixel(x, y, px);
                }
            }

            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
            sp.hideFlags = HideFlags.DontSave;
            cachedPhaseCardSprites[idx] = sp;
            return sp;
        }

        public static Sprite GetPhaseCircleSwatchSprite(int phaseIndex)
        {
            int idx = Mathf.Clamp(phaseIndex, 0, 2);
            if (IsValid(cachedPhaseSwatchSprites[idx]))
                return cachedPhaseSwatchSprites[idx];

            const int d = 72;
            const float center = d * 0.5f;
            const float radius = d * 0.5f - 6f;

            Color coreCol, rimCol, glowCol;
            if (idx == 0)
            {
                coreCol = new Color(1.00f, 1.00f, 1.00f, 1.00f);
                rimCol = new Color(1.00f, 1.00f, 1.00f, 1.00f);
                glowCol = new Color(0.75f, 0.88f, 1.00f, 0.45f);
            }
            else if (idx == 1)
            {
                coreCol = new Color(0.58f, 0.64f, 0.72f, 1.00f);
                rimCol = new Color(0.95f, 0.98f, 1.00f, 0.96f);
                glowCol = new Color(0.85f, 0.92f, 1.00f, 0.28f);
            }
            else
            {
                coreCol = new Color(0.015f, 0.025f, 0.045f, 1.00f);
                rimCol = new Color(0.32f, 0.86f, 1.00f, 0.98f);
                glowCol = new Color(0.20f, 0.75f, 1.00f, 0.42f);
            }

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
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center)) - radius;
                    if (dist <= -2.2f)
                    {
                        tex.SetPixel(x, y, coreCol);
                    }
                    else if (dist <= 0.5f)
                    {
                        float t = Mathf.Clamp01((dist + 2.2f) / 2.2f);
                        float edgeA = Mathf.Clamp01(0.5f - dist);
                        Color c = Color.Lerp(coreCol, rimCol, t);
                        c.a *= edgeA;
                        tex.SetPixel(x, y, c);
                    }
                    else if (dist <= 5.5f)
                    {
                        float g = Mathf.Clamp01(1f - (dist - 0.5f) / 5.0f);
                        tex.SetPixel(x, y, new Color(glowCol.r, glowCol.g, glowCol.b, glowCol.a * g * g));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            cachedPhaseSwatchSprites[idx] = sp;
            return sp;
        }

        public static Sprite GetChamferedTagSprite()
        {
            if (IsValid(cachedChamferedTagSprite))
                return cachedChamferedTagSprite;

            const int w = 96, h = 28;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            for (int y = 0; y < h; y++)
            {
                float ny = Mathf.Abs((y + 0.5f) - h * 0.5f); // 0 at center, 14 at top/bottom
                for (int x = 0; x < w; x++)
                {
                    float leftBoundary = 1f + ny * 0.55f;
                    float rightBoundary = (w - 1f) - ny * 0.55f;
                    float dEdge = Mathf.Min((x + 0.5f) - leftBoundary, rightBoundary - (x + 0.5f));
                    dEdge = Mathf.Min(dEdge, (h * 0.5f - 1f) - ny);

                    float alpha = Mathf.Clamp01(dEdge + 0.5f);
                    if (alpha <= 0.001f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else
                    {
                        bool isRim = dEdge < 1.3f;
                        Color c = isRim ? new Color(1f, 1f, 1f, alpha) : new Color(0.78f, 0.84f, 0.92f, alpha);
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            cachedChamferedTagSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 4, 12, 4));
            cachedChamferedTagSprite.hideFlags = HideFlags.DontSave;
            return cachedChamferedTagSprite;
        }

        public static Sprite GetDoubleSlashTicksSprite()
        {
            if (IsValid(cachedDoubleSlashSprite))
                return cachedDoubleSlashSprite;

            const int w = 36, h = 20;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            for (int y = 0; y < h; y++)
            {
                float slant = (y - 2f) * 0.48f;
                for (int x = 0; x < w; x++)
                {
                    if (y < 2 || y >= h - 2)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    float d1 = Mathf.Min((x + 0.5f) - (4f + slant), (12f + slant) - (x + 0.5f));
                    float d2 = Mathf.Min((x + 0.5f) - (16f + slant), (24f + slant) - (x + 0.5f));
                    float a = Mathf.Clamp01(Mathf.Max(d1, d2) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            cachedDoubleSlashSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            cachedDoubleSlashSprite.hideFlags = HideFlags.DontSave;
            return cachedDoubleSlashSprite;
        }
    }
}

