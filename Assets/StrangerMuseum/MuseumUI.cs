using System;
using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MainStory1 의 화면 부품 모음. 프롤로그·캠프 대화창과 같은 그림(panel_fill / panel_border)과 도트 글꼴(PixelText)만 쓴다.
/// 웹 빌드에는 OS 글꼴이 없어서 OnGUI 글자는 한글이 안 보인다 → 이 장면의 글자는 전부 여기 부품으로 그린다.
/// 캔버스는 1920x1080 기준으로 늘어나며(가로·세로 절반씩 맞춤), 좌표는 모두 그 기준 단위다.
/// </summary>
public static class MuseumUI
{
    public static readonly Color Ink = new Color(0.94f, 0.95f, 0.96f);
    public static readonly Color Amber = new Color(0.949f, 0.773f, 0.447f);
    public static readonly Color Slate = new Color(0.58f, 0.63f, 0.69f);
    public static readonly Color Mint = new Color(0.45f, 0.93f, 0.72f);
    public static readonly Color Red = new Color(0.95f, 0.42f, 0.38f);
    public static readonly Color Fill = new Color(0.05f, 0.06f, 0.09f, 1f);
    public static readonly Color Gold = new Color(0.62f, 0.5f, 0.3f);
    public static readonly Color Teal = new Color(0.3f, 0.52f, 0.62f);

    private static Sprite fill, border;
    public static Sprite FillSprite { get { if (fill == null) fill = Resources.Load<Sprite>("StoryUI/panel_fill"); return fill; } }
    public static Sprite BorderSprite { get { if (border == null) border = Resources.Load<Sprite>("StoryUI/panel_border"); return border; } }

    public static Canvas MakeCanvas(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var cv = go.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = order;
        cv.pixelPerfect = false;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        // 창 비율이 16:9 와 달라도 1920×1080 전체가 화면 안에 들어오게(좁은 브라우저에서 대사가 양옆으로 잘리던 것)
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return cv;
    }

    public static RectTransform Node(string name, Transform parent)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        return r;
    }

    /// <summary>앵커·피벗을 한 점(ax, ay)에 두고 그 점 기준 위치·크기를 정한다.</summary>
    public static RectTransform Anchor(RectTransform r, float ax, float ay, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(ax, ay);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    public static RectTransform At(RectTransform r, float x, float y, float w, float h) { r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); return r; }

    public static void Stretch(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    public static RectTransform Stretched(RectTransform r, float inset = 0f) { Stretch(r, inset); return r; }

    public static Image Img(RectTransform r, Sprite s, Color c)
    {
        var i = r.gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false;
        return i;
    }

    public static Image Sliced(RectTransform r, Sprite s, Color c)
    {
        var i = Img(r, s, c); i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f / 3f;
        return i;
    }

    /// <summary>프롤로그 대화창과 같은 판(어두운 바탕 + 금색 테두리). 테두리 Image 를 돌려준다.</summary>
    public static Image Panel(RectTransform r, Color? fillColor = null, Color? borderColor = null)
    {
        var back = Node("Back", r); Stretch(back); Sliced(back, FillSprite, fillColor ?? Fill);
        var bd = Node("Border", r); Stretch(bd);
        return Sliced(bd, BorderSprite, borderColor ?? Gold);
    }

    public static PixelText Text(Transform parent, string name, string text, int scale, Color color, TextAnchor align)
    {
        var r = Node(name, parent);
        var t = r.gameObject.AddComponent<PixelText>();
        t.scale = scale; t.align = align; t.color = color; t.text = text ?? ""; t.raycastTarget = false;
        return t;
    }

    public static PixelText Text(Transform parent, string name, string text, int scale, Color color, TextAnchor align, float x, float y, float w, float h)
    {
        var t = Text(parent, name, text, scale, color, align);
        At(t.rectTransform, x, y, w, h);
        t.maxWidth = w;
        return t;
    }

    /// <summary>도트 글꼴 폭(캔버스 단위) 기준으로 줄을 나눈다. 띄어쓰기 단위, 너무 긴 낱말은 글자 단위.</summary>
    public static string Wrap(string s, float width, int scale)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int limit = Mathf.Max(16, Mathf.FloorToInt(width / Mathf.Max(1, scale)));
        var sb = new System.Text.StringBuilder();
        foreach (string para in s.Split('\n'))
        {
            if (sb.Length > 0) sb.Append('\n');
            int lineW = 0;
            foreach (string word in para.Split(' '))
            {
                int ww = PixelFontData.Measure(word);
                int sp = PixelFontData.Advance(' ');
                if (lineW > 0 && lineW + sp + ww > limit) { sb.Append('\n'); lineW = 0; }
                else if (lineW > 0) { sb.Append(' '); lineW += sp; }
                if (ww > limit)
                {
                    foreach (char c in word)
                    {
                        int a = PixelFontData.Advance(c);
                        if (lineW + a > limit) { sb.Append('\n'); lineW = 0; }
                        sb.Append(c); lineW += a;
                    }
                }
                else { sb.Append(word); lineW += ww; }
            }
        }
        return sb.ToString();
    }

    public static int Lines(string s) { int n = 1; foreach (char c in s) if (c == '\n') n++; return n; }

    /// <summary>폭에 맞춰 줄을 나눠 넣는다(글씨를 줄이지 않는다).</summary>
    public static void SetWrapped(PixelText t, string s, float width)
    {
        t.maxWidth = 0f;
        t.text = Wrap(s ?? "", width, t.scale);
    }

    /// <summary>글자 줄 수에 맞는 높이(캔버스 단위). PixelText 의 줄 간격(4도트)을 따른다.</summary>
    public static float TextHeight(string s, int scale) { int n = Lines(s); return (n * PixelFontData.Cell + (n - 1) * 4) * scale; }

    // ---------------------------------------------------------------- 단추
    private static readonly List<PxButton> buttons = new List<PxButton>();
    private static int lastFrame = -1;
    private static PxButton hovered;

    public static PxButton Button(Transform parent, string name, string label, int scale, Action onClick)
    {
        var r = Node(name, parent);
        var b = new PxButton(r, label, scale, onClick);
        buttons.Add(b);
        return b;
    }

    public static void Forget(PxButton b) { buttons.Remove(b); }

    /// <summary>마우스가 지금 어떤 단추 위에 있는가(월드 클릭을 막을 때 쓴다).</summary>
    public static bool PointerOverButton { get; private set; }

    /// <summary>true 면 단추를 누를 수 없다(세로 화면 안내가 덮고 있을 때).</summary>
    public static bool InputBlocked;

    // ---------------------------------------------------------------- 포인터(마우스 + 터치 공용)
    // 웹 모바일에서는 터치가 마우스로 흉내 나지 않을 수 있다 → 마우스와 터치를 같이 읽는다.
    // 터치 직후 브라우저가 흉내 낸 마우스 클릭이 한 번 더 오면 두 번 눌리지 않게 잠깐 마우스 클릭을 무시한다.
    private static readonly List<Vector2> pressList = new List<Vector2>();
    private static int pressFrame = -1;
    private static float lastTouchAt = -99f;

    /// <summary>터치를 한 번이라도 썼는가(터치 조작 단추를 보일지 정할 때).</summary>
    public static bool TouchUsed => lastTouchAt > 0f;

    private static void CollectPresses()
    {
        if (pressFrame == Time.frameCount) return;
        pressFrame = Time.frameCount;
        pressList.Clear();
        if (InputBlocked) return;
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts != null)
            foreach (var t in ts.touches)
                if (t.press.wasPressedThisFrame) { pressList.Add(t.position.ReadValue()); lastTouchAt = Time.unscaledTime; }
        var ms = Mouse.current;
        if (ms != null && ms.leftButton.wasPressedThisFrame && Time.unscaledTime - lastTouchAt > 0.6f) pressList.Add(ms.position.ReadValue());
    }

    /// <summary>이번 프레임에 새로 누른 자리들(화면 좌표, 왼쪽 아래 기준). 마우스 왼쪽 + 새 터치.</summary>
    public static List<Vector2> Presses { get { CollectPresses(); return pressList; } }

    /// <summary>이번 프레임에 화면을 눌렀는가(마우스 왼쪽 또는 새 터치). 첫 자리를 돌려준다.</summary>
    public static bool PointerPressed(out Vector2 pos)
    {
        CollectPresses();
        pos = pressList.Count > 0 ? pressList[0] : new Vector2(-9999, -9999);
        return pressList.Count > 0;
    }

    /// <summary>지금 누르고 있는가(마우스 왼쪽 또는 아무 터치).</summary>
    public static bool PointerHeld
    {
        get
        {
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) return true;
            var ms = Mouse.current;
            return ms != null && ms.leftButton.isPressed;
        }
    }

    /// <summary>
    /// 창 밖을 눌렀는가: 이번 프레임에 누른 자리 중 rect 밖이면서 단추·except 위가 아닌 것이 있으면 true.
    /// (퍼즐 창 '창 밖 누르면 닫기'를 마우스·터치 모두에서 쓰도록)
    /// </summary>
    public static bool PressedOutside(RectTransform rect, Func<Vector2, bool> except = null)
    {
        foreach (var p in Presses)
        {
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, p, null)) continue;
            if (OverButton(p)) continue;
            if (except != null && except(p)) continue;
            return true;
        }
        return false;
    }

    /// <summary>화면 좌표가 보이는(눌 수 있는) 단추 위인가.</summary>
    public static bool OverButton(Vector2 screen) => TopAt(screen) != null;

    private static PxButton TopAt(Vector2 sp)
    {
        PxButton top = null; int topOrder = int.MinValue;
        for (int i = buttons.Count - 1; i >= 0; i--)
        {
            var b = buttons[i];
            if (b.Rect == null) { buttons.RemoveAt(i); continue; }
            if (!b.Visible || !b.Enabled) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(b.Rect, sp, null)) continue;
            int order = b.SortOrder;
            if (order >= topOrder) { top = b; topOrder = order; }
        }
        return top;
    }

    /// <summary>매 프레임 한 번(아무 화면 부품이나 먼저 부르는 쪽이) 단추의 올려놓기·누르기를 처리한다.</summary>
    public static void Tick()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        var ms = Mouse.current;
        Vector2 mp = ms != null && !InputBlocked ? ms.position.ReadValue() : new Vector2(-9999, -9999);
        PxButton top = TopAt(mp);
        bool overByTouch = false;
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts != null && !InputBlocked)
            foreach (var t in ts.touches)
                if (t.press.isPressed && TopAt(t.position.ReadValue()) != null) overByTouch = true;
        PointerOverButton = top != null || overByTouch;
        if (top != hovered)
        {
            if (hovered != null) hovered.SetHover(false);
            hovered = top;
            if (hovered != null && Time.unscaledTime - lastTouchAt > 0.6f) { hovered.SetHover(true); Sfx.Play("ui_hover", 0.25f); }
        }
        // 이번 프레임에 누른 자리마다(손가락 여러 개) 맨 위 단추를 누른다
        foreach (var p in Presses)
        {
            var b = TopAt(p);
            if (b != null) { b.Click(); PointerOverButton = true; }
        }
        foreach (var b in buttons) b.Animate();
    }

    public static void ClearAll() { buttons.Clear(); hovered = null; PointerOverButton = false; InputBlocked = false; }
}

/// <summary>도트 단추: 어두운 판 + 테두리 + 도트 글씨. 올려놓으면 테두리가 금색으로 밝아진다.</summary>
public sealed class PxButton
{
    public readonly RectTransform Rect;
    public readonly PixelText Label;
    private readonly Image back, edge;
    private readonly Action onClick;
    private Canvas canvas;
    private bool hover;
    private float pressT;
    public bool Enabled = true;
    public bool Selected;
    public Color Accent = MuseumUI.Amber;
    public Color BaseFill = new Color(0.1f, 0.12f, 0.16f, 0.98f);
    public string Sound = "ui_select";

    public PxButton(RectTransform r, string label, int scale, Action onClick)
    {
        Rect = r; this.onClick = onClick;
        var b = MuseumUI.Node("Back", r); MuseumUI.Stretch(b); back = MuseumUI.Sliced(b, MuseumUI.FillSprite, BaseFill);
        var e = MuseumUI.Node("Edge", r); MuseumUI.Stretch(e); edge = MuseumUI.Sliced(e, MuseumUI.BorderSprite, MuseumUI.Gold);
        Label = MuseumUI.Text(r, "Label", label, scale, MuseumUI.Ink, TextAnchor.MiddleCenter);
        MuseumUI.Stretch(Label.rectTransform, 6);
    }

    public bool Visible => Rect != null && Rect.gameObject.activeInHierarchy;
    public int SortOrder { get { if (canvas == null && Rect != null) canvas = Rect.GetComponentInParent<Canvas>(); return canvas != null ? canvas.sortingOrder : 0; } }

    public void SetHover(bool on) { hover = on; }
    public void Click() { pressT = 0.12f; if (!string.IsNullOrEmpty(Sound)) Sfx.Play(Sound, 0.6f); onClick?.Invoke(); }

    public void Animate()
    {
        if (Rect == null || !Rect.gameObject.activeInHierarchy) return;
        pressT = Mathf.Max(0f, pressT - Time.unscaledDeltaTime);
        Color f = BaseFill;
        if (Selected) f = Color.Lerp(BaseFill, Accent, 0.35f);
        if (hover && Enabled) f = Color.Lerp(f, Accent, 0.18f);
        if (pressT > 0f) f = Color.Lerp(f, Accent, 0.4f);
        if (!Enabled) f = new Color(f.r * 0.6f, f.g * 0.6f, f.b * 0.6f, f.a);
        back.color = f;
        edge.color = !Enabled ? new Color(0.3f, 0.3f, 0.32f) : (hover || Selected) ? Accent : MuseumUI.Gold;
        Label.color = Enabled ? (Selected ? Color.white : MuseumUI.Ink) : new Color(0.5f, 0.52f, 0.55f);
    }

    public void SetText(string s) { Label.text = s; }
    public void SetActive(bool on) { if (Rect != null && Rect.gameObject.activeSelf != on) Rect.gameObject.SetActive(on); }
}
