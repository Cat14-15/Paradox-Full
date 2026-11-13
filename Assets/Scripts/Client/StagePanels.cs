using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>스테이지 HUD 공용 색·조각 팩토리(명세 3절 톤: 배경 #06080D, 패널 #0D1420, 테두리 #2A3A55, 앰버 #F2C572).</summary>
    public static class HudKit
    {
        public static readonly Color Bg = Hex("06080D");
        public static readonly Color Panel = new Color(0.051f, 0.078f, 0.125f, 0.96f);
        public static readonly Color PanelSolid = new Color(0.051f, 0.078f, 0.125f, 1f);
        public static readonly Color Dark = new Color(0.03f, 0.045f, 0.07f, 1f);
        public static readonly Color Border = Hex("2A3A55");
        public static readonly Color Amber = Hex("F2C572");
        public static readonly Color Ink = Hex("F0F2F5");
        public static readonly Color Dim = Hex("8A96A8");
        public static readonly Color Red = Hex("E5484D");
        public static readonly Color Green = Hex("5FB870");
        public static readonly Color Yellow = Hex("E8B93A");
        public static readonly Color Blue = Hex("4F7BC9");
        public static readonly Color Teal = Hex("5FD3C4");
        public static readonly Color Cream = Hex("F1E7D0");
        public static readonly Color Tan = Hex("D9CBAA");
        public static readonly Color InkDark = Hex("1B1F27");
        public static readonly Color MonoGreen = Hex("7FE39B");
        public static readonly Color Slate = Hex("1A2436");

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        public static string H(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>배경 + 테두리가 있는 고정 박스.</summary>
        public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color? border = null, float bw = 1f, Vector2? pivot = null)
        {
            var rt = UI.Panel(parent, name, bg, anchor, pos, size, pivot);
            if (border != null) Frame(rt, border.Value, bw);
            return rt;
        }

        /// <summary>레이아웃 그룹 안에서 쓰는 배경 박스(LayoutElement로 높이 확보).</summary>
        public static RectTransform Block(Transform parent, string name, Color bg, float minH = -1, Color? border = null, float bw = 1f)
        {
            var rt = UI.Rect(parent, name);
            UI.Bg(rt, bg, false);
            if (minH >= 0) UI.LE(rt, -1, minH);
            if (border != null) Frame(rt, border.Value, bw);
            return rt;
        }

        /// <summary>네 변에 얇은 테두리 이미지를 붙인다.</summary>
        public static void Frame(RectTransform rt, Color c, float w = 1f)
        {
            Edge(rt, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -w), new Vector2(0f, 0f), c);
            Edge(rt, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, w), c);
            Edge(rt, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(w, 0f), c);
            Edge(rt, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-w, 0f), new Vector2(0f, 0f), c);
        }

        private static void Edge(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color c)
        {
            var e = UI.Rect(parent, "Edge" + name);
            e.anchorMin = aMin; e.anchorMax = aMax; e.offsetMin = oMin; e.offsetMax = oMax;
            var img = e.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            var le = e.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        }

        /// <summary>국가 코드 배지(역할색 사각형 + 코드).</summary>
        public static RectTransform CodeBadge(Transform parent, int slot, float size, int fontSize = 11)
        {
            var rt = UI.Rect(parent, "Code");
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Roles.Color(slot); img.raycastTarget = false;
            UI.LE(rt, size, size, 0, 0, size, size);
            var t = UI.Label(rt, Roles.Code(slot), fontSize, InkDark, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            return rt;
        }

        /// <summary>SpriteLib 픽셀 아이콘을 부모 가운데에 고정 크기로 놓는다(레이아웃 무시). 도넛 배지·액션 열·로그 항목이 쓴다(D-A7/P-A9).</summary>
        public static Image IconImage(RectTransform parent, string kind, float size, Color? tint = null, string name = "Icon")
        {
            var rt = UI.Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteLib.Icon(kind); img.color = tint ?? Color.white; img.preserveAspect = true; img.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;
            return img;
        }

        /// <summary>레이아웃 그룹 안에 놓는 픽셀 아이콘(고정 폭·높이 셀).</summary>
        public static Image IconCell(Transform parent, string kind, float size, Color? tint = null)
        {
            var img = UI.Image(parent, SpriteLib.Icon(kind), tint ?? Color.white, new Vector2(size, size));
            return img;
        }

        /// <summary>두 점을 잇는 선(부모의 가운데 기준 로컬 좌표). UI Image 회전·스케일.</summary>
        public static Image Line(RectTransform parent, Vector2 from, Vector2 to, float thickness, Color c, string name = "Line")
        {
            var rt = UI.Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var d = to - from;
            rt.anchoredPosition = (from + to) * 0.5f;
            rt.sizeDelta = new Vector2(Mathf.Max(thickness, d.magnitude), thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        /// <summary>부모 가운데 기준 고정 위치의 원 이미지.</summary>
        public static Image Dot(RectTransform parent, Vector2 pos, float size, Color c, string name = "Dot")
        {
            var rt = UI.Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteLib.Disc(0.08f); img.color = c; img.raycastTarget = false;
            return img;
        }

        /// <summary>가로 줄(HStack) 컨테이너.</summary>
        public static RectTransform Row(Transform parent, float h, float spacing = 6f, bool expand = true, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = UI.Rect(parent, "Row");
            UI.HStack(rt, 0, spacing, expand, align);
            UI.LE(rt, -1, h);
            return rt;
        }

        /// <summary>세로 열(VStack) 컨테이너.</summary>
        public static RectTransform Col(Transform parent, int pad = 0, float spacing = 6f, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = UI.Rect(parent, "Col");
            UI.VStack(rt, pad, spacing, false, align);
            return rt;
        }

        /// <summary>버튼(글자색 지정 가능 — 밝은 배경엔 어두운 글자).</summary>
        public static Button Btn(Transform parent, string label, Color bg, UnityAction onClick, int fontSize = 15, float minH = 40, float minW = 60, bool darkText = false)
        {
            var b = UI.Button(parent, label, bg, onClick, fontSize, minH, minW);
            if (darkText) UI.ButtonLabel(b).color = InkDark;
            return b;
        }

        /// <summary>글자가 든 셀(표용).</summary>
        public static Text Cell(Transform parent, string text, Color bg, Color fg, int fontSize, FontStyle style = FontStyle.Normal, Color? border = null)
        {
            var rt = UI.Rect(parent, "Cell");
            UI.Bg(rt, bg, false);
            if (border != null) Frame(rt, border.Value, 1f);
            var t = UI.Label(rt, text, fontSize, fg, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 60f), TextAnchor.MiddleCenter);
            var trt = (RectTransform)t.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            t.fontStyle = style;
            return t;
        }

        /// <summary>자식을 즉시 레이아웃에서 빼고(비활성) 파괴한다 — 같은 프레임에 새 자식을 넣어도 겹치지 않게.</summary>
        public static void Clear(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                Object.Destroy(c);
            }
        }

        /// <summary>글자 자체를 부모에 꽉 채운다(고정 박스 안 텍스트용).</summary>
        public static Text Fill(RectTransform parent, string text, int size, Color color, TextAnchor align, float pad = 8f)
        {
            var t = UI.Label(parent, text, size, color, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, align);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
            return t;
        }
    }

    /// <summary>HUD 배치 상수(1600×900 기준). 코치마크가 같은 사각형을 비춘다.</summary>
    public static class HudLayout
    {
        public const float TopH = 62f;
        public static readonly Vector2 BadgeAnchor = new Vector2(0f, 1f), BadgePos = new Vector2(16f, -78f), BadgeSize = new Vector2(184f, 62f);
        public static readonly Vector2 MiniAnchor = new Vector2(1f, 1f), MiniPos = new Vector2(-16f, -78f), MiniSize = new Vector2(360f, 280f), MiniBig = new Vector2(640f, 470f);
        public static readonly Vector2 SwitchAnchor = new Vector2(1f, 1f), SwitchPos = new Vector2(-16f, -366f), SwitchSize = new Vector2(360f, 44f);
        public static readonly Vector2 ActionAnchor = new Vector2(0f, 0f), ActionPos = new Vector2(16f, 16f), ActionSize = new Vector2(250f, 206f);
        public static readonly Vector2 RoleAnchor = new Vector2(1f, 0f), RolePos = new Vector2(-16f, 16f), RoleSize = new Vector2(360f, 360f);
        public static readonly Vector2 LogAnchor = new Vector2(0f, 0f), LogPos = new Vector2(16f, 236f), LogSize = new Vector2(430f, 400f);
        /// <summary>집결 게이지: 아래 변(188px)이 출구 셔터 라벨(화면 y≈204)보다 위에 오도록 118px 높이.</summary>
        public static readonly Vector2 GatherAnchor = new Vector2(0.5f, 1f), GatherPos = new Vector2(0f, -70f), GatherSize = new Vector2(380f, 118f);
        public static readonly Vector2 TeamAnchor = new Vector2(0f, 1f), TeamPos = new Vector2(612f, 0f), TeamSize = new Vector2(756f, TopH);
        public static readonly Vector2 TimerAnchor = new Vector2(1f, 1f), TimerPos = new Vector2(-64f, 0f), TimerSize = new Vector2(160f, TopH);
        /// <summary>액션 열 한 줄(수신 로그 = 첫 줄).</summary>
        public static readonly Vector2 LogIconPos = new Vector2(16f, 178f), LogIconSize = new Vector2(250f, 44f);

        /// <summary>UI.Fixed 규칙(pivot = anchor)으로 놓인 사각형을 캔버스 가운데 기준 좌표로 바꾼다.</summary>
        public static Rect ToCanvasRect(Vector2 anchor, Vector2 pos, Vector2 size, Vector2 canvas)
        {
            float px = (anchor.x - 0.5f) * canvas.x + pos.x;
            float py = (anchor.y - 0.5f) * canvas.y + pos.y;
            float xMin = px - anchor.x * size.x;
            float yMin = py - anchor.y * size.y;
            return new Rect(xMin, yMin, size.x, size.y);
        }

        public static Vector2 CanvasSize()
        {
            var c = UIRoot.Canvas;
            if (c != null && c.rect.width > 10f) return c.rect.size;
            return UIRoot.Reference;
        }
    }

    // =====================================================================================
    // 상단 바
    // =====================================================================================

    /// <summary>상단 바: 스테이지 제목 · 현재 관문 진행 · 팀 4칸 · 침수 타이머 · ? 버튼.</summary>
    public class TopBar
    {
        private RectTransform _root;
        private Text _progress, _timer;
        private readonly RectTransform[] _cells = new RectTransform[4];
        private readonly Text[] _cellText = new Text[4];
        private readonly Image[] _cellBg = new Image[4];
        private Button _help;
        private int _blinkPhase;
        // D-A8: 관문 2 라운드 서브 게이지(4칸) — gate1Done && !gate2Done 동안 항상 표시
        private readonly Image[] _roundBars = new Image[4];
        private Text _roundText;
        private RectTransform _roundBox;

        /// <summary>테스트 훅: 라운드 서브 게이지가 보이는가.</summary>
        public bool RoundGaugeVisible => _roundBox != null && _roundBox.gameObject.activeSelf;

        public void Build(RectTransform parent, UnityAction onHelp)
        {
            _root = UI.Rect(parent, "TopBar");
            _root.anchorMin = new Vector2(0f, 1f); _root.anchorMax = new Vector2(1f, 1f); _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = Vector2.zero; _root.sizeDelta = new Vector2(0f, HudLayout.TopH);
            UI.Bg(_root, HudKit.PanelSolid, true);
            var bottom = UI.Rect(_root, "Line");
            bottom.anchorMin = new Vector2(0f, 0f); bottom.anchorMax = new Vector2(1f, 0f); bottom.offsetMin = Vector2.zero; bottom.offsetMax = new Vector2(0f, 1f);
            UI.Bg(bottom, HudKit.Border, false);

            // 제목
            UI.Label(_root, "STAGE 1 · PHILIPPINES", 11, HudKit.Dim, new Vector2(0f, 1f), new Vector2(18f, -9f), new Vector2(250f, 16f), TextAnchor.MiddleLeft);
            UI.Label(_root, "침수된 박물관", 18, HudKit.Ink, new Vector2(0f, 1f), new Vector2(18f, -26f), new Vector2(250f, 26f), TextAnchor.MiddleLeft);
            Divider(276f);

            // 현재 관문
            UI.Label(_root, "현재 관문", 11, HudKit.Dim, new Vector2(0f, 1f), new Vector2(292f, -9f), new Vector2(310f, 16f), TextAnchor.MiddleLeft);
            _progress = UI.Label(_root, "", 16, HudKit.Ink, new Vector2(0f, 1f), new Vector2(292f, -26f), new Vector2(316f, 26f), TextAnchor.MiddleLeft);
            // D-A8: 관문 2 라운드 4칸 — "현재 관문" 캡션 줄 오른쪽(x 452~604, 구분선 608 안쪽)
            _roundBox = UI.Fixed(_root, "Rounds", new Vector2(0f, 1f), new Vector2(452f, -9f), new Vector2(152f, 16f), new Vector2(0f, 1f));
            _roundText = UI.Label(_roundBox, "라운드", 10, HudKit.Dim, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(60f, 16f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            for (int i = 0; i < 4; i++)
            {
                var b = UI.Fixed(_roundBox, "R" + i, new Vector2(1f, 0.5f), new Vector2(-(3 - i) * 21f, 0f), new Vector2(18f, 7f), new Vector2(1f, 0.5f));
                _roundBars[i] = UI.Bg(b, HudKit.Border, false);
            }
            _roundBox.gameObject.SetActive(false);
            Divider(608f);

            // 팀 4칸
            var team = UI.Fixed(_root, "Team", HudLayout.TeamAnchor, HudLayout.TeamPos, HudLayout.TeamSize);
            for (int i = 0; i < 4; i++)
            {
                float x = 10f + i * 186f;
                var cell = UI.Panel(team, "Cell" + i, HudKit.Slate, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(176f, 48f), new Vector2(0f, 0.5f));
                _cells[i] = cell;
                _cellBg[i] = cell.GetComponent<Image>();
                HudKit.Frame(cell, HudKit.Border, 1f);
                var badge = HudKit.CodeBadge(cell, i, 32f, 11);
                badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f); badge.pivot = new Vector2(0f, 0.5f); badge.anchoredPosition = new Vector2(8f, 0f);
                _cellText[i] = UI.Label(cell, "", 13, HudKit.Ink, new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(124f, 44f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            }
            Divider(1372f);

            // 타이머
            var tm = UI.Fixed(_root, "Timer", HudLayout.TimerAnchor, HudLayout.TimerPos, HudLayout.TimerSize);
            UI.Label(tm, "침수까지", 11, HudKit.Dim, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(150f, 16f), TextAnchor.MiddleRight, new Vector2(1f, 1f));
            _timer = UI.Label(tm, "12:00", 26, HudKit.Red, new Vector2(1f, 1f), new Vector2(-8f, -24f), new Vector2(150f, 32f), TextAnchor.MiddleRight, new Vector2(1f, 1f));
            _timer.fontStyle = FontStyle.Bold;

            // ? 버튼
            var hb = UI.Fixed(_root, "HelpHolder", new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(44f, 44f), new Vector2(1f, 0.5f));
            _help = UI.Button(hb, "?", HudKit.Slate, onHelp, 20, 44, 44);
            var hrt = (RectTransform)_help.transform; hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            HudKit.Frame(hrt, HudKit.Border, 1f);
        }

        private void Divider(float x)
        {
            var d = UI.Fixed(_root, "Div", new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(1f, 44f), new Vector2(0f, 0.5f));
            UI.Bg(d, HudKit.Border, false);
        }

        /// <summary>상단 진행 표시 문구(명세 5-2).</summary>
        public static string ProgressText(StageState st)
        {
            if (st == null) return "";
            switch (st.step)
            {
                case 0: return "관문 1 — 계산 자물쇠  <color=#F2C572>■</color><color=#2A3A55>□□</color>";
                case 1:
                    {
                        bool inMosaic = false;
                        var map = Session.Map;
                        if (map != null)
                            for (int i = 0; i < 4; i++)
                            {
                                var p = Session.PosOf(i);
                                if (p != null && map.RoomAt(p.Value) == 3) { inMosaic = true; break; }
                            }
                        if (inMosaic)
                        {
                            int r = Mathf.Clamp(st.round + 1, 1, 4);
                            string sub = st.round >= 4 ? "판독 대기" : $"라운드 {r}/4";
                            return $"관문 2 — 방향 자물쇠  <color=#F2C572>■■</color><color=#2A3A55>□</color>  <size=12><color=#8A96A8>· {sub}</color></size>";
                        }
                        return "이동 구간 — 침수 현황판  <color=#F2C572>■■</color><color=#2A3A55>□</color>";
                    }
                default: return "최종 집결 — 중앙 분수  <color=#F2C572>■■■</color>";
            }
        }

        public void OnState()
        {
            if (_root == null) return;
            var st = Session.St;
            int me = Session.LocalSlot;
            _progress.text = ProgressText(st);
            for (int i = 0; i < 4; i++)
            {
                bool mine = i == me;
                var rc = Roles.Color(i);
                _cellBg[i].color = mine ? new Color(rc.r * 0.22f + 0.06f, rc.g * 0.22f + 0.08f, rc.b * 0.22f + 0.12f, 1f) : HudKit.Slate;
                string name = Session.NameOf(i);
                // B4: 남의 담당 오브젝트는 비공개(규칙 S-03) — 남의 칸은 이름+역할만("담당 ?"), 내 칸만 오브젝트 이름
                string prop = mine ? Comms.PropForRole(i, st.step) : "담당 ?";
                _cellText[i].text = $"<color=#{Roles.Hex(i)}><b>{Roles.Name(i)}</b></color>{(mine ? " <size=10>(나)</size>" : "")}\n<size=11><color=#8A96A8>{prop} · {name}</color></size>";
            }
        }

        public void Tick()
        {
            if (_timer == null) return;
            var st = Session.St;
            TickRounds(st);
            if (st.cleared) { _timer.text = "<color=#7FE39B>CLEAR</color>"; return; }
            if (!st.timerRunning && !st.failed)
            {
                // 브리핑 게이트(D1): 전원이 튜토리얼을 마치거나 30초가 지나야 물이 차오르기 시작한다 (C4: 무엇을 기다리는지 그대로 적는다)
                _timer.text = $"<size=11>모두 [시작] 누르면\n시작 ({GameLogic.BriefCount(st)}/4)</size>";
                _timer.color = HudKit.Amber;
                return;
            }
            float left = Mathf.Max(0f, st.timed ? Session.TimeLeft : 0f);
            int m = (int)(left / 60f), s = (int)(left % 60f);
            _timer.text = $"{m:00}:{s:00}";
            bool warn = left < 60f && !st.failed;
            _timer.color = warn && Mathf.Sin(Time.time * 8f) > 0f ? HudKit.Ink : HudKit.Red;
        }

        /// <summary>D-A8: 관문 2 동안(gate1Done && !gate2Done) 라운드 4칸 — 끝난 라운드 앰버, 진행 중 칸 깜빡임, 남은 칸 테두리색. 판독 대기(4/4)는 4칸 모두 앰버.</summary>
        private void TickRounds(StageState st)
        {
            if (_roundBox == null) return;
            bool show = st != null && st.gate1Done && !st.gate2Done && !st.cleared && !st.failed;
            if (_roundBox.gameObject.activeSelf != show) _roundBox.gameObject.SetActive(show);
            if (!show) return;
            int done = Mathf.Clamp(st.round, 0, 4);
            bool blink = Mathf.Sin(Time.time * 5f) > 0f;
            for (int i = 0; i < 4; i++)
            {
                if (i < done) _roundBars[i].color = HudKit.Amber;
                else if (i == done) _roundBars[i].color = blink ? HudKit.Amber : HudKit.Alpha(HudKit.Amber, 0.35f);
                else _roundBars[i].color = HudKit.Border;
            }
            _roundText.text = done >= 4 ? "판독 대기" : $"라운드 {done + 1}/4";
        }
    }

    // =====================================================================================
    // 좌상단 역할 배지
    // =====================================================================================

    /// <summary>좌상단 역할 배지: 코드 + 역할명 + "국적 · 12세" + 이름.</summary>
    public class RoleBadge
    {
        private RectTransform _root;
        private RectTransform _code;
        private Text _text;
        private int _builtSlot = -1;
        private string _builtName = "";

        public void Build(RectTransform parent)
        {
            _root = HudKit.Box(parent, "RoleBadge", HudLayout.BadgeAnchor, HudLayout.BadgePos, HudLayout.BadgeSize, HudKit.Panel, HudKit.Border, 1f);
            _text = UI.Label(_root, "", 17, HudKit.Ink, new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(124f, 56f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            OnState();
        }

        public void OnState()
        {
            if (_root == null) return;
            int me = Session.LocalSlot;
            if (!Roles.Valid(me)) me = 0;
            var p = Session.Me;
            string name = p != null && !string.IsNullOrEmpty(p.name) ? p.name : Roles.Name(me);
            if (me == _builtSlot && name == _builtName) return;
            _builtSlot = me; _builtName = name;
            if (_code != null) Object.Destroy(_code.gameObject);
            _code = HudKit.CodeBadge(_root, me, 38f, 12);
            _code.anchorMin = _code.anchorMax = new Vector2(0f, 0.5f); _code.pivot = new Vector2(0f, 0.5f); _code.anchoredPosition = new Vector2(12f, 0f);
            _text.text = $"<color=#{Roles.Hex(me)}><b>{Roles.Name(me)}</b></color>\n<size=11><color=#8A96A8>{Comms.NationShort(me)} · 12세 · {name}</color></size>";
        }
    }

    // =====================================================================================
    // 좌하단 액션 열
    // =====================================================================================

    /// <summary>좌하단 액션 열: 수신 로그(Q·도넛) / 지난 신호(C) / 상호작용(E) / 확대(TAB). 클릭도 동작. 아이콘은 SpriteLib 픽셀 픽토그램(P-A9).</summary>
    public class ActionBar
    {
        private RectTransform _root;
        private readonly Image[] _icons = new Image[4];
        private readonly Image[] _iconArt = new Image[4];
        private readonly Image[] _labels = new Image[4];

        public void Build(RectTransform parent, UnityAction onDonut, UnityAction onLog, UnityAction onInteract, UnityAction onZoom)
        {
            _root = UI.Fixed(parent, "ActionBar", HudLayout.ActionAnchor, HudLayout.ActionPos, HudLayout.ActionSize);
            UI.VStack(_root, 0, 10, false, TextAnchor.LowerLeft);
            Item(0, "donut", "수신 로그", "Q · 도넛 열기", onDonut);
            Item(1, "list", "지난 신호", "C", onLog);
            Item(2, "hand", "상호작용", "E", onInteract);
            Item(3, "zoom", "확대", "TAB", onZoom);
        }

        private void Item(int i, string iconKind, string label, string key, UnityAction onClick)
        {
            var row = HudKit.Row(_root, 44f, 8f, false, TextAnchor.MiddleLeft);
            var icon = UI.Button(row, "", HudKit.Slate, onClick, 18, 44, 44);
            UI.LE(icon, 44, 44, 0f);
            HudKit.Frame((RectTransform)icon.transform, HudKit.Border, 1f);
            _icons[i] = icon.targetGraphic as Image;
            _iconArt[i] = HudKit.IconImage((RectTransform)icon.transform, iconKind, 28f, HudKit.Ink);
            // 폭 176: "수신 로그  Q · 도넛 열기"가 한 줄에 들어간다(열 오른쪽 끝 16+44+8+176=244 < ActionSize.x 250)
            var b = UI.Button(row, $"{label}  <size=11><color=#8A96A8>{key}</color></size>", HudKit.Panel, onClick, 14, 36, 176);
            UI.LE(b, 176, 36, 0f);
            HudKit.Frame((RectTransform)b.transform, HudKit.Border, 1f);
            var bl = UI.ButtonLabel(b);
            bl.alignment = TextAnchor.MiddleLeft;
            bl.horizontalOverflow = HorizontalWrapMode.Overflow;   // 키 힌트가 길어도 두 줄로 꺾이지 않게
            var lrt = (RectTransform)bl.transform; lrt.offsetMin = new Vector2(12f, 2f);
            _labels[i] = b.targetGraphic as Image;
        }

        /// <summary>첫 줄 문구: 조작자는 도넛이 없으므로(D4) "말하기 T · 통역만 읽어요"로 바꾸고 아이콘도 말풍선(chat)으로.</summary>
        public void SetOperatorMode(bool op)
        {
            if (_labels[0] == null) return;
            var b = _labels[0].GetComponent<Button>();
            if (b == null) return;
            UI.ButtonLabel(b).text = op ? "말하기  <size=11><color=#8A96A8>T · 통역만 읽어요</color></size>" : "수신 로그  <size=11><color=#8A96A8>Q · 도넛 열기</color></size>";
            if (_iconArt[0] != null) _iconArt[0].sprite = SpriteLib.Icon(op ? "chat" : "donut");
        }

        /// <summary>열림 상태 강조(도넛/로그). 켜지면 버튼이 앰버가 되므로 아이콘은 어두운 잉크로.</summary>
        public void SetActive(int i, bool on)
        {
            if (i < 0 || i >= 4 || _icons[i] == null) return;
            _icons[i].color = on ? HudKit.Amber : HudKit.Slate;
            _labels[i].color = on ? new Color(0.16f, 0.15f, 0.10f, 0.98f) : HudKit.Panel;
            if (_iconArt[i] != null) _iconArt[i].color = on ? HudKit.InkDark : HudKit.Ink;
        }
    }

    // =====================================================================================
    // 최종 집결 게이지(중앙 상단, PDF s31)
    // =====================================================================================

    /// <summary>"중앙 분수 아트리움 · 전원 집결 n/4" + 4칸 바.</summary>
    public class GatherPanel
    {
        private RectTransform _root;
        private Text _count;
        private readonly Image[] _bars = new Image[4];

        public void Build(RectTransform parent)
        {
            _root = HudKit.Box(parent, "Gather", HudLayout.GatherAnchor, HudLayout.GatherPos, HudLayout.GatherSize, HudKit.Panel, HudKit.Amber, 2f);
            // 118px 높이에 맞춘 간격(셔터 개방 연출을 가리지 않도록 압축)
            UI.Label(_root, "중앙 분수 아트리움 · 전원 집결", 12, HudKit.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(340f, 18f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            _count = UI.Label(_root, "0 / 4", 34, HudKit.Ink, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(340f, 40f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            _count.fontStyle = FontStyle.Bold;
            for (int i = 0; i < 4; i++)
            {
                var b = UI.Fixed(_root, "Bar" + i, new Vector2(0.5f, 0f), new Vector2(-126f + i * 84f, 28f), new Vector2(76f, 8f), new Vector2(0.5f, 0f));
                _bars[i] = UI.Bg(b, HudKit.Border, false);
            }
            UI.Label(_root, "한 명이라도 구역을 벗어나면 게이지가 줄어든다", 10, HudKit.Dim, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(340f, 16f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
            _root.gameObject.SetActive(false);
        }

        public void Tick()
        {
            if (_root == null) return;
            var st = Session.St;
            // PDF s34 순서(체크 이펙트 + CLEAR → 셔터 개방): "4 / 4 ✓"를 1초 보여준 뒤 패널을 내려 셔터 개방과 CLEAR 배너만 남긴다
            bool show = st.step >= 2 && !st.failed && !(st.cleared && Session.Clock - st.clearClock > 1f);
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                var p = Session.PosOf(i);
                bool inside = p != null && Stage1Logic.InGather(i, p.Value, st, Session.Clock);
                if (inside) n++;
                _bars[i].color = inside ? Roles.Color(i) : HudKit.Border;
            }
            _count.text = st.cleared ? "<color=#7FE39B>4 / 4 ✓</color>" : $"{n} / 4";
        }
    }

    // =====================================================================================
    // 우하단 역할 패널
    // =====================================================================================

    /// <summary>
    /// 우하단 역할 패널(360폭, 내용에 따라 위로 늘어남). 역할별 도구 + 맨 아래 채팅 한 줄.
    /// 조작자: 관문 진행 3줄 + 받은 카드 / 통역: 숫자·방향 카드 / 관측자: 색 신고 4버튼 + 최근 신고 / 내비: 침수 8줄 + 휠 4버튼 + 여기로! 6버튼.
    /// </summary>
    public class RolePanel
    {
        private RectTransform _root;
        private int _builtSlot = -1;
        private Text _guide, _dynamic;
        private InputField _chat;
        private readonly List<string> _recentReports = new List<string>();
        private readonly List<float> _recentReportTime = new List<float>();
        private Text _recentText;
        private readonly Image[] _floodFill = new Image[8];
        private readonly Text[] _floodPct = new Text[8];
        private RectTransform _floodBox, _floodGrid;
        private Text _floodHint, _safeLine, _floodCaption;
        private float _floodShownClock = float.NaN;   // 격자에 칠해 둔 스냅샷 시각(boardClock) — 바뀔 때만 다시 칠한다
        private int _selNumber = -1, _selDir = -1;
        private Text _pending, _cardCaption;
        private readonly List<Button> _numBtns = new List<Button>();
        private readonly List<Button> _dirBtns = new List<Button>();
        private Text _obsBadge;                 // B1: 관측자 관문 2 "라운드 n · 신고 필요/완료" 배지
        private Image _obsBadgeBg;
        private readonly Text[] _gateRows = new Text[3];
        private readonly Image[] _gateBg = new Image[3];
        private float _nextTick;

        public InputField Chat => _chat;
        public bool ChatFocused => _chat != null && _chat.isFocused;
        /// <summary>패널 루트(테스트 훅: 내용이 늘어 조종 전환 줄을 덮지 않는지 높이를 검사한다).</summary>
        public RectTransform Root => _root;

        public void Build(RectTransform parent)
        {
            _root = HudKit.Box(parent, "RolePanel", HudLayout.RoleAnchor, HudLayout.RolePos, HudLayout.RoleSize, HudKit.Panel, HudKit.Border, 1f);
            UI.VStack(_root, 12, 6, false, TextAnchor.UpperLeft);
            UI.Fit(_root, false, true);
            Rebuild();
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            _chat = null;
        }

        public void FocusChat()
        {
            if (_chat == null) return;
            _chat.ActivateInputField();
            _chat.Select();
        }

        /// <summary>채팅 입력칸에 문장을 미리 써 둔다(점자 칩). Enter로 전송.</summary>
        public void SetDraft(string text)
        {
            if (_chat == null) return;
            _chat.text = text ?? "";
            FocusChat();
            _chat.caretPosition = _chat.text.Length;
        }

        public void OnState()
        {
            if (_root == null) return;
            int me = Session.LocalSlot;
            if (me != _builtSlot) { Rebuild(); return; }
            Refresh();
        }

        public void OnEvent(GameEvent e)
        {
            if (e == null) return;
            if (e.type == "Color" && e.slot == Session.LocalSlot)
            {
                var st = Session.St;
                string where = st.step == 0 ? "경고등" : $"색상등 {Mathf.Clamp(st.round + 1, 1, 4)}R";
                _recentReports.Insert(0, $"<color=#{HudKit.H(Comms.ColorValue(e.a))}>■</color> {Comms.ColorName(e.a)} · {where}");
                _recentReportTime.Insert(0, Time.time);
                while (_recentReports.Count > 3) { _recentReports.RemoveAt(3); _recentReportTime.RemoveAt(3); }
                RefreshRecent();
            }
            if (e.type == "NumberCard" || e.type == "DirCard")
            {
                // 통역이 보낸 뒤 선택 해제
                if (e.slot == Session.LocalSlot) { _selNumber = -1; _selDir = -1; RefreshCards(); }
                Refresh();
            }
        }

        // ---------------------------------------------------------------- 조립

        private void Rebuild()
        {
            HudKit.Clear(_root);
            HudKit.Frame(_root, HudKit.Border, 1f); // Clear가 테두리 조각도 지우므로 다시 붙인다
            int me = Session.LocalSlot;
            _builtSlot = me;
            _guide = null; _dynamic = null; _chat = null; _recentText = null; _floodBox = null; _floodGrid = null; _floodHint = null; _safeLine = null; _floodCaption = null; _pending = null;
            _cardCaption = null; _obsBadge = null; _obsBadgeBg = null;
            _floodShownClock = float.NaN;
            _numBtns.Clear(); _dirBtns.Clear();
            for (int i = 0; i < 8; i++) { _floodFill[i] = null; _floodPct[i] = null; }
            for (int i = 0; i < 3; i++) { _gateRows[i] = null; _gateBg[i] = null; }
            if (!Roles.Valid(me)) me = 0;

            switch (me)
            {
                case Comms.Op: BuildOperator(); break;
                case Comms.In: BuildInterpreter(); break;
                case Comms.Na: BuildNavigator(); break;
                default: BuildObserver(); break;
            }
            BuildChat(me);
            Refresh();
        }

        private void Header(string title, string right, int slot)
        {
            var row = HudKit.Row(_root, 22f, 6f, false, TextAnchor.MiddleLeft);
            var t = UI.Text(row, $"<b>{title}</b>", 14, Roles.Color(slot), TextAnchor.MiddleLeft, false);
            UI.LE(t, 120, 22, 1f);
            var r = UI.Text(row, right, 11, HudKit.Dim, TextAnchor.MiddleRight, false);
            UI.LE(r, 80, 22, 0f);
            var line = UI.Rect(_root, "Line"); UI.Bg(line, HudKit.Border, false); UI.LE(line, -1, 1);
            // 지금 할 일(관문 1 동안 상황에 따라 바뀜)
            var gbox = HudKit.Block(_root, "Guide", new Color(0.10f, 0.11f, 0.08f, 0.9f), 40f, HudKit.Alpha(HudKit.Amber, 0.35f));
            _guide = HudKit.Fill(gbox, "", 12, new Color(0.97f, 0.95f, 0.8f), TextAnchor.MiddleLeft, 8f);
        }

        private void BuildOperator()
        {
            Header("조작자 콘솔", "자유 채팅 가능", Comms.Op);
            UI.Text(_root, "관문 진행 상황", 11, HudKit.Dim, TextAnchor.MiddleLeft, false, 16);
            string[] names = { "계산 자물쇠 · 안내데스크", "방향 자물쇠 · 나침반 모자이크방", "최종 집결 · 중앙 분수 아트리움" };
            for (int i = 0; i < 3; i++)
            {
                var box = HudKit.Block(_root, "Gate" + i, HudKit.Slate, 34f, HudKit.Border);
                _gateBg[i] = box.GetComponent<Image>();
                var numBox = UI.Panel(box, "Num", HudKit.Amber, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(22f, 22f), new Vector2(0f, 0.5f));
                numBox.GetComponent<Image>().raycastTarget = false;
                HudKit.Fill(numBox, (i + 1).ToString(), 12, HudKit.InkDark, TextAnchor.MiddleCenter, 0f);
                _gateRows[i] = UI.Label(box, names[i], 12, HudKit.Ink, new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(290f, 30f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            }
            var cbox = HudKit.Block(_root, "Cards", HudKit.Dark, 30f, HudKit.Border);
            _dynamic = HudKit.Fill(cbox, "", 13, HudKit.Amber, TextAnchor.MiddleLeft, 8f);
        }

        private void BuildInterpreter()
        {
            Header("통역 · 숫자/방향 카드", "모두의 채팅을 읽어요", Comms.In);
            var pbox = HudKit.Block(_root, "Pending", HudKit.Dark, 30f, HudKit.Border);
            _pending = HudKit.Fill(pbox, "", 13, HudKit.Ink, TextAnchor.MiddleLeft, 8f);
            // B7: 지금 단계에 맞는 카드 세트만 켜 둔다(관문 1 = 숫자, 관문 2 = 방향) — 캡션으로 차례를 알려준다
            _cardCaption = UI.Text(_root, "", 11, HudKit.Amber, TextAnchor.MiddleLeft, false, 16);

            var grid = UI.Rect(_root, "Numbers");
            UI.Grid(grid, new Vector2(62f, 32f), new Vector2(6f, 6f), 5);
            UI.LE(grid, -1, 32 * 2 + 6);
            int[] order = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
            foreach (int n in order)
            {
                int v = n;
                var b = HudKit.Btn(grid, v.ToString(), HudKit.Slate, () => { _selNumber = _selNumber == v ? -1 : v; RefreshCards(); }, 16, 32, 62);
                _numBtns.Add(b);
            }
            var drow = HudKit.Row(_root, 34f, 6f, true);
            for (int d = 0; d < 4; d++)
            {
                int v = d;
                var b = HudKit.Btn(drow, Comms.DirGlyph(d), HudKit.Slate, () => { _selDir = _selDir == v ? -1 : v; RefreshCards(); }, 18, 34, 60);
                _dirBtns.Add(b);
            }
            var srow = HudKit.Row(_root, 36f, 6f, true);
            var send = HudKit.Btn(srow, "조작자에게 전송", HudKit.Teal, SendCards, 15, 36, 180, true);
            UI.LE(send, 180, 36, 2f);
            var clear = HudKit.Btn(srow, "지우기", HudKit.Slate, () => { _selNumber = -1; _selDir = -1; RefreshCards(); }, 14, 36, 80);
            UI.LE(clear, 80, 36, 1f);
            UI.Text(_root, "숫자·방향 카드는 조작자 화면에만 도착한다. 벽보·벽화 앞에서 보내야 한다.", 11, HudKit.Dim, TextAnchor.UpperLeft, true, 16);
        }

        private void SendCards()
        {
            if (_selNumber < 0 && _selDir < 0)
            {
                if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("보낼 카드를 먼저 고르세요 (숫자 또는 방향)", GameLogic.ToneWarn);
                return;
            }
            if (_selNumber >= 0) Session.Send("Card", _selNumber);
            if (_selDir >= 0) Session.Send("DirCard", _selDir);
        }

        private void BuildObserver()
        {
            Header("관측자 · 색상 신고", "도넛 · 채팅(통역만 읽음)", Comms.Ob);
            // B1: 관문 2 동안 "라운드 n · 신고 필요(깜빡)/완료 ✓" 배지 — 라운드가 바뀌면 색이 같아도 다시 신고해야 함을 눈에 띄게
            var bbox = HudKit.Block(_root, "RoundBadge", HudKit.Slate, 26f, HudKit.Border);
            _obsBadgeBg = bbox.GetComponent<Image>();
            _obsBadge = HudKit.Fill(bbox, "", 12, HudKit.Ink, TextAnchor.MiddleLeft, 8f);
            bbox.gameObject.SetActive(false);
            UI.Text(_root, "지금 보이는 색을 신고", 11, HudKit.Dim, TextAnchor.MiddleLeft, false, 16);
            var grid = UI.Rect(_root, "Colors");
            UI.Grid(grid, new Vector2(165f, 44f), new Vector2(6f, 6f), 2);
            UI.LE(grid, -1, 44 * 2 + 6);
            for (int c = 0; c < 4; c++)
            {
                int v = c;
                var col = Comms.ColorValue(c);
                HudKit.Btn(grid, $"■ {Comms.ColorName(c)}", HudKit.Mul(col, 0.55f), () => Session.Send("Color", v), 15, 44, 165);
            }
            UI.Text(_root, "최근 신고", 11, HudKit.Dim, TextAnchor.MiddleLeft, false, 16);
            var rbox = HudKit.Block(_root, "Recent", HudKit.Dark, 56f, HudKit.Border);
            _recentText = HudKit.Fill(rbox, "", 12, HudKit.Ink, TextAnchor.UpperLeft, 8f);
            RefreshRecent();
        }

        private void BuildNavigator()
        {
            Header("내비게이터 · 침수 현황판", "도넛 · 채팅(통역만 읽음)", Comms.Na);
            _floodBox = HudKit.Block(_root, "Flood", HudKit.Dark, -1, HudKit.Border);
            UI.VStack(_floodBox, 8, 3, false, TextAnchor.UpperLeft);
            // D-A4: 격자는 실시간이 아니라 마지막으로 현황판을 연 시각(boardClock)의 스냅샷 — 캡션에 "n초 전 확인"을 적는다
            _floodCaption = UI.Text(_floodBox, "침수 현황판 · 구역별 침수율", 11, HudKit.Dim, TextAnchor.MiddleLeft, false, 14);
            _floodHint = UI.Text(_floodBox, "현황판(관리실)에서 확인해야 값이 보여요", 12, HudKit.Dim, TextAnchor.MiddleLeft, true, 18);
            // 침수 8줄은 2열×4행 격자(0~3 왼쪽, 4~7 오른쪽) — 8줄 세로(149px)면 관문 1의 "지금 할 일" 줄과 합쳐 패널이 조종 전환 줄을 덮는다
            _floodGrid = UI.Rect(_floodBox, "Zones");
            var g = UI.Grid(_floodGrid, new Vector2(157f, 15f), new Vector2(6f, 2f));
            g.startAxis = GridLayoutGroup.Axis.Vertical; g.constraint = GridLayoutGroup.Constraint.FixedRowCount; g.constraintCount = 4;
            UI.LE(_floodGrid, -1, 15 * 4 + 2 * 3);
            for (int z = 0; z < 8; z++)
            {
                var row = HudKit.Row(_floodGrid, 15f, 4f, false);
                var name = UI.Text(row, Stage1Logic.ZoneName(z), 11, HudKit.Ink, TextAnchor.MiddleLeft, false);
                UI.LE(name, 76, 15, 0f);
                var bar = UI.Bar(row, new Color(0.12f, 0.16f, 0.24f), HudKit.Green, 8f);
                UI.LE(bar.transform.parent, 40, 8, 1f);
                _floodFill[z] = bar;
                var pct = UI.Text(row, "", 11, HudKit.Dim, TextAnchor.MiddleRight, false);
                UI.LE(pct, 28, 15, 0f);
                _floodPct[z] = pct;
            }
            _safeLine = UI.Text(_floodBox, "", 11, HudKit.Amber, TextAnchor.MiddleLeft, false, 16);
            var wrow = HudKit.Row(_root, 34f, 6f, true);
            string[] ids = { "GO", "DANGER", "WHERE", "YOU" };
            foreach (var id in ids)
            {
                string s = id;
                // 도넛·현황판 팝업과 같은 표기(영문 + 한국어, 위험은 한국어만)로 통일. 위험·you 는 오른쪽 위에 작은 픽셀 아이콘(D-A7)
                var sb = HudKit.Btn(wrow, Comms.SignalCaption(id, 10, false), HudKit.Mul(Comms.SignalColor(id), 0.75f), () => Session.Send("Signal", 0, 0, s), 13, 34, 70, true);
                string kind = Comms.SignalIconKind(id);
                if (kind != null)
                {
                    var ic = HudKit.IconImage((RectTransform)sb.transform, kind, 14f);
                    var irt = (RectTransform)ic.transform;
                    irt.anchorMin = irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(1f, 1f); irt.anchoredPosition = new Vector2(-3f, -3f);
                }
            }
            UI.Text(_root, "여기로!  <size=10>(방 입구에 10초간 표시)</size>", 11, HudKit.Dim, TextAnchor.MiddleLeft, false, 16);
            var grid = UI.Rect(_root, "Rooms");
            UI.Grid(grid, new Vector2(108f, 30f), new Vector2(6f, 6f), 3);
            UI.LE(grid, -1, 30 * 2 + 6);
            for (int r = 0; r < 6; r++)
            {
                int room = r;
                HudKit.Btn(grid, Stage1Logic.ZoneName(r), HudKit.Slate, () => Session.Send("Ping", room), 11, 30, 108);
            }
        }

        private void BuildChat(int me)
        {
            var line = UI.Rect(_root, "Line2"); UI.Bg(line, HudKit.Border, false); UI.LE(line, -1, 1);
            _chat = UI.Input(_root, me == Comms.In ? "모두가 읽을 수 있어요 · Enter" : "메시지 · Enter (통역만 읽어요)", "", 14, 60);
            UI.LE(_chat, -1, 36);
            _chat.onEndEdit.AddListener(text =>
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                var kb = Keyboard.current;
                bool enter = kb == null || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.enterKey.isPressed || kb.numpadEnterKey.isPressed;
                if (!enter) return;
                Session.Send("Chat", 0, 0, text.Trim());
                _chat.text = "";
            });
        }

        // ---------------------------------------------------------------- 갱신

        private void Refresh()
        {
            var st = Session.St;
            int me = Session.LocalSlot;
            if (_guide != null)
            {
                // A1: "지금 할 일"은 관문 1 뒤에도 항상 보인다(이동·관문 2·집결은 StageGuide.Move 의 "· " 한 줄 지시)
                var g = _guide.transform.parent.gameObject;
                if (!g.activeSelf) g.SetActive(true);
                _guide.text = "<b>지금 할 일</b>  " + StageGuide.Instruction(me);
            }
            RefreshObserverBadge(st);
            if (_gateRows[0] != null)
            {
                string[] names = { "계산 자물쇠 · 안내데스크", "방향 자물쇠 · 나침반 모자이크방", "최종 집결 · 중앙 분수 아트리움" };
                string[] state = new string[3];
                state[0] = st.gate1Done ? "<color=#7FE39B>완료 ✓</color>" : "<color=#F2C572>진행 중</color>";
                state[1] = st.gate2Done ? "<color=#7FE39B>완료 ✓</color>" : (!st.gate1Done ? "<color=#8A96A8>잠김</color>" : $"<color=#F2C572>진행 중 · 라운드 {Mathf.Clamp(st.round + 1, 1, 4)}/4</color>");
                int n = 0;
                if (st.step >= 2) for (int i = 0; i < 4; i++) { var p = Session.PosOf(i); if (p != null && Stage1Logic.InGather(i, p.Value, st, Session.Clock)) n++; }
                state[2] = st.cleared ? "<color=#7FE39B>완료 ✓</color>" : (st.step >= 2 ? $"<color=#F2C572>{n} / 4</color>" : "<color=#8A96A8>0 / 4</color>");
                for (int i = 0; i < 3; i++)
                {
                    bool cur = st.step == i && !st.cleared;
                    _gateRows[i].text = $"{names[i]}   <size=11>{state[i]}</size>";
                    _gateRows[i].color = cur ? HudKit.Ink : HudKit.Dim;
                    if (_gateBg[i] != null) _gateBg[i].color = cur ? new Color(0.16f, 0.15f, 0.10f, 1f) : HudKit.Slate;
                }
            }
            if (_dynamic != null && me == Comms.Op)
            {
                string num = st.numberCard >= 0 ? $"숫자 <b>{st.numberCard}</b>" : "숫자 —";
                string dir = st.dirCard >= 0 ? $"방향 <b>{Comms.DirGlyph(st.dirCard)}</b>" : "방향 —";
                _dynamic.text = $"통역이 보낸 카드:  {num}   /   {dir}";
            }
            RefreshCards();
            if (_floodBox != null)
            {
                bool opened = st.boardOpened;
                if (_floodHint != null) _floodHint.gameObject.SetActive(!opened);
                if (_safeLine != null)
                {
                    _safeLine.gameObject.SetActive(opened);
                    _safeLine.text = $"비상 전원 안전 라인: <b>{Stage1Logic.SafeLine(st)}번</b>  <color=#8A96A8>(배전함 판독의 열쇠)</color>";
                }
                if (_floodGrid != null) _floodGrid.gameObject.SetActive(opened);
            }
        }

        private void RefreshCards()
        {
            if (_pending == null) return;
            string n = _selNumber >= 0 ? $"<color=#F2C572><b>{_selNumber}</b></color>" : "<color=#8A96A8>—</color>";
            string d = _selDir >= 0 ? $"<color=#F2C572><b>{Comms.DirGlyph(_selDir)}</b></color>" : "<color=#8A96A8>—</color>";
            var st = Session.St;
            string sent = "";
            if (st.numberCard >= 0) sent += $"  <size=11><color=#8A96A8>보낸 숫자 {st.numberCard}</color></size>";
            if (st.dirCard >= 0) sent += $"  <size=11><color=#8A96A8>보낸 방향 {Comms.DirGlyph(st.dirCard)}</color></size>";
            _pending.text = $"전송 대기   {n}   {d}{sent}";
            // B7: 단계에 맞지 않는 세트는 회색·비활성(잘못 고른 선택도 지운다)
            bool numOn = st.step == 0 && !st.gate1Done;
            bool dirOn = st.step == 1 && st.gate1Done && !st.gate2Done && st.round < 4;
            if (!numOn && _selNumber >= 0) _selNumber = -1;
            if (!dirOn && _selDir >= 0) _selDir = -1;
            if (_cardCaption != null)
                _cardCaption.text = numOn ? "지금은 숫자 카드 차례" : (dirOn ? "지금은 방향 카드 차례" : "<color=#8A96A8>지금은 보낼 카드가 없어요</color>");
            var offBg = new Color(0.08f, 0.10f, 0.14f, 1f);
            var offFg = new Color(0.40f, 0.44f, 0.52f, 1f);
            for (int i = 0; i < _numBtns.Count; i++)
            {
                int v = i == 9 ? 0 : i + 1;
                _numBtns[i].interactable = numOn;
                UI.SetButtonColor(_numBtns[i], !numOn ? offBg : (_selNumber == v ? HudKit.Teal : HudKit.Slate));
                UI.ButtonLabel(_numBtns[i]).color = !numOn ? offFg : (_selNumber == v ? HudKit.InkDark : Color.white);
            }
            for (int i = 0; i < _dirBtns.Count; i++)
            {
                _dirBtns[i].interactable = dirOn;
                UI.SetButtonColor(_dirBtns[i], !dirOn ? offBg : (_selDir == i ? HudKit.Teal : HudKit.Slate));
                UI.ButtonLabel(_dirBtns[i]).color = !dirOn ? offFg : (_selDir == i ? HudKit.InkDark : Color.white);
            }
        }

        /// <summary>B1: 관측자 배지 — 관문 2(gate1Done && !gate2Done) 동안만. colorReport&lt;0 이면 "신고 필요"(Tick 에서 깜빡), 아니면 "완료 ✓".</summary>
        private void RefreshObserverBadge(StageState st)
        {
            if (_obsBadge == null || st == null) return;
            var go = _obsBadge.transform.parent.gameObject;
            bool show = st.gate1Done && !st.gate2Done && !st.cleared && !st.failed;
            if (go.activeSelf != show) go.SetActive(show);
            if (!show) return;
            int r = Mathf.Clamp(st.round + 1, 1, 4);
            bool need = st.colorReport < 0;
            if (st.round >= 4) { _obsBadge.text = "<color=#8A96A8>판독 대기 — 신고 없음</color>"; if (_obsBadgeBg != null) _obsBadgeBg.color = HudKit.Slate; return; }
            bool blink = Mathf.Sin(Time.time * 6f) > 0f;
            if (need)
            {
                _obsBadge.text = $"라운드 {r} · <b><color=#F2C572>신고 필요</color></b>  <size=10><color=#8A96A8>색이 같아도 다시</color></size>";
                if (_obsBadgeBg != null) _obsBadgeBg.color = blink ? new Color(0.24f, 0.20f, 0.10f, 1f) : HudKit.Slate;
            }
            else
            {
                _obsBadge.text = $"라운드 {r} · <color=#7FE39B>신고 완료 ✓</color>";
                if (_obsBadgeBg != null) _obsBadgeBg.color = new Color(0.08f, 0.16f, 0.11f, 1f);
            }
        }

        private void RefreshRecent()
        {
            if (_recentText == null) return;
            if (_recentReports.Count == 0) { _recentText.text = "<color=#8A96A8>아직 신고한 색이 없어요</color>"; return; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _recentReports.Count; i++)
                sb.Append(_recentReports[i]).Append(" · <color=#8A96A8>").Append(Comms.Ago(Time.time - _recentReportTime[i])).Append("</color>\n");
            _recentText.text = sb.ToString().TrimEnd();
        }

        public void Tick()
        {
            if (_root == null) return;
            var st = Session.St;
            // 침수 막대(현황판을 연 뒤): boardClock 시점 스냅샷 — 값은 다시 열 때만 바뀌고, 캡션의 "n초 전 확인"만 매초 흐른다
            if (_floodBox != null && st.boardOpened)
            {
                if (_floodShownClock != st.boardClock)
                {
                    _floodShownClock = st.boardClock;
                    for (int z = 0; z < 8; z++)
                    {
                        if (_floodFill[z] == null) continue;
                        float f = Stage1Logic.FloodSnapshot(st, z);   // D-A4: 실시간이 아니라 boardClock 시점의 값
                        _floodFill[z].fillAmount = f / 100f;
                        _floodFill[z].color = f < 40f ? HudKit.Green : (f < 70f ? HudKit.Yellow : HudKit.Red);
                        _floodPct[z].text = $"<color=#{HudKit.H(_floodFill[z].color)}>{f:0}%</color>";
                    }
                }
                if (_floodCaption != null)
                    _floodCaption.text = $"침수 현황판 · <color=#F2C572>{Comms.Ago(Stage1Logic.SecondsSinceBoard(st, Session.Clock))} 확인</color>  <color=#8A96A8>최신은 현황판에서</color>";
            }
            else if (_floodCaption != null && _floodCaption.text != "침수 현황판 · 구역별 침수율")
                _floodCaption.text = "침수 현황판 · 구역별 침수율";
            if (_obsBadge != null) RefreshObserverBadge(st);   // B1: 깜빡임은 매 프레임
            if (Time.time > _nextTick)
            {
                _nextTick = Time.time + 1f;
                RefreshRecent();
                if (_guide != null && _guide.transform.parent.gameObject.activeSelf) _guide.text = "<b>지금 할 일</b>  " + StageGuide.Instruction(Session.LocalSlot);
                if (_gateRows[0] != null && st.step >= 2) Refresh();
            }
        }
    }
}
