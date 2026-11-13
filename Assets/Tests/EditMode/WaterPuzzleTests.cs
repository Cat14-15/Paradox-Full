using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    public class WaterPuzzleTests
    {
        [Test]
        public void SolutionReachesTarget_AndIsUnique_AndLinkageMatters()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var p = WaterPuzzle.Generate(seed);
                var st = new bool[4];
                for (int i = 0; i < 4; i++) if (p.solution[i]) p.Toggle(st, i);
                Assert.IsTrue(p.Check(st), "seed " + seed);

                var naive = new bool[4];
                for (int i = 0; i < 4; i++) if (p.target[i]) p.Toggle(naive, i);
                Assert.IsFalse(p.Check(naive), "naive press of target must fail, seed " + seed);

                bool same = true; for (int i = 0; i < 4; i++) if (p.solution[i] != p.target[i]) same = false;
                Assert.IsFalse(same, "naive press must fail, seed " + seed);

                Assert.GreaterOrEqual(p.TargetNumbers().Length, 1);
                var seen = new bool[4]; foreach (int t in p.tagOf) seen[t] = true;
                Assert.IsTrue(seen[0] && seen[1] && seen[2] && seen[3]);
            }
        }

        [Test]
        public void Deterministic()
        {
            var a = WaterPuzzle.Generate(3); var b = WaterPuzzle.Generate(3);
            CollectionAssert.AreEqual(a.link, b.link);
            CollectionAssert.AreEqual(a.target, b.target);
        }
    }
}
