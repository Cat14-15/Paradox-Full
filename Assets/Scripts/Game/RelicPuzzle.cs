using System;

namespace Paradox.Game
{
    /// <summary>
    /// 퍼즐 3 — 유물 보관실. 선반에 유물 5개(칸 0~4), 받침대 4개(왼쪽부터 0~3). 그중 하나는 가짜(복제품).
    /// 진짜 4개를 "오래된 순서"대로 받침대에 올리면 열쇠 보관함이 열린다. 조작자는 한 번에 하나만 들 수 있다.
    ///  · 통역: 유물 이름표의 필리핀어 시대 이름 ↔ 시대(고대·스페인·미국·현대)
    ///  · 관측자: 자외선등 아래에서 어느 칸이 복제품인지 (가짜는 진짜와 같은 시대 이름표를 달고 있다)
    ///  · 내비게이터: 받침대 배치도 — 각 받침대에 올릴 유물이 몇 번째로 오래된 것인지
    ///  · 조작자: 이름표를 읽고(못 읽는다) 직접 옮긴다
    /// 유니티 비의존 규칙.
    /// </summary>
    public sealed class RelicPuzzle
    {
        public const int Slots = 5, Pedestals = 4, Eras = 4;
        public static readonly string[] EraTl = { "Sinauna", "Kastila", "Amerikano", "Makabago" };
        public static readonly string[] EraKo = { "고대", "스페인 식민 시대", "미국 식민 시대", "현대" };

        public int[] eraOf = new int[Slots];          // 칸의 이름표 시대(0=가장 오래됨 … 3=가장 최근)
        public int fake;                              // 복제품인 칸
        public int[] pedestalEra = new int[Pedestals]; // 받침대에 올려야 할 시대

        public static RelicPuzzle Generate(int seed)
        {
            var rng = new Random(seed);
            var p = new RelicPuzzle();
            p.fake = rng.Next(Slots);
            var eras = Shuffle(rng, new[] { 0, 1, 2, 3 });
            int k = 0;
            for (int s = 0; s < Slots; s++) if (s != p.fake) p.eraOf[s] = eras[k++];
            p.eraOf[p.fake] = rng.Next(Eras);            // 진짜 하나와 같은 이름표 — 이름표만으론 못 가린다
            do { p.pedestalEra = Shuffle(rng, new[] { 0, 1, 2, 3 }); } while (IsSorted(p.pedestalEra));   // 왼→오 정렬 그대로면 배치도가 필요 없다
            return p;
        }

        private static int[] Shuffle(Random rng, int[] a)
        {
            for (int i = a.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }

        private static bool IsSorted(int[] a) { for (int i = 0; i < a.Length; i++) if (a[i] != i) return false; return true; }

        /// <summary>placed[받침대] = 올려 둔 칸(없으면 -1). 네 받침대가 모두 진품이고 시대가 맞으면 true.</summary>
        public bool Check(int[] placed)
        {
            if (placed == null || placed.Length != Pedestals) return false;
            for (int p = 0; p < Pedestals; p++)
            {
                int s = placed[p];
                if (s < 0 || s >= Slots || s == fake || eraOf[s] != pedestalEra[p]) return false;
            }
            return true;
        }

        public static bool IsFull(int[] placed) { foreach (int s in placed) if (s < 0) return false; return true; }

        /// <summary>받침대 p 에 올릴 진짜 유물이 있는 칸.</summary>
        public int RealSlotFor(int pedestal)
        {
            for (int s = 0; s < Slots; s++) if (s != fake && eraOf[s] == pedestalEra[pedestal]) return s;
            return -1;
        }
    }
}
