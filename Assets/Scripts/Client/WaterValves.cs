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

        public override string Label => WaterPanel.Cleared ? null : !Story1Blackout.PowerOn ? "전원이 꺼져 있다 (퓨즈함 먼저)" : "배수 밸브 조작";

        private void Awake() { if (render != null && render.sprite == null) render.sprite = MakeSprite(); }

        public override void Activate()
        {
            if (!Story1Blackout.PowerOn || WaterPanel.Cleared) return;
            if (_puzzle == null) _puzzle = WaterPuzzle.Generate(seed >= 0 ? seed : Story1Net.Seed(Story1Net.PWater));
            panel.Open(this, _puzzle);
        }

        public void OnSolved() { WaterPanel.MarkCleared(); if (render != null) render.color = new Color(0.65f, 0.75f, 0.7f, 1f); }

        // 파이프 + 밸브 바퀴 도트(32×20)
        private static Sprite MakeSprite()
        {
            const int W = 32, H = 20;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            void Rect(int x, int y, int w, int h, Color c) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = c; }
            Color ol = new Color(0.08f, 0.09f, 0.11f), pipe = new Color(0.42f, 0.5f, 0.55f), hi = new Color(0.62f, 0.7f, 0.74f), red = new Color(0.8f, 0.25f, 0.22f);
            Rect(0, 4, 32, 8, ol); Rect(0, 5, 32, 6, pipe); Rect(0, 9, 32, 1, hi);
            for (int k = 0; k < 4; k++) { int x = 3 + k * 7; Rect(x, 3, 2, 10, ol); Rect(x, 4, 1, 8, hi); }
            Rect(15, 12, 2, 4, ol);
            Rect(10, 15, 12, 4, ol); Rect(11, 16, 10, 2, red);
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);
        }
    }
}
