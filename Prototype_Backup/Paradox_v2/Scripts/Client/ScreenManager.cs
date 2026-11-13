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
    /// 유저 플로우의 화면 전환기. 세션 단계(Phase)에 맞는 화면을 만들고 지운다.
    /// 공용 토스트(알림)와 개발용 단축키(1~4: 봇 슬롯 조종 전환)도 여기서 처리한다.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        private RectTransform _screenLayer;
        private RectTransform _toastLayer;
        private ScreenBase _current;
        private string _key = "";

        private class ToastItem { public Text text; public float until; }
        private readonly List<ToastItem> _toasts = new List<ToastItem>();
        private VerticalLayoutGroup _toastStack;

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

            Session.StateChanged += OnState;
            Session.EventReceived += OnEvent;
            if (NetHub.Instance != null)
            {
                NetHub.Instance.StatusChanged += s => LastNetMessage = s;
                NetHub.Instance.Disconnected += () => { LastNetMessage = NetHub.Instance.Status; OnState(); };
            }
            OnState();
        }

        private void OnDestroy()
        {
            Session.StateChanged -= OnState;
            Session.EventReceived -= OnEvent;
        }

        private string ComputeKey()
        {
            if (!Session.Connected) return "Title";
            var s = Session.State;
            switch (s.phase)
            {
                case Phase.Lobby: return "Lobby";
                case Phase.CharacterSelect: return "Char";
                case Phase.Prologue:
                case Phase.StageIntro:
                case Phase.StageClear:
                case Phase.Ending: return "Cut_" + s.cutsceneId;
                case Phase.StagePlay: return $"Stage_{s.stage}_{s.st.seed}_{s.st.retries}";
                case Phase.Result: return "Result";
                default: return "Title";
            }
        }

        private ScreenBase Create(string key)
        {
            if (key == "Title") return new TitleScreen();
            if (key == "Lobby") return new LobbyScreen();
            if (key == "Char") return new CharacterSelectScreen();
            if (key.StartsWith("Cut_")) return new CutsceneScreen();
            if (key.StartsWith("Stage_")) return new StageScreen();
            if (key == "Result") return new ResultScreen();
            return new TitleScreen();
        }

        private void OnState()
        {
            string key = ComputeKey();
            if (key != _key || _current == null)
            {
                _current?.Dispose();
                InteractionState.Reset();
                ClearToasts();
                SetToastPlacement(new Vector2(0.5f, 1f), new Vector2(0f, -140f), false);
                _current = Create(key);
                _current.Build(_screenLayer);
                _key = key;
                _toastLayer.SetAsLastSibling();
            }
            _current.OnState();
        }

        private void OnEvent(GameEvent e)
        {
            if (e.type == "Toast")
            {
                if (e.target < 0 || e.target == Session.LocalSlot) ShowToast(e.s, e.a);
            }
            else if (e.type == "Flash")
            {
                FeedbackFX.Flash(ToneColor(e.a), e.a == GameLogic.ToneBad ? 0.42f : 0.35f);
                if (e.a == GameLogic.ToneBad) FeedbackFX.Shake(0.5f);
            }
            _current?.OnEvent(e);
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

        public void ShowToast(string text, int tone)
        {
            if (string.IsNullOrEmpty(text)) return;
            var rt = UI.Rect(_toastLayer, "Toast");
            UI.Bg(rt, new Color(0.06f, 0.07f, 0.10f, 0.9f), false);
            UI.LE(rt, -1, 34);
            var t = UI.Text(rt, text, 17, ToneColor(tone), TextAnchor.MiddleCenter);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(14, 4); trt.offsetMax = new Vector2(-14, -4);
            _toasts.Add(new ToastItem { text = t, until = Time.time + 4.5f });
            while (_toasts.Count > 3)
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
                    if (bg != null) { var bc = bg.color; bc.a = 0.9f * remain / 0.8f; bg.color = bc; }
                }
            }

            HandleDevKeys();
            _current?.Tick();
        }

        private void HandleDevKeys()
        {
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
