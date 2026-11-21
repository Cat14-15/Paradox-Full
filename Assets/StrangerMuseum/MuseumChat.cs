using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// MainStory1 채팅창. 오른쪽 아래에 4줄로 떠 있고, 입력을 시작하거나 글을 누르면 10줄로 늘어난다(휠로 스크롤).
/// 다른 곳을 누르거나 Esc 를 누르면 다시 4줄. 모양은 박물관 HUD(어두운 갈색 판 + 금색 선)에 맞춘다.
/// 글자는 도트 글꼴(PixelText)이라 웹 빌드에서도 한글이 나온다. 글은 Story1Net.SendChat 으로 서버를 거쳐 모두에게 간다.
/// </summary>
public sealed class MuseumChat : MonoBehaviour
{
    /// <summary>글을 쓰는 중이면 true — 걷기·E 상호작용을 멈춘다.</summary>
    public static bool Typing { get; private set; }
    /// <summary>입력 중이거나, 이번 프레임에 입력을 끝냈다(Esc·Enter 한 번이 다른 창까지 닫지 않게 — Update 순서와 상관없이).</summary>
    public static bool TypingNow => Typing || Time.frameCount <= closedFrame + 1;
    /// <summary>입력 중에 이야기 때문에 채팅이 강제로 닫힌 시각(그 직후의 타자가 이야기를 넘기지 않게).</summary>
    public static float ForcedCloseAt = -99f;
    private static int closedFrame = -10;
    /// <summary>true 이면 채팅창을 통째로 숨긴다(이야기 연출 중).</summary>
    public static bool Hidden;
    public static bool PuzzleActive;

    private static MuseumChat instance;

    /// <summary>OnGUI 좌표(왼쪽 위 기준)의 사각형이 채팅창(또는 이모티콘 선택창)과 겹치는가.</summary>
    public static bool CoversGuiRect(Rect gui)
    {
        if (instance == null) return false;
        return instance.Overlaps(instance.panel, gui) || (instance.pickerOpen && instance.picker != null && instance.picker.gameObject.activeSelf && instance.Overlaps(instance.picker, gui));
    }

    /// <summary>채팅창(펼쳐졌든 접혔든 지금 보이는 모양 + 떠 있는 카드 줄)의 OnGUI 좌표 사각형.</summary>
    public static bool ChatRect(out Rect gui)
    {
        gui = default;
        if (instance == null || instance.panel == null || !instance.panel.gameObject.activeInHierarchy) return false;
        var cs = new Vector3[4];
        instance.panel.GetWorldCorners(cs);
        float x0 = cs[0].x, y0 = cs[0].y, x1 = cs[2].x, y1 = cs[2].y;
        if (instance.foldBtn != null && instance.foldBtn.Visible)
        {
            instance.foldBtn.Rect.GetWorldCorners(cs);
            x0 = Mathf.Min(x0, cs[0].x); y1 = Mathf.Max(y1, cs[2].y);
        }
        if (instance.cards != null && instance.cards.gameObject.activeInHierarchy)
        {
            instance.cards.GetWorldCorners(cs);
            x0 = Mathf.Min(x0, cs[0].x); y1 = Mathf.Max(y1, cs[2].y);
        }
        gui = Rect.MinMaxRect(x0, Screen.height - y1, x1, Screen.height - y0);
        return true;
    }

    private bool Overlaps(RectTransform rt, Rect gui)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);   // 오버레이 캔버스라 월드 좌표 = 화면 좌표(왼쪽 아래 기준)
        var screen = Rect.MinMaxRect(c[0].x, Screen.height - c[2].y, c[2].x, Screen.height - c[0].y);
        return screen.Overlaps(gui);
    }

    private const int CollapsedRows = 3, FocusRows = 4, ExpandedRows = 8;   // 접힘 3줄 · 말할 때 4줄(카드 줄과 함께라 화면을 덜 가리게) · 휠로 올려 볼 때 8줄
    private const int S = 2;   // 도트 글꼴 배율(1920x1080 기준 캔버스에서 정수배라 또렷하다)
    private const float PanelW = 540f, RowH = 38f, Pad = 14f, InputH = 50f, Margin = 20f, MiniH = 50f;

    // 접기: C 키 또는 ▼ 단추. 접혀 있으면 새 글만 잠깐 한 줄 보여 주고, Enter 로 쓰기 시작하면 펼쳐진다.
    private const string MinKey = "stranger.chatMin";
    private static bool minimized;
    private static bool minLoaded;
    private PxButton foldBtn;
    private PixelText miniHint;
    private float peekUntil, nextWheel;
    private int unread;
    private bool lastMini, lastPeek;
    // 퍼즐 창이 열려 있으면 단서가 채팅으로 오가니 접혀 있어도 펼쳐 보인다
    private bool Mini => minimized && !focused && !PuzzleActive;
    private bool Peeking => Mini && Time.time < peekUntil;
    private const int MaxKept = 300, MaxInput = Story1Net.ChatMax;

    private static readonly Color PanelCol = new Color32(31, 24, 20, 215);
    private static readonly Color PanelColOpen = new Color32(31, 24, 20, 245);
    private static readonly Color Gold = new Color32(204, 159, 79, 255);
    private static readonly Color Cream = new Color32(255, 245, 220, 255);
    private static readonly Color Dim = new Color32(150, 134, 112, 255);
    private static readonly Color InputBg = new Color32(20, 15, 12, 255);

    private struct Row { public string name; public Color nameCol; public string body; public float indent; }

    private readonly List<Row> rows = new List<Row>();
    private RectTransform panel, inputBox;
    private Image panelBg, inputBorder, thumb;
    private RectTransform track;
    private PixelText[] rowName;
    private RichLine[] rowBody;
    private RichLine inputLine;
    private PixelText modeTag, pickerTitle;
    private Image selBox, emojiBtnBg, emojiBtnIcon, pickerBg, hoverBox;
    private RectTransform emojiBtn, picker;
    private Image[] pickerIcons;
    private bool pickerOpen, selAll, modUsed;
    private const float EmojiIcon = 36f, EmojiUnits = 19f;   // 글 안에서 이모티콘 하나의 크기(캔버스 단위 / 글꼴 칸 단위)
    private const float PickCell = 72f, PickPad = 16f, PickHead = 40f;
    private const int PickCols = 6;
    private bool expanded, focused;
    private int offset;   // 맨 아래에서 몇 줄 위를 보고 있는가
    private string text = "";
    private string ime = "";
    private readonly HangulComposer hg = new HangulComposer();
    private bool hgOn, imeSeen, hooked;
    private float bsTimer;
    private Keyboard hookedKb;

    private static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;

    private void Awake()
    {
        Typing = false;
        PuzzleActive = false;
        instance = this;
        Build();
    }

    private void OnEnable() { instance = this; Story1Net.ChatLine += OnLine; }
    private void OnDisable() { Story1Net.ChatLine -= OnLine; Unhook(); Typing = false; if (instance == this) instance = null; }

    // ---------------------------------------------------------------- 만들기

    private void Build()
    {
        var cv = new GameObject("ChatCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cv.transform.SetParent(transform, false);
        var canvas = cv.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var sc = cv.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 좁은 창에서도 채팅창이 화면 밖으로 잘리지 않게

        panel = NewRect("Panel", cv.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
        panel.anchoredPosition = new Vector2(-Margin, Margin);
        panelBg = panel.gameObject.AddComponent<Image>();
        panelBg.color = PanelCol;

        var top = NewRect("GoldLine", panel);
        top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
        top.sizeDelta = new Vector2(0, 5);
        top.gameObject.AddComponent<Image>().color = Gold;

        rowName = new PixelText[ExpandedRows];
        rowBody = new RichLine[ExpandedRows];
        for (int i = 0; i < ExpandedRows; i++)
        {
            rowName[i] = NewText("Name" + i, panel, Cream, TextAnchor.MiddleLeft);
            rowBody[i] = new RichLine(panel);
        }

        var tr = NewRect("Track", panel);
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(1, 1);
        tr.sizeDelta = new Vector2(8, 40);
        var trImg = tr.gameObject.AddComponent<Image>(); trImg.color = new Color32(60, 48, 38, 255); trImg.raycastTarget = false;
        track = tr;
        var th = NewRect("Thumb", tr);
        th.anchorMin = new Vector2(0, 1); th.anchorMax = new Vector2(1, 1); th.pivot = new Vector2(0.5f, 1);
        thumb = th.gameObject.AddComponent<Image>(); thumb.color = Gold; thumb.raycastTarget = false;

        miniHint = NewText("MiniHint", panel, Dim, TextAnchor.MiddleLeft);
        miniHint.rectTransform.anchoredPosition = new Vector2(Pad, 0f);
        miniHint.gameObject.SetActive(false);

        if (!minLoaded) { minLoaded = true; try { minimized = PlayerPrefs.GetInt(MinKey, 1) == 1; } catch { minimized = true; } }   // 기본은 접힘(화면을 가린다는 의견이 가장 많았다)
        foldBtn = MuseumUI.Button(cv.transform, "ChatFold", "▼", 2, ToggleMin);
        foldBtn.Rect.anchorMin = foldBtn.Rect.anchorMax = foldBtn.Rect.pivot = new Vector2(1, 0);
        foldBtn.Rect.sizeDelta = new Vector2(52f, 38f);
        foldBtn.Sound = "ui_select";

        var box = NewRect("InputBox", panel);
        inputBox = box;
        box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 0); box.pivot = new Vector2(0.5f, 0);
        box.offsetMin = new Vector2(Pad, Pad); box.offsetMax = new Vector2(-Pad, Pad + InputH);
        inputBorder = box.gameObject.AddComponent<Image>(); inputBorder.color = new Color32(90, 72, 52, 255);
        var inner = NewRect("Inner", box);
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one; inner.offsetMin = new Vector2(4, 4); inner.offsetMax = new Vector2(-4, -4);
        inner.gameObject.AddComponent<Image>().color = InputBg;
        var sel = NewRect("Selection", inner);
        sel.anchorMin = sel.anchorMax = sel.pivot = new Vector2(0, 0);
        selBox = sel.gameObject.AddComponent<Image>(); selBox.color = new Color32(204, 159, 79, 110); selBox.raycastTarget = false;
        inputLine = new RichLine(inner);
        modeTag = NewText("Mode", inner, Gold, TextAnchor.MiddleRight);
        Stretch(modeTag.rectTransform, 0, 0, 76, 0);

        // 이모티콘 단추(입력 줄 오른쪽 끝)
        var eb = NewRect("EmojiButton", inner);
        eb.anchorMin = eb.anchorMax = new Vector2(1, 0.5f); eb.pivot = new Vector2(1, 0.5f);
        eb.anchoredPosition = new Vector2(-4, 0); eb.sizeDelta = new Vector2(44, 44);
        emojiBtn = eb;
        emojiBtnBg = eb.gameObject.AddComponent<Image>(); emojiBtnBg.color = new Color(0, 0, 0, 0);
        var ei = NewRect("Icon", eb);
        ei.anchorMin = ei.anchorMax = ei.pivot = new Vector2(0.5f, 0.5f); ei.sizeDelta = new Vector2(36, 36);
        emojiBtnIcon = ei.gameObject.AddComponent<Image>(); emojiBtnIcon.sprite = MuseumEmoji.Get(9); emojiBtnIcon.raycastTarget = false;

        BuildPicker(cv.transform);
        if (gameObject.scene.name == "MainStory1") BuildCards(cv.transform);

        Layout();
    }

    private void BuildPicker(Transform canvas)
    {
        int n = MuseumEmoji.PickCount;
        int rowsN = (n + PickCols - 1) / PickCols;
        picker = NewRect("EmojiPicker", canvas);
        picker.anchorMin = picker.anchorMax = picker.pivot = new Vector2(1, 0);
        picker.sizeDelta = new Vector2(PickPad * 2 + PickCols * PickCell, PickPad * 2 + PickHead + rowsN * PickCell);
        pickerBg = picker.gameObject.AddComponent<Image>(); pickerBg.color = PanelColOpen;
        var line = NewRect("GoldLine", picker);
        line.anchorMin = new Vector2(0, 1); line.anchorMax = new Vector2(1, 1); line.pivot = new Vector2(0.5f, 1); line.sizeDelta = new Vector2(0, 5);
        line.gameObject.AddComponent<Image>().color = Gold;
        pickerTitle = NewText("Title", picker, Gold, TextAnchor.MiddleLeft);
        var trt = pickerTitle.rectTransform; trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0, 1);
        trt.anchoredPosition = new Vector2(PickPad, -PickPad - 4); trt.sizeDelta = new Vector2(400, PickHead);
        var hv = NewRect("Hover", picker);
        hv.anchorMin = hv.anchorMax = hv.pivot = new Vector2(0, 1); hv.sizeDelta = new Vector2(PickCell - 8, PickCell - 8);
        hoverBox = hv.gameObject.AddComponent<Image>(); hoverBox.color = new Color32(204, 159, 79, 90); hoverBox.raycastTarget = false;
        pickerIcons = new Image[n];
        for (int i = 0; i < n; i++)
        {
            var r = NewRect("E" + i, picker);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(48, 48);
            r.anchoredPosition = CellCenter(i);
            var im = r.gameObject.AddComponent<Image>(); im.sprite = MuseumEmoji.Get(i); im.raycastTarget = false;
            pickerIcons[i] = im;
        }
        picker.gameObject.SetActive(false);
    }

    // 선택창 가운데 기준 좌표
    private Vector2 CellCenter(int i)
    {
        int c = i % PickCols, r = i / PickCols;
        Vector2 size = picker.sizeDelta;
        return new Vector2(PickPad + c * PickCell + PickCell * 0.5f - size.x * 0.5f, size.y * 0.5f - (PickPad + PickHead + r * PickCell + PickCell * 0.5f));
    }

    private int CellAt(Vector2 screen)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(picker, screen, null, out var local)) return -1;
        Vector2 size = picker.sizeDelta;
        float x = local.x + size.x, yTop = size.y - local.y;   // 왼쪽 위 기준
        if (x < PickPad || yTop < PickPad + PickHead) return -1;
        int c = (int)((x - PickPad) / PickCell), r = (int)((yTop - PickPad - PickHead) / PickCell);
        if (c < 0 || c >= PickCols || r < 0) return -1;
        int idx = r * PickCols + c;
        return idx < MuseumEmoji.PickCount ? idx : -1;
    }

    /// <summary>글 한 줄(글자 + 이모티콘 그림이 섞인)을 그린다. 글 조각과 그림 조각을 풀에서 꺼내 가로로 이어 붙인다.</summary>
    private sealed class RichLine
    {
        private readonly Transform parent;
        private readonly List<PixelText> texts = new List<PixelText>();
        private readonly List<Image> icons = new List<Image>();

        public RichLine(Transform parent) { this.parent = parent; }

        private PixelText Text(int i)
        {
            while (texts.Count <= i) texts.Add(NewText("Seg" + texts.Count, parent, Cream, TextAnchor.MiddleLeft));
            return texts[i];
        }

        private Image Icon(int i)
        {
            while (icons.Count <= i)
            {
                var r = NewRect("Emoji" + icons.Count, parent);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 0);
                var im = r.gameObject.AddComponent<Image>(); im.raycastTarget = false;
                icons.Add(im);
            }
            return icons[i];
        }

        public void SetActive(bool on)
        {
            if (!on) Trim(0, 0);
        }

        private void Trim(int t, int ic)
        {
            for (int i = t; i < texts.Count; i++) if (texts[i].gameObject.activeSelf) texts[i].gameObject.SetActive(false);
            for (int i = ic; i < icons.Count; i++) if (icons[i].gameObject.activeSelf) icons[i].gameObject.SetActive(false);
        }

        public void Set(string s, Color color, float x, float y, float h)
        {
            int ti = 0, ii = 0;
            var sb = new System.Text.StringBuilder();
            float cx = x;
            foreach (char ch in s ?? "")
            {
                if (!MuseumEmoji.Is(ch)) { sb.Append(ch); continue; }
                Flush(sb, color, ref cx, y, h, ref ti);
                var im = Icon(ii++);
                im.gameObject.SetActive(true);
                im.sprite = MuseumEmoji.Get(ch - MuseumEmoji.Base);
                var rt = im.rectTransform;
                rt.sizeDelta = new Vector2(EmojiIcon, EmojiIcon);
                rt.anchoredPosition = new Vector2(cx + 1, y + (h - EmojiIcon) * 0.5f);
                cx += EmojiUnits * S;
            }
            Flush(sb, color, ref cx, y, h, ref ti);
            Trim(ti, ii);
        }

        private void Flush(System.Text.StringBuilder sb, Color color, ref float cx, float y, float h, ref int ti)
        {
            if (sb.Length == 0) return;
            string seg = sb.ToString(); sb.Clear();
            var t = Text(ti++);
            t.gameObject.SetActive(true);
            t.text = seg; t.color = color;
            var rt = t.rectTransform;
            float w = PixelFontData.Measure(seg) * S;
            rt.anchoredPosition = new Vector2(cx, y); rt.sizeDelta = new Vector2(w + 4, h);
            cx += w;
        }
    }

    private static float Width(string str)   // 글꼴 칸 단위
    {
        float w = 0f;
        foreach (char c in str) w += MuseumEmoji.Is(c) ? EmojiUnits : PixelFontData.Advance(c);
        return w;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        return (RectTransform)g.transform;
    }

    private static PixelText NewText(string name, Transform parent, Color c, TextAnchor a)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<PixelText>();
        t.scale = S; t.align = a; t.color = c; t.raycastTarget = false;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
        return t;
    }

    private static void Stretch(RectTransform r, float l, float b, float rt, float t)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
    }

    private int VisibleRows => expanded ? ExpandedRows : focused ? FocusRows : CollapsedRows;
    private float LogH => VisibleRows * RowH;

    private void Layout()
    {
        bool mini = Mini, peek = Peeking;
        lastMini = mini; lastPeek = peek;
        if (mini)
        {
            float w = peek ? PanelW : PixelFontData.Measure(miniHint.text) * S + Pad * 2 + 8f;
            panel.sizeDelta = new Vector2(Mathf.Clamp(w, 180f, PanelW), MiniH);
        }
        else panel.sizeDelta = new Vector2(PanelW, Pad + LogH + 16f + InputH + Pad);
        panelBg.color = expanded || focused ? PanelColOpen : PanelCol;
        track.anchoredPosition = new Vector2(-8, -16f);
        track.sizeDelta = new Vector2(8, LogH - 8f);
        inputBorder.color = focused ? Gold : new Color32(90, 72, 52, 255);
        if (inputBox != null && inputBox.gameObject.activeSelf == mini) inputBox.gameObject.SetActive(!mini);
        miniHint.gameObject.SetActive(mini && !peek);
        miniHint.rectTransform.sizeDelta = new Vector2(PanelW - Pad * 2, MiniH);
        // 접기 단추: 채팅창 왼쪽 위 바깥(다른 글과 겹치지 않게)
        foldBtn.Rect.anchoredPosition = new Vector2(-Margin - panel.sizeDelta.x - 4f, Margin + panel.sizeDelta.y - 38f);
        foldBtn.SetText(mini ? "▲" : "▼");
        foldBtn.SetActive(!Hidden && !focused);
        float cardsH = 0f;
        if (cards != null)
        {
            bool showCards = focused && !Hidden;   // 카드 줄은 말할 때만(평소엔 화면을 가리지 않게)
            if (cards.gameObject.activeSelf != showCards) cards.gameObject.SetActive(showCards);
            cards.anchoredPosition = new Vector2(-Margin, Margin + panel.sizeDelta.y);   // 채팅창 바로 위에 같은 너비로 붙인다(틈·삐져나옴 없이)
            cardsH = showCards ? cards.sizeDelta.y : 0f;
        }
        if (picker != null) picker.anchoredPosition = new Vector2(-Margin, Margin + panel.sizeDelta.y + cardsH + 12f);
    }

    private void ToggleMin()
    {
        minimized = !minimized;
        try { PlayerPrefs.SetInt(MinKey, minimized ? 1 : 0); PlayerPrefs.Save(); } catch { }
        if (!minimized) unread = 0;
        peekUntil = 0f; expanded = false; offset = 0;
        Layout();
    }

    // ---------------------------------------------------------------- 글 받기

    private void OnLine(int slot, string line)
    {
        Add(slot, line);
        if (gameObject.scene.name == "MainStory1" && !Story1Role.IsBot(slot)) MuseumWords.LearnFrom(slot, line);
        if (slot != Story1Role.Current) Sfx.Play("chime", 0.35f);   // 남의 글이 도착
        if (gameObject.scene.name != "MainStory1" && !Story1Role.IsBot(slot) && FirstBot() >= 0 && pendingAt < 0f)   // (옛 장면) 사람이 말하면 AI 하나가 잠깐 뒤에 답한다
        {
            int h = Hash(line.GetHashCode() ^ slot * 7919);
            pendingRole = BotAt(h); pendingText = Replies[pendingRole][h / 4 % Replies[pendingRole].Length]; pendingAt = Time.time + 1.2f + (h % 5) * 0.2f;
        }
    }

    /// <summary>화면 좌표(왼쪽 아래 기준)가 채팅창·신호 카드 줄 위인가.</summary>
    public static bool CoversScreen(Vector3 screen)
    {
        if (MuseumHud.ModalCovers(screen)) return true;   // 퍼즐 창 위에 띄운 수첩을 눌러도 퍼즐 창이 닫히지 않게
        if (instance == null || instance.panel == null || !instance.panel.gameObject.activeInHierarchy) return false;
        if (RectTransformUtility.RectangleContainsScreenPoint(instance.panel, screen, null)) return true;
        if (instance.foldBtn != null && instance.foldBtn.Visible && RectTransformUtility.RectangleContainsScreenPoint(instance.foldBtn.Rect, screen, null)) return true;
        return instance.cards != null && instance.cards.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(instance.cards, screen, null);
    }

    // ---------------------------------------------------------------- 신호 카드(누르면 바로 보내진다)
    private static readonly string[] CardText = { "1", "2", "3", "4", "←", "→", "↑", "↓", "▲", "●", "■", "★", "○", "×", "?" };
    private RectTransform cards;
    private readonly List<PxButton> cardButtons = new List<PxButton>();
    private PixelText cardsHint;
    private const float CardW = 32f, CardH = 44f, CardGap = 2f, CardSide = 16f;

    private void BuildCards(Transform canvas)
    {
        cards = NewRect("SignalCards", canvas);
        cards.anchorMin = cards.anchorMax = cards.pivot = new Vector2(1, 0);
        cards.sizeDelta = new Vector2(PanelW, CardH + 52f);   // 채팅창과 같은 너비
        var bg = cards.gameObject.AddComponent<Image>(); bg.color = PanelCol; bg.raycastTarget = false;
        cardsHint = NewText("Hint", cards, Gold, TextAnchor.MiddleLeft);
        var hr = cardsHint.rectTransform; hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0, 1);
        hr.anchoredPosition = new Vector2(CardSide, -4f); hr.sizeDelta = new Vector2(PanelW - CardSide * 2f, 36f);
        cardsHint.scale = 2; cardsHint.maxWidth = PanelW - CardSide * 2f; cardsHint.text = "신호 카드 · 누르면 바로 전송";
        float wordsH = BuildWordCards();
        cards.sizeDelta = new Vector2(cards.sizeDelta.x, cards.sizeDelta.y + wordsH);
        if (wordsH > 0f) cardsHint.text = "말 카드 · " + MuseumVoice.Language[Mathf.Clamp(Story1Role.Current, 0, 3)] + "  (카드로만 말한다)";
        float x0 = (cards.sizeDelta.x - (CardText.Length * (CardW + CardGap) - CardGap)) * 0.5f;   // 가운데(왼쪽 낱말 카드와 같은 여백)
        for (int i = 0; i < CardText.Length; i++)
        {
            string s = CardText[i];
            var b = MuseumUI.Button(cards, "Card" + i, s, 2, () => SendCard(s));
            b.Rect.anchorMin = b.Rect.anchorMax = b.Rect.pivot = new Vector2(0, 0);
            b.Rect.anchoredPosition = new Vector2(x0 + i * (CardW + CardGap), 10f);
            b.Rect.sizeDelta = new Vector2(CardW, CardH);
            b.Sound = "ui_next";
            cardButtons.Add(b);
        }
    }

    /// <summary>한국어를 모르는 역할(이안·린·카이)인가: 자유 입력 대신 자기 나라 말 카드로 말한다.</summary>
    /// <summary>박물관에서는 모두 카드로만 말한다(자유 입력 없음).</summary>
    private bool Restricted => cards != null;

    /// <summary>내 나라 말 카드 줄(신호 카드 줄 위). 만든 높이를 돌려준다.</summary>
    private float BuildWordCards()
    {
        int r = Mathf.Clamp(Story1Role.Current, 0, 3);
        var words = new List<(string word, string mean)>
        {
            (MuseumVoice.Number(r, 1), "1"), (MuseumVoice.Number(r, 2), "2"), (MuseumVoice.Number(r, 3), "3"), (MuseumVoice.Number(r, 4), "4"),
            (MuseumVoice.Left(r), "←"), (MuseumVoice.Right(r), "→"), (MuseumVoice.YesW(r), "○"), (MuseumVoice.NoW(r), "×"),
            (MuseumVoice.HereW(r), "여기"), (MuseumVoice.GoW(r), "가자"), (MuseumVoice.ThanksW(r), "고마워"), (MuseumVoice.WhatW(r).TrimEnd('?'), "?"),
        };
        float maxW = cards.sizeDelta.x - CardSide * 2f, x = 0f, rowH = 42f, gap = 4f;
        int row = 0;
        var made = new List<(PxButton b, int row, float x)>();
        foreach (var (word, mean) in words)
        {
            string label = r == 0 ? word : word + " " + mean;   // 한국어 카드는 뜻을 겹쳐 적지 않는다
            float w = PixelFontData.Measure(label) * 2f + 20f;
            if (x > 0f && x + w > maxW) { row++; x = 0f; }
            string send = word;
            var b = MuseumUI.Button(cards, "Word" + made.Count, label, 2, () => SendCard(send));
            b.Sound = "ui_next";
            b.Rect.anchorMin = b.Rect.anchorMax = b.Rect.pivot = new Vector2(0, 0);
            b.Rect.sizeDelta = new Vector2(w, rowH);
            made.Add((b, row, x));
            x += w + gap;
        }
        foreach (var (label, act) in new (string, System.Action)[] { ("지우기", () => { text = ""; selAll = false; }), ("보내기", SendNow) })
        {
            float w = PixelFontData.Measure(label) * 2f + 28f;
            if (x > 0f && x + w > maxW) { row++; x = 0f; }
            var b = MuseumUI.Button(cards, label, label, 2, act);
            b.Accent = label == "보내기" ? MuseumUI.Mint : MuseumUI.Red;
            b.Rect.anchorMin = b.Rect.anchorMax = b.Rect.pivot = new Vector2(0, 0);
            b.Rect.sizeDelta = new Vector2(w, rowH);
            made.Add((b, row, x));
            x += w + gap;
        }
        int rows = row + 1;
        float baseY = CardH + 18f;
        foreach (var (b, rw, bx) in made) b.Rect.anchoredPosition = new Vector2(CardSide + bx, baseY + (rows - 1 - rw) * (rowH + gap));
        return rows * (rowH + gap) + 6f;
    }

    private void SendNow()
    {
        string send = text.Trim();
        if (send.Length == 0) return;
        Story1Net.SendChat(send);
        Sfx.Play("ui_next", 0.7f);
        text = ""; selAll = false; offset = 0;
        SetFocus(false);
    }

    /// <summary>카드 하나를 누르면: 입력 중이면 입력 줄에 붙이고, 아니면 바로 보낸다.</summary>
    private void SendCard(string s)
    {
        if (Restricted)
        {
            if (!focused) SetFocus(true);
            Append((text.Length > 0 && !text.EndsWith(" ") ? " " : "") + s);
            return;
        }
        if (focused) { FlushHangul(); Append(s); return; }   // 조합 중인 한글을 먼저 확정해야 순서가 맞다
        Story1Net.SendChat(s);
    }

    // ---------------------------------------------------------------- AI 동료의 말
    // 모든 PC 가 같은 시계로 같은 말을 고르도록(서버 시계 + 해시) 정한다. AI 말은 네트워크로 보내지 않는다.
    // AI 도 나라가 다르다: 말 대신 누구나 읽는 신호(숫자·방향·기호)로만 이야기한다.
    private static readonly string[][] Ambient =
    {
        new[] { "?", "→ 2", E(0) + " ?", E(4) + " →" },
        new[] { "← 1", E(1) + " !", E(5) + " ?", E(6) + " ?" },
        new[] { "↑ 3", E(4) + " 1", "↓ 2", E(3) + " ?" },
        new[] { E(7) + " ?", E(2) + " !", "+ 1", E(6) + " ↓" },
    };
    private static readonly string[][] Replies =
    {
        new[] { "○", "○ !", "→", E(11) },
        new[] { "○ ○", E(11), "?", E(9) },
        new[] { "→", "○", E(11) + " !", "?" },
        new[] { "○ !", E(7), E(8), "←" },
    };
    private static string E(int i) => MuseumEmoji.Char(i).ToString();
    private int lastTick = -1, pendingRole, pendingAmbient;
    private float pendingAt = -1f;
    private string pendingText;

    private static int Hash(int x) { unchecked { uint h = (uint)x * 2654435761u; h ^= h >> 15; return (int)(h & 0x7fffffff); } }

    private static int FirstBot() { for (int r = 0; r < Roles.Count; r++) if (Story1Role.IsBot(r)) return r; return -1; }

    private static int BotAt(int h)
    {
        int n = 0; for (int r = 0; r < Roles.Count; r++) if (Story1Role.IsBot(r)) n++;
        if (n == 0) return -1;
        int k = h % n;
        for (int r = 0; r < Roles.Count; r++) if (Story1Role.IsBot(r) && k-- == 0) return r;
        return -1;
    }

    private void BotTick()
    {
        if (pendingAt >= 0f && Time.time >= pendingAt)
        {
            pendingAt = -1f;
            if (pendingRole >= 0 && Story1Role.IsBot(pendingRole)) Add(pendingRole, pendingText);
        }
        if (FirstBot() < 0) return;
        int tick = (int)(Story1Net.Now / 22f);
        if (lastTick < 0) { lastTick = tick; return; }
        if (tick == lastTick) return;
        lastTick = tick;
        int h = Hash(tick * 31 + 17 + (Story1Net.Online ? Session.State.puzzleSeed : 0));
        int role = BotAt(h);
        if (role >= 0) Add(role, Ambient[role][h / 8 % Ambient[role].Length]);
    }

    private string NameOf(int slot)
    {
        string n = Roles.DefaultName(slot);
        if (!Story1Role.IsBot(slot))
        {
            var p = Session.State?.players[slot];
            if (Story1Net.Online && p != null && !string.IsNullOrEmpty(p.name)) n = p.name;
        }
        else n += " (AI)";
        return n;
    }

    private struct Msg { public int slot; public string text; }
    private readonly List<Msg> msgs = new List<Msg>();
    private int viewRole = -2;

    /// <summary>slot &lt; 0 이면 시스템 안내. 원문을 보관하고, 보는 역할(탭으로 바꿀 수 있다)에 맞춰 다시 그린다.</summary>
    /// <summary>AI 동료의 말을 채팅에 올린다(네트워크로 보내지 않는다).</summary>
    public static void Say(int slot, string line) { if (instance != null && !string.IsNullOrEmpty(line)) instance.Add(slot, line); }

    /// <summary>통역 능력: 말 속 외국어 낱말의 뜻(예: "tatlo=3 · kaliwa=왼쪽 ←"). 없으면 null.</summary>
    private static string Gloss(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        string low = line.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var kv in MuseumVoice.Glossary)
        {
            if (kv.Value.lang == 0 || kv.Value.lang == Story1Role.Current) continue;   // 한국어(화면 말)·내 나라 말은 풀이 불필요
            int i = low.IndexOf(kv.Key, System.StringComparison.Ordinal);
            if (i < 0) continue;
            bool a = i == 0 || !char.IsLetter(low[i - 1]); int e = i + kv.Key.Length; bool b = e >= low.Length || !char.IsLetter(low[e]);
            if (!a || !b) continue;
            if (sb.Length > 0) sb.Append("  /  ");   // 점(·)만으로는 낱말끼리 구분이 어렵다는 의견 → 슬래시 + 띄어쓰기
            sb.Append(kv.Key).Append(" = ").Append(kv.Value.meaning);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    /// <summary>내가 수첩에 이미 배운 낱말(내 나라 말 제외)만 뜻을 붙인다. 없으면 null.</summary>
    private static string LearnedGloss(string line)
    {
        if (string.IsNullOrEmpty(line) || MuseumWords.Learned.Count == 0) return null;
        string low = line.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        var seen = new HashSet<string>();
        foreach (var e in MuseumWords.Learned)
        {
            if (e.lang == Story1Role.Current || string.IsNullOrEmpty(e.word)) continue;
            string k = e.word.ToLowerInvariant();
            if (!seen.Add(k)) continue;
            int i = low.IndexOf(k, System.StringComparison.Ordinal);
            if (i < 0) continue;
            bool a = i == 0 || !char.IsLetter(low[i - 1]); int end = i + k.Length; bool b = end >= low.Length || !char.IsLetter(low[end]);
            if (!a || !b) continue;
            if (sb.Length > 0) sb.Append("  /  ");
            sb.Append(e.word).Append(" = ").Append(e.meaning);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    public void Add(int slot, string line)
    {
        if (slot >= 0) MuseumHud.Bubble(slot, line);   // 말한 사람 머리 위 말풍선
        if (Mini) { peekUntil = Time.time + 5f; if (slot >= 0 && slot != Story1Role.Current) unread++; }
        msgs.Add(new Msg { slot = slot, text = line ?? "" });
        while (msgs.Count > MaxKept) msgs.RemoveAt(0);
        bool stick = offset == 0;
        Wrap(slot, line ?? "");
        if (slot >= 0 && slot != Story1Role.Current)
        {
            // 통역은 모든 낱말의 뜻, 다른 사람은 쉬움에서만 수첩에 이미 배운 낱말의 뜻(보통·어려움은 스스로 떠올리거나 수첩 N)
            string g = MuseumGame.IHave(MuseumGame.Interpreter) ? Gloss(line)
                     : Story1Net.Difficulty == Story1Rules.Easy ? LearnedGloss(line) : null;
            if (g != null) { msgs.Add(new Msg { slot = -2, text = g }); Wrap(-2, g); }
        }
        while (rows.Count > MaxKept * 3) rows.RemoveAt(0);
        if (stick) offset = 0;
    }

    // 보는 역할이 바뀌면(혼자 테스트할 때 Tab) 지난 글도 그 역할 기준으로 다시 읽힌다
    private void Rebuild()
    {
        rows.Clear();
        foreach (var m in msgs) Wrap(m.slot, m.text);
        offset = Mathf.Clamp(offset, 0, Mathf.Max(0, rows.Count - VisibleRows));
    }

    /// <summary>AI 동료의 말("낱말들   [2] ←1")처럼 큰 띄어쓰기로 나뉜 부분은 " / " 로 보이게 한다(어디까지가 말이고 어디부터 신호인지).</summary>
    private static readonly System.Text.RegularExpressions.Regex Gap = new System.Text.RegularExpressions.Regex(" {3,}");

    private void Wrap(int slot, string line)
    {
        line = Gap.Replace(line ?? "", "  /  ");
        string name = slot >= 0 ? NameOf(slot) : slot == -2 ? "  뜻" : "알림";
        Color col = slot >= 0 ? Roles.Color(slot) : Gold;
        string head = name + "  ";
        float headW = PixelFontData.Measure(head);                 // 글꼴 칸 단위
        float width = (PanelW - Pad * 2 - 24f) / S;                // 오른쪽 스크롤 막대 자리 빼고
        float avail = width - headW;
        var cur = new System.Text.StringBuilder();
        float curW = 0f; bool first = true;
        foreach (char ch in line)
        {
            bool em = MuseumEmoji.Is(ch);
            if (ch == '\n' || (!em && !PixelFontData.Has(ch) && ch != ' ')) continue;
            float adv = em ? EmojiUnits : PixelFontData.Advance(ch);
            if (curW + adv > avail && cur.Length > 0)
            {
                rows.Add(new Row { name = first ? head : null, nameCol = col, body = cur.ToString(), indent = headW * S });
                first = false; cur.Clear(); curW = 0f;
                if (ch == ' ') continue;
            }
            cur.Append(ch); curW += adv;
        }
        if (cur.Length > 0 || first)
            rows.Add(new Row { name = first ? head : null, nameCol = col, body = cur.ToString(), indent = headW * S });
    }

    // ---------------------------------------------------------------- 입력

    private void Hook(Keyboard kb)
    {
        if (kb == null || hookedKb == kb) return;
        Unhook();
        kb.onTextInput += OnChar;
        kb.onIMECompositionChange += OnIme;
        hookedKb = kb;
    }

    private void Unhook()
    {
        if (hookedKb == null) return;
        hookedKb.onTextInput -= OnChar;
        hookedKb.onIMECompositionChange -= OnIme;
        hookedKb.SetIMEEnabled(false);
        hookedKb = null;
    }

    private void OnIme(UnityEngine.InputSystem.LowLevel.IMECompositionString s)
    {
        string next = focused && !Restricted ? s.ToString() : "";
        if (next.Length > 0 && next != ime) TypeSfx(1f);   // 입력기로 조합하는 글자도 키마다 소리
        ime = next;
        if (ime.Length > 0) { imeSeen = true; hgOn = false; hg.Reset(); ReplaceSelection(); }   // OS 입력기가 살아 있으면 자체 조합기는 끈다
    }

    // 전체 선택된 상태에서 입력하면 선택한 글이 바뀐다
    private void ReplaceSelection() { if (selAll) { text = ""; selAll = false; } }

    private static bool Accepts(char ch) => MuseumEmoji.Is(ch) || PixelFontData.Has(ch);

    private static readonly string[] TypeIds = { "ui_type", "ui_type2", "ui_type3" };
    private int typeN;
    private void TypeSfx(float v) { typeN = (typeN + Random.Range(1, 3)) % 3; Sfx.Play(TypeIds[typeN], v); }

    private void Append(string s, bool sound = true)
    {
        if (string.IsNullOrEmpty(s)) return;
        ReplaceSelection();
        bool added = false;
        foreach (char ch in s) if (Accepts(ch) && text.Length < MaxInput) { text += ch; added = true; }
        if (added && sound) TypeSfx(1f);   // 글자를 칠 때마다 타자 소리(로비 이름 입력과 같은 소리)
    }

    private void InsertEmoji(int index)
    {
        ReplaceSelection(); FlushHangul();
        if (text.Length < MaxInput) text += MuseumEmoji.Char(index);
        Sfx.Play("ui_select", 0.6f);
    }

    private void FlushHangul() { if (hg.Active) { Append(hg.Current); hg.Reset(); } }

    private void OnChar(char c)
    {
        if (!focused || Restricted) return;
        if (Web && c >= 128) return;   // 웹: 브라우저 입력기 대신 자체 한글 조합기만 쓴다
        if (Web && !hgOn && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return;   // 웹 영문·숫자는 Update 에서 키로 직접 읽는다
        if (c >= 128) { imeSeen = true; hgOn = false; hg.Reset(); }
        if (hgOn)
        {
            if (c < 128 && char.IsLetter(c)) return;   // 영문 글자는 자체 조합기가 키로 처리
            FlushHangul();
        }
        if (c >= ' ' && c != 127) Append(c.ToString(), c < 128);   // 한글 입력기가 확정한 글자는 조합 중에 이미 소리를 냈다
    }

    private void SetFocus(bool on)
    {
        if (focused == on) return;
        if (!on) closedFrame = Time.frameCount;
        focused = on;
        Typing = on;
        expanded = false;   // 말할 때는 4줄만(휠로 올리면 8줄)
        if (on) unread = 0;
        if (!on) { offset = 0; ime = ""; FlushHangul(); hg.Reset(); pickerOpen = false; selAll = false; }
        var kb = Keyboard.current;
        if (kb != null) kb.SetIMEEnabled(on);
        Layout();
    }

    private float restrictToastUntil;

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        Hook(kb);
        if (panel != null && panel.gameObject.activeSelf == Hidden)   // 숨김 ↔ 보임 전환
        {
            if (Hidden) { if (focused) ForcedCloseAt = Time.unscaledTime; SetFocus(false); pickerOpen = false; if (picker != null) picker.gameObject.SetActive(false); }
            panel.gameObject.SetActive(!Hidden);
            Layout();
        }
        if (Hidden) return;
        if (Mini != lastMini || Peeking != lastPeek) Layout();
        if (Mini)
        {
            string hint = unread > 0 ? "채팅  C  ·  새 글 " + unread : "채팅  C";
            if (miniHint.text != hint) { miniHint.text = hint; miniHint.color = unread > 0 ? Gold : Dim; Layout(); }
        }
        var mouse0 = Mouse.current;
        bool overFold = mouse0 != null && foldBtn.Visible && RectTransformUtility.RectangleContainsScreenPoint(foldBtn.Rect, mouse0.position.ReadValue(), null);
        MuseumUI.Tick();   // 신호 카드·접기 단추(채팅의 마우스 처리보다 먼저)

        // 마우스: 창 안을 누르면 열리고(입력 시작), 밖을 누르면 닫힌다. 휠은 창 위에서 스크롤.
        if (mouse != null && panel != null)
        {
            Vector2 sp = mouse.position.ReadValue();
            bool inPanel = RectTransformUtility.RectangleContainsScreenPoint(panel, sp, null);
            bool inPicker = pickerOpen && RectTransformUtility.RectangleContainsScreenPoint(picker, sp, null);
            bool inBtn = RectTransformUtility.RectangleContainsScreenPoint(emojiBtn, sp, null);
            bool inCards = cards != null && cards.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(cards, sp, null);
            bool inside = inPanel || inPicker;
            if (mouse.leftButton.wasPressedThisFrame && (inCards || overFold)) { }   // 신호 카드·접기 단추는 단추가 처리한다
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                if (inside || inBtn) Sfx.Play("ui_select", 0.6f);
                else if (focused) Sfx.Play("ui_back", 0.5f);   // 창 밖을 눌러 닫음
                if (inPicker) { int idx = CellAt(sp); if (idx >= 0) InsertEmoji(idx); SetFocus(true); }
                else if (inBtn) { SetFocus(true); pickerOpen = !pickerOpen; }
                else if (inPanel) { SetFocus(true); pickerOpen = false; }
                else SetFocus(false);
            }
            if (inside && !Mini)
            {
                // 휠 한 칸 = 한 줄(웹·트랙패드는 한 번에 여러 값이 와서 너무 많이 넘어갔다 → 짧은 간격으로 한 줄씩만)
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && Time.unscaledTime >= nextWheel)
                {
                    if (!expanded) { expanded = true; Layout(); }
                    offset += wheel > 0 ? 1 : -1;
                    nextWheel = Time.unscaledTime + 0.16f;   // 트랙패드로 조금만 굴려도 확 올라간다는 의견 → 더 천천히
                }
            }
            else if (expanded && !focused) { expanded = false; offset = 0; Layout(); }
        }
        if (viewRole != Story1Role.Current) { viewRole = Story1Role.Current; Rebuild(); }
        offset = Mathf.Clamp(offset, 0, Mathf.Max(0, rows.Count - VisibleRows));

        if (TouchControls.ChatPressed) SetFocus(!focused);   // 폰: 터치 '채팅' 단추로 카드 줄 열고 닫기
        else if (kb != null)
        {
            if (!focused)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) SetFocus(true);
                else if (kb.cKey.wasPressedThisFrame && gameObject.scene.name == "MainStory1" && !MuseumStory.Busy) ToggleMin();
            }
            else UpdateTyping(kb);
        }

        if (!PuzzleActive && gameObject.scene.name != "MainStory1") BotTick();
        Refresh();
    }

    private void UpdateTyping(Keyboard kb)
    {
        bool mod = kb.ctrlKey.isPressed || kb.altKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
        if (Web && !hgOn && !mod && !Restricted)
        {
            bool sh = kb.shiftKey.isPressed;
            for (int k = 0; k < 26; k++)
                if (kb[(Key)((int)Key.A + k)].wasPressedThisFrame) Append(((char)((sh ? 'A' : 'a') + k)).ToString());
            for (int k = 0; k < 10; k++)
                if (kb[(Key)((int)Key.Digit0 + k)].wasPressedThisFrame && !sh) Append(((char)('0' + k)).ToString());
        }
        // 복사·붙여넣기·전체 선택: 윈도우는 Ctrl, 맥은 Command
        bool ctl = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
        if (ctl && !Restricted)
        {
            if (kb.aKey.wasPressedThisFrame) { FlushHangul(); selAll = text.Length > 0; modUsed = true; }
            if ((kb.cKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) && selAll)
            {
                GUIUtility.systemCopyBuffer = text; modUsed = true;
                if (kb.xKey.wasPressedThisFrame) { text = ""; selAll = false; }
            }
            if (kb.vKey.wasPressedThisFrame)
            {
                modUsed = true;
                FlushHangul();
                var clip = new System.Text.StringBuilder();
                foreach (char ch in GUIUtility.systemCopyBuffer ?? "") if (ch >= ' ' && Accepts(ch)) clip.Append(ch);
                Append(clip.ToString());
            }
        }
        // 한/영 전환: 오른쪽 Alt·Caps Lock·F2 는 바로, 오른쪽 Ctrl·Command 는 다른 키 없이 뗄 때(복사 같은 단축키와 겹치지 않게)
        if (kb.rightCtrlKey.wasPressedThisFrame || kb.rightCommandKey.wasPressedThisFrame) modUsed = false;
        bool toggle = kb.rightAltKey.wasPressedThisFrame || kb.capsLockKey.wasPressedThisFrame || kb.f2Key.wasPressedThisFrame
            || ((kb.rightCtrlKey.wasReleasedThisFrame || kb.rightCommandKey.wasReleasedThisFrame) && !modUsed);
        if (!imeSeen && toggle)
        {
            FlushHangul(); hgOn = !hgOn;
        }
        if (hgOn && !mod && !Restricted)
        {
            bool shift = kb.shiftKey.isPressed;
            for (int k = 0; k < 26; k++)
            {
                if (!kb[(Key)((int)Key.A + k)].wasPressedThisFrame) continue;
                char j = HangulComposer.JamoOf((char)('a' + k), shift);
                if (j == '\0') continue;
                ReplaceSelection();
                string done = hg.Key(j);
                if (string.IsNullOrEmpty(done)) TypeSfx(1f); else Append(done);   // 조합 중인 글자도 키마다 소리
            }
        }
        // 방향키는 방향 기호를 입력한다(누구나 읽는 신호)
        if (kb.leftArrowKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) FlushHangul();
        if (kb.leftArrowKey.wasPressedThisFrame) Append("←");
        if (kb.upArrowKey.wasPressedThisFrame) Append("↑");
        if (kb.rightArrowKey.wasPressedThisFrame) Append("→");
        if (kb.downArrowKey.wasPressedThisFrame) Append("↓");
        bool back = kb.backspaceKey.wasPressedThisFrame;
        if (kb.backspaceKey.isPressed) { bsTimer += Time.deltaTime; if (bsTimer > 0.45f) { back = true; bsTimer = 0.38f; } } else bsTimer = 0f;
        if (back)
        {
            if (selAll) { text = ""; selAll = false; TypeSfx(0.8f); }
            else if (hgOn && hg.Back()) TypeSfx(0.8f);
            else if (Restricted && text.Length > 0) { string t = text.TrimEnd(); int sp = t.LastIndexOf(' '); text = sp < 0 ? "" : t.Substring(0, sp); TypeSfx(0.8f); }   // 카드 하나씩 지운다
            else if (ime.Length == 0 && text.Length > 0) { text = text.Substring(0, text.Length - 1); TypeSfx(0.8f); }
        }

        if (kb.escapeKey.wasPressedThisFrame) { SetFocus(false); return; }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            if (hgOn) FlushHangul();
            string send = text.Trim();
            if (send.Length == 0) { SetFocus(false); return; }
            Story1Net.SendChat(send);
            Sfx.Play("ui_next", 0.7f);   // 보냄
            text = ""; ime = ""; hg.Reset(); selAll = false;
            offset = 0;
        }
    }

    // ---------------------------------------------------------------- 그리기

    private void Refresh()
    {
        int v = VisibleRows;
        if (Mini)
        {
            // 접힘: 새 글이 오면 그 글의 첫 줄만 잠깐
            int head = rows.Count - 1;
            while (head > 0 && rows[head].name == null) head--;
            bool peek = Peeking && head >= 0;
            for (int i = 0; i < ExpandedRows; i++)
            {
                bool on = peek && i == 0;
                rowName[i].gameObject.SetActive(on);
                rowBody[i].SetActive(on);
                if (!on) continue;
                var r = rows[head];
                float y = (MiniH - RowH) * 0.5f;
                rowName[i].text = r.name ?? ""; rowName[i].color = r.nameCol;
                rowName[i].rectTransform.anchoredPosition = new Vector2(Pad, y); rowName[i].rectTransform.sizeDelta = new Vector2(r.indent + 2, RowH);
                string body = r.body + (head + 1 < rows.Count && rows[head + 1].name == null ? " …" : "");
                rowBody[i].Set(body, Cream, Pad + r.indent, y, RowH);
            }
            track.gameObject.SetActive(false);
            if (picker.gameObject.activeSelf) picker.gameObject.SetActive(false);
            return;
        }
        for (int i = 0; i < ExpandedRows; i++)
        {
            int idx = rows.Count - 1 - offset - (v - 1 - i);
            bool on = i < v && idx >= 0 && idx < rows.Count;
            rowName[i].gameObject.SetActive(on);
            rowBody[i].SetActive(on);
            if (!on) continue;
            var r = rows[idx];
            float y = Pad + InputH + 16f + (v - 1 - i) * RowH;
            var nrt = rowName[i].rectTransform;
            rowName[i].text = r.name ?? ""; rowName[i].color = r.nameCol;
            nrt.anchoredPosition = new Vector2(Pad, y); nrt.sizeDelta = new Vector2(r.indent + 2, RowH);
            rowBody[i].Set(r.body, Cream, Pad + r.indent, y, RowH);
        }

        // 스크롤 막대(열려 있고 넘칠 때만)
        bool bar = expanded && rows.Count > v;
        track.gameObject.SetActive(bar);
        if (bar)
        {
            float h = track.sizeDelta.y;
            float frac = (float)v / rows.Count;
            float th = Mathf.Max(24f, h * frac);
            float maxOff = rows.Count - v;
            float pos = (h - th) * (1f - offset / maxOff);   // offset 0 = 맨 아래
            var trt = (RectTransform)thumb.transform;
            trt.sizeDelta = new Vector2(0, th);
            trt.anchoredPosition = new Vector2(0, -pos);
        }

        // 입력 줄
        string shown;
        Color col = Cream;
        string comp = hgOn ? hg.Current : ime;
        bool blink = focused && (int)(Time.unscaledTime * 2f) % 2 == 0;
        if (text.Length == 0 && comp.Length == 0)
        {
            if (focused) shown = blink ? "_" : "";
            else { shown = Restricted ? "Enter  카드로 말하기" : "Enter  팀 채팅"; col = Dim; }
        }
        else
        {
            shown = text + comp + (blink && !selAll ? "_" : "");
            float maxW = PanelW - Pad * 2 - 8 - 16 - 76 - 52;
            while (shown.Length > 1 && Width(shown) * S > maxW) shown = shown.Substring(1);   // 길면 뒤쪽(쓰는 곳)만 보인다
        }
        inputLine.Set(shown, col, 16f, 0f, InputH - 8f);
        bool showSel = selAll && text.Length > 0;
        selBox.gameObject.SetActive(showSel);
        if (showSel)
        {
            var srt = selBox.rectTransform;
            srt.anchoredPosition = new Vector2(12f, 6f);
            srt.sizeDelta = new Vector2(Mathf.Min(Width(text) * S, PanelW - Pad * 2 - 160f) + 8f, InputH - 20f);
        }
        modeTag.text = focused && !imeSeen && !Restricted ? (hgOn ? "한" : "A") : "";

        // 이모티콘 선택창
        bool showPicker = focused && pickerOpen;
        if (picker.gameObject.activeSelf != showPicker) picker.gameObject.SetActive(showPicker);
        emojiBtnBg.color = showPicker ? new Color32(204, 159, 79, 120) : new Color(0, 0, 0, 0);
        if (showPicker)
        {
            int hov = -1;
            var mouse = Mouse.current;
            if (mouse != null) hov = CellAt(mouse.position.ReadValue());
            hoverBox.gameObject.SetActive(hov >= 0);
            if (hov >= 0)
            {
                var hrt = hoverBox.rectTransform;
                Vector2 c = CellCenter(hov) + picker.sizeDelta * 0.5f;   // 왼쪽 아래 기준
                hrt.anchoredPosition = new Vector2(c.x - hrt.sizeDelta.x * 0.5f, c.y - picker.sizeDelta.y + hrt.sizeDelta.y * 0.5f);
            }
            pickerTitle.text = hov >= 0 ? MuseumEmoji.Labels[hov] : "이모티콘";
            pickerTitle.color = hov >= 0 ? Cream : Gold;
        }
    }
}
