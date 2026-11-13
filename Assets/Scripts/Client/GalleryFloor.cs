using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 회화 전시실의 끊어진 전선·젖은 바닥. [E] 로 GalleryPanel(퍼즐 2)을 연다. 퍼즐 1(전원 복구) 뒤에 열린다.
    /// 퍼즐 내용은 GalleryPuzzle(시드로 생성) — 온라인에서는 서버가 시드를 내려 줄 자리.
    /// </summary>
    public sealed class GalleryFloor : Story1Interactable
    {
        public GalleryPanel panel;
        public SpriteRenderer render;
        [Tooltip("-1 이면 열 때마다 새로 뽑는다(테스트). 0 이상이면 고정")]
        public int seed = -1;

        private GalleryPuzzle _puzzle;
        private int _round = -1;

        public override string Label => GalleryPanel.Cleared ? null : !Story1Blackout.PowerOn ? "전원이 꺼져 있다 (퓨즈함 먼저)" : "젖은 바닥 건너기";

        private void Awake() { if (render != null && render.sprite == null) render.sprite = MakeSprite(); }

        public override void Activate()
        {
            if (!Story1Blackout.PowerOn || GalleryPanel.Cleared) return;
            int r = Story1Net.RoundOf(Story1Net.PGallery);
            if (_puzzle == null || r != _round) { _puzzle = GalleryPuzzle.Generate(seed >= 0 ? seed : Story1Net.Seed(Story1Net.PGallery)); _round = r; }   // 라운드가 바뀌면 새 문제
            panel.Open(this, _puzzle);
        }

        public void OnSolved()
        {
            if (!Story1Net.IsFinal(Story1Net.PGallery)) { Story1Net.AnnounceRound(Story1Net.PGallery); return; }   // 아직 더 풀어야 한다 — 다음 라운드
            GalleryPanel.MarkCleared(); if (render != null) render.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }

        private void Update()
        {
            if (render == null || GalleryPanel.Cleared) return;
            float k = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 7f)) * (Mathf.PerlinNoise(Time.time * 3f, 0f) > 0.5f ? 1f : 0.3f);
            render.color = new Color(1f, 1f, 1f, k);
        }

        // 끊어진 전선 + 물웅덩이 도트(32×16)
        private static Sprite MakeSprite()
        {
            const int W = 32, H = 16;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            void Rect(int x, int y, int w, int h, Color c) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = c; }
            Color w1 = new Color(0.25f, 0.45f, 0.6f, 0.75f), w2 = new Color(0.4f, 0.65f, 0.8f, 0.8f), wire = new Color(0.1f, 0.1f, 0.12f), sp = new Color(1f, 0.9f, 0.35f);
            Rect(3, 1, 26, 8, w1); Rect(1, 3, 30, 4, w1); Rect(6, 6, 8, 1, w2); Rect(18, 3, 7, 1, w2);
            Rect(14, 9, 2, 6, wire); Rect(12, 14, 3, 1, wire); Rect(16, 9, 5, 1, wire);
            Rect(15, 8, 1, 1, sp); Rect(21, 10, 1, 1, sp); Rect(13, 8, 1, 1, sp);
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);
        }
    }
}
