using System.Collections.Generic;
using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    public class BotLinesTests
    {
        private static void AllFilled(string[] l)
        {
            Assert.AreEqual(3, l.Length);
            foreach (var s in l) Assert.IsFalse(string.IsNullOrEmpty(s));
        }

        [Test]
        public void EveryPuzzleHasThreeLines()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                AllFilled(BotLines.Fuse(FusePuzzle.Generate(seed)));
                AllFilled(BotLines.Gallery(GalleryPuzzle.Generate(seed), seed % 3, seed % 2 == 0));
                AllFilled(BotLines.Water(WaterPuzzle.Generate(seed)));
                AllFilled(BotLines.Relic(RelicPuzzle.Generate(seed)));
                AllFilled(BotLines.FinalRound(FinalPuzzle.Generate(seed), seed % FinalPuzzle.Rounds));
            }
            AllFilled(BotLines.FinalGo());
        }

        [Test]
        public void GalleryBotsDoNotNameTheAnswerDirectly()
        {
            // 통역은 막힌 통로만, 내비는 이어진 통로만 말한다 — 둘을 합쳐야 답이 나온다
            var p = GalleryPuzzle.Generate(7);
            var l = BotLines.Gallery(p, 0, false);
            StringAssert.Contains(BotLines.LaneArrow[p.decoy[0]], l[0]);
            StringAssert.DoesNotContain(BotLines.LaneArrow[p.answer[0]], l[0]);
            StringAssert.Contains(BotLines.LaneArrow[p.answer[0]], l[1]);
            StringAssert.Contains(BotLines.LaneArrow[p.decoy[0]], l[1]);
        }

        [Test]
        public void BotsOnlyUseCardVocabulary()
        {
            // 봇은 문장을 만들지 않는다: 신호 카드에 있는 글자(숫자·화살표·색 단어·응/아니/여기)와 공백뿐
            const string allowed = "01234567890 ←↑→불물별풀응아니여기";
            for (int seed = 0; seed < 30; seed++)
            {
                var all = new List<string>();
                all.AddRange(BotLines.Fuse(FusePuzzle.Generate(seed)));
                all.AddRange(BotLines.Gallery(GalleryPuzzle.Generate(seed), seed % 3, seed % 2 == 0));
                all.AddRange(BotLines.Water(WaterPuzzle.Generate(seed)));
                all.AddRange(BotLines.Relic(RelicPuzzle.Generate(seed)));
                all.AddRange(BotLines.FinalRound(FinalPuzzle.Generate(seed), seed % FinalPuzzle.Rounds));
                all.AddRange(BotLines.FinalGo());
                foreach (var line in all) foreach (char c in line) Assert.GreaterOrEqual(allowed.IndexOf(c), 0, "카드에 없는 글자 '" + c + "' in " + line);
            }
        }

        [Test]
        public void FinalRoundRanksMatchPuzzle()
        {
            var p = FinalPuzzle.Generate(3);
            var l = BotLines.FinalRound(p, 1);
            for (int i = 0; i < 3; i++) Assert.AreEqual((p.RankOf(1, BotLines.RoleOrder[i]) + 1).ToString(), l[i]);
        }
    }
}
