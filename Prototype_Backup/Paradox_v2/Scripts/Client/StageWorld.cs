using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 스테이지 월드 렌더러. Session.Map(지형)과 상태로부터 바닥·벽·표지판·장치·도착 원·안개를 그린다.
    /// 지형 키가 바뀌면 통째로 다시 만들고, 그 외에는 상태 변화에 맞춰 사물만 갱신한다.
    /// 표지판 글자는 이번 스테이지의 현지인에게만 원래대로 보이고 나머지에게는 ???로 보인다.
    /// </summary>
    public class StageWorld
    {
        private class PropView
        {
            public MapProp prop;
            public GameObject go;
            public SpriteRenderer icon;
            public TextMesh text;
            public TextMesh sub;
        }

        private GameObject _root;
        private MapData _map;
        private readonly Dictionary<string, PropView> _props = new Dictionary<string, PropView>();
        private readonly List<SpriteRenderer> _zoneDiscs = new List<SpriteRenderer>();
        private readonly List<TextMesh> _zoneLabels = new List<TextMesh>();
        private readonly List<SpriteRenderer> _tints = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _fires = new List<SpriteRenderer>();
        private SpriteRenderer _fog;
        private float _fogRadius = -1f;
        private float _jiggleT;
        private int _zoneSector = -1;

        private static readonly Color Green = new Color(0.35f, 0.85f, 0.45f);
        private static readonly Color Yellow = new Color(0.95f, 0.85f, 0.3f);
        private static readonly Color Red = new Color(0.95f, 0.3f, 0.3f);

        public void Build()
        {
            Dispose();
            _root = new GameObject("StageWorld");
            Rebuild();
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _map = null;
            _props.Clear();
            _zoneDiscs.Clear(); _zoneLabels.Clear(); _tints.Clear(); _fires.Clear();
            _fog = null;
            _fogRadius = -1f;
        }

        /// <summary>상태 변화 시 호출. 지형이 바뀌면 재생성, 아니면 사물만 갱신.</summary>
        public void Refresh()
        {
            if (_root == null) return;
            if (Session.Map != _map) Rebuild();
            else RefreshProps();
        }

        private void Rebuild()
        {
            if (_root == null) return;
            for (int i = _root.transform.childCount - 1; i >= 0; i--) Object.Destroy(_root.transform.GetChild(i).gameObject);
            _props.Clear();
            _zoneDiscs.Clear(); _zoneLabels.Clear(); _tints.Clear(); _fires.Clear();
            _fog = null; _fogRadius = -1f;
            _map = Session.Map;
            if (_map == null) return;

            var st = Session.St;
            var def = StageDefs.Get(st.stage);
            var t = _root.transform;

            // 배경(바위/어둠)과 바닥
            var bg = SpriteLib.Quad(t, "Rock", _map.bounds.center, _map.bounds.size + new Vector2(40f, 40f), def.rock, -1100);
            foreach (var o in _map.open)
            {
                var go = new GameObject("Floor");
                go.transform.SetParent(t, false);
                go.transform.position = new Vector3(o.center.x, o.center.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteLib.FloorTile(def.floorA, def.floorB);
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = o.size;
                sr.sortingOrder = -1000;
            }

            // 벽/장애물
            foreach (var s in _map.solids)
            {
                var wall = SpriteLib.Quad(t, "Wall", s.center, s.size, Color.Lerp(def.rock, Color.white, 0.22f), YDepthSort.OrderFor(s.yMin));
                SpriteLib.Quad(t, "WallTop", new Vector2(s.center.x, s.yMax - 0.12f), new Vector2(s.width, 0.24f), Color.Lerp(def.rock, Color.white, 0.4f), YDepthSort.OrderFor(s.yMin) + 1);
            }

            // 위험 구역(침수)
            if (st.stage == 2)
                foreach (var h in _map.hazards)
                    SpriteLib.Quad(t, "Flood", h.center, h.size, new Color(def.flood.r, def.flood.g, def.flood.b, 0.45f), -980);

            // Stage 3 구역 색(관측자 고글) — 안개 위에 그려서 고글로 '연기 너머'를 본다
            if (st.stage == 3)
            {
                for (int i = 0; i < _map.sectors.Length; i++)
                {
                    var r = _map.sectors[i];
                    var tint = SpriteLib.Quad(t, "Tint" + i, r.center, r.size, Color.clear, 955);
                    _tints.Add(tint);
                }
            }

            // Stage 4 불
            if (st.stage == 4)
            {
                foreach (var f in st.fires)
                {
                    var fire = SpriteLib.Make(t, "Fire", f.V, SpriteLib.Icon("flame"), Color.white, 1.7f, YDepthSort.OrderFor(f.y - 0.6f));
                    _fires.Add(fire);
                    var glow = SpriteLib.Make(t, "FireGlow", f.V, SpriteLib.Disc(0.6f), new Color(1f, 0.5f, 0.15f, 0.25f), 3.2f, -979);
                    glow.transform.SetParent(fire.transform, true);
                }
            }

            // 사물
            foreach (var p in _map.props) BuildProp(p);

            // 도착 원
            BuildZones();

            // 안개
            var fogGo = new GameObject("Fog");
            fogGo.transform.SetParent(t, false);
            _fog = fogGo.AddComponent<SpriteRenderer>();
            _fog.sortingOrder = 950;
            _fog.color = new Color(0.02f, 0.02f, 0.04f, 0.97f);
            _fog.enabled = false;

            RefreshProps();
        }

        private void BuildZones()
        {
            var st = Session.St;
            Vector2[] zones = _map.zones;
            if (st.stage == 3)
            {
                var me = Session.PosOf(Session.LocalSlot);
                int sec = me != null ? _map.SectorAt(me.Value) : StageMaps.Stage3StartSector;
                if (sec < 0) sec = StageMaps.Stage3StartSector;
                _zoneSector = sec;
                zones = new Vector2[4];
                StageMaps.SectorZones(sec, zones);
            }
            for (int i = 0; i < 4; i++)
            {
                var c = Roles.Color(i);
                var disc = SpriteLib.Make(_root.transform, "Zone" + i, zones[i], SpriteLib.Ring(0.16f), new Color(c.r, c.g, c.b, 0.85f), _map.zoneRadius * 2f, -990);
                disc.transform.localScale = new Vector3(_map.zoneRadius * 2f, _map.zoneRadius * 1.1f, 1f);
                var fill = SpriteLib.Make(disc.transform, "Fill", zones[i], SpriteLib.Disc(0.3f), new Color(c.r, c.g, c.b, 0.18f), 1f, -991);
                fill.transform.localScale = Vector3.one;
                _zoneDiscs.Add(disc);
                var label = WorldText.Make(_root.transform, Roles.Short(i), new Vector3(zones[i].x, zones[i].y - 0.05f, 0f), 0.07f, new Color(c.r, c.g, c.b, 0.9f), -989, TextAnchor.MiddleCenter, 48);
                _zoneLabels.Add(label);
            }
        }

        private void BuildProp(MapProp p)
        {
            var view = new PropView { prop = p };
            var go = new GameObject(p.kind + ":" + p.id);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(p.pos.x, p.pos.y, 0f);
            view.go = go;
            int order = YDepthSort.OrderFor(p.pos.y - 0.45f);

            string iconKind = null;
            float scale = p.size;
            Color color = Color.white;
            switch (p.kind)
            {
                case "sign": iconKind = "sign"; scale = 1.15f; break;
                case "lever": iconKind = "lever"; scale = 1.1f; break;
                case "rubble": iconKind = "rubble"; break;
                case "console": iconKind = "console"; scale = 1.6f; break;
                case "wheel": iconKind = "wheel"; break;
                case "extinguisher": iconKind = "extinguisher"; break;
                case "bell": iconKind = "bell"; break;
                case "shutterswitch": iconKind = "keypad"; color = new Color(0.9f, 0.6f, 0.4f); break;
                case "keypad": iconKind = "keypad"; break;
                case "speaker": iconKind = "speaker"; break;
                case "doorpanel": iconKind = "door"; break;
                case "confirm": iconKind = "board"; break;
                case "lamp":
                    {
                        var sr = SpriteLib.Make(go.transform, "Lamp", p.pos, SpriteLib.Disc(0.4f), new Color(0.3f, 0.3f, 0.3f), 0.6f, order);
                        view.icon = sr;
                        break;
                    }
                case "gate":
                    {
                        var sr = SpriteLib.Quad(go.transform, "Gate", p.pos, new Vector2(0.7f, p.size), new Color(0.55f, 0.45f, 0.3f), YDepthSort.OrderFor(p.pos.y - p.size * 0.5f));
                        view.icon = sr;
                        break;
                    }
                case "shutter":
                    {
                        var sr = SpriteLib.Quad(go.transform, "Shutter", p.pos, new Vector2(0.7f, p.size), new Color(0.5f, 0.52f, 0.6f), YDepthSort.OrderFor(p.pos.y - p.size * 0.5f));
                        for (int i = 0; i < Mathf.RoundToInt(p.size); i++)
                            SpriteLib.Quad(go.transform, "Stripe", new Vector2(p.pos.x, p.pos.y - p.size * 0.5f + 0.5f + i), new Vector2(0.7f, 0.12f), new Color(0.25f, 0.26f, 0.3f), sr.sortingOrder + 1);
                        view.icon = sr;
                        break;
                    }
                case "sectorlabel":
                case "shop":
                    break;
            }
            if (iconKind != null)
            {
                view.icon = SpriteLib.Make(go.transform, "Icon", p.pos, SpriteLib.Icon(iconKind), color, scale, order);
                if (p.slot >= 0 && (p.kind == "keypad" || p.kind == "confirm" || p.kind == "wheel" || p.kind == "extinguisher" || p.kind == "bell" || p.kind == "shutterswitch"))
                {
                    var c = Roles.Color(p.slot);
                    var ring = SpriteLib.Make(go.transform, "RoleRing", p.pos + new Vector2(0f, -0.45f), SpriteLib.Ring(0.25f), new Color(c.r, c.g, c.b, 0.8f), 1.1f, -989);
                    ring.transform.localScale = new Vector3(1.2f, 0.55f, 1f);
                }
            }

            float textY = p.kind == "rubble" ? 1.4f : (p.kind == "sign" ? 0.75f : 0.7f);
            view.text = WorldText.Make(go.transform, "", new Vector3(0f, textY, 0f), 0.055f, Color.white, order + 30, TextAnchor.LowerCenter, 56);
            if (p.kind == "lever" || p.kind == "console" || p.kind == "wheel" || p.kind == "keypad" || p.kind == "confirm")
                view.sub = WorldText.Make(go.transform, "", new Vector3(0f, -0.62f, 0f), 0.05f, UI.InkDim, order + 30, TextAnchor.UpperCenter, 48);

            _props[p.id] = view;
        }

        private static string Garble(int len)
        {
            var sb = new System.Text.StringBuilder();
            int n = Mathf.Clamp(len / 3, 3, 9);
            for (int i = 0; i < n; i++) { sb.Append('?', Random.Range(1, 4)); if (i < n - 1) sb.Append(' '); }
            return sb.ToString();
        }

        /// <summary>상태·시점(내 역할)에 맞게 사물 표시를 갱신.</summary>
        public void RefreshProps()
        {
            if (_map == null) return;
            var st = Session.St;
            int me = Session.LocalSlot;
            bool canRead = Session.CanReadSigns;

            foreach (var kv in _props)
            {
                var v = kv.Value;
                var p = v.prop;
                switch (p.kind)
                {
                    case "sign":
                        {
                            bool universal = p.slot < 0;
                            bool readable = universal || canRead;
                            v.text.text = readable ? p.text : Garble(p.text.Length);
                            v.text.color = readable ? (universal ? UI.InkDim : new Color(1f, 0.95f, 0.75f)) : new Color(0.85f, 0.45f, 0.45f);
                            v.text.characterSize = readable ? 0.05f : 0.06f;
                            break;
                        }
                    case "lever":
                        {
                            bool op = me == (int)GameRole.Operator;
                            v.text.text = st.stage == 1 && op && st.fork == p.a / 1 && true ? "" : "";
                            int fork = p.id.EndsWith("0") ? 0 : (p.id.EndsWith("1") ? 1 : 2);
                            bool active = st.stage == 1 && st.fork == fork && st.step == 0;
                            v.sub.text = op ? Stage1Logic.LeverLabel(st, fork, p.a) : "???";
                            v.sub.color = op ? (active ? UI.Gold : UI.InkDim) : new Color(0.7f, 0.45f, 0.45f);
                            v.text.text = ""; // [E] 표시는 Tick에서 가까이 갔을 때만
                            if (v.icon != null) v.icon.color = active ? Color.white : new Color(0.6f, 0.6f, 0.65f);
                            break;
                        }
                    case "rubble": v.text.text = p.text; v.text.color = UI.InkDim; break;
                    case "console":
                        {
                            string mode = st.consoleMode >= 0 ? StageDefs.ConsoleModes[st.consoleMode] : "—";
                            v.text.text = "펌프 콘솔";
                            v.sub.text = $"모드: {mode}{(st.holding ? "  · 붙잡는 중" : "")}";
                            v.sub.color = st.holding ? UI.Gold : UI.InkDim;
                            break;
                        }
                    case "wheel":
                        {
                            v.text.text = p.a == 0 ? "수문 A" : "수문 B";
                            v.sub.text = Roles.Name(p.slot) + " 담당";
                            v.sub.color = Roles.Color(p.slot);
                            bool recent = st.pullClock != null && p.slot < st.pullClock.Length && Session.Clock - st.pullClock[p.slot] < 1.5f;
                            if (v.icon != null) v.icon.color = recent ? UI.Gold : Color.white;
                            break;
                        }
                    case "gate": v.go.SetActive(!st.gatesOpen); break;
                    case "shutter": v.go.SetActive(st.stage == 1 ? !st.cleared : !st.codeDone); break;
                    case "doorpanel":
                        {
                            bool isStart = p.a == StageMaps.Stage3StartSector;
                            v.text.text = isStart ? "입구" : "탈출문";
                            v.text.color = UI.InkDim;
                            if (isStart && v.icon != null) v.icon.color = new Color(0.5f, 0.5f, 0.55f);
                            break;
                        }
                    case "confirm":
                        {
                            bool isStart = p.a == StageMaps.Stage3StartSector;
                            v.go.SetActive(!isStart);
                            bool done = p.slot == (int)GameRole.Observer ? (st.confirmObs && st.confirmObsSector == p.a) : (st.confirmNav && st.confirmNavSector == p.a);
                            v.text.text = done ? "✓" : "";
                            v.text.color = Green;
                            v.sub.text = Roles.Short(p.slot) + " 확인";
                            v.sub.color = Roles.Color(p.slot);
                            break;
                        }
                    case "sectorlabel": v.text.text = p.text; v.text.color = new Color(1f, 1f, 1f, 0.35f); v.text.characterSize = 0.09f; break;
                    case "shop": v.text.text = p.text; v.text.color = new Color(1f, 1f, 1f, 0.3f); v.text.transform.localPosition = Vector3.zero; break;
                    case "speaker": v.text.text = "방송 스피커"; v.text.color = UI.InkDim; break;
                    case "extinguisher":
                    case "bell":
                    case "shutterswitch":
                        {
                            bool done = st.device != null && p.slot < st.device.Length && st.device[p.slot];
                            bool readable = canRead;
                            v.text.text = (readable ? p.text : Garble(p.text.Length)) + (done ? " ✓" : "");
                            v.text.color = done ? Green : (readable ? UI.Ink : new Color(0.85f, 0.5f, 0.5f));
                            if (v.icon != null) v.icon.color = done ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
                            break;
                        }
                    case "keypad":
                        {
                            v.text.text = "";
                            string typed = st.keypad != null && p.slot < st.keypad.Length ? st.keypad[p.slot] : "";
                            bool ok = st.codeClock != null && p.slot < st.codeClock.Length && st.codeClock[p.slot] > -90f;
                            v.sub.text = Roles.Short(p.slot) + (ok ? " ✓" : (typed.Length > 0 ? " " + typed : ""));
                            v.sub.color = ok ? Green : Roles.Color(p.slot);
                            break;
                        }
                }
            }

            // 관측자 고글: 구역 색
            if (st.stage == 3)
            {
                bool goggles = StageDefs.HasGoggles(st.stage, me);
                for (int i = 0; i < _tints.Count; i++)
                {
                    var c = st.gas[i] == 0 ? Green : (st.gas[i] == 1 ? Yellow : Red);
                    _tints[i].color = goggles ? new Color(c.r, c.g, c.b, 0.22f) : Color.clear;
                }
            }

            RefreshFog();
        }

        private void RefreshFog()
        {
            if (_fog == null) return;
            var st = Session.St;
            float r = StageDefs.FogRadius(st.stage, Session.LocalSlot, st.devicesDone);
            if (r <= 0f) { _fog.enabled = false; return; }
            if (Mathf.Abs(r - _fogRadius) > 0.01f)
            {
                _fogRadius = r;
                _fog.sprite = SpriteLib.Fog(r, 72f, st.stage == 1 ? 3.5f : 2.2f);
            }
            _fog.enabled = true;
        }

        public void OnEvent(GameEvent e)
        {
            if (e.type == "LeverOk" || e.type == "LeverBad")
            {
                string id = (e.a == 0 ? "leverL" : "leverR") + Session.St.fork;
                if (_props.TryGetValue(id, out var v) && v.icon != null)
                    v.icon.color = e.type == "LeverOk" ? Green : Red;
            }
        }

        public void Tick()
        {
            if (_map == null) return;
            var st = Session.St;
            float clock = Session.Clock;
            _jiggleT += Time.deltaTime;

            // 안개는 조종 중인 아바타를 따라간다
            if (_fog != null && _fog.enabled)
            {
                var me = Session.PosOf(Session.LocalSlot);
                if (me != null) _fog.transform.position = new Vector3(me.Value.x, me.Value.y, 0f);
            }

            // Stage 1: 경고등 + 레버 [E] 근접 표시
            if (st.stage == 1)
            {
                float ph = Stage1Logic.Phase01(st, clock);
                bool active = st.step == 0;
                var mePos = Session.PosOf(Session.LocalSlot);
                bool opMe = Session.LocalSlot == (int)GameRole.Operator;
                foreach (var kv in _props)
                {
                    if (kv.Value.prop.kind != "lever" || kv.Value.text == null) continue;
                    bool near = opMe && active && mePos != null && Vector2.Distance(mePos.Value, kv.Value.prop.pos) <= StageInteractions.Reach;
                    string want = near ? (Stage1Logic.CanPullLever(st) ? "[E] 당기기" : "[E] 글자 대기 중") : "";
                    if (kv.Value.text.text != want) kv.Value.text.text = want;
                }
                foreach (var kv in _props)
                {
                    if (kv.Value.prop.kind != "lamp" || kv.Value.icon == null) continue;
                    bool thisFork = kv.Value.prop.a == st.fork;
                    Color c;
                    if (!active || !thisFork) c = new Color(0.25f, 0.25f, 0.28f);
                    else
                    {
                        float k = Mathf.Pow(ph, 3f);
                        float blink = ph > 0.75f ? (Mathf.Sin(clock * 30f) > 0f ? 1f : 0.4f) : 1f;
                        c = Color.Lerp(new Color(0.3f, 0.3f, 0.2f), new Color(1f, 0.15f, 0.1f), k) * blink;
                        c.a = 1f;
                    }
                    if (st.signalCut) c = new Color(0.2f, 0.4f, 1f);
                    kv.Value.icon.color = c;
                }
                // ??? 표지판 지글거림
                if (_jiggleT > 0.15f && !Session.CanReadSigns)
                {
                    _jiggleT = 0f;
                    foreach (var kv in _props)
                        if (kv.Value.prop.kind == "sign" && kv.Value.prop.slot >= 0) kv.Value.text.text = Garble(kv.Value.prop.text.Length);
                }
            }
            else if (_jiggleT > 0.15f && !Session.CanReadSigns)
            {
                _jiggleT = 0f;
                foreach (var kv in _props)
                {
                    var k = kv.Value.prop.kind;
                    if ((k == "sign" && kv.Value.prop.slot >= 0) || k == "extinguisher" || k == "bell" || k == "shutterswitch")
                    {
                        bool done = st.device != null && kv.Value.prop.slot >= 0 && kv.Value.prop.slot < st.device.Length && st.device[kv.Value.prop.slot];
                        kv.Value.text.text = Garble(kv.Value.prop.text.Length) + (k != "sign" && done ? " ✓" : "");
                    }
                }
            }

            // Stage 3: 내 구역의 도착 원
            if (st.stage == 3)
            {
                var me = Session.PosOf(Session.LocalSlot);
                int sec = me != null ? _map.SectorAt(me.Value) : -1;
                if (sec >= 0 && sec != _zoneSector)
                {
                    _zoneSector = sec;
                    var zones = new Vector2[4];
                    StageMaps.SectorZones(sec, zones);
                    for (int i = 0; i < 4 && i < _zoneDiscs.Count; i++)
                    {
                        _zoneDiscs[i].transform.position = new Vector3(zones[i].x, zones[i].y, 0f);
                        _zoneLabels[i].transform.position = new Vector3(zones[i].x, zones[i].y - 0.05f, 0f);
                    }
                }
            }

            // 도착 원 점유 표시
            for (int i = 0; i < _zoneDiscs.Count; i++)
            {
                if (_zoneDiscs[i] == null) continue;
                var p = Session.PosOf(i);
                bool inside = p != null && Vector2.Distance(p.Value, _zoneDiscs[i].transform.position) <= _map.zoneRadius;
                var c = Roles.Color(i);
                _zoneDiscs[i].color = inside ? new Color(1f, 1f, 1f, 1f) : new Color(c.r, c.g, c.b, 0.85f);
                float pulse = inside ? 1f + 0.06f * Mathf.Sin(Time.time * 8f) : 1f;
                _zoneDiscs[i].transform.localScale = new Vector3(_map.zoneRadius * 2f * pulse, _map.zoneRadius * 1.1f * pulse, 1f);
            }

            // 불 흔들림
            for (int i = 0; i < _fires.Count; i++)
            {
                if (_fires[i] == null) continue;
                float s = 1.7f + 0.25f * Mathf.Sin(Time.time * 9f + i * 1.7f);
                _fires[i].transform.localScale = new Vector3(s, s * (1f + 0.1f * Mathf.Sin(Time.time * 13f + i)), 1f);
            }
        }
    }
}
