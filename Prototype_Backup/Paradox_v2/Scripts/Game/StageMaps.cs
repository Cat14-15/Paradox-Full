using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>맵 위의 사물(표지판·장치·구역 등). kind로 종류를 구분한다.</summary>
    public class MapProp
    {
        public string kind = "";   // sign / lever / rubble / console / wheel / extinguisher / bell / shutter / keypad / speaker / doorpanel / confirm / door
        public string id = "";
        public Vector2 pos;
        public string text = "";
        public int slot = -1;      // 이 사물을 다루는 역할(-1 = 누구나/해당 없음)
        public int a;              // 부가 번호(레버 좌우, 구역 번호 등)
        public float size = 1f;
    }

    /// <summary>
    /// 한 스테이지의 지형 데이터. 통행 가능 영역(open)의 합집합에서 장애물(solid)을 뺀 곳만 걸을 수 있다.
    /// 서버(도착 판정)와 클라이언트(렌더링·이동) 모두 같은 시드로 같은 맵을 만든다.
    /// </summary>
    public class MapData
    {
        public Rect bounds;
        public readonly List<Rect> open = new List<Rect>();
        public readonly List<Rect> solids = new List<Rect>();
        public readonly List<MapProp> props = new List<MapProp>();
        public readonly List<Vector2> railNodes = new List<Vector2>();
        public readonly List<Rect> hazards = new List<Rect>();
        public Rect[] sectors = new Rect[0];
        public Vector2[] spawns = new Vector2[4];
        public Vector2[] zones = new Vector2[4];
        public float zoneRadius = 1.0f;
        public Rect flooded;      // Stage 2: 침수되는 갈래(미니맵 표시용)
        public Rect exitRoom;

        public MapProp Prop(string id)
        {
            foreach (var p in props) if (p.id == id) return p;
            return null;
        }

        public Vector2 Point(string id)
        {
            var p = Prop(id);
            return p != null ? p.pos : Vector2.zero;
        }

        public bool Walkable(Vector2 p, float r)
        {
            bool inside = false;
            foreach (var o in open)
            {
                if (p.x >= o.xMin + r && p.x <= o.xMax - r && p.y >= o.yMin + r && p.y <= o.yMax - r) { inside = true; break; }
            }
            if (!inside) return false;
            foreach (var s in solids)
            {
                if (p.x > s.xMin - r && p.x < s.xMax + r && p.y > s.yMin - r && p.y < s.yMax + r) return false;
            }
            return true;
        }

        /// <summary>벽에 막히면 축별로 미끄러지며 이동한다.</summary>
        public Vector2 Move(Vector2 from, Vector2 delta, float r)
        {
            Vector2 full = from + delta;
            if (Walkable(full, r)) return full;
            Vector2 xOnly = new Vector2(from.x + delta.x, from.y);
            if (Mathf.Abs(delta.x) > 1e-5f && Walkable(xOnly, r)) return xOnly;
            Vector2 yOnly = new Vector2(from.x, from.y + delta.y);
            if (Mathf.Abs(delta.y) > 1e-5f && Walkable(yOnly, r)) return yOnly;
            return from;
        }

        public int SectorAt(Vector2 p)
        {
            for (int i = 0; i < sectors.Length; i++)
                if (sectors[i].Contains(p)) return i;
            return -1;
        }

        public bool InZone(int slot, Vector2 p) => Vector2.Distance(zones[slot], p) <= zoneRadius;

        private const float Cell = 0.5f;

        /// <summary>
        /// 0.5 유닛 격자 위에서 너비 우선 탐색으로 길을 찾는다(봇 이동용). 목표 칸이 못 걷는 곳이면 근처의 걷는 칸을 목표로 삼는다.
        /// 경로가 없으면 null.
        /// </summary>
        public List<Vector2> FindPath(Vector2 from, Vector2 to, float r, int maxExpand = 30000)
        {
            int w = Mathf.Max(1, Mathf.CeilToInt(bounds.width / Cell));
            int h = Mathf.Max(1, Mathf.CeilToInt(bounds.height / Cell));
            Vector2 origin = new Vector2(bounds.xMin, bounds.yMin);

            Vector2Int Idx(Vector2 p) => new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / Cell), 0, w - 1),
                Mathf.Clamp(Mathf.FloorToInt((p.y - origin.y) / Cell), 0, h - 1));
            Vector2 Center(Vector2Int c) => origin + new Vector2((c.x + 0.5f) * Cell, (c.y + 0.5f) * Cell);
            int Key(Vector2Int c) => c.y * w + c.x;

            var start = Idx(from);
            var goal = Idx(to);
            if (!Walkable(Center(goal), r))
            {
                bool found = false;
                for (int rad = 1; rad <= 8 && !found; rad++)
                    for (int dx = -rad; dx <= rad && !found; dx++)
                        for (int dy = -rad; dy <= rad && !found; dy++)
                        {
                            var c = new Vector2Int(goal.x + dx, goal.y + dy);
                            if (c.x < 0 || c.y < 0 || c.x >= w || c.y >= h) continue;
                            if (Walkable(Center(c), r)) { goal = c; found = true; }
                        }
                if (!found) return null;
            }

            var prev = new Dictionary<int, int>();
            var visited = new HashSet<int> { Key(start) };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            int[] dxs = { 1, -1, 0, 0 }, dys = { 0, 0, 1, -1 };
            bool ok = start == goal;
            int expanded = 0;
            while (queue.Count > 0 && !ok)
            {
                var c = queue.Dequeue();
                if (++expanded > maxExpand) break;
                for (int k = 0; k < 4; k++)
                {
                    var n = new Vector2Int(c.x + dxs[k], c.y + dys[k]);
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;
                    int nk = Key(n);
                    if (visited.Contains(nk)) continue;
                    if (!Walkable(Center(n), r)) continue;
                    visited.Add(nk);
                    prev[nk] = Key(c);
                    if (n == goal) { ok = true; break; }
                    queue.Enqueue(n);
                }
            }
            if (!ok) return null;

            var path = new List<Vector2>();
            var cur = goal;
            while (cur != start)
            {
                path.Add(Center(cur));
                int pk = prev[Key(cur)];
                cur = new Vector2Int(pk % w, pk / w);
            }
            path.Reverse();
            path.Add(to);
            return path;
        }
    }

    /// <summary>스테이지별 맵 생성기(결정적).</summary>
    public static class StageMaps
    {
        /// <summary>네 명이 한 지점에 모일 때 슬롯별 자리 오프셋.</summary>
        public static readonly Vector2[] SlotOffsets =
        {
            new Vector2(-1.15f, 0.55f), new Vector2(1.15f, 0.55f), new Vector2(-1.15f, -0.7f), new Vector2(1.15f, -0.7f)
        };

        public const float AvatarRadius = 0.32f;

        public static MapData Build(StageState st)
        {
            switch (st.stage)
            {
                case 1: return Stage1(st);
                case 2: return Stage2(st);
                case 3: return Stage3(st);
                case 4: return Stage4(st);
                default: return Stage1(st);
            }
        }

        private static Rect MM(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(x0, y0, x1, y1);

        private static MapProp Add(MapData m, string kind, string id, float x, float y, string text = "", int slot = -1, int a = 0, float size = 1f)
        {
            var p = new MapProp { kind = kind, id = id, pos = new Vector2(x, y), text = text, slot = slot, a = a, size = size };
            m.props.Add(p);
            return p;
        }

        // =====================================================================
        // Stage 1 — 필리핀 · 정전된 복도 · 릴레이 체인 (레일 이동 + 로비 자유 이동)
        // =====================================================================
        public static readonly float[] Stage1NodeX = { 0f, 8f, 18f, 23f, 28f, 38f };
        public const int Stage1ObstacleNode = 3;
        public const int Stage1LobbyNode = 5;

        public static int Stage1ForkOfNode(int node)
        {
            switch (node) { case 1: return 0; case 2: return 1; case 4: return 2; default: return -1; }
        }

        private static MapData Stage1(StageState st)
        {
            var m = new MapData { bounds = MM(-9f, -7f, 46f, 7f) };
            m.open.Add(MM(-5f, -3.2f, 35f, 3.2f));   // 복도
            m.open.Add(MM(34f, -5.5f, 45f, 5.5f));   // 로비

            foreach (var x in Stage1NodeX) m.railNodes.Add(new Vector2(x, 0f));

            for (int f = 0; f < 3; f++)
            {
                int node = f == 0 ? 1 : (f == 1 ? 2 : 4);
                float x = Stage1NodeX[node];
                string word = st.forkWord != null && f < st.forkWord.Length && !string.IsNullOrEmpty(st.forkWord[f]) ? st.forkWord[f] : StageDefs.ForkPairs[f][0];
                Add(m, "sign", "sign" + f, x, 2.7f, "태풍 대피 경로 ▶ " + word, StageDefs.LocalSlot(1), f);
                Add(m, "lever", "leverL" + f, x - 1.7f, -2.2f, "", (int)GameRole.Operator, 0);
                Add(m, "lever", "leverR" + f, x + 1.7f, -2.2f, "", (int)GameRole.Operator, 1);
                Add(m, "lamp", "lamp" + f, x, -2.9f, "", -1, f, 0.5f);
            }

            if (!st.obstacleCleared)
            {
                // 장애물은 3번 레일 지점(x=23) 바로 앞(오른쪽)을 막는다 — 네 명이 그 앞에 멈춰 연타한다
                Add(m, "rubble", "obstacle", Stage1NodeX[Stage1ObstacleNode] + 2.6f, 0f, "쓰러진 집기", -1, 0, 2.2f);
                m.solids.Add(MM(25.0f, -3.2f, 26.2f, 3.2f));
            }

            Add(m, "sign", "lobbysign", 39.5f, 4.2f, "로비 · 전원이 원 안으로", -1, 9);
            Add(m, "shutter", "shutter", 44.2f, 0f, "", -1, 0, 3f);
            m.zones[0] = new Vector2(37f, 1.9f);
            m.zones[1] = new Vector2(40.2f, 1.9f);
            m.zones[2] = new Vector2(37f, -1.9f);
            m.zones[3] = new Vector2(40.2f, -1.9f);
            m.zoneRadius = 1.05f;
            for (int i = 0; i < 4; i++) m.spawns[i] = m.railNodes[0] + SlotOffsets[i];
            return m;
        }

        // =====================================================================
        // Stage 2 — 베트남 · 침수 터널 · 3자 동시 조작 (수문 방 → 갈림길 → 상부 통로)
        // =====================================================================
        private static MapData Stage2(StageState st)
        {
            bool up = st.exitUp;
            var m = new MapData { bounds = MM(-2f, -16f, 48f, 16f) };

            // 정답 갈래가 위쪽(up)인 기준 배치. 아래쪽이 정답이면 Y를 뒤집는다.
            m.open.Add(Y(MM(0f, -5f, 16f, 5f), up));        // 수문 방
            m.open.Add(Y(MM(15f, -1.5f, 24.5f, 1.5f), up)); // 복도
            m.open.Add(Y(MM(23f, -1.5f, 26f, 10.5f), up));  // 세로 갈래(정답)
            m.open.Add(Y(MM(23f, -10.5f, 26f, 1.5f), up));  // 세로 갈래(막다른 길)
            m.open.Add(Y(MM(25f, 8f, 37f, 10.5f), up));     // 가로 통로(정답)
            m.open.Add(Y(MM(36f, 5f, 46f, 13f), up));       // 상부 통로(출구)
            m.open.Add(Y(MM(25f, -10.5f, 35f, -8f), up));   // 가로 통로(침수)
            m.open.Add(Y(MM(34f, -13f, 42f, -5f), up));     // 침수되는 막다른 방

            if (!st.gatesOpen) m.solids.Add(Y(MM(16f, -1.7f, 16.7f, 1.7f), up));

            m.hazards.Add(Y(MM(23f, -13f, 42f, -3.2f), up));
            m.flooded = Y(MM(23f, -13f, 42f, -3.2f), up);
            m.exitRoom = Y(MM(36f, 5f, 46f, 13f), up);

            string mode = StageDefs.ConsoleModes[Mathf.Clamp(st.requiredMode, 0, 2)];
            Add(m, "console", "console", 8f, Yf(-2.4f, up), "", (int)GameRole.Operator);
            Add(m, "wheel", "wheelA", 2.4f, Yf(0.6f, up), "", (int)GameRole.Observer, 0, 1.4f);
            Add(m, "wheel", "wheelB", 13.6f, Yf(0.6f, up), "", (int)GameRole.Interpreter, 1, 1.4f);
            Add(m, "sign", "sign0", 8f, Yf(3.6f, up), $"펌프 모드를 [{mode}]로 맞춘 뒤, 수문 두 개를 동시에 여시오", StageDefs.LocalSlot(2), 0);
            Add(m, "sign", "sign1", 24.5f, Yf(-0.6f, up), up ? "아래쪽 통로 침수 중 — 위쪽으로" : "위쪽 통로 침수 중 — 아래쪽으로", StageDefs.LocalSlot(2), 1);
            Add(m, "gate", "gate", 16.35f, 0f, "", -1, 0, 3.4f);
            Add(m, "sign", "exitsign", 41f, Yf(12.2f, up), "상부 통로 · 전원이 원 안으로", -1, 9);

            m.zones[0] = new Vector2(39.6f, Yf(10.2f, up));
            m.zones[1] = new Vector2(42.6f, Yf(10.2f, up));
            m.zones[2] = new Vector2(39.6f, Yf(7.6f, up));
            m.zones[3] = new Vector2(42.6f, Yf(7.6f, up));
            m.zoneRadius = 1.05f;
            for (int i = 0; i < 4; i++) m.spawns[i] = new Vector2(7f, Yf(0.4f, up)) + SlotOffsets[i];
            return m;
        }

        private static Rect Y(Rect r, bool keep) => keep ? r : MM(r.xMin, -r.yMax, r.xMax, -r.yMin);
        private static float Yf(float y, bool keep) => keep ? y : -y;

        // =====================================================================
        // Stage 3 — 국적 미정 · 유독가스 · 정보 교차검증 (4 x 3 구역)
        // =====================================================================
        public const int Stage3Cols = 4, Stage3Rows = 3;
        public const int Stage3StartSector = 4; // (0,1)

        public static Rect Stage3SectorRect(int idx)
        {
            int c = idx % Stage3Cols, r = idx / Stage3Cols;
            return MM(c * 9f, r * 8f, c * 9f + 7f, r * 8f + 6f);
        }

        private static MapData Stage3(StageState st)
        {
            var m = new MapData { bounds = MM(-6f, -2f, 36f, 24f) };
            m.sectors = new Rect[Stage3Cols * Stage3Rows];
            for (int i = 0; i < m.sectors.Length; i++)
            {
                m.sectors[i] = Stage3SectorRect(i);
                m.open.Add(m.sectors[i]);
            }
            // 문(통로): 가로 인접. 통행 영역끼리는 캐릭터 반지름의 두 배 이상(1유닛) 겹쳐야 이음새에서 막히지 않는다.
            for (int r = 0; r < Stage3Rows; r++)
                for (int c = 0; c < Stage3Cols - 1; c++)
                {
                    int a = r * Stage3Cols + c, b = a + 1;
                    var door = MM(c * 9f + 6.0f, r * 8f + 2f, c * 9f + 10.0f, r * 8f + 4f);
                    m.open.Add(door);
                    // 붕괴 구역은 가운데 줄(r==1)에는 없고, 붕괴 구역의 가로 문은 잔해로 막힌다.
                    if (IsBlocked(st, a) || IsBlocked(st, b)) m.solids.Add(MM(c * 9f + 7.2f, r * 8f + 1.8f, c * 9f + 8.8f, r * 8f + 4.2f));
                }
            // 문(통로): 세로 인접 — 가운데 줄과 연결되는 문은 항상 열려 있다(막다른 길 = 붕괴 구역).
            for (int r = 0; r < Stage3Rows - 1; r++)
                for (int c = 0; c < Stage3Cols; c++)
                {
                    var door = MM(c * 9f + 2.5f, r * 8f + 5.0f, c * 9f + 4.5f, r * 8f + 9.0f);
                    m.open.Add(door);
                }

            for (int i = 0; i < m.sectors.Length; i++)
            {
                var s = m.sectors[i];
                float x = s.xMin, y = s.yMin;
                if (IsBlocked(st, i))
                {
                    Add(m, "rubble", "rubble" + i, x + 3.5f, y + 3.4f, "", -1, i, 2.6f);
                    m.solids.Add(MM(x + 2.2f, y + 2.2f, x + 4.8f, y + 4.6f));
                }
                Add(m, "doorpanel", "door" + i, x + 3.5f, y + 5.2f, "탈출문", -1, i, 1.2f);
                Add(m, "confirm", "confObs" + i, x + 1.1f, y + 5.1f, "", (int)GameRole.Observer, i, 0.8f);
                Add(m, "confirm", "confNav" + i, x + 5.9f, y + 5.1f, "", (int)GameRole.Navigator, i, 0.8f);
                Add(m, "sectorlabel", "label" + i, x + 0.7f, y + 0.5f, SectorName(i), -1, i);
            }

            var start = m.sectors[Stage3StartSector];
            Add(m, "sign", "sign0", start.xMin + 3.5f, start.yMin + 0.9f, "유독가스 경보 · 초록 구역만 안전 — 노란 구역도 위험", StageDefs.LocalSlot(3), 0);

            m.zoneRadius = 0.85f;
            SetSectorZones(m, st.safeSector);
            for (int i = 0; i < 4; i++) m.spawns[i] = start.center + SlotOffsets[i];
            return m;
        }

        public static bool IsBlocked(StageState st, int idx) => st.blocked != null && idx >= 0 && idx < st.blocked.Length && st.blocked[idx];

        public static string SectorName(int idx) => $"{(char)('A' + idx % Stage3Cols)}{idx / Stage3Cols + 1}";

        /// <summary>구역 안의 4개 도착 원 위치(모든 구역이 같은 배치).</summary>
        public static void SectorZones(int idx, Vector2[] outZones)
        {
            var s = Stage3SectorRect(idx);
            float x = s.xMin, y = s.yMin;
            outZones[0] = new Vector2(x + 2.4f, y + 3.6f);
            outZones[1] = new Vector2(x + 4.6f, y + 3.6f);
            outZones[2] = new Vector2(x + 2.4f, y + 1.8f);
            outZones[3] = new Vector2(x + 4.6f, y + 1.8f);
        }

        private static void SetSectorZones(MapData m, int idx) => SectorZones(idx, m.zones);

        // =====================================================================
        // Stage 4 — 한국 · 지하상가 화재 · 우선순위 판단 + 멀티태스킹
        // =====================================================================
        public static readonly Vector2[] Stage4FireCandidates = BuildFireCandidates();

        private static Vector2[] BuildFireCandidates()
        {
            var list = new List<Vector2>();
            float[] vx = { 13f, 24f, 35f };
            float[] vy = { 2f, 6.5f, 11f, 15.5f, 18.5f };
            foreach (var x in vx) foreach (var y in vy) list.Add(new Vector2(x, y));
            float[] hx = { 7.5f, 18.5f, 29.5f, 40.5f };
            float[] hy = { 2f, 11f, 18.5f };
            foreach (var x in hx) foreach (var y in hy) list.Add(new Vector2(x, y));
            list.Add(new Vector2(2f, 8f)); list.Add(new Vector2(2f, 13f));
            list.Add(new Vector2(46f, 3f)); list.Add(new Vector2(46f, 15f));
            return list.ToArray();
        }

        private static MapData Stage4(StageState st)
        {
            var m = new MapData { bounds = MM(-8f, -2f, 52f, 22f) };
            m.open.Add(MM(0f, 0f, 48f, 20f));
            m.open.Add(MM(44.5f, 8.2f, 50f, 11.8f)); // 출구 포켓
            for (int bx = 0; bx < 4; bx++)
                for (int by = 0; by < 2; by++)
                {
                    var block = MM(4f + bx * 11f, 4f + by * 9f, 11f + bx * 11f, 9f + by * 9f);
                    m.solids.Add(block);
                    Add(m, "shop", "shop" + (bx + by * 4), block.center.x, block.center.y, ShopName(bx + by * 4), -1, 0, 1f);
                }
            if (!st.codeDone) m.solids.Add(MM(45f, 8f, 45.7f, 12f));
            m.exitRoom = MM(45.7f, 8.2f, 50f, 11.8f);

            Add(m, "speaker", "speaker", 2f, 3.2f, "비상 안내 방송", -1, 0, 1.1f);
            Add(m, "extinguisher", "dev1", 2f, 18.6f, "소화기", (int)GameRole.Interpreter, 1, 1.3f);
            Add(m, "bell", "dev2", 24f, 1.4f, "비상벨", (int)GameRole.Navigator, 2, 1.3f);
            Add(m, "shutterswitch", "dev3", 46.2f, 18.6f, "셔터 스위치", (int)GameRole.Observer, 3, 1.3f);
            Add(m, "keypad", "pad1", 43.6f, 11.2f, "", (int)GameRole.Interpreter, 1, 1f);
            Add(m, "keypad", "pad2", 43.6f, 10f, "", (int)GameRole.Navigator, 2, 1f);
            Add(m, "keypad", "pad3", 43.6f, 8.8f, "", (int)GameRole.Observer, 3, 1f);
            Add(m, "shutter", "shutter", 45.35f, 10f, "", -1, 0, 4f);
            Add(m, "sign", "exitsign", 47.5f, 12.4f, "비상 탈출구 · 전원이 원 안으로", (int)GameRole.Operator, 9);
            Add(m, "sign", "sign0", 6f, 1.2f, "비상구 → 동쪽 끝 (오른쪽)", (int)GameRole.Operator, 0);

            foreach (var f in st.fires) m.hazards.Add(MM(f.x - 0.9f, f.y - 0.9f, f.x + 0.9f, f.y + 0.9f));

            m.zones[0] = new Vector2(47f, 10.9f);
            m.zones[1] = new Vector2(48.9f, 10.9f);
            m.zones[2] = new Vector2(47f, 9.1f);
            m.zones[3] = new Vector2(48.9f, 9.1f);
            m.zoneRadius = 0.8f;
            for (int i = 0; i < 4; i++) m.spawns[i] = new Vector2(2.2f, 1.6f) + SlotOffsets[i] * 0.8f;
            return m;
        }

        private static string ShopName(int i)
        {
            string[] names = { "분식", "문구", "옷가게", "서점", "약국", "카페", "신발", "휴대폰" };
            return names[i % names.Length];
        }
    }
}
