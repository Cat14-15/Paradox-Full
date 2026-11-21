namespace Paradox.Game
{
    /// <summary>
    /// 난이도에 따른 Story1 규칙(유니티 비의존 — 서버·클라이언트·테스트가 같은 표를 쓴다).
    ///  · 쉬움: 퍼즐 1라운드, 힌트가 빨리 나온다 (초등 저학년·처음 해 보는 팀)
    ///  · 보통: 퍼즐 2라운드
    ///  · 어려움: 퍼즐 3라운드, 힌트가 늦게 나온다
    /// 마지막 퍼즐(id 4)은 이미 라운드 3개로 이뤄져 있어 늘리지 않는다.
    /// </summary>
    public static class Story1Rules
    {
        public const int Easy = 0, Normal = 1, Hard = 2, Count = 3;
        public const int FinalPuzzle = 4;
        public static readonly string[] Names = { "쉬움", "보통", "어려움" };
        public static readonly string[] Blurbs = { "단서에 숫자·화살표가 늘 붙어요 · 틀리면 몇 개 맞았는지 알려 줘요", "배운 낱말엔 신호가 안 붙어요(수첩 N) · 실수 +5초", "단서는 외국어만 · ? 로 다시 물어야 신호 · 실수 +15초" };

        public static int Clamp(int d) => d < 0 ? 0 : d >= Count ? Count - 1 : d;

        /// <summary>퍼즐 id 를 끝내려면 몇 번 풀어야 하는가(라운드 수).</summary>
        public static int Need(int difficulty, int puzzleId) => puzzleId == FinalPuzzle ? 1 : Clamp(difficulty) + 1;

        /// <summary>몇 번 틀리면 힌트를 보여 주는가.</summary>
        public static int HintAfter(int difficulty)
        {
            switch (Clamp(difficulty)) { case Easy: return 1; case Normal: return 3; default: return 6; }
        }

        public const int RoundBits = 3, RoundMask = 7;
        public static int RoundsOf(int packed, int puzzleId) => (packed >> (RoundBits * puzzleId)) & RoundMask;
        public static int WithRounds(int packed, int puzzleId, int n) => (packed & ~(RoundMask << (RoundBits * puzzleId))) | ((n & RoundMask) << (RoundBits * puzzleId));
    }
}
