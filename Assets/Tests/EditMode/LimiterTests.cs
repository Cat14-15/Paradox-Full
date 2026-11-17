using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;
using Paradox.Net;

namespace Paradox.Tests
{
    public class LimiterTests
    {
        [SetUp] public void Up() { Limiter.Clear(); }

        [Test]
        public void SignalSpam_IsCut_AfterBurst()
        {
            var c = Cmd.Make("S1", Story1Net.Signal, 1);
            int ok = 0;
            for (int i = 0; i < 10; i++) if (Limiter.Allow(1, c, 10f)) ok++;
            Assert.AreEqual(4, ok, "순간 4개까지만");
            Assert.IsTrue(Limiter.Allow(1, c, 11.1f), "1초 뒤 1개 더");
        }

        [Test]
        public void HeavyFlood_MutesSender_OthersUnaffected()
        {
            var c = Cmd.Make("Ready", 1);
            for (int i = 0; i < 300; i++) Limiter.Allow(1, c, 10f);
            Assert.IsFalse(Limiter.Allow(1, c, 14f), "8초 무시 중");
            Assert.IsTrue(Limiter.Allow(2, c, 14f), "다른 사람은 정상");
            Assert.IsTrue(Limiter.Allow(1, c, 25f), "시간이 지나면 풀림");
        }

        [Test]
        public void NormalUse_NeverBlocked()
        {
            var c = Cmd.Make("Ready", 1);
            for (int i = 0; i < 200; i++) Assert.IsTrue(Limiter.Allow(1, c, 10f + i * 0.1f));
        }
    }
}
