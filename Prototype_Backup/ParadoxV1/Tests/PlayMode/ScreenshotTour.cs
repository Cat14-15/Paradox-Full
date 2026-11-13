using System.Collections;
using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;
using Paradox.Net;
using UnityEngine.TestTools;

namespace Paradox.Tests
{
    /// <summary>
    /// 화면 검수용 스크린샷 투어. 환경변수 PARADOX_SHOTS 가 설정된 경우에만 실행되어
    /// 흐름의 각 단계·역할별 화면을 PNG로 저장한다. (Unity -batchmode 에서 -nographics 없이 실행)
    /// </summary>
    public class ScreenshotTour
    {
        [UnityTearDown]
        public IEnumerator Down() => Flow.Teardown();

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CaptureAllScreens()
        {
            if (!Shots.Enabled) Assert.Ignore("PARADOX_SHOTS 환경변수가 없어 건너뜀");
            // 화면 검수가 목적이므로 렌더 파이프라인 등 게임 외부의 에러 로그로 실패하지 않게 한다
            LogAssert.ignoreFailingMessages = true;
            System.Func<string, IEnumerator> shot = Shots.Capture;

            yield return Flow.Boot();
            yield return shot("01_title");
            yield return Flow.StartSolo();
            yield return shot("02_lobby");
            yield return Flow.LobbyToPrologue(4, "테스터", shot);

            yield return shot("04_prologue_0");
            Session.Send("Next"); yield return null;
            yield return shot("04_prologue_1");
            for (int i = 0; i < 5; i++) { Session.Send("Next"); yield return null; }
            yield return shot("04_prologue_7");
            yield return Flow.SkipCutscene(Phase.StageIntro, 1, "Stage 1 도입");
            yield return shot("05_s1_intro_0");
            for (int i = 0; i < 3; i++) { Session.Send("Next"); yield return null; }
            // 언어 장벽 대사(스태프): 조작자 시점과 통역 시점
            yield return shot("05_s1_intro_staff_op");
            Flow.As(1);
            yield return shot("05_s1_intro_staff_interpreter");
            Flow.As(0);
            yield return Flow.SkipCutscene(Phase.StagePlay, 1, "Stage 1 플레이");

            yield return Flow.Stage1(shot);
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 1 클리어 컷씬");
            yield return shot("20_s1_clear_cutscene");
            yield return Flow.SkipCutscene(Phase.StageIntro, 2, "Stage 2 도입");
            yield return shot("20_s2_intro");
            yield return Flow.SkipCutscene(Phase.StagePlay, 2, "Stage 2 플레이");

            yield return Flow.Stage2(shot);
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 2 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 3, "Stage 3 도입");
            yield return shot("30_s3_intro");
            yield return Flow.SkipCutscene(Phase.StagePlay, 3, "Stage 3 플레이");

            yield return Flow.Stage3(shot);
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 3 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 4, "Stage 4 도입");
            yield return shot("40_s4_intro");
            yield return Flow.SkipCutscene(Phase.StagePlay, 4, "Stage 4 플레이");

            yield return Flow.Stage4(shot);
            yield return Flow.Until(() => Session.State.phase == Phase.Ending, 12f, "엔딩");
            yield return shot("50_ending");
            yield return Flow.SkipCutscene(Phase.Result, -1, "결과 화면");
            yield return shot("51_result");

            NetHub.Instance.Shutdown();
            yield return null;
            yield return shot("52_title_after");
        }
    }
}
