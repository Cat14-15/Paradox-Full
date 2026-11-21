using Paradox.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 타이틀 메뉴(PDF s02). 좌측 로고, 세로 버튼 3개(■ 시작하기 / 이어하기 / 설정), 우하단 버전 표기.
    /// 시작하기 → 프롤로그, 이어하기(프롤로그를 봤거나 Stage 1 클리어) → 스테이지 선택, 설정 → 설정.
    /// </summary>
    public class TitleScreen : ScreenBase
    {
        private Button _continue;
        private Image _lantern;
        private float _t;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            Skin.Vignette(Root, new Vector2(0f, 0.5f), new Vector2(260f, 120f), new Vector2(1100f, 900f), 0.14f);

            // 좌측 로고
            _lantern = LogoScreen.MakeLogo(Root, new Vector2(0f, 0.5f), new Vector2(360f, 150f), 0.82f);

            // 세로 버튼 3개
            var menu = UI.Fixed(Root, "Menu", new Vector2(0f, 0.5f), new Vector2(110f, -110f), new Vector2(370f, 230f), new Vector2(0f, 0.5f));
            UI.VStack(menu, 0, 14, false, TextAnchor.UpperLeft);

            var start = MenuButton(menu, "시작하기", true, () => LocalNav.Go(LocalScreen.Prologue));
            _continue = MenuButton(menu, "이어하기", false, () => LocalNav.Go(LocalScreen.StageSelect));
            MenuButton(menu, "설정", false, () => LocalNav.Go(LocalScreen.Settings));

            UI.Label(Root, "v1.0 · 4인 협동", 13, Skin.Dim,
                new Vector2(1f, 0f), new Vector2(-36f, 22f), new Vector2(400f, 22f), TextAnchor.MiddleRight, new Vector2(1f, 0f));
            UI.Label(Root, "부산소프트웨어마이스터고 · UNESCO 청소년 프로젝트", 12, Skin.Alpha(Skin.Dim, 0.7f),
                new Vector2(0f, 0f), new Vector2(36f, 22f), new Vector2(600f, 22f), TextAnchor.MiddleLeft, new Vector2(0f, 0f));

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 60f), true);
            OnState();
        }

        /// <summary>■ 표시가 앞에 붙는 넓은 메뉴 버튼. accent 면 앰버 테두리 + 앰버 ■ + 어두운 앰버 속.</summary>
        private static Button MenuButton(Transform parent, string label, bool accent, UnityEngine.Events.UnityAction onClick)
        {
            var b = Skin.Outline(parent, "", onClick, 22, 62, 370,
                accent ? Skin.Amber : Skin.Border, Skin.Ink);
            var inner = b.transform.Find("Inner") as RectTransform;
            if (inner != null)
            {
                var bg = inner.GetComponent<Image>();
                if (bg != null) bg.color = accent ? new Color(0.16f, 0.12f, 0.06f, 1f) : Skin.PanelSolid;
                var lbl = inner.Find("Label") as RectTransform;
                if (lbl != null) { lbl.offsetMin = new Vector2(52f, 2f); }
                var sq = UI.Fixed(inner, "Square", new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(14f, 14f), new Vector2(0f, 0.5f));
                UI.Bg(sq, accent ? Skin.Amber : new Color(0.33f, 0.40f, 0.55f), false);
            }
            var t = UI.ButtonLabel(b);
            if (t != null)
            {
                t.text = label;
                t.alignment = TextAnchor.MiddleLeft;
                t.color = accent ? Skin.Amber : Skin.Ink;
                if (accent) t.fontStyle = FontStyle.Bold;
            }
            return b;
        }

        public override void OnState()
        {
            if (_continue != null)
            {
                bool can = Progress.CanContinue;
                _continue.interactable = can;
                var t = UI.ButtonLabel(_continue);
                if (t != null)
                {
                    t.text = can ? (Progress.stage1Cleared ? "이어하기  <size=13><color=#8A96A8>Stage 1 클리어</color></size>" : "이어하기") : "이어하기  <size=13><color=#5A6478>(기록 없음)</color></size>";
                    t.color = can ? Skin.Ink : Skin.Dim;
                }
            }
        }

        public override void Tick()
        {
            _t += Time.deltaTime;
            if (_lantern != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_t * 2.0f);
                _lantern.color = new Color(0.95f, 0.78f, 0.45f, 0.26f + 0.12f * k);
            }
            var kb = Keyboard.current;
            if (kb != null && !InteractionState.InputLocked && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                LocalNav.Go(LocalScreen.Prologue);
        }
    }
}
