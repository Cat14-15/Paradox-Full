using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Core
{
    /// <summary>
    /// 코드로 만드는 스프라이트 모음(사각형·원·링·점선 원·비네트·16x16 픽셀 아이콘).
    /// 외부 아트 없이 박물관 맵과 오브젝트를 그릴 수 있게 한다. 한 번 만든 스프라이트는 캐시된다.
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

        /// <summary>점선 링(지름 1유닛). 최종 집결의 "미도착" 원에 쓴다. dashes = 점선 조각 수.</summary>
        public static Sprite DashedRing(int dashes = 20, float thickness = 0.11f)
        {
            string key = $"dring{dashes}_{thickness}";
            if (Cache.TryGetValue(key, out var s)) return s;
            int size = 128;
            var tex = NewTex(size);
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a = d > 1f ? 0f : (d < 1f - thickness ? 0f : 1f);
                    if (a > 0f)
                    {
                        float ang = Mathf.Atan2(dy, dx);
                        if (ang < 0f) ang += Mathf.PI * 2f;
                        int seg = Mathf.FloorToInt(ang / (Mathf.PI * 2f) * dashes * 2f);
                        if (seg % 2 == 1) a = 0f;
                    }
                    if (d > 0.97f) a *= Mathf.Clamp01((1f - d) / 0.03f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            Cache[key] = s;
            return s;
        }

        /// <summary>가장자리로 갈수록 진해지는 비네트(1유닛 정사각형). 화면 붉은 경고 테두리와 월드 어둠 연출에 쓴다.</summary>
        public static Sprite Vignette(float inner = 0.55f)
        {
            string key = "vig" + inner;
            if (Cache.TryGetValue(key, out var s)) return s;
            int size = 96;
            var tex = NewTex(size);
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 사각형 가장자리 기준 거리(0 중앙 ~ 1 모서리)
                    float nx = Mathf.Abs(x - c) / c, ny = Mathf.Abs(y - c) / c;
                    float d = Mathf.Sqrt(nx * nx + ny * ny) / 1.4142f;
                    float a = Mathf.Pow(Mathf.Clamp01((d - inner) / (1f - inner)), 1.6f);
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
        /// 16x16 픽셀 아이콘(1유닛 크기).
        /// STRANGER Stage 1: panel / poster / lamp / board / leverpad / mural / colorlamp / junction / fountain / shutterdoor / beacon / sign
        /// HUD·신호: unknown(비담당 "?" 상자) / danger / you / go / where / yes / no(휠 신호) / donut / list / hand / zoom / chat(액션 열)
        /// (옛 아이콘 extinguisher / bell / shutter / wheel / console / lever / rubble / speaker / keypad / flame / door / arrow 도 남겨 둔다)
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
            void Frame(int x0, int y0, int w, int h, Color c) { R(x0, y0, w, 1, c); R(x0, y0 + h - 1, w, 1, c); R(x0, y0, 1, h, c); R(x0 + w - 1, y0, 1, h, c); }

            Color dark = new Color(0.16f, 0.16f, 0.2f);
            Color light = new Color(0.85f, 0.85f, 0.9f);
            Color steel = new Color(0.36f, 0.40f, 0.48f);
            Color steelDark = new Color(0.24f, 0.27f, 0.33f);
            Color amber = new Color(0.95f, 0.77f, 0.45f);
            Color cream = new Color(0.93f, 0.88f, 0.72f);
            Color red = new Color(0.9f, 0.3f, 0.3f);
            Color yellow = new Color(0.95f, 0.85f, 0.3f);
            Color green = new Color(0.35f, 0.82f, 0.45f);
            Color blue = new Color(0.35f, 0.55f, 0.95f);
            Color teal = new Color(0.25f, 0.65f, 0.6f);
            Color wood = new Color(0.5f, 0.35f, 0.2f);
            switch (kind)
            {
                // ---------------- STRANGER Stage 1 ----------------
                case "panel":
                    // 비상 제어반: 세로 계기판 + 작은 화면 + 키패드 + 레버 손잡이
                    R(2, 1, 12, 14, steel);
                    Frame(2, 1, 12, 14, steelDark);
                    R(4, 10, 8, 3, new Color(0.15f, 0.45f, 0.35f));       // 화면
                    R(5, 11, 2, 1, new Color(0.6f, 1f, 0.7f));
                    for (int y = 3; y < 9; y += 2) for (int x = 4; x < 10; x += 2) R(x, y, 1, 1, light); // 키패드
                    R(11, 3, 2, 6, dark);                                    // 레버 홈
                    R(11, 7, 2, 2, red);                                     // 레버 손잡이
                    break;
                case "poster":
                    // 코팅된 벽보: 크림색 종이 + 표(줄) + 압정
                    R(2, 1, 12, 14, cream);
                    Frame(2, 1, 12, 14, new Color(0.6f, 0.5f, 0.35f));
                    R(4, 11, 8, 1, dark); R(4, 9, 8, 1, steel); R(4, 7, 8, 1, steel); R(4, 5, 8, 1, steel);
                    R(7, 3, 1, 9, steel);
                    R(7, 14, 2, 1, red);                                     // 압정
                    R(4, 3, 3, 1, red);                                      // 제목 줄
                    break;
                case "lamp":
                    // 경고등 패널: 검은 상자 + 큰 램프 원(색은 코드가 덧칠)
                    R(3, 0, 10, 3, steelDark);
                    R(2, 3, 12, 12, dark);
                    Frame(2, 3, 12, 12, steel);
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 8.5f) * (y - 8.5f));
                        if (d < 4.2f) P(x, y, light);
                    }
                    break;
                case "board":
                    // 침수 현황판: 어두운 디오라마 판 + 구역 막대 3줄(초록/노랑/빨강)
                    R(1, 2, 14, 12, new Color(0.12f, 0.2f, 0.26f));
                    Frame(1, 2, 14, 12, steel);
                    R(3, 10, 5, 2, green); R(8, 10, 5, 2, steelDark);
                    R(3, 7, 8, 2, yellow); R(11, 7, 2, 2, steelDark);
                    R(3, 4, 10, 2, red);
                    R(6, 0, 4, 2, steelDark);                                // 받침
                    break;
                case "leverpad":
                    // 방향 레버반: 제어대 + 십자 홈 + 가운데 레버 손잡이
                    R(1, 2, 14, 11, steel);
                    Frame(1, 2, 14, 11, steelDark);
                    R(7, 4, 2, 7, dark); R(4, 7, 8, 1, dark);                 // 십자 홈
                    R(7, 7, 2, 2, red);                                      // 손잡이
                    R(3, 13, 10, 2, steelDark);                              // 받침
                    R(7, 0, 2, 2, amber); R(7, 11, 2, 1, amber);             // 위·아래 표시
                    break;
                case "mural":
                    // 나침반 벽화 매뉴얼: 벽돌색 벽 + 원형 나침반 + 4방향 점
                    R(1, 1, 14, 14, new Color(0.55f, 0.42f, 0.32f));
                    Frame(1, 1, 14, 14, wood);
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                        if (d < 5.6f && d > 4.4f) P(x, y, cream);
                    }
                    R(7, 3, 2, 10, cream); R(3, 7, 10, 2, cream);
                    R(7, 11, 2, 2, red);                                     // 북쪽
                    break;
                case "colorlamp":
                    // 색상 표시등: 기둥 위 램프 갓(색은 코드가 덧칠)
                    R(7, 0, 2, 7, steelDark);
                    R(5, 0, 6, 1, steelDark);
                    R(4, 7, 8, 2, steel);
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 11f) * (y - 11f));
                        if (d < 3.8f) P(x, y, light);
                    }
                    break;
                case "junction":
                    // 배전함: 회색 함 + 단자 두 줄 + 배선 + 경고 스티커
                    R(2, 0, 12, 15, new Color(0.42f, 0.44f, 0.5f));
                    Frame(2, 0, 12, 15, steelDark);
                    for (int x = 4; x < 12; x += 2) { R(x, 12, 1, 1, dark); R(x, 3, 1, 1, dark); }
                    R(4, 5, 1, 7, red); R(6, 5, 1, 7, yellow); R(8, 5, 1, 7, green); R(10, 5, 1, 7, blue);
                    R(11, 13, 2, 1, yellow);
                    break;
                case "fountain":
                    // 분수(위에서 본 모양): 돌 테두리 + 물 + 가운데 물줄기
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                        if (d < 7.8f) P(x, y, d > 6.3f ? new Color(0.62f, 0.6f, 0.56f) : (d > 2.2f ? new Color(0.3f, 0.55f, 0.8f) : light));
                    }
                    R(7, 7, 2, 2, Color.white);
                    R(3, 9, 2, 1, new Color(0.6f, 0.8f, 1f)); R(10, 5, 2, 1, new Color(0.6f, 0.8f, 1f));
                    break;
                case "shutterdoor":
                    // 잠긴 셔터 문: 가로줄 셔터 + 자물쇠
                    R(1, 0, 14, 16, new Color(0.45f, 0.48f, 0.55f));
                    for (int y = 1; y < 16; y += 3) R(1, y, 14, 1, steelDark);
                    R(6, 5, 4, 4, amber); R(7, 9, 2, 2, amber); R(7, 6, 2, 2, dark);
                    break;
                case "beacon":
                    // 핑 비콘: 아래로 향한 앰버 화살촉 + 바닥 점
                    R(3, 12, 10, 3, amber);
                    R(4, 10, 8, 2, amber); R(5, 8, 6, 2, amber); R(6, 6, 4, 2, amber); R(7, 4, 2, 2, amber);
                    R(6, 0, 4, 2, new Color(0.95f, 0.77f, 0.45f, 0.6f));
                    break;
                case "sign":
                    // 표지판: 크림 판 + 기둥 + 글줄
                    R(2, 6, 12, 9, cream);
                    Frame(2, 6, 12, 9, dark);
                    R(7, 1, 2, 5, wood);
                    R(4, 9, 8, 1, new Color(0.4f, 0.4f, 0.45f)); R(4, 11, 6, 1, new Color(0.4f, 0.4f, 0.45f));
                    break;

                // ---------------- HUD·신호 아이콘(P-A1 / D-A7 / P-A8 / P-A9) ----------------
                case "unknown":
                    {
                        // 비담당 오브젝트 "?" 상자(명세 5-3, PDF s12): 슬레이트 사각 + 테두리 + 가운데 물음표
                        Color slate = new Color(0.16f, 0.19f, 0.26f);
                        Color edge = new Color(0.45f, 0.50f, 0.60f);
                        Color q = new Color(0.72f, 0.77f, 0.86f);
                        R(1, 1, 14, 14, slate);
                        Frame(1, 1, 14, 14, edge);
                        R(5, 11, 6, 2, q); R(4, 10, 2, 1, q); R(10, 9, 2, 2, q); R(8, 7, 3, 2, q); R(7, 5, 2, 2, q); R(7, 2, 2, 2, q);
                        break;
                    }
                case "danger":
                    {
                        // 경고 삼각형(빨강) + 느낌표
                        Color darkRed = new Color(0.55f, 0.12f, 0.12f);
                        for (int y = 1; y <= 14; y++)
                        {
                            float halfW = 1f + (14 - y) * 6f / 13f;
                            for (int x = 0; x < n; x++) if (Mathf.Abs(x + 0.5f - 8f) <= halfW) P(x, y, red);
                        }
                        R(1, 1, 14, 1, darkRed);
                        R(7, 5, 2, 5, dark); R(7, 2, 2, 2, dark);
                        break;
                    }
                case "you":
                    {
                        // 위를 가리키는 손가락(살구색 + 어두운 외곽선 + 소매)
                        Color skin = new Color(0.98f, 0.85f, 0.66f);
                        Color outline = new Color(0.45f, 0.28f, 0.18f);
                        Color cuff = new Color(0.30f, 0.50f, 0.90f);
                        R(2, 2, 11, 8, outline); R(1, 4, 1, 5, outline);            // 주먹 외곽
                        R(5, 8, 5, 7, outline); R(6, 15, 3, 1, outline);           // 검지 외곽
                        R(3, 3, 9, 6, skin); R(2, 5, 1, 3, skin);                  // 주먹
                        R(6, 9, 3, 6, skin);                                       // 검지
                        R(9, 4, 1, 4, outline); R(11, 6, 1, 1, outline);           // 접힌 손가락 마디
                        R(4, 1, 7, 2, cuff); R(3, 0, 9, 1, outline);               // 소매
                        break;
                    }
                case "go":
                    R(2, 6, 8, 4, dark);
                    for (int i = 0; i < 5; i++) R(10 + i, 3 + i, 1, 10 - 2 * i, dark);
                    break;
                case "where":
                    R(5, 11, 6, 2, dark); R(4, 10, 2, 1, dark); R(10, 9, 2, 2, dark); R(8, 7, 3, 2, dark); R(7, 5, 2, 2, dark); R(7, 2, 2, 2, dark);
                    break;
                case "yes":
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                        if (d <= 6.5f && d >= 4.5f) P(x, y, dark);
                    }
                    break;
                case "no":
                    for (int i = 0; i <= 10; i++) { R(2 + i, 2 + i, 2, 2, dark); R(2 + i, 12 - i, 2, 2, dark); }
                    break;
                case "donut":
                    // 6칸 도넛: 밝은 링 + 칸 나누는 선 6개
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float dx = x - 7.5f, dy = y - 7.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > 7.4f || d < 3.0f) continue;
                        float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (deg < 0f) deg += 360f;
                        float a = Mathf.Repeat(deg - 30f, 60f);
                        P(x, y, a < 8f || a > 52f ? dark : light);
                    }
                    break;
                case "list":
                    // 지난 신호 목록: 점 + 줄 3개
                    for (int y = 3; y <= 11; y += 4) { R(3, y, 2, 2, light); R(6, y, 8, 2, light); }
                    break;
                case "hand":
                    // 상호작용: 펼친 손바닥
                    R(3, 2, 10, 6, light);
                    R(3, 8, 2, 5, light); R(6, 8, 2, 6, light); R(9, 8, 2, 6, light); R(12, 8, 2, 4, light);
                    R(1, 3, 2, 4, light);
                    R(5, 0, 6, 2, light);
                    break;
                case "zoom":
                    // 확대: 돋보기(렌즈 링 + 손잡이)
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x - 6.5f) * (x - 6.5f) + (y - 9.5f) * (y - 9.5f));
                        if (d <= 5.0f && d >= 3.4f) P(x, y, light);
                    }
                    for (int i = 0; i < 5; i++) R(10 + i, 5 - i, 2, 2, light);
                    break;
                case "chat":
                    // 말하기: 말풍선 + 점 3개
                    R(2, 5, 12, 8, light);
                    P(2, 5, T); P(13, 5, T); P(2, 12, T); P(13, 12, T);
                    R(4, 3, 2, 2, light); P(4, 2, light);
                    R(4, 8, 2, 2, dark); R(7, 8, 2, 2, dark); R(10, 8, 2, 2, dark);
                    break;

                // ---------------- 옛 아이콘(호환) ----------------
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
                    R(2, 3, 12, 10, steel);
                    R(4, 7, 8, 4, teal);
                    R(4, 4, 2, 2, red); R(7, 4, 2, 2, yellow); R(10, 4, 2, 2, green);
                    R(3, 13, 10, 2, dark);
                    break;
                case "lever":
                    R(3, 2, 10, 4, steel);
                    R(7, 6, 2, 8, new Color(0.8f, 0.8f, 0.85f));
                    R(6, 13, 4, 3, red);
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
                    R(4, 11, 8, 3, teal);
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
                    Frame(3, 3, 10, 10, dark);
                    R(7, 6, 2, 4, dark); R(7, 11, 2, 1, dark);   // 물음표 느낌
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
