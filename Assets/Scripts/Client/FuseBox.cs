using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 안내실 벽의 퓨즈함(분전반). [E] 로 FusePanel 을 연다. 정답을 맞히면 전원이 복구되고(Story1Blackout.RestorePower) 표시등이 초록으로 바뀐다.
    /// 퍼즐 내용은 FusePuzzle(시드로 생성) — 온라인에서는 서버가 시드를 내려 줄 자리.
    /// </summary>
    public sealed class FuseBox : Story1Interactable
    {
        public FusePanel panel;
        public SpriteRenderer render;
        public SpriteRenderer led;
        [Tooltip("-1 이면 열 때마다 새로 뽑는다(테스트). 0 이상이면 고정")]
        public int seed = -1;

        private FusePuzzle _puzzle;
        private bool _done;

        public override string Label => _done || Story1Blackout.PowerOn ? null : "퓨즈함 열기";

        private void Awake()
        {
            if (render != null && render.sprite == null) render.sprite = MakeSprite();
            if (led != null && led.sprite == null) led.sprite = MakeLed();
            _done = Story1Blackout.PowerOn;
            UpdateLed();
        }

        protected override void OnEnable() { base.OnEnable(); Story1Blackout.PowerRestored += OnPower; }
        protected override void OnDisable() { base.OnDisable(); Story1Blackout.PowerRestored -= OnPower; }
        private void OnPower() { _done = true; UpdateLed(); }

        public override void Activate()
        {
            if (_puzzle == null) _puzzle = FusePuzzle.Generate(seed >= 0 ? seed : Story1Net.Seed(Story1Net.PFuse));
            panel.Open(this, _puzzle);
        }

        public void OnSolved() { Story1Blackout.RestorePower(); }

        private void Update()
        {
            if (led == null || _done) return;
            // 정전 중에는 빨간 표시등이 천천히 깜빡인다
            float k = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 2.2f));
            led.color = new Color(1f, 0.25f, 0.2f, k);
        }

        private void UpdateLed() { if (led != null) led.color = _done ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(1f, 0.25f, 0.2f, 1f); }

        // ---- 도트 스프라이트(24×32). 파일 없이 코드로 그린다 -------------------------
        private static Sprite MakeSprite()
        {
            const int W = 24, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0, 0, 0, 0);
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            Color outline = new Color(0.08f, 0.09f, 0.11f), body = new Color(0.42f, 0.46f, 0.5f), dark = new Color(0.3f, 0.33f, 0.37f), light = new Color(0.6f, 0.64f, 0.68f);
            void Rect(int x, int y, int w, int h, Color c) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) px[yy * W + xx] = c; }
            Rect(1, 0, 22, 32, outline);
            Rect(2, 1, 20, 30, body);
            Rect(2, 1, 20, 2, dark);
            Rect(2, 29, 20, 2, light);
            Rect(4, 4, 16, 24, dark);          // 문짝
            Rect(5, 5, 14, 22, body);
            // 퓨즈 창 4개
            Color[] fc = { new Color(0.92f, 0.27f, 0.27f), new Color(0.27f, 0.52f, 0.96f), new Color(0.98f, 0.80f, 0.22f), new Color(0.31f, 0.82f, 0.42f) };
            for (int i = 0; i < 4; i++) { Rect(6 + i * 3 + (i > 1 ? 0 : 0), 14, 2, 8, outline); Rect(6 + i * 3, 15, 2, 6, fc[i] * 0.55f); }
            // 경고 삼각형
            Rect(10, 7, 4, 1, new Color(0.95f, 0.8f, 0.2f)); Rect(11, 8, 2, 2, new Color(0.95f, 0.8f, 0.2f));
            Rect(9, 24, 6, 2, outline);       // 손잡이
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);
        }

        private static Sprite MakeLed()
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            px[0] = px[3] = px[12] = px[15] = new Color(1, 1, 1, 0);
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 24f);
        }
    }
}
