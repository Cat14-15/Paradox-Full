using Paradox.Core;
using Paradox.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>타이틀 화면 — 방 만들기(호스트) / 참가 / 혼자 테스트(봇 3명).</summary>
    public class TitleScreen : ScreenBase
    {
        private Text _status;
        private InputField _ip;
        private float _t;
        private Text _logo;
        private Button _hostBtn, _joinBtn, _soloBtn;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, new Color(0.07f, 0.08f, 0.12f, 1f));

            // 배경 장식: 네 역할 색 띠
            for (int i = 0; i < 4; i++)
            {
                var band = UI.Fixed(Root, "Band", new Vector2(0.5f, 0.5f), new Vector2(-330f + i * 220f, 60f), new Vector2(180f, 900f));
                var c = Game.Roles.Color(i);
                UI.Bg(band, new Color(c.r, c.g, c.b, 0.05f), false);
                band.localRotation = Quaternion.Euler(0, 0, 18f);
            }

            var panel = UI.Panel(Root, "Panel", new Color(0.10f, 0.11f, 0.16f, 0.96f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(620f, 620f));
            UI.VStack(panel, 32, 14, false, TextAnchor.UpperCenter);

            _logo = UI.Text(panel, "PARADOX", 64, UI.Gold, TextAnchor.MiddleCenter, false, 80);
            UI.Text(panel, "말이 안 통해도, 함께.", 22, UI.Ink, TextAnchor.MiddleCenter, false, 34);
            UI.Text(panel, "4인 협동 · 언어 장벽 탈출 게임\n조작자 · 통역 · 내비게이터 · 관측자가 한 팀이 되어 네 나라의 재난에서 탈출합니다.",
                15, UI.InkDim, TextAnchor.MiddleCenter, true, 56);

            _hostBtn = UI.Button(panel, "방 만들기 (호스트)", UI.BtnAccent, () => Try(NetHub.Instance.StartHost(false)), 20, 52);
            var row = UI.Rect(panel, "JoinRow");
            UI.HStack(row, 0, 10, true);
            UI.LE(row, -1, 48);
            _ip = UI.Input(row, "호스트 IP (예: 192.168.0.10)", "127.0.0.1", 17);
            UI.LE(_ip, 240, 46, 1f);
            _joinBtn = UI.Button(row, "참가하기", UI.BtnGood, () => Try(NetHub.Instance.StartClient(_ip.text)), 19, 46, 150);
            UI.LE(_joinBtn, 150, 46, 0f);

            _soloBtn = UI.Button(panel, "혼자 테스트 (봇 3명 · 1~4 키로 역할 전환)", UI.BtnColor, () => Try(NetHub.Instance.StartHost(true)), 18, 48);

            _status = UI.Text(panel, "", 15, new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleCenter, true, 40);

            UI.Text(panel,
                "<b>조작</b>  WASD/화살표 이동 · E 상호작용 · SPACE 연타 · 마우스로 버튼\n" +
                "<b>온라인</b>  같은 Wi-Fi에서 호스트 IP로 참가 (포트 7777) · 4명이 모이면 시작\n" +
                "<b>테스트</b>  혼자 시작하면 봇 3명이 채워지고, 1~4 키로 네 역할을 번갈아 조종할 수 있습니다",
                13, UI.InkDim, TextAnchor.UpperLeft, true, 80);

            UI.Label(Root, "부산소프트웨어마이스터고 · UNESCO 청소년 프로젝트", 13, new Color(0.5f, 0.55f, 0.65f),
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(700f, 24f), TextAnchor.MiddleCenter);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 48f), true);
            Refresh();
        }

        private void Try(bool ok)
        {
            Refresh();
        }

        private void Refresh()
        {
            var hub = NetHub.Instance;
            bool running = hub != null && hub.IsRunning;
            string msg = hub != null ? hub.Status : "";
            if (string.IsNullOrEmpty(msg) && ScreenManager.Instance != null) msg = ScreenManager.Instance.LastNetMessage;
            if (_status != null) _status.text = running ? (string.IsNullOrEmpty(msg) ? "접속 중…" : msg) : msg;
            if (_hostBtn != null) _hostBtn.interactable = !running;
            if (_joinBtn != null) _joinBtn.interactable = !running;
            if (_soloBtn != null) _soloBtn.interactable = !running;
        }

        public override void Tick()
        {
            _t += Time.deltaTime;
            if (_logo != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_t * 1.6f);
                _logo.color = Color.Lerp(UI.Gold, new Color(1f, 0.98f, 0.85f), k * 0.5f);
            }
            var hub = NetHub.Instance;
            if (hub != null && hub.IsRunning && !Session.Connected)
            {
                // 접속 중 표시(참가 실패 시 NGO가 Disconnect 콜백을 준다)
                if (_status != null) _status.text = hub.Status + new string('.', (int)(_t * 2f) % 4);
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { hub.Shutdown(); Refresh(); }
            }
            else if (_t % 1f < Time.deltaTime) Refresh();
        }
    }
}
