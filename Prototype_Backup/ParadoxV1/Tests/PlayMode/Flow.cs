using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paradox.Tests
{
    /// <summary>화면 캡처(스크린샷 투어용). 환경변수 PARADOX_SHOTS 에 폴더가 있을 때만 동작한다.</summary>
    public static class Shots
    {
        public static string Dir => Environment.GetEnvironmentVariable("PARADOX_SHOTS");
        public static bool Enabled => !string.IsNullOrEmpty(Dir);
        public const int W = 1600, H = 900;

        public static IEnumerator Capture(string name)
        {
            if (!Enabled) yield break;
            Directory.CreateDirectory(Dir);
            yield return null;
            yield return null;

            var cam = Camera.main;
            var canvasGo = GameObject.Find("UIRootCanvas");
            var canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
            Assert.IsNotNull(cam, "카메라 없음");
            Assert.IsNotNull(canvas, "UI 캔버스 없음");

            var rt = new RenderTexture(W, H, 24);
            var prevTarget = cam.targetTexture;
            var prevMode = canvas.renderMode;
            int prevOrder = canvas.sortingOrder;
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 30000; // 실제 플레이(오버레이)처럼 UI가 월드(안개 등) 위에 오도록
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());

            cam.targetTexture = prevTarget;
            canvas.renderMode = prevMode;
            canvas.worldCamera = null;
            canvas.sortingOrder = prevOrder;
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            yield return null;
        }
    }

    /// <summary>
    /// 혼자 테스트(호스트 + 봇 3명) 모드로 유저 플로우를 조작하는 공용 드라이버.
    /// shot 콜백이 있으면 중요한 순간마다 화면을 캡처한다.
    /// </summary>
    public static class Flow
    {
        public static IEnumerator Until(Func<bool> cond, float timeout, string what)
        {
            // 실제 경과 시간으로 잰다(프레임이 느려져 deltaTime이 잘려도 시간 초과가 늘어나지 않게)
            float start = Time.realtimeSinceStartup;
            float last = start;
            int frames = 0;
            while (!cond())
            {
                float now = Time.realtimeSinceStartup;
                if (now - last > 1.5f) Debug.LogWarning($"[Flow] 느린 프레임 {now - last:0.0}초 (대기: {what}, {frames}프레임)");
                last = now;
                float elapsed = now - start;
                if (elapsed > timeout)
                    Assert.Fail($"시간 초과: {what} (phase={Session.State.phase}, stage={Session.Stage}, step={Session.St.step}, status='{Session.St.status}', {Positions()}, {frames}프레임/{elapsed:0.0}초)");
                frames++;
                yield return null;
            }
            if (frames > 0) Debug.Log($"[Flow] 완료: {what} ({frames}프레임/{Time.realtimeSinceStartup - start:0.0}초)");
        }

        public static string Positions()
        {
            var sb = new System.Text.StringBuilder("pos:");
            for (int i = 0; i < 4; i++)
            {
                var p = Session.PosOf(i);
                sb.Append(p == null ? $" [{i}]?" : $" [{i}]({p.Value.x:0.0},{p.Value.y:0.0})");
            }
            return sb.ToString();
        }

        /// <summary>테스트가 실패하더라도 다음 테스트가 깨끗한 상태에서 시작하도록 세션을 끝낸다.</summary>
        public static IEnumerator Teardown()
        {
            if (NetHub.Instance != null && NetHub.Instance.IsRunning) NetHub.Instance.Shutdown();
            yield return null;
            yield return null;
        }

        public static void As(int slot)
        {
            Session.SetLocalSlot(slot);
            Assert.AreEqual(slot, Session.LocalSlot, $"슬롯 {slot} 조종 전환 실패");
        }

        public static void Teleport(int slot, Vector2 pos)
        {
            Assert.IsTrue(Session.Avatars.ContainsKey(slot), $"아바타 {slot} 없음");
            var av = Session.Avatars[slot];
            var nt = av.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null) nt.Teleport(new Vector3(pos.x, pos.y, 0f), Quaternion.identity, av.transform.localScale);
            av.transform.position = new Vector3(pos.x, pos.y, 0f);
            if (slot == Session.LocalSlot && GameApp.Instance != null && GameApp.Instance.Follow != null)
                GameApp.Instance.Follow.SnapToTarget();
        }

        public static void TeleportAllToZones(Vector2[] zones)
        {
            for (int i = 0; i < 4; i++) Teleport(i, zones[i]);
        }

        public static IEnumerator Shot(Func<string, IEnumerator> shot, string name)
        {
            if (shot != null) yield return shot(name);
        }

        /// <summary>네 역할 시점을 차례로 캡처한 뒤 원래 슬롯으로 돌아온다.</summary>
        public static IEnumerator RoleViews(Func<string, IEnumerator> shot, string prefix)
        {
            if (shot == null) yield break;
            int prev = Session.LocalSlot;
            for (int s = 0; s < 4; s++)
            {
                As(s);
                yield return shot($"{prefix}_{s}{Roles.Short(s)}");
            }
            As(prev);
        }

        public static IEnumerator SkipCutscene(Phase expectNext, int expectStage, string what)
        {
            Session.Send("Skip");
            yield return Until(() => Session.State.phase == expectNext && (expectStage < 0 || Session.Stage == expectStage), 8f, what);
        }

        public static IEnumerator Boot()
        {
            SceneManager.LoadScene("Paradox");
            yield return null;
            yield return null;
            Assert.IsNotNull(GameApp.Instance, "GameApp 없음");
            Assert.IsNotNull(NetHub.Instance, "NetHub 없음");
            Assert.GreaterOrEqual(GameApp.Instance.CharacterCount, 9, "캐릭터 프리팹 참조가 비어 있음");
        }

        /// <summary>botsAuto=false 이면 봇이 스스로 행동하지 않아 테스트가 네 역할을 직접 조작한다(결정적 검증용).</summary>
        public static IEnumerator StartSolo(bool botsAuto = false)
        {
            Assert.IsTrue(NetHub.Instance.StartHost(true), "호스트 시작 실패");
            yield return Until(() => Session.Connected && Session.State.phase == Phase.Lobby && Session.State.PresentCount == 4, 15f, "로비 + 봇 3명");
            Assert.AreEqual(0, Session.MySlot, "호스트는 0번(조작자) 자리");
            Assert.IsTrue(Session.State.players[1].isBot && Session.State.players[3].isBot);
            Session.Send("BotsAuto", botsAuto ? 1 : 0);
            yield return Until(() => Session.State.botsAuto == botsAuto, 3f, "봇 자동 설정");
        }

        /// <summary>발신 역할로 단어를 고르고 [보내기]를 누른다.</summary>
        public static IEnumerator SendWord(int senderSlot, string word)
        {
            As(senderSlot);
            foreach (var ch in word) Session.Send("Pick", 0, 0, ch.ToString());
            yield return null;
            Assert.AreEqual(word, Session.St.picked, "트레이에 글자가 담겨야 함");
            Session.Send("SendLetters");
            yield return null;
            Assert.AreEqual("", Session.St.picked, "보낸 뒤 트레이는 비어야 함");
        }

        public static IEnumerator LobbyToPrologue(int character, string name, Func<string, IEnumerator> shot)
        {
            Session.Send("StartGame");
            yield return Until(() => Session.State.phase == Phase.CharacterSelect, 5f, "캐릭터 선택 화면");
            yield return Shot(shot, "03_charselect_before");
            Session.Send("Char", character);
            Session.Send("Name", 0, 0, name);
            yield return null;
            Assert.AreEqual(character, Session.Me.character);
            Assert.AreEqual(name, Session.Me.name);
            yield return Shot(shot, "03_charselect_picked");
            Session.Send("Ready", 1);
            yield return Until(() => Session.State.phase == Phase.Prologue, 5f, "프롤로그");
            yield return Until(() => Session.Avatars.Count == 4, 8f, "아바타 4개 스폰");
        }

        // ------------------------------------------------------------------ Stage 1

        public static IEnumerator Stage1(Func<string, IEnumerator> shot)
        {
            Assert.IsNotNull(Session.Map, "Stage 1 맵 없음");
            yield return Shot(shot, "10_s1_rail_start");
            yield return Until(() => Session.St.step == 0 && Session.St.fork == 0, 20f, "첫 갈림길 도착(레일 이동)");
            yield return RoleViews(shot, "11_s1_fork0");

            for (int fork = 0; fork < 3; fork++)
            {
                var st = Session.St;
                Assert.AreEqual(fork, st.fork);
                string word = st.word;
                Assert.IsTrue(word.Length >= 2, "갈림길 단어 없음");

                if (fork == 0)
                {
                    As(3);
                    Session.Send("Signal", 0, -1, "WARN");
                    yield return null;
                    Assert.AreEqual(-1, Session.St.guardedCycle, "창 밖 경고는 무시되어야 함");
                }

                // 통역 → 내비게이터 → 조작자 릴레이
                yield return SendWord(1, word);
                Assert.AreEqual(word.Length, Session.St.inflight.Count, "글자가 전달 중이어야 함");
                if (fork == 0) yield return Shot(shot, "12_s1_interpreter_sent");

                As(2);
                yield return Until(() => Stage1Logic.RelayReady(Session.St, Session.Clock), 5f, "글자가 내비게이터에게 도착");
                if (fork == 0) yield return Shot(shot, "13_s1_navigator_incoming");
                for (int i = 0; i < word.Length; i++)
                {
                    yield return Until(() => Stage1Logic.RelayReady(Session.St, Session.Clock), 5f, "다음 글자 도착");
                    Session.Send("Relay");
                    yield return null;
                }
                yield return Until(() => Session.St.received[0] == word, 6f, "조작자에게 단어가 도착");

                if (fork == 0)
                {
                    // 관측자 경고: 창 안에서 파동을 막는다
                    As(3);
                    yield return Until(() => Stage1Logic.InGuardWindow(Session.St, Session.Clock), 12f, "경고등 최고조 창");
                    yield return Shot(shot, "14_s1_observer_window");
                    Session.Send("Signal", 0, -1, "WARN");
                    yield return null;
                    Assert.GreaterOrEqual(Session.St.guardedCycle, 0, "창 안 경고가 접수되어야 함");
                    yield return Until(() => Session.St.signalCut, 5f, "신호 차단 배너");
                    yield return Shot(shot, "15_s1_signal_cut");
                    yield return Until(() => !Session.St.signalCut, 5f, "신호 차단 해제");
                }

                As(0);
                if (fork == 0) yield return Shot(shot, "16_s1_operator_received");
                int wrong = 1 - Session.St.leverSide[fork];
                Session.Send("Lever", wrong);
                yield return null;
                Assert.AreEqual(0, Session.St.step, "잘못된 레버는 진행하지 않음");
                Session.Send("Lever", Session.St.leverSide[fork]);
                yield return Until(() => Session.St.step == 2, 3f, "올바른 레버 → 레일 이동");

                if (fork == 1)
                {
                    yield return Until(() => Session.St.step == 3, 20f, "장애물 도착");
                    As(0);
                    float t = 0f;
                    bool shotTaken = false;
                    while (Session.St.step == 3 && t < 30f)
                    {
                        Session.Send("Mash");
                        t += Time.deltaTime;
                        if (!shotTaken && Session.MeterA > 40f) { shotTaken = true; yield return Shot(shot, "17_s1_mash"); }
                        yield return null;
                    }
                    Assert.IsTrue(Session.St.obstacleCleared, "장애물이 치워져야 함");
                }

                if (fork < 2) yield return Until(() => Session.St.step == 0 && Session.St.fork == fork + 1, 25f, $"갈림길 {fork + 2} 도착");
            }

            yield return Until(() => Session.St.step == 4, 25f, "로비 도착");
            Assert.AreEqual(1, Session.St.moveMode, "로비는 자유 이동");
            yield return Shot(shot, "18_s1_lobby");
            TeleportAllToZones(Session.Map.zones);
            yield return Until(() => Session.St.cleared, 6f, "Stage 1 클리어");
            yield return Shot(shot, "19_s1_clear");
        }

        // ------------------------------------------------------------------ Stage 2

        public static IEnumerator Stage2(Func<string, IEnumerator> shot)
        {
            var map = Session.Map;
            Assert.IsNotNull(map);
            var st = Session.St;
            Assert.AreEqual(0, st.step);
            string word = st.word;
            yield return RoleViews(shot, "21_s2_gateroom");

            yield return SendWord(2, word);
            yield return Until(() => Session.St.received[0] == word, 8f, "펌프 모드 단어가 조작자에게 도착");

            bool opened = false;
            for (int attempt = 0; attempt < 4 && !opened; attempt++)
            {
                As(0);
                Teleport(0, map.Point("console") + new Vector2(0f, -0.9f));
                yield return null;
                Session.Send("Console", Session.St.requiredMode);
                yield return null;
                Assert.AreEqual(Session.St.requiredMode, Session.St.consoleMode);
                yield return Until(() => Stage2Logic.WavePhase(Session.St, Session.Clock) < 5f && Session.Clock >= Session.St.knockUntil, 15f, "파도 사이");
                Session.Send("Hold", 1);
                yield return Until(() => Session.St.holding, 2f, "콘솔 붙잡기");
                yield return Until(() => Session.MeterA >= 70f || !Session.St.holding, 6f, "방벽 70");
                if (!Session.St.holding) continue;
                if (attempt == 0) yield return Shot(shot, "22_s2_operator_holding");

                Teleport(3, map.Point("wheelA") + new Vector2(0f, -0.9f));
                Teleport(1, map.Point("wheelB") + new Vector2(0f, -0.9f));
                As(1);
                yield return Until(() => Stage2Logic.Current(Session.Clock) < 25f, 10f, "물살 약함");
                if (attempt == 0) yield return Shot(shot, "23_s2_interpreter_current_low");
                As(3); Session.Send("Pull");
                As(1); Session.Send("Pull");
                yield return null;
                yield return null;
                opened = Session.St.gatesOpen;
            }
            Assert.IsTrue(opened, "수문이 열려야 함");
            Assert.AreEqual(1, Session.St.step);
            As(2);
            yield return Shot(shot, "24_s2_navigator_route");

            float penaltyBefore = Session.St.penalty;
            Teleport(0, Session.Map.flooded.center);
            yield return Until(() => Session.St.penalty > penaltyBefore, 6f, "침수 갈래 페널티");

            TeleportAllToZones(Session.Map.zones);
            yield return Until(() => Session.St.cleared, 6f, "Stage 2 클리어");
        }

        // ------------------------------------------------------------------ Stage 3

        public static IEnumerator Stage3(Func<string, IEnumerator> shot)
        {
            var map = Session.Map;
            Assert.IsNotNull(map);
            var st = Session.St;
            int safe = st.safeSector;
            Assert.AreNotEqual(StageMaps.Stage3StartSector, safe);
            Assert.AreEqual(0, st.gas[safe]);
            Assert.IsFalse(st.blocked[safe]);
            Assert.Less(st.ppm[safe], Stage3Logic.PpmDanger);
            for (int i = 0; i < 12; i++)
            {
                if (i == safe || i == StageMaps.Stage3StartSector) continue;
                Assert.IsFalse(st.gas[i] == 0 && !st.blocked[i] && st.ppm[i] < Stage3Logic.PpmDanger, $"구역 {i}도 모든 조건을 만족함(정답이 둘)");
            }
            yield return RoleViews(shot, "31_s3_start");

            As(3); Session.Send("Mark", safe); yield return null;
            Assert.IsTrue(Session.St.markObs[safe]);
            As(2); Session.Send("Mark", safe); yield return null;
            Assert.IsTrue(Session.St.markNav[safe]);
            As(1); Session.Send("Mark", 0); yield return null;
            Assert.IsTrue(Session.St.markInt[0]);
            As(0); Session.Send("Mark", safe); yield return null;
            Assert.IsTrue(Session.St.markObs[safe] && Session.St.markNav[safe], "조작자는 표시를 바꿀 수 없음");
            yield return Shot(shot, "32_s3_board_marked_op");

            int wrongSector = -1;
            for (int i = 0; i < 12; i++)
                if (i != safe && i != StageMaps.Stage3StartSector && !Session.St.blocked[i]) { wrongSector = i; break; }
            if (wrongSector >= 0)
            {
                var wz = new Vector2[4];
                StageMaps.SectorZones(wrongSector, wz);
                yield return ConfirmIn(wrongSector);
                TeleportAllToZones(wz);
                yield return Until(() => Session.St.wrongConfirms >= 1, 6f, "틀린 구역 확인 페널티");
                Assert.IsFalse(Session.St.cleared);
            }

            var zones = new Vector2[4];
            StageMaps.SectorZones(safe, zones);
            yield return ConfirmIn(safe);
            As(1);
            yield return Shot(shot, "33_s3_interpreter_in_safe");
            TeleportAllToZones(zones);
            yield return Until(() => Session.St.cleared, 8f, "Stage 3 클리어");
        }

        public static IEnumerator ConfirmIn(int sector)
        {
            var map = Session.Map;
            As(3);
            Teleport(3, map.Point("confObs" + sector) + new Vector2(0f, -0.8f));
            yield return null;
            Session.Send("Confirm");
            yield return Until(() => Session.St.confirmObs && Session.St.confirmObsSector == sector, 4f, "관측자 확인");
            As(2);
            Teleport(2, map.Point("confNav" + sector) + new Vector2(0f, -0.8f));
            yield return null;
            Session.Send("Confirm");
            yield return Until(() => Session.St.confirmNav && Session.St.confirmNavSector == sector, 4f, "내비게이터 확인");
        }

        // ------------------------------------------------------------------ Stage 4

        public static IEnumerator Stage4(Func<string, IEnumerator> shot)
        {
            var map = Session.Map;
            Assert.IsNotNull(map);
            var st = Session.St;
            string word = st.word;
            Assert.IsTrue(st.broadcast.Contains(word), "방송에 암호가 있어야 함");
            Assert.Greater(st.fires.Count, 0, "불이 있어야 함");
            yield return RoleViews(shot, "41_s4_start");

            As(0);
            int chatsBefore = Session.State.stats.chats;
            Session.Send("Chat", 0, 0, "안녕");
            yield return null;
            Assert.AreEqual(chatsBefore, Session.State.stats.chats, "Stage 4 조작자 채팅은 막혀야 함");

            float penaltyBefore = Session.St.penalty;
            Teleport(1, Session.St.fires[0].V);
            yield return Until(() => Session.St.penalty > penaltyBefore, 6f, "화상 페널티");

            yield return SendWord(0, word);
            yield return Until(() => Session.St.received[1] == word && Session.St.received[2] == word && Session.St.received[3] == word, 8f, "친구 셋에게 암호 도착");

            for (int s = 1; s <= 3; s++)
            {
                As(s);
                Teleport(s, Session.Map.Point("dev" + s) + new Vector2(0f, -0.9f));
                yield return null;
                if (s == 1) yield return Shot(shot, "42_s4_friend_at_device");
                Session.Send("Operate");
                yield return Until(() => Session.St.device[s], 4f, $"장치 {s} 작동");
            }
            Assert.IsTrue(Session.St.devicesDone);
            Assert.AreEqual(0, Session.St.fires.Count, "장치 완료 후 불이 꺼져야 함");

            for (int s = 1; s <= 3; s++)
            {
                As(s);
                Teleport(s, Session.Map.Point("pad" + s) + new Vector2(-0.8f, 0f));
                yield return null;
                yield return null;
                if (s == 1) yield return Shot(shot, "43_s4_keypad");
                foreach (var ch in word) Session.Send("Key", 0, 0, ch.ToString());
                yield return null;
            }
            yield return Until(() => Session.St.codeDone, 4f, "셔터 암호 일치");
            As(0);
            yield return Shot(shot, "44_s4_operator_map");

            TeleportAllToZones(Session.Map.zones);
            yield return Until(() => Session.St.cleared, 6f, "Stage 4 클리어");
        }
    }
}
