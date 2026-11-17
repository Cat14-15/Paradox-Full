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

        public override string Label => GalleryPanel.Cleared ? null : !Story1Blackout.PowerOn ? "전원이 꺼져 있다 (퓨즈함 먼저)" : "젖은 바닥 건너가기";

        private void Awake()
        {
            if (render != null && render.sprite == null) render.sprite = MakeSprite();
            if (render != null) { render.sortingOrder = -800; transform.localScale = Vector3.one * 2f; }   // 바닥 물건: 캐릭터가 위로 지나간다. 눈에 띄게 크게
            radius = 3f;
        }

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
            float k = 0.88f + 0.12f * (Mathf.PerlinNoise(Time.time * 6f, 0f) > 0.5f ? 1f : 0f);   // 전선 불꽃이 깜빡이되 형체는 늘 또렷하게
            render.color = new Color(1f, 1f, 1f, k);
        }

        // "젖은 바닥 주의" 표지판 + 큰 물웅덩이 + 끊어져 늘어진 전선과 불꽃 (48×32). 한눈에 "저기다" 싶게
        private static Sprite MakeSprite()
        {
            const int W = 48, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            void Rect(int x, int y, int w, int h, Color c) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = c; }
            Color w1 = new Color(0.22f, 0.42f, 0.62f, 0.92f), w2 = new Color(0.45f, 0.72f, 0.9f, 0.95f), wire = new Color(0.08f, 0.08f, 0.1f), sp = new Color(1f, 0.92f, 0.35f), yel = new Color(0.98f, 0.8f, 0.15f), ol = new Color(0.1f, 0.08f, 0.05f);
            // 물웅덩이
            Rect(10, 1, 34, 11, w1); Rect(6, 3, 42, 7, w1); Rect(14, 9, 26, 3, w1);
            Rect(14, 7, 10, 1, w2); Rect(28, 4, 9, 1, w2); Rect(20, 10, 6, 1, w2);
            // 표지판(A자 모양, 노란 바탕에 검은 느낌표)
            Rect(2, 8, 14, 2, ol); Rect(3, 9, 12, 1, yel);
            Rect(3, 10, 12, 18, ol); Rect(4, 11, 10, 16, yel);
            Rect(8, 15, 2, 8, ol); Rect(8, 12, 2, 2, ol);
            Rect(3, 27, 12, 1, ol);
            // 끊어진 전선과 불꽃
            Rect(31, 17, 2, 15, wire); Rect(30, 16, 1, 2, wire); Rect(33, 16, 1, 1, wire);
            Rect(29, 14, 2, 1, sp); Rect(33, 13, 2, 1, sp); Rect(31, 12, 1, 3, sp); Rect(35, 15, 1, 1, sp); Rect(28, 12, 1, 1, sp);
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.35f), 24f);
        }
    }
}
