using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Paradox.Net
{
    /// <summary>
    /// 네트워크 아바타(Fusion). 각 플레이어가 자기 아바타를 스폰해 그 StateAuthority 가 되고, 권한자가 직접 움직인다(NetworkTransform 이 복제).
    /// 방장은 봇 슬롯의 아바타도 소유하므로 1~4 키로 조종 대상을 바꿀 수 있고, 조종하지 않는 봇은 BotBrain이 움직인다.
    /// 위험 구역(침수율 70% 이상인 방) 안에서는 이동 속도가 절반이 된다. E 또는 좌클릭으로 발광 중인 담당 오브젝트의 팝업을 연다.
    /// </summary>
    public class AvatarNet : NetworkBehaviour
    {
        /// <summary>역할 슬롯. 스폰하는 쪽이 OnBeforeSpawned 에서 채운다.</summary>
        [Networked] public int Slot { get; set; } = -1;

        public const float Speed = 4.3f;

        public int SlotIndex => _slot;
        private int _slot = -1;

        /// <summary>이 아바타의 권한자가 나인가(내 아바타 또는 방장이 소유한 봇).</summary>
        public bool IsOwner => Object != null && Object.IsValid && Object.HasStateAuthority;
        public bool IsSpawned => Object != null && Object.IsValid;
        /// <summary>권한자의 PlayerId(테스트·진단용).</summary>
        public int OwnerId => IsSpawned ? Object.StateAuthority.PlayerId : -1;
        public AvatarView View { get; private set; }
        public StageInteractions.Prompt CurrentPrompt { get; private set; }
        public bool IsControlled => IsOwner && Session.LocalSlot == SlotIndex;

        /// <summary>지금 내가 서 있는 방이 위험 구역인가(속도 절반·비네트). 소유자만 갱신한다.</summary>
        public bool InDanger { get; private set; }

        /// <summary>이번 프레임 이동 속도(테스트 훅: 위험 구역이면 Speed × 0.5). 소유자만 갱신한다.</summary>
        public float CurrentSpeed { get; private set; } = Speed;

        private NetworkTransform _nt;
        private int _registeredSlot = -1;
        private float _stuckTimer;
        private int _sidestep = 1;

        // 봇 길찾기
        private List<Vector2> _path;
        private Vector2 _pathGoal;
        private float _pathAge;
        private MapData _pathMap;

        // 봇 목표 캐시(BotBrain이 쓴다)
        [System.NonSerialized] public Vector2? botGoal;
        [System.NonSerialized] public float botStop = 0.15f;
        [System.NonSerialized] public float botThinkAt;

        private void Awake()
        {
            _nt = GetComponent<NetworkTransform>();
            View = GetComponent<AvatarView>() ?? gameObject.AddComponent<AvatarView>();
        }

        public override void Spawned()
        {
            _slot = Slot;
            Register();
            Session.StateChanged += Refresh;
            Session.EventReceived += OnEvent;
            Session.LocalSlotChanged += Refresh;
            Refresh();
            // 스폰 전에 도착해 쌓인 Teleport(스폰 자리) 반영
            if (IsOwner && Session.PendingTeleports.TryGetValue(_slot, out var pend))
            {
                Session.PendingTeleports.Remove(_slot);
                MoveTo(pend);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
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
            // 캐릭터 선택은 없다 — 항상 슬롯 번호(역할 전용 픽셀 시트)
            View.Build(s, s, p.present ? p.name : Roles.Name(s));
            View.SetVisible(Session.InStage);
        }

        // ---------------------------------------------------------------- 이벤트 → 말풍선

        private void OnEvent(GameEvent e)
        {
            int s = SlotIndex;
            int me = Session.LocalSlot;
            switch (e.type)
            {
                case "Signal":
                    BotBrain.NoteSignal(e.slot, e.s);
                    if (e.slot == s) View.ShowCard(e.s);
                    break;

                case "Chat":
                    if (e.slot == s)
                    {
                        bool understand = Comms.CanUnderstand(me, e.slot);
                        if (understand) View.ShowBubble(e.s, Color.white, 3f);
                        else View.ShowBubble(Comms.Garble(e.s), new Color(0.92f, 0.6f, 0.62f), 3f, "(외국어)");
                    }
                    break;

                case "Color":
                    if (e.slot == s)
                    {
                        // 색 이름은 통역(수신자)과 보낸 사람만 본다. 다른 사람에게는 "[색 신고]"
                        bool see = me == (int)GameRole.Interpreter || me == e.slot;
                        if (see) View.ShowBubble("■ " + Stage1Logic.ColorName(e.a), LampColor(e.a), 2.6f, "→ 통역");
                        else View.ShowBubble("[색 신고]", UI.InkDim, 2.2f, "→ 통역");
                    }
                    break;

                case "NumberCard":
                    if (e.slot == s)
                    {
                        bool see = me == (int)GameRole.Operator || me == e.slot;
                        if (see) View.ShowBubble($"숫자 카드 {e.a}", UI.Gold, 2.6f, "→ 조작자");
                        else View.ShowBubble("[카드]", UI.InkDim, 2.2f, "→ 조작자");
                    }
                    break;

                case "DirCard":
                    if (e.slot == s)
                    {
                        bool see = me == (int)GameRole.Operator || me == e.slot;
                        if (see) View.ShowBubble("방향 카드 " + Stage1Logic.DirGlyph(e.a), UI.Gold, 2.6f, "→ 조작자");
                        else View.ShowBubble("[카드]", UI.InkDim, 2.2f, "→ 조작자");
                    }
                    break;

                case "Ping":
                    if (e.slot == s)
                    {
                        // D5: 방 이름은 현지어(??? 원칙) — 내비게이터 자신의 화면에만 방 이름, 나머지는 "→ 여기로!" 카드 + 방 입구 비콘 + 미니맵 핀
                        if (me == (int)GameRole.Navigator) View.ShowBubble("→ " + StageWorld.RoomLabel(e.a), new Color(0.95f, 0.77f, 0.45f), 3f, "여기로!");
                        else View.ShowBubble("→ 여기로!", new Color(0.95f, 0.77f, 0.45f), 3f, "입구의 표시를 따라가요");
                    }
                    break;
            }

            if (!IsOwner || e.slot != s) return;
            if (e.type == "Teleport") MoveTo(e.pos.V);
        }

        /// <summary>권한자가 위치를 직접 옮긴다(NetworkTransform.Teleport 가 보간 없이 모두에게 알린다). 테스트 훅으로도 쓴다.</summary>
        public void MoveTo(Vector2 p)
        {
            var pos = new Vector3(p.x, p.y, 0f);
            transform.position = pos;
            if (_nt != null && IsSpawned) _nt.Teleport(pos, null);
            _path = null;
            botGoal = null;
            botThinkAt = 0f;
            // 고정 카메라가 아닌 화면에서는 순간이동한 내 캐릭터를 카메라가 바로 따라간다
            if (IsControlled && GameApp.Instance != null && GameApp.Instance.Follow != null)
            {
                GameApp.Instance.Follow.target = transform;
                GameApp.Instance.Follow.SnapToTarget();
            }
        }

        private static Color LampColor(int a)
        {
            switch (a)
            {
                case 0: return new Color(1f, 0.42f, 0.4f);
                case 1: return new Color(1f, 0.88f, 0.4f);
                case 2: return new Color(0.5f, 0.92f, 0.55f);
                case 3: return new Color(0.5f, 0.68f, 1f);
                default: return Color.white;
            }
        }

        /// <summary>휠 신호 id → 로그용 짧은 표기(기호 + 단어). 옛 호출 호환.</summary>
        public static string SignalText(string id) => AvatarView.CardSymbol(id) + " " + AvatarView.CardWord(id);

        // ---------------------------------------------------------------- 매 프레임

        private void Update()
        {
            if (!IsSpawned) return;
            bool inStage = Session.InStage;
            if (View != null) View.SetVisible(inStage);

            if (!IsOwner)
            {
                if (View != null) View.UpdateRemote();
                return;
            }
            if (!inStage) { InDanger = false; CurrentPrompt = default; return; }

            var st = Session.St;
            var map = Session.Map;
            float dt = Time.deltaTime;
            bool controlled = IsControlled;
            Vector2 pos = transform.position;

            // 위험 구역: 속도 절반(서버 규칙 IsDanger를 그대로 표시에 쓴다)
            int room = map != null ? map.RoomAt(pos) : -1;
            InDanger = room >= 0 && Stage1Logic.IsDanger(st, room, Session.Clock);
            float speed = InDanger ? Speed * 0.5f : Speed;
            CurrentSpeed = speed;

            Vector2 delta = Vector2.zero;
            bool canMove = st.moveMode != 0 && !st.cleared && !st.failed;
            if (canMove)
            {
                if (controlled) delta = ReadInput() * speed * dt;
                // 조종하지 않는 "봇" 슬롯만 BotBrain이 움직인다(명세 9절). 호스트가 봇 자리로 전환한 동안 사람 슬롯의 아바타는 제자리.
                else if (Roles.Valid(SlotIndex) && Session.State.players[SlotIndex].isBot) delta = BotBrain.Decide(this, pos, speed, dt);
            }

            Vector2 next = pos;
            if (delta.sqrMagnitude > 0f)
            {
                next = map != null ? map.Move(pos, delta, StageMaps.AvatarRadius) : pos + delta;
                if (!controlled)
                {
                    // 봇이 벽에 걸리면 옆걸음으로 빠져나온다
                    float progress = (next - pos).magnitude;
                    if (progress < delta.magnitude * 0.3f)
                    {
                        _stuckTimer += dt;
                        if (_stuckTimer > 0.25f)
                        {
                            Vector2 perp = new Vector2(-delta.y, delta.x).normalized * _sidestep * speed * dt;
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
                HandleKeys();
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

        /// <summary>E 키 / 좌클릭 상호작용. 팝업이 열려 있거나 채팅 입력 중(InputLocked)이면 무시한다.</summary>
        private void HandleKeys()
        {
            if (InteractionState.InputLocked) return;

            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) Trigger(CurrentPrompt);

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI())
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector2 wp = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    string id = StageInteractions.ClickTarget(SlotIndex, transform.position, wp);
                    if (id != null) OpenPopup(id);
                }
            }
        }

        private static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        private void Trigger(StageInteractions.Prompt p)
        {
            if (!string.IsNullOrEmpty(p.popupId)) OpenPopup(p.popupId);
            else if (p.cmd != null) Session.Send(p.cmd);
        }

        private static void OpenPopup(string propId)
        {
            var screen = StageScreen.Instance;
            if (screen == null || screen.IsPopupOpen) return;
            screen.OpenPopup(propId);
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
                // D7: 침수 70% 이상인 방은 피해서 간다(피할 길이 없으면 그냥 간다)
                var avoid = BotBrain.DangerRects(map, Session.St, Session.Clock, pos, goal);
                _path = avoid != null ? map.FindPath(pos, goal, StageMaps.AvatarRadius, avoid) : null;
                if (_path == null) _path = map.FindPath(pos, goal, StageMaps.AvatarRadius);
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
    /// 호스트가 조종하지 않는 봇 아바타의 이동 두뇌(명세 9절). 명령은 서버 BotDriver가 내고, 여기서는 "어디로 걸어갈지"만 정한다.
    ///  - 지시 게이팅: 조작자·통역·관측자 봇은 lastPingRoom == 목표 방, 또는 내비의 GO 신호 이후, 또는 단계 시작 25초 후에 움직인다.
    ///  - 내비 봇은 단계 시작 2초 후부터 행동(현황판 → 아트리움 대기 → 배전함).
    ///  - 목표 = 담당 오브젝트 위치 + (0, −0.9). 최종 집결은 GatherCenter(slot) 추적(stop 0.25). 목표는 0.5초마다 다시 계산.
    ///  - botsAuto=false(테스트가 직접 조작)면 움직이지 않는다.
    /// </summary>
    public static class BotBrain
    {
        private const int Op = 0, In = 1, Na = 2, Ob = 3;
        public const float ThinkInterval = 0.5f;
        public const float InstructionTimeout = 25f;
        public const float NavigatorDelay = 2f;
        public static readonly Vector2 PropOffset = new Vector2(0f, -0.9f);
        public static readonly Vector2 AtriumWait = StageMaps.Px(650f, 560f);   // 홀 왼쪽(현수막 아래) — 집결 궤도·문 앞을 비켜 선다

        // 단계 시작 시각 추적(클라이언트 관찰 기준). 새 스테이지 판정은 seed·retries 로 한다(startClock 은 브리핑 게이트 해제 때 다시 찍히므로).
        private static int _seenStep = -1;
        private static int _seenSeed = int.MinValue;
        private static int _seenRetries = -1;
        private static float _stepStartClock;
        private static float _lastGoClock = -999f;

        /// <summary>휠 신호를 관찰한다(내비의 GO가 지시 게이팅을 푼다).</summary>
        public static void NoteSignal(int slot, string id)
        {
            if (slot == Na && !string.IsNullOrEmpty(id) && id.Trim().ToUpperInvariant() == "GO")
                _lastGoClock = Session.Clock;
        }

        private static void TrackStep(StageState st, float clock)
        {
            bool newStage = _seenSeed != st.seed || _seenRetries != st.retries;
            if (newStage)
            {
                _seenSeed = st.seed;
                _seenRetries = st.retries;
                _seenStep = st.step;
                _stepStartClock = clock;
                _lastGoClock = -999f;
            }
            else if (_seenStep != st.step)
            {
                _seenStep = st.step;
                _stepStartClock = clock;
            }
        }

        /// <summary>단계가 시작된 뒤 흐른 서버 시간.</summary>
        public static float StepElapsed(StageState st, float clock)
        {
            TrackStep(st, clock);
            return clock - _stepStartClock;
        }

        /// <summary>조작자·통역·관측자 봇이 목표 방으로 움직여도 되는가.</summary>
        public static bool Instructed(StageState st, int targetRoom, float clock)
        {
            TrackStep(st, clock);
            if (st.lastPingRoom == targetRoom) return true;
            if (_lastGoClock >= _stepStartClock) return true;
            return clock - _stepStartClock >= InstructionTimeout;
        }

        /// <summary>이번 프레임 봇의 이동 변위.</summary>
        public static Vector2 Decide(AvatarNet me, Vector2 pos, float speed, float dt)
        {
            var st = Session.St;
            var map = Session.Map;
            if (map == null || st == null) return Vector2.zero;
            if (!Session.State.botsAuto) return Vector2.zero;
            if (!Roles.Valid(me.SlotIndex) || !Session.State.players[me.SlotIndex].isBot) return Vector2.zero;   // 사람 슬롯은 절대 자동 이동하지 않는다
            if (st.cleared || st.failed) return Vector2.zero;

            float clock = Session.Clock;
            if (Time.time >= me.botThinkAt)
            {
                me.botThinkAt = Time.time + ThinkInterval;
                me.botGoal = ChooseGoal(st, map, me.SlotIndex, clock, out me.botStop);
            }
            if (me.botGoal == null) return Vector2.zero;
            float step = speed * 0.92f * dt;
            return me.StepAlongPath(pos, me.botGoal.Value, step, me.botStop);
        }

        /// <summary>
        /// 길찾기에서 피할 사각형: 침수율 70% 이상인 방(내가 서 있는 방·목표가 있는 방은 제외). 없으면 null.
        /// </summary>
        public static List<Rect> DangerRects(MapData map, StageState st, float clock, Vector2 pos, Vector2 goal)
        {
            if (map == null || st == null || map.rooms == null) return null;
            List<Rect> list = null;
            for (int r = 0; r < map.rooms.Length; r++)
            {
                if (Stage1Logic.Flood(st, r, clock) < Stage1Logic.DangerFlood) continue;
                var rect = map.rooms[r];
                if (rect.Contains(pos) || rect.Contains(goal)) continue;
                if (list == null) list = new List<Rect>();
                list.Add(rect);
            }
            return list;
        }

        /// <summary>이 슬롯이 이번 단계에서 맡은 오브젝트 id(없으면 null).</summary>
        public static string MissionProp(int slot, int step)
        {
            if (step == 0)
            {
                switch (slot) { case Op: return "panel"; case In: return "poster"; case Ob: return "lamp"; case Na: return "board"; }
            }
            else if (step == 1)
            {
                switch (slot) { case Op: return "lever"; case In: return "mural"; case Ob: return "colorlamp"; case Na: return "junction"; }
            }
            return null;
        }

        private static Vector2? ChooseGoal(StageState st, MapData map, int slot, float clock, out float stop)
        {
            stop = 0.25f;
            if (!Roles.Valid(slot)) return null;

            // 최종 집결: 내 원(움직이는 목표) 추적
            if (st.step == 2)
            {
                stop = 0.25f;
                return Stage1Logic.GatherCenter(slot, st, clock);
            }

            float elapsed = StepElapsed(st, clock);
            if (slot == Na)
            {
                if (elapsed < NavigatorDelay) return null;
                if (st.step == 0)
                {
                    // 현황판을 열고 안내데스크를 핑한 뒤에는 아트리움에서 기다린다
                    bool briefed = st.boardOpened && st.lastPingRoom == 1;
                    if (!briefed) return PropGoal(map, "board", ref stop);
                    return AtriumWait;
                }
                if (st.step == 1)
                {
                    // 모자이크방을 핑한 뒤(또는 시간이 지나면) 배전함 앞으로
                    bool go = st.lastPingRoom == 3 || elapsed >= InstructionTimeout;
                    if (!go) return AtriumWait;
                    return PropGoal(map, "junction", ref stop);
                }
                return null;
            }

            // 조작자·통역·관측자: 지시를 기다렸다가 담당 오브젝트 앞으로
            string id = MissionProp(slot, st.step);
            if (id == null) return null;
            if (StageInteractions.MissionDone(st, id)) return null;
            int targetRoom = st.step == 0 ? 1 : 3;
            if (st.step == 1 && !st.gate1Done) return null;   // 문이 아직 잠겨 있다
            if (!Instructed(st, targetRoom, clock)) return null;
            return PropGoal(map, id, ref stop);
        }

        private static Vector2? PropGoal(MapData map, string id, ref float stop)
        {
            var p = map.Prop(id);
            if (p == null) return null;
            stop = 0.25f;
            return p.pos + PropOffset;
        }
    }
}
