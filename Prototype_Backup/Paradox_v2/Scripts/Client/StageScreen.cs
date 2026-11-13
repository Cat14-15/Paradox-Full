using System.Text;
using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 스테이지 플레이 화면 = 월드(StageWorld) + 개인 시야 HUD.
    /// 역할 카드 · 지금 할 일 · 목표/타이머 · 신호 피드 · 도구 패널(StageTools) · 미니맵 · 하단 소통 바(5버튼/방향/채팅) · 오버레이.
    /// </summary>
    public class StageScreen : ScreenBase
    {
        private StageWorld _world;
        private MinimapView _minimap;
        private StageTools _tools;

        private Text _roleText, _instr, _title, _status, _timer, _feedText, _prompt, _targetText, _commsLabel;
        private RectTransform _commsButtons, _roleCard;
        private string _introLine1 = "", _introLine2 = "", _titleLine = "";
        private Image _roleStripe;
        private RectTransform _failPanel, _clearPanel, _switchPanel;
        private readonly System.Collections.Generic.List<Button> _switchBtns = new System.Collections.Generic.List<Button>();
        private Text _failText;
        private Image _water;
        private InputField _chat;
        private bool _chatLocked;
        private int _target = -1;
        private string _commsKey = "";
        private float _feedDirty;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            _world = new StageWorld();
            _world.Build();

            // 왼쪽 위: 역할 카드(역할 색) + 지금 할 일
            int me0 = Session.LocalSlot;
            var rc0 = Roles.Color(Roles.Valid(me0) ? me0 : 0);
            _roleCard = UI.Panel(Root, "RoleCard", new Color(rc0.r * 0.30f, rc0.g * 0.30f, rc0.b * 0.30f, 0.95f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(440f, 72f));
            _roleStripe = UI.Bg(UI.Fixed(_roleCard, "Stripe", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 72f), new Vector2(0f, 0.5f)), rc0, false);
            _roleText = UI.Label(_roleCard, "", 15, UI.Ink, new Vector2(0f, 1f), new Vector2(18f, -7f), new Vector2(412f, 60f), TextAnchor.UpperLeft);

            var todo = UI.Panel(Root, "Todo", new Color(0.10f, 0.11f, 0.08f, 0.92f), new Vector2(0f, 1f), new Vector2(16f, -90f), new Vector2(440f, 92f));
            _instr = UI.Label(todo, "", 14, new Color(0.97f, 0.95f, 0.8f), new Vector2(0f, 1f), new Vector2(14f, -7f), new Vector2(412f, 82f), TextAnchor.UpperLeft);

            // 가운데 위: 제목 / 목표 / 타이머
            var top = UI.Panel(Root, "Top", new Color(0.06f, 0.07f, 0.10f, 0.8f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(640f, 84f));
            _title = UI.Label(top, "", 19, UI.Gold, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(620f, 26f), TextAnchor.MiddleCenter);
            _status = UI.Label(top, "", 14, UI.Ink, new Vector2(0.5f, 1f), new Vector2(0f, -31f), new Vector2(620f, 22f), TextAnchor.MiddleCenter);
            _timer = UI.Label(top, "", 24, UI.Ink, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(620f, 30f), TextAnchor.MiddleCenter);

            // 스테이지 시작 직후 몇 초간은 위쪽 패널이 "당신은 ○○" 역할 안내로 바뀐다(월드를 가리지 않게)

            // 오른쪽 위: 신호 피드
            var feed = UI.Panel(Root, "Feed", new Color(0.06f, 0.07f, 0.10f, 0.86f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(400f, 236f));
            UI.Label(feed, "신호 피드 — 내 입장에서 보이는 것", 12, new Color(0.55f, 0.6f, 0.7f), new Vector2(0f, 1f), new Vector2(12f, -6f), new Vector2(380f, 18f), TextAnchor.MiddleLeft);
            _feedText = UI.Label(feed, "", 14, UI.Ink, new Vector2(0f, 0f), new Vector2(12f, 8f), new Vector2(376f, 200f), TextAnchor.LowerLeft, new Vector2(0f, 0f));
            _feedText.verticalOverflow = VerticalWrapMode.Overflow;

            // 왼쪽 가운데: 도구 패널 / 왼쪽 아래: 미니맵
            _tools = new StageTools();
            _tools.Build(Root);
            _minimap = new MinimapView();
            _minimap.Build(Root);

            // Stage 2 물 오버레이
            var waterRt = UI.Rect(Root, "Water");
            waterRt.anchorMin = new Vector2(0f, 0f); waterRt.anchorMax = new Vector2(1f, 0f); waterRt.pivot = new Vector2(0.5f, 0f);
            waterRt.offsetMin = Vector2.zero; waterRt.offsetMax = new Vector2(0f, 0f);
            _water = UI.Bg(waterRt, new Color(0.2f, 0.45f, 0.85f, 0f), false);
            waterRt.SetAsFirstSibling();

            // 상호작용 안내
            _prompt = UI.Label(Root, "", 18, UI.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(700f, 30f), TextAnchor.MiddleCenter);

            // 하단 소통 바
            BuildComms();

            // 조종 전환(호스트 + 봇) — 오른쪽 위 피드 아래
            _switchPanel = UI.Panel(Root, "Switch", UI.PanelDark, new Vector2(1f, 1f), new Vector2(-16f, -256f), new Vector2(400f, 64f));
            UI.HStack(_switchPanel, 8, 6, false, TextAnchor.MiddleLeft);
            var swLabel = UI.Text(_switchPanel, "조종 전환\n<size=10>(1~4 키 · 봇 자리만)</size>", 12, UI.InkDim, TextAnchor.MiddleLeft, false);
            UI.LE(swLabel, 96, 48, 0f);
            _switchBtns.Clear();
            for (int i = 0; i < 4; i++)
            {
                int s = i;
                var b = UI.Button(_switchPanel, Roles.Short(i), UI.BtnColor, () => Session.SetLocalSlot(s), 14, 44, 64);
                UI.LE(b, 64, 44, 1f);
                _switchBtns.Add(b);
            }

            // 신호 차단 배너는 위쪽 패널(제목/상태)이 대신 표시한다 — 월드를 가리지 않게

            // 알림은 아래 가운데(소통 바 위)에 위로 쌓인다
            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 182f), true);

            _failPanel = UI.Panel(Root, "Fail", new Color(0.22f, 0.08f, 0.10f, 0.96f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(620f, 200f));
            UI.VStack(_failPanel, 24, 12, false, TextAnchor.UpperCenter);
            _failText = UI.Text(_failPanel, "", 26, new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter, true, 70);
            if (Session.IsHost) UI.Button(_failPanel, "다시 시도 (같은 스테이지)", UI.BtnAccent, () => Session.Send("Retry"), 19, 50);
            else UI.Text(_failPanel, "호스트가 다시 시도를 누르면 재도전합니다", 15, UI.InkDim, TextAnchor.MiddleCenter, false, 30);
            _failPanel.gameObject.SetActive(false);

            _clearPanel = UI.Panel(Root, "Clear", new Color(0.08f, 0.18f, 0.12f, 0.96f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(680f, 150f));
            UI.VStack(_clearPanel, 20, 8, false, TextAnchor.UpperCenter);
            UI.Text(_clearPanel, "STAGE CLEAR", 38, new Color(0.8f, 1f, 0.85f), TextAnchor.MiddleCenter, false, 52);
            UI.Text(_clearPanel, "말이 안 통해도, 함께 해냈다", 20, UI.Ink, TextAnchor.MiddleCenter, false, 30);
            _clearPanel.gameObject.SetActive(false);

            Session.LocalSlotChanged += OnLocalSlot;
            // 아래쪽 HUD(소통 바)가 월드를 덜 가리도록 카메라를 캐릭터보다 약간 아래로 둔다
            var appCam = GameApp.Instance;
            if (appCam != null && appCam.Follow != null) appCam.Follow.offset = new Vector3(0f, -0.9f, -10f);
            OnState();
            RefreshFeed();
            UpdateCamera(true);
        }

        public override void Dispose()
        {
            Session.LocalSlotChanged -= OnLocalSlot;
            if (_chatLocked) { InteractionState.PopLock(); _chatLocked = false; }
            _world?.Dispose();
            _tools?.Dispose();
            _minimap?.Dispose();
            var app = GameApp.Instance;
            if (app != null && app.Follow != null) { app.Follow.useBounds = false; app.Follow.offset = new Vector3(0f, 0f, -10f); }
            base.Dispose();
        }

        private void OnLocalSlot()
        {
            UpdateCamera(true);
        }

        // ---------------------------------------------------------------- 소통 바

        private void BuildComms()
        {
            var bar = UI.Panel(Root, "Comms", new Color(0.09f, 0.10f, 0.14f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1000f, 118f));
            UI.VStack(bar, 10, 6, false, TextAnchor.UpperLeft);

            var row1 = UI.Rect(bar, "Row1");
            UI.HStack(row1, 0, 8, false);
            UI.LE(row1, -1, 26);
            _commsLabel = UI.Text(row1, "", 14, UI.Ink, TextAnchor.MiddleLeft, false);
            UI.LE(_commsLabel, 300, 26, 1f);
            _targetText = UI.Text(row1, "대상:", 13, UI.InkDim, TextAnchor.MiddleRight, false);
            UI.LE(_targetText, 50, 26, 0f);
            var targets = UI.Rect(row1, "Targets");
            UI.HStack(targets, 0, 4, false);
            UI.LE(targets, 300, 26, 0f);
            MakeTargetButton(targets, -1, "전체");
            for (int i = 0; i < 4; i++) MakeTargetButton(targets, i, Roles.Short(i));

            _commsButtons = UI.Rect(bar, "Row2");
            UI.HStack(_commsButtons, 0, 8, true);
            UI.LE(_commsButtons, -1, 52);
        }

        private readonly System.Collections.Generic.List<Button> _targetBtns = new System.Collections.Generic.List<Button>();

        private void MakeTargetButton(Transform parent, int target, string label)
        {
            int t = target;
            var b = UI.Button(parent, label, target == -1 ? UI.BtnAccent : UI.BtnColor, () => { _target = t; RefreshTargets(); }, 13, 26, 48);
            _targetBtns.Add(b);
        }

        private void RefreshTargets()
        {
            for (int i = 0; i < _targetBtns.Count; i++)
            {
                int t = i - 1;
                bool me = t == Session.LocalSlot;
                _targetBtns[i].gameObject.SetActive(!me);
                UI.SetButtonColor(_targetBtns[i], t == _target ? UI.BtnAccent : UI.BtnColor);
            }
        }

        private void RebuildCommsButtons()
        {
            int me = Session.LocalSlot;
            int stage = Session.Stage;
            var st = Session.St;
            string key = $"{me}_{stage}_{st.step}_{st.signalCut}";
            if (key == _commsKey) return;
            _commsKey = key;
            if (_chatLocked) { InteractionState.PopLock(); _chatLocked = false; }
            _chat = null;
            UI.Clear(_commsButtons);

            bool chat = StageDefs.ChatAllowed(stage, me);
            _commsLabel.text = $"내 역할 <color=#{Roles.Hex(me)}><b>{Roles.Name(me)}</b></color> · " +
                (chat ? "<color=#FFE9A0>채팅 자유</color> (친구들에게는 외국어로 들림) + 신호" : (stage == 4 && me == 0 ? "<color=#FFB0A0>채팅 불가 — 버튼 5개 + 글자 카드</color>" : "버튼 5개 신호"));

            Signal("GO", "GO ▶", UI.BtnGood);
            Signal("STOP", "STOP ■", UI.BtnWarn);
            Signal("OK", "✓ 확인", UI.BtnColor);
            Signal("ASK", "? 다시", UI.BtnColor);
            var warn = Signal("WARN", "⚠ 경고", new Color(0.65f, 0.35f, 0.2f));
            _warnBtn = warn;

            if (StageDefs.HasDirectionPad(stage, me))
            {
                Signal("L", "←", UI.BtnAccent, 44);
                Signal("U", "↑", UI.BtnAccent, 44);
                Signal("D", "↓", UI.BtnAccent, 44);
                Signal("R", "→", UI.BtnAccent, 44);
            }

            if (chat)
            {
                _chat = UI.Input(_commsButtons, "메시지 입력 후 Enter (T로 포커스)", "", 15);
                UI.LE(_chat, 220, 48, 1.2f);
                _chat.onEndEdit.AddListener(text =>
                {
                    if (string.IsNullOrWhiteSpace(text)) return;
                    Session.Send("Chat", 0, 0, text.Trim());
                    _chat.text = "";
                });
            }
            RefreshTargets();
        }

        private Button _warnBtn;

        private Button Signal(string id, string label, Color color, float minW = 90)
        {
            string s = id;
            return UI.Button(_commsButtons, label, color, () => Session.Send("Signal", 0, _target, s), 16, 48, minW);
        }

        // ---------------------------------------------------------------- 상태 갱신

        public override void OnState()
        {
            var S = Session.State;
            var st = S.st;
            int me = Session.LocalSlot;
            var def = StageDefs.Get(st.stage);

            _world.Refresh();
            _tools.Rebuild();
            _minimap.OnState();
            RebuildCommsButtons();

            var p = Session.Me;
            bool local = StageDefs.CanRead(st.stage, me);
            var rc = Roles.Color(me);
            if (_roleCard != null) UI.Bg(_roleCard, new Color(rc.r * 0.30f, rc.g * 0.30f, rc.b * 0.30f, 0.95f));
            if (_roleStripe != null) _roleStripe.color = rc;
            string charName = GameApp.Instance != null && GameApp.Instance.HasRoleArt(me) ? "전용 캐릭터" : GameApp.CharacterName(p != null ? p.character : -1);
            _roleText.text = $"<size=19><color=#{Roles.Hex(me)}><b>{Roles.Name(me)}</b></color></size> <size=13>({Roles.Nation(me)})</size>  ·  {(p != null ? p.name : "")} · {charName}\n" +
                             (local ? "<color=#FFE9A0>★ 이번 스테이지의 현지인 — 표지판이 읽힙니다</color>" : $"<color=#E08080>표지판이 ???로 보임</color> — 현지인은 <color=#{Roles.Hex(def.localSlot)}>{Roles.Name(def.localSlot)}</color>");
            _instr.text = "<b>지금 할 일</b>  " + StageGuide.Instruction(me);
            _introLine1 = $"당신은 <color=#{Roles.Hex(me)}><b>{Roles.Name(me)}</b></color> ({Roles.Nation(me)})";
            _introLine2 = local ? "<color=#FFE9A0>★ 이번 스테이지의 현지인 — 표지판을 읽고 친구들에게 전달하세요</color>"
                                : $"표지판은 ???로 보입니다 — <color=#{Roles.Hex(def.localSlot)}>{Roles.Name(def.localSlot)}</color>의 전달에 의지하세요";
            _titleLine = $"{def.Headline}   ·   {def.mechanic}";
            _title.text = $"{def.Headline}   ·   {def.mechanic}";
            _status.text = st.status;

            _failPanel.gameObject.SetActive(st.failed);
            _clearPanel.gameObject.SetActive(st.cleared);
            _switchPanel.gameObject.SetActive(Session.IsHost && Session.HasBots);
            for (int i = 0; i < _switchBtns.Count; i++)
            {
                bool cur = i == me;
                UI.SetButtonColor(_switchBtns[i], cur ? Roles.Color(i) : UI.BtnColor);
                UI.ButtonLabel(_switchBtns[i]).text = cur ? $"★ {Roles.Short(i)}" : Roles.Short(i);
                _switchBtns[i].interactable = Session.CanControl(i);
            }
            if (st.failed) _failText.text = $"{FailText(st.stage)}\n<size=16>재시도 {st.retries}회</size>";

            UpdateCamera(false);
            RefreshFeed();
        }

        private static string FailText(int stage)
        {
            switch (stage)
            {
                case 2: return "물에 잠겼다…";
                case 3: return "가스에 휩싸였다…";
                case 4: return "연기에 갇혔다…";
                default: return "실패…";
            }
        }

        public override void OnEvent(GameEvent e)
        {
            _world.OnEvent(e);
            if (e.type == "Signal" || e.type == "Chat" || e.type == "LetterArrive") _feedDirty = 0.01f;
            if (e.type == "Fail") { }
        }

        private void RefreshFeed()
        {
            var sb = new StringBuilder();
            int me = Session.LocalSlot;
            int start = Mathf.Max(0, Session.Log.Count - 8);
            for (int i = start; i < Session.Log.Count; i++)
            {
                var e = Session.Log[i].e;
                if (e.type == "Toast") continue;
                string who = e.slot >= 0 ? $"<color=#{Roles.Hex(e.slot)}><b>{Roles.Short(e.slot)}</b></color>" : "시스템";
                switch (e.type)
                {
                    case "Signal":
                        {
                            string to = e.target < 0 ? "" : (e.target == me ? " <color=#FFE9A0>→ 나</color>" : $" → {Roles.Short(e.target)}");
                            sb.Append($"{who} {AvatarNet.SignalText(e.s)}{to}\n");
                            break;
                        }
                    case "Chat":
                        {
                            bool understand = e.slot == me;
                            sb.Append(understand ? $"{who} {e.s}\n" : $"{who} <color=#C08890>{Garble(e.s)}</color> <size=11><color=#8A7A80>(외국어)</color></size>\n");
                            break;
                        }
                    case "LetterArrive":
                        {
                            string to = e.target < 0 ? "모두" : (e.target == me ? "<color=#FFE9A0>나</color>" : Roles.Short(e.target));
                            sb.Append($"{who} 글자 <color=#FFE9A0>[{e.s}]</color> → {to}\n");
                            break;
                        }
                }
            }
            _feedText.text = sb.ToString();
        }

        // 외국어 채팅은 어디서나 같은 방식(??? 덩어리)으로 보인다 — 말풍선·컷씬과 통일
        private static string Garble(string text)
        {
            var sb = new StringBuilder(text.Length + 8);
            int run = 0;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) { if (run > 0) { sb.Append(' '); run = 0; } continue; }
                int n = 1 + ((c * 13 + 7) % 3);
                if (run == 0) sb.Append('?', n); else if (run % 2 == 0) sb.Append('?');
                run++;
                if (run >= 4) { sb.Append(' '); run = 0; }
            }
            return sb.ToString().Trim();
        }

        private void UpdateCamera(bool snap)
        {
            var app = GameApp.Instance;
            if (app == null || app.Follow == null) return;
            var av = Session.ControlledAvatar;
            if (av != null) app.Follow.target = av.transform;
            var map = Session.Map;
            if (map != null) { app.Follow.useBounds = true; app.Follow.bounds = map.bounds; }
            if (snap && av != null) app.Follow.SnapToTarget();
        }

        // ---------------------------------------------------------------- 매 프레임

        public override void Tick()
        {
            var st = Session.St;
            _world.Tick();
            _tools.Tick();
            _minimap.Tick();

            if (_feedDirty > 0f) { _feedDirty = 0f; RefreshFeed(); }

            // 스테이지 시작 후 5초 동안은 위쪽 패널이 역할 안내가 된다
            bool intro = !st.cleared && !st.failed && Session.Clock - st.startClock < 5f;
            if (st.signalCut)
            {
                _title.text = "<color=#80B3FF>지금은 신호가 끊깁니다</color>";
                _status.text = "정전 파동 — 전달이 잠시 멈춥니다 (전달 중인 글자는 보호됨)";
            }
            else if (intro)
            {
                _title.text = _introLine1;
                _status.text = _introLine2;
            }
            else
            {
                _title.text = _titleLine;
                _status.text = st.status;
            }

            // 타이머 + 물
            if (st.timed && !st.cleared)
            {
                float left = Mathf.Max(0f, Session.TimeLeft);
                _timer.text = $"⏱ {(int)(left / 60)}:{(int)(left % 60):00}";
                _timer.color = left < 20f ? new Color(1f, 0.45f, 0.45f) : (left < 45f ? new Color(1f, 0.8f, 0.5f) : UI.Ink);
                if (st.stage == 2 && _water != null)
                {
                    float f = 1f - Mathf.Clamp01(left / Mathf.Max(1f, st.timeLimit));
                    var rt = (RectTransform)_water.transform;
                    rt.sizeDelta = new Vector2(0f, f * 620f);
                    _water.color = new Color(0.2f, 0.45f, 0.85f, 0.18f + 0.3f * f);
                }
            }
            else
            {
                _timer.text = st.cleared ? "<color=#7FE39B>CLEAR</color>" : (st.timed ? "" : "시간 압박 없음 — 튜토리얼");
            }

            // 카메라 대상이 아직 없었으면 붙인다
            var app = GameApp.Instance;
            if (app != null && app.Follow != null && app.Follow.target == null) UpdateCamera(true);

            // 상호작용 안내
            var av = Session.ControlledAvatar;
            _prompt.text = av != null ? av.CurrentPrompt.text : "";

            // ⚠ 강조(Stage 1 관측자 경고 창)
            if (_warnBtn != null)
            {
                bool glow = st.stage == 1 && Session.LocalSlot == (int)GameRole.Observer && Stage1Logic.GuardGlow(st, Session.Clock);
                UI.SetButtonColor(_warnBtn, glow ? (Mathf.Sin(Time.time * 25f) > 0f ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.6f, 0.3f)) : new Color(0.65f, 0.35f, 0.2f));
            }

            // 채팅 포커스 잠금
            bool focused = _chat != null && _chat.isFocused;
            if (focused != _chatLocked)
            {
                if (focused) InteractionState.PushLock(); else InteractionState.PopLock();
                _chatLocked = focused;
            }
            var kb = Keyboard.current;
            if (kb != null && !InteractionState.InputLocked)
            {
                if (_chat != null && kb.tKey.wasPressedThisFrame) _chat.ActivateInputField();
                if (kb.escapeKey.wasPressedThisFrame && Session.IsHost && st.failed) Session.Send("Retry");
            }

            // 키패드 근접 여부가 바뀌면 도구 패널 갱신(Stage 4)
            if (st.stage == 4 && av != null && Session.LocalSlot >= 1)
            {
                bool near = StageInteractions.NearKeypad(Session.LocalSlot, av.transform.position) && !st.codeDone;
                if (near != _tools.KeypadMode) _tools.Rebuild();
            }
        }
    }
}
