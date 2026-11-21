using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 박물관 협동 퍼즐의 공통 틀.
///  · 월드: 정해진 방의 조작 지점(지도 픽셀) 근처에서 E → 조작창이 열린다.
///  · 조작창: 채팅 왼쪽에 뜨는 도트 패널(제목 · 한 줄 설명 · 본문 · 팀 상태 · 닫기). 채팅은 항상 보인다.
///  · 상태는 MuseumNet.Puzzle 이벤트로만 바꾼다(혼자 할 때도 같은 길) → 온라인에서도 모두 같은 상태.
///  · AI 친구의 판단은 MuseumNet.Authority(혼자면 나, 온라인은 방장)에서만 한다.
/// </summary>
public abstract class MuseumPuzzle : MonoBehaviour
{
    public abstract int Id { get; }
    public abstract int Stage { get; }
    public abstract string Floor { get; }
    public abstract string Room { get; }
    public abstract string Title { get; }

    public bool IsOpen { get; private set; }
    public bool Solved { get; protected set; }
    public bool Active => MuseumGame.Stage == Stage && !Solved && !MuseumStory.Busy;
    /// <summary>이 미션이 끝났는가(내가 풀었든, 다른 화면에서 끝났다는 소식을 받았든).</summary>
    public bool Done => Solved || MuseumGame.Stage > Stage;
    /// <summary>누군가 조작을 시작했는가(그 전에는 AI 도 가만히 있는다).</summary>
    public bool Engaged { get; protected set; }

    protected StrangerMuseum Museum => StrangerMuseum.Instance;
    protected static int Me => Story1Role.Current;
    protected static bool Bot(int slot) => Story1Role.IsBot(slot);

    protected RectTransform panel, body;
    protected PixelText titleText, subText, keysText;
    /// <summary>창 맨 아래 키보드 안내.</summary>
    protected virtual string KeysHint => "X 닫기";
    private static readonly string[] RoleShort = { "조작자 · 장치 작동은 내 몫", "통역 · 외국어 뜻이 보인다", "내비게이터 · 지도(M)와 길", "관측자 · 맞은 칸이 보인다" };
    /// <summary>창 맨 아래 줄: 내 역할(무엇을 하는지) + 키 안내.</summary>
    /// <summary>이 퍼즐에서 특히 능력이 빛나는 역할(-1 = 없음). 제목 옆에 "핵심 · 관측자 카이"처럼 보인다.</summary>
    protected virtual int KeyRole => -1;
    private static readonly string[] RoleName = { "조작자", "통역", "내비게이터", "관측자" };
    protected string TitleLine(string t) => KeyRole < 0 ? t : t + "   · 핵심 " + RoleName[KeyRole] + " " + Name(KeyRole) + (KeyRole == Me ? "(나)" : "");
    protected string KeysLine => KeysHint;   // 역할은 제목 옆(핵심 역할)·H 도움말에 있다. 이 줄은 작아서 키만.
    private string subRaw;

    /// <summary>제목 아래 설명(두 줄까지 자동 줄바꿈).</summary>
    protected void Sub(string s)
    {
        if (s == subRaw) return;
        subRaw = s;
        MuseumUI.SetWrapped(subText, s, PanelW - 72f);
    }

    /// <summary>본문 아래 한 줄 도움말(본문 맨 아래 칸).</summary>
    protected PixelText MakeHint(RectTransform b)
    {
        var h = MuseumUI.Text(b, "Hint", "", 2, MuseumUI.Slate, TextAnchor.MiddleCenter);
        MuseumUI.Anchor(h.rectTransform, 0.5f, 0f, 0f, 2f, PanelW - 80f, 40f);
        h.maxWidth = PanelW - 80f;
        return h;
    }
    private PxButton closeButton;
    private readonly PixelText[] chips = new PixelText[4];
    private readonly UnityEngine.UI.Image[] chipBacks = new UnityEngine.UI.Image[4];
    private int openFrame;

    protected virtual float PanelW => 940f;
    protected virtual float PanelH => 600f;
    protected virtual bool ShowTeam => true;

    // ---------------------------------------------------------------- 수명
    protected virtual void Awake() { }

    protected virtual void Start()
    {
        MuseumNet.PuzzleEvent += OnPuzzleNet;
        Story1Net.ChatLine += OnChatLine;
        BuildPanel();
        BuildWorld();
    }

    protected virtual void OnDestroy()
    {
        MuseumNet.PuzzleEvent -= OnPuzzleNet;
        Story1Net.ChatLine -= OnChatLine;
    }

    private void OnPuzzleNet(int puzzle, int op, string v, int actor) { if (puzzle == Id) OnNet(op, v, actor); }
    private void OnChatLine(int slot, string text) { if (Active) OnChat(slot, text); }

    // ---------------------------------------------------------------- 월드에서
    /// <summary>근처에 조작 지점이 있으면 안내 문구를 돌려준다.</summary>
    public virtual bool Near(string floor, string room, Vector2 pos, float radius, out string prompt)
    {
        prompt = null;
        if (floor != Floor || room != Room || MuseumGame.Stage < Stage) return false;
        int i = NearestStation(pos, radius);
        if (i < 0) return false;
        prompt = Done ? Title + " (완료)" : PromptAt(i);
        return true;
    }

    public virtual bool Interact(string floor, string room, Vector2 pos, float radius)
    {
        if (!Near(floor, room, pos, radius, out _)) return false;
        if (Done) { MuseumHud.Toast(Title + " : 이미 해결했어요", MuseumUI.Mint); return true; }
        OnInteract(NearestStation(pos, radius));
        return true;
    }

    protected abstract Vector2[] Stations { get; }

    /// <summary>이 층의 퍼즐 장치 근처인가(단풍이가 장치 앞에 숨지 않게). 지도 좌표는 층 안에서 겹치지 않으니 방은 보지 않는다.</summary>
    public bool NearStation(string floor, string room, Vector2 p, float radius)
    {
        if (floor != Floor) return false;
        foreach (var s in Stations) if ((s - p).sqrMagnitude < radius * radius) return true;
        return false;
    }
    protected virtual string PromptAt(int station) => Title;
    protected virtual void OnInteract(int station) { Open(); }

    protected int NearestStation(Vector2 pos, float radius)
    {
        var st = Stations; int best = -1; float bd = radius * radius;
        for (int i = 0; i < st.Length; i++) { float d = (st[i] - pos).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        return best;
    }

    /// <summary>AI(slot)가 지금 가서 서 있어야 할 자리. 없으면 false(플레이어를 따라다닌다).</summary>
    public virtual bool BotStand(int slot, out Vector2 stand) { stand = default; return false; }
    /// <summary>BotStand 자리가 있는 방(기본은 이 퍼즐의 방).</summary>
    public virtual string BotStandRoom(int slot) => Room;

    /// <summary>미션 카드에 덧붙일 진행 한 줄(예: "명판 2/4"). 없으면 null.</summary>
    public virtual string Progress => null;

    /// <summary>안내 표시가 가리킬 자리(이 퍼즐의 방 안).</summary>
    public virtual Vector2 GuidePoint => Stations.Length > 0 ? Stations[0] : Vector2.zero;
    public virtual string GuideFloor => Floor;
    /// <summary>같은 방에 왔을 때 안내 표시에 쓸 이름.</summary>
    public virtual string GuideLabel => Title;
    public virtual string GuideRoom => Room;

    // ---------------------------------------------------------------- 조작창
    public void Open()
    {
        if (IsOpen || MuseumStory.Busy) return;
        MuseumGame.CloseAllPanels();
        IsOpen = true; openFrame = Time.frameCount;
        panel.gameObject.SetActive(true);
        MuseumChat.PuzzleActive = true;
        Sfx.Play("book_open", 0.55f);
        titleText.text = TitleLine(Title);
        OnOpened();
        Relayout();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        MuseumGame.LastCloseFrame = Time.frameCount;
        panel.gameObject.SetActive(false);
        MuseumChat.PuzzleActive = false;
        Sfx.Play("book_close", 0.5f);
        OnClosed();
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosed() { }

    private void BuildPanel()
    {
        var cv = MuseumGame.PanelCanvas;
        panel = MuseumUI.Node(GetType().Name + "Panel", cv.transform);
        MuseumUI.Anchor(panel, 0f, 0.5f, 24f, 30f, PanelW, PanelH + 12f);   // +12: 아래 키 안내를 크게(2배) 쓰는 만큼
        MuseumUI.Panel(panel);
        titleText = MuseumUI.Text(panel, "Title", Title, 3, MuseumUI.Amber, TextAnchor.MiddleLeft);
        MuseumUI.Anchor(titleText.rectTransform, 0f, 1f, 36f, -26f, PanelW - 260f, 52f);
        titleText.maxWidth = PanelW - 260f;
        subText = MuseumUI.Text(panel, "Sub", "", 2, MuseumUI.Slate, TextAnchor.UpperLeft);
        MuseumUI.Anchor(subText.rectTransform, 0f, 1f, 36f, -90f, PanelW - 72f, 72f);
        closeButton = MuseumUI.Button(panel, "Close", "닫기  X", 2, Close);
        MuseumUI.Anchor(closeButton.Rect, 1f, 1f, -24f, -24f, 196f, 56f);
        closeButton.Sound = "ui_back";

        body = MuseumUI.Node("Body", panel);
        body.anchorMin = new Vector2(0f, 0f); body.anchorMax = new Vector2(1f, 1f); body.pivot = new Vector2(0.5f, 0.5f);
        body.offsetMin = new Vector2(36f, ShowTeam ? 120f : 44f); body.offsetMax = new Vector2(-36f, -172f);

        if (ShowTeam)
        {
            for (int i = 0; i < 4; i++)
            {
                var c = MuseumUI.Node("Team" + i, panel);
                float w = (PanelW - 72f - 3 * 12f) / 4f;
                MuseumUI.Anchor(c, 0f, 0f, 36f + i * (w + 12f), 40f, w, 64f);
                chipBacks[i] = MuseumUI.Sliced(c, MuseumUI.FillSprite, new Color(0.1f, 0.12f, 0.16f, 1f));
                chips[i] = MuseumUI.Text(c, "Label", "", 2, MuseumUI.Ink, TextAnchor.MiddleCenter);
                MuseumUI.Stretch(chips[i].rectTransform, 4);
                chips[i].maxWidth = w - 8f;
            }
        }
        keysText = MuseumUI.Text(panel, "Keys", KeysLine, 2, MuseumUI.Slate, TextAnchor.MiddleCenter);
        MuseumUI.Anchor(keysText.rectTransform, 0.5f, 0f, 0f, 6f, PanelW - 72f, 30f);
        keysText.maxWidth = PanelW - 72f;
        BuildBody(body);
        panel.gameObject.SetActive(false);
    }

    /// <summary>채팅창과 겹치지 않게 폭을 줄인다(좁은 화면).</summary>
    private void Relayout()
    {
        float maxW = PanelW;
        if (MuseumChat.ChatRect(out Rect chat))
        {
            var cv = MuseumGame.PanelCanvas;
            float sf = cv.scaleFactor > 0.01f ? cv.scaleFactor : 1f;
            float chatLeft = chat.xMin / sf;
            maxW = Mathf.Min(PanelW, chatLeft - 48f);
        }
        float s = Mathf.Clamp(maxW / PanelW, 0.6f, 1f);
        panel.localScale = new Vector3(s, s, 1f);
    }

    protected abstract void BuildBody(RectTransform body);
    protected abstract void RefreshPanel();
    protected virtual void BuildWorld() { }
    protected virtual void UpdateWorld() { }

    /// <summary>팀 상태 칩(이름 + 상태). 기본은 이름만.</summary>
    protected virtual string TeamState(int slot, out Color color) { color = MuseumUI.Slate; return ""; }

    protected virtual void Update()
    {
        UpdateWorld();
        if (Active && MuseumNet.Authority) BotThink();
        if (!IsOpen) return;
        if (MuseumGame.Stage != Stage && !Solved) { Close(); return; }
        var kb = Keyboard.current;
        // 수첩 같은 창이 퍼즐 위에 떠 있으면 그 창이 키를 먼저 쓴다(같은 Esc·X 로 퍼즐까지 닫히지 않게)
        bool modalOver = MuseumHud.ModalOpen || Time.frameCount <= MuseumGame.LastCloseFrame + 1;
        if (kb != null && Time.frameCount > openFrame && !MuseumChat.TypingNow && !modalOver)
        {
            // 닫기: Esc · X(취소) · E/Z(열 때와 같은 키) · Tab
            if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) { Close(); return; }
            OnKeys(kb);
            if (!IsOpen) return;
        }
        MuseumUI.Tick();
        // 창 밖(채팅·신호 카드 제외)을 누르면 닫는다
        // (마우스·터치 공통) · 터치 '닫기' 단추도 닫는다
        if (Time.frameCount > openFrame && !modalOver)
        {
            if (TouchControls.ClosePressed || MuseumUI.PressedOutside(panel, p => MuseumChat.CoversScreen(p))) { Close(); return; }
        }
        Relayout();
        if (ShowTeam)
        {
            for (int i = 0; i < 4; i++)
            {
                string st = TeamState(i, out Color c);
                string nm = MuseumGame.NameOf(i) + (i == Me ? "(나)" : "");
                chips[i].text = string.IsNullOrEmpty(st) ? nm : nm + " " + st;
                chips[i].color = i == Me ? MuseumUI.Amber : MuseumUI.Ink;
                chipBacks[i].color = Color.Lerp(new Color(0.1f, 0.12f, 0.16f, 1f), c, 0.35f);
            }
        }
        RefreshPanel();
    }

    // ---------------------------------------------------------------- 네트워크 · 채팅 · AI
    protected abstract void OnNet(int op, string value, int actor);

    /// <summary>온라인 동기화용 전체 상태(방장이 1초마다 보낸다). null 이면 보내지 않는다.</summary>
    public virtual int[] SnapState() => null;
    /// <summary>방장이 보낸 전체 상태로 덮어쓴다(방장이 아닌 화면에서만 불린다).</summary>
    public virtual void LoadState(int[] s) { }
    protected static void CopyIn(int[] s, int at, int[] dst) { for (int i = 0; i < dst.Length && at + i < s.Length; i++) dst[i] = s[at + i]; }
    protected static void CopyIn(int[] s, int at, bool[] dst) { for (int i = 0; i < dst.Length && at + i < s.Length; i++) dst[i] = s[at + i] != 0; }
    protected static void Put(System.Collections.Generic.List<int> l, int[] a) { foreach (int x in a) l.Add(x); }
    protected static void Put(System.Collections.Generic.List<int> l, bool[] a) { foreach (bool x in a) l.Add(x ? 1 : 0); }
    protected virtual void OnChat(int slot, string text) { }
    protected virtual void BotThink() { }
    /// <summary>조작창이 열려 있을 때의 키보드 조작(채팅 입력 중이 아닐 때).</summary>
    protected virtual void OnKeys(Keyboard kb) { }

    /// <summary>숫자 키 1~n 중 이번 프레임에 눌린 것(0부터). 없으면 -1.</summary>
    protected static int DigitPressed(Keyboard kb, int n)
    {
        for (int i = 0; i < n && i < 9; i++)
            if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame || kb[(Key)((int)Key.Numpad1 + i)].wasPressedThisFrame) return i;
        return -1;
    }

    protected void Send(int op, string value = "", int actor = -1) { MuseumNet.Puzzle(Id, op, value, actor); }

    /// <summary>AI 친구가 말한다(자기 나라 말 + 그림 신호). 권한 쪽에서만 부른다.</summary>
    protected static void BotSay(int slot, string text) { if (MuseumNet.Authority && Bot(slot)) MuseumNet.BotSay(slot, text); }

    /// <summary>퍼즐을 풀었다(권한 쪽에서 한 번만 미션 완료를 알린다).</summary>
    protected void Finish()
    {
        if (Solved) return;
        Solved = true;
        Sfx.Play("clear", 0.8f);
        if (MuseumNet.Authority) MuseumNet.Mission(Stage);
    }

    protected static string Name(int slot) => MuseumGame.NameOf(slot);

    // ---------------------------------------------------------------- 그리기 도우미
    protected static Texture2D MakeTex(int w, int h, System.Action<Color32[], int, int> draw)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        draw(px, w, h);
        t.SetPixels32(px); t.Apply();
        return t;
    }

    protected static Sprite MakeSprite(Texture2D t, float ppu = 100f) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu);

    protected static void Put(Color32[] px, int w, int h, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }

    /// <summary>지도 픽셀 → 월드 좌표.</summary>
    protected static Vector3 World(Vector2 map, float lift = 0f) => new Vector3(map.x / 100f, -map.y / 100f + lift, 0f);
}
