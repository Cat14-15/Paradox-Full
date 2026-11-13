using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 우상단 미니맵 "박물관 1층 · 내 담당 표시 / TAB(확대)".
    /// 내비게이터: 방 6개 + 이름 + 미션 ! 마커 + 위험 방 물결(현황판을 마지막으로 연 시각의 스냅샷, D-A4) + 4명 점 + 핑 비콘.
    /// 나머지: 아트리움만 밝고 다른 방은 ? 회색, 내 담당 오브젝트가 발광한 방만 색으로. 집결 지점 점선 원. 내 점만.
    /// </summary>
    public class MinimapView
    {
        private RectTransform _panel, _field;
        private Text _title;
        private MapData _map;
        private Vector2 _fieldSize = new Vector2(336f, 200f);
        private bool _big;
        private int _builtSlot = -1, _builtStep = -1;
        private bool _builtBoard, _built;
        private readonly HashSet<int> _revealed = new HashSet<int>();
        private readonly RectTransform[] _dots = new RectTransform[4];
        private readonly Image[] _danger = new Image[6];
        private readonly RectTransform[] _gather = new RectTransform[4];
        private readonly Image[] _gatherRing = new Image[4];
        private RectTransform _beacon;
        private Text _beaconText;
        private Text _legend;

        private static readonly Color[] RoomTint =
        {
            new Color(0.62f, 0.58f, 0.46f, 0.85f),  // 0 아트리움 밝은 베이지
            new Color(0.36f, 0.42f, 0.52f, 0.85f),  // 1 안내데스크 회청
            new Color(0.40f, 0.40f, 0.42f, 0.85f),  // 2 관리실 회색
            new Color(0.52f, 0.44f, 0.30f, 0.85f),  // 3 모자이크방 갈색빛
            new Color(0.24f, 0.46f, 0.50f, 0.85f),  // 4 수족관방 청록
            new Color(0.26f, 0.48f, 0.42f, 0.85f),  // 5 연못정원방 초록빛
        };

        public bool Enlarged => _big;

        /// <summary>S-05: 팝업이 열린 동안 통째로 숨긴다(상태 갱신은 계속 받는다).</summary>
        public void SetVisible(bool v)
        {
            if (_panel == null || _panel.gameObject.activeSelf == v) return;
            _panel.gameObject.SetActive(v);
        }

        public void Build(RectTransform parent)
        {
            _panel = HudKit.Box(parent, "Minimap", HudLayout.MiniAnchor, HudLayout.MiniPos, HudLayout.MiniSize, HudKit.Panel, HudKit.Border, 1f);
            _title = UI.Label(_panel, "박물관 1층 · 내 담당 표시", 12, HudKit.Amber, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(260f, 18f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
            UI.Label(_panel, "TAB", 11, HudKit.Dim, new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(60f, 18f), TextAnchor.MiddleRight, new Vector2(1f, 1f));
            _field = UI.Fixed(_panel, "Field", new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), _fieldSize);
            UI.Bg(_field, new Color(0.03f, 0.045f, 0.07f, 1f), false);
            _legend = UI.Label(_panel, "", 10, HudKit.Dim, new Vector2(0f, 0f), new Vector2(12f, 6f), new Vector2(336f, 16f), TextAnchor.MiddleLeft, new Vector2(0f, 0f));
            OnState();
        }

        public void Dispose()
        {
            if (_panel != null) Object.Destroy(_panel.gameObject);
            _panel = null;
        }

        /// <summary>TAB — 확대/축소 토글.</summary>
        public void ToggleEnlarged()
        {
            if (_panel == null) return;
            _big = !_big;
            var size = _big ? HudLayout.MiniBig : HudLayout.MiniSize;
            _panel.sizeDelta = size;
            _fieldSize = new Vector2(size.x - 24f, size.y - 80f);
            _field.sizeDelta = _fieldSize;
            ((RectTransform)_legend.transform).sizeDelta = new Vector2(size.x - 24f, 16f);
            RebuildStatic();
        }

        // ---------------------------------------------------------------- 좌표

        private Vector2 ToMap(Vector2 world)
        {
            var b = _map.bounds;
            float u = Mathf.InverseLerp(b.xMin, b.xMax, world.x);
            float v = Mathf.InverseLerp(b.yMin, b.yMax, world.y);
            return new Vector2((u - 0.5f) * _fieldSize.x, (v - 0.5f) * _fieldSize.y);
        }

        private Vector2 ToMapSize(Vector2 size)
        {
            var b = _map.bounds;
            return new Vector2(size.x / b.width * _fieldSize.x, size.y / b.height * _fieldSize.y);
        }

        private RectTransform Box(Rect r, Color c, string name = "Box")
        {
            var rt = UI.Rect(_field, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ToMap(r.center);
            rt.sizeDelta = ToMapSize(r.size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return rt;
        }

        private RectTransform Dot(Vector2 world, Color c, float size, string name = "Dot", string label = null, Sprite sprite = null)
        {
            var rt = UI.Rect(_field, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ToMap(world);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : SpriteLib.Disc(); img.color = c; img.raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var t = UI.Label(rt, label, 10, HudKit.InkDark, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 14f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
                t.fontStyle = FontStyle.Bold;
            }
            return rt;
        }

        private Text Label(Vector2 world, string text, int size, Color c, Vector2 offset)
        {
            return UI.Label(_field, text, size, c, new Vector2(0.5f, 0.5f), ToMap(world) + offset, new Vector2(90f, 26f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
        }

        // ---------------------------------------------------------------- 상태

        public void OnState()
        {
            if (_panel == null) return;
            var st = Session.St;
            int me = Session.LocalSlot;
            if (me != _builtSlot) _revealed.Clear();
            bool needRebuild = Session.Map != _map || me != _builtSlot || st.step != _builtStep || st.boardOpened != _builtBoard || !_built;
            if (needRebuild) RebuildStatic();
        }

        public void OnEvent(GameEvent e)
        {
            // 핑은 상태(lastPingRoom/lastPingClock)로 그린다 — 이벤트는 따로 처리할 것이 없다
        }

        private void RebuildStatic()
        {
            HudKit.Clear(_field);
            for (int i = 0; i < 4; i++) { _dots[i] = null; _gather[i] = null; _gatherRing[i] = null; }
            for (int i = 0; i < 6; i++) _danger[i] = null;
            _beacon = null; _beaconText = null;
            _map = Session.Map;
            var st = Session.St;
            int me = Session.LocalSlot;
            _builtSlot = me; _builtStep = st.step; _builtBoard = st.boardOpened;
            _built = _map != null;
            if (_map == null) return;
            bool nav = me == Comms.Na;
            _legend.text = nav
                ? "<color=#F2C572>!</color> 미션 방   <color=#F2C572>○</color> 현황판   <color=#4F7BC9>■</color> 침수 위험   <color=#F2C572>▼</color> 내 지시   <color=#5FD3C4>○</color> 집결"
                : $"<color=#{Roles.Hex(Roles.Valid(me) ? me : 0)}>■</color> 내 담당   <color=#3A4A66>■</color> 남의 담당 (비공개)   ○ 집결 지점";

            // 통로·문(연하게)
            foreach (var o in _map.open) Box(o, new Color(0.14f, 0.16f, 0.21f, 1f), "Open");

            // 방 6개
            var rooms = _map.rooms;
            int n = rooms != null ? Mathf.Min(rooms.Length, 6) : 0;
            for (int i = 0; i < n; i++)
            {
                bool known = nav || i == 0 || _revealed.Contains(i);
                Color c = known ? RoomTint[i] : new Color(0.16f, 0.18f, 0.22f, 1f);
                if (!nav && _revealed.Contains(i)) c = HudKit.Alpha(Roles.Color(me), 0.55f);
                Box(rooms[i], c, "Room" + i);
                if (nav)
                {
                    var t = Label(rooms[i].center, Stage1Logic.ZoneName(i), _big ? 12 : 9, Color.white, new Vector2(0f, i == 0 ? -ToMapSize(new Vector2(0f, 2.4f)).y : 0f));
                    t.fontStyle = FontStyle.Bold;
                }
                else if (!known)
                {
                    var q = Label(rooms[i].center, "?", _big ? 18 : 14, HudKit.Dim, Vector2.zero);
                    q.fontStyle = FontStyle.Bold;
                }
                else if (i == 0)
                {
                    Label(rooms[i].center, "아트리움", _big ? 11 : 9, new Color(0.3f, 0.28f, 0.22f), new Vector2(0f, -ToMapSize(new Vector2(0f, 2.4f)).y));
                }
            }

            // 벽·잠긴 문·분수
            foreach (var s in _map.solids) Box(s, new Color(0.08f, 0.09f, 0.12f, 1f), "Solid");

            if (nav)
            {
                // 위험 방 물결(수족관방·연못정원방)
                for (int r = 4; r <= 5 && r < n; r++)
                {
                    var d = Box(rooms[r], new Color(0.25f, 0.5f, 1f, 0f), "Danger" + r);
                    _danger[r] = d.GetComponent<Image>();
                }
                // 미션 ! 마커
                var missions = new List<int>();
                if (st.step == 0) missions.Add(1);
                else if (st.step == 1) { if (!st.boardOpened) missions.Add(2); missions.Add(3); }
                else missions.Add(0);
                foreach (var room in missions)
                {
                    if (room >= n) continue;
                    var pos = rooms[room].center + new Vector2(0f, rooms[room].height * 0.25f);
                    Dot(pos, HudKit.Amber, _big ? 22f : 16f, "Mission", "!");
                }
                // C5: 관문 1 동안 내 담당 현황판(관리실)은 속 빈 ○ 마커 — 관문 !(팀을 보낼 곳)와 구분되는 "나만 들를 곳"
                if (st.step == 0 && !st.boardOpened && 2 < n)
                {
                    var bp = rooms[2].center + new Vector2(0f, rooms[2].height * 0.25f);
                    Dot(bp, HudKit.Amber, _big ? 20f : 14f, "Board", null, SpriteLib.Ring(0.22f));
                }
            }
            else
            {
                // 내 담당 오브젝트(발광한 방만)
                foreach (var id in Comms.PropIds)
                {
                    if (Comms.PropSlot(id) != me) continue;
                    int room = Comms.PropRoom(id);
                    if (!_revealed.Contains(room) || Comms.MissionDone(id, st)) continue;
                    var p = _map.Prop(id);
                    if (p == null) continue;
                    var m = Dot(p.pos, Roles.Color(me), _big ? 14f : 10f, "MyProp", null, SpriteLib.White);
                    var lbl = UI.Label(m, Comms.PropName(id), 9, Roles.Color(me), new Vector2(0.5f, 0f), new Vector2(0f, -2f), new Vector2(60f, 12f), TextAnchor.UpperCenter, new Vector2(0.5f, 1f));
                    lbl.fontStyle = FontStyle.Bold;
                }
            }

            // 집결 지점: 점선 원(집결 전) / 4개 원(집결 중)
            if (st.step < 2)
            {
                float rWorld = Stage1Logic.GatherR + Stage1Logic.GatherRadius;
                var sz = ToMapSize(new Vector2(rWorld, rWorld));
                for (int k = 0; k < 14; k++)
                {
                    float a = k / 14f * Mathf.PI * 2f;
                    var pos = ToMap(StageMaps.GatherOrigin) + new Vector2(Mathf.Cos(a) * sz.x, Mathf.Sin(a) * sz.y);
                    var dd = UI.Rect(_field, "Dash");
                    dd.anchorMin = dd.anchorMax = new Vector2(0.5f, 0.5f); dd.pivot = new Vector2(0.5f, 0.5f);
                    dd.anchoredPosition = pos; dd.sizeDelta = new Vector2(3f, 3f);
                    var di = dd.gameObject.AddComponent<Image>(); di.sprite = SpriteLib.Disc(); di.color = HudKit.Teal; di.raycastTarget = false;
                }
                Label(StageMaps.GatherOrigin, "집결 지점", _big ? 11 : 9, HudKit.Teal, new Vector2(0f, -sz.y - 8f));
            }
            else
            {
                float rad = Stage1Logic.GatherRadius;
                var sz = ToMapSize(new Vector2(rad * 2f, rad * 2f));
                for (int i = 0; i < 4; i++)
                {
                    var g = Dot(Stage1Logic.GatherCenter(i, st, Session.Clock), Roles.Color(i), Mathf.Max(sz.x, 8f), "Gather" + i, null, SpriteLib.Ring(0.22f));
                    _gather[i] = g; _gatherRing[i] = g.GetComponent<Image>();
                }
            }

            // 핑 비콘(모두)
            _beacon = UI.Rect(_field, "Beacon");
            _beacon.anchorMin = _beacon.anchorMax = new Vector2(0.5f, 0.5f); _beacon.pivot = new Vector2(0.5f, 0.5f); _beacon.sizeDelta = new Vector2(60f, 30f);
            _beaconText = UI.Label(_beacon, "▼", 16, HudKit.Amber, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 30f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            _beaconText.fontStyle = FontStyle.Bold;
            _beacon.gameObject.SetActive(false);

            // 아바타 점: 내비는 4명, 나머지는 나만
            for (int i = 0; i < 4; i++)
            {
                if (!nav && i != me) continue;
                var c = Roles.Color(i);
                _dots[i] = Dot(_map.spawns[i], c, _big ? 14f : 10f, "Avatar");
                var ring = UI.Rect(_dots[i], "Ring");
                ring.anchorMin = Vector2.zero; ring.anchorMax = Vector2.one; ring.offsetMin = new Vector2(-3, -3); ring.offsetMax = new Vector2(3, 3);
                var ri = ring.gameObject.AddComponent<Image>();
                ri.sprite = SpriteLib.Ring(0.3f); ri.color = Color.white; ri.raycastTarget = false;
                ring.gameObject.SetActive(i == me);
            }
        }

        // ---------------------------------------------------------------- 매 프레임

        public void Tick()
        {
            if (_panel == null || _map == null) return;
            var st = Session.St;
            int me = Session.LocalSlot;
            float clock = Session.Clock;

            // 아바타 점
            for (int i = 0; i < 4; i++)
            {
                if (_dots[i] == null) continue;
                var p = Session.PosOf(i);
                if (p != null) _dots[i].anchoredPosition = ToMap(p.Value);
            }

            // 내 담당 오브젝트가 처음 발광하면 그 방이 색으로 드러난다
            if (me != Comms.Na)
            {
                var myPos = Session.PosOf(me);
                if (myPos != null)
                {
                    int room = _map.RoomAt(myPos.Value);
                    foreach (var id in Comms.PropIds)
                    {
                        if (Comms.PropSlot(id) != me || Comms.PropRoom(id) != room || Comms.MissionDone(id, st)) continue;
                        if (_revealed.Add(room)) { RebuildStatic(); return; }
                    }
                }
            }

            // 위험 물결 — 실시간이 아니라 내비가 마지막으로 현황판을 연 시각의 스냅샷(D-A4 "가서 봐야 아는"). 열기 전에는 물결 없음
            for (int r = 4; r <= 5; r++)
            {
                if (_danger[r] == null) continue;
                bool danger = Stage1Logic.IsDangerSnapshot(st, r);
                float a = danger ? 0.35f + 0.25f * Mathf.Sin(Time.time * 4f + r) : 0f;
                _danger[r].color = new Color(0.25f, 0.5f, 1f, a);
            }

            // 집결 원
            if (_gather[0] != null && st.step >= 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_gather[i] == null) continue;
                    _gather[i].anchoredPosition = ToMap(Stage1Logic.GatherCenter(i, st, clock));
                    var p = Session.PosOf(i);
                    bool inside = p != null && Stage1Logic.InGather(i, p.Value, st, clock);
                    _gatherRing[i].color = HudKit.Alpha(Roles.Color(i), inside ? 1f : 0.45f + 0.15f * Mathf.Sin(Time.time * 6f));
                }
            }

            // 핑 비콘(10초)
            if (_beacon != null)
            {
                bool show = st.lastPingRoom >= 0 && clock - st.lastPingClock < 10f && clock >= st.lastPingClock;
                if (_beacon.gameObject.activeSelf != show) _beacon.gameObject.SetActive(show);
                if (show)
                {
                    var door = Stage1Logic.RoomDoor(st.lastPingRoom);
                    _beacon.anchoredPosition = ToMap(door) + new Vector2(0f, 8f + 3f * Mathf.Sin(Time.time * 6f));
                    _beaconText.color = HudKit.Alpha(HudKit.Amber, Mathf.Sin(Time.time * 8f) > -0.4f ? 1f : 0.3f);
                }
            }
        }
    }
}
