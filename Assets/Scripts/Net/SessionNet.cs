using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Fusion;
using Paradox.Client;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Net
{
    /// <summary>
    /// 세션 네트워크 오브젝트(권한자 = 방장/솔로가 하나 스폰). 권한자는 GameLogic 을 돌리고 상태를 모두에게 보내며,
    /// 나머지는 받은 상태를 Session 에 반영한다. 명령은 권한자에게 RPC, 이벤트는 모두에게 RPC.
    ///
    /// 상태 JSON(수 KB)은 RPC 한도(512B)를 넘으므로 Deflate 로 압축해 조각(StateChunk)으로 나눠 보내고 받는 쪽이 다시 조립한다.
    /// (신뢰 데이터 API 는 공유 모드 지원이 문서에 없어 쓰지 않는다.) 클록·미터는 [Networked] 로 복제한다.
    /// 권한자 자신에게는 RPC 를 거치지 않고 직접 반영한다(Single/Shared 공통 경로).
    /// </summary>
    public class SessionNet : NetworkBehaviour
    {
        public static SessionNet Instance { get; private set; }

        [Networked] public float ClockVar { get; set; }
        [Networked] public float MeterAVar { get; set; }
        [Networked] public float MeterBVar { get; set; }

        /// <summary>권한자에게만 존재하는 규칙 객체.</summary>
        public GameLogic Logic { get; private set; }

        public bool IsAuthority => Object != null && Object.IsValid && HasStateAuthority;

        private const int ChunkBytes = 360;           // RPC 한도 512B 에서 머리말 여유
        private const int MaxRpcBytes = 440;
        private const float BroadcastInterval = 0.05f;

        private float _lastBroadcast = -1f;
        private bool _inBroadcast;
        private int _msgId;
        private static bool s_loggedPayload;

        // 수신 조립
        private int _rxMsg = -1, _rxTotal, _rxCount;
        private byte[][] _rxParts;

        // 아바타(권한자)
        private readonly Vector2[] _lastPos = new Vector2[4];
        private readonly bool[] _hasPos = new bool[4];
        private float _spawnedAt = -99f;
        private float _nextReconcile;

        // ---------- 생명주기 ----------

        public override void Spawned()
        {
            Instance = this;
            // 로비 → 프롤로그 → 게임 장면으로 Unity 씬이 바뀌어도 세션 오브젝트가 사라지지 않게 한다
            // (사라지면 명령이 전달되지 않아 프롤로그 "기다리는 중"에서 영원히 멈춘다)
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            Session.MyClientId = Runner.LocalPlayer.PlayerId;
            Session.IsHost = HasStateAuthority;
            var hub = NetHub.Instance;
            if (hub != null) hub.OnSessionSpawned(this);

            if (HasStateAuthority)
            {
                Logic = new GameLogic();
                Logic.Emit = EmitEvent;
                Logic.GetPos = GetPos;
                Logic.OnGameStarted = SpawnAvatars;
                Logic.S.phase = Phase.Lobby;
                Logic.Dirty = true;
                foreach (var p in Runner.ActivePlayers) PlayerJoined(p);
                if (hub != null && hub.WantBotsOnce()) FillBotsFromHost();
                Broadcast();
            }
            else
            {
                RequestStateRpc();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            var hub = NetHub.Instance;
            if (hub != null) hub.OnSessionGone(runner);
        }

        private bool HasRemotes
        {
            get
            {
                var hub = NetHub.Instance;
                return hub != null && hub.PlayerCount > 1;
            }
        }

        private void Update()
        {
            if (Object == null || !Object.IsValid) return;
            if (HasStateAuthority && Logic != null)
            {
                float dt = Time.deltaTime;
                Logic.Clock += dt;
                ClockVar = Logic.Clock;
                Logic.Tick(dt);
                MeterAVar = Logic.MeterA;
                MeterBVar = Logic.MeterB;
                if (Logic.Dirty && Time.time - _lastBroadcast >= BroadcastInterval) Broadcast();
                Session.Clock = Logic.Clock;
                Session.MeterA = Logic.MeterA;
                Session.MeterB = Logic.MeterB;
                TrackAvatars();
            }
            else
            {
                Session.Clock = ClockVar;
                Session.MeterA = MeterAVar;
                Session.MeterB = MeterBVar;
            }
        }

        // ---------- 권한자 → 모두 ----------

        private void Broadcast()
        {
            if (Logic == null || _inBroadcast) return;
            _inBroadcast = true;
            try
            {
                Logic.S.version++;
                Logic.Dirty = false;
                _lastBroadcast = Time.time;
                string json = Json.To(Logic.S);
                Session.ApplyState(Json.From<SessionState>(json));   // 권한자 자신은 직접(사본으로 — Logic.S 와 공유하지 않는다)
                if (HasRemotes) SendState(json);
            }
            finally { _inBroadcast = false; }
        }

        private void SendState(string json)
        {
            byte[] raw = Encoding.UTF8.GetBytes(json);
            byte[] zip;
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) ds.Write(raw, 0, raw.Length);
                zip = ms.ToArray();
            }
            int total = (zip.Length + ChunkBytes - 1) / ChunkBytes;
            if (!s_loggedPayload)
            {
                s_loggedPayload = true;
                Debug.Log($"[SessionNet] 상태 JSON {raw.Length}B -> 압축 {zip.Length}B -> RPC {total}조각(조각당 {ChunkBytes}B)");
            }
            int msg = ++_msgId;
            for (int i = 0; i < total; i++)
            {
                int off = i * ChunkBytes;
                int len = Math.Min(ChunkBytes, zip.Length - off);
                var part = new byte[len];
                Buffer.BlockCopy(zip, off, part, 0, len);
                StateChunkRpc(msg, i, total, part);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false, TickAligned = false)]
        public void StateChunkRpc(int msg, int index, int total, byte[] data)
        {
            if (msg != _rxMsg)
            {
                _rxMsg = msg; _rxTotal = total; _rxCount = 0;
                _rxParts = new byte[total][];
            }
            if (_rxParts == null || index < 0 || index >= _rxParts.Length || _rxParts[index] != null) return;
            _rxParts[index] = data;
            if (++_rxCount < _rxTotal) return;

            try
            {
                using (var ms = new MemoryStream())
                {
                    foreach (var p in _rxParts) ms.Write(p, 0, p.Length);
                    ms.Position = 0;
                    using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        ds.CopyTo(outMs);
                        Session.ApplyState(Json.From<SessionState>(Encoding.UTF8.GetString(outMs.ToArray())));
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[SessionNet] 상태 조립 실패: " + e.Message); }
            _rxParts = null; _rxMsg = -1;
        }

        private void EmitEvent(GameEvent e)
        {
            Session.Dispatch(Json.From<GameEvent>(Json.To(e)));
            if (!HasRemotes) return;
            byte[] data = Fit(e);
            EventRpc(data);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false, TickAligned = false)]
        public void EventRpc(byte[] data)
        {
            Session.Dispatch(Json.From<GameEvent>(Encoding.UTF8.GetString(data)));
        }

        /// <summary>
        /// Story1 위치 전송: 누가 보내든 모두에게, 비신뢰 채널로(몇 개 빠져도 상관없고, 신뢰 RPC 를 막지 않는다).
        /// 예전에는 위치도 명령 RPC → 권한자 → 이벤트 RPC 로 돌아서 4명이 움직이면 신뢰 메시지가 넘쳐 끊겼다.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false, Channel = RpcChannel.Unreliable, TickAligned = false)]
        public void PosRpc(float x, float y, RpcInfo info = default)
        {
            int slot = Session.State.SlotOfClient(info.Source.PlayerId);
            if (!Limiter.AllowPos(info.Source.PlayerId, Time.unscaledTime)) return;
            Story1Net.OnRemotePos(slot, new Vector2(x, y));
        }

        /// <summary>JSON 이 RPC 한도를 넘으면 문자열 필드를 줄인다.</summary>
        private static byte[] Fit(GameEvent e)
        {
            var copy = Json.From<GameEvent>(Json.To(e));
            byte[] b = Encoding.UTF8.GetBytes(Json.To(copy));
            while (b.Length > MaxRpcBytes && copy.s != null && copy.s.Length > 4)
            {
                copy.s = copy.s.Substring(0, copy.s.Length * 3 / 4);
                b = Encoding.UTF8.GetBytes(Json.To(copy));
            }
            return b;
        }

        // ---------- 클라 → 권한자 ----------

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RequestStateRpc(RpcInfo info = default)
        {
            if (Logic == null) return;
            Logic.Dirty = true;
            Broadcast();
        }

        public void Submit(Cmd c)
        {
            if (c == null || Object == null || !Object.IsValid) return;
            if (HasStateAuthority) { ApplyCommand(c, Runner.LocalPlayer); return; }
            var copy = Json.From<Cmd>(Json.To(c));
            byte[] b = Encoding.UTF8.GetBytes(Json.To(copy));
            while (b.Length > MaxRpcBytes && copy.s != null && copy.s.Length > 4)
            {
                copy.s = copy.s.Substring(0, copy.s.Length * 3 / 4);
                b = Encoding.UTF8.GetBytes(Json.To(copy));
            }
            CommandRpc(b);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void CommandRpc(byte[] data, RpcInfo info = default)
        {
            var c = Json.From<Cmd>(Encoding.UTF8.GetString(data));
            if (c == null) return;
            ApplyCommand(c, info.Source);
        }

        /// <summary>권한자 전용. 보낸 사람(PlayerRef)으로 슬롯을 다시 계산한다 — 클라가 넣은 slot 은 믿지 않는다(봇 슬롯은 방장만 대신 조작).</summary>
        private void ApplyCommand(Cmd c, PlayerRef sender)
        {
            if (Logic == null) return;
            bool fromHost = sender == Runner.LocalPlayer;
            if (!fromHost && !Limiter.Allow(sender.PlayerId, c, Time.unscaledTime)) return;   // 도배 차단(권한자가 버린다 — 다른 사람에게 전파되지 않는다)
            int senderSlot = Logic.S.SlotOfClient(sender.PlayerId);
            int slot = c.slot;
            bool allowed = Roles.Valid(slot) && (slot == senderSlot || (fromHost && Logic.IsBot(slot)));
            if (!allowed) slot = senderSlot;
            if (slot < 0) return;
            c.slot = slot;
            Logic.Apply(c, fromHost);
            if (Logic.Dirty) Broadcast();
        }

        // ---------- 접속 (NetHub 가 호출, 권한자 전용) ----------

        public void PlayerJoined(PlayerRef player)
        {
            if (!HasStateAuthority || Logic == null) return;
            int slot = Logic.Join(player.PlayerId, "");
            if (slot < 0)
            {
                KickRpc(player.PlayerId, slot == -2 ? "게임이 진행 중이에요 — 다음 판에 들어와요" : "방이 가득 찼어요 (4명)");
                return;
            }
            if (player == Runner.LocalPlayer) Logic.S.hostSlot = slot;
            UpdateRoomOpen();
            Broadcast();
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority || Logic == null) return;
            Logic.Leave(player.PlayerId); Limiter.Forget(player.PlayerId);
            Broadcast();
        }

        private void FillBotsFromHost()
        {
            if (Logic == null) return;
            Logic.FillBots(Runner.LocalPlayer.PlayerId);
            Broadcast();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false)]
        public void KickRpc(int playerId, string reason)
        {
            if (Runner.LocalPlayer.PlayerId != playerId) return;
            var hub = NetHub.Instance;
            if (hub != null) hub.Lost(reason);
        }

        /// <summary>판이 시작되면 방을 닫아 새 참가를 막고, 로비로 돌아오면 다시 연다.</summary>
        private bool _roomOpen = true;
        private void UpdateRoomOpen()
        {
            if (Runner == null || Runner.IsSinglePlayer || Logic == null) return;
            bool open = Logic.S.phase == Phase.Lobby && Logic.S.PresentCount < NetHub.MaxPlayers;
            if (open == _roomOpen) return;
            try { Runner.SessionInfo.IsOpen = open; _roomOpen = open; }
            catch (Exception e) { Debug.LogWarning("[SessionNet] 방 열기/닫기 실패: " + e.Message); }
        }

        // ---------- 아바타 ----------

        /// <summary>판이 시작될 때(권한자). 각 플레이어가 자기 아바타를 자기 권한으로 스폰한다 — 봇은 방장 몫.</summary>
        private void SpawnAvatars()
        {
            var owners = new int[4];
            for (int i = 0; i < 4; i++)
            {
                var p = Logic.S.players[i];
                owners[i] = p.present ? (int)p.clientId : -1;
            }
            _spawnedAt = Time.time;
            HandleSpawnAvatars(owners[0], owners[1], owners[2], owners[3]);
            if (HasRemotes) SpawnAvatarsRpc(owners[0], owners[1], owners[2], owners[3]);
            UpdateRoomOpen();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, InvokeLocal = false, TickAligned = false)]
        public void SpawnAvatarsRpc(int o0, int o1, int o2, int o3)
        {
            HandleSpawnAvatars(o0, o1, o2, o3);
        }

        private void HandleSpawnAvatars(int o0, int o1, int o2, int o3)
        {
            var owners = new[] { o0, o1, o2, o3 };
            // 내가 권한을 가진 이전 판 아바타는 지운다(Despawned 가 Session.Avatars 에서 스스로 뺀다)
            var old = new List<AvatarNet>(Session.Avatars.Values);
            foreach (var av in old)
                if (av != null && av.IsSpawned && av.IsOwner) Runner.Despawn(av.Object);

            int me = Runner.LocalPlayer.PlayerId;
            var hub = NetHub.Instance;
            for (int i = 0; i < 4; i++)
            {
                if (owners[i] != me || hub == null) continue;
                hub.SpawnAvatar(i, new Vector2(-1000f + i * 3f, -1000f));
            }
        }

        /// <summary>권한자: 봇 아바타의 마지막 위치를 기억하고, 사람이 나가 봇이 된 자리의 아바타가 없으면 그 자리에 새로 스폰한다.</summary>
        private void TrackAvatars()
        {
            for (int i = 0; i < 4; i++)
            {
                if (Session.Avatars.TryGetValue(i, out var a) && a != null && a.IsSpawned)
                {
                    _lastPos[i] = a.transform.position;
                    _hasPos[i] = true;
                }
            }
            if (Time.time < _nextReconcile) return;
            _nextReconcile = Time.time + 1f;
            UpdateRoomOpen();
            var S = Logic.S;
            if (S.phase < Phase.StageIntro || S.phase > Phase.Result) return;
            if (Time.time - _spawnedAt < 3f) return;
            var hub = NetHub.Instance;
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                if (!p.present || !p.isBot) continue;
                if (Session.Avatars.TryGetValue(i, out var a) && a != null && a.IsSpawned && a.IsOwner) continue;
                Vector2 at = _hasPos[i] ? _lastPos[i] : new Vector2(-1000f + i * 3f, -1000f);
                if (hub != null) hub.SpawnAvatar(i, at);
                Debug.Log($"[SessionNet] 봇 슬롯 {i} 아바타를 새로 스폰");
            }
        }

        private Vector2? GetPos(int slot)
        {
            if (Session.Avatars.TryGetValue(slot, out var a) && a != null) return (Vector2)a.transform.position;
            return null;
        }
    }
}


namespace Paradox.Net
{
    /// <summary>
    /// 보낸 사람별 명령 속도 제한(토큰 버킷). 권한자에서만 쓴다.
    /// 일반 명령: 초당 20개(순간 30개). 신호 카드: 순간 4개, 이후 초당 1개. 위치: 초당 30개.
    /// 일반 한도를 거듭 넘기면 8초 동안 그 사람의 명령을 모두 무시한다.
    /// </summary>
    public static class Limiter
    {
        private class B { public float gen = 30, sig = 4, pos = 40, last, mutedUntil; public int strikes; }
        private static readonly System.Collections.Generic.Dictionary<int, B> _b = new System.Collections.Generic.Dictionary<int, B>();

        private static B Get(int id, float now)
        {
            B b;
            if (!_b.TryGetValue(id, out b)) { b = new B { last = now }; _b[id] = b; }
            float dt = Mathf.Clamp(now - b.last, 0f, 5f); b.last = now;
            b.gen = Mathf.Min(30f, b.gen + dt * 20f);
            b.sig = Mathf.Min(4f, b.sig + dt * 1f);
            b.pos = Mathf.Min(40f, b.pos + dt * 30f);
            if (b.strikes > 0 && dt > 3f) b.strikes = 0;
            return b;
        }

        public static bool Allow(int id, Paradox.Game.Cmd c, float now)
        {
            var b = Get(id, now);
            if (now < b.mutedUntil) return false;
            if (b.gen < 1f)
            {
                if (++b.strikes >= 20) { b.mutedUntil = now + 8f; b.strikes = 0; Debug.LogWarning("[Limiter] 도배로 8초간 무시: player " + id); }
                return false;
            }
            b.gen -= 1f;
            if (c != null && c.type == "S1" && c.a == Paradox.Client.Story1Net.Signal)
            {
                if (b.sig < 1f) return false;
                b.sig -= 1f;
            }
            return true;
        }

        public static bool AllowPos(int id, float now)
        {
            var b = Get(id, now);
            if (b.pos < 1f) return false;
            b.pos -= 1f; return true;
        }

        public static void Forget(int id) { _b.Remove(id); }
        public static void Clear() { _b.Clear(); }
    }
}
