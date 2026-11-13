using System;
using System.Collections.Generic;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 클라이언트 쪽 "내가 아는 세션". 서버가 보낸 상태 사본, 내 슬롯, 지금 조종 중인 슬롯(호스트가 봇을 대신 조작할 때),
    /// 연속값(클록·미터), 이벤트 로그를 담는다. 모든 화면/HUD/월드는 여기만 읽는다.
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

        public struct LogEntry { public GameEvent e; public float t; }
        public static readonly List<LogEntry> Log = new List<LogEntry>();
        public const int MaxLog = 40;

        public static event Action StateChanged;
        public static event Action<GameEvent> EventReceived;
        public static event Action LocalSlotChanged;

        private static int _mapKey = int.MinValue;

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
            _mapKey = int.MinValue;
            Avatars.Clear();
            Log.Clear();
            StateChanged?.Invoke();
        }

        public static void ApplyState(SessionState s)
        {
            if (s == null) return;
            State = s;
            Connected = true;
            MySlot = s.SlotOfClient(MyClientId);
            if (LocalSlot < 0 || !OwnsSlot(LocalSlot)) LocalSlot = MySlot;
            RebuildMapIfNeeded();
            StateChanged?.Invoke();
        }

        private static void RebuildMapIfNeeded()
        {
            if (State.phase != Phase.StagePlay) { Map = null; _mapKey = int.MinValue; return; }
            var st = State.st;
            int key = st.stage * 1000003 + st.seed * 7919 + (st.gatesOpen ? 1 : 0) + (st.obstacleCleared ? 2 : 0) + (st.codeDone ? 4 : 0) + st.fires.Count * 8 + st.retries * 131;
            if (key != _mapKey || Map == null)
            {
                _mapKey = key;
                Map = StageMaps.Build(st);
            }
        }

        public static void Dispatch(GameEvent e)
        {
            if (e == null) return;
            if (e.type == "Signal" || e.type == "Chat" || e.type == "Toast" || e.type == "LetterArrive")
            {
                Log.Add(new LogEntry { e = e, t = Time.time });
                while (Log.Count > MaxLog) Log.RemoveAt(0);
            }
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

        // ---------- 스테이지 편의 ----------

        public static int Stage => State.stage;
        public static StageState St => State.st;
        public static bool InStage => State.phase == Phase.StagePlay;
        public static bool CanReadSigns => InStage && StageDefs.CanRead(State.stage, LocalSlot);

        public static float TimeLeft
        {
            get
            {
                var st = State.st;
                if (!st.timed) return 0f;
                return st.timeLimit - (Clock - st.startClock) - st.penalty;
            }
        }

        public static Vector2? PosOf(int slot)
        {
            if (Avatars.TryGetValue(slot, out var a) && a != null) return (Vector2)a.transform.position;
            return null;
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
