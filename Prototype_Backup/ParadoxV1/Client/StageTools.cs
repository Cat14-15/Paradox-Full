using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 왼쪽 도구 패널 — 역할·스테이지·단계에 따라 내용이 바뀐다.
    /// 글자 카드(현지인), 릴레이 상자(내비), 레버(조작자), 경고등(관측자), 콘솔/물살/동기화(Stage 2),
    /// 공용 보드/감지기/고글(Stage 3), 방송/장치/키패드(Stage 4).
    /// </summary>
    public class StageTools
    {
        private RectTransform _panel;

        private Image _meterFill; private Text _meterText;
        private Image _lampImg; private Text _lampText;
        private Image _currentFill; private Text _currentText;
        private Text _waveText, _holdText, _syncText, _detectorText, _broadcastText, _zoneText, _keyText;
        private Image _barrierFill;
        private readonly List<Button> _modeBtns = new List<Button>();
        private float _jiggle;
        private bool _keypadMode;

        private static readonly Color Green = new Color(0.4f, 0.9f, 0.5f);
        private static readonly Color Blue = new Color(0.5f, 0.7f, 1f);
        private static readonly Color Red = new Color(1f, 0.45f, 0.45f);

        public bool KeypadMode => _keypadMode;

        public void Build(RectTransform parent)
        {
            _panel = UI.Panel(parent, "Tools", UI.PanelDark, new Vector2(0f, 1f), new Vector2(16f, -188f), new Vector2(440f, 386f));
            UI.VStack(_panel, 12, 6, false, TextAnchor.UpperLeft);
            UI.Fit(_panel, false, true); // 내용이 길면 배경도 아래로 늘어난다(글자 잘림 방지)
            Rebuild();
        }

        public void Dispose()
        {
            if (_panel != null) Object.Destroy(_panel.gameObject);
            _panel = null;
        }

        public void Rebuild()
        {
            if (_panel == null) return;
            UI.Clear(_panel);
            _meterFill = null; _meterText = null; _lampImg = null; _lampText = null; _currentFill = null; _currentText = null;
            _waveText = _holdText = _syncText = _detectorText = _broadcastText = _zoneText = _keyText = null;
            _barrierFill = null; _modeBtns.Clear();
            _relayText = null; _relayBtn = null; _leverConfirm = null; _leverHint = null; _leverBtns.Clear(); _leverPick = -1;
            _sendBtn = null; _letterStatus = null; _keyText = null;
            _keypadMode = false;

            var st = Session.St;
            int me = Session.LocalSlot;
            const int Op = 0, In = 1, Na = 2, Ob = 3;

            if (st.failed || st.cleared) { _panel.gameObject.SetActive(false); return; }
            _panel.gameObject.SetActive(true);

            switch (st.stage)
            {
                case 1:
                    if (st.step == 0)
                    {
                        if (me == In) LetterPanel("Pick", $"표지판 단어: <b><color=#FFE9A0>{st.word}</color></b> — 한 글자씩 내비게이터에게", true);
                        else if (me == Na) RelayPanel();
                        else if (me == Op) { ReceivedPanel(Op); LeverPanel(); }
                        else if (me == Ob) LampPanel();
                    }
                    else if (st.step == 2) Title("이동 중…", "네 명이 함께 다음 지점으로 이동합니다.");
                    else if (st.step == 3) MashPanel();
                    else if (st.step == 4) ZonePanel("로비 도착", "각자 자기 색 원 안으로. 네 명이 동시에!");
                    break;

                case 2:
                    if (st.step == 0)
                    {
                        if (me == Na) LetterPanel("Pick", $"표지판: 펌프 모드 <b><color=#FFE9A0>{st.word}</color></b> — 조작자에게 직접 전달", true);
                        else if (me == Op) { ReceivedPanel(Op); ConsolePanel(); }
                        else if (me == In) { CurrentPanel(); SyncPanel(); }
                        else if (me == Ob) SyncPanel();
                    }
                    else ZonePanel("상부 통로로", me == Na ? "미니맵의 파란 갈래는 침수 중! 신호로 안내하세요." : "내비게이터의 신호를 따라 물이 안 차는 통로로.");
                    break;

                case 3:
                    BoardPanel();
                    if (me == In) DetectorPanel();
                    else if (me == Ob) GogglesPanel();
                    else if (me == Na) Note("미니맵(왼쪽 아래)의 <color=#FFB0A0>붕괴</color> 구역은 막다른 길. 구조가 안전한 구역을 보드에 ✓ 하세요.");
                    else Note("친구들의 표시가 ✓✓이고 ✗가 없는 구역이 정답. 그곳의 원 4개에 모두 들어가고, 관측자·내비게이터가 확인 패널을 누르면 문이 열립니다.");
                    break;

                case 4:
                    BroadcastPanel();
                    if (me == Op) LetterPanel("Pick", $"탈출 암호 <b><color=#FFE9A0>{st.word}</color></b> — 세 친구 모두에게 전송", true);
                    else
                    {
                        var pos = Session.PosOf(me);
                        bool nearPad = pos != null && StageInteractions.NearKeypad(me, pos.Value) && !st.codeDone;
                        if (nearPad) { _keypadMode = true; KeypadPanel(); }
                        else
                        {
                            ReceivedPanel(me);
                            if (me == Ob) Note("<color=#FFB0A0>고글 무력화</color> — 연기로 색상 감지가 안 됩니다. 조작자의 신호에 의지하세요.");
                        }
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- 공용 조각

        private void Title(string title, string desc)
        {
            UI.Text(_panel, $"<b>{title}</b>", 18, UI.Gold, TextAnchor.MiddleLeft, false, 26);
            if (!string.IsNullOrEmpty(desc)) UI.Text(_panel, desc, 14, UI.InkDim, TextAnchor.UpperLeft, true, 22);
        }

        private void Note(string text)
        {
            UI.Text(_panel, text, 14, UI.Ink, TextAnchor.UpperLeft, true, 44);
        }

        private Button _sendBtn;
        private Text _letterStatus;

        private void LetterPanel(string cmdType, string hint, bool showPicked)
        {
            var st = Session.St;
            bool keypad = cmdType == "Key";
            Title(keypad ? "키패드" : "글자 카드", hint);
            var grid = UI.Rect(_panel, "Grid");
            UI.Grid(grid, new Vector2(64f, 38f), new Vector2(5f, 5f), 6);
            UI.LE(grid, -1, 5 * 38 + 4 * 5);
            foreach (var syl in StageDefs.Syllables)
            {
                string s = syl;
                UI.Button(grid, s, UI.BtnColor, () => Session.Send(cmdType, 0, 0, s), 19, 38, 64);
            }
            var row = UI.Rect(_panel, "Row");
            UI.HStack(row, 0, 8, true);
            UI.LE(row, -1, 40);
            string picked = keypad ? st.keypad[Session.LocalSlot] : st.picked;
            _keyText = UI.Text(row, keypad ? $"입력: <b>{Spaced(picked)}</b>" : $"고른 글자: <b>{Spaced(picked)}</b>", 17, UI.Gold, TextAnchor.MiddleLeft, false);
            UI.LE(_keyText, -1, 40, 1f);
            UI.Button(row, "지우기", UI.BtnWarn, () => Session.Send(keypad ? "KeyClear" : "ClearLetters"), 15, 36, 90);

            if (!keypad)
            {
                string who = ReceiverName();
                _sendBtn = UI.Button(_panel, $"{who}에게 보내기 ▶", UI.BtnGood, () => Session.Send("SendLetters"), 19, 50);
                _sendBtn.interactable = st.picked.Length > 0 && !st.signalCut;
                _letterStatus = UI.Text(_panel, "", 13, UI.InkDim, TextAnchor.UpperLeft, true, 40);
            }
        }

        private static string ReceiverName()
        {
            switch (Session.Stage)
            {
                case 1: return "내비게이터";
                case 2: return "조작자";
                case 4: return "친구 셋";
                default: return "상대";
            }
        }

        /// <summary>발신자가 보는 글자 상태: 고른 글자 → 날아가는 중 → (중계 대기) → 도착.</summary>
        private void RefreshLetterStatus()
        {
            var st = Session.St;
            if (_keyText != null && !_keypadMode) _keyText.text = $"고른 글자: <b>{Spaced(st.picked)}</b>";
            if (_sendBtn != null) _sendBtn.interactable = st.picked.Length > 0 && !st.signalCut;
            if (_letterStatus == null) return;

            float clock = Session.Clock;
            var sb = new System.Text.StringBuilder();
            if (st.stage == 4)
            {
                foreach (var m in st.inflight)
                    sb.Append($"[{m.ch}] → {Roles.Short(m.to)} <color=#C8D0E0>{(clock < m.arriveClock ? "날아가는 중" : "도착")}</color>   ");
                bool any = false;
                for (int s = 1; s <= 3; s++) if (st.received[s].Length > 0) { any = true; break; }
                if (any) sb.Append($"\n도착: 통역 {Spaced(st.received[1])} · 내비 {Spaced(st.received[2])} · 관측 {Spaced(st.received[3])}");
            }
            else
            {
                foreach (var m in st.inflight)
                {
                    string state = m.hop == 0
                        ? (clock < m.arriveClock ? "내비게이터에게 날아가는 중 ✈" : "<color=#FFE9A0>내비게이터 대기 중 — [그대로 전달]을 기다립니다</color>")
                        : "조작자에게 날아가는 중 ✈";
                    sb.Append($"[{m.ch}] {state}\n");
                }
                if (st.received[0].Length > 0) sb.Append($"<color=#66E68A>조작자 도착 ✓ {Spaced(st.received[0])}</color>");
            }
            string text = sb.ToString().TrimEnd();
            if (string.IsNullOrEmpty(text)) text = st.signalCut ? "<color=#80B3FF>신호 차단 중 — 잠시 후 보낼 수 있어요</color>" : "글자를 골라 [보내기]를 누르면 상대에게 날아갑니다.";
            if (_letterStatus.text != text) _letterStatus.text = text;
        }

        private static string Spaced(string s)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            var sb = new System.Text.StringBuilder();
            foreach (var c in s) { if (sb.Length > 0) sb.Append(' '); sb.Append('[').Append(c).Append(']'); }
            return sb.ToString();
        }

        private void ReceivedPanel(int slot)
        {
            var st = Session.St;
            string letters = st.received[slot];
            Title("도착한 글자", st.stage == 4 && slot != 0 ? "조작자가 보낸 글자를 같은 모양으로 키패드에 입력" : "릴레이로 도착한 글자를 모아 단어를 유추하세요");
            var box = UI.Rect(_panel, "Letters");
            UI.Bg(box, new Color(0.12f, 0.13f, 0.18f, 1f), false);
            UI.LE(box, -1, 64);
            var t = UI.Text(box, string.IsNullOrEmpty(letters) ? "<color=#8892A0>(아직 없음)</color>" : Spaced(letters), 30, UI.Gold, TextAnchor.MiddleCenter, false);
            var trt = (RectTransform)t.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }

        private void RelayPanel()
        {
            var st = Session.St;
            Title("릴레이 중계", "통역이 보낸 글자가 도착하면 [그대로 전달]로 조작자에게 넘기세요.");
            var box = UI.Rect(_panel, "Queue");
            UI.Bg(box, new Color(0.12f, 0.13f, 0.18f, 1f), false);
            UI.LE(box, -1, 64);
            _relayText = UI.Text(box, "", 24, UI.Gold, TextAnchor.MiddleCenter, false);
            var trt = (RectTransform)_relayText.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            _relayBtn = UI.Button(_panel, "그대로 전달 ▶", UI.BtnAccent, () => Session.Send("Relay"), 19, 52);
            _relayBtn.interactable = false;
            UI.Text(_panel, "뜻은 몰라도 괜찮아요. 모양 그대로, 순서대로! 전달한 글자는 조작자에게 날아갑니다.", 13, UI.InkDim, TextAnchor.UpperLeft, true, 36);
            RefreshRelay();
        }

        private Text _relayText;
        private Button _relayBtn;

        private void RefreshRelay()
        {
            if (_relayText == null || _relayBtn == null) return;
            var st = Session.St;
            float clock = Session.Clock;
            var ready = new List<string>();
            var incoming = new List<string>();
            var outgoing = new List<string>();
            foreach (var m in st.inflight)
            {
                if (m.hop == 0) { if (clock >= m.arriveClock) ready.Add(m.ch); else incoming.Add(m.ch); }
                else outgoing.Add(m.ch);
            }
            string line;
            if (ready.Count > 0) line = "도착: " + Spaced(string.Join("", ready));
            else if (incoming.Count > 0) line = $"<color=#C8D0E0>글자가 날아오는 중… ({incoming.Count})</color>";
            else if (outgoing.Count > 0) line = "<color=#8FD0A0>조작자에게 전달 중… ▶</color>";
            else line = "<color=#8892A0>신호 대기 중…</color>";
            _relayText.text = line;
            bool can = ready.Count > 0 && !st.signalCut;
            _relayBtn.interactable = can;
            UI.ButtonLabel(_relayBtn).text = can ? $"[{ready[0]}] 그대로 전달 ▶" : (st.signalCut ? "신호 차단 중…" : "전달할 글자 없음");
        }

        private int _leverPick = -1;
        private readonly List<Button> _leverBtns = new List<Button>();
        private Button _leverConfirm;
        private Text _leverHint;

        private void LeverPanel()
        {
            var st = Session.St;
            Title("레버 선택 (사다리 갈림길)", "도착한 단어와 같은 라벨의 레버를 고른 뒤 [확정]. 월드의 레버 앞에서 E로 당겨도 됩니다.");
            var row = UI.Rect(_panel, "Levers");
            UI.HStack(row, 0, 10, true);
            UI.LE(row, -1, 64);
            _leverBtns.Clear();
            _leverPick = -1;
            for (int side = 0; side < 2; side++)
            {
                int s = side;
                string label = Stage1Logic.LeverLabel(st, st.fork, side);
                var b = UI.Button(row, side == 0 ? $"◀  {label}" : $"{label}  ▶", UI.BtnColor, () => { _leverPick = s; RefreshLever(); }, 22, 60);
                _leverBtns.Add(b);
            }
            _leverConfirm = UI.Button(_panel, "확정", UI.BtnGood, () => { if (_leverPick >= 0) Session.Send("Lever", _leverPick); }, 18, 46);
            _leverHint = UI.Text(_panel, "", 13, UI.InkDim, TextAnchor.UpperLeft, true, 36);
            RefreshLever();
        }

        private void RefreshLever()
        {
            if (_leverConfirm == null) return;
            var st = Session.St;
            bool can = Stage1Logic.CanPullLever(st);
            for (int i = 0; i < _leverBtns.Count; i++)
            {
                _leverBtns[i].interactable = can;
                UI.SetButtonColor(_leverBtns[i], _leverPick == i ? UI.BtnAccent : UI.BtnColor);
            }
            _leverConfirm.interactable = can && _leverPick >= 0;
            UI.ButtonLabel(_leverConfirm).text = _leverPick >= 0 ? $"[{Stage1Logic.LeverLabel(st, st.fork, _leverPick)}] 레버 확정" : "레버를 먼저 고르세요";
            _leverHint.text = st.signalCut ? "<color=#80B3FF>신호 차단 중 — 잠시 후 다시</color>"
                : !can ? $"글자 {st.received[0].Length}/{Stage1Logic.WordLength} 도착 — 단어가 다 오면 레버가 켜집니다. 이상하면 아래 <b>?</b>(다시 요청)."
                : "단어와 <b>같은 라벨</b>을 고르세요. 글자가 이상하면 아래 <b>?</b>(다시 요청)으로 처음부터 다시 받을 수 있어요.";
        }

        private void LampPanel()
        {
            Title("정전 경고등", "붉게 최고조로 깜빡일 때 ⚠ 경고 버튼! 그 순간을 놓치면 전달 중인 글자가 사라집니다.");
            var holder = UI.Rect(_panel, "Lamp");
            UI.LE(holder, -1, 150);
            var img = UI.Rect(holder, "Disc");
            img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f);
            img.sizeDelta = new Vector2(130f, 130f);
            _lampImg = img.gameObject.AddComponent<Image>();
            _lampImg.sprite = SpriteLib.Disc(0.12f);
            _lampImg.raycastTarget = false;
            _lampText = UI.Text(_panel, "", 16, UI.Ink, TextAnchor.MiddleCenter, false, 26);
        }

        private void MashPanel()
        {
            Title("장애물 제거", "네 명 모두 SPACE 연타! 한 명이라도 멈추면 진행도가 빨리 떨어집니다.");
            _meterFill = UI.Bar(_panel, new Color(0.15f, 0.16f, 0.2f), new Color(0.4f, 0.9f, 0.5f), 26f);
            _meterText = UI.Text(_panel, "", 15, UI.Ink, TextAnchor.MiddleCenter, false, 22);
        }

        private void ZonePanel(string title, string desc)
        {
            Title(title, desc);
            _zoneText = UI.Text(_panel, "", 18, UI.Gold, TextAnchor.MiddleCenter, false, 30);
        }

        private void ConsolePanel()
        {
            var st = Session.St;
            Title("펌프 콘솔", "모드를 맞추고, 콘솔 앞에서 E로 붙잡아 방벽을 60 이상 유지하세요.");
            var row = UI.Rect(_panel, "Modes");
            UI.HStack(row, 0, 8, true);
            UI.LE(row, -1, 48);
            _modeBtns.Clear();
            for (int i = 0; i < StageDefs.ConsoleModes.Length; i++)
            {
                int m = i;
                bool sel = st.consoleMode == i;
                var b = UI.Button(row, StageDefs.ConsoleModes[i], sel ? UI.BtnAccent : UI.BtnColor, () => Session.Send("Console", m), 18, 46);
                _modeBtns.Add(b);
            }
            _holdText = UI.Text(_panel, "", 15, UI.Ink, TextAnchor.MiddleLeft, true, 24);
            _barrierFill = UI.Bar(_panel, new Color(0.15f, 0.16f, 0.2f), new Color(0.5f, 0.75f, 1f), 22f);
            _waveText = UI.Text(_panel, "", 15, new Color(1f, 0.8f, 0.5f), TextAnchor.MiddleLeft, true, 24);
        }

        private void CurrentPanel()
        {
            Title("물살 감지", "물살이 약할 때(초록) GO 신호를 보내고 함께 당기세요.");
            _currentFill = UI.Bar(_panel, new Color(0.15f, 0.16f, 0.2f), Blue, 26f);
            _currentText = UI.Text(_panel, "", 16, UI.Ink, TextAnchor.MiddleCenter, false, 26);
        }

        private void SyncPanel()
        {
            int me = Session.LocalSlot;
            Title(me == (int)GameRole.Observer ? "수문 A (동기화 대기)" : "수문 B", me == (int)GameRole.Observer ? "통역의 GO 신호가 오면 바로 핸들 앞에서 E!" : "물살이 약할 때 GO 신호 → 핸들 앞에서 E!");
            _syncText = UI.Text(_panel, "", 15, UI.Ink, TextAnchor.UpperLeft, true, 48);
        }

        private void BoardPanel()
        {
            var st = Session.St;
            int me = Session.LocalSlot;
            Title("공용 보드 — 안전 구역 표시", me == 3 ? "고글로 본 초록 구역에 ✓" : (me == 2 ? "구조가 안전한(붕괴 아님) 구역에 ✓" : (me == 1 ? "감지기 50 이상인 구역에 ✗" : "세 표시가 겹치는 곳을 찾으세요")));
            var grid = UI.Rect(_panel, "Board");
            UI.Grid(grid, new Vector2(98f, 54f), new Vector2(6f, 6f), 4);
            UI.LE(grid, -1, 3 * 54 + 2 * 6);
            // 위쪽 줄이 화면 위(북쪽)이 되도록 행을 뒤집어 그린다
            for (int row = StageMaps.Stage3Rows - 1; row >= 0; row--)
                for (int col = 0; col < StageMaps.Stage3Cols; col++)
                {
                    int idx = row * StageMaps.Stage3Cols + col;
                    bool start = idx == StageMaps.Stage3StartSector;
                    bool o = st.markObs[idx], n = st.markNav[idx], i = st.markInt[idx];
                    bool match = o && n && !i && !start;
                    string marks = (o ? "<color=#66E68A>✓</color>" : "<color=#444>·</color>") + (n ? "<color=#80B3FF>✓</color>" : "<color=#444>·</color>") + (i ? "<color=#FF7070>✗</color>" : "<color=#444>·</color>");
                    string label = start ? "출발" : $"{StageMaps.SectorName(idx)}\n{marks}";
                    Color bg = match ? new Color(0.45f, 0.38f, 0.12f) : (start ? new Color(0.2f, 0.2f, 0.24f) : UI.BtnColor);
                    int cell = idx;
                    var b = UI.Button(grid, label, bg, () => Session.Send("Mark", cell), 14, 54, 98);
                    b.interactable = !start && (me == 1 || me == 2 || me == 3);
                }
            UI.Text(_panel, "<color=#66E68A>✓</color> 관측(색)  <color=#80B3FF>✓</color> 내비(구조)  <color=#FF7070>✗</color> 통역(감지기)  ·  <color=#FFE9A0>금색</color> = 셋이 겹침", 12, UI.InkDim, TextAnchor.MiddleLeft, true, 20);
            _zoneText = UI.Text(_panel, "", 14, UI.Gold, TextAnchor.MiddleLeft, true, 22);
        }

        private void DetectorPanel()
        {
            Title("가스 감지기 (범위: 내 구역 + 이웃 구역)", "숫자는 언어와 상관없이 읽을 수 있어요. 50 이상 = 위험 → 보드에 ✗.");
            _detectorText = UI.Text(_panel, "", 15, UI.Ink, TextAnchor.UpperLeft, true, 60);
        }

        private void GogglesPanel()
        {
            Title("색상 고글", "바닥 색이 보입니다. 경고문: 초록만 안전, 노랑도 위험!");
            UI.Text(_panel, "<color=#66E68A>■ 초록</color> 안전   <color=#F2D94D>■ 노랑</color> 주의(위험)   <color=#F24D4D>■ 빨강</color> 위험", 14, UI.Ink, TextAnchor.MiddleLeft, true, 22);
        }

        private void BroadcastPanel()
        {
            var st = Session.St;
            int me = Session.LocalSlot;
            bool readable = me == (int)GameRole.Operator;
            Title("비상 안내 방송", readable ? "당신만 읽을 수 있습니다." : "알아들을 수 없는 방송이 흘러나온다…");
            var box = UI.Rect(_panel, "Broadcast");
            UI.Bg(box, new Color(0.12f, 0.13f, 0.18f, 1f), false);
            UI.LE(box, -1, 96);
            _broadcastText = UI.Text(box, readable ? st.broadcast : "", 14, readable ? UI.Ink : new Color(0.85f, 0.5f, 0.55f), TextAnchor.UpperLeft, true);
            var trt = (RectTransform)_broadcastText.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(8, 6); trt.offsetMax = new Vector2(-8, -6);
            string dev = "";
            for (int i = 1; i <= 3; i++)
                dev += $"<color=#{Roles.Hex(i)}>{Roles.Short(i)}</color> {Stage4Logic.DeviceNames[i]} {(st.device[i] ? "<color=#66E68A>✓</color>" : "<color=#888>…</color>")}   ";
            UI.Text(_panel, dev + (st.codeDone ? "\n<color=#66E68A>셔터 개방!</color>" : (st.devicesDone ? "\n<color=#FFE9A0>장치 완료 — 셔터 암호 입력</color>" : "")), 14, UI.Ink, TextAnchor.UpperLeft, true, 40);
        }

        private void KeypadPanel()
        {
            var st = Session.St;
            string got = st.received[Session.LocalSlot];
            string hint = string.IsNullOrEmpty(got)
                ? "<color=#FFB0A0>아직 받은 글자가 없어요</color> — 조작자가 암호를 보내면 여기에 표시됩니다"
                : $"받은 글자 <b><color=#FFE9A0>{Spaced(got)}</color></b> 와 <b>같은 모양</b>을 순서대로 — 세 명이 4초 안에 함께!";
            LetterPanel("Key", hint, false);
        }

        // ---------------------------------------------------------------- 매 프레임

        public void Tick()
        {
            if (_panel == null || !_panel.gameObject.activeSelf) return;
            var st = Session.St;
            float clock = Session.Clock;
            int me = Session.LocalSlot;
            _jiggle += Time.deltaTime;

            if (_meterFill != null)
            {
                _meterFill.fillAmount = Session.MeterA / 100f;
                _meterText.text = $"{Session.MeterA:0}%  —  SPACE 연타!";
            }
            if (_lampImg != null)
            {
                float ph = Stage1Logic.Phase01(st, clock);
                bool window = Stage1Logic.InGuardWindow(st, clock);
                bool transit = Stage1Logic.LettersInTransit(st);
                float k = Mathf.Pow(ph, 3f);
                Color c = Color.Lerp(new Color(0.3f, 0.3f, 0.22f), new Color(1f, 0.15f, 0.1f), k);
                if (window) c = Mathf.Sin(clock * 30f) > 0f ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.6f, 0.3f);
                if (st.signalCut) c = new Color(0.3f, 0.5f, 1f);
                _lampImg.color = c;
                bool guarded = st.guardedCycle == Stage1Logic.Cycle(st, clock);
                _lampText.text = st.signalCut ? "<color=#80B3FF>신호 차단 중 — 글자 보호됨</color>"
                    : guarded ? "<color=#66E68A>이번 파동은 막았어요 ✓</color>"
                    : window && transit ? "<color=#FF6060><b>지금! ⚠ 경고 — 글자가 전달 중!</b></color>"
                    : window ? "<color=#FFB060>최고조 (전달 중인 글자 없음)</color>"
                    : $"파동까지 {(1f - ph) * st.period:0.0}초" + (transit ? "  ·  <color=#FFE9A0>글자 전달 중</color>" : "");
            }
            RefreshRelay();
            RefreshLever();
            RefreshLetterStatus();
            if (_zoneText != null)
            {
                int n = 0;
                if (Session.Map != null)
                {
                    Vector2[] zones = Session.Map.zones;
                    if (st.stage == 3)
                    {
                        var mp = Session.PosOf(me);
                        int sec = mp != null ? Session.Map.SectorAt(mp.Value) : -1;
                        if (sec >= 0) { zones = new Vector2[4]; StageMaps.SectorZones(sec, zones); }
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Session.PosOf(i);
                        if (p != null && Vector2.Distance(p.Value, zones[i]) <= Session.Map.zoneRadius) n++;
                    }
                }
                string conf = st.stage == 3 ? $"   ·   확인: 관측 {(st.confirmObs ? "✓" : "…")} / 내비 {(st.confirmNav ? "✓" : "…")}" : "";
                _zoneText.text = $"원 안에 {n}/4명{conf}";
            }
            if (_barrierFill != null)
            {
                _barrierFill.fillAmount = Session.MeterA / 100f;
                _barrierFill.color = Session.MeterA >= Stage2Logic.BarrierNeeded ? Green : Blue;
                var opPos = Session.PosOf(me);
                bool nearConsole = opPos != null && Session.Map != null && Vector2.Distance(opPos.Value, Session.Map.Point("console")) <= 2.0f;
                _holdText.text = (nearConsole ? "" : "<color=#FFB0A0>콘솔(아래 가운데)로 걸어가세요 — 가까이 가야 조작할 수 있어요</color>\n") +
                    $"방벽 {Session.MeterA:0}/100 (60 이상 필요)   ·   {(st.holding ? "<color=#66E68A>붙잡는 중</color>" : "<color=#FFB0A0>놓은 상태 — 콘솔 앞에서 E</color>")}";
                float w = Stage2Logic.WavePhase(st, clock);
                _waveText.text = w >= Stage2Logic.WaveWarnAt ? $"<b>큰 물살 접근! {(Stage2Logic.WavePeriod - w):0.0}초 — E로 놓고 피하세요</b>" : $"다음 물살까지 {(Stage2Logic.WaveWarnAt - w):0}초";
                for (int i = 0; i < _modeBtns.Count; i++)
                {
                    UI.SetButtonColor(_modeBtns[i], st.consoleMode == i ? UI.BtnAccent : UI.BtnColor);
                    _modeBtns[i].interactable = nearConsole;
                }
            }
            if (_currentFill != null)
            {
                float cur = Stage2Logic.Current(clock);
                _currentFill.fillAmount = cur / 100f;
                bool safe = cur < Stage2Logic.CurrentSafe;
                _currentFill.color = safe ? Green : Red;
                _currentText.text = safe ? "<color=#66E68A>물살 약함 — 지금 GO!</color>" : $"<color=#FF7070>물살 강함 {cur:0}</color>";
            }
            if (_syncText != null)
            {
                int partner = me == (int)GameRole.Observer ? (int)GameRole.Interpreter : (int)GameRole.Observer;
                float mine = st.pullClock[me], theirs = st.pullClock[partner];
                string m = clock - mine < Stage2Logic.SyncWindow ? $"<color=#66E68A>내 당김 ✓ ({Stage2Logic.SyncWindow - (clock - mine):0.0}초 남음)</color>" : "내 당김: 대기";
                string t = clock - theirs < Stage2Logic.SyncWindow ? $"<color=#66E68A>{Roles.Name(partner)} 당김 ✓</color>" : $"{Roles.Name(partner)}: 대기";
                _syncText.text = $"{m}\n{t}\n실패 {st.gateFails}회";
            }
            if (_detectorText != null)
            {
                var p = Session.PosOf(me);
                int sec = p != null && Session.Map != null ? Session.Map.SectorAt(p.Value) : -1;
                if (sec < 0) _detectorText.text = "통로 — 구역 안으로 들어가면 측정됩니다";
                else
                {
                    // 현재 구역 + 이웃 구역까지 감지(범위 센서)
                    var sb = new System.Text.StringBuilder();
                    int c0 = sec % StageMaps.Stage3Cols, r0 = sec / StageMaps.Stage3Cols;
                    for (int i = 0; i < Stage3Logic.Sectors; i++)
                    {
                        int c = i % StageMaps.Stage3Cols, r = i / StageMaps.Stage3Cols;
                        int dist = Mathf.Abs(c - c0) + Mathf.Abs(r - r0);
                        if (dist > 1) continue;
                        int ppm = st.ppm[i];
                        string col = ppm >= Stage3Logic.PpmDanger ? "#FF7070" : "#66E68A";
                        string here = i == sec ? "<b>" : "";
                        string hereEnd = i == sec ? "</b> (여기)" : "";
                        sb.Append($"{here}{StageMaps.SectorName(i)}: <color={col}>{ppm} ppm</color>{hereEnd}   ");
                    }
                    _detectorText.text = sb.ToString();
                }
            }
            if (_broadcastText != null && me != (int)GameRole.Operator && _jiggle > 0.14f)
            {
                _jiggle = 0f;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 14; i++) { sb.Append('?', Random.Range(1, 5)); sb.Append(' '); }
                _broadcastText.text = sb.ToString();
            }
        }
    }
}
