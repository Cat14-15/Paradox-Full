using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainStory1 의 화면 표시(모두 도트 글꼴 · 프롤로그와 같은 판 그림).
///  · 왼쪽 위: 장소 이름 + 조작 키(방에 들어갈 때 · H 키로 잠깐)
///  · 왼쪽 아래: 지금 미션 카드 하나, 그 위에 속마음
///  · 위 가운데: 알림(새 낱말, 잠긴 문 …)
///  · 머리 위: E 안내 / 캐릭터 이름표(정전 중엔 손전등 안만)
///  · 다음 목표 표시, 전시물 설명 창, 배운 말 수첩(N), 일시 정지(Esc)
/// </summary>
public sealed class MuseumHud : MonoBehaviour
{
    private static MuseumHud I;

    private Canvas hud, modal;
    private float sf = 1f;

    // 장소
    private RectTransform banner; private CanvasGroup bannerGroup; private PixelText placeText, keysText, perkText, helpText, help2Text;
    private float bannerUntil; private string lastPlace;

    // 방에 들어갈 때 잠깐 뜨는 장소 이름(작게)
    private RectTransform placeChip; private CanvasGroup placeChipGroup; private PixelText placeChipText;
    private float placeChipUntil;

    // 말풍선(채팅한 사람 머리 위)
    private readonly RectTransform[] bubbles = new RectTransform[4];
    private readonly PixelText[] bubbleText = new PixelText[4];
    private readonly float[] bubbleUntil = new float[4];
    private Vector3 myHead; private bool myHeadOk;

    // 미션 카드 · 속마음
    private const float CardW = 560f, CardText = 504f;
    private static bool cardMini;
    private PxButton cardFold;
    private RectTransform card; private CanvasGroup cardGroup; private PixelText cardTitle, cardPlace, cardTodo, cardProg;
    private RectTransform thought; private CanvasGroup thoughtGroup; private PixelText thoughtName, thoughtText;
    private readonly Queue<string> thoughts = new Queue<string>();
    private float thoughtUntil = -1f;
    private int cardStage = -1; private string cardProgText;

    // 알림
    private RectTransform toast; private CanvasGroup toastGroup; private PixelText toastText; private Image toastEdge;
    private readonly Queue<(string, Color)> toasts = new Queue<(string, Color)>();
    private float toastUntil = -1f;

    // 머리 위 안내 · 이름표 · 목표 표시
    private RectTransform prompt; private PixelText promptText; private Image promptBack;
    private readonly RectTransform[] tags = new RectTransform[4]; private readonly PixelText[] tagText = new PixelText[4], tagShadow = new PixelText[4];
    private RectTransform guide; private PixelText guideText;
    private RectTransform guideArrow; private PixelText guideArrowText, guideArrowShadow;
    public static bool PromptClicked;

    // 창(설명 · 수첩 · 일시 정지)
    private RectTransform inspect; private PixelText inspTitle, inspText, inspWord; private PxButton inspClose; private UnityEngine.UI.Image inspPic;
    private RectTransform book; private PixelText bookTitle; private readonly PixelText[] bookCols = new PixelText[3]; private PxButton bookClose;
    private RectTransform pause; private PixelText sfxText, bgmText; private PxButton resumeBtn, sfxDown, sfxUp, bgmDown, bgmUp, leaveBtn;
    private int modalFrame;
    private bool chatHiddenByModal;

    public static bool ModalOpen => I != null && (I.inspect.gameObject.activeSelf || I.book.gameObject.activeSelf || I.pause.gameObject.activeSelf || I.map.gameObject.activeSelf);
    public static bool PauseOpen => I != null && I.pause.gameObject.activeSelf;

    private void Awake()
    {
        I = this;
        hud = MuseumUI.MakeCanvas(transform, "HudCanvas", 94);
        modal = MuseumUI.MakeCanvas(transform, "ModalCanvas", 105);
        BuildBanner(); BuildCard(); BuildToast(); BuildWorldMarks(); BuildInspect(); BuildBook(); BuildPause(); BuildMap();
        modals = new[] { inspect, book, pause, map };
        BuildPings(); BuildTips(); BuildRotate(); BuildClock();
        MuseumWords.NewWord += OnNewWord;
        MuseumNet.PingReceived += OnPing;
    }

    private RectTransform[] modals;

    /// <summary>화면 좌표(왼쪽 아래 기준)가 지금 떠 있는 수첩·설명·지도·메뉴 창 위인가.</summary>
    public static bool ModalCovers(Vector3 screen)
    {
        if (I == null || I.modals == null) return false;
        foreach (var d in I.modals)
        {
            if (d == null || !d.gameObject.activeSelf) continue;
            var pnl = (RectTransform)d.Find("Panel");
            if (pnl != null && RectTransformUtility.RectangleContainsScreenPoint(pnl, screen, null)) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- 팀원 핑(Q · 휠 클릭)
    private struct PingMark { public bool on; public string floor, room, place; public Vector2 map; public float until; }
    private readonly PingMark[] pings = new PingMark[4];
    private readonly RectTransform[] pingNodes = new RectTransform[4];
    private readonly PixelText[] pingArrow = new PixelText[4], pingName = new PixelText[4];
    private readonly RectTransform[] pingLabel = new RectTransform[4];
    private readonly CanvasGroup[] pingGroup = new CanvasGroup[4];
    private float nextPing;

    private void BuildPings()
    {
        for (int i = 0; i < 4; i++)
        {
            var n = MuseumUI.Node("Ping" + i, hud.transform);
            n.anchorMin = n.anchorMax = Vector2.zero; n.pivot = new Vector2(0.5f, 0.5f); n.sizeDelta = new Vector2(56f, 56f);
            pingGroup[i] = n.gameObject.AddComponent<CanvasGroup>();
            pingArrow[i] = MuseumUI.Text(n, "Arrow", "▼", 3, Roles.Color(i), TextAnchor.MiddleCenter);
            MuseumUI.Stretch(pingArrow[i].rectTransform);
            var lab = MuseumUI.Node("Label", n);
            lab.anchorMin = lab.anchorMax = new Vector2(0.5f, 0.5f); lab.pivot = new Vector2(0.5f, 0.5f);
            MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", lab)), MuseumUI.FillSprite, new Color(0.05f, 0.06f, 0.09f, 0.9f));
            MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Edge", lab)), MuseumUI.BorderSprite, Roles.Color(i));
            pingName[i] = MuseumUI.Text(lab, "Name", "", 2, Roles.Color(i), TextAnchor.MiddleCenter);
            MuseumUI.Stretch(pingName[i].rectTransform, 6);
            pingLabel[i] = lab;
            pingNodes[i] = n;
            n.gameObject.SetActive(false);
        }
    }

    private void OnPing(int slot, string floor, string room, Vector2 map)
    {
        if (slot < 0 || slot > 3) return;
        string place = room;
        var m = StrangerMuseum.Instance;
        if (m != null) foreach (var r in m.FloorRooms(floor)) if (r.id == room) { place = r.label; break; }
        string fl = floor == "1F" ? "1층" : floor == "2F" ? "2층" : "지하";
        pings[slot] = new PingMark { on = true, floor = floor, room = room, place = fl + " " + place, map = map, until = Time.time + 3f };
        Sfx.Play("chime", 0.5f);
    }

    /// <summary>Q: 내 자리, 휠 클릭: 마우스가 가리키는 곳(같은 방)에 핑.</summary>
    private void TrySendPing(StrangerMuseum m, bool atMouse)
    {
        if (m == null || Time.unscaledTime < nextPing) return;
        Vector2 map = m.PlayerMap;
        var cam = m.GetComponent<Camera>();
        var ms = Mouse.current;
        if (atMouse && cam != null && ms != null)
        {
            Vector2 sp = ms.position.ReadValue();
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
            map = new Vector2(w.x * 100f, -w.y * 100f);
        }
        nextPing = Time.unscaledTime + 1f;
        MuseumNet.Ping(m.CurFloor, m.CurRoom, map);
    }

    private static int FloorOrder(string f) => f == "B1" ? 0 : f == "1F" ? 1 : 2;

    private void UpdatePings(StrangerMuseum m, bool hidden)
    {
        float cw = Screen.width / sf, ch = Screen.height / sf;
        var cam = m != null ? m.GetComponent<Camera>() : null;
        for (int i = 0; i < 4; i++)
        {
            var pk = pings[i];
            bool on = pk.on && Time.time < pk.until && !hidden && m != null && cam != null;
            if (pingNodes[i].gameObject.activeSelf != on) pingNodes[i].gameObject.SetActive(on);
            if (!on) continue;
            pingGroup[i].alpha = (int)(Time.unscaledTime * 6f) % 2 == 0 ? 1f : 0.45f;
            string who = MuseumGame.NameOf(i);
            Vector2 c = new Vector2(cw * 0.5f, ch * 0.5f), pos; Vector2 dir; bool edge;
            if (pk.floor == m.CurFloor && pk.room == m.CurRoom)
            {
                Vector3 s = cam.WorldToScreenPoint(new Vector3(pk.map.x / 100f, -pk.map.y / 100f, 0f));
                pos = new Vector2(s.x / sf, s.y / sf);
                edge = s.z < 0f || pos.x < 40f || pos.x > cw - 40f || pos.y < 40f || pos.y > ch - 40f;
                dir = (pos - c).normalized;
            }
            else if (pk.floor == m.CurFloor)
            {
                Vector2 d = pk.map - m.PlayerMap;
                dir = new Vector2(d.x, -d.y).normalized; edge = true; pos = c;
            }
            else { dir = FloorOrder(pk.floor) > FloorOrder(m.CurFloor) ? Vector2.up : Vector2.down; edge = true; pos = c; }
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            string arrow;
            if (!edge) { arrow = "▼"; pos += new Vector2(0f, 26f); }
            else
            {
                float tx = Mathf.Abs(dir.x) > 0.001f ? (cw * 0.5f - 70f) / Mathf.Abs(dir.x) : float.MaxValue;
                float ty = Mathf.Abs(dir.y) > 0.001f ? (ch * 0.5f - 70f) / Mathf.Abs(dir.y) : float.MaxValue;
                pos = c + dir * Mathf.Min(tx, ty);
                arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x < 0 ? "◀" : "▶") : (dir.y < 0 ? "▼" : "▲");
            }
            pingArrow[i].text = arrow;
            string label = edge && !(pk.floor == m.CurFloor && pk.room == m.CurRoom) ? who + " · " + pk.place : who;
            pingName[i].text = label;
            float w = PixelFontData.Measure(label) * 2f + 24f;
            pingLabel[i].sizeDelta = new Vector2(w, 40f);
            // 이름표는 화살표 안쪽(화면 가운데 쪽)에
            Vector2 lo = !edge || arrow == "▼" ? new Vector2(0f, 46f) : arrow == "▲" ? new Vector2(0f, -46f) : arrow == "◀" ? new Vector2(w * 0.5f + 32f, 0f) : new Vector2(-w * 0.5f - 32f, 0f);
            pingLabel[i].anchoredPosition = lo;
            pos.x = Mathf.Clamp(pos.x, 36f, cw - 36f); pos.y = Mathf.Clamp(pos.y, 36f, ch - 36f);
            pingNodes[i].anchoredPosition = pos;
        }
    }

    // ---------------------------------------------------------------- 처음 하는 사람 안내(한 번만)
    private const string TipsKey = "stranger.tips1";
    private static readonly string[] TipLines =
    {
        "① 미션 카드와 목표 화살표를 보고 이동",
        "② 장치·전시물 앞에서  Z 또는 E",
        "③ 모르는 말은  N 수첩  ·  채팅 ? 카드로 묻기",
    };
    private RectTransform tips; private PixelText tipsText; private CanvasGroup tipsGroup;
    private int tipStep = -1; private bool tipsChecked; private float tipUntil;

    private void BuildTips()
    {
        tips = MuseumUI.Node("Tips", hud.transform);
        MuseumUI.Anchor(tips, 0.5f, 1f, 0f, -28f, 820f, 60f);
        MuseumUI.Panel(tips, null, MuseumUI.Mint);
        tipsGroup = tips.gameObject.AddComponent<CanvasGroup>(); tipsGroup.alpha = 0f;
        tipsText = MuseumUI.Text(tips, "Text", "", 2, MuseumUI.Ink, TextAnchor.MiddleCenter);
        MuseumUI.Stretch(tipsText.rectTransform, 10);
    }

    private void NextTip()
    {
        tipStep++;
        if (tipStep >= TipLines.Length) { tipStep = TipLines.Length; return; }
        tipUntil = Time.time + 8f;
        Sfx.Play("ui_next", 0.5f);
    }

    private void UpdateTips(bool story, bool blocked)
    {
        if (!tipsChecked && !story && MuseumGame.Stage == 0)
        {
            tipsChecked = true;
            int seen = 0; try { seen = PlayerPrefs.GetInt(TipsKey, 0); } catch { }
            if (seen == 0) { tipStep = 0; tipUntil = Time.time + 8f; try { PlayerPrefs.SetInt(TipsKey, 1); PlayerPrefs.Save(); } catch { } }
        }
        bool on = tipStep >= 0 && tipStep < TipLines.Length && !story && !blocked;
        if (on)
        {
            string t = TipLines[tipStep] + "     " + (tipStep + 1) + "/" + TipLines.Length + " ▶";
            if (tipsText.text != t) { tipsText.text = t; tips.sizeDelta = new Vector2(Mathf.Min(Screen.width / sf - 48f, PixelFontData.Measure(t) * 2f + 60f), 60f); tipsText.maxWidth = tips.sizeDelta.x - 20f; }
            var kb = Keyboard.current; var ms = Mouse.current;
            bool click = ms != null && ms.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(tips, ms.position.ReadValue(), null);
            if (Time.time > tipUntil || click || (kb != null && !MuseumChat.TypingNow && kb.spaceKey.wasPressedThisFrame)) NextTip();
            // 알림 · 장소 안내 아래에
            float y = -28f;
            if (toastGroup.alpha > 0.05f) y = toast.anchoredPosition.y - toast.sizeDelta.y - 10f;
            float left = (Screen.width / sf - tips.sizeDelta.x) * 0.5f;
            if (bannerGroup.alpha > 0.05f && left < 24f + banner.sizeDelta.x) y = Mathf.Min(y, -24f - banner.sizeDelta.y - 12f);
            else if (placeChipGroup.alpha > 0.05f && left < 24f + placeChip.sizeDelta.x) y = Mathf.Min(y, -24f - placeChip.sizeDelta.y - 12f);
            tips.anchoredPosition = new Vector2(0f, y);
        }
        tipsGroup.alpha = Mathf.MoveTowards(tipsGroup.alpha, on ? 1f : 0f, Time.deltaTime * 5f);
    }

    // ---------------------------------------------------------------- 세로 화면(모바일): 가로로 돌려 달라고 덮는다
    private Canvas rotateCanvas; private bool pausedByPortrait; private float savedScale = 1f;

    private void BuildRotate()
    {
        rotateCanvas = MuseumUI.MakeCanvas(transform, "RotateCanvas", 300);
        var bg = MuseumUI.Node("Back", rotateCanvas.transform); MuseumUI.Stretch(bg);
        var img = MuseumUI.Img(bg, null, new Color(0.03f, 0.03f, 0.05f, 0.97f)); img.raycastTarget = true;
        var t = MuseumUI.Text(bg, "Text", "가로로 돌려 주세요", 4, MuseumUI.Amber, TextAnchor.MiddleCenter);
        MuseumUI.Anchor(t.rectTransform, 0.5f, 0.5f, 0f, 30f, 900f, 90f); t.maxWidth = 900f;
        var s = MuseumUI.Text(bg, "Sub", "화면을 가로로 돌리면 이어서 할 수 있어요", 2, MuseumUI.Ink, TextAnchor.MiddleCenter);
        MuseumUI.Anchor(s.rectTransform, 0.5f, 0.5f, 0f, -50f, 900f, 50f); s.maxWidth = 900f;
        rotateCanvas.gameObject.SetActive(false);
    }

    private void UpdateRotate()
    {
        bool portrait = Screen.height > Screen.width;
        if (rotateCanvas.gameObject.activeSelf != portrait) rotateCanvas.gameObject.SetActive(portrait);
        MuseumUI.InputBlocked = portrait;
        // 혼자 할 때는 세로인 동안 시간을 멈춘다(온라인은 다른 사람이 있어 멈추지 않는다)
        bool pause = portrait && !Story1Net.Online;
        if (pause && !pausedByPortrait) { savedScale = Time.timeScale; Time.timeScale = 0f; pausedByPortrait = true; }
        else if (!pause && pausedByPortrait) { Time.timeScale = savedScale > 0f ? savedScale : 1f; pausedByPortrait = false; }
    }

    private void OnDestroy()
    {
        MuseumWords.NewWord -= OnNewWord; MuseumNet.PingReceived -= OnPing;
        if (pausedByPortrait) Time.timeScale = savedScale > 0f ? savedScale : 1f;
        MuseumUI.InputBlocked = false;
        if (I == this) I = null;
    }

    private void OnNewWord(MuseumWords.Entry e)
    {
        Toast("새 낱말  " + e.word + " = " + e.meaning + "  (" + MuseumVoice.Language[e.lang] + ")", MuseumUI.Mint);
        Sfx.Play("chime", 0.4f);
    }

    // ---------------------------------------------------------------- 만들기
    private static PixelText Label(Transform p, string name, int scale, Color c, TextAnchor a, float ax, float ay, float x, float y, float w, float h)
    {
        var t = MuseumUI.Text(p, name, "", scale, c, a);
        MuseumUI.Anchor(t.rectTransform, ax, ay, x, y, w, h);
        t.maxWidth = w;
        return t;
    }

    private void BuildBanner()
    {
        banner = MuseumUI.Node("Banner", hud.transform);
        MuseumUI.Anchor(banner, 0f, 1f, 24f, -24f, 820f, 250f);
        MuseumUI.Panel(banner);
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>(); bannerGroup.alpha = 0f;
        placeText = Label(banner, "Place", 2, MuseumUI.Amber, TextAnchor.MiddleLeft, 0f, 1f, 28f, -16f, 764f, 40f);
        keysText = Label(banner, "Keys", 2, MuseumUI.Ink, TextAnchor.MiddleLeft, 0f, 1f, 28f, -62f, 764f, 40f);
        helpText = Label(banner, "Help", 2, MuseumUI.Ink, TextAnchor.MiddleLeft, 0f, 1f, 28f, -104f, 764f, 40f);
        help2Text = Label(banner, "Help2", 2, MuseumUI.Slate, TextAnchor.MiddleLeft, 0f, 1f, 28f, -146f, 764f, 40f);
        perkText = Label(banner, "Perk", 2, MuseumUI.Mint, TextAnchor.MiddleLeft, 0f, 1f, 28f, -192f, 764f, 40f);
        keysText.text = "WASD 이동   Z/E 조사·조작   X 닫기   Esc 메뉴";
        helpText.text = "Enter 카드로 말하기   C 채팅 접기   N 수첩·복습   Q 핑";
        help2Text.text = "H 이 도움말   M 지도(내비게이터만)   ? 카드 = 다시 말해 줘";

        placeChip = MuseumUI.Node("PlaceChip", hud.transform);
        MuseumUI.Anchor(placeChip, 0f, 1f, 24f, -24f, 300f, 52f);
        MuseumUI.Panel(placeChip);
        placeChipGroup = placeChip.gameObject.AddComponent<CanvasGroup>(); placeChipGroup.alpha = 0f;
        placeChipText = MuseumUI.Text(placeChip, "Text", "", 2, MuseumUI.Amber, TextAnchor.MiddleCenter);
        MuseumUI.Stretch(placeChipText.rectTransform, 8);
    }

    private void BuildCard()
    {
        card = MuseumUI.Node("Mission", hud.transform);
        MuseumUI.Anchor(card, 0f, 0f, 24f, 24f, CardW, 200f);
        MuseumUI.Panel(card, null, MuseumUI.Teal);
        cardGroup = card.gameObject.AddComponent<CanvasGroup>(); cardGroup.alpha = 0f;
        cardTitle = Label(card, "Title", 2, MuseumUI.Amber, TextAnchor.UpperLeft, 0f, 1f, 24f, -16f, CardText - 52f, 36f);
        cardPlace = Label(card, "Place", 2, MuseumUI.Slate, TextAnchor.UpperLeft, 0f, 1f, 24f, -58f, CardText, 36f);
        cardTodo = Label(card, "Todo", 2, MuseumUI.Ink, TextAnchor.UpperLeft, 0f, 1f, 24f, -100f, CardText, 120f);
        cardTodo.maxWidth = 0f;
        cardProg = Label(card, "Prog", 2, MuseumUI.Mint, TextAnchor.LowerLeft, 0f, 0f, 24f, 14f, CardText, 36f);
        // 접기 단추(오른쪽 위): 접으면 제목 + 진행만
        cardFold = MuseumUI.Button(card, "Fold", "▼", 2, () => { cardMini = !cardMini; cardStage = -1; });
        MuseumUI.Anchor(cardFold.Rect, 1f, 1f, -10f, -10f, 48f, 38f);
        cardFold.Sound = "ui_select";

        thought = MuseumUI.Node("Thought", hud.transform);
        MuseumUI.Anchor(thought, 0f, 0f, 24f, 240f, CardW, 112f);
        MuseumUI.Panel(thought);
        thoughtGroup = thought.gameObject.AddComponent<CanvasGroup>(); thoughtGroup.alpha = 0f;
        thoughtName = Label(thought, "Name", 2, MuseumUI.Amber, TextAnchor.UpperLeft, 0f, 1f, 24f, -12f, CardText, 34f);
        thoughtText = Label(thought, "Text", 2, new Color(0.75f, 0.87f, 1f), TextAnchor.UpperLeft, 0f, 1f, 24f, -52f, CardText, 64f);
        thoughtText.maxWidth = 0f;
    }

    private void BuildToast()
    {
        toast = MuseumUI.Node("Toast", hud.transform);
        MuseumUI.Anchor(toast, 0.5f, 1f, 0f, -28f, 900f, 64f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", toast)), MuseumUI.FillSprite, MuseumUI.Fill);
        toastEdge = MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Edge", toast)), MuseumUI.BorderSprite, MuseumUI.Gold);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0f;
        toastText = MuseumUI.Text(toast, "Text", "", 2, MuseumUI.Ink, TextAnchor.MiddleCenter);
        MuseumUI.Stretch(toastText.rectTransform, 12);
    }

    private void BuildWorldMarks()
    {
        for (int i = 0; i < 4; i++)
        {
            tags[i] = MuseumUI.Node("Tag" + i, hud.transform);
            MuseumUI.Anchor(tags[i], 0.5f, 0.5f, 0f, 0f, 240f, 32f);
            tags[i].anchorMin = tags[i].anchorMax = Vector2.zero;
            var bg = MuseumUI.Node("Shadow", tags[i]); MuseumUI.Stretch(bg);
            tagText[i] = MuseumUI.Text(tags[i], "Text", "", 2, Roles.Color(i), TextAnchor.MiddleCenter);
            MuseumUI.Stretch(tagText[i].rectTransform);
            var sh = bg.gameObject.AddComponent<PixelText>(); sh.scale = 2; sh.align = TextAnchor.MiddleCenter; sh.color = new Color(0, 0, 0, 0.9f); sh.raycastTarget = false;
            bg.offsetMin = new Vector2(2f, -2f); bg.offsetMax = new Vector2(2f, -2f);
            tagShadow[i] = sh;
            tags[i].gameObject.SetActive(false);
        }

        prompt = MuseumUI.Node("Prompt", hud.transform);
        prompt.anchorMin = prompt.anchorMax = Vector2.zero; prompt.pivot = new Vector2(0.5f, 0f);
        promptBack = MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", prompt)), MuseumUI.FillSprite, MuseumUI.Fill);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Edge", prompt)), MuseumUI.BorderSprite, MuseumUI.Amber);
        var key = MuseumUI.Node("Key", prompt);
        MuseumUI.Anchor(key, 0f, 0.5f, 10f, 0f, 44f, 44f);
        MuseumUI.Img(key, null, MuseumUI.Amber);
        var kt = MuseumUI.Text(key, "K", "Z", 2, new Color(0.14f, 0.09f, 0.06f), TextAnchor.MiddleCenter); MuseumUI.Stretch(kt.rectTransform);
        promptText = MuseumUI.Text(prompt, "Text", "", 2, MuseumUI.Ink, TextAnchor.MiddleLeft);
        MuseumUI.Anchor(promptText.rectTransform, 0f, 0.5f, 66f, 0f, 600f, 44f);
        prompt.gameObject.SetActive(false);

        guide = MuseumUI.Node("Guide", hud.transform);
        guide.anchorMin = guide.anchorMax = Vector2.zero; guide.pivot = new Vector2(0.5f, 0.5f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", guide)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.12f, 0.85f));
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Edge", guide)), MuseumUI.BorderSprite, MuseumUI.Mint);
        guideText = MuseumUI.Text(guide, "Text", "", 2, MuseumUI.Mint, TextAnchor.MiddleCenter);
        MuseumUI.Stretch(guideText.rectTransform, 6);
        guide.gameObject.SetActive(false);

        // 목표 바로 위에 붙는 작은 화살표(통통 튄다)
        guideArrow = MuseumUI.Node("GuideArrow", hud.transform);
        guideArrow.anchorMin = guideArrow.anchorMax = Vector2.zero; guideArrow.pivot = new Vector2(0.5f, 0.5f);
        guideArrow.sizeDelta = new Vector2(56f, 56f);
        var ash = MuseumUI.Node("Shadow", guideArrow); MuseumUI.Stretch(ash); ash.offsetMin = new Vector2(3f, -3f); ash.offsetMax = new Vector2(3f, -3f);
        guideArrowShadow = ash.gameObject.AddComponent<PixelText>(); guideArrowShadow.scale = 3; guideArrowShadow.align = TextAnchor.MiddleCenter; guideArrowShadow.color = new Color(0f, 0f, 0f, 0.85f); guideArrowShadow.raycastTarget = false;
        guideArrowText = MuseumUI.Text(guideArrow, "Arrow", "▼", 3, MuseumUI.Mint, TextAnchor.MiddleCenter);
        MuseumUI.Stretch(guideArrowText.rectTransform);
        guideArrow.gameObject.SetActive(false);

        for (int i = 0; i < 4; i++)
        {
            bubbles[i] = MuseumUI.Node("Bubble" + i, hud.transform);
            bubbles[i].anchorMin = bubbles[i].anchorMax = Vector2.zero; bubbles[i].pivot = new Vector2(0.5f, 0f);
            MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", bubbles[i])), MuseumUI.FillSprite, new Color(0.96f, 0.93f, 0.86f, 0.96f));
            MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Edge", bubbles[i])), MuseumUI.BorderSprite, Roles.Color(i));
            bubbleText[i] = MuseumUI.Text(bubbles[i], "Text", "", 2, new Color(0.14f, 0.1f, 0.08f), TextAnchor.MiddleCenter);
            MuseumUI.Stretch(bubbleText[i].rectTransform, 6);
            bubbles[i].gameObject.SetActive(false);
        }
    }

    /// <summary>채팅한 사람 머리 위에 잠깐 말풍선(짧게 자른다. 도트 글꼴에 없는 글자·이모티콘은 뺀다).</summary>
    public static void Bubble(int slot, string text)
    {
        if (I == null || slot < 0 || slot > 3 || string.IsNullOrEmpty(text)) return;
        var sb = new System.Text.StringBuilder();
        foreach (char c in text) if (c == ' ' || PixelFontData.Has(c)) sb.Append(c);
        string s = System.Text.RegularExpressions.Regex.Replace(sb.ToString().Trim(), " {2,}", " ");
        if (s.Length == 0) return;
        const int max = 150;   // 글꼴 칸 단위(배율 2 → 300)
        if (PixelFontData.Measure(s) > max)
        {
            while (s.Length > 1 && PixelFontData.Measure(s + "…") > max) s = s.Substring(0, s.Length - 1);
            s = s.TrimEnd() + "…";
        }
        I.bubbleText[slot].text = s;
        I.bubbles[slot].sizeDelta = new Vector2(PixelFontData.Measure(s) * 2f + 28f, 44f);
        I.bubbleUntil[slot] = Time.time + 3f;
    }

    private RectTransform ModalPanel(string name, float w, float h)
    {
        var dim = MuseumUI.Node(name, modal.transform); MuseumUI.Stretch(dim);
        MuseumUI.Img(dim, null, new Color(0f, 0f, 0f, 0.45f));
        var p = MuseumUI.Node("Panel", dim);
        MuseumUI.Anchor(p, 0.5f, 0.5f, 0f, 20f, w, h);
        MuseumUI.Panel(p);
        dim.gameObject.SetActive(false);
        return dim;
    }

    private void BuildInspect()
    {
        inspect = ModalPanel("Inspect", 980f, 460f);
        var p = (RectTransform)inspect.Find("Panel");
        inspTitle = Label(p, "Title", 3, MuseumUI.Amber, TextAnchor.MiddleLeft, 0f, 1f, 40f, -30f, 900f, 52f);
        inspText = Label(p, "Text", 2, MuseumUI.Ink, TextAnchor.UpperLeft, 0f, 1f, 40f, -104f, 900f, 200f);
        inspText.maxWidth = 0f;
        inspWord = Label(p, "Word", 2, MuseumUI.Mint, TextAnchor.MiddleLeft, 0f, 0f, 40f, 40f, 640f, 40f);
        // 그림(단풍이 보상 등) · 오른쪽 위, 없으면 숨긴다
        var pic = MuseumUI.Node("Pic", p); MuseumUI.Anchor(pic, 1f, 1f, -40f, -24f, 192f, 192f);
        inspPic = MuseumUI.Img(pic, null, Color.white); inspPic.preserveAspect = true; inspPic.gameObject.SetActive(false);
        inspClose = MuseumUI.Button(p, "Close", "닫기  X", 2, CloseModalsInternal);
        MuseumUI.Anchor(inspClose.Rect, 1f, 0f, -32f, 28f, 200f, 60f);
        inspClose.Sound = "ui_back";
    }

    private void BuildBook()
    {
        book = ModalPanel("Book", 1240f, 720f);
        var p = (RectTransform)book.Find("Panel");
        bookTitle = Label(p, "Title", 3, MuseumUI.Amber, TextAnchor.MiddleLeft, 0f, 1f, 40f, -30f, 900f, 52f);
        Label(p, "Sub", 2, MuseumUI.Slate, TextAnchor.MiddleLeft, 0f, 1f, 40f, -88f, 1160f, 40f).text = "동료가 신호와 함께 한 말, 전시물에서 읽은 말";
        for (int i = 0; i < 3; i++)
        {
            var col = MuseumUI.Node("Col" + i, p);
            MuseumUI.Anchor(col, 0f, 1f, 40f + i * 390f, -150f, 370f, 470f);
            MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", col)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.13f, 1f));
            var head = Label(col, "Head", 2, Roles.Color(i + 1), TextAnchor.MiddleCenter, 0.5f, 1f, 0f, -12f, 340f, 40f);
            head.text = MuseumVoice.Language[i + 1] + " · " + Roles.DefaultName(i + 1);
            bookCols[i] = Label(col, "List", 2, MuseumUI.Ink, TextAnchor.UpperLeft, 0f, 1f, 18f, -66f, 334f, 390f);
            bookCols[i].maxWidth = 0f;
        }
        bookClose = MuseumUI.Button(p, "Close", "닫기  X", 2, CloseModalsInternal);
        MuseumUI.Anchor(bookClose.Rect, 1f, 0f, -32f, 28f, 200f, 60f);
        bookClose.Sound = "ui_back";

        // 복습: 배운 낱말 4지선다
        quizBtn = MuseumUI.Button(p, "Quiz", "복습", 2, ToggleQuiz);
        MuseumUI.Anchor(quizBtn.Rect, 1f, 0f, -252f, 28f, 200f, 60f);
        quizBtn.Accent = MuseumUI.Mint;
        quizRoot = MuseumUI.Node("QuizRoot", p); MuseumUI.Stretch(quizRoot);
        quizScore = Label(quizRoot, "Score", 2, MuseumUI.Mint, TextAnchor.MiddleRight, 1f, 1f, -40f, -36f, 400f, 40f);
        quizQ = Label(quizRoot, "Q", 4, MuseumUI.Amber, TextAnchor.MiddleCenter, 0.5f, 1f, 0f, -200f, 1100f, 80f);
        quizHint = Label(quizRoot, "Hint", 2, MuseumUI.Slate, TextAnchor.MiddleCenter, 0.5f, 1f, 0f, -262f, 1100f, 40f);
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            quizOpts[i] = MuseumUI.Button(quizRoot, "Opt" + i, "", 2, () => PickQuiz(k));
            MuseumUI.Anchor(quizOpts[i].Rect, 0.5f, 1f, (i % 2 == 0 ? -280f : 280f), -350f - (i / 2) * 100f, 520f, 80f);
        }
        quizResult = Label(quizRoot, "Result", 2, MuseumUI.Ink, TextAnchor.MiddleCenter, 0.5f, 0f, 0f, 110f, 1100f, 40f);
        quizRoot.gameObject.SetActive(false);
    }

    // ---- 복습 퀴즈
    private PxButton quizBtn; private RectTransform quizRoot; private PixelText quizQ, quizHint, quizScore, quizResult;
    private readonly PxButton[] quizOpts = new PxButton[4];
    private bool quizMode, quizAnswered; private int quizAnswer = -1, quizRight, quizTotal; private float quizNextAt = -1f;
    private string quizLastWord;

    private void ToggleQuiz()
    {
        quizMode = !quizMode;
        if (quizMode) NextQuiz();
        RefreshBook();
    }

    private void NextQuiz()
    {
        quizAnswered = false; quizNextAt = -1f; quizResult.text = "";
        foreach (var b in quizOpts) b.Selected = false;
        var learned = MuseumWords.Learned;
        if (learned.Count == 0)
        {
            quizQ.text = "아직 배운 말이 없다"; quizHint.text = "동료의 말과 전시물에서 낱말을 모아 보자";
            foreach (var b in quizOpts) b.SetActive(false);
            quizAnswer = -1;
            return;
        }
        MuseumWords.Entry e = learned[Random.Range(0, learned.Count)];
        if (learned.Count > 1) for (int t = 0; t < 5 && e.word == quizLastWord; t++) e = learned[Random.Range(0, learned.Count)];
        quizLastWord = e.word;
        quizQ.text = e.word + " = ?";
        quizHint.text = MuseumVoice.Language[Mathf.Clamp(e.lang, 0, 3)] + " · 뜻을 골라요";
        // 보기: 정답 + 다른 뜻 3개(배운 말 → 사전 순으로 채운다)
        var opts = new List<string> { e.meaning };
        foreach (var o in learned) { if (opts.Count >= 4) break; if (!opts.Contains(o.meaning)) opts.Add(o.meaning); }
        if (opts.Count < 4)
        {
            var pool = new List<string>();
            foreach (var kv in MuseumVoice.Glossary) if (!opts.Contains(kv.Value.meaning) && !pool.Contains(kv.Value.meaning)) pool.Add(kv.Value.meaning);
            while (opts.Count < 4 && pool.Count > 0) { int r = Random.Range(0, pool.Count); opts.Add(pool[r]); pool.RemoveAt(r); }
        }
        // 정답 뒤 보기도 섞는다
        for (int i = opts.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (opts[i], opts[j]) = (opts[j], opts[i]); }
        quizAnswer = opts.IndexOf(e.meaning);
        for (int i = 0; i < 4; i++)
        {
            bool on = i < opts.Count;
            quizOpts[i].SetActive(on);
            if (on) { quizOpts[i].SetText(opts[i]); quizOpts[i].Label.maxWidth = 500f; }
        }
    }

    private void PickQuiz(int i)
    {
        if (quizAnswered || quizAnswer < 0) return;
        quizAnswered = true; quizTotal++;
        quizOpts[quizAnswer].Selected = true;
        if (i == quizAnswer) { quizRight++; quizResult.text = "정답!"; quizResult.color = MuseumUI.Mint; Sfx.Play("chime", 0.5f); }
        else { quizResult.text = "아쉬워요 · 정답은 " + quizOpts[quizAnswer].Label.text; quizResult.color = MuseumUI.Red; Sfx.Play("ui_back", 0.5f); }
        quizScore.text = "맞힌 수  " + quizRight + " / " + quizTotal;
        quizNextAt = Time.unscaledTime + 1.4f;
    }

    // ---------------------------------------------------------------- 지도(내비게이터 능력)
    private RectTransform map, mapArea, mapContent; private Image mapImg; private PixelText mapTitle, mapGoal;
    private readonly RectTransform[] mapDots = new RectTransform[4]; private readonly PixelText[] mapNames = new PixelText[4];
    private PixelText mapStar; private string mapFloor, mapFitted;
    private float mapScale = 1f;
    private readonly List<(string id, RectTransform box, Image fill, PixelText name)> mapRooms = new List<(string, RectTransform, Image, PixelText)>();
    private readonly PxButton[] floorBtns = new PxButton[3];
    private const float MapW = 1100f, MapH = 860f;

    public static bool CanSeeMap => MuseumGame.IHave(MuseumGame.Navigator);

    private static string FloorName(string f) => f == "1F" ? "1층" : f == "2F" ? "2층" : "지하";

    private void BuildMap()
    {
        map = ModalPanel("Map", MapW + 380f, MapH + 100f);
        var p = (RectTransform)map.Find("Panel");
        mapTitle = Label(p, "Title", 3, MuseumUI.Amber, TextAnchor.MiddleLeft, 0f, 1f, 36f, -30f, 300f, 52f);
        Label(p, "Who", 2, MuseumUI.Mint, TextAnchor.UpperLeft, 0f, 1f, 36f, -96f, 300f, 40f).text = "내비게이터의 지도";
        mapArea = MuseumUI.Node("Area", p); MuseumUI.Anchor(mapArea, 1f, 0.5f, -36f, 0f, MapW, MapH);
        MuseumUI.Img(mapArea, null, new Color(0.02f, 0.02f, 0.03f, 1f));
        mapArea.gameObject.AddComponent<RectMask2D>();
        mapContent = MuseumUI.Node("Content", mapArea);
        mapContent.anchorMin = mapContent.anchorMax = mapContent.pivot = new Vector2(0f, 1f);
        var im = MuseumUI.Node("Image", mapContent); MuseumUI.Stretch(im);
        mapImg = MuseumUI.Img(im, null, new Color(0.42f, 0.4f, 0.4f, 1f));   // 바닥 그림은 어둡게, 방 이름이 잘 보이게
        for (int i = 0; i < 3; i++)
        {
            string fid = StrangerMuseum.MapFloors[i];
            var b = MuseumUI.Button(p, "Floor" + i, FloorName(fid), 2, () => mapFloor = fid);
            MuseumUI.Anchor(b.Rect, 0f, 1f, 36f, -160f - i * 72f, 200f, 60f);
            floorBtns[i] = b;
        }
        // 누가 어떤 색인지
        for (int i = 0; i < 4; i++)
        {
            var sw = MuseumUI.Node("Key" + i, p); MuseumUI.Anchor(sw, 0f, 1f, 36f, -400f - i * 44f, 26f, 26f);
            MuseumUI.Img(sw, null, Roles.Color(i));
            Label(p, "KeyName" + i, 2, MuseumUI.Ink, TextAnchor.MiddleLeft, 0f, 1f, 76f, -400f - i * 44f, 240f, 36f).text = MuseumGame.NameOf(i) + (i == Story1Role.Current ? " (나)" : "");
        }
        mapGoal = Label(p, "Goal", 2, MuseumUI.Amber, TextAnchor.UpperLeft, 0f, 0f, 36f, 200f, 300f, 90f);
        var close = MuseumUI.Button(p, "Close", "닫기  X", 2, CloseModalsInternal);
        MuseumUI.Anchor(close.Rect, 0f, 0f, 36f, 36f, 200f, 60f);
        close.Sound = "ui_back";
        mapStar = MuseumUI.Text(mapContent, "Goal", "★", 4, MuseumUI.Amber, TextAnchor.MiddleCenter);
        mapStar.rectTransform.anchorMin = mapStar.rectTransform.anchorMax = new Vector2(0f, 1f);
        mapStar.rectTransform.sizeDelta = new Vector2(64f, 64f);
        for (int i = 0; i < 4; i++)
        {
            mapDots[i] = MuseumUI.Node("Dot" + i, mapContent); mapDots[i].anchorMin = mapDots[i].anchorMax = new Vector2(0f, 1f);
            mapDots[i].sizeDelta = new Vector2(38f, 38f);
            MuseumUI.Img(mapDots[i], null, Roles.Color(i));
            mapNames[i] = MuseumUI.Text(mapDots[i], "Name", "", 2, Color.black, TextAnchor.MiddleCenter);
            MuseumUI.Stretch(mapNames[i].rectTransform);
        }
    }

    public static void ToggleMap()
    {
        if (I == null) return;
        if (I.map.gameObject.activeSelf) { CloseModals(); return; }
        if (!CanSeeMap) { string n = MuseumGame.NameOf(MuseumGame.Navigator); Toast("지도는 내비게이터 " + n + "에게만 있다 · " + n + "에게 길을 묻기", MuseumUI.Amber); Sfx.Play("ui_back", 0.5f); return; }
        MuseumGame.CloseAllPanels();
        var m = StrangerMuseum.Instance;
        I.mapFloor = m != null ? m.CurFloor : "1F";
        I.mapFitted = null;
        I.Show(I.map);
        I.RefreshMap();
        Sfx.Play("book_open", 0.55f);
    }

    private Vector2 MapPos(Vector2 px) => new Vector2(px.x * mapScale, -px.y * mapScale);

    private void FitMap(StrangerMuseum m)
    {
        if (mapFitted == mapFloor) return;
        mapFitted = mapFloor;
        mapImg.sprite = m.FloorSprite(mapFloor);
        foreach (var r in mapRooms) Destroy(r.box.gameObject);
        mapRooms.Clear();
        foreach (var (id, label, rect) in m.FloorRooms(mapFloor))
        {
            var box = MuseumUI.Node("Room_" + id, mapContent);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
            box.SetAsFirstSibling(); mapImg.transform.SetAsFirstSibling();
            var fill = MuseumUI.Img(box, null, new Color(1f, 0.75f, 0.3f, 0f));
            fill.raycastTarget = false;
            var name = MuseumUI.Text(box, "Name", label, 2, MuseumUI.Ink, TextAnchor.MiddleCenter);
            MuseumUI.Stretch(name.rectTransform);
            mapRooms.Add((id, box, fill, name));
        }
        Rect b = m.FloorBounds(mapFloor);
        b.xMin -= 40f; b.yMin -= 40f; b.xMax += 40f; b.yMax += 40f;
        mapScale = Mathf.Min(MapW / b.width, MapH / b.height);
        mapContent.sizeDelta = new Vector2(2048f * mapScale, 1664f * mapScale);
        // 건물이 가운데 오게
        mapContent.anchoredPosition = new Vector2(-b.xMin * mapScale + (MapW - b.width * mapScale) * 0.5f, b.yMin * mapScale - (MapH - b.height * mapScale) * 0.5f);
        var rooms = m.FloorRooms(mapFloor);
        for (int i = 0; i < mapRooms.Count && i < rooms.Count; i++)
        {
            Vector2 c = m.RoomLabelPoint(mapFloor, rooms[i].rect);
            mapRooms[i].box.pivot = new Vector2(0.5f, 0.5f);
            mapRooms[i].box.anchoredPosition = MapPos(c);
            mapRooms[i].box.sizeDelta = new Vector2(PixelFontData.Measure(rooms[i].label) * 2f + 24f, 40f);
        }
    }

    private void RefreshMap()
    {
        var m = StrangerMuseum.Instance;
        if (m == null) return;
        FitMap(m);
        mapTitle.text = FloorName(mapFloor);
        for (int i = 0; i < 3; i++) floorBtns[i].Selected = StrangerMuseum.MapFloors[i] == mapFloor;
        for (int i = 0; i < 4; i++)
        {
            bool on = m.CharacterOnMap(i, out string f, out Vector2 px) && f == mapFloor;
            mapDots[i].gameObject.SetActive(on);
            if (!on) continue;
            mapDots[i].anchoredPosition = MapPos(px) + new Vector2((i - 1.5f) * 42f, 0f);   // 같은 곳에 서도 겹치지 않게 조금씩 벌린다
            string n = MuseumGame.NameOf(i);
            mapNames[i].text = n.Length > 0 ? n.Substring(0, 1) : "?";
        }
        var cur = MuseumGame.Current;
        bool star = cur != null && !cur.Done && cur.GuideFloor == mapFloor;
        foreach (var r in mapRooms)
        {
            bool goal = star && r.id == cur.GuideRoom, here = m.CurFloor == mapFloor && m.CurRoom == r.id;
            r.fill.color = new Color(0f, 0f, 0f, goal || here ? 0.75f : 0.55f);   // 이름 밑에만 어두운 띠
            r.name.color = goal ? MuseumUI.Amber : here ? MuseumUI.Mint : MuseumUI.Ink;
        }
        mapStar.gameObject.SetActive(star);
        if (star) { mapStar.rectTransform.anchoredPosition = MapPos(cur.GuidePoint); mapStar.color = (int)(Time.unscaledTime * 3f) % 2 == 0 ? MuseumUI.Amber : Color.white; }
        mapGoal.text = cur != null && !cur.Done ? "★ 다음 목표\n" + MuseumGame.Missions[Mathf.Clamp(MuseumGame.Stage, 0, MuseumGame.Missions.Length - 1)].place : "";
    }

    private void BuildPause()
    {
        pause = ModalPanel("Pause", 640f, 560f);
        var p = (RectTransform)pause.Find("Panel");
        var t = Label(p, "Title", 3, MuseumUI.Amber, TextAnchor.MiddleCenter, 0.5f, 1f, 0f, -36f, 560f, 52f); t.text = "일시 정지";
        resumeBtn = MuseumUI.Button(p, "Resume", "계속하기", 2, CloseModalsInternal);
        MuseumUI.Anchor(resumeBtn.Rect, 0.5f, 1f, 0f, -110f, 440f, 64f);
        sfxText = Label(p, "Sfx", 2, MuseumUI.Ink, TextAnchor.MiddleCenter, 0.5f, 1f, 0f, -200f, 280f, 56f);
        sfxDown = MuseumUI.Button(p, "SfxDown", "◀", 2, () => Vol(true, -0.1f)); MuseumUI.Anchor(sfxDown.Rect, 0.5f, 1f, -200f, -196f, 72f, 60f);
        sfxUp = MuseumUI.Button(p, "SfxUp", "▶", 2, () => Vol(true, 0.1f)); MuseumUI.Anchor(sfxUp.Rect, 0.5f, 1f, 200f, -196f, 72f, 60f);
        bgmText = Label(p, "Bgm", 2, MuseumUI.Ink, TextAnchor.MiddleCenter, 0.5f, 1f, 0f, -276f, 280f, 56f);
        bgmDown = MuseumUI.Button(p, "BgmDown", "◀", 2, () => Vol(false, -0.1f)); MuseumUI.Anchor(bgmDown.Rect, 0.5f, 1f, -200f, -272f, 72f, 60f);
        bgmUp = MuseumUI.Button(p, "BgmUp", "▶", 2, () => Vol(false, 0.1f)); MuseumUI.Anchor(bgmUp.Rect, 0.5f, 1f, 200f, -272f, 72f, 60f);
        var help = Label(p, "Help", 2, MuseumUI.Slate, TextAnchor.MiddleCenter, 0.5f, 1f, 0f, -350f, 560f, 40f); help.text = "H 도움말 · N 수첩·복습 · C 채팅 접기";
        leaveBtn = MuseumUI.Button(p, "Leave", "타이틀로 나가기", 2, Leave);
        MuseumUI.Anchor(leaveBtn.Rect, 0.5f, 0f, 0f, 36f, 440f, 64f);
        leaveBtn.Accent = MuseumUI.Red; leaveBtn.Sound = "ui_back";
    }

    private void Vol(bool sfx, float d)
    {
        if (sfx) AudioPrefs.Sfx = Mathf.Clamp01(Mathf.Round((AudioPrefs.Sfx + d) * 10f) / 10f);
        else AudioPrefs.Bgm = Mathf.Clamp01(Mathf.Round((AudioPrefs.Bgm + d) * 10f) / 10f);
    }

    private void Leave()
    {
        CloseModalsInternal();
        MuseumStory.LeaveToTitle();
    }

    // ---------------------------------------------------------------- 밖에서 부르는 것
    public static void Toast(string text, Color? color = null)
    {
        if (I == null || string.IsNullOrEmpty(text)) return;
        foreach (var t in I.toasts) if (t.Item1 == text) return;
        if (I.toastText.text == text && Time.time < I.toastUntil) return;
        I.toasts.Enqueue((text, color ?? MuseumUI.Ink));
        while (I.toasts.Count > 3) I.toasts.Dequeue();
    }

    public static void Think(string text) { if (I != null && !string.IsNullOrEmpty(text)) I.thoughts.Enqueue(text); }

    public static void ShowPlace(float seconds = 4f) { if (I != null) I.bannerUntil = Time.time + seconds; }

    public static void ShowInspect(string title, string text, string word, int lang, string meaning, Sprite picture = null)
    {
        if (I == null) return;
        MuseumGame.CloseAllPanels();
        I.inspTitle.text = title;
        float textW = picture != null ? 680f : 900f;   // 그림이 있으면 글은 왼쪽 좁게
        I.inspText.text = MuseumUI.Wrap(text, textW, 2);
        I.inspPic.sprite = picture;
        I.inspPic.gameObject.SetActive(picture != null);
        I.inspWord.text = string.IsNullOrEmpty(word) ? "" : "낱말  " + word + " = " + meaning + "  (" + MuseumVoice.Language[lang] + ")";
        I.Show(I.inspect);
        Sfx.Play("book_open", 0.55f);
    }

    public static void ToggleBook()
    {
        if (I == null) return;
        if (I.book.gameObject.activeSelf) { CloseModals(); return; }
        CloseModals();   // 퍼즐 창은 닫지 않는다(수첩이 그 위에 뜬다 · 퍼즐 중에 낱말을 찾아볼 수 있게)
        I.RefreshBook();
        I.Show(I.book);
        Sfx.Play("book_open", 0.55f);
    }

    public static void TogglePause()
    {
        if (I == null) return;
        if (I.pause.gameObject.activeSelf) { CloseModals(); return; }
        MuseumGame.CloseAllPanels();
        I.Show(I.pause);
        Sfx.Play("ui_select", 0.6f);
    }

    public static void CloseModals() { if (I != null) I.CloseModalsInternal(); }

    private void CloseModalsInternal()
    {
        bool any = inspect.gameObject.activeSelf || book.gameObject.activeSelf || pause.gameObject.activeSelf || map.gameObject.activeSelf;
        inspect.gameObject.SetActive(false); book.gameObject.SetActive(false); pause.gameObject.SetActive(false); map.gameObject.SetActive(false);
        if (any) { Sfx.Play("book_close", 0.45f); MuseumGame.LastCloseFrame = Time.frameCount; }
    }

    private void Show(RectTransform m)
    {
        inspect.gameObject.SetActive(m == inspect); book.gameObject.SetActive(m == book); pause.gameObject.SetActive(m == pause); map.gameObject.SetActive(m == map);
        modalFrame = Time.frameCount;
    }

    private void RefreshBook()
    {
        int total = MuseumWords.Learned.Count;
        bookTitle.text = quizMode ? "복습" : "배운 말 수첩   " + total + "개";
        quizRoot.gameObject.SetActive(quizMode);
        foreach (var c in bookCols) c.transform.parent.gameObject.SetActive(!quizMode);
        quizBtn.SetText(quizMode ? "수첩" : "복습");
        if (quizMode) { quizScore.text = quizTotal > 0 ? "맞힌 수  " + quizRight + " / " + quizTotal : ""; return; }
        for (int l = 1; l <= 3; l++)
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var e in MuseumWords.Learned)
            {
                if (e.lang != l) continue;
                if (n >= 9) { sb.Append("…"); break; }
                if (n > 0) sb.Append('\n');
                sb.Append(e.word).Append(" = ").Append(e.meaning);
                n++;
            }
            bookCols[l - 1].text = n == 0 ? "없음" : sb.ToString();
            bookCols[l - 1].color = n == 0 ? MuseumUI.Slate : MuseumUI.Ink;
        }
    }

    // ---------------------------------------------------------------- 매 프레임
    private void LateUpdate()
    {
        UpdateRotate();
        MuseumUI.Tick();
        sf = hud.scaleFactor > 0.01f ? hud.scaleFactor : 1f;
        var m = StrangerMuseum.Instance;
        bool story = MuseumStory.Busy;
        bool panel = MuseumGame.AnyPanelOpen;
        var kb = Keyboard.current;

        // 창 키 처리
        if (kb != null && !MuseumChat.TypingNow && !story && Time.frameCount != modalFrame)
        {
            bool closeKey = kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame;   // Esc · X 는 어느 창이든 닫는다
            if (inspect.gameObject.activeSelf && (closeKey || kb.eKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) CloseModalsInternal();
            else if (kb.nKey.wasPressedThisFrame && !pause.gameObject.activeSelf && !inspect.gameObject.activeSelf) ToggleBook();
            else if (kb.mKey.wasPressedThisFrame && !pause.gameObject.activeSelf && !inspect.gameObject.activeSelf) ToggleMap();
            else if ((map.gameObject.activeSelf || book.gameObject.activeSelf) && (closeKey || kb.zKey.wasPressedThisFrame)) CloseModalsInternal();
            else if (pause.gameObject.activeSelf && closeKey) CloseModalsInternal();
            else if (kb.hKey.wasPressedThisFrame) ShowPlace(6f);
            else if (kb.qKey.wasPressedThisFrame && !ModalOpen) TrySendPing(m, false);
        }
        if (TouchControls.PingPressed && !story && !ModalOpen) TrySendPing(m, false);   // 폰: 터치 '핑' 단추
        // 휠 클릭: 마우스가 가리키는 곳에 핑
        if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame && !story && !ModalOpen && !MuseumUI.PointerOverButton && !MuseumUI.InputBlocked) TrySendPing(m, true);
        if (book.gameObject.activeSelf && quizMode && quizNextAt > 0f && Time.unscaledTime >= quizNextAt) NextQuiz();
        // 창 밖을 누르면 닫는다(단추 위가 아닐 때)
        var msx = Mouse.current;
        if (ModalOpen && msx != null && msx.leftButton.wasPressedThisFrame && Time.frameCount != modalFrame && !MuseumUI.PointerOverButton)
        {
            RectTransform open = inspect.gameObject.activeSelf ? inspect : book.gameObject.activeSelf ? book : map.gameObject.activeSelf ? map : pause;
            var pnl = (RectTransform)open.Find("Panel");
            if (pnl != null && !RectTransformUtility.RectangleContainsScreenPoint(pnl, msx.position.ReadValue(), null)) CloseModalsInternal();
        }
        // 수첩·설명·일시 정지 창이 떠 있으면 채팅창은 잠시 접어 둔다(창 위로 겹치지 않게)
        bool modalNow = ModalOpen && !story;
        if (modalNow != chatHiddenByModal) { chatHiddenByModal = modalNow; if (!story) MuseumChat.Hidden = modalNow; }
        if (map.gameObject.activeSelf) RefreshMap();
        // 좁은 화면(세로 화면 등)에서는 창을 화면 안에 들어오게 줄인다
        float cw0 = Screen.width / sf, ch0 = Screen.height / sf;
        foreach (var d in modals)
        {
            if (!d.gameObject.activeSelf) continue;
            var pnl = (RectTransform)d.Find("Panel");
            if (pnl == null) continue;
            float s = Mathf.Min(1f, (cw0 - 32f) / pnl.sizeDelta.x, (ch0 - 32f) / pnl.sizeDelta.y);
            pnl.localScale = new Vector3(s, s, 1f);
        }
        if (pause.gameObject.activeSelf)
        {
            sfxText.text = "효과음  " + Mathf.RoundToInt(AudioPrefs.Sfx * 100f) + "%";
            bgmText.text = "배경음  " + Mathf.RoundToInt(AudioPrefs.Bgm * 100f) + "%";
        }

        // 장소
        if (m != null)
        {
            string place = m.PlaceLabel;
            if (place != lastPlace)
            {
                // 방을 옮길 때는 큰 안내판 대신 장소 이름만 작게 잠깐(조작 키·능력은 H 키 / 이야기 뒤에만)
                lastPlace = place; placeText.text = place;
                placeChipText.text = place;
                placeChip.sizeDelta = new Vector2(PixelFontData.Measure(place) * 2f + 48f, 52f);
                if (!story && Time.time >= bannerUntil) placeChipUntil = Time.time + 1.6f;
            }
        }
        perkText.text = "내 능력 · " + MuseumGame.Perk[Mathf.Clamp(Story1Role.Current, 0, 3)];
        bool showBanner = !story && !panel && Time.time < bannerUntil;
        bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, showBanner ? 1f : 0f, Time.deltaTime * 4f);
        bool showChip = !story && !showBanner && Time.time < placeChipUntil;
        placeChipGroup.alpha = Mathf.MoveTowards(placeChipGroup.alpha, showChip ? 1f : 0f, Time.deltaTime * 5f);

        // 미션 카드
        bool showCard = !story && !panel && MuseumGame.Stage < MuseumGame.Done && m != null;
        if (showCard) UpdateCard();
        cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, showCard ? 1f : 0f, Time.deltaTime * 5f);
        cardFold.SetActive(cardGroup.alpha > 0.5f);

        // 속마음
        if (thoughtUntil < 0f && thoughts.Count > 0 && !story)
        {
            thoughtName.text = MuseumGame.NameOf(0) + "의 속마음";
            thoughtText.text = MuseumUI.Wrap(thoughts.Dequeue(), CardText, 2);
            thoughtUntil = Time.time + 5.5f;
        }
        bool showThought = thoughtUntil > 0f && Time.time < thoughtUntil && !story && !panel;
        if (thoughtUntil > 0f && Time.time >= thoughtUntil + 0.6f) thoughtUntil = -1f;
        float lines = MuseumUI.Lines(thoughtText.text);
        thought.sizeDelta = new Vector2(CardW, 68f + lines * 40f);
        thought.anchoredPosition = new Vector2(24f, 24f + (showCard ? card.sizeDelta.y + 12f : 0f));
        thoughtGroup.alpha = Mathf.MoveTowards(thoughtGroup.alpha, showThought ? 1f : 0f, Time.deltaTime * 4f);

        // 알림
        if (toastUntil < 0f && toasts.Count > 0)
        {
            var (text, col) = toasts.Dequeue();
            // 화면보다 길면 줄을 나눈다(좁은 화면에서 양옆이 잘리지 않게)
            float maxW = Mathf.Min(1500f, Screen.width / sf - 48f);
            float w = Mathf.Min(maxW, PixelFontData.Measure(text) * 2f + 80f);
            string shown = PixelFontData.Measure(text) * 2f + 80f > w ? MuseumUI.Wrap(text, w - 48f, 2) : text;
            toastText.text = shown; toastText.color = col; toastEdge.color = col;
            toastText.maxWidth = w - 24f;
            toast.sizeDelta = new Vector2(w, Mathf.Max(64f, MuseumUI.TextHeight(shown, 2) + 28f));
            toastUntil = Time.time + 3.2f;
        }
        bool showToast = toastUntil > 0f && Time.time < toastUntil && !story;
        if (toastUntil > 0f && Time.time >= toastUntil + 0.4f) toastUntil = -1f;
        toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, showToast ? 1f : 0f, Time.deltaTime * 6f);
        {
            float cwT = Screen.width / sf, left = (cwT - toast.sizeDelta.x) * 0.5f, y = -28f;
            if (bannerGroup.alpha > 0.05f && left < 24f + banner.sizeDelta.x) y = -24f - banner.sizeDelta.y - 12f;
            else if (placeChipGroup.alpha > 0.05f && left < 24f + placeChip.sizeDelta.x) y = -24f - placeChip.sizeDelta.y - 12f;
            toast.anchoredPosition = new Vector2(0f, y);
        }

        // 머리 위 · 이름표 · 목표 표시
        UpdatePrompt(m, story || panel || ModalOpen);
        UpdateTags(m, story);
        UpdateBubbles(story || panel);   // 퍼즐 창이 열려 있으면 말풍선은 숨긴다(창 위에 겹쳤다 · 그때는 채팅이 펼쳐져 있다)
        UpdateGuide(m, story || panel || ModalOpen);
        UpdatePings(m, story || ModalOpen);
        UpdateTips(story, panel || ModalOpen);
        UpdateClock(story);
    }

    // ---------------------------------------------------------------- 시계(오른쪽 위) · 실수 벌점 · 스피드런 구간 기록
    private RectTransform clock; private PixelText clockText, penText, splitText; private float penUntil; private CanvasGroup clockGroup;

    private void BuildClock()
    {
        clock = MuseumUI.Node("Clock", hud.transform);
        MuseumUI.Anchor(clock, 1f, 1f, -24f, -24f, 250f, 64f);
        MuseumUI.Panel(clock);
        clockGroup = clock.gameObject.AddComponent<CanvasGroup>(); clockGroup.alpha = 0f; clockGroup.blocksRaycasts = false;
        clockText = MuseumUI.Text(clock, "Time", "0:00", 3, MuseumUI.Ink, TextAnchor.MiddleCenter);
        MuseumUI.Anchor(clockText.rectTransform, 0.5f, 1f, 0f, -6f, 230f, 52f);
        penText = MuseumUI.Text(hud.transform, "Penalty", "", 3, MuseumUI.Red, TextAnchor.MiddleRight);
        MuseumUI.Anchor(penText.rectTransform, 1f, 1f, -230f, -30f, 200f, 52f);
        splitText = MuseumUI.Text(clock, "Splits", "", 2, MuseumUI.Slate, TextAnchor.UpperLeft);
        MuseumUI.Anchor(splitText.rectTransform, 0f, 1f, 18f, -70f, 400f, 300f);
        splitText.maxWidth = 0f;
    }

    /// <summary>실수 벌점을 시계 옆에 잠깐 빨갛게(+5초 · +15초).</summary>
    public static void PenaltyFlash(float sec)
    {
        if (I == null) return;
        I.penText.text = "+" + Mathf.RoundToInt(sec) + "초";
        I.penUntil = Time.time + 2f;
        Sfx.Play("ui_back", 0.6f);
    }

    private static string Clock(float t, bool tenths)
    {
        t = Mathf.Max(0f, t);
        int m = (int)(t / 60f); float s = t - m * 60f;
        return tenths ? m + ":" + s.ToString("00.0") : m + ":" + ((int)s).ToString("00");
    }

    private void UpdateClock(bool story)
    {
        bool speed = Story1Net.Speedrun;
        // 시계는 스피드런 · 보통 · 어려움(벌점이 붙는 판)에서 늘 · 쉬움 스토리 모드에서는 이야기 밖에서만 작게
        bool on = !story && MuseumGame.StartTime > 0f;
        clockGroup.alpha = Mathf.MoveTowards(clockGroup.alpha, on ? 1f : 0f, Time.deltaTime * 5f);
        clockText.text = Clock(MuseumGame.Elapsed, speed);
        clockText.color = MuseumGame.ClearTime > 0f ? MuseumUI.Mint : MuseumUI.Ink;
        bool pen = Time.time < penUntil && on;
        if (penText.gameObject.activeSelf != pen) penText.gameObject.SetActive(pen);
        float h = 64f;
        if (speed)
        {
            var sb = new System.Text.StringBuilder();
            int lines = 0;
            for (int i = 0; i < MuseumGame.Missions.Length; i++)
            {
                if (i > MuseumGame.Stage) break;
                bool done = i < MuseumGame.Stage;
                if (lines > 0) sb.Append('\n');
                sb.Append(i + 1).Append(' ').Append(MuseumGame.Missions[i].title);
                sb.Append("  ").Append(done ? Clock(MuseumGame.Splits[i], true) : "…");
                lines++;
            }
            if (MuseumGame.Penalty > 0f) { sb.Append('\n').Append("실수 ").Append(MuseumGame.Mistakes).Append("  +").Append(Mathf.RoundToInt(MuseumGame.Penalty)).Append("초"); lines++; }
            splitText.text = sb.ToString();
            h = 74f + lines * 38f;
            // 글이 길면 패널을 넓힌다(구간 기록 글자가 패널 밖으로 삐져나오지 않게)
            float w = 0f;
            foreach (var ln in splitText.text.Split('\n')) w = Mathf.Max(w, PixelFontData.Measure(ln) * 2f);
            clock.sizeDelta = new Vector2(Mathf.Max(220f, w + 40f), h);
            splitText.rectTransform.sizeDelta = new Vector2(clock.sizeDelta.x - 36f, h);
        }
        else { splitText.text = ""; clock.sizeDelta = new Vector2(Mathf.Max(190f, PixelFontData.Measure(clockText.text) * 3f + 40f), h); }
        clockText.rectTransform.sizeDelta = new Vector2(clock.sizeDelta.x - 20f, 52f);
        if (splitText.gameObject.activeSelf != speed) splitText.gameObject.SetActive(speed);
        penText.rectTransform.anchoredPosition = new Vector2(-24f - clock.sizeDelta.x - 12f, -30f);
    }

    private readonly List<Rect> bubbleRects = new List<Rect>();

    private void UpdateBubbles(bool hidden)
    {
        bubbleRects.Clear();
        for (int i = 0; i < 4; i++)
        {
            bool on = !hidden && Time.time < bubbleUntil[i];
            Vector2 p = default;
            if (on)
            {
                if (i == Story1Role.Current)
                {
                    on = myHeadOk;
                    p = Canvas(myHead) + new Vector2(0f, prompt.gameObject.activeSelf ? 84f : 22f);
                }
                else
                {
                    on = tags[i].gameObject.activeSelf;   // 이름표가 보일 때만(화면 안 · 밝은 곳)
                    p = tags[i].anchoredPosition + new Vector2(0f, 22f);
                }
            }
            if (on)
            {
                float w = bubbles[i].sizeDelta.x, cw = Screen.width / sf, ch = Screen.height / sf;
                p.x = Mathf.Clamp(p.x, w * 0.5f + 8f, cw - w * 0.5f - 8f);
                p.y = Mathf.Min(p.y, ch - 52f);
                // 먼저 뜬 말풍선과 겹치면 위로 쌓는다(둘이 붙어 서서 동시에 말할 때)
                float bh = bubbles[i].sizeDelta.y;
                for (int guard = 0; guard < 4; guard++)
                {
                    var me = new Rect(p.x - w * 0.5f, p.y, w, bh);
                    bool hit = false;
                    foreach (var o in bubbleRects) if (o.Overlaps(me)) { p.y = o.yMax + 4f; hit = true; break; }
                    if (!hit) break;
                }
                bubbleRects.Add(new Rect(p.x - w * 0.5f, p.y, w, bh));
                bubbles[i].anchoredPosition = p;
                if (MuseumChat.CoversScreen(new Vector3(p.x * sf, p.y * sf, 0f))) on = false;
            }
            if (bubbles[i].gameObject.activeSelf != on) bubbles[i].gameObject.SetActive(on);
        }
    }

    private void UpdateCard()
    {
        int st = MuseumGame.Stage;
        string prog = MuseumGame.ProgressLine;
        if (st == cardStage && prog == cardProgText) return;
        cardStage = st; cardProgText = prog;
        var mi = MuseumGame.Missions[Mathf.Clamp(st, 0, MuseumGame.Missions.Length - 1)];
        cardTitle.text = "미션 " + (st + 1) + "/" + MuseumGame.Missions.Length + "  ·  " + mi.title;
        cardProg.text = prog ?? "";
        cardFold.SetText(cardMini ? "▲" : "▼");
        cardPlace.gameObject.SetActive(!cardMini);
        cardTodo.gameObject.SetActive(!cardMini);
        if (cardMini)
        {
            // 접힘: 제목 + 진행 한 줄
            card.sizeDelta = new Vector2(CardW, 60f + (string.IsNullOrEmpty(prog) ? 0f : 44f));
            return;
        }
        cardPlace.text = mi.place;
        string todo = MuseumUI.Wrap(mi.todo, CardText, 2);
        cardTodo.text = todo;
        float todoH = MuseumUI.TextHeight(todo, 2);
        cardTodo.rectTransform.sizeDelta = new Vector2(CardText, todoH);
        float h = 100f + todoH + (string.IsNullOrEmpty(prog) ? 20f : 60f);
        card.sizeDelta = new Vector2(CardW, h);
    }

    private Vector2 Canvas(Vector3 screen) => new Vector2(screen.x / sf, screen.y / sf);

    private void UpdateTags(StrangerMuseum m, bool hidden)
    {
        for (int i = 0; i < 4; i++)
        {
            bool on = !hidden && m != null && m.TagScreen(i, out Vector3 sp) && MuseumStory.IsLitScreen(sp);
            if (on)
            {
                m.TagScreen(i, out sp);
                string n = MuseumGame.NameOf(i);
                if (Story1Net.HasHumanPeers && Story1Role.IsBot(i)) n += " (AI)";
                tagText[i].text = n;
                tagText[i].color = i == Story1Role.Current ? MuseumUI.Amber : Roles.Color(i);
                tagShadow[i].text = n;
                tags[i].anchoredPosition = Canvas(sp) + new Vector2(0f, 22f);
                tags[i].sizeDelta = new Vector2(PixelFontData.Measure(n) * 2f + 16f, 36f);
                if (MuseumChat.CoversScreen(sp)) on = false;
            }
            if (tags[i].gameObject.activeSelf != on) tags[i].gameObject.SetActive(on);
        }
    }

    private void UpdatePrompt(StrangerMuseum m, bool hidden)
    {
        string action = null;
        Vector3 head = default;
        myHeadOk = false;
        if (m != null)
        {
            string a = m.PromptAction(out head);
            myHead = head; myHeadOk = head.z >= 0f;
            if (!hidden && !MuseumChat.Typing) action = a;
        }
        bool on = action != null;
        if (prompt.gameObject.activeSelf != on) prompt.gameObject.SetActive(on);
        if (!on) return;
        promptText.text = action;
        float w = PixelFontData.Measure(action) * 2f + 92f;
        prompt.sizeDelta = new Vector2(w, 60f);
        promptText.rectTransform.sizeDelta = new Vector2(w - 70f, 44f);
        Vector2 p = Canvas(head) + new Vector2(0f, 14f);
        float cw = Screen.width / sf, ch = Screen.height / sf;
        p.x = Mathf.Clamp(p.x, w * 0.5f + 12f, cw - w * 0.5f - 12f);
        p.y = Mathf.Clamp(p.y, 12f, ch - 72f);
        prompt.anchoredPosition = p;
        var ms = Mouse.current;
        bool hover = ms != null && RectTransformUtility.RectangleContainsScreenPoint(prompt, ms.position.ReadValue(), null);
        promptBack.color = hover ? new Color(0.2f, 0.16f, 0.1f, 0.97f) : MuseumUI.Fill;
        // 창을 닫은 바로 그 클릭이 E 안내까지 눌러 창이 다시 열리지 않게(명판 '닫기' 버그)
        bool justClosed = Time.frameCount <= MuseumGame.LastCloseFrame + 2 || Time.frameCount <= modalFrame + 1;
        if (hover && ms.leftButton.wasPressedThisFrame && !justClosed && !MuseumUI.PointerOverButton) PromptClicked = true;
    }

    private void UpdateGuide(StrangerMuseum m, bool hidden)
    {
        string label = null; Vector3 sp = default;
        // 목표 화살표는 내비게이터만 · 내비게이터가 AI면 린이 손짓한 길로 보인다(이름이 붙는다). 지도(M)는 늘 린만
        // 쉬움: 처음 하는 사람이 길을 잃지 않게 모두에게 보인다(보통·어려움은 내비게이터의 능력)
        bool nav = MuseumGame.IHave(MuseumGame.Navigator), follow = !nav && (MuseumGame.SharedByAI(MuseumGame.Navigator) || Story1Net.Difficulty == Story1Rules.Easy);
        if (!hidden && m != null && (nav || follow)) label = m.GuideTarget(out sp, follow);
        bool on = label != null;
        if (guideArrow.gameObject.activeSelf != on) guideArrow.gameObject.SetActive(on);
        if (!on) { if (guide.gameObject.activeSelf) guide.gameObject.SetActive(false); return; }
        float cw = Screen.width / sf, ch = Screen.height / sf;
        Vector2 p = Canvas(sp);
        const float edge = 40f;
        bool off = sp.z < 0f || p.x < edge || p.x > cw - edge || p.y < edge || p.y > ch - edge;
        float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 8f;
        string arrow = "▼";
        Vector2 dir = Vector2.down;   // 화살표가 가리키는 쪽
        if (off)
        {
            Vector2 c = new Vector2(cw * 0.5f, ch * 0.5f), d = p - c;
            if (sp.z < 0f) d = -d;
            if (Mathf.Abs(d.x) / cw > Mathf.Abs(d.y) / ch) { arrow = d.x < 0 ? "◀" : "▶"; dir = d.x < 0 ? Vector2.left : Vector2.right; }
            else { arrow = d.y < 0 ? "▼" : "▲"; dir = d.y < 0 ? Vector2.down : Vector2.up; }
        }
        // 화살표: 목표 바로 위에 딱 붙인다(화면 밖이면 그쪽 가장자리)
        Vector2 ap = off
            ? new Vector2(Mathf.Clamp(p.x, edge, cw - edge), Mathf.Clamp(p.y, edge, ch - edge)) - dir * bob
            : p + new Vector2(0f, 30f + bob);
        guideArrowText.text = arrow; guideArrowShadow.text = arrow;
        guideArrow.anchoredPosition = ap;

        // 이름표: 화살표 옆에 작게
        guideText.text = label;
        float w = PixelFontData.Measure(label) * 2f + 28f, h = 44f;
        guide.sizeDelta = new Vector2(w, h);
        Vector2 lp;
        if (!off || dir == Vector2.down) lp = ap + new Vector2(0f, 48f);
        else if (dir == Vector2.up) lp = ap - new Vector2(0f, 48f);
        else if (dir == Vector2.left) lp = ap + new Vector2(w * 0.5f + 34f, 0f);
        else lp = ap - new Vector2(w * 0.5f + 34f, 0f);
        lp.x = Mathf.Clamp(lp.x, w * 0.5f + 12f, cw - w * 0.5f - 12f);
        lp.y = Mathf.Clamp(lp.y, h * 0.5f + 8f, ch - h * 0.5f - 8f);
        var me = new Rect(lp.x - w * 0.5f, lp.y - h * 0.5f, w, h);
        bool showLabel = true;
        // 채팅창·미션 카드·장소 안내 밑에 숨으면 이름표는 접는다(화살표만)
        if (MuseumChat.ChatRect(out Rect cr))
        {
            var chat = new Rect(cr.xMin / sf, (Screen.height - cr.yMax) / sf, cr.width / sf, cr.height / sf);
            if (me.Overlaps(chat)) showLabel = false;
            if (chat.Contains(ap)) guideArrow.anchoredPosition = ap = new Vector2(ap.x, chat.yMax + 30f);
        }
        if (cardGroup.alpha > 0.5f && me.Overlaps(new Rect(24f, 24f, card.sizeDelta.x, card.sizeDelta.y))) showLabel = false;
        // 오른쪽 위 시계와 겹치면 이름표를 시계 아래로 내리고, 화살표도 시계 위에 올라가지 않게
        if (clockGroup != null && clockGroup.alpha > 0.5f)
        {
            var cr2 = new Rect(cw - 24f - clock.sizeDelta.x, ch - 24f - clock.sizeDelta.y, clock.sizeDelta.x, clock.sizeDelta.y);
            if (me.Overlaps(cr2)) { lp.y = cr2.yMin - h * 0.5f - 10f; me = new Rect(lp.x - w * 0.5f, lp.y - h * 0.5f, w, h); }
            if (cr2.Contains(ap)) guideArrow.anchoredPosition = ap = new Vector2(ap.x, cr2.yMin - 30f);
        }
        if (bannerGroup.alpha > 0.05f && me.Overlaps(new Rect(24f, ch - 24f - banner.sizeDelta.y, banner.sizeDelta.x, banner.sizeDelta.y))) showLabel = false;
        // E 안내와 겹치면 이름표를 숨긴다(E 안내가 우선)
        if (prompt.gameObject.activeSelf)
        {
            Vector2 pp = prompt.anchoredPosition; Vector2 ps = prompt.sizeDelta;
            var pr = new Rect(pp.x - ps.x * 0.5f, pp.y, ps.x, ps.y);
            if (me.Overlaps(pr)) showLabel = false;
            if (pr.Contains(ap)) guideArrow.gameObject.SetActive(false);
        }
        if (guide.gameObject.activeSelf != showLabel) guide.gameObject.SetActive(showLabel);
        guide.anchoredPosition = lp;
    }
}
