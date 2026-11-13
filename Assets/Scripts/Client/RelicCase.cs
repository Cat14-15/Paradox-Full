using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 수장고의 유물 전시대. [E] 로 RelicPanel(퍼즐 3)을 연다. 퍼즐 1(전원 복구) 뒤에 열린다.
    /// 퍼즐 내용은 RelicPuzzle(시드로 생성) — 온라인에서는 서버가 시드를 내려 줄 자리.
    /// </summary>
    public sealed class RelicCase : Story1Interactable
    {
        public RelicPanel panel;
        public SpriteRenderer render;
        [Tooltip("-1 이면 열 때마다 새로 뽑는다(테스트). 0 이상이면 고정")]
        public int seed = -1;

        private RelicPuzzle _puzzle;

        public override string Label => RelicPanel.Cleared ? null : !Story1Blackout.PowerOn ? "전원이 꺼져 있다 (퓨즈함 먼저)" : "유물 전시대 살펴보기";

        private void Awake() { if (render != null && render.sprite == null) render.sprite = MakeSprite(); }

        public override void Activate()
        {
            if (!Story1Blackout.PowerOn || RelicPanel.Cleared) return;
            if (_puzzle == null) _puzzle = RelicPuzzle.Generate(seed >= 0 ? seed : Story1Net.Seed(Story1Net.PRelic));
            panel.Open(this, _puzzle);
        }

        public void OnSolved() { RelicPanel.MarkCleared(); if (render != null) render.color = new Color(0.7f, 0.8f, 0.7f, 1f); }

        // 유리 전시대 + 유물 4개 도트(36×20)
        private static Sprite MakeSprite()
        {
            const int W = 36, H = 20;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            void Rect(int x, int y, int w, int h, Color c) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = c; }
            Color ol = new Color(0.1f, 0.09f, 0.1f), wood = new Color(0.4f, 0.28f, 0.2f), glass = new Color(0.65f, 0.78f, 0.82f, 0.55f), gold = new Color(0.9f, 0.72f, 0.3f), clay = new Color(0.7f, 0.42f, 0.3f), jade = new Color(0.4f, 0.7f, 0.55f);
            Rect(0, 0, 36, 7, ol); Rect(1, 1, 34, 5, wood);
            Rect(0, 7, 36, 12, ol); Rect(1, 8, 34, 10, glass);
            Rect(4, 8, 4, 5, clay); Rect(5, 13, 2, 1, clay);
            Rect(12, 8, 5, 3, gold); Rect(13, 11, 3, 2, gold);
            Rect(20, 8, 4, 6, jade);
            Rect(28, 8, 5, 2, gold); Rect(29, 10, 3, 3, gold);
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 24f);
        }
    }
}
