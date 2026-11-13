using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 미니맵(내비게이터 전용 · Stage 4는 조작자 전용). 지형·위험·장치·도착 원과 네 아바타 위치를 보여준다.
    /// Stage 2에서는 침수되는 갈래, Stage 3에서는 붕괴 구역, Stage 4에서는 불·장치·키패드가 보인다.
    /// </summary>
    public class MinimapView
    {
        private RectTransform _panel;
        private RectTransform _field;
        private readonly RectTransform[] _dots = new RectTransform[4];
        private readonly List<RectTransform> _fireDots = new List<RectTransform>();
        private MapData _map;
        private Vector2 _fieldSize = new Vector2(250f, 168f);
        private Text _title;
        private Text _notice;
        private int _fireCount = -1;

        public void Build(RectTransform parent)
        {
            _panel = UI.Panel(parent, "Minimap", UI.PanelDark, new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(276f, 212f));
            _title = UI.Label(_panel, "미니맵", 14, new Color(0.6f, 0.9f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(260f, 20f), TextAnchor.MiddleCenter);
            _field = UI.Fixed(_panel, "Field", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), _fieldSize);
            UI.Bg(_field, new Color(0.05f, 0.05f, 0.07f, 1f), false);
            _notice = UI.Label(_panel, "", 13, new Color(1f, 0.6f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(240f, 120f), TextAnchor.MiddleCenter);
            OnState();
        }

        public void Dispose()
        {
            if (_panel != null) Object.Destroy(_panel.gameObject);
            _panel = null;
        }

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

        private RectTransform Dot(Vector2 world, Color c, float size, string name = "Dot", string label = null)
        {
            var rt = UI.Rect(_field, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ToMap(world);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteLib.Disc(); img.color = c; img.raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var t = UI.Label(rt, label, 10, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 14f), TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
            }
            return rt;
        }

        public void OnState()
        {
            if (_panel == null) return;
            var st = Session.St;
            int me = Session.LocalSlot;
            bool has = StageDefs.HasMinimap(st.stage, me);
            bool disabled = st.stage == 4 && me == (int)GameRole.Navigator;
            _panel.gameObject.SetActive(has || disabled);
            if (disabled)
            {
                _field.gameObject.SetActive(false);
                _notice.text = "미니맵 무력화\n연기로 지도 신호가 끊겼다.\n조작자의 안내에 의지하세요.";
                _title.text = "미니맵 (내비게이터)";
                return;
            }
            if (!has) return;
            _notice.text = "";
            _field.gameObject.SetActive(true);
            _title.text = st.stage == 4 ? "전체 지도 (조작자 전용)" : "미니맵 (내비게이터 전용)";

            if (Session.Map != _map || _dots[0] == null)
            {
                _map = Session.Map;
                RebuildStatic();
            }
            if (st.stage == 4 && st.fires.Count != _fireCount) RebuildFires();
        }

        private void RebuildStatic()
        {
            UI.Clear(_field);
            _fireDots.Clear();
            _fireCount = -1;
            if (_map == null) return;
            var st = Session.St;

            foreach (var o in _map.open) Box(o, new Color(0.30f, 0.33f, 0.40f, 1f), "Open");
            foreach (var s in _map.solids) Box(s, new Color(0.10f, 0.10f, 0.13f, 1f), "Solid");

            if (st.stage == 2)
            {
                Box(_map.flooded, new Color(0.25f, 0.5f, 1f, 0.6f), "Flooded");
                Box(_map.exitRoom, new Color(0.35f, 0.9f, 0.5f, 0.35f), "Exit");
                var lbl = UI.Label(_field, "침수", 11, new Color(0.7f, 0.85f, 1f), new Vector2(0.5f, 0.5f), ToMap(_map.flooded.center), new Vector2(60f, 14f), TextAnchor.MiddleCenter);
                var lbl2 = UI.Label(_field, "출구", 11, new Color(0.7f, 1f, 0.8f), new Vector2(0.5f, 0.5f), ToMap(_map.exitRoom.center), new Vector2(60f, 14f), TextAnchor.MiddleCenter);
            }
            if (st.stage == 3)
            {
                for (int i = 0; i < _map.sectors.Length; i++)
                {
                    bool blocked = StageMaps.IsBlocked(st, i);
                    Box(_map.sectors[i], blocked ? new Color(0.45f, 0.25f, 0.2f, 0.9f) : new Color(0.32f, 0.42f, 0.36f, 1f), "Sector");
                    var name = UI.Label(_field, StageMaps.SectorName(i) + (blocked ? "\n붕괴" : ""), 10, blocked ? new Color(1f, 0.7f, 0.6f) : Color.white, new Vector2(0.5f, 0.5f), ToMap(_map.sectors[i].center), new Vector2(50f, 26f), TextAnchor.MiddleCenter);
                }
            }
            if (st.stage == 4)
            {
                foreach (var p in _map.props)
                {
                    if (p.kind == "extinguisher" || p.kind == "bell" || p.kind == "shutterswitch")
                        Dot(p.pos, Roles.Color(p.slot), 14f, "Device", Roles.Short(p.slot)[0].ToString());
                    else if (p.kind == "keypad")
                        Dot(p.pos, Roles.Color(p.slot), 8f, "Pad");
                }
                Box(_map.exitRoom, new Color(0.35f, 0.9f, 0.5f, 0.35f), "Exit");
            }

            for (int i = 0; i < 4; i++) Dot(_map.zones[i], Roles.Color(i), 9f, "Zone");
            for (int i = 0; i < 4; i++)
            {
                var c = Roles.Color(i);
                _dots[i] = Dot(_map.spawns[i], c, 11f, "Avatar");
                var ring = UI.Rect(_dots[i], "Ring");
                ring.anchorMin = Vector2.zero; ring.anchorMax = Vector2.one; ring.offsetMin = new Vector2(-3, -3); ring.offsetMax = new Vector2(3, 3);
                var ri = ring.gameObject.AddComponent<Image>();
                ri.sprite = SpriteLib.Ring(0.3f); ri.color = Color.white; ri.raycastTarget = false;
                ring.gameObject.SetActive(false);
            }
            RebuildFires();
        }

        private void RebuildFires()
        {
            foreach (var f in _fireDots) if (f != null) Object.Destroy(f.gameObject);
            _fireDots.Clear();
            var st = Session.St;
            _fireCount = st.fires.Count;
            if (_map == null || st.stage != 4) return;
            foreach (var f in st.fires) _fireDots.Add(Dot(f.V, new Color(1f, 0.5f, 0.15f), 10f, "Fire"));
        }

        public void Tick()
        {
            if (_panel == null || !_panel.gameObject.activeSelf || _map == null || _dots[0] == null) return;
            for (int i = 0; i < 4; i++)
            {
                var p = Session.PosOf(i);
                if (p == null || _dots[i] == null) continue;
                _dots[i].anchoredPosition = ToMap(p.Value);
                var ring = _dots[i].Find("Ring");
                if (ring != null) ring.gameObject.SetActive(i == Session.LocalSlot);
            }
        }
    }
}
