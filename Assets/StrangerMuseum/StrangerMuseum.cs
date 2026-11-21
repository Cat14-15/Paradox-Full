using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MainStory1 박물관의 걷기·방 이동·카메라·동료(사람/AI) 표시.
/// 화면 글자·퍼즐·이야기는 따로: MuseumHud(표시) · MuseumGame(진행) · Puzzle*(협동 퍼즐) · MuseumStory(이야기·어둠) · MuseumChat(채팅).
/// 지도 좌표는 MuseumLayout.json 의 픽셀(아래로 갈수록 y 가 크다), 월드는 100픽셀 = 1 유닛.
/// </summary>
public sealed class StrangerMuseum : MonoBehaviour
{
    public static StrangerMuseum Instance { get; private set; }

    [SerializeField] private TextAsset layout;
    [SerializeField] private SpriteRenderer visitor;
    [SerializeField] private GameObject[] floorRoots;
    [SerializeField] private Sprite[] portraits;
    [SerializeField] private Texture2D[] storyRoleSheets;

    private const float PixelsPerUnit = 100;
    private const float PortalReach = 26;
    private const int WallSideClearance = 5;
    private const float WalkPixelsPerSecond = 150;
    private const float StorySpriteScale = 0.25f;
    private const float ViewHeight = 320f;   // 화면에 보이는 지도 높이(px) · 모든 방 같은 배율
    private Camera viewCamera;
    private Layout museum;
    private Floor floor;
    private Room room;
    private string roomId;
    private string curFloorId;
    private Vector2 position;
    private int role;
    private int direction = PixelSprites.DirDown;
    private float walkTime;
    private Vector2 moveInput;
    private Sprite[][] storyFrames;

    public string CurFloor => curFloorId;
    public string CurRoom => roomId;
    public Vector2 PlayerMap => position;
    public SpriteRenderer Visitor => visitor;
    public float InspectRadius => museum != null ? museum.player.inspectRadius : 42f;

    // ---- 멀티: 다른 사람(역할 슬롯 0~3)을 같은 층에서 보여 준다 ----
    private static readonly string[] FloorIds = { "1F", "2F", "B1" };
    private const float FloorStride = 10000f;   // 위치 RPC 의 x 에 층·방 번호를 실어 보낸다
    private SpriteRenderer[] remotes;
    private int[] remoteDir;
    private float[] remoteWalk;
    private float nextPosSend;
    private float stepTimer;
    private float[] remoteStep;
    private bool[] remoteWasShown;
    private int[] remoteKey;
    private Vector2[] remoteLastPos;
    private const float StepInterval = 0.34f;
    private Vector2 lastSent;
    private struct Trail { public float t; public Vector2 pos; public int floor, room; }
    private readonly List<Trail> trail = new List<Trail>();
    // 바짝 붙어 졸졸 따라오면 무섭다는 의견 → 조금 늦게, 조금 더 떨어져서 따라온다
    private static readonly float[] BotDelay = { 2.1f, 0.9f, 1.5f, 2.1f };
    private static readonly Vector2[] BotOffset = { new Vector2(-86, 28), new Vector2(-44, 50), new Vector2(44, 50), new Vector2(86, 28) };   // 내 뒤로 부채꼴 · 이름표가 겹치지 않게

    private void Awake()
    {
        Instance = this;
        if (Application.targetFrameRate <= 0) Application.targetFrameRate = 60;   // 웹에서 프레임 상한이 없으면 브라우저가 과하게 돈다
        viewCamera = GetComponent<Camera>();
        museum = JsonConvert.DeserializeObject<Layout>(layout.text);
        storyFrames = (storyRoleSheets ?? System.Array.Empty<Texture2D>()).Select(Story1Companions.Slice).ToArray();
        role = Mathf.Clamp(Story1Role.Current, 0, Roles.Count - 1);
        Bgm.Play("story1", 0.3f);
        BuildRemotes();
        // 순서 중요: 진행(퍼즐 목록·패널 캔버스) → 화면 → 채팅 → 퍼즐 → 이야기
        gameObject.AddComponent<MuseumGame>();
        gameObject.AddComponent<MuseumHud>();
        if (GetComponent<MuseumChat>() == null) gameObject.AddComponent<MuseumChat>();
        gameObject.AddComponent<TouchControls>();   // 휴대폰 터치 조작(PC 에서는 안 보인다) · MuseumGame 의 MuseumUI.ClearAll 뒤에 단추를 만든다
        gameObject.AddComponent<PuzzleValves>();
        gameObject.AddComponent<PuzzleBreakers>();
        gameObject.AddComponent<PuzzleCompass>();
        gameObject.AddComponent<PuzzleScript>();
        gameObject.AddComponent<PuzzleWords>();
        gameObject.AddComponent<PuzzleGather>();
        gameObject.AddComponent<MuseumBots>();
        gameObject.AddComponent<MuseumStory>();
        gameObject.AddComponent<MuseumCats>();
        Enter("1F");
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void BuildRemotes()
    {
        int n = Roles.Count;
        remotes = new SpriteRenderer[n]; remoteDir = new int[n]; remoteWalk = new float[n]; remoteStep = new float[n];
        remoteWasShown = new bool[n]; remoteKey = new int[n]; remoteLastPos = new Vector2[n];
        for (int i = 0; i < n; i++) remoteKey[i] = -1;
        for (int i = 0; i < n; i++)
        {
            SpriteRenderer r = Instantiate(visitor, visitor.transform.parent);
            r.name = "Remote_" + Roles.DefaultName(i);
            r.gameObject.SetActive(false);
            remotes[i] = r;
        }
    }

    private void Enter(string floorId, string destination = null, int[] arrival = null)
    {
        floor = museum.floors[floorId];
        curFloorId = floorId;
        roomId = destination ?? floor.defaultScene;
        room = floor.scenes[roomId];
        position = Point(arrival ?? floor.spawn);
        Vector2 spread = position + new Vector2((role - 1.5f) * 24f, 0);   // 네 명이 한 점에 겹치지 않게 나란히
        if (CanMove(spread.x, spread.y)) position = spread;
        arrivePos = position;
        for (int k = 0; k < botKey.Length; k++) botKey[k] = -1;   // 방이 바뀌면 AI 는 다시 문으로 따라 들어오거나(따라오기) 퍼즐 자리에 서 있다
        moveInput = Vector2.zero;
        walkTime = 0;
        routeKey = null;
        portalArmed = false;
        lastMoveTime = Time.time;
        foreach (GameObject root in floorRoots)
            root.SetActive(root.name == floorId);
        PlaceVisitor();
    }

    private static Vector2 Point(int[] pair) => new Vector2(pair[0], pair[1]);

    // ---------------------------------------------------------------- 입력
    // ---------------------------------------------------------------- 층별 배경음악
    // 정전 중: B1 = museum_b1, 그 밖 = story1 · 불이 켜진 뒤: 1F = museum_1f, 2F = museum_2f, B1 = story1.
    // 이야기(MuseumStory) 가 따로 곡을 트는 동안은 손대지 않고, 끝나면 층 음악으로 돌아온다. 파일이 없으면 story1.
    private string floorBgm;
    private bool bgmPaused;
    private static readonly HashSet<string> MissingBgm = new HashSet<string>();

    private void UpdateFloorBgm()
    {
        if (MuseumStory.Busy) { bgmPaused = true; return; }
        bool dark = MuseumGame.Stage <= MuseumGame.Breakers;
        string track = dark ? (curFloorId == "B1" ? "museum_b1" : "story1")
                     : curFloorId == "1F" ? "museum_1f" : curFloorId == "2F" ? "museum_2f" : "story1";
        if (track == floorBgm && !bgmPaused) return;
        floorBgm = track; bgmPaused = false;
        if (track != "story1" && (MissingBgm.Contains(track) || Resources.Load<AudioClip>("Music/" + track) == null))
        { MissingBgm.Add(track); track = "story1"; }
        Bgm.Play(track, 0.3f);   // 같은 곡이면 Bgm 이 다시 틀지 않는다(크로스페이드)
    }

    private void Update()
    {
        UpdateFloorBgm();
        Keyboard keys = Keyboard.current;
        bool blocked = MuseumStory.Busy || MuseumChat.Typing || MuseumGame.AnyPanelOpen;
        // 키보드(방향키·WASD) + 터치 조이스틱(휴대폰)
        Vector2 mv = Story1Scene.ReadMovement(keys) + TouchControls.Move;
        moveInput = blocked ? Vector2.zero : Vector2.ClampMagnitude(mv, 1f);
        if (moveInput.sqrMagnitude > 0.0001f && TouchControls.Move == Vector2.zero) moveInput = moveInput.normalized;
        if (MuseumStory.Busy || MuseumChat.TypingNow) { MuseumHud.PromptClicked = false; return; }   // 휴대폰은 키보드가 없을 수 있다(null 이어도 계속)
        bool justClosed = Time.frameCount <= MuseumGame.LastCloseFrame + 1;

        if (keys != null && keys.escapeKey.wasPressedThisFrame && !MuseumGame.AnyPanelOpen && !justClosed)
        {
            MuseumHud.TogglePause();
            return;
        }
#if UNITY_EDITOR
        if (keys != null && keys.tabKey.wasPressedThisFrame && Story1Role.Solo)   // 에디터에서 혼자 확인할 때만: 역할을 돌려 본다
        {
            Story1Role.Cycle();
            role = Story1Role.Current;
            UpdateVisitorSprite();
        }
#endif
        if (MuseumGame.AnyPanelOpen) { MuseumHud.PromptClicked = false; return; }

        // 조사·조작: E 또는 Z(쯔꾸르식 Z 결정) 또는 터치 '조사' 단추. X 는 월드에서 아무 동작 없음(이야기 건너뛰기·창 닫기 전용)
        bool keyPressed = keys != null && (keys.eKey.wasPressedThisFrame || keys.zKey.wasPressedThisFrame);
        bool ePressed = ((keyPressed || TouchControls.InteractPressed) && !justClosed) || MuseumHud.PromptClicked;
        MuseumHud.PromptClicked = false;
        if (!ePressed) return;

        // 순서: 아직 안 푼 퍼즐 장치 → 문 → 푼 퍼즐 · 전시물 (장치 옆 문이 먼저 열려 방을 나가 버리지 않게)
        foreach (var p in MuseumGame.Puzzles)
            if (!p.Done && p.Interact(curFloorId, roomId, position, InspectRadius)) { moveInput = Vector2.zero; return; }
        Portal portal = NearbyPortal();
        if (portal != null) { UsePortal(portal); return; }
        foreach (var p in MuseumGame.Puzzles)
            if (p.Interact(curFloorId, roomId, position, InspectRadius)) { moveInput = Vector2.zero; return; }
        Exhibit ex = NearbyExhibit();
        if (ex != null) { moveInput = Vector2.zero; MuseumGame.Inspect(ex.id, ex.name); }
    }

    private float lastPortalAt = -9f;

    private void UsePortal(Portal portal)
    {
        // 문으로 걸어 들어가 자동으로 넘어간 바로 그때 E 가 눌리면, 도착한 곳의 "되돌아가는 문"이 열려 원래 방으로 튕겨 나갔다 → 잠깐 막는다
        if (Time.time - lastPortalAt < 0.6f) return;
        if (MuseumGame.DoorLocked(portal.name, portal.targetFloor, portal.targetScene, out string why))
        {
            if (Time.time < lockToastAt) return;
            lockToastAt = Time.time + 1.5f;
            Sfx.Play("door_locked", 0.6f);
            MuseumHud.Toast(why, MuseumUI.Amber);
            return;
        }
        lastPortalAt = Time.time;
        PlayPass(portal.name != null && portal.name.Contains("계단"), 0.6f, true);
        Enter(portal.targetFloor, portal.targetScene, portal.arrival);
    }

    // ---------------------------------------------------------------- 문 밟고 지나가기(자동 이동)
    private bool portalArmed;     // 도착한 뒤 문 칸에서 한 번 벗어나야 다시 작동한다(들어오자마자 되돌아 나가지 않게)
    private float lockToastAt;
    private readonly Dictionary<Portal, Vector2> exitDirs = new Dictionary<Portal, Vector2>();

    /// <summary>문 칸을 밟고 문 바깥쪽으로 걸어가면 E 없이 옆 방으로 간다.</summary>
    private void AutoPortal()
    {
        if (room == null || MuseumStory.Busy || MuseumChat.Typing || MuseumGame.AnyPanelOpen) return;
        Portal inside = null;
        foreach (Portal portal in floor.portals)
        {
            if (string.IsNullOrEmpty(portal.targetFloor) || portal.sourceScene != roomId) continue;
            int[] b = portal.rect;
            if (position.x >= b[0] - 3 && position.x <= b[0] + b[2] + 3 && position.y >= b[1] - 3 && position.y <= b[1] + b[3] + 3) { inside = portal; break; }
        }
        if (inside == null) { portalArmed = true; return; }
        if (!portalArmed || moveInput.sqrMagnitude < 0.01f) return;
        Vector2 exit = ExitDir(inside);
        if (exit == Vector2.zero) return;
        Vector2 mv = new Vector2(moveInput.x, -moveInput.y).normalized;   // 지도 좌표(아래가 +y)
        if (Vector2.Dot(mv, exit) < 0.6f) return;
        // 문 칸의 바깥쪽 절반까지 들어가야 넘어간다(문 옆 장치로 다가가다가 방을 나가 버리지 않게)
        int[] r = inside.rect;
        Vector2 center = new Vector2(r[0] + r[2] * 0.5f, r[1] + r[3] * 0.5f);
        if (Vector2.Dot(position - center, exit) < 0f) return;
        UsePortal(inside);
    }

    /// <summary>
    /// 문 바깥쪽 방향(지도 좌표, 아래가 +y). 되돌아오는 문의 도착 지점(이 문 바로 안쪽)에서 이 문 쪽으로 가는 방향.
    /// 세로로 긴 문(옆문)은 좌우, 가로로 긴 문(계단·위아래 문)은 위아래만 본다. 모르면 0(E 로만 이동).
    /// </summary>
    private Vector2 ExitDir(Portal portal)
    {
        if (exitDirs.TryGetValue(portal, out Vector2 cached)) return cached;
        Vector2 found = Vector2.zero;
        int[] b = portal.rect;
        Vector2 c = new Vector2(b[0] + b[2] * 0.5f, b[1] + b[3] * 0.5f);
        if (museum.floors.TryGetValue(portal.targetFloor, out Floor other) && other.portals != null)
        {
            foreach (Portal back in other.portals)
            {
                if (back.sourceScene != portal.targetScene || back.targetFloor != curFloorId || back.targetScene != portal.sourceScene || back.arrival == null) continue;
                Vector2 v = c - Point(back.arrival);
                if (b[3] > b[2]) { if (Mathf.Abs(v.x) > 0.5f) found = new Vector2(Mathf.Sign(v.x), 0); }
                else if (Mathf.Abs(v.y) > 0.5f) found = new Vector2(0, Mathf.Sign(v.y));
                break;
            }
        }
        exitDirs[portal] = found;
        return found;
    }

    private void FixedUpdate()
    {
        Vector2 previous = position;
        Vector2 move = new Vector2(moveInput.x, -moveInput.y) * (WalkPixelsPerSecond * Time.fixedDeltaTime);
        int steps = Mathf.Max(1, Mathf.CeilToInt(move.magnitude / 2));
        move /= steps;
        for (int i = 0; i < steps && move != Vector2.zero; i++)
        {
            if (CanMove(position.x + move.x, position.y)) position.x += move.x;
            if (CanMove(position.x, position.y + move.y)) position.y += move.y;
        }
        direction = PixelSprites.DirectionFor(moveInput.x, moveInput.y, direction);
        walkTime = position != previous ? walkTime + Time.fixedDeltaTime : 0;
        if (position != previous) lastMoveTime = Time.time;
        if (walkTime > 0)
        {
            stepTimer += Time.fixedDeltaTime;
            if (Time.time < stairsUntil) stepTimer = 0f;
            else if (stepTimer >= StepInterval) { stepTimer -= StepInterval; Sfx.Play("step_" + Random.Range(1, 6), 0.45f); }
        }
        else stepTimer = StepInterval * 0.5f;
        RecordTrail();
        AutoPortal();
        PlaceVisitor();
    }

    private void LateUpdate()
    {
        if (room == null || remotes == null) return;
        SendMyPosition();
        UpdateRemotes();
        PlaceLight();
    }

    // ---------------------------------------------------------------- 위치 기록 · 보내기
    private readonly Dictionary<string, List<string>> roomOrder = new Dictionary<string, List<string>>();

    private int RoomIndex(string floorId, string r)
    {
        if (!roomOrder.TryGetValue(floorId, out var list))
            roomOrder[floorId] = list = museum.floors[floorId].scenes.Keys.ToList();
        return Mathf.Max(0, list.IndexOf(r));
    }

    private string RoomName(int floorIdx, int roomIdx)
    {
        string f = FloorIds[Mathf.Clamp(floorIdx, 0, 2)];
        if (!roomOrder.TryGetValue(f, out var list)) roomOrder[f] = list = museum.floors[f].scenes.Keys.ToList();
        return roomIdx >= 0 && roomIdx < list.Count ? list[roomIdx] : null;
    }

    private const int FloorCount = 3;
    private static int Code(int floorIdx, int roomIdx) => floorIdx + FloorCount * roomIdx;

    private void RecordTrail()
    {
        float now = Time.time;
        trail.Add(new Trail { t = now, pos = position, floor = FloorIndex(), room = RoomIndex(curFloorId, roomId) });
        while (trail.Count > 2 && trail[1].t < now - 4f) trail.RemoveAt(0);
    }

    private Trail TrailAt(float t)
    {
        if (trail.Count == 0) return new Trail { pos = position, floor = FloorIndex(), room = RoomIndex(curFloorId, roomId) };
        if (t <= trail[0].t) return trail[0];
        for (int k = 1; k < trail.Count; k++)
        {
            if (trail[k].t < t) continue;
            Trail a = trail[k - 1], b = trail[k];
            if (a.floor != b.floor || a.room != b.room || (b.pos - a.pos).sqrMagnitude > 3600f) return b;
            return new Trail { t = t, floor = b.floor, room = b.room, pos = Vector2.Lerp(a.pos, b.pos, Mathf.InverseLerp(a.t, b.t, t)) };
        }
        return trail[trail.Count - 1];
    }

    private int FloorIndex() => System.Array.IndexOf(FloorIds, curFloorId);

    private void SendMyPosition()
    {
        if (!Story1Net.HasHumanPeers || Time.time < nextPosSend) return;
        bool moved = (position - lastSent).sqrMagnitude > 1f;
        nextPosSend = Time.time + (moved ? 0.15f : 0.8f);
        lastSent = position;
        Story1Net.SendPos(new Vector2(position.x + Code(FloorIndex(), RoomIndex(curFloorId, roomId)) * FloorStride, position.y));
    }

    private const float VelWindow = 0.12f;

    private bool RemoteSample(int i, bool bot, float t, out Vector2 p, out int fl, out int rm)
    {
        if (bot)
        {
            // 정전 중 손전등을 든 서하린(AI)은 바로 곁에서 따라온다(그 빛으로 길이 보인다)
            bool lamp = i == FlashlightSlot && FlashlightOn;
            Trail tr = TrailAt(t - (lamp ? 0.6f : BotDelay[i]));
            Vector2 off = lamp ? new Vector2(-26, 20) : BotOffset[i];
            // 내 발자취(벽을 지나지 않은 길)에서 옆으로 벌려 선다. 벌린 자리가 벽 너머면 덜 벌리고, 그래도 안 되면 발자취 그대로.
            p = tr.pos + off;
            bool sameRoom = tr.floor == FloorIndex() && tr.room == RoomIndex(curFloorId, roomId);
            if (!sameRoom || !SegmentWalkable(tr.pos, p)) p = tr.pos + off * 0.45f;
            if (!sameRoom || !SegmentWalkable(tr.pos, p)) p = tr.pos;
            fl = tr.floor; rm = tr.room;
            return fl == FloorIndex();
        }
        Vector2 raw = Story1Net.RemoteAt(i, t - 0.35f);
        int code = Mathf.Max(0, Mathf.RoundToInt(raw.x / FloorStride));
        p = new Vector2(raw.x - code * FloorStride, raw.y);
        fl = code % FloorCount; rm = code / FloorCount;
        return fl == FloorIndex();
    }

    /// <summary>다른 사람(사람 동료)의 지도 위치·층·방. 모르면 false.</summary>
    public bool RemoteHumanAt(int slot, out Vector2 p, out string floorId, out string roomName)
    {
        p = default; floorId = null; roomName = null;
        if (!Story1Net.IsRemoteHuman(slot) || !Story1Net.RemoteFresh(slot)) return false;
        Vector2 raw = Story1Net.RemoteAt(slot, Time.time - 0.2f);
        int code = Mathf.Max(0, Mathf.RoundToInt(raw.x / FloorStride));
        p = new Vector2(raw.x - code * FloorStride, raw.y);
        floorId = FloorIds[code % FloorCount]; roomName = RoomName(code % FloorCount, code / FloorCount);
        return true;
    }

    // ---------------------------------------------------------------- 문 · 계단 소리
    private float stairsUntil;

    private void PlayPass(bool stairs, float volume, bool mine = false)
    {
        if (stairs)
        {
            if (mine) stairsUntil = Time.time + 0.75f;
            else if (Time.time < stairsUntil) return;
            StartCoroutine(Stairs(volume));
        }
        else Sfx.Play(Random.value < 0.5f ? "door_open" : "door_open2", volume);
    }

    private System.Collections.IEnumerator Stairs(float volume)
    {
        for (int k = 0; k < 3; k++) { Sfx.Play("step_" + Random.Range(1, 6), volume); yield return new WaitForSeconds(0.24f); }
    }

    // ---------------------------------------------------------------- 동료 그리기(사람 · AI)
    private readonly Vector2[] botPos = new Vector2[4];
    private readonly bool[] botArrived = new bool[4];
    private readonly List<Vector2>[] botPath = new List<Vector2>[4];
    private readonly Vector2[] botPathGoal = new Vector2[4];
    private readonly Vector2[] remoteDisp = new Vector2[4];   // 화면에 그리는 동료 위치(부드럽게 따라간다)
    private readonly bool[] remoteDispOk = new bool[4];
    private readonly int[] remoteDispKey = new int[4];

    /// <summary>a→b 직선이 걸을 수 있는 바닥만 지나는가.</summary>
    private bool SegmentWalkable(Vector2 a, Vector2 b)
    {
        float len = Vector2.Distance(a, b);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 5f));
        for (int k = 1; k <= n; k++) { Vector2 s = Vector2.Lerp(a, b, k / (float)n); if (!CanMove(s.x, s.y)) return false; }
        return true;
    }

    private const int PathCell = 6;

    // 방마다 바닥 격자(걸을 수 있나)를 한 번만 계산해 둔다(길 찾기를 자주 해도 렉이 없게)
    private readonly Dictionary<string, byte[]> walkGrids = new Dictionary<string, byte[]>();

    /// <summary>지금 방 안에서 from → to 길(바닥 격자 너비 우선 탐색 + 직선으로 줄이기). 길이 없으면 null(벽을 뚫고 곧장 가지 않는다).</summary>
    private List<Vector2> FindPath(Vector2 from, Vector2 to)
    {
        var result = new List<Vector2>();
        int[] v = room.view;
        int w = v[2] / PathCell + 1, h = v[3] / PathCell + 1;
        string gridKey = curFloorId + "/" + roomId;
        if (!walkGrids.TryGetValue(gridKey, out byte[] state) || state.Length != w * h) walkGrids[gridKey] = state = new byte[w * h];   // 0 모름 · 1 바닥 · 2 막힘
        bool Open(int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cx >= w || cy >= h) return false;
            int k = cy * w + cx;
            if (state[k] == 0) state[k] = CanMove(v[0] + cx * PathCell, v[1] + cy * PathCell) ? (byte)1 : (byte)2;
            return state[k] == 1;
        }
        Vector2 CellPos(int cx, int cy) => new Vector2(v[0] + cx * PathCell, v[1] + cy * PathCell);
        bool Nearest(Vector2 p, out int ox, out int oy)
        {
            int bx = Mathf.RoundToInt((p.x - v[0]) / PathCell), by = Mathf.RoundToInt((p.y - v[1]) / PathCell);
            for (int r = 0; r <= 8; r++)
            {
                float bd = float.MaxValue; ox = oy = -1;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r || !Open(bx + dx, by + dy)) continue;
                        float d = (CellPos(bx + dx, by + dy) - p).sqrMagnitude;
                        if (d < bd) { bd = d; ox = bx + dx; oy = by + dy; }
                    }
                if (ox >= 0) return true;
            }
            ox = oy = -1; return false;
        }
        if (SegmentWalkable(from, to)) { result.Add(to); return result; }
        if (!Nearest(from, out int sx, out int sy) || !Nearest(to, out int gx, out int gy)) return null;
        var prevCell = new int[w * h];
        for (int k = 0; k < prevCell.Length; k++) prevCell[k] = -1;
        var q = new Queue<int>();
        int start = sy * w + sx, goal = gy * w + gx;
        prevCell[start] = start; q.Enqueue(start);
        int[] ddx = { 1, -1, 0, 0, 1, 1, -1, -1 }, ddy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            if (cur == goal) break;
            int cx = cur % w, cy = cur / w;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + ddx[d], ny = cy + ddy[d];
                if (!Open(nx, ny)) continue;
                if (d >= 4 && (!Open(cx + ddx[d], cy) || !Open(cx, cy + ddy[d]))) continue;   // 모서리를 비스듬히 뚫지 않는다
                int nk = ny * w + nx;
                if (prevCell[nk] >= 0) continue;
                prevCell[nk] = cur; q.Enqueue(nk);
            }
        }
        if (prevCell[goal] < 0) return null;
        var cells = new List<Vector2>();
        for (int k = goal; ; k = prevCell[k]) { cells.Add(CellPos(k % w, k / w)); if (k == start) break; }
        cells.Reverse();
        cells.Add(to);
        // 직선으로 갈 수 있는 중간 점은 건너뛴다
        Vector2 at = from;
        int idx = 0;
        while (idx < cells.Count)
        {
            int far = idx;
            for (int j = cells.Count - 1; j > idx; j--) if (SegmentWalkable(at, cells[j])) { far = j; break; }
            result.Add(cells[far]);
            at = cells[far];
            idx = far + 1;
        }
        return result;
    }

    public bool BotArrived(int slot) => slot >= 0 && slot < 4 && botArrived[slot];

    // ---- AI 둘러보기(내가 가만히 있을 때) ----
    private readonly bool[] botIdle = new bool[4];
    private readonly int[] botIdleDir = new int[4];
    private readonly float[] botLookAt = new float[4];
    private readonly Vector2[] botIdleSpot = new Vector2[4];
    private bool idleFacing;
    private float lastMoveTime;
    private static readonly int[] LookDirs = { PixelSprites.DirUp, PixelSprites.DirUp, PixelSprites.DirLeft, PixelSprites.DirRight, PixelSprites.DirDown };

    /// <summary>botPath[i] 를 따라 speed 로 걷는다.</summary>
    private void WalkPath(int i, float speed)
    {
        var path = botPath[i];
        if (path == null) return;
        float budget = speed * Time.deltaTime;
        while (budget > 0f && path.Count > 0)
        {
            Vector2 next = path[0];
            float dist = Vector2.Distance(botPos[i], next);
            if (dist <= budget) { botPos[i] = next; budget -= dist; path.RemoveAt(0); }
            else { botPos[i] = Vector2.MoveTowards(botPos[i], next, budget); budget = 0f; }
        }
    }

    /// <summary>같은 방에서 나와 가까운 전시물 앞(다른 AI 와 겹치지 않는 곳)을 고른다.</summary>
    private bool PickIdleSpot(int i, out Vector2 spot)
    {
        spot = default;
        float best = float.MaxValue;
        foreach (Exhibit ex in floor.objects)
        {
            if (ex.room != roomId) continue;
            Vector2 s;
            if (ex.interact != null && ex.interact.Length >= 2) s = Point(ex.interact) + new Vector2(0, 10);
            else if (ex.collision != null && ex.collision.Length >= 4) s = new Vector2(ex.collision[0] + ex.collision[2] * 0.5f, ex.collision[1] + ex.collision[3] + 14f);
            else continue;
            float d = (s - position).magnitude;
            if (d < 50f || d > 280f || !CanMove(s.x, s.y)) continue;
            bool taken = false;
            for (int k = 0; k < 4; k++) if (k != i && botIdle[k] && (botIdleSpot[k] - s).sqrMagnitude < 45f * 45f) taken = true;
            if (taken) continue;
            d += Random.Range(0f, 40f);   // 매번 같은 자리만 가지 않게
            if (d < best) { best = d; spot = s; }
        }
        if (best == float.MaxValue) return false;
        botIdleSpot[i] = spot;
        return true;
    }

    // ---- AI 걷기 ----
    // 예전: "내 발자취 + 옆으로 벌린 자리"를 화면에 바로 그렸다 → 벌린 자리가 벽에 막혀 바뀌면 뚝 끊기고(90px 넘으면 순간이동),
    //       그 사이를 직선으로 미끄러져 벽을 지나갔다. 퍼즐 자리로는 내 걸음의 1.6배로 달려갔다.
    // 지금: AI 마다 실제 위치(botPos)가 있고, 목표(따라갈 자리 · 퍼즐 자리 · 둘러볼 전시물 · 나갈 문)만 바뀐다.
    //       위치는 언제나 바닥 길을 따라 걸어서만 움직인다. 방이 바뀔 때만 문에서 나타나고 문으로 사라진다.
    private const float BotFollowSpeed = 150f, BotCatchUpSpeed = 245f, BotStandSpeed = 155f, BotIdleSpeed = 70f, BotExitSpeed = 175f;
    private readonly int[] botKey = { -1, -1, -1, -1 };      // AI 가 지금 있는 방(Code) · 내 방과 같을 때만 보인다
    private readonly float[] botRepathAt = new float[4];
    private readonly float[] botLastSeen = new float[4];      // 마지막으로 내 방에 보였던 때
    private readonly float[] botStillSince = new float[4];
    private readonly bool[] botWalking = new bool[4];
    private readonly bool[] botAway = new bool[4];            // 다른 방 퍼즐 자리로 나가 있다
    private readonly string[] botAwayFloor = new string[4], botAwayRoom = new string[4];
    private readonly bool[] botExiting = new bool[4];
    private readonly Portal[] botExit = new Portal[4];
    private readonly float[] botExitUntil = new float[4];
    private Vector2 arrivePos;

    private int HereKey() => Code(FloorIndex(), RoomIndex(curFloorId, roomId));

    /// <summary>AI i 를 이 방의 at(걸을 수 있는 가장 가까운 곳)에 세운다.</summary>
    private void SpawnBot(int i, Vector2 at, int key)
    {
        botPos[i] = NearestWalkable(at);
        botPath[i] = null; botPathGoal[i] = new Vector2(-99999, -99999);
        botKey[i] = key; botAway[i] = false; botExiting[i] = false; botIdle[i] = false;
        botStillSince[i] = Time.time; botWalking[i] = false;
    }

    private void LeaveBot(int i, string toFloor, string toRoom)
    {
        botKey[i] = -1; botAway[i] = true; botAwayFloor[i] = toFloor; botAwayRoom[i] = toRoom;
        botExiting[i] = false; botExit[i] = null; botIdle[i] = false;
    }

    private Vector2 NearestWalkable(Vector2 p)
    {
        if (CanMove(p.x, p.y)) return p;
        for (int r = 6; r <= 72; r += 6)
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (CanMove(q.x, q.y)) return q;
            }
        return position;
    }

    /// <summary>AI 한 명의 이번 프레임. 내 방에 보이면 true(p = 위치, prev = 걸음 방향을 알기 위한 조금 전 위치).</summary>
    private bool UpdateBot(int i, float now, out Vector2 p, out Vector2 prev, out bool standing, out bool facingIdle)
    {
        p = prev = default; standing = false; facingIdle = false;
        botArrived[i] = false;
        int hereKey = HereKey();
        bool lamp = i == FlashlightSlot && FlashlightOn;
        Vector2 goal; float speed; bool follow = false;

        if (MuseumGame.BotTarget(i, out string sf, out string sr, out Vector2 stand))
        {
            botIdle[i] = false;
            if (sf == curFloorId && sr == roomId)
            {
                // 퍼즐 자리가 이 방: 방금까지 나와 함께였으면 나와 같은 문으로 들어와 걸어가고, 오래 떨어져 있었으면 이미 그 자리에 있다
                if (botKey[i] != hereKey) SpawnBot(i, now - botLastSeen[i] < 4f ? arrivePos : stand, hereKey);
                botExiting[i] = false;
                standing = true;
                goal = stand; speed = BotStandSpeed;
            }
            else
            {
                // 퍼즐 자리가 다른 방: 내 곁에 있었다면 그 방 쪽 문까지 걸어가서 나간다(그냥 사라지지 않게)
                if (botKey[i] == hereKey)
                {
                    if (!botExiting[i]) { botExiting[i] = true; botExit[i] = FindRoute(sf, sr); botExitUntil[i] = now + 7f; botPath[i] = null; }
                    if (botExit[i] != null && now < botExitUntil[i])
                    {
                        int[] b = botExit[i].rect;
                        Vector2 door = new Vector2(b[0] + b[2] * 0.5f, b[1] + b[3] * 0.5f);
                        if ((botPos[i] - door).sqrMagnitude > 14f * 14f)
                        {
                            StepBot(i, door, BotExitSpeed, false, now, out p, out prev);
                            botLastSeen[i] = now;
                            return true;
                        }
                    }
                    LeaveBot(i, sf, sr);
                }
                else if (!botAway[i]) { botAway[i] = true; botAwayFloor[i] = sf; botAwayRoom[i] = sr; }
                botArrived[i] = sf != curFloorId;
                return false;
            }
        }
        else
        {
            botExiting[i] = false;
            Trail tr = TrailAt(now - (lamp ? 0.6f : BotDelay[i]));
            bool trHere = Code(tr.floor, tr.room) == hereKey;
            if (botKey[i] != hereKey)
            {
                if (!trHere) return false;   // 아직 옛 방 · 곧 내가 지나온 문으로 따라 들어온다
                Vector2 from = tr.pos;
                if (botAway[i])
                {
                    // 다른 방 퍼즐을 마치고 돌아온다: 그 방 쪽 문에서 나타난다
                    Portal back = botAwayFloor[i] != null ? FindRoute(botAwayFloor[i], botAwayRoom[i]) : null;
                    if (back != null) { int[] b = back.rect; from = new Vector2(b[0] + b[2] * 0.5f, b[1] + b[3] * 0.5f); }
                }
                SpawnBot(i, from, hereKey);
            }
            // 따라갈 자리: 조금 전 내 발자취에서 옆으로 벌린 곳(벽 너머면 덜 벌린다)
            Vector2 basePos = trHere ? tr.pos : position;
            Vector2 off = lamp ? new Vector2(-26, 20) : BotOffset[i];
            goal = basePos + off;
            if (!SegmentWalkable(basePos, goal)) goal = basePos + off * 0.45f;
            if (!SegmentWalkable(basePos, goal)) goal = basePos;
            follow = true;
            float d = (goal - botPos[i]).magnitude;
            speed = Mathf.Lerp(BotFollowSpeed, BotCatchUpSpeed, Mathf.InverseLerp(50f, 220f, d));

            // 내가 가만히 서 있으면 가까운 전시물 앞으로 천천히 가서 둘러본다 · 내가 움직이면 바로 걸어서 돌아온다
            if (lamp || MuseumStory.Busy || now - lastMoveTime < 0.1f) botIdle[i] = false;
            else if (!botIdle[i] && now - lastMoveTime > 2.5f && PickIdleSpot(i, out Vector2 spot))
            {
                botIdle[i] = true; botIdleDir[i] = PixelSprites.DirUp; botLookAt[i] = now + Random.Range(2.5f, 4.5f);
            }
            if (botIdle[i]) { goal = botIdleSpot[i]; speed = BotIdleSpeed; follow = false; }
        }

        bool moved = StepBot(i, goal, speed, follow, now, out p, out prev);
        botLastSeen[i] = now;
        if (standing) botArrived[i] = !moved && (botPos[i] - goal).sqrMagnitude < 8f * 8f || botPath[i] != null && botPath[i].Count == 0;
        if (botIdle[i] && !moved)
        {
            facingIdle = true;
            if (now > botLookAt[i]) { botLookAt[i] = now + Random.Range(2f, 4.5f); botIdleDir[i] = LookDirs[Random.Range(0, LookDirs.Length)]; }
        }
        return true;
    }

    /// <summary>botPos[i] 를 goal 쪽으로 바닥 길을 따라 한 프레임 걷는다. 움직였으면 true.</summary>
    private bool StepBot(int i, Vector2 goal, float speed, bool follow, float now, out Vector2 p, out Vector2 prev)
    {
        Vector2 before = botPos[i];
        float dist = (goal - botPos[i]).magnitude;
        // 따라갈 때: 서 있다가 조금 벌어진 정도로는 움직이지 않는다(내가 한 걸음 옮길 때마다 꼼지락거리지 않게)
        bool hold = follow && !botWalking[i] && dist < 20f || dist < 2f;
        if (hold) botPath[i] = null;
        else if (botPath[i] == null || botPath[i].Count == 0 || (botPathGoal[i] - goal).sqrMagnitude > 10f * 10f)
        {
            if (SegmentWalkable(botPos[i], goal)) { botPath[i] = new List<Vector2> { goal }; botPathGoal[i] = goal; }
            else if (now >= botRepathAt[i])
            {
                botRepathAt[i] = now + 0.25f;
                var path = FindPath(botPos[i], goal);
                if (path != null) { botPath[i] = path; botPathGoal[i] = goal; }
            }
        }
        if (!hold) WalkPath(i, speed);
        if (follow && dist < 8f) botPath[i] = null;
        Vector2 step = botPos[i] - before;
        bool moved = step.sqrMagnitude > 0.0001f;
        botWalking[i] = moved;
        if (moved) botStillSince[i] = now;
        else if (!hold && now - botStillSince[i] > 3f && dist > 60f && follow)
        {
            // 아주 드물게 길을 못 찾으면(내 발자취는 늘 걸을 수 있는 곳) 발자취 위로 옮겨 다시 걷게 한다
            botPos[i] = NearestWalkable(goal); botPath[i] = null; botStillSince[i] = now;
        }
        p = botPos[i];
        prev = moved ? p - step.normalized * 20f : p;
        return moved;
    }

    // ---- 손전등(정전 중): 서하린(슬롯 0) 혼자 들고 있다 ----
    private const int FlashlightSlot = 0;
    private static bool FlashlightOn => !MuseumStory.PowerOn;

    /// <summary>어둠 속 빛의 중심. 서하린이 같은 화면에 있으면 서하린 쪽(손전등), 없으면 나(최소 밝기). 내가 빛 가장자리 안에 남도록 당긴다.</summary>
    private void PlaceLight()
    {
        Vector3 me = visitor.transform.position + new Vector3(0, 0.3f, 0);
        Vector3 center = me;
        if (FlashlightOn && role != FlashlightSlot && remotes != null && remotes[FlashlightSlot].gameObject.activeSelf)
        {
            Vector3 lamp = remotes[FlashlightSlot].transform.position + new Vector3(0, 0.3f, 0);
            // 빛 반지름은 화면 짧은 변의 0.42 → 나는 그 0.6 안쪽에 남게(너무 어두워 진행 불가가 되지 않게)
            float maxOff = ViewHeight / PixelsPerUnit * 0.42f * 0.6f * Mathf.Min(1f, viewCamera.aspect);
            Vector3 off = lamp - me;
            center = me + Vector3.ClampMagnitude(off, maxOff);
        }
        MuseumStory.PlayerWorld = center;
    }

    private void UpdateRemotes()
    {
        float now = Time.time;
        for (int i = 0; i < remotes.Length; i++)
        {
            bool bot = i != role && Story1Role.IsBot(i);
            bool present = bot || (i != role && Story1Net.IsRemoteHuman(i) && Story1Net.RemoteFresh(i));
            bool show = false;
            Vector2 p = default, prev = default;
            bool standing = false;
            idleFacing = false;
            if (present && bot)
            {
                // AI: 늘 바닥 격자로 길을 찾아 사람 걸음으로 걷는다(순간이동·벽 통과 없이). 방을 옮길 때만 문에서 나타나고 문으로 사라진다.
                show = UpdateBot(i, now, out p, out prev, out standing, out bool facing);
                idleFacing = facing;
                if (show != remoteWasShown[i] && !MuseumStory.Busy)
                {
                    Vector2 at = show ? p : remoteLastPos[i];
                    float near = 1f - Mathf.Clamp01((at - position).magnitude / 450f);
                    PlayPass(false, 0.25f * near + 0.06f);
                }
                remoteKey[i] = -1;
            }
            else if (present)
            {
                show = RemoteSample(i, false, now, out p, out int fl, out int rm);
                if (show && !RemoteSample(i, false, now - VelWindow, out prev, out _, out _)) prev = p;
                int key = Code(fl, rm);
                if (show)
                {
                    // 받은 위치 사이를 부드럽게 잇는다(위치는 0.15초마다 띄엄띄엄 온다 → 그대로 쓰면 뚝뚝 끊겨 보인다)
                    Vector2 target = p;
                    if (!remoteDispOk[i] || remoteDispKey[i] != key || (target - remoteDisp[i]).sqrMagnitude > 160f * 160f) remoteDisp[i] = target;
                    else
                    {
                        float gap = (target - remoteDisp[i]).magnitude;
                        remoteDisp[i] = Vector2.MoveTowards(remoteDisp[i], target, Mathf.Max(WalkPixelsPerSecond * 1.2f, gap * 6f) * Time.deltaTime);
                    }
                    remoteDispOk[i] = true; remoteDispKey[i] = key;
                    prev = remoteDisp[i] - (p - prev);
                    p = remoteDisp[i];
                }
                else remoteDispOk[i] = false;
                if (remoteKey[i] >= 0 && key != remoteKey[i] && (show || remoteWasShown[i]))
                {
                    Vector2 at = show ? p : remoteLastPos[i];
                    float near = 1f - Mathf.Clamp01((at - position).magnitude / 450f);
                    PlayPass(fl != remoteKey[i] % FloorCount, 0.3f * near + 0.08f);
                }
                remoteKey[i] = key;
            }
            else remoteKey[i] = -1;

            if (remotes[i].gameObject.activeSelf != show) remotes[i].gameObject.SetActive(show);
            remoteWasShown[i] = show;
            if (!show) continue;
            remoteLastPos[i] = p;

            Vector2 d = p - prev;
            bool moving = d.sqrMagnitude > 4f && d.sqrMagnitude < 40000f;
            if (moving)
            {
                remoteDir[i] = PixelSprites.DirectionFor(d.x, -d.y, remoteDir[i]);
                remoteWalk[i] += Time.deltaTime;
                remoteStep[i] += Time.deltaTime;
                if (Time.time < stairsUntil) remoteStep[i] = 0;
                else if (remoteStep[i] >= StepInterval * 1.1f)
                {
                    remoteStep[i] = 0;
                    float near = 1f - Mathf.Clamp01((p - position).magnitude / 450f);
                    if (near > 0.05f) Sfx.Play("step_" + Random.Range(1, 6), 0.3f * near);
                }
            }
            else
            {
                remoteWalk[i] = 0;
                remoteStep[i] = StepInterval * 0.5f;
                if (standing) remoteDir[i] = PixelSprites.DirUp;   // 퍼즐 자리에서는 장치를 바라본다
                else if (idleFacing) remoteDir[i] = botIdleDir[i];   // 전시물 앞에서 둘러보기
            }

            int frame = remoteWalk[i] == 0 ? PixelSprites.IdleFrame : PixelSprites.WalkCycle[(int)(remoteWalk[i] / PixelSprites.FrameTime) % PixelSprites.WalkCycle.Length];
            Sprite sp = i < storyFrames.Length ? storyFrames[i][remoteDir[i] * PixelSprites.Cols + frame] : null;
            if (sp != null) remotes[i].sprite = sp;
            remotes[i].transform.localScale = Vector3.one * StorySpriteScale;
            remotes[i].transform.position = new Vector3(p.x / PixelsPerUnit, -p.y / PixelsPerUnit + 0.55f * StorySpriteScale, 0);
            remotes[i].sortingOrder = Mathf.RoundToInt(p.y);
        }
    }

    private void UpdateVisitorSprite()
    {
        int frame = walkTime == 0 ? PixelSprites.IdleFrame : PixelSprites.WalkCycle[(int)(walkTime / PixelSprites.FrameTime) % PixelSprites.WalkCycle.Length];
        Sprite storySprite = role < storyFrames.Length ? storyFrames[role][direction * PixelSprites.Cols + frame] : null;
        visitor.sprite = storySprite != null ? storySprite : portraits[role];
        visitor.transform.localScale = storySprite != null ? Vector3.one * StorySpriteScale : Vector3.one;
    }

    // ---------------------------------------------------------------- 충돌 · 문 · 전시물
    /// <summary>지금 방에서 이 지도 좌표를 걸을 수 있는가(고양이 등 장식도 벽을 뚫지 않게).</summary>
    public bool Walkable(Vector2 map) => room != null && CanMove(map.x, map.y);

    /// <summary>
    /// 아무 층 · 방에서 숨기 좋은 바닥 자리를 rng 로 고른다(단풍이 숨바꼭질). 걸을 수 있고, 둘레에 뛸 자리가 있고,
    /// 문 · 퍼즐 장치 · 조사할 전시물 앞에서 떨어진 곳. 못 찾으면 false.
    /// </summary>
    public bool RandomHideSpot(string floorId, string roomName, System.Random rng, out Vector2 spot)
    {
        spot = default;
        if (museum == null || !museum.floors.TryGetValue(floorId, out Floor f) || f.scenes == null || !f.scenes.TryGetValue(roomName, out Room r)) return false;
        bool Can(float x, float y) => CanMoveIn(f, r, x, y);
        for (int tries = 0; tries < 400; tries++)
        {
            var p = new Vector2(r.view[0] + 30 + (float)rng.NextDouble() * (r.view[2] - 60), r.view[1] + 30 + (float)rng.NextDouble() * (r.view[3] - 60));
            if (!Can(p.x, p.y)) continue;
            int room8 = 0;
            for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; if (Can(p.x + Mathf.Cos(a) * 48f, p.y + Mathf.Sin(a) * 48f)) room8++; }
            if (room8 < 5) continue;   // 구석에 끼지 않게(둘레에 뛸 자리)
            bool near = false;
            foreach (Portal pt in f.portals) { if (pt.sourceScene != roomName || pt.rect == null) continue; var c = new Vector2(pt.rect[0] + pt.rect[2] * 0.5f, pt.rect[1] + pt.rect[3] * 0.5f); if ((c - p).sqrMagnitude < 90f * 90f) near = true; }
            foreach (Exhibit ex in f.objects) if (ex.interact != null && ex.room == roomName && (Point(ex.interact) - p).sqrMagnitude < 70f * 70f) near = true;
            foreach (var pz in MuseumGame.Puzzles) if (pz.NearStation(floorId, roomName, p, 90f)) near = true;
            if (near) continue;
            spot = p; return true;
        }
        return false;
    }

    /// <summary>지금 방의 문(다른 방으로 이어진 곳) 가운데 자리들.</summary>
    public List<Vector2> DoorPoints()
    {
        var list = new List<Vector2>();
        if (floor == null || floor.portals == null) return list;
        foreach (Portal pt in floor.portals)
            if (pt.sourceScene == roomId && !string.IsNullOrEmpty(pt.targetFloor) && pt.rect != null)
                list.Add(new Vector2(pt.rect[0] + pt.rect[2] * 0.5f, pt.rect[1] + pt.rect[3] * 0.5f));
        return list;
    }

    /// <summary>a → b 직선이 지금 방의 바닥만 지나는가.</summary>
    public bool WalkableLine(Vector2 a, Vector2 b) => room != null && SegmentWalkable(a, b);

    private bool CanMove(float x, float y) => CanMoveIn(floor, room, x, y);

    private bool CanMoveIn(Floor floor, Room room, float x, float y)
    {
        int[] view = room.view;
        if (x < view[0] || x >= view[0] + view[2] || y < view[1] || y >= view[1] + view[3]) return false;
        int left = Mathf.FloorToInt(x - museum.player.footprint[0] * 0.5f);
        int right = Mathf.CeilToInt(x + museum.player.footprint[0] * 0.5f);
        int top = Mathf.FloorToInt(y - museum.player.footprint[1] * 0.5f);
        int bottom = Mathf.CeilToInt(y + museum.player.footprint[1] * 0.5f);
        if (top < 0 || bottom >= floor.walkableRows.Length) return false;
        for (int row = top; row <= bottom; row++)
        {
            bool fits = false;
            foreach (int[] span in floor.walkableRows[row]) fits |= left - WallSideClearance >= span[0] && right + WallSideClearance < span[1];
            if (!fits) return false;
        }
        foreach (Exhibit exhibit in floor.objects)
        {
            int[] b = exhibit.collision;
            if (b != null && left < b[0] + b[2] && right >= b[0] && top < b[1] + b[3] && bottom >= b[1]) return false;
        }
        return true;
    }

    private Portal NearbyPortal()
    {
        float bestDistance = PortalReach * PortalReach;
        Portal nearest = null;
        foreach (Portal portal in floor.portals)
        {
            if (string.IsNullOrEmpty(portal.targetFloor) || portal.sourceScene != roomId) continue;
            int[] b = portal.rect;
            float dx = position.x - Mathf.Clamp(position.x, b[0], b[0] + b[2]);
            float dy = position.y - Mathf.Clamp(position.y, b[1], b[1] + b[3]);
            float distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = portal;
        }
        return nearest;
    }

    private Exhibit NearbyExhibit()
    {
        float range = InspectRadius * InspectRadius;
        Exhibit nearest = null;
        foreach (Exhibit exhibit in floor.objects)
        {
            if (exhibit.interact == null || exhibit.room != roomId || !MuseumGame.Exhibits.ContainsKey(exhibit.id)) continue;
            float distance = (Point(exhibit.interact) - position).sqrMagnitude;
            if (distance >= range) continue;
            range = distance;
            nearest = exhibit;
        }
        return nearest;
    }

    private void PlaceVisitor()
    {
        UpdateVisitorSprite();
        float footOffset = visitor.sprite == portraits[role] ? museum.player.height * 0.5f / PixelsPerUnit : 0.55f * StorySpriteScale;
        visitor.transform.position = new Vector3(position.x / PixelsPerUnit, -position.y / PixelsPerUnit + footOffset, 0);
        visitor.sortingOrder = Mathf.RoundToInt(position.y);

        // 모든 방을 같은 배율로 본다(방마다 확대가 달라 어지럽다는 의견). 방보다 화면이 크면 방 가운데에 둔다.
        int[] b = room.view;
        float aspect = Mathf.Max(0.01f, viewCamera.aspect);
        float halfHeight = ViewHeight * 0.5f;
        float halfWidth = halfHeight * aspect;
        float x = b[2] > halfWidth * 2f ? Mathf.Clamp(position.x, b[0] + halfWidth, b[0] + b[2] - halfWidth) : b[0] + b[2] * 0.5f;
        float y = b[3] > halfHeight * 2f ? Mathf.Clamp(position.y - 18, b[1] + halfHeight, b[1] + b[3] - halfHeight) : b[1] + b[3] * 0.5f;
        viewCamera.orthographicSize = halfHeight / PixelsPerUnit;
        viewCamera.transform.position = new Vector3(x / PixelsPerUnit, -y / PixelsPerUnit, -10);
        if (remotes != null) PlaceLight(); else MuseumStory.PlayerWorld = visitor.transform.position + new Vector3(0, 0.3f, 0);
    }

    // ---------------------------------------------------------------- 화면 표시가 묻는 것
    private static string FloorShort(string f) => f == "1F" ? "1층" : f == "2F" ? "2층" : "지하";

    public string PlaceLabel
    {
        get
        {
            if (room == null) return "";
            string r = room.label ?? "";
            if (r.StartsWith("ㄱ자 ")) r = r.Substring(3);
            return FloorShort(curFloorId) + "  ·  " + r;
        }
    }

    /// <summary>동료 i 의 이름표 자리(화면 좌표, 머리 위). 나 자신이거나 지금 화면에 없으면 false.</summary>
    public bool TagScreen(int i, out Vector3 screen)
    {
        screen = default;
        if (i == role) return false;
        SpriteRenderer sr = remotes != null && i < remotes.Length && remotes[i].gameObject.activeSelf ? remotes[i] : null;
        if (sr == null) return false;
        screen = viewCamera.WorldToScreenPoint(new Vector3(sr.bounds.center.x, sr.bounds.max.y - sr.bounds.size.y * 0.08f, 0));
        return screen.x > -50 && screen.x < Screen.width + 50 && screen.y > -50 && screen.y < Screen.height + 50;
    }

    /// <summary>지금 E 로 할 수 있는 일(머리 위 안내). 없으면 null.</summary>
    public string PromptAction(out Vector3 head)
    {
        head = viewCamera.WorldToScreenPoint(new Vector3(visitor.bounds.center.x, visitor.bounds.max.y, 0));
        foreach (var p in MuseumGame.Puzzles)
            if (!p.Done && p.Near(curFloorId, roomId, position, InspectRadius, out string active)) return active;
        Portal portal = NearbyPortal();
        if (portal != null)
        {
            string name = portal.name ?? "";
            string label = name == "복도로 돌아가는 문" ? "복도로 나가기" : name == "반원 홀로 돌아가는 문" ? "반원 홀로 나가기"
                : name.EndsWith(" 문") ? name.Substring(0, name.Length - 2) + " 들어가기" : name;
            if (MuseumGame.DoorLocked(portal.name, portal.targetFloor, portal.targetScene, out _)) label += " (잠김)";
            return label;
        }
        foreach (var p in MuseumGame.Puzzles)
            if (p.Near(curFloorId, roomId, position, InspectRadius, out string prompt)) return prompt;
        Exhibit ex = NearbyExhibit();
        if (ex != null && MuseumGame.Exhibits.TryGetValue(ex.id, out var lore)) return lore.title + " 살펴보기";
        return null;
    }

    private string routeKey; private Portal routePortal;
    private string routeFloor, routeRoom, routeTf, routeTr; private int routeStage;

    /// <summary>다음 목표 표시(화면 좌표 + 이름). 가까이 왔거나 목표가 없으면 null.</summary>
    public string GuideTarget(out Vector3 screen, bool followNavigator = false)
    {
        screen = default;
        var cur = MuseumGame.Current;
        if (cur == null || cur.Done) return null;
        string tf = cur.GuideFloor, tr = cur.GuideRoom;
        Vector2 target = cur.GuidePoint;
        string label = cur.GuideLabel;
        if (tf == curFloorId && tr == roomId)
        {
            if ((target - position).sqrMagnitude < (InspectRadius * 1.6f) * (InspectRadius * 1.6f)) return null;
            if (label == null) return null;
        }
        else
        {
            // 매 프레임 문자열을 만들지 않게 바뀐 경우에만 길을 다시 찾는다
            if (routeKey == null || routeFloor != curFloorId || routeRoom != roomId || routeTf != tf || routeTr != tr || routeStage != MuseumGame.Stage)
            {
                routeKey = "ok"; routeFloor = curFloorId; routeRoom = roomId; routeTf = tf; routeTr = tr; routeStage = MuseumGame.Stage;
                routePortal = FindRoute(tf, tr);
            }
            if (routePortal == null) return null;
            int[] b = routePortal.rect;
            target = new Vector2(b[0] + b[2] * 0.5f, b[1] + b[3] * 0.5f);
            if ((target - position).sqrMagnitude < 40f * 40f) return null;
            string n = routePortal.name ?? "";
            label = n.Contains("계단") ? n : n == "복도로 돌아가는 문" ? "복도로" : n == "반원 홀로 돌아가는 문" ? "반원 홀로" : n;
        }
        // 내비게이터가 AI면 그 친구가 손짓으로 길을 알려 주는 것 · 이름을 붙여 보여 준다
        if (followNavigator) label = MuseumGame.NameOf(MuseumGame.Navigator) + ": " + label;
        screen = viewCamera.WorldToScreenPoint(new Vector3(target.x / PixelsPerUnit, -target.y / PixelsPerUnit, 0));
        return label;
    }

    private Portal FindRoute(string targetFloor, string destination)
    {
        var visited = new HashSet<(string, string)> { (curFloorId, roomId) };
        var pending = new Queue<(string floor, string room, Portal first)>();
        pending.Enqueue((curFloorId, roomId, null));
        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            foreach (Portal portal in museum.floors[node.floor].portals)
            {
                if (portal.sourceScene != node.room || string.IsNullOrEmpty(portal.targetFloor)) continue;
                if (MuseumGame.DoorLocked(portal.name, portal.targetFloor, portal.targetScene, out _)) continue;
                if (!visited.Add((portal.targetFloor, portal.targetScene))) continue;
                Portal first = node.first ?? portal;
                if (portal.targetFloor == targetFloor && portal.targetScene == destination) return first;
                pending.Enqueue((portal.targetFloor, portal.targetScene, first));
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- 지도(내비게이터)
    public static readonly string[] MapFloors = { "1F", "2F", "B1" };

    /// <summary>이 층에서 방들이 차지하는 범위(지도 px). 지도를 건물 크기에 맞춰 확대하는 데 쓴다.</summary>
    public Rect FloorBounds(string floorId)
    {
        if (museum == null || !museum.floors.TryGetValue(floorId, out Floor f) || f.scenes == null) return new Rect(0, 0, 2048, 1664);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var r in f.scenes.Values)
        {
            if (r.view == null || r.view.Length < 4) continue;
            x0 = Mathf.Min(x0, r.view[0]); y0 = Mathf.Min(y0, r.view[1]);
            x1 = Mathf.Max(x1, r.view[0] + r.view[2]); y1 = Mathf.Max(y1, r.view[1] + r.view[3]);
        }
        return x1 > x0 ? Rect.MinMaxRect(x0, y0, x1, y1) : new Rect(0, 0, 2048, 1664);
    }

    /// <summary>이 층의 방들(이름, 범위 · 지도 px).</summary>
    public List<(string id, string label, Rect rect)> FloorRooms(string floorId)
    {
        var list = new List<(string, string, Rect)>();
        if (museum == null || !museum.floors.TryGetValue(floorId, out Floor f) || f.scenes == null) return list;
        foreach (var kv in f.scenes)
            if (kv.Value.view != null && kv.Value.view.Length >= 4)
                list.Add((kv.Key, kv.Value.label ?? kv.Key, new Rect(kv.Value.view[0], kv.Value.view[1], kv.Value.view[2], kv.Value.view[3])));
        return list;
    }

    /// <summary>방 이름을 놓을 자리 · 그 방에서 걸을 수 있는 바닥의 한가운데(ㄱ자 방이면 바닥 위의 가장 가까운 점).</summary>
    public Vector2 RoomLabelPoint(string floorId, Rect view)
    {
        if (museum == null || !museum.floors.TryGetValue(floorId, out Floor f) || f.walkableRows == null) return view.center;
        double sx = 0, sy = 0; long n = 0;
        int y0 = Mathf.Max(0, (int)view.yMin), y1 = Mathf.Min(f.walkableRows.Length, (int)view.yMax);
        for (int y = y0; y < y1; y += 6)
            foreach (int[] sp in f.walkableRows[y])
            {
                int a = Mathf.Max(sp[0], (int)view.xMin), b = Mathf.Min(sp[1], (int)view.xMax);
                if (b <= a) continue;
                sx += (a + b) * 0.5 * (b - a); sy += (double)y * (b - a); n += b - a;
            }
        if (n == 0) return view.center;
        var c = new Vector2((float)(sx / n), (float)(sy / n));
        Vector2 best = c; float bd = float.MaxValue;
        for (int y = y0; y < y1; y += 6)
            foreach (int[] sp in f.walkableRows[y])
            {
                float a = Mathf.Max(sp[0], view.xMin), b = Mathf.Min(sp[1], view.xMax);
                if (b <= a) continue;
                var q = new Vector2(Mathf.Clamp(c.x, a + 60f, Mathf.Max(a + 60f, b - 60f)), y);
                float d = (q - c).sqrMagnitude;
                if (d < bd) { bd = d; best = q; }
            }
        return best;
    }

    public Sprite FloorSprite(string floorId)
    {
        foreach (GameObject root in floorRoots)
        {
            if (root.name != floorId) continue;
            var st = root.transform.Find("Structure");
            var sr = st != null ? st.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr.sprite : null;
        }
        return null;
    }

    /// <summary>캐릭터 i 가 지도 위 어디에 있는가(층 · 지도 픽셀). 모르면 false.</summary>
    public bool CharacterOnMap(int i, out string floorId, out Vector2 map)
    {
        floorId = null; map = default;
        if (i == role) { floorId = curFloorId; map = position; return true; }
        if (remotes != null && i < remotes.Length && remotes[i].gameObject.activeSelf)
        {
            var p = remotes[i].transform.position;
            floorId = curFloorId; map = new Vector2(p.x * PixelsPerUnit, -(p.y - 0.55f * StorySpriteScale) * PixelsPerUnit);
            return true;
        }
        if (Story1Role.IsBot(i) && MuseumGame.BotTarget(i, out string bf, out string br, out Vector2 stand)) { floorId = bf; map = stand; return true; }
        if (RemoteHumanAt(i, out Vector2 rp, out string rf, out _)) { floorId = rf; map = rp; return true; }
        return false;
    }

    // ---------------------------------------------------------------- 시험용(에디터 자동 확인)
    public void DebugTeleport(string floorId, string roomName, float x, float y) { Enter(floorId, roomName, new[] { Mathf.RoundToInt(x), Mathf.RoundToInt(y) }); }

    // ---------------------------------------------------------------- 지도 자료
    private sealed class Layout { public Dictionary<string, Floor> floors; public Player player; }

    private sealed class Floor
    {
        public string label, defaultScene;
        public int[] spawn;
        public int[][][] walkableRows;
        public Dictionary<string, Room> scenes;
        public Exhibit[] objects;
        public Portal[] portals;
    }

    private sealed class Room { public string label; public int[] view; }
    private sealed class Exhibit { public string id, name, room; public int[] interact, collision; }
    private sealed class Portal { public string name, sourceScene, targetScene, targetFloor; public int[] rect, arrival; }
    private sealed class Player { public int height, inspectRadius; public int[] footprint; }
}
