using System.Collections.Generic;
using NUnit.Framework;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Tests
{
    /// <summary>
    /// 서버 규칙(GameLogic + Stage1Logic)을 네트워크·씬 없이 헤드리스로 돌린다. GameLogic 은 MonoBehaviour 가 아니고
    /// Clock 이 public 이라 시간을 직접 진행시킬 수 있다(30초 자동 시작·12분 시간 초과를 실제로 기다리지 않는다).
    /// 이벤트는 Emit 으로 모으고, 아바타 위치는 GetPos 로 준다(Teleport 이벤트가 위치를 갱신).
    /// </summary>
    public class GameLogicHeadlessTests
    {
        private const int Op = 0, In = 1, Na = 2, Ob = 3;
        private const long HostClient = 1;

        [Test]
        public void LobbyLaunchFillsEmptySeatsWithBots()
        {
            // 2~3명이어도 사람이 모두 준비했으면 방장이 시작할 수 있고, 빈 자리는 AI 가 맡는다
            var game = new GameLogic();
            void Send(int slot, string type, int value = 0) =>
                game.Apply(new Cmd { slot = slot, type = type, a = value }, slot == 0);
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.AreEqual(slot, game.Join(slot + 1, "P" + slot));
                Send(slot, "Ready", 1);
            }
            Send(2, "Launch");   // 방장이 아니면 못 시작
            Assert.AreEqual(Phase.Lobby, game.S.phase);
            Send(0, "Launch");
            Assert.AreEqual(Phase.StageIntro, game.S.phase);
            Assert.IsTrue(game.S.players[3].present && game.S.players[3].isBot);
            Assert.IsFalse(game.S.players[0].isBot);
        }

        [Test]
        public void LobbyLaunchRequiresAllHumansReadyAndHost()
        {
            var game = new GameLogic();
            void Send(int slot, string type, int value = 0) =>
                game.Apply(new Cmd { slot = slot, type = type, a = value }, slot == 0);
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.AreEqual(slot, game.Join(slot + 1, "P" + slot));
                Send(slot, "Ready", 1);
            }
            game.Join(4, "P3");
            Send(0, "Launch");
            Assert.AreEqual(Phase.Lobby, game.S.phase);
            Send(3, "Ready", 1);
            Send(2, "Launch");
            Send(2, "Ready", 0);
            Send(0, "Launch");
            Assert.AreEqual(Phase.Lobby, game.S.phase);
            game.Leave(4);
            game.Join(5, "P4");
            Assert.IsFalse(game.S.players[3].ready);
            Send(2, "Ready", 1);
            Send(3, "Ready", 1);
            Send(0, "Launch");
            Assert.AreEqual(Phase.StageIntro, game.S.phase);
        }

        /// <summary>호스트 1명 + 봇 3명 세션. 명령은 전부 호스트 권한(fromHost=true)으로 넣는다.</summary>
        private sealed class Headless
        {
            public readonly GameLogic G = new GameLogic();
            public readonly List<GameEvent> Events = new List<GameEvent>();
            public readonly Vector2?[] Pos = new Vector2?[4];

            public Headless()
            {
                G.Emit = e =>
                {
                    Events.Add(e);
                    if (e.type == "Teleport" && Roles.Valid(e.slot)) Pos[e.slot] = e.pos.V;
                };
                G.GetPos = s => Roles.Valid(s) ? Pos[s] : null;
                Assert.AreEqual(0, G.Join(HostClient, "테스터"), "호스트는 0번 자리");
                G.S.hostSlot = 0;
                G.FillBots(HostClient);
                Assert.AreEqual(4, G.S.PresentCount);
            }

            public StageState St => G.S.st;

            public void Cmd(int slot, string type, int a = 0, int b = 0, string s = "")
                => G.Apply(new Cmd { type = type, slot = slot, a = a, b = b, s = s ?? "" }, true);

            /// <summary>서버 클록을 dt 만큼 진행하고 한 틱 돌린다.</summary>
            public void Tick(float dt) { G.Clock += dt; G.Tick(dt); }

            /// <summary>dt 씩 total 초 진행(집결처럼 매 틱 위치를 갱신해야 할 때는 before 콜백).</summary>
            public void Run(float total, float dt = 0.05f, System.Action before = null)
            {
                for (float t = 0f; t < total - 1e-4f; t += dt) { before?.Invoke(); Tick(dt); }
            }

            /// <summary>로비 → 이름 → 준비 → (1.5초) 오프닝 → Skip → Stage 1 플레이. 봇 자동은 끈다(결정적 검증).</summary>
            public void ToStage()
            {
                Cmd(Op, "BotsAuto", 0);
                Cmd(Op, "StartGame");
                Assert.AreEqual(Phase.NameSetup, G.S.phase);
                Cmd(Op, "Name", 0, 0, "테스터");
                Cmd(Op, "Ready", 1);
                Assert.AreEqual(Phase.Ready, G.S.phase, "봇은 이미 준비 상태라 내가 준비하면 바로 Ready");
                Tick(GameLogic.ReadyDelay + 0.01f);
                Assert.AreEqual(Phase.StageIntro, G.S.phase);
                Cmd(Op, "Skip");
                Assert.AreEqual(Phase.StagePlay, G.S.phase);
                Assert.IsNotNull(G.Map, "StartStage 는 맵을 만든다");
                for (int i = 0; i < 4; i++) Assert.IsTrue(Pos[i].HasValue, $"스폰 Teleport 이벤트 {i}");
            }

            public void BriefAll()
            {
                for (int i = 0; i < 4; i++) Cmd(i, "Brief");
                Assert.IsTrue(St.timerRunning, "전원 Brief → 타이머 시작");
            }

            /// <summary>슬롯을 담당 오브젝트 앞(서버 근접 조건 안)으로.</summary>
            public void Near(int slot, string prop)
            {
                var p = G.Map.Prop(prop);
                Assert.IsNotNull(p, $"오브젝트 {prop}");
                Pos[slot] = p.pos + new Vector2(0f, -0.9f);
            }

            public void SolveGate1()
            {
                Near(Op, "panel");
                Cmd(Op, "Digit", St.calcAnswer);
                Cmd(Op, "DigitConfirm");
                Assert.IsTrue(St.gate1Done && St.step == 1 && St.checkpoint == 1, "관문 1 해제");
            }

            public void SolveGate2()
            {
                for (int r = 0; r < Stage1Logic.Rounds; r++)
                {
                    Near(In, "mural"); Cmd(In, "DirCard", St.answerDir[r]);
                    Near(Op, "lever"); Cmd(Op, "Lever", St.answerDir[r]);
                    Assert.AreEqual(r + 1, St.round);
                }
                Near(Na, "junction"); Cmd(Na, "Judge", 1);
                Assert.IsTrue(St.gate2Done && St.step == 2, "관문 2 해제 → 최종 집결");
            }

            public int Count(string type) { int n = 0; foreach (var e in Events) if (e.type == type) n++; return n; }
            public GameEvent Last(string type) { for (int i = Events.Count - 1; i >= 0; i--) if (Events[i].type == type) return Events[i]; return null; }

            /// <summary>대상 슬롯(-1 = 전체)에게 간 토스트 중 문구를 포함하는 것의 수.</summary>
            public int Toasts(int target, string contains)
            {
                int n = 0;
                foreach (var e in Events) if (e.type == "Toast" && e.target == target && e.s.Contains(contains)) n++;
                return n;
            }
        }

        // ------------------------------------------------------------------ 안내 토스트·기록 필드(B1/B2/B6/A3/C4)

        [Test]
        public void Gate1Wrong_RecordsLastWrongDigit_ToastsInterpreter_ClearedByCardAndByCorrect()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            var st = h.St;
            Assert.AreEqual(-1, st.lastWrongDigit);
            int wrong = (st.calcAnswer + 1) % 10;
            h.Near(Op, "panel");
            h.Cmd(Op, "Digit", wrong); h.Cmd(Op, "DigitConfirm");
            Assert.AreEqual(wrong, st.lastWrongDigit, "오답 입력값 기록(B2)");
            Assert.AreEqual(1, h.Toasts(In, Stage1Logic.WrongDigitText(wrong)), "통역 타깃 토스트 1회");
            Assert.AreEqual(0, h.Toasts(Op, Stage1Logic.WrongDigitText(wrong)), "조작자에게는 가지 않는다");
            // 새 숫자 카드가 가면 지워진다
            h.Near(In, "poster"); h.Cmd(In, "Card", st.calcAnswer);
            Assert.AreEqual(-1, st.lastWrongDigit, "카드 갱신 시 −1");
            // 다시 틀리면 다시 기록, 정답이면 −1
            h.Run(Stage1Logic.Gate1Lock + 0.1f);
            int wrong2 = (st.calcAnswer + 2) % 10;
            h.Cmd(Op, "Digit", wrong2); h.Cmd(Op, "DigitConfirm");
            Assert.AreEqual(wrong2, st.lastWrongDigit);
            h.Run(Stage1Logic.Gate1Lock + 0.1f);
            h.Cmd(Op, "Digit", st.calcAnswer); h.Cmd(Op, "DigitConfirm");
            Assert.IsTrue(st.gate1Done);
            Assert.AreEqual(-1, st.lastWrongDigit, "정답 시 −1");
        }

        [Test]
        public void Lever_ToastsObserverEachNewRound_ResetToastsOperatorAndObserver()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            h.SolveGate1();
            var st = h.St;
            for (int r = 0; r < Stage1Logic.Rounds; r++)
            {
                h.Near(In, "mural"); h.Cmd(In, "DirCard", st.answerDir[r]);
                h.Near(Op, "lever"); h.Cmd(Op, "Lever", st.answerDir[r]);
                if (r < Stage1Logic.Rounds - 1)
                    Assert.AreEqual(1, h.Toasts(Ob, Stage1Logic.RoundStartText(r + 2)), $"라운드 {r + 2} 시작 토스트는 관측자에게 1회(B1)");
            }
            Assert.AreEqual(0, h.Toasts(Ob, Stage1Logic.RoundStartText(5)), "5라운드는 없다 — 4라운드 완료 뒤에는 라운드 토스트 없음");
            Assert.AreEqual(0, h.Toasts(-1, "색이 같아도"), "라운드 토스트는 전체 방송이 아니다");

            // B6: 리셋(위험 판정) → 조작자 "새 패턴", 관측자 "색을 다시 신고"
            h.Near(Na, "junction"); h.Cmd(Na, "Judge", 0);
            Assert.AreEqual(0, st.round);
            Assert.AreEqual(1, h.Toasts(Op, Stage1Logic.ResetOperatorText));
            Assert.AreEqual(1, h.Toasts(Ob, Stage1Logic.ResetObserverText));
            Assert.AreEqual(0, h.Toasts(In, Stage1Logic.ResetOperatorText), "통역에게는 가지 않는다");
        }

        [Test]
        public void PingNudge_After40sWithoutPing_OnceOnly_NotIfPinged()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            var st = h.St;
            h.Run(Stage1Logic.PingNudgeAfter - 1f);
            Assert.IsFalse(st.pingNudged, "39초까지는 안내 없음");
            Assert.AreEqual(0, h.Toasts(Na, Stage1Logic.PingNudgeNavText));
            h.Run(1.2f);
            Assert.IsTrue(st.pingNudged, "40초 경과 + 핑 없음 → 안내(A3)");
            Assert.AreEqual(1, h.Toasts(Na, Stage1Logic.PingNudgeNavText), "내비에게 방법");
            for (int i = 0; i < 4; i++) if (i != Na) Assert.AreEqual(1, h.Toasts(i, Stage1Logic.PingNudgeOthersText), $"슬롯 {i} 에게 목적지");
            h.Run(45f);
            Assert.AreEqual(1, h.Toasts(Na, Stage1Logic.PingNudgeNavText), "한 번만");

            // 핑이 먼저 있으면 안내가 나오지 않는다
            var h2 = new Headless();
            h2.ToStage();
            h2.BriefAll();
            h2.Cmd(Na, "Ping", StageMaps.RoomDesk);
            h2.Run(Stage1Logic.PingNudgeAfter + 5f);
            Assert.IsFalse(h2.St.pingNudged);
            Assert.AreEqual(0, h2.Toasts(Na, Stage1Logic.PingNudgeNavText));
        }

        [Test]
        public void TimeWarnings_At6_3_1Minutes_OnceEach_WithFlash()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            var st = h.St;
            int flashes = h.Count("Flash");
            Assert.AreEqual(0, st.timeWarned);
            // 720 → 남은 361초: 아직
            h.Tick(Stage1Logic.TimeLimit - 361f);
            Assert.AreEqual(0, st.timeWarned, "6:01 에는 아직");
            h.Tick(1.5f);
            Assert.AreEqual(1, st.timeWarned, "6:00 아래 → 비트 1");
            Assert.AreEqual(1, h.Toasts(-1, Stage1Logic.TimeWarnText(6)));
            Assert.AreEqual(flashes + 1, h.Count("Flash"), "Flash 1회");
            h.Run(10f);
            Assert.AreEqual(1, h.Toasts(-1, Stage1Logic.TimeWarnText(6)), "한 번만");
            // 3:00
            h.Tick(h.G.TimeLeft - 179.5f);
            Assert.AreEqual(3, st.timeWarned);
            Assert.AreEqual(1, h.Toasts(-1, Stage1Logic.TimeWarnText(3)));
            // 1:00 — 감점으로 넘어가도 잡힌다
            st.penalty += h.G.TimeLeft - 59f;
            h.Tick(0.05f);
            Assert.AreEqual(7, st.timeWarned);
            Assert.AreEqual(1, h.Toasts(-1, Stage1Logic.TimeWarnText(1)));
            Assert.AreEqual(flashes + 3, h.Count("Flash"), "경고마다 Flash 1회");
            Assert.IsFalse(st.failed);
        }

        [Test]
        public void TimeWarnings_SkippingSeveralThresholdsInOneTick_AnnouncesOnlyLowest()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            var st = h.St;
            h.Tick(Stage1Logic.TimeLimit - 100f);   // 남은 100초: 6:00·3:00 을 한 틱에 지나침
            Assert.AreEqual(3, st.timeWarned, "지나친 비트는 모두 기록");
            Assert.AreEqual(0, h.Toasts(-1, Stage1Logic.TimeWarnText(6)), "6분 토스트는 생략");
            Assert.AreEqual(1, h.Toasts(-1, Stage1Logic.TimeWarnText(3)), "가장 작은 3분만");
        }

        // ------------------------------------------------------------------ 브리핑 게이트(v2 D1, QA test-T12)

        [Test]
        public void BriefGate_StopsUntilAllPresent_AutoStartsAfter30s_IgnoresDuplicates()
        {
            var h = new Headless();
            h.ToStage();
            var st = h.St;
            float stageStart = st.startClock;
            Assert.IsFalse(st.timerRunning, "스테이지 시작 직후 정지");
            Assert.AreEqual(st.timeLimit, h.G.TimeLeft, 0.001f);

            // 3/4 만 Brief → 여전히 정지, 시간이 흘러도 TimeLeft 고정, 관문 명령 거부
            for (int i = 0; i < 3; i++)
            {
                h.Cmd(i, "Brief");
                Assert.IsFalse(st.timerRunning, $"{i + 1}/4 Brief 로는 시작하지 않는다");
                Assert.AreEqual(i + 1, GameLogic.BriefCount(st));
            }
            h.Run(5f);
            Assert.IsFalse(st.timerRunning);
            Assert.AreEqual(st.timeLimit, h.G.TimeLeft, 0.001f, "게이트 중에는 시간이 흐르지 않는다");
            h.Near(Op, "panel"); h.Cmd(Op, "Digit", 7);
            Assert.AreEqual("", st.calcInput, "게이트 중 Digit 거부");
            h.Cmd(Na, "Ping", 1);
            Assert.AreEqual(1, st.lastPingRoom, "게이트 중에도 Ping 은 허용");
            h.Cmd(Op, "Brief");
            Assert.AreEqual(3, GameLogic.BriefCount(st), "중복 Brief 는 인원을 늘리지 않는다");

            // 30초 자동 시작(마지막 한 명이 튜토리얼을 안 닫아도 게임이 멈추지 않는다)
            h.Run(Stage1Logic.BriefTimeout - 5f - 0.5f);
            Assert.IsFalse(st.timerRunning, "29.5초까지는 정지");
            h.Run(1f);
            Assert.IsTrue(st.timerRunning, "30초 경과 → 자동 시작");
            Assert.AreEqual(1, h.Count("TimerStart"));
            Assert.AreEqual(stageStart + Stage1Logic.BriefTimeout, st.startClock, 0.06f, "startClock 은 타이머 시작 순간(30초 경과 틱) 다시 찍힌다");
            float startClock = st.startClock;
            Assert.IsFalse(st.briefDone[Ob], "자동 시작은 briefDone 을 채우지 않는다");

            // 시작 뒤 늦은 Brief / 중복 Brief 는 startClock 을 다시 찍지 않는다
            h.Run(2f);
            h.Cmd(Ob, "Brief"); h.Cmd(Op, "Brief");
            Assert.AreEqual(startClock, st.startClock, 0.0001f, "시작 뒤 Brief 는 startClock 을 바꾸지 않는다");
            Assert.AreEqual(1, h.Count("TimerStart"), "TimerStart 는 한 번");
            Assert.AreEqual(4, GameLogic.BriefCount(st));
            Assert.Less(h.G.TimeLeft, st.timeLimit - 1.9f, "시작 뒤에는 시간이 흐른다");
            h.Cmd(Op, "Digit", 7);
            Assert.AreEqual("7", st.calcInput, "게이트 해제 뒤 Digit 허용");
        }

        // ------------------------------------------------------------------ 시간 초과 → 재시도(QA test-T1)

        [Test]
        public void Timeout_Fails_ThenRetryResumesFromCheckpoint1_WithNewSeedAndImmediateTimer()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            var st0 = h.St;
            int seed0 = st0.seed;
            h.SolveGate1();
            Assert.AreEqual(-1, st0.lastPingRoom, "관문 1 해제 시 핑 초기화");

            // 관문 2 를 한 라운드 진행해 둔다 — 재시도 뒤 라운드가 0 으로 돌아가는지 보기 위해
            h.Near(In, "mural"); h.Cmd(In, "DirCard", st0.answerDir[0]);
            h.Near(Op, "lever"); h.Cmd(Op, "Lever", st0.answerDir[0]);
            Assert.AreEqual(1, st0.round);
            h.Cmd(Op, "Retry");
            Assert.AreSame(st0, h.St, "실패 전 Retry 는 무시된다");

            // 12분 경과 → Fail(이동 잠김, playSeconds 누적, Fail 이벤트)
            h.Run(3f);
            h.Tick(Stage1Logic.TimeLimit);
            Assert.IsTrue(st0.failed, "TimeLeft ≤ 0 → failed");
            Assert.AreEqual(0, st0.moveMode, "실패하면 이동 잠김");
            Assert.LessOrEqual(h.G.TimeLeft, 0f);
            Assert.AreEqual(1, h.Count("Fail"));
            Assert.AreEqual(h.G.Clock - st0.startClock, h.G.S.stats.playSeconds, 0.01f, "실패한 구간이 플레이 시간에 누적");
            Assert.AreEqual(Phase.StagePlay, h.G.S.phase, "실패 화면은 StagePlay 위 오버레이(단계는 그대로)");

            // 실패 중에는 스테이지 명령·채팅 통계 외 명령이 거부된다
            h.Cmd(Na, "Ping", 3);
            Assert.AreEqual(-1, st0.lastPingRoom, "실패 중 Ping 거부");
            h.Cmd(Ob, "Brief");
            h.Cmd(Ob, "Signal", 0, 0, "GO");
            Assert.AreEqual(0, h.G.S.stats.signals, "실패 중 Signal 거부");
            int retriesStat = h.G.S.stats.retries;

            // 호스트 Retry → 체크포인트 1(관문 2부터), 새 시드, 타이머 즉시, 스폰 아트리움
            int teleportsBefore = h.Count("Teleport");
            h.Cmd(Op, "Retry");
            var st1 = h.St;
            Assert.AreNotSame(st0, st1, "재시도는 새 StageState");
            Assert.IsFalse(st1.failed);
            Assert.AreEqual(1, st1.retries);
            Assert.AreEqual(retriesStat + 1, h.G.S.stats.retries);
            Assert.AreEqual(seed0 + 17, st1.seed, "재시도 시드 = 이전 시드 + 17");
            Assert.AreEqual(1, st1.checkpoint);
            Assert.IsTrue(st1.gate1Done && st1.step == 1, "체크포인트 1 = 관문 2부터");
            Assert.IsFalse(st1.gate2Done);
            Assert.AreEqual(0, st1.round); Assert.AreEqual(0, st1.gate2Attempts); Assert.AreEqual(-1, st1.dirCard); Assert.AreEqual(-1, st1.colorReport);
            foreach (int d in st1.chosenDir) Assert.AreEqual(-1, d);
            Assert.IsTrue(Stage1Logic.ValidateLadder(st1));
            Assert.AreEqual(0f, st1.penalty, 0.001f);
            Assert.AreEqual(1, st1.moveMode, "재시도 뒤 이동 가능");
            Assert.IsTrue(st1.timerRunning, "재시도는 튜토리얼이 없어 타이머 즉시 시작(14-1 D1(2))");
            Assert.AreEqual(h.G.Clock, st1.startClock, 0.001f);
            Assert.AreEqual(st1.timeLimit, h.G.TimeLeft, 0.001f, "시간은 다시 12분");
            Assert.AreEqual(teleportsBefore + 4, h.Count("Teleport"), "4명 스폰 Teleport");
            for (int i = 0; i < 4; i++) Assert.AreEqual(StageMaps.RoomAtrium, h.G.RoomOf(i), $"재시도 스폰은 아트리움 {i}");
            StringAssert.Contains("관문 2부터", h.Last("Toast").s);
            Assert.IsTrue(h.G.Map.Walkable(StageMaps.LockedDoor3.center, StageMaps.AvatarRadius), "체크포인트 1 맵은 모자이크방 문이 열려 있다");

            h.Run(1f);
            Assert.AreEqual(st1.timeLimit - 1f, h.G.TimeLeft, 0.06f, "재시도 타이머 감소");
        }

        [Test]
        public void ForceFail_BeforeGate1_RetryRestartsFromCheckpoint0()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            h.Cmd(Op, "ForceFail");
            Assert.IsTrue(h.St.failed);
            h.Cmd(Op, "Retry");
            var st = h.St;
            Assert.AreEqual(0, st.checkpoint); Assert.AreEqual(0, st.step); Assert.IsFalse(st.gate1Done);
            Assert.AreEqual(1, st.retries);
            Assert.IsTrue(st.timerRunning, "재시도는 체크포인트 0 이어도 타이머 즉시");
            StringAssert.Contains("처음부터", h.Last("Toast").s);
            Assert.IsFalse(h.G.Map.Walkable(StageMaps.LockedDoor3.center, StageMaps.AvatarRadius), "체크포인트 0 맵은 모자이크방 문이 잠겨 있다");
        }

        // ------------------------------------------------------------------ 관문 2 게이팅(QA test-T3/T4/T5 헤드리스판)

        [Test]
        public void Gate2_LeverNeedsCard_JudgeNeedsFourRounds_DangerResetIsFree_AfterFourRoundsCardsRejected()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            h.SolveGate1();
            var st = h.St;

            // 카드 없이 레버 → 거부
            h.Near(Op, "lever"); h.Cmd(Op, "Lever", st.answerDir[0]);
            Assert.AreEqual(0, st.round); Assert.AreEqual(-1, st.chosenDir[0]);
            // 다른 역할의 DirCard / 멀리서 보낸 DirCard → 거부
            h.Near(Op, "mural"); h.Cmd(Op, "DirCard", 0);
            Assert.AreEqual(-1, st.dirCard, "조작자의 DirCard 무시");
            h.Pos[In] = h.G.Map.spawns[In]; h.Cmd(In, "DirCard", 0);
            Assert.AreEqual(-1, st.dirCard, "벽화에서 멀면 DirCard 거부");
            int cards = h.G.S.stats.cards;
            // 1라운드 정상
            h.Near(In, "mural"); h.Cmd(In, "DirCard", st.answerDir[0]);
            Assert.AreEqual(cards + 1, h.G.S.stats.cards, "받아들인 카드만 센다");
            h.Near(Op, "lever"); h.Cmd(Op, "Lever", st.answerDir[0]);
            Assert.AreEqual(1, st.round); Assert.AreEqual(-1, st.dirCard, "레버 뒤 카드 소모");
            // 라운드 도중 '안전' 판정 → 무시(페널티·리셋 없음)
            float pen = st.penalty; int att = st.gate2Attempts;
            h.Near(Na, "junction"); h.Cmd(Na, "Judge", 1);
            Assert.AreEqual(pen, st.penalty, 0.001f); Assert.AreEqual(att, st.gate2Attempts); Assert.AreEqual(1, st.round); Assert.IsFalse(st.gate2Done);
            // '위험' → 감점 0, 새 시드, RoundReset 1회
            int[] digitsBefore = (int[])st.brailleDigit.Clone(); int startBefore = st.ladderStart;
            h.Cmd(Na, "Judge", 0);
            Assert.AreEqual(pen, st.penalty, 0.001f, "위험 재시도는 감점 0(v2 D3)");
            Assert.AreEqual(att + 1, st.gate2Attempts); Assert.AreEqual(0, st.round); Assert.AreEqual(1, h.Count("RoundReset"));
            Assert.IsTrue(Stage1Logic.ValidateLadder(st));
            bool same = startBefore == st.ladderStart; for (int r = 0; r < 4; r++) same &= digitsBefore[r] == st.brailleDigit[r];
            Assert.IsFalse(same, "새 시드로 퍼즐을 다시 만든다");
            h.Cmd(Na, "Judge", 0);
            Assert.AreEqual(1, h.Count("RoundReset"), "round==0 에서 위험은 무시");
            Assert.AreEqual(0, h.G.S.stats.wrongInputs, "위험 재시도는 오답이 아니다");

            // 4라운드 채운 뒤 카드·레버 거부, 안전 오판은 −15초 + 오답 1 + 리셋
            for (int r = 0; r < Stage1Logic.Rounds; r++)
            {
                h.Near(In, "mural"); h.Cmd(In, "DirCard", (st.answerDir[r] + (r == 0 ? 1 : 0)) % 4);
                h.Near(Op, "lever"); h.Cmd(Op, "Lever", st.dirCard);
            }
            Assert.AreEqual(4, st.round);
            h.Near(In, "mural"); h.Cmd(In, "DirCard", 0);
            Assert.AreEqual(-1, st.dirCard, "4라운드 뒤 방향 카드 거부");
            int end = Stage1Logic.PathEnd(st);
            h.Near(Op, "lever"); h.Cmd(Op, "Lever", 0);
            Assert.AreEqual(4, st.round); Assert.AreEqual(end, Stage1Logic.PathEnd(st), "4라운드 뒤 레버는 경로를 바꾸지 않는다");
            Assert.AreNotEqual(st.ladderSafe, end, "틀린 레버가 있으면 안전 단자가 아니다");
            pen = st.penalty; att = st.gate2Attempts;
            h.Near(Na, "junction"); h.Cmd(Na, "Judge", 1);
            Assert.AreEqual(Stage1Logic.Gate2Penalty, st.penalty - pen, 0.001f, "안전 오판 −15초");
            Assert.AreEqual(1, h.G.S.stats.wrongInputs); Assert.AreEqual(att + 1, st.gate2Attempts); Assert.AreEqual(0, st.round);
        }

        // ------------------------------------------------------------------ 현황판 스냅샷(D-A4 "가서 봐야 아는")

        [Test]
        public void BoardSnapshot_FrozenAtBoardClock_RefreshesOnReopen_ZeroBeforeOpen()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            var st = h.St;
            int z = StageMaps.RoomAquarium;
            // 침수를 "타이머 시작 뒤 초당 1%" 로 만들어 값을 예측 가능하게
            st.floodBase[z] = 0f; st.floodRate[z] = 1f;

            Assert.IsFalse(st.boardOpened);
            Assert.AreEqual(0f, Stage1Logic.FloodSnapshot(st, z), "열기 전 스냅샷은 0");
            Assert.Less(Stage1Logic.SecondsSinceBoard(st, h.G.Clock), 0f, "열기 전 -1");
            Assert.IsFalse(Stage1Logic.IsDangerSnapshot(st, z));
            h.Cmd(Na, "OpenBoard");
            Assert.IsFalse(st.boardOpened, "현황판에서 멀면 거부");
            h.Near(Op, "board"); h.Cmd(Op, "OpenBoard");
            Assert.IsFalse(st.boardOpened, "내비게이터만 열 수 있다");

            // 5초 뒤 처음 열기 → boardClock = 지금, 스냅샷 = 그 시각의 실시간 값(≈5)
            h.Run(5f);
            h.Near(Na, "board"); h.Cmd(Na, "OpenBoard");
            Assert.IsTrue(st.boardOpened);
            Assert.AreEqual(h.G.Clock, st.boardClock, 0.001f, "boardClock = 연 시각");
            float snap1 = Stage1Logic.FloodSnapshot(st, z);
            Assert.AreEqual(Stage1Logic.Flood(st, z, h.G.Clock), snap1, 0.001f, "스냅샷 = 연 시각의 실시간 값");
            Assert.AreEqual(5f, snap1, 0.1f);
            StringAssert.Contains("안전 라인", h.Last("Toast").s, "처음 열면 안전 라인 안내");

            // 20초 동안 실시간은 오르지만 스냅샷은 고정
            h.Run(20f);
            Assert.Greater(Stage1Logic.Flood(st, z, h.G.Clock), snap1 + 19f, "실시간은 계속 오른다");
            Assert.AreEqual(snap1, Stage1Logic.FloodSnapshot(st, z), 0.001f, "스냅샷은 boardClock 시점 값으로 고정");
            Assert.AreEqual(20f, Stage1Logic.SecondsSinceBoard(st, h.G.Clock), 0.1f, "확인한 뒤 지난 시간");

            // 다시 열면 갱신(early return 없음)
            h.Cmd(Na, "OpenBoard");
            Assert.AreEqual(h.G.Clock, st.boardClock, 0.001f, "다시 열면 boardClock 갱신");
            float snap2 = Stage1Logic.FloodSnapshot(st, z);
            Assert.AreEqual(25f, snap2, 0.15f, "스냅샷이 지금 값으로");
            Assert.AreEqual(0f, Stage1Logic.SecondsSinceBoard(st, h.G.Clock), 0.001f);
            StringAssert.Contains("다시 확인", h.Last("Toast").s, "다시 열면 갱신 안내");

            // 실제가 70 을 넘어도 스냅샷 위험 판정은 마지막 확인 시점 기준 — 다시 봐야 안다
            h.Run(50f);
            Assert.IsTrue(Stage1Logic.IsDanger(st, z, h.G.Clock), "실시간은 위험(≈75)");
            Assert.IsFalse(Stage1Logic.IsDangerSnapshot(st, z), "스냅샷(25)은 아직 안전 — 미니맵 물결 없음");
            h.Cmd(Na, "OpenBoard");
            Assert.IsTrue(Stage1Logic.IsDangerSnapshot(st, z), "다시 보면 위험");
            Assert.IsFalse(Stage1Logic.IsDangerSnapshot(st, StageMaps.RoomAtrium), "아트리움은 위험 방이 아니다");

            // 관문 1 을 풀어도(step 1) 열림 상태·스냅샷 시각은 유지된다
            h.SolveGate1();
            Assert.IsTrue(st.boardOpened);
            Assert.AreEqual(snap2 + 50f, Stage1Logic.FloodSnapshot(st, z), 0.2f, "관문 통과는 스냅샷을 바꾸지 않는다");
        }

        // ------------------------------------------------------------------ 배전함 "위험 — 처음부터" 몸짓 카드(D-A5)

        [Test]
        public void Judge0_ServerEmitsDangerThenNoSignal_ForTeam_CountedOnce()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            h.SolveGate1();
            var st = h.St;
            h.Near(In, "mural"); h.Cmd(In, "DirCard", st.answerDir[0]);
            h.Near(Op, "lever"); h.Cmd(Op, "Lever", st.answerDir[0]);
            Assert.AreEqual(1, st.round);
            Assert.AreEqual(0, h.G.S.stats.signals, "아직 신호 없음");

            // 멀리서 / 4라운드 전 '안전' 은 신호를 내지 않는다
            h.Pos[Na] = h.G.Map.spawns[Na]; h.Cmd(Na, "Judge", 0);
            Assert.AreEqual(0, h.G.S.stats.signals, "배전함에서 멀면 신호도 없다");
            h.Near(Na, "junction"); h.Cmd(Na, "Judge", 1);
            Assert.AreEqual(0, h.G.S.stats.signals, "4라운드 전 안전 판정(무시)은 신호가 없다");

            int from = h.Events.Count;
            h.Cmd(Na, "Judge", 0);
            var sigs = new List<string>();
            int lastSignalAt = -1, judgeAt = -1, resetAt = -1;
            for (int i = from; i < h.Events.Count; i++)
            {
                var e = h.Events[i];
                if (e.type == "Signal") { Assert.AreEqual(Na, e.slot, "신호는 내비 슬롯으로"); sigs.Add(e.s); lastSignalAt = i; }
                else if (e.type == "Judge" && e.a == 0) judgeAt = i;
                else if (e.type == "RoundReset") resetAt = i;
            }
            CollectionAssert.AreEqual(new[] { "DANGER", "NO" }, sigs, "위험 → 아니오 순서로 몸짓 카드");
            Assert.Greater(judgeAt, lastSignalAt, "카드 두 장이 Judge 이벤트보다 먼저");
            Assert.Greater(resetAt, judgeAt);
            Assert.AreEqual(2, h.G.S.stats.signals, "통계 signals 에 2 만 더해진다(봇 중복 없음)");
            Assert.AreEqual(1, st.gate2Attempts); Assert.AreEqual(0, st.round);
            Assert.AreEqual(0, h.G.S.stats.wrongInputs, "위험 재시도는 오답이 아니다");

            // round==0 에서 '위험' 은 무시 — 신호도 없다
            h.Cmd(Na, "Judge", 0);
            Assert.AreEqual(2, h.G.S.stats.signals, "round==0 에서 Judge 0 은 신호를 내지 않는다");
            Assert.AreEqual(1, st.gate2Attempts);
        }

        // ------------------------------------------------------------------ 최종 집결(QA test-T13)

        [Test]
        public void Gather_NeedsAllFourInsideForHoldTime_LeavingResets()
        {
            var h = new Headless();
            h.ToStage();
            h.BriefAll();
            h.SolveGate1();
            h.SolveGate2();
            var st = h.St;
            Assert.Less(st.allInSince, 0f);

            void Inside(int n) { for (int i = 0; i < 4; i++) h.Pos[i] = i < n ? Stage1Logic.GatherCenter(i, st, h.G.Clock) : h.G.Map.spawns[i]; }

            // 3명만 → 1.2초 지나도 클리어·유지 시작 없음
            h.Run(1.2f, 0.05f, () => Inside(3));
            Assert.IsFalse(st.cleared, "3명으로는 클리어되지 않는다");
            Assert.Less(st.allInSince, 0f, "3명으로는 유지 시간이 시작되지 않는다");
            Assert.AreEqual(3, h.G.InGatherCount());

            // 4명 → 유지 시작, 0.3초 뒤 아직 미클리어 → 한 명 이탈 → 리셋
            h.Run(0.3f, 0.05f, () => Inside(4));
            Assert.GreaterOrEqual(st.allInSince, 0f, "4명이 모이면 유지 시간 시작");
            Assert.IsFalse(st.cleared, "0.6초 전에는 클리어되지 않는다");
            h.Run(0.1f, 0.05f, () => Inside(3));
            Assert.Less(st.allInSince, 0f, "한 명이 나가면 유지 시간 리셋");
            Assert.IsFalse(st.cleared);

            // 다시 4명 0.6초 → 클리어 → 3.2초 뒤 클리어 컷씬
            h.Run(Stage1Logic.AllInHold + 0.15f, 0.05f, () => Inside(4));
            Assert.IsTrue(st.cleared, "4명 0.6초 동시 유지 → 클리어");
            Assert.AreEqual(0, st.moveMode); Assert.AreEqual(1, h.Count("Clear"));
            Assert.AreEqual(st.clearClock - st.startClock, h.G.S.stats.playSeconds, 0.01f, "playSeconds = 타이머 시작 → 클리어");
            Assert.GreaterOrEqual(st.clearClock - st.allInSince, Stage1Logic.AllInHold - 0.001f, "클리어는 유지 시간을 채운 뒤");
            h.Run(GameLogic.ClearDelay + 0.1f);
            Assert.AreEqual(Phase.StageClear, h.G.S.phase);
            Assert.AreEqual(StoryData.ClearId(1), h.G.S.cutsceneId);
            h.Cmd(Op, "Skip");
            Assert.AreEqual(Phase.Result, h.G.S.phase);
            h.Cmd(Op, "Again");
            Assert.AreEqual(Phase.Lobby, h.G.S.phase);
            Assert.AreEqual(-1, h.G.S.hostControlSlot);
        }
    }
}
