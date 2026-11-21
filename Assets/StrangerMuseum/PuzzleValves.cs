using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미션 1 · 물길 막기 (지하 관리 통로).
///  · 밸브 4개. 누구나 빈 밸브를 E 로 잡는다(한 사람에 하나). 돌린 값은 놓아도 남는다.
///  · 밸브마다 정답(← 또는 → 로 1~2칸)이 있고, 네 사람은 각자 "다른 밸브 하나"의 정답만 안다.
///    내가 정답을 아는 밸브는 내가 잡을 수 없다 → 정답을 아는 사람과 돌리는 사람이 늘 다르다.
///  · 네 밸브가 모두 잡히고 네 사람이 모두 "고정"하면 판정. 다 맞으면 물이 멈춘다. 틀리면 몇 개가 맞았는지만 알려 주고 고정이 풀린다.
///  · AI 친구: 빈 밸브를 잡으러 걸어가고, 자기가 아는 밸브를 누가 잡으면 자기 나라 말 + 신호로 알려 준다.
///    자기 밸브의 정답은 채팅 신호("[2] ←1", "2 ←1")로 들어야만 돌린다. 한국어 문장은 못 알아듣는다.
/// </summary>
public sealed class PuzzleValves : MuseumPuzzle
{
    public override int Id => 10;
    public override int Stage => MuseumGame.Valves;
    public override string Floor => "B1";
    public override string Room => "maintenance";
    public override string Title => "급수 밸브";
    protected override float PanelH => 666f;
    protected override int KeyRole => MuseumGame.Observer;   // 어느 밸브가 맞았는지 본다

    private static readonly Vector2[] Pos = { new Vector2(1192, 868), new Vector2(1280, 868), new Vector2(1370, 868), new Vector2(1548, 868) };
    protected override Vector2[] Stations => Pos;

    private const int OpGrab = 1, OpRelease = 2, OpTurn = 3, OpLock = 4, OpUnlockAll = 5, OpTold = 6, OpUnlock = 7;
    private float evalAt = -1f;     // 방장: 넷 다 고정인데 판정이 안 났으면 다시 판정(이벤트를 놓친 화면 보정)

    private readonly int[] holder = { -1, -1, -1, -1 };   // 밸브 → 잡은 사람
    private readonly int[] val = new int[4];              // 밸브 → 지금 값(-3~3)
    private readonly int[] ans = new int[4];              // 밸브 → 정답
    private readonly bool[] locked = new bool[4];         // 밸브 → 고정됨
    private readonly int[] knows = new int[4];            // 사람 → 정답을 아는 밸브
    private bool ready;
    private int lastRight = -1;     // 화면 힌트용(틀린 판정 뒤 다시 고정할 때까지)
    private bool botRetry;          // AI 용(틀린 판정 뒤 한 번 다시 알려 주기)

    // AI
    private readonly float[] botNext = new float[4];
    private readonly bool[] botHeard = new bool[4];       // AI 가 잡은 밸브의 정답을 들었는가
    private readonly int[] botHeardVal = new int[4];
    private readonly int[] sharedClue = new int[4]; // 밸브를 잡기 전에 도착한 신호도 보관한다.
    private readonly float[] toldAt = { -99f, -99f, -99f, -99f };   // 밸브 정답을 누가 마지막으로 알려 준 시각
    private readonly bool[] botAsked = new bool[4];
    private float engagedAt = -1f;

    // 화면
    private PixelText holdTitle, valueText, clueTitle, clueText, whoText, hintText, legendText;
    private PxButton leftBtn, rightBtn, lockBtn, releaseBtn, tellBtn;
    private RectTransform dial;
    private Image dialImg;

    // 월드
    private GameObject worldRoot;
    private Transform[] wheels;
    private SpriteRenderer[] lamps;

    private void Ensure()
    {
        if (ready) return;
        var rng = new System.Random(Story1Net.Seed(Id));
        int[] k = { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); (k[i], k[j]) = (k[j], k[i]); }
        for (int s = 0; s < 4; s++) knows[s] = k[s];
        int max = Story1Net.Difficulty == Story1Rules.Easy ? 2 : 3;
        for (int v = 0; v < 4; v++) { int n = rng.Next(1, max + 1); ans[v] = rng.Next(2) == 0 ? -n : n; }
        ready = true;
    }

    protected override void Awake() { base.Awake(); Ensure(); MuseumGame.Register(this); }

    private int HeldBy(int slot) { for (int v = 0; v < 4; v++) if (holder[v] == slot) return v; return -1; }
    private static string Arrow(int x) => x == 0 ? "0" : (x < 0 ? "←" : "→") + Mathf.Abs(x);

    public override string Progress
    {
        get { int h = 0; for (int v = 0; v < 4; v++) if (holder[v] >= 0) h++; int l = 0; for (int v = 0; v < 4; v++) if (locked[v]) l++; return "잡은 밸브 " + h + "/4   ·   고정 " + l + "/4"; }
    }

    protected override string PromptAt(int v)
    {
        if (holder[v] == Me) return (v + 1) + "번 밸브 돌리기";
        if (holder[v] >= 0) return (v + 1) + "번 밸브 (" + Name(holder[v]) + ")";
        if (knows[Me] == v) return (v + 1) + "번 밸브 (정답을 내가 알아요)";
        return (v + 1) + "번 밸브 잡기";
    }

    protected override void OnInteract(int v)
    {
        if (holder[v] == Me) { Open(); return; }
        if (holder[v] >= 0) { MuseumHud.Toast((v + 1) + "번 밸브는 " + Name(holder[v]) + MuseumGame.Iga(Name(holder[v])) + " 담당 중"); Sfx.Play("ui_back", 0.6f); return; }
        if (knows[Me] == v) { MuseumHud.Toast("내가 정답을 아는 밸브 · 다른 사람이 돌려야 한다", MuseumUI.Amber); Sfx.Play("ui_back", 0.6f); Open(); return; }
        int mine = HeldBy(Me);
        if (mine < 0 && !Feasible(Me, v) && AnyFeasibleFor(Me))
        {
            // 이 밸브를 잡으면 남은 친구가 잡을 수 있는 밸브가 없어진다
            MuseumHud.Toast("이걸 잡으면 남은 사람이 잡을 밸브가 없다 · 다른 밸브로", MuseumUI.Amber);
            Sfx.Play("ui_back", 0.6f);
            return;
        }
        if (mine >= 0) Send(OpRelease, mine.ToString());
        Send(OpGrab, v.ToString());
        Open();
    }

    private bool AnyFeasibleFor(int s)
    {
        for (int x = 0; x < 4; x++) if (holder[x] < 0 && knows[s] != x && Feasible(s, x)) return true;
        return false;
    }

    /// <summary>밸브가 없는데, 남은 빈 밸브가 전부 "내가 정답을 아는 밸브"라 잡을 수 없는 사람.</summary>
    private bool Stuck(int p)
    {
        if (HeldBy(p) >= 0) return false;
        bool anyFree = false;
        for (int x = 0; x < 4; x++) if (holder[x] < 0) { anyFree = true; if (knows[p] != x) return false; }
        return anyFree;
    }

    public override bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!Bot(slot) || MuseumGame.Stage != Stage) return false;
        int v = HeldBy(slot);
        if (v < 0) return false;
        stand = Pos[v] + new Vector2(0, 12);
        return true;
    }

    public override Vector2 GuidePoint
    {
        get { for (int v = 0; v < 4; v++) if (holder[v] < 0 && knows[Me] != v) return Pos[v]; return Pos[1]; }
    }

    // ---------------------------------------------------------------- 상태 바꾸기(모두 이벤트로)
    protected override void OnNet(int op, string value, int actor)
    {
        if (Solved) return;
        int.TryParse(value, out int v);
        switch (op)
        {
            case OpGrab:
                if (v < 0 || v > 3 || holder[v] >= 0 || HeldBy(actor) >= 0 || knows[actor] == v) return;
                holder[v] = actor; locked[v] = false;
                botHeardVal[actor] = sharedClue[v];
                botHeard[actor] = sharedClue[v] != 0;
                Engaged = true; if (engagedAt < 0f) engagedAt = Time.time;
                if (actor == Me) Sfx.Play("museum_metal_click", 0.65f);
                break;
            case OpRelease:
                if (v < 0 || v > 3 || holder[v] != actor) return;
                holder[v] = -1; locked[v] = false;
                break;
            case OpTurn:
            {
                int sep = value.IndexOf(':');
                if (sep < 0) return;
                int.TryParse(value.Substring(0, sep), out v);
                int.TryParse(value.Substring(sep + 1), out int nv);
                if (v < 0 || v > 3 || holder[v] != actor || locked[v]) return;
                val[v] = Mathf.Clamp(nv, -3, 3);
                break;
            }
            case OpLock:
                if (v < 0 || v > 3 || holder[v] != actor) return;
                locked[v] = true;
                Sfx.Play("ui_select", 0.5f);
                Evaluate();
                break;
            case OpUnlock:   // 내 밸브 고정 풀기(잘못 고정했을 때 다시 돌릴 수 있게)
                if (v < 0 || v > 3 || holder[v] != actor || !locked[v]) return;
                locked[v] = false;
                if (actor == Me) Sfx.Play("museum_metal_click", 0.5f);
                break;
            case OpUnlockAll:
                for (int i = 0; i < 4; i++) locked[i] = false;
                lastRight = v; botRetry = true;
                Sfx.Play("door_locked", 0.5f);
            {
                // 몇 개 맞았는지는 쉬움에서만(보통·어려움에서 숫자를 보면 찍어서 맞출 수 있다는 의견)
                int diff = Story1Net.Difficulty;
                MuseumGame.Mistake();
                MuseumHud.Toast(diff == Story1Rules.Easy ? "4개 중 " + v + "개 맞음 · 고정이 풀렸다 · 정답을 다시 맞추고 다시 고정!"
                                                         : "물이 멈추지 않는다 · 고정이 풀렸다 · 단서를 다시 확인하고 다시 고정!", MuseumUI.Red);
                if (MuseumNet.Authority && Bot(MuseumGame.Observer))   // AI 관측자: 틀린 밸브 번호를 알려 준다(어려움에서는 "아니야!"만)
                {
                    var w = new System.Text.StringBuilder();
                    if (diff != Story1Rules.Hard) for (int i = 0; i < 4; i++) if (val[i] != ans[i]) w.Append("[").Append(i + 1).Append("] × ");
                    BotSay(MuseumGame.Observer, MuseumVoice.NoW(MuseumGame.Observer) + "!  " + w.ToString().Trim());
                }
            }
                break;
            case OpTold:
                if (v >= 0 && v < 4)
                {
                    toldAt[v] = Time.time;
                    if (Bot(actor) && knows[actor] == v) sharedClue[v] = ans[v];
                }
                break;
        }
    }

    public override int[] SnapState()
    {
        var l = new System.Collections.Generic.List<int>();
        Put(l, holder); Put(l, val); Put(l, locked); Put(l, ans); Put(l, knows); l.Add(Engaged ? 1 : 0);
        return l.ToArray();
    }

    public override void LoadState(int[] s)
    {
        if (s.Length < 21) return;
        CopyIn(s, 0, holder); CopyIn(s, 4, val); CopyIn(s, 8, locked); CopyIn(s, 12, ans); CopyIn(s, 16, knows);
        if (s[20] != 0) Engaged = true;
    }

    protected override void Update()
    {
        base.Update();
        // 방장 보정: 넷 다 잡고 다 고정했는데 1.5초가 지나도 판정이 없으면 다시 판정한다
        if (!MuseumNet.Authority || Solved || MuseumGame.Stage != Stage) { evalAt = -1f; return; }
        bool all = true;
        for (int v = 0; v < 4; v++) if (holder[v] < 0 || !locked[v]) { all = false; break; }
        if (!all) { evalAt = -1f; return; }
        if (evalAt < 0f) evalAt = Time.time + 1.5f;
        else if (Time.time > evalAt) { evalAt = Time.time + 3f; Evaluate(); }
    }

    private void Evaluate()
    {
        for (int v = 0; v < 4; v++) if (holder[v] < 0 || !locked[v]) return;
        int right = 0; for (int v = 0; v < 4; v++) if (val[v] == ans[v]) right++;
        if (!MuseumNet.Authority) return;
        if (right == 4) { Solved = true; MuseumNet.Mission(Stage); }
        else Send(OpUnlockAll, right.ToString());
    }

    // ---------------------------------------------------------------- 채팅(AI 가 듣는 것)
    protected override void OnChat(int slot, string text)
    {
        if (Bot(slot)) return;
        string t = MuseumSignals.Normalize(text);
        // "[2] ←1" / "2 ←1" / "2번 ← 1": 첫 숫자 = 밸브, 화살표 뒤 숫자 = 칸
        int num = -1, dir = 0, n = -1;
        foreach (char c in t)
        {
            if (c >= '1' && c <= '4') { if (num < 0) num = c - '1'; else if (dir != 0 && n < 0) n = c - '0'; }
            else if ((c == '←' || c == '→') && num >= 0 && dir == 0) dir = c == '←' ? -1 : 1;
        }
        if (num >= 0 && dir != 0 && n > 0)
        {
            int value = Mathf.Clamp(dir * n, -3, 3);
            sharedClue[num] = value;
            toldAt[num] = Time.time;
            int h = holder[num];
            if (h >= 0 && Bot(h) && MuseumNet.Authority) { botHeard[h] = true; botHeardVal[h] = value; botNext[h] = Time.time + 1.2f; }
            return;
        }
        if (t.Contains("?") && MuseumNet.Authority)   // 물음표: 내 밸브 정답을 아는 AI 가 다시 말해 준다
        {
            int mine = HeldBy(slot);
            if (mine >= 0) for (int s = 0; s < 4; s++) if (Bot(s) && knows[s] == mine) { botAsked[s] = false; toldAt[mine] = -99f; botNext[s] = Time.time + 0.8f; }
        }
    }

    // ---------------------------------------------------------------- AI
    protected override void BotThink()
    {
        if (!Engaged)
        {
            // 사람이 관리 통로에 들어오면 AI 도 밸브를 잡으러 간다
            var m = Museum;
            if (MuseumGame.AnyHumanIn(Floor, Room)) { Engaged = true; engagedAt = Time.time; }
            else return;
        }
        for (int s = 0; s < 4; s++)
        {
            if (!Bot(s) || Time.time < botNext[s]) continue;
            int mine = HeldBy(s);
            if (mine < 0)
            {
                // 빈 밸브 중 내가 정답을 모르는 것(사람이 노릴 만한 건 피한다: 번호가 큰 것부터)
                int pick = -1;
                for (int pass = 0; pass < 2 && pick < 0; pass++)
                    for (int v = 3; v >= 0; v--)
                        if (holder[v] < 0 && knows[s] != v && (pass == 1 || !HumanWants(v)) && Feasible(s, v)) { pick = v; break; }
                if (pick >= 0) Send(OpGrab, pick.ToString(), s);
                botNext[s] = Time.time + 0.9f + s * 0.35f;
                continue;
            }
            // 밸브를 못 잡고 있는 친구가 있으면 내 밸브를 넘겨주고 남은 밸브로 옮긴다
            bool swapped = false;
            for (int p = 0; p < 4 && !swapped; p++)
            {
                if (p == s || !Stuck(p) || knows[p] == mine) continue;
                for (int x = 0; x < 4; x++)
                {
                    if (holder[x] >= 0 || knows[s] == x) continue;
                    Send(OpRelease, mine.ToString(), s);
                    Send(OpGrab, x.ToString(), s);
                    BotSay(s, MuseumVoice.YesW(s) + "! [" + (mine + 1) + "] ○");
                    swapped = true; break;
                }
            }
            if (swapped) { botNext[s] = Time.time + 1.2f; continue; }
            // 내가 아는 밸브를 누가 잡았으면 알려 준다
            int kv = knows[s];
            if (holder[kv] >= 0 && holder[kv] != s && Time.time - toldAt[kv] > 30f && !botAsked[s])
            {
                if (Time.time - engagedAt > 2.5f + s * 1.1f)
                {
                    // 어려움: 처음엔 그 나라 말 + 밸브 번호만(방향·칸은 수첩의 낱말로 풀기). 다시 물으면(?) 신호까지.
                    bool full = Story1Net.Difficulty != Story1Rules.Hard || botTold[s];
                    BotSay(s, ValveLine(s, kv, ans[kv], full));
                    botTold[s] = true;
                    Send(OpTold, kv.ToString(), s);
                    if (Bot(holder[kv])) { botHeard[holder[kv]] = true; botHeardVal[holder[kv]] = ans[kv]; botNext[holder[kv]] = Time.time + 1.4f; }
                    botAsked[s] = true;
                    botNext[s] = Time.time + 1.5f;
                    continue;
                }
            }
            // 내 밸브: 정답을 들었으면 맞추고 고정
            if (botHeard[s])
            {
                if (val[mine] != botHeardVal[s]) { Send(OpTurn, mine + ":" + botHeardVal[s], s); botNext[s] = Time.time + 0.6f; continue; }
                if (!locked[mine]) { Send(OpLock, mine.ToString(), s); BotSay(s, MuseumVoice.YesW(s) + "! " + MuseumVoice.ThanksW(s) + " ○"); }
                botNext[s] = Time.time + 1f;
                continue;
            }
            // 오래 아무도 안 알려 주면 묻는다
            if (Time.time - engagedAt > 18f && Time.time - toldAt[mine] > 25f)
            {
                BotSay(s, MuseumVoice.WhatW(s).TrimEnd('?') + "… [" + (mine + 1) + "] ?");
                toldAt[mine] = Time.time;
            }
            botNext[s] = Time.time + 1f;
        }
        // 틀린 판정 뒤: AI 는 다시 고정, 아는 친구는 다시 알려 준다
        if (botRetry) { botRetry = false; for (int s = 0; s < 4; s++) { botAsked[s] = false; } for (int v = 0; v < 4; v++) toldAt[v] = -99f; }
    }

    /// <summary>s 가 v 를 잡아도, 아직 밸브가 없는 나머지 사람들이 "자기가 정답을 모르는 빈 밸브"를 하나씩 나눠 가질 수 있는가.</summary>
    private bool Feasible(int s, int v)
    {
        var people = new System.Collections.Generic.List<int>();
        var free = new System.Collections.Generic.List<int>();
        for (int p = 0; p < 4; p++) if (p != s && HeldBy(p) < 0) people.Add(p);
        for (int x = 0; x < 4; x++) if (x != v && holder[x] < 0) free.Add(x);
        return Match(people, free, 0, new bool[free.Count]);
    }

    private bool Match(System.Collections.Generic.List<int> people, System.Collections.Generic.List<int> free, int i, bool[] used)
    {
        if (i >= people.Count) return true;
        for (int j = 0; j < free.Count; j++)
        {
            if (used[j] || knows[people[i]] == free[j]) continue;
            used[j] = true;
            if (Match(people, free, i + 1, used)) return true;
            used[j] = false;
        }
        return false;
    }

    /// <summary>사람이 잡으려는 밸브(사람이 그 앞에 서 있다)는 AI 가 비켜 준다.</summary>
    private bool HumanWants(int v)
    {
        var m = Museum;
        return m != null && m.CurFloor == Floor && m.CurRoom == Room && (m.PlayerMap - Pos[v]).sqrMagnitude < 70f * 70f && knows[Me] != v;
    }

    private readonly bool[] botTold = new bool[4];

    private static string ValveLine(int s, int v, int a, bool full = true)
    {
        string dirW = a < 0 ? MuseumVoice.Left(s) : MuseumVoice.Right(s);
        string word = s == 0 ? "밸브" : s == 1 ? "Balbula" : s == 2 ? "Van" : "Ivalvu";
        return word + " " + MuseumVoice.Number(s, v + 1) + "! " + dirW + ", " + MuseumVoice.Number(s, Mathf.Abs(a)) + "   [" + (v + 1) + "]" + (full ? " " + Arrow(a) : "");
    }

    // ---------------------------------------------------------------- 조작창
    protected override void BuildBody(RectTransform b)
    {
        // 왼쪽: 내 밸브(돌리고 고정)
        var left = MuseumUI.Node("Left", b); MuseumUI.Anchor(left, 0f, 1f, 0f, 0f, 424f, 330f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", left)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.13f, 1f));
        holdTitle = MuseumUI.Text(left, "Title", "", 2, MuseumUI.Amber, TextAnchor.MiddleCenter, 0f, 135f, 400f, 40f);
        dial = MuseumUI.Node("Dial", left); MuseumUI.At(dial, 0f, 50f, 120f, 120f);
        dialImg = MuseumUI.Img(dial, MakeSprite(WheelTex()), Color.white);
        leftBtn = MuseumUI.Button(left, "L", "◀", 3, () => Turn(-1)); MuseumUI.At(leftBtn.Rect, -150f, 50f, 84f, 84f);
        rightBtn = MuseumUI.Button(left, "R", "▶", 3, () => Turn(1)); MuseumUI.At(rightBtn.Rect, 150f, 50f, 84f, 84f);
        valueText = MuseumUI.Text(left, "Value", "0", 4, MuseumUI.Ink, TextAnchor.MiddleCenter, 0f, -50f, 300f, 70f);
        lockBtn = MuseumUI.Button(left, "Lock", "고정", 2, LockMine); MuseumUI.At(lockBtn.Rect, 0f, -122f, 360f, 56f);
        lockBtn.Accent = MuseumUI.Mint;
        releaseBtn = MuseumUI.Button(left, "Release", "놓기", 2, ReleaseMine); MuseumUI.At(releaseBtn.Rect, 0f, -400f, 10f, 10f);
        releaseBtn.SetActive(false);

        // 오른쪽: 내가 알려 줄 정답
        var right = MuseumUI.Node("Right", b); MuseumUI.Anchor(right, 1f, 1f, 0f, 0f, 424f, 330f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", right)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.13f, 1f));
        clueTitle = MuseumUI.Text(right, "Title", "알려 줄 정답", 2, MuseumUI.Amber, TextAnchor.MiddleCenter, 0f, 135f, 400f, 40f);
        clueText = MuseumUI.Text(right, "Clue", "", 4, MuseumUI.Mint, TextAnchor.MiddleCenter, 0f, 60f, 400f, 72f);
        whoText = MuseumUI.Text(right, "Who", "", 2, MuseumUI.Ink, TextAnchor.MiddleCenter, 0f, -20f, 400f, 40f);
        tellBtn = MuseumUI.Button(right, "Tell", "보내기", 2, Tell); MuseumUI.At(tellBtn.Rect, 0f, -122f, 360f, 56f);
        tellBtn.Accent = MuseumUI.Mint;
        legendText = MuseumUI.Text(right, "Legend", "", 1, MuseumUI.Slate, TextAnchor.MiddleCenter, 0f, -300f, 10f, 10f);

        hintText = MakeHint(b);
    }

    protected override string KeysHint => "← → 돌리기   Space 고정/풀기   T 보내기   X 닫기";

    protected override void OnKeys(UnityEngine.InputSystem.Keyboard kb)
    {
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Turn(-1);
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Turn(1);
        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) LockMine();
        if (kb.tKey.wasPressedThisFrame) Tell();
    }

    private void Turn(int d)
    {
        int v = HeldBy(Me); if (v < 0 || locked[v]) return;
        int nv = Mathf.Clamp(val[v] + d, -3, 3);
        if (nv == 0) nv = Mathf.Clamp(nv + d, -3, 3);   // 0 칸은 건너뛴다(정답은 늘 한쪽으로 1칸 이상)
        Send(OpTurn, v + ":" + nv);
        Sfx.Play("museum_valve_turn", 0.45f);
    }

    /// <summary>고정 ↔ 고정 풀기. 잘못 고정했으면 풀고 다시 돌린다.</summary>
    private void LockMine()
    {
        int v = HeldBy(Me); if (v < 0) return;
        Send(locked[v] ? OpUnlock : OpLock, v.ToString());
    }
    private void ReleaseMine() { int v = HeldBy(Me); if (v >= 0) Send(OpRelease, v.ToString()); }

    private void Tell()
    {
        int kv = knows[Me];
        Story1Net.SendChat("[" + (kv + 1) + "] " + Arrow(ans[kv]));
    }

    protected override void RefreshPanel()
    {
        int v = HeldBy(Me);
        // 지금 할 일을 한 단계씩(①잡기 → ②단서 보내기 → ③받은 단서대로 돌리기 → ④고정)
        int kv0 = knows[Me];
        bool toldMine = toldAt[kv0] > 0f && holder[kv0] >= 0;
        string step = v < 0 ? "① 빈 밸브 앞에서 E 로 잡기"
            : !toldMine && holder[kv0] >= 0 ? "② 오른쪽 '보내기'로 " + Name(holder[kv0]) + "에게 단서 보내기"
            : sharedClue[v] == 0 ? "③ 내 밸브 단서를 기다리기 (모르면 ? 카드)"
            : !locked[v] ? "③ 받은 단서대로 돌리고 ④ 고정"
            : "④ 고정 완료 · 넷 다 고정하면 판정 (틀렸으면 고정 풀기)";
        Sub(step);
        hintText.text = "";
        if (v < 0)
        {
            holdTitle.text = "내 밸브 없음";
            valueText.text = Stuck(Me) ? "교대 요청 필요" : "빈 밸브에서 E";
            valueText.scale = 2; valueText.color = MuseumUI.Slate;
            leftBtn.Enabled = rightBtn.Enabled = lockBtn.Enabled = false;
        }
        else
        {
            holdTitle.text = "내 밸브 · " + (v + 1) + "번";
            // 받은 단서를 숫자로 대신 풀어 주는 건 쉬움만(보통·어려움은 채팅의 말을 읽어서 · 모르는 낱말은 수첩 N)
            hintText.text = sharedClue[v] == 0 ? "내 밸브의 단서를 기다리는 중"
                : Story1Net.Difficulty != Story1Rules.Easy ? "단서가 왔다 · 채팅의 말을 읽어 보자 (모르면 수첩 N · ? 카드)"
                : "받은 단서   " + (v + 1) + "번 밸브   " + Arrow(sharedClue[v]) + "칸";
            hintText.color = MuseumUI.Mint;
            valueText.scale = 4;
            valueText.text = val[v] == 0 ? "0" : (val[v] < 0 ? "← " : "→ ") + Mathf.Abs(val[v]) + "칸";
            valueText.color = locked[v] ? MuseumUI.Mint : MuseumUI.Ink;
            leftBtn.Enabled = rightBtn.Enabled = !locked[v];
            lockBtn.Enabled = true; lockBtn.SetText(locked[v] ? "고정 풀기" : "고정");
            dial.localRotation = Quaternion.Euler(0, 0, -val[v] * 30f);
        }
        int kv = knows[Me];
        clueText.text = (kv + 1) + "번  " + (ans[kv] < 0 ? "← " : "→ ") + Mathf.Abs(ans[kv]) + "칸";
        whoText.text = holder[kv] >= 0 ? "→ " + Name(holder[kv]) + "에게" : "아직 빈 밸브";
        whoText.color = holder[kv] >= 0 ? Roles.Color(holder[kv]) : MuseumUI.Slate;
        tellBtn.Enabled = true;
        if (lastRight >= 0 && v >= 0 && !locked[v]) { hintText.text = "틀린 밸브 있음 · ? 카드로 다시 묻기"; hintText.color = MuseumUI.Red; }
        if (v >= 0 && locked[v]) lastRight = -1;
    }

    protected override string TeamState(int slot, out Color color)
    {
        int v = HeldBy(slot);
        if (v < 0) { color = MuseumUI.Slate; return "-"; }
        color = locked[v] ? MuseumUI.Mint : MuseumUI.Amber;
        if (MuseumGame.IHave(MuseumGame.Observer)) { bool ok = val[v] == ans[v]; color = ok ? MuseumUI.Mint : MuseumUI.Red; return "[" + (v + 1) + "]" + (ok ? "○" : "×"); }   // 관측자: 맞은 밸브가 보인다
        return "[" + (v + 1) + "]" + (locked[v] ? "○" : "");
    }

    // ---------------------------------------------------------------- 월드
    private static Texture2D WheelTex()
    {
        Color32 dark = new Color32(60, 28, 22, 255), red = new Color32(214, 84, 56, 255), light = new Color32(255, 160, 120, 255);
        return MakeTex(32, 32, (px, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = x - 15.5f, dy = y - 15.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                bool ring = d > 11f && d < 15f, hub = d < 3.5f, spoke = (Mathf.Abs(dx) < 1.3f || Mathf.Abs(dy) < 1.3f) && d < 15f;
                if (d > 11f && d < 15.9f && (d < 11.9f || d > 14.9f)) Put(px, w, h, x, y, dark);
                else if (ring) Put(px, w, h, x, y, (dx + dy) < 0 ? light : red);
                else if (hub) Put(px, w, h, x, y, light);
                else if (spoke) Put(px, w, h, x, y, red);
                if (y >= 26 && y <= 29 && x >= 14 && x <= 17) Put(px, w, h, x, y, new Color32(255, 230, 140, 255));   // 위쪽 표시
            }
        });
    }

    protected override void BuildWorld()
    {
        worldRoot = new GameObject("Valves");
        worldRoot.transform.SetParent(transform.parent, false);
        var wheelSp = MakeSprite(WheelTex());
        var lampSp = MakeSprite(MakeTex(6, 6, (px, w, h) => { for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) Put(px, w, h, x, y, (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? new Color32(40, 30, 26, 255) : new Color32(255, 255, 255, 255)); }));
        wheels = new Transform[4]; lamps = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = World(Pos[i], 0.46f);
            int order = Mathf.RoundToInt(Pos[i].y) - 12;
            var w = new GameObject("Wheel" + (i + 1)).AddComponent<SpriteRenderer>(); w.sprite = wheelSp; w.sortingOrder = order;
            w.transform.SetParent(worldRoot.transform, false); w.transform.position = p; w.transform.localScale = Vector3.one * 0.8f;
            wheels[i] = w.transform;
            var l = new GameObject("Lamp" + (i + 1)).AddComponent<SpriteRenderer>(); l.sprite = lampSp; l.sortingOrder = order + 1;
            l.transform.SetParent(worldRoot.transform, false); l.transform.position = p + new Vector3(0, 0.24f, 0); l.transform.localScale = Vector3.one * 1.4f;
            lamps[i] = l;
        }
        worldRoot.SetActive(false);
    }

    protected override void UpdateWorld()
    {
        if (worldRoot == null) return;
        var m = Museum;
        bool on = m != null && m.CurFloor == Floor;
        if (worldRoot.activeSelf != on) worldRoot.SetActive(on);
        if (!on) return;
        for (int i = 0; i < 4; i++)
        {
            wheels[i].rotation = Quaternion.Euler(0, 0, -(Done ? ans[i] : val[i]) * 30f);
            lamps[i].color = Done || locked[i] ? new Color(0.37f, 0.9f, 0.5f) : holder[i] >= 0 ? new Color(1f, 0.78f, 0.3f) : new Color(0.45f, 0.45f, 0.48f);
        }
    }
}
