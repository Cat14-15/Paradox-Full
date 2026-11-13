using NUnit.Framework;
using Paradox.Game;

namespace Paradox.Tests
{
    public class GalleryPuzzleTests
    {
        [Test]
        public void EveryStageHasExactlyOneOpenLane_AndBothCluesAreNeeded()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var p = GalleryPuzzle.Generate(seed);
                for (int s = 0; s < GalleryPuzzle.Stages; s++)
                {
                    Assert.AreEqual(1, p.OpenLaneCount(s));
                    Assert.AreNotEqual(p.answer[s], p.decoy[s]);
                    int conn = 0, open = 0;
                    for (int l = 0; l < GalleryPuzzle.Lanes; l++) { if (p.Connected(s, l)) conn++; if (!p.Closed(s, l)) open++; }
                    Assert.AreEqual(2, conn);   // 안내도만으로는 둘 중 하나
                    Assert.AreEqual(2, open);   // 안내문만으로도 둘 중 하나
                }
            }
        }

        [Test]
        public void SparkHasSafeWindow()
        {
            var p = GalleryPuzzle.Generate(7);
            int off = 0, on = 0;
            for (float t = 0f; t < GalleryPuzzle.Period; t += 0.05f) { if (p.SparkOn(0, t)) on++; else off++; }
            Assert.Greater(off, 0); Assert.Greater(on, 0);
        }

        [Test]
        public void Deterministic()
        {
            var a = GalleryPuzzle.Generate(5); var b = GalleryPuzzle.Generate(5);
            CollectionAssert.AreEqual(a.answer, b.answer);
            CollectionAssert.AreEqual(a.decoy, b.decoy);
        }
    }
}
