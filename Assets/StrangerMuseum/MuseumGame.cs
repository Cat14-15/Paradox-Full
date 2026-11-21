using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;

/// <summary>
/// MainStory1 의 진행(미션 차례 · 잠긴 문 · 미션 카드 · 이야기 끼우기 · 전시물 설명).
/// 미션은 한 번에 하나만 보인다. 미션을 끝내면(MuseumNet.Mission) 모든 화면에서 같은 차례로 넘어가고, 짧은 이야기가 나온다.
/// </summary>
public sealed class MuseumGame : MonoBehaviour
{
    public const int Valves = 0, Breakers = 1, Compass = 2, Script = 3, Words = 4, Gather = 5, Done = 6;

    /// <summary>지금 해야 할 미션 번호(Valves~Gather). Done 이면 탈출.</summary>
    public static int Stage { get; private set; }
    public static float StartTime { get; private set; }
    public static float ClearTime { get; private set; }
    /// <summary>난이도 실수 벌점(초). 보통 +5 · 어려움 +15. 기록(랭킹)에 더해진다.</summary>
    public static float Penalty { get; private set; }
    public static int Mistakes { get; private set; }
    /// <summary>지금까지 걸린 시간(벌점 포함). 탈출했으면 탈출 시각까지.</summary>
    public static float Elapsed => Mathf.Max(0f, (ClearTime > 0f ? ClearTime : Time.time) - StartTime) + Penalty;
    /// <summary>미션별로 끝낸 시각(벌점 포함) · 스피드런 구간 기록.</summary>
    public static readonly float[] Splits = new float[Done];

    /// <summary>퍼즐을 틀렸다(모든 PC 에서 같은 이벤트로 불린다). 난이도에 따라 시간 벌점.</summary>
    public static void Mistake()
    {
        Mistakes++;
        int d = Story1Net.Difficulty;
        float sec = d == Story1Rules.Hard ? 15f : d == Story1Rules.Normal ? 5f : 0f;
        if (sec <= 0f) return;
        Penalty += sec;
        MuseumHud.PenaltyFlash(sec);
    }
    public static Canvas PanelCanvas { get; private set; }
    public static MuseumGame Instance { get; private set; }

    public static readonly List<MuseumPuzzle> Puzzles = new List<MuseumPuzzle>();

    public sealed class Mission
    {
        public string title, place, todo;
        public Mission(string title, string place, string todo) { this.title = title; this.place = place; this.todo = todo; }
    }

    public static readonly Mission[] Missions =
    {
        new Mission("물길 막기", "지하 · 관리 통로", "밸브를 하나씩 잡는다. 서로의 정답을 알려 주고, 맞춘 뒤 고정."),
        new Mission("불 켜기", "지하 · 전기실", "차단기를 하나씩 맡는다. 몇째인지 알려 주고 차례대로 올린다."),
        new Mission("나침반 잠금", "1층 · 네 전시실 → 중앙 복도", "네 전시실 벽의 명판(글귀)을 읽고, 복도 나침반에 모양을 맞춘다."),
        new Mission("옛 글자 풀기", "2층 · 기록 아카이브", "필리핀 옛 글자 3개. 동료의 풀이표로 소리를 찾아 넣는다."),
        new Mission("네 나라 말 사전", "2층 · 자료 열람실", "사전 자물쇠의 낱말 3개. 그 나라 친구가 뜻을 그림 카드로 알려 주면 조작자가 넣는다."),
        new Mission("함께 서기", "2층 · 반원 홀", "발판마다 주인이 있다. 관측자가 알려 준 내 발판에 넷이 함께 선다."),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Stage = 0; Puzzles.Clear(); PanelCanvas = null; Instance = null; StartTime = 0f; ClearTime = 0f; Penalty = 0f; Mistakes = 0; System.Array.Clear(Splits, 0, Splits.Length); }

    private void Awake()
    {
        Instance = this;
        Stage = 0; Puzzles.Clear(); StartTime = Time.time; ClearTime = 0f; Penalty = 0f; Mistakes = 0; System.Array.Clear(Splits, 0, Splits.Length);
        MuseumWords.Reset();
        MuseumClues.Reset();
        MuseumNet.Hook();
        MuseumNet.MissionDone += OnMissionDone;
        MuseumNet.SnapReceived += OnSnap;
        PanelCanvas = MuseumUI.MakeCanvas(transform, "PuzzleCanvas", 96);
        MuseumUI.ClearAll();
        mySlot = Story1Role.Current;
        if (Paradox.Net.NetHub.Instance != null) Paradox.Net.NetHub.Instance.Disconnected += OnDisconnected;
    }

    private int mySlot;
    private float nextSnap;
    private void Update()
    {
        if (!Story1Net.Online) return;
        mySlot = Story1Role.Current;
        // 온라인 동기화: 방장이 1초마다 미션 차례 + 지금 퍼즐 상태를 보낸다(늦게 들어와 놓친 조작·미션 완료를 따라잡게)
        if (!MuseumNet.Authority || Time.unscaledTime < nextSnap) return;
        nextSnap = Time.unscaledTime + 1f;
        var p = Current;
        int[] st = p != null && !p.Solved ? p.SnapState() : null;
        MuseumNet.Snap(Stage, p != null ? p.Id : 0, st != null ? string.Join(",", st) : "");
    }

    private void OnSnap(int stage, int id, string data)
    {
        if (MuseumNet.Authority) return;
        int guard = 0;
        while (Stage < stage && Stage < Done && guard++ < Done + 2) OnMissionDone(Stage);   // 놓친 미션 완료
        if (stage != Stage || string.IsNullOrEmpty(data)) return;
        foreach (var p in Puzzles)
        {
            if (p.Id != id || p.Stage != stage || p.Solved) continue;
            var parts = data.Split(',');
            var arr = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) int.TryParse(parts[i], out arr[i]);
            p.LoadState(arr);
        }
    }

    /// <summary>온라인이 끊겼다(방장이 나감·인터넷 끊김). 하던 역할 그대로, 나머지는 AI 친구가 맡아 혼자 이어 간다.</summary>
    private void OnDisconnected()
    {
        if (MuseumStory.Leaving || Stage >= Done) return;   // 타이틀로 나가는 중(내가 끊음) · 엔딩 중에는 알릴 필요 없음
        Story1Role.Keep(mySlot);
        var hub = Paradox.Net.NetHub.Instance;
        string why = hub != null && !string.IsNullOrEmpty(hub.Status) ? hub.Status + " · " : "";
        MuseumHud.Toast(why + "AI 동료가 이어서 맡는다", MuseumUI.Amber);
    }

    private void OnDestroy()
    {
        if (Paradox.Net.NetHub.Instance != null) Paradox.Net.NetHub.Instance.Disconnected -= OnDisconnected;
        MuseumNet.MissionDone -= OnMissionDone;
        MuseumNet.SnapReceived -= OnSnap;
        MuseumNet.Unhook();
        if (Instance == this) Instance = null;
    }

    public static void Register(MuseumPuzzle p) { if (!Puzzles.Contains(p)) Puzzles.Add(p); }

    public static MuseumPuzzle Current
    {
        get { foreach (var p in Puzzles) if (p.Stage == Stage) return p; return null; }
    }

    public static bool AnyPanelOpen { get { foreach (var p in Puzzles) if (p.IsOpen) return true; return MuseumHud.ModalOpen; } }

    public static void CloseAllPanels() { foreach (var p in Puzzles) p.Close(); MuseumHud.CloseModals(); }

    /// <summary>창을 마지막으로 닫은 프레임(같은 Esc · E 로 다른 창이 바로 다시 열리지 않게).</summary>
    public static int LastCloseFrame = -10;

    /// <summary>AI(slot)가 지금 퍼즐 때문에 가 있어야 할 자리(층 · 방 · 지도 좌표).</summary>
    public static bool BotTarget(int slot, out string floor, out string room, out Vector2 pos)
    {
        floor = null; room = null; pos = default;
        var c = Current;
        if (c == null || MuseumStory.Busy) return false;   // 이야기가 나오는 동안은 다 같이 모여 있다
        if (!c.BotStand(slot, out pos)) return false;
        floor = c.Floor; room = c.BotStandRoom(slot);
        return true;
    }

    /// <summary>이름 뒤 조사 "이/가"(받침이 있으면 이). 영문 이름 등은 "이(가)".</summary>
    public static string Iga(string name)
    {
        if (string.IsNullOrEmpty(name)) return "이(가)";
        char c = name[name.Length - 1];
        if (c < 0xAC00 || c > 0xD7A3) return "이(가)";
        return (c - 0xAC00) % 28 != 0 ? "이" : "가";
    }

    // ---------------------------------------------------------------- 역할 능력
    public const int Operator = 0, Interpreter = 1, Navigator = 2, Observer = 3;
    public static readonly string[] Perk =
    {
        "조작자 · 장치의 최종 작동은 내가 맡는다",
        "통역 · 외국어 명판과 채팅 낱말의 뜻이 보인다",
        "내비게이터 · 지도(M)와 목표 방향이 나에게만 보인다",
        "관측자 · 퍼즐에서 맞은 칸이 보인다",
    };

    /// <summary>이 역할의 능력을 지금 내가 쓰는가(내가 그 역할).</summary>
    public static bool IHave(int role) => Story1Role.Current == role;

    /// <summary>능력 담당이 AI 라서, 사람들 모두가 그 능력의 도움을 받는가.</summary>
    public static bool SharedByAI(int role) => Story1Role.IsBot(role);

    /// <summary>사람(나 또는 접속한 동료) 중 누군가 그 방에 있는가. AI 는 이것을 보고 움직인다(방장 화면만 보지 않는다).</summary>
    public static bool AnyHumanIn(string floor, string room)
    {
        var m = StrangerMuseum.Instance;
        if (m == null) return false;
        if (m.CurFloor == floor && m.CurRoom == room) return true;
        for (int s = 0; s < Roles.Count; s++)
            if (s != Story1Role.Current && !Story1Role.IsBot(s) && m.RemoteHumanAt(s, out _, out string f, out string r) && f == floor && r == room) return true;
        return false;
    }

    /// <summary>조작자만 할 수 있는 작동을 지금 내가 할 수 있는가(조작자가 AI 면 누구나).</summary>
    public static bool CanOperate => IHave(Operator) || SharedByAI(Operator);

    public static string NameOf(int slot)
    {
        string n = Roles.DefaultName(slot);
        if (Story1Net.Online && !Story1Role.IsBot(slot))
        {
            var p = Session.State?.players[slot];
            if (p != null && !string.IsNullOrEmpty(p.name)) n = p.name;
        }
        return n;
    }

    // ---------------------------------------------------------------- 미션 진행
    private void OnMissionDone(int stage)
    {
        if (stage != Stage) return;   // 이미 넘어간 미션(늦게 온 이벤트)
        foreach (var p in Puzzles) if (p.Stage == stage) p.Close();
        Stage = stage + 1;
        if (Stage >= Done) ClearTime = Time.time;
        if (stage >= 0 && stage < Splits.Length) Splits[stage] = Elapsed;
        if (Stage == Compass) MuseumStory.RestorePower();
        MuseumStory.PlayBeat(stage);
    }

    /// <summary>문·계단이 잠겨 있으면 이유를 돌려준다.</summary>
    public static bool DoorLocked(string portalName, string targetFloor, string targetRoom, out string reason)
    {
        reason = null;
        if (targetFloor == "B1" && (targetRoom == "power" || targetRoom == "safety" || targetRoom == "rescue") && Stage <= Valves)
        { reason = "문틈으로 물이 샌다 · 밸브부터"; return true; }
        if (targetFloor == "1F" && (targetRoom == "life" || targetRoom == "time" || targetRoom == "craft" || targetRoom == "city") && Stage <= Breakers)
        { reason = "전자 잠금 · 전원이 필요하다"; return true; }
        if (targetFloor == "2F" && portalName != null && portalName.Contains("계단") && Stage <= Compass)
        { reason = "셔터가 내려져 있다 · 복도 나침반부터"; return true; }
        return false;
    }

    /// <summary>미션 카드 한 줄 진행(퍼즐이 알려 준다).</summary>
    public static string ProgressLine { get { var c = Current; return c != null ? c.Progress : null; } }

    // ---------------------------------------------------------------- 전시물 설명(조사)
    public sealed class Lore
    {
        public string title, text, word; public int lang; public string meaning;
        public Lore(string title, string text, string word = null, int lang = 1, string meaning = null)
        { this.title = title; this.text = text; this.word = word; this.lang = lang; this.meaning = meaning; }
    }

    /// <summary>조사할 수 있는 전시물(나머지는 장식). 필리핀 문화를 쉬운 말로, 낱말 하나씩.</summary>
    public static readonly Dictionary<string, Lore> Exhibits = new Dictionary<string, Lore>
    {
        ["floor-guide"] = new Lore("층별 안내판", "1층 상설 전시 · 2층 반원 홀과 전시실 · 지하 관리 구역. 글씨는 필리핀어와 영어로 적혀 있다.", "palapag", 1, "층"),
        ["hall-reception"] = new Lore("안내 데스크", "방명록에 여러 나라 글씨가 섞여 있다. 맨 아래에 한글로 '또 올게요!'라고 적힌 줄도 보인다.", "kumusta", 1, "안녕"),
        ["life-heritage-jar"] = new Lore("마눙굴 항아리", "3천 년 전 필리핀 사람들이 만든 항아리. 뚜껑 위 작은 배에 두 사람이 타고 있다. 먼 길을 함께 떠나는 모습이라고 한다.", "banga", 1, "항아리"),
        ["life-table-center"] = new Lore("바하이 쿠보 모형", "대나무와 야자잎으로 지은 필리핀 전통 집. 이웃들이 함께 집을 통째로 들어 옮기는 풍습 '바야니한'이 유명하다.", "bayanihan", 1, "다 함께 돕기"),
        ["time-long-shelf-ne"] = new Lore("역사 연표", "필리핀에는 180개가 넘는 언어가 있다. 한 나라 안에서도 말이 다른 이웃이 함께 산다.", "wika", 1, "언어"),
        ["craft-table-center"] = new Lore("이푸가오 베틀", "산악 지방 사람들이 쓰던 베틀. 무늬마다 집안과 마을의 이야기가 담겨 있다.", "habi", 1, "손으로 짠 천"),
        ["city-table-center"] = new Lore("1910 마닐라 전차 모형", "옛 마닐라 거리를 달리던 전차. 여러 나라에서 온 사람들이 같은 전차를 타고 일하러 갔다.", "tranvia", 1, "전차"),
        ["rescue-dehumid-nw"] = new Lore("제습기", "젖은 유물이 곰팡이 슬지 않게 공기를 말린다. 필리핀은 일 년 내내 습하다.", "tubig", 1, "물"),
        ["safety-console"] = new Lore("방재 관제 콘솔", "화면에 태풍 경보가 깜빡인다. 필리핀에는 해마다 태풍이 스무 개쯤 지나간다.", "bagyo", 1, "태풍"),
        ["rescue-crate-sw1"] = new Lore("방수 수장 상자", "상자 옆에 네 나라 말로 '조심히 다뤄 주세요'라고 적힌 스티커가 붙어 있다.", "ingat", 1, "조심해"),
        ["archive-side-desk-e"] = new Lore("기록원 책상", "스페인 사람들이 오기 전, 필리핀 사람들은 '바이바이인'이라는 고유 글자를 썼다. 대나무에 칼로 새겼다고 한다.", "baybayin", 1, "필리핀 옛 글자"),
        ["preserve-lab-c"] = new Lore("보존 작업대", "물에 젖은 유물을 천천히 말리는 곳. 서두르면 유물이 갈라진다.", "dahan-dahan", 1, "천천히"),
        ["reading-table"] = new Lore("열람 책상", "펼쳐진 사전에 같은 낱말이 네 나라 말로 적혀 있다. 물 : tubig : nuoc : amanzi.", "nuoc", 2, "물(베트남어)"),
        ["special-wall-art"] = new Lore("해양 교류 벽화", "옛날 배들은 바다를 건너 여러 나라를 오갔다. 말이 달라도 손짓과 물건으로 서로를 이해했다.", "dagat", 1, "바다"),
        ["atrium-statue"] = new Lore("반원 홀 동상", "받침돌에 '함께 선 이들에게 길이 열린다'라는 글이 새겨져 있다. 둘레에 발판 네 개가 있다.", null),
    };

    public static void Inspect(string exhibitId, string fallbackName)
    {
        if (!Exhibits.TryGetValue(exhibitId, out var lore)) return;
        MuseumHud.ShowInspect(lore.title, lore.text, lore.word, lore.lang, lore.meaning);
        if (!string.IsNullOrEmpty(lore.word)) MuseumWords.Learn(lore.word, lore.lang, lore.meaning);
    }

    // ---------------------------------------------------------------- 시험용(에디터 자동 확인)
    public static void DebugFinishCurrent() { var c = Current; if (c != null) MuseumNet.Mission(c.Stage); }
}
