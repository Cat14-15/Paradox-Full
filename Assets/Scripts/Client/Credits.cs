using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>만든 사람들 · 출처 목록(엔딩 크레딧과 타이틀 설정 창이 같이 쓴다).</summary>
    public static class Credits
    {
        public static readonly string[] Lines =
        {
        "STRANGER",
        "",
        "PARALLAX 팀  ·  2026 유네스코학교 프로젝트",
        "",
        "만든 사람들",
        "김은호  ·  기획, 맵 등 디자인, 게임 개발",
        "김민건  ·  기획, 맵 등 디자인",
        "김현우  ·  기획",
        "신은총  ·  기획",
        "이시은  ·  기획",
        "강준영  ·  기획",
        "이재준  ·  기획",
        "",
        "도움 받은 말",
        "필리핀어 · 베트남어 · 줄루어 인사와 숫자",
        "(베트남어는 도트 글꼴 때문에 성조 표시를 뺐습니다)",
        "",
        "음악 (CC0 · OpenGameArt)",
        "The Budding of Consciousness : fengyu970",
        "Forget Me Not : mentassss",
        "Apple Cider · Happy Adventure (Loop) · Mysterious Ambience (song21)",
        "",
        "효과음 (CC0)",
        "Kenney : UI Audio · RPG Audio · Interface Sounds",
        "rubberduck : 100 CC0 SFX",
        "Freesound : sonically_sound, mpaol2023, joedeshon, gokalp_gonen,",
        "yottasounds, myrinvp, claravinleb, khenshom, audible-edge,",
        "Deathscyp, jcdecha, EskildNP, cyborgcollective",
        "",
        "그림",
        "단풍이  ·  우리 학교 고양이 (특별 출연)",
        "LPC (Liberated Pixel Cup) : Lanea Zimmerman, Stephen Challener,",
        "Charles Sanchez, Manuel Riecke, Daniel Armstrong 외 (CC-BY-SA 3.0 / OGA-BY 3.0)",
        "Big 32x32 Tileset : Pxl Dev, Ommtdh, Cherin, Kapa",
        "Furniture 32x32 (OpenGameArt, CC0)",
        "",
        "온라인  Photon Fusion (Exit Games)",
        "엔진  Unity",
        "",
        "",
        "함께 플레이해 주셔서 고맙습니다.",
        "Salamat · Cam on · Ngiyabonga · 고마워요",
        };

        public static bool IsHead(string line) => line == "STRANGER";
        public static bool IsSection(string line) => line == "만든 사람들" || line.StartsWith("음악") || line.StartsWith("효과음") || line == "그림" || line == "도움 받은 말";
    }

    /// <summary>
    /// "만든 사람들" 창(타이틀 → 설정). 화면 위에 따로 캔버스를 띄운다.
    /// 조작: 마우스 휠 · ↑ ↓ 스크롤, X · Esc · 닫기 단추로 닫기. 처음엔 천천히 저절로 올라간다(엔딩 크레딧처럼).
    /// </summary>
    public sealed class CreditsPanel : MonoBehaviour
    {
        private static CreditsPanel _inst;
        public static bool IsOpen => _inst != null;
        public static int ClosedFrame = -10;

        public static void Open()
        {
            if (_inst != null) return;
            _inst = new GameObject("CreditsPanel").AddComponent<CreditsPanel>();
        }

        private static readonly Color Ink = new Color(0.93f, 0.96f, 0.97f), Mint = new Color(0.376f, 0.984f, 0.796f),
            Amber = new Color(0.949f, 0.773f, 0.447f), Bd = new Color(0.62f, 0.5f, 0.3f), Soft = new Color(0.78f, 0.84f, 0.88f);

        private RectTransform _panel, _view, _content, _close;
        private float _y, _contentH, _auto = 1f;
        private int _openFrame;
        private const float ViewH = 640f;

        private void Start()
        {
            _openFrame = Time.frameCount;
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 310;
            var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)transform;
            var back = Node("Back", root); Stretch(back, 0f);
            var bi = back.gameObject.AddComponent<Image>(); bi.color = new Color(0, 0, 0, 0.85f);

            _panel = Node("Panel", root); _panel.sizeDelta = new Vector2(1400, 900);
            var frame = Node("Frame", _panel); Stretch(frame, -4f); var fi = frame.gameObject.AddComponent<Image>(); fi.color = Bd; fi.raycastTarget = false;
            var inner = Node("Inner", _panel); Stretch(inner, 0f); var ii = inner.gameObject.AddComponent<Image>(); ii.color = new Color(0.05f, 0.06f, 0.09f, 1f);
            Text(_panel, "만든 사람들", 4, Amber, 0, 385, 800, 70);

            // 스크롤 영역(가린 창 안에서 목록이 움직인다)
            _view = Node("View", _panel); _view.anchoredPosition = new Vector2(0, 20); _view.sizeDelta = new Vector2(1320, ViewH);
            _view.gameObject.AddComponent<RectMask2D>();
            _content = Node("Content", _view); _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 1f); _content.pivot = new Vector2(0.5f, 1f);
            float y = 0f;
            foreach (string line in Credits.Lines)
            {
                bool head = Credits.IsHead(line), section = Credits.IsSection(line);
                var t = Text(_content, line, head ? 4 : 2, head ? Mint : section ? Amber : Ink, 0, 0, 1280, head ? 80f : 44f);
                var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f); r.anchoredPosition = new Vector2(0, -y);
                y += head ? 96f : 48f;
            }
            _contentH = y;
            _content.sizeDelta = new Vector2(1280, y);
            Text(_panel, "휠 · ↑ ↓  넘겨 보기      X  닫기", 2, Soft, 0, -345, 1200, 36);
            _close = Node("Close", _panel); _close.anchoredPosition = new Vector2(0, -405); _close.sizeDelta = new Vector2(260, 60);
            var cimg = _close.gameObject.AddComponent<Image>(); cimg.color = new Color(0.09f, 0.11f, 0.15f, 1f);
            Text(_close, "닫기  X", 2, Ink, 0, 0, 240, 60);
        }

        private void Update()
        {
            float cw = StoryText.CanvasWidth(_panel), ch = ((RectTransform)transform).rect.height;
            float s = Mathf.Clamp(Mathf.Min((cw - 32f) / 1420f, (ch - 32f) / 920f), 0.4f, 1f);
            _panel.localScale = new Vector3(s, s, 1f);
            if (Time.frameCount <= _openFrame) return;
            var kb = Keyboard.current; var ms = Mouse.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)) { Close(); return; }
            if (ms != null && ms.leftButton.wasPressedThisFrame && StoryText.Over(_close)) { Close(); return; }
            float move = 0f;
            if (kb != null) { if (kb.downArrowKey.isPressed) move += 600f * Time.unscaledDeltaTime; if (kb.upArrowKey.isPressed) move -= 600f * Time.unscaledDeltaTime; }
            if (ms != null) { float w = ms.scroll.ReadValue().y; if (Mathf.Abs(w) > 0.01f) move -= Mathf.Sign(w) * 48f; }
            if (move != 0f) _auto = 0f;
            else if (_auto > 0f) move = 40f * Time.unscaledDeltaTime;   // 손대기 전에는 천천히 저절로
            _y = Mathf.Clamp(_y + move, 0f, Mathf.Max(0f, _contentH - ViewH + 40f));
            _content.anchoredPosition = new Vector2(0, _y);
        }

        private void Close()
        {
            ClosedFrame = Time.frameCount;
            if (_inst == this) _inst = null;
            Paradox.Core.Sfx.Play("ui_back", 0.7f);
            Destroy(gameObject);
        }

        private void OnDestroy() { if (_inst == this) _inst = null; }

        private static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r, float inset) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset); }

        private static PixelText Text(RectTransform parent, string s, int scale, Color c, float x, float y, float w, float h)
        {
            var r = Node("T", parent); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
            var t = r.gameObject.AddComponent<PixelText>(); t.scale = scale; t.align = TextAnchor.MiddleCenter; t.color = c; t.text = s; t.raycastTarget = false; t.maxWidth = w;
            return t;
        }
    }
}
