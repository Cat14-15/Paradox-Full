using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미션 4 · 옛 글자 풀기 (2층 기록 아카이브).
///  · 잠긴 보관장에 필리핀 옛 글자 '바이바이인' 세 글자가 새겨져 있다(KA · SA · MA = kasama, 함께하는 친구).
///  · 글자 풀이표(8글자)는 네 조각으로 찢어져 있어 네 사람이 두 글자씩 가지고 있다. 세 글자는 서로 다른 사람에게 흩어져 있다.
///  · 풀이표 조각은 그림이라 채팅으로 보여 줄 수 있다(AI 친구는 그림 + 소리로 알려 준다). 모양을 맞춰 보고 소리 단추를 눌러 채운다.
/// </summary>
public sealed class PuzzleScript : MuseumPuzzle
{
    public override int Id => 13;
    public override int Stage => MuseumGame.Script;
    public override string Floor => "2F";
    public override string Room => "archive";
    public override string Title => "바이바이인 보관장";
    protected override float PanelH => 716f;
    protected override int KeyRole => MuseumGame.Operator;   // 글자 입력은 조작자

    private static readonly Vector2[] Pos = { new Vector2(583, 250), new Vector2(778, 126), new Vector2(588, 124), new Vector2(408, 124) };
    protected override Vector2[] Stations => Pos;
    public static readonly string[] Sound = { "KA", "SA", "MA", "BA", "LA", "TA", "NA", "GA" };
    /// <summary>보관장 낱말 후보(모두 필리핀어). 쉬움은 늘 kasama(이야기와 이어짐), 보통·어려움은 판마다 바뀐다.</summary>
    public static readonly int[][] Words = { new[] { 0, 1, 2 }, new[] { 0, 5, 7 }, new[] { 5, 4, 7 }, new[] { 5, 6, 7 } };   // kasama 함께하는 친구 · kataga 낱말 · talaga 정말 · tanaga 필리핀 전통 시
    public static readonly string[] WordMeaning = { "함께하는 친구", "낱말", "정말", "필리핀 전통 시" };
    /// <summary>이번 판의 낱말 번호(이야기에서 쓴다).</summary>
    public static int WordIndex { get; private set; }
    private int[] Word = Words[0];
    public static string WordText { get { var w = Words[WordIndex]; return Sound[w[0]] + "-" + Sound[w[1]] + "-" + Sound[w[2]]; } }
    public static string WordLower { get { var w = Words[WordIndex]; return (Sound[w[0]] + Sound[w[1]] + Sound[w[2]]).ToLowerInvariant(); } }
    private bool IsTarget(int g) => System.Array.IndexOf(Word, g) >= 0;

    private const int OpPick = 1, OpClear = 2, OpOpened = 3;

    private readonly int[][] piece = new int[4][];   // 사람 → 가진 글자 두 개
    private readonly int[] filled = { -1, -1, -1 };
    private readonly int[] buttonOrder = new int[8];
    private bool ready;
    private int fails;

    private readonly float[] botNext = new float[4];
    private readonly bool[] botSaid = new bool[4];
    private float engagedAt = -1f;

    private readonly Image[] lockGlyph = new Image[3];
    private readonly PixelText[] lockSlot = new PixelText[3];
    private readonly Image[] myGlyph = new Image[2];
    private readonly PixelText[] mySound = new PixelText[2];
    private readonly PxButton[] soundBtn = new PxButton[8];
    private PxButton clearBtn, tellBtn;
    private PixelText hintText;

    private void Ensure()
    {
        if (ready) return;
        var rng = new System.Random(Story1Net.Seed(Id));
        WordIndex = rng.Next(Words.Length);   // 쉬움도 판마다 다른 낱말("KASAMA 만 계속 나와서 찍기 쉽다"는 의견)
        Word = Words[WordIndex];
        for (int tries = 0; tries < 200; tries++)
        {
            int[] g = { 0, 1, 2, 3, 4, 5, 6, 7 };
            for (int i = 7; i > 0; i--) { int j = rng.Next(i + 1); (g[i], g[j]) = (g[j], g[i]); }
            bool ok = true;
            for (int p = 0; p < 4; p++)
            {
                piece[p] = new[] { g[p * 2], g[p * 2 + 1] };
                int targets = (IsTarget(piece[p][0]) ? 1 : 0) + (IsTarget(piece[p][1]) ? 1 : 0);
                if (targets > 1) ok = false;
            }
            if (ok) break;
        }
        int[] o = { 0, 1, 2, 3, 4, 5, 6, 7 };
        for (int i = 7; i > 0; i--) { int j = rng.Next(i + 1); (o[i], o[j]) = (o[j], o[i]); }
        for (int i = 0; i < 8; i++) buttonOrder[i] = o[i];
        ready = true;
    }

    protected override void Awake() { base.Awake(); Ensure(); MuseumGame.Register(this); }

    private static char Glyph(int g) => MuseumEmoji.Char(MuseumEmoji.Baybayin + g);
    private static Sprite GlyphSprite(int g) => MuseumEmoji.Get(MuseumEmoji.Baybayin + g);

    public override string Progress
    {
        get { int n = 0; foreach (int f in filled) if (f >= 0) n++; return "채운 글자 " + n + "/3"; }
    }

    protected override string PromptAt(int i) => i == 0 ? "잠긴 보관장 보기" : "풀이표 조각 보기";

    public override bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!Bot(slot) || MuseumGame.Stage != Stage) return false;
        var m = Museum;
        if (m == null || m.CurFloor != Floor || m.CurRoom != Room) return false;
        stand = Pos[slot] + new Vector2(0, 20);
        return true;
    }

    protected override void OnNet(int op, string value, int actor)
    {
        if (Done) return;
        int.TryParse(value, out int v);
        switch (op)
        {
            case OpPick:
            {
                Engaged = true; if (engagedAt < 0f) engagedAt = Time.time;
                int slot = System.Array.IndexOf(filled, -1);
                if (slot < 0 || v < 0 || v > 7) return;
                filled[slot] = v;
                Sfx.Play("museum_tile_place", 0.45f);
                if (slot == 2)
                {
                    bool right = filled[0] == Word[0] && filled[1] == Word[1] && filled[2] == Word[2];
                    if (right) { if (MuseumNet.Authority) { Solved = true; MuseumNet.Mission(Stage); } }
                    else
                    {
                        fails++;
                        MuseumGame.Mistake();
                        int n = 0; for (int i = 0; i < 3; i++) if (filled[i] == Word[i]) n++;
                        Sfx.Play("door_locked", 0.6f);
                        MuseumHud.Toast(Story1Net.Difficulty == Story1Rules.Easy ? "열리지 않는다 · " + n + "/3 맞음" : "열리지 않는다 · 모양을 다시 비교", MuseumUI.Red);
                        for (int i = 0; i < 3; i++) filled[i] = -1;
                        if (MuseumNet.Authority) for (int s = 0; s < 4; s++) { botSaid[s] = false; botNext[s] = Time.time + 1.5f + s; }
                    }
                }
                break;
            }
            case OpClear:
                for (int i = 0; i < 3; i++) filled[i] = -1;
                break;
            case OpOpened:
                Engaged = true; if (engagedAt < 0f) engagedAt = Time.time;
                break;
        }
    }

    protected override void OnOpened() { if (!Engaged) Send(OpOpened); }

    public override int[] SnapState()
    {
        var l = new System.Collections.Generic.List<int>();
        Put(l, filled); Put(l, buttonOrder); l.Add(fails); l.Add(Engaged ? 1 : 0);
        return l.ToArray();
    }

    public override void LoadState(int[] s)
    {
        if (s.Length < 13) return;
        CopyIn(s, 0, filled); CopyIn(s, 3, buttonOrder); fails = s[11];
        if (s[12] != 0) Engaged = true;
    }

    protected override void OnChat(int slot, string text)
    {
        if (Bot(slot) || !MuseumNet.Authority) return;
        if (MuseumSignals.Normalize(text).Contains("?"))
            for (int s = 0; s < 4; s++) if (Bot(s)) { botSaid[s] = false; botNext[s] = Time.time + 0.8f + s * 0.8f; }
    }

    protected override void BotThink()
    {
        var m = Museum;
        if (!Engaged && MuseumGame.AnyHumanIn(Floor, Room)) { Engaged = true; engagedAt = Time.time; }
        if (!Engaged) return;
        for (int s = 0; s < 4; s++)
        {
            if (!Bot(s) || botSaid[s] || Time.time < botNext[s]) continue;
            if (Time.time - engagedAt < 3f + s * 2.2f) continue;
            string here = s == 0 ? "이거" : s == 1 ? "Ito" : s == 2 ? "Day" : "Nansi";
            BotSay(s, here + ":  " + Glyph(piece[s][0]) + " = " + Sound[piece[s][0]] + "    " + Glyph(piece[s][1]) + " = " + Sound[piece[s][1]]);
            botSaid[s] = true;
        }
    }

    // ---------------------------------------------------------------- 조작창
    protected override void BuildBody(RectTransform b)
    {
        var lockBox = MuseumUI.Node("Lock", b); MuseumUI.Anchor(lockBox, 0f, 1f, 0f, 0f, 520f, 236f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", lockBox)), MuseumUI.FillSprite, new Color(0.18f, 0.13f, 0.08f, 1f));
        MuseumUI.Text(lockBox, "Head", "보관장 자물쇠", 2, MuseumUI.Amber, TextAnchor.MiddleCenter, 0f, 92f, 500f, 36f);
        for (int i = 0; i < 3; i++)
        {
            var g = MuseumUI.Node("G" + i, lockBox); MuseumUI.At(g, -160f + i * 160f, 14f, 104f, 104f);
            lockGlyph[i] = MuseumUI.Img(g, GlyphSprite(Word[i]), new Color(0.98f, 0.88f, 0.62f));
            lockSlot[i] = MuseumUI.Text(lockBox, "S" + i, "_", 3, MuseumUI.Mint, TextAnchor.MiddleCenter, -160f + i * 160f, -78f, 140f, 52f);
        }

        var mine = MuseumUI.Node("Mine", b); MuseumUI.Anchor(mine, 1f, 1f, 0f, 0f, 330f, 236f);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", mine)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.13f, 1f));
        MuseumUI.Text(mine, "Head", "내 풀이표 조각", 2, MuseumUI.Amber, TextAnchor.MiddleCenter, 0f, 92f, 320f, 36f);
        for (int i = 0; i < 2; i++)
        {
            var g = MuseumUI.Node("G" + i, mine); MuseumUI.At(g, -100f, 40f - i * 56f, 52f, 52f);
            myGlyph[i] = MuseumUI.Img(g, null, Color.white);
            mySound[i] = MuseumUI.Text(mine, "S" + i, "", 3, MuseumUI.Ink, TextAnchor.MiddleLeft, 30f, 40f - i * 56f, 200f, 52f);
        }
        tellBtn = MuseumUI.Button(mine, "Tell", "보내기", 2, Tell);
        MuseumUI.Anchor(tellBtn.Rect, 0.5f, 0f, 0f, 10f, 300f, 48f);
        tellBtn.Accent = MuseumUI.Mint;

        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            soundBtn[i] = MuseumUI.Button(b, "Snd" + i, "", 3, () => Pick(buttonOrder[idx]));
            MuseumUI.Anchor(soundBtn[i].Rect, 0f, 1f, (i % 4) * 136f, -250f - (i / 4) * 76f, 126f, 66f);
        }
        clearBtn = MuseumUI.Button(b, "Clear", "지우기", 2, () => Send(OpClear));
        MuseumUI.Anchor(clearBtn.Rect, 1f, 1f, 0f, -250f, 300f, 66f);
        clearBtn.Sound = "ui_back";

        hintText = MakeHint(b);
    }

    protected override string KeysHint => "1~8 소리   Backspace 지우기   T 보내기   X 닫기";

    protected override void OnKeys(UnityEngine.InputSystem.Keyboard kb)
    {
        int d = DigitPressed(kb, 8);
        if (d >= 0) Pick(buttonOrder[d]);
        if (kb.backspaceKey.wasPressedThisFrame) Send(OpClear);
        if (kb.tKey.wasPressedThisFrame) Tell();
    }

    private void Pick(int g) { if (!MuseumGame.CanOperate) { MuseumHud.Toast("글자 입력은 조작자(" + Name(MuseumGame.Operator) + ") 담당 · 소리를 알려 주기", MuseumUI.Amber); return; } if (System.Array.IndexOf(filled, -1) >= 0) Send(OpPick, g.ToString()); }

    private void Tell()
    {
        var p = piece[Me];
        Story1Net.SendChat(Glyph(p[0]) + " = " + Sound[p[0]] + "   " + Glyph(p[1]) + " = " + Sound[p[1]]);
    }

    protected override void RefreshPanel()
    {
        Sub("필리핀 옛 글자 '바이바이인'. 모양을 풀이표와 맞춰 소리 3칸을 채운다.");
        for (int i = 0; i < 3; i++) lockSlot[i].text = filled[i] >= 0 ? Sound[filled[i]] : "_";
        var p = piece[Me];
        for (int i = 0; i < 2; i++)
        {
            myGlyph[i].sprite = GlyphSprite(p[i]);
            mySound[i].text = "= " + Sound[p[i]];
        }
        bool canPick = System.Array.IndexOf(filled, -1) >= 0;
        for (int i = 0; i < 8; i++) { soundBtn[i].SetText(Sound[buttonOrder[i]]); soundBtn[i].Enabled = canPick && MuseumGame.CanOperate; }
        hintText.text = fails > 0 ? "비슷한 모양 주의 · 획을 하나씩 비교" : "동료 조각은 채팅으로 온다 · 내 조각도 공유";
    }

    protected override string TeamState(int slot, out Color color)
    {
        bool has = false; foreach (int g in piece[slot]) if (IsTarget(g)) has = true;
        color = has ? MuseumUI.Amber : MuseumUI.Slate;
        return slot == Me ? "" : botSaid[slot] ? "○" : "";
    }
}
