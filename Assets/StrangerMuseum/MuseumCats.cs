using Paradox.Client;
using Paradox.Core;
using UnityEngine;

/// <summary>
/// 이스터 에그 · 단풍이 숨바꼭질.
/// 단풍이는 우리 학교 고양이다. 견학 버스에 몰래 타 박물관까지 따라와 숨어 버렸다(오프닝에서 꼬리만 보인다).
///  · 한 번에 한 곳에만 있다. 가까이 가면 "야옹!" 하고 다음 숨을 곳으로 달아난다(어느 층 쪽인지 채팅에 발자국 힌트).
///  · 찾을 때마다 친구 나라 말로 "고양이"를 배운다(pusa · meo · ikati) — 같은 고양이도 부르는 말은 다르다.
///  · 다섯 번 다 찾으면 단풍이가 끝까지 내 곁을 따라다니고, 'magkakasama(모두 함께)'를 배우며, 엔딩·랭킹에 기록된다.
/// 숨는 자리는 게임 순서대로(처음 자리는 시작한 복도) — 잠긴 곳에 숨지 않는다. 길을 막거나 퍼즐에 영향을 주지 않는다.
/// 그림: Resources/Danpung(실제 단풍이 사진을 보고 털색을 맞춘 도트 · 앉기 1장 + 걷기 2장).
/// </summary>
public sealed class MuseumCats : MonoBehaviour
{
    // 숨는 차례마다 후보 자리(판마다 하나를 고른다 · 공용 시드라 온라인 친구들도 같은 자리) — 층, 지도 px(발 위치), 장소 이름, 방
    // 차례는 게임 순서대로: 시작 복도 → 지하 통로(밸브) → 지하 방들(정전 중) → 1층 전시실(불 켜진 뒤) → 2층. 잠긴 곳에 숨지 않는다.
    private static readonly (string floor, Vector2 at, string place, string room)[][] Candidates =
    {
        new[] { ("1F", new Vector2(779, 1285), "1층 중앙 복도", "hall"), ("1F", new Vector2(1120, 1300), "1층 중앙 복도", "hall") },
        new[] { ("B1", new Vector2(1627, 1187), "지하 관리 통로", "maintenance"), ("B1", new Vector2(851, 867), "지하 관리 통로", "maintenance"), ("B1", new Vector2(1300, 880), "지하 관리 통로", "maintenance") },
        new[] { ("B1", new Vector2(1883, 1419), "지하 구조 장비실", "rescue"), ("B1", new Vector2(715, 835), "지하 방재실", "safety") },
        new[] { ("1F", new Vector2(1313, 1391), "1층 도시 전시실", "city"), ("1F", new Vector2(587, 1061), "1층 생활사 전시실", "life"), ("1F", new Vector2(475, 1399), "1층 공예 전시실", "craft"), ("1F", new Vector2(1450, 1100), "1층 연표 전시실", "time") },
        new[] { ("2F", new Vector2(1150, 474), "2층 반원 홀", "atrium"), ("2F", new Vector2(281, 616), "2층 자료 열람실", "reading"), ("2F", new Vector2(1476, 804), "2층 기획 전시실", "special") },
    };
    private static readonly (string floor, Vector2 at, string place, string room)[] Spots = new (string, Vector2, string, string)[5];

    /// <summary>이번 판에 고른 k 번째 자리(시험용).</summary>
    public static (string floor, Vector2 at, string room) SpotAt(int k) => (Spots[k].floor, Spots[k].at, Spots[k].room);

    // 차례마다 숨을 수 있는 방(게임 순서대로 · 그때 열려 있는 곳만). 판마다 시드로 방 하나 → 그 방 바닥의 아무 곳.
    private static readonly (string floor, string room, string place)[][] Rooms =
    {
        new[] { ("1F", "hall", "1층") },
        new[] { ("B1", "maintenance", "지하") },
        new[] { ("B1", "rescue", "지하"), ("B1", "safety", "지하"), ("B1", "power", "지하") },
        new[] { ("1F", "life", "1층"), ("1F", "time", "1층"), ("1F", "craft", "1층"), ("1F", "city", "1층") },
        new[] { ("2F", "atrium", "2층"), ("2F", "reading", "2층"), ("2F", "special", "2층"), ("2F", "archive", "2층"), ("2F", "preserve", "2층") },
    };

    /// <summary>
    /// 완전 랜덤(마크 시드처럼 판마다 다름) + 규칙(반랜덤): 잠긴 곳 · 벽 속 · 문 앞 · 퍼즐 장치 앞 · 전시물 앞에는 숨지 않는다.
    /// 시드는 공용(온라인은 방장이 정한 판 시드)이라 같은 판 친구들은 같은 자리를 본다. 바닥을 못 찾으면 미리 확인한 후보 자리로.
    /// </summary>
    private static void PickSpots()
    {
        var rng = new System.Random(Story1Net.Seed(77));
        var m = StrangerMuseum.Instance;
        for (int k = 0; k < Spots.Length; k++)
        {
            var pool = Rooms[k];
            var pick = pool[rng.Next(pool.Length)];
            if (m != null && m.RandomHideSpot(pick.floor, pick.room, rng, out Vector2 at))
                Spots[k] = (pick.floor, at, pick.place, pick.room);
            else Spots[k] = Candidates[k][rng.Next(Candidates[k].Length)];
        }
    }
    private const float Size = 0.38f;        // 32px 그림(1유닛) → 지도 약 38px
    private const float FindRadius = 52f;

    /// <summary>한 번이라도 다 찾았는가(타이틀 화면 보상). Paradox.Client.IntroSequence 와 같은 키.</summary>
    public const string FriendKey = "stranger.danpung.friend";
    public static bool AllFound => found >= Spots.Length;

    private static int found;
    public static int FoundCount => found;
    public static int Total => Spots.Length;
    public static float AllFoundAt { get; private set; } = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { found = 0; AllFoundAt = -1f; }

    private SpriteRenderer cat;
    private Sprite sit, walk, walk2;
    private float runUntil = -1f, runTime = 1f; private Vector2 runFrom, runTo;
    private float petStuck;
    private Vector2 petPos; private string petFloor; private float petWalk;

    private static readonly int[] Langs = { 1, 2, 3 };
    private static readonly string[] CatWord = { "고양이", "pusa", "meo", "ikati" };

    private void Start()
    {
        found = 0; AllFoundAt = -1f;
        PickSpots();
        sit = Resources.Load<Sprite>("Danpung/danpung_sit");
        walk = Resources.Load<Sprite>("Danpung/danpung_walk");
        walk2 = Resources.Load<Sprite>("Danpung/danpung_walk2");
        if (sit == null) { enabled = false; return; }
        if (walk == null) walk = sit;
        if (walk2 == null) walk2 = walk;
        var go = new GameObject("Danpung");
        cat = go.AddComponent<SpriteRenderer>();
        cat.sprite = sit;
        var like = StrangerMuseum.Instance != null ? StrangerMuseum.Instance.Visitor : null;
        if (like != null) { cat.sortingLayerID = like.sortingLayerID; cat.sharedMaterial = like.sharedMaterial; }
        go.transform.localScale = new Vector3(Size, Size, 1f);
        go.SetActive(false);
    }

    private void OnDestroy() { found = 0; AllFoundAt = -1f; }   // 다음 판은 처음부터

    private void Place(Vector2 map, bool faceLeft, Sprite s, float hop = 0f)
    {
        cat.sprite = s;
        cat.transform.position = new Vector3(map.x / 100f, -map.y / 100f + hop, 0f);
        cat.transform.localScale = new Vector3((faceLeft ? -1f : 1f) * Size, Size, 1f);
        cat.sortingOrder = Mathf.RoundToInt(map.y);
    }

    private void Update()
    {
        var museum = StrangerMuseum.Instance;
        if (museum == null || cat == null) return;
        if (found >= Spots.Length) { Follow(museum); return; }

        // 달아나는 중: 걷는 그림으로 옆으로 뛰어가다 사라진다
        // 달아나는 중: 문까지 걸어서(뛰어서) 가고, 문에 닿을 즈음 사라진다
        if (runUntil > 0f)
        {
            float k = 1f - (runUntil - Time.time) / runTime;
            if (k >= 1f) { runUntil = -1f; cat.gameObject.SetActive(false); }
            else
            {
                cat.color = new Color(1f, 1f, 1f, k < 0.75f ? 1f : Mathf.Clamp01((1f - k) * 4f));
                Place(Vector2.Lerp(runFrom, runTo, k), runTo.x < runFrom.x, ((int)(Time.time * 10f) % 2 == 0) ? walk : walk2);
            }
            return;
        }

        var s = Spots[found];
        bool here = s.floor == museum.CurFloor && s.room == museum.CurRoom;   // 같은 방일 때만(옆방 벽 너머로 보이지 않게)
        if (cat.gameObject.activeSelf != here) cat.gameObject.SetActive(here);
        if (!here) return;
        cat.color = Color.white;
        // 앉아서 숨쉬기 + 가끔 몸을 돌린다
        bool left = (int)(Time.time / 3.7f) % 3 == 0;
        Place(s.at, left, sit);
        cat.transform.localScale = new Vector3(cat.transform.localScale.x, Size * (1f + Mathf.Sin(Time.time * 2.2f) * 0.03f), 1f);

        if (MuseumStory.Busy || (museum.PlayerMap - s.at).sqrMagnitude > FindRadius * FindRadius) return;
        Found(museum, s.at);
    }

    private void Found(StrangerMuseum museum, Vector2 at)
    {
        found++;
        Sfx.Play("ui_select", 0.6f);
        int lang = Langs[(found - 1) % Langs.Length];
        if (lang != Story1Role.Current) MuseumWords.Learn(CatWord[lang], lang, "고양이");
        if (found >= Spots.Length)
        {
            AllFoundAt = Time.time;
            MuseumWords.Learn("magkakasama", 1, "모두 함께");
            petPos = at; petFloor = museum.CurFloor;
            try { PlayerPrefs.SetInt(FriendKey, 1); PlayerPrefs.Save(); } catch { }   // 영구 보상: 타이틀 화면에 단풍이가 앉아 있다
            MuseumHud.ShowInspect("단풍이를 모두 찾았다!",
                "pusa, meo, ikati, 고양이. 부르는 말은 다 달라도 단풍이는 누가 불러도 꼬리를 세우고 다가왔다.\n" +
                "말이 달라도, 같은 것을 아끼는 마음은 통한다.\n\n" +
                "보상: 단풍이 동행 · 엔딩 장면 · 랭킹 ★ · 타이틀",
                "magkakasama", 1, "모두 함께", sit);
            MuseumChat.Say(-1, "단풍이 " + Spots.Length + "/" + Spots.Length + " · 보상: 단풍이 동행 · 엔딩 장면 · 랭킹 ★ · 타이틀 화면");
            return;
        }
        // 달아난다: 벽을 지나지 않고 곧장 갈 수 있는 문 중 나에게서 먼 문으로 · 없으면 바닥이 이어진 쪽으로 조금
        runFrom = at;
        PickRun(museum, at);
        runTime = Mathf.Clamp((runTo - runFrom).magnitude / 260f, 0.5f, 2.2f);
        runUntil = Time.time + runTime;
        var next = Spots[found];
        string word = lang != Story1Role.Current ? "  ·  " + CatWord[lang] + " = 고양이" : "";
        MuseumHud.Toast("야옹! 단풍이다! " + found + "/" + Spots.Length + "  ·  또 달아났다" + word);
        MuseumChat.Say(-1, "단풍이 발자국이 " + Hint(next.place) + " 쪽으로 이어져 있다");
    }

    private void PickRun(StrangerMuseum museum, Vector2 at)
    {
        float bestDoor = -1f; runTo = at;
        foreach (var door in museum.DoorPoints())
        {
            if (!museum.WalkableLine(at, door)) continue;
            float score = (door - museum.PlayerMap).magnitude;   // 나에게서 먼 문
            if (score > bestDoor) { bestDoor = score; runTo = door; }
        }
        if (bestDoor > 0f) return;
        Vector2 away = at - museum.PlayerMap; if (away.sqrMagnitude < 1f) away = Vector2.right;
        Vector2[] dirs = { Vector2.right, Vector2.left, Vector2.up, Vector2.down, new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized, new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized };
        float best = -1f; Vector2 runDir = Vector2.zero; float runLen = 0f;
        foreach (var d in dirs)
        {
            float len = 0f;
            for (float t = 8f; t <= 150f; t += 8f) { if (!museum.Walkable(at + d * t)) break; len = t; }
            if (len < 24f) continue;
            float score = len + Vector2.Dot(d, away.normalized) * 80f;   // 멀리 갈 수 있고 · 나와 반대쪽일수록
            if (score > best) { best = score; runDir = d; runLen = len; }
        }
        runTo = at + runDir * runLen;   // 갈 곳이 없으면 제자리에서 사라진다
    }

    /// <summary>다음 자리 힌트: 층만 알려 준다(방은 직접 찾아보게).</summary>
    private static string Hint(string place)
    {
        if (place.StartsWith("지하")) return "지하";
        if (place.StartsWith("1층")) return "1층";
        if (place.StartsWith("2층")) return "2층";
        return place;
    }

    // ---------------------------------------------------------------- 보상: 끝까지 따라오는 단풍이
    private void Follow(StrangerMuseum museum)
    {
        if (!cat.gameObject.activeSelf) cat.gameObject.SetActive(true);
        cat.color = Color.white;
        Vector2 me = museum.PlayerMap;
        Vector2 want = me + new Vector2(petPos.x <= me.x ? -26f : 26f, 8f);
        if (petFloor != museum.CurFloor || (want - petPos).sqrMagnitude > 180f * 180f) { petPos = want; petFloor = museum.CurFloor; }
        // 곁자리가 벽 너머면 내 바로 뒤로
        if (!museum.Walkable(want) || !museum.WalkableLine(me, want)) want = me + new Vector2(0f, 10f);
        if (!museum.Walkable(want)) want = me;
        float d = (want - petPos).magnitude;
        bool moving = d > 10f;
        if (moving)
        {
            // 벽을 뚫지 않게: 한 걸음씩 가로·세로를 따로 확인하며 걷는다
            Vector2 step = Vector2.MoveTowards(petPos, want, Mathf.Lerp(120f, 230f, Mathf.InverseLerp(10f, 90f, d)) * Time.deltaTime) - petPos;
            Vector2 before = petPos;
            if (museum.Walkable(petPos + new Vector2(step.x, 0f))) petPos.x += step.x;
            if (museum.Walkable(petPos + new Vector2(0f, step.y))) petPos.y += step.y;
            petStuck = (petPos - before).sqrMagnitude < 0.01f ? petStuck + Time.deltaTime : 0f;
            // 모서리에 걸려 못 오면(0.8초) 내 곁으로 살짝 다시 나타난다
            if (petStuck > 0.8f) { petPos = want; petStuck = 0f; }
            petWalk += Time.deltaTime;
        }
        else { petWalk = 0f; petStuck = 0f; }
        bool faceLeft = moving ? want.x < petPos.x : cat.transform.localScale.x < 0f;
        Place(petPos, faceLeft, moving ? (((int)(petWalk * 8f) % 2 == 0) ? walk : walk2) : sit);
    }
}
