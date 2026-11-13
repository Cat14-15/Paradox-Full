using System;

namespace Paradox.Game
{
    /// <summary>
    /// 퍼즐 5 — 메인 홀 4인 동시 입력(최종). 바닥의 원 4개는 네 역할의 것.
    ///  · 라운드 1~3: 원이 한 박자씩 차례로 켜진다(4박 + 쉼 1박). 각자 화면에는 "내 원"이 켜지는 순간만 보인다
    ///    → 모두의 "몇 번째로 켜졌는지"를 합쳐야 전체 순서가 나온다. 조작자가 그 순서대로 입력.
    ///  · 마지막: 카운트(하나·둘·셋·넷) 뒤 "지금!"에 네 명이 동시에 자기 원을 밟는다. 한 명이라도 어긋나면 카운트부터 다시.
    /// 유니티 비의존 규칙.
    /// </summary>
    public sealed class FinalPuzzle
    {
        public const int Rounds = 3, Roles = 4, CycleTicks = 5;
        public static readonly float[] TickSeconds = { 1.0f, 0.9f, 0.8f };
        public const float BeatSeconds = 0.8f;       // 마지막 카운트 간격
        public const float GoWindow = 0.35f;         // "지금!" ± 이 안에 네 명 모두 밟아야 한다

        /// <summary>order[라운드][박] = 그 박에 켜지는 역할(원)</summary>
        public int[][] order = new int[Rounds][];

        public static FinalPuzzle Generate(int seed)
        {
            var rng = new Random(seed);
            var p = new FinalPuzzle();
            int[] prev = null;
            for (int r = 0; r < Rounds; r++)
            {
                int[] perm;
                do
                {
                    perm = new[] { 0, 1, 2, 3 };
                    for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); var t = perm[i]; perm[i] = perm[j]; perm[j] = t; }
                } while (prev != null && Same(perm, prev));   // 라운드마다 순서가 달라야 한다
                p.order[r] = perm; prev = perm;
            }
            return p;
        }

        private static bool Same(int[] a, int[] b) { for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

        /// <summary>그 라운드에서 역할 role 의 원이 켜지는 박(0~3).</summary>
        public int RankOf(int round, int role) => Array.IndexOf(order[round], role);

        /// <summary>라운드 시작 후 t 초에 현재 몇 번째 박인가(0~4, 4 = 쉼).</summary>
        public static int TickAt(int round, float t) => t < 0f ? CycleTicks - 1 : (int)(t / TickSeconds[round]) % CycleTicks;

        /// <summary>t 초에 역할 role 의 원이 켜져 있는가(자기 박에서만).</summary>
        public bool Lit(int round, int role, float t) => TickAt(round, t) == RankOf(round, role);

        public bool CheckOrder(int round, int[] entered)
        {
            if (entered == null || entered.Length != Roles) return false;
            return Same(entered, order[round]);
        }

        /// <summary>네 명의 누른 시각(음수 = 안 누름)이 모두 goTime ± GoWindow 안인가.</summary>
        public static bool Simultaneous(float[] pressTimes, float goTime)
        {
            if (pressTimes == null || pressTimes.Length != Roles) return false;
            foreach (float t in pressTimes) if (t < 0f || Math.Abs(t - goTime) > GoWindow) return false;
            return true;
        }
    }
}
