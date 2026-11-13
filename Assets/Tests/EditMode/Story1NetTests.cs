using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;

namespace Paradox.Tests
{
    public class Story1NetTests
    {
        private static void Online(long hostId, float startClock)
        {
            Session.Connected = true; Session.MySlot = 0; Session.MyClientId = hostId;
            var S = Session.State;
            S.hostSlot = 0; S.gameStartClock = startClock;
            for (int i = 0; i < 4; i++) { S.players[i].present = i < 2; S.players[i].isBot = false; S.players[i].clientId = hostId + i; }
        }

        [TearDown]
        public void Cleanup()
        {
            Session.Connected = false; Session.MySlot = -1; Session.MyClientId = -1;
            for (int i = 0; i < 4; i++) { var p = Session.State.players[i]; p.present = false; p.isBot = false; p.clientId = -1; }
            Session.State.gameStartClock = 0f;
        }

        [Test]
        public void SeedIsSameForEveryClientAndDiffersPerPuzzle()
        {
            Online(11, 5f);
            int a = Story1Net.Seed(Story1Net.PFuse), b = Story1Net.Seed(Story1Net.PFuse);
            Assert.AreEqual(a, b);
            Session.MySlot = 1;   // 다른 PC(다른 슬롯)에서 계산해도 같다
            Assert.AreEqual(a, Story1Net.Seed(Story1Net.PFuse));
            var set = new System.Collections.Generic.HashSet<int>();
            for (int id = 0; id < 5; id++) set.Add(Story1Net.Seed(id));
            Assert.AreEqual(5, set.Count);
            Assert.GreaterOrEqual(a, 1);
        }

        [Test]
        public void SamePuzzleOnEveryClient()
        {
            Online(11, 5f);
            var p1 = WaterPuzzle.Generate(Story1Net.Seed(Story1Net.PWater));
            Session.MySlot = 1;
            var p2 = WaterPuzzle.Generate(Story1Net.Seed(Story1Net.PWater));
            CollectionAssert.AreEqual(p1.link, p2.link);
            CollectionAssert.AreEqual(p1.target, p2.target);
        }

        [Test]
        public void RemoteHumanAndSayAndSignalEvents()
        {
            Online(11, 5f);
            Assert.IsTrue(Story1Net.HasHumanPeers);
            Assert.IsTrue(Story1Net.IsRemoteHuman(1));
            Assert.IsFalse(Story1Net.IsRemoteHuman(0));
            int v = Story1Net.SayVersion;
            Session.Dispatch(new GameEvent { type = "S1", slot = 0, a = Story1Net.Say, b = Story1Net.PGallery, s = "테스트" });
            Assert.AreEqual("테스트", Story1Net.SayText[Story1Net.PGallery]);
            Assert.Greater(Story1Net.SayVersion, v);
            int f = Story1Net.FeedVersion;
            Session.Dispatch(new GameEvent { type = "S1", slot = 1, a = Story1Net.Signal, b = 3 });
            Assert.Greater(Story1Net.FeedVersion, f);
            Assert.IsTrue(Story1Net.IsMine(new GameEvent { slot = 0 }));
            Assert.IsFalse(Story1Net.IsMine(new GameEvent { slot = 1 }));
        }
    }
}
