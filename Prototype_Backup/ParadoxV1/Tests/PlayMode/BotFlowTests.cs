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
    /// 혼자 테스트를 "실제처럼": 사람은 조작자 한 명뿐이고 통역·내비게이터·관측자는 봇이 스스로 자기 역할을 수행한다.
    /// 조작자가 할 일(레버·연타·콘솔·이동·암호 전송)만 직접 하고 나머지는 봇에게 맡겨 네 스테이지가 끝까지 굴러가는지 본다.
    /// </summary>
    public class BotFlowTests
    {
        private const int Op = 0;

        [UnityTearDown]
        public IEnumerator Down() => Flow.Teardown();

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator SoloWithBots_PlaysLikeRealGame()
        {
            yield return Flow.Boot();
            yield return Flow.StartSolo(botsAuto: true);
            yield return Flow.LobbyToPrologue(2, "혼자", null);
            yield return Flow.SkipCutscene(Phase.StageIntro, 1, "Stage 1 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 1, "Stage 1 플레이");

            // ---------------- Stage 1: 봇이 글자를 보내고 중계하고 경고한다. 조작자는 레버만.
            for (int fork = 0; fork < 3; fork++)
            {
                int f = fork;
                yield return Flow.Until(() => Session.St.step == 0 && Session.St.fork == f, 45f, $"갈림길 {f + 1} 도착");
                yield return Flow.Until(() => Stage1Logic.CanPullLever(Session.St), 60f, $"봇 릴레이로 단어 도착 (갈림길 {f + 1})");
                string word = Session.St.received[Op];
                Assert.AreEqual(Session.St.word, word, "봇이 보낸 단어가 표지판 단어와 같아야 함");
                int side = Stage1Logic.LeverLabel(Session.St, f, 0) == word ? 0 : 1;
                Flow.As(Op);
                Session.Send("Lever", side);
                yield return Flow.Until(() => Session.St.step == 2, 5f, "레버 → 레일 이동");

                if (fork == 1)
                {
                    yield return Flow.Until(() => Session.St.step == 3, 30f, "장애물 도착");
                    float t = 0f;
                    while (Session.St.step == 3 && t < 40f)
                    {
                        Session.Send("Mash");
                        t += Time.deltaTime;
                        yield return null;
                    }
                    Assert.IsTrue(Session.St.obstacleCleared, "장애물이 치워져야 함");
                }
            }
            yield return Flow.Until(() => Session.St.step == 4, 40f, "로비 도착");
            Flow.Teleport(Op, Session.Map.zones[Op]);
            yield return Flow.Until(() => Session.St.cleared, 45f, "봇들이 자기 원으로 걸어와 Stage 1 클리어");

            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 1 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 2, "Stage 2 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 2, "Stage 2 플레이");

            // ---------------- Stage 2: 봇 내비가 단어를 보내고, 봇 통역·관측자가 수문을 당긴다. 조작자는 콘솔.
            yield return Flow.Until(() => Session.St.received[Op] == Session.St.word, 40f, "봇 내비게이터가 펌프 모드 전송");
            Flow.As(Op);
            Flow.Teleport(Op, Session.Map.Point("console") + new Vector2(0f, -0.9f));
            yield return null;
            Session.Send("Console", Session.St.requiredMode);
            yield return Flow.Until(() => Session.St.consoleMode == Session.St.requiredMode, 3f, "콘솔 모드");
            float t2 = 0f;
            while (!Session.St.gatesOpen && t2 < 120f)
            {
                var st = Session.St;
                float w = Stage2Logic.WavePhase(st, Session.Clock);
                if (st.holding && w >= Stage2Logic.WavePeriod - 1.6f) Session.Send("Hold", 0);
                else if (!st.holding && w < Stage2Logic.WavePeriod - 3.5f && Session.Clock >= st.knockUntil) Session.Send("Hold", 1);
                t2 += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(Session.St.gatesOpen, "봇 통역·관측자가 수문을 동시에 당겨야 함");
            Flow.Teleport(Op, Session.Map.zones[Op]);
            yield return Flow.Until(() => Session.St.cleared, 90f, "봇들이 상부 통로로 걸어와 Stage 2 클리어");

            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 2 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 3, "Stage 3 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 3, "Stage 3 플레이");

            // ---------------- Stage 3: 봇이 보드에 표시한다. 조작자가 정답 구역으로 이끌면 봇 관측자·내비가 확인 패널을 누른다.
            int safe = Session.St.safeSector;
            yield return Flow.Until(() => Session.St.markObs[safe] && Session.St.markNav[safe], 40f, "봇 관측자·내비의 보드 표시");
            Flow.Teleport(Op, Session.Map.sectors[safe].center);
            yield return null;
            // 진단: 봇이 확인 패널까지 길을 찾을 수 있는지 기록해 둔다
            foreach (int s in new[] { 2, 3 })
            {
                var from = Session.PosOf(s);
                var panel = Session.Map.Point((s == 3 ? "confObs" : "confNav") + safe) + new Vector2(0f, -0.8f);
                var path = from != null ? Session.Map.FindPath(from.Value, panel, StageMaps.AvatarRadius) : null;
                Debug.Log($"[BotFlow] Stage3 path slot{s} from {from} to {panel}: {(path == null ? "null" : path.Count + " points")}");
            }
            yield return Flow.Until(() => Session.St.confirmObs && Session.St.confirmObsSector == safe && Session.St.confirmNav && Session.St.confirmNavSector == safe, 90f, "봇 관측자·내비 확인");
            var zones = new Vector2[4];
            StageMaps.SectorZones(safe, zones);
            Flow.Teleport(Op, zones[Op]);
            yield return Flow.Until(() => Session.St.cleared, 60f, "봇들이 원으로 들어와 Stage 3 클리어");

            yield return Flow.Until(() => Session.State.phase == Phase.StageClear, 12f, "Stage 3 클리어 컷씬");
            yield return Flow.SkipCutscene(Phase.StageIntro, 4, "Stage 4 도입");
            yield return Flow.SkipCutscene(Phase.StagePlay, 4, "Stage 4 플레이");

            // ---------------- Stage 4: 조작자가 암호를 보내면 봇 친구들이 장치를 켜고 키패드를 친다.
            yield return Flow.SendWord(Op, Session.St.word);
            yield return Flow.Until(() => Session.St.received[1] == Session.St.word, 8f, "친구들에게 암호 도착");
            yield return Flow.Until(() => Session.St.devicesDone, 150f, "봇 친구들이 장치 3개 작동");
            yield return Flow.Until(() => Session.St.codeDone, 90f, "봇 친구들이 키패드 동시 입력");
            Flow.Teleport(Op, Session.Map.zones[Op]);
            yield return Flow.Until(() => Session.St.cleared, 60f, "Stage 4 클리어");

            yield return Flow.Until(() => Session.State.phase == Phase.Ending, 12f, "엔딩");
            yield return Flow.SkipCutscene(Phase.Result, -1, "결과 화면");
            NetHub.Instance.Shutdown();
            yield return null;
        }
    }
}
