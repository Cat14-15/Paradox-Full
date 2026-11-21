using Paradox.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 휴대폰·태블릿 터치 조작(PC 에서는 아무것도 보이지 않는다).
///  · 왼쪽 아래: 누른 자리를 중심으로 생기는 가상 조이스틱(도트 원 2개) → 이동.
///  · 오른쪽 아래(채팅 막대 위): 큰 단추 "조사"(Z/E) · "닫기"(X), 작은 단추 "채팅" · "수첩" · "핑".
/// 값은 정적 속성으로 내놓는다: StrangerMuseum 이 이동·조사를 읽고, 채팅·핑·닫기(이야기 건너뛰기)는 각 화면이 *Pressed 를 읽어 쓴다.
/// </summary>
public sealed class TouchControls : MonoBehaviour
{
    /// <summary>조이스틱 이동(-1~1, 위가 +y). 터치 조작이 꺼져 있으면 0.</summary>
    public static Vector2 Move { get; private set; }
    /// <summary>이번 프레임에 누른 단추(한 프레임만 true).</summary>
    public static bool InteractPressed => I != null && I.interactFrame == Time.frameCount;
    public static bool ClosePressed => I != null && I.closeFrame == Time.frameCount;
    public static bool ChatPressed => I != null && I.chatFrame == Time.frameCount;
    public static bool PingPressed => I != null && I.pingFrame == Time.frameCount;
    /// <summary>터치 조작이 켜져 있는가(휴대폰이거나 터치를 쓴 적 있음).</summary>
    public static bool Active => I != null && I.on;

    private static TouchControls I;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { I = null; Move = Vector2.zero; }

    private Canvas cv;
    private RectTransform root, stickBase, stickKnob, right;
    private PxButton interactBtn, closeBtn, chatBtn, bookBtn, pingBtn;
    private bool on;
    private int stickTouch = -1;
    private Vector2 stickOrigin;
    private int interactFrame = -10, closeFrame = -10, chatFrame = -10, pingFrame = -10;

    private const float StickRadius = 110f;   // 캔버스 단위(1920x1080 기준)

    private void Awake()
    {
        I = this;
        cv = MuseumUI.MakeCanvas(transform, "TouchCanvas", 97);
        root = MuseumUI.Node("Touch", cv.transform); MuseumUI.Stretch(root);

        stickBase = MuseumUI.Node("StickBase", root);
        MuseumUI.Img(stickBase, Ring(48, 4), new Color(1f, 1f, 1f, 0.35f));
        stickBase.sizeDelta = new Vector2(StickRadius * 2f, StickRadius * 2f);
        stickKnob = MuseumUI.Node("StickKnob", stickBase);
        MuseumUI.Img(stickKnob, Disc(24), new Color(0.949f, 0.773f, 0.447f, 0.75f));
        stickKnob.sizeDelta = new Vector2(96f, 96f);
        stickBase.gameObject.SetActive(false);

        right = MuseumUI.Node("Right", root);
        right.anchorMin = right.anchorMax = right.pivot = new Vector2(1f, 0f);
        right.sizeDelta = new Vector2(420f, 330f);
        interactBtn = MuseumUI.Button(right, "Interact", "조사", 3, () => interactFrame = Time.frameCount);
        MuseumUI.Anchor(interactBtn.Rect, 1f, 0f, -24f, 24f, 190f, 150f);
        interactBtn.Accent = MuseumUI.Mint;
        closeBtn = MuseumUI.Button(right, "Close", "닫기", 3, OnClose);
        MuseumUI.Anchor(closeBtn.Rect, 1f, 0f, -230f, 24f, 160f, 130f);
        closeBtn.Sound = "ui_back";
        chatBtn = MuseumUI.Button(right, "Chat", "채팅", 2, () => chatFrame = Time.frameCount);
        MuseumUI.Anchor(chatBtn.Rect, 1f, 0f, -24f, 190f, 120f, 70f);
        bookBtn = MuseumUI.Button(right, "Book", "수첩", 2, OnBook);
        MuseumUI.Anchor(bookBtn.Rect, 1f, 0f, -156f, 190f, 120f, 70f);
        pingBtn = MuseumUI.Button(right, "Ping", "핑", 2, () => pingFrame = Time.frameCount);
        MuseumUI.Anchor(pingBtn.Rect, 1f, 0f, -288f, 190f, 100f, 70f);

        on = Application.isMobilePlatform;
        root.gameObject.SetActive(on);
    }

    private void OnDestroy() { if (I == this) { I = null; Move = Vector2.zero; } }

    private void OnClose()
    {
        closeFrame = Time.frameCount;
        if (MuseumGame.AnyPanelOpen) MuseumGame.CloseAllPanels();   // 퍼즐 창·수첩·설명 창 닫기(X 와 같은 일)
    }

    private void OnBook() { if (!MuseumStory.Busy) MuseumHud.ToggleBook(); }

    private void Update()
    {
        // 휴대폰이 아니어도 터치를 한 번 쓰면 켠다(터치 노트북·태블릿 브라우저)
        if (!on && MuseumUI.TouchUsed) { on = true; root.gameObject.SetActive(true); }
        if (!on) { Move = Vector2.zero; return; }
        MuseumUI.Tick();
        PlaceRight();

        bool story = MuseumStory.Busy;
        bool panel = MuseumGame.AnyPanelOpen;
        // 이야기 중에는 '닫기'(건너뛰기)만, 창이 열려 있으면 '닫기'와 작은 단추만
        interactBtn.SetActive(!story && !panel);
        chatBtn.SetActive(!story);
        bookBtn.SetActive(!story);
        pingBtn.SetActive(!story && !panel);
        closeBtn.SetText(story ? "건너뛰기" : "닫기");
        closeBtn.SetActive(story || panel);

        UpdateStick(!story && !panel && !MuseumChat.Typing);
    }

    /// <summary>오른쪽 단추 묶음을 채팅 막대 바로 위에 둔다(겹치지 않게).</summary>
    private void PlaceRight()
    {
        float sf = cv.scaleFactor > 0.01f ? cv.scaleFactor : 1f;
        float bottom = 0f;
        if (MuseumChat.ChatRect(out Rect gui) && gui.xMax > Screen.width * 0.5f)
            bottom = (Screen.height - gui.yMin) / sf + 8f;   // 채팅의 위쪽 끝(화면 아래에서 잰 높이)
        float maxBottom = Screen.height / sf - 340f;
        right.anchoredPosition = new Vector2(0f, Mathf.Clamp(bottom, 0f, Mathf.Max(0f, maxBottom)));
    }

    private void UpdateStick(bool allowed)
    {
        var ts = Touchscreen.current;
        if (!allowed || ts == null)
        {
            stickTouch = -1; Move = Vector2.zero;
            if (stickBase.gameObject.activeSelf) stickBase.gameObject.SetActive(false);
            return;
        }
        float sf = cv.scaleFactor > 0.01f ? cv.scaleFactor : 1f;
        if (stickTouch < 0)
        {
            foreach (var t in ts.touches)
            {
                if (!t.press.wasPressedThisFrame) continue;
                Vector2 p = t.position.ReadValue();
                // 화면 왼쪽 아래 60% · 단추·채팅 위가 아닌 곳
                if (p.x > Screen.width * 0.5f || p.y > Screen.height * 0.7f) continue;
                if (MuseumUI.OverButton(p) || MuseumChat.CoversScreen(p)) continue;
                stickTouch = t.touchId.ReadValue();
                stickOrigin = p;
                stickBase.anchorMin = stickBase.anchorMax = Vector2.zero; stickBase.pivot = new Vector2(0.5f, 0.5f);
                stickBase.anchoredPosition = p / sf;
                stickBase.gameObject.SetActive(true);
                break;
            }
        }
        if (stickTouch < 0) { Move = Vector2.zero; return; }
        bool found = false;
        foreach (var t in ts.touches)
        {
            if (t.touchId.ReadValue() != stickTouch) continue;
            if (!t.press.isPressed) break;
            found = true;
            Vector2 off = (t.position.ReadValue() - stickOrigin) / sf;
            Vector2 clamped = Vector2.ClampMagnitude(off, StickRadius);
            stickKnob.anchoredPosition = clamped;
            Vector2 v = clamped / StickRadius;
            Move = v.magnitude < 0.18f ? Vector2.zero : v;
            break;
        }
        if (!found)
        {
            stickTouch = -1; Move = Vector2.zero;
            stickKnob.anchoredPosition = Vector2.zero;
            stickBase.gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------- 도트 원 그림
    private static Sprite Ring(int r, int thick)
    {
        int n = r * 2;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - r + 0.5f, y - r + 0.5f).magnitude;
                px[y * n + x] = d <= r - 0.5f && d >= r - thick ? new Color32(255, 255, 255, 255) : d < r - thick ? new Color32(255, 255, 255, 40) : new Color32(0, 0, 0, 0);
            }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite Disc(int r)
    {
        int n = r * 2;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - r + 0.5f, y - r + 0.5f).magnitude;
                px[y * n + x] = d <= r - 0.5f ? (d >= r - 3 ? new Color32(60, 40, 20, 255) : new Color32(255, 255, 255, 255)) : new Color32(0, 0, 0, 0);
            }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
