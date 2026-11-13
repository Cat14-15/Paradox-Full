using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 6칸 휠(도넛) — Q 또는 액션 버튼으로 화면 가운데에 뜬다(명세 6-2).
    /// 상좌 yes(○) · 상우 no(×) · 좌 where(?) · 우 go(→) · 하좌 you(★) · 하우 위험(!).
    /// 각 칸에는 내 픽셀 캐릭터 앞모습 + 기호 + 영문 단어. 칸을 누르면 Signal 명령을 보내고 닫힌다. 조작자(v2 D4)에게는 열리지 않는다.
    /// </summary>
    public class Donut
    {
        private RectTransform _layer, _disc;
        private readonly Image[] _portraits = new Image[6];
        private int _builtSlot = -1;

        private static readonly Vector2[] SlotPos =
        {
            new Vector2(-100f, 128f), new Vector2(100f, 128f),   // yes, no
            new Vector2(-172f, 0f), new Vector2(172f, 0f),       // where, go
            new Vector2(-100f, -128f), new Vector2(100f, -128f)  // you, danger
        };

        public bool IsOpen => _layer != null && _layer.gameObject.activeSelf;

        public void Build(RectTransform parent)
        {
            _layer = UI.Stretch(parent, "Donut");
            // 바깥을 누르면 닫힌다
            var dimBtn = UI.Button(_layer, "", new Color(0f, 0f, 0f, 0.35f), Close, 1, 0, 0);
            var drt = (RectTransform)dimBtn.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var dle = dimBtn.GetComponent<LayoutElement>(); if (dle != null) Object.Destroy(dle);

            _disc = UI.Fixed(_layer, "Disc", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(500f, 500f));
            var discImg = _disc.gameObject.AddComponent<Image>();
            discImg.sprite = SpriteLib.Disc(0.02f); discImg.color = new Color(0.90f, 0.86f, 0.74f, 0.92f); discImg.raycastTarget = true;
            // 칸 나누는 선 6개(구멍에서 테두리까지)
            float[] angles = { 90f, 270f, 30f, 150f, 210f, 330f };
            foreach (var a in angles)
            {
                var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                HudKit.Line(_disc, dir * 76f, dir * 249f, 2f, new Color(0.25f, 0.22f, 0.18f, 0.55f), "Sep");
            }
            var hole = HudKit.Dot(_disc, Vector2.zero, 150f, new Color(0.03f, 0.04f, 0.06f, 1f), "Hole");
            hole.sprite = SpriteLib.Disc(0.03f);
            HudKit.Dot(_disc, Vector2.zero, 150f, new Color(0.25f, 0.22f, 0.18f, 1f), "HoleRim").sprite = SpriteLib.Ring(0.05f);
            UI.Label(_disc, "Q · Esc\n닫기", 11, HudKit.Dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 40f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));

            for (int i = 0; i < 6; i++) BuildSlot(i);

            // 캡션은 반투명 크림 패널 위에(뒤의 방 이름·표지판과 겹쳐 읽히지 않게)
            var capBg = UI.Fixed(_layer, "CaptionBg", new Vector2(0.5f, 0.5f), new Vector2(0f, -272f), new Vector2(620f, 30f), new Vector2(0.5f, 0.5f));
            UI.Bg(capBg, new Color(0.90f, 0.86f, 0.74f, 0.85f), false);
            UI.Label(_layer, "칸을 고르면 내 캐릭터 머리 위 말풍선으로 뜬다 — 몸짓은 말이 안 통해도 통한다", 14, HudKit.InkDark, new Vector2(0.5f, 0.5f), new Vector2(0f, -272f), new Vector2(700f, 24f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            _layer.gameObject.SetActive(false);
        }

        private void BuildSlot(int i)
        {
            string id = Comms.SignalIds[i];
            var c = Comms.SignalColor(id);
            var btn = UI.Button(_disc, "", new Color(c.r, c.g, c.b, 0.92f), () => Pick(id), 12, 0, 0);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = SlotPos[i]; rt.sizeDelta = new Vector2(118f, 132f);
            var le = btn.GetComponent<LayoutElement>(); if (le != null) Object.Destroy(le);
            HudKit.Frame(rt, new Color(0.15f, 0.13f, 0.10f, 1f), 2f);
            var lbl = UI.ButtonLabel(btn); lbl.text = "";
            // 내 캐릭터 앞모습
            var prt = UI.Fixed(rt, "Portrait", new Vector2(0.5f, 1f), new Vector2(-10f, -8f), new Vector2(40f, 80f), new Vector2(0.5f, 1f));
            var pimg = prt.gameObject.AddComponent<Image>();
            pimg.preserveAspect = true; pimg.raycastTarget = false;
            _portraits[i] = pimg;
            // 기호 배지 — 위험·you 는 픽셀 아이콘(D-A7), 나머지는 글리프
            var badge = UI.Fixed(rt, "Glyph", new Vector2(1f, 0f), new Vector2(-6f, 28f), new Vector2(32f, 32f), new Vector2(1f, 0f));
            UI.Bg(badge, Color.white, false);
            HudKit.Frame(badge, new Color(0.15f, 0.13f, 0.10f, 1f), 2f);
            string iconKind = Comms.SignalIconKind(id);
            if (iconKind != null) HudKit.IconImage(badge, iconKind, 24f);
            else
            {
                var g = HudKit.Fill(badge, Comms.SignalGlyph(id), 18, HudKit.InkDark, TextAnchor.MiddleCenter, 0f);
                g.fontStyle = FontStyle.Bold;
            }
            // 단어
            var w = UI.Label(rt, Comms.SignalCaption(id, 10, true), 13, HudKit.InkDark, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(116f, 22f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
        }

        private void Pick(string id)
        {
            Session.Send("Signal", 0, 0, id);
            Close();
        }

        public void Dispose()
        {
            if (_layer != null) Object.Destroy(_layer.gameObject);
            _layer = null;
        }

        public void OnState()
        {
            int me = Session.LocalSlot;
            if (me == _builtSlot || !Roles.Valid(me)) return;
            _builtSlot = me;
            var sprite = PixelSprites.Portrait(me);
            for (int i = 0; i < 6; i++)
            {
                if (_portraits[i] == null) continue;
                _portraits[i].sprite = sprite;
                _portraits[i].color = sprite != null ? Color.white : HudKit.Alpha(Roles.Color(me), 0.6f);
                _portraits[i].enabled = true;
            }
        }

        public void Open()
        {
            if (_layer == null) return;
            var st = Session.St;
            if (st.failed || st.cleared) return;
            if (Session.LocalSlot == Comms.Op) return;   // 조작자에게는 도넛이 없다(D4) — 안내 토스트는 StageScreen.OpenDonut
            OnState();
            _layer.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_layer == null) return;
            _layer.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }
    }
}
