using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 학예실(관리실)의 배수 밸브 패널. [E] 로 WaterPanel(퍼즐 4)을 연다. 퍼즐 1(전원 복구) 뒤에 열린다.
    /// 퍼즐 내용은 WaterPuzzle(시드로 생성) — 온라인에서는 서버가 시드를 내려 줄 자리.
    /// </summary>
    public sealed class WaterValves : Story1Interactable
    {
        public WaterPanel panel;
        public SpriteRenderer render;
        [Tooltip("-1 이면 열 때마다 새로 뽑는다(테스트). 0 이상이면 고정")]
        public int seed = -1;

        private WaterPuzzle _puzzle;
        private int _round = -1;

        public override string Label => WaterPanel.Cleared ? null : !Story1Blackout.PowerOn ? "전원이 꺼져 있다 (퓨즈함 먼저)" : "배수 밸브 돌리기";

        private void Awake()
        {
            if (render != null && render.sprite == null) render.sprite = MakeSprite();
            if (render != null) render.sortingOrder = -800;
        }

        public override void Activate()
        {
            if (!Story1Blackout.PowerOn || WaterPanel.Cleared) return;
            int r = Story1Net.RoundOf(Story1Net.PWater);
            if (_puzzle == null || r != _round) { _puzzle = WaterPuzzle.Generate(seed >= 0 ? seed : Story1Net.Seed(Story1Net.PWater)); _round = r; }   // 라운드가 바뀌면 새 문제
            panel.Open(this, _puzzle);
        }

        public void OnSolved()
        {
            if (!Story1Net.IsFinal(Story1Net.PWater)) { Story1Net.AnnounceRound(Story1Net.PWater); return; }   // 아직 더 풀어야 한다 — 다음 라운드
            WaterPanel.MarkCleared(); if (render != null) render.color = new Color(0.65f, 0.75f, 0.7f, 1f);
        }

        // 배수 제어 패널(32×26): 금속 판 + 압력계 + 파이프 + 밸브 바퀴 4개. 벽에 그냥 파이프만 붙어 보이지 않게 한다
        private static Sprite MakeSprite()
        {
            const int W = 32, H = 26;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            void Rect(int x, int y, int w, int h, Color c) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = c; }
            Color ol = new Color(0.08f, 0.09f, 0.11f), plate = new Color(0.22f, 0.27f, 0.31f), pipe = new Color(0.42f, 0.5f, 0.55f), hi = new Color(0.62f, 0.7f, 0.74f), red = new Color(0.8f, 0.25f, 0.22f), dial = new Color(0.85f, 0.88f, 0.8f), warn = new Color(0.95f, 0.78f, 0.25f);
            Rect(0, 0, 32, 26, ol); Rect(1, 1, 30, 24, plate);               // 금속 판
            Rect(2, 22, 28, 2, warn);                                        // 위쪽 경고 띠
            Rect(2, 17, 5, 4, ol); Rect(3, 18, 3, 2, dial); Rect(4, 18, 1, 1, red);   // 압력계
            Rect(1, 6, 30, 8, ol); Rect(1, 7, 30, 6, pipe); Rect(1, 11, 30, 1, hi);   // 파이프
            for (int k = 0; k < 4; k++) { int x = 4 + k * 7; Rect(x, 5, 2, 10, ol); Rect(x, 6, 1, 8, hi); Rect(x - 1, 15, 4, 1, ol); Rect(x - 1, 16, 4, 3, red); }   // 밸브 바퀴 4개
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);
        }
    }
}
