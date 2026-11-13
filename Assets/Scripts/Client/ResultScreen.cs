using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 결과(명세 3절). "STAGE 1 CLEAR", 팀 초상 4, 기록(총 시간 · 오답 · 카드 · 채팅 · 재시도), 내레이션 한 줄,
    /// 버튼 "스테이지 선택으로"(모두: 로컬 StageSelect 로 간 뒤 Shutdown) / "로비로 (한 번 더)"(호스트 Again).
    /// 로컬 기록(Progress.stage1Cleared / bestSeconds)도 여기서 갱신한다.
    /// </summary>
    public class ResultScreen : ScreenBase
    {
        private float _t;
        private Text _title;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            Skin.Vignette(Root, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1500f, 900f), 0.24f);

            var S = Session.State;
            var stats = S.stats;
            var st = S.st;
            float total = stats != null ? stats.playSeconds : 0f;
            if (total <= 0f) total = Mathf.Max(0f, Session.Clock - S.gameStartClock);
            Progress.RecordClear(total);

            UI.Label(Root, Skin.Spaced("STAGE 1 · PHILIPPINES · 침수된 박물관"), 14, Skin.Dim, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 22f), TextAnchor.MiddleCenter);
            _title = UI.Label(Root, "STAGE 1  CLEAR", 44, Skin.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(800f, 60f), TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.Bold;

            // 팀 초상 4
            var team = UI.Fixed(Root, "Team", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1200f, 190f));
            UI.HStack(team, 0, 18, false, TextAnchor.MiddleCenter);
            for (int i = 0; i < 4; i++)
            {
                var c = Roles.Color(i);
                var cell = Skin.BorderedCell(team, "P" + i, 270, 180, Skin.Alpha(c, 0.8f), Skin.PanelSolid, 2f);
                UI.HStack(cell, 12, 12, false, TextAnchor.MiddleLeft);
                var slotBox = UI.Rect(cell, "Slot");
                UI.Bg(slotBox, Skin.Slot, false);
                UI.LE(slotBox, 96, 150, 0f);
                var port = UIPortrait.MakeForSlot(slotBox, i, i, 44f);
                port.anchorMin = port.anchorMax = new Vector2(0.5f, 0.5f);
                port.anchoredPosition = Vector2.zero;
                string nm = Session.NameOf(i);
                var info = UI.Text(cell, $"<color=#{Roles.Hex(i)}><b>{Roles.Name(i)}</b></color>\n<size=18>{nm}</size>\n<size=12><color=#8A96A8>{Roles.Nation(i)} · {Roles.Code(i)}</color></size>", 15, Skin.Ink, TextAnchor.MiddleLeft, true);
                UI.LE(info, -1, -1, 1f);
            }

            // 기록
            var panel = Skin.Bordered(Root, "Stats", new Vector2(0.5f, 0.5f), new Vector2(-240f, -70f), new Vector2(540f, 300f), Skin.Border, Skin.Panel, 2f);
            UI.VStack(panel, 24, 10, false, TextAnchor.UpperLeft);
            UI.Text(panel, "<b>팀 기록</b>", 20, Skin.Amber, TextAnchor.MiddleLeft, false, 30);
            StatRow(panel, "총 시간", FormatTime(total));
            StatRow(panel, "오답 횟수", $"{(stats != null ? stats.wrongInputs : 0)}회");
            StatRow(panel, "보낸 카드", $"{(stats != null ? stats.cards : 0)}장");
            StatRow(panel, "채팅", $"{(stats != null ? stats.chats : 0)}줄");
            StatRow(panel, "재시도", $"{(st != null ? st.retries : 0)}회");
            string best = Progress.bestSeconds > 0f ? FormatTime(Progress.bestSeconds) : "-";
            UI.Text(panel, $"<size=13><color=#8A96A8>내 최고 기록: {best}</color></size>", 13, Skin.Dim, TextAnchor.MiddleLeft, false, 20);

            // 내레이션 한 줄
            var msg = Skin.Bordered(Root, "Message", new Vector2(0.5f, 0.5f), new Vector2(300f, -70f), new Vector2(520f, 300f), Skin.Border, Skin.Panel, 2f);
            UI.VStack(msg, 24, 12, false, TextAnchor.UpperLeft);
            UI.Text(msg, "<b>클리어 후</b>", 20, Skin.Amber, TextAnchor.MiddleLeft, false, 30);
            // QA D-A10: 내레이션은 클리어 컷씬(StoryData s1_clear)의 속마음 줄이 단일 출처
            string inner = StoryData.ClearInnerText(1);
            var narr = UI.Text(msg, string.IsNullOrEmpty(inner) ? "" : $"\"{inner}\"", 16, Skin.Ink, TextAnchor.UpperLeft, true, 90);
            narr.fontStyle = FontStyle.Italic;
            narr.lineSpacing = 1.2f;
            UI.Text(msg, $"— {VnView.PlayerName(0)} ({Roles.Name(0)})", 13, Skin.Dim, TextAnchor.MiddleRight, false, 20);
            UI.Text(msg, "말은 안 통했지만, 네 사람은 함께 물이 차오르는 박물관을 빠져나왔다.\n다음 봉투는 아직 봉인되어 있다.", 13, Skin.Dim, TextAnchor.UpperLeft, true, 44);

            var bottom = UI.Fixed(Root, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(760f, 56f));
            UI.HStack(bottom, 0, 14, true, TextAnchor.MiddleCenter);
            Skin.Outline(bottom, "스테이지 선택으로", () => { LocalNav.Go(LocalScreen.StageSelect); if (NetHub.Instance != null) NetHub.Instance.Shutdown(); }, 18, 52, 220);
            if (Session.IsHost) Skin.Accent(bottom, "로비로 (한 번 더)", () => Session.Send("Again"), 18, 52, 220);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 108f), true);
        }

        private static void StatRow(Transform parent, string label, string value)
        {
            var row = UI.Rect(parent, "Row");
            UI.HStack(row, 0, 8, true, TextAnchor.MiddleLeft);
            UI.LE(row, -1, 26);
            var l = UI.Text(row, label, 15, Skin.Dim, TextAnchor.MiddleLeft, false);
            UI.LE(l, -1, -1, 1f);
            var v = UI.Text(row, $"<b>{value}</b>", 17, Skin.Ink, TextAnchor.MiddleRight, false);
            UI.LE(v, 120, -1, 0f);
        }

        public static string FormatTime(float sec)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(sec));
            return $"{s / 60}분 {s % 60:00}초";
        }

        public override void Tick()
        {
            _t += Time.deltaTime;
            if (_title != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_t * 2.2f);
                _title.color = Color.Lerp(Skin.Amber, new Color(1f, 0.96f, 0.85f), k * 0.4f);
            }
        }
    }
}
