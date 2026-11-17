using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Paradox.Client;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.TestTools;

namespace Paradox.Tests
{
    /// <summary>
    /// 에디터가 방(Photon Shared 모드, 실제 클라우드 접속)을 만들고, 실제 클라이언트 빌드(exe)를 자동 참가 모드
    /// (<c>PARADOX_AUTOJOIN=&lt;방 코드&gt;</c>)로 띄워 온라인 경로 전체를 검증한다: 참가 → 슬롯 배정 → 이름/준비 → 아바타 스폰(클라 소유) → 위치 동기화 → Brief.
    /// 환경변수 <c>PARADOX_CLIENT_EXE</c>(build_client.ps1 이 만든 exe)가 없으면 건너뛴다.
    /// 로그: <c>PARADOX_LOG_DIR</c>(없으면 exe 폴더)에 client_player.log / client_autojoin.log.
    /// </summary>
    public class OnlineClientTests
    {
        private Process _client;

        private static string Exe => Environment.GetEnvironmentVariable("PARADOX_CLIENT_EXE");

        private static string LogDir
        {
            get
            {
                string d = Environment.GetEnvironmentVariable("PARADOX_LOG_DIR");
                if (string.IsNullOrEmpty(d)) d = Path.GetDirectoryName(Exe);
                return d;
            }
        }

        [UnityTearDown]
        public IEnumerator Down()
        {
            KillClient();
            yield return Flow.Teardown();
        }

        private void KillClient()
        {
            try
            {
                if (_client != null && !_client.HasExited) { _client.Kill(); _client.WaitForExit(5000); }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[LanClient] 클라 프로세스 종료 실패: " + e.Message); }
            _client = null;
        }

        /// <summary>호스트가 아닌 사람 플레이어의 슬롯(없으면 -1).</summary>
        private static int OtherHuman(long hostClientId)
        {
            var S = Session.State;
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                if (p.present && !p.isBot && p.clientId != hostClientId) return i;
            }
            return -1;
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator RealClient_Joins_SpawnsAvatar_SyncsMovement_AndReadies()
        {
            string exe = Exe;
            if (string.IsNullOrEmpty(exe)) Assert.Ignore("PARADOX_CLIENT_EXE 환경변수가 없어 건너뜀 (build_client.ps1 로 빌드한 Paradox.exe 경로를 넣는다)");
            Assert.IsTrue(File.Exists(exe), $"클라이언트 exe 없음: {exe}");
            Directory.CreateDirectory(LogDir);
            string playerLog = Path.Combine(LogDir, "client_player.log");
            string autoLog = Path.Combine(LogDir, "client_autojoin.log");
            if (File.Exists(autoLog)) File.Delete(autoLog);

            // 호스트(에디터) — 봇 없이 방만 연다
            yield return Flow.Boot();
            Assert.IsTrue(NetHub.Instance.StartHost(false), "방 만들기 시작 실패");
            yield return Flow.Until(() => Session.Connected && Session.State.phase == Phase.Lobby && Session.MySlot == 0 && !string.IsNullOrEmpty(NetHub.Instance.RoomCode), 40f,
                $"방장 로비(0번 자리) + 방 코드 — Photon 접속 문제면 status='{NetHub.Instance.Status}' 와 로그의 [NetHub] 줄 확인");
            string roomCode = NetHub.Instance.RoomCode;
            UnityEngine.Debug.Log($"[Online] 방 코드 {roomCode}");
            Assert.IsTrue(NetHub.Instance.IsAuthority, "방을 만든 쪽이 권한자(공유 모드 마스터)");
            long hostClient = Session.MyClientId;
            Assert.AreEqual(1, Session.State.PresentCount, "봇 없이 열었으니 호스트 혼자");

            // 클라이언트 exe 실행(자동 참가)
            var psi = new ProcessStartInfo(exe, $"-batchmode -nographics -logFile \"{playerLog}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe),
            };
            psi.EnvironmentVariables[GameApp.AutoJoinEnv] = roomCode;
            psi.EnvironmentVariables[GameApp.AutoJoinLogEnv] = autoLog;
            _client = Process.Start(psi);
            Assert.IsNotNull(_client, "클라이언트 프로세스 시작 실패");
            UnityEngine.Debug.Log($"[LanClient] 클라이언트 시작 pid={_client.Id} log={playerLog}");

            // 1) 참가: present 2 이상, 두 번째 사람의 clientId != 호스트
            int slot = -1;
            yield return Flow.Until(() => { slot = OtherHuman(hostClient); return slot >= 0 && Session.State.PresentCount >= 2; }, 60f,
                "클라이언트 참가(present ≥ 2, clientId ≠ 호스트) — 실패하면 client_player.log / client_autojoin.log 확인");
            var joined = Session.State.players[slot];
            Assert.AreNotEqual(hostClient, joined.clientId, "클라이언트 clientId 는 호스트와 달라야 함");
            Assert.IsFalse(joined.isBot, "참가자는 봇이 아니다");
            Assert.AreEqual(2, NetHub.Instance.PlayerCount, "Fusion 접속 수 = 방장 + 클라");
            UnityEngine.Debug.Log($"[LanClient] 클라이언트 슬롯 {slot} clientId {joined.clientId}");

            // 2) 호스트: 나머지를 봇으로 채우고 시작 → 클라가 Name/Ready 를 보내 진행되는지
            Session.Send("FillBots");
            yield return Flow.Until(() => Session.State.PresentCount == 4, 5f, "빈 자리 봇 채우기");
            Assert.IsFalse(Session.State.players[slot].isBot, "봇 채우기가 클라이언트 자리를 덮으면 안 됨");
            Session.Send("StartGame");
            yield return Flow.Until(() => Session.State.phase == Phase.NameSetup, 5f, "이름 설정 단계");
            yield return Flow.Until(() => Session.State.players[slot].name == GameApp.AutoJoinName && Session.State.players[slot].ready, 20f,
                $"클라이언트 Name('{GameApp.AutoJoinName}') + Ready 수신(CommandRpc → GameLogic.Apply)");
            Session.Send("Name", 0, 0, "호스트");
            Session.Send("Ready", 1);
            yield return Flow.Until(() => Session.State.phase == Phase.Ready || Session.State.phase == Phase.StageIntro, 10f, "전원 준비 → Ready");
            yield return Flow.Until(() => Session.State.phase == Phase.StageIntro, 10f, "오프닝 컷씬(서버 자동 전환)");

            // 3) 아바타: 클라 슬롯의 아바타가 Session.Avatars 에 있고 활성이며 소유자가 클라
            yield return Flow.Until(() => Session.Avatars.Count == 4 && Session.Avatars.ContainsKey(slot), 10f, "아바타 4개 스폰(호스트 시점)");
            var av = Session.Avatars[slot];
            Assert.IsNotNull(av, "클라 슬롯 아바타");
            Assert.IsTrue(av.gameObject.activeInHierarchy, "클라 슬롯 아바타 GameObject 가 활성이어야 함");
            Assert.IsTrue(av.IsSpawned, "클라 슬롯 아바타가 스폰 상태");
            Assert.AreEqual((int)joined.clientId, av.OwnerId, "아바타 StateAuthority = 클라이언트");
            Assert.IsFalse(av.IsOwner, "호스트 프로세스에서는 클라 아바타의 소유자가 아니다");

            // 4) 플레이: 스폰 텔레포트(클라가 Teleport 이벤트를 처리해 아트리움으로) → 3초마다 움직인 위치가 호스트에 동기화
            yield return Flow.SkipCutscene(Phase.StagePlay, 1, "Stage 1 플레이");
            yield return Flow.Until(() => Session.Map != null && Session.RoomOf(slot) == StageMaps.RoomAtrium, 20f,
                "클라 아바타가 아트리움 스폰 자리로(클라이언트가 Teleport 이벤트를 처리하고 NetworkTransform 이 위치를 보냄)");
            yield return Flow.WaitReal(1.0f);   // 텔레포트 보간이 가라앉을 시간
            Vector2 p0 = Session.PosOf(slot).Value;
            yield return Flow.WaitReal(7.0f);   // AutoMoveInterval(3초) × 2 이상
            Vector2 p1 = Session.PosOf(slot).Value;
            UnityEngine.Debug.Log($"[LanClient] 클라 아바타 위치 {p0} → {p1} (거리 {Vector2.Distance(p0, p1):0.00})");
            Assert.Greater(Vector2.Distance(p0, p1), 0.3f, "클라이언트가 움직인 위치가 호스트에 동기화되어야 함(NetworkTransform)");
            Assert.AreEqual(StageMaps.RoomAtrium, Session.RoomOf(slot), "자동 이동은 아트리움 안에서만");

            // 5) 브리핑: 클라가 Brief 를 보내 briefDone[slot]
            yield return Flow.Until(() => Session.St.briefDone != null && Session.St.briefDone[slot], 15f, "클라이언트 Brief 수신(briefDone)");
            Assert.IsFalse(_client.HasExited, "클라이언트 프로세스가 도중에 종료됨 — client_player.log 확인");
        }

        /// <summary>없는 방 코드로 참가하면 새 방이 만들어지지 않고 "방을 찾지 못했어요" 로 실패한다(매칭 화면에 남는다). 코드 형식 오류도 즉시 거절.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator UnknownRoomCode_FailsWithMessage()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;   // Photon 이 "Game does not exist" 를 오류 로그로 남기는 건 예상된 동작
            yield return Flow.Boot();
            Assert.IsFalse(NetHub.Instance.StartClient("A1"), "너무 짧은 코드는 시작도 안 함");
            Assert.IsFalse(NetHub.Instance.IsRunning);
            string code = "0Q" + UnityEngine.Random.Range(1000, 9999);   // 리전 미지정('0') + 존재할 수 없는 이름
            Assert.IsTrue(NetHub.Instance.StartClient(code), "형식이 맞는 코드는 접속 시도를 시작");
            Assert.IsTrue(NetHub.Instance.IsRunning, "접속 시도 중");
            yield return Flow.Until(() => !NetHub.Instance.IsRunning, 40f, "접속 실패로 돌아옴 — Photon 접속 문제면 로그의 [NetHub] 줄 확인");
            Assert.IsTrue(NetHub.Instance.LastDisconnectWasConnectFailure, "접속 자체의 실패로 표시");
            Assert.AreEqual(NetHub.ConnectFailedMessage, NetHub.Instance.Status);
            Assert.IsFalse(Session.Connected);
            Assert.AreEqual(LocalScreen.Match, LocalNav.Screen, "매칭 화면에 남는다");
        }
    }
}
