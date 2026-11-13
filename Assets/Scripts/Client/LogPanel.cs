using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 수신 로그(C) — 읽기 전용, 불투명 패널(P-A6). 최근 12개: 채팅(가시성 규칙) · 휠 신호(카드) · 색 신고 · 카드 · 핑.
    /// 항목 = 국가 코드 배지(28px) + "역할 · n초 전" + 본문(높이는 내용에 맞춤). 최신이 위.
    /// </summary>
    public class LogPanel
    {
        private RectTransform _root, _list;
        private bool _dirty;
        private float _next;

        public bool Visible => _root != null && _root.gameObject.activeSelf;

        public void Build(RectTransform parent)
        {
            // P-A6: 불투명 패널(뒤의 방·표지판이 비쳐 글이 안 읽히지 않게)
            _root = HudKit.Box(parent, "LogPanel", HudLayout.LogAnchor, HudLayout.LogPos, HudLayout.LogSize, HudKit.PanelSolid, HudKit.Border, 1f);
            UI.Label(_root, "수신 로그", 14, HudKit.Amber, new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(200f, 20f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
            UI.Label(_root, "읽기 전용", 11, HudKit.Dim, new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(120f, 20f), TextAnchor.MiddleRight, new Vector2(1f, 1f));
            var line = UI.Rect(_root, "Line"); line.anchorMin = new Vector2(0f, 1f); line.anchorMax = new Vector2(1f, 1f); line.offsetMin = new Vector2(0f, -35f); line.offsetMax = new Vector2(0f, -34f);
            UI.Bg(line, HudKit.Border, false);
            _list = UI.Stretch(_root, "List", 10f, 30f, 10f, 40f);
            UI.VStack(_list, 4, 6, false, TextAnchor.UpperLeft);
            var foot = UI.Rect(_root, "Foot"); foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f); foot.offsetMin = new Vector2(0f, 0f); foot.offsetMax = new Vector2(0f, 26f);
            UI.Bg(foot, new Color(0.04f, 0.06f, 0.10f, 1f), false);
            HudKit.Fill(foot, "<b>×</b>  C 또는 Esc로 닫기 · 팝업 중에도 C · 최근 12개 · 채팅은 통역만 다 읽는다", 11, HudKit.Dim, TextAnchor.MiddleLeft, 12f);
            _root.gameObject.SetActive(false);
            _dirty = true;
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        public void Show(bool v)
        {
            if (_root == null) return;
            _root.gameObject.SetActive(v);
            if (v) Refresh();
        }

        public void Toggle() => Show(!Visible);

        /// <summary>A2: 팝업 층보다 위로 올린다(벽보·벽화를 보면서 조작자 채팅을 다시 읽을 수 있게). 튜토리얼은 열릴 때 스스로 맨 위로 온다.</summary>
        public void BringToFront()
        {
            if (_root != null) _root.SetAsLastSibling();
        }

        public void OnEvent(GameEvent e)
        {
            if (e != null && Comms.IsLogged(e.type)) _dirty = true;
        }

        public void Tick()
        {
            if (!Visible) return;
            if (_dirty || Time.time > _next) Refresh();
        }

        private void Refresh()
        {
            if (_list == null) return;
            _dirty = false;
            _next = Time.time + 1f;
            HudKit.Clear(_list);
            int me = Session.LocalSlot;
            int shown = 0;
            for (int i = Session.Log.Count - 1; i >= 0 && shown < 12; i--)
            {
                var entry = Session.Log[i];
                var e = entry.e;
                if (e == null || !Comms.IsLogged(e.type)) continue;
                Row(e, Time.time - entry.t, me);
                shown++;
            }
            if (shown == 0)
            {
                // 조작자에게는 도넛이 없다(D4) — 채팅(T)만 안내한다
                string empty = me == Comms.Op
                    ? "아직 받은 신호가 없어요. T를 눌러 채팅으로 말해 보세요 (통역만 읽어요)."
                    : "아직 받은 신호가 없어요. Q를 눌러 도넛으로 알리거나, 아래 채팅으로 말해 보세요.";
                UI.Text(_list, $"<color=#8A96A8>{empty}</color>", 13, HudKit.Dim, TextAnchor.UpperLeft, true, 40);
            }
        }

        /// <summary>
        /// 로그 한 항목(P-A6): [국가 코드 배지 28px] + [ "역할 · n초 전" / 본문 ] — 높이는 내용에 맞춘다.
        /// 예전에는 가로 줄(HStack, childForceExpandHeight)이 세로 목록의 남는 공간을 나눠 가져 배지가 70px 색 막대로 늘어났다 → 항목 flexibleHeight 0 + 배지 고정.
        /// 위험·you 신호는 글리프 대신 픽셀 아이콘(D-A7).
        /// </summary>
        private void Row(GameEvent e, float ago, int me)
        {
            var entry = UI.Rect(_list, "Entry");
            var h = UI.HStack(entry, 0, 8f, false, TextAnchor.UpperLeft);
            h.childForceExpandHeight = false;
            UI.LE(entry, -1, 28, -1, 0);
            int slot = e.slot;
            if (Roles.Valid(slot)) HudKit.CodeBadge(entry, slot, 28f, 10);
            else
            {
                var sysBox = UI.Rect(entry, "Sys");
                UI.Bg(sysBox, HudKit.Slate, false);
                UI.LE(sysBox, 28, 28, 0, 0, 28, 28);
                HudKit.Fill(sysBox, "SYS", 9, HudKit.Dim, TextAnchor.MiddleCenter, 0f);
            }
            var col = UI.Rect(entry, "Body");
            UI.VStack(col, 0, 1f, false, TextAnchor.UpperLeft);
            UI.LE(col, 120, -1, 1f, 0);
            string who = Roles.Valid(slot) ? $"<color=#{Roles.Hex(slot)}>{Roles.Name(slot)}</color>" : "시스템";
            UI.Text(col, $"<color=#8A96A8>{who} · {Comms.Ago(ago)}</color>", 10, HudKit.Dim, TextAnchor.MiddleLeft, false, 12);

            string iconKind = e.type == "Signal" ? Comms.SignalIconKind(e.s) : null;
            if (iconKind != null)
            {
                var line = UI.Rect(col, "Line");
                var lh = UI.HStack(line, 0, 6f, false, TextAnchor.MiddleLeft);
                lh.childForceExpandHeight = false;
                UI.LE(line, -1, 18, -1, 0);
                HudKit.IconCell(line, iconKind, 18f);
                var t = UI.Text(line, Comms.LogText(e, me, false), 13, HudKit.Ink, TextAnchor.MiddleLeft, true);
                UI.LE(t, 60, 18, 1f, 0);
            }
            else
            {
                var t = UI.Text(col, Comms.LogText(e, me), 13, HudKit.Ink, TextAnchor.UpperLeft, true, 16);
                UI.LE(t, 60, 16, 1f, 0);
            }
        }
    }
}
