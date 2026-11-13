using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Net
{
    /// <summary>
    /// 네트워크 아바타. 서버가 슬롯(역할)을 배정해 스폰하고, 소유자가 움직인다(소유자 권한 트랜스폼).
    /// 호스트는 봇 슬롯의 아바타도 소유하므로 1~4 키로 조종 대상을 바꿀 수 있고, 조종하지 않는 봇은 BotBrain이 움직인다.
    /// </summary>
    public class AvatarNet : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public const float Speed = 4.3f;

        public int SlotIndex => Slot.Value;
        public AvatarView View { get; private set; }
        public StageInteractions.Prompt CurrentPrompt { get; private set; }
        public bool IsControlled => IsOwner && Session.LocalSlot == SlotIndex;

        private NetworkTransform _nt;
        private float _knockUntil;
        private Vector2 _knockDir;
        private int _registeredSlot = -1;
        private float _stuckTimer;
        private int _sidestep = 1;

        // 봇 길찾기
        private List<Vector2> _path;
        private Vector2 _pathGoal;
        private float _pathAge;
        private MapData _pathMap;

        private void Awake()
        {
            _nt = GetComponent<NetworkTransform>();
            View = GetComponent<AvatarView>() ?? gameObject.AddComponent<AvatarView>();
        }

        /// <summary>스폰 전에 서버가 정해 둔 슬롯. 스폰 시점에 NetworkVariable로 옮긴다(스폰 전 쓰기 경고 방지).</summary>
        [HideInInspector] public int pendingSlot = -1;

        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingSlot >= 0 && Slot.Value != pendingSlot) Slot.Value = pendingSlot;
            Register();
            Slot.OnValueChanged += (a, b) => Register();
            Session.StateChanged += Refresh;
            Session.EventReceived += OnEvent;
            Session.LocalSlotChanged += Refresh;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            Session.StateChanged -= Refresh;
            Session.EventReceived -= OnEvent;
            Session.LocalSlotChanged -= Refresh;
            if (_registeredSlot >= 0 && Session.Avatars.TryGetValue(_registeredSlot, out var a) && a == this)
                Session.Avatars.Remove(_registeredSlot);
        }

        private void Register()
        {
            int s = SlotIndex;
            if (s < 0) return;
            if (_registeredSlot >= 0 && _registeredSlot != s && Session.Avatars.TryGetValue(_registeredSlot, out var old) && old == this)
                Session.Avatars.Remove(_registeredSlot);
            Session.Avatars[s] = this;
            _registeredSlot = s;
            Refresh();
        }

        private void Refresh()
        {
            int s = SlotIndex;
            if (!Roles.Valid(s)) return;
            var p = Session.State.players[s];
            int character = p.character >= 0 ? p.character : s;
            View.Build(s, character, p.present ? p.name : Roles.Name(s));
            View.SetVisible(Session.InStage);
        }

        private void OnEvent(GameEvent e)
        {
            int s = SlotIndex;
            if (e.type == "Signal" && e.slot == s)
            {
                View.ShowBubble(SignalText(e.s), Roles.Color(s));
            }
            else if (e.type == "Chat" && e.slot == s)
            {
                bool understand = Session.LocalSlot == s;
                View.ShowBubble(understand ? e.s : "???", understand ? Color.white : new Color(0.9f, 0.6f, 0.6f), 3f);
            }
            else if (e.type == "LetterSent" && e.slot == s)
            {
                View.ShowBubble("✈ " + e.s, UI.Gold, 1.6f);
            }
            else if (e.type == "LetterArrive" && e.target == s)
            {
                View.ShowBubble("▶ " + e.s, UI.Gold, 1.6f);
            }

            if (!IsOwner || e.slot != s) return;
            if (e.type == "Teleport")
            {
                var pos = new Vector3(e.pos.x, e.pos.y, 0f);
                if (_nt != null && IsSpawned) _nt.Teleport(pos, Quaternion.identity, transform.localScale);
                else transform.position = pos;
                _knockUntil = 0f;
                _path = null;
                // 내가 조종하는 아바타가 순간이동하면 카메라도 바로 따라간다(스테이지 시작 등)
                if (IsControlled && GameApp.Instance != null && GameApp.Instance.Follow != null)
                {
                    GameApp.Instance.Follow.target = transform;
                    GameApp.Instance.Follow.SnapToTarget();
                }
            }
            else if (e.type == "Knock")
            {
                _knockDir = e.pos.V;
                _knockUntil = Time.time + 0.35f;
            }
        }

        public static string SignalText(string id)
        {
            switch (id)
            {
                case "GO": return "GO ▶";
                case "STOP": return "STOP ■";
                case "OK": return "✓ 확인";
                case "ASK": return "? 다시";
                case "WARN": return "⚠ 경고";
                case "L": return "← 왼쪽";
                case "R": return "→ 오른쪽";
                case "U": return "↑ 위";
                case "D": return "↓ 아래";
                default: return id;
            }
        }

        private void Update()
        {
            bool inStage = Session.InStage;
            if (View != null) View.SetVisible(inStage);

            if (!IsOwner)
            {
                if (View != null) View.UpdateRemote();
                return;
            }
            if (!inStage) return;

            var st = Session.St;
            float dt = Time.deltaTime;
            bool controlled = IsControlled;
            Vector2 delta = Vector2.zero;
            Vector2 pos = transform.position;

            if (Time.time < _knockUntil)
            {
                delta = _knockDir * 7f * dt;
            }
            else if (st.moveMode == 2)
            {
                delta = Toward(pos, st.rail[SlotIndex].V, Speed * dt, 0.08f);
            }
            else if (st.moveMode == 1)
            {
                if (controlled) delta = ReadInput() * Speed * dt;
                else delta = BotBrain.Decide(this, pos, dt);
            }

            Vector2 next = pos;
            if (delta.sqrMagnitude > 0f)
            {
                var map = Session.Map;
                next = map != null ? map.Move(pos, delta, StageMaps.AvatarRadius) : pos + delta;
                if (!controlled && st.moveMode == 1)
                {
                    float progress = (next - pos).magnitude;
                    if (progress < delta.magnitude * 0.3f)
                    {
                        _stuckTimer += dt;
                        if (_stuckTimer > 0.25f)
                        {
                            Vector2 perp = new Vector2(-delta.y, delta.x).normalized * _sidestep * Speed * dt;
                            var alt = map != null ? map.Move(pos, perp, StageMaps.AvatarRadius) : pos + perp;
                            if ((alt - pos).sqrMagnitude > 1e-6f) next = alt; else _sidestep = -_sidestep;
                            if (_stuckTimer > 1.2f) { _sidestep = -_sidestep; _stuckTimer = 0f; _path = null; }
                        }
                    }
                    else _stuckTimer = 0f;
                }
            }
            transform.position = new Vector3(next.x, next.y, 0f);
            View.SetMoving((next - pos).sqrMagnitude > 1e-7f, next.x - pos.x, next.y - pos.y);

            if (controlled)
            {
                CurrentPrompt = StageInteractions.Get(SlotIndex, next);
                HandleKeys(st);
            }
            else CurrentPrompt = default;
        }

        private static Vector2 ReadInput()
        {
            if (InteractionState.InputLocked) return Vector2.zero;
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            Vector2 mv = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1f;
            return Vector2.ClampMagnitude(mv, 1f);
        }

        private void HandleKeys(StageState st)
        {
            if (InteractionState.InputLocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.eKey.wasPressedThisFrame && CurrentPrompt.cmd != null) Session.Send(CurrentPrompt.cmd);
            if (kb.spaceKey.wasPressedThisFrame && st.stage == 1 && st.step == 3) Session.Send("Mash");
        }

        public static Vector2 Toward(Vector2 from, Vector2 to, float maxStep, float stopDist)
        {
            Vector2 d = to - from;
            float dist = d.magnitude;
            if (dist <= stopDist) return Vector2.zero;
            return d / dist * Mathf.Min(maxStep, dist);
        }

        /// <summary>봇: 길찾기 경로를 따라 목표로 한 걸음 이동한 변위를 돌려준다.</summary>
        public Vector2 StepAlongPath(Vector2 pos, Vector2 goal, float step, float stopDist)
        {
            if (Vector2.Distance(pos, goal) <= stopDist) { _path = null; return Vector2.zero; }
            var map = Session.Map;
            if (map == null) return Toward(pos, goal, step, stopDist);

            _pathAge += Time.deltaTime;
            bool stale = _path == null || _pathMap != map || Vector2.Distance(_pathGoal, goal) > 0.6f || _pathAge > 3f;
            if (stale)
            {
                _path = map.FindPath(pos, goal, StageMaps.AvatarRadius);
                _pathGoal = goal;
                _pathMap = map;
                _pathAge = 0f;
            }
            if (_path == null || _path.Count == 0) return Toward(pos, goal, step, stopDist);

            while (_path.Count > 1 && Vector2.Distance(pos, _path[0]) < 0.35f) _path.RemoveAt(0);
            Vector2 wp = _path[0];
            if (_path.Count == 1) return Toward(pos, wp, step, stopDist);
            return Toward(pos, wp, step, 0.05f);
        }
    }

    /// <summary>
    /// 호스트가 조종하지 않는 봇 아바타의 이동 두뇌(혼자/부분 테스트용). 길찾기(BFS)로 벽을 피해 간다.
    ///  - 도착 원이 의미 있는 단계에서 리더(조종 중인 아바타)가 자기 원에 들어가면 나도 내 원으로.
    ///  - Stage 2 수문 방: 각자 맡은 장치 앞. Stage 3: 리더가 있는 구역의 내 확인 패널. Stage 4: 내 장치 → 내 키패드.
    ///  - 그 외에는 리더를 따라다닌다(너무 멀면 근처로 순간이동).
    /// </summary>
    public static class BotBrain
    {
        private static readonly Vector2[] Tmp = new Vector2[4];
        private const int Op = 0, In = 1, Na = 2, Ob = 3;

        public static Vector2 Decide(AvatarNet me, Vector2 pos, float dt)
        {
            var st = Session.St;
            var map = Session.Map;
            int slot = me.SlotIndex;
            float step = AvatarNet.Speed * 0.92f * dt;
            if (map == null) return Vector2.zero;

            var leader = Session.ControlledAvatar;
            Vector2? leaderPos = leader != null && leader != me ? (Vector2?)leader.transform.position : null;

            Vector2? goal = ChooseGoal(st, map, slot, pos, leaderPos, out float stop);
            if (goal == null)
            {
                // 리더 따라가기
                if (leaderPos == null) return Vector2.zero;
                float dist = Vector2.Distance(pos, leaderPos.Value);
                if (dist > 18f)
                {
                    me.transform.position = new Vector3(leaderPos.Value.x - 0.6f * (slot + 1), leaderPos.Value.y, 0f);
                    return Vector2.zero;
                }
                if (dist <= 2.4f) return Vector2.zero;
                return me.StepAlongPath(pos, leaderPos.Value + StageMaps.SlotOffsets[slot] * 1.2f, step, 0.3f);
            }
            return me.StepAlongPath(pos, goal.Value, step, stop);
        }

        private static Vector2? ChooseGoal(StageState st, MapData map, int slot, Vector2 pos, Vector2? leaderPos, out float stop)
        {
            stop = 0.15f;
            int leaderSlot = Session.LocalSlot;

            // 1) 도착 원: 리더가 자기 원에 들어가면 나도 내 원으로
            if (ZonesActive(st))
            {
                Vector2[] zones = map.zones;
                if (st.stage == 3 && leaderPos != null)
                {
                    int sec = map.SectorAt(leaderPos.Value);
                    if (sec >= 0) { StageMaps.SectorZones(sec, Tmp); zones = Tmp; }
                }
                bool leaderInZone = leaderPos == null || (Roles.Valid(leaderSlot) && Vector2.Distance(leaderPos.Value, zones[leaderSlot]) <= map.zoneRadius + 0.3f);
                if (leaderInZone) return zones[slot];
            }

            switch (st.stage)
            {
                case 2:
                    if (st.step == 0)
                    {
                        string station = slot == Op ? "console" : (slot == Ob ? "wheelA" : (slot == In ? "wheelB" : ""));
                        if (!string.IsNullOrEmpty(station))
                        {
                            var p = map.Prop(station);
                            if (p != null) { stop = 0.25f; return p.pos + new Vector2(0f, -1.1f); }
                        }
                    }
                    break;

                case 3:
                    if ((slot == Ob || slot == Na) && leaderPos != null)
                    {
                        int sec = map.SectorAt(leaderPos.Value);
                        if (sec >= 0 && sec != StageMaps.Stage3StartSector)
                        {
                            bool done = slot == Ob ? (st.confirmObs && st.confirmObsSector == sec) : (st.confirmNav && st.confirmNavSector == sec);
                            if (!done)
                            {
                                var p = map.Prop((slot == Ob ? "confObs" : "confNav") + sec);
                                if (p != null) { stop = 0.3f; return p.pos + new Vector2(0f, -0.8f); }
                            }
                        }
                    }
                    break;

                case 4:
                    if (slot >= 1 && slot <= 3)
                    {
                        if (!st.device[slot])
                        {
                            var d = map.Prop("dev" + slot);
                            if (d != null) { stop = 0.35f; return d.pos + new Vector2(0f, -1.0f); }
                        }
                        else if (st.devicesDone && !st.codeDone)
                        {
                            var k = map.Prop("pad" + slot);
                            if (k != null) { stop = 0.3f; return k.pos + new Vector2(-0.9f, 0f); }
                        }
                    }
                    break;
            }
            return null;
        }

        private static bool ZonesActive(StageState st)
        {
            switch (st.stage)
            {
                case 1: return st.step == 4;
                case 2: return st.step == 1;
                case 3: return true;
                case 4: return st.codeDone;
                default: return false;
            }
        }
    }
}
