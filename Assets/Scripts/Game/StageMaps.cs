using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>맵 위의 사물. kind == id(Stage 1은 오브젝트가 전부 고유하다). slot = 담당 역할(-1 = 장식/공용), room = 놓인 방(-1 = 복도·문).</summary>
    public class MapProp
    {
        public string kind = "";   // panel / poster / lamp / board / lever / mural / colorlamp / junction / fountain / door3 / sign
        public string id = "";     // panel / poster / lamp / board / lever / mural / colorlamp / junction / fountain / door3 / sign0~2
        public Vector2 pos;
        public string text = "";   // 이름 또는 표지판 문구
        public int slot = -1;      // 이 사물을 다루는 역할(-1 = 누구나/해당 없음)
        public int room = -1;      // 놓인 방 번호(발광 판정: 담당자가 이 방에 들어왔을 때)
        public int a;              // 부가 번호
        public float size = 1f;
    }

    /// <summary>
    /// 박물관 1층 지형 데이터. 통행 가능 영역(open)의 합집합에서 장애물(solid)을 뺀 곳만 걸을 수 있다.
    /// 서버(근접·방 판정)와 클라이언트(렌더링·이동)가 같은 StageState로 같은 맵을 만든다(맵 키: stage, seed, retries, gate1Done, step).
    /// </summary>
    public class MapData
    {
        public Rect bounds;
        public readonly List<Rect> open = new List<Rect>();
        public readonly List<Rect> solids = new List<Rect>();
        public readonly List<MapProp> props = new List<MapProp>();
        public Rect[] rooms = new Rect[StageMaps.RoomCount];   // 방 0~5(문·복도는 포함하지 않는다)
        public Vector2[] spawns = new Vector2[4];

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

        /// <summary>이 지점이 속한 방 번호(-1 = 복도/문).</summary>
        public int RoomAt(Vector2 p)
        {
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i].Contains(p)) return i;
            return -1;
        }

        public static string RoomName(int room) => StageMaps.RoomName(room);

        /// <summary>방 입구(핑 비콘 위치).</summary>
        public Vector2 RoomDoor(int room) => StageMaps.RoomDoor(room);

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

        private const float Cell = 0.5f;

        /// <summary>
        /// 0.5 유닛 격자 위에서 너비 우선 탐색으로 길을 찾는다(봇 이동용). 목표 칸이 못 걷는 곳이면 근처의 걷는 칸을 목표로 삼는다.
        /// 경로가 없으면 null.
        /// </summary>
        public List<Vector2> FindPath(Vector2 from, Vector2 to, float r, int maxExpand = 30000) => FindPath(from, to, r, null, maxExpand);

        /// <summary>
        /// avoid 사각형(침수 70% 이상인 방 등) 안의 칸은 지나지 않는 길찾기(v2 D7). 목표 칸이 avoid 안이면 그 칸만 허용한다.
        /// 피해 가는 길이 없으면 null — 호출자(BotBrain)가 avoid 없이 다시 찾는다.
        /// </summary>
        public List<Vector2> FindPath(Vector2 from, Vector2 to, float r, IList<Rect> avoid, int maxExpand = 30000)
        {
            bool Avoided(Vector2 p)
            {
                if (avoid == null) return false;
                for (int i = 0; i < avoid.Count; i++) if (avoid[i].Contains(p)) return true;
                return false;
            }
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
                    if (n != goal && Avoided(Center(n))) continue;
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

    /// <summary>Stage 1 — 필리핀 · 침수된 박물관 1층(탑다운, 한 층 전체가 한 화면). 월드 유닛.</summary>
    public static class StageMaps
    {
        public const int RoomCount = 6;
        public const int RoomAtrium = 0, RoomDesk = 1, RoomOffice = 2, RoomMosaic = 3, RoomAquarium = 4, RoomGarden = 5;

        public const float AvatarRadius = 0.32f;

        // ---- 메인 맵 그림(Resources/Backgrounds/museum_map.png, 1536×1024)과 월드 좌표의 대응 ----
        // 그림 왼쪽 위 픽셀 (0,0) → 월드 MapOrigin, 1유닛 = MapPxPerUnit 픽셀. 아래 방·문·장애물·오브젝트는 전부 "그림 픽셀 좌표"로 적는다.
        public const float MapPxPerUnit = 36f;
        public const float MapOriginX = -21.8f, MapOriginY = 13.0f;
        public const float MapPxWidth = 1536f, MapPxHeight = 1024f;
        public static Vector2 MapOrigin => new Vector2(MapOriginX, MapOriginY);

        /// <summary>그림 픽셀 좌표(왼쪽 위 원점, y 아래로) → 월드 좌표.</summary>
        public static Vector2 Px(float px, float py) => new Vector2(MapOriginX + px / MapPxPerUnit, MapOriginY - py / MapPxPerUnit);

        /// <summary>그림 픽셀 사각형(x0,y0 = 왼쪽 위, x1,y1 = 오른쪽 아래) → 월드 Rect.</summary>
        public static Rect PxRect(float x0, float y0, float x1, float y1)
            => Rect.MinMaxRect(MapOriginX + x0 / MapPxPerUnit, MapOriginY - y1 / MapPxPerUnit, MapOriginX + x1 / MapPxPerUnit, MapOriginY - y0 / MapPxPerUnit);

        /// <summary>그림 전체가 차지하는 월드 사각형.</summary>
        public static Rect MapImageRect => PxRect(0f, 0f, MapPxWidth, MapPxHeight);

        /// <summary>최종 집결 원들이 도는 중심 = 중앙 홀 바닥의 나침반 문양(그림 767,622).</summary>
        public static Vector2 GatherOrigin => Px(767f, 622f);

        /// <summary>네 명이 한 지점에 모일 때 슬롯별 자리 오프셋(봇 리더 추종용).</summary>
        public static readonly Vector2[] SlotOffsets =
        {
            new Vector2(-1.15f, 0.55f), new Vector2(1.15f, 0.55f), new Vector2(-1.15f, -0.7f), new Vector2(1.15f, -0.7f)
        };

        private static readonly string[] RoomNames =
        {
            "중앙 분수 아트리움", "안내데스크", "관리실·보안 데스크", "나침반 모자이크방", "수족관방", "연못정원방"
        };

        public static string RoomName(int room) => room >= 0 && room < RoomNames.Length ? RoomNames[room] : "복도";

        /// <summary>방의 통행 영역(문 제외). 그림: 0 가운데 홀, 1 왼쪽 위 전시실, 2 오른쪽 끝 STAFF 복도, 3 오른쪽 아래, 4 왼쪽 아래(고래 뼈), 5 오른쪽 위.</summary>
        public static Rect RoomRect(int room)
        {
            switch (room)
            {
                case RoomAtrium: return PxRect(522f, 250f, 1015f, 765f);
                case RoomDesk: return PxRect(78f, 215f, 468f, 428f);
                case RoomOffice: return PxRect(1422f, 206f, 1506f, 696f);
                case RoomMosaic: return PxRect(1068f, 600f, 1413f, 800f);
                case RoomAquarium: return PxRect(78f, 600f, 468f, 800f);
                case RoomGarden: return PxRect(1068f, 215f, 1405f, 428f);
                default: return new Rect();
            }
        }

        /// <summary>
        /// 방으로 들어가는 문(통행 영역, 양쪽 방과 1유닛 이상 겹친다). 아트리움은 문이 없다.
        /// 관리실(STAFF 복도)의 "안전한" 문은 모자이크방 동쪽 개구부(관문 1 전에는 LockedDoor3B 로 막힘) — 그 전에는 연못정원방 지름길로만 간다.
        /// </summary>
        public static Rect DoorRect(int room)
        {
            switch (room)
            {
                case RoomDesk: return PxRect(376f, 222f, 597f, 308f);
                case RoomOffice: return PxRect(1341f, 610f, 1494f, 696f);
                case RoomMosaic: return PxRect(938f, 560f, 1160f, 646f);
                case RoomAquarium: return PxRect(376f, 560f, 597f, 646f);
                case RoomGarden: return PxRect(938f, 222f, 1160f, 308f);
                default: return new Rect();
            }
        }

        /// <summary>
        /// 지름길 통로(v2 D7): 연못정원방(5, 위험 구역) ↔ 관리실(2) — 그림의 STAFF 복도 쪽 문. 양쪽 방과 1유닛씩 겹친다.
        /// 홀 → 연못정원방 → 관리실이 빠른 길, 홀 → 모자이크방 → 복도가 안전한 길(관문 1 뒤). 그림에 수족관방 쪽 두 번째 문이 없어 지름길은 1개다.
        /// </summary>
        public static readonly Rect[] ShortcutDoors =
        {
            PxRect(1333f, 223f, 1494f, 309f)   // 연못정원방(x ≤ 1405px) ↔ 관리실 복도(x ≥ 1422px)
        };

        /// <summary>방 입구 위치(핑 비콘·미니맵 표시). 아트리움은 정문 안쪽(시작 지점).</summary>
        public static Vector2 RoomDoor(int room)
        {
            switch (room)
            {
                case RoomDesk: return Px(497f, 265f);
                case RoomOffice: return Px(1421f, 266f);
                case RoomMosaic: return Px(1036f, 603f);
                case RoomAquarium: return Px(497f, 603f);
                case RoomGarden: return Px(1036f, 265f);
                default: return Px(767f, 700f);
            }
        }

        /// <summary>잠긴 모자이크방 문(홀 쪽, 관문 1을 통과하기 전까지 solid).</summary>
        public static readonly Rect LockedDoor3 = PxRect(1020f, 552f, 1052f, 654f);

        /// <summary>잠긴 모자이크방 동쪽 개구부(STAFF 복도 쪽) — 연못정원방 → 복도로 돌아 들어오는 길도 관문 1 전에는 막는다.</summary>
        public static readonly Rect LockedDoor3B = PxRect(1400f, 602f, 1432f, 704f);

        /// <summary>홀 가운데 동상 받침대("분수" 자리, 항상 solid).</summary>
        public static readonly Rect FountainSolid = PxRect(695f, 420f, 842f, 500f);

        /// <summary>그림 속 전시물·가구(항상 solid). 키 큰 전시물은 아래쪽 받침 부분만 막는다(탑다운 3/4 시점의 발 자리).</summary>
        public static readonly Rect[] ExhibitSolids =
        {
            // 홀: 현수막 옆 화분, 벤치, 가로등, 아래 모서리 화분
            PxRect(522f, 402f, 553f, 490f), PxRect(985f, 402f, 1015f, 490f),
            PxRect(522f, 657f, 548f, 735f), PxRect(987f, 657f, 1015f, 735f),
            PxRect(612f, 722f, 635f, 760f), PxRect(902f, 722f, 925f, 760f),
            PxRect(522f, 738f, 588f, 765f), PxRect(947f, 738f, 1015f, 765f),
            // 안내데스크(왼쪽 위): 항아리 케이스 2, 가운데 목상(로프), 벤치, 화분 2
            PxRect(160f, 300f, 197f, 335f), PxRect(232f, 310f, 322f, 382f), PxRect(358f, 300f, 397f, 335f),
            PxRect(235f, 402f, 320f, 428f), PxRect(78f, 398f, 120f, 428f), PxRect(428f, 398f, 468f, 428f),
            // 수족관방(왼쪽 아래): 고래 뼈, 화분 2
            PxRect(165f, 685f, 392f, 755f), PxRect(78f, 768f, 100f, 800f), PxRect(430f, 768f, 468f, 800f),
            // 연못정원방(오른쪽 위): 항아리 3점, 벤치, 화분 2
            PxRect(1158f, 255f, 1310f, 318f), PxRect(1195f, 405f, 1273f, 428f), PxRect(1068f, 398f, 1108f, 428f), PxRect(1355f, 398f, 1405f, 428f),
            // 모자이크방(오른쪽 아래): 벤치 2, 케이스 3, 화분 2
            PxRect(1083f, 700f, 1103f, 745f), PxRect(1114f, 700f, 1143f, 765f), PxRect(1203f, 695f, 1277f, 752f),
            PxRect(1334f, 700f, 1360f, 765f), PxRect(1377f, 700f, 1398f, 745f), PxRect(1068f, 768f, 1108f, 800f), PxRect(1382f, 768f, 1413f, 800f)
        };

        /// <summary>위험 구역 테스트용: 수족관방 안(고래 뼈 위쪽 통로)·연못정원방 안(서쪽 통로)의 걸을 수 있는 지점.</summary>
        public static Vector2 AquariumInside => Px(270f, 645f);
        public static Vector2 GardenInside => Px(1110f, 300f);

        public static MapData Build(StageState st) => Stage1(st);

        private static Rect MM(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(x0, y0, x1, y1);

        private static MapProp Add(MapData m, string id, float x, float y, string text, int slot, int room, float size = 1f)
        {
            var p = new MapProp { kind = id, id = id, pos = new Vector2(x, y), text = text, slot = slot, room = room, size = size };
            m.props.Add(p);
            return p;
        }

        private static MapProp AddPx(MapData m, string id, float px, float py, string text, int slot, int room, float size = 1f)
        {
            var w = Px(px, py);
            return Add(m, id, w.x, w.y, text, slot, room, size);
        }

        private const int Op = (int)GameRole.Operator, In = (int)GameRole.Interpreter, Na = (int)GameRole.Navigator, Ob = (int)GameRole.Observer;

        private static MapData Stage1(StageState st)
        {
            // bounds 는 길찾기 격자(0.5u)의 원점이기도 하다 — 바꾸면 좁은 통로의 격자 연결이 달라지니 도달 가능성 테스트를 다시 돌릴 것
            var m = new MapData { bounds = MM(-22f, -16f, 21.2f, 13.2f) };

            for (int r = 0; r < RoomCount; r++)
            {
                m.rooms[r] = RoomRect(r);
                m.open.Add(m.rooms[r]);
                if (r != RoomAtrium) m.open.Add(DoorRect(r));
            }
            foreach (var d in ShortcutDoors) m.open.Add(d);

            m.solids.Add(FountainSolid);
            foreach (var s in ExhibitSolids) m.solids.Add(s);
            if (st == null || !st.gate1Done) { m.solids.Add(LockedDoor3); m.solids.Add(LockedDoor3B); }

            // 관문 1 — 안내데스크(방 1, 왼쪽 위 전시실): 그림 벽 바로 아래 한 줄
            AddPx(m, "panel", 222f, 238f, "비상 제어반", Op, RoomDesk);
            AddPx(m, "poster", 112f, 238f, "코팅된 벽보(계산 모듈 매뉴얼)", In, RoomDesk);
            AddPx(m, "lamp", 432f, 238f, "경고등 패널", Ob, RoomDesk);
            // 이동 구간 — 관리실(방 2, STAFF 복도): STAFF ONLY 문 아래
            AddPx(m, "board", 1464f, 345f, "침수 현황판", Na, RoomOffice);
            // 관문 2 — 나침반 모자이크방(방 3, 오른쪽 아래 전시실)
            AddPx(m, "lever", 1381f, 625f, "방향 레버반", Op, RoomMosaic);
            AddPx(m, "mural", 1273f, 620f, "나침반 벽화 매뉴얼", In, RoomMosaic);
            AddPx(m, "colorlamp", 1165f, 625f, "색상 표시등", Ob, RoomMosaic);
            AddPx(m, "junction", 1306f, 778f, "배전함", Na, RoomMosaic);
            // 장식
            AddPx(m, "fountain", 769f, 470f, "분수", -1, RoomAtrium, 2f);
            Add(m, "door3", LockedDoor3.center.x, LockedDoor3.center.y, "모자이크방 문", -1, -1, 1f);
            Add(m, "door3", LockedDoor3B.center.x, LockedDoor3B.center.y, "모자이크방 문", -1, -1, 1f).id = "door3b";
            // 필리핀어 표지판(분위기용, 통역만 읽힘)
            AddPx(m, "sign", 672f, 530f, "TYPHOON: Manatiling kalmado", In, RoomAtrium).id = "sign0";
            AddPx(m, "sign", 128f, 380f, "Emergency exit → Atrium", In, RoomDesk).id = "sign1";
            AddPx(m, "sign", 1172f, 778f, "Power room — authorized only", In, RoomMosaic).id = "sign2";

            // 스폰: 홀 아래쪽(정문 안쪽) 가로 1.6유닛 간격 — 홀 폭(6.8u)과 양옆 벤치 사이에 들어가는 최대 간격
            for (int i = 0; i < 4; i++) m.spawns[i] = Px(680.5f + 57.65f * i, 688f);
            return m;
        }
    }
}
