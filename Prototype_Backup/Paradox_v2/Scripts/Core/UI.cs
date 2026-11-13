using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Paradox.Core
{
    /// <summary>
    /// UGUI를 코드로 빠르게 만드는 팩토리. 모든 화면/HUD가 이 헬퍼로 구성된다.
    /// (프리팹 없이 런타임 생성 → 에디터 배선 불필요)
    /// </summary>
    public static class UI
    {
        public static readonly Color Ink = new Color(0.95f, 0.96f, 0.98f);
        public static readonly Color InkDim = new Color(0.70f, 0.74f, 0.80f);
        public static readonly Color PanelDark = new Color(0.08f, 0.09f, 0.13f, 0.94f);
        public static readonly Color PanelMid = new Color(0.13f, 0.15f, 0.20f, 0.96f);
        public static readonly Color BtnColor = new Color(0.24f, 0.27f, 0.34f);
        public static readonly Color BtnAccent = new Color(0.30f, 0.45f, 0.62f);
        public static readonly Color BtnGood = new Color(0.25f, 0.55f, 0.38f);
        public static readonly Color BtnWarn = new Color(0.72f, 0.28f, 0.25f);
        public static readonly Color Gold = new Color(1f, 0.90f, 0.55f);

        public static Font Font => UIFontHelper.Load();

        // ---------- 기본 컨테이너 ----------

        public static RectTransform Rect(Transform parent, string name = "Rect")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>부모를 꽉 채우는 사각형(오프셋 지정 가능).</summary>
        public static RectTransform Stretch(Transform parent, string name = "Stretch", float l = 0, float b = 0, float r = 0, float t = 0)
        {
            var rt = Rect(parent, name);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>앵커 기준으로 위치·크기가 고정된 사각형.</summary>
        public static RectTransform Fixed(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var rt = Rect(parent, name);
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Bg(RectTransform rt, Color color, bool raycast = true)
        {
            var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static RectTransform Panel(Transform parent, string name, Color bg, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var rt = Fixed(parent, name, anchor, pos, size, pivot);
            Bg(rt, bg);
            return rt;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, int pad = 12, float spacing = 8, bool expandH = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = expandH;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, int pad = 0, float spacing = 8, bool expandW = true, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = expandW; h.childForceExpandHeight = true;
            return h;
        }

        public static GridLayoutGroup Grid(RectTransform rt, Vector2 cell, Vector2 spacing, int columns = 0)
        {
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = spacing;
            g.childAlignment = TextAnchor.UpperLeft;
            if (columns > 0) { g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns; }
            return g;
        }

        public static LayoutElement LE(Component c, float minW = -1, float minH = -1, float flexW = -1, float flexH = -1, float prefW = -1, float prefH = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (minW >= 0) le.minWidth = minW;
            if (minH >= 0) le.minHeight = minH;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (prefH >= 0) le.preferredHeight = prefH;
            return le;
        }

        public static ContentSizeFitter Fit(Component c, bool horizontal, bool vertical)
        {
            var f = c.gameObject.GetComponent<ContentSizeFitter>() ?? c.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        // ---------- 텍스트 ----------

        public static Text Text(Transform parent, string content, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true, float minH = -1)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = content; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (minH >= 0) LE(t, -1, minH);
            return t;
        }

        /// <summary>앵커 기준 위치가 고정된 라벨(레이아웃 그룹 밖에서 사용).</summary>
        public static Text Label(Transform parent, string content, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, TextAnchor align = TextAnchor.UpperLeft, Vector2? pivot = null)
        {
            var rt = Fixed(parent, "Label", anchor, pos, sizeDelta, pivot);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = content; t.fontSize = size; t.color = color; t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ---------- 버튼 / 입력 ----------

        public static Button Button(Transform parent, string label, Color bg, UnityAction onClick, int fontSize = 18, float minH = 44, float minW = 80)
        {
            var rt = Rect(parent, "Button");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            colors.colorMultiplier = 1f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            LE(btn, minW, minH);

            var trt = Stretch(rt, "Label", 6, 2, 6, 2);
            var t = trt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = label; t.fontSize = fontSize; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return btn;
        }

        public static Text ButtonLabel(Button b) => b.GetComponentInChildren<Text>();

        public static void SetButtonColor(Button b, Color c)
        {
            var img = b.targetGraphic as Image;
            if (img != null) img.color = c;
        }

        public static InputField Input(Transform parent, string placeholder, string initial = "", int fontSize = 17, int charLimit = 0)
        {
            var rt = Rect(parent, "Input");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.18f, 0.20f, 0.25f);
            LE(rt, 120, 42);

            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = img;
            input.characterLimit = charLimit;

            var prt = Stretch(rt, "Placeholder", 10, 6, 10, 6);
            var pt = prt.gameObject.AddComponent<Text>();
            pt.font = Font; pt.fontSize = fontSize; pt.color = new Color(0.6f, 0.62f, 0.66f);
            pt.alignment = TextAnchor.MiddleLeft; pt.text = placeholder; pt.supportRichText = false;

            var trt = Stretch(rt, "Text", 10, 6, 10, 6);
            var t = trt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = fontSize; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft; t.supportRichText = false;

            input.textComponent = t;
            input.placeholder = pt;
            input.text = initial;
            return input;
        }

        public static Image Image(Transform parent, Sprite sprite, Color color, Vector2 size)
        {
            var rt = Rect(parent, "Image");
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.preserveAspect = true; img.raycastTarget = false;
            LE(img, size.x, size.y, 0, 0, size.x, size.y);
            return img;
        }

        /// <summary>가로 진행 바(배경 + 채움). 채움 Image를 돌려주고 fillAmount로 갱신한다.</summary>
        public static Image Bar(Transform parent, Color back, Color fill, float height = 16f)
        {
            var rt = Rect(parent, "Bar");
            Bg(rt, back, false);
            LE(rt, 80, height, 1, 0);
            var frt = Stretch(rt, "Fill", 2, 2, 2, 2);
            var f = frt.gameObject.AddComponent<Image>();
            f.color = fill; f.raycastTarget = false;
            f.type = UnityEngine.UI.Image.Type.Filled;
            f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            f.fillAmount = 0f;
            f.sprite = SpriteLib.White;
            return f;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static string Colored(string s, Color c) => $"<color=#{Hex(c)}>{s}</color>";

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
