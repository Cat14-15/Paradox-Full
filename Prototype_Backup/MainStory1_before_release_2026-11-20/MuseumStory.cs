using System.Collections;
using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using Paradox.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainStory1 의 이야기와 어둠.
///  · 대화창: 프롤로그·캠프와 똑같은 모양(초상 · 이름 · 타자 글씨 · ▼ · X 건너뛰기).
///  · 시작: 캠프 장면이 끝난 어둠(손전등) 그대로 이어서 짧은 이야기 → 미션 1.
///  · 미션을 끝낼 때마다 짧은 이야기(PlayBeat). 마지막은 탈출 → 캠프의 밤 → 한국 교실(지호) → 메시지 → 크레딧 → 타이틀.
///  · 이야기가 나오는 동안(Busy) 이동·HUD·이름표·채팅은 멈추거나 숨는다.
///  · 어둠: 손전등 불빛만 보이다가, 전기실 차단기를 올리면(RestorePower) 천천히 밝아진다.
/// </summary>
public sealed class MuseumStory : MonoBehaviour
{
    public static bool Busy { get; private set; }
    public static bool PowerOn { get; private set; }
    public static Vector3 PlayerWorld;

    private static int mode;          // 0 밝음 · 1 완전 암흑 · 2 손전등
    private static float powerAt;
    private static Vector2 litCenter; // 화면 좌표(왼쪽 아래 기준)
    private static float litRadius;
    private static MuseumStory I;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { PowerOn = false; Busy = false; mode = 0; I = null; powerAt = 0f; litRadius = 0f; }

    public static void RestorePower() { if (PowerOn) return; PowerOn = true; powerAt = Time.time; Sfx.Play("power_on", 0.8f); }

    /// <summary>화면 좌표(왼쪽 아래 기준)가 지금 보이는가(어두울 땐 손전등 안만).</summary>
    public static bool IsLitScreen(Vector3 screen)
    {
        if (mode == 0) return true;
        if (PowerOn && Time.time - powerAt >= 2.5f) return true;
        if (mode == 1) return false;
        return ((Vector2)screen - litCenter).magnitude < litRadius * 0.85f;
    }

    // ---------------------------------------------------------------- 대사 자료
    private sealed class Line
    {
        public string who, text, note, sfx; public int kind, face, dark, bg; public float vol;
    }

    // kind: 0 대사 · 1 내레이션 · 2 속마음 · 3 미션 안내. face: 0 서하린 1 이안 2 린 3 카이 4 스태프 5 지호 -1 없음
    // dark: 0 그대로 · 1 깜빡이고 암흑 · 2 손전등. bg: 0 그대로 · 1 캠프의 밤 · 2 한국 교실 · 3 배경 걷기(박물관)
    private static Line L(string who, string text, int kind, int face, string note = null) => new Line { who = who, text = text, kind = kind, face = face, note = note };
    private static Line N(string text) => new Line { who = "", text = text, kind = 1, face = -1 };
    private static Line T(string text) => new Line { who = "서하린", text = text, kind = 2, face = 0 };
    private static Line M(string text) => new Line { who = "", text = text, kind = 3, face = -1, sfx = "ui_select", vol = 0.8f };
    private static Line S(Line l, string sfx, float vol) { l.sfx = sfx; l.vol = vol; return l; }
    private static Line B(Line l, int bg) { l.bg = bg; return l; }

    private static readonly Line[] Opening =
    {
        N("손전등 불빛 하나에 의지해 네 사람은 중앙 복도에 모였다. 발밑으로 차가운 물이 조금씩 스며든다."),
        L("린", "Nuoc… Nuoc! Duoi kia!", 0, 2, "바닥의 물을 가리키고, 지하 계단 쪽을 손으로 짚으며"),
        T("물이 아래에서 올라오고 있어. 린이 지하를 가리키는 것 같은데…"),
        L("이안", "Ang tubig, galing sa ibaba. Tara na!", 0, 1, "계단을 가리키며 앞장서서"),
        L("카이", "Yebo! Sihambe!", 0, 3, "엄지를 들어 보이며"),
        T("다들 뭐라는지 하나도 모르겠어. …그래도 '가자'는 건 알겠다."),
        M("[ 미션 1 ]  물길 막기 — 지하 관리 통로의 급수 밸브를 잠가 물이 차오르는 걸 막는다."),
        N("친구들은 한국어를 몰라요. 채팅창 위 신호 카드(1 2 3 4 · ← →)를 누르면 바로 보내져요."),
    };

    private static readonly Line[][] Beats =
    {
        new[]
        {
            S(N("끼익— 마지막 밸브가 잠기자, 파이프를 울리던 물소리가 거짓말처럼 멎는다."), "clear", 0.6f),
            L("카이", "Kuhle! Kuhle kakhulu!", 0, 3, "두 팔을 번쩍 들며"),
            L("이안", "Galing! Salamat, Seoharin!", 0, 1, "서하린의 어깨를 툭 치며"),
            T("내 밸브의 숫자는 나만 몰랐어. 친구가 알려 줘서 맞춘 거야."),
            T("내가 다 이끌어야 한다고 생각했는데… 혼자서는 밸브 하나도 못 돌렸네."),
            M("[ 미션 2 ]  불 켜기 — 지하 전기실의 차단기를 올려 박물관의 불을 되살린다."),
        },
        new[]
        {
            S(N("철컥, 철컥, 철컥— 차단기가 하나씩 올라가고, 천장의 불빛이 차례로 깨어난다."), "power_on", 0.8f),
            N("환해진 지하. 그제야 네 사람은 서로의 얼굴을 제대로 본다. 다들 땀과 먼지투성이다."),
            L("린", "Cam on… Cam on!", 0, 2, "웃으며 두 손을 모으고"),
            L("서하린", "어… 그건 고맙다는 말이지? 나도! 고마워!", 0, 0, "따라 웃으며"),
            T("말은 몰라도, 웃는 얼굴은 다 알아보겠다."),
            L("이안", "Nakasara ang hagdan sa itaas… Kailangan ang kumpas!", 0, 1, "벽의 안내문을 읽고 위층을 가리키며"),
            T("또 뭔가 적혀 있나 봐. 나만 못 읽는 글씨… 이번엔 친구들을 믿어 보자."),
            M("[ 미션 3 ]  나침반 잠금 — 1층 네 전시실의 명판에서 기호를 모아, 복도 나침반으로 2층 셔터를 연다."),
        },
        new[]
        {
            S(N("나침반이 맞물려 돌자, 덜컹 소리와 함께 2층 계단 셔터가 말려 올라간다."), "door_open", 0.8f),
            N("명판을 함께 읽을 때, 린은 서하린에게 자꾸 같은 말을 해 보였다. 서하린은 그 말을 조심스럽게 따라 해 본다."),
            L("서하린", "씬… 짜오? 이거 맞아?", 0, 0, "린에게, 조심스럽게"),
            L("린", "Dung roi! Xin chao!", 0, 2, "눈이 동그래져서, 활짝 웃으며"),
            T("씬 짜오… 어디서 들었는데. 아, 지호가 알려 줬던 말이다."),
            T("그때 우리는 '또 해 봐, 또!' 하면서 웃었지. 지호도 웃고 있었는데… 정말 웃고 싶었을까."),
            M("[ 미션 4 ]  옛 글자 풀기 — 2층 기록 아카이브의 잠긴 보관장을 연다."),
        },
        new[]
        {
            S(N("세 글자가 맞춰지자 보관장이 열린다. 안에는 대나무 조각 하나와 비상문 열쇠가 들어 있다."), "door_open", 0.7f),
            L("이안", "KA-SA-MA. Kasama!", 0, 1, "자기 가슴과 서하린, 린, 카이를 차례로 가리키며"),
            L("서하린", "카사마? 우리… 넷을 말하는 거야?", 0, 0),
            L("카이", "Yebo! Umngane!", 0, 3, "네 사람의 손을 한데 모으며"),
            T("함께하는 친구. 처음 만났을 때 나는 얘네를 '챙겨 줘야 할 애들'이라고만 생각했는데."),
            M("[ 미션 5 ]  함께 서기 — 2층 반원 홀, 동상 둘레의 발판 네 개에 모두 함께 선다."),
        },
        new[]
        {
            S(N("네 사람이 발판에 올라서자, 동상 아래에서 묵직한 톱니 소리가 울린다. 동상 뒤의 비상문이 천천히 열린다."), "museum_thud", 0.7f),
            N("문 너머로 바람이 잦아든 하늘이 보인다. 멀리서 캠프 스태프의 목소리가 들린다."),
            L("스태프", "여기 있었구나! 다들 괜찮아요? 정말 다행이다…!", 0, 4),
            B(S(N("그날 밤, BAYANI CAMP. 태풍이 지나간 하늘에 별이 가득하다. 네 사람은 모닥불 앞에 나란히 앉아 있다."), "ambience_camp", 0.25f), 1),
            L("이안", "Seoharin. Kasama.", 0, 1, "자기 팔찌를 풀어 서하린의 손목에 채워 주며"),
            T("오늘 하루, 나는 한마디도 제대로 알아듣지 못했다. 표지판도, 안내 방송도, 친구들의 말도."),
            T("답답하고, 무섭고… 나만 아무것도 모르는 사람이 된 기분이었어."),
            T("그런데 친구들은 나를 기다려 줬어. 손짓하고, 그림을 그리고, 같은 말을 몇 번이고 다시 해 주면서."),
            T("지호는 매일 이런 기분이었을까. 아니… 지호는 한국말을 다 아는데도, 우리가 지호를 '손님'처럼 대했던 거야."),
            L("서하린", "고마워. 정말로. …Salamat. Cam on. Ngiyabonga.", 0, 0, "또박또박, 세 나라 말로"),
            N("세 친구가 동시에 웃음을 터뜨린다. 발음은 엉망이었지만, 뜻은 정확히 전해졌다."),
            B(S(N("몇 주 뒤, 한국. 사회 시간, 조별 과제 발표 날 아침."), "school_bell", 0.6f), 2),
            L("서하린", "지호야, 발표… 같이 하자. 앞부분은 네가 해 줄래? 네가 하고 싶다던 부분.", 0, 0, "지호의 자리로 걸어가며"),
            L("지호", "…나? 진짜?", 0, 5, "놀라서"),
            L("서하린", "응. 그리고 하나 물어봐도 돼? 베트남어로 '고마워'는 어떻게 말해? 장난 아니고, 진짜 배우고 싶어서.", 0, 0),
            L("지호", "…깜언. '깜언'이야.", 0, 5, "천천히, 웃으며"),
            L("서하린", "깜언, 지호야.", 0, 0),
            N("지호가 웃는다. 이번에는, 정말로."),
        },
    };

    // ---------------------------------------------------------------- 상태
    private Camera cam;
    private Canvas darkCv, dlgCv, endCv;
    private Image blackFull; private bool flicking; private float blackA;
    private readonly RectTransform[] fills = new RectTransform[4];
    private readonly Image[] fillImgs = new Image[4];
    private RawImage spotImg; private Texture2D spot;

    private CanvasGroup dlgGroup, skipGroup, bgGroup;
    private Image portraitImg, bgImg; private RectTransform portraitRect;
    private PixelText nameText, tagText, bodyText, nextMark;
    private readonly Sprite[] faces = new Sprite[6];
    private Sprite bgCamp, bgClass;

    private Line[] script; private System.Action onDone;
    private int li = -1; private bool typing, ending; private string full = ""; private float typed, tClock, lineStart;
    private float faceAlpha; private bool wantFace;
    private int skipFrame = -1;

    private const float CharsPerSec = 34f;

    // ---------------------------------------------------------------- 시작
    private void Awake()
    {
        I = this; cam = GetComponent<Camera>();
        Busy = true; mode = 2; PowerOn = false; MuseumChat.Hidden = true;
    }

    private void OnDestroy() { if (I == this) I = null; Busy = false; MuseumChat.Hidden = false; }

    private void Start()
    {
        BuildDark();
        BuildDialogue();
        Sfx.PlayLoop("storm_wind", 0.12f);
        Play(Opening, () => { MuseumHud.ShowPlace(5f); MuseumHud.Think("우선 지하로 내려가자. 복도 오른쪽 위에 계단이 있었어."); });
    }

    /// <summary>미션을 끝낸 뒤의 이야기. 마지막 미션 뒤에는 엔딩으로 이어진다.</summary>
    public static void PlayBeat(int stage)
    {
        if (I == null || stage < 0 || stage >= Beats.Length) return;
        bool last = stage == Beats.Length - 1;
        I.Play(Beats[stage], last ? (System.Action)(() => I.StartCoroutine(I.EndCard())) : () => MuseumHud.ShowPlace(4f));
    }

    public static void LeaveToTitle() { if (I != null) I.StartCoroutine(I.Leave()); }

    private IEnumerator Leave()
    {
        Busy = true;
        float a = 0f;
        var fade = MakeFade();
        while (a < 1f) { a += Time.unscaledDeltaTime / 0.5f; fade.color = new Color(0, 0, 0, a); yield return null; }
        var hub = NetHub.Instance;
        if (hub != null && hub.IsRunning) hub.Shutdown();
        Bgm.Stop(); Sfx.Stop();
        IntroSequence.SkipToMenu = true;
        SceneManager.LoadScene("Intro");
    }

    private Image MakeFade()
    {
        var cv = MuseumUI.MakeCanvas(transform, "FadeCanvas", 130);
        var r = MuseumUI.Node("Fade", cv.transform); MuseumUI.Stretch(r);
        var img = MuseumUI.Img(r, null, new Color(0, 0, 0, 0));
        img.raycastTarget = true;
        return img;
    }

    // ---------------------------------------------------------------- 어둠
    private void BuildDark()
    {
        darkCv = MuseumUI.MakeCanvas(transform, "DarkCanvas", 90);
        darkCv.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        for (int i = 0; i < 4; i++)
        {
            fills[i] = MuseumUI.Node("Fill" + i, darkCv.transform);
            fills[i].anchorMin = fills[i].anchorMax = Vector2.zero; fills[i].pivot = Vector2.zero;
            fillImgs[i] = MuseumUI.Img(fills[i], null, Color.black);
        }
        var sp = MuseumUI.Node("Spot", darkCv.transform);
        sp.anchorMin = sp.anchorMax = Vector2.zero; sp.pivot = Vector2.zero;
        spotImg = sp.gameObject.AddComponent<RawImage>(); spotImg.texture = Spot(); spotImg.raycastTarget = false;
        var bf = MuseumUI.Node("Black", darkCv.transform); MuseumUI.Stretch(bf);
        blackFull = MuseumUI.Img(bf, null, Color.black);
        blackA = 0f;
    }

    private Texture2D Spot()
    {
        if (spot != null) return spot;
        const int n = 128;
        spot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Clamp01(new Vector2(x - (n - 1) / 2f, y - (n - 1) / 2f).magnitude / (n / 2f));
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
            }
        spot.SetPixels32(px); spot.Apply();
        return spot;
    }

    private static void Place(RectTransform r, float x, float y, float w, float h) { r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h)); }

    private void LateUpdate()
    {
        float lightUp = PowerOn ? Mathf.Clamp01((Time.time - powerAt) / 2.5f) : 0f;
        float dark = 1f - lightUp;
        darkCv.enabled = (mode >= 1 && dark > 0.001f) || flicking;
        if (!darkCv.enabled || cam == null) return;
        if (!flicking) blackA = Mathf.MoveTowards(blackA, mode >= 2 ? 0f : 1f, Time.unscaledDeltaTime * (mode >= 2 ? 3f : 6f));
        blackFull.color = new Color(0.01f, 0.012f, 0.02f, blackA * (mode >= 2 ? dark : 1f));
        Vector3 sp = cam.WorldToScreenPoint(PlayerWorld);
        float R = Mathf.Min(Screen.width, Screen.height) * 0.42f, W = Screen.width, H = Screen.height;
        float x0 = sp.x - R, x1 = sp.x + R, y0 = sp.y - R, y1 = sp.y + R;
        Place(fills[0], 0, y1, W, H - y1);
        Place(fills[1], 0, 0, W, y0);
        Place(fills[2], 0, y0, x0, 2 * R);
        Place(fills[3], x1, y0, W - x1, 2 * R);
        float fa = mode >= 2 ? 0.94f * dark : 0f;
        for (int i = 0; i < 4; i++) fillImgs[i].color = new Color(0, 0, 0, fa);
        Place((RectTransform)spotImg.transform, x0, y0, 2 * R, 2 * R);
        spotImg.color = new Color(1, 1, 1, fa);
        litCenter = new Vector2(sp.x, sp.y); litRadius = mode >= 2 ? R : 0f;
    }

    private IEnumerator Flicker()
    {
        flicking = true; mode = 1;
        float[] a = { 0.95f, 0.1f, 0.9f, 0.25f, 0.97f };
        foreach (float v in a) { blackA = v; yield return new WaitForSeconds(v > 0.5f ? 0.16f : 0.09f); }
        blackA = 1f; flicking = false;
    }

    // ---------------------------------------------------------------- 대화창(프롤로그·캠프와 같은 모양)
    private void BuildDialogue()
    {
        dlgCv = MuseumUI.MakeCanvas(transform, "DialogueCanvas", 110);
        var bgr = MuseumUI.Node("Background", dlgCv.transform); MuseumUI.Stretch(bgr);
        bgGroup = bgr.gameObject.AddComponent<CanvasGroup>(); bgGroup.alpha = 0f; bgGroup.blocksRaycasts = false;
        var bgBlack = MuseumUI.Node("Black", bgr); MuseumUI.Stretch(bgBlack); MuseumUI.Img(bgBlack, null, Color.black);
        var bgPic = MuseumUI.Node("Picture", bgr); MuseumUI.Stretch(bgPic);
        bgImg = MuseumUI.Img(bgPic, null, Color.white); bgImg.preserveAspect = false;
        bgCamp = Resources.Load<Sprite>("StoryUI/bg_camp");
        bgClass = Resources.Load<Sprite>("StoryUI/bg_class");

        for (int i = 0; i < 4; i++) faces[i] = Resources.Load<Sprite>("StoryUI/portrait_" + i);
        faces[4] = Resources.Load<Sprite>("StoryUI/portrait_staff");
        faces[5] = Resources.Load<Sprite>("StoryUI/portrait_jiho");

        var dlg = MuseumUI.Node("Dialogue", dlgCv.transform); MuseumUI.Stretch(dlg);
        dlgGroup = dlg.gameObject.AddComponent<CanvasGroup>(); dlgGroup.alpha = 0f; dlgGroup.blocksRaycasts = false; dlgGroup.interactable = false;
        portraitRect = MuseumUI.Node("Portrait", dlg); portraitRect.pivot = new Vector2(0.5f, 0f);
        MuseumUI.At(portraitRect, -680, -232, 470, 514);
        portraitImg = MuseumUI.Img(portraitRect, null, new Color(1, 1, 1, 0)); portraitImg.preserveAspect = true;

        var box = MuseumUI.Node("Box", dlg); MuseumUI.At(box, 0, -365, 1760, 330);
        MuseumUI.Sliced(box, MuseumUI.FillSprite, new Color(0.05f, 0.06f, 0.09f, 0.95f));
        var bd = MuseumUI.Node("Border", box); MuseumUI.Stretch(bd); MuseumUI.Sliced(bd, MuseumUI.BorderSprite, MuseumUI.Gold);

        nameText = MuseumUI.Text(dlg, "Name", "", 3, MuseumUI.Ink, TextAnchor.MiddleLeft); MuseumUI.At(nameText.rectTransform, -550, -268, 500, 56);
        tagText = MuseumUI.Text(dlg, "Tag", "", 2, MuseumUI.Slate, TextAnchor.MiddleLeft); MuseumUI.At(tagText.rectTransform, 0, -272, 700, 36);
        bodyText = MuseumUI.Text(dlg, "Body", "", 3, MuseumUI.Ink, TextAnchor.UpperLeft); MuseumUI.At(bodyText.rectTransform, 0, -415, 1600, 200);
        nextMark = MuseumUI.Text(dlg, "Next", "▼", 2, MuseumUI.Amber, TextAnchor.MiddleCenter); MuseumUI.At(nextMark.rectTransform, 810, -466, 40, 36);

        var pill = MuseumUI.Node("Skip", dlgCv.transform);
        MuseumUI.Anchor(pill, 1f, 1f, -32f, -32f, 260f, 56f);
        MuseumUI.Img(pill, null, new Color(0.04f, 0.05f, 0.08f, 0.72f));
        var st = MuseumUI.Text(pill, "Hint", "X  건너뛰기", 2, new Color(0.97f, 0.97f, 0.94f), TextAnchor.MiddleCenter); MuseumUI.Stretch(st.rectTransform);
        skipGroup = pill.gameObject.AddComponent<CanvasGroup>(); skipGroup.alpha = 0f; skipGroup.blocksRaycasts = false;
    }

    private void Play(Line[] lines, System.Action done)
    {
        script = lines; onDone = done; li = -1; ending = false; typing = false;
        Busy = true; MuseumChat.Hidden = true;
        MuseumGame.CloseAllPanels();
        StartCoroutine(Begin());
    }

    private IEnumerator Begin()
    {
        yield return new WaitForSeconds(0.35f);
        for (float t = 0f; t < 0.25f; t += Time.deltaTime) { dlgGroup.alpha = t / 0.25f; skipGroup.alpha = dlgGroup.alpha; yield return null; }
        dlgGroup.alpha = 1f; skipGroup.alpha = 1f;
        NextLine();
    }

    private void Update()
    {
        if (!Busy || script == null || ending) return;
        tClock += Time.deltaTime;
        var kb = Keyboard.current; var ms = Mouse.current;
        if (dlgGroup.alpha > 0.5f && kb != null && (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
        {
            Sfx.Play("ui_back"); skipFrame = Time.frameCount;
            StartCoroutine(EndScript(true)); return;
        }
        bool go = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                  || (ms != null && ms.leftButton.wasPressedThisFrame);
        if (go && li >= 0 && dlgGroup.alpha > 0.5f && Time.time - lineStart > 0.12f) { if (typing) typed = full.Length; else NextLine(); }

        if (typing)
        {
            typed += Time.deltaTime * CharsPerSec;
            int n = Mathf.Min(full.Length, (int)typed);
            bodyText.text = full.Substring(0, n);
            if (n >= full.Length) typing = false;
        }
        nextMark.gameObject.SetActive(!typing && li >= 0);
        if (nextMark.gameObject.activeSelf)
        {
            var r = nextMark.rectTransform;
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, -466f + Mathf.Round(Mathf.Sin(tClock * 5f)) * 5f);
        }
        faceAlpha = Mathf.MoveTowards(faceAlpha, wantFace ? 1f : 0f, Time.deltaTime * 6f);
        var c = portraitImg.color; c.a = faceAlpha; portraitImg.color = c;
    }

    private void NextLine()
    {
        if (ending) return;
        li++;
        if (li > 0) Sfx.Play("ui_next", 0.7f);
        if (li >= script.Length) { StartCoroutine(EndScript(false)); return; }
        var l = script[li];
        lineStart = Time.time;
        if (!string.IsNullOrEmpty(l.sfx)) Sfx.Play(l.sfx, l.vol);
        if (l.dark == 1) { if (mode != 1) StartCoroutine(Flicker()); }
        else if (l.dark == 2) mode = 2;
        if (l.bg != 0) StartCoroutine(SetBackground(l.bg));

        bool narr = l.kind == 1 || l.kind == 3;
        nameText.text = narr ? "" : l.who;
        string note = l.kind == 2 ? "속마음" : l.note;
        tagText.text = note ?? "";
        nameText.color = l.face == 0 || l.kind == 2 ? MuseumUI.Amber : MuseumUI.Ink;
        bodyText.color = l.kind == 3 ? MuseumUI.Amber : narr ? new Color(0.78f, 0.8f, 0.84f) : l.kind == 2 ? new Color(0.72f, 0.86f, 1f) : new Color(0.96f, 0.96f, 0.94f);

        wantFace = l.face >= 0 && l.face < faces.Length && faces[l.face] != null;
        if (wantFace)
        {
            var sp = faces[l.face];
            portraitImg.sprite = sp;
            var pc = portraitImg.color; pc.a = 0f; portraitImg.color = pc; faceAlpha = 0f;
            float ph = 514f;
            portraitRect.sizeDelta = new Vector2(ph * sp.rect.width / sp.rect.height, ph);
        }

        full = MuseumUI.Wrap(l.text, 1560f, 3);
        const float BoxCenter = -365f, NameH = 48f, Gap = 16f, LineH = 48f, LineGap = 12f;
        int n = MuseumUI.Lines(full);
        bool hasName = !narr && !string.IsNullOrEmpty(l.who);
        float textH = n * LineH + (n - 1) * LineGap;
        float blockH = (hasName ? NameH + Gap : 0f) + textH;
        float top = BoxCenter + blockH * 0.5f;
        bodyText.rectTransform.anchoredPosition = new Vector2(0f, top - (hasName ? NameH + Gap : 0f) - bodyText.rectTransform.sizeDelta.y * 0.5f);
        nameText.rectTransform.anchoredPosition = new Vector2(-550f, top - NameH * 0.5f);
        if (!string.IsNullOrEmpty(note))
            tagText.rectTransform.anchoredPosition = new Vector2(-800f + PixelFontData.Measure(l.who ?? "") * 3 + 30f + 350f, nameText.rectTransform.anchoredPosition.y - 4f);
        if (!hasName && !string.IsNullOrEmpty(note)) tagText.text = "";
        typed = 0f; typing = true; bodyText.text = "";
    }

    private IEnumerator SetBackground(int bg)
    {
        if (bg == 3) { for (float t = bgGroup.alpha; t > 0f; t -= Time.deltaTime * 2f) { bgGroup.alpha = t; yield return null; } bgGroup.alpha = 0f; yield break; }
        Sprite s = bg == 1 ? bgCamp : bg == 2 ? bgClass : null;
        if (bgGroup.alpha > 0.5f) { for (float t = 1f; t > 0f; t -= Time.deltaTime * 3f) { bgImg.color = new Color(1, 1, 1, t); yield return null; } }
        bgImg.sprite = s;
        Color tint = bg == 1 ? new Color(0.42f, 0.48f, 0.72f) : Color.white;   // 캠프의 밤: 푸르게 어둡게
        for (float t = 0f; t < 1f; t += Time.deltaTime * 1.5f) { bgImg.color = new Color(tint.r, tint.g, tint.b, s != null ? t : 0f); bgGroup.alpha = Mathf.Max(bgGroup.alpha, t); yield return null; }
        bgImg.color = new Color(tint.r, tint.g, tint.b, s != null ? 1f : 0f); bgGroup.alpha = 1f;
    }

    private IEnumerator EndScript(bool skipped)
    {
        if (ending) yield break;
        ending = true; typing = false;
        // 건너뛰어도 그 이야기에서 일어난 일(어둠·배경)은 끝 상태로 맞춘다
        if (skipped && script != null)
            for (int k = Mathf.Max(0, li + 1); k < script.Length; k++)
            {
                if (script[k].dark == 2) mode = 2;
                if (script[k].bg != 0 && script[k].bg != 3) { bgImg.sprite = script[k].bg == 1 ? bgCamp : bgClass; bgImg.color = script[k].bg == 1 ? new Color(0.42f, 0.48f, 0.72f) : Color.white; bgGroup.alpha = 1f; }
            }
        for (float t = 0f; t < 0.35f; t += Time.deltaTime) { dlgGroup.alpha = 1f - t / 0.35f; skipGroup.alpha = dlgGroup.alpha; yield return null; }
        dlgGroup.alpha = 0f; skipGroup.alpha = 0f; wantFace = false; faceAlpha = 0f;
        var c = portraitImg.color; c.a = 0f; portraitImg.color = c;
        while (flicking) yield return null;
        var done = onDone; script = null;
        bool endsGame = MuseumGame.Stage >= MuseumGame.Done;
        if (!endsGame)
        {
            if (bgGroup.alpha > 0f) yield return SetBackground(3);
            MuseumChat.Hidden = false;
            yield return new WaitForSeconds(0.25f);
            Busy = false;
        }
        done?.Invoke();
    }

    // ---------------------------------------------------------------- 엔딩 메시지 · 크레딧
    private IEnumerator EndCard()
    {
        Busy = true; MuseumChat.Hidden = true;
        float elapsed = MuseumGame.ClearTime > 0f ? MuseumGame.ClearTime - MuseumGame.StartTime : Time.time - MuseumGame.StartTime;
        Progress.RecordClear(elapsed);
        Bgm.Play("intro", 0.32f);
        Sfx.PlayLoop("storm_wind", 0f);

        endCv = MuseumUI.MakeCanvas(transform, "EndCanvas", 120);
        var root = MuseumUI.Node("Root", endCv.transform); MuseumUI.Stretch(root);
        var grp = root.gameObject.AddComponent<CanvasGroup>(); grp.alpha = 0f;
        MuseumUI.Img(MuseumUI.Stretched(MuseumUI.Node("Black", root)), null, new Color(0.02f, 0.025f, 0.035f, 1f));

        var logo = Resources.Load<Sprite>("Logo/stranger_logo");
        if (logo == null) { var tex = Resources.Load<Texture2D>("Logo/stranger_logo"); if (tex != null) logo = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)); }
        var lg = MuseumUI.Node("Logo", root); MuseumUI.Anchor(lg, 0.5f, 0.5f, 0f, 300f, 720f, 150f);
        var lgImg = MuseumUI.Img(lg, logo, Color.white); lgImg.preserveAspect = true; if (logo == null) lgImg.color = new Color(0, 0, 0, 0);

        var tag = MuseumUI.Text(root, "Tagline", "낯선 말, 하나가 되는 길", 3, MuseumUI.Mint, TextAnchor.MiddleCenter); MuseumUI.Anchor(tag.rectTransform, 0.5f, 0.5f, 0f, 170f, 1600f, 56f);
        string msg = "말이 통하지 않는 곳에서, 누군가는 매일 이런 하루를 보냅니다.\n오늘 내가 느낀 답답함을, 우리 반의 낯선 친구는 매일 느끼고 있을지도 몰라요.\n먼저 다가가 이름을 불러 주세요. 그 친구의 말로 '안녕'을 건네 보세요.";
        var body = MuseumUI.Text(root, "Message", msg, 2, MuseumUI.Ink, TextAnchor.MiddleCenter); MuseumUI.Anchor(body.rectTransform, 0.5f, 0.5f, 0f, 20f, 1700f, 180f);
        int words = MuseumWords.Learned.Count;
        string stat = "탈출 시간  " + ResultScreen.FormatTime(elapsed) + "     ·     배운 말  " + words + "개 (필리핀어 " + MuseumWords.CountOf(1) + " · 베트남어 " + MuseumWords.CountOf(2) + " · 줄루어 " + MuseumWords.CountOf(3) + ")";
        var st = MuseumUI.Text(root, "Stats", stat, 2, MuseumUI.Amber, TextAnchor.MiddleCenter); MuseumUI.Anchor(st.rectTransform, 0.5f, 0.5f, 0f, -130f, 1700f, 40f); st.maxWidth = 1700f;

        bool next = false;
        var credits = MuseumUI.Button(root, "Credits", "만든 사람들", 2, () => next = true);
        MuseumUI.Anchor(credits.Rect, 0.5f, 0.5f, -170f, -250f, 300f, 68f);
        bool title = false;
        var toTitle = MuseumUI.Button(root, "Title", "타이틀로", 2, () => title = true);
        MuseumUI.Anchor(toTitle.Rect, 0.5f, 0.5f, 170f, -250f, 300f, 68f);

        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { grp.alpha = t / 1.2f; yield return null; }
        grp.alpha = 1f;
        // 대화창 배경(교실)은 여기서 걷는다
        bgGroup.alpha = 0f;
        while (!next && !title)
        {
            MuseumUI.Tick();
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) next = true;
            yield return null;
        }
        if (next) yield return Credits(root);
        yield return Leave();
    }

    private static readonly string[] CreditLines =
    {
        "STRANGER",
        "",
        "기획 · 개발",
        "PARALLAX 팀  ·  2026 유네스코학교 프로젝트",
        "",
        "도움 받은 말",
        "필리핀어 · 베트남어 · 줄루어 인사와 숫자",
        "(베트남어는 도트 글꼴 때문에 성조 표시를 뺐습니다)",
        "",
        "음악 (CC0 · OpenGameArt)",
        "The Budding of Consciousness — fengyu970",
        "Forget Me Not — mentassss",
        "Apple Cider · Happy Adventure (Loop) · Mysterious Ambience (song21)",
        "",
        "효과음 (CC0)",
        "Kenney — UI Audio · RPG Audio · Interface Sounds",
        "rubberduck — 100 CC0 SFX",
        "Freesound — sonically_sound, mpaol2023, joedeshon, gokalp_gonen,",
        "yottasounds, myrinvp, claravinleb, khenshom, audible-edge,",
        "Deathscyp, jcdecha, EskildNP, cyborgcollective",
        "",
        "그림",
        "LPC (Liberated Pixel Cup) — Lanea Zimmerman, Stephen Challener,",
        "Charles Sanchez, Manuel Riecke, Daniel Armstrong 외 (CC-BY-SA 3.0 / OGA-BY 3.0)",
        "Big 32x32 Tileset — Pxl Dev, Ommtdh, Cherin, Kapa",
        "Furniture 32x32 (OpenGameArt, CC0)",
        "",
        "온라인  Photon Fusion (Exit Games)",
        "엔진  Unity",
        "",
        "",
        "함께 플레이해 주셔서 고맙습니다.",
        "Salamat · Cam on · Ngiyabonga · 고마워요",
    };

    private IEnumerator Credits(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 1; i--) root.GetChild(i).gameObject.SetActive(false);
        var roll = MuseumUI.Node("Roll", root); MuseumUI.Anchor(roll, 0.5f, 0f, 0f, 0f, 1700f, 10f);
        float y = 0f;
        var items = new List<RectTransform>();
        foreach (string line in CreditLines)
        {
            bool head = line == "STRANGER";
            bool section = line == "기획 · 개발" || line.StartsWith("음악") || line.StartsWith("효과음") || line == "그림" || line == "도움 받은 말";
            var t = MuseumUI.Text(roll, "L", line, head ? 4 : 2, head ? MuseumUI.Mint : section ? MuseumUI.Amber : MuseumUI.Ink, TextAnchor.MiddleCenter);
            MuseumUI.Anchor(t.rectTransform, 0.5f, 1f, 0f, -y, 1700f, head ? 80f : 44f);
            t.maxWidth = 1700f;
            y += head ? 96f : 48f;
        }
        roll.sizeDelta = new Vector2(1700f, y);
        float ch = endCv.GetComponent<RectTransform>().rect.height;
        float pos = -y - 20f, endPos = ch + 40f;
        bool fast = false;
        while (pos < endPos)
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            fast = (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed)) || (ms != null && ms.leftButton.isPressed);
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)) break;
            pos += Time.deltaTime * (fast ? 420f : 70f);
            roll.anchoredPosition = new Vector2(0f, pos);
            yield return null;
        }
    }
}
