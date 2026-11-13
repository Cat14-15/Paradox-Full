using System.Collections.Generic;
using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    /// <summary>Stage1Logic 순수 규칙 진리표(네트워크 없이, QA test-T2 외).</summary>
    public class Stage1RuleTests
    {
        private static StageState Flooded(float value)
        {
            var st = new StageState { timerRunning = true, startClock = 0f };
            for (int z = 0; z < Stage1Logic.Zones; z++) { st.floodBase[z] = value; st.floodRate[z] = 0f; }
            return st;
        }

        [TestCase(-1, false)]
        [TestCase(StageMaps.RoomAtrium, false)] [TestCase(StageMaps.RoomDesk, false)] [TestCase(StageMaps.RoomOffice, false)] [TestCase(StageMaps.RoomMosaic, false)]
        [TestCase(StageMaps.RoomAquarium, true)] [TestCase(StageMaps.RoomGarden, true)]
        [TestCase(6, false)] [TestCase(7, false)]
        public void IsDanger_OnlyRooms4And5_AtOrAbove70(int room, bool expect)
        {
            var st = Flooded(100f);
            Assert.AreEqual(expect, Stage1Logic.IsDanger(st, room, 10f), $"방 {room} 침수 100");
            if (room < 0 || room >= Stage1Logic.Zones) return;
            st.floodBase[room] = Stage1Logic.DangerFlood;
            Assert.AreEqual(expect, Stage1Logic.IsDanger(st, room, 10f), "경계값 70 은 위험(≥)");
            st.floodBase[room] = Stage1Logic.DangerFlood - 0.1f;
            Assert.IsFalse(Stage1Logic.IsDanger(st, room, 10f), "69.9 는 안전");
            st.floodBase[room] = 30f; st.floodRate[room] = 1f;
            Assert.IsFalse(Stage1Logic.IsDanger(st, room, 10f), "30 + 1×10 = 40");
            Assert.AreEqual(expect, Stage1Logic.IsDanger(st, room, 40f), "30 + 1×40 = 70 도달");
            st.timerRunning = false;
            Assert.IsFalse(Stage1Logic.IsDanger(st, room, 40f), "타이머 전에는 base 고정(D1)");
        }

        [Test]
        public void Flood_ClampsAndFreezesBeforeTimer()
        {
            var st = Flooded(95f);
            st.floodRate[StageMaps.RoomAquarium] = 1f;
            Assert.AreEqual(100f, Stage1Logic.Flood(st, StageMaps.RoomAquarium, 30f), 0.001f, "100 에서 멈춘다");
            Assert.AreEqual(2, Stage1Logic.FloodLevel(70f)); Assert.AreEqual(1, Stage1Logic.FloodLevel(40f)); Assert.AreEqual(0, Stage1Logic.FloodLevel(39.9f));
            st.timerRunning = false;
            Assert.AreEqual(95f, Stage1Logic.Flood(st, StageMaps.RoomAquarium, 30f), 0.001f, "브리핑 게이트 전에는 기본값 고정");
            Assert.AreEqual(0f, Stage1Logic.Flood(null, 0, 0f), "null 상태는 0");
            Assert.AreEqual(0f, Stage1Logic.Flood(st, 99, 0f), "범위 밖 구역은 0");
        }

        [Test]
        public void FloodSnapshot_UsesBoardClock_ZeroBeforeOpen_BaseIfOpenedBeforeTimer()
        {
            // D-A4: 스냅샷 = Flood(st, zone, boardClock). 30 + 1×t 로 만든다
            var st = Flooded(30f);
            int z = StageMaps.RoomAquarium;
            st.floodRate[z] = 1f;
            Assert.AreEqual(0f, Stage1Logic.FloodSnapshot(st, z), "열기 전 0");
            Assert.AreEqual(-1f, Stage1Logic.SecondsSinceBoard(st, 50f), 0.001f, "열기 전 -1");
            Assert.IsFalse(Stage1Logic.IsDangerSnapshot(st, z), "열기 전 물결 없음");
            Assert.AreEqual(0f, Stage1Logic.FloodSnapshot(null, z), "null 상태 0");
            Assert.AreEqual(-1f, Stage1Logic.SecondsSinceBoard(null, 50f), 0.001f);

            st.boardOpened = true; st.boardClock = 20f;
            Assert.AreEqual(50f, Stage1Logic.FloodSnapshot(st, z), 0.001f, "boardClock 20 → 30 + 20");
            Assert.AreEqual(80f, Stage1Logic.Flood(st, z, 50f), 0.001f, "실시간 50초 → 80");
            Assert.IsTrue(Stage1Logic.IsDanger(st, z, 50f), "실시간은 위험");
            Assert.IsFalse(Stage1Logic.IsDangerSnapshot(st, z), "스냅샷(50)은 안전 — 가서 봐야 안다");
            Assert.AreEqual(30f, Stage1Logic.SecondsSinceBoard(st, 50f), 0.001f);
            Assert.AreEqual(0f, Stage1Logic.SecondsSinceBoard(st, 10f), 0.001f, "클록이 뒤로 가도 음수는 아니다");
            st.boardClock = 45f;
            Assert.IsTrue(Stage1Logic.IsDangerSnapshot(st, z), "boardClock 45 → 75 위험");
            Assert.IsFalse(Stage1Logic.IsDangerSnapshot(st, StageMaps.RoomAtrium), "위험 방(4·5)이 아니면 false");
            Assert.AreEqual(0f, Stage1Logic.FloodSnapshot(st, 99), "범위 밖 구역 0");

            // 브리핑 게이트 중에 열었다면(boardClock < startClock) 스냅샷은 기본값 — 타이머가 시작돼도 그 시각의 값
            st.startClock = 100f; st.boardClock = 60f;
            Assert.AreEqual(30f, Stage1Logic.FloodSnapshot(st, z), 0.001f, "타이머 전 스냅샷 = 기본값");
            st.timerRunning = false;
            Assert.AreEqual(30f, Stage1Logic.FloodSnapshot(st, z), 0.001f, "타이머가 아직 안 돌아도 기본값");
        }

        /// <summary>구역별 침수 70% 도달 순서(빠른 순). rate 0 이면 맨 뒤.</summary>
        private static int[] DangerOrder(StageState st)
        {
            var zones = new List<int>();
            for (int z = 0; z < Stage1Logic.Zones; z++) zones.Add(z);
            float T(int z) => st.floodRate[z] > 0f ? (Stage1Logic.DangerFlood - st.floodBase[z]) / st.floodRate[z] : float.MaxValue;
            zones.Sort((a, b) => T(a).CompareTo(T(b)));
            return zones.ToArray();
        }

        private static StageState Generated(int seed)
        {
            var g = new GameLogic();
            g.S.st.seed = seed;
            Stage1Logic.Start(g);
            return g.S.st;
        }

        [Test]
        public void Flood_Order_VariesBySeed_AtriumAlwaysSafest()
        {
            // B-05: 침수 진행 순서가 시드마다 다르다(방 4·5 스왑 50% + 관문 방 1·2·3 rate 순열). 시드 1·3 은 지름길 방 순서(5→4 / 4→5)와 관문 방 순서가 모두 다르다
            var a = DangerOrder(Generated(1));
            var b = DangerOrder(Generated(3));
            CollectionAssert.AreNotEqual(a, b, $"시드 1 [{string.Join(",", a)}] 과 시드 3 [{string.Join(",", b)}] 의 70% 도달 순서는 달라야 한다");
            Assert.AreEqual(Stage1Logic.Zones - 1, System.Array.IndexOf(a, StageMaps.RoomAtrium), "아트리움은 항상 마지막(가장 안전)");
            Assert.AreEqual(Stage1Logic.Zones - 1, System.Array.IndexOf(b, StageMaps.RoomAtrium));

            var seen = new HashSet<string>();
            int aquariumFirst = 0, gardenFirst = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var st = Generated(seed);
                var o = DangerOrder(st);
                seen.Add(string.Join(",", o));
                if (System.Array.IndexOf(o, StageMaps.RoomAquarium) < System.Array.IndexOf(o, StageMaps.RoomGarden)) aquariumFirst++; else gardenFirst++;
                // 아트리움 보정은 그대로: 다른 모든 구역보다 base 가 1 이상 낮고 rate 는 최소의 0.9 이하
                for (int z = 1; z < Stage1Logic.Zones; z++)
                {
                    Assert.LessOrEqual(st.floodBase[0], st.floodBase[z] - 1f + 0.001f, $"시드 {seed} 아트리움 base");
                    Assert.LessOrEqual(st.floodRate[0], st.floodRate[z] * 0.9f + 1e-5f, $"시드 {seed} 아트리움 rate");
                }
                Assert.AreEqual(-1, st.lastWrongDigit); Assert.IsFalse(st.pingNudged); Assert.AreEqual(0, st.timeWarned);
            }
            Assert.GreaterOrEqual(seen.Count, 5, "시드 60개면 서로 다른 순서가 여럿");
            Assert.Greater(aquariumFirst, 0, "수족관방이 먼저 위험해지는 시드가 있다");
            Assert.Greater(gardenFirst, 0, "연못정원방이 먼저 위험해지는 시드가 있다(스왑)");
        }

        [Test]
        public void Ladder_ManySeeds_AllValid_AndAnswerPathReachesSafe()
        {
            // 생성기 불변식: "4개 전부 정답 ⇔ 안전 단자 도착"이 모든 시드에서 성립하고, 정답 경로는 연속 라운드에 같은 단자에 머물지 않는다
            for (int seed = 1; seed <= 300; seed++)
            {
                var st = new StageState();
                Stage1Logic.GenerateGate2(st, seed);
                Assert.IsTrue(Stage1Logic.ValidateLadder(st), $"시드 {seed} 사다리 규칙 위반");
                Assert.AreEqual(0, st.round); Assert.AreEqual(-1, st.dirCard);
                for (int r = 0; r < Stage1Logic.Rounds; r++)
                {
                    Assert.AreEqual(Stage1Logic.Table2[Stage1Logic.Row(st.brailleDigit[r])][st.lampColor2[r]], st.answerDir[r], $"시드 {seed} 라운드 {r} 정답은 표에서 나온다");
                    Assert.AreNotEqual(Stage1Logic.AnswerPathAt(st, r), Stage1Logic.AnswerPathAt(st, r + 1), $"시드 {seed} 라운드 {r}: 정답 경로가 제자리에 머문다");
                }
                Assert.AreEqual(st.ladderSafe, Stage1Logic.AnswerPathAt(st, Stage1Logic.Rounds));
                Assert.AreEqual(-1, Stage1Logic.PathEnd(st), "레버를 당기기 전에는 경로 끝이 없다");
                Assert.AreEqual(st.ladderSafe + 1, Stage1Logic.SafeLine(st));
            }
        }

        [Test]
        public void Braille_DotsAndDigitsRoundTrip()
        {
            foreach (int d in Stage1Logic.BrailleDigits)
            {
                Assert.AreEqual(d, Stage1Logic.DigitOfDots(Stage1Logic.BrailleDots(d)), $"점자 {d}");
                Assert.GreaterOrEqual(Stage1Logic.Row(d), 0, $"점자 {d} 는 표의 행에 있어야 함");
                StringAssert.StartsWith("점자 ", Stage1Logic.DotsChat(d));
            }
            Assert.AreEqual(-1, Stage1Logic.DigitOfDots(new int[0]));
            Assert.AreEqual(-1, Stage1Logic.DigitOfDots(null));
            Assert.AreEqual(-1, Stage1Logic.Row(8), "8 은 관문 2 후보가 아니다");
            Assert.AreEqual(0, Stage1Logic.Parity(-4)); Assert.AreEqual(1, Stage1Logic.Parity(-3)); Assert.AreEqual(1, Stage1Logic.Parity(7));
        }

        [Test]
        public void Keypad_Gather_Helpers()
        {
            var st = new StageState { lockUntil1 = 10f };
            Assert.IsTrue(Stage1Logic.KeypadLocked(st, 9.9f)); Assert.IsFalse(Stage1Logic.KeypadLocked(st, 10f)); Assert.IsFalse(Stage1Logic.KeypadLocked(null, 0f));
            st.gatherStart = 100f;
            for (int s = 0; s < 4; s++)
            {
                var c = Stage1Logic.GatherCenter(s, st, 100f);
                Assert.IsTrue(Stage1Logic.InGather(s, c, st, 100f), $"원 중심은 원 안 {s}");
                Assert.IsFalse(Stage1Logic.InGather(s, c + new UnityEngine.Vector2(Stage1Logic.GatherRadius + 0.05f, 0f), st, 100f), $"반지름 밖은 원 밖 {s}");
                float r = (c - Paradox.Game.StageMaps.GatherOrigin).magnitude;
                Assert.IsTrue(r > Stage1Logic.GatherR - Stage1Logic.GatherWobble - 0.01f && r < Stage1Logic.GatherR + Stage1Logic.GatherWobble + 0.01f, $"원은 분수 주위 반지름 범위 안 {s}: {r}");
            }
            Assert.AreEqual("■□□", Stage1Logic.ProgressGlyphs(new StageState { step = 0 }));
            Assert.AreEqual("■■■", Stage1Logic.ProgressGlyphs(new StageState { step = 1, cleared = true }));
        }
    }
}
