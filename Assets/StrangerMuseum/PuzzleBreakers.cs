using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미션 2 · 불 켜기 (지하 전기실).
///  · 배전반에 차단기 4개(▲ ● ■ ★). 차단기마다 주인이 있다 — 네 사람이 각자 "자기 차단기" 하나만 올릴 수 있다.
///  · 정해진 순서대로 올려야 불이 켜진다. 순서를 틀리면 전부 "퍽" 하고 내려간다.
///  · 순서는 네 칸이고, 네 사람이 한 칸씩만 안다(누가 몇 번째를 아는지는 판마다 다르다).
///    → 서로 아는 칸을 알려 주고, 자기 차례가 오면 자기 차단기를 올린다. 한 명이라도 빠지면 켤 수 없다.
///  · AI 친구는 자기 나라 말의 '몇 번째' 낱말 + 모양으로 알려 준다(예: "Ikalawa ●" = 둘째는 ●).
///    앞 미션에서 배운 숫자(dalawa=2, hai=2, kubili=2)로 짐작할 수 있다. 틀리거나 ? 를 보내면 손가락 숫자([2])까지 붙여 다시 말한다.
///    AI 는 사람이 아는 칸을 들어야 순서를 다 안다 → 사람도 자기 칸을 알려 줘야 한다.
/// </summary>
public sealed class PuzzleBreakers : MuseumPuzzle
{
    public override int Id => 11;
    public override int Stage => MuseumGame.Breakers;
    public override string Floor => "B1";
    public override string Room => "power";
    public override string Title => "전기실 배전반";
    protected override float PanelH => 666f;
    protected override int KeyRole => MuseumGame.Interpreter;   // "첫째·둘째"를 그 나라 말로 듣는다

    private static readonly Vector2[] Pos = { new Vector2(1824, 912), new Vector2(1736, 900), new Vector2(1958, 944), new Vector2(1842, 982) };
    protected override Vector2[] Stations => Pos;
    public static readonly string[] Shape = { "▲", "●", "■", "★" };

    private const int OpPress = 1, OpAsk = 3, OpKnown = 4;

    private readonly int[] order = new int[4];      // 칸(0~3) → 모양
    private readonly int[] knowsStep = new int[4];  // 사람 → 아는 칸
    private readonly int[] owner = new int[4];      // 모양 → 주인(사람)
    private readonly int[] layout = new int[4];     // 배전반 자리(왼쪽부터) → 모양
    private int pressed;                            // 올라간 차단기 수
    private readonly bool[] up = new bool[4];       // 모양 → 올라감
    private readonly bool[] teamKnows = new bool[4];// 칸 → AI 들이 이 칸을 안다(말해진 칸)
    private bool ready;
    private float failAt = -99f;
    private int fails;

    private readonly float[] botNext = new float[4];
    private readonly int[] botTold = new int[4];    // 0 아직 · 1 말만 · 2 숫자까지
    private readonly bool[] askedAgain = new bool[4];
    private float engagedAt = -1f, lastPressAt = -1f, askedAt = -99f, cuedAt = -99f, botPressAt = -99f;

    private PixelText clueText, mineText, orderText, hintText;
    private readonly PxButton[] breakers = new PxButton[4];
    private readonly Image[] leverImg = new Image[4];
    private readonly PixelText[] ownerText = new PixelText[4];
    private PxButton tellBtn;

    private void Ensure()
    {
        if (ready) return;
        var rng = new System.Random(Story1Net.Seed(Id));
        int[] a = { 0, 1, 2, 3 }; Shuffle(rng, a); for (int i = 0; i < 4; i++) order[i] = a[i];
        int[] b = { 0, 1, 2, 3 }; Shuffle(rng, b); for (int i = 0; i < 4; i++) knowsStep[i] = b[i];
        int[] c = { 0, 1, 2, 3 }; Shuffle(rng, c); for (int i = 0; i < 4; i++) layout[i] = c[i];
        int[] d = { 0, 1, 2, 3 }; Shuffle(rng, d); for (int i = 0; i < 4; i++) owner[i] = d[i];
        ready = true;
    }

    private static void Shuffle(System.Random rng, int[] a) { for (int i = a.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); } }

    protected override void Awake() { base.Awake(); Ensure(); MuseumGame.Register(this); }

    private int ShapeOf(int slot) { for (int s = 0; s < 4; s++) if (owner[s] == slot) return s; return -1; }

    public override string Progress => "올라간 차단기 " + pressed + "/4" + (fails > 0 ? "   ·   다시 시도 " + fails : "");

    protected override string PromptAt(int i) => "배전반 열기";

    public override bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!Bot(slot) || MuseumGame.Stage != Stage) return false;
        var m = Museum;
        if (m == null || m.CurFloor != Floor || m.CurRoom != Room) return false;
        stand = Pos[slot] + new Vector2(0, 14);
        return true;
    }

    // ---------------------------------------------------------------- 상태
    protected override void OnNet(int op, string value, int actor)
    {
        if (Done) return;
        int.TryParse(value, out int v);
        switch (op)
        {
            case OpPress:
                if (v < 0 || v > 3 || up[v] || owner[v] != actor) return;   // 시간 조건은 보내는 쪽에서만(화면마다 시계가 달라 상태가 갈라진다)
                Engaged = true; if (engagedAt < 0f) engagedAt = Time.time;
                lastPressAt = Time.time;
                if (order[pressed] == v)
                {
                    up[v] = true; teamKnows[pressed] = true; pressed++;
                    if (pressed < 4) Sfx.Play("museum_switch", 0.65f);
                    if (pressed >= 4 && MuseumNet.Authority) { Solved = true; MuseumNet.Mission(Stage); }
                }
                else
                {
                    for (int i = 0; i < 4; i++) up[i] = false;
                    pressed = 0; failAt = Time.time; fails++;
                    MuseumGame.Mistake();
                    Sfx.Play("power_cut", 0.6f);
                    MuseumHud.Toast("퍽! 순서가 틀려 차단기가 전부 내려갔다 · 처음부터", MuseumUI.Red);
                    if (MuseumNet.Authority) for (int s = 0; s < 4; s++) if (Bot(s)) { botTold[s] = Mathf.Min(botTold[s], 1); botNext[s] = Time.time + 1.6f + s * 0.7f; }
                }
                break;
            case OpAsk:
                Engaged = true; if (engagedAt < 0f) engagedAt = Time.time;
                break;
            case OpKnown:
                if (v >= 0 && v < 4) teamKnows[v] = true;
                break;
        }
    }

    protected override void OnOpened() { if (!Engaged) Send(OpAsk); }

    public override int[] SnapState()
    {
        var l = new System.Collections.Generic.List<int>();
        Put(l, order); Put(l, knowsStep); Put(l, owner); Put(l, layout); Put(l, up); Put(l, teamKnows);
        l.Add(pressed); l.Add(fails); l.Add(Engaged ? 1 : 0);
        return l.ToArray();
    }

    public override void LoadState(int[] s)
    {
        if (s.Length < 27) return;
        CopyIn(s, 0, order); CopyIn(s, 4, knowsStep); CopyIn(s, 8, owner); CopyIn(s, 12, layout); CopyIn(s, 16, up); CopyIn(s, 20, teamKnows);
        pressed = Mathf.Clamp(s[24], 0, 4); fails = s[25];
        if (s[26] != 0) Engaged = true;
    }

    protected override void OnChat(int slot, string text)
    {
        if (Bot(slot) || !MuseumNet.Authority) return;
        string t = MuseumSignals.Normalize(text);
        // "[2] ●" / "2 ●": 몇째 칸이 어떤 모양인지. AI 는 들은 대로 믿는다(틀리게 말하면 틀리게 누른다).
        int k = -1, sh = -1;
        foreach (char c in t)
        {
            if (c >= '1' && c <= '4' && k < 0) k = c - '1';
            int si = System.Array.IndexOf(Shape, c.ToString());
            if (si >= 0 && sh < 0) sh = si;
        }
        if (k >= 0 && sh >= 0)
        {
            if (order[k] == sh) Send(OpKnown, k.ToString());
            return;
        }
        if (t.Contains("?"))
            for (int s = 0; s < 4; s++) if (Bot(s) && !up[order[knowsStep[s]]]) { botTold[s] = 1; askedAgain[s] = true; botNext[s] = Time.time + 0.8f + s * 0.6f; }
    }

    // ---------------------------------------------------------------- AI
    protected override void BotThink()
    {
        var m = Museum;
        if (!Engaged && MuseumGame.AnyHumanIn(Floor, Room)) { Engaged = true; engagedAt = Time.time; }
        if (!Engaged) return;
        // 1) 자기가 아는 칸을 말한다(처음엔 자기 말 + 모양, 오래 걸리거나 틀리면 손가락 숫자까지)
        for (int s = 0; s < 4; s++)
        {
            if (!Bot(s) || Time.time < botNext[s]) continue;
            int step = knowsStep[s];
            if (Time.time - engagedAt < 2f + s * 1.2f) continue;
            int diff = Story1Net.Difficulty;
            if (botTold[s] == 0 && diff == Story1Rules.Easy)
            {
                // 쉬움: 처음부터 손가락 숫자까지
                BotSay(s, MuseumVoice.Order(s, step + 1) + " = [" + (step + 1) + "]  " + Shape[order[step]]);
                Send(OpKnown, step.ToString(), s);
                botTold[s] = 2; botNext[s] = Time.time + 20f;
            }
            else if (botTold[s] == 0)
            {
                BotSay(s, MuseumVoice.Order(s, step + 1) + "!  " + Shape[order[step]]);
                Send(OpKnown, step.ToString(), s);
                botTold[s] = 1; botNext[s] = Time.time + 12f;
            }
            else if (botTold[s] == 1 && diff == Story1Rules.Hard && !askedAgain[s]) botNext[s] = Time.time + 1f;   // 어려움: 숫자는 ? 로 다시 물어야 보여 준다
            else if (botTold[s] == 1 && (fails > 0 || askedAgain[s] || Time.time - engagedAt > 26f + s * 2f))
            {
                BotSay(s, MuseumVoice.Order(s, step + 1) + " = [" + (step + 1) + "]  " + Shape[order[step]]);
                botTold[s] = 2; botNext[s] = Time.time + 20f;
            }
            else botNext[s] = Time.time + 1f;
        }
        if (pressed >= 4 || Time.time - failAt < 2f) return;
        // 2) 다음 칸을 아무도 말하지 않았으면 물어본다
        if (!teamKnows[pressed])
        {
            if (Time.time - engagedAt > 14f && Time.time - askedAt > 16f)
            {
                int asker = -1; for (int s = 1; s < 4 && asker < 0; s++) if (Bot(s)) asker = s;
                if (asker >= 0) { BotSay(asker, MuseumVoice.WhatW(asker).TrimEnd('?') + "… [" + (pressed + 1) + "] ?"); askedAt = Time.time; }
            }
            return;
        }
        // 3) 다음 칸의 주인이 AI 면 올리고, 사람이면 이름을 불러 준다
        int next = order[pressed];
        int who = owner[next];
        if (Bot(who))
        {
            if (Time.time - Mathf.Max(lastPressAt, engagedAt) > 1.6f && Time.time - botPressAt > 1f)
            {
                botPressAt = Time.time;   // 온라인: 이벤트가 돌아오기 전에 매 프레임 다시 보내지 않게
                Send(OpPress, next.ToString(), who);
                if (pressed == 0) BotSay(who, MuseumVoice.MineW(who) + "! " + Shape[next]);
            }
        }
        else if (Time.time - cuedAt > 14f && Time.time - Mathf.Max(lastPressAt, engagedAt) > 4f)
        {
            int caller = -1; for (int s = 1; s < 4 && caller < 0; s++) if (Bot(s)) caller = s;
            if (caller >= 0) { BotSay(caller, Name(who) + "! " + Shape[next] + "  " + MuseumVoice.YoursW(caller) + "!"); cuedAt = Time.time; }
        }
    }

    // ---------------------------------------------------------------- 조작창
    protected override void BuildBody(RectTransform b)
    {
        var board = MuseumUI.Node("Board", b); MuseumUI.Anchor(board, 0f, 1f, 0f, 0f, 520f, 330f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", board)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.13f, 1f));
        MuseumUI.Text(board, "Head", "배전반", 2, MuseumUI.Amber, TextAnchor.MiddleCenter, 0f, 135f, 500f, 40f);
        for (int i = 0; i < 4; i++)
        {
            int slotIndex = i;
            breakers[i] = MuseumUI.Button(board, "Breaker" + i, "", 3, () => Press(slotIndex));
            MuseumUI.At(breakers[i].Rect, -180f + i * 120f, -4f, 104f, 210f);
            var slotBg = MuseumUI.Node("Slot", breakers[i].Rect); MuseumUI.At(slotBg, 0f, -24f, 20f, 100f);
            MuseumUI.Img(slotBg, null, new Color(0.03f, 0.03f, 0.05f, 1f));
            var lever = MuseumUI.Node("Lever", breakers[i].Rect); MuseumUI.At(lever, 0f, -50f, 52f, 40f);
            leverImg[i] = MuseumUI.Img(lever, null, new Color(0.35f, 0.35f, 0.38f));
            breakers[i].Label.rectTransform.offsetMin = new Vector2(6f, 136f);
            ownerText[i] = MuseumUI.Text(board, "Owner" + i, "", 2, MuseumUI.Slate, TextAnchor.MiddleCenter, -180f + i * 120f, -136f, 116f, 32f);
        }

        var right = MuseumUI.Node("Right", b); MuseumUI.Anchor(right, 1f, 1f, 0f, 0f, 330f, 330f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", right)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.13f, 1f));
        MuseumUI.Text(right, "ClueHead", "알려 줄 순서", 2, MuseumUI.Amber, TextAnchor.MiddleCenter, 0f, 135f, 320f, 36f);
        clueText = MuseumUI.Text(right, "Clue", "", 4, MuseumUI.Mint, TextAnchor.MiddleCenter, 0f, 50f, 320f, 72f);
        mineText = MuseumUI.Text(right, "Mine", "", 2, MuseumUI.Slate, TextAnchor.MiddleCenter, 0f, -30f, 320f, 36f);
        tellBtn = MuseumUI.Button(right, "Tell", "보내기", 2, Tell); MuseumUI.At(tellBtn.Rect, 0f, -122f, 290f, 56f);
        tellBtn.Accent = MuseumUI.Mint;
        orderText = MuseumUI.Text(right, "Order", "", 1, MuseumUI.Slate, TextAnchor.MiddleCenter, 0f, -400f, 10f, 10f);
        orderText.gameObject.SetActive(false);

        hintText = MakeHint(b);
    }

    protected override string KeysHint => "1~4 차단기 올리기   T 보내기   X 닫기";

    protected override void OnKeys(UnityEngine.InputSystem.Keyboard kb)
    {
        int d = DigitPressed(kb, 4);
        if (d >= 0) Press(d);
        if (kb.tKey.wasPressedThisFrame) Tell();
    }

    private void Press(int place)
    {
        int shape = layout[place];
        if (up[shape] || Time.time - failAt < 1.2f) return;
        if (owner[shape] != Me) { MuseumHud.Toast(Shape[shape] + " 차단기는 " + Name(owner[shape]) + " 담당", MuseumUI.Amber); return; }
        Send(OpPress, shape.ToString());
    }

    private void Tell()
    {
        int st = knowsStep[Me];
        Story1Net.SendChat("[" + (st + 1) + "] " + Shape[order[st]]);
    }

    protected override void RefreshPanel()
    {
        Sub("차단기는 1인 1개. 순서는 넷이 한 칸씩 안다. 내 차례에 내 것을 올린다.");
        int mine = ShapeOf(Me);
        for (int i = 0; i < 4; i++)
        {
            int sh = layout[i];
            bool own = owner[sh] == Me;
            breakers[i].SetText(Shape[sh]);
            breakers[i].Selected = up[sh];
            breakers[i].Enabled = own && !up[sh] && Time.time - failAt >= 1.2f;
            breakers[i].Accent = own ? MuseumUI.Amber : MuseumUI.Slate;
            leverImg[i].rectTransform.anchoredPosition = new Vector2(0f, up[sh] ? 6f : -50f);
            leverImg[i].color = up[sh] ? MuseumUI.Mint : own ? MuseumUI.Amber : new Color(0.38f, 0.38f, 0.42f);
            ownerText[i].text = own ? "나" : Name(owner[sh]);
            ownerText[i].color = own ? MuseumUI.Amber : Roles.Color(owner[sh]);
        }
        mineText.text = "내 차단기 " + (mine >= 0 ? Shape[mine] : "-");
        int st = knowsStep[Me];
        clueText.text = (st + 1) + "번째  " + Shape[order[st]];
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 4; i++) { if (i > 0) sb.Append(' '); sb.Append(i < pressed ? Shape[order[i]] : "_"); }
        orderText.text = sb.ToString();
        bool myTurn = pressed < 4 && order[pressed] == mine && teamKnowsOrMine(pressed);
        if (Time.time - failAt < 2.5f) { hintText.text = "순서 틀림 · 처음부터 다시"; hintText.color = MuseumUI.Red; }
        else if (pressed < 4 && teamKnowsOrMine(pressed) && Story1Net.Difficulty == Story1Rules.Easy)
        {
            // 다음 칸을 누군가 알려 줬으면 차례를 크게 보여 준다
            int who = owner[order[pressed]];
            hintText.text = "올라감 " + pressed + "/4  ·  다음: " + (myTurn ? "내 차례! " + Shape[mine] + " 올리기" : Name(who) + " 차례");
            hintText.color = myTurn ? MuseumUI.Amber : MuseumUI.Ink;
        }
        else if (pressed < 4 && Story1Net.Difficulty != Story1Rules.Easy) { hintText.text = "올라감 " + pressed + "/4  ·  친구들의 말을 읽고 내 차례를 맞히자 (수첩 N · ? 카드)"; hintText.color = MuseumUI.Slate; }
        else { hintText.text = "올라감 " + pressed + "/4  ·  " + (pressed == 0 ? "첫째 칸을 아는 사람은? (모르는 말은 N 수첩)" : "다음 칸은? (? 카드로 묻기)"); hintText.color = MuseumUI.Slate; }
    }

    private bool teamKnowsOrMine(int step) => teamKnows[step] || knowsStep[Me] == step;

    protected override string TeamState(int slot, out Color color)
    {
        int sh = ShapeOf(slot);
        color = sh >= 0 && up[sh] ? MuseumUI.Mint : MuseumUI.Slate;
        return sh >= 0 ? Shape[sh] + (up[sh] ? "○" : "") : "";
    }
}
