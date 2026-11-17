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
        private int _round = -1;

        public override string Label => RelicPanel.Cleared ? null : !Story1Blackout.PowerOn ? "전원이 꺼져 있다 (퓨즈함 먼저)" : "유물 전시대 살펴보기";

        private void Awake()
        {
            if (render != null && render.sprite == null) render.sprite = MakeSprite();
            if (render != null) render.sortingOrder = -800;   // 캐릭터가 전시대 "뒤"로 가려지지 않게(퓨즈함과 같다)
        }

        public override void Activate()
        {
            if (!Story1Blackout.PowerOn || RelicPanel.Cleared) return;
            int r = Story1Net.RoundOf(Story1Net.PRelic);
            if (_puzzle == null || r != _round) { _puzzle = RelicPuzzle.Generate(seed >= 0 ? seed : Story1Net.Seed(Story1Net.PRelic)); _round = r; }   // 라운드가 바뀌면 새 문제
            panel.Open(this, _puzzle);
        }

        public void OnSolved()
        {
            if (!Story1Net.IsFinal(Story1Net.PRelic)) { Story1Net.AnnounceRound(Story1Net.PRelic); return; }   // 아직 더 풀어야 한다 — 다음 라운드
            RelicPanel.MarkCleared(); if (render != null) render.color = new Color(0.7f, 0.8f, 0.7f, 1f);
        }

        // 유리 전시대(다리 + 바닥 그림자 포함) + 유물 4개 도트(36×30): 공중에 떠 보이지 않게 바닥에 닿는다
        private static Sprite MakeSprite()
        {
            const int W = 36, H = 30, O = 8;   // O = 다리·그림자 높이
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            void Rect(int x, int y, int w, int h, Color c) { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = c; }
            Color ol = new Color(0.1f, 0.09f, 0.1f), wood = new Color(0.4f, 0.28f, 0.2f), glass = new Color(0.65f, 0.78f, 0.82f, 0.55f), gold = new Color(0.9f, 0.72f, 0.3f), clay = new Color(0.7f, 0.42f, 0.3f), jade = new Color(0.4f, 0.7f, 0.55f), shadow = new Color(0f, 0f, 0f, 0.35f);
            Rect(2, 0, 32, 2, shadow); Rect(0, 1, 36, 1, shadow);          // 바닥 그림자
            Rect(3, 2, 3, O - 2, ol); Rect(30, 2, 3, O - 2, ol);           // 다리
            Rect(0, O, 36, 7, ol); Rect(1, O + 1, 34, 5, wood);
            Rect(0, O + 7, 36, 12, ol); Rect(1, O + 8, 34, 10, glass);
            Rect(4, O + 8, 4, 5, clay); Rect(5, O + 13, 2, 1, clay);
            Rect(12, O + 8, 5, 3, gold); Rect(13, O + 11, 3, 2, gold);
            Rect(20, O + 8, 4, 6, jade);
            Rect(28, O + 8, 5, 2, gold); Rect(29, O + 10, 3, 3, gold);
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, (O + 10f) / H), 24f);   // 전시대 몸통이 원래 자리에 오도록
        }
    }
}
