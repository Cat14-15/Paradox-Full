using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Paradox.Client;
using UnityEngine;

namespace Paradox.Net
{
    /// <summary>
    /// 네트워크 진입점(Photon Fusion 2). 온라인은 <see cref="GameMode.Shared"/> — Photon 릴레이를 거치므로 학교·가정 NAT 에서도 접속된다.
    /// 방 코드 = 세션 이름(+ 리전 한 글자). 방을 만든 사람(공유 모드 마스터 클라이언트)이 "권한자(<see cref="IsAuthority"/>)"로
    /// 세션 오브젝트를 스폰하고 GameLogic 을 돌린다. 솔로("혼자 테스트")는 <see cref="GameMode.Single"/> — 네트워크 없이 로컬이며
    /// 같은 코드 경로를 타므로 자동 테스트는 Photon 서버 없이도 돈다.
    /// 마스터가 나가면(호스트 마이그레이션 미지원) 남은 사람은 "방장이 방을 닫았어요" 와 함께 로컬 화면으로 돌아간다.
    /// </summary>
    public class NetHub : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static NetHub Instance { get; private set; }

        public const int MaxPlayers = 4;
        /// <summary>접속(StartGame) 제한 시간(초). 넘기면 실패로 돌아온다.</summary>
        public const int ConnectAttempts = 20;

        public const string HostClosedReason = "방장이 방을 닫았어요";
        public const string ConnectFailedMessage = "그 방을 찾지 못했어요 — 방 코드를 다시 확인하세요";
        public const string OfflineMessage = "인터넷에 연결하지 못했어요 — 네트워크를 확인하고 다시 시도하세요";

        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int NameLength = 5;   // 방 코드 = 리전 1자 + 이름 5자 = 6자
        // 리전 토큰 <-> 방 코드 첫 글자. 방은 리전 안에서만 보이므로 코드에 리전을 실어 참가자가 같은 리전으로 접속하게 한다.
        private static readonly string[] RegionTokens = { "asia", "au", "cae", "cn", "eu", "hk", "in", "jp", "kr", "mx", "ru", "rue", "sa", "tr", "uae", "us", "usw", "ussc", "za" };
        private const string RegionChars = "ABCDEFGHJKLMNPQRSTU";   // RegionTokens 와 같은 순서

        public NetworkRunner Runner { get; private set; }

        public bool IsRunning => _active;
        public bool IsSolo { get; private set; }
        public string Status { get; private set; } = "";
        public string RoomCode { get; private set; } = "";
        public event Action<string> StatusChanged;
        public event Action Disconnected;
        public event Action<string> RoomCodeChanged;

        /// <summary>마지막 끊김이 "접속 자체가 실패"(방을 못 찾음 등)였는가. 매칭 화면이 스테이지 선택으로 튕기지 않고 남는 데 쓴다.</summary>
        public bool LastDisconnectWasConnectFailure { get; private set; }

        /// <summary>내가 방장(공유 모드 마스터) 또는 솔로인가 — 세션 오브젝트를 스폰하고 GameLogic 을 돌리는 쪽.</summary>
        public bool IsAuthority => _active && Runner != null && Runner.IsRunning && (Runner.IsSinglePlayer || Runner.IsSharedModeMasterClient);

        /// <summary>세션 오브젝트의 권한자(방장). SessionNet 이 스폰될 때 채워진다.</summary>
        public PlayerRef MasterPlayer { get; private set; }

        public int PlayerCount
        {
            get
            {
                int n = 0;
                if (Runner != null && Runner.IsRunning) foreach (var _ in Runner.ActivePlayers) n++;
                return n;
            }
        }

        public int LocalPlayerId => Runner != null ? Runner.LocalPlayer.PlayerId : -1;

        private bool _active;
        private bool _connected;       // 방 입장 완료(접속 실패와 끊김을 구분)
        private bool _wantBots;
        private bool _creating;
        private string _joinName;
        private int _retries;
        private NetworkObject _sessionPrefab, _avatarPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) { Teardown(); Instance = null; }
        }

        // ---------- 시작 ----------

        /// <summary>방 만들기. withBots 이면 솔로(봇 3명, 로컬). 방 코드는 접속이 끝나면 <see cref="RoomCode"/>/<see cref="RoomCodeChanged"/> 로 알린다(리전이 정해져야 코드가 완성된다).</summary>
        public bool StartHost(bool withBots)
        {
            if (_active) return false;
            _wantBots = withBots;
            _creating = true;
            IsSolo = withBots;
            _joinName = withBots ? "" : RandomName();
            SetRoomCode("");
            SetStatus(withBots ? "혼자 테스트 시작 중…" : "방을 만드는 중…");
            return Begin(withBots ? GameMode.Single : GameMode.Shared, _joinName, null);
        }

        /// <summary>방 코드로 참가. 코드 6자 = 리전 1자 + 이름 5자.</summary>
        public bool StartClient(string roomCode)
        {
            if (_active) return false;
            if (!TryParseCode(roomCode, out string name, out string region))
            {
                LastDisconnectWasConnectFailure = true;
                SetStatus("방 코드는 영문 대문자·숫자 6자예요");
                return false;
            }
            _wantBots = false;
            _creating = false;
            IsSolo = false;
            _joinName = name;
            SetRoomCode(Normalize(roomCode));
            SetStatus($"방 {RoomCode} 에 접속 중… (최대 {ConnectAttempts}초)");
            return Begin(GameMode.Shared, name, region);
        }

        /// <summary>방 코드 길이(리전 1자 + 이름 5자).</summary>
        public const int CodeLength = 6;

        public static string Normalize(string code) => (code ?? "").Trim().ToUpperInvariant();

        public static bool TryParseCode(string code, out string name, out string region)
        {
            name = null; region = null;
            code = Normalize(code);
            if (code.Length != CodeLength) return false;
            foreach (char c in code) if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            int i = RegionChars.IndexOf(code[0]);
            region = i >= 0 ? RegionTokens[i] : null;
            name = code.Substring(1);
            return true;
        }

        private static string RandomName()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < NameLength; i++) sb.Append(CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)]);
            return sb.ToString();
        }

        private static string BuildCode(string regionToken, string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string r = (regionToken ?? "").ToLowerInvariant();
            int cut = r.IndexOfAny(new[] { '/', ':' });
            if (cut >= 0) r = r.Substring(0, cut);
            int i = Array.IndexOf(RegionTokens, r);
            return (i >= 0 ? RegionChars[i] : '0') + name;
        }

        private bool Begin(GameMode mode, string sessionName, string regionToken)
        {
            _active = true;
            _connected = false;
            LastDisconnectWasConnectFailure = false;
            MasterPlayer = default;
            Session.Reset();

            var go = new GameObject("Runner");
            go.transform.SetParent(transform, false);
            var runner = go.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            runner.AddCallbacks(this);
            var scenes = go.AddComponent<NetworkSceneManagerDefault>();
            var provider = go.AddComponent<NetworkObjectProviderDefault>();
            Runner = runner;

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = mode == GameMode.Single ? null : sessionName,
                PlayerCount = mode == GameMode.Single ? 1 : MaxPlayers,
                SceneManager = scenes,
                ObjectProvider = provider,
                IsVisible = false,
                IsOpen = true,
                EnableClientSessionCreation = _creating,
                Address = NetAddress.Any(),
            };
            if (mode == GameMode.Shared && !string.IsNullOrEmpty(regionToken) && PhotonAppSettings.Global != null)
            {
                var settings = PhotonAppSettings.Global.AppSettings.GetCopy();
                settings.FixedRegion = regionToken;
                args.CustomPhotonAppSettings = settings;
            }
            StartCoroutine(StartRoutine(runner, args));
            return true;
        }

        private IEnumerator StartRoutine(NetworkRunner runner, StartGameArgs args)
        {
            Task<StartGameResult> task;
            try { task = runner.StartGame(args); }
            catch (Exception e) { Debug.LogWarning("[NetHub] StartGame 예외: " + e.Message); if (!TryCreateAgain()) Fail(OfflineMessage); yield break; }

            float t0 = Time.realtimeSinceStartup;
            while (!task.IsCompleted)
            {
                if (runner != Runner) yield break;   // 그 사이 Shutdown 됨
                if (Time.realtimeSinceStartup - t0 > ConnectAttempts) { if (!TryCreateAgain()) Fail(TimeoutMessage); yield break; }
                yield return null;
            }
            if (runner != Runner) yield break;
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning("[NetHub] StartGame 실패: " + (task.Exception != null ? task.Exception.GetBaseException().Message : "취소"));
                if (!TryCreateAgain()) Fail(OfflineMessage);
                yield break;
            }

            var res = task.Result;
            if (!res.Ok)
            {
                Debug.LogWarning($"[NetHub] StartGame 실패 reason={res.ShutdownReason} msg={res.ErrorMessage}");
                if (_creating && res.ShutdownReason == ShutdownReason.GameIdAlreadyExists && !IsSolo) { RetryNewName(); yield break; }
                // 서버 한도 · 방 없음 · 가득 참 · 진행 중은 다시 해도 같으니 바로 알린다. 그 밖의 방 만들기 실패는 한 번 더 자동으로
                bool final = res.ShutdownReason == ShutdownReason.MaxCcuReached || res.ShutdownReason == ShutdownReason.GameIsFull
                             || res.ShutdownReason == ShutdownReason.GameClosed || res.ShutdownReason == ShutdownReason.GameNotFound;
                if (!final && TryCreateAgain()) yield break;
                Fail(MessageFor(res.ShutdownReason));
                yield break;
            }

            if (runner.GameMode == GameMode.Shared)
            {
                string region = runner.SessionInfo.Region;
                bool master = runner.IsSharedModeMasterClient;
                Debug.Log($"[NetHub] 입장 완료 region='{region}' master={master} player={runner.LocalPlayer.PlayerId} session='{runner.SessionInfo.Name}'");
                if (_creating && !master) { RetryNewName(); yield break; }                 // 같은 이름의 남의 방에 들어왔다 — 다른 코드로 다시
                if (!_creating && master) { Fail(ConnectFailedMessage); yield break; }     // 없는 방이라 새로 만들어졌다 — 닫고 실패로
                if (_creating) SetRoomCode(BuildCode(region, _joinName));
            }
            _connected = true;
            _createRetried = false;

            if (runner.IsSinglePlayer || runner.IsSharedModeMasterClient)
            {
                if (_sessionPrefab == null) _sessionPrefab = Resources.Load<NetworkObject>("NetPrefabs/Session");
                if (_sessionPrefab == null) { Fail("세션 프리팹을 찾지 못했어요 (Resources/NetPrefabs/Session)"); yield break; }
                runner.Spawn(_sessionPrefab, Vector3.zero, Quaternion.identity);
                SetStatus(IsSolo ? "혼자 테스트 시작 (봇 3명)" : $"방을 열었습니다 — 방 코드 {RoomCode}. 친구들이 참가하면 자리가 채워집니다");
            }
            else
            {
                SetStatus("접속 성공 — 로비로 들어갑니다");
            }
        }

        private void RetryNewName()
        {
            if (++_retries > 5) { _retries = 0; Fail(OfflineMessage); return; }
            Teardown();
            _active = false;
            StartHost(false);
        }

        public const string TimeoutMessage = "서버 응답이 늦어요 — 인터넷을 확인하고 다시 시도하세요";

        /// <summary>방 만들기가 타임아웃 · 연결 오류로 실패했으면 새 코드로 한 번만 자동으로 다시 시도한다.</summary>
        private bool _createRetried;
        private bool TryCreateAgain()
        {
            if (!_creating || IsSolo || _createRetried) return false;
            _createRetried = true;
            Debug.LogWarning("[NetHub] 방 만들기 실패 — 한 번 더 시도");
            Teardown();
            _active = false;
            StartHost(false);
            SetStatus("방을 다시 만드는 중… (한 번 더 시도)");
            return true;
        }

        private static string MessageFor(ShutdownReason r)
        {
            switch (r)
            {
                case ShutdownReason.GameNotFound: return ConnectFailedMessage;
                case ShutdownReason.GameIsFull: return "방이 가득 찼어요 (4명) — 다른 방 코드로 들어오거나 새 방을 만드세요";
                case ShutdownReason.GameClosed: return "이미 게임이 시작된 방이에요 — 다음 판에 들어오거나 새 방을 만드세요";
                case ShutdownReason.MaxCcuReached: return "지금 접속한 사람이 너무 많아 서버가 가득 찼어요 — 잠시 뒤 다시 시도하거나 '혼자 하기'로 즐겨요";
                case ShutdownReason.InvalidRegion: return "방의 지역에 접속하지 못했어요 — 방 코드를 확인하세요";
                case ShutdownReason.PhotonCloudTimeout: case ShutdownReason.OperationTimeout: return TimeoutMessage;
                case ShutdownReason.ConnectionRefused: case ShutdownReason.ConnectionTimeout: return OfflineMessage;
                default: return "방에 들어가지 못했어요 (" + r + ") — 다시 시도해 주세요";
            }
        }

        // ---------- 종료 ----------

        /// <summary>자발 종료(나가기·스테이지 선택으로·테스트 정리). 접속 중이었다면 Disconnected 를 한 번 발행해 ScreenManager 가 로컬 StageSelect 로 돌아가게 한다.</summary>
        public void Shutdown()
        {
            bool was = _active;
            Teardown();
            _active = false;
            _retries = 0;
            _createRetried = false;
            LastDisconnectWasConnectFailure = false;
            SetRoomCode("");
            Session.Reset();
            SetStatus("");
            if (was) Disconnected?.Invoke();
        }

        private void Teardown()
        {
            var r = Runner;
            Runner = null;
            _connected = false;
            if (r != null)
            {
                try { r.Shutdown(); }
                catch (Exception e) { Debug.LogWarning("[NetHub] Shutdown 예외: " + e.Message); }
            }
        }

        /// <summary>접속 시도 실패. 매칭 화면에 남아 코드를 고쳐 다시 시도하게 한다.</summary>
        private void Fail(string msg)
        {
            Teardown();
            _active = false;
            _createRetried = false;
            LastDisconnectWasConnectFailure = true;
            SetRoomCode("");
            Session.Reset();
            SetStatus(msg);
            Disconnected?.Invoke();
        }

        /// <summary>접속 후 끊김(방장 퇴장·인터넷 끊김 등). 로컬 스테이지 선택으로 돌아간다.</summary>
        public void Lost(string msg)
        {
            if (!_active) return;
            bool wasConnected = _connected;
            Teardown();
            _active = false;
            LastDisconnectWasConnectFailure = !wasConnected;
            SetRoomCode("");
            Session.Reset();
            SetStatus(msg);
            Disconnected?.Invoke();
        }

        // ---------- 세션 연동 ----------

        public void OnSessionSpawned(SessionNet sn)
        {
            MasterPlayer = sn.Object.StateAuthority;
        }

        public bool WantBotsOnce()
        {
            bool b = _wantBots;
            _wantBots = false;
            return b;
        }

        /// <summary>세션 오브젝트가 사라졌다 — 권한자가 나갔거나 방이 닫혔다.</summary>
        public void OnSessionGone(NetworkRunner runner)
        {
            if (_active && runner == Runner && !IsAuthority) Lost(HostClosedReason);
        }

        public NetworkObject SpawnAvatar(int slot, Vector2 pos)
        {
            if (Runner == null) return null;
            if (_avatarPrefab == null) _avatarPrefab = Resources.Load<NetworkObject>("NetPrefabs/Avatar");
            if (_avatarPrefab == null) { Debug.LogError("[NetHub] 아바타 프리팹이 없습니다 (Resources/NetPrefabs/Avatar)"); return null; }
            return Runner.Spawn(_avatarPrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity, null,
                (r, o) => { var av = o.GetComponent<AvatarNet>(); if (av != null) av.Slot = slot; });
        }

        private void SetRoomCode(string c)
        {
            if (RoomCode == c) return;
            RoomCode = c;
            RoomCodeChanged?.Invoke(c);
        }

        private void SetStatus(string s)
        {
            Status = s;
            StatusChanged?.Invoke(s);
            if (!string.IsNullOrEmpty(s)) Debug.Log("[NetHub] " + s);
        }

        // ---------- INetworkRunnerCallbacks ----------

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner != Runner) return;
            var sn = SessionNet.Instance;
            if (IsAuthority && sn != null) sn.PlayerJoined(player);
            if (IsAuthority && player != runner.LocalPlayer) SetStatus("플레이어 접속");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner != Runner) return;
            if (IsAuthority)
            {
                var sn = SessionNet.Instance;
                if (sn != null) sn.PlayerLeft(player);
                SetStatus("플레이어 퇴장");
            }
            else if (MasterPlayer != default && player == MasterPlayer) Lost(HostClosedReason);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (runner != Runner) return;   // 우리가 요청한 종료(Teardown)는 Runner 가 이미 null
            // 접속 도중(아직 방에 못 들어간 상태)의 종료는 "연결 끊김" 이 아니라 접속 실패다: 없는 방 코드면 "그 방을 찾지 못했어요" 를 보여 준다
            if (!_connected && shutdownReason != ShutdownReason.Ok)
            {
                if (!_active || (_creating && shutdownReason == ShutdownReason.GameIdAlreadyExists)) return;   // 후자는 StartRoutine 이 코드를 바꿔 다시 시도한다
                if (shutdownReason != ShutdownReason.MaxCcuReached && TryCreateAgain()) return;   // 방 만들기 도중 끊김: 한 번 더
                Fail(MessageFor(shutdownReason));
                return;
            }
            Lost(shutdownReason == ShutdownReason.Ok ? HostClosedReason : "서버와 연결이 끊겼어요");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (runner != Runner) return;
            Lost("서버와 연결이 끊겼어요");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            if (runner != Runner) return;
            Fail(OfflineMessage);
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
