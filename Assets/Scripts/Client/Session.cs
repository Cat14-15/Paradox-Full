using System;
using System.Collections.Generic;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 클라이언트 쪽 "내가 아는 세션". 서버가 보낸 상태 사본, 내 슬롯, 지금 조종 중인 슬롯(호스트가 봇을 대신 조작할 때),
    /// 연속값(클록), 이벤트 로그를 담는다. 모든 화면/HUD/월드는 여기만 읽는다. 규칙 판정은 하지 않는다(표시용 계산만).
    /// </summary>
    public static class Session
    {
        public static SessionState State = new SessionState();
        public static bool Connected;
        public static bool IsHost;
        public static long MyClientId = -1;
        public static int MySlot = -1;
        public static int LocalSlot = -1;
        public static float Clock, MeterA, MeterB;
        public static MapData Map;

        public static readonly Dictionary<int, AvatarNet> Avatars = new Dictionary<int, AvatarNet>();

        /// <summary>아바타가 스폰되기 전에 도착한 Teleport(스폰 자리) — 아바타가 스폰될 때 적용한다.</summary>
        public static readonly Dictionary<int, Vector2> PendingTeleports = new Dictionary<int, Vector2>();

        public struct LogEntry { public GameEvent e; public float t; }
        public static readonly List<LogEntry> Log = new List<LogEntry>();
        public const int MaxLog = 40;

        /// <summary>수신 로그에 남기는 이벤트 타입.</summary>
        public static readonly string[] LoggedTypes = { "Signal", "Chat", "Color", "NumberCard", "DirCard", "Ping", "Toast" };

        public static event Action StateChanged;
        public static event Action<GameEvent> EventReceived;
        public static event Action LocalSlotChanged;

        private static long _mapKey = long.MinValue;

        /// <summary>접속 종료 시 초기화. LocalNav(접속 전 화면)는 건드리지 않는다.</summary>
        public static void Reset()
        {
            State = new SessionState();
            Connected = false;
            IsHost = false;
            MyClientId = -1;
            MySlot = -1;
            LocalSlot = -1;
            Clock = MeterA = MeterB = 0f;
            Map = null;
            _mapKey = long.MinValue;
            Avatars.Clear();
            PendingTeleports.Clear();
            Log.Clear();
            StateChanged?.Invoke();
        }

        public static void ApplyState(SessionState s)
        {
            if (s == null) return;
            // 늦게 도착한 옛 상태로 되돌아가지 않게(버전 가드, LAN 감사 C-7)
            if (State != null && s.version != 0 && s.version < State.version) return;
            State = s;
            Connected = true;
            MySlot = s.SlotOfClient(MyClientId);
            if (LocalSlot < 0 || !OwnsSlot(LocalSlot)) LocalSlot = MySlot;
            // 로비·이름·준비 화면에서는 항상 내 자리를 조종한다(게임 끝에 봇 자리를 보고 있었어도 다음 판은 내 자리에서 시작 — 서버도 BackToLobby 에서 hostControlSlot 을 푼다)
            bool lobbyish = s.phase == Phase.Lobby || s.phase == Phase.NameSetup || s.phase == Phase.Ready;
            if (lobbyish && MySlot >= 0 && LocalSlot != MySlot)
            {
                LocalSlot = MySlot;
                LocalSlotChanged?.Invoke();
            }
            RebuildMapIfNeeded();
            StateChanged?.Invoke();
        }

        /// <summary>맵 재생성 키 = stage, seed, retries, gate1Done, step. 이 값이 바뀔 때만 새 MapData를 만든다.</summary>
        private static void RebuildMapIfNeeded()
        {
            if (State.phase != Phase.StagePlay) { Map = null; _mapKey = long.MinValue; return; }
            var st = State.st;
            long key = ((long)st.stage * 1000003L) + ((long)st.seed * 7919L) + ((long)st.retries * 131L) + (st.gate1Done ? 1L : 0L) + ((long)st.step * 2L);
            if (key != _mapKey || Map == null)
            {
                _mapKey = key;
                Map = StageMaps.Build(st);
            }
        }

        public static void Dispatch(GameEvent e)
        {
            if (e == null) return;
            if (Array.IndexOf(LoggedTypes, e.type) >= 0)
            {
                Log.Add(new LogEntry { e = e, t = Time.time });
                while (Log.Count > MaxLog) Log.RemoveAt(0);
            }
            if (e.type == "Teleport" && (!Avatars.TryGetValue(e.slot, out var av) || av == null || !av.IsSpawned)) PendingTeleports[e.slot] = e.pos.V;
            EventReceived?.Invoke(e);
        }

        // ---------- 슬롯 ----------

        public static bool OwnsSlot(int slot)
        {
            if (!Roles.Valid(slot)) return false;
            var p = State.players[slot];
            return p.present && p.clientId == MyClientId;
        }

        /// <summary>내가 조종 가능한 슬롯: 내 자리 + (내가 호스트면) 봇 자리.</summary>
        public static bool CanControl(int slot) => OwnsSlot(slot);

        public static void SetLocalSlot(int slot)
        {
            if (!CanControl(slot) || slot == LocalSlot) return;
            LocalSlot = slot;
            LocalSlotChanged?.Invoke();
            StateChanged?.Invoke();
            // 호스트가 봇 자리를 직접 조종하면 그 봇의 자동 행동은 멈춘다
            if (IsHost) Send(Cmd.Make("Control", slot));
        }

        public static PlayerInfo Me => Roles.Valid(LocalSlot) ? State.players[LocalSlot] : null;

        public static string NameOf(int slot)
        {
            if (!Roles.Valid(slot)) return "?";
            var p = State.players[slot];
            return p.present ? p.name : "빈 자리";
        }

        public static bool HasBots
        {
            get { foreach (var p in State.players) if (p.present && p.isBot) return true; return false; }
        }

        // ---------- 스테이지 편의(표시용) ----------

        public static int Stage => State.stage;
        public static StageState St => State.st;
        public static bool InStage => State.phase == Phase.StagePlay;

        /// <summary>내가 이 스테이지의 표지판·벽보를 읽을 수 있는가(통역).</summary>
        public static bool CanReadSigns => InStage && StageDefs.CanRead(State.stage, LocalSlot);

        /// <summary>침수까지 남은 시간(초) = timeLimit − (Clock − startClock) − penalty. timed가 아니면 0. 브리핑 게이트(timerRunning) 전에는 timeLimit.</summary>
        public static float TimeLeft
        {
            get
            {
                var st = State.st;
                if (!st.timed) return 0f;
                if (!st.timerRunning) return st.timeLimit;
                return st.timeLimit - (Clock - st.startClock) - st.penalty;
            }
        }

        public static Vector2? PosOf(int slot)
        {
            if (Avatars.TryGetValue(slot, out var a) && a != null) return (Vector2)a.transform.position;
            return null;
        }

        /// <summary>슬롯의 아바타가 있는 방(-1 = 복도/문/모름).</summary>
        public static int RoomOf(int slot)
        {
            if (Map == null) return -1;
            var p = PosOf(slot);
            return p == null ? -1 : Map.RoomAt(p.Value);
        }

        /// <summary>최종 집결: 자기 원 안에 있는 사람 수(표시용, 판정은 서버).</summary>
        public static int GatherCount
        {
            get
            {
                if (!InStage || St.step != 2) return 0;
                int n = 0;
                for (int i = 0; i < 4; i++)
                {
                    var p = PosOf(i);
                    if (p != null && Stage1Logic.InGather(i, p.Value, St, Clock)) n++;
                }
                return n;
            }
        }

        public static AvatarNet ControlledAvatar => Avatars.TryGetValue(LocalSlot, out var a) ? a : null;

        // ---------- 명령 ----------

        public static void Send(Cmd c)
        {
            if (c == null) return;
            c.slot = LocalSlot;
            var sn = SessionNet.Instance;
            if (sn != null) sn.Submit(c);
        }

        public static void Send(string type, int a = 0, int b = 0, string s = "", float f = 0f)
            => Send(Cmd.Make(type, a, b, s, f));
    }
}
