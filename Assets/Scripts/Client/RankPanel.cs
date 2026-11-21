using System.Collections.Generic;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 랭킹 창(타이틀 · 엔딩에서 연다). 모드(스토리/스피드런) × 난이도 탭(쉬움/보통/어려움) × 내 기록/온라인. 화면 위에 따로 캔버스를 띄운다.
    /// 조작: 마우스 클릭 · ← → 난이도 · Tab 로컬/온라인 · X/Esc 닫기.
    /// </summary>
    public sealed class RankPanel : MonoBehaviour
    {
        private static RankPanel _inst;
        public static bool IsOpen => _inst != null;
        /// <summary>닫힌 프레임(같은 클릭 · 키가 뒤 화면에 넘어가지 않게).</summary>
        public static int ClosedFrame = -10;

        /// <summary>board: 0~2 스토리(난이도) · 3~5 스피드런(난이도 + 3). -1 이면 지금 고른 난이도·모드.</summary>
        public static void Open(int board = -1, RankEntry highlight = null)
        {
            if (_inst != null) return;
            var go = new GameObject("RankPanel");
            _inst = go.AddComponent<RankPanel>();
            if (board < 0) board = Ranking.Board(Story1Net.Difficulty, Story1Net.Speedrun);
            _inst._diff = Mathf.Clamp(board % Ranking.Modes, 0, Story1Rules.Count - 1);
            _inst._speed = board >= Ranking.Modes;
            _inst._highlight = highlight;
            _inst._online = Ranking.CanSubmit;   // 웹이면 온라인부터
        }

        public static void CloseAll() { if (_inst != null) _inst.Close(); }

        private static readonly Color Ink = new Color(0.93f, 0.96f, 0.97f), Soft = new Color(0.78f, 0.84f, 0.88f),
            Mint = new Color(0.376f, 0.984f, 0.796f), Amber = new Color(0.949f, 0.773f, 0.447f), Bd = new Color(0.62f, 0.5f, 0.3f);

        private int _diff; private bool _online, _speed; private RankEntry _highlight;
        private int Board => Ranking.Board(_diff, _speed);
        private int _openFrame;
        private RectTransform _panel;
        private PixelText _status, _boardTitle;
        private const int MaxRows = 10;
        // 표의 칸(왼쪽 x · 너비): 순위 · 시간 · 팀 · 단풍 · 날짜
        private static readonly float[] ColX = { -600f, -470f, -290f, 330f, 450f };
        private static readonly float[] ColW = { 110f, 170f, 600f, 110f, 180f };
        private static readonly string[] ColName = { "순위", "시간", "팀", "단풍", "날짜" };
        private readonly PixelText[,] _cells = new PixelText[MaxRows, 5];
        private readonly Image[] _rowBg = new Image[MaxRows];
        private readonly List<(RectTransform r, Image bg, PixelText t, System.Action a, System.Func<bool> on)> _btns = new List<(RectTransform, Image, PixelText, System.Action, System.Func<bool>)>();
        private List<RankEntry> _onlineList; private int _onlineDiff = -1; private bool _loading;

        private void Start()
        {
            _openFrame = Time.frameCount;
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 300;
            var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;

            var back = Node("Back", root); Stretch(back);
            var bimg = back.gameObject.AddComponent<Image>(); bimg.color = new Color(0, 0, 0, 0.82f); bimg.raycastTarget = true;   // 뒤 화면 클릭 막기

            // 금색 테두리 = 창보다 4px 큰 판을 창 "뒤"에(창의 자식으로 두면 창 바탕을 덮어 전체가 금색이 된다)
            _panel = Node("Panel", root); _panel.sizeDelta = new Vector2(1320, 900);
            var pimg = _panel.gameObject.AddComponent<Image>(); pimg.color = new Color(0.05f, 0.06f, 0.09f, 1f);
            var frame = Node("Frame", _panel); Stretch(frame, -4f);
            var bdi = frame.gameObject.AddComponent<Image>(); bdi.color = Bd; bdi.raycastTarget = false;
            var inner = Node("Inner", _panel); Stretch(inner, 0f);
            var ii = inner.gameObject.AddComponent<Image>(); ii.color = new Color(0.05f, 0.06f, 0.09f, 1f); ii.raycastTarget = false;

            // 윗부분: 제목 → (모드 | 내 기록·온라인) → 난이도 → 지금 보는 기록표 이름
            Text(_panel, "랭킹", 4, Amber, 0, 392, 400, 70);
            Button("스토리", -500, 305, 190, 60, () => SetSpeed(false), () => !_speed);
            Button("스피드런", -300, 305, 190, 60, () => SetSpeed(true), () => _speed);
            Button("내 기록", 300, 305, 190, 60, () => SetOnline(false), () => !_online);
            Button("온라인", 500, 305, 190, 60, () => SetOnline(true), () => _online);
            for (int d = 0; d < Story1Rules.Count; d++)
            {
                int dd = d;
                Button(Story1Rules.Names[d], -210 + d * 210, 225, 190, 60, () => SetDiff(dd), () => _diff == dd);
            }
            _boardTitle = Text(_panel, "", 2, Soft, 0, 170, 1200, 36);

            // 표
            var line = Node("Line", _panel); line.anchoredPosition = new Vector2(0, 104); line.sizeDelta = new Vector2(1240, 3);
            var li = line.gameObject.AddComponent<Image>(); li.color = Bd; li.raycastTarget = false;
            for (int c = 0; c < 5; c++) Text(_panel, ColName[c], 2, Amber, ColX[c] + ColW[c] * 0.5f, 130, ColW[c], 36, TextAnchor.MiddleLeft);
            for (int i = 0; i < MaxRows; i++)
            {
                float y = 72 - i * 44;
                var bg = Node("Row" + i, _panel); bg.anchoredPosition = new Vector2(0, y); bg.sizeDelta = new Vector2(1240, 40);
                _rowBg[i] = bg.gameObject.AddComponent<Image>(); _rowBg[i].raycastTarget = false;
                _rowBg[i].color = i % 2 == 0 ? new Color(0.09f, 0.11f, 0.15f, 1f) : new Color(0.065f, 0.08f, 0.11f, 1f);
                for (int c = 0; c < 5; c++) _cells[i, c] = Text(_panel, "", 2, Ink, ColX[c] + ColW[c] * 0.5f, y, ColW[c], 38, TextAnchor.MiddleLeft);
            }
            _status = Text(_panel, "", 2, Soft, 0, -20, 1200, 40);
            Text(_panel, "← → 난이도     ↑ ↓ 스토리 · 스피드런     Tab 내 기록 · 온라인", 2, Soft, 0, -355, 1200, 36);
            Button("닫기  X", 0, -410, 260, 60, Close, () => false);
            Refresh();
        }

        private void SetDiff(int d) { _diff = d; Refresh(); }
        private void SetSpeed(bool on) { _speed = on; Refresh(); }
        private void SetOnline(bool on) { _online = on; Refresh(); }

        private void Refresh()
        {
            List<RankEntry> list;
            string status = "";
            _boardTitle.text = (_online ? "온라인" : "내 기록") + "  ·  " + (_speed ? "스피드런" : "스토리") + "  ·  " + Story1Rules.Names[_diff] + "  ·  빠른 순";
            if (_online)
            {
                if (_onlineDiff != Board && !_loading)
                {
                    _loading = true; int want = Board; _onlineList = null;
                    Ranking.FetchOnline(want, l => { if (this == null) return; _loading = false; _onlineDiff = want; _onlineList = l; Refresh(); });
                }
                list = _onlineDiff == Board ? _onlineList : null;
                if (_loading) status = "온라인 기록을 불러오는 중…";
                else if (list == null) status = "온라인 랭킹에 연결하지 못했어요 · 내 기록은 볼 수 있어요";
                else if (list.Count == 0) status = "아직 기록이 없어요 · 첫 기록의 주인공이 되어 보세요!";
            }
            else
            {
                list = Ranking.Local(Board);
                if (list.Count == 0) status = "아직 기록이 없어요 · " + Story1Rules.Names[_diff] + (_speed ? " 스피드런" : "") + "으로 탈출해 보세요";
            }
            for (int i = 0; i < MaxRows; i++)
            {
                var e = list != null && i < list.Count ? list[i] : null;
                _rowBg[i].enabled = e != null;
                if (e == null) { for (int c = 0; c < 5; c++) _cells[i, c].text = ""; continue; }
                bool mine = _highlight != null && Mathf.Abs(e.time - _highlight.time) < 0.05f && Ranking.TeamText(e) == Ranking.TeamText(_highlight);
                _cells[i, 0].text = (i + 1) + "위";
                _cells[i, 1].text = _speed ? Ranking.TimeTextFine(e.time) : Ranking.TimeText(e.time);
                _cells[i, 2].text = Ranking.TeamText(e);
                _cells[i, 3].text = e.cats >= 5 ? "5 ★" : e.cats + "/5";
                _cells[i, 4].text = e.date ?? "";
                Color col = mine ? Mint : i < 3 ? Amber : Ink;
                for (int c = 0; c < 5; c++) _cells[i, c].color = c == 4 && !mine ? Soft : col;
                _rowBg[i].color = mine ? new Color(0.1f, 0.24f, 0.21f, 1f) : i % 2 == 0 ? new Color(0.09f, 0.11f, 0.15f, 1f) : new Color(0.065f, 0.08f, 0.11f, 1f);
            }
            _status.text = status;
        }

        private void Close()
        {
            ClosedFrame = Time.frameCount;
            if (_inst == this) _inst = null;
            Paradox.Core.Sfx.Play("ui_back", 0.7f);
            Destroy(gameObject);
        }

        private void OnDestroy() { if (_inst == this) _inst = null; }

        private void Update()
        {
            // 좁은 화면에서도 창 전체가 보이게
            float cw = StoryText.CanvasWidth(_panel), ch = ((RectTransform)transform).rect.height;
            float s = Mathf.Clamp(Mathf.Min((cw - 32f) / 1340f, (ch - 32f) / 920f), 0.4f, 1f);
            _panel.localScale = new Vector3(s, s, 1f);

            var ms = Mouse.current; var kb = Keyboard.current;
            bool click = ms != null && ms.leftButton.wasPressedThisFrame && Time.frameCount > _openFrame;
            foreach (var b in _btns)
            {
                bool over = StoryText.Over(b.r), on = b.on();
                b.bg.color = on ? new Color(0.12f, 0.3f, 0.26f, 1f) : over ? new Color(0.16f, 0.2f, 0.26f, 1f) : new Color(0.09f, 0.11f, 0.15f, 1f);
                b.t.color = on ? Mint : Ink;
                if (click && over) { Paradox.Core.Sfx.Play("ui_select", 0.7f); b.a(); return; }
            }
            if (kb == null || Time.frameCount <= _openFrame) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) { Close(); return; }
            if (kb.leftArrowKey.wasPressedThisFrame) SetDiff((_diff + Story1Rules.Count - 1) % Story1Rules.Count);
            if (kb.rightArrowKey.wasPressedThisFrame) SetDiff((_diff + 1) % Story1Rules.Count);
            if (kb.tabKey.wasPressedThisFrame) SetOnline(!_online);
            if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) SetSpeed(!_speed);
        }

        // ---------------------------------------------------------------- 만들기 도우미
        private static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r, float inset = 0f) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset); }

        private static PixelText Text(RectTransform parent, string s, int scale, Color c, float x, float y, float w, float h, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var r = Node("T", parent); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
            var t = r.gameObject.AddComponent<PixelText>(); t.scale = scale; t.align = a; t.color = c; t.text = s; t.raycastTarget = false; t.maxWidth = w;
            return t;
        }

        private void Button(string label, float x, float y, float w, float h, System.Action act, System.Func<bool> on)
        {
            var r = Node("Btn", _panel); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
            var bg = r.gameObject.AddComponent<Image>(); bg.raycastTarget = false;
            var t = Text(r, label, 2, Ink, 0, 0, w - 12, h);
            _btns.Add((r, bg, t, act, on));
        }
    }
}
