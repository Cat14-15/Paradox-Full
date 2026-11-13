using System.Collections.Generic;
using Paradox.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 컷씬 배경. Resources/Backgrounds/{key}.png 가 있으면 그 그림을, 없으면 코드로 그린 단순 배경을 쓴다.
    /// 배경 키: classroom(교실), school(학교 정문·횡단보도), camp(캠프장·버스), museum_dark(정전 로비), museum_relief(분수 아트리움).
    /// 사용자는 Assets/Resources/Backgrounds/ 에 같은 이름의 png만 넣으면 자동으로 바뀐다(README.txt 참고).
    /// </summary>
    public static class Backgrounds
    {
        public const string Classroom = "classroom";
        public const string School = "school";
        public const string Camp = "camp";
        public const string MuseumDark = "museum_dark";
        public const string MuseumRelief = "museum_relief";

        private static readonly Dictionary<string, Texture2D> TexCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>Resources.Load 캐시. 없으면 null(코드 배경으로 대체).</summary>
        public static Texture2D Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Texture2D tex;
            if (TexCache.TryGetValue(key, out tex) && tex != null) return tex;
            if (Missing.Contains(key)) return null;
            tex = Resources.Load<Texture2D>("Backgrounds/" + key);
            if (tex == null) { Missing.Add(key); return null; }
            TexCache[key] = tex;
            return tex;
        }

        public static bool Has(string key) => Get(key) != null;

        /// <summary>UI Image 용 스프라이트(텍스처 전체).</summary>
        public static Sprite GetSprite(string key)
        {
            var tex = Get(key);
            if (tex == null) return null;
            Sprite s;
            if (SpriteCache.TryGetValue(key, out s) && s != null) return s;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = "bg_" + key;
            SpriteCache[key] = s;
            return s;
        }

        /// <summary>
        /// target 안을 배경으로 채운다(기존 자식은 지우지 않는다 — 호출자가 UI.Clear 후 부른다).
        /// 그림이 있으면 꽉 채운 Image, 없으면 코드 배경.
        /// </summary>
        public static void Draw(RectTransform target, string key)
        {
            if (target == null) return;
            var sprite = GetSprite(key);
            if (sprite != null)
            {
                // 비율을 지키면서 화면을 꽉 채운다(넘치는 쪽은 가운데 기준으로 잘림) — 3:2 그림을 16:9 화면에 늘리지 않는다
                var rt = UI.Stretch(target, "BgImage");
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var fit = rt.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectRatio = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                return;
            }
            DrawCode(target, key);
        }

        /// <summary>코드 배경만 강제로 그린다(그림이 있어도). 카드 일러스트 등 작은 미리보기용.</summary>
        public static void DrawCode(RectTransform t, string key)
        {
            switch (key)
            {
                case Classroom: DrawClassroom(t); break;
                case School: DrawSchool(t); break;
                case Camp: DrawCamp(t); break;
                case MuseumDark: DrawMuseumDark(t); break;
                case MuseumRelief: DrawMuseumRelief(t); break;
                default: Gradient(t, new Color(0.10f, 0.12f, 0.18f), new Color(0.03f, 0.04f, 0.06f), 6); break;
            }
        }

        // ------------------------------------------------------------------ 그리기 도구(부모 크기의 비율 좌표, y는 아래가 0)

        private static RectTransform Box(Transform parent, float x0, float y0, float x1, float y1, Color c, string name = "Box")
        {
            var rt = UI.Rect(parent, name);
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return rt;
        }

        private static RectTransform Oval(Transform parent, float cx, float cy, float rx, float ry, Color c, string name = "Oval")
        {
            var rt = UI.Rect(parent, name);
            rt.anchorMin = new Vector2(cx - rx, cy - ry); rt.anchorMax = new Vector2(cx + rx, cy + ry);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteLib.Disc(0.08f);
            img.color = c; img.raycastTarget = false;
            return rt;
        }

        private static void Gradient(Transform parent, Color top, Color bottom, int bands, float y0 = 0f, float y1 = 1f)
        {
            for (int i = 0; i < bands; i++)
            {
                float a = y1 - (y1 - y0) * (i + 1) / bands;
                float b = y1 - (y1 - y0) * i / bands;
                Box(parent, 0f, a, 1f, b + 0.002f, Color.Lerp(top, bottom, bands == 1 ? 0f : i / (float)(bands - 1)), "Band");
            }
        }

        private static void TextBox(Transform parent, float x0, float y0, float x1, float y1, string text, int size, Color c)
        {
            var rt = UI.Rect(parent, "TextBox");
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UI.Font; t.text = text; t.fontSize = size; t.color = c;
            t.alignment = TextAnchor.MiddleCenter; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }

        // ------------------------------------------------------------------ 교실: 베이지 바닥 + 책상 격자 + 칠판

        private static void DrawClassroom(RectTransform t)
        {
            // 벽(위)과 바닥(아래)
            Gradient(t, new Color(0.93f, 0.89f, 0.80f), new Color(0.86f, 0.80f, 0.68f), 4, 0.55f, 1f);
            Gradient(t, new Color(0.82f, 0.72f, 0.54f), new Color(0.66f, 0.55f, 0.38f), 5, 0f, 0.55f);
            Box(t, 0f, 0.545f, 1f, 0.56f, new Color(0.55f, 0.42f, 0.28f), "Baseboard");
            // 바닥 널빤지 줄
            for (int i = 0; i < 8; i++)
            {
                float y = 0.06f + i * 0.062f;
                Box(t, 0f, y, 1f, y + 0.004f, new Color(0.55f, 0.44f, 0.30f, 0.55f), "Plank");
            }
            // 칠판(나무 틀 + 초록 판 + 분필 글씨)
            Box(t, 0.20f, 0.63f, 0.80f, 0.93f, new Color(0.45f, 0.30f, 0.17f), "BoardFrame");
            Box(t, 0.21f, 0.645f, 0.79f, 0.915f, new Color(0.13f, 0.33f, 0.27f), "Board");
            Box(t, 0.20f, 0.61f, 0.80f, 0.63f, new Color(0.60f, 0.44f, 0.26f), "ChalkTray");
            Box(t, 0.26f, 0.84f, 0.46f, 0.855f, new Color(1f, 1f, 1f, 0.55f), "Chalk");
            Box(t, 0.26f, 0.79f, 0.60f, 0.803f, new Color(1f, 1f, 1f, 0.40f), "Chalk");
            Box(t, 0.26f, 0.74f, 0.40f, 0.752f, new Color(1f, 0.95f, 0.70f, 0.45f), "Chalk");
            Box(t, 0.56f, 0.70f, 0.74f, 0.74f, new Color(1f, 1f, 1f, 0.18f), "ChalkBox");
            // 오른쪽 문, 왼쪽 창문
            Box(t, 0.875f, 0.56f, 0.965f, 0.90f, new Color(0.56f, 0.40f, 0.24f), "Door");
            Box(t, 0.885f, 0.72f, 0.955f, 0.86f, new Color(0.72f, 0.80f, 0.84f), "DoorGlass");
            Oval(t, 0.95f, 0.70f, 0.006f, 0.010f, new Color(0.85f, 0.75f, 0.40f), "Knob");
            Box(t, 0.04f, 0.66f, 0.15f, 0.90f, new Color(0.70f, 0.82f, 0.90f), "Window");
            Box(t, 0.093f, 0.66f, 0.097f, 0.90f, new Color(0.95f, 0.95f, 0.92f), "Sash");
            Box(t, 0.04f, 0.776f, 0.15f, 0.784f, new Color(0.95f, 0.95f, 0.92f), "Sash");
            // 책상 격자 4×3 (의자는 책상 위쪽)
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    float x0 = 0.12f + c * 0.205f, y0 = 0.08f + r * 0.155f;
                    float w = 0.135f, h = 0.075f;
                    Box(t, x0 + 0.03f, y0 + h, x0 + w - 0.03f, y0 + h + 0.03f, new Color(0.42f, 0.32f, 0.26f), "Chair");
                    Box(t, x0, y0 - 0.025f, x0 + w, y0, new Color(0.40f, 0.28f, 0.17f), "DeskFront");
                    Box(t, x0, y0, x0 + w, y0 + h, new Color(0.68f, 0.50f, 0.32f), "Desk");
                    Box(t, x0 + 0.01f, y0 + h - 0.012f, x0 + w - 0.01f, y0 + h - 0.004f, new Color(0.80f, 0.64f, 0.44f), "DeskEdge");
                    if ((r + c) % 3 == 0) Box(t, x0 + 0.035f, y0 + 0.02f, x0 + 0.095f, y0 + 0.055f, new Color(0.95f, 0.95f, 0.92f), "Paper");
                }
            }
        }

        // ------------------------------------------------------------------ 학교 정문: 회색 도로 + 횡단보도 흰 줄 + 초록 나무

        private static void DrawSchool(RectTransform t)
        {
            Gradient(t, new Color(0.60f, 0.74f, 0.86f), new Color(0.82f, 0.88f, 0.92f), 5, 0.55f, 1f);
            Oval(t, 0.82f, 0.90f, 0.035f, 0.06f, new Color(1f, 0.96f, 0.80f, 0.9f), "Sun");
            // 학교 건물
            Box(t, 0.29f, 0.865f, 0.73f, 0.89f, new Color(0.52f, 0.34f, 0.30f), "Roof");
            Box(t, 0.30f, 0.56f, 0.72f, 0.865f, new Color(0.86f, 0.82f, 0.74f), "School");
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 6; c++)
                    Box(t, 0.325f + c * 0.063f, 0.63f + r * 0.075f, 0.325f + c * 0.063f + 0.04f, 0.63f + r * 0.075f + 0.05f, new Color(0.52f, 0.66f, 0.80f), "Win");
            Box(t, 0.485f, 0.56f, 0.535f, 0.64f, new Color(0.40f, 0.28f, 0.20f), "Entrance");
            // 정문 기둥 + 담장
            Box(t, 0.10f, 0.56f, 0.25f, 0.62f, new Color(0.66f, 0.64f, 0.62f), "Fence");
            Box(t, 0.77f, 0.56f, 0.92f, 0.62f, new Color(0.66f, 0.64f, 0.62f), "Fence");
            Box(t, 0.245f, 0.56f, 0.285f, 0.72f, new Color(0.58f, 0.58f, 0.60f), "Pillar");
            Box(t, 0.735f, 0.56f, 0.775f, 0.72f, new Color(0.58f, 0.58f, 0.60f), "Pillar");
            // 초록 나무
            float[] tx = { 0.06f, 0.15f, 0.86f, 0.95f };
            for (int i = 0; i < tx.Length; i++)
            {
                Box(t, tx[i] - 0.008f, 0.56f, tx[i] + 0.008f, 0.66f, new Color(0.40f, 0.28f, 0.16f), "Trunk");
                Oval(t, tx[i], 0.70f, 0.045f, 0.085f, new Color(0.22f, 0.50f, 0.28f), "Crown");
                Oval(t, tx[i] - 0.012f, 0.72f, 0.022f, 0.04f, new Color(0.34f, 0.64f, 0.36f, 0.8f), "Leaf");
            }
            // 인도 + 연석
            Box(t, 0f, 0.46f, 1f, 0.56f, new Color(0.74f, 0.72f, 0.68f), "Sidewalk");
            Box(t, 0f, 0.455f, 1f, 0.468f, new Color(0.55f, 0.53f, 0.50f), "Curb");
            // 회색 도로 + 횡단보도 흰 줄
            Box(t, 0f, 0f, 1f, 0.455f, new Color(0.36f, 0.37f, 0.41f), "Road");
            for (int i = 0; i < 9; i++)
            {
                float x0 = 0.30f + i * 0.048f;
                Box(t, x0, 0.04f, x0 + 0.028f, 0.42f, new Color(0.93f, 0.93f, 0.90f, 0.92f), "Zebra");
            }
            Box(t, 0f, 0.225f, 0.24f, 0.235f, new Color(0.95f, 0.85f, 0.35f, 0.7f), "CenterLine");
            Box(t, 0.76f, 0.225f, 1f, 0.235f, new Color(0.95f, 0.85f, 0.35f, 0.7f), "CenterLine");
            // 신호등
            Box(t, 0.245f, 0.455f, 0.255f, 0.60f, new Color(0.25f, 0.25f, 0.28f), "SignalPole");
            Box(t, 0.232f, 0.60f, 0.268f, 0.70f, new Color(0.20f, 0.20f, 0.22f), "SignalBox");
            Oval(t, 0.25f, 0.68f, 0.010f, 0.016f, new Color(0.9f, 0.25f, 0.2f), "Red");
            Oval(t, 0.25f, 0.62f, 0.010f, 0.016f, new Color(0.35f, 0.85f, 0.45f), "Green");
        }

        // ------------------------------------------------------------------ 캠프장: 초록 바탕 + 흙길 + 버스

        private static void DrawCamp(RectTransform t)
        {
            Gradient(t, new Color(0.52f, 0.70f, 0.88f), new Color(0.80f, 0.88f, 0.94f), 5, 0.55f, 1f);
            Oval(t, 0.20f, 0.52f, 0.28f, 0.14f, new Color(0.24f, 0.44f, 0.32f), "Hill");
            Oval(t, 0.58f, 0.50f, 0.30f, 0.12f, new Color(0.28f, 0.50f, 0.34f), "Hill");
            Oval(t, 0.90f, 0.52f, 0.22f, 0.13f, new Color(0.22f, 0.42f, 0.30f), "Hill");
            // 초록 바탕
            Box(t, 0f, 0f, 1f, 0.55f, new Color(0.38f, 0.60f, 0.32f), "Grass");
            Box(t, 0f, 0.50f, 1f, 0.55f, new Color(0.30f, 0.52f, 0.28f), "GrassFar");
            // 흙길(위로 갈수록 좁아지는 사다리꼴 느낌)
            Box(t, 0.30f, 0f, 0.70f, 0.18f, new Color(0.62f, 0.50f, 0.32f), "Path");
            Box(t, 0.36f, 0.18f, 0.64f, 0.36f, new Color(0.60f, 0.48f, 0.30f), "Path");
            Box(t, 0.42f, 0.36f, 0.58f, 0.52f, new Color(0.57f, 0.45f, 0.28f), "Path");
            // 버스 사각형
            Box(t, 0.06f, 0.30f, 0.42f, 0.53f, new Color(0.92f, 0.70f, 0.22f), "Bus");
            Box(t, 0.07f, 0.53f, 0.41f, 0.56f, new Color(0.78f, 0.56f, 0.16f), "BusRoof");
            Box(t, 0.06f, 0.30f, 0.42f, 0.34f, new Color(0.70f, 0.50f, 0.14f), "BusSkirt");
            for (int i = 0; i < 5; i++)
                Box(t, 0.085f + i * 0.055f, 0.41f, 0.085f + i * 0.055f + 0.042f, 0.50f, new Color(0.62f, 0.80f, 0.88f), "BusWin");
            Box(t, 0.365f, 0.31f, 0.41f, 0.50f, new Color(0.72f, 0.52f, 0.14f), "BusDoor");
            Box(t, 0.375f, 0.40f, 0.40f, 0.49f, new Color(0.62f, 0.80f, 0.88f), "BusDoorWin");
            Oval(t, 0.13f, 0.30f, 0.030f, 0.052f, new Color(0.12f, 0.12f, 0.13f), "Wheel");
            Oval(t, 0.35f, 0.30f, 0.030f, 0.052f, new Color(0.12f, 0.12f, 0.13f), "Wheel");
            Oval(t, 0.13f, 0.30f, 0.012f, 0.022f, new Color(0.70f, 0.70f, 0.72f), "Hub");
            Oval(t, 0.35f, 0.30f, 0.012f, 0.022f, new Color(0.70f, 0.70f, 0.72f), "Hub");
            // 캠프 간판 + 깃발 + 천막
            Box(t, 0.555f, 0.40f, 0.567f, 0.62f, new Color(0.45f, 0.32f, 0.20f), "SignPost");
            Box(t, 0.48f, 0.62f, 0.64f, 0.70f, new Color(0.95f, 0.90f, 0.78f), "SignBoard");
            TextBox(t, 0.48f, 0.62f, 0.64f, 0.70f, "BAYANI CAMP", 16, new Color(0.35f, 0.22f, 0.12f));
            Box(t, 0.72f, 0.42f, 0.86f, 0.52f, new Color(0.88f, 0.58f, 0.36f), "Tent");
            Box(t, 0.70f, 0.52f, 0.88f, 0.55f, new Color(0.70f, 0.42f, 0.26f), "TentRoof");
            Box(t, 0.775f, 0.42f, 0.805f, 0.48f, new Color(0.40f, 0.25f, 0.15f), "TentDoor");
            Box(t, 0.93f, 0.42f, 0.94f, 0.72f, new Color(0.60f, 0.60f, 0.62f), "FlagPole");
            Box(t, 0.94f, 0.65f, 0.99f, 0.71f, new Color(0.85f, 0.30f, 0.28f), "Flag");
        }

        // ------------------------------------------------------------------ 정전 로비: 손전등 빛만

        private static void DrawMuseumDark(RectTransform t)
        {
            Box(t, 0f, 0f, 1f, 1f, new Color(0.015f, 0.02f, 0.035f), "Dark");
            Box(t, 0f, 0f, 1f, 0.42f, new Color(0.05f, 0.06f, 0.09f), "Floor");
            float[] px = { 0.12f, 0.37f, 0.62f, 0.87f };
            foreach (var x in px) Box(t, x - 0.025f, 0.35f, x + 0.025f, 0.95f, new Color(0.08f, 0.09f, 0.13f), "Pillar");
            Box(t, 0.68f, 0.55f, 0.80f, 0.72f, new Color(0.10f, 0.12f, 0.16f), "Poster");
            Oval(t, 0.55f, 0.45f, 0.32f, 0.22f, new Color(0.95f, 0.85f, 0.60f, 0.08f), "Light");
            Oval(t, 0.55f, 0.45f, 0.16f, 0.11f, new Color(0.95f, 0.85f, 0.60f, 0.14f), "LightCore");
            Box(t, 0.48f, 0.40f, 0.62f, 0.415f, new Color(0.20f, 0.30f, 0.45f, 0.5f), "WaterLine");
        }

        // ------------------------------------------------------------------ 분수 아트리움(클리어 후): 조금 밝은 빛

        private static void DrawMuseumRelief(RectTransform t)
        {
            Gradient(t, new Color(0.10f, 0.11f, 0.16f), new Color(0.06f, 0.07f, 0.10f), 4, 0.42f, 1f);
            Box(t, 0f, 0f, 1f, 0.42f, new Color(0.14f, 0.15f, 0.20f), "Floor");
            float[] px = { 0.10f, 0.32f, 0.68f, 0.90f };
            foreach (var x in px) Box(t, x - 0.025f, 0.35f, x + 0.025f, 0.95f, new Color(0.11f, 0.12f, 0.17f), "Pillar");
            Oval(t, 0.5f, 0.78f, 0.36f, 0.26f, new Color(0.95f, 0.85f, 0.60f, 0.10f), "Skylight");
            Oval(t, 0.5f, 0.36f, 0.24f, 0.10f, new Color(0.22f, 0.30f, 0.42f), "FountainRim");
            Oval(t, 0.5f, 0.36f, 0.18f, 0.075f, new Color(0.30f, 0.52f, 0.68f, 0.85f), "Water");
            Box(t, 0.485f, 0.36f, 0.515f, 0.50f, new Color(0.40f, 0.42f, 0.50f), "Pedestal");
            Oval(t, 0.5f, 0.52f, 0.05f, 0.025f, new Color(0.45f, 0.48f, 0.56f), "Bowl");
            Box(t, 0.30f, 0.60f, 0.70f, 0.94f, new Color(0.12f, 0.14f, 0.20f), "Shutter");
            for (int i = 0; i < 6; i++) Box(t, 0.30f, 0.62f + i * 0.052f, 0.70f, 0.62f + i * 0.052f + 0.006f, new Color(0.20f, 0.23f, 0.30f), "ShutterLine");
        }
    }
}
