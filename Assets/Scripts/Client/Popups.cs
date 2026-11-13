using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 담당자 화면에만 뜨는 퍼즐 팝업 8개(명세 5-4~5-6):
    /// panel(제어반) · poster(벽보) · lamp(경고등) · board(침수 현황판) · lever(레버반) · mural(벽화 매뉴얼) · colorlamp(색상등) · junction(배전함).
    /// 열릴 때 InteractionState.PushLock(이동 불가), 닫힐 때 PopLock. 규칙 판정은 전부 서버 — 여기서는 명령만 보내고 상태를 그린다.
    /// v2: 현황판이 안전 라인(D2)을 알려주고, 제어반은 오답 뒤 3초 잠금(D3), 조작자 팝업의 빠른 문구 버튼이 구조화 Chat(D4)을 보내며,
    /// 레버는 방향 카드가 오면 미리 선택되고(D6a), 담당 미션이 끝나면 "완료 ✓" 3초 뒤 자동으로 닫힌다(D6c).
    /// 카드는 780×팝업별 높이(CardHeight, 완료 상태 300), 가로 행은 고정 높이(Row — P-A3). 현황판 팝업은 실시간이고 "확인한 시각"을 표기한다(D-A4).
    /// </summary>
    public class Popups
    {
        /// <summary>담당 미션 완료 뒤 팝업이 스스로 닫히기까지의 시간.</summary>
        public const float DoneCloseDelay = 3f;

        /// <summary>팝업 카드 폭(고정). 높이는 팝업별(CardHeight) — 화면 중앙 유지.</summary>
        public const float CardW = 780f;
        /// <summary>"완료 ✓" 상태의 카드 높이.</summary>
        public const float DoneCardH = 300f;

        /// <summary>팝업별 카드 높이(P-A10): 램프 두 개는 내용이 적어 380, 나머지는 470.</summary>
        public static float CardHeight(string propId)
        {
            switch (propId)
            {
                case "lamp": case "colorlamp": return 380f;
                default: return 470f;
            }
        }

        private RectTransform _layer, _card, _body, _titleBar;
        private Image _titleSwatch;
        private Text _title;
        private string _current = "";
        private string _structKey = "";
        private bool _locked;
        private System.Action _refresh, _tick;
        private float _doneAt = -1f;      // "완료 ✓" 상태로 바뀐 시각(-1 = 아님)
        private Text _doneNote;

        // 통역 선택 / 조작자 레버 선택 / 관측자 신고 기록(라운드별, 오래된 것부터)
        private int _selNumber = -1, _selDir = -1, _selLever = -1, _seenDirCard = -1;
        private readonly List<string> _colorTags = new List<string>();
        private readonly List<string> _colorHistory = new List<string>();

        /// <summary>점자 칩을 누르면 채팅 초안을 써 달라고 알린다(StageScreen이 연결).</summary>
        public System.Action<string> ChatDraft;

        public bool IsOpen => _layer != null && _layer.gameObject.activeSelf;
        public string Current => IsOpen ? _current : "";
        /// <summary>담당 미션이 끝나 "완료 ✓" 상태(3초 뒤 자동 닫힘)인가.</summary>
        public bool IsDone => IsOpen && _doneAt >= 0f;

        // ---------------------------------------------------------------- 생명주기

        public void Build(RectTransform parent)
        {
            _layer = UI.Stretch(parent, "Popups");
            var dim = UI.Bg(_layer, new Color(0f, 0f, 0f, 0.85f), true);   // S-05: 팝업 뒤 월드가 거의 보이지 않게(0.55 → 0.85)
            dim.raycastTarget = true;
            _card = HudKit.Box(_layer, "Card", new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(CardW, 470f), HudKit.PanelSolid, HudKit.Border, 2f);
            _titleBar = UI.Rect(_card, "TitleBar");
            _titleBar.anchorMin = new Vector2(0f, 1f); _titleBar.anchorMax = new Vector2(1f, 1f); _titleBar.pivot = new Vector2(0.5f, 1f);
            _titleBar.anchoredPosition = Vector2.zero; _titleBar.sizeDelta = new Vector2(0f, 50f);
            UI.Bg(_titleBar, new Color(0.04f, 0.06f, 0.10f, 1f), false);
            var line = UI.Rect(_titleBar, "Line"); line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(1f, 0f); line.offsetMin = Vector2.zero; line.offsetMax = new Vector2(0f, 1f);
            UI.Bg(line, HudKit.Border, false);
            var sw = UI.Fixed(_titleBar, "Swatch", new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(18f, 18f), new Vector2(0f, 0.5f));
            _titleSwatch = UI.Bg(sw, HudKit.Amber, false);
            _title = UI.Label(_titleBar, "", 19, HudKit.Ink, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(640f, 40f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            var xb = UI.Button(_titleBar, "×", HudKit.Slate, Close, 20, 34, 34);
            var xrt = (RectTransform)xb.transform; xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 0.5f); xrt.pivot = new Vector2(1f, 0.5f); xrt.anchoredPosition = new Vector2(-10f, 0f); xrt.sizeDelta = new Vector2(34f, 34f);
            HudKit.Frame(xrt, HudKit.Border, 1f);
            _body = UI.Stretch(_card, "Body", 16f, 16f, 16f, 64f);
            UI.HStack(_body, 0, 14, false, TextAnchor.UpperLeft);
            _layer.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            Close();
            if (_layer != null) Object.Destroy(_layer.gameObject);
            _layer = null;
        }

        public void Open(string propId)
        {
            if (_layer == null || !Comms.IsProp(propId)) return;
            if (Comms.PropSlot(propId) != Session.LocalSlot) return;   // 명세 5-3: 팝업은 담당자 화면에만 존재(호출자와 별개로 한 번 더 막는다)
            if (IsOpen && _current == propId) return;
            _current = propId;
            _doneAt = -1f; _doneNote = null;
            _layer.gameObject.SetActive(true);
            if (!_locked) { InteractionState.PushLock(); _locked = true; }
            _card.sizeDelta = new Vector2(CardW, CardHeight(propId));   // P-A10: 팝업별 높이(카드 피벗이 중앙이라 위치는 그대로)
            BuildContent();
            // 이미 끝난 미션의 팝업을 열면 바로 "완료 ✓" 상태(3초 뒤 닫힘)
            if (Comms.MissionDone(propId, Session.St)) ShowDone();
            // 현황판을 열면 서버에 알린다(안전 라인이 배전함에도 표시된다)
            if (propId == "board") Session.Send("OpenBoard");
        }

        public void Close()
        {
            if (_layer == null || !IsOpen) return;
            HudKit.Clear(_body);
            _refresh = null; _tick = null;
            _layer.gameObject.SetActive(false);
            if (_locked) { InteractionState.PopLock(); _locked = false; }
            _current = "";
            _structKey = "";
            _doneAt = -1f; _doneNote = null;
            _selLever = -1; _seenDirCard = -1;
        }

        public void OnState()
        {
            if (!IsOpen) return;
            var st = Session.St;
            if (st.failed) { Close(); return; }
            // 미션이 끝난 오브젝트의 팝업은 "완료 ✓" 상태로 바뀌고 3초 뒤 스스로 닫힌다(오답·오판·리셋에는 닫지 않는다)
            if (Comms.MissionDone(_current, st)) { if (_doneAt < 0f) ShowDone(); return; }
            string key = StructKey();
            if (key != _structKey) BuildContent();
            else _refresh?.Invoke();
        }

        public void OnEvent(GameEvent e)
        {
            if (e == null) return;
            if (e.type == "Color" && e.slot == Session.LocalSlot)
            {
                // 라운드별 신고 기록: 같은 라운드는 최신 신고로 바꾸고, 오래된 라운드부터 "1R 빨강 · 2R 초록" 순으로 쌓는다
                var st = Session.St;
                string tag = st.step == 0 ? "경고등" : $"{Mathf.Clamp(st.round + 1, 1, 4)}R";
                string line = $"{tag} <color=#{HudKit.H(Comms.ColorValue(e.a))}>■</color>{Comms.ColorName(e.a)}";
                int at = _colorTags.IndexOf(tag);
                if (at >= 0) { _colorTags.RemoveAt(at); _colorHistory.RemoveAt(at); }
                _colorTags.Add(tag); _colorHistory.Add(line);
                while (_colorHistory.Count > 5) { _colorHistory.RemoveAt(0); _colorTags.RemoveAt(0); }
            }
            if (IsOpen && (e.type == "NumberCard" || e.type == "DirCard") && e.slot == Session.LocalSlot)
            {
                _selNumber = -1; _selDir = -1;
            }
            if (IsOpen && _doneAt < 0f) _refresh?.Invoke();
        }

        public void Tick()
        {
            if (!IsOpen) return;
            if (_doneAt >= 0f)
            {
                float left = DoneCloseDelay - (Time.time - _doneAt);
                if (left <= 0f) { Close(); return; }
                if (_doneNote != null) _doneNote.text = $"{Mathf.CeilToInt(left)}초 뒤 자동으로 닫혀요 — 닫히면 다시 움직일 수 있어요 (× 로 바로 닫기)";
                return;
            }
            _tick?.Invoke();
        }

        /// <summary>담당 미션 완료: 본문을 "완료 ✓"로 바꾼다. 3초 뒤 Tick 이 닫는다.</summary>
        private void ShowDone()
        {
            _doneAt = Time.time;
            _refresh = null; _tick = null;
            HudKit.Clear(_body);
            _card.sizeDelta = new Vector2(CardW, DoneCardH);   // P-A10: 완료 상태는 낮은 카드(본문 220px)
            var col = HudKit.Col(_body, 0, 10f);
            UI.LE(col, 300, -1, 1f);
            var box = DarkBox(col, 160f);
            var big = UI.Label(box, "완료 ✓", 44, HudKit.MonoGreen, new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(500f, 60f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            big.fontStyle = FontStyle.Bold;
            UI.Label(box, DoneText(_current), 16, HudKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(600f, 30f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            _doneNote = UI.Text(col, "", 12, HudKit.Dim, TextAnchor.MiddleCenter, true, 30);
        }

        private static string DoneText(string id)
        {
            switch (id)
            {
                case "panel": case "poster": case "lamp": return "관문 1 해제 — 모자이크방 문이 열렸다. 내비게이터의 안내를 따라가자";
                case "board": return "관문 2 해제 — 이제 중앙 분수 아트리움으로";
                default: return "관문 2 해제 — 셔터 개방! 모두 중앙 분수 아트리움으로";
            }
        }

        private string StructKey()
        {
            var st = Session.St;
            return $"{_current}_{st.round}_{st.gate2Attempts}_{st.seed}_{st.retries}_{Session.LocalSlot}";
        }

        // ---------------------------------------------------------------- 조립

        private void BuildContent()
        {
            HudKit.Clear(_body);
            _refresh = null; _tick = null;
            _structKey = StructKey();
            int owner = Comms.PropSlot(_current);
            _titleSwatch.color = Roles.Color(owner);
            _title.text = $"{Comms.PropTitle(_current)}  <size=12><color=#8A96A8>담당: {Roles.Name(owner)}</color></size>";
            switch (_current)
            {
                case "panel": BuildPanel(); break;
                case "poster": BuildPoster(); break;
                case "lamp": BuildLamp(); break;
                case "board": BuildBoard(); break;
                case "lever": BuildLever(); break;
                case "mural": BuildMural(); break;
                case "colorlamp": BuildColorLamp(); break;
                case "junction": BuildJunction(); break;
            }
            _refresh?.Invoke();
        }

        /// <summary>왼쪽 넓은 열 / 오른쪽 좁은 열.</summary>
        private RectTransform Left(float flex = 1.55f)
        {
            var c = HudKit.Col(_body, 0, 10f);
            UI.LE(c, 300, -1, flex);
            return c;
        }

        private RectTransform Right(float width = 280f)
        {
            var c = HudKit.Col(_body, 0, 10f);
            UI.LE(c, width, -1, 0f, -1, width);
            return c;
        }

        private static RectTransform DarkBox(Transform parent, float minH)
            => HudKit.Block(parent, "Dark", HudKit.Dark, minH, HudKit.Border, 2f);

        /// <summary>
        /// 고정 높이 가로 행(P-A3). HudKit.Row 는 안쪽 HStack 의 childForceExpandHeight 때문에 flexibleHeight 1 을 보고해
        /// 열(VStack)의 남는 높이를 받아 세로로 늘어난다 — LayoutElement(우선순위가 높다)로 flexibleHeight 0 을 못 박는다.
        /// </summary>
        private static RectTransform Row(Transform parent, float h, float spacing, bool expand)
        {
            var rt = HudKit.Row(parent, h, spacing, expand);
            UI.LE(rt, -1, h, -1, 0f);
            return rt;
        }

        private static Text Note(Transform parent, string text, float minH = 44f)
        {
            var box = HudKit.Block(parent, "Note", HudKit.Slate, minH, HudKit.Border);
            return HudKit.Fill(box, text, 12, HudKit.Ink, TextAnchor.MiddleLeft, 10f);
        }

        // ---------------------------------------------------------------- 조작자 채팅(A2: 통역이 팝업을 연 채로 읽는다)

        /// <summary>수신 로그(Session.Log)의 가장 최근 조작자 채팅. 없으면 false.</summary>
        private static bool LatestOpChat(out GameEvent e, out float at)
        {
            for (int i = Session.Log.Count - 1; i >= 0; i--)
            {
                var le = Session.Log[i];
                if (le.e != null && le.e.type == "Chat" && le.e.slot == (int)GameRole.Operator) { e = le.e; at = le.t; return true; }
            }
            e = null; at = 0f; return false;
        }

        private static string Ago(float t)
        {
            float s = Mathf.Max(0f, Time.time - t);
            if (s < 1f) return "방금";
            if (s < 60f) return $"{Mathf.FloorToInt(s)}초 전";
            return $"{Mathf.FloorToInt(s / 60f)}분 전";
        }

        /// <summary>
        /// "지금 받은 정보" 칸의 조작자 채팅 줄. 통역만 여는 팝업이므로 원문을 그대로 보여 준다.
        /// 구조화 값(a=1 홀짝 b, a=2 점 비트마스크 — 조작자 팝업의 빠른 문구)이 있으면 그 뜻("홀수"/"짝수", "점자 1,2,4")을 굵게.
        /// </summary>
        private static string OpChatLine(string label, string waiting)
        {
            if (!LatestOpChat(out var e, out float at))
                return $"<color=#3A4A66>□</color> {label}: <color=#8A96A8>{waiting}</color>";
            string body = e.a == 1
                ? $"<b>{(e.b != 0 ? "홀수" : "짝수")}</b>  <size=11><color=#8A96A8>\"{e.s}\"</color></size>"
                : $"<b>{e.s}</b>";
            return $"<color=#F2C572>■</color> {label}: {body}  <size=11><color=#8A96A8>({Ago(at)} · 조작자 채팅)</color></size>";
        }

        /// <summary>"n초 전"이 흐르도록 열려 있는 동안 0.5초마다 다시 그린다(새 채팅은 OnEvent 가 즉시 반영).</summary>
        private void TickRefreshSlow()
        {
            if (Time.frameCount % 30 == 0) _refresh?.Invoke();
        }

        private static Image Lamp(RectTransform parent, float size, Vector2 pos)
        {
            HudKit.Dot(parent, pos, size * 1.35f, new Color(1f, 1f, 1f, 0.10f), "Glow");
            var ring = HudKit.Dot(parent, pos, size + 8f, new Color(0.05f, 0.06f, 0.08f, 1f), "Rim");
            ring.sprite = SpriteLib.Disc(0.04f);
            var img = HudKit.Dot(parent, pos, size, Color.gray, "Lamp");
            img.sprite = SpriteLib.Disc(0.05f);
            return img;
        }

        // ---------------------------------------------------------------- 관문 1 · 제어반(조작자)

        private void BuildPanel()
        {
            var st = Session.St;
            var left = Left();
            var screen = DarkBox(left, 186f);
            UI.Label(screen, "연산 결과", 12, HudKit.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(300f, 18f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            var big = UI.Label(screen, "", 60, HudKit.MonoGreen, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(400f, 76f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            big.fontStyle = FontStyle.Bold;
            var formula = UI.Label(screen, "", 18, HudKit.MonoGreen, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 26f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
            var hint = Note(left, "<color=#F2C572>?</color>  램프 색과 매뉴얼은 내 담당이 아니다. 결과가 <b>홀수인지 짝수인지</b> 통역에게 알리자 — 아래 버튼 한 번이면 된다.", 52f);
            // 빠른 문구(D4): 구조화 Chat(a=1, b=1 홀/0 짝). 통역만 읽는다
            var quick = Row(left, 40f, 8f, true);
            HudKit.Btn(quick, "홀수라고 말하기", HudKit.Teal, () => Session.Send("Chat", 1, 1, "결과 홀수야"), 14, 40, 120, true);
            HudKit.Btn(quick, "짝수라고 말하기", HudKit.Teal, () => Session.Send("Chat", 1, 0, "결과 짝수야"), 14, 40, 120, true);
            var card = Note(left, "", 40f);
            card.color = HudKit.Amber;

            var right = Right();
            var input = DarkBox(right, 60f);
            UI.Label(input, "입력", 12, HudKit.Dim, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(60f, 20f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            var inText = UI.Label(input, "", 30, HudKit.Amber, new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(200f, 50f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            inText.fontStyle = FontStyle.Bold;

            var keys = new List<Button>();
            var grid = UI.Rect(right, "Keypad");
            UI.Grid(grid, new Vector2(88f, 46f), new Vector2(8f, 8f), 3);
            UI.LE(grid, -1, 46 * 4 + 8 * 3);
            for (int n = 1; n <= 9; n++)
            {
                int v = n;
                keys.Add(HudKit.Btn(grid, v.ToString(), HudKit.Slate, () => Session.Send("Digit", v), 20, 46, 88));
            }
            UI.Rect(grid, "Pad");
            keys.Add(HudKit.Btn(grid, "0", HudKit.Slate, () => Session.Send("Digit", 0), 20, 46, 88));
            UI.Rect(grid, "Pad");
            var row = Row(right, 44f, 8f, true);
            keys.Add(HudKit.Btn(row, "지우기", HudKit.Slate, () => Session.Send("DigitClear"), 15, 44, 100));
            keys.Add(HudKit.Btn(row, "확인", HudKit.Amber, () => Session.Send("DigitConfirm"), 17, 44, 120, true));
            var lockText = UI.Text(right, "", 12, HudKit.Red, TextAnchor.MiddleLeft, true, 18);

            _refresh = () =>
            {
                var s = Session.St;
                big.text = s.calcResult.ToString();
                formula.text = $"{s.calcA} × {s.calcB} {(s.calcOp == 0 ? "+" : "−")} {s.calcC}";
                inText.text = string.IsNullOrEmpty(s.calcInput) ? "<color=#3A4A66>_</color>" : s.calcInput + " <color=#3A4A66>_</color>";
                card.text = s.numberCard >= 0
                    ? $"통역이 보낸 숫자: <b><size=18>{s.numberCard}</size></b>   <size=11><color=#8A96A8>(직접 눌러서 입력)</color></size>"
                    : "<color=#8A96A8>통역이 보낸 숫자: 아직 없음</color>";
                if (s.calcFails > 0) hint.text = $"<color=#E5484D>오답 {s.calcFails}회 (−{Stage1Logic.Gate1Penalty:0}초씩)</color>  홀짝을 다시 확인하고, 통역에게 다시 물어보자.";
            };
            // 키보드로도 입력(팝업이 잠금을 쥐고 있어 1~4 조종 전환과 겹치지 않는다). 채팅 입력 중에는 무시. 오답 잠금(D3) 중에는 키패드 비활성 + 카운트다운.
            _tick = () =>
            {
                var s = Session.St;
                bool locked = Stage1Logic.KeypadLocked(s, Session.Clock);
                foreach (var k in keys) k.interactable = !locked;
                lockText.text = locked ? $"<b>잠금 {Mathf.CeilToInt(s.lockUntil1 - Session.Clock)}초</b> — 오답 뒤 {Stage1Logic.Gate1Lock:0}초 동안은 입력할 수 없어요" : "";
                if (locked) return;
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return;
                if (StageScreen.Instance != null && StageScreen.Instance.ChatFocused) return;
                var digits = new[] { kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
                for (int n = 0; n <= 9; n++)
                    if (digits[n] != null && digits[n].wasPressedThisFrame) { Session.Send("Digit", n); return; }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Session.Send("DigitConfirm");
                else if (kb.backspaceKey.wasPressedThisFrame) Session.Send("DigitClear");
            };
        }

        // ---------------------------------------------------------------- 관문 1 · 벽보(통역)

        private void BuildPoster()
        {
            var left = Left();
            var paper = HudKit.Block(left, "Paper", HudKit.Cream, 330f);
            UI.VStack(paper, 16, 8, false, TextAnchor.UpperLeft);
            UI.Text(paper, "<b>계산 모듈 안내</b>", 20, HudKit.InkDark, TextAnchor.MiddleLeft, false, 28);
            UI.Text(paper, "결과의 홀수 / 짝수 여부와 램프 색을 맞춰 올바른 숫자를 조작자에게 알려라.", 13, HudKit.InkDark, TextAnchor.UpperLeft, true, 36);
            var grid = UI.Rect(paper, "Table");
            UI.Grid(grid, new Vector2(80f, 44f), new Vector2(4f, 4f), 5);
            UI.LE(grid, -1, 44 * 3 + 8);
            HudKit.Cell(grid, "색 →", HudKit.Tan, HudKit.InkDark, 14, FontStyle.Bold, HudKit.InkDark);
            for (int c = 0; c < 4; c++) HudKit.Cell(grid, Comms.ColorName(c), Comms.ColorValue(c), c == 1 ? HudKit.InkDark : Color.white, 14, FontStyle.Bold, HudKit.InkDark);
            string[] rows = { "짝수", "홀수" };
            for (int r = 0; r < 2; r++)
            {
                HudKit.Cell(grid, rows[r], HudKit.Tan, HudKit.InkDark, 14, FontStyle.Bold, HudKit.InkDark);
                for (int c = 0; c < 4; c++) HudKit.Cell(grid, Comms.Table1[r, c].ToString(), Color.white, HudKit.InkDark, 22, FontStyle.Bold, HudKit.InkDark);
            }
            UI.Text(paper, "<size=11>· 홀짝은 조작자가 채팅으로 알려 준다  · 색은 관측자의 신고로 온다</size>", 11, new Color(0.35f, 0.32f, 0.28f), TextAnchor.UpperLeft, true, 18);

            var right = Right();
            var info = HudKit.Block(right, "Info", HudKit.Slate, 92f, HudKit.Border);   // A2: 조작자 채팅 원문 줄이 들어가 70 → 92
            var infoText = HudKit.Fill(info, "", 13, HudKit.Ink, TextAnchor.MiddleLeft, 10f);
            Note(right, "이 스테이지의 표지판은 필리핀어다. 나만 읽을 수 있다.", 36f);
            var numGrid = UI.Rect(right, "Numbers");
            UI.Grid(numGrid, new Vector2(50f, 34f), new Vector2(6f, 6f), 5);
            UI.LE(numGrid, -1, 34 * 2 + 6);
            var btns = new List<Button>();
            int[] order = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
            foreach (int n in order)
            {
                int v = n;
                btns.Add(HudKit.Btn(numGrid, v.ToString(), HudKit.Slate, () => { _selNumber = _selNumber == v ? -1 : v; _refresh?.Invoke(); }, 16, 34, 50));
            }
            var send = HudKit.Btn(right, "숫자 카드로 알리기", HudKit.Teal, () =>
            {
                if (_selNumber < 0) { if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("숫자를 먼저 고르세요", GameLogic.ToneWarn); return; }
                Session.Send("Card", _selNumber);
            }, 15, 42, 200, true);
            var status = UI.Text(right, "", 12, HudKit.Dim, TextAnchor.UpperLeft, true, 44);   // B2: 오답 안내 2줄

            _refresh = () =>
            {
                var s = Session.St;
                string lamp = s.colorReport >= 0
                    ? $"<color=#{HudKit.H(Comms.ColorValue(s.colorReport))}>■</color> 램프: <b>{Comms.ColorName(s.colorReport)}</b> (관측자)"
                    : "<color=#3A4A66>□</color> 램프: <color=#8A96A8>미확인 (관측자의 신고를 기다린다)</color>";
                infoText.text = $"<size=11><color=#8A96A8>지금 받은 정보</color></size>\n{lamp}\n{OpChatLine("홀짝", "조작자 채팅에서 읽는다")}";
                for (int i = 0; i < btns.Count; i++)
                {
                    int v = i == 9 ? 0 : i + 1;
                    UI.SetButtonColor(btns[i], _selNumber == v ? HudKit.Teal : HudKit.Slate);
                    UI.ButtonLabel(btns[i]).color = _selNumber == v ? HudKit.InkDark : Color.white;
                }
                UI.ButtonLabel(send).text = _selNumber >= 0 ? $"숫자 카드 {_selNumber} 로 알리기" : "숫자 카드로 알리기";
                status.text = s.lastWrongDigit >= 0
                    ? $"<color=#E5484D>{Stage1Logic.WrongDigitText(s.lastWrongDigit)}</color>"
                    : (s.numberCard >= 0 ? $"보낸 카드: <color=#F2C572>{s.numberCard}</color> — 조작자 화면에 도착했다. 틀렸으면 다시 보낼 수 있다." : "");
            };
            _tick = TickRefreshSlow;
        }

        // ---------------------------------------------------------------- 관문 1 · 경고등(관측자)

        private void BuildLamp()
        {
            var left = Left();
            var box = DarkBox(left, 300f);
            UI.Label(box, "경고등 #1", 12, HudKit.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(300f, 18f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            var lamp = Lamp(box, 130f, new Vector2(0f, 14f));
            UI.Label(box, "색을 읽고 신고한다", 15, HudKit.Ink, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(300f, 24f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));

            var right = Right();
            var btns = new List<Button>();
            for (int c = 0; c < 4; c++)
            {
                int v = c;
                btns.Add(HudKit.Btn(right, $"{Comms.ColorName(c)} 신고", Comms.ColorValue(c), () => Session.Send("Color", v), 16, 52, 200, c == 1));
            }
            Note(right, "색만 보낼 수 있다. 어디에 쓰는지는 모른다. 신고는 통역 화면에만 도착한다.", 50f);

            _refresh = () =>
            {
                var s = Session.St;
                lamp.color = Comms.ColorValue(s.lampColor);
                for (int i = 0; i < btns.Count; i++)
                    UI.ButtonLabel(btns[i]).text = s.colorReport == i ? $"{Comms.ColorName(i)} 신고  ✓" : $"{Comms.ColorName(i)} 신고";
            };
        }

        // ---------------------------------------------------------------- 이동 구간 · 침수 현황판(내비게이터)

        private void BuildBoard()
        {
            var left = Left();
            var box = DarkBox(left, 340f);
            UI.VStack(box, 12, 4, false, TextAnchor.UpperLeft);
            UI.Text(box, "구역별 침수율 · 실시간", 12, HudKit.Dim, TextAnchor.MiddleLeft, false, 16);
            var fills = new Image[8];
            var pcts = new Text[8];
            for (int z = 0; z < 8; z++)
            {
                var row = Row(box, 22f, 8f, false);
                var name = UI.Text(row, Stage1Logic.ZoneName(z), 13, HudKit.Ink, TextAnchor.MiddleLeft, false);
                UI.LE(name, 130, 22, 0f);
                var bar = UI.Bar(row, new Color(0.12f, 0.16f, 0.24f), HudKit.Green, 16f);
                UI.LE(bar.transform.parent, 120, 16, 1f);
                fills[z] = bar;
                pcts[z] = UI.Text(row, "", 13, HudKit.Green, TextAnchor.MiddleRight, false);
                UI.LE(pcts[z], 46, 22, 0f);
            }
            UI.Text(box, "<size=11><color=#8A96A8>초록 40% 미만 · 노랑 70% 미만 · 빨강 70% 이상(위험 구역: 속도 절반)</color></size>", 11, HudKit.Dim, TextAnchor.UpperLeft, true, 14);
            // 비상 전원 라인(D2): 안전 단자 번호는 여기서만 알 수 있다 — 배전함 판독의 열쇠
            UI.Text(box, "<b>비상 전원 라인 1~8</b>  <size=11><color=#8A96A8>○ 건전 · × 침수 단락 — 배전함 판독의 열쇠, 번호를 기억하자</color></size>", 12, HudKit.Amber, TextAnchor.MiddleLeft, false, 18);
            var lineRow = Row(box, 40f, 4f, true);
            var lineCells = new Text[8];
            for (int t = 0; t < 8; t++)
                lineCells[t] = HudKit.Cell(lineRow, "", HudKit.Slate, HudKit.Ink, 13, FontStyle.Bold, HudKit.Border);

            var right = Right();
            Note(right, "안전한 경로를 골라 도넛으로 팀을 이동시킨다. 표지판은 읽을 수 없다. 수족관방·연못정원방은 빠른 지름길이지만 물이 빨리 찬다.", 64f);
            // PDF s26 처럼 큰 버튼 두 개 나란히(100px). 행은 고정 높이(P-A3)
            var row2 = Row(right, 100f, 10f, true);
            HudKit.Btn(row2, "go", HudKit.Mul(Comms.SignalColor("GO"), 0.9f), () => Session.Send("Signal", 0, 0, "GO"), 24, 100, 100, true);
            HudKit.Btn(row2, "위험", HudKit.Mul(Comms.SignalColor("DANGER"), 0.9f), () => Session.Send("Signal", 0, 0, "DANGER"), 24, 100, 100, true);
            Note(right, "역할 패널의 <b>여기로!</b> 버튼으로 방 입구에 표시를 남길 수 있다.", 44f);
            // D-A4 "가서 봐야 아는": 팝업은 실시간, 역할 패널·미니맵은 마지막으로 연 시각의 스냅샷 — 그 시각을 여기에 표기
            var stamp = UI.Text(right, "", 12, HudKit.Amber, TextAnchor.UpperLeft, true, 34);

            _tick = () =>
            {
                var s = Session.St;
                float clock = Session.Clock;
                float since = Stage1Logic.SecondsSinceBoard(s, clock);
                string when = since < 0f ? "<color=#8A96A8>—</color>" : (since < 1f ? "<b>지금</b>" : $"<b>{Mathf.FloorToInt(since)}초 전</b>");
                stamp.text = $"확인한 시각: {when}  <size=11><color=#8A96A8>역할 패널·미니맵은 이 시각의 값 — 다시 열면 갱신</color></size>";
                for (int z = 0; z < 8; z++)
                {
                    float f = Stage1Logic.Flood(s, z, clock);
                    fills[z].fillAmount = f / 100f;
                    var c = f < 40f ? HudKit.Green : (f < 70f ? HudKit.Yellow : HudKit.Red);
                    fills[z].color = c;
                    pcts[z].color = c;
                    pcts[z].text = $"{f:0}%";
                }
                int safe = Stage1Logic.SafeLine(s);
                for (int t = 0; t < 8; t++)
                {
                    bool ok = t + 1 == safe;
                    lineCells[t].text = $"<size=11>{t + 1}</size>\n<b>{(ok ? "○" : "×")}</b>";
                    lineCells[t].color = ok ? HudKit.MonoGreen : HudKit.Red;
                }
            };
        }

        // ---------------------------------------------------------------- 관문 2 · 레버반(조작자)

        private static readonly Vector2[] DotPos =
        {
            new Vector2(-26f, 52f), new Vector2(-26f, 0f), new Vector2(-26f, -52f),   // 1 2 3 (왼쪽 열 위→아래)
            new Vector2(26f, 52f), new Vector2(26f, 0f), new Vector2(26f, -52f)       // 4 5 6 (오른쪽 열)
        };

        private void BuildLever()
        {
            var st = Session.St;
            int r = Mathf.Clamp(st.round, 0, 3);
            bool done = st.round >= 4;
            int digit = !done && st.brailleDigit != null && st.brailleDigit.Length > r ? st.brailleDigit[r] : -1;
            var dots = done ? new int[0] : Stage1Logic.BrailleDots(digit);
            bool Lit(int n) { foreach (var d in dots) if (d == n) return true; return false; }

            var left = Left();
            var box = DarkBox(left, 286f);
            UI.Label(box, "점자 숫자 표시부", 12, HudKit.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(300f, 18f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            var cell = HudKit.Box(box, "Cell", new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(124f, 176f), new Color(0.06f, 0.08f, 0.13f, 1f), HudKit.Border, 2f);
            for (int i = 0; i < 6; i++)
            {
                bool lit = Lit(i + 1);
                if (lit) HudKit.Dot(cell, DotPos[i], 60f, new Color(1f, 0.92f, 0.7f, 0.18f), "Glow");
                var d = HudKit.Dot(cell, DotPos[i], 42f, lit ? HudKit.Cream : new Color(0.12f, 0.16f, 0.26f, 1f));
                d.sprite = SpriteLib.Disc(0.06f);
                var num = UI.Label(cell, (i + 1).ToString(), 10, lit ? HudKit.InkDark : HudKit.Dim, new Vector2(0.5f, 0.5f), DotPos[i] + new Vector2(0f, -1f), new Vector2(30f, 14f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
                num.fontStyle = FontStyle.Bold;
            }
            UI.Label(box, done ? "4라운드 완료 — 표시부가 꺼졌다" : "이 패턴이 무슨 숫자인지는 통역의 매뉴얼에만 있다", 12, HudKit.Ink, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(400f, 20f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));

            var chipRow = Row(left, 34f, 8f, false);
            var lbl = UI.Text(chipRow, "모양을 통역에게:", 12, HudKit.Dim, TextAnchor.MiddleLeft, false);
            UI.LE(lbl, 100, 34, 0f);
            if (!done)
            {
                // D4: 점 번호 칩 → 채팅 초안 / [통역에게 말하기] → 구조화 Chat(a=2, b=점 비트마스크). 통역은 원문을 읽는다
                string draft = Comms.BrailleChatText(digit);
                int mask = 0;
                foreach (int d in dots) mask |= 1 << (d - 1);
                var chip = HudKit.Btn(chipRow, draft, HudKit.Amber, () => ChatDraft?.Invoke(draft), 14, 32, 120, true);
                UI.LE(chip, 120, 32, 0f);
                var say = HudKit.Btn(chipRow, "통역에게 말하기", HudKit.Teal, () => Session.Send("Chat", 2, mask, draft), 13, 32, 130, true);
                UI.LE(say, 130, 32, 0f);
                var tip = UI.Text(chipRow, "<size=11>통역만 읽는다</size>", 11, HudKit.Dim, TextAnchor.MiddleLeft, false);
                UI.LE(tip, 60, 34, 1f);
            }
            Note(left, "램프 색은 관측자, 변환표는 통역. <b>방향 카드를 받아야</b> 레버를 당길 수 있다. 카드가 오면 그 방향이 미리 골라진다 — 바꿀 수도 있다.", 44f);

            var right = Right();
            var rbox = DarkBox(right, 56f);
            UI.Label(rbox, "라운드", 12, HudKit.Dim, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(80f, 20f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
            var rt = UI.Label(rbox, $"{Mathf.Min(st.round + 1, 4)} / 4", 26, HudKit.Amber, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(120f, 40f), TextAnchor.MiddleRight, new Vector2(1f, 0.5f));
            rt.fontStyle = FontStyle.Bold;

            var cross = UI.Rect(right, "Cross");
            UI.Grid(cross, new Vector2(88f, 58f), new Vector2(6f, 6f), 3);
            UI.LE(cross, -1, 58 * 3 + 12);
            var dirBtns = new Button[4];
            // 배치: [ ][▲][ ] / [◀][레버][▶] / [ ][▼][ ]
            UI.Rect(cross, "Pad"); dirBtns[0] = DirBtn(cross, 0); UI.Rect(cross, "Pad");
            dirBtns[2] = DirBtn(cross, 2);
            var center = HudKit.Block(cross, "Lever", HudKit.Dark, -1, HudKit.Border);
            HudKit.Fill(center, "레버", 12, HudKit.Dim, TextAnchor.MiddleCenter, 0f);
            dirBtns[3] = DirBtn(cross, 3);
            UI.Rect(cross, "Pad"); dirBtns[1] = DirBtn(cross, 1); UI.Rect(cross, "Pad");

            // D6(a): 방향 버튼은 "선택", [레버 당기기]가 확정. 방향 카드가 오면 그 방향이 미리 선택된다
            var pull = HudKit.Btn(right, "레버 당기기", HudKit.Amber, () =>
            {
                if (_selLever < 0) { if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("방향을 먼저 고르세요 — 통역의 방향 카드를 기다려요", GameLogic.ToneWarn); return; }
                Session.Send("Lever", _selLever);
            }, 15, 40, 200, true);
            var cardBox = HudKit.Block(right, "Card", HudKit.Slate, 48f, HudKit.Border);
            var cardText = HudKit.Fill(cardBox, "", 13, HudKit.Ink, TextAnchor.MiddleLeft, 10f);
            _selLever = st.dirCard >= 0 && st.round < 4 ? st.dirCard : -1;
            _seenDirCard = st.dirCard;

            _refresh = () =>
            {
                var s = Session.St;
                bool has = s.dirCard >= 0 && s.round < 4;
                if (s.dirCard != _seenDirCard) { _seenDirCard = s.dirCard; if (has) _selLever = s.dirCard; }
                if (s.round >= 4) _selLever = -1;
                for (int d = 0; d < 4; d++)
                {
                    bool sel = _selLever == d;
                    UI.SetButtonColor(dirBtns[d], sel ? HudKit.Amber : HudKit.Slate);
                    UI.ButtonLabel(dirBtns[d]).color = sel ? HudKit.InkDark : Color.white;
                    dirBtns[d].interactable = s.round < 4;
                }
                pull.interactable = s.round < 4 && has;
                UI.ButtonLabel(pull).text = _selLever >= 0 ? $"레버 당기기  {Comms.DirGlyph(_selLever)}" : "레버 당기기";
                cardText.text = s.round >= 4
                    ? "<color=#7FE39B>4라운드 완료</color> — 내비게이터의 배전함 판독을 기다린다"
                    : (has ? $"통역이 보낸 방향 카드:  <b><size=20>{Comms.DirGlyph(s.dirCard)}</size></b>  <size=11><color=#8A96A8>미리 골라 뒀어요 — 확인하고 [레버 당기기]</color></size>"
                           : "통역이 보낸 방향 카드: <color=#8A96A8>대기 중</color>");
            };
        }

        private Button DirBtn(Transform parent, int d)
        {
            int v = d;
            return HudKit.Btn(parent, Comms.DirGlyph(d), HudKit.Slate, () => { _selLever = v; _refresh?.Invoke(); }, 22, 58, 88);
        }

        // ---------------------------------------------------------------- 관문 2 · 벽화 매뉴얼(통역)

        /// <summary>작은 점자 그림(6점).</summary>
        private static void MiniBraille(RectTransform parent, int digit, Vector2 center, float dot, Color on, Color off)
        {
            var dots = Comms.BrailleDots(digit);
            float gap = dot * 1.6f;
            for (int i = 0; i < 6; i++)
            {
                bool lit = false; foreach (var d in dots) if (d == i + 1) { lit = true; break; }
                int col = i / 3, row = i % 3;
                var pos = center + new Vector2((col - 0.5f) * gap, (1 - row) * gap);
                var img = HudKit.Dot(parent, pos, dot, lit ? on : off, "b");
                img.sprite = SpriteLib.Disc(0.1f);
            }
        }

        private void BuildMural()
        {
            var left = Left();
            var paper = HudKit.Block(left, "Paper", HudKit.Cream, 352f);
            UI.VStack(paper, 14, 6, false, TextAnchor.UpperLeft);
            UI.Text(paper, "<b>방향 모듈 안내</b>", 20, HudKit.InkDark, TextAnchor.MiddleLeft, false, 26);
            UI.Text(paper, "가운데의 점자 숫자와 램프 색에 따라 올바른 방향을 조작자에게 알려라.", 12, HudKit.InkDark, TextAnchor.UpperLeft, true, 18);
            var grid = UI.Rect(paper, "Table");
            UI.Grid(grid, new Vector2(87f, 48f), new Vector2(2f, 4f), 5);   // C1: 셀 84 → 87(5×87 + 4×2 = 443 ≤ 종이 안폭 446, 오른쪽 열 260)
            UI.LE(grid, -1, 48 * 5 + 16);
            HudKit.Cell(grid, "점자", HudKit.Tan, HudKit.InkDark, 13, FontStyle.Bold, HudKit.InkDark);
            for (int c = 0; c < 4; c++) HudKit.Cell(grid, Comms.ColorName(c), Comms.ColorValue(c), c == 1 ? HudKit.InkDark : Color.white, 13, FontStyle.Bold, HudKit.InkDark);
            for (int r = 0; r < 4; r++)
            {
                var digits = Comms.Table2Digits[r];
                var head = HudKit.Cell(grid, "", HudKit.Tan, HudKit.InkDark, 12, FontStyle.Bold, HudKit.InkDark);
                var hrt = (RectTransform)head.transform.parent;
                // 위 왼쪽: 점자 그림 두 개 / 위 오른쪽: 숫자 쌍 / 아래 전체 폭: 점 번호(C1: 8px 는 읽을 수 없었다 → 12px, 셀 폭 전체 83px 사용)
                MiniBraille(hrt, digits[0], new Vector2(-30f, 11f), 6f, HudKit.InkDark, new Color(0.75f, 0.70f, 0.58f));
                MiniBraille(hrt, digits[1], new Vector2(-12f, 11f), 6f, HudKit.InkDark, new Color(0.75f, 0.70f, 0.58f));
                head.text = $"<b>{digits[0]} · {digits[1]}</b>";
                head.alignment = TextAnchor.MiddleRight;
                var trt = (RectTransform)head.transform; trt.offsetMin = new Vector2(40f, 22f); trt.offsetMax = new Vector2(-4f, -2f);
                var dotsLbl = UI.Label(hrt, $"{Dots(digits[0])} / {Dots(digits[1])}", 12, HudKit.InkDark, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(85f, 18f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
                dotsLbl.horizontalOverflow = HorizontalWrapMode.Overflow;   // 가장 긴 "1,2 / 1,2,4,5"(≈80px)가 줄바꿈되지 않게
                for (int c = 0; c < 4; c++) HudKit.Cell(grid, Comms.DirGlyph(Comms.Table2[r, c]), Color.white, HudKit.InkDark, 22, FontStyle.Bold, HudKit.InkDark);
            }
            UI.Text(paper, "점 번호: 왼쪽 줄 위→아래 1·2·3, 오른쪽 줄 4·5·6. 조작자가 채팅으로 \"점자 1,2,4\"처럼 보낸다.", 12, new Color(0.35f, 0.32f, 0.28f), TextAnchor.UpperLeft, true, 30);

            var right = Right(260f);   // C1: 표 열을 넓히려 오른쪽 열 280 → 260(방향 버튼 4×60 + 18 = 258)
            var info = HudKit.Block(right, "Info", HudKit.Slate, 104f, HudKit.Border);   // P-A2: 3줄이 70 에서 넘쳤다 → 92, A2: 채팅 원문 줄이 접힐 수 있어 104
            var infoText = HudKit.Fill(info, "", 13, HudKit.Ink, TextAnchor.MiddleLeft, 10f);
            var pick = HudKit.Block(right, "Pick", HudKit.Dark, 44f, HudKit.Border);
            var pickText = HudKit.Fill(pick, "", 13, HudKit.Ink, TextAnchor.MiddleLeft, 10f);
            var drow = Row(right, 44f, 6f, true);
            var dirBtns = new Button[4];
            for (int d = 0; d < 4; d++)
            {
                int v = d;
                dirBtns[d] = HudKit.Btn(drow, Comms.DirGlyph(d), HudKit.Slate, () => { _selDir = _selDir == v ? -1 : v; _refresh?.Invoke(); }, 20, 44, 60);
            }
            HudKit.Btn(right, "방향 카드 전송", HudKit.Teal, () =>
            {
                if (_selDir < 0) { if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("방향을 먼저 고르세요", GameLogic.ToneWarn); return; }
                Session.Send("DirCard", _selDir);
            }, 15, 42, 200, true);
            var status = UI.Text(right, "", 12, HudKit.Dim, TextAnchor.UpperLeft, true, 30);

            _refresh = () =>
            {
                var s = Session.St;
                string lamp = s.colorReport >= 0
                    ? $"<color=#{HudKit.H(Comms.ColorValue(s.colorReport))}>■</color> 램프: <b>{Comms.ColorName(s.colorReport)}</b> (관측자)"
                    : "<color=#3A4A66>□</color> 램프: <color=#8A96A8>미확인 (이번 라운드 관측자 신고를 기다린다)</color>";
                infoText.text = $"<size=11><color=#8A96A8>지금 받은 정보 · 라운드 {Mathf.Min(s.round + 1, 4)}/4</color></size>\n{lamp}\n{OpChatLine("점자", "조작자 채팅에서 (점 번호 → 표의 그림)")}";
                pickText.text = _selDir >= 0 ? $"보낼 방향 카드  <b><size=20>{Comms.DirGlyph(_selDir)}</size></b>" : "보낼 방향 카드  <color=#8A96A8>—</color>";
                for (int d = 0; d < 4; d++)
                {
                    UI.SetButtonColor(dirBtns[d], _selDir == d ? HudKit.Teal : HudKit.Slate);
                    UI.ButtonLabel(dirBtns[d]).color = _selDir == d ? HudKit.InkDark : Color.white;
                }
                status.text = s.round >= 4 ? "4라운드 완료 — 내비게이터의 판독을 기다린다."
                    : (s.dirCard >= 0 ? $"보낸 카드: <color=#F2C572>{Comms.DirGlyph(s.dirCard)}</color> — 조작자가 레버를 당기면 다음 라운드." : "");
            };
            _tick = TickRefreshSlow;
        }

        private static string Dots(int digit)
        {
            var d = Comms.BrailleDots(digit);
            return d.Length == 0 ? "?" : string.Join(",", System.Array.ConvertAll(d, x => x.ToString()));
        }

        // ---------------------------------------------------------------- 관문 2 · 색상등(관측자)

        private void BuildColorLamp()
        {
            var left = Left();
            var box = DarkBox(left, 300f);
            var head = UI.Label(box, "", 12, HudKit.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(300f, 18f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f));
            var lamp = Lamp(box, 130f, new Vector2(0f, 14f));
            UI.Label(box, "라운드가 바뀌면(색이 같아도) 다시 신고해야 한다.", 13, HudKit.Ink, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(400f, 24f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));   // B1

            var right = Right();
            var btns = new List<Button>();
            for (int c = 0; c < 4; c++)
            {
                int v = c;
                btns.Add(HudKit.Btn(right, $"{Comms.ColorName(c)} 신고", Comms.ColorValue(c), () => Session.Send("Color", v), 16, 48, 200, c == 1));
            }
            var memo = Note(right, "", 56f);

            _refresh = () =>
            {
                var s = Session.St;
                bool off = s.round >= 4;
                int r = Mathf.Clamp(s.round, 0, 3);
                int col = !off && s.lampColor2 != null && s.lampColor2.Length > r ? s.lampColor2[r] : -1;
                head.text = off ? "색상등 · 꺼짐" : $"색상등 · 라운드 {r + 1}";
                lamp.color = off ? new Color(0.12f, 0.14f, 0.18f, 1f) : Comms.ColorValue(col);
                for (int i = 0; i < btns.Count; i++)
                {
                    btns[i].interactable = !off;
                    UI.ButtonLabel(btns[i]).text = (!off && s.colorReport == i) ? $"{Comms.ColorName(i)} 신고  ✓" : $"{Comms.ColorName(i)} 신고";
                }
                memo.text = _colorHistory.Count == 0
                    ? "<color=#8A96A8>라운드별로 내가 신고한 색이 여기에 쌓인다 (1R 빨강 · 2R 초록 …)</color>"
                    : "<size=11><color=#8A96A8>내 신고 기록</color></size>\n" + string.Join(" · ", _colorHistory.ToArray());
            };
        }

        // ---------------------------------------------------------------- 관문 2 · 배전함(내비게이터)

        private void BuildJunction()
        {
            var left = Left();
            var box = DarkBox(left, 250f);
            UI.Label(box, "배전함 · 4라운드 결과 누적", 12, HudKit.Dim, new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(300f, 16f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
            var diagram = UI.Fixed(box, "Diagram", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(440f, 200f));
            var cardsRow = Row(left, 66f, 6f, true);
            var right = Right();
            var readBox = HudKit.Block(right, "Read", HudKit.Slate, 90f, HudKit.Border);
            var readText = HudKit.Fill(readBox, "", 13, HudKit.Ink, TextAnchor.MiddleLeft, 10f);
            var safeBtn = HudKit.Btn(right, "안전 — 셔터 개방", HudKit.Green, () => Session.Send("Judge", 1), 15, 46, 200, true);
            HudKit.Btn(right, "위험 — 처음부터", HudKit.Red, () => Session.Send("Judge", 0), 15, 46, 200, false);
            Note(right, $"'안전'이 틀리면 −{Stage1Logic.Gate2Penalty:0}초를 잃고 라운드를 처음부터 다시 해요. '위험'은 시간을 잃지 않고 처음부터. 도착한 라인 번호가 현황판의 <b>안전 라인 번호</b>와 같은지 비교하자.", 70f);   // 4줄(13px)이 여유 있게 들어가는 높이

            float X(int t) => -196f + t * 56f;
            float[] rowY = { 46f, 12f, -22f, -56f };
            const float topY = 78f, bottomY = -76f;
            string lastDrawn = "";

            _refresh = () =>
            {
                var s = Session.St;
                string key = $"{s.round}_{s.gate2Attempts}_{s.boardOpened}_{s.seed}";
                if (key != lastDrawn)
                {
                    lastDrawn = key;
                    HudKit.Clear(diagram);
                    // 단자 8개: 번호 1~8만(D2). 현황판을 봤으면 안전 라인 아래에 ○
                    int safeLine = Stage1Logic.SafeLine(s);
                    for (int t = 0; t < 8; t++)
                    {
                        HudKit.Line(diagram, new Vector2(X(t), topY), new Vector2(X(t), bottomY), 1f, new Color(0.16f, 0.22f, 0.32f, 1f), "Col");
                        var n = UI.Label(diagram, (t + 1).ToString(), 12, HudKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(X(t), topY + 12f), new Vector2(40f, 16f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
                        n.fontStyle = FontStyle.Bold;
                        string foot = !s.boardOpened ? "?" : (t + 1 == safeLine ? "○" : "×");
                        var fc = !s.boardOpened ? HudKit.Dim : (t + 1 == safeLine ? HudKit.MonoGreen : new Color(0.6f, 0.3f, 0.32f));
                        var f = UI.Label(diagram, $"{t + 1}번\n<size=13>{foot}</size>", 9, fc, new Vector2(0.5f, 0.5f), new Vector2(X(t), bottomY - 18f), new Vector2(54f, 34f), TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f));
                        f.fontStyle = FontStyle.Bold;
                    }
                    var w = UI.Fixed(diagram, "Wires", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 200f));
                    // 시작 단자
                    int p = Mathf.Clamp(s.ladderStart, 0, 7);
                    HudKit.Dot(w, new Vector2(X(p), topY), 10f, HudKit.Amber, "Start");
                    float y = topY;
                    int drawn = Mathf.Min(s.round, 4);
                    var cards = new List<string>();
                    for (int r = 0; r < drawn; r++)
                    {
                        int d = s.chosenDir != null && s.chosenDir.Length > r ? s.chosenDir[r] : -1;
                        if (d < 0) break;
                        int idx = r * 32 + p * 4 + d;
                        int next = s.ladderNext != null && s.ladderNext.Length > idx ? Mathf.Clamp(s.ladderNext[idx], 0, 7) : p;
                        // C2: 배선·카드는 앰버 단색 — 램프 색은 내비가 알 수 없는 정보다(관측자 → 통역만). 카드엔 방향만
                        var c = HudKit.Amber;
                        HudKit.Line(w, new Vector2(X(p), y), new Vector2(X(p), rowY[r]), 4f, c, "V");
                        HudKit.Line(w, new Vector2(X(p), rowY[r]), new Vector2(X(next), rowY[r]), 4f, c, "H");
                        HudKit.Dot(w, new Vector2(X(next), rowY[r]), 8f, c, "Node");
                        cards.Add($"<b>{r + 1}R</b>  <size=18>{Comms.DirGlyph(d)}</size>\n<size=11>{p + 1} → {next + 1}</size>");
                        p = next;
                        y = rowY[r];
                    }
                    if (drawn >= 4 && cards.Count == 4)
                    {
                        HudKit.Line(w, new Vector2(X(p), y), new Vector2(X(p), bottomY), 4f, HudKit.Amber, "End");
                        HudKit.Dot(w, new Vector2(X(p), bottomY), 12f, HudKit.Amber, "EndDot");
                    }
                    HudKit.Clear(cardsRow);
                    for (int r = 0; r < 4; r++)
                    {
                        var cb = HudKit.Block(cardsRow, "R" + r, HudKit.Slate, 62f, HudKit.Border);
                        HudKit.Fill(cb, r < cards.Count ? cards[r] : $"<color=#8A96A8>{r + 1}R\n<size=11>대기</size></color>", 13, HudKit.Ink, TextAnchor.MiddleCenter, 4f);
                    }
                }
                int end = s.round >= 4 ? Stage1Logic.PathEnd(s) : -1;
                string safeText = s.boardOpened
                    ? $"안전 라인: <b><color=#7FE39B>{Stage1Logic.SafeLine(s)}번</color></b> <size=11><color=#8A96A8>(현황판에서 확인)</color></size>"
                    : "안전 라인: <color=#E5484D>미확인</color> <size=11><color=#8A96A8>— 침수 현황판을 보고 오세요</color></size>";
                if (s.round >= 4 && end >= 0)
                {
                    readText.text = $"<size=11><color=#F2C572>경로 판독</color></size>\n<b>{end + 1}번 단자</b>로 이어진다.\n{safeText}";
                    safeBtn.interactable = true;
                }
                else
                {
                    readText.text = $"<size=11><color=#8A96A8>경로 판독</color></size>\n라운드 {Mathf.Min(s.round + 1, 4)}/4 진행 중 — 4라운드가 끝나면 몇 번 단자로 이어지는지 읽을 수 있다.\n{safeText}";
                    safeBtn.interactable = false;
                }
            };
        }
    }
}
