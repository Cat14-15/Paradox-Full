using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 준비 완료(PDF s06). "네 명 모두 준비됐습니다", 카드 4장(역할색 테두리 + 픽셀 초상 + 이름 + "✓ 완료"),
    /// 하단 "4명 전원이 중앙 분수에 함께 모여야 클리어됩니다". 서버가 1.5초 뒤 자동으로 오프닝으로 넘긴다. 초상은 살짝 통통 뛴다.
    /// </summary>
    public class ReadyScreen : ScreenBase
    {
        private readonly RectTransform[] _portraits = new RectTransform[4];
        private float _t;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            DrawCurtain(Root);

            // QA P-A5: 커튼 금색 띠(y 0.925~0.933 → 위에서 60~68px)와 겹치지 않게 아래로
            UI.Label(Root, "네 명 모두 준비됐습니다", 32, Skin.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(800f, 48f), TextAnchor.MiddleCenter);

            var row = UI.Fixed(Root, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1320f, 420f));
            UI.HStack(row, 0, 30, false, TextAnchor.MiddleCenter);
            var S = Session.State;
            for (int i = 0; i < 4; i++)
            {
                var c = Roles.Color(i);
                var card = Skin.BorderedCell(row, "Card" + i, 290, 390, c, Skin.PanelSolid, 3f);
                UI.VStack(card, 18, 8, false, TextAnchor.UpperCenter);
                var slotBox = UI.Rect(card, "Slot");
                UI.Bg(slotBox, Skin.Slot, false);
                UI.LE(slotBox, 190, 220, 0f);
                var portrait = UIPortrait.MakeForSlot(slotBox, i, i, 80f);
                portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.5f);
                portrait.anchoredPosition = new Vector2(0f, 4f);
                _portraits[i] = portrait;

                var p = S.players[i];
                string nm = p.present ? (string.IsNullOrEmpty(p.name) ? Roles.DefaultName(i) : p.name) : Roles.DefaultName(i);
                UI.Text(card, $"<b>{nm}</b>", 20, Skin.Ink, TextAnchor.MiddleCenter, false, 30);
                UI.Text(card, $"<size=11><color=#{Roles.Hex(i)}>{Roles.Name(i)}</color></size>", 12, Skin.Dim, TextAnchor.MiddleCenter, false, 16);

                // QA P-A4: "✓ 완료" 배지는 92×30 알약으로 고정 — VStack 이 줄을 늘리지 않게(flex 0) + HStack 이 세로로 늘리지 않게
                var badgeRow = UI.Rect(card, "BadgeRow");
                var badgeStack = UI.HStack(badgeRow, 0, 0, false, TextAnchor.MiddleCenter);
                badgeStack.childForceExpandHeight = false;
                UI.LE(badgeRow, -1, 32, 0, 0, -1, 32);
                var badge = UI.Rect(badgeRow, "Badge");
                UI.Bg(badge, c, false);
                UI.LE(badge, 92, 30, 0f, 0f, 92, 30);
                var bt = UI.Label(badge, "✓ 완료", 15, Skin.DarkText, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 30f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
                bt.fontStyle = FontStyle.Bold;
            }

            UI.Label(Root, "4명 전원이 중앙 분수에 함께 모여야 클리어됩니다", 15, Skin.Dim, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(700f, 24f), TextAnchor.MiddleCenter);
            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 84f), true);
        }

        /// <summary>극장 커튼 프레이밍: 양옆 어두운 자주색 세로 주름 + 가운데 어두운 무대. 이름 설정 화면과 공유.</summary>
        public static void DrawCurtain(RectTransform root)
        {
            var curtain = UI.Stretch(root, "Curtain");
            UI.Bg(curtain, new Color(0.12f, 0.05f, 0.08f), false);
            const int folds = 44;
            for (int i = 0; i < folds; i++)
            {
                float x0 = i / (float)folds, x1 = (i + 1) / (float)folds;
                var f = UI.Rect(curtain, "Fold");
                f.anchorMin = new Vector2(x0, 0f); f.anchorMax = new Vector2(x1, 1f);
                f.offsetMin = Vector2.zero; f.offsetMax = Vector2.zero;
                float k = (i % 2 == 0) ? 0.55f : 1f;
                UI.Bg(f, new Color(0.20f * k, 0.07f * k, 0.11f * k), false);
                // 주름 하이라이트 얇은 줄
                var hl = UI.Rect(f, "Hl");
                hl.anchorMin = new Vector2(0.62f, 0f); hl.anchorMax = new Vector2(0.72f, 1f);
                hl.offsetMin = Vector2.zero; hl.offsetMax = Vector2.zero;
                UI.Bg(hl, new Color(0.30f * k, 0.12f * k, 0.16f * k), false);
            }
            // 윗단 장식 띠
            var valance = UI.Rect(curtain, "Valance");
            valance.anchorMin = new Vector2(0f, 0.93f); valance.anchorMax = new Vector2(1f, 1f);
            valance.offsetMin = Vector2.zero; valance.offsetMax = Vector2.zero;
            UI.Bg(valance, new Color(0.16f, 0.06f, 0.10f), false);
            var gold = UI.Rect(curtain, "Gold");
            gold.anchorMin = new Vector2(0f, 0.925f); gold.anchorMax = new Vector2(1f, 0.933f);
            gold.offsetMin = Vector2.zero; gold.offsetMax = Vector2.zero;
            UI.Bg(gold, Skin.Alpha(Skin.Amber, 0.45f), false);
            // 가운데 무대(어두운 네이비)
            var stage = UI.Rect(curtain, "Stage");
            stage.anchorMin = new Vector2(0.085f, 0f); stage.anchorMax = new Vector2(0.915f, 0.885f);
            stage.offsetMin = Vector2.zero; stage.offsetMax = Vector2.zero;
            UI.Bg(stage, Skin.C("070A11"), false);
        }

        public override void Tick()
        {
            _t += Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                if (_portraits[i] == null) continue;
                float bob = Mathf.Max(0f, Mathf.Sin(_t * 5f + i * 0.9f)) * 10f;
                _portraits[i].anchoredPosition = new Vector2(0f, 4f + bob);
            }
        }
    }
}
