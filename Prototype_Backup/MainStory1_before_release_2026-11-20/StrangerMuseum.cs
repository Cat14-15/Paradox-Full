using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class StrangerMuseum : MonoBehaviour
{
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
    private Camera viewCamera;
    private Layout museum;
    private Floor floor;
    private Room room;
    private string roomId;
    private string curFloorId;
    private Vector2 position;
    private Exhibit inspected;
    private int role;
    private int direction = PixelSprites.DirDown;
    private float walkTime;
    private Vector2 moveInput;
    private Sprite[][] storyFrames;
    private GUIStyle label;
    private GUIStyle button;
    private GUIStyle promptLabel;
    private GUIStyle promptKey;
    private Portal objectivePortal;
    private int routedStep = -1;
    private bool guideObjectives;
    private GUIStyle inspectionTitle;
    private GUIStyle inspectionBody;
    private GUIStyle inspectionDetail;
    private Vector2 scroll;

    // ---- 멀티: 다른 사람(역할 슬롯 0~3)을 같은 층에서 보여 준다 ----
    private static readonly string[] FloorIds = { "1F", "2F", "B1" };
    private const float FloorStride = 10000f;   // 위치 RPC 의 x 에 층 번호를 실어 보낸다(x + 층 * 10000)
    private SpriteRenderer[] remotes;
    private int[] remoteDir;
    private float[] remoteWalk;
    private Vector2[] remoteLast;
    private float nextPosSend;
    private float stepTimer;
    private float[] remoteStep;
    private bool[] remoteWasShown;
    private int[] remoteKey;            // 동료가 있던 (층, 방) 번호 — 바뀌면 문·계단을 지난 것
    private Vector2[] remoteLastPos;
    private const float StepInterval = 0.34f;
    private Vector2 lastSent;
    private struct Trail { public float t; public Vector2 pos; public int floor, room; }
    private readonly List<Trail> trail = new List<Trail>();
    private static readonly float[] BotDelay = { 1.6f, 0.6f, 1.1f, 1.6f };
    private static readonly Vector2[] BotOffset = { new Vector2(0, 26), new Vector2(-30, 8), new Vector2(30, 8), new Vector2(0, 40) };

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();
        guideObjectives = gameObject.scene.name == "MainStory1";
        if (guideObjectives)
        {
            chapterStart = Time.time;
            Story1Net.Received += ReceiveChapterClear;
        }
        museum = JsonConvert.DeserializeObject<Layout>(layout.text);
        storyFrames = (storyRoleSheets ?? System.Array.Empty<Texture2D>()).Select(Story1Companions.Slice).ToArray();
        role = Mathf.Clamp(Story1Role.Current, 0, Roles.Count - 1);
        Bgm.Play("story1", 0.3f);   // 새 맵에서도 배경음악이 이어지도록(이전 장면에서 멈춘 음악을 다시 건다)
        BuildRemotes();
        if (GetComponent<MuseumChat>() == null) gameObject.AddComponent<MuseumChat>();
        if (!guideObjectives)
        {
            if (GetComponent<MuseumValves>() == null) gameObject.AddComponent<MuseumValves>();
            if (GetComponent<MuseumGas>() == null) gameObject.AddComponent<MuseumGas>();
            if (GetComponent<MuseumBreakers>() == null) gameObject.AddComponent<MuseumBreakers>();
        }
        if (GetComponent<MuseumStory>() == null) gameObject.AddComponent<MuseumStory>();
        Enter("1F");
    }

    private void OnDestroy()
    {
        if (guideObjectives) Story1Net.Received -= ReceiveChapterClear;
        if (valveFace != null) Destroy(valveFace);
    }

    private void BuildRemotes()
    {
        remotes = new SpriteRenderer[Roles.Count];
        remoteDir = new int[Roles.Count];
        remoteWalk = new float[Roles.Count];
        remoteStep = new float[Roles.Count];
        remoteWasShown = new bool[Roles.Count];
        remoteKey = new int[Roles.Count];
        remoteLastPos = new Vector2[Roles.Count];
        for (int i = 0; i < remoteKey.Length; i++) remoteKey[i] = -1;
        remoteLast = new Vector2[Roles.Count];
        for (int i = 0; i < remotes.Length; i++)
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
        routedStep = -1;
        room = floor.scenes[roomId];
        position = Point(arrival ?? floor.spawn);
        Vector2 spread = position + new Vector2((role - 1.5f) * 24f, 0);   // 네 명이 한 점에 겹치지 않게 나란히
        if (CanMove(spread.x, spread.y)) position = spread;
        inspected = null;
        moveInput = Vector2.zero;
        walkTime = 0;

        foreach (GameObject root in floorRoots)
            root.SetActive(root.name == floorId);

        MuseumPuzzles.SetLocation(curFloorId, roomId);
        PlaceVisitor();
    }

    private void CloseInspection()
    {
        if (inspected == null)
            return;
        inspected = null;
        Sfx.Play("book_close", 0.6f);
    }

    private static Vector2 Point(int[] pair) => new Vector2(pair[0], pair[1]);

    private void Update()
    {
        if (guideObjectives) TryFinishTeamOperation();
        Keyboard keys = Keyboard.current;
        if (ChapterPanelOpen)
        {
            moveInput = Vector2.zero;
            if (keys != null && keys.escapeKey.wasPressedThisFrame && !ChapterComplete)
                CloseChapter();
            return;
        }
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame && !MuseumChat.Typing) hudUntil = Time.time + 7f;   // H: 안내 칸을 다시 보여 준다
        moveInput = inspected == null && !MuseumChat.Typing && !MuseumPuzzles.AnyOpen && !MuseumStory.Busy ? Story1Scene.ReadMovement(keys).normalized : Vector2.zero;
        if (keys == null)
            return;

        if (keys.escapeKey.wasPressedThisFrame)
            CloseInspection();
        if (keys.tabKey.wasPressedThisFrame && Story1Role.Solo && !MuseumChat.Typing)   // 접속 없이 혼자 확인할 때만 역할을 돌려 본다
        {
            Story1Role.Cycle();
            role = Story1Role.Current;
            UpdateVisitorSprite();
        }

        if (inspected != null || MuseumChat.Typing || MuseumPuzzles.AnyOpen || MuseumStory.Busy)
            return;

        bool ePressed = keys.eKey.wasPressedThisFrame || promptClicked;
        promptClicked = false;
        if (ePressed)
        {
            Portal portal = NearbyPortal();
            if (portal != null && ChapterDoorLocked(portal))
            {
                if (guideObjectives) Sfx.Play("door_locked", .6f);
                else MuseumPuzzles.BumpLockedDoor(portal.name);
            }
            else if (portal != null)
            {
                PlayPass(portal.name != null && portal.name.Contains("계단"), 0.6f, true);   // 계단은 발소리, 문은 문 소리
                Enter(portal.targetFloor, portal.targetScene, portal.arrival);
            }
            else
            {
                if (TryOpenChapter()) return;
                if (!guideObjectives && MuseumPuzzles.TryInteract(position, museum.player.inspectRadius))
                {
                    moveInput = Vector2.zero;
                    return;
                }
                inspected = NearbyExhibit();
                if (inspected != null)
                {
                    moveInput = Vector2.zero;
                    Sfx.Play("book_open", 0.6f);
                }
                scroll = Vector2.zero;
            }
        }
    }

    private void FixedUpdate()
    {
        Vector2 previous = position;
        Vector2 move = new Vector2(moveInput.x, -moveInput.y) * (WalkPixelsPerSecond * Time.fixedDeltaTime);
        int steps = Mathf.Max(1, Mathf.CeilToInt(move.magnitude / 2));
        move /= steps;

        for (int i = 0; i < steps && move != Vector2.zero; i++)
        {
            if (CanMove(position.x + move.x, position.y))
                position.x += move.x;
            if (CanMove(position.x, position.y + move.y))
                position.y += move.y;
        }

        direction = PixelSprites.DirectionFor(moveInput.x, moveInput.y, direction);
        walkTime = position != previous ? walkTime + Time.fixedDeltaTime : 0;
        if (walkTime > 0)
        {
            stepTimer += Time.fixedDeltaTime;
            if (Time.time < stairsUntil) stepTimer = 0f;
            else if (stepTimer >= StepInterval) { stepTimer -= StepInterval; Sfx.Play("step_" + Random.Range(1, 6), 0.45f); }
        }
        else
            stepTimer = StepInterval * 0.5f;   // 멈췄다 걸으면 반 박자 뒤에 첫 발소리
        RecordTrail();   // 걷는 갱신(고정 간격)마다 기록해야 AI 동료가 끊기지 않고 부드럽게 따라온다
        PlaceVisitor();
    }

    private void LateUpdate()
    {
        if (room == null || remotes == null)
            return;
        SendMyPosition();
        UpdateRemotes();
    }

    private readonly Dictionary<string, List<string>> roomOrder = new Dictionary<string, List<string>>();

    // 층 안의 방 번호(JSON 에 적힌 차례). 위치를 보낼 때 층·방 번호를 같이 실어, 받는 쪽이 문을 지난 걸 알 수 있다.
    private int RoomIndex(string floorId, string room)
    {
        if (!roomOrder.TryGetValue(floorId, out var list))
            roomOrder[floorId] = list = museum.floors[floorId].scenes.Keys.ToList();
        return Mathf.Max(0, list.IndexOf(room));
    }

    private const int FloorCount = 3;
    private static int Code(int floorIdx, int roomIdx) => floorIdx + FloorCount * roomIdx;

    private void RecordTrail()
    {
        float now = Time.time;
        trail.Add(new Trail { t = now, pos = position, floor = FloorIndex(), room = RoomIndex(curFloorId, roomId) });
        while (trail.Count > 2 && trail[1].t < now - 4f)
            trail.RemoveAt(0);
    }

    // 시각 t 에 내가 있던 자리(층·방이 바뀌거나 크게 튄 구간은 보간하지 않는다)
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

    // 움직일 때 0.15초마다, 가만히 있어도 0.8초마다(동료 화면에서 사라지지 않게)
    private void SendMyPosition()
    {
        if (!Story1Net.HasHumanPeers || Time.time < nextPosSend)
            return;
        bool moved = (position - lastSent).sqrMagnitude > 1f;
        nextPosSend = Time.time + (moved ? 0.15f : 0.8f);
        lastSent = position;
        Story1Net.SendPos(new Vector2(position.x + Code(FloorIndex(), RoomIndex(curFloorId, roomId)) * FloorStride, position.y));
    }

    private const float VelWindow = 0.12f;   // 이 시간 동안 얼마나 움직였는지로 걷는 중인지 정한다

    // 역할 i 가 시각 t 에 있던 자리와 (층, 방). 반환값: 나와 같은 층이라 보여야 하는가.
    private bool RemoteSample(int i, bool bot, float t, out Vector2 p, out int fl, out int rm)
    {
        if (bot)
        {
            Trail tr = TrailAt(t - BotDelay[i]);
            p = tr.pos + BotOffset[i];
            if (!CanMove(p.x, p.y))
                p = tr.pos;
            fl = tr.floor; rm = tr.room;
            return fl == FloorIndex();
        }
        Vector2 raw = Story1Net.RemoteAt(i, t - 0.35f);
        int code = Mathf.Max(0, Mathf.RoundToInt(raw.x / FloorStride));
        p = new Vector2(raw.x - code * FloorStride, raw.y);
        fl = code % FloorCount; rm = code / FloorCount;
        return fl == FloorIndex();   // 다른 층에 있는 사람은 보이지 않는다
    }

    // 문 소리 / 계단 소리(층을 오갈 때는 계단이라 발소리)
    private float stairsUntil;   // 계단 소리가 나는 동안은 평소 걷는 발소리를 끈다(겹쳐서 이상하게 들리지 않게)

    private void PlayPass(bool stairs, float volume, bool mine = false)
    {
        if (stairs)
        {
            if (mine) stairsUntil = Time.time + 0.75f;
            else if (Time.time < stairsUntil) return;   // 내 계단 소리와 겹치면 동료 쪽은 생략
            StartCoroutine(Stairs(volume));
        }
        else Sfx.Play(Random.value < 0.5f ? "door_open" : "door_open2", volume);
    }

    private System.Collections.IEnumerator Stairs(float volume)
    {
        for (int k = 0; k < 3; k++)
        {
            Sfx.Play("step_" + Random.Range(1, 6), volume);
            yield return new WaitForSeconds(0.24f);
        }
    }

    private readonly Vector2[] botPos = new Vector2[4];
    private readonly bool[] botStanding = new bool[4];
    private const float BotWalkSpeed = 260f;   // 밸브까지 걸어가는 AI 의 속도(지도 픽셀/초)

    private bool promptClicked;   // 화면 아래 "E …" 안내 박스를 마우스로 눌렀다

    private void UpdateRemotes()
    {
        float now = Time.time;
        for (int i = 0; i < remotes.Length; i++)
        {
            bool bot = i != role && Story1Role.IsBot(i);   // AI 동료: 내가 지나온 길을 조금 늦게 따라온다
            bool present = bot || (i != role && Story1Net.IsRemoteHuman(i) && Story1Net.RemoteFresh(i));
            bool show = false;
            Vector2 p = default, prev = default;
            if (present)
            {
                show = RemoteSample(i, bot, now, out p, out int fl, out int rm);
                if (show && !RemoteSample(i, bot, now - VelWindow, out prev, out _, out _))
                    prev = p;
                // 동료가 방·층을 옮겼다(문이나 계단을 지났다): 가까울수록 크게. 층이 바뀌면 계단이라 발소리.
                int key = Code(fl, rm);
                if (remoteKey[i] >= 0 && key != remoteKey[i] && (show || remoteWasShown[i]))
                {
                    Vector2 at = show ? p : remoteLastPos[i];
                    float near = 1f - Mathf.Clamp01((at - position).magnitude / 450f);
                    PlayPass(fl != remoteKey[i] % FloorCount, 0.3f * near + 0.08f);
                }
                remoteKey[i] = key;
            }
            else
                remoteKey[i] = -1;
            if (remotes[i].gameObject.activeSelf != show)
                remotes[i].gameObject.SetActive(show);
            remoteWasShown[i] = show;
            if (show) remoteLastPos[i] = p;
            if (!show)
            {
                botStanding[i] = false; MuseumPuzzles.SetReached(i, false);
                continue;
            }

            // 밸브를 잡은 AI 는 따라오지 않고 자기 밸브 앞까지 걸어가서 선다
            if (bot && MuseumPuzzles.BotStand(i, out Vector2 stand))
            {
                if (!botStanding[i]) { botStanding[i] = true; botPos[i] = p; }
                Vector2 to = stand - botPos[i];
                bool arrived = to.sqrMagnitude < 4f;
                if (!arrived) botPos[i] = Vector2.MoveTowards(botPos[i], stand, BotWalkSpeed * Time.deltaTime);
                p = botPos[i];
                prev = arrived ? p : p - to.normalized * BotWalkSpeed * VelWindow;
                MuseumPuzzles.SetReached(i, arrived);
                remoteLastPos[i] = p;
            }
            else { botStanding[i] = false; MuseumPuzzles.SetReached(i, false); }

            // 프레임 사이 위치 차이는 고정 갱신 때문에 들쭉날쭉하므로, 짧은 시간 구간의 이동량으로 걷는 중인지·방향을 정한다
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
                    float near = 1f - Mathf.Clamp01((p - position).magnitude / 450f);   // 멀수록 작게
                    if (near > 0.05f) Sfx.Play("step_" + Random.Range(1, 6), 0.3f * near);
                }
            }
            else
            {
                remoteWalk[i] = 0;
                remoteStep[i] = StepInterval * 0.5f;
            }

            int frame = remoteWalk[i] == 0
                ? PixelSprites.IdleFrame
                : PixelSprites.WalkCycle[(int)(remoteWalk[i] / PixelSprites.FrameTime) % PixelSprites.WalkCycle.Length];
            Sprite sp = i < storyFrames.Length ? storyFrames[i][remoteDir[i] * PixelSprites.Cols + frame] : null;
            if (sp != null)
                remotes[i].sprite = sp;
            remotes[i].transform.localScale = Vector3.one * StorySpriteScale;
            remotes[i].transform.position = new Vector3(p.x / PixelsPerUnit, -p.y / PixelsPerUnit + 0.55f * StorySpriteScale, 0);
            remotes[i].sortingOrder = Mathf.RoundToInt(p.y);
        }
    }

    private void DrawNameTags()
    {
        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false };
            nameStyle.font = label.font;
        }
        for (int i = 0; i < Roles.Count; i++)
        {
            Transform t = i == role ? visitor.transform : remotes[i].gameObject.activeSelf ? remotes[i].transform : null;
            if (t == null)
                continue;
            string name = Roles.DefaultName(i);
            var info = Session.State?.players[i];
            if (Story1Net.Online && info != null && !Story1Role.IsBot(i) && !string.IsNullOrEmpty(info.name))
                name = info.name;
            if (i == role)
                name += " (나)";
            else if (Story1Role.IsBot(i))
                name += " (AI)";
            SpriteRenderer sr = i == role ? visitor : remotes[i];
            Vector3 feet = viewCamera.WorldToScreenPoint(new Vector3(sr.bounds.center.x, sr.bounds.min.y, 0));
            var rect = new Rect(feet.x - 80, Screen.height - feet.y + 2, 160, 22);
            if (i != role && !MuseumStory.IsLit(new Vector2(feet.x, Screen.height - feet.y - 20)))
                continue;   // 정전 중에는 손전등 불빛 안의 동료 이름표만 보인다
            if (MuseumChat.CoversGuiRect(rect))
                continue;   // 채팅창 아래에 가려지는 이름표는 그리지 않는다(OnGUI 는 항상 맨 위에 그려지므로)
            Color prev = GUI.color;
            nameStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), name, nameStyle);
            nameStyle.normal.textColor = Roles.Color(i);
            GUI.Label(rect, name, nameStyle);
            GUI.color = prev;
        }
    }

    private GUIStyle nameStyle;

    private void UpdateVisitorSprite()
    {
        int frame = walkTime == 0
            ? PixelSprites.IdleFrame
            : PixelSprites.WalkCycle[(int)(walkTime / PixelSprites.FrameTime) % PixelSprites.WalkCycle.Length];
        Sprite storySprite = role < storyFrames.Length ? storyFrames[role][direction * PixelSprites.Cols + frame] : null;
        visitor.sprite = storySprite != null ? storySprite : portraits[role];
        visitor.transform.localScale = storySprite != null ? Vector3.one * StorySpriteScale : Vector3.one;
    }

    private bool CanMove(float x, float y)
    {
        int[] view = room.view;
        if (x < view[0] || x >= view[0] + view[2] || y < view[1] || y >= view[1] + view[3])
            return false;

        int left = Mathf.FloorToInt(x - museum.player.footprint[0] * 0.5f);
        int right = Mathf.CeilToInt(x + museum.player.footprint[0] * 0.5f);
        int top = Mathf.FloorToInt(y - museum.player.footprint[1] * 0.5f);
        int bottom = Mathf.CeilToInt(y + museum.player.footprint[1] * 0.5f);
        if (top < 0 || bottom >= floor.walkableRows.Length)
            return false;

        for (int row = top; row <= bottom; row++)
        {
            bool fits = false;
            foreach (int[] span in floor.walkableRows[row])
                fits |= left - WallSideClearance >= span[0] && right + WallSideClearance < span[1];
            if (!fits)
                return false;
        }

        foreach (Exhibit exhibit in floor.objects)
        {
            int[] bounds = exhibit.collision;
            if (bounds != null && left < bounds[0] + bounds[2] && right >= bounds[0]
                && top < bounds[1] + bounds[3] && bottom >= bounds[1])
                return false;
        }

        return true;
    }

    private Portal NearbyPortal()
    {
        float bestDistance = PortalReach * PortalReach;
        Portal nearest = null;
        foreach (Portal portal in floor.portals)
        {
            if (string.IsNullOrEmpty(portal.targetFloor) || portal.sourceScene != roomId)
                continue;

            int[] bounds = portal.rect;
            float dx = position.x - Mathf.Clamp(position.x, bounds[0], bounds[0] + bounds[2]);
            float dy = position.y - Mathf.Clamp(position.y, bounds[1], bounds[1] + bounds[3]);
            float distance = dx * dx + dy * dy;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            nearest = portal;
        }

        return nearest;
    }

    private Exhibit NearbyExhibit()
    {
        float range = museum.player.inspectRadius * museum.player.inspectRadius;
        Exhibit nearest = null;
        foreach (Exhibit exhibit in floor.objects)
        {
            if (exhibit.interact == null || exhibit.room != roomId)
                continue;
            float distance = (Point(exhibit.interact) - position).sqrMagnitude;
            if (distance >= range)
                continue;
            range = distance;
            nearest = exhibit;
        }

        return nearest;
    }

    private void PlaceVisitor()
    {
        UpdateVisitorSprite();
        float footOffset = visitor.sprite == portraits[role]
            ? museum.player.height * 0.5f / PixelsPerUnit
            : 0.55f * StorySpriteScale;
        visitor.transform.position = new Vector3(position.x / PixelsPerUnit, -position.y / PixelsPerUnit + footOffset, 0);
        visitor.sortingOrder = Mathf.RoundToInt(position.y);

        int[] bounds = room.view;
        float aspect = Mathf.Max(0.01f, viewCamera.aspect);
        float height = Mathf.Min(420, Mathf.Min(bounds[3], bounds[2] / aspect));
        float halfHeight = height * 0.5f;
        float halfWidth = halfHeight * aspect;
        float x = Mathf.Clamp(position.x, bounds[0] + halfWidth, bounds[0] + bounds[2] - halfWidth);
        float y = Mathf.Clamp(position.y - 18, bounds[1] + halfHeight, bounds[1] + bounds[3] - halfHeight);
        viewCamera.orthographicSize = halfHeight / PixelsPerUnit;
        viewCamera.transform.position = new Vector3(x / PixelsPerUnit, -y / PixelsPerUnit, -10);
        MuseumStory.PlayerWorld = visitor.transform.position + new Vector3(0, 0.3f, 0);
    }

    private void OnGUI()
    {
        MuseumValves.PopupOpen = inspected != null;
        if (room == null)
            return;

        if (label == null || promptLabel == null || inspectionTitle == null)
        {
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 16);
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15 };
            promptLabel = new GUIStyle(label)
            {
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(), normal = { textColor = new Color32(255, 245, 220, 255) }
            };
            promptKey = new GUIStyle(promptLabel)
            {
                alignment = TextAnchor.MiddleCenter, wordWrap = false,
                normal = { textColor = new Color32(35, 23, 16, 255) }
            };
        }

        if (inspectionTitle == null)
        {
            inspectionBody = new GUIStyle(label)
            {
                padding = new RectOffset(), normal = { textColor = new Color32(255, 245, 229, 255) }
            };
            inspectionTitle = new GUIStyle(inspectionBody) { fontStyle = FontStyle.Bold };
            inspectionDetail = new GUIStyle(inspectionBody)
            {
                normal = { textColor = new Color32(231, 186, 99, 255) }
            };
        }

        if (MuseumStory.Busy) return;   // 시작 이야기 중에는 상단 정보·이름표·안내 문구를 전부 숨긴다
        if (!hudArmed) { hudArmed = true; hudUntil = Time.time + 8f; }   // 이야기가 끝나면 안내 칸이 잠깐 떴다가 사라진다

        if (ChapterPanelOpen)
        {
            DrawChapter();
            return;
        }

        Rect hud = DrawTopHud();
        if (guideObjectives && inspected == null) DrawChapterObjective();

        DrawNameTags();
        if (inspected == null)
        {
            MuseumStory.DrawObjective(hud);
            if (!MuseumPuzzles.AnyOpen)
            {
                if (guideObjectives && !MuseumChat.Typing) DrawObjectiveRoute(hud);
                DrawInteractionPrompt(hud);
            }
            return;
        }

        DrawInspection(hud.yMax);
    }

    private GUIStyle hudLabel;
    private float hudUntil; private bool hudArmed;

    /// <summary>왼쪽 위 안내(위치·조작 키). 디자인은 그대로 두고 글자만 화면 크기에 맞춰 키운다. 조사 팝업이 떠 있으면 그리지 않는다(자리만 계산).</summary>
    private Rect DrawTopHud()
    {
        float sc = Mathf.Clamp(Screen.height / 720f, 1f, 1.5f);
        if (hudLabel == null) hudLabel = new GUIStyle(label) { fontStyle = FontStyle.Bold };
        hudLabel.fontSize = Mathf.RoundToInt(16 * sc);
        float width = Mathf.Min(520 * sc, Screen.width - 24);
        float hudHeight = (Screen.width < 385 ? 92 : 74) * sc;
        Rect hud = new Rect(12, 12, width, hudHeight);
        MuseumStory.HudBottomPx = hud.yMax;
        if (inspected != null || Time.time >= hudUntil) return hud;
        Color prevCol = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01((hudUntil - Time.time) / 0.6f));
        GUI.Box(hud, GUIContent.none); GUI.Box(hud, GUIContent.none); GUI.Box(hud, GUIContent.none);   // 배경이 비치지 않게 겹쳐 진하게
        string place = (!string.IsNullOrEmpty(room.label) && floor.label.Contains(room.label)) ? floor.label : $"{floor.label}  /  {room.label}";
        string line1 = $"{place}  ·  {Roles.Name(role)}";
        string line2 = "WASD 이동   E 사용   H 도움말";
        Rect area = new Rect(20, 16, width - 16, hudHeight - 8);
        hudLabel.normal.textColor = new Color32(0, 0, 0, 230);   // 그림자
        GUILayout.BeginArea(new Rect(area.x + 1.5f, area.y + 1.5f, area.width, area.height));
        GUILayout.Label(line1, hudLabel); GUILayout.Label(line2, hudLabel);
        GUILayout.EndArea();
        hudLabel.normal.textColor = Color.white;
        GUILayout.BeginArea(area);
        GUILayout.Label(line1, hudLabel); GUILayout.Label(line2, hudLabel);
        GUILayout.EndArea();
        GUI.color = prevCol;
        return hud;
    }

    private void DrawInspection(float hudBottom)
    {
        float scale = Mathf.Clamp(Screen.height / 720f, 0.8f, 1.4f);
        float padding = 24 * scale;
        float areaLeft = 0, areaWidth = Screen.width;
        float available = Screen.height - hudBottom - padding * 2;
        float width = Mathf.Min(730 * scale, Screen.width - padding * 2);
        float height = Mathf.Min(410 * scale, available);
        if (height <= 0 || width <= 0)
            return;

        inspectionTitle.fontSize = Mathf.RoundToInt(30 * scale);
        inspectionBody.fontSize = Mathf.RoundToInt(24 * scale);
        inspectionDetail.fontSize = Mathf.RoundToInt(21 * scale);
        button.fontSize = Mathf.RoundToInt(22 * scale);
        var panel = new Rect(areaLeft + (areaWidth - width) / 2, hudBottom + (available - height) / 2 + padding, width, height);
        Color previous = GUI.color;
        GUI.color = new Color32(31, 24, 20, 250);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color32(204, 159, 79, 255);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3 * scale), Texture2D.whiteTexture);
        GUI.color = previous;

        GUILayout.BeginArea(new Rect(panel.x + padding, panel.y + padding, width - padding * 2, height - padding * 2));
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(inspected.name, inspectionTitle);
        if (!string.IsNullOrWhiteSpace(inspected.role))
            GUILayout.Label(inspected.role, inspectionDetail);

        if (!string.IsNullOrWhiteSpace(inspected.description))
        {
            GUILayout.Space(12 * scale);
            string action = System.Text.RegularExpressions.Regex.Replace(inspected.description, @"^P\d{2}\s+\[[^\]]+\]\s*", "");
            GUILayout.Label(action, inspectionBody);
        }

        foreach (Puzzle puzzle in museum.puzzles)
        {
            if (inspected.puzzles == null || !inspected.puzzles.Contains(puzzle.id) || string.IsNullOrWhiteSpace(puzzle.clueTitle))
                continue;
            GUILayout.Space(14 * scale);
            GUILayout.Label("함께 확인할 단서", inspectionDetail);
            GUILayout.Label(puzzle.clueTitle, inspectionBody);
        }

        GUILayout.EndScrollView();
        if (GUILayout.Button("닫기  ·  Esc", button, GUILayout.Height(40 * scale)))
            CloseInspection();
        GUILayout.EndArea();
    }

    private Portal FindObjectivePortal()
    {
        ChapterTask chapter = CurrentChapter;
        if (chapter == null)
            return null;

        string targetFloor = chapter.Floor;
        string destination = chapter.Room;
        if (curFloorId == targetFloor && roomId == destination)
            return null;

        var visited = new HashSet<(string floor, string room)> { (curFloorId, roomId) };
        var pending = new Queue<(string floor, string room, Portal first)>();
        pending.Enqueue((curFloorId, roomId, null));
        while (pending.Count > 0)
        {
            var node = pending.Dequeue();
            foreach (Portal portal in museum.floors[node.floor].portals)
            {
                if (portal.sourceScene != node.room || string.IsNullOrEmpty(portal.targetFloor)
                    || ChapterDoorLocked(portal) || !visited.Add((portal.targetFloor, portal.targetScene)))
                    continue;

                Portal first = node.first ?? portal;
                if (portal.targetFloor == targetFloor && portal.targetScene == destination)
                    return first;
                pending.Enqueue((portal.targetFloor, portal.targetScene, first));
            }
        }

        return null;
    }

    private void DrawObjectiveRoute(Rect hud)
    {
        int step = chapterIndex;
        if (routedStep != step)
        {
            routedStep = step;
            objectivePortal = FindObjectivePortal();
        }

        Exhibit chapterExhibit = CurrentChapterExhibit();
        Vector2 target;
        if (chapterExhibit != null && chapterExhibit.interact != null)
        {
            target = Point(chapterExhibit.interact);
            if (Vector2.Distance(position, target) <= museum.player.inspectRadius)
                return;
        }
        else
        {
            if (objectivePortal == null || NearbyPortal() == objectivePortal)
                return;
            int[] bounds = objectivePortal.rect;
            target = new Vector2(bounds[0] + bounds[2] * .5f, bounds[1] + bounds[3] * .5f);
        }

        Vector3 world = new Vector3(target.x / PixelsPerUnit, -target.y / PixelsPerUnit);
        Vector3 point = viewCamera.WorldToScreenPoint(world);
        float scale = Mathf.Clamp(Screen.height / 720f, .8f, 1.35f);
        float width = Mathf.Min(210 * scale, Screen.width - 24);
        float height = 34 * scale;
        float x = Mathf.Clamp(point.x - width / 2, 12, Screen.width - width - 12);
        float y = Mathf.Clamp(Screen.height - point.y - height - 10, 12, Screen.height - height - 12);
        if (new Rect(x, y, width, height).Overlaps(hud))
            y = Mathf.Min(hud.yMax + 8, Screen.height - height - 12);

        string directionLabel = point.y > Screen.height ? "↑" : point.y < 0 ? "↓"
            : point.x < 0 ? "←" : point.x > Screen.width ? "→" : "◆";
        string labelText = chapterExhibit != null ? "다음 단서" : objectivePortal.name.Contains("계단") ? "다음 계단" : "다음 출입구";
        var marker = new Rect(x, y, width, height);
        Color previous = GUI.color;
        GUI.color = new Color32(43, 31, 22, 250);
        GUI.DrawTexture(marker, Texture2D.whiteTexture);
        GUI.color = previous;
        promptLabel.fontSize = Mathf.RoundToInt(19 * scale);
        GUI.Label(new Rect(x + 10, y, width - 20, height), directionLabel + "  " + labelText, promptLabel);
    }

    private void DrawInteractionPrompt(Rect hud)
    {
        Portal portal = NearbyPortal();
        string stationPrompt = null;
        int valve = !guideObjectives && portal == null && MuseumPuzzles.NearStation(position, museum.player.inspectRadius, out stationPrompt) ? 0 : -1;
        Exhibit exhibit = portal == null ? NearbyChapterExhibit() : null;
        if (exhibit == null && portal == null && valve < 0) exhibit = NearbyExhibit();
        if (portal == null && exhibit == null && valve < 0)
            return;

        string action;
        if (exhibit != null && exhibit == CurrentChapterExhibit())
            action = CurrentChapter.Title;
        else if (valve >= 0)
            action = stationPrompt;
        else if (portal == null)
            action = exhibit.name + " 조사";
        else if (portal.name == "복도로 돌아가는 문")
            action = "복도로 이동";
        else
            action = (portal.name.EndsWith(" 문") ? portal.name.Substring(0, portal.name.Length - 2) : portal.name) + " 이동";
        if (portal != null && ChapterDoorLocked(portal)) action = guideObjectives ? "잠김 · 목표부터 완료" : action + " (잠김)";

        float scale = Mathf.Clamp(Screen.height / 720f, 0.8f, 1.35f);
        promptLabel.fontSize = Mathf.RoundToInt(23 * scale);
        promptKey.fontSize = promptLabel.fontSize;
        float margin = 12 * scale;
        float keySize = 32 * scale;
        var content = new GUIContent(action);
        float chrome = keySize + margin * 3;
        float width = Mathf.Min(Screen.width - margin * 2, promptLabel.CalcSize(content).x + chrome);
        float textWidth = Mathf.Max(1, width - chrome);
        float height = Mathf.Max(keySize, promptLabel.CalcHeight(content, textWidth)) + margin;

        Bounds bounds = visitor.bounds;
        Vector3 head = viewCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
        Vector3 feet = viewCamera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        float x = Mathf.Clamp(head.x - width / 2, margin, Screen.width - margin - width);
        float y = Screen.height - head.y - margin - height;
        if (y < margin || new Rect(x, y, width, height).Overlaps(hud))
            y = Screen.height - feet.y + margin;
        y = Mathf.Clamp(y, margin, Screen.height - margin - height);

        var panel = new Rect(x, y, width, height);
        var key = new Rect(x + margin, y + (height - keySize) / 2, keySize, keySize);
        Color previous = GUI.color;
        bool hover = panel.Contains(Event.current.mousePosition) && !MuseumChat.CoversGuiRect(panel);
        if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            promptClicked = true;   // E 를 누른 것과 같다
            Event.current.Use();
        }
        GUI.color = hover ? new Color32(62, 45, 34, 250) : new Color32(32, 23, 19, 248);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color32(231, 186, 99, 255);
        GUI.DrawTexture(key, Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(key, "E", promptKey);
        GUI.Label(new Rect(key.xMax + margin, y, textWidth, height), content, promptLabel);
    }

    private sealed class Layout
    {
        public Dictionary<string, Floor> floors;
        public Player player;
        public Puzzle[] puzzles;
    }

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
    private sealed class Exhibit
    {
        public string id, name, role, description, room;
        public int[] interact, collision, puzzles;
    }

    private sealed class Portal
    {
        public string name, sourceScene, targetScene, targetFloor;
        public int[] rect, arrival;
    }

    private sealed class Player
    {
        public int height, inspectRadius;
        public int[] footprint;
        public Character[] characters;
    }

    private sealed class Character { public string name; }
    private sealed class Puzzle { public int id; public string code, title, arch, clueTitle; }
}

