using System;

namespace Paradox.Game
{
    /// <summary>
    /// 퍼즐 4 — 물길 돌리기. 관리실의 밸브 4개(왼쪽부터 0~3번째)를 돌려 "열려야 할 밸브"만 열면 물이 배수구로 빠진다.
    /// 밸브는 서로 연동돼 있어(하나를 돌리면 짝 하나가 같이 돌아감) 그냥 따라 누르면 안 된다.
    ///  · 내비게이터: 파이프 지도 — "열려야 할 밸브" 를 번호(1~4)로 안다
    ///  · 통역: 밸브에 붙은 필리핀어 서수 이름표(Una/Ikalawa/Ikatlo/Ikaapat) ↔ 번호
    ///  · 관측자: 어느 밸브를 돌리면 어느 밸브가 같이 도는지(연동)
    ///  · 조작자: 위 세 정보로 눌러야 할 밸브를 정해 실제로 돌린다. 누를 때마다·시간이 갈수록 수위가 오른다.
    /// 유니티 비의존 규칙.
    /// </summary>
    public sealed class WaterPuzzle
    {
        public const int Valves = 4;
        public const float MaxWater = 12f, RisePerSecond = 1f / 12f, PressCost = 1f, WrongCost = 2f;

        public static readonly string[] TagTl = { "Una", "Ikalawa", "Ikatlo", "Ikaapat" };

        public int[] tagOf = new int[Valves];        // tagOf[위치] = 이름표 번호(0~3)
        public int[] link = new int[Valves];         // link[위치] = 같이 돌아가는 다른 위치
        public bool[] target = new bool[Valves];     // target[위치] = 열려야 하는가
        public bool[] solution = new bool[Valves];   // 눌러야 할 위치(검증·테스트용)

        public static WaterPuzzle Generate(int seed)
        {
            var rng = new Random(seed);
            for (int attempt = 0; attempt < 5000; attempt++)
            {
                var p = new WaterPuzzle();
                var perm = new[] { 0, 1, 2, 3 };
                for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); var t = perm[i]; perm[i] = perm[j]; perm[j] = t; }
                for (int i = 0; i < Valves; i++)
                {
                    p.tagOf[i] = perm[i];
                    int q;
                    do { q = rng.Next(Valves); } while (q == i);
                    p.link[i] = q;
                }
                int w = 2 + rng.Next(2);   // 누를 밸브 2~3개
                var pick = new bool[Valves];
                int n = 0;
                while (n < w) { int k = rng.Next(Valves); if (!pick[k]) { pick[k] = true; n++; } }
                var st = new bool[Valves];
                for (int i = 0; i < Valves; i++) if (pick[i]) p.Toggle(st, i);
                int open = 0; for (int i = 0; i < Valves; i++) if (st[i]) open++;
                if (open < 1 || open > 3) continue;   // 전부 열라는 퍼즐은 재미가 없다
                // 단순히 "열려야 할 밸브를 그대로 누르기" 로는 풀리지 않아야 한다 (연동 정보가 꼭 필요)
                bool same = true; for (int i = 0; i < Valves; i++) if (st[i] != pick[i]) same = false;
                if (same) continue;
                // 그냥 목표를 그대로 눌러 봐도 안 풀려야 한다
                var naive = new bool[Valves];
                for (int i = 0; i < Valves; i++) if (st[i]) p.Toggle(naive, i);
                bool naiveOk = true; for (int i = 0; i < Valves; i++) if (naive[i] != st[i]) naiveOk = false;
                if (naiveOk) continue;
                Array.Copy(st, p.target, Valves);
                Array.Copy(pick, p.solution, Valves);
                return p;
            }
            throw new InvalidOperationException("물길 퍼즐을 만들지 못했습니다");
        }

        /// <summary>위치 p 의 밸브를 돌린다(연동된 짝도 같이).</summary>
        public void Toggle(bool[] state, int p) { state[p] = !state[p]; state[link[p]] = !state[link[p]]; }

        public bool Check(bool[] state)
        {
            for (int i = 0; i < Valves; i++) if (state[i] != target[i]) return false;
            return true;
        }

        /// <summary>내비게이터 지도에 쓰는 "열려야 할 밸브 번호"(1~4, 오름차순).</summary>
        public int[] TargetNumbers()
        {
            var l = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Valves; i++) if (target[i]) l.Add(tagOf[i] + 1);
            l.Sort();
            return l.ToArray();
        }

        public static string TagKo(int number) => (number + 1) + "번";
    }
}
