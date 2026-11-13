using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    /// <summary>퍼즐 1(퓨즈 복구): 어떤 시드든 정답이 하나뿐이고, 어느 역할의 단서를 빼도 답이 하나로 안 정해져야 한다(= 협동 필수).</summary>
    public class FusePuzzleTests
    {
        [Test]
        public void EverySeed_HasExactlyOneSolution_AndAnswerSatisfiesClues()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var p = FusePuzzle.Generate(seed);
                Assert.AreEqual(1, p.CountSolutions(), "seed " + seed);
                Assert.IsTrue(p.Satisfies(p.answer), "정답이 자기 단서를 만족해야 함 seed " + seed);
                Assert.IsTrue(p.Check(p.answer));
            }
        }

        [Test]
        public void ManualText_MatchesAnswerOrder()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var p = FusePuzzle.Generate(seed);
                foreach (var o in p.order)
                {
                    Assert.Less(System.Array.IndexOf(p.answer, o.left), System.Array.IndexOf(p.answer, o.right), "seed " + seed);
                    StringAssert.Contains(FusePuzzle.ColorTl[o.left], FusePuzzle.ManualTagalog(o));
                    StringAssert.StartsWith(FusePuzzle.ColorKo[o.left], FusePuzzle.ManualKorean(o));
                }
            }
        }

        [Test]
        public void SameSeed_GivesSamePuzzle_OnEveryComputer()
        {
            var a = FusePuzzle.Generate(4242); var b = FusePuzzle.Generate(4242);
            CollectionAssert.AreEqual(a.answer, b.answer);
            Assert.AreEqual(a.wired.slot, b.wired.slot);
        }

        [Test]
        public void EveryRole_IsNeeded()
        {
            for (int seed = 0; seed < 300; seed++)
                Assert.IsTrue(FusePuzzle.Generate(seed).NoRedundantRole(), "seed " + seed);
        }

        [Test]
        public void Check_RejectsWrongAndIncomplete()
        {
            var p = FusePuzzle.Generate(7);
            var wrong = (int[])p.answer.Clone();
            int t = wrong[0]; wrong[0] = wrong[1]; wrong[1] = t;
            Assert.IsFalse(p.Check(wrong));
            var partial = (int[])p.answer.Clone();
            partial[2] = FusePuzzle.Empty;
            Assert.IsFalse(p.Check(partial));
            Assert.IsFalse(FusePuzzle.IsFull(partial));
            Assert.IsTrue(FusePuzzle.IsFull(p.answer));
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            var a = FusePuzzle.Generate(123);
            var b = FusePuzzle.Generate(123);
            CollectionAssert.AreEqual(a.answer, b.answer);
            Assert.AreEqual(a.wired.slot, b.wired.slot);
        }

        [Test]
        public void ManualText_UsesColorNames()
        {
            var o = new FusePuzzle.OrderClue { left = 0, right = 1 };
            Assert.AreEqual("Ang pula ay nasa kaliwa ng asul.", FusePuzzle.ManualTagalog(o));
            StringAssert.Contains("빨강", FusePuzzle.ManualKorean(o));
        }
    }
}
