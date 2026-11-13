using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    public class FinalPuzzleTests
    {
        [Test]
        public void EveryRoundIsAPermutation_AndRoundsDiffer_AndEachRoleNeeded()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var p = FinalPuzzle.Generate(seed);
                for (int r = 0; r < FinalPuzzle.Rounds; r++)
                {
                    var seen = new bool[4];
                    foreach (int role in p.order[r]) seen[role] = true;
                    Assert.IsTrue(seen[0] && seen[1] && seen[2] && seen[3]);
                    for (int role = 0; role < 4; role++) Assert.AreEqual(role, p.order[r][p.RankOf(r, role)]);
                    if (r > 0) CollectionAssert.AreNotEqual(p.order[r - 1], p.order[r]);
                    Assert.IsTrue(p.CheckOrder(r, p.order[r]));
                    // 한 역할의 순서만 틀려도 오답
                    var bad = (int[])p.order[r].Clone(); var t = bad[0]; bad[0] = bad[1]; bad[1] = t;
                    Assert.IsFalse(p.CheckOrder(r, bad));
                }
            }
        }

        [Test]
        public void EachRoleSeesOnlyItsOwnTick()
        {
            var p = FinalPuzzle.Generate(11);
            for (int r = 0; r < FinalPuzzle.Rounds; r++)
            {
                int litCount = 0;
                for (int role = 0; role < 4; role++)
                    for (int k = 0; k < FinalPuzzle.CycleTicks; k++)
                        if (p.Lit(r, role, (k + 0.5f) * FinalPuzzle.TickSeconds[r])) litCount++;
                Assert.AreEqual(4, litCount);   // 한 주기에 네 원이 각자 한 번씩
                // 쉼 박에는 아무도 안 켜진다
                for (int role = 0; role < 4; role++) Assert.IsFalse(p.Lit(r, role, 4.5f * FinalPuzzle.TickSeconds[r]));
            }
        }

        [Test]
        public void SimultaneousNeedsAllFourWithinWindow()
        {
            float go = 3.2f;
            Assert.IsTrue(FinalPuzzle.Simultaneous(new[] { 3.2f, 3.3f, 3.1f, 3.4f }, go));
            Assert.IsFalse(FinalPuzzle.Simultaneous(new[] { 3.2f, 3.3f, 3.1f, -1f }, go));
            Assert.IsFalse(FinalPuzzle.Simultaneous(new[] { 3.2f, 3.3f, 3.1f, 3.9f }, go));
            Assert.IsFalse(FinalPuzzle.Simultaneous(new[] { 2.5f, 3.3f, 3.1f, 3.2f }, go));
        }
    }
}
