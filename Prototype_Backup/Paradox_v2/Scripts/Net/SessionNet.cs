using Paradox.Client;
using Paradox.Game;
using Unity.Netcode;
using UnityEngine;

namespace Paradox.Net
{
    /// <summary>
    /// 세션 네트워크 오브젝트(서버가 하나 스폰). 서버에서는 GameLogic을 돌리고 상태를 JSON으로 브로드캐스트하며,
    /// 클라이언트에서는 받은 상태를 Session에 반영한다. 명령은 서버 RPC로, 이벤트는 클라이언트 RPC로 흐른다.
    /// 연속값(클록·미터)은 NetworkVariable로 매 틱 복제된다.
    /// </summary>
    public class SessionNet : NetworkBehaviour
    {
        public static SessionNet Instance { get; private set; }

        public readonly NetworkVariable<float> ClockVar = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> MeterAVar = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> MeterBVar = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>서버에서만 존재하는 규칙 객체.</summary>
        public GameLogic Logic { get; private set; }

        private float _lastBroadcast = -1f;
        private const float BroadcastInterval = 0.05f;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Session.MyClientId = (long)NetworkManager.LocalClientId;
            Session.IsHost = IsHost;

            if (IsServer)
            {
                Logic = new GameLogic();
                Logic.Emit = e => EventRpc(Json.To(e));
                Logic.GetPos = GetPos;
                Logic.OnGameStarted = SpawnAvatars;
                Logic.S.phase = Phase.Lobby;
                Logic.Dirty = true;
            }
            else
            {
                RequestStateRpc();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (IsServer && Logic != null)
            {
                float dt = Time.deltaTime;
                Logic.Clock += dt;
                ClockVar.Value = Logic.Clock;
                Logic.Tick(dt);
                MeterAVar.Value = Logic.MeterA;
                MeterBVar.Value = Logic.MeterB;
                if (Logic.Dirty && Time.time - _lastBroadcast >= BroadcastInterval) Broadcast();
            }
            Session.Clock = ClockVar.Value;
            Session.MeterA = MeterAVar.Value;
            Session.MeterB = MeterBVar.Value;
        }

        private void Broadcast()
        {
            if (Logic == null) return;
            Logic.S.version++;
            Logic.Dirty = false;
            _lastBroadcast = Time.time;
            StateRpc(Json.To(Logic.S));
        }

        // ---------- 서버 → 클라 ----------

        [Rpc(SendTo.ClientsAndHost)]
        private void StateRpc(string json)
        {
            Session.ApplyState(Json.From<SessionState>(json));
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void EventRpc(string json)
        {
            Session.Dispatch(Json.From<GameEvent>(json));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void StateToRpc(string json, RpcParams rpcParams)
        {
            Session.ApplyState(Json.From<SessionState>(json));
        }

        // ---------- 클라 → 서버 ----------

        [Rpc(SendTo.Server)]
        private void RequestStateRpc(RpcParams rpcParams = default)
        {
            if (Logic == null) return;
            StateToRpc(Json.To(Logic.S), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        public void Submit(Cmd c)
        {
            if (c == null) return;
            CommandRpc(Json.To(c));
        }

        [Rpc(SendTo.Server)]
        private void CommandRpc(string json, RpcParams rpcParams = default)
        {
            if (Logic == null) return;
            var c = Json.From<Cmd>(json);
            if (c == null) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            bool fromHost = sender == NetworkManager.ServerClientId;
            int senderSlot = Logic.S.SlotOfClient((long)sender);
            int slot = c.slot;
            bool allowed = Roles.Valid(slot) && (slot == senderSlot || (fromHost && Logic.IsBot(slot)));
            if (!allowed) slot = senderSlot;
            if (slot < 0) return;
            c.slot = slot;
            Logic.Apply(c, fromHost);
            if (Logic.Dirty) Broadcast();
        }

        // ---------- 접속 (NetHub가 호출, 서버 전용) ----------

        public void OnClientJoined(ulong clientId)
        {
            if (!IsServer || Logic == null) return;
            int slot = Logic.Join((long)clientId, "");
            if (slot < 0)
            {
                NetworkManager.DisconnectClient(clientId, "방이 가득 찼어요 (4명)");
                return;
            }
            if (clientId == NetworkManager.ServerClientId) Logic.S.hostSlot = slot;
            Broadcast();
        }

        public void OnClientLeft(ulong clientId)
        {
            if (!IsServer || Logic == null) return;
            Logic.Leave((long)clientId);
            Broadcast();
        }

        public void FillBotsFromHost()
        {
            if (!IsServer || Logic == null) return;
            Logic.FillBots((long)NetworkManager.ServerClientId);
            Broadcast();
        }

        // ---------- 아바타 (서버) ----------

        private void SpawnAvatars()
        {
            if (!IsServer) return;
            foreach (var kv in Session.Avatars)
            {
                if (kv.Value != null && kv.Value.NetworkObject != null && kv.Value.NetworkObject.IsSpawned)
                    kv.Value.NetworkObject.Despawn(true);
            }
            Session.Avatars.Clear();
            for (int i = 0; i < 4; i++)
            {
                var p = Logic.S.players[i];
                if (!p.present) continue;
                ulong owner = p.isBot ? NetworkManager.ServerClientId : (ulong)p.clientId;
                NetHub.Instance.SpawnAvatar(i, owner, new Vector2(-1000f + i * 3f, -1000f));
            }
        }

        private Vector2? GetPos(int slot)
        {
            if (Session.Avatars.TryGetValue(slot, out var a) && a != null) return (Vector2)a.transform.position;
            return null;
        }
    }
}
