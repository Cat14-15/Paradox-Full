using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    public class Story1RulesTests
    {
        [Test]
        public void RoundsScaleWithDifficulty_FinalPuzzleStaysOne()
        {
            Assert.AreEqual(1, Story1Rules.Need(Story1Rules.Easy, 0));
            Assert.AreEqual(2, Story1Rules.Need(Story1Rules.Normal, 1));
            Assert.AreEqual(3, Story1Rules.Need(Story1Rules.Hard, 3));
            for (int d = 0; d < Story1Rules.Count; d++) Assert.AreEqual(1, Story1Rules.Need(d, Story1Rules.FinalPuzzle));
            Assert.AreEqual(3, Story1Rules.Need(99, 0));   // 범위 밖은 어려움으로
        }

        [Test]
        public void HintsComeEarlierOnEasy()
        {
            Assert.Less(Story1Rules.HintAfter(Story1Rules.Easy), Story1Rules.HintAfter(Story1Rules.Normal));
            Assert.Less(Story1Rules.HintAfter(Story1Rules.Normal), Story1Rules.HintAfter(Story1Rules.Hard));
        }

        [Test]
        public void PackedRoundsAreIndependentPerPuzzle()
        {
            int p = 0;
            p = Story1Rules.WithRounds(p, 1, 2);
            p = Story1Rules.WithRounds(p, 3, 3);
            Assert.AreEqual(0, Story1Rules.RoundsOf(p, 0));
            Assert.AreEqual(2, Story1Rules.RoundsOf(p, 1));
            Assert.AreEqual(0, Story1Rules.RoundsOf(p, 2));
            Assert.AreEqual(3, Story1Rules.RoundsOf(p, 3));
            p = Story1Rules.WithRounds(p, 1, 1);
            Assert.AreEqual(1, Story1Rules.RoundsOf(p, 1));
            Assert.AreEqual(3, Story1Rules.RoundsOf(p, 3));
        }
    }
}
