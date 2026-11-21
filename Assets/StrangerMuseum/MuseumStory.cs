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
    private static void ResetStatics() { PowerOn = false; Busy = false; Leaving = false; mode = 0; I = null; powerAt = 0f; litRadius = 0f; }

    public static void RestorePower() { if (PowerOn) return; PowerOn = true; powerAt = Time.time; Sfx.Play("museum_power_restore", 0.8f); }

    /// <summary>화면 좌표(왼쪽 아래 기준)가 지금 보이는가(어두울 땐 손전등 안만).</summary>
    public static bool IsLitScreen(Vector3 screen)
    {
        if (mode == 0) return true;
        if (PowerOn && Time.time - powerAt >= 2.5f) return true;
        if (mode == 1) return false;
        if (litRadius2 > 0f && ((Vector2)screen - litCenter2).magnitude < litRadius2 * 0.85f) return true;
        return ((Vector2)screen - litCenter).magnitude < litRadius * 0.85f;
    }

    private static Vector2 litCenter2; private static float litRadius2;   // 내 주위의 작은 빛(서하린이 아닐 때)

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
        S(N("그때 진열장 사이에서 \"야옹\" 소리. 주황빛 얼룩 꼬리가 어둠 속으로 쏙 사라진다."), "ui_select", 0.4f),
        T("…단풍이? 우리 학교 고양이가 왜 여기 있어? 견학 버스에 몰래 탔구나. 찾아 줘야겠다."),
        M("[ 미션 1 ]  물길 막기 : 지하 관리 통로의 급수 밸브를 잠가 물이 차오르는 걸 막는다."),
        N("서로의 말을 모르는 넷은 카드로만 말할 수 있다. 오른쪽 아래 카드를 눌러 말을 만들고 Enter로 보낸다."),
    };

    private static readonly Line[][] Beats =
    {
        new[]
        {
            S(N("끼익. 마지막 밸브가 잠기자, 파이프를 울리던 물소리가 거짓말처럼 멎는다."), "clear", 0.6f),
            L("카이", "Kuhle! Kuhle kakhulu!", 0, 3, "두 팔을 번쩍 들며"),
            L("이안", "Galing! Salamat, Seoharin!", 0, 1, "서하린의 어깨를 툭 치며"),
            T("내 밸브의 숫자는 나만 몰랐어. 친구가 알려 줘서 맞춘 거야."),
            T("내가 다 이끌어야 한다고 생각했는데… 혼자서는 밸브 하나도 못 돌렸네."),
            M("[ 미션 2 ]  불 켜기 : 지하 전기실의 차단기를 올려 박물관의 불을 되살린다."),
        },
        new[]
        {
            N("차단기가 올라가고 천장의 불이 차례로 켜진다."),
            N("환해진 지하. 그제야 네 사람은 서로의 얼굴을 제대로 본다. 다들 땀과 먼지투성이다."),
            L("린", "Cam on… Cam on!", 0, 2, "웃으며 두 손을 모으고"),
            L("서하린", "어… 그건 고맙다는 말이지? 나도! 고마워!", 0, 0, "따라 웃으며"),
            T("말은 몰라도, 웃는 얼굴은 다 알아보겠다."),
            L("이안", "Nakasara ang hagdan sa itaas… Kailangan ang kumpas!", 0, 1, "벽의 안내문을 읽고 위층을 가리키며"),
            T("또 뭔가 적혀 있나 봐. 나만 못 읽는 글씨… 이번엔 친구들을 믿어 보자."),
            M("[ 미션 3 ]  나침반 잠금 : 1층 네 전시실의 명판에서 기호를 모아, 복도 나침반으로 2층 셔터를 연다."),
        },
        new[]
        {
            S(N("나침반이 맞물려 돌자, 덜컹 소리와 함께 2층 계단 셔터가 말려 올라간다."), "door_open", 0.8f),
            N("명판을 함께 읽을 때, 린은 서하린에게 자꾸 같은 말을 해 보였다. 서하린은 그 말을 조심스럽게 따라 해 본다."),
            L("서하린", "씬… 짜오? 이거 맞아?", 0, 0, "린에게, 조심스럽게"),
            L("린", "Dung roi! Xin chao!", 0, 2, "눈이 동그래져서, 활짝 웃으며"),
            T("씬 짜오… 어디서 들었는데. 아, 지호가 알려 줬던 말이다."),
            T("그때 우리는 '또 해 봐, 또!' 하면서 웃었지. 지호도 웃고 있었는데… 정말 웃고 싶었을까."),
            M("[ 미션 4 ]  옛 글자 풀기 : 2층 기록 아카이브의 잠긴 보관장을 연다."),
        },
        new[]
        {
            S(N("세 글자가 맞춰지자 보관장이 열린다. 안에는 대나무 조각 하나와 비상문 열쇠가 들어 있다."), "door_open", 0.7f),
            L("이안", "KA-SA-MA. Kasama!", 0, 1, "자기 가슴과 서하린, 린, 카이를 차례로 가리키며"),
            L("서하린", "카사마? 우리… 넷을 말하는 거야?", 0, 0),
            L("카이", "Yebo! Umngane!", 0, 3, "네 사람의 손을 한데 모으며"),
            T("함께하는 친구. 처음 만났을 때 나는 얘네를 '챙겨 줘야 할 애들'이라고만 생각했는데."),
            M("[ 미션 5 ]  네 나라 말 사전 : 2층 자료 열람실, 사전 자물쇠의 낱말 뜻을 그림으로 맞춘다."),
        },
        new[]
        {
            S(N("딸깍. 사전 자물쇠가 풀리자 서랍 안에서 반원 홀 비상문의 설계도가 나온다."), "door_open", 0.7f),
            L("린", "Nuoc, lua, sao, co… Hieu roi!", 0, 2, "사전의 그림을 하나씩 짚으며"),
            T("같은 '물'인데 tubig, nuoc, amanzi… 부르는 말은 다 달라. 그래도 가리키는 건 똑같은 물이야."),
            L("카이", "Sihambe! Umngane, sihambe!", 0, 3, "반원 홀 쪽을 가리키며"),
            M("[ 미션 6 ]  함께 서기 : 2층 반원 홀, 관측자가 알려 준 내 발판에 넷이 함께 선다."),
        },
        new[]
        {
            S(N("네 사람이 발판에 올라서자, 동상 아래에서 묵직한 톱니 소리가 울린다. 동상 뒤의 비상문이 천천히 열린다."), "museum_thud", 0.7f),
            N("문 너머로 바람이 잦아든 하늘이 보인다. 멀리서 캠프 스태프의 목소리가 들린다."),
            L("스태프", "여기 있었구나! 다들 괜찮아요? 정말 다행이다…!", 0, 4),
            B(N("그날 밤, BAYANI CAMP. 태풍이 지나간 하늘에 별이 가득하다. 네 사람은 모닥불 앞에 나란히 앉아 있다."), 1),
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

    /// <summary>단풍이를 다 찾았을 때만 엔딩에 들어가는 장면(모닥불 · 교실).</summary>
    private static Line[] WithDanpung(Line[] src)
    {
        var list = new List<Line>();
        foreach (var l in src)
        {
            list.Add(l);
            if (l.kind == 1 && l.text.StartsWith("세 친구가 동시에 웃음을"))
            {
                list.Add(N("모닥불 옆에서 단풍이가 하품을 한다. 이안은 'pusa', 린은 'meo', 카이는 'ikati'라고 부르는데, 단풍이는 누가 불러도 꼬리를 세우고 다가간다."));
                list.Add(T("부르는 말은 넷 다 달라도, 다들 같은 단풍이를 아끼고 있어. …우리도 그렇게 된 것 같아."));
            }
            if (l.kind == 1 && l.text.StartsWith("지호가 웃는다"))
            {
                list.Add(N("창밖 화단에서 '야옹' 소리. 단풍이다."));
                list.Add(L("지호", "Meo! …아니, 단풍아!", 0, 5, "창밖으로 손을 흔들며"));
                list.Add(T("베트남어로 고양이는 '메오'. 지호한테 배운 두 번째 말이다."));
            }
        }
        return list.ToArray();
    }

    /// <summary>
    /// 미션 뒤 이야기. 옛 글자 풀기(3)는 보통 · 어려움에서 보관장 낱말이 판마다 바뀌므로(PuzzleScript.WordIndex),
    /// 그 낱말과 뜻을 먼저 짧게 말하고 → 대나무 조각의 kasama 로 이어진다. 쉬움(WordIndex 0 = kasama)은 원래 대사 그대로.
    /// </summary>
    private static Line[] BeatLines(int stage)
    {
        var src = Beats[stage];
        if (stage == Beats.Length - 1 && MuseumCats.AllFound) return WithDanpung(src);
        if (stage != MuseumGame.Script || PuzzleScript.WordIndex == 0) return src;
        int wi = PuzzleScript.WordIndex;
        string meaning = wi >= 0 && wi < PuzzleScript.WordMeaning.Length ? PuzzleScript.WordMeaning[wi] : "";
        string lower = PuzzleScript.WordLower;
        string cap = string.IsNullOrEmpty(lower) ? lower : char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        var list = new List<Line>();
        foreach (var l in src)
        {
            if (l.who == "이안" && l.text.StartsWith("KA-SA-MA"))
            {
                list.Add(L("이안", PuzzleScript.WordText + ". " + cap + "!", 0, 1, "풀어낸 세 글자를 손가락으로 짚으며"));
                list.Add(N("'" + lower + "' — 풀이표에 적힌 뜻은 '" + meaning + "'. 그런데 이안이 보관장 안의 대나무 조각을 꺼내 보인다. 거기에도 글자 세 개가 새겨져 있다."));
                list.Add(L("이안", "At ito… KA-SA-MA. Kasama!", 0, 1, "대나무 조각을 읽고, 자기 가슴과 서하린, 린, 카이를 차례로 가리키며"));
                continue;
            }
            list.Add(l);
        }
        return list.ToArray();
    }

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
    private RectTransform skipRect; private Image skipBg;
    private RectTransform dlgRoot, endRoot;
    private float shownAt;   // 대화창이 다 떠오른 시각(연타한 X 가 다음 이야기까지 넘기지 않게)

    private const float CharsPerSec = 34f;

    // ---------------------------------------------------------------- 시작
    private void Awake()
    {
        I = this; cam = GetComponent<Camera>();
        Busy = true; mode = 2; PowerOn = false; MuseumChat.Hidden = true;
    }

    private void OnDestroy() { if (I == this) I = null; Busy = false; Leaving = false; MuseumChat.Hidden = false; }

    private void Start()
    {
        Ranking.NewRun();   // 이번 판 클리어 기록은 한 번 보낼 수 있다
        BuildDark();
        BuildDialogue();
        Sfx.PlayLoop("storm_wind", 0.12f);
        Play(Opening, () => { MuseumHud.ShowPlace(5f); MuseumHud.Think("지하로 가자. 복도 오른쪽 위에 계단이 있었어."); });
    }

    /// <summary>미션을 끝낸 뒤의 이야기. 마지막 미션 뒤에는 엔딩으로 이어진다.</summary>
    public static void PlayBeat(int stage)
    {
        if (I == null || stage < 0 || stage >= Beats.Length) return;
        bool last = stage == Beats.Length - 1;
        I.Play(BeatLines(stage), last ? (System.Action)(() => I.StartCoroutine(I.EndCard())) : () => { });   // 큰 안내판은 처음 한 번(오프닝 뒤)만 · 이후는 H
    }

    public static void LeaveToTitle() { if (I != null) I.StartCoroutine(I.Leave()); }

    /// <summary>시험용: 지금 나오는 이야기를 건너뛴다(X 키와 같다).</summary>
    public static void DebugSkip() { if (I != null && I.script != null && !I.ending) I.StartCoroutine(I.EndScript(true)); }

    /// <summary>시험용: 다음 대사로(타자 중이면 끝까지 보이기).</summary>
    public static void DebugNext() { if (I != null && I.script != null && !I.ending) { if (I.typing) I.typed = I.full.Length; else I.NextLine(); } }

    public static bool Leaving { get; private set; }

    private IEnumerator Leave()
    {
        Busy = true; Leaving = true;
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
        // 빛 구멍 두 개(손전등 + 내 주위 작은 빛)를 그리는 저해상도 가림막. 쓰일 때만 위의 네 판 + 둥근 구멍 대신 켠다
        var mk = MuseumUI.Node("Mask2", darkCv.transform);
        mk.anchorMin = mk.anchorMax = Vector2.zero; mk.pivot = Vector2.zero;
        maskImg = mk.gameObject.AddComponent<RawImage>(); maskImg.raycastTarget = false; maskImg.enabled = false;
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
        litCenter = new Vector2(sp.x, sp.y); litRadius = mode >= 2 ? R : 0f;

        // 내 주위의 작은 빛(반지름 약 30%): 내가 서하린이 아니고, 손전등 빛 밖으로 나와 있을 때만 두 번째 구멍
        litRadius2 = 0f;
        bool two = false;
        var museum = StrangerMuseum.Instance;
        if (mode >= 2 && Story1Role.Current != 0 && museum != null && museum.Visitor != null)
        {
            Vector3 mp = cam.WorldToScreenPoint(museum.Visitor.transform.position + new Vector3(0, 0.3f, 0));
            litCenter2 = new Vector2(mp.x, mp.y); litRadius2 = R * 0.3f;
            two = (litCenter2 - litCenter).magnitude > R * 0.55f;   // 이미 큰 빛 안이면 구멍 하나로 충분
        }
        maskImg.enabled = two && fa > 0.001f;
        for (int i = 0; i < 4; i++) fillImgs[i].color = new Color(0, 0, 0, two ? 0f : fa);
        spotImg.color = new Color(1, 1, 1, two ? 0f : fa);
        Place((RectTransform)spotImg.transform, x0, y0, 2 * R, 2 * R);
        if (maskImg.enabled) DrawMask(W, H, litCenter, R, litCenter2, litRadius2, fa);
    }

    // ---------------------------------------------------------------- 두 구멍 가림막(저해상도, 바뀔 때만 다시 그린다)
    private RawImage maskImg; private Texture2D maskTex; private Color32[] maskPx;
    private Vector4 maskLast = new Vector4(-1, -1, -1, -1); private float maskLastA = -1f;
    private const int MaskW = 192, MaskH = 108;

    private void DrawMask(float W, float H, Vector2 c1, float r1, Vector2 c2, float r2, float a)
    {
        Place((RectTransform)maskImg.transform, 0, 0, W, H);
        var key = new Vector4(Mathf.Round(c1.x / 3f), Mathf.Round(c1.y / 3f), Mathf.Round(c2.x / 3f), Mathf.Round(c2.y / 3f));
        if (maskTex != null && key == maskLast && Mathf.Abs(a - maskLastA) < 0.01f) return;
        maskLast = key; maskLastA = a;
        if (maskTex == null)
        {
            maskTex = new Texture2D(MaskW, MaskH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            maskPx = new Color32[MaskW * MaskH];
            maskImg.texture = maskTex;
        }
        float sx = W / MaskW, sy = H / MaskH;
        for (int y = 0; y < MaskH; y++)
        {
            float py = (y + 0.5f) * sy;
            for (int x = 0; x < MaskW; x++)
            {
                float px = (x + 0.5f) * sx;
                float d1 = Mathf.Sqrt((px - c1.x) * (px - c1.x) + (py - c1.y) * (py - c1.y)) / Mathf.Max(1f, r1);
                float d2 = Mathf.Sqrt((px - c2.x) * (px - c2.x) + (py - c2.y) * (py - c2.y)) / Mathf.Max(1f, r2);
                float k = Mathf.Min(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d1)), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d2)));
                maskPx[y * MaskW + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(k * a * 255f));
            }
        }
        maskTex.SetPixels32(maskPx); maskTex.Apply(false);
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
        // 그림은 프롤로그·캠프와 같은 원본(Assets/Art)을 묶음(StoryArt)으로 받는다 — 빌드에 한 번만 들어간다
        var art = Resources.Load<StoryArt>("StoryUI/StoryArt");
        if (art != null)
        {
            bgCamp = art.bgCamp; bgClass = art.bgClass;
            for (int i = 0; i < faces.Length && art.faces != null && i < art.faces.Length; i++) faces[i] = art.faces[i];
        }
        else Debug.LogWarning("[MuseumStory] Resources/StoryUI/StoryArt 가 없어 초상·배경 없이 진행합니다");

        var dlg = MuseumUI.Node("Dialogue", dlgCv.transform); MuseumUI.Stretch(dlg);
        dlgRoot = dlg;
        dlgGroup = dlg.gameObject.AddComponent<CanvasGroup>(); dlgGroup.alpha = 0f; dlgGroup.blocksRaycasts = false; dlgGroup.interactable = false;
        portraitRect = MuseumUI.Node("Portrait", dlg); portraitRect.pivot = new Vector2(0.5f, 0f);
        MuseumUI.At(portraitRect, -680, -232, 470, 514);
        portraitImg = MuseumUI.Img(portraitRect, null, new Color(1, 1, 1, 0)); portraitImg.preserveAspect = true;

        var box = MuseumUI.Node("Box", dlg); MuseumUI.At(box, 0, -365, 1760, 330);
        MuseumUI.Sliced(box, MuseumUI.FillSprite, new Color(0.05f, 0.06f, 0.09f, 0.985f));
        var bd = MuseumUI.Node("Border", box); MuseumUI.Stretch(bd); MuseumUI.Sliced(bd, MuseumUI.BorderSprite, MuseumUI.Gold);

        nameText = MuseumUI.Text(dlg, "Name", "", 3, MuseumUI.Ink, TextAnchor.MiddleLeft); MuseumUI.At(nameText.rectTransform, -550, -268, 500, 56);
        tagText = MuseumUI.Text(dlg, "Tag", "", 2, MuseumUI.Slate, TextAnchor.MiddleLeft); MuseumUI.At(tagText.rectTransform, 0, -272, 700, 36);
        bodyText = MuseumUI.Text(dlg, "Body", "", 3, MuseumUI.Ink, TextAnchor.UpperLeft); MuseumUI.At(bodyText.rectTransform, 0, -415, 1600, 200);
        nextMark = MuseumUI.Text(dlg, "Next", "▼", 2, MuseumUI.Amber, TextAnchor.MiddleCenter); MuseumUI.At(nextMark.rectTransform, 810, -466, 40, 36);

        // 건너뛰기: X · Esc 또는 마우스로 누르기(프롤로그 · 캠프와 같은 모양)
        var pill = MuseumUI.Node("Skip", dlgCv.transform);
        MuseumUI.Anchor(pill, 1f, 1f, -32f, -32f, 300f, 64f);
        skipBg = MuseumUI.Img(pill, null, new Color(0.04f, 0.05f, 0.08f, 0.72f));
        var st = MuseumUI.Text(pill, "Hint", StoryText.SkipLabel, 3, new Color(0.97f, 0.97f, 0.94f), TextAnchor.MiddleCenter); MuseumUI.Stretch(st.rectTransform);
        st.maxWidth = 284f;
        skipRect = pill;
        skipGroup = pill.gameObject.AddComponent<CanvasGroup>(); skipGroup.alpha = 0f; skipGroup.blocksRaycasts = false;
    }

    private Coroutine beginRoutine;
    private int playId;

    private void Play(Line[] lines, System.Action done)
    {
        if (beginRoutine != null) StopCoroutine(beginRoutine);
        script = lines; onDone = done; li = -1; ending = false; typing = false;
        Busy = true; MuseumChat.Hidden = true;
        MuseumGame.CloseAllPanels();
        playId++;
        // 스피드런: 이야기는 바로 건너뛴다(그 이야기에서 일어나는 일 · 어둠 · 배경은 EndScript 가 끝 상태로 맞춘다)
        if (Story1Net.Speedrun) { li = -1; StartCoroutine(EndScript(true)); return; }
        beginRoutine = StartCoroutine(Begin(playId));
    }

    private IEnumerator Begin(int id)
    {
        yield return new WaitForSeconds(0.35f);
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            if (ending || id != playId) yield break;   // 그 사이에 건너뛰었다면 대화창을 다시 띄우지 않는다
            dlgGroup.alpha = t / 0.25f; skipGroup.alpha = dlgGroup.alpha; yield return null;
        }
        if (ending || id != playId) yield break;
        dlgGroup.alpha = 1f; skipGroup.alpha = 1f;
        shownAt = Time.unscaledTime;
        beginRoutine = null;
        NextLine();
    }

    private void Update()
    {
        StoryText.FitWidth(dlgRoot, 1800f);   // 좁은 화면 · 다른 비율에서도 대사가 화면 밖(왼쪽)으로 잘리지 않게
        StoryText.FitWidth(endRoot, 1760f);
        if (!Busy || script == null || ending) return;
        tClock += Time.deltaTime;
        var kb = Keyboard.current; var ms = Mouse.current;
        if (Time.unscaledTime - MuseumChat.ForcedCloseAt < 1f) { kb = null; ms = null; }   // 채팅을 치던 중에 이야기가 시작됐다: 이어 친 글자(X·E·Space)가 이야기를 넘기지 않게
        StoryText.HoverSkip(skipRect, skipBg);
        bool touchSkip = TouchControls.ClosePressed && Time.unscaledTime - shownAt > 0.8f;   // 폰: 터치 '건너뛰기' 단추
        if (!touchSkip && MuseumUI.PointerPressed(out var sp0) && RectTransformUtility.RectangleContainsScreenPoint(skipRect, sp0, null) && Time.unscaledTime - shownAt > 0.8f) touchSkip = true;   // 터치로 건너뛰기 상자를 눌렀다
        if (dlgGroup.alpha > 0.99f && (touchSkip || (kb != null && StoryText.SkipPressed(skipRect, Time.unscaledTime - shownAt))))
        {
            Sfx.Play("ui_back"); skipFrame = Time.frameCount;
            StartCoroutine(EndScript(true)); return;
        }
        bool onSkip = StoryText.Over(skipRect);
        bool go = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                  || (Time.unscaledTime - MuseumChat.ForcedCloseAt >= 1f && MuseumUI.PointerPressed(out var tp) && !RectTransformUtility.RectangleContainsScreenPoint(skipRect, tp, null) && !onSkip && !MuseumUI.OverButton(tp));   // 마우스·터치 공통
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
        if (li >= script.Length) { StartCoroutine(EndScript(false)); return; }
        var l = script[li];
        lineStart = Time.time;
        if (!string.IsNullOrEmpty(l.sfx)) Sfx.Play(l.sfx, l.vol);
        else if (li > 0) Sfx.Play("ui_next", 0.35f);
        if (l.bg == 1) Bgm.Play("camp", 0.24f);
        if (l.bg == 2) Bgm.Play("prologue", 0.24f);
        if (l.dark == 1) { if (mode != 1) StartCoroutine(Flicker()); }
        else if (l.dark == 2) mode = 2;
        if (l.bg != 0) StartCoroutine(SetBackground(l.bg));

        bool narr = l.kind == 1 || l.kind == 3;
        string who = StoryText.Speaker(l.who);   // 로비에서 정한 이름으로
        nameText.text = narr ? "" : who;
        string note = l.kind == 2 ? "속마음" : StoryText.Apply(l.note);
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
            // 내 캐릭터는 왼쪽, 상대는 오른쪽(오른쪽은 좌우를 뒤집어 안쪽을 보게)
            bool left = StoryText.IsMine(l.face, true);
            portraitRect.anchoredPosition = new Vector2(left ? -680f : 680f, portraitRect.anchoredPosition.y);
            portraitRect.localScale = new Vector3(left ? 1f : -1f, 1f, 1f);
        }

        full = MuseumUI.Wrap(StoryText.Apply(l.text), 1560f, 3);
        bool hasName = !narr && !string.IsNullOrEmpty(who);
        // 이름표 · 대사 자리는 고정(말하는 사람이나 줄 수가 바뀌어도 움직이지 않는다). 넘치는 줄은 글씨를 줄여 상자 안에
        bodyText.maxWidth = 1600f;
        bodyText.rectTransform.anchoredPosition = new Vector2(0f, StoryText.BodyTop - bodyText.rectTransform.sizeDelta.y * 0.5f);
        nameText.rectTransform.anchoredPosition = new Vector2(-550f, StoryText.NameY);
        if (!string.IsNullOrEmpty(note))
            tagText.rectTransform.anchoredPosition = new Vector2(-800f + PixelFontData.Measure(who ?? "") * 3 + 30f + 350f, nameText.rectTransform.anchoredPosition.y - 4f);
        if (!hasName && !string.IsNullOrEmpty(note)) tagText.text = "";
        typed = 0f; typing = true; bodyText.text = "";
    }

    private IEnumerator SetBackground(int bg)
    {
        if (bg == 3) { for (float t = bgGroup.alpha; t > 0f; t -= Time.deltaTime * 2f) { bgGroup.alpha = t; yield return null; } bgGroup.alpha = 0f; yield break; }
        Sprite s = bg == 1 ? bgCamp : bg == 2 ? bgClass : null;
        if (bgGroup.alpha > 0.5f) { for (float t = 1f; t > 0f; t -= Time.deltaTime * 3f) { bgImg.color = new Color(1, 1, 1, t); yield return null; } }
        bgImg.sprite = s;
        Color tint = bg == 1 ? new Color(0.3f, 0.34f, 0.58f) : Color.white;   // 캠프의 밤: 푸르게 어둡게
        for (float t = 0f; t < 1f; t += Time.deltaTime * 1.5f) { bgImg.color = new Color(tint.r, tint.g, tint.b, s != null ? t : 0f); bgGroup.alpha = Mathf.Max(bgGroup.alpha, t); yield return null; }
        bgImg.color = new Color(tint.r, tint.g, tint.b, s != null ? 1f : 0f); bgGroup.alpha = 1f;
    }

    private IEnumerator EndScript(bool skipped)
    {
        if (ending) yield break;
        ending = true; typing = false;
        int myId = playId;   // 닫는 사이에 다음 이야기가 시작되면(온라인: 친구가 다음 미션을 끝냄) 그 이야기를 지우지 않는다
        if (beginRoutine != null) { StopCoroutine(beginRoutine); beginRoutine = null; }
        // 건너뛰어도 그 이야기에서 일어난 일(어둠·배경)은 끝 상태로 맞춘다
        if (skipped && script != null)
            for (int k = Mathf.Max(0, li + 1); k < script.Length; k++)
            {
                if (script[k].dark == 2) mode = 2;
                if (script[k].bg != 0 && script[k].bg != 3 && MuseumGame.Stage < MuseumGame.Done) { bgImg.sprite = script[k].bg == 1 ? bgCamp : bgClass; bgImg.color = script[k].bg == 1 ? new Color(0.3f, 0.34f, 0.58f) : Color.white; bgGroup.alpha = 1f; }
            }
        float from = dlgGroup.alpha;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            if (playId != myId) yield break;
            dlgGroup.alpha = from * (1f - t / 0.35f); skipGroup.alpha = dlgGroup.alpha; yield return null;
        }
        if (playId != myId) yield break;
        dlgGroup.alpha = 0f; skipGroup.alpha = 0f; wantFace = false; faceAlpha = 0f;
        var c = portraitImg.color; c.a = 0f; portraitImg.color = c;
        while (flicking) yield return null;
        if (playId != myId) yield break;
        var done = onDone; script = null;
        bool endsGame = MuseumGame.Stage >= MuseumGame.Done;
        if (!endsGame)
        {
            if (bgGroup.alpha > 0f) yield return SetBackground(3);
            if (playId != myId) { done?.Invoke(); yield break; }
            MuseumChat.Hidden = false;
            yield return new WaitForSeconds(0.25f);
            if (playId != myId) { done?.Invoke(); yield break; }
            Busy = false;
        }
        done?.Invoke();
    }

    // ---------------------------------------------------------------- 엔딩 메시지 · 크레딧
    private IEnumerator EndCard()
    {
        Busy = true; MuseumChat.Hidden = true;
        float elapsed = MuseumGame.Elapsed;   // 실수 벌점(보통 +5초 · 어려움 +15초) 포함
        float prevBest = Progress.bestSeconds;
        bool newBest = elapsed > 0f && (prevBest <= 0f || elapsed < prevBest);
        Progress.RecordClear(elapsed);
        // 랭킹: 로컬(난이도별 상위 5)에 넣고, 온라인은 혼자 하기 · 방장만 1회 보낸다(실패해도 조용히)
        int diff = Story1Net.Difficulty;
        bool speed = Story1Net.Speedrun;
        int board = Ranking.Board(diff, speed);   // 스토리 · 스피드런 기록은 따로 센다
        var entry = Ranking.Make(elapsed, MuseumCats.FoundCount);
        int place = Ranking.AddLocal(board, entry);
        int onlineRank = 0;   // 0 기다리는 중 · -1 없음 · n 순위
        if (!Story1Net.Online || Session.IsHost) Ranking.Submit(board, entry, r => onlineRank = r);
        else onlineRank = -1;
        Bgm.Play("intro", 0.32f);
        Sfx.PlayLoop("storm_wind", 0f);

        endCv = MuseumUI.MakeCanvas(transform, "EndCanvas", 120);
        var root = MuseumUI.Node("Root", endCv.transform); MuseumUI.Stretch(root);
        endRoot = root;
        var grp = root.gameObject.AddComponent<CanvasGroup>(); grp.alpha = 0f;
        var blackR = MuseumUI.Stretched(MuseumUI.Node("Black", root));
        blackR.offsetMin = new Vector2(-2400f, -2400f); blackR.offsetMax = new Vector2(2400f, 2400f);   // 좁은 화면에서 Root 를 줄여도 검은 바탕은 화면을 덮게
        MuseumUI.Img(blackR, null, new Color(0.02f, 0.025f, 0.035f, 1f));

        var logo = Resources.Load<Sprite>("Logo/stranger_logo");
        if (logo == null) { var tex = Resources.Load<Texture2D>("Logo/stranger_logo"); if (tex != null) logo = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)); }
        var lg = MuseumUI.Node("Logo", root); MuseumUI.Anchor(lg, 0.5f, 0.5f, 0f, 300f, 720f, 150f);
        var lgImg = MuseumUI.Img(lg, logo, Color.white); lgImg.preserveAspect = true; if (logo == null) lgImg.color = new Color(0, 0, 0, 0);

        var tag = MuseumUI.Text(root, "Tagline", "낯선 말, 하나가 되는 길", 3, MuseumUI.Mint, TextAnchor.MiddleCenter); MuseumUI.Anchor(tag.rectTransform, 0.5f, 0.5f, 0f, 170f, 1600f, 56f);
        string msg = "말이 통하지 않는 낯선 곳. 누군가에게는 그곳이 매일의 교실입니다.\n오늘 느낀 답답함을, 우리 곁의 친구는 매일 겪고 있을지도 모릅니다.\n그 친구에게 필요한 것은 '챙겨 주는 사람'이 아니라, 함께하는 친구입니다.";
        var body = MuseumUI.Text(root, "Message", msg, 2, MuseumUI.Ink, TextAnchor.MiddleCenter); MuseumUI.Anchor(body.rectTransform, 0.5f, 0.5f, 0f, 20f, 1700f, 180f);
        int words = MuseumWords.Learned.Count;
        string stat = "탈출 시간  " + (speed ? Ranking.TimeTextFine(elapsed) : ResultScreen.FormatTime(elapsed)) + (MuseumGame.Mistakes > 0 ? "  (실수 " + MuseumGame.Mistakes + (MuseumGame.Penalty > 0f ? " · +" + Mathf.RoundToInt(MuseumGame.Penalty) + "초" : "") + ")" : "") + "     ·     배운 말  " + words + "개 (필리핀어 " + MuseumWords.CountOf(1) + " · 베트남어 " + MuseumWords.CountOf(2) + " · 줄루어 " + MuseumWords.CountOf(3) + ")";
        var st = MuseumUI.Text(root, "Stats", stat, 2, MuseumUI.Amber, TextAnchor.MiddleCenter); MuseumUI.Anchor(st.rectTransform, 0.5f, 0.5f, 0f, -130f, 1700f, 40f); st.maxWidth = 1700f;
        // 두 번째 줄: 최고 기록(타임어택) · 숨은 고양이
        string best = newBest ? "최고 기록 갱신!" : "최고 기록  " + ResultScreen.FormatTime(Progress.bestSeconds);
        string catLine = "숨은 단풍이  " + MuseumCats.FoundCount + "/" + MuseumCats.Total + (MuseumCats.FoundCount >= MuseumCats.Total ? "  · 단풍이와 함께 탈출!" : "");
        string rankLine = Paradox.Game.Story1Rules.Names[Paradox.Game.Story1Rules.Clamp(diff)] + (speed ? " 스피드런" : "") + " 이번 기록 " + place + "위";
        var st2 = MuseumUI.Text(root, "Stats2", best + "   ·   " + rankLine + "   ·   " + catLine, 2, newBest ? MuseumUI.Mint : MuseumUI.Slate, TextAnchor.MiddleCenter);
        MuseumUI.Anchor(st2.rectTransform, 0.5f, 0.5f, 0f, -176f, 1700f, 40f); st2.maxWidth = 1700f;

        bool next = false;
        var credits = MuseumUI.Button(root, "Credits", "만든 사람들", 2, () => next = true);
        MuseumUI.Anchor(credits.Rect, 0.5f, 0.5f, -340f, -250f, 300f, 68f);
        var rankBtn = MuseumUI.Button(root, "Ranking", "랭킹", 2, () => RankPanel.Open(board, entry));
        MuseumUI.Anchor(rankBtn.Rect, 0.5f, 0.5f, 0f, -250f, 300f, 68f);
        bool title = false;
        var toTitle = MuseumUI.Button(root, "Title", "타이틀로", 2, () => title = true);
        MuseumUI.Anchor(toTitle.Rect, 0.5f, 0.5f, 340f, -250f, 300f, 68f);

        // 로컬 상위 5(이번 기록은 민트)
        var top = Ranking.Local(board);
        for (int i = 0; i < top.Count && i < Ranking.LocalKeep; i++)
        {
            var e = top[i];
            bool mine = Mathf.Abs(e.time - entry.time) < 0.05f && Ranking.TeamText(e) == Ranking.TeamText(entry) && e.date == entry.date;
            string row = (i + 1) + "위   " + (speed ? Ranking.TimeTextFine(e.time) : Ranking.TimeText(e.time)) + "   " + Ranking.TeamText(e) + "   단풍 " + e.cats;
            var rt = MuseumUI.Text(root, "Top" + i, row, 2, mine ? MuseumUI.Mint : MuseumUI.Ink, TextAnchor.MiddleCenter);
            MuseumUI.Anchor(rt.rectTransform, 0.5f, 0.5f, 0f, -322f - i * 34f, 1400f, 32f); rt.maxWidth = 1400f;
        }

        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { grp.alpha = t / 1.2f; yield return null; }
        grp.alpha = 1f;
        // 대화창 배경(교실)은 여기서 걷는다
        bgGroup.alpha = 0f;
        int shownOnline = 0;
        while (!next && !title)
        {
            if (onlineRank != shownOnline) { shownOnline = onlineRank; if (onlineRank > 0) st2.text = best + "   ·   " + rankLine + " (온라인 " + onlineRank + "위)   ·   " + catLine; }
            if (RankPanel.IsOpen || Time.frameCount <= RankPanel.ClosedFrame + 1) { yield return null; continue; }   // 랭킹 창이 열려 있으면 뒤 단추 · 키는 쉰다
            MuseumUI.Tick();
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) next = true;
            yield return null;
        }
        RankPanel.CloseAll();
        if (next) yield return Credits(root);
        yield return Leave();
    }

    private static string[] CreditLines => Paradox.Client.Credits.Lines;   // 타이틀 설정 창의 "만든 사람들"과 같은 목록

    private IEnumerator Credits(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 1; i--) root.GetChild(i).gameObject.SetActive(false);
        var roll = MuseumUI.Node("Roll", root); MuseumUI.Anchor(roll, 0.5f, 0f, 0f, 0f, 1700f, 10f);
        float y = 0f;
        var items = new List<RectTransform>();
        foreach (string line in CreditLines)
        {
            bool head = line == "STRANGER";
            bool section = line == "만든 사람들" || line.StartsWith("음악") || line.StartsWith("효과음") || line == "그림" || line == "도움 받은 말";
            var t = MuseumUI.Text(roll, "L", line, head ? 4 : 2, head ? MuseumUI.Mint : section ? MuseumUI.Amber : MuseumUI.Ink, TextAnchor.MiddleCenter);
            MuseumUI.Anchor(t.rectTransform, 0.5f, 1f, 0f, -y, 1700f, head ? 80f : 44f);
            t.maxWidth = 1700f;
            y += head ? 96f : 48f;
        }
        roll.sizeDelta = new Vector2(1700f, y);
        float ch = endCv.GetComponent<RectTransform>().rect.height;
        float pos = -y + 120f, endPos = ch + 40f;
        bool fast = false;
        while (pos < endPos)
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            fast = (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed)) || (ms != null && ms.leftButton.isPressed);
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)) break;
            pos += Time.deltaTime * (fast ? 420f : 85f);
            roll.anchoredPosition = new Vector2(0f, pos);
            yield return null;
        }
    }
}
