using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Core
{
    /// <summary>
    /// 코드로 만드는 스프라이트 모음(사각형·원·링·간단한 아이콘).
    /// 외부 아트 없이 맵과 장치를 그릴 수 있게 한다. 한 번 만든 스프라이트는 캐시된다.
    /// </summary>
    public static class SpriteLib
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>1x1 흰 픽셀. 스케일로 어떤 사각형이든 만든다(1유닛 = 1스케일).</summary>
        public static Sprite White
        {
            get
            {
                if (Cache.TryGetValue("white", out var s)) return s;
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                s = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                Cache["white"] = s;
                return s;
            }
        }

        /// <summary>부드러운 원(지름 1유닛).</summary>
        public static Sprite Disc(float edge = 0.06f)
        {
            string key = "disc" + edge;
            if (Cache.TryGetValue(key, out var s)) return s;
            int size = 64;
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - d) / edge));
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            Cache[key] = s;
            return s;
        }

        /// <summary>링(지름 1유닛, 두께 비율).</summary>
        public static Sprite Ring(float thickness = 0.14f)
        {
            string key = "ring" + thickness;
            if (Cache.TryGetValue(key, out var s)) return s;
            int size = 64;
            var tex = NewTex(size);
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    float a = d > 1f ? 0f : (d < 1f - thickness ? 0f : 1f);
                    if (d > 0.96f) a *= Mathf.Clamp01((1f - d) / 0.04f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            Cache[key] = s;
            return s;
        }

        /// <summary>가운데가 뚫린 안개 스프라이트(시야 제한). worldSize 유닛을 덮는다.</summary>
        public static Sprite Fog(float visionRadius, float worldSize = 64f, float feather = 2.2f)
        {
            string key = $"fog{visionRadius:0.0}_{worldSize}";
            if (Cache.TryGetValue(key, out var s)) return s;
            int size = 160;
            var tex = NewTex(size);
            tex.filterMode = FilterMode.Bilinear;
            float half = size * 0.5f;
            float wpp = worldSize / size;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float worldR = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) * wpp;
                    float a = Mathf.Clamp01(Mathf.InverseLerp(visionRadius, visionRadius + feather, worldR));
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / worldSize);
            Cache[key] = s;
            return s;
        }

        /// <summary>체커 무늬 바닥 타일(반복용).</summary>
        public static Sprite FloorTile(Color a, Color b, int block = 16)
        {
            string key = $"floor{ColorUtility.ToHtmlStringRGBA(a)}{ColorUtility.ToHtmlStringRGBA(b)}{block}";
            if (Cache.TryGetValue(key, out var s)) return s;
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            var px = new Color[size * size];
            Color seam = Color.Lerp(a, Color.black, 0.18f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool checker = ((x / block) + (y / block)) % 2 == 0;
                    Color c = checker ? a : b;
                    if (x % block == 0 || y % block == 0) c = Color.Lerp(c, seam, 0.5f);
                    px[y * size + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            Cache[key] = s;
            return s;
        }

        /// <summary>
        /// 간단한 픽셀 아이콘. kind: extinguisher / bell / shutter / wheel / console / lever / sign / rubble / speaker / keypad / flame / board
        /// 16x16 픽셀, 1유닛 크기.
        /// </summary>
        public static Sprite Icon(string kind)
        {
            string key = "icon_" + kind;
            if (Cache.TryGetValue(key, out var s)) return s;
            int n = 16;
            var tex = NewTex(n);
            var px = new Color[n * n];
            Color T = new Color(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = T;

            void P(int x, int y, Color c) { if (x >= 0 && x < n && y >= 0 && y < n) px[y * n + x] = c; }
            void R(int x0, int y0, int w, int h, Color c) { for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) P(x, y, c); }

            Color dark = new Color(0.16f, 0.16f, 0.2f);
            Color light = new Color(0.85f, 0.85f, 0.9f);
            switch (kind)
            {
                case "extinguisher":
                    R(5, 1, 6, 11, new Color(0.85f, 0.15f, 0.15f));
                    R(6, 12, 4, 2, dark);
                    R(9, 13, 5, 1, dark); R(13, 10, 1, 4, dark);
                    R(6, 3, 4, 3, light);
                    break;
                case "bell":
                    R(6, 2, 4, 1, new Color(1f, 0.85f, 0.3f));
                    R(5, 3, 6, 6, new Color(1f, 0.8f, 0.25f));
                    R(4, 9, 8, 2, new Color(0.9f, 0.7f, 0.2f));
                    R(7, 11, 2, 2, dark);
                    R(7, 0, 2, 2, dark);
                    break;
                case "shutter":
                    R(2, 1, 12, 14, new Color(0.45f, 0.48f, 0.55f));
                    for (int y = 2; y < 15; y += 3) R(3, y, 10, 1, dark);
                    R(6, 13, 4, 1, new Color(1f, 0.6f, 0.2f));
                    break;
                case "wheel":
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                        if (d < 7.5f && d > 5.5f) P(x, y, new Color(0.75f, 0.55f, 0.25f));
                        if (d < 2f) P(x, y, dark);
                    }
                    R(7, 2, 2, 12, new Color(0.75f, 0.55f, 0.25f)); R(2, 7, 12, 2, new Color(0.75f, 0.55f, 0.25f));
                    break;
                case "console":
                    R(2, 3, 12, 10, new Color(0.35f, 0.38f, 0.45f));
                    R(4, 7, 8, 4, new Color(0.2f, 0.7f, 0.6f));
                    R(4, 4, 2, 2, new Color(0.9f, 0.3f, 0.3f)); R(7, 4, 2, 2, new Color(0.9f, 0.8f, 0.3f)); R(10, 4, 2, 2, new Color(0.3f, 0.8f, 0.4f));
                    R(3, 13, 10, 2, dark);
                    break;
                case "lever":
                    R(3, 2, 10, 4, new Color(0.35f, 0.38f, 0.45f));
                    R(7, 6, 2, 8, new Color(0.8f, 0.8f, 0.85f));
                    R(6, 13, 4, 3, new Color(0.9f, 0.3f, 0.3f));
                    break;
                case "sign":
                    R(2, 6, 12, 9, new Color(0.93f, 0.88f, 0.70f));
                    R(2, 6, 12, 1, dark); R(2, 14, 12, 1, dark); R(2, 6, 1, 9, dark); R(13, 6, 1, 9, dark);
                    R(7, 1, 2, 5, new Color(0.5f, 0.35f, 0.2f));
                    R(4, 9, 8, 1, new Color(0.4f, 0.4f, 0.45f)); R(4, 11, 6, 1, new Color(0.4f, 0.4f, 0.45f));
                    break;
                case "rubble":
                    R(1, 1, 6, 5, new Color(0.45f, 0.42f, 0.4f)); R(8, 2, 7, 6, new Color(0.5f, 0.47f, 0.44f));
                    R(3, 7, 9, 5, new Color(0.4f, 0.38f, 0.36f)); R(0, 10, 5, 5, new Color(0.52f, 0.5f, 0.46f)); R(10, 9, 6, 6, new Color(0.38f, 0.36f, 0.34f));
                    break;
                case "speaker":
                    R(2, 5, 4, 6, new Color(0.3f, 0.32f, 0.38f));
                    R(6, 3, 4, 10, new Color(0.5f, 0.52f, 0.6f));
                    R(11, 6, 1, 4, light); R(13, 4, 1, 8, light);
                    break;
                case "keypad":
                    R(3, 1, 10, 14, new Color(0.3f, 0.32f, 0.38f));
                    R(4, 11, 8, 3, new Color(0.2f, 0.7f, 0.6f));
                    for (int y = 2; y < 10; y += 3) for (int x = 4; x < 12; x += 3) R(x, y, 2, 2, light);
                    break;
                case "flame":
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float dx = (x - 7.5f) / 6f, dy = (y - 5f) / 9f;
                        float d = dx * dx + dy * dy;
                        if (d < 1f) P(x, y, d < 0.35f ? new Color(1f, 0.92f, 0.4f) : (d < 0.7f ? new Color(1f, 0.6f, 0.15f) : new Color(0.9f, 0.25f, 0.1f)));
                    }
                    break;
                case "board":
                    R(1, 3, 14, 11, new Color(0.15f, 0.35f, 0.3f));
                    R(1, 3, 14, 1, light); R(1, 13, 14, 1, light); R(1, 3, 1, 11, light); R(14, 3, 1, 11, light);
                    R(3, 6, 4, 1, light); R(3, 9, 6, 1, light);
                    break;
                case "door":
                    R(3, 1, 10, 14, new Color(0.55f, 0.38f, 0.22f));
                    R(4, 2, 8, 12, new Color(0.65f, 0.46f, 0.28f));
                    R(10, 7, 2, 2, new Color(1f, 0.85f, 0.3f));
                    break;
                case "arrow":
                    R(2, 7, 9, 2, light); R(9, 5, 2, 6, light); R(11, 6, 1, 4, light); R(12, 7, 1, 2, light);
                    break;
                default:
                    R(3, 3, 10, 10, light);
                    break;
            }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            Cache[key] = s;
            return s;
        }

        private static Texture2D NewTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        // ---------- 월드 오브젝트 생성 헬퍼 ----------

        public static SpriteRenderer Quad(Transform parent, string name, Vector2 center, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = White; sr.color = color; sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer Make(Transform parent, string name, Vector2 pos, Sprite sprite, Color color, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
            return sr;
        }
    }
}
