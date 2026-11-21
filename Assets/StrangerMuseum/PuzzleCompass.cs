using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미션 3 · 나침반 잠금 (1층 네 전시실 → 중앙 복도).
///  · 네 전시실 벽에 명판이 하나씩. 명판에는 "방위 낱말 = 모양"이 그 나라 말로 적혀 있다.
///    생활사(필리핀어 → 이안) · 역사 연표(베트남어 → 린) · 공예(줄루어 → 카이) · 근현대 도시(한국어 → 서하린).
///    자기 말 명판은 뜻까지 보이고, 남의 말 명판은 글자만 보인다.
///  · 복도 나침반에는 네 방위가 네 나라 말로 새겨져 있다. 친구가 "Hilaga = ★" 라고 하면, 나침반에서 hilaga 를 찾아 ★ 을 맞춘다.
///  · 네 방위를 다 맞추고 "돌리기"를 누르면 2층 계단 셔터가 열린다.
/// </summary>
public sealed class PuzzleCompass : MuseumPuzzle
{
    public override int Id => 12;
    public override int Stage => MuseumGame.Compass;
    public override string Floor => "1F";
    public override string Room => "hall";
    public override string Title => "복도 나침반";
    protected override float PanelH => 726f;
    protected override int KeyRole => MuseumGame.Interpreter;   // 남의 나라 명판을 읽는다

    private static readonly Vector2 Dial = new Vector2(950, 1296);
    private static readonly string[] PlaqueRoom = { "life", "time", "craft", "city" };
    private static readonly string[] PlaqueRoomName = { "생활사 전시실", "역사 연표 전시실", "공예·기술 전시실", "근현대 도시 전시실" };
    private static readonly Vector2[] PlaquePos = { new Vector2(530, 1034), new Vector2(1367, 1034), new Vector2(532, 1368), new Vector2(1368, 1368) };
    private static readonly int[] PlaqueReader = { 1, 2, 3, 0 };
    protected override Vector2[] Stations => new[] { Dial };

    private const int OpRead = 1, OpSet = 2, OpTry = 3;

    private readonly int[] plaqueDir = new int[4];   // 명판 → 방위
    private readonly int[] answer = new int[4];      // 방위 → 모양
    private readonly int[] setting = { -1, -1, -1, -1 };
    private readonly bool[] reported = new bool[4];  // 명판 → 누군가 읽어 알렸다
    private bool ready;
    private int viewPlaque = -1;                      // 조작창이 명판을 보고 있다(-1 = 나침반)
    private float tryLockUntil;                       // 틀린 뒤 나침반이 식는 동안 다시 못 돌린다

    private readonly float[] botNext = new float[4];
    private readonly bool[] botSaid = new bool[4];
    private float stageAt = -1f;

    private RectTransform dialView, plaqueView;
    private readonly PxButton[] dirBtn = new PxButton[4];
    private readonly PixelText[] dirHead = new PixelText[4], dirWords = new PixelText[4];
    private PxButton tryBtn, tellBtn;
    private PixelText hintText, plaqueTitle, plaqueBig, plaqueMean, plaqueNote;

    private void Ensure()
    {
        if (ready) return;
        var rng = new System.Random(Story1Net.Seed(Id));
        int[] a = { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); }
        for (int i = 0; i < 4; i++) plaqueDir[i] = a[i];
        int[] b = { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--) { int j = rng.Next(i + 1); (b[i], b[j]) = (b[j], b[i]); }
        for (int i = 0; i < 4; i++) answer[i] = b[i];
        ready = true;
    }

    protected override void Awake() { base.Awake(); Ensure(); MuseumGame.Register(this); }

    private int Reported { get { int n = 0; for (int i = 0; i < 4; i++) if (reported[i]) n++; return n; } }
    public override string Progress => "모은 명판 " + Reported + "/4";

    // 이 퍼즐은 복도 나침반 + 네 전시실 명판, 두 종류의 조작 지점이 있다
    public override bool Near(string floor, string room, Vector2 pos, float radius, out string prompt)
    {
        prompt = null;
        if (floor != Floor || MuseumGame.Stage < Stage) return false;
        if (room == Room && (Dial - pos).sqrMagnitude < radius * radius) { prompt = Done ? "나침반 (열림)" : "나침반 맞추기"; return true; }
        int p = System.Array.IndexOf(PlaqueRoom, room);
        if (p >= 0 && (PlaquePos[p] - pos).sqrMagnitude < (radius + 14f) * (radius + 14f)) { prompt = "명판 읽기"; return true; }
        return false;
    }

    public override bool Interact(string floor, string room, Vector2 pos, float radius)
    {
        if (!Near(floor, room, pos, radius, out _)) return false;
        int p = System.Array.IndexOf(PlaqueRoom, room);
        if (room == Room)
        {
            if (Done) { MuseumHud.Toast("나침반 고정됨 · 2층 셔터 열림", MuseumUI.Mint); return true; }
            viewPlaque = -1; Open();
        }
        else if (p >= 0)
        {
            viewPlaque = p; Open();
            if (CanRead(p) && !reported[p] && !Done) Send(OpRead, p.ToString());
        }
        return true;
    }

    /// <summary>이 명판의 뜻을 내가 읽을 수 있나 · 자기 말이거나, 통역(이안)이면 외국어 명판까지(한국어 명판은 서하린만).</summary>
    private bool CanRead(int p) => PlaqueReader[p] == Me || (MuseumGame.IHave(MuseumGame.Interpreter) && PlaqueReader[p] != 0);

    private int NextPlaque { get { for (int i = 0; i < 4; i++) if (!reported[i]) return i; return -1; } }
    public override Vector2 GuidePoint { get { int p = NextPlaque; return p >= 0 ? PlaquePos[p] : Dial; } }
    public override string GuideLabel => NextPlaque >= 0 ? "명판" : Title;
    public override string GuideRoom { get { int p = NextPlaque; return p >= 0 ? PlaqueRoom[p] : Room; } }

    /// <summary>AI 는 함께 그 전시실에 들어왔을 때만 자기 말 명판 앞으로 간다(그 밖에는 나를 따라다닌다).</summary>
    public override bool BotStand(int slot, out Vector2 stand)
    {
        stand = default;
        if (!Bot(slot) || MuseumGame.Stage != Stage) return false;
        int p = System.Array.IndexOf(PlaqueReader, slot);
        var m = Museum;
        if (p < 0 || m == null || m.CurFloor != Floor || m.CurRoom != PlaqueRoom[p]) return false;
        stand = PlaquePos[p] + new Vector2(0, 26);
        return true;
    }

    public override string BotStandRoom(int slot) { int p = System.Array.IndexOf(PlaqueReader, slot); return p >= 0 ? PlaqueRoom[p] : Room; }

    // ---------------------------------------------------------------- 상태
    protected override void OnNet(int op, string value, int actor)
    {
        if (Done) return;
        switch (op)
        {
            case OpRead:
                if (int.TryParse(value, out int p) && p >= 0 && p < 4 && !reported[p]) { reported[p] = true; Engaged = true; }
                break;
            case OpSet:
            {
                int sep = value.IndexOf(':');
                if (sep < 0) return;
                int.TryParse(value.Substring(0, sep), out int d);
                int.TryParse(value.Substring(sep + 1), out int s);
                if (d < 0 || d > 3) return;
                int next = Mathf.Clamp(s, -1, 3);
                if (setting[d] != next && actor == Me) Sfx.Play("museum_metal_click", 0.65f);
                setting[d] = next;
                break;
            }
            case OpTry:
            {
                int right = 0;
                for (int d = 0; d < 4; d++) if (setting[d] == answer[d]) right++;
                if (Time.time < tryLockUntil - 0.2f) return;   // 식는 중(연타·찍기 방지)
                if (right == 4) { if (MuseumNet.Authority) { Solved = true; MuseumNet.Mission(Stage); } }
                else
                {
                    // 몇 개 맞았는지는 쉬움에서만 알려 준다(보통·어려움은 명판을 다 모아서 풀어야 한다)
                    int diff = Story1Net.Difficulty;
                    MuseumGame.Mistake();
                    tryLockUntil = Time.time + (diff == Story1Rules.Easy ? 3f : diff == Story1Rules.Normal ? 6f : 10f);
                    Sfx.Play("door_locked", 0.6f);
                    MuseumHud.Toast(diff == Story1Rules.Easy ? "나침반이 헛돈다 · " + right + "/4 맞음" : "나침반이 헛돈다 · 명판을 다시 확인", MuseumUI.Red);
                }
                break;
            }
        }
    }

    public override int[] SnapState()
    {
        var l = new System.Collections.Generic.List<int>();
        Put(l, plaqueDir); Put(l, answer); Put(l, setting); Put(l, reported); l.Add(Engaged ? 1 : 0);
        return l.ToArray();
    }

    public override void LoadState(int[] s)
    {
        if (s.Length < 17) return;
        CopyIn(s, 0, plaqueDir); CopyIn(s, 4, answer); CopyIn(s, 8, setting); CopyIn(s, 12, reported);
        if (s[16] != 0) Engaged = true;
    }

    protected override void OnChat(int slot, string text)
    {
        if (Bot(slot) || !MuseumNet.Authority) return;
        if (MuseumSignals.Normalize(text).Contains("?"))
            for (int s = 0; s < 4; s++) if (Bot(s)) { botSaid[s] = false; botNext[s] = Time.time + 0.8f + s * 0.7f; }
    }

    // ---------------------------------------------------------------- AI
    private readonly float[] arrivedAt = { -1f, -1f, -1f, -1f };

    protected override void BotThink()
    {
        var m = Museum;
        for (int s = 0; s < 4; s++)
        {
            if (!Bot(s)) continue;
            int p = System.Array.IndexOf(PlaqueReader, s);
            if (p < 0) continue;
            bool localHere = m != null && m.CurFloor == Floor && m.CurRoom == PlaqueRoom[p];
            bool there = localHere ? m.BotArrived(s) : MuseumGame.AnyHumanIn(Floor, PlaqueRoom[p]);   // 다른 사람과 함께 들어간 경우도 읽어 준다
            if (!there) { arrivedAt[s] = -1f; continue; }
            if (arrivedAt[s] < 0f) arrivedAt[s] = Time.time;
            if (botSaid[s] || Time.time < botNext[s] || Time.time - arrivedAt[s] < 1.2f) continue;
            int d = plaqueDir[p];
            BotSay(s, MuseumVoice.Direction(s, d) + " = " + PuzzleBreakers.Shape[answer[d]] + " !");
            if (!reported[p]) Send(OpRead, p.ToString(), s);
            botSaid[s] = true;
        }
    }

    // ---------------------------------------------------------------- 조작창
    protected override void BuildBody(RectTransform b)
    {
        dialView = MuseumUI.Node("Dial", b); MuseumUI.Stretch(dialView);
        for (int d = 0; d < 4; d++)
        {
            int dir = d;
            var cell = MuseumUI.Node("Dir" + d, dialView);
            // 2x2 칸: 북(왼쪽 위) 동(오른쪽 위) 서(왼쪽 아래) 남(오른쪽 아래)
            int col = d == 0 || d == 3 ? 0 : 1, row = d == 0 || d == 1 ? 0 : 1;
            MuseumUI.Anchor(cell, 0f, 1f, col * 440f, -row * 172f, 428f, 164f);
            MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", cell)), MuseumUI.FillSprite, new Color(0.08f, 0.1f, 0.13f, 1f));
            dirHead[d] = MuseumUI.Text(cell, "Head", MuseumVoice.DirArrow[d] + " " + MuseumVoice.Direction(0, d), 2, MuseumUI.Amber, TextAnchor.UpperLeft);
            MuseumUI.Anchor(dirHead[d].rectTransform, 0f, 1f, 18f, -10f, 280f, 36f);
            dirWords[d] = MuseumUI.Text(cell, "Words", "", 2, MuseumUI.Ink, TextAnchor.UpperLeft);
            MuseumUI.Anchor(dirWords[d].rectTransform, 0f, 1f, 18f, -50f, 280f, 108f);
            dirWords[d].text = MuseumVoice.Direction(1, d) + "\n" + MuseumVoice.Direction(2, d) + "\n" + MuseumVoice.Direction(3, d);
            dirBtn[d] = MuseumUI.Button(cell, "Sym", "?", 4, () => Cycle(dir));
            MuseumUI.Anchor(dirBtn[d].Rect, 1f, 0.5f, -14f, 0f, 104f, 136f);
        }
        tryBtn = MuseumUI.Button(dialView, "Try", "나침반 돌리기", 2, TryDial);
        MuseumUI.Anchor(tryBtn.Rect, 0.5f, 0f, 0f, 48f, 360f, 56f);
        tryBtn.Accent = MuseumUI.Mint;

        plaqueView = MuseumUI.Node("Plaque", b); MuseumUI.Stretch(plaqueView);
        MuseumUI.Sliced(MuseumUI.Stretched(MuseumUI.Node("Back", plaqueView)), MuseumUI.FillSprite, new Color(0.16f, 0.12f, 0.08f, 1f));
        plaqueTitle = MuseumUI.Text(plaqueView, "Title", "", 2, MuseumUI.Slate, TextAnchor.MiddleCenter, 0f, 150f, 820f, 40f);
        plaqueBig = MuseumUI.Text(plaqueView, "Big", "", 5, new Color(0.96f, 0.86f, 0.6f), TextAnchor.MiddleCenter, 0f, 60f, 840f, 100f);
        plaqueMean = MuseumUI.Text(plaqueView, "Mean", "", 3, MuseumUI.Mint, TextAnchor.MiddleCenter, 0f, -30f, 820f, 56f);
        plaqueNote = MuseumUI.Text(plaqueView, "Note", "", 2, MuseumUI.Ink, TextAnchor.MiddleCenter, 0f, -96f, 840f, 40f);
        tellBtn = MuseumUI.Button(plaqueView, "Tell", "보내기", 2, TellPlaque);
        MuseumUI.Anchor(tellBtn.Rect, 0.5f, 0f, 0f, 24f, 360f, 56f);
        tellBtn.Accent = MuseumUI.Mint;

        hintText = MakeHint(b);
    }

    protected override string KeysHint => viewPlaque >= 0 ? "T 보내기   X 닫기" : "1~4 모양 바꾸기   Space 돌리기   X 닫기";

    protected override void OnKeys(UnityEngine.InputSystem.Keyboard kb)
    {
        if (viewPlaque >= 0) { if (kb.tKey.wasPressedThisFrame) TellPlaque(); return; }
        int d = DigitPressed(kb, 4);
        if (d >= 0) Cycle(d);
        if ((kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) && tryBtn.Enabled) TryDial();
    }

    private bool CoolingDown => Time.time < tryLockUntil;
    private void TryDial() { if (!CoolingDown && Reported >= 4) Send(OpTry); }

    private void Cycle(int d)
    {
        int next = setting[d] + 1; if (next > 3) next = 0;
        Send(OpSet, d + ":" + next);
    }

    private void TellPlaque()
    {
        if (viewPlaque < 0) return;
        int d = plaqueDir[viewPlaque];
        int reader = PlaqueReader[viewPlaque];
        if (CanRead(viewPlaque)) Story1Net.SendChat(MuseumVoice.Direction(reader, d) + " = " + PuzzleBreakers.Shape[answer[d]] + "  " + MuseumVoice.DirArrow[d]);
        else Story1Net.SendChat(MuseumVoice.Direction(reader, d) + " = " + PuzzleBreakers.Shape[answer[d]] + " ?");
    }

    protected override void RefreshPanel()
    {
        bool plaque = viewPlaque >= 0;
        keysText.text = KeysLine;
        dialView.gameObject.SetActive(!plaque);
        plaqueView.gameObject.SetActive(plaque);
        titleText.text = plaque ? "명판 · " + PlaqueRoomName[viewPlaque] : TitleLine(Title);
        if (plaque)
        {
            int p = viewPlaque, d = plaqueDir[p], reader = PlaqueReader[p];
            bool mine = CanRead(p);
            Sub(reader == Me ? "내 나라 말 명판. 뜻을 동료에게 전한다." : mine ? "통역 · " + MuseumVoice.Language[reader] + " 명판도 읽을 수 있다." : MuseumVoice.Language[reader] + " 명판 · 뜻은 " + Name(reader) + "만 안다.");
            plaqueTitle.text = "[ " + MuseumVoice.Language[reader] + " 명판 ]";
            plaqueBig.text = MuseumVoice.Direction(reader, d).ToUpperInvariant() + "  =  " + PuzzleBreakers.Shape[answer[d]];
            plaqueMean.text = mine ? MuseumVoice.DirArrow[d] + " " + MuseumVoice.Direction(0, d) + " 칸에  " + PuzzleBreakers.Shape[answer[d]] : "";
            plaqueNote.text = mine ? (Me == 0 ? "한국어 명판이다. 이건… 나만 읽을 수 있어." : "") : "복도 나침반에 같은 낱말이 있다.";
            tellBtn.SetText(mine ? "보내기" : "이 글자 물어보기");
            hintText.text = "";
            return;
        }
        Sub("들은 낱말을 찾아 그 방위 칸에 모양을 맞춘다.");
        for (int d = 0; d < 4; d++)
        {
            dirBtn[d].SetText(setting[d] < 0 ? "?" : PuzzleBreakers.Shape[setting[d]]);
            dirBtn[d].Selected = setting[d] >= 0;
        }
        bool all = setting[0] >= 0 && setting[1] >= 0 && setting[2] >= 0 && setting[3] >= 0;
        bool cool = CoolingDown;
        tryBtn.Enabled = all && MuseumGame.CanOperate && !cool && Reported >= 4;
        tryBtn.SetText(!MuseumGame.CanOperate ? "돌리기는 조작자 담당" : cool ? "식는 중 · " + Mathf.CeilToInt(tryLockUntil - Time.time) + "초" : Reported < 4 ? "명판 " + Reported + "/4 모으는 중" : "나침반 돌리기");
        for (int d = 0; d < 4; d++) dirHead[d].color = MuseumGame.IHave(MuseumGame.Observer) && setting[d] >= 0 ? (setting[d] == answer[d] ? MuseumUI.Mint : MuseumUI.Red) : MuseumUI.Amber;
        hintText.text = Reported < 4 ? "네 전시실(1층 네 귀퉁이 방) 벽의 명판을 먼저 읽는다" : all ? "준비되면 돌리기" : "칸을 눌러 모양 바꾸기";
    }

    protected override string TeamState(int slot, out Color color)
    {
        int p = System.Array.IndexOf(PlaqueReader, slot);
        bool r = p >= 0 && reported[p];
        color = r ? MuseumUI.Mint : MuseumUI.Slate;
        return r ? "○" : "…";
    }
}
