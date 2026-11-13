using System.Collections;
using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.TestTools;

namespace Paradox.Tests
{
    /// <summary>
    /// 화면 검수용 스크린샷 투어. 환경변수 PARADOX_SHOTS 가 설정된 경우에만 실행되어
    /// 흐름의 각 단계·역할별 화면을 PNG로 저장한다. (Unity -batchmode 에서 -nographics 없이 실행)
    /// 파일 이름 번호 순서 = 유저 플로우 순서.
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

            // ---------------- 로컬 화면(접속 전)
            yield return Flow.ToLocal(LocalScreen.Logo, "LogoScreen");
            yield return WaitReal(0.8f);                     // 로고 연출이 조금 진행된 뒤
            yield return shot("00_logo");
            yield return Flow.ToLocal(LocalScreen.Title, "TitleScreen");
            yield return shot("01_title");
            yield return Flow.ToLocal(LocalScreen.Prologue, "PrologueScreen");
            yield return WaitReal(1.6f);                     // 타자 효과로 첫 대사가 어느 정도 찍힌 뒤
            yield return shot("02_prologue_1");
            // 02_prologue_scene2 / scene3: PrologueScreen 이 줄 이동 훅을 노출하지 않아 첫 장면만 찍는다(통합 요청 참고)
            yield return Flow.ToLocal(LocalScreen.StageSelect, "StageSelectScreen");
            yield return shot("03_stageselect");
            yield return Flow.ToLocal(LocalScreen.Match, "MatchScreen");
            yield return shot("04_match");

            // ---------------- 로비 → 이름 → 준비 → 오프닝
            yield return Flow.StartSolo(botsAuto: false);
            yield return shot("05_lobby");

            Session.Send("StartGame");
            yield return Flow.Until(() => Session.State.phase == Phase.NameSetup, 5f, "이름 설정 화면");
            yield return shot("06_names");
            Session.Send("Name", 0, 0, "테스터");
            yield return Flow.Until(() => Session.Me != null && Session.Me.name == "테스터", 3f, "내 이름 반영");
            Session.Send("Ready", 1);
            yield return Flow.Until(() => Session.State.phase == Phase.Ready, 5f, "준비 완료 화면");
            yield return shot("07_ready");
            yield return Flow.Until(() => Session.State.phase == Phase.StageIntro, 8f, "오프닝 컷씬");
            yield return WaitReal(1.2f);
            yield return shot("08_opening_kor");

            // 스태프 대사(언어 장벽 줄)까지 진행해 통역 시점으로 한 장
            int barrier = Flow.BarrierLineIndex("s1_intro", 1);
            if (barrier >= 0) yield return Flow.NextUntilLine(barrier);
            yield return WaitReal(0.8f);
            Flow.As(1);
            yield return WaitReal(0.4f);
            yield return shot("08_opening_phl");
            Flow.As(0);
            yield return null;

            // 씬 3 정전(QA D-A2)·씬 4 초상+벽보(QA D-A3): StoryData 에서 줄을 찾아 진행한다(줄 번호 하드코딩 없음)
            int blackout = StoryData.FindLine("s1_intro", l => l.blackout);
            if (blackout >= 0)
            {
                yield return Flow.NextUntilLine(blackout);
                yield return Flow.Until(() => CutsceneScreen.Instance != null && !CutsceneScreen.Instance.BlackoutActive, VnView.BlackoutHold + 3f, "정전 암전 종료");
                yield return WaitReal(1.4f);                 // 걷힘 + 타자
                yield return shot("08_opening_3_blackout");
            }
            int posterLine = StoryData.FindLine("s1_intro", l => l.portraitRight >= 0 && l.portrait >= 0);
            if (posterLine >= 0)
            {
                yield return Flow.NextUntilLine(posterLine);
                yield return WaitReal(1.2f);
                yield return shot("08_opening_4_poster");
            }

            // ---------------- 스테이지 진입(튜토리얼은 자동 건너뜀)
            yield return Flow.SkipCutscene(Phase.StagePlay, 1, "Stage 1 플레이");
            yield return Flow.Until(() => Session.Avatars.Count == 4, 10f, "아바타 4개 스폰");
            yield return Flow.Until(() => Session.Map != null, 5f, "스테이지 맵 생성");
            yield return WaitReal(0.6f);
            yield return Flow.RoleViews(shot, "10_stage");

            // 도넛 / 수신 로그(로그에 내용이 있도록 채팅·휠 신호를 먼저 보낸다)
            Flow.As(0);
            Session.Send("Chat", 0, 0, "경고등 무슨 색이야?");
            Flow.As(3);
            Session.Send("Signal", 0, 0, "DANGER");
            Flow.As(2);
            Session.Send("Signal", 0, 0, "GO");
            Flow.As(1);
            Session.Send("Chat", 0, 0, "모두 안내데스크로 가자");
            yield return Flow.Until(() => Session.State.stats.chats >= 2 && Session.State.stats.signals >= 2, 4f, "채팅 2 + 신호 2");
            Flow.As(0);
            yield return WaitReal(0.5f);
            Assert.IsNotNull(StageScreen.Instance, "StageScreen 없음");
            StageScreen.Instance.OpenDonut();          // 조작자(D4): 도넛 대신 안내 토스트만 찍힌다
            yield return WaitReal(0.3f);
            yield return shot("11_donut_op_none");
            Flow.CloseOverlays();
            yield return null;
            Flow.As(3);                                 // 관측자 시점의 도넛
            yield return null;
            StageScreen.Instance.OpenDonut();
            yield return Flow.Until(() => StageScreen.Instance != null && StageScreen.Instance.IsDonutOpen, 3f, "관측자 도넛 열기");
            yield return WaitReal(0.3f);
            yield return shot("11_donut_ob");
            Flow.CloseOverlays();
            Flow.As(0);
            yield return null;
            StageScreen.Instance.ShowLog(true);
            yield return WaitReal(0.3f);
            yield return shot("12_log");
            StageScreen.Instance.ShowLog(false);
            yield return null;

            // 브리핑 게이트 통과(10_stage_* 에는 "준비 중 · 시작! 1/4" 타이머가 찍힌다)
            yield return Flow.BriefAll(true);

            // ---------------- 관문 1 팝업 4장(각 담당자 시점, 오브젝트 앞)
            yield return Flow.PopupShot(shot, 0, "panel", "20_popup_panel_op");
            yield return Flow.PopupShot(shot, 1, "poster", "21_popup_poster_in");
            yield return Flow.PopupShot(shot, 3, "lamp", "22_popup_lamp_ob");
            yield return Flow.PopupShot(shot, 2, "board", "23_popup_board_na");

            // ---------------- 관문 1 → 관문 2(팝업 4장은 Gate2Manual 안에서 2라운드 뒤에 찍힌다)
            yield return Flow.Gate1Manual(shot);
            yield return Flow.Gate2Manual(wrongFirst: false, shot: shot);

            // ---------------- 최종 집결 → 클리어 → 컷씬 → 결과
            Flow.As(0);
            Flow.TeleportAllToGather();
            yield return WaitReal(0.3f);
            Flow.AssertGatherPanelClearOfShutter();
            yield return shot("40_gather");
            yield return Flow.GatherAll();
            yield return WaitReal(0.5f);
            yield return shot("41_clear");
            Flow.AssertRolePanelFits("41_clear");
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "클리어 컷씬");
            yield return WaitReal(1.2f);
            yield return shot("42_clear_cutscene");
            yield return Flow.SkipCutscene(Phase.Result, -1, "결과 화면");
            yield return WaitReal(0.4f);
            yield return shot("43_result");
            // 결과 통계(QA test-T9): 투어는 As(3) DANGER · As(2) GO 로 신호 2개, 관문 2 는 wrongFirst:false
            {
                var stats = Session.State.stats;
                Assert.AreEqual(2, stats.signals, "도넛 신호 = DANGER + GO");
                Assert.AreEqual(7, stats.chats, "채팅 = 로그용 2 + 홀짝 1 + 점자 4");
                Assert.AreEqual(5, stats.cards, "카드 = 숫자 1 + 방향 4");
                Assert.AreEqual(1, stats.wrongInputs, "오답 = 관문 1 오답 1(관문 2 오판 없음)");
                Assert.AreEqual(0, stats.retries, "재시도 없음");
                Assert.AreEqual("1회", Flow.StatValue("오답 횟수"));
                Assert.AreEqual("5장", Flow.StatValue("보낸 카드"));
                Assert.AreEqual("7줄", Flow.StatValue("채팅"));
                Assert.AreEqual("0회", Flow.StatValue("재시도"));
            }

            Session.Send("Again");
            yield return Flow.Until(() => Session.State.phase == Phase.Lobby, 5f, "로비 복귀");
            NetHub.Instance.Shutdown();
            yield return Flow.Until(() => !Session.Connected, 3f, "접속 종료");
        }

        /// <summary>
        /// 튜토리얼 오버레이 3장(QA P-A7). 오버레이를 켠 채(AutoSkip=false) 스테이지에 들어가면 첫 화면은 역할 팝업(09_role_popup),
        /// 이어서 TutorialOverlay.ShowCoachmark(0)/(4) 로 코치마크 1·5단계(09_coachmark_1 / 09_coachmark_5). 다른 장면을 가리지 않도록 본 투어와 분리했다.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CaptureCoachmark()
        {
            if (!Shots.Enabled) Assert.Ignore("PARADOX_SHOTS 환경변수가 없어 건너뜀");
            LogAssert.ignoreFailingMessages = true;

            yield return Flow.Boot();
            yield return Flow.StartSolo(botsAuto: false);
            Session.Send("StartGame");
            yield return Flow.Until(() => Session.State.phase == Phase.NameSetup, 5f, "이름 설정 화면");
            Session.Send("Ready", 1);
            yield return Flow.Until(() => Session.State.phase == Phase.StageIntro, 10f, "오프닝 컷씬");

            TutorialOverlay.AutoSkip = false;
            yield return Flow.SkipCutscene(Phase.StagePlay, 1, "Stage 1 플레이(튜토리얼 켜짐)");
            yield return Flow.Until(() => Session.Avatars.Count == 4, 10f, "아바타 4개 스폰");
            yield return WaitReal(1.0f);
            yield return Shots.Capture("09_role_popup");
            Assert.IsNotNull(TutorialOverlay.Instance, "TutorialOverlay 없음");
            TutorialOverlay.Instance.ShowCoachmark(0);
            yield return WaitReal(0.4f);
            yield return Shots.Capture("09_coachmark_1");
            TutorialOverlay.Instance.ShowCoachmark(4);
            yield return WaitReal(0.4f);
            yield return Shots.Capture("09_coachmark_5");
            TutorialOverlay.AutoSkip = true;   // 떠 있는 오버레이를 닫는다(Skip → Brief)
            yield return null;
        }

        /// <summary>실제 시간으로 잠깐 기다린다(연출·타자 효과가 진행되게).</summary>
        private static IEnumerator WaitReal(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }
    }
}
