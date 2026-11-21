using System.Collections.Generic;
using System.Text.RegularExpressions;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 방재실(가스 환기)·전기실(차단기 순서) 퍼즐의 공통 뼈대.
///  · 4개의 "자리"가 방 안에 놓여 있고, 자리 번호 = 역할 번호(0~3)다. 내 자리에서 E 를 누르면 조작판이 열린다.
///  · 정보는 항상 일부만 보인다 → 채팅으로 서로 알려 줘야 한다. AI 도 같은 규칙(채팅으로 듣고 말한다).
///  · AI 는 사람이 처음 조작을 시작한 뒤에야 움직이고 말한다(시작 전에 정답을 흘리지 않는다).
/// </summary>
public abstract class StationPuzzle : MonoBehaviour
{
    public const int Count = 4;
    public static readonly List<StationPuzzle> All = new List<StationPuzzle>();

    protected abstract string RoomId { get; }
    protected abstract Vector2[] Stands { get; }      // 지도 픽셀: 각 자리에 서는 위치(번호 1~4)
    protected abstract int NetKind { get; }
    protected abstract int SeedId { get; }
    protected abstract string Noun { get; }            // "환풍기" / "차단기"
    protected abstract float PanelHeight { get; }      // 720p 기준 조작판 높이
    protected abstract void Init(System.Random rng);   // 판마다 무작위 값
    protected abstract void BuildWorld();
    protected abstract void RefreshWorld();
    protected abstract string TagText(int i);
    protected abstract void Tick();                    // 규칙 판정 + AI
    protected abstract void Keys(Keyboard kb);
    protected abstract void DrawPanel(int me, float k, float width);
    protected abstract void ApplyRemote(int station, string op, float f);
    protected abstract void OnChatText(int slot, string text);

    public bool IsOpen { get; protected set; }
    public bool Solved { get; protected set; }

    protected bool inRoom, ready, engaged;
    protected float engagedAt = -1f;
    protected readonly bool[] reached = new bool[Count];
    protected Camera cam;
    protected GameObject worldRoot;
    protected GUIStyle title, body, big, small, btn, tag;
    private int openFrame = -1;

    protected static float Sc => Mathf.Clamp(Screen.height / 720f, 0.9f, 1.5f);
    protected static string NameOf(int slot) => Roles.Valid(slot) ? Roles.DefaultName(slot) : "";

    // ---------------------------------------------------------------- 수명
    protected virtual void Awake() { cam = GetComponent<Camera>(); }
    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    protected virtual void Start()
    {
        Story1Net.Received -= OnNetEvent; Story1Net.Received += OnNetEvent;
        Story1Net.ChatLine -= OnChatLine; Story1Net.ChatLine += OnChatLine;
        worldRoot = new GameObject(GetType().Name);
        worldRoot.transform.SetParent(transform.parent, false);
        BuildWorld();
        worldRoot.SetActive(false);
    }

    protected virtual void OnDestroy() { Story1Net.Received -= OnNetEvent; Story1Net.ChatLine -= OnChatLine; IsOpen = false; }

    protected void Ensure()
    {
        if (ready) return;
        Init(new System.Random(Story1Net.Seed(SeedId)));
        ready = true;
    }

    // ---------------------------------------------------------------- 외부에서 쓰는 것
    public void SetLoc(string floorId, string roomId)
    {
        inRoom = floorId == "B1" && roomId == RoomId;
        if (!inRoom) IsOpen = false;
    }

    public int Near(Vector2 pos, float radius)
    {
        if (!inRoom) return -1;
        int best = -1; float bd = radius * radius;
        for (int i = 0; i < Count; i++)
        {
            float d = (Stands[i] - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    public string Prompt(int i)
    {
        int me = Story1Role.Current;
        if (Solved) return (i + 1) + "번 " + Noun + " (해결됨)";
        if (i == me) return (i + 1) + "번 " + Noun + " 조작 (내 자리)";
        return (i + 1) + "번 " + Noun + " (" + NameOf(i) + " 님 자리)";
    }

    public bool TryInteract(Vector2 pos, float radius)
    {
        int i = Near(pos, radius);
        if (i < 0) return false;
        Ensure();
        int me = Story1Role.Current;
        if (Solved) { MuseumValves.Notice("이미 해결했어요.", true, 2f); return true; }
        if (i != me)
        {
            Sfx.Play("ui_back", 0.7f);
            MuseumValves.Notice((i + 1) + "번 " + Noun + "은(는) " + NameOf(i) + " 님 자리예요. 내 자리는 " + (me + 1) + "번이에요.", false, 2.8f);
            return true;
        }
        Open();
        return true;
    }

    public bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!inRoom || Solved || !engaged || !Story1Role.IsBot(slot) || slot < 0 || slot >= Count) return false;
        stand = Stands[slot];
        return true;
    }

    public void SetReached(int slot, bool on) { if (slot >= 0 && slot < Count) reached[slot] = on; }

    protected void Open()
    {
        if (!inRoom || IsOpen) return;
        Ensure();
        if (!engaged) { engaged = true; engagedAt = Time.time; }
        IsOpen = true; openFrame = Time.frameCount;
        Sfx.Play("book_open", 0.6f);
    }

    protected void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Sfx.Play("book_close", 0.6f);
    }

    // ---------------------------------------------------------------- 네트워크 / 채팅
    protected void Send(int station, string op, float f) { Story1Net.Send(NetKind, station, op, f); }

    private void OnNetEvent(GameEvent e)
    {
        if (e == null || e.a != NetKind || !ready) return;
        int b = e.b;
        if (b < 0 || b >= Count || !Roles.Valid(e.slot) || e.slot != b) return;   // 자리 주인만 조작한다
        if (Story1Net.IsMine(e)) return;                                           // 내 조작은 이미 반영했다
        if (!engaged) { engaged = true; engagedAt = Time.time; }
        ApplyRemote(b, e.s ?? "", e.f);
    }

    private void OnChatLine(int slot, string text)
    {
        if (!inRoom || !ready || Solved || string.IsNullOrEmpty(text)) return;
        OnChatText(slot, text);
    }

    // ---------------------------------------------------------------- 프레임
    private void Update()
    {
        if (worldRoot != null && worldRoot.activeSelf != inRoom) worldRoot.SetActive(inRoom);
        if (!inRoom) return;
        Ensure();
        RefreshWorld();
        if (IsOpen && Story1Role.Current >= Count) IsOpen = false;
        Tick();
        Keyboard kb = Keyboard.current;
        if (!IsOpen || kb == null || MuseumChat.Typing) return;
        if (kb.escapeKey.wasPressedThisFrame || (kb.eKey.wasPressedThisFrame && Time.frameCount != openFrame)) { Close(); return; }
        Keys(kb);
    }

    // ---------------------------------------------------------------- 화면
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
        big.fontSize = Mathf.RoundToInt(40 * k); small.fontSize = Mathf.RoundToInt(16 * k);
        btn.fontSize = Mathf.RoundToInt(21 * k); tag.fontSize = Mathf.RoundToInt(18 * k);
    }

    private void OnGUI()
    {
        Styles();
        GUI.depth = -10;
        if (!inRoom) return;
        Ensure();
        DrawTags();
        int me = Story1Role.Current;
        if (!IsOpen || me < 0 || me >= Count) return;

        float k = Sc;
        Rect area = new Rect(12, 12, Screen.width - 24, Screen.height - 24);
        bool side = false;
        if (MuseumChat.ChatRect(out Rect cr))
        {
            if (cr.xMin - 24 >= 560 * k) { area.xMax = cr.xMin - 12; side = true; }
            else area.yMax = Mathf.Max(area.yMin + 260 * k, cr.yMin - 8);
        }
        float w = Mathf.Min(900 * k, area.width), h = Mathf.Min(PanelHeight * k, area.height);
        var box = new Rect(area.x + (area.width - w) / 2, area.yMax - h, w, h);
        if (side && MuseumChat.ChatRect(out Rect c2)) { h = Mathf.Clamp(c2.height, PanelHeight * k, PanelHeight * k * 1.4f); box = new Rect(box.x, c2.yMax - h, w, h); }
        GUI.Box(box, GUIContent.none); GUI.Box(box, GUIContent.none); GUI.Box(box, GUIContent.none);
        float pad = 20 * k;
        GUILayout.BeginArea(new Rect(box.x + pad, box.y + 12 * k, w - pad * 2, h - 20 * k));
        DrawPanel(me, k, w - pad * 2);
        GUILayout.EndArea();
    }

    private void DrawTags()
    {
        if (cam == null) return;
        int me = Story1Role.Current;
        for (int i = 0; i < Count; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(Stands[i].x / 100f, -(Stands[i].y - 30f) / 100f + 0.34f, 0));
            Rect r = new Rect(sp.x - 80, Screen.height - sp.y - 14, 160, 28);
            if (MuseumChat.CoversGuiRect(r)) continue;
            tag.normal.textColor = i == me ? new Color(0.55f, 1f, 0.7f) : new Color(1f, 0.93f, 0.75f);
            GUI.Label(r, TagText(i), tag);
        }
    }

    // ---------------------------------------------------------------- 그리기 도우미
    protected static Texture2D MakeTex(int size, System.Action<Color32[], int> draw)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        draw(px, size);
        t.SetPixels32(px); t.Apply();
        return t;
    }

    protected static void Put(Color32[] px, int n, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < n && y < n) px[y * n + x] = c; }

    protected SpriteRenderer NewSprite(string name, Sprite sp, Vector2 stand, float lift, int orderOffset, float scale)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.sprite = sp; r.sortingOrder = Mathf.RoundToInt(stand.y) + orderOffset;
        r.transform.SetParent(worldRoot.transform, false);
        r.transform.position = new Vector3(stand.x / 100f, -(stand.y - lift) / 100f, 0);
        r.transform.localScale = Vector3.one * scale;
        return r;
    }

    protected static Sprite Pix(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
}

// =====================================================================================
/// <summary>
/// 방재실 — 환기 맞추기(쉬운 판). 환풍기 4대(= 자리 1~4), 세기는 0~3.
/// 각자 "다음 번호 환풍기의 정답 세기"만 볼 수 있다 → 그 번호 담당에게 채팅으로 알려 준다(예: 3 2).
/// 모두 자기 정답 세기로 맞추면 잠깐 뒤 해결 → 전기실 문이 열린다.
/// </summary>
public sealed class MuseumGas : StationPuzzle
{
    public static MuseumGas Instance { get; private set; }

    private static readonly Vector2[] St = { new Vector2(648, 860), new Vector2(644, 892), new Vector2(674, 898), new Vector2(728, 922) };
    protected override string RoomId => "safety";
    protected override Vector2[] Stands => St;
    protected override int NetKind => Story1Net.GasSet;
    protected override int SeedId => 8;
    protected override string Noun => "환풍기";
    protected override float PanelHeight => 230f;

    private const int MaxFan = 3;
    private readonly int[] fan = new int[Count];
    private readonly int[] target = new int[Count];        // 각 환풍기의 정답 세기
    private readonly int[] told = new int[Count];          // AI 가 전해 들은 자기 정답
    private readonly bool[] hasTold = new bool[Count];
    private readonly float[] actAt = new float[Count];
    private readonly float[] sayAt = new float[Count];
    private readonly int[] lastSaid = new int[Count];
    private readonly int[] heard = new int[Count];         // 채팅으로 들은 내 정답(-1 = 없음)
    private readonly float[] heardAt = new float[Count];
    private float safeSince = -1f;

    protected override void OnEnable() { base.OnEnable(); Instance = this; }
    protected override void OnDisable() { base.OnDisable(); if (Instance == this) Instance = null; }

    protected override void Init(System.Random rng)
    {
        bool anyNonZero = false;
        for (int z = 0; z < Count; z++) { target[z] = rng.Next(0, MaxFan + 1); if (target[z] != 0) anyNonZero = true; }
        if (!anyNonZero) target[0] = 2;
        for (int z = 0; z < Count; z++) { fan[z] = 0; hasTold[z] = false; heard[z] = -1; lastSaid[z] = -999; actAt[z] = -1f; sayAt[z] = -1f; }
    }

    // 월드에는 아무것도 더 만들지 않는다(방이 어수선해지지 않게). 방에 원래 있는 설비가 자리다.
    protected override void BuildWorld() { }
    protected override void RefreshWorld() { }
    protected override string TagText(int i) => (i + 1) + "번";

    private void Complete()
    {
        Solved = true; IsOpen = false;
        Sfx.Play("chime", 0.9f); Sfx.Play("door_open", 0.5f);
        MuseumValves.Notice("환기 성공!" + (char)10 + "전기실 문이 열렸어요", true, 5f);
    }

    protected override void Tick()
    {
        if (Solved) return;
        bool all = engaged;
        for (int z = 0; z < Count && all; z++) if (fan[z] != target[z]) all = false;
        if (!all) safeSince = -1f;
        else if (safeSince < 0f) safeSince = Time.time;
        else if (Time.time - safeSince >= 1.2f) { Complete(); return; }
        BotWork();
    }

    private void Learn(int z, int level) { if (z < 0 || z >= Count) return; level = Mathf.Clamp(level, 0, MaxFan); hasTold[z] = true; told[z] = level; heard[z] = level; heardAt[z] = Time.time; }

    private void BotWork()
    {
        if (!engaged) return;
        for (int s = 0; s < Count; s++)
        {
            if (!Story1Role.IsBot(s) || !reached[s]) continue;
            int z = (s + 1) % Count, g = target[z];

            // (1) 내가 보는 정답을 그 번호 담당에게 채팅으로 알려 준다(사람이 아직 못 맞췄으면 가끔 다시)
            if (sayAt[s] < 0f) sayAt[s] = engagedAt + 1.5f + 0.6f * s;
            bool toBot = Story1Role.IsBot(z);
            if (Time.time >= sayAt[s] && (toBot ? lastSaid[s] != g : fan[z] != g))
            {
                lastSaid[s] = g; sayAt[s] = Time.time + 20f;
                MuseumChat.Say(s, (z + 1) + " " + g);
                Learn(z, g);
            }

            // (2) 전해 들은 내 정답으로 내 환풍기를 한 칸씩 맞춘다
            if (hasTold[s] && Time.time >= actAt[s])
            {
                if (fan[s] < told[s]) fan[s]++;
                else if (fan[s] > told[s]) fan[s]--;
                if (fan[s] == told[s]) hasTold[s] = false;
                actAt[s] = Time.time + 0.6f;
            }
        }
    }

    private void SetFan(int s, int v)
    {
        v = Mathf.Clamp(v, 0, MaxFan);
        if (Solved || v == fan[s]) return;
        fan[s] = v;
        Sfx.Play("ui_select", 0.7f);
        Send(s, "F", v);
    }

    protected override void ApplyRemote(int station, string op, float f)
    {
        if (op == "F") fan[station] = Mathf.Clamp(Mathf.RoundToInt(f), 0, MaxFan);
    }

    protected override void OnChatText(int slot, string text)
    {
        Match m = Regex.Match(text, @"^\s*([1-4])\D{0,3}?([0-3])\s*$");
        if (m.Success) Learn(m.Groups[1].Value[0] - '1', m.Groups[2].Value[0] - '0');
    }

    protected override void Keys(Keyboard kb)
    {
        int me = Story1Role.Current;
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) SetFan(me, fan[me] - 1);
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) SetFan(me, fan[me] + 1);
    }

    // ---- 조작판 (초등학생도 3단계로: ① 내 세기 돌리기 ② 알려 주기 ③ 초록불 확인)
    private int Correct() { int c = 0; for (int z = 0; z < Count; z++) if (fan[z] == target[z]) c++; return c; }

    private void DrawGas(Rect r, int correct)
    {
        Color prev = GUI.color;
        GUI.color = new Color32(20, 16, 14, 255); GUI.DrawTexture(r, Texture2D.whiteTexture);
        float f = (Count - correct) / (float)Count;   // 맞은 환풍기가 늘수록 가스가 줄어든다
        GUI.color = Color.Lerp(new Color32(90, 220, 120, 255), new Color32(235, 110, 70, 255), f);
        GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * f, r.height - 4), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    protected override void DrawPanel(int me, float k, float width)
    {
        int z = (me + 1) % Count, g = target[z];
        int from = (me + 3) % Count;
        int ok = Correct();
        string lamps = "";
        for (int i = 0; i < Count; i++) lamps += fan[i] == target[i] ? "<color=#7be08e>●</color> " : "<color=#777777>○</color> ";

        GUILayout.BeginHorizontal();
        GUILayout.Label($"<color=#e7ba63><b>① {me + 1}번 환풍기</b></color>   세기를 돌려요", body, GUILayout.Width(width * 0.42f));
        GUI.enabled = !Solved;
        if (GUILayout.Button("◀ A", btn, GUILayout.Height(44 * k), GUILayout.Width(90 * k))) SetFan(me, fan[me] - 1);
        GUILayout.Label($"<size={Mathf.RoundToInt(34 * k)}><b>{fan[me]}</b></size>", big, GUILayout.Width(50 * k));
        if (GUILayout.Button("D ▶", btn, GUILayout.Height(44 * k), GUILayout.Width(90 * k))) SetFan(me, fan[me] + 1);
        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("닫기", btn, GUILayout.Height(44 * k), GUILayout.Width(90 * k))) Close();
        GUILayout.EndHorizontal();
        GUILayout.Space(6 * k);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"<color=#e7ba63><b>② 알려 주기</b></color>   {z + 1}번 {NameOf(z)} 님의 정답은 <size={Mathf.RoundToInt(28 * k)}><color=#9fd8a8><b>{g}</b></color></size>", body, GUILayout.Width(width * 0.62f));
        GUI.enabled = !Solved;
        if (GUILayout.Button($"채팅으로 보내기  “{z + 1} {g}”", btn, GUILayout.Height(44 * k))) { Story1Net.SendChat((z + 1) + " " + g); Sfx.Play("ui_next", 0.6f); }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Space(6 * k);

        string got = heard[me] >= 0 ? $"<b>{heard[me]}</b> 이에요" : "아직 없어요";
        GUILayout.Label($"<color=#e7ba63><b>③ 확인</b></color>   내 정답은 {NameOf(from)} 님이 알려 줘요 → {got}      맞은 환풍기  {lamps}", body);
        GUILayout.Space(4 * k);
        DrawGas(GUILayoutUtility.GetRect(width, 16 * k), ok);
        GUILayout.Label("가스 농도 — 네 개가 다 초록불이면 가스가 다 빠져요!", small);
    }
}

// =====================================================================================
/// <summary>
/// 전기실 — 차단기 순서 복구. 차단기 4개(▲ ● ■ ◆, = 자리)를 정해진 순서대로 올려야 전원이 돌아온다. 틀리면 전부 튕겨서 처음부터.
/// 순서는 아무도 전부 모른다: 각자 "다른 차단기 하나가 몇 번째인지"만 안다 → 채팅으로 "차단기번호 순서"(예: 3 2)를 알려 주고, 모이면 자기 순서가 나온다.
/// 마지막 차단기가 올라가면 불이 켜진다.
/// </summary>
public sealed class MuseumBreakers : StationPuzzle
{
    public static MuseumBreakers Instance { get; private set; }

    private static readonly Vector2[] St = { new Vector2(1736, 900), new Vector2(1824, 912), new Vector2(1958, 944), new Vector2(1842, 982) };
    protected override string RoomId => "power";
    protected override Vector2[] Stands => St;
    protected override int NetKind => Story1Net.BreakerSet;
    protected override int SeedId => 9;
    protected override string Noun => "차단기";
    protected override float PanelHeight => 270f;

    private static readonly string[] Sym = { "▲", "●", "■", "◆" };
    private static readonly Color32[] SymCol = { new Color32(230, 90, 80, 255), new Color32(90, 160, 240, 255), new Color32(240, 200, 70, 255), new Color32(100, 210, 130, 255) };
    private readonly int[] pos = new int[Count];        // 차단기 → 올라가야 하는 순서(0~3)
    private readonly int[] kb = new int[Count];         // 사람 → 순서를 아는 차단기
    private readonly int[] heard = new int[Count];      // 채팅으로 들은 차단기 순서(-1 = 모름)
    private readonly bool[] on = new bool[Count];
    private readonly bool[] said = new bool[Count];
    private readonly float[] sayAt = new float[Count];
    private readonly float[] pressAt = new float[Count];
    private int step;
    private Transform[] levers; private SpriteRenderer[] lamps;

    protected override void OnEnable() { base.OnEnable(); Instance = this; }
    protected override void OnDisable() { base.OnDisable(); if (Instance == this) Instance = null; }

    protected override void Init(System.Random rng)
    {
        int[] p = { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); int t = p[i]; p[i] = p[j]; p[j] = t; }
        int shift = rng.Next(1, Count);
        for (int b = 0; b < Count; b++)
        {
            pos[b] = p[b]; kb[b] = (b + shift) % Count;   // 사람 b 는 자기 말고 다른 차단기 하나의 순서를 안다(모든 차단기를 정확히 한 사람이 안다)
            heard[b] = -1; on[b] = false; said[b] = false; sayAt[b] = -1f; pressAt[b] = -1f;
        }
        step = 0;
    }

    // ---- 월드
    protected override void BuildWorld()
    {
        Color32 dark = new Color32(28, 28, 34, 255), steel = new Color32(120, 130, 146, 255), light = new Color32(190, 200, 214, 255);
        Sprite box = Pix(MakeTex(32, (px, n) =>
        {
            for (int y = 4; y < 28; y++) for (int x = 8; x < 24; x++)
                Put(px, n, x, y, (x == 8 || x == 23 || y == 4 || y == 27) ? dark : (y > 18 ? new Color32(255, 255, 255, 255) : steel));   // 위 띠는 기호 색으로 칠한다
        }));
        Sprite lever = Pix(MakeTex(16, (px, n) => { for (int y = 0; y < 12; y++) for (int x = 6; x < 10; x++) Put(px, n, x, y, light); for (int y = 10; y < 14; y++) for (int x = 5; x < 11; x++) Put(px, n, x, y, new Color32(235, 120, 80, 255)); }));
        Sprite lampSp = Pix(MakeTex(8, (px, n) => { for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) Put(px, n, x, y, (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? dark : new Color32(255, 255, 255, 255)); }));
        levers = new Transform[Count]; lamps = new SpriteRenderer[Count];
        for (int i = 0; i < Count; i++)
        {
            NewSprite("Breaker" + (i + 1), box, Stands[i], 26f, -12, 1.0f).color = new Color(SymCol[i].r / 255f, SymCol[i].g / 255f, SymCol[i].b / 255f, 1f) * 0.5f + new Color(0.5f, 0.5f, 0.5f, 0.5f);
            levers[i] = NewSprite("Lever" + (i + 1), lever, Stands[i], 22f, -11, 0.9f).transform;
            lamps[i] = NewSprite("BreakerLamp" + (i + 1), lampSp, Stands[i], 44f, -10, 1.1f);
        }
    }

    protected override void RefreshWorld()
    {
        for (int i = 0; i < Count; i++)
        {
            levers[i].rotation = Quaternion.Euler(0, 0, on[i] ? 0f : 180f);   // 올라가 있으면 위, 내려가 있으면 아래
            lamps[i].color = on[i] ? new Color(0.37f, 0.88f, 0.48f) : new Color(0.85f, 0.3f, 0.25f);
        }
    }

    protected override string TagText(int i) => Sym[i] + " " + (i + 1) + " · " + NameOf(i);

    // ---- 규칙
    private void Press(int b)
    {
        if (Solved || on[b]) return;
        if (!engaged) { engaged = true; engagedAt = Time.time; }
        if (pos[b] == step)
        {
            on[b] = true; step++;
            Sfx.Play("fuse_pop", 0.5f); Sfx.Play("ui_next", 0.8f);
            if (step >= Count) Complete();
        }
        else
        {
            for (int i = 0; i < Count; i++) { on[i] = false; pressAt[i] = -1f; }
            step = 0;
            Sfx.Play("fuse_pop", 1f); Sfx.Play("ui_back", 0.9f);
            MuseumValves.Notice("차단기가 전부 튕겼어요!" + (char)10 + "순서가 틀렸어요. 다시 맞춰 보세요", false, 3.5f);
        }
    }

    private void Complete()
    {
        Solved = true; IsOpen = false;
        Sfx.Play("power_on", 1f);
        MuseumStory.RestorePower();
        MuseumValves.Notice("전원 복구!" + (char)10 + "건물의 불이 다시 켜졌어요", true, 5f);
    }

    private int Base(int viewer, int b) => heard[b] >= 0 ? heard[b] : (b == kb[viewer] ? pos[b] : -1);

    /// <summary>viewer 가 아는 정보(채팅으로 들은 것 + 자기 단서)로 알아낸 차단기 b 의 순서. 나머지 셋을 알면 마지막 것은 제외로 알아낸다. 모르면 -1.</summary>
    private int PosFor(int viewer, int b)
    {
        int p = Base(viewer, b);
        if (p >= 0) return p;
        int mask = 0;
        for (int o = 0; o < Count; o++) { if (o == b) continue; int q = Base(viewer, o); if (q < 0) return -1; mask |= 1 << q; }
        for (int q = 0; q < Count; q++) if ((mask & (1 << q)) == 0) return q;
        return -1;
    }

    protected override void Tick()
    {
        if (Solved || !engaged) return;
        for (int s = 0; s < Count; s++)
        {
            if (!Story1Role.IsBot(s) || !reached[s]) continue;
            // (1) 내가 아는 차단기의 순서를 채팅으로 알려 준다(한 번)
            if (sayAt[s] < 0f) sayAt[s] = engagedAt + 2.4f + 0.8f * s;
            if (!said[s] && Time.time >= sayAt[s])
            {
                said[s] = true;
                MuseumChat.Say(s, (kb[s] + 1) + " " + (pos[kb[s]] + 1));
                heard[kb[s]] = pos[kb[s]];
            }
            // (2) 내 차단기의 순서를 알게 되면, 내 차례에 올린다
            int p = PosFor(s, s);
            if (p == step && !on[s])
            {
                if (pressAt[s] < 0f) pressAt[s] = Time.time + 1.2f;
                else if (Time.time >= pressAt[s]) { pressAt[s] = -1f; Press(s); }
            }
        }
    }

    protected override void ApplyRemote(int station, string op, float f) { if (op == "P") Press(station); }

    protected override void OnChatText(int slot, string text)
    {
        Match m = Regex.Match(text, @"^\s*([1-4])\D{0,3}?([1-4])\s*$");
        if (m.Success) heard[m.Groups[1].Value[0] - '1'] = m.Groups[2].Value[0] - '1';
    }

    private void PressMine()
    {
        int me = Story1Role.Current;
        if (Solved || on[me]) return;
        Press(me);
        Send(me, "P", 0f);
    }

    protected override void Keys(Keyboard kbd)
    {
        if (kbd.spaceKey.wasPressedThisFrame || kbd.enterKey.wasPressedThisFrame) PressMine();
    }

    // ---- 조작판
    private string KnownLine(int me)
    {
        var sb = new System.Text.StringBuilder();
        for (int b = 0; b < Count; b++)
        {
            int p = PosFor(me, b);
            sb.Append(Sym[b] + (b + 1) + ": " + (p >= 0 ? (p + 1) + "번째" : "?") + "     ");
        }
        return sb.ToString();
    }

    protected override void DrawPanel(int me, float k, float width)
    {
        int c = kb[me];
        GUILayout.Label($"<color=#e7ba63><b>{Sym[me]} {me + 1}번 차단기</b></color>     지금  <b>{Mathf.Min(step + 1, Count)}번째</b> 차례   (올라간 차단기 {step} / {Count})", body);
        GUILayout.Space(4 * k);
        GUILayout.Label($"내가 아는 정보   {Sym[c]} {c + 1}번 차단기는  <size={Mathf.RoundToInt(34 * k)}><b>{pos[c] + 1}번째</b></size>", body);
        GUILayout.Label($"→ 채팅으로 알려 주세요.   예)  {c + 1} {pos[c] + 1}        (내 {me + 1}번 차단기가 몇 번째인지는 다른 사람이 알아요)", small);
        GUILayout.Label("알려진 순서:   " + KnownLine(me), small);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUI.enabled = !Solved && !on[me];
        if (GUILayout.Button(on[me] ? "올렸어요" : "내 차단기 올리기 (Space)", btn, GUILayout.Height(54 * k), GUILayout.ExpandWidth(true))) PressMine();
        GUI.enabled = true;
        if (GUILayout.Button("닫기 (E)", btn, GUILayout.Height(54 * k), GUILayout.Width(140 * k))) Close();
        GUILayout.EndHorizontal();
    }
}

// =====================================================================================
/// <summary>퍼즐 3개(밸브·가스·차단기)를 StrangerMuseum 에 한 얼굴로 보여 주는 창구 + 문 잠금 규칙.</summary>
public static class MuseumPuzzles
{
    public static bool AnyOpen
    {
        get
        {
            if (MuseumValves.IsOpen) return true;
            foreach (StationPuzzle p in StationPuzzle.All) if (p.IsOpen) return true;
            return false;
        }
    }

    public static void SetLocation(string floorId, string roomId)
    {
        MuseumValves.SetLocation(floorId, roomId);
        foreach (StationPuzzle p in StationPuzzle.All) p.SetLoc(floorId, roomId);
    }

    public static bool NearStation(Vector2 pos, float radius, out string prompt)
    {
        prompt = null;
        int v = MuseumValves.NearValve(pos, radius);
        if (v >= 0) { prompt = MuseumValves.PromptFor(v); return true; }
        foreach (StationPuzzle p in StationPuzzle.All)
        {
            int i = p.Near(pos, radius);
            if (i >= 0) { prompt = p.Prompt(i); return true; }
        }
        return false;
    }

    public static bool TryInteract(Vector2 pos, float radius)
    {
        if (MuseumValves.TryInteract(pos, radius)) return true;
        foreach (StationPuzzle p in StationPuzzle.All) if (p.TryInteract(pos, radius)) return true;
        return false;
    }

    public static bool BotStand(int slot, out Vector2 stand)
    {
        if (MuseumValves.BotStand(slot, out stand)) return true;
        foreach (StationPuzzle p in StationPuzzle.All) if (p.BotStand(slot, out stand)) return true;
        stand = default;
        return false;
    }

    public static void SetReached(int slot, bool on)
    {
        MuseumValves.SetReached(slot, on);
        foreach (StationPuzzle p in StationPuzzle.All) p.SetReached(slot, on);
    }

    public static bool DoorLocked(string portalName)
    {
        if (string.IsNullOrEmpty(portalName)) return false;
        if (portalName.Contains("방재실 문") || portalName.Contains("응급 수장고 문")) return !MuseumValves.Solved;
        if (portalName.Contains("전기실 문")) return !(MuseumValves.Solved && MuseumGas.Instance != null && MuseumGas.Instance.Solved);
        return false;
    }

    public static void BumpLockedDoor(string portalName)
    {
        Sfx.Play("door_locked", 0.9f);
        if (portalName != null && portalName.Contains("전기실 문") && MuseumValves.Solved)
            MuseumValves.Notice("방 안에 가스가 차 있어 위험해요. 방재실의 환풍기를 먼저 맞춰요.", false);
        else
            MuseumValves.Notice("수압이 높아 문이 잠겨 있어요. 급수 밸브(관리 통로)를 네 명이 맞춰요.", false);
    }
}
