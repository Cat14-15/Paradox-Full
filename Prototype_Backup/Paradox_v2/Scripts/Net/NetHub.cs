using System;
using System.Reflection;
using Paradox.Client;
using Paradox.Game;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Paradox.Net
{
    /// <summary>
    /// 네트워크 골격. NetworkManager / 유니티 트랜스포트 / 런타임 프리팹(세션·아바타)을 코드로 만들고,
    /// 호스트(방 만들기) · 참가 · 혼자 테스트(봇 3명)를 시작한다. 에디터 프리팹 배선이 필요 없다.
    /// </summary>
    public class NetHub : MonoBehaviour
    {
        public static NetHub Instance { get; private set; }

        public const ushort Port = 7777;

        // 런타임 생성 프리팹의 고정 해시(호스트/클라 동일 코드 → 동일 값이라 매칭됨)
        private const uint SessionPrefabHash = 0x50415853; // "PAXS"
        private const uint AvatarPrefabHash = 0x50415841;  // "PAXA"

        public NetworkManager NM { get; private set; }
        public UnityTransport Transport { get; private set; }

        private GameObject _sessionTemplate;
        private GameObject _avatarTemplate;

        public bool IsRunning => NM != null && NM.IsListening;
        public string Status { get; private set; } = "";
        public event Action<string> StatusChanged;
        public event Action Disconnected;

        private bool _wantBots;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Setup();
        }

        private void Setup()
        {
            var nmGo = new GameObject("NetworkManager");
            DontDestroyOnLoad(nmGo);
            NM = nmGo.AddComponent<NetworkManager>();
            Transport = nmGo.AddComponent<UnityTransport>();

            if (NM.NetworkConfig == null) NM.NetworkConfig = new NetworkConfig();
            NM.NetworkConfig.NetworkTransport = Transport;
            NM.NetworkConfig.EnableSceneManagement = false;
            NM.NetworkConfig.ConnectionApproval = false;
            NM.NetworkConfig.PlayerPrefab = null;

            _sessionTemplate = BuildSessionTemplate();
            _avatarTemplate = BuildAvatarTemplate();
            NM.AddNetworkPrefab(_sessionTemplate);
            NM.AddNetworkPrefab(_avatarTemplate);

            NM.OnClientConnectedCallback += OnClientConnected;
            NM.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // ---------- 시작 ----------

        public bool StartHost(bool withBots)
        {
            if (IsRunning) return false;
            _wantBots = withBots;
            Transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
            bool ok = NM.StartHost();
            if (!ok) { SetStatus("호스트 시작 실패 — 포트가 사용 중일 수 있어요"); return false; }

            Session.Reset();
            Session.MyClientId = (long)NM.LocalClientId;
            Session.IsHost = true;

            var go = Instantiate(_sessionTemplate);
            go.name = "Session";
            go.SetActive(true);
            var no = go.GetComponent<NetworkObject>();
            SetHash(no, SessionPrefabHash);
            no.Spawn();

            // 호스트 자신의 접속 콜백은 StartHost 안에서 이미 지나갔을 수 있으므로 여기서 직접 자리를 잡는다(중복 호출은 무시됨).
            var sn = SessionNet.Instance;
            if (sn != null)
            {
                sn.OnClientJoined(NM.LocalClientId);
                if (_wantBots) { sn.FillBotsFromHost(); _wantBots = false; }
            }

            SetStatus(withBots ? "혼자 테스트 시작 (봇 3명)" : "방을 열었습니다. 친구들이 참가하면 자리가 채워집니다");
            return true;
        }

        public bool StartClient(string ip)
        {
            if (IsRunning) return false;
            if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";
            Transport.SetConnectionData(ip.Trim(), Port);
            bool ok = NM.StartClient();
            if (!ok) { SetStatus("참가 시작 실패"); return false; }
            Session.Reset();
            Session.IsHost = false;
            SetStatus($"{ip} 에 접속 중…");
            return true;
        }

        public void Shutdown()
        {
            if (NM != null && NM.IsListening) NM.Shutdown();
            Session.Reset();
            SetStatus("");
        }

        // ---------- 접속 콜백 (서버/클라 공용) ----------

        private void OnClientConnected(ulong clientId)
        {
            if (NM.IsServer)
            {
                var sn = SessionNet.Instance;
                if (sn != null) sn.OnClientJoined(clientId);
                if (clientId == NM.LocalClientId && _wantBots && sn != null)
                {
                    sn.FillBotsFromHost();
                    _wantBots = false;
                }
                if (clientId != NM.LocalClientId) SetStatus($"플레이어 접속 (현재 {NM.ConnectedClientsList.Count}명)");
            }
            else if (clientId == NM.LocalClientId)
            {
                Session.MyClientId = (long)clientId;
                SetStatus("접속 성공 — 로비로 들어갑니다");
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (NM.IsServer && clientId != NM.LocalClientId)
            {
                var sn = SessionNet.Instance;
                if (sn != null) sn.OnClientLeft(clientId);
                return;
            }
            // 내가 끊긴 경우(서버 종료·접속 실패 포함)
            string reason = NM.DisconnectReason;
            SetStatus(string.IsNullOrEmpty(reason) ? "연결이 끊겼습니다" : $"연결 종료: {reason}");
            Session.Reset();
            Disconnected?.Invoke();
        }

        private void SetStatus(string s)
        {
            Status = s;
            StatusChanged?.Invoke(s);
            if (!string.IsNullOrEmpty(s)) Debug.Log("[NetHub] " + s);
        }

        // ---------- 아바타 스폰 (서버) ----------

        public AvatarNet SpawnAvatar(int slot, ulong ownerClientId, Vector2 pos)
        {
            var go = Instantiate(_avatarTemplate);
            go.name = "Avatar_" + slot;
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.SetActive(true);
            var no = go.GetComponent<NetworkObject>();
            SetHash(no, AvatarPrefabHash);
            var av = go.GetComponent<AvatarNet>();
            av.pendingSlot = slot;
            no.SpawnWithOwnership(ownerClientId);
            return av;
        }

        // ---------- 템플릿 ----------

        private GameObject BuildSessionTemplate()
        {
            var go = new GameObject("SessionTemplate");
            var no = go.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;
            no.AutoObjectParentSync = false;
            SetHash(no, SessionPrefabHash);
            go.AddComponent<SessionNet>();
            go.SetActive(false);
            DontDestroyOnLoad(go);
            return go;
        }

        private GameObject BuildAvatarTemplate()
        {
            var go = new GameObject("AvatarTemplate");
            var no = go.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;
            no.AutoObjectParentSync = false;
            SetHash(no, AvatarPrefabHash);

            var nt = go.AddComponent<OwnerNetworkTransform>();
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = false; nt.SyncRotAngleY = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false; nt.SyncScaleY = false; nt.SyncScaleZ = false;
            nt.Interpolate = true;
            nt.PositionThreshold = 0.01f;

            go.AddComponent<AvatarNet>();
            go.SetActive(false);
            DontDestroyOnLoad(go);
            return go;
        }

        // NetworkObject.GlobalObjectIdHash는 보통 에디터가 굽지만,
        // 런타임 생성 프리팹은 리플렉션으로 고정값을 넣어 호스트/클라 매칭을 맞춘다.
        private static void SetHash(NetworkObject no, uint hash)
        {
            var f = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) f.SetValue(no, hash);
            else Debug.LogWarning("[NetHub] GlobalObjectIdHash 필드를 찾지 못했습니다 — NGO 버전 확인 필요");
        }
    }
}
