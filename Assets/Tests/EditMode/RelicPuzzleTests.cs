using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    public class RelicPuzzleTests
    {
        [Test]
        public void SolutionUnique_FakeIsAmbiguousByLabel_AndPedestalOrderNeeded()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var p = RelicPuzzle.Generate(seed);
                var ans = new int[4];
                for (int i = 0; i < 4; i++) { ans[i] = p.RealSlotFor(i); Assert.GreaterOrEqual(ans[i], 0); }
                Assert.IsTrue(p.Check(ans), "seed " + seed);

                // 가짜는 진짜 하나와 이름표가 같다 → 이름표만으로는 가려낼 수 없다
                int dup = 0; for (int s = 0; s < 5; s++) if (p.eraOf[s] == p.eraOf[p.fake]) dup++;
                Assert.AreEqual(2, dup);
                // 가짜를 끼우면 오답
                var bad = (int[])ans.Clone();
                for (int i = 0; i < 4; i++) if (p.eraOf[p.fake] == p.pedestalEra[i]) bad[i] = p.fake;
                Assert.IsFalse(p.Check(bad));
                // 왼→오 시대순으로 그냥 놓으면 풀리지 않는다(배치도가 필요)
                var sorted = new int[4];
                for (int e = 0; e < 4; e++) for (int s = 0; s < 5; s++) if (s != p.fake && p.eraOf[s] == e) sorted[e] = s;
                Assert.IsFalse(p.Check(sorted));
                // 완전 해는 하나
                int hits = 0;
                for (int a = 0; a < 5; a++) for (int b = 0; b < 5; b++) for (int c = 0; c < 5; c++) for (int d = 0; d < 5; d++)
                {
                    if (a == b || a == c || a == d || b == c || b == d || c == d) continue;
                    if (p.Check(new[] { a, b, c, d })) hits++;
                }
                Assert.AreEqual(1, hits, "seed " + seed);
            }
        }

        [Test]
        public void CheckRejectsEmpty()
        {
            var p = RelicPuzzle.Generate(1);
            Assert.IsFalse(p.Check(new[] { -1, -1, -1, -1 }));
            Assert.IsFalse(p.Check(null));
        }
    }
}
