using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 역할별 전용 픽셀 캐릭터 시트(직접 그린 도트 캐릭터)를 방향·프레임별 스프라이트로 잘라 쓴다.
    /// 시트 규격: 160x256, 칸 32x64, 행 = 앞(아래)/뒤(위)/왼쪽/오른쪽, 열 = 걷기 5프레임.
    /// 프레임 1이 서 있는 자세, 0·2가 발을 내딛는 자세다.
    /// </summary>
    public static class PixelSprites
    {
        public const int Cols = 5, Rows = 4, CellW = 32, CellH = 64;
        public const float PPU = 32f;
        public const int DirDown = 0, DirUp = 1, DirLeft = 2, DirRight = 3;
        public const int IdleFrame = 1;
        public static readonly int[] WalkCycle = { 0, 1, 2, 3 };
        public const float FrameTime = 0.13f;

        // 발끝(위에서 57번째 줄)이 캐릭터 원점보다 0.55유닛 아래에 오도록 피벗을 잡는다(종이인형 캐릭터와 발 높이를 맞춤)
        public static readonly Vector2 Pivot = new Vector2(0.5f, (7f + 0.55f * PPU) / CellH);

        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        public static Sprite Get(int slot, int dir, int frame)
        {
            var app = GameApp.Instance;
            var tex = app != null ? app.RoleSheet(slot) : null;
            if (tex == null) return null;
            dir = Mathf.Clamp(dir, 0, Rows - 1);
            frame = Mathf.Clamp(frame, 0, Cols - 1);
            int key = slot * 1000 + dir * 10 + frame;
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var rect = new Rect(frame * CellW, (Rows - 1 - dir) * CellH, CellW, CellH);
            s = Sprite.Create(tex, rect, Pivot, PPU, 0, SpriteMeshType.FullRect);
            s.name = $"role{slot}_d{dir}_f{frame}";
            Cache[key] = s;
            return s;
        }

        public static Sprite Portrait(int slot) => Get(slot, DirDown, IdleFrame);

        /// <summary>이동 변위로 바라보는 방향을 정한다. 움직임이 없으면 이전 방향 유지.</summary>
        public static int DirectionFor(float dx, float dy, int previous)
        {
            if (Mathf.Abs(dx) < 1e-4f && Mathf.Abs(dy) < 1e-4f) return previous;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx < 0f ? DirLeft : DirRight;
            return dy > 0f ? DirUp : DirDown;
        }
    }
}
