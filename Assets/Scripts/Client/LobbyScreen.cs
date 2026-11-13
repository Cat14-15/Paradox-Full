using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 로비(팀 모으기). 역할 카드 4장(픽셀 초상, 이름/봇/빈 자리, 자리 이동), 방 코드, 봇 채우기, 게임 시작(호스트), 나가기.
    /// 나가기 → 로컬 스테이지 선택으로 돌아간 뒤 접속 종료.
    /// </summary>
    public class LobbyScreen : ScreenBase
    {
        private RectTransform _cards;
        private Text _info, _code;
        private Button _startBtn, _botBtn;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            Skin.Vignette(Root, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1500f, 900f), 0.16f);

            UI.Label(Root, Skin.Spaced("STAGE 1 · PHILIPPINES"), 14, Skin.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 22f), TextAnchor.MiddleCenter);
            UI.Label(Root, "팀 모으기", 34, Skin.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(900f, 50f), TextAnchor.MiddleCenter);
            _code = UI.Label(Root, "", 30, Skin.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1100f, 44f), TextAnchor.MiddleCenter);
            _info = UI.Label(Root, "", 15, Skin.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(1100f, 30f), TextAnchor.MiddleCenter);
            if (NetHub.Instance != null) NetHub.Instance.RoomCodeChanged += OnCode;

            _cards = UI.Fixed(Root, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1300f, 470f));
            UI.HStack(_cards, 0, 22, false, TextAnchor.MiddleCenter);

            var bottom = UI.Fixed(Root, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 56f));
            UI.HStack(bottom, 0, 14, true, TextAnchor.MiddleCenter);
            Skin.Outline(bottom, "나가기", () => { LocalNav.Go(LocalScreen.StageSelect); if (NetHub.Instance != null) NetHub.Instance.Shutdown(); }, 17, 52, 140);
            _botBtn = Skin.Outline(bottom, "빈 자리 봇으로 채우기", () => Session.Send("FillBots"), 17, 52, 220);
            _startBtn = Skin.Accent(bottom, "게임 시작", () => Session.Send("StartGame"), 20, 52, 260);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 112f), true);
            OnState();
        }

        private void OnCode(string c) { if (Root != null) RefreshCode(); }

        /// <summary>방 코드를 크게 표시한다. 방장은 코드를 알려 주고, 참가자는 자기가 들어온 방의 코드를 본다. 솔로에서는 숨긴다.</summary>
        private void RefreshCode()
        {
            if (_code == null) return;
            var hub = NetHub.Instance;
            if (hub == null || hub.IsSolo) { _code.text = ""; return; }
            string c = hub.RoomCode;
            if (string.IsNullOrEmpty(c)) { _code.text = "방 코드를 만드는 중…"; return; }
            _code.text = Session.IsHost
                ? $"방 코드  <b>{c}</b>   <size=14>친구에게 알려 주세요 (Ctrl+C 로 복사)</size>"
                : $"방 코드  <b>{c}</b>   <size=14>방장이 시작할 때까지 기다려 주세요</size>";
        }

        public override void Tick()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var hub = NetHub.Instance;
            if (kb != null && hub != null && !string.IsNullOrEmpty(hub.RoomCode) && !hub.IsSolo
                && (kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed) && kb.cKey.wasPressedThisFrame)
            {
                GUIUtility.systemCopyBuffer = hub.RoomCode;
                if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("방 코드를 복사했어요: " + hub.RoomCode, Paradox.Game.GameLogic.ToneInfo);
            }
        }

        public override void Dispose()
        {
            if (NetHub.Instance != null) NetHub.Instance.RoomCodeChanged -= OnCode;
            base.Dispose();
        }

        public override void OnState()
        {
            var S = Session.State;
            UI.Clear(_cards);
            for (int i = 0; i < 4; i++) BuildCard(i, S.players[i]);

            int n = S.PresentCount;
            bool host = Session.IsHost;
            if (_startBtn != null)
            {
                _startBtn.gameObject.SetActive(host);
                _startBtn.interactable = n >= 4;
                var t = UI.ButtonLabel(_startBtn);
                if (t != null) t.text = n >= 4 ? "게임 시작  ▶" : $"게임 시작  ({n}/4 대기 중)";
            }
            if (_botBtn != null) _botBtn.gameObject.SetActive(host && n < 4);

            if (_info != null)
                _info.text = "<size=13>빈 자리를 누르면 그 역할로 옮길 수 있어요 · 역할은 게임 내내 고정됩니다</size>";
            RefreshCode();
        }

        private void BuildCard(int slot, PlayerInfo p)
        {
            var c = Roles.Color(slot);
            bool mine = Session.LocalSlot == slot;
            var card = Skin.BorderedCell(_cards, "Card" + slot, 300, 450, mine ? c : Skin.Border, mine ? new Color(c.r * 0.16f, c.g * 0.16f, c.b * 0.16f, 1f) : Skin.PanelSolid, mine ? 3f : 2f);
            UI.VStack(card, 16, 8, false, TextAnchor.UpperCenter);

            // 국기색 라벨 + 역할명
            Skin.Swatch(card, c, Roles.Nation(slot) + " · " + Roles.Code(slot), 14, c);
            UI.Text(card, $"<color=#{Roles.Hex(slot)}><b>{Roles.Name(slot)}</b></color>", 24, Skin.Ink, TextAnchor.MiddleCenter, false, 32);

            // 픽셀 초상(항상 슬롯의 캐릭터)
            var slotBox = UI.Rect(card, "Slot");
            UI.Bg(slotBox, Skin.Slot, false);
            UI.LE(slotBox, 180, 150, 0f);
            var portrait = UIPortrait.MakeForSlot(slotBox, slot, slot, 56f);
            portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.5f);
            portrait.anchoredPosition = new Vector2(0f, 2f);

            string who = p.present ? (p.isBot ? "봇 (컴퓨터)" : p.name) : "빈 자리";
            string tag = p.present && !p.isBot && slot == Session.State.hostSlot ? " <size=12><color=#8A96A8>(호스트)</color></size>" : "";
            UI.Text(card, $"<b>{who}</b>{tag}{(mine ? "  <color=#F2C572>★ 나</color>" : "")}", 17, p.present ? Skin.Ink : Skin.Dim, TextAnchor.MiddleCenter, true, 28);
            UI.Text(card, Roles.Job(slot), 12, Skin.Dim, TextAnchor.UpperCenter, true, 66);

            if (!p.present || (p.isBot && Session.IsHost))
            {
                if (!mine)
                {
                    int target = slot;
                    Skin.Outline(card, p.present ? "이 자리로 (봇과 교체)" : "이 자리로 이동", () => Session.Send("Slot", target), 14, 40, 200, Skin.Amber, Skin.Amber);
                }
            }
            else if (mine)
            {
                UI.Text(card, "내 자리", 13, c, TextAnchor.MiddleCenter, false, 40);
            }
        }
    }
}
