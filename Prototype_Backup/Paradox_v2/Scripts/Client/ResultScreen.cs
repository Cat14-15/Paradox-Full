using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>결과 화면 — 팀 기록과 메시지.</summary>
    public class ResultScreen : ScreenBase
    {
        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, new Color(0.08f, 0.09f, 0.13f, 1f));

            var S = Session.State;
            var stats = S.stats;
            float total = Mathf.Max(0f, Session.Clock - S.gameStartClock);

            UI.Label(Root, "결과", 40, UI.Gold, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 56f), TextAnchor.MiddleCenter);
            UI.Label(Root, "말은 안 통했지만, 네 사람은 함께 해냈습니다.", 20, UI.Ink, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(900f, 32f), TextAnchor.MiddleCenter);

            // 팀
            var team = UI.Fixed(Root, "Team", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1100f, 150f));
            UI.HStack(team, 0, 16, true, TextAnchor.MiddleCenter);
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                var cell = UI.Rect(team, "P" + i);
                UI.Bg(cell, new Color(0.12f, 0.13f, 0.18f, 1f), false);
                UI.HStack(cell, 10, 10, false, TextAnchor.MiddleLeft);
                var port = UIPortrait.MakeForSlot(cell, i, p.present ? p.character : -1, 34f);
                UI.LE(port, 80, 90, 0f);
                var info = UI.Text(cell, $"<color=#{Roles.Hex(i)}><b>{Roles.Name(i)}</b></color>\n{Session.NameOf(i)}\n<size=13>{Roles.Nation(i)}</size>", 16, UI.Ink, TextAnchor.MiddleLeft, true);
                UI.LE(info, -1, -1, 1f);
            }

            // 기록
            var panel = UI.Panel(Root, "Stats", UI.PanelMid, new Vector2(0.5f, 0.5f), new Vector2(-250f, -40f), new Vector2(560f, 330f));
            UI.VStack(panel, 22, 8, false, TextAnchor.UpperLeft);
            UI.Text(panel, "<b>팀 기록</b>", 20, UI.Gold, TextAnchor.MiddleLeft, false, 30);
            UI.Text(panel, $"총 플레이 시간: <b>{FormatTime(total)}</b>", 16, UI.Ink, TextAnchor.MiddleLeft, false, 24);
            for (int s = 1; s <= 4; s++)
            {
                var def = StageDefs.Get(s);
                float sec = s < stats.stageSeconds.Length ? stats.stageSeconds[s] : 0f;
                int retry = s < stats.retries.Length ? stats.retries[s] : 0;
                UI.Text(panel, $"{def.title} · {def.country}: {FormatTime(sec)}  ·  재시도 {retry}회", 15, UI.InkDim, TextAnchor.MiddleLeft, false, 22);
            }
            UI.Text(panel, $"보낸 신호 {stats.signals}회 · 글자 카드 {stats.letters}장 · 채팅 {stats.chats}줄 · 소실/실수 {stats.losses}회", 15, UI.InkDim, TextAnchor.MiddleLeft, true, 40);
            int retries = 0; foreach (var r in stats.retries) retries += r;
            int score = Mathf.Clamp(100 - retries * 8 - stats.losses * 2 - Mathf.RoundToInt(total / 60f), 10, 100);
            UI.Text(panel, $"협동 점수: <b><color=#FFE9A0>{score}</color></b> / 100", 22, UI.Ink, TextAnchor.MiddleLeft, false, 34);

            // 메시지
            var msg = UI.Panel(Root, "Message", UI.PanelMid, new Vector2(0.5f, 0.5f), new Vector2(330f, -40f), new Vector2(560f, 330f));
            UI.VStack(msg, 22, 10, false, TextAnchor.UpperLeft);
            UI.Text(msg, "<b>네 스테이지가 남긴 것</b>", 20, UI.Gold, TextAnchor.MiddleLeft, false, 30);
            UI.Text(msg, "<b>Stage 1</b>  처음 느끼는 답답함 — 표지판을 못 읽는 기분을 조작자가 처음 겪었습니다.", 14, UI.Ink, TextAnchor.UpperLeft, true, 44);
            UI.Text(msg, "<b>Stage 2</b>  정보를 가진 사람은 상황마다 다릅니다. 통역도 이번엔 모르는 쪽이었습니다.", 14, UI.Ink, TextAnchor.UpperLeft, true, 44);
            UI.Text(msg, "<b>Stage 3</b>  언어를 몰라도 도울 방법은 있습니다 — 지도, 감지기, 색.", 14, UI.Ink, TextAnchor.UpperLeft, true, 44);
            UI.Text(msg, "<b>Stage 4</b>  우리 나라에서는 내가 누군가를 도와줄 수 있는 사람이 됩니다.", 14, UI.Ink, TextAnchor.UpperLeft, true, 44);
            UI.Text(msg, "<i>\"나도 다른 나라에 가면 이방인이 되고, 우리 나라에서는 내가 누군가를 도와줄 수 있는 사람이 된다.\"</i>", 14, UI.Gold, TextAnchor.UpperLeft, true, 50);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 104f), true);
            var bottom = UI.Fixed(Root, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(700f, 56f));
            UI.HStack(bottom, 0, 14, true, TextAnchor.MiddleCenter);
            UI.Button(bottom, "타이틀로 (나가기)", UI.BtnColor, () => NetHub.Instance.Shutdown(), 18, 52);
            if (Session.IsHost) UI.Button(bottom, "로비로 돌아가기 (한 번 더)", UI.BtnAccent, () => Session.Send("Again"), 18, 52);
        }

        public static string FormatTime(float sec)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(sec));
            return $"{s / 60}분 {s % 60:00}초";
        }
    }
}
