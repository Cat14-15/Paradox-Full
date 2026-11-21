using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미션 5 · 네 나라 말 사전 (2층 자료 열람실).
///  · 열람 책상의 사전 자물쇠에 낱말 세 개가 적혀 있다: 필리핀어 하나 · 베트남어 하나 · 줄루어 하나.
///  · 뜻(불 · 물 · 별 · 마음 중 하나)은 그 나라 친구(이안 · 린 · 카이)와 통역만 읽을 수 있다.
///  · 뜻을 아는 사람은 그림(이모티콘)으로 알려 주고, 조작자가 칸마다 그림을 골라 자물쇠를 연다.
///  · 한국어 화자(서하린)는 하나도 못 읽는다 → 친구들 말을 듣고 넣는 역할.
/// </summary>
public sealed class PuzzleWords : MuseumPuzzle
{
    public override int Id => 15;
    public override int Stage => MuseumGame.Words;
    public override string Floor => "2F";
    public override string Room => "reading";
    public override string Title => "사전 자물쇠";
    protected override float PanelH => 720f;
    protected override int KeyRole => MuseumGame.Operator;   // 그림을 넣고 여는 건 조작자

    private static readonly Vector2 Desk = new Vector2(264, 722);
    protected override Vector2[] Stations => new[] { Desk };

    // 그림 4개(이모티콘 번호) · 나라별 낱말 [언어 1~3][그림]
    private static readonly int[] Pic = { 0, 1, 7, 8 };                 // 불 · 물 · 별 · 하트
    public static readonly string[] PicName = { "불", "물", "별", "마음" };
    public static readonly string[][] WordOf =
    {
        null,
        new[] { "apoy", "tubig", "bituin", "puso" },                   // 필리핀어
        new[] { "lua", "nuoc", "sao", "tim" },                         // 베트남어
        new[] { "umlilo", "amanzi", "inkanyezi", "inhliziyo" },        // 줄루어
    };

    private const int OpSet = 1, OpTry = 2, OpOpened = 3;

    private readonly int[] meaning = new int[3];        // 칸(0 필리핀어 · 1 베트남어 · 2 줄루어) → 그림 번호(0~3)
    private readonly int[] setting = { -1, -1, -1 };
    private bool ready;
    private float tryLockUntil, engagedAt = -1f;
    private int fails;

    // AI(방장 화면)
    private readonly float[] botNext = new float[4];
    private readonly bool[] botSaid = new bool[4];
    private readonly int[] heard = { -1, -1, -1 };      // 조작자 AI 가 들은 칸의 그림
    private float botTryAt = -1f;

    private readonly PixelText[] langText = new PixelText[3], wordText = new PixelText[3], meanText = new PixelText[3];
    private readonly PxButton[] picBtn = new PxButton[3];
    private readonly Image[] picImg = new Image[3], meanImg = new Image[3];
    private PxButton tryBtn, tellBtn;
    private PixelText hintText;

    private void Ensure()
    {
        if (ready) return;
        var rng = new System.Random(Story1Net.Seed(Id));
        int[] a = { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); }
        for (int i = 0; i < 3; i++) meaning[i] = a[i];
        ready = true;
    }

    protected override void Awake() { base.Awake(); Ensure(); MuseumGame.Register(this); }

    private static int LangOf(int slot) => slot + 1;                 // 칸 0 → 필리핀어(이안 1) …
    private static string Word(int slot, int m) => WordOf[LangOf(slot)][m];
    private static char Emoji(int m) => MuseumEmoji.Char(Pic[m]);

    /// <summary>이 칸의 뜻을 내가 읽을 수 있나: 내 나라 말이거나 통역(이안)이거나.</summary>
    private bool CanRead(int slot) => LangOf(slot) == Me || MuseumGame.IHave(MuseumGame.Interpreter);

    private int Filled { get { int n = 0; foreach (int s in setting) if (s >= 0) n++; return n; } }
    public override string Progress => "넣은 그림 " + Filled + "/3";
    protected override string PromptAt(int i) => "사전 자물쇠 보기";

    public override bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!Bot(slot) || MuseumGame.Stage != Stage) return false;
        var m = Museum;
        if (m == null || m.CurFloor != Floor || m.CurRoom != Room) return false;
        stand = Desk + new Vector2(-60f + slot * 40f, 34f);
        return true;
    }

    // ---------------------------------------------------------------- 상태
    protected override void OnNet(int op, string value, int actor)
    {
        if (Done) return;
        switch (op)
        {
            case OpOpened:
                Engaged = true; if (engagedAt < 0f) engagedAt = Time.time;
                break;
            case OpSet:
            {
                int sep = value.IndexOf(':');
                if (sep < 0) return;
                int.TryParse(value.Substring(0, sep), out int s);
                int.TryParse(value.Substring(sep + 1), out int m);
                if (s < 0 || s > 2) return;
                setting[s] = Mathf.Clamp(m, -1, 3);
                if (actor == Me) Sfx.Play("museum_tile_place", 0.45f);
                break;
            }
            case OpTry:
            {
                if (Time.time < tryLockUntil - 0.2f) return;
                int right = 0; for (int s = 0; s < 3; s++) if (setting[s] == meaning[s]) right++;
                if (right == 3) { if (MuseumNet.Authority) { Solved = true; MuseumNet.Mission(Stage); } break; }
                fails++;
                int diff = Story1Net.Difficulty;
                MuseumGame.Mistake();
                tryLockUntil = Time.time + (diff == Story1Rules.Easy ? 3f : diff == Story1Rules.Normal ? 6f : 10f);
                Sfx.Play("door_locked", 0.6f);
                MuseumHud.Toast(diff == Story1Rules.Easy ? "자물쇠가 안 열린다 · " + right + "/3 맞음" : "자물쇠가 안 열린다 · 친구들에게 다시 물어보자 (?)", MuseumUI.Red);
                if (MuseumNet.Authority) { for (int s = 0; s < 4; s++) { botSaid[s] = false; botNext[s] = Time.time + 1.5f + s; } for (int k = 0; k < 3; k++) heard[k] = -1; botTryAt = -1f; }
                break;
            }
        }
    }

    protected override void OnOpened() { if (!Engaged) Send(OpOpened); }

    public override int[] SnapState()
    {
        var l = new System.Collections.Generic.List<int>();
        Put(l, meaning); Put(l, setting); l.Add(fails); l.Add(Engaged ? 1 : 0);
        return l.ToArray();
    }

    public override void LoadState(int[] s)
    {
        if (s.Length < 8) return;
        CopyIn(s, 0, meaning); CopyIn(s, 3, setting); fails = s[6];
        if (s[7] != 0) Engaged = true;
    }

    // ---------------------------------------------------------------- 채팅(AI 조작자가 듣는 것)
    protected override void OnChat(int slot, string text)
    {
        if (!MuseumNet.Authority || string.IsNullOrEmpty(text)) return;
        string low = text.ToLowerInvariant();
        if (!Bot(slot) && MuseumSignals.Normalize(text).Contains("?"))
            for (int s = 0; s < 4; s++) if (Bot(s)) { botSaid[s] = false; botNext[s] = Time.time + 0.8f + s * 0.7f; }
        // "낱말 = 그림": 그 칸의 그림으로 믿는다(틀리게 말하면 틀리게 넣는다)
        for (int k = 0; k < 3; k++)
        {
            int at = -1;
            for (int m = 0; m < 4 && at < 0; m++) { string w = Word(k, m); int i = low.IndexOf(w, System.StringComparison.Ordinal); if (i >= 0 && w == Word(k, meaning[k])) at = i; }
            if (at < 0) continue;
            for (int m = 0; m < 4; m++) if (text.IndexOf(Emoji(m)) >= 0) { heard[k] = m; break; }
        }
    }

    // ---------------------------------------------------------------- AI
    protected override void BotThink()
    {
        if (!Engaged && MuseumGame.AnyHumanIn(Floor, Room)) { Engaged = true; engagedAt = Time.time; }
        if (!Engaged) return;
        // 1) 자기 나라 말 칸을 그림으로 알려 준다(통역 AI 도 자기 칸만 — 능력은 그 역할만)
        for (int s = 1; s < 4; s++)
        {
            if (!Bot(s) || botSaid[s] || Time.time < botNext[s]) continue;
            if (Time.time - engagedAt < 2f + s * 1.6f) continue;
            int k = s - 1;
            BotSay(s, MuseumVoice.HereW(s) + "!   " + Word(k, meaning[k]) + " = " + Emoji(meaning[k]));
            heard[k] = meaning[k];
            botSaid[s] = true;
        }
        // 2) 조작자가 AI 면: 세 칸을 다 들으면 넣고 연다
        if (!Bot(MuseumGame.Operator)) return;
        int op = MuseumGame.Operator;
        for (int k = 0; k < 3; k++) if (heard[k] < 0) return;
        for (int k = 0; k < 3; k++) if (setting[k] != heard[k]) { Send(OpSet, k + ":" + heard[k], op); return; }
        if (Time.time < tryLockUntil) return;
        if (botTryAt < 0f) botTryAt = Time.time + 1.2f;
        else if (Time.time > botTryAt) { botTryAt = Time.time + 4f; Send(OpTry, "", op); BotSay(op, MuseumVoice.YesW(op) + "!  ○"); }
    }

    // ---------------------------------------------------------------- 조작창
    protected override void BuildBody(RectTransform b)
    {
        string[] lang = { "필리핀어", "베트남어", "줄루어" };
        for (int k = 0; k < 3; k++)
        {
            int slot = k;
            var col = MuseumUI.Node("Col" + k, b); MuseumUI.Anchor(col, 0f, 1f, k * 292f, 0f, 280f, 262f);
            MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", col)), MuseumUI.FillSprite, new Color(0.16f, 0.12f, 0.08f, 1f));
            langText[k] = MuseumUI.Text(col, "Lang", lang[k], 2, MuseumUI.Slate, TextAnchor.MiddleCenter, 0f, 108f, 270f, 32f);
            wordText[k] = MuseumUI.Text(col, "Word", "", 3, new Color(0.96f, 0.86f, 0.6f), TextAnchor.MiddleCenter, 0f, 66f, 270f, 52f);
            wordText[k].maxWidth = 266f;
            picBtn[k] = MuseumUI.Button(col, "Pic", "", 2, () => Cycle(slot)); MuseumUI.At(picBtn[k].Rect, 0f, -26f, 108f, 108f);
            var pi = MuseumUI.Node("Img", picBtn[k].Rect); MuseumUI.At(pi, 0f, 0f, 72f, 72f);
            picImg[k] = MuseumUI.Img(pi, null, Color.white);
            meanText[k] = MuseumUI.Text(col, "Mean", "", 2, MuseumUI.Mint, TextAnchor.MiddleLeft, -20f, -106f, 220f, 36f);
            meanText[k].maxWidth = 220f;
            var mi = MuseumUI.Node("MeanImg", col); MuseumUI.At(mi, 96f, -106f, 34f, 34f);
            meanImg[k] = MuseumUI.Img(mi, null, Color.white);
        }
        tellBtn = MuseumUI.Button(b, "Tell", "아는 뜻 보내기", 2, Tell);
        MuseumUI.Anchor(tellBtn.Rect, 0f, 0f, 0f, 48f, 420f, 56f);
        tellBtn.Accent = MuseumUI.Mint;
        tryBtn = MuseumUI.Button(b, "Try", "자물쇠 열기", 2, TryOpen);
        MuseumUI.Anchor(tryBtn.Rect, 1f, 0f, 0f, 48f, 420f, 56f);
        tryBtn.Accent = MuseumUI.Mint;
        hintText = MakeHint(b);
    }

    protected override string KeysHint => "1~3 그림 바꾸기   Space 열기   T 보내기   X 닫기";

    protected override void OnKeys(UnityEngine.InputSystem.Keyboard kb)
    {
        int d = DigitPressed(kb, 3);
        if (d >= 0) Cycle(d);
        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) TryOpen();
        if (kb.tKey.wasPressedThisFrame) Tell();
    }

    private void Cycle(int k)
    {
        if (!MuseumGame.CanOperate) { MuseumHud.Toast("그림 넣기는 조작자(" + Name(MuseumGame.Operator) + ") 담당 · 아는 뜻을 알려 주기", MuseumUI.Amber); return; }
        int next = setting[k] + 1; if (next > 3) next = 0;
        Send(OpSet, k + ":" + next);
    }

    private void TryOpen()
    {
        if (!MuseumGame.CanOperate || Filled < 3 || Time.time < tryLockUntil) return;
        Send(OpTry);
    }

    private void Tell()
    {
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < 3; k++)
        {
            if (!CanRead(k)) continue;
            if (sb.Length > 0) sb.Append("   ");
            sb.Append(Word(k, meaning[k])).Append(" = ").Append(Emoji(meaning[k]));
        }
        if (sb.Length == 0) { Story1Net.SendChat("? " + Word(0, meaning[0]) + " " + Word(1, meaning[1]) + " " + Word(2, meaning[2])); return; }   // 못 읽으면 물어본다
        Story1Net.SendChat(sb.ToString());
    }

    protected override void RefreshPanel()
    {
        bool any = false;
        for (int k = 0; k < 3; k++)
        {
            wordText[k].text = Word(k, meaning[k]).ToUpperInvariant();
            picImg[k].sprite = setting[k] >= 0 ? MuseumEmoji.Get(Pic[setting[k]]) : null;
            picImg[k].color = setting[k] >= 0 ? Color.white : new Color(1f, 1f, 1f, 0f);
            picBtn[k].SetText(setting[k] >= 0 ? "" : "?");
            picBtn[k].Enabled = MuseumGame.CanOperate;
            bool read = CanRead(k); any |= read;
            meanText[k].text = read ? "뜻 = " + PicName[meaning[k]] : Name(LangOf(k)) + "만 읽는다";
            meanText[k].color = read ? MuseumUI.Mint : MuseumUI.Slate;
            meanImg[k].sprite = read ? MuseumEmoji.Get(Pic[meaning[k]]) : null;
            meanImg[k].color = read ? Color.white : new Color(1f, 1f, 1f, 0f);
            langText[k].color = LangOf(k) == Me ? MuseumUI.Amber : MuseumUI.Slate;
        }
        bool cool = Time.time < tryLockUntil;
        tellBtn.SetText(any ? "아는 뜻 보내기" : "뜻 물어보기 ?");
        tryBtn.Enabled = MuseumGame.CanOperate && Filled == 3 && !cool;
        tryBtn.SetText(!MuseumGame.CanOperate ? "열기는 조작자 담당" : cool ? "잠깐 · " + Mathf.CeilToInt(tryLockUntil - Time.time) + "초" : "자물쇠 열기");
        Sub(MuseumGame.CanOperate
            ? "친구가 알려 준 그림을 칸에 넣고 연다. 모르는 낱말은 ? 로 묻기."
            : any ? "내가 읽을 수 있는 낱말의 뜻을 그림으로 알려 주자." : "이 낱말들은 못 읽는다. 친구들의 그림을 보고 조작자를 돕자.");
        hintText.text = fails > 0 ? "같은 그림도 나라마다 부르는 말이 다르다" : "불 · 물 · 별 · 마음 중 셋";
    }

    protected override string TeamState(int slot, out Color color)
    {
        if (slot == 0) { color = MuseumUI.Slate; return ""; }
        bool said = botSaid[slot];
        color = said ? MuseumUI.Mint : MuseumUI.Slate;
        return said ? "○" : "";
    }
}
