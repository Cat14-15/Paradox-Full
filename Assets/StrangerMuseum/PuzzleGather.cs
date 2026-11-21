using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;

/// <summary>
/// 미션 5 · 함께 서기 (2층 반원 홀).
///  · 동상 둘레 바닥 발판 4개. 네 사람이 동시에 발판 위에 서서 2초를 버티면 비상문이 열린다.
///  · 사람은 걸어서 올라간다(내 발판 상태는 이벤트로 알린다). AI 친구는 사람이 발판에 오르면 남은 발판으로 걸어간다.
///  · 한 명이라도 빠지면 열리지 않는다.
/// </summary>
public sealed class PuzzleGather : MuseumPuzzle
{
    public override int Id => 14;
    public override int Stage => MuseumGame.Gather;
    public override string Floor => "2F";
    public override string Room => "atrium";
    public override string Title => "함께 서기";
    protected override bool ShowTeam => false;

    private static readonly Vector2[] Pads = { new Vector2(896, 623), new Vector2(1004, 623), new Vector2(896, 725), new Vector2(1004, 725) };
    protected override Vector2[] Stations => Pads;

    private const int OpOn = 1;
    private const float PadRadius = 22f;

    private readonly int[] onPad = { -1, -1, -1, -1 };   // 사람 → 올라선 발판
    // 사람 → 서야 할 발판. 발판 빛 색으로 주인이 보이는 건 관측자뿐 → 관측자가 "이름 = [번호]"로 알려 준다.
    private readonly int[] assign = new int[4];
    private bool ready, toldAll, observerTipShown;
    private float engagedAt = -1f, wrongToastAt = -99f;
    private string lastWrongKey;
    protected override int KeyRole => MuseumGame.Observer;

    private void Ensure()
    {
        if (ready) return;
        var rng = new System.Random(Story1Net.Seed(Id));
        int[] a = { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); }
        for (int i = 0; i < 4; i++) assign[i] = a[i];
        ready = true;
    }

    private bool AllRight { get { for (int s = 0; s < 4; s++) if (onPad[s] != assign[s]) return false; return true; } }
    private readonly int[] botPad = { -1, -1, -1, -1 };
    private readonly float[] padAt = { -1f, -1f, -1f, -1f };
    private int myLast = -2;
    private float allSince = -1f;
    private float humanStepAt = -1f;

    private GameObject worldRoot;
    private SpriteRenderer[] glow;

    protected override void Awake() { base.Awake(); Ensure(); MuseumGame.Register(this); }

    private int Occupied { get { int n = 0; for (int p = 0; p < 4; p++) if (Who(p) >= 0) n++; return n; } }
    private int Who(int pad) { for (int s = 0; s < 4; s++) if (onPad[s] == pad) return s; return -1; }

    public override string Progress => "발판 위 " + Occupied + "/4";
    public override string GuideLabel => myLast >= 0 ? null : "동상 둘레 발판";
    public override Vector2 GuidePoint => new Vector2(950, 674);
    protected override string PromptAt(int i) => "발판 (네 명이 함께 서요)";
    protected override void OnInteract(int i) { MuseumHud.Toast("발판 위에서 대기 · 네 명이 모두 올라야 열린다", MuseumUI.Amber); }

    public override bool Near(string floor, string room, Vector2 pos, float radius, out string prompt)
    {
        prompt = null;
        return false;   // 발판은 걸어서 올라서기만 하면 된다(E 안내를 띄우지 않는다)
    }

    public override bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!Bot(slot) || MuseumGame.Stage != Stage || botPad[slot] < 0) return false;
        stand = Pads[botPad[slot]] + new Vector2(0, 6);
        return true;
    }

    protected override void OnNet(int op, string value, int actor)
    {
        if (op != OpOn || Done) return;
        int.TryParse(value, out int p);
        onPad[actor] = p >= 0 && p < 4 ? p : -1;
        if (Bot(actor)) botPad[actor] = onPad[actor];   // 방장이 아닌 화면에서도 AI 가 발판으로 걸어가 보이게
        if (p >= 0) Sfx.Play("ui_select", 0.4f);
    }

    public override int[] SnapState()
    {
        var l = new System.Collections.Generic.List<int>();
        Put(l, onPad); Put(l, botPad); Put(l, assign);
        return l.ToArray();
    }

    public override void LoadState(int[] s)
    {
        if (s.Length < 8) return;
        if (s.Length >= 12) CopyIn(s, 8, assign);
        for (int i = 0; i < 4; i++)
        {
            if (i == Me) continue;   // 내 발판은 내가 직접 알린다
            onPad[i] = s[i];
            if (Bot(i)) botPad[i] = s[4 + i];
        }
        // 방장이 내 발판을 모르면(알림을 놓쳤으면) 다시 알린다
        if (Me >= 0 && Me < 4 && s[Me] != myLast && myLast >= -1) myLast = -2;
    }

    protected override void Update()
    {
        base.Update();
        var m = Museum;
        if (m == null || MuseumGame.Stage != Stage || MuseumStory.Busy) return;

        // 내 발판 상태를 알린다(바뀔 때만)
        int mine = -1;
        if (m.CurFloor == Floor && m.CurRoom == Room)
            for (int p = 0; p < 4; p++) if ((m.PlayerMap - Pads[p]).sqrMagnitude < PadRadius * PadRadius) mine = p;
        if (mine >= 0 && Who(mine) >= 0 && Who(mine) != Me) mine = -1;   // 이미 다른 사람이 서 있는 발판
        if (mine != myLast) { myLast = mine; Send(OpOn, mine.ToString()); if (mine >= 0 && humanStepAt < 0f) humanStepAt = Time.time; }
        // 내가 관측자면 한 번 알려 준다: 발판 빛 색 = 그 발판의 주인
        if (!observerTipShown && MuseumGame.IHave(MuseumGame.Observer) && m.CurFloor == Floor && m.CurRoom == Room)
        {
            observerTipShown = true;
            MuseumHud.Toast("관측자 능력 · 발판 빛의 색 = 그 자리 주인 · 동료에게 \"이름 [번호]\"로 알려 주자", MuseumUI.Mint);
        }

        // 넷 다 올라섰는데 주인이 다르면 알려 준다(모든 화면)
        if (Occupied == 4 && !AllRight && Time.time - wrongToastAt > 5f)
        {
            wrongToastAt = Time.time;
            string key = string.Join(",", onPad);   // 같은 자리 그대로 서 있는 동안은 벌점을 한 번만
            if (key != lastWrongKey) { lastWrongKey = key; MuseumGame.Mistake(); }
            Sfx.Play("door_locked", 0.5f);
            MuseumHud.Toast("발판의 주인이 다르다 · 관측자 " + Name(MuseumGame.Observer) + "의 말을 들어 보자", MuseumUI.Amber);
        }
        if (!MuseumNet.Authority || Solved) return;
        if (Occupied == 4 && AllRight)
        {
            if (allSince < 0f) { allSince = Time.time; MuseumHud.Toast("모두 올라섰다 · 그대로 대기", MuseumUI.Mint); Sfx.Play("chime", 0.6f); }
            if (Time.time - allSince >= 2f) { Solved = true; MuseumNet.Mission(Stage); }
        }
        else allSince = -1f;
    }

    protected override void OnChat(int slot, string text)
    {
        // 사람 관측자가 번호를 말하면 AI 들이 자기 발판으로 간다(AI 는 관측자의 말을 알아듣는다고 본다)
        if (slot == MuseumGame.Observer && !Bot(slot) && MuseumNet.Authority && MuseumSignals.Digits(text, 1, 4).Count > 0) toldAll = true;
    }

    protected override void BotThink()
    {
        var m0 = Museum;
        if (engagedAt < 0f && MuseumGame.AnyHumanIn(Floor, Room)) engagedAt = Time.time;
        if (engagedAt < 0f) return;
        // 관측자가 AI 면: 사람들에게 각자 설 발판을 알려 준다(이름 = [번호]). 다시 물으면(?) 또 말한다.
        if (Bot(MuseumGame.Observer) && !toldAll && Time.time - engagedAt > 2f)
        {
            int ob = MuseumGame.Observer;
            var sb = new System.Text.StringBuilder(MuseumVoice.HereW(ob) + "!");
            for (int s = 0; s < 4; s++) if (!Bot(s)) sb.Append("  ").Append(Name(s)).Append(" = [").Append(assign[s] + 1).Append("]");
            BotSay(ob, sb.ToString());
            toldAll = true;
        }
        // 사람 관측자가 오래 말이 없으면(25초) AI 는 스스로 자리를 찾아간다
        if (!toldAll && Time.time - engagedAt > 25f) toldAll = true;
        if (!toldAll) return;
        for (int s = 0; s < 4; s++)
        {
            if (!Bot(s)) continue;
            if (botPad[s] < 0) { if (Time.time - engagedAt > 2.5f + s * 0.9f) { botPad[s] = assign[s]; padAt[s] = Time.time; } continue; }
            if (botPad[s] >= 0)
            {
                // 내 발판에 다른 사람이 서 있으면 옆에서 기다린다(관측자가 바로잡아 주길)
                bool stolen = false;
                for (int o = 0; o < 4; o++) if (o != s && onPad[o] == botPad[s]) stolen = true;
                if (stolen)
                {
                    if (onPad[s] >= 0) Send(OpOn, "-1", s);
                    if (Time.time - wrongToastAt < 0.1f && s == MuseumGame.Observer) BotSay(s, MuseumVoice.NoW(s) + "!  [" + (assign[s] + 1) + "] ×");
                    continue;
                }
                // 도착했으면 발판에 선 것으로 알린다
                var m = Museum;
                bool hostHere = m != null && m.CurFloor == Floor && m.CurRoom == Room;
                if (padAt[s] < 0f) padAt[s] = Time.time;
                if (onPad[s] != botPad[s] && m != null && (hostHere ? m.BotArrived(s) : Time.time - padAt[s] > 2.5f)) { Send(OpOn, botPad[s].ToString(), s); if (s == 1) BotSay(s, MuseumVoice.HereW(s) + "!  ○"); }
                continue;
            }
        }
    }

    protected override void BuildBody(RectTransform b) { }
    protected override void RefreshPanel() { }

    protected override void BuildWorld()
    {
        worldRoot = new GameObject("GatherPads");
        worldRoot.transform.SetParent(transform.parent, false);
        var tex = MakeTex(24, 14, (px, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = (x - 11.5f) / 11.5f, dy = (y - 6.5f) / 6.5f, d = dx * dx + dy * dy;
                if (d <= 1f) Put(px, w, h, x, y, new Color32(255, 255, 255, (byte)(d > 0.6f ? 255 : 120)));
            }
        });
        var sp = MakeSprite(tex);
        glow = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            var g = new GameObject("Pad" + i).AddComponent<SpriteRenderer>();
            g.sprite = sp; g.sortingOrder = Mathf.RoundToInt(Pads[i].y) - 30;
            g.transform.SetParent(worldRoot.transform, false);
            g.transform.position = World(Pads[i]);
            g.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
            glow[i] = g;
        }
        worldRoot.SetActive(false);
    }

    protected override void UpdateWorld()
    {
        if (worldRoot == null) return;
        var m = Museum;
        bool on = m != null && m.CurFloor == Floor && MuseumGame.Stage >= Stage;
        if (worldRoot.activeSelf != on) worldRoot.SetActive(on);
        if (!on) return;
        float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 3f);
        for (int i = 0; i < 4; i++)
        {
            int w = Who(i);
            // 관측자만 비어 있는 발판의 주인 색을 본다(나머지는 노란 빛)
            int owner = System.Array.IndexOf(assign, i);
            Color idle = MuseumGame.IHave(MuseumGame.Observer) && owner >= 0 ? Roles.Color(owner) : new Color(1f, 0.85f, 0.45f);
            idle.a = pulse * (MuseumGame.Stage == Stage ? 1f : 0.3f);
            Color c = Done ? new Color(0.5f, 1f, 0.7f, 0.9f) : w >= 0 ? new Color(Roles.Color(w).r, Roles.Color(w).g, Roles.Color(w).b, 0.9f) : idle;
            glow[i].color = c;
        }
    }
}
