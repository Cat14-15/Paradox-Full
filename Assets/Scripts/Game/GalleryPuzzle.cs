using System;

namespace Paradox.Game
{
    /// <summary>
    /// 퍼즐 2 — 어두운 전시실, 젖은 바닥 건너기. 3개 구간, 구간마다 통로 3개(왼·가운데·오른).
    /// 조작자는 손전등 반경밖엔 못 본다. 길과 타이밍은 동료의 신호로만 안다.
    ///  · 내비게이터: 전시실 안내도 — 구간마다 "이어진 통로" 2개 (하나는 막다른 길)
    ///  · 통역: 벽의 필리핀어 안내문 — 구간마다 "막힌 통로" 1개 (진열장이 쓰러짐)
    ///    → 두 정보의 교집합이 올바른 통로 하나.
    ///  · 관측자: 끊어진 전선의 스파크 리듬 — 불꽃이 꺼져 있을 때만 건널 수 있다.
    /// 유니티 비의존 규칙이라 서버·테스트·솔로가 그대로 쓴다.
    /// </summary>
    public sealed class GalleryPuzzle
    {
        public const int Stages = 3, Lanes = 3;
        public static readonly string[] LaneKo = { "왼쪽", "가운데", "오른쪽" };
        public static readonly string[] LaneTl = { "kaliwang", "gitnang", "kanang" };

        public int[] answer = new int[Stages];   // 올바른 통로
        public int[] decoy = new int[Stages];    // 안내도에는 이어져 있지만 실제로는 막힌 통로
        public float[] offset = new float[Stages];
        public const float Period = 4.0f, SafeTime = 2.0f;   // 한 주기 4초 중 앞 2초는 불꽃이 꺼져 있다

        public static GalleryPuzzle Generate(int seed)
        {
            var rng = new Random(seed);
            var p = new GalleryPuzzle();
            for (int s = 0; s < Stages; s++)
            {
                p.answer[s] = rng.Next(Lanes);
                int d;
                do { d = rng.Next(Lanes); } while (d == p.answer[s]);
                p.decoy[s] = d;
                p.offset[s] = (float)(rng.NextDouble() * Period);
            }
            return p;
        }

        /// <summary>구간 stage 의 스파크가 시각 t 에 켜져 있는가.</summary>
        public bool SparkOn(int stage, float t)
        {
            float x = (t + offset[stage]) % Period;
            if (x < 0f) x += Period;
            return x >= SafeTime;
        }

        /// <summary>안내도: 이 통로가 이어져 있는가(올바른 길 + 막다른 길).</summary>
        public bool Connected(int stage, int lane) => lane == answer[stage] || lane == decoy[stage];

        /// <summary>안내문: 이 통로가 막혔는가.</summary>
        public bool Closed(int stage, int lane) => lane == decoy[stage];

        /// <summary>두 정보를 합쳐 건널 수 있는 통로 개수. 항상 1.</summary>
        public int OpenLaneCount(int stage)
        {
            int n = 0;
            for (int l = 0; l < Lanes; l++) if (Connected(stage, l) && !Closed(stage, l)) n++;
            return n;
        }

        public string SignTagalog(int stage) => "Sarado ang " + LaneTl[decoy[stage]] + " daan.";
        public string SignKorean(int stage) => LaneKo[decoy[stage]] + " 통로는 막혔다.";
    }
}
