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
    /// 혼자 테스트(호스트 + 봇 3명)로 STRANGER Stage 1 유저 플로우 전체를 헤드리스로 돌린다(봇 수동 = 테스트가 네 역할을 직접 조종).
    /// 로고 → 타이틀 → 프롤로그 → 스테이지 선택 → 매칭 → 로비 → 이름 → 준비 → 오프닝 → 관문 1 → 위험 구역 → 시간 초과 → 재시도(관문 2부터) → 관문 2 → 최종 집결 → 클리어 → 결과 → 로비 → 종료.
    /// </summary>
    public class FullFlowTests
    {
        [UnityTearDown]
        public IEnumerator Down() => Flow.Teardown();

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator SoloFlow_RunsThroughStage1()
        {
            yield return Flow.Boot();

            // ---------------- 채팅 가시성 규칙(순수 함수): 나 자신 / 통역이 보는 것 / 통역이 쓴 것은 읽히고, 그 외는 ???.
            Assert.IsTrue(Comms.CanUnderstand(0, 0), "자기 채팅은 읽힌다");
            Assert.IsTrue(Comms.CanUnderstand(1, 3), "통역은 모두의 채팅을 읽는다");
            Assert.IsTrue(Comms.CanUnderstand(3, 1), "통역이 쓴 채팅은 모두 읽는다");
            Assert.IsFalse(Comms.CanUnderstand(0, 3), "조작자에게 관측자의 채팅은 외국어다");
            Assert.IsFalse(Comms.CanUnderstand(2, 0), "내비게이터에게 조작자의 채팅은 외국어다");

            // ---------------- 로컬 화면 전이(접속 전): 화면 키가 ScreenManager 에 반영되는지 화면 루트 오브젝트로 확인
            yield return Flow.ToLocal(LocalScreen.Logo, "LogoScreen");
            yield return Flow.ToLocal(LocalScreen.Title, "TitleScreen");
            yield return Flow.ToLocal(LocalScreen.Prologue, "PrologueScreen");
            yield return Flow.ToLocal(LocalScreen.StageSelect, "StageSelectScreen");
            yield return Flow.ToLocal(LocalScreen.Match, "MatchScreen");

            // ---------------- 매칭 → 로비 → 이름 → 준비 → 오프닝 → 플레이
            yield return Flow.StartSolo(botsAuto: false);
            yield return Flow.LobbyToStage("테스터");
            Assert.AreEqual(1, Session.Stage);
            var firstAvatars = new System.Collections.Generic.List<AvatarNet>(Session.Avatars.Values);
            Assert.IsTrue(Session.St.timed && Session.St.timeLimit > 0f, "Stage 1 은 침수 타이머가 있어야 함");
            Assert.IsTrue(Stage1Logic.ValidateLadder(Session.St), "생성된 사다리(배전함)가 규칙을 만족해야 함");
            Assert.AreEqual(Session.St.ladderSafe + 1, Stage1Logic.SafeLine(Session.St), "안전 라인 번호 = ladderSafe + 1 (v2 D2)");

            // ---------------- 지름길 통로(v2 D7, 메인 맵 그림 배치): 연못정원방(위험 구역) ↔ 관리실(STAFF 복도) 문. 관문 1 전에는 이 지름길이 관리실로 가는 유일한 길이다
            {
                var map = Session.Map;
                Assert.AreEqual(StageMaps.RoomCount + 5 + StageMaps.ShortcutDoors.Length, map.open.Count, "통행 영역 = 방 6 + 문 5 + 지름길");
                Vector2 from = StageMaps.GardenInside;             // 연못정원방 안
                Vector2 to = Flow.Approach("board");               // 관리실 현황판 앞
                Assert.IsTrue(map.Walkable(StageMaps.ShortcutDoors[0].center, StageMaps.AvatarRadius), "지름길 문(연못정원방 ↔ 관리실) 안은 걸을 수 있어야 함");
                var direct = map.FindPath(from, to, StageMaps.AvatarRadius);
                Assert.IsNotNull(direct, "연못정원방 → 관리실 경로");
                Assert.IsTrue(direct.Exists(p => StageMaps.ShortcutDoors[0].Contains(p)), "최단 경로는 지름길 문을 지난다");
                var avoid = new System.Collections.Generic.List<Rect> { StageMaps.ShortcutDoors[0] };
                Assert.IsNull(map.FindPath(from, to, StageMaps.AvatarRadius, avoid), "관문 1 전: 지름길을 피하면 관리실로 갈 길이 없다(모자이크방 쪽 문이 잠김)");
            }

            // ---------------- 관문 1 전(QA test-T6): 모자이크방 문(LockedDoor3)은 solid — 걸어서도, 길찾기로도 못 들어간다(명세 4절)
            float rad = StageMaps.AvatarRadius;
            {
                var m0 = Session.Map;
                Assert.IsFalse(m0.Walkable(StageMaps.LockedDoor3.center, rad), "관문 1 전: 잠긴 문 가운데는 못 걷는다");
                Assert.IsFalse(m0.Walkable(StageMaps.LockedDoor3.center + new Vector2(0f, 0.6f), rad) || m0.Walkable(StageMaps.LockedDoor3.center + new Vector2(0f, -0.6f), rad), "관문 1 전: 문 위아래 틈으로도 못 지나간다");
                Assert.IsNull(m0.FindPath(m0.spawns[0], Flow.Approach("lever"), rad), "관문 1 전: 스폰 → 레버반 경로가 없어야 함");
            }

            // ---------------- 관문 1: 오답 페널티 확인 → 정답
            Assert.AreEqual(-1, Session.St.lastWrongDigit, "시작 시 오답 기록 없음(B2)");
            Assert.IsFalse(Session.St.pingNudged, "시작 시 핑 안내 없음(A3)");
            Assert.AreEqual(0, Session.St.timeWarned, "시작 시 남은 시간 경고 없음(C4)");
            yield return Flow.Gate1Manual();
            Assert.IsFalse(Session.St.pingNudged, "내비가 40초 안에 핑했으므로 핑 안내(A3)는 나오지 않는다");
            Assert.AreEqual(0, Session.St.timeWarned, "관문 1 동안 남은 시간은 6:00 위 — 경고 없음(C4)");

            // ---------------- 관문 1 뒤(QA test-T6): 맵 재생성(키 gate1Done) → 문 개방, 레버반까지 걸어갈 수 있다
            yield return Flow.Until(() => Session.Map != null && Session.Map.Walkable(StageMaps.LockedDoor3.center, rad), 3f, "관문 1 뒤 맵 재생성 → 모자이크방 문 개방");
            {
                // 관문 1 뒤: 지름길을 피해도 모자이크방 → STAFF 복도로 돌아가는 안전한 길이 생긴다(더 길다)
                var map1 = Session.Map; Vector2 from1 = StageMaps.GardenInside, to1 = Flow.Approach("board");
                var direct1 = map1.FindPath(from1, to1, rad);
                var detour1 = map1.FindPath(from1, to1, rad, new System.Collections.Generic.List<Rect> { StageMaps.ShortcutDoors[0] });
                Assert.IsNotNull(direct1, "관문 1 뒤 연못정원방 → 관리실 경로");
                Assert.IsNotNull(detour1, "관문 1 뒤: 지름길을 피해도(모자이크방 경유) 경로가 있어야 함");
                Assert.IsFalse(detour1.Exists(p => StageMaps.ShortcutDoors[0].Contains(p)), "avoid 사각형은 지나지 않는다");
                Assert.Greater(detour1.Count, direct1.Count, "우회 경로가 더 길다");
            }
            {
                var m1 = Session.Map;
                var toLever = m1.FindPath(m1.spawns[0], Flow.Approach("lever"), rad);
                Assert.IsNotNull(toLever, "관문 1 뒤: 스폰 → 레버반 경로가 있어야 함");
                Assert.IsTrue(toLever.Exists(p => StageMaps.DoorRect(StageMaps.RoomMosaic).Contains(p)), "관문 1 뒤 경로는 모자이크방 문을 지난다");
            }

            // ---------------- 위험 구역(5-5, QA test-T2): 방 4·5 침수 ≥70 → InDanger·속도 ×0.5·5초마다 경고·감점 없음
            {
                var logic = SessionNet.Instance.Logic;                      // 호스트 권한 상태(Session.St 는 JSON 사본)
                Assert.IsNotNull(logic, "호스트 Logic");
                float baseBefore = logic.S.st.floodBase[StageMaps.RoomAquarium];
                logic.S.st.floodBase[StageMaps.RoomAquarium] = 95f; logic.MarkDirty();
                yield return Flow.Until(() => Stage1Logic.IsDanger(Session.St, StageMaps.RoomAquarium, Session.Clock), 3f, "수족관방 위험 구역(브로드캐스트 반영)");
                Assert.IsFalse(Stage1Logic.IsDanger(Session.St, StageMaps.RoomAtrium, Session.Clock), "아트리움은 위험 구역이 아니다");

                var toastClocks = new System.Collections.Generic.List<float>();   // 수신 시각(서버 클록)
                System.Action<GameEvent> rec = e => { if (e.type == "Toast" && e.target == 3 && e.s.Contains("허리")) toastClocks.Add(Session.Clock); };
                Session.EventReceived += rec;
                try
                {
                    float penaltyBefore = Session.St.penalty;
                    Flow.Teleport(3, StageMaps.AquariumInside);                // 수족관방 안(메인 맵 그림 좌표)
                    yield return Flow.Until(() => Session.Avatars[3].InDanger, 3f, "관측자 아바타 InDanger");
                    Assert.AreEqual(AvatarNet.Speed * 0.5f, Session.Avatars[3].CurrentSpeed, 0.001f, "위험 구역 속도 ×0.5");
                    Assert.IsFalse(Session.Avatars[0].InDanger, "다른 방에 있는 조작자는 위험이 아니다");
                    yield return Flow.Until(() => toastClocks.Count >= 1, 3f, "위험 경고 토스트 1회");
                    yield return Flow.WaitReal(1f);
                    Assert.AreEqual(1, toastClocks.Count, "경고는 매 프레임 쏟아지지 않는다(1초 안에 1회)");
                    yield return Flow.Until(() => toastClocks.Count >= 2, Stage1Logic.DangerToastEvery + 3f, "위험 경고 토스트 2회");
                    Assert.AreEqual(2, toastClocks.Count, "5초 간격이면 두 번째까지만");
                    Assert.GreaterOrEqual(toastClocks[1] - toastClocks[0], Stage1Logic.DangerToastEvery - 0.5f, "경고 간격은 5초(서버 클록, 프레임 히치 여유 0.5초)");
                    Assert.AreEqual(penaltyBefore, Session.St.penalty, 0.01f, "위험 구역은 감점이 없다");
                    Flow.Teleport(3, Session.Map.spawns[3]); yield return null; yield return null;
                    Assert.IsFalse(Session.Avatars[3].InDanger, "빠져나오면 해제");
                    Assert.AreEqual(AvatarNet.Speed, Session.Avatars[3].CurrentSpeed, 0.001f, "속도 원복");
                }
                finally { Session.EventReceived -= rec; }
                logic.S.st.floodBase[StageMaps.RoomAquarium] = baseBefore; logic.MarkDirty();   // 뒤 흐름에 영향 없게 복원
            }

            // ---------------- 시간 초과 → 실패 → 재시도(체크포인트 1): 튜토리얼 없이 타이머 즉시 시작(명세 14-1 D1(2), QA test-T1)
            yield return Flow.RetrySkipsTutorial();
            Assert.IsTrue(Session.St.gate1Done && Session.St.step == 1 && Session.St.checkpoint == 1, "재시도는 관문 2부터");
            Assert.AreEqual(0, Session.St.timeWarned, "재시도는 새 StageState — 남은 시간 경고 비트 초기화(C4)");
            Assert.IsFalse(Session.St.pingNudged, "재시도(관문 2부터)에는 관문 1 핑 안내가 없다(A3)");
            Assert.AreEqual(4, Session.Avatars.Count, "재시도 뒤에도 아바타 4개");
            Assert.IsTrue(Session.Map.Walkable(StageMaps.LockedDoor3.center, rad), "재시도(체크포인트 1) 맵도 모자이크방 문이 열려 있다");

            // ---------------- 관문 2: 틀린 레버 1개 포함 4라운드 → 안전 오판 → 페널티·리셋 → 4라운드 정답 → 안전 → step 2
            yield return Flow.Gate2Manual(wrongFirst: true);
            Assert.IsTrue(Stage1Logic.ValidateLadder(Session.St), "리셋 뒤 다시 생성된 사다리도 규칙을 만족해야 함");
            Flow.AssertGatherPanelClearOfShutter();

            // ---------------- 최종 집결: 3명·이탈로는 안 됨(QA test-T13) → 4명 0.6초 → 클리어
            yield return Flow.GatherNegative();
            yield return Flow.GatherAll();
            yield return Flow.Until(() => Session.St.cleared, 2f, "Stage 1 클리어");
            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "클리어 컷씬");
            Assert.AreEqual("s1_clear", Session.State.cutsceneId, "클리어 컷씬 id");

            // ---------------- 결과 → 로비 → 종료
            yield return Flow.SkipCutscene(Phase.Result, -1, "결과 화면");
            // ---------------- 결과 통계(QA test-T9): 정확한 값 + 결과 화면 표시
            {
                var stats = Session.State.stats;
                var st = Session.St;
                Assert.AreEqual(10, stats.chats, "채팅 = 홀짝 1 + 점자(틀린 패스 4 + 위험 재시도 전 1 + 정답 패스 4)");
                Assert.AreEqual(10, stats.cards, "카드 = 숫자 카드 1 + 방향 카드(4 + 1 + 4) — 거부된 카드는 세지 않는다");
                Assert.AreEqual(2, stats.wrongInputs, "오답 = 관문 1 오답 1 + 관문 2 안전 오판 1(위험 재시도·빈 입력은 오답이 아니다)");
                Assert.AreEqual(2, stats.signals, "봇 수동 흐름은 도넛 신호를 보내지 않는다 — 내비의 '위험 — 처음부터'(Judge 0) 때 서버가 대신 낸 위험·아니오 카드 2장만(D-A5)");
                Assert.AreEqual(1, stats.retries, "시간 초과 재시도 1회");
                Assert.AreEqual(st.retries, stats.retries, "Stats.retries 와 StageState.retries 는 같아야 함(결과 화면은 st.retries 표시)");
                float lastSegment = st.clearClock - st.startClock;
                Assert.Greater(lastSegment, 0f, "재시도 뒤 구간 시간");
                Assert.Greater(stats.playSeconds, lastSegment, "playSeconds = 실패한 구간 + 재시도 구간(누적)");
                Assert.Less(stats.playSeconds - lastSegment, 120f, "실패한 첫 구간은 관문 1 뒤 잠깐(2분 미만)이어야 함");
                Assert.AreEqual("2회", Flow.StatValue("오답 횟수"), "결과 화면 오답 표시");
                Assert.AreEqual("10장", Flow.StatValue("보낸 카드"), "결과 화면 카드 표시");
                Assert.AreEqual("10줄", Flow.StatValue("채팅"), "결과 화면 채팅 표시");
                Assert.AreEqual("1회", Flow.StatValue("재시도"), "결과 화면 재시도 표시");
                Assert.AreEqual(ResultScreen.FormatTime(stats.playSeconds), Flow.StatValue("총 시간"), "결과 화면 총 시간 표시");
            }
            Session.Send("Again");
            yield return Flow.Until(() => Session.State.phase == Phase.Lobby, 5f, "로비 복귀");
            Assert.IsFalse(Session.State.players[0].ready, "로비로 돌아오면 준비가 해제되어야 함");
            Assert.AreEqual(Session.MySlot, Session.LocalSlot, "로비로 돌아오면 조종 슬롯은 내 자리(0)로 돌아와야 함(게임 끝에 내비 봇 자리를 보고 있었다)");
            Assert.AreEqual(-1, Session.State.hostControlSlot, "로비로 돌아오면 서버의 hostControlSlot 이 풀려야 함");

            // ---------------- 한 번 더: 로비 → 이름 → 준비 → 오프닝 → 플레이 재진입(옛 아바타 정리 + 새 아바타 4개 스폰)
            yield return Flow.LobbyToStage("테스터");
            Assert.AreEqual(4, Session.Avatars.Count, "2판 아바타 4개");
            foreach (var kv in Session.Avatars)
            {
                Assert.IsTrue(kv.Value != null && kv.Value.IsSpawned, $"2판 아바타 {kv.Key} 스폰 상태");
                Assert.IsFalse(firstAvatars.Contains(kv.Value), $"2판 아바타 {kv.Key} 는 새로 스폰된 것이어야 함");
            }
            foreach (var av in firstAvatars) Assert.IsTrue(av == null || !av.IsSpawned, "1판 아바타는 모두 디스폰되어야 함");

            NetHub.Instance.Shutdown();
            yield return Flow.Until(() => !Session.Connected, 3f, "접속 종료");
            yield return Flow.Until(() => LocalNav.Screen == LocalScreen.StageSelect, 3f, "접속 종료 후 스테이지 선택 화면으로 복귀");
        }
    }
}
