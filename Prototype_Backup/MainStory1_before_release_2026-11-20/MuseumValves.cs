using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// B1 관리 통로 "급수 밸브" 협동 퍼즐.
///  · 밸브 4개(번호 1~4)가 월드에 놓여 있다. 담당은 없다 — E 로 "잡은" 사람이 그 순간 그 밸브를 돌린다.
///    한 번에 밸브 하나만 잡을 수 있고(다른 밸브를 잡으면 앞의 것은 저절로 놓는다), 이미 잡힌 밸브는 다른 사람이 못 잡는다.
///    밸브는 놓아도 돌려 둔 자리에 그대로 남는다.
///  · 정답은 밸브마다 판마다 무작위. 각 사람은 "밸브 하나의 정답"만 볼 수 있다(누가 어느 밸브를 아는지도 판마다 무작위).
///    단, 내가 잡고 있는 밸브의 정답은 가려진다 → 정답을 아는 사람과 돌리는 사람이 달라야 해서 서로 말해 줘야 한다.
///  · 정답은 채팅(숫자+화살표, 예: 3 ←2)으로 전한다. AI 동료도 똑같이 채팅으로 정답을 받아야 자기 밸브를 돌린다.
///  · 서로 다른 네 사람이 네 밸브를 "동시에" 잡고 전부 정답 자리에 있으면 해결(한 사람이 돌아다니며 혼자 풀 수 없다). 관리 통로의 세 문이 열린다.
///    밸브 하나하나가 맞았는지는 알려 주지 않고, 최종 확인에서 "맞다/아니다"만 알려 준다.
/// 정답·누가 무엇을 아는지는 같은 시드(Story1Net.Seed)로 계산하고, 잡기/놓기/돌리기/고정만 동기화한다.
/// </summary>
public sealed class MuseumValves : MonoBehaviour
{
    public const int Count = 4, MaxTurn = 4;
    private const int PuzzleId = 7;
    private static readonly Vector2[] ValvePos = { new Vector2(1192, 868), new Vector2(1280, 868), new Vector2(1370, 868), new Vector2(1548, 868) };   // 지도 픽셀(번호 1~4)

    public static bool IsOpen { get; private set; }
    public static bool Solved { get; private set; }

    private static bool inRoom;
    private static float engagedAt = -1f;       // 처음 밸브를 잡은 시각(그 전에는 AI 도 가만히 있는다)
    private static int openFrame = -1;
    private static int pendingOpen = -1;
    private static float pendingUntil;

    // 밸브 기준 배열
    private static readonly int[] holder = new int[Count];     // 밸브 → 잡은 슬롯(-1 = 비어 있음)
    private static readonly int[] ans = new int[Count];
    private static readonly int[] val = new int[Count];
    private static float allRightSince = -1f;
    private const float ConfirmWindow = 3f;                      // 네 사람이 이 시간 안에 모두 "최종 확인"을 누르면 판정
    private static readonly float[] confirmAt = { -99f, -99f, -99f, -99f };   // 밸브별 마지막 최종 확인 시각
    private static bool AllHeld() { for (int v = 0; v < Count; v++) if (holder[v] < 0) return false; return true; }
    private static bool Confirmed(int v) => Time.time - confirmAt[v] <= ConfirmWindow;
    private Vector2 scroll;
    private static readonly bool[] hasTold = new bool[Count];  // 채팅으로 정답을 전해 들었는가(AI 가 잡은 밸브용)
    private static readonly int[] toldVal = new int[Count];
    private static readonly float[] botAtV = new float[Count];
    // 슬롯 기준 배열
    private static readonly int[] knows = new int[Count];      // 이 사람이 정답을 볼 수 있는 밸브
    private static readonly float[] grabAt = new float[Count];
    private static readonly float[] hintAt = new float[Count];
    private static readonly bool[] hinted = new bool[Count];
    private static readonly int[][] pref = new int[Count][];
    private static bool ready;

    private GUIStyle title, body, big, small, btn, tag;
    private GameObject worldRoot;
    private Transform[] wheels;
    private SpriteRenderer[] lamps;
    private Camera cam;

    // ---------------------------------------------------------------- 상태 도우미
    private static int HeldBy(int slot) { for (int v = 0; v < Count; v++) if (holder[v] == slot) return v; return -1; }
    private static bool Correct(int v) => val[v] == ans[v];
    private static string Dir(int v) => v == 0 ? "0" : (v < 0 ? "← " : "→ ") + Mathf.Abs(v);
    private static string ClueOf(int v) => (ans[v] < 0 ? "←" : "→") + Mathf.Abs(ans[v]);
    private static string NameOf(int slot) => Roles.Valid(slot) ? Roles.DefaultName(slot) : "";

    /// <summary>내가 정답을 볼 수 있는 밸브(없으면 -1). 내가 잡고 있는 밸브의 정답은 가려진다.</summary>
    private static int VisibleClue(int slot)
    {
        Ensure();
        int v = knows[slot];
        return holder[v] == slot ? -1 : v;
    }

    // ---------------------------------------------------------------- AI 가 밸브까지 걸어가 서 있는다
    private static readonly bool[] reached = new bool[Count];

    /// <summary>AI(slot)가 잡은 밸브 앞 자리(지도 픽셀). 방에 있고 잡은 밸브가 있을 때만 true.</summary>
    public static bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!inRoom || !Story1Role.IsBot(slot)) return false;
        int v = HeldBy(slot);
        if (v < 0) return false;
        stand = ValvePos[v] + new Vector2(0, 8);
        return true;
    }

    /// <summary>AI 가 자기 밸브 앞에 도착했는가(StrangerMuseum 이 알려 준다). 도착해야 정답을 말하고 밸브를 돌린다.</summary>
    public static void SetReached(int slot, bool on) { if (slot >= 0 && slot < Count) reached[slot] = on; }

    // ---------------------------------------------------------------- 외부에서 쓰는 것
    public static void SetLocation(string floorId, string roomId)
    {
        inRoom = floorId == "B1" && roomId == "maintenance";
        if (!inRoom) IsOpen = false;
    }

    /// <summary>플레이어 근처의 밸브 번호(0~3). 없으면 -1.</summary>
    public static int NearValve(Vector2 pos, float radius)
    {
        if (!inRoom) return -1;
        int best = -1; float bd = radius * radius;
        for (int i = 0; i < Count; i++)
        {
            float d = (ValvePos[i] - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    public static string PromptFor(int v)
    {
        Ensure();
        int me = Story1Role.Current;
        if (Solved) return (v + 1) + "번 밸브 (해결됨)";
        if (holder[v] == me) return (v + 1) + "번 밸브 조작 (잡고 있어요)";
        if (holder[v] >= 0) return (v + 1) + "번 밸브 (" + NameOf(holder[v]) + " 님이 잡고 있어요)";
        if (HeldBy(me) >= 0) return (v + 1) + "번 밸브로 바꿔 잡기";
        return (v + 1) + "번 밸브 잡기";
    }

    /// <summary>E 를 눌렀을 때. 근처 밸브가 있으면 처리하고 true.</summary>
    public static bool TryInteract(Vector2 pos, float radius)
    {
        int v = NearValve(pos, radius);
        if (v < 0) return false;
        Ensure();
        int me = Story1Role.Current;
        if (Solved) { Notice("이미 해결했어요. 문이 열려 있어요.", true, 2f); return true; }
        if (holder[v] == me) { Open(); return true; }
        if (holder[v] >= 0) { Sfx.Play("ui_back", 0.7f); Notice((v + 1) + "번 밸브는 " + NameOf(holder[v]) + " 님이 잡고 있어요.", false, 2.4f); return true; }
        int mine = HeldBy(me);
        if (mine >= 0)   // 다른 밸브를 잡고 있었다면 그것은 놓고(돌려 둔 자리는 그대로) 이쪽을 잡는다
        {
            holder[mine] = -1;
            Story1Net.Send(Story1Net.ValveSet, mine, "R", 0f);
        }
        pendingOpen = v; pendingUntil = Time.time + 2f;
        Story1Net.Send(Story1Net.ValveSet, v, "G", 0f);   // 잡기는 서버를 한 바퀴 돌아온 순서대로 정해진다(동시에 잡으면 먼저 도착한 쪽)
        return true;
    }

    public static void Open()
    {
        if (!inRoom || IsOpen) return;
        Ensure();
        IsOpen = true; openFrame = Time.frameCount;
        Sfx.Play("book_open", 0.6f);
    }

    private static void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Sfx.Play("book_close", 0.6f);
    }

    // ---------------------------------------------------------------- 초기화
    private void Awake()
    {
        ready = false; Solved = false; IsOpen = false; inRoom = false; engagedAt = -1f; pendingOpen = -1; allRightSince = -1f;
        for (int i = 0; i < Count; i++)
        {
            holder[i] = -1; val[i] = 0; hasTold[i] = false; reached[i] = false; confirmAt[i] = -99f; mentioned[i] = false; botAtV[i] = -1f;
            grabAt[i] = -1f; hintAt[i] = -1f; hinted[i] = false;
        }
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        Story1Net.Received -= OnNet; Story1Net.Received += OnNet;
        Story1Net.ChatLine -= OnChat; Story1Net.ChatLine += OnChat;
        BuildWorld();
    }

    private void OnDestroy() { Story1Net.Received -= OnNet; Story1Net.ChatLine -= OnChat; IsOpen = false; }

    private static void Shuffle(System.Random rng, int[] a)
    {
        for (int i = a.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = a[i]; a[i] = a[j]; a[j] = t; }
    }

    private static void Ensure()
    {
        if (ready) return;
        var rng = new System.Random(Story1Net.Seed(PuzzleId));
        int[] k = { 0, 1, 2, 3 };
        Shuffle(rng, k);
        for (int s = 0; s < Count; s++) knows[s] = k[s];   // 사람마다 서로 다른 밸브의 정답을 안다
        int max = Story1Net.Difficulty == Story1Rules.Easy ? 2 : Story1Net.Difficulty == Story1Rules.Normal ? 3 : MaxTurn;
        for (int v = 0; v < Count; v++)
        {
            int n = rng.Next(1, max + 1);
            ans[v] = rng.Next(2) == 0 ? -n : n;
        }
        for (int s = 0; s < Count; s++) { pref[s] = new[] { 0, 1, 2, 3 }; Shuffle(rng, pref[s]); }
        ready = true;
    }

    // ---------------------------------------------------------------- 월드의 밸브(스프라이트)
    private static Texture2D MakeTex(int size, System.Action<Color32[], int> draw)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        draw(px, size);
        t.SetPixels32(px); t.Apply();
        return t;
    }

    private static void Put(Color32[] px, int n, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < n && y < n) px[y * n + x] = c; }

    private void BuildWorld()
    {
        worldRoot = new GameObject("MuseumValves");
        worldRoot.transform.SetParent(transform.parent, false);
        Color32 dark = new Color32(60, 28, 22, 255), red = new Color32(214, 84, 56, 255), light = new Color32(255, 160, 120, 255), steel = new Color32(120, 134, 150, 255);
        Texture2D wheelTex = MakeTex(32, (px, n) =>
        {
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = x - 15.5f, dy = y - 15.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                bool ring = d > 11f && d < 15f, hub = d < 3.5f, spoke = (Mathf.Abs(dx) < 1.3f || Mathf.Abs(dy) < 1.3f) && d < 15f;
                if (d > 11f && d < 15.9f && (d < 11.9f || d > 14.9f)) Put(px, n, x, y, dark);
                else if (ring) Put(px, n, x, y, (dx + dy) < 0 ? light : red);
                else if (hub) Put(px, n, x, y, light);
                else if (spoke) Put(px, n, x, y, red);
            }
        });
        Texture2D baseTex = MakeTex(32, (px, n) =>
        {
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                if (x >= 12 && x <= 19 && y >= 4 && y < 28) Put(px, n, x, y, steel);
                if (x >= 8 && x <= 23 && (y == 4 || y == 5 || y == 26 || y == 27)) Put(px, n, x, y, dark);
            }
        });
        Texture2D lampTex = MakeTex(8, (px, n) => { for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) Put(px, n, x, y, (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? dark : new Color32(255, 255, 255, 255)); });
        Sprite wheelSp = Sprite.Create(wheelTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
        Sprite baseSp = Sprite.Create(baseTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
        Sprite lampSp = Sprite.Create(lampTex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f);

        wheels = new Transform[Count]; lamps = new SpriteRenderer[Count];
        for (int i = 0; i < Count; i++)
        {
            Vector3 p = new Vector3(ValvePos[i].x / 100f, -(ValvePos[i].y - 46f) / 100f, 0);
            int order = Mathf.RoundToInt(ValvePos[i].y) - 12;   // 사람이 앞에 서면 사람이 위에 그려진다
            var b = new GameObject("Base" + (i + 1)).AddComponent<SpriteRenderer>(); b.sprite = baseSp; b.sortingOrder = order - 1;
            b.transform.SetParent(worldRoot.transform, false); b.transform.position = p; b.transform.localScale = Vector3.one * 0.8f;
            var w = new GameObject("Wheel" + (i + 1)).AddComponent<SpriteRenderer>(); w.sprite = wheelSp; w.sortingOrder = order;
            w.transform.SetParent(worldRoot.transform, false); w.transform.position = p; w.transform.localScale = Vector3.one * 0.8f;
            wheels[i] = w.transform;
            var l = new GameObject("Lamp" + (i + 1)).AddComponent<SpriteRenderer>(); l.sprite = lampSp; l.sortingOrder = order + 1;
            l.transform.SetParent(worldRoot.transform, false); l.transform.position = p + new Vector3(0, 0.26f, 0); l.transform.localScale = Vector3.one * 1.1f;
            lamps[i] = l;
        }
        worldRoot.SetActive(false);
    }

    private void UpdateWorld()
    {
        if (worldRoot == null) return;
        if (worldRoot.activeSelf != inRoom) worldRoot.SetActive(inRoom);
        if (!inRoom) return;
        for (int i = 0; i < Count; i++)
        {
            wheels[i].rotation = Quaternion.Euler(0, 0, -val[i] * 22f);   // 돌린 만큼 핸들이 돈다
            lamps[i].color = Solved || Confirmed(i) ? new Color(0.37f, 0.88f, 0.48f) : holder[i] >= 0 ? new Color(1f, 0.8f, 0.35f) : new Color(0.45f, 0.45f, 0.45f);
        }
    }

    private void DrawValveTags()
    {
        if (!inRoom || cam == null) return;
        int me = Story1Role.Current;
        for (int i = 0; i < Count; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(ValvePos[i].x / 100f, -(ValvePos[i].y - 46f) / 100f + 0.46f, 0));
            Rect r = new Rect(sp.x - 70, Screen.height - sp.y - 14, 140, 28);
            if (MuseumChat.CoversGuiRect(r)) continue;
            bool mine = holder[i] == me;
            tag.normal.textColor = mine ? new Color(0.55f, 1f, 0.7f) : holder[i] >= 0 ? new Color(1f, 0.8f, 0.5f) : new Color(1f, 0.93f, 0.75f);
            GUI.Label(r, holder[i] >= 0 ? (i + 1) + " · " + NameOf(holder[i]) : (i + 1).ToString(), tag);
        }
    }

    // ---------------------------------------------------------------- 네트워크 / 채팅
    /// <summary>밸브 이벤트: b = 밸브 번호, s = G 잡기 / R 놓기 / T 돌리기 / C 최종 확인, f = 값. 잡고 있는 사람만 돌릴 수 있다.</summary>
    private static void OnNet(GameEvent e)
    {
        if (e == null || e.a != Story1Net.ValveSet) return;
        int v = e.b, slot = e.slot;
        if (v < 0 || v >= Count || !Roles.Valid(slot)) return;
        string op = e.s ?? "";
        if (op == "G")
        {
            if (holder[v] < 0 && HeldBy(slot) < 0) { holder[v] = slot; if (engagedAt < 0f) engagedAt = Time.time; }
            return;
        }
        if (holder[v] != slot) return;              // 잡은 사람만 조작할 수 있다
        if (Story1Net.IsMine(e)) return;            // 내 조작은 이미 화면에 반영했다
        if (op == "R") holder[v] = -1;
        else if (op == "T") val[v] = Mathf.Clamp(Mathf.RoundToInt(e.f), -MaxTurn, MaxTurn);
        else if (op == "C") confirmAt[v] = Time.time;
    }

    /// <summary>채팅에서 "번호 방향 숫자"(예: 3 ←2)를 읽어, 그 밸브를 AI 가 잡고 있으면 정답을 전해 들은 것으로 본다. AI 는 말뜻 그대로만 믿는다(틀리게 말하면 틀리게 돌린다).</summary>
    private static void OnChat(int slot, string text)
    {
        if (!ready || Solved || string.IsNullOrEmpty(text)) return;
        if (!Story1Role.IsBot(slot)) foreach (char ch in text) if (ch >= '1' && ch <= '4') mentioned[ch - '1'] = true;   // 사람이 번호를 말했다 → 그 밸브를 아는 AI 가 답한다
        text = text.Replace("<-", "←").Replace("->", "→").Replace("<", "←").Replace(">", "→");   // 키보드로 친 화살표도 인정
        int num = -1, dir = 0, n = -1;
        foreach (char c in text)
        {
            if (c >= '1' && c <= '4')
            {
                if (num < 0) num = c - '1';
                else if (dir != 0 && n < 0) n = c - '0';
            }
            else if (c == '←' || c == '→') { if (num >= 0 && dir == 0) dir = c == '←' ? -1 : 1; }
        }
        if (num < 0 || dir == 0 || n < 0) return;
        Tell(num, Mathf.Clamp(dir * n, -MaxTurn, MaxTurn));
    }

    private static void Tell(int v, int value)
    {
        hasTold[v] = true; toldVal[v] = value; botAtV[v] = Time.time + 1.8f;   // 듣고 조금 있다가 돌린다
    }

    // ---------------------------------------------------------------- 내 조작
    private static void TurnMine(int d)
    {
        int v = HeldBy(Story1Role.Current);
        if (Solved || v < 0) return;
        int nv = Mathf.Clamp(val[v] + d, -MaxTurn, MaxTurn);
        if (nv == val[v]) return;
        val[v] = nv;
        Sfx.Play("ui_select", 0.7f);
        Story1Net.Send(Story1Net.ValveSet, v, "T", nv);
    }

    private static void ConfirmMine()
    {
        int v = HeldBy(Story1Role.Current);
        if (Solved || v < 0) return;
        if (!AllHeld()) { Sfx.Play("ui_back", 0.6f); Notice("네 사람이 모두 밸브를 잡아야 최종 확인을 할 수 있어요.", false, 2.4f); return; }
        confirmAt[v] = Time.time;
        Sfx.Play("ui_next", 0.8f);
        Story1Net.Send(Story1Net.ValveSet, v, "C", 0f);
    }

    private static void ReleaseMine()
    {
        int v = HeldBy(Story1Role.Current);
        if (Solved || v < 0) return;
        holder[v] = -1;
        IsOpen = false;
        Sfx.Play("book_close", 0.6f);
        Story1Net.Send(Story1Net.ValveSet, v, "R", 0f);
    }

    private void Update()
    {
        UpdateWorld();
        if (!inRoom) return;
        Ensure();
        int me = Story1Role.Current;

        if (pendingOpen >= 0)
        {
            if (holder[pendingOpen] == me) { pendingOpen = -1; Sfx.Play("ui_select", 0.7f); Open(); }
            else if (Time.time > pendingUntil || holder[pendingOpen] >= 0) { pendingOpen = -1; }
        }
        if (IsOpen && HeldBy(me) < 0) IsOpen = false;   // 역할이 바뀌었거나(Tab) 놓은 경우

        BotWork();

        if (!Solved)
        {
            bool all = false, allConf = true, allRight = true;
            for (int i = 0; i < Count; i++) { if (holder[i] < 0 || !Confirmed(i)) allConf = false; if (!Correct(i)) allRight = false; }
            if (allConf)   // 네 사람이 함께 최종 확인을 눌렀다
            {
                for (int i = 0; i < Count; i++) confirmAt[i] = -99f;
                if (allRight) all = true;
                else { Sfx.Play("ui_back", 0.9f); Notice("밸브 값이 맞지 않아요" + (char)10 + "채팅으로 서로 정답을 다시 확인해 보세요", false, 3.5f); }
            }
            if (all)
            {
                Solved = true; IsOpen = false;
                for (int i = 0; i < Count; i++) { holder[i] = -1; reached[i] = false; }
                Sfx.Play("chime", 0.9f); Sfx.Play("door_open", 0.5f);
                Notice("밸브 해결!" + (char)10 + "방재실 · 응급 수장고 문이 열렸어요", true, 5f);
            }
        }

        Keyboard kb = Keyboard.current;
        if (!IsOpen || kb == null || MuseumChat.Typing) return;
        if (kb.escapeKey.wasPressedThisFrame || (kb.eKey.wasPressedThisFrame && Time.frameCount != openFrame)) { Close(); return; }
        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) ConfirmMine();
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) TurnMine(-1);
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) TurnMine(1);
    }

    /// <summary>
    /// AI: (1) 사람이 밸브를 잡은 뒤(engagedAt)부터 남은 밸브를 하나씩 잡는다(자기가 정답을 아는 밸브는 피한다).
    ///     (2) 자기가 아는 정답을, 그 밸브를 잡은 사람에게 채팅으로 알려 준다.
    ///     (3) 자기가 잡은 밸브는 "전해 들은 값"대로만 돌린다.
    /// </summary>
    private void BotWork()
    {
        if (Solved || engagedAt < 0f) return;
        int me = Story1Role.Current;
        for (int s = 0; s < Count; s++)
        {
            if (!Story1Role.IsBot(s)) continue;

            // (1) 밸브 잡기(한 번 잡으면 계속 잡고 있는다)
            int mine = HeldBy(s);
            if (mine < 0)
            {
                if (grabAt[s] < 0f) grabAt[s] = Time.time + 0.4f + s * 0.4f;
                if (Time.time >= grabAt[s])
                {
                    int pick = -1;
                    for (int j = 0; j < Count && pick < 0; j++) { int v = pref[s][j]; if (holder[v] < 0 && v != knows[s]) pick = v; }
                    for (int v = 0; v < Count && pick < 0; v++) if (holder[v] < 0) pick = v;
                    if (pick >= 0) { holder[pick] = s; Sfx.Play("ui_select", 0.35f); mine = pick; }
                    grabAt[s] = Time.time + 1f;
                }
            }

            if (mine < 0 || !reached[s]) continue;   // 밸브를 잡고 그 앞에 도착하기 전에는 말도 돌리기도 하지 않는다
            if (mine >= 0 && AllHeld()) confirmAt[mine] = Time.time;   // AI 는 확인 버튼을 계속 누르고 있는다(사람이 누르는 순간 판정)

            // (2) 내가 아는 밸브의 정답 알려 주기
            int kv = knows[s];
            if (holder[kv] >= 0 && holder[kv] != s)
            {
                if (Story1Role.IsBot(holder[kv]))
                {
                    if (!hinted[s])
                    {
                        if (hintAt[s] < 0f) hintAt[s] = Time.time + 1.5f + 0.4f * s;
                        if (Time.time >= hintAt[s])
                        {
                            hinted[s] = true;
                            MuseumChat.Say(s, (kv + 1) + " " + ClueOf(kv));
                            Tell(kv, ans[kv]);
                        }
                    }
                }
                else if (holder[kv] == me)
                {
                    if (hintAt[s] < 0f) hintAt[s] = Time.time + 2f + 0.6f * s;
                    if (Time.time >= hintAt[s]) { hintAt[s] = Time.time + 24f; MuseumChat.Say(s, (kv + 1) + " " + ClueOf(kv)); }
                }
            }
            else hintAt[s] = -1f;

            // (3) 내 밸브 돌리기
            if (mine < 0 || !hasTold[mine] || val[mine] == toldVal[mine]) continue;
            if (botAtV[mine] < 0f || Time.time < botAtV[mine]) continue;
            if (val[mine] != toldVal[mine]) { val[mine] += val[mine] < toldVal[mine] ? 1 : -1; botAtV[mine] = Time.time + 0.7f; }
            else botAtV[mine] = -1f;
        }
    }

    // ---------------------------------------------------------------- 화면
    /// <summary>조사 팝업 등이 떠 있으면 true — 이때는 접힌 카드를 숨긴다.</summary>
    public static bool PopupOpen;
    private static readonly bool[] mentioned = new bool[Count];   // 사람이 채팅에서 이 밸브 번호를 말했는가
    private static float Sc => Mathf.Clamp(Screen.height / 720f, 0.9f, 1.5f);

    private void Styles()
    {
        if (title == null)
        {
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 16);
            body = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, richText = true, normal = { textColor = new Color32(255, 245, 229, 255) } };
            title = new GUIStyle(body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color32(231, 186, 99, 255) } };
            big = new GUIStyle(body) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            small = new GUIStyle(body) { normal = { textColor = new Color32(210, 200, 180, 255) } };
            btn = new GUIStyle(GUI.skin.button) { font = font };
            tag = new GUIStyle(body) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }
        float k = Sc;
        body.fontSize = Mathf.RoundToInt(19 * k); title.fontSize = Mathf.RoundToInt(21 * k);
        big.fontSize = Mathf.RoundToInt(44 * k); small.fontSize = Mathf.RoundToInt(16 * k);
        btn.fontSize = Mathf.RoundToInt(21 * k); tag.fontSize = Mathf.RoundToInt(18 * k);
    }

    private string Lamps()
    {
        var sb = new System.Text.StringBuilder();
        for (int n = 0; n < Count; n++)
        {
            string c = Solved || Confirmed(n) ? "#5fe07a" : holder[n] >= 0 ? "#ffcc59" : "#7a7a7a";
            sb.Append("<color=" + c + ">●</color> " + (n + 1) + "    ");
        }
        return sb.ToString();
    }

    private void OnGUI()
    {
        Styles();
        GUI.depth = -10;
        DrawNotice();
        if (!inRoom) return;
        Ensure();
        DrawValveTags();
        int me = Story1Role.Current;
        if (me < 0 || me >= Count) return;
        int held = HeldBy(me), clue = VisibleClue(me);
        string clueLine = clue >= 0
            ? $"내가 아는 정답:  {clue + 1}번 밸브 <b>{ClueOf(clue)}</b>"
            : $"내가 아는 {knows[me] + 1}번 정답은 내가 잡고 있어서 가려져요";

        if (!IsOpen) return;   // 밸브를 열지 않았을 때는 화면에 아무것도 올리지 않는다(램프·이름표는 월드의 밸브 위에 있다)

        // 채팅창과 겹치지 않는 영역(채팅 왼쪽, 좁으면 채팅 위)의 아래쪽에 가로로 긴 띠로 붙인다 — OnGUI 는 채팅(uGUI)보다 항상 위에 그려지기 때문
        float k = Sc;
        Rect area = new Rect(12, 12, Screen.width - 24, Screen.height - 24);
        if (MuseumChat.ChatRect(out Rect cr))
        {
            if (cr.xMin - 24 >= 560 * k) area.xMax = cr.xMin - 12;
            else area.yMax = Mathf.Max(area.yMin + 260 * k, cr.yMin - 8);
        }
        float w = Mathf.Min(900 * k, area.width), h = Mathf.Min(176 * k, area.height);
        Rect box = new Rect(area.x + (area.width - w) / 2, area.yMax - h, w, h);
        if (MuseumChat.ChatRect(out Rect cr2) && Mathf.Abs(area.xMax - (cr2.xMin - 12)) < 1f)   // 옆에 나란히 놓일 때는 채팅창과 같은 높이
        {
            h = Mathf.Clamp(cr2.height, 150 * k, 340 * k);
            box = new Rect(area.x + (area.width - w) / 2, cr2.yMax - h, w, h);
        }
        GUI.Box(box, GUIContent.none); GUI.Box(box, GUIContent.none);
        float pad = 20 * k;
        GUILayout.BeginArea(new Rect(box.x + pad, box.y + 12 * k, w - pad * 2, h - 20 * k));
        if (clue >= 0)
            GUILayout.Label($"<color=#e7ba63><b>{held + 1}번 밸브</b></color>     내가 아는 정답   {clue + 1}번 밸브 <size={Mathf.RoundToInt(34 * k)}><b>{ClueOf(clue)}</b></size>", body);
        else
            GUILayout.Label($"<color=#e7ba63><b>{held + 1}번 밸브</b></color>     내가 아는 정답은 {knows[me] + 1}번 밸브예요 (지금 잡고 있어서 안 보여요)", body);
        if (clue >= 0)
            GUILayout.Label(holder[clue] >= 0 ? $"{clue + 1}번을 잡은 {NameOf(holder[clue])} 님에게 채팅으로 알려 주세요" : $"{clue + 1}번 밸브는 아직 아무도 안 잡았어요", small);
        GUILayout.FlexibleSpace();
        GUI.enabled = !Solved;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀  A", btn, GUILayout.Height(58 * k), GUILayout.Width(96 * k))) TurnMine(-1);
        GUILayout.Label(Dir(val[held]), big, GUILayout.Height(58 * k), GUILayout.Width(150 * k));
        if (GUILayout.Button("D  ▶", btn, GUILayout.Height(58 * k), GUILayout.Width(96 * k))) TurnMine(1);
        GUILayout.Space(18 * k);
        string confirmText = Confirmed(held) ? "확인함 (기다리는 중)" : AllHeld() ? "최종 확인 (Space)" : "네 명이 다 잡아야 확인";
        if (GUILayout.Button(confirmText, btn, GUILayout.Height(58 * k), GUILayout.ExpandWidth(true))) ConfirmMine();
        if (GUILayout.Button("놓기", btn, GUILayout.Height(58 * k), GUILayout.Width(100 * k))) { ReleaseMine(); GUI.enabled = true; GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        GUILayout.EndArea();
    }

    // ---------------------------------------------------------------- 알림 / 잠긴 문
    private static string noticeText = ""; private static float noticeUntil, noticeStart; private static bool noticeGood;

    public static void Notice(string text, bool good, float seconds = 3.2f)
    {
        noticeText = text; noticeGood = good; noticeStart = Time.time; noticeUntil = Time.time + seconds;
    }

    /// <summary>밸브를 풀기 전에는 B1 관리 통로의 세 방(방재실·전기실·응급 수장고)이 잠겨 있다.</summary>
    public static bool DoorLocked(string portalName)
    {
        if (Solved || !inRoom || string.IsNullOrEmpty(portalName)) return false;
        return portalName.Contains("방재실 문") || portalName.Contains("전기실 문") || portalName.Contains("응급 수장고 문");
    }

    public static void BumpLockedDoor()
    {
        Sfx.Play("ui_back", 0.8f);
        Notice("수압이 높아 문이 잠겨 있어요. 급수 밸브(관리 통로)를 네 명이 맞춰요.", false);
    }

    private void DrawNotice()
    {
        if (Time.time >= noticeUntil || string.IsNullOrEmpty(noticeText)) return;
        float a = Mathf.Clamp01((noticeUntil - Time.time) / 0.5f), pop = Mathf.Clamp01((Time.time - noticeStart) / 0.15f);
        GUIStyle st = new GUIStyle(big) { fontSize = Mathf.RoundToInt((noticeGood ? 26 : 18) * Sc), wordWrap = true, richText = true };
        st.normal.textColor = noticeGood ? new Color(0.5f, 1f, 0.65f, a) : new Color(1f, 0.85f, 0.6f, a);
        float w = Mathf.Min(Screen.width * 0.5f, (noticeGood ? 560 : 520) * Sc), h = (noticeGood ? 100 : 64) * Sc;
        float bottom = MuseumStory.StackTopGui > 1f ? MuseumStory.StackTopGui : Screen.height * 0.5f;   // 왼쪽 아래 목표 칸 바로 위(맵을 덜 가린다)
        Rect r = new Rect(12, bottom - h - (1 - pop) * 10, w, h);
        Color c = GUI.color; GUI.color = new Color(1, 1, 1, a); GUI.Box(r, GUIContent.none); GUI.Box(r, GUIContent.none); GUI.color = c;
        GUI.Label(r, noticeText, st);
    }
}
