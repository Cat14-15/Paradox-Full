using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 메인 홀 바닥의 원(최종 퍼즐). [E] 로 FinalPanel 을 연다. 퍼즐 1·2·4 를 모두 마쳐야 열린다.
    /// 퍼즐 내용은 FinalPuzzle(시드로 생성) — 온라인에서는 서버가 시드를 내려 주고 네 명의 입력을 모을 자리.
    /// </summary>
    public sealed class FinalFloor : Story1Interactable
    {
        public FinalPanel panel;
        public SpriteRenderer glow;
        [Tooltip("-1 이면 열 때마다 새로 뽑는다(테스트). 0 이상이면 고정")]
        public int seed = -1;

        private FinalPuzzle _puzzle;

        private static bool Ready => Story1Blackout.PowerOn && GalleryPanel.Cleared && RelicPanel.Cleared && WaterPanel.Cleared;
        public override string Label => FinalPanel.Cleared ? null : Ready ? "바닥의 원 밟기" : "셔터가 내려와 있다 (미션을 먼저)";

        private void Awake() { if (glow != null && glow.sprite == null) glow.sprite = MakeGlow(); }

        public override void Activate()
        {
            if (!Ready || FinalPanel.Cleared) return;
            if (_puzzle == null) _puzzle = FinalPuzzle.Generate(seed >= 0 ? seed : Story1Net.Seed(Story1Net.PFinal));
            panel.Open(this, _puzzle);
        }

        public void OnSolved() { FinalPanel.MarkCleared(); if (glow != null) glow.color = new Color(1f, 0.95f, 0.7f, 0.5f); }

        private void Update()
        {
            if (glow == null || FinalPanel.Cleared) return;
            float a = Ready ? 0.25f + 0.2f * Mathf.Sin(Time.time * 2.5f) : 0f;
            glow.color = new Color(1f, 0.85f, 0.45f, Mathf.Max(0f, a));
        }

        // 바닥의 원 위에 겹치는 은은한 타원 빛(64×32)
        private static Sprite MakeGlow()
        {
            const int W = 64, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - (W - 1) * 0.5f) / (W * 0.5f), dy = (y - (H - 1) * 0.5f) / (H * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * W + x] = new Color(1, 1, 1, Mathf.Clamp01(1f - d) * 0.9f);
                }
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
