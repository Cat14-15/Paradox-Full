using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 스테이지 선택(PDF s04). 카드 4장: Stage 1(앰버 테두리 + 설명 + PLAY), Stage 2~4(TOP SECRET 도장 + 자물쇠, 클릭하면 흔들림).
    /// PLAY → 매칭 화면. 좌하단 "타이틀로". Stage 1을 클리어했으면 카드에 CLEAR 와 최고 기록을 표시한다.
    /// </summary>
    public class StageSelectScreen : ScreenBase
    {
        private class Shaker { public RectTransform rt; public Vector2 basePos; public float t; }
        private readonly List<Shaker> _shakers = new List<Shaker>();
        private Image _glow;
        private float _t;

        private static readonly string[] LockedNations = { "", "", "베트남", "남아공", "한국" };

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            Skin.Vignette(Root, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1500f, 900f), 0.26f);

            UI.Label(Root, Skin.Spaced("국제 청소년 교류 캠프"), 14, Skin.Dim,
                new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(600f, 24f), TextAnchor.MiddleCenter);
            UI.Label(Root, "스테이지 선택", 38, Skin.Amber,
                new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(600f, 54f), TextAnchor.MiddleCenter);

            var row = UI.Fixed(Root, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1420f, 520f));
            UI.HStack(row, 0, 26, false, TextAnchor.MiddleCenter);

            BuildStage1(row);
            for (int s = 2; s <= 4; s++) BuildLocked(row, s);

            UI.Label(Root, "Stage 1을 클리어하면 다음 봉투가 열린다", 15, Skin.Dim,
                new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(700f, 24f), TextAnchor.MiddleCenter);

            var back = Skin.Outline(Root, "◀  타이틀로", () => LocalNav.Go(LocalScreen.Title), 15, 42, 130);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f); brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(36f, 36f); brt.sizeDelta = new Vector2(130f, 42f);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 84f), true);
        }

        private void BuildStage1(RectTransform row)
        {
            // 앰버 발광(카드보다 조금 큰 옅은 앰버 사각) + 앰버 테두리 카드
            var holder = UI.Rect(row, "Stage1Holder");
            UI.LE(holder, 372, 532, 0f, 0f, 372, 532);
            var glowRt = UI.Stretch(holder, "Glow", -10, -10, -10, -10);
            _glow = UI.Bg(glowRt, new Color(0.95f, 0.78f, 0.45f, 0.10f), false);
            var cardOuter = UI.Stretch(holder, "Card", 6, 6, 6, 6);
            UI.Bg(cardOuter, Skin.Amber, false);
            var card = UI.Stretch(cardOuter, "Inner", 3, 3, 3, 3);
            UI.Bg(card, Skin.PanelSolid, false);

            // 위: 일러스트 자리(코드 배경으로 침수 박물관 미리보기)
            var art = UI.Rect(card, "Art");
            art.anchorMin = new Vector2(0f, 0.52f); art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;
            UI.Bg(art, Skin.Slot, false);
            var artIn = UI.Stretch(art, "ArtIn");
            artIn.gameObject.AddComponent<RectMask2D>();
            Backgrounds.Draw(artIn, Backgrounds.MuseumDark);
            // 물이 차오른 느낌: 아래쪽 파란 반투명 띠
            var water = UI.Rect(artIn, "Water");
            water.anchorMin = new Vector2(0f, 0f); water.anchorMax = new Vector2(1f, 0.30f);
            water.offsetMin = Vector2.zero; water.offsetMax = Vector2.zero;
            UI.Bg(water, new Color(0.20f, 0.42f, 0.70f, 0.45f), false);
            UI.Label(art, "STAGE 1", 12, Skin.Alpha(Skin.Amber, 0.6f), new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(200f, 18f), TextAnchor.MiddleLeft);

            if (Progress.stage1Cleared)
            {
                string best = Progress.bestSeconds > 0f ? " · " + ResultScreen.FormatTime(Progress.bestSeconds) : "";
                var badge = UI.Fixed(art, "Clear", new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(190f, 30f), new Vector2(1f, 1f));
                UI.Bg(badge, Skin.Amber, false);
                var bt = UI.Label(badge, "★ CLEAR" + best, 14, Skin.DarkText, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 30f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
                bt.fontStyle = FontStyle.Bold;
            }

            // 아래: 글 + PLAY
            var body = UI.Rect(card, "Body");
            body.anchorMin = new Vector2(0f, 0f); body.anchorMax = new Vector2(1f, 0.52f);
            body.offsetMin = Vector2.zero; body.offsetMax = Vector2.zero;
            UI.VStack(body, 24, 8, false, TextAnchor.UpperLeft);
            UI.Text(body, Skin.Spaced("STAGE 1 · PHILIPPINES"), 12, Skin.Dim, TextAnchor.MiddleLeft, false, 18);
            UI.Text(body, "침수된 박물관", 26, Skin.Ink, TextAnchor.MiddleLeft, false, 36);
            UI.Text(body, "태풍으로 물이 차오르는 박물관. 표지판은 필리핀어로만 쓰여 있다.\n네 명이 서로 다른 것을 보고, 서로 다른 것을 할 수 있다.", 13, Skin.Dim, TextAnchor.UpperLeft, true, 60);
            var spacer = UI.Rect(body, "Spacer"); UI.LE(spacer, -1, 4, 0f, 1f);
            var play = Skin.Accent(body, "PLAY", () => LocalNav.Go(LocalScreen.Match), 24, 58, 200);
            UI.LE(play, -1, 58);
        }

        private void BuildLocked(RectTransform row, int stage)
        {
            var inner = Skin.BorderedCell(row, "Stage" + stage, 300, 430, Skin.Border, Skin.PanelSolid, 2f);
            var outer = (RectTransform)inner.parent;

            // 카드 전체를 버튼으로(클릭 → 흔들림 + 안내 토스트)
            var btnImg = outer.GetComponent<Image>();
            btnImg.raycastTarget = true;
            var btn = outer.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.None;
            var shaker = new Shaker { rt = outer, basePos = Vector2.zero, t = 0f };
            _shakers.Add(shaker);
            btn.onClick.AddListener(() =>
            {
                // 레이아웃 그룹이 정해 준 위치를 기준점으로 잡고 흔든다
                if (shaker.t <= 0f) shaker.basePos = outer.anchoredPosition;
                shaker.t = 0.5f;
                if (ScreenManager.Instance != null)
                    ScreenManager.Instance.ShowToast("아직 열리지 않은 봉투예요 — Stage 1을 먼저 클리어해요", GameLogic.ToneWarn);
            });

            // TOP SECRET 도장(살짝 기울어진 빨간 테두리 사각)
            var stampOuter = UI.Fixed(inner, "Stamp", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(168f, 168f), new Vector2(0.5f, 0.5f));
            stampOuter.localRotation = Quaternion.Euler(0f, 0f, 12f);
            UI.Bg(stampOuter, Skin.Alpha(Skin.Red, 0.85f), false);
            var stampIn = UI.Stretch(stampOuter, "In", 3, 3, 3, 3);
            UI.Bg(stampIn, Skin.PanelSolid, false);
            var st = UI.Label(stampIn, "TOP\nSECRET", 22, Skin.Alpha(Skin.Red, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 100f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            st.fontStyle = FontStyle.Bold;

            // 자물쇠(고리 + 몸통)
            var shackle = UI.Fixed(inner, "Shackle", new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(30f, 30f), new Vector2(0.5f, 0.5f));
            var si = shackle.gameObject.AddComponent<Image>();
            si.sprite = SpriteLib.Ring(0.22f); si.color = new Color(0.55f, 0.62f, 0.75f); si.raycastTarget = false;
            var lockBody = UI.Fixed(inner, "LockBody", new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(40f, 30f), new Vector2(0.5f, 0.5f));
            UI.Bg(lockBody, new Color(0.55f, 0.62f, 0.75f), false);
            var keyhole = UI.Fixed(lockBody, "Keyhole", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, 12f), new Vector2(0.5f, 0.5f));
            UI.Bg(keyhole, Skin.PanelSolid, false);

            UI.Label(inner, Skin.Spaced("STAGE " + stage), 15, Skin.Dim, new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(260f, 24f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
            UI.Label(inner, $"{LockedNations[stage]} · 미정", 13, Skin.Alpha(Skin.Dim, 0.7f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(260f, 22f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
        }

        public override void Tick()
        {
            _t += Time.deltaTime;
            if (_glow != null) _glow.color = new Color(0.95f, 0.78f, 0.45f, 0.08f + 0.06f * (0.5f + 0.5f * Mathf.Sin(_t * 2.4f)));
            foreach (var s in _shakers)
            {
                if (s.rt == null) continue;
                if (s.t > 0f)
                {
                    s.t -= Time.deltaTime;
                    float k = Mathf.Max(0f, s.t / 0.5f);
                    float dx = Mathf.Sin(s.t * 60f) * 9f * k;
                    s.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(s.t * 50f) * 2.5f * k);
                    s.rt.anchoredPosition = s.basePos + new Vector2(dx, 0f);
                    if (s.t <= 0f) { s.rt.localRotation = Quaternion.identity; s.rt.anchoredPosition = s.basePos; }
                }
            }
        }
    }
}
