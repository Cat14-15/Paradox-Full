using System.Collections;
using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;
using Paradox.Net;
using UnityEngine.TestTools;

namespace Paradox.Tests
{
    /// <summary>
    /// 혼자 테스트(호스트 + 봇 3명)로 유저 플로우 전체를 헤드리스로 돌린다:
    /// 타이틀 → 로비 → 캐릭터 선택 → 프롤로그 → Stage 1~4 (각 퍼즐 규칙 실행) → 엔딩 → 결과.
    /// </summary>
    public class FullFlowTests
    {
        [UnityTearDown]
        public IEnumerator Down() => Flow.Teardown();

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator SoloFlow_RunsThroughAllStages()
        {
            yield return Flow.Boot();
            yield return Flow.StartSolo();
            yield return Flow.LobbyToPrologue(4, "테스터", null);

            Session.Send("Next");
            yield return null;
            Assert.AreEqual(1, Session.State.cutsceneIndex);
            yield return Flow.SkipCutscene(Phase.StageIntro, 1, "Stage 1 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 1, "Stage 1 플레이");

            yield return Flow.Stage1(null);
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 1 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 2, "Stage 2 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 2, "Stage 2 플레이");

            yield return Flow.Stage2(null);
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 2 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 3, "Stage 3 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 3, "Stage 3 플레이");

            yield return Flow.Stage3(null);
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 3 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 4, "Stage 4 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 4, "Stage 4 플레이");

            yield return Flow.Stage4(null);
            yield return Flow.Until(() => Session.State.phase == Phase.Ending, 12f, "엔딩");
            yield return Flow.SkipCutscene(Phase.Result, -1, "결과 화면");
            Assert.AreEqual(Phase.Result, Session.State.phase);

            Session.Send("Again");
            yield return Flow.Until(() => Session.State.phase == Phase.Lobby, 5f, "로비 복귀");

            NetHub.Instance.Shutdown();
            yield return null;
            Assert.IsFalse(Session.Connected);
        }
    }
}
