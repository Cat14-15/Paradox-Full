using System.Net;
using System.Net.Sockets;
using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>로비 — 4인 매칭 / 인원 확인. 역할 카드 4장, 빈 자리 클릭으로 자리 이동, 호스트가 시작.</summary>
    public class LobbyScreen : ScreenBase
    {
        private RectTransform _cards;
        private Text _title;
        private Text _info;
        private Button _startBtn, _botBtn;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, new Color(0.08f, 0.09f, 0.13f, 1f));

            _title = UI.Label(Root, "4인 매칭 · 인원 확인", 34, UI.Gold, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 50f), TextAnchor.MiddleCenter);
            _info = UI.Label(Root, "", 16, UI.InkDim, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(1000f, 60f), TextAnchor.MiddleCenter);

            _cards = UI.Fixed(Root, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1240f, 420f));
            UI.HStack(_cards, 0, 20, true, TextAnchor.MiddleCenter);

            var bottom = UI.Fixed(Root, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 60f));
            UI.HStack(bottom, 0, 14, true, TextAnchor.MiddleCenter);
            UI.Button(bottom, "나가기", UI.BtnColor, () => NetHub.Instance.Shutdown(), 18, 52, 140);
            _botBtn = UI.Button(bottom, "빈 자리 봇으로 채우기", UI.BtnColor, () => Session.Send("FillBots"), 18, 52);
            _startBtn = UI.Button(bottom, "게임 시작", UI.BtnGood, () => Session.Send("StartGame"), 20, 52);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 112f), true);
            OnState();
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
                UI.ButtonLabel(_startBtn).text = n >= 4 ? "게임 시작 ▶" : $"게임 시작 ({n}/4 대기 중)";
            }
            if (_botBtn != null) _botBtn.gameObject.SetActive(host && n < 4);

            string ip = host ? $"내 IP: <b>{LocalIPs()}</b> · 포트 7777 — 친구들은 타이틀에서 이 IP로 참가" : "호스트가 시작할 때까지 기다려 주세요";
            if (_info != null)
                _info.text = $"{ip}\n<size=14>빈 자리를 클릭하면 그 역할로 옮길 수 있어요 · 역할은 게임 내내 고정됩니다</size>";
        }

        private void BuildCard(int slot, PlayerInfo p)
        {
            var c = Roles.Color(slot);
            bool mine = Session.LocalSlot == slot;
            var card = UI.Rect(_cards, "Card" + slot);
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = mine ? new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 1f) : new Color(0.13f, 0.14f, 0.19f, 1f);
            UI.LE(card, 280, 400, 1f);
            var v = UI.VStack(card, 16, 8, false, TextAnchor.UpperCenter);

            UI.Text(card, $"<color=#{Roles.Hex(slot)}><b>{Roles.Name(slot)}</b></color>", 26, UI.Ink, TextAnchor.MiddleCenter, false, 36);
            UI.Text(card, $"{Roles.Nation(slot)} · {Roles.Code(slot)}", 15, UI.InkDim, TextAnchor.MiddleCenter, false, 22);

            var portrait = UIPortrait.Card(card, slot, p.present ? p.character : -1, 42f);
            UI.LE(portrait, 100, 118);

            string who = p.present ? (p.isBot ? "봇 (컴퓨터)" : p.name) : "빈 자리";
            string tag = p.present && !p.isBot && slot == Session.State.hostSlot ? " <size=13>(호스트)</size>" : "";
            UI.Text(card, $"<b>{who}</b>{tag}{(mine ? "  ★ 나" : "")}", 18, p.present ? UI.Ink : new Color(0.5f, 0.52f, 0.58f), TextAnchor.MiddleCenter, true, 30);
            UI.Text(card, Roles.Job(slot), 13, UI.InkDim, TextAnchor.UpperCenter, true, 64);

            if (!p.present || (p.isBot && Session.IsHost))
            {
                if (!mine)
                {
                    int target = slot;
                    var btn = UI.Button(card, p.present ? "이 자리로 (봇과 교체)" : "이 자리로 이동", UI.BtnAccent, () => Session.Send("Slot", target), 15, 40);
                }
            }
            else if (mine)
            {
                UI.Text(card, "내 자리", 14, c, TextAnchor.MiddleCenter, false, 40);
            }
        }

        private static string LocalIPs()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                string s = "";
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                    string a = ip.ToString();
                    if (a.StartsWith("127.")) continue;
                    s += (s.Length > 0 ? ", " : "") + a;
                }
                return string.IsNullOrEmpty(s) ? "127.0.0.1" : s;
            }
            catch { return "127.0.0.1"; }
        }
    }
}
