using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>화면 한 장의 공통 골격. 상태가 바뀌면 OnState, 이벤트가 오면 OnEvent, 매 프레임 Tick.</summary>
    public abstract class ScreenBase
    {
        public RectTransform Root { get; protected set; }

        public virtual void Build(RectTransform parent)
        {
            Root = UI.Stretch(parent, GetType().Name);
        }

        public virtual void OnState() { }
        public virtual void OnEvent(GameEvent e) { }
        public virtual void Tick() { }

        public virtual void Dispose()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = null;
        }
    }

    /// <summary>
    /// STRANGER 공통 톤(명세 3절): 배경 #06080D, 패널 #0D1420, 테두리 #2A3A55, 앰버 #F2C572, 본문 #F0F2F5, 흐린 글자 #8A96A8.
    /// 사각(라운드 없음) 테두리 패널과 앰버/윤곽 버튼을 만든다. 모든 화면이 여기서 색을 가져다 쓴다.
    /// </summary>
    public static class Skin
    {
        public static readonly Color Bg = C("06080D");
        public static readonly Color Panel = new Color(0.051f, 0.078f, 0.125f, 0.96f);   // #0D1420 α0.96
        public static readonly Color PanelSolid = C("0D1420");
        public static readonly Color Border = C("2A3A55");
        public static readonly Color Amber = C("F2C572");
        public static readonly Color AmberDim = new Color(0.949f, 0.773f, 0.447f, 0.55f);
        public static readonly Color Ink = C("F0F2F5");
        public static readonly Color Dim = C("8A96A8");
        public static readonly Color DarkText = C("1A1206");
        public static readonly Color Red = C("D9534F");
        public static readonly Color Green = C("6FCF8A");
        public static readonly Color Slot = C("0A0F18");       // 초상/일러스트 빈 슬롯 색

        public static Color C(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>"STAGE 1" → "S T A G E  1" 처럼 글자 사이를 띄운 소제목 표기.</summary>
        public static string Spaced(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                sb.Append(s[i]);
                if (i < s.Length - 1) sb.Append(' ');
            }
            return sb.ToString();
        }

        /// <summary>테두리가 있는 고정 위치 패널. 반환값은 내용물을 넣을 안쪽 Rect(바깥 테두리 Rect는 parent).</summary>
        public static RectTransform Bordered(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
            Color border, Color fill, float thick = 2f, Vector2? pivot = null, bool raycast = false)
        {
            var outer = UI.Fixed(parent, name, anchor, pos, size, pivot);
            UI.Bg(outer, border, raycast);
            var inner = UI.Stretch(outer, "Inner", thick, thick, thick, thick);
            UI.Bg(inner, fill, raycast);
            return inner;
        }

        /// <summary>레이아웃 그룹 자식용 테두리 패널. 바깥 Rect에 LayoutElement 크기를 걸고 안쪽 Rect를 돌려준다.</summary>
        public static RectTransform BorderedCell(Transform parent, string name, float w, float h,
            Color border, Color fill, float thick = 2f, float flexW = 0f)
        {
            var outer = UI.Rect(parent, name);
            UI.Bg(outer, border, false);
            UI.LE(outer, w, h, flexW, 0f, w, h);
            var inner = UI.Stretch(outer, "Inner", thick, thick, thick, thick);
            UI.Bg(inner, fill, false);
            return inner;
        }

        /// <summary>안쪽 Rect의 테두리 색을 바꾼다(Bordered/BorderedCell 반환값 기준).</summary>
        public static void SetBorder(RectTransform inner, Color border)
        {
            if (inner == null || inner.parent == null) return;
            var img = inner.parent.GetComponent<Image>();
            if (img != null) img.color = border;
        }

        /// <summary>앰버 강조 버튼(앰버 배경 + 어두운 글자).</summary>
        public static Button Accent(Transform parent, string label, UnityEngine.Events.UnityAction onClick, int fontSize = 18, float minH = 48, float minW = 120)
        {
            var b = UI.Button(parent, label, Amber, onClick, fontSize, minH, minW);
            var t = UI.ButtonLabel(b);
            if (t != null) { t.color = DarkText; t.fontStyle = FontStyle.Bold; }
            var colors = b.colors;
            colors.disabledColor = new Color(0.5f, 0.45f, 0.35f, 0.5f);
            b.colors = colors;
            return b;
        }

        /// <summary>윤곽선 버튼(슬레이트 테두리 + 어두운 속 + 밝은 글자). 테두리 색을 바꾸려면 borderColor.</summary>
        public static Button Outline(Transform parent, string label, UnityEngine.Events.UnityAction onClick, int fontSize = 18, float minH = 48, float minW = 120, Color? borderColor = null, Color? textColor = null)
        {
            var rt = UI.Rect(parent, "Button");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = borderColor ?? Border;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.4f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            colors.colorMultiplier = 1f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            UI.LE(btn, minW, minH);

            var inner = UI.Stretch(rt, "Inner", 2, 2, 2, 2);
            UI.Bg(inner, PanelSolid, false);
            var trt = UI.Stretch(inner, "Label", 8, 2, 8, 2);
            var t = trt.gameObject.AddComponent<Text>();
            t.font = UI.Font; t.text = label; t.fontSize = fontSize; t.color = textColor ?? Ink;
            t.alignment = TextAnchor.MiddleCenter; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return btn;
        }

        /// <summary>윤곽선 버튼의 라벨 Text(Outline 전용 — UI.ButtonLabel 도 동작한다).</summary>
        public static Text OutlineLabel(Button b) => b != null ? b.GetComponentInChildren<Text>() : null;

        /// <summary>작은 색 사각형 + 글자(국기색 라벨, 시점 배지 등). 레이아웃 그룹 자식용 가로 묶음.</summary>
        public static RectTransform Swatch(Transform parent, Color color, string text, int fontSize, Color textColor, float squareSize = 16f)
        {
            var row = UI.Rect(parent, "Swatch");
            UI.HStack(row, 0, 8, false, TextAnchor.MiddleCenter);
            UI.LE(row, -1, squareSize + 6);
            var sq = UI.Rect(row, "Square");
            UI.Bg(sq, color, false);
            UI.LE(sq, squareSize, squareSize, 0f, 0f, squareSize, squareSize);
            var t = UI.Text(row, text, fontSize, textColor, TextAnchor.MiddleLeft, false);
            UI.LE(t, -1, -1, 0f);
            return row;
        }

        /// <summary>앰버 비네트(가운데가 밝은 큰 원). 배경 위에 얹어 로고/스테이지 선택 톤을 만든다.</summary>
        public static Image Vignette(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float alpha)
        {
            var rt = UI.Fixed(parent, "Vignette", anchor, pos, size, new Vector2(0.5f, 0.5f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteLib.Disc(0.98f);
            img.color = new Color(0.55f, 0.42f, 0.22f, alpha);
            img.raycastTarget = false;
            return img;
        }
    }

    /// <summary>
    /// 유저 플로우의 화면 전환기.
    /// 접속 전에는 "Local_" + LocalNav.Screen, 접속 후에는 서버 phase 로 화면 키를 정한다.
    /// 공용 토스트(알림)와 개발용 단축키(1~4: 봇 슬롯 조종 전환)도 여기서 처리한다.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        private RectTransform _screenLayer;
        private RectTransform _toastLayer;
        private ScreenBase _current;
        private string _key = "";
        private bool _building;   // 화면 Build 중 — 호스트 루프백(StateRpc)으로 되돌아오는 상태 변경은 미룬다
        private bool _pending;    // Build 중에 들어온 상태 변경이 있었는가

        private class ToastItem { public Text text; public float until; }
        private readonly List<ToastItem> _toasts = new List<ToastItem>();
        private VerticalLayoutGroup _toastStack;

        /// <summary>지금 떠 있는 화면 키(테스트/디버그용).</summary>
        public string CurrentKey => _key;

        /// <summary>화면마다 알림이 겹치지 않는 자리를 고른다. growUp이면 아래에서 위로 쌓인다.</summary>
        public void SetToastPlacement(Vector2 anchor, Vector2 pos, bool growUp)
        {
            if (_toastLayer == null) return;
            _toastLayer.anchorMin = anchor; _toastLayer.anchorMax = anchor;
            _toastLayer.pivot = new Vector2(0.5f, growUp ? 0f : 1f);
            _toastLayer.anchoredPosition = pos;
            if (_toastStack != null) _toastStack.childAlignment = growUp ? TextAnchor.LowerCenter : TextAnchor.UpperCenter;
        }

        public void ClearToasts()
        {
            foreach (var t in _toasts) if (t.text != null) Destroy(t.text.transform.parent.gameObject);
            _toasts.Clear();
        }

        public string LastNetMessage { get; private set; } = "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _screenLayer = UI.Stretch(UIRoot.Canvas, "Screens");
            _toastLayer = UI.Fixed(UIRoot.Canvas, "Toasts", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(820f, 130f));
            _toastStack = UI.VStack(_toastLayer, 0, 6, false, TextAnchor.UpperCenter);
            _toastStack.childForceExpandWidth = true;

            Progress.ApplyAudio();

            Session.StateChanged += OnState;
            Session.EventReceived += OnEvent;
            LocalNav.Changed += OnState;
            if (NetHub.Instance != null)
            {
                NetHub.Instance.StatusChanged += s => LastNetMessage = s;
                NetHub.Instance.Disconnected += OnDisconnected;
            }
            OnState();
        }

        private void OnDestroy()
        {
            Session.StateChanged -= OnState;
            Session.EventReceived -= OnEvent;
            LocalNav.Changed -= OnState;
            if (NetHub.Instance != null) NetHub.Instance.Disconnected -= OnDisconnected;
        }

        /// <summary>접속이 끊기면 로컬 스테이지 선택으로 돌아간다. 끊긴 이유(방장이 방을 닫음 등)는 토스트로 알린다.
        /// 참가 자체가 실패한 경우(방을 못 찾음 등)는 매칭 화면에 그대로 남아 방 코드를 고쳐 다시 시도할 수 있게 한다.</summary>
        private void OnDisconnected()
        {
            var hub = NetHub.Instance;
            string msg = hub != null ? hub.Status : "";
            LastNetMessage = msg;
            bool stayOnMatch = hub != null && hub.LastDisconnectWasConnectFailure && LocalNav.Screen == LocalScreen.Match;
            LocalNav.Go(stayOnMatch ? LocalScreen.Match : LocalScreen.StageSelect);   // Changed → OnState
            if (!string.IsNullOrEmpty(msg)) ShowToast(msg, GameLogic.ToneWarn);
        }

        private string ComputeKey()
        {
            if (!Session.Connected) return "Local_" + LocalNav.Screen;
            var s = Session.State;
            switch (s.phase)
            {
                case Phase.Lobby: return "Lobby";
                case Phase.NameSetup: return "Name";
                case Phase.Ready: return "Ready";
                case Phase.StageIntro:
                case Phase.StageClear: return "Cut_" + s.cutsceneId;
                case Phase.StagePlay: return $"Stage_{s.stage}_{s.st.seed}_{s.st.retries}";
                case Phase.Result: return "Result";
                default: return "Local_" + LocalNav.Screen;
            }
        }

        private ScreenBase Create(string key)
        {
            switch (key)
            {
                case "Local_Logo": return new LogoScreen();
                case "Local_Title": return new TitleScreen();
                case "Local_Prologue": return new PrologueScreen();
                case "Local_StageSelect": return new StageSelectScreen();
                case "Local_Match": return new MatchScreen();
                case "Local_Settings": return new SettingsScreen();
                case "Lobby": return new LobbyScreen();
                case "Name": return new NameSetupScreen();
                case "Ready": return new ReadyScreen();
                case "Result": return new ResultScreen();
            }
            if (key.StartsWith("Cut_")) return new CutsceneScreen();
            if (key.StartsWith("Stage_")) return new StageScreen();
            return new TitleScreen();
        }

        private void OnState()
        {
            if (_building) { _pending = true; return; }
            string key = ComputeKey();
            if (key != _key || _current == null)
            {
                _building = true;
                try
                {
                    _current?.Dispose();
                    InteractionState.Reset();
                    ClearToasts();
                    SetToastPlacement(new Vector2(0.5f, 1f), new Vector2(0f, -140f), false);
                    _key = key;                        // Build 전에 갱신 — 재진입 판정과 CurrentKey 가 새 키를 본다
                    _current = Create(key);
                    _current.Build(_screenLayer);
                    _toastLayer.SetAsLastSibling();
                }
                finally { _building = false; }
            }
            _current.OnState();
            if (_pending) { _pending = false; OnState(); }   // Build 중 바뀐 상태(키가 바뀌었을 수도 있다)를 한 번 더 반영
        }

        private float _lastAlarmAt = -10f;

        private void OnEvent(GameEvent e)
        {
            if (e.type == "Toast")
            {
                if (e.target < 0 || e.target == Session.LocalSlot) ShowToast(e.s, e.a);
            }
            else if (e.type == "Flash")
            {
                FeedbackFX.Flash(ToneColor(e.a), e.a == GameLogic.ToneBad ? 0.42f : 0.35f);
                if (e.a == GameLogic.ToneBad) { FeedbackFX.Shake(0.5f); Alarm(); }
            }
            // 효과음 훅(QA D-A1): 오답·오판 경보 / 클리어 / 카드·색 수신자 / 도넛 신호. 버튼 클릭음은 없다.
            switch (e.type)
            {
                case "Alarm": Alarm(); break;
                case "Clear": Sfx.Play("clear"); break;
                case "Signal": Sfx.Play("click", 0.8f); break;
                case "NumberCard": case "DirCard": case "Color":
                    if (e.target == Session.LocalSlot) Sfx.Play("chime");
                    break;
            }
            _current?.OnEvent(e);
        }

        /// <summary>경보음 — 감점 Flash(ToneBad)와 Alarm 이벤트가 같은 순간에 오므로 0.3초 안에는 한 번만.</summary>
        private void Alarm()
        {
            if (Time.unscaledTime - _lastAlarmAt < 0.3f) return;
            _lastAlarmAt = Time.unscaledTime;
            Sfx.Play("alarm");
        }

        public static Color ToneColor(int tone)
        {
            switch (tone)
            {
                case GameLogic.ToneGood: return new Color(0.35f, 0.9f, 0.5f);
                case GameLogic.ToneBad: return new Color(0.95f, 0.3f, 0.3f);
                case GameLogic.ToneWarn: return new Color(1f, 0.75f, 0.3f);
                default: return new Color(0.75f, 0.85f, 1f);
            }
        }

        /// <summary>토스트 큐 상한(QA P-A13): 넘치면 가장 오래된 것부터 지운다.</summary>
        public const int MaxToasts = 2;

        /// <summary>지금 떠 있는 토스트 수(테스트용).</summary>
        public int ToastCount => _toasts.Count;

        public void ShowToast(string text, int tone)
        {
            if (string.IsNullOrEmpty(text)) return;
            // "잠금 해제" 계열(ToneGood)이 오면 남아 있는 "키패드 잠금 중" 토스트는 바로 치운다(QA P-A13)
            if (tone == GameLogic.ToneGood && text.Contains("잠금 해제"))
            {
                for (int i = _toasts.Count - 1; i >= 0; i--)
                {
                    var t0 = _toasts[i];
                    if (t0.text != null && t0.text.text != null && t0.text.text.Contains("키패드 잠금 중"))
                    {
                        Destroy(t0.text.transform.parent.gameObject);
                        _toasts.RemoveAt(i);
                    }
                }
            }
            var rt = UI.Rect(_toastLayer, "Toast");
            UI.Bg(rt, new Color(0.03f, 0.05f, 0.08f, 0.92f), false);
            UI.LE(rt, -1, 34);
            var t = UI.Text(rt, text, 17, ToneColor(tone), TextAnchor.MiddleCenter);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(14, 4); trt.offsetMax = new Vector2(-14, -4);
            _toasts.Add(new ToastItem { text = t, until = Time.time + 4.5f });
            while (_toasts.Count > MaxToasts)
            {
                Destroy(_toasts[0].text.transform.parent.gameObject);
                _toasts.RemoveAt(0);
            }
        }

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                if (t.text == null) { _toasts.RemoveAt(i); continue; }
                float remain = t.until - Time.time;
                if (remain <= 0f) { Destroy(t.text.transform.parent.gameObject); _toasts.RemoveAt(i); continue; }
                if (remain < 0.8f)
                {
                    var c = t.text.color; c.a = remain / 0.8f; t.text.color = c;
                    var bg = t.text.transform.parent.GetComponent<Image>();
                    if (bg != null) { var bc = bg.color; bc.a = 0.92f * remain / 0.8f; bg.color = bc; }
                }
            }

            HandleDevKeys();
            _current?.Tick();
        }

        private void HandleDevKeys()
        {
            if (!Session.IsHost) return;   // C-6: 봇 자리 조종 전환은 호스트 전용(클라이언트는 CanControl 이 항상 false 라 경고 토스트만 떴다)
            if (!Session.Connected || InteractionState.InputLocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            int pick = -1;
            if (kb.digit1Key.wasPressedThisFrame) pick = 0;
            else if (kb.digit2Key.wasPressedThisFrame) pick = 1;
            else if (kb.digit3Key.wasPressedThisFrame) pick = 2;
            else if (kb.digit4Key.wasPressedThisFrame) pick = 3;
            if (pick >= 0)
            {
                if (pick == Session.LocalSlot) return;
                if (Session.CanControl(pick))
                {
                    Session.SetLocalSlot(pick);
                    ShowToast($"이제 <b>{Roles.Name(pick)}</b>({Roles.Nation(pick)})을 조종합니다 — 화면이 그 역할의 시점으로 바뀝니다", GameLogic.ToneInfo);
                }
                else ShowToast("그 자리는 다른 사람이 조종하고 있어요 (봇 자리만 전환 가능)", GameLogic.ToneWarn);
            }
        }
    }
}
