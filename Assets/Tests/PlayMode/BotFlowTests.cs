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
    /// 혼자 테스트를 "실제처럼": 사람은 조작자(0번) 한 명뿐이고 통역·내비게이터·관측자는 봇이 스스로 자기 역할을 수행한다.
    /// 조작자가 할 일(홀짝 채팅, 숫자 입력, 점자 채팅, 레버, 자기 원 따라가기)만 직접 하고 나머지는 봇에게 맡겨 Stage 1 이 끝까지 굴러가는지 본다.
    /// 주의: 0번 외의 슬롯에 Flow.As 를 쓰면 그 봇의 자동 행동이 멈추므로(Control 명령) 여기서는 절대 쓰지 않는다.
    /// </summary>
    public class BotFlowTests
    {
        private const int Op = 0;
        private const float ChatRepeat = 10f;   // 봇 통역이 못 읽었을 때를 대비해 같은 채팅을 다시 보내는 간격
        private const int MaxPasses = 15;       // 관문 2 루프 상한: (4라운드 + 판정 대기 1) × 3시도 — 봇 통역·내비가 그 안에 정답을 못 맞추면 실패

        [UnityTearDown]
        public IEnumerator Down() => Flow.Teardown();

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator SoloWithBots_PlaysLikeRealGame()
        {
            yield return Flow.Boot();
            yield return Flow.StartSolo(botsAuto: true);
            yield return Flow.LobbyToStage("혼자", briefAll: false);   // 봇 3명은 1.5초 뒤 스스로 Brief → 타이머 시작(D1)
            Assert.AreEqual(Op, Session.LocalSlot, "사람은 조작자 자리");
            Assert.IsTrue(Session.St.timerRunning && Session.St.briefDone[1] && Session.St.briefDone[2] && Session.St.briefDone[3], "봇 슬롯의 자동 브리핑");

            // ---------------- 관문 1: 봇 내비가 안내데스크로 핑 → 조작자는 제어반 앞으로
            yield return Flow.Until(() => Session.St.lastPingRoom == 1, 90f, "봇 내비게이터의 핑(안내데스크, 방 1) — 내비 봇이 현황판을 열고 Ping 1 을 보내야 함");
            yield return Flow.GoTo(Op, "panel");

            // 홀짝 채팅 → 봇 통역이 (봇 관측자의 색 신고와 함께) 숫자 카드를 보낼 때까지
            int parity = Stage1Logic.Parity(Session.St.calcResult);
            string parityChat = parity == 0 ? "결과 짝수야" : "결과 홀수야";
            yield return ChatUntil(parityChat, () => Session.St.numberCard >= 0, 120f, "봇 통역의 숫자 카드 — 봇 관측자 Color + 조작자 홀짝 채팅을 봇 통역이 조합해야 함");
            int card = Session.St.numberCard;
            Assert.AreEqual(Session.St.calcAnswer, card, "봇 통역이 보낸 숫자는 정답이어야 함");

            // 조작자가 받은 숫자를 직접 입력
            yield return Flow.GoTo(Op, "panel");
            Session.Send("Digit", card);
            yield return Flow.Until(() => Session.St.calcInput == card.ToString(), 3f, "숫자 입력");
            Session.Send("DigitConfirm");
            yield return Flow.Until(() => Session.St.gate1Done && Session.St.step == 1, 5f, "관문 1 해제");

            // ---------------- 이동 구간 → 관문 2: 봇 내비가 모자이크방으로 핑
            yield return Flow.Until(() => Session.St.lastPingRoom == 3, 120f, "봇 내비게이터의 핑(모자이크방, 방 3)");
            yield return Flow.GoTo(Op, "lever");

            // 4라운드: 점자 채팅 → 봇 통역의 방향 카드 → 레버. 봇 내비가 오판하면(리셋) 다시 채운다.
            int passes = 0;
            while (!Session.St.gate2Done)
            {
                Assert.Less(passes, MaxPasses, $"관문 2 라운드 진행/리셋이 {MaxPasses}회((4라운드 + 판정 1) × 3시도)를 넘음 — 봇 통역·내비가 정답을 못 맞춤");
                passes++;
                int r = Session.St.round;
                if (r >= 4)
                {
                    // 봇 내비게이터의 배전함 판정 대기: 안전이면 gate2Done, 아니면 라운드 리셋
                    yield return Flow.Until(() => Session.St.gate2Done || Session.St.round == 0, 60f, "봇 내비게이터의 배전함 판정(Judge)");
                    continue;
                }

                int digit = Session.St.brailleDigit[r];
                int attemptsAtStart = Session.St.gate2Attempts;
                string brailleChat = "점자 " + string.Join(",", Stage1Logic.BrailleDots(digit));
                yield return Flow.GoTo(Op, "lever");
                yield return ChatUntil(brailleChat,
                    () => Session.St.dirCard >= 0 || Session.St.round != r || Session.St.gate2Attempts != attemptsAtStart,
                    120f, $"라운드 {r + 1}: 봇 통역의 방향 카드 — 봇 관측자 Color + 조작자 점자 채팅을 봇 통역이 해독해야 함");
                if (Session.St.round != r || Session.St.gate2Attempts != attemptsAtStart) continue;   // 그 사이 리셋됨

                int dir = Session.St.dirCard;
                Debug.Log($"[BotFlow] 라운드 {r + 1}: 점자 {digit} / 색 {Stage1Logic.ColorName(Session.St.lampColor2[r])} / 카드 {Stage1Logic.DirGlyph(dir)} / 정답 {Stage1Logic.DirGlyph(Session.St.answerDir[r])}");
                yield return Flow.GoTo(Op, "lever");
                Session.Send("Lever", dir);
                yield return Flow.Until(() => Session.St.round == r + 1 || Session.St.round == 0, 5f, $"라운드 {r + 1} 레버");
            }
            yield return Flow.Until(() => Session.St.step == 2, 5f, "최종 집결 단계");

            // ---------------- 최종 집결: 조작자만 자기 원을 매 프레임 따라가고, 봇 셋은 스스로 걸어온다
            float start = Time.realtimeSinceStartup;
            while (!Session.St.cleared)
            {
                Flow.Teleport(Op, Flow.Gather(Op));
                if (Time.realtimeSinceStartup - start > 90f)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Session.PosOf(i);
                        bool inside = p != null && Stage1Logic.InGather(i, p.Value, Session.St, Session.Clock);
                        sb.Append($" [{i}] in={inside} center=({Flow.Gather(i).x:0.0},{Flow.Gather(i).y:0.0})");
                    }
                    Assert.Fail($"시간 초과: 봇들이 자기 원으로 걸어와 클리어 (allInSince={Session.St.allInSince:0.0}, {Flow.Where()}, {Flow.Positions()},{sb})");
                }
                yield return null;
            }

            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 1 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.Result, -1, "결과 화면");
            NetHub.Instance.Shutdown();
            yield return Flow.Until(() => !Session.Connected, 3f, "접속 종료");
        }

        /// <summary>
        /// 호스트가 1~4 키로 봇 자리를 조종하는 동안 호스트 자신의(사람) 아바타는 BotBrain 이 움직이면 안 된다(명세 9절: 조종하지 않는 "봇"만 자동 이동).
        /// botsAuto=true 여야 드러나므로 ScreenshotTour(botsAuto=false)의 RoleViews 가 아니라 여기서 검사한다.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator SoloWithBots_HumanAvatarStaysStillWhileHostControlsBot()
        {
            yield return Flow.Boot();
            yield return Flow.StartSolo(botsAuto: true);
            yield return Flow.LobbyToStage("혼자", briefAll: false);
            yield return Flow.Until(() => Session.PosOf(Op) != null && Session.PosOf(Op).Value.x > -100f, 8f, "아바타 0 스테이지 시작 위치");
            Flow.As(1);   // 호스트가 통역 봇을 조종 — 사람 슬롯 0의 아바타는 움직이면 안 된다
            yield return Flow.Until(() => Session.State.hostControlSlot == 1, 3f, "Control 반영");
            Vector2 start = Session.PosOf(Op).Value;
            yield return Flow.Until(() => Session.St.lastPingRoom == 1, 90f, "봇 내비게이터의 핑(안내데스크)");
            yield return Flow.WaitReal(6f);
            float moved = Vector2.Distance(start, Session.PosOf(Op).Value);
            Flow.As(0);   // Control(0) → hostControlSlot=-1, 통역 봇 자동 행동 재개
            Assert.Less(moved, 0.5f, $"사람(호스트) 아바타 0 이 조종하지 않는 동안 스스로 움직임: {moved:0.00} 유닛");
            NetHub.Instance.Shutdown();
            yield return Flow.Until(() => !Session.Connected, 3f, "접속 종료");
        }

        /// <summary>조작자 채팅을 보내고 조건이 참이 될 때까지 기다린다. 봇 통역이 못 읽었을 수 있어 ChatRepeat 초마다 같은 문장을 다시 보낸다.</summary>
        private static IEnumerator ChatUntil(string text, System.Func<bool> cond, float timeout, string what)
        {
            float start = Time.realtimeSinceStartup;
            float lastSent = -999f;
            while (!cond())
            {
                float now = Time.realtimeSinceStartup;
                if (now - lastSent >= ChatRepeat)
                {
                    Session.Send("Chat", 0, 0, text);
                    lastSent = now;
                }
                if (now - start > timeout)
                    Assert.Fail($"시간 초과: {what} (채팅 '{text}', {Flow.Where()}, colorReport={Session.St.colorReport}, numberCard={Session.St.numberCard}, dirCard={Session.St.dirCard}, {Flow.Positions()})");
                yield return null;
            }
        }
    }
}
