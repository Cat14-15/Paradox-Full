using Paradox.Core;
using Paradox.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 매칭(팀 모으기) — 접속 진입점. 방 만들기(방 코드 발급) / 방 코드 입력 + 참가하기 / 혼자 테스트(봇 3명).
    /// 접속에 성공하면 서버 상태가 와서 로비 화면으로 바뀐다. 뒤로 → 스테이지 선택(접속 시도 중이면 취소).
    /// </summary>
    public class MatchScreen : ScreenBase
    {
        private Text _status;
        private InputField _code;
        private Button _hostBtn, _joinBtn, _soloBtn;
        private float _t;
        private bool _codeLocked;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            Skin.Vignette(Root, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1300f, 900f), 0.16f);

            UI.Label(Root, Skin.Spaced("국제 청소년 교류 캠프"), 14, Skin.Dim,
                new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(600f, 24f), TextAnchor.MiddleCenter);
            UI.Label(Root, "팀 모으기 — Stage 1 · 침수된 박물관", 32, Skin.Amber,
                new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(900f, 50f), TextAnchor.MiddleCenter);
            UI.Label(Root, "네 명이 한 팀입니다. 한 명이 방을 만들어 방 코드를 알려 주면, 나머지는 그 코드로 인터넷에서 참가합니다.", 15, Skin.Dim,
                new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(900f, 26f), TextAnchor.MiddleCenter);

            var panel = Skin.Bordered(Root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(640f, 470f), Skin.Border, Skin.Panel, 2f);
            UI.VStack(panel, 32, 14, false, TextAnchor.UpperCenter);

            UI.Text(panel, "방 만들기", 15, Skin.Dim, TextAnchor.MiddleLeft, false, 20);
            _hostBtn = Skin.Accent(panel, "방 만들기 (방 코드 받기)", () => Try(NetHub.Instance != null && NetHub.Instance.StartHost(false)), 20, 54, 200);

            UI.Text(panel, "친구 방에 참가하기 (방 코드 입력)", 15, Skin.Dim, TextAnchor.MiddleLeft, false, 20);
            var row = UI.Rect(panel, "JoinRow");
            UI.HStack(row, 0, 10, true);
            UI.LE(row, -1, 50);
            _code = UI.Input(row, "방 코드 (예: K7QM2)", "", 22);
            _code.characterLimit = 6;
            _code.onValueChanged.AddListener(v => { string u = (v ?? "").ToUpperInvariant(); if (u != v) _code.text = u; });
            var ipImg = _code.GetComponent<Image>();
            if (ipImg != null) ipImg.color = Skin.C("0A0F18");
            UI.LE(_code, 300, 48, 1f);
            _joinBtn = Skin.Outline(row, "참가하기", () => Try(NetHub.Instance != null && NetHub.Instance.StartClient(_code.text)), 18, 48, 150, Skin.Amber, Skin.Amber);
            UI.LE(_joinBtn, 150, 48, 0f);

            UI.Text(panel, "혼자 해 보기", 15, Skin.Dim, TextAnchor.MiddleLeft, false, 20);
            _soloBtn = Skin.Outline(panel, "혼자 테스트 (봇 3명 · 1~4 키로 역할 전환)", () => Try(NetHub.Instance != null && NetHub.Instance.StartHost(true)), 17, 50, 200);

            _status = UI.Text(panel, "", 15, Skin.Amber, TextAnchor.MiddleCenter, true, 40);

            UI.Text(panel,
                "<b>온라인</b>  인터넷만 되면 어디서나 — 한 명이 [방 만들기]로 받은 방 코드를 알려 주면 나머지가 입력해 참가 · 4명이 모이면 시작\n" +
                "<b>테스트</b>  혼자 시작하면 봇 3명이 채워지고, 1~4 키로 네 역할을 번갈아 조종할 수 있어요",
                13, Skin.Dim, TextAnchor.UpperLeft, true, 60);

            if (NetHub.Instance != null) NetHub.Instance.StatusChanged += OnNetStatus;

            var back = Skin.Outline(Root, "◀  뒤로", () => Back(), 15, 42, 120);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f); brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(36f, 36f); brt.sizeDelta = new Vector2(120f, 42f);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 60f), true);
            Refresh();
        }

        private void Back()
        {
            var hub = NetHub.Instance;
            if (hub != null && hub.IsRunning && !Session.Connected) { hub.Shutdown(); Refresh(); return; }
            LocalNav.Go(LocalScreen.StageSelect);
        }

        private void Try(bool ok)
        {
            Refresh();
        }

        /// <summary>접속 실패(방을 못 찾음 등)처럼 NetHub 상태가 바뀌면 바로 문구·버튼을 되살린다(1초 주기 Refresh 를 기다리지 않는다).</summary>
        private void OnNetStatus(string s)
        {
            if (Root == null) return;
            var hub = NetHub.Instance;
            bool failed = hub != null && !hub.IsRunning && hub.LastDisconnectWasConnectFailure;
            Refresh();
            if (failed && _status != null) _status.color = Skin.Red;
        }

        private void Refresh()
        {
            var hub = NetHub.Instance;
            bool running = hub != null && hub.IsRunning;
            string msg = hub != null ? hub.Status : "";
            if (string.IsNullOrEmpty(msg) && ScreenManager.Instance != null) msg = ScreenManager.Instance.LastNetMessage;
            if (_status != null)
            {
                _status.text = running ? (string.IsNullOrEmpty(msg) ? "접속 중…" : msg) : msg;
                _status.color = hub != null && !running && hub.LastDisconnectWasConnectFailure ? Skin.Red : Skin.Amber;
            }
            if (_hostBtn != null) _hostBtn.interactable = !running;
            if (_joinBtn != null) _joinBtn.interactable = !running;
            if (_soloBtn != null) _soloBtn.interactable = !running;
            if (_code != null) _code.interactable = !running;
        }

        public override void Dispose()
        {
            if (NetHub.Instance != null) NetHub.Instance.StatusChanged -= OnNetStatus;
            if (_codeLocked) { InteractionState.PopLock(); _codeLocked = false; }
            base.Dispose();
        }

        public override void Tick()
        {
            _t += Time.deltaTime;
            // 방 코드 입력 중에는 단축키가 먹지 않게
            bool focused = _code != null && _code.isFocused;
            if (focused && !_codeLocked) { InteractionState.PushLock(); _codeLocked = true; }
            else if (!focused && _codeLocked) { InteractionState.PopLock(); _codeLocked = false; }

            var hub = NetHub.Instance;
            if (hub != null && hub.IsRunning && !Session.Connected)
            {
                if (_status != null) _status.text = hub.Status + new string('.', (int)(_t * 2f) % 4) + "   <size=12>(Esc · 뒤로 = 취소)</size>";
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { hub.Shutdown(); Refresh(); }
            }
            else if (_t % 1f < Time.deltaTime) Refresh();

            var k = Keyboard.current;
            if (k != null && !focused && k.escapeKey.wasPressedThisFrame && !(hub != null && hub.IsRunning)) LocalNav.Go(LocalScreen.StageSelect);
        }
    }
}
