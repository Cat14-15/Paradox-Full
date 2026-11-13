using System;
using System.Collections.Generic;

namespace Paradox.Game
{
    /// <summary>
    /// 퍼즐 1 — 퓨즈 복구. 퓨즈 4개(빨·파·노·초)를 슬롯 1~4(왼→오)에 하나씩 꽂는다.
    /// 정답은 네 역할이 나눠 가진 단서를 합쳐야만 하나로 정해진다(혼자서는 못 푼다).
    ///  · 통역: 필리핀어 설명서 — "A 는 B 보다 왼쪽" 2개 (순서 규칙)
    ///  · 관측자: 퓨즈함 속 그을음 — "슬롯 k 에는 색 X 불가" 2개 (제외 조건)
    ///  · 내비게이터: 배선도 — "색 X 는 슬롯 k 에 연결" 1개 (위치 규칙)
    ///  · 조작자: 단서를 모아 실제로 꽂는다
    /// 유니티에 의존하지 않는 순수 규칙이라 서버(GameLogic)·테스트·솔로 모드가 그대로 쓴다.
    /// </summary>
    public sealed class FusePuzzle
    {
        public const int Count = 4;
        public const int Empty = -1;

        public static readonly string[] ColorKo = { "빨강", "파랑", "노랑", "초록" };
        public static readonly string[] ColorTl = { "pula", "asul", "dilaw", "berde" };   // 필리핀어(타갈로그)

        public struct OrderClue { public int left, right; }      // 색 left 는 색 right 보다 왼쪽 슬롯
        public struct SlotColor { public int slot, color; }

        /// <summary>answer[슬롯] = 색 번호</summary>
        public int[] answer = new int[Count];
        public OrderClue[] order = new OrderClue[2];     // 통역
        public SlotColor[] banned = new SlotColor[2];    // 관측자
        public SlotColor wired;                          // 내비게이터

        public static FusePuzzle Generate(int seed)
        {
            var rng = new Random(seed);
            for (int attempt = 0; attempt < 5000; attempt++)
            {
                var p = new FusePuzzle();
                var perm = new List<int> { 0, 1, 2, 3 };
                for (int i = perm.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = perm[i]; perm[i] = perm[j]; perm[j] = t; }
                for (int s = 0; s < Count; s++) p.answer[s] = perm[s];

                // 내비게이터: 한 슬롯의 정답을 그대로 알려 준다
                int ws = rng.Next(Count);
                p.wired = new SlotColor { slot = ws, color = p.answer[ws] };

                // 관측자: 서로 다른 두 슬롯에서 "여기엔 안 된다"는 색(정답이 아닌 색)
                int s1 = rng.Next(Count), s2;
                do { s2 = rng.Next(Count); } while (s2 == s1);
                p.banned[0] = new SlotColor { slot = s1, color = WrongColor(rng, p.answer[s1]) };
                p.banned[1] = new SlotColor { slot = s2, color = WrongColor(rng, p.answer[s2]) };

                // 통역: 정답에서 성립하는 서로 다른 두 쌍의 좌우 순서
                int a1, b1, a2, b2;
                do { a1 = rng.Next(Count); b1 = rng.Next(Count); } while (a1 == b1);
                do { a2 = rng.Next(Count); b2 = rng.Next(Count); } while (a2 == b2 || (a2 == a1 && b2 == b1) || (a2 == b1 && b2 == a1));
                p.order[0] = Ordered(p.answer, a1, b1);
                p.order[1] = Ordered(p.answer, a2, b2);

                if (p.CountSolutions() == 1 && p.NoRedundantRole()) return p;
            }
            throw new InvalidOperationException("유일해를 가진 퓨즈 퍼즐을 만들지 못했습니다");
        }

        private static int WrongColor(Random rng, int right)
        {
            int c;
            do { c = rng.Next(Count); } while (c == right);
            return c;
        }

        private static OrderClue Ordered(int[] answer, int colorA, int colorB)
        {
            int pa = Array.IndexOf(answer, colorA), pb = Array.IndexOf(answer, colorB);
            return pa < pb ? new OrderClue { left = colorA, right = colorB } : new OrderClue { left = colorB, right = colorA };
        }

        /// <summary>이 배치(perm[슬롯] = 색)가 모든 단서를 만족하는가.</summary>
        public bool Satisfies(int[] perm)
        {
            if (perm[wired.slot] != wired.color) return false;
            foreach (var b in banned) if (perm[b.slot] == b.color) return false;
            foreach (var o in order) if (Array.IndexOf(perm, o.left) >= Array.IndexOf(perm, o.right)) return false;
            return true;
        }

        public int CountSolutions()
        {
            int n = 0;
            foreach (var perm in AllPerms()) if (Satisfies(perm)) n++;
            return n;
        }

        /// <summary>어느 역할의 단서든 빼면 답이 둘 이상이 되어야 한다 = 모두가 꼭 필요하다.</summary>
        public bool NoRedundantRole()
        {
            return CountWithout(0) > 1 && CountWithout(1) > 1 && CountWithout(2) > 1;
        }

        // 0 통역 / 1 관측자 / 2 내비게이터 의 단서를 뺐을 때의 해 개수
        private int CountWithout(int role)
        {
            int n = 0;
            foreach (var perm in AllPerms())
            {
                bool ok = true;
                if (role != 2 && perm[wired.slot] != wired.color) ok = false;
                if (ok && role != 1) foreach (var b in banned) if (perm[b.slot] == b.color) ok = false;
                if (ok && role != 0) foreach (var o in order) if (Array.IndexOf(perm, o.left) >= Array.IndexOf(perm, o.right)) ok = false;
                if (ok) n++;
            }
            return n;
        }

        /// <summary>slots[슬롯] = 색(또는 Empty). 네 칸이 모두 차고 정답과 같으면 true.</summary>
        public bool Check(int[] slots)
        {
            if (slots == null || slots.Length != Count) return false;
            for (int s = 0; s < Count; s++) if (slots[s] != answer[s]) return false;
            return true;
        }

        public static bool IsFull(int[] slots)
        {
            foreach (int c in slots) if (c < 0 || c >= Count) return false;
            return true;
        }

        private static IEnumerable<int[]> AllPerms()
        {
            var p = new int[Count];
            var used = new bool[Count];
            return Rec(p, used, 0);
        }

        private static IEnumerable<int[]> Rec(int[] p, bool[] used, int k)
        {
            if (k == Count) { yield return (int[])p.Clone(); yield break; }
            for (int c = 0; c < Count; c++)
            {
                if (used[c]) continue;
                used[c] = true; p[k] = c;
                foreach (var r in Rec(p, used, k + 1)) yield return r;
                used[c] = false;
            }
        }

        // ---- 표시용 문구 -------------------------------------------------

        /// <summary>설명서 한 줄(필리핀어). 예: "Ang pula ay nasa kaliwa ng asul."</summary>
        public static string ManualTagalog(OrderClue o) => "Ang " + ColorTl[o.left] + " ay nasa kaliwa ng " + ColorTl[o.right] + ".";

        /// <summary>설명서의 뜻(통역만 읽을 수 있다). 예: "빨강은 파랑보다 왼쪽에 꽂는다."</summary>
        public static string ManualKorean(OrderClue o) => ColorKo[o.left] + "은 " + ColorKo[o.right] + "보다 왼쪽에 꽂는다.";
    }
}
