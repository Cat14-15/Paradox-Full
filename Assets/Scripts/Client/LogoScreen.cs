using Paradox.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 로고 인트로(PDF s01). 앰버 비네트 + 코드로 그린 로고(STRANGER 글자 · 랜턴 빛 · 발자국) + 크레딧.
    /// 3.5초 뒤 자동으로, 또는 아무 키/클릭으로 타이틀로 넘어간다. LocalNav.SkipLogo 면 첫 프레임에 바로 넘어간다.
    /// </summary>
    public class LogoScreen : ScreenBase
    {
        public const float AutoSeconds = 3.5f;

        private float _t;
        private bool _done;
        private CanvasGroup _group;
        private Image _lantern;
        private Text _hint;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            Skin.Vignette(Root, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1500f, 1050f), 0.30f);
            Skin.Vignette(Root, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 520f), 0.16f);

            var content = UI.Stretch(Root, "Content");
            _group = content.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            _lantern = MakeLogo(content, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), 1f);

            UI.Label(content, Skin.Spaced("PARALLAX · 2026 유네스코학교 교육의 미래 프로젝트"), 14, Skin.AmberDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1000f, 26f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            _hint = UI.Label(content, "아무 키나 누르면 넘어갑니다", 14, Skin.Dim,
                new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(500f, 24f), TextAnchor.MiddleCenter);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 90f), true);
        }

        /// <summary>
        /// 코드로 그린 로고 묶음: 랜턴 빛(원) + 손잡이 + "S T R A N G E R" + 발자국 타원 6개.
        /// 타이틀 화면도 같은 로고를 쓴다. 반환값은 랜턴 빛 Image(펄스 연출용).
        /// </summary>
        private static Texture2D _logoTex; private static bool _logoLooked;
        /// <summary>사용자 제작 로고 그림(Resources/Logo/stranger_logo). 없으면 null.</summary>
        public static Texture2D LogoTexture
        {
            get
            {
                if (!_logoLooked) { _logoLooked = true; _logoTex = Resources.Load<Texture2D>("Logo/stranger_logo"); }
                return _logoTex;
            }
        }

        public static Image MakeLogo(Transform parent, Vector2 anchor, Vector2 pos, float scale)
        {
            var root = UI.Fixed(parent, "Logo", anchor, pos, new Vector2(640f * scale, 260f * scale), new Vector2(0.5f, 0.5f));

            // 랜턴 빛: 큰 부드러운 원 + 밝은 심지
            var glowRt = UI.Fixed(root, "LanternGlow", new Vector2(0.5f, 0.5f), new Vector2(-250f * scale, 40f * scale), new Vector2(260f * scale, 260f * scale), new Vector2(0.5f, 0.5f));
            var glow = glowRt.gameObject.AddComponent<Image>();
            glow.sprite = SpriteLib.Disc(0.95f); glow.color = new Color(0.95f, 0.78f, 0.45f, 0.35f); glow.raycastTarget = false;
            var coreRt = UI.Fixed(root, "LanternCore", new Vector2(0.5f, 0.5f), new Vector2(-250f * scale, 40f * scale), new Vector2(46f * scale, 46f * scale), new Vector2(0.5f, 0.5f));
            var core = coreRt.gameObject.AddComponent<Image>();
            core.sprite = SpriteLib.Disc(0.3f); core.color = new Color(1f, 0.93f, 0.72f, 0.95f); core.raycastTarget = false;
            // 랜턴 몸통(작은 사각 틀 + 손잡이 고리)
            var frame = UI.Fixed(root, "LanternFrame", new Vector2(0.5f, 0.5f), new Vector2(-250f * scale, 40f * scale), new Vector2(34f * scale, 50f * scale), new Vector2(0.5f, 0.5f));
            UI.Bg(frame, new Color(0.18f, 0.13f, 0.06f, 0.0f), false);
            var top = UI.Fixed(frame, "Top", new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(34f * scale, 5f * scale), new Vector2(0.5f, 1f));
            UI.Bg(top, new Color(0.22f, 0.16f, 0.08f), false);
            var bottom = UI.Fixed(frame, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(34f * scale, 5f * scale), new Vector2(0.5f, 0f));
            UI.Bg(bottom, new Color(0.22f, 0.16f, 0.08f), false);
            var ringRt = UI.Fixed(frame, "Handle", new Vector2(0.5f, 1f), new Vector2(0f, 14f * scale), new Vector2(22f * scale, 22f * scale), new Vector2(0.5f, 0.5f));
            var ring = ringRt.gameObject.AddComponent<Image>();
            ring.sprite = SpriteLib.Ring(0.16f); ring.color = new Color(0.30f, 0.22f, 0.10f); ring.raycastTarget = false;

            // 글자 — Resources/Logo/stranger_logo.png(사용자 제작 로고, 투명 배경)가 있으면 그 그림, 없으면 코드 글자
            var logoTex = LogoTexture;
            if (logoTex != null)
            {
                float w = 560f * scale, h = w * logoTex.height / Mathf.Max(1, logoTex.width);
                var lrt = UI.Fixed(root, "LogoImage", new Vector2(0.5f, 0.5f), new Vector2(60f * scale, 40f * scale), new Vector2(w, h), new Vector2(0.5f, 0.5f));
                var limg = lrt.gameObject.AddComponent<Image>();
                limg.sprite = Sprite.Create(logoTex, new Rect(0, 0, logoTex.width, logoTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                limg.preserveAspect = true; limg.raycastTarget = false;
            }
            else
            {
                // 글자(가로 여유를 넉넉히 — UI.Label 은 항상 줄바꿈하므로 좁으면 두 줄이 된다)
                var title = UI.Label(root, Skin.Spaced("STRANGER"), Mathf.RoundToInt(60f * scale), Skin.Amber,
                    new Vector2(0.5f, 0.5f), new Vector2(40f * scale, 40f * scale), new Vector2(620f * scale, 90f * scale), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
                title.fontStyle = FontStyle.Bold;
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                var shadow = UI.Label(root, Skin.Spaced("STRANGER"), Mathf.RoundToInt(60f * scale), new Color(0f, 0f, 0f, 0.55f),
                    new Vector2(0.5f, 0.5f), new Vector2(43f * scale, 36f * scale), new Vector2(620f * scale, 90f * scale), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
                shadow.fontStyle = FontStyle.Bold;
                shadow.horizontalOverflow = HorizontalWrapMode.Overflow;
                shadow.transform.SetSiblingIndex(title.transform.GetSiblingIndex());
            }
            UI.Label(root, "낯선 말, 하나가 되는 길", Mathf.RoundToInt(16f * scale), Skin.Dim,
                new Vector2(0.5f, 0.5f), new Vector2(40f * scale, -20f * scale), new Vector2(620f * scale, 26f * scale), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));

            // 발자국 타원 6개: 글자 아래에서 오른쪽 위로 걸어 나간다(좌·우 발 번갈아, 갈수록 옅게)
            for (int i = 0; i < 6; i++)
            {
                float x = (-120f + i * 58f) * scale;
                float y = (-70f + i * 9f) * scale + (i % 2 == 0 ? 0f : 14f * scale);
                var fp = UI.Fixed(root, "Footprint", new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(13f * scale, 22f * scale), new Vector2(0.5f, 0.5f));
                fp.localRotation = Quaternion.Euler(0f, 0f, -18f);
                var img = fp.gameObject.AddComponent<Image>();
                img.sprite = SpriteLib.Disc(0.2f);
                img.color = new Color(0.95f, 0.78f, 0.45f, 0.55f - i * 0.07f);
                img.raycastTarget = false;
                var toe = UI.Fixed(root, "Toe", new Vector2(0.5f, 0.5f), new Vector2(x + 3f * scale, y + 14f * scale), new Vector2(7f * scale, 7f * scale), new Vector2(0.5f, 0.5f));
                var timg = toe.gameObject.AddComponent<Image>();
                timg.sprite = SpriteLib.Disc(0.2f);
                timg.color = new Color(0.95f, 0.78f, 0.45f, 0.45f - i * 0.06f);
                timg.raycastTarget = false;
            }
            return glow;
        }

        private void Finish()
        {
            if (_done) return;
            _done = true;
            LocalNav.Go(LocalScreen.Title);
        }

        public override void Tick()
        {
            if (_done) return;
            if (LocalNav.SkipLogo) { Finish(); return; }
            _t += Time.deltaTime;
            if (_group != null) _group.alpha = Mathf.Clamp01(_t / 0.7f);
            if (_lantern != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_t * 2.2f);
                _lantern.color = new Color(0.95f, 0.78f, 0.45f, 0.28f + 0.14f * k);
            }
            if (_hint != null) _hint.color = new Color(Skin.Dim.r, Skin.Dim.g, Skin.Dim.b, 0.5f + 0.5f * Mathf.Sin(_t * 3f));

            if (_t >= AutoSeconds) { Finish(); return; }
            if (_t < 0.25f) return; // 화면이 뜨자마자 눌린 키는 무시
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if ((kb != null && kb.anyKey.wasPressedThisFrame) || StoryText.Tap()) Finish();   // 마우스·터치 공통
        }
    }
}
