using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 박물관 월드 렌더러(명세 4절·5-3·5-5·5-7·5-8). Session.Map(지형)과 상태로부터
    /// 방별 바닥·테두리·분수·잠긴 문·필리핀어 표지판·담당 오브젝트(발광)·침수 오버레이·핑 비콘·최종 집결 원·클리어 셔터를 그린다.
    /// 지형 키가 바뀌면 통째로 다시 만들고(Refresh), 그 외에는 매 프레임 Tick에서 상태에 맞춰 갱신한다.
    /// 카메라는 Build에서 고정 모드(한 층 전체)로 켜고 Dispose에서 되돌린다.
    /// </summary>
    public class StageWorld
    {
        private class PropView
        {
            public MapProp prop;
            public GameObject go;
            public SpriteRenderer icon;
            public SpriteRenderer glow;      // 담당자 발광(앰버 Disc)
            public SpriteRenderer ring;      // 맥동 링
            public TextMesh label;           // 발광 중 오브젝트 이름
            public TextMesh text;            // 표지판 글
            public SpriteRenderer hand;      // G1-12: 내가 Reach 안이면 프롭 위에 뜨는 손 아이콘("여기서 E")
            public bool glowing;
            public bool mine;                // 내 담당(원래 아이콘) / 비담당("?" 상자, P-A1)
            public bool revealed;            // B3/S-03: 내 담당이라도 방에 가 보거나 핑을 받기 전에는 "?" 상자 — 한 번 드러나면 유지
            public float phase;
        }

        private class Fx { public SpriteRenderer sr; public float born; public float life; public float scale; public float alpha = 0.9f; }

        public const int RoomCount = 6;
        public const float BeaconDuration = 10f;
        public const float ShutterOpenTime = 1.6f;
        /// <summary>클리어 셔터의 월드 사각형(아트리움 북쪽 벽, 라벨 포함) — 집결 게이지 패널이 이 영역을 가리지 않는지 테스트가 검사한다.</summary>
        // 메인 맵 그림: 위쪽 가운데 "TO 2F" 계단(그림 x 700~836) 앞을 막는다. 아래 끝 = 홀 북쪽 벽(그림 y 250 → 월드 3.03)
        public const float ShutterX0 = -2.36f, ShutterX1 = 1.42f, ShutterBottom = 6.0f, ShutterTopWithLabel = 8.4f;
        /// <summary>메인 맵 그림 키(Resources/Backgrounds/museum_map.png). 있으면 코드 바닥·벽 대신 이 그림 한 장을 깐다.</summary>
        public const string MapImageKey = "museum_map";
        /// <summary>지금 월드가 메인 맵 그림 위에 그려졌는가(테스트·디버그용).</summary>
        public bool UsesMapImage { get; private set; }
        /// <summary>따라가기 카메라의 세로 반 크기(월드 유닛). 작을수록 캐릭터·맵이 크게 보인다.</summary>
        public const float FollowCamSize = 6f;
        private float _savedCamSize = -1f;

        private GameObject _root;
        private MapData _map;
        private readonly Dictionary<string, PropView> _props = new Dictionary<string, PropView>();
        private readonly List<PropView> _missionProps = new List<PropView>();
        private readonly List<PropView> _signs = new List<PropView>();
        private readonly List<TextMesh> _roomLabels = new List<TextMesh>();   // 방 이름 라벨(내비게이터·통역 화면에만 표시)
        private readonly SpriteRenderer[] _floods = new SpriteRenderer[RoomCount];
        private readonly Vector2[] _floodBase = new Vector2[RoomCount];
        private readonly Vector2[] _floodSize = new Vector2[RoomCount];
        private readonly List<Fx> _fx = new List<Fx>();

        // 최종 집결 원
        private readonly SpriteRenderer[] _gatherRing = new SpriteRenderer[4];
        private readonly SpriteRenderer[] _gatherGlow = new SpriteRenderer[4];
        private readonly TextMesh[] _gatherLabel = new TextMesh[4];
        private readonly bool[] _gatherInside = new bool[4];
        private bool _gatherShown;

        // 핑 비콘
        private GameObject _beacon;
        private SpriteRenderer _beaconIcon, _beaconRing;
        private TextMesh _beaconText;
        private Vector2 _beaconPos;
        private float _beaconUntil = -1f;

        // 클리어 셔터(아트리움 북쪽 벽)
        private SpriteRenderer _shutter, _shutterLight;
        private readonly List<SpriteRenderer> _shutterStripes = new List<SpriteRenderer>();
        private TextMesh _shutterLabel;
        private float _shutterTop, _shutterHeight;
        private float _clearSeenAt = -1f;

        private SpriteRenderer _fountainRipple;
        private float _jiggleT;
        private Color _savedBg;
        private bool _cameraSet;

        private static readonly Color Amber = new Color(0.95f, 0.77f, 0.45f);
        private static readonly Color GrayIcon = new Color(0.5f, 0.53f, 0.6f);
        private static readonly Color FloodBlue = new Color(0.22f, 0.48f, 0.92f);
        private static readonly Color Border = new Color(0.52f, 0.56f, 0.66f, 0.9f);
        private static readonly Color DoorFloorA = new Color(0.25f, 0.25f, 0.29f);
        private static readonly Color DoorFloorB = new Color(0.22f, 0.22f, 0.26f);

        /// <summary>방 번호 → 한국어 이름(핑 말풍선·라벨 공용).</summary>
        public static string RoomLabel(int room) => room >= 0 && room < RoomCount ? MapData.RoomName(room) : "?";

        // ---------------------------------------------------------------- 수명

        public void Build()
        {
            Dispose();
            _root = new GameObject("StageWorld");
            ApplyCamera(true);
            Session.LocalSlotChanged += OnLocalSlot;   // 호스트가 조종 슬롯을 바꾸면 "?" 상자·표지판·방 이름을 새 시점으로(Dispose 에서 해제)
            Rebuild();
        }

        private void OnLocalSlot() => RefreshProps();

        public void Dispose()
        {
            Session.LocalSlotChanged -= OnLocalSlot;
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _map = null;
            ClearRefs();
            ApplyCamera(false);
            FeedbackFX.Vignette(false);
        }

        private void ClearRefs()
        {
            _props.Clear(); _missionProps.Clear(); _signs.Clear(); _roomLabels.Clear(); _fx.Clear(); _shutterStripes.Clear();
            for (int i = 0; i < RoomCount; i++) _floods[i] = null;
            for (int i = 0; i < 4; i++) { _gatherRing[i] = null; _gatherGlow[i] = null; _gatherLabel[i] = null; _gatherInside[i] = false; }
            _gatherShown = false;
            _beacon = null; _beaconIcon = null; _beaconRing = null; _beaconText = null; _beaconUntil = -1f;
            _shutter = null; _shutterLight = null; _shutterLabel = null; _fountainRipple = null;
            _clearSeenAt = -1f;
        }

        /// <summary>고정 카메라(한 층 전체) + 정전 배경색. 끌 때 원래대로.</summary>
        private void ApplyCamera(bool on)
        {
            var app = GameApp.Instance;
            var follow = app != null ? app.Follow : null;
            var cam = Camera.main;
            if (on)
            {
                // 맵이 2배로 커져 한 층 전체를 보여 주지 않고, 내 캐릭터를 따라가는 카메라(맵 그림 경계 안)로 본다
                if (follow != null)
                {
                    follow.useBounds = true;
                    follow.bounds = StageMaps.MapImageRect;
                    if (cam != null) { if (_savedCamSize < 0f) _savedCamSize = cam.orthographicSize; cam.orthographicSize = FollowCamSize; }
                    follow.SnapToTarget();
                }
                if (cam != null && !_cameraSet) { _savedBg = cam.backgroundColor; cam.backgroundColor = new Color(0.024f, 0.031f, 0.051f); }
                _cameraSet = true;
            }
            else
            {
                if (follow != null) follow.useBounds = false;
                if (cam != null && _savedCamSize > 0f) cam.orthographicSize = _savedCamSize;
                _savedCamSize = -1f;
                if (cam != null && _cameraSet) cam.backgroundColor = _savedBg;
                _cameraSet = false;
            }
        }

        /// <summary>상태 변화 시 호출. 지형이 바뀌면 재생성, 아니면 사물만 갱신.</summary>
        public void Refresh()
        {
            if (_root == null) return;
            if (Session.Map != _map) Rebuild();
            else RefreshProps();
        }

        // ---------------------------------------------------------------- 생성

        private void Rebuild()
        {
            if (_root == null) return;
            for (int i = _root.transform.childCount - 1; i >= 0; i--) Object.Destroy(_root.transform.GetChild(i).gameObject);
            ClearRefs();
            _map = Session.Map;
            if (_map == null) return;

            var t = _root.transform;
            var st = Session.St;

            // 배경: open 밖은 어둠
            SpriteLib.Quad(t, "Dark", _map.bounds.center, _map.bounds.size + new Vector2(60f, 60f), new Color(0.024f, 0.031f, 0.051f), -1100);

            // 메인 맵 그림이 있으면 그 한 장이 바닥·벽·전시물이다(코드 바닥/벽/장애물 블록은 그리지 않는다)
            UsesMapImage = BuildMapImage(t);

            // 방 바닥(방마다 색 다르게) + 테두리
            var rooms = _map.rooms ?? new Rect[0];
            for (int r = 0; r < rooms.Length; r++)
            {
                if (!UsesMapImage)
                {
                    RoomFloorColors(r, out var a, out var b);
                    Floor(t, "Floor" + r, rooms[r], a, b, -1000);
                    BuildBorder(t, rooms[r], r);
                }
                // 방 이름 라벨: 회색 작은 글씨. 방 이름은 현지어(??? 원칙)라 내비게이터(도면)·통역(현지어)에게만 보인다(RefreshProps에서 토글).
                // 관리실(2)은 아래 두 모서리가 액션 열·침수 현황판(−8,−5.8, 아이콘 1.5배)에 가리므로 오른쪽 위(벽 그림자 띠 바로 아래)에 둔다.
                // 아트리움(0)은 스폰 줄(y −2.4)이 아래 모서리를 밟으므로 왼쪽 위에 둔다.
                var labelColor = UsesMapImage ? new Color(1f, 1f, 1f, 0.78f) : new Color(1f, 1f, 1f, 0.3f);
                TextMesh label;
                if (UsesMapImage && r == StageMaps.RoomOffice)   // 좁은 STAFF 복도: 복도 아래쪽 끝 밑, 오른쪽 정렬
                    label = WorldText.Make(t, RoomLabel(r), new Vector3(rooms[r].xMax - 0.05f, rooms[r].yMin - 0.1f, 0f), 0.07f, labelColor, -996, TextAnchor.UpperRight, 48);
                else if (r == StageMaps.RoomOffice)
                    label = WorldText.Make(t, RoomLabel(r), new Vector3(rooms[r].xMax - 0.35f, rooms[r].yMax - 0.45f, 0f), 0.07f, labelColor, -996, TextAnchor.UpperRight, 48);
                else if (r == StageMaps.RoomAtrium)
                    label = WorldText.Make(t, RoomLabel(r), new Vector3(rooms[r].xMin + 0.35f, rooms[r].yMax - 0.45f, 0f), 0.07f, labelColor, -996, TextAnchor.UpperLeft, 48);
                else
                    label = WorldText.Make(t, RoomLabel(r), new Vector3(rooms[r].xMin + 0.35f, rooms[r].yMin + 0.28f, 0f), 0.07f, labelColor, -996, TextAnchor.LowerLeft, 48);
                _roomLabels.Add(label);
            }
            // 문(통로): 방이 아닌 open 사각형. 테두리 위에 그려 벽을 뚫는다
            foreach (var o in _map.open)
            {
                if (UsesMapImage) break;
                if (RoomIndexOf(o, rooms) >= 0) continue;
                Floor(t, "Door", o, DoorFloorA, DoorFloorB, -997);
            }

            // 모자이크방 나침반 무늬(원 + 십자)
            if (rooms.Length > 3 && !UsesMapImage)
            {
                var c = rooms[3].center;
                var pat = new Color(0.93f, 0.85f, 0.65f, 0.16f);
                SpriteLib.Make(t, "Compass", c, SpriteLib.Ring(0.08f), pat, 3.4f, -999);
                SpriteLib.Make(t, "CompassIn", c, SpriteLib.Ring(0.12f), pat, 1.6f, -999);
                SpriteLib.Quad(t, "CompassV", c, new Vector2(0.12f, 3.8f), pat, -999);
                SpriteLib.Quad(t, "CompassH", c, new Vector2(3.8f, 0.12f), pat, -999);
                WorldText.Make(t, "N", new Vector3(c.x, c.y + 2.05f, 0f), 0.08f, pat, -998, TextAnchor.MiddleCenter, 48);
            }

            // 침수 오버레이(방 사각형 위 파란 반투명)
            for (int r = 0; r < rooms.Length && r < RoomCount; r++)
            {
                _floodBase[r] = rooms[r].center;
                _floodSize[r] = rooms[r].size;
                _floods[r] = SpriteLib.Quad(t, "Flood" + r, rooms[r].center, rooms[r].size, new Color(FloodBlue.r, FloodBlue.g, FloodBlue.b, 0f), -980);
            }

            // 분수 이외의 장애물(있다면) — 어두운 블록
            foreach (var s in _map.solids)
            {
                if (UsesMapImage) break;   // 전시물은 그림에 이미 있다
                bool fountain = Vector2.Distance(s.center, StageMaps.FountainSolid.center) < 0.1f;
                bool door3 = Vector2.Distance(s.center, StageMaps.LockedDoor3.center) < 0.1f || Vector2.Distance(s.center, StageMaps.LockedDoor3B.center) < 0.1f;
                if (fountain || door3) continue;
                SpriteLib.Quad(t, "Solid", s.center, s.size, new Color(0.12f, 0.13f, 0.17f), YDepthSort.OrderFor(s.yMin, 50f));
            }

            // 사물
            foreach (var p in _map.props) BuildProp(p);

            // 최종 집결 원(숨김 상태로 준비)
            BuildGather();

            // 아트리움 북쪽 셔터(클리어 시 열림 연출)
            BuildShutter(rooms.Length > 0 ? rooms[0] : StageMaps.RoomRect(StageMaps.RoomAtrium));

            // 정전 분위기: 바깥으로 갈수록 어두운 비네트
            var vig = SpriteLib.Make(t, "WorldVignette", _map.bounds.center, SpriteLib.Vignette(0.38f), new Color(0f, 0f, 0f, UsesMapImage ? 0.45f : 0.7f), 1f, 940);
            vig.transform.localScale = new Vector3(_map.bounds.width + 6f, _map.bounds.height + 6f, 1f);

            RefreshProps();
        }

        /// <summary>
        /// 메인 맵 그림(museum_map) 한 장을 월드에 깐다: 그림 왼쪽 위 = StageMaps.MapOrigin, 1유닛 = MapPxPerUnit 픽셀(임포트 크기가 달라도 월드 크기에 맞춰 늘린다).
        /// 그림이 없으면 false — 호출자가 기존 코드 바닥·벽을 그린다.
        /// </summary>
        private bool BuildMapImage(Transform t)
        {
            var tex = Backgrounds.Get(MapImageKey);
            if (tex == null) return false;
            tex.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "map_" + MapImageKey;
            var rect = StageMaps.MapImageRect;
            var go = new GameObject("MapImage");
            go.transform.SetParent(t, false);
            go.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
            go.transform.localScale = new Vector3(rect.width / (tex.width / 100f), rect.height / (tex.height / 100f), 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -1000;   // 기존 방 바닥과 같은 층
            return true;
        }

        private static void RoomFloorColors(int room, out Color a, out Color b)
        {
            switch (room)
            {
                case 0: a = new Color(0.44f, 0.40f, 0.31f); b = new Color(0.40f, 0.36f, 0.28f); break;   // 아트리움: 밝은 베이지
                case 1: a = new Color(0.29f, 0.33f, 0.40f); b = new Color(0.26f, 0.30f, 0.36f); break;   // 안내데스크: 회청
                case 2: a = new Color(0.31f, 0.31f, 0.33f); b = new Color(0.28f, 0.28f, 0.30f); break;   // 관리실: 회색
                case 3: a = new Color(0.38f, 0.31f, 0.23f); b = new Color(0.34f, 0.28f, 0.21f); break;   // 모자이크방: 따뜻한 갈색 + 무늬
                case 4:
                case 5: a = new Color(0.20f, 0.37f, 0.39f); b = new Color(0.18f, 0.33f, 0.35f); break;   // 수족관·연못정원: 청록
                default: a = DoorFloorA; b = DoorFloorB; break;
            }
        }

        private static int RoomIndexOf(Rect o, Rect[] rooms)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                if (Mathf.Abs(r.xMin - o.xMin) < 0.02f && Mathf.Abs(r.xMax - o.xMax) < 0.02f && Mathf.Abs(r.yMin - o.yMin) < 0.02f && Mathf.Abs(r.yMax - o.yMax) < 0.02f) return i;
            }
            return -1;
        }

        private static SpriteRenderer Floor(Transform t, string name, Rect rect, Color a, Color b, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.transform.position = new Vector3(rect.center.x, rect.center.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLib.FloorTile(a, b);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = rect.size;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>방 테두리(벽선). 문 자리는 문 바닥이 위에 덮어 뚫린 것처럼 보인다.</summary>
        private void BuildBorder(Transform t, Rect r, int room)
        {
            const float th = 0.16f;
            int order = -998;
            SpriteLib.Quad(t, "WallN" + room, new Vector2(r.center.x, r.yMax), new Vector2(r.width + th, th), Border, order);
            SpriteLib.Quad(t, "WallS" + room, new Vector2(r.center.x, r.yMin), new Vector2(r.width + th, th), Border, order);
            SpriteLib.Quad(t, "WallW" + room, new Vector2(r.xMin, r.center.y), new Vector2(th, r.height + th), Border, order);
            SpriteLib.Quad(t, "WallE" + room, new Vector2(r.xMax, r.center.y), new Vector2(th, r.height + th), Border, order);
            // 벽 그림자(안쪽 위)
            SpriteLib.Quad(t, "WallShade" + room, new Vector2(r.center.x, r.yMax - 0.2f), new Vector2(r.width, 0.4f), new Color(0f, 0f, 0f, 0.22f), -999);
        }

        private void BuildProp(MapProp p)
        {
            var view = new PropView { prop = p };
            var go = new GameObject("prop:" + p.id);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(p.pos.x, p.pos.y, 0f);
            view.go = go;
            int order = YDepthSort.OrderFor(p.pos.y - 0.45f, 50f);
            string id = p.id ?? "";

            if (id.StartsWith("sign"))
            {
                view.icon = SpriteLib.Make(go.transform, "Icon", p.pos, SpriteLib.Icon("sign"), Color.white, 1.0f, order);
                view.text = WorldText.Make(go.transform, "", new Vector3(0f, 0.72f, 0f), 0.05f, new Color(1f, 0.95f, 0.75f), order + 30, TextAnchor.LowerCenter, 56);
                _signs.Add(view);
            }
            else if (id == "fountain" && UsesMapImage)
            {
                // 그림의 동상이 분수 자리를 대신한다 — 코드 분수는 그리지 않는다
            }
            else if (id == "fountain")
            {
                // 분수: 돌 테두리 원 + 물 + 가운데 + 물결
                var stone = SpriteLib.Make(go.transform, "Stone", p.pos, SpriteLib.Disc(0.05f), new Color(0.62f, 0.60f, 0.56f), 2.15f, YDepthSort.OrderFor(-1.0f, 50f));
                SpriteLib.Make(go.transform, "Water", p.pos, SpriteLib.Disc(0.08f), new Color(0.28f, 0.52f, 0.80f), 1.75f, stone.sortingOrder + 1);
                _fountainRipple = SpriteLib.Make(go.transform, "Ripple", p.pos, SpriteLib.Ring(0.1f), new Color(0.75f, 0.9f, 1f, 0.5f), 0.8f, stone.sortingOrder + 2);
                SpriteLib.Make(go.transform, "Core", p.pos, SpriteLib.Disc(0.2f), new Color(0.9f, 0.95f, 1f), 0.42f, stone.sortingOrder + 3);
                view.icon = stone;
            }
            else if (p.kind == "door3")
            {
                // 잠긴 문: 셔터 모양(gate1Done 전까지). 높이는 잠금 solid(홀 쪽 / STAFF 복도 쪽)에 맞춘다
                float h = StageMaps.LockedDoor3.height;
                var body = SpriteLib.Quad(go.transform, "Shutter", p.pos, new Vector2(0.62f, h), new Color(0.48f, 0.50f, 0.57f), YDepthSort.OrderFor(p.pos.y - h * 0.5f, 50f));
                for (int i = 0; i < 6; i++)
                    SpriteLib.Quad(go.transform, "Stripe", new Vector2(p.pos.x, p.pos.y - h * 0.5f + 0.25f + i * 0.38f), new Vector2(0.62f, 0.09f), new Color(0.25f, 0.26f, 0.3f), body.sortingOrder + 1);
                SpriteLib.Make(go.transform, "Lock", p.pos, SpriteLib.Icon("shutterdoor"), Color.white, 0.55f, body.sortingOrder + 2);
                view.icon = body;
                view.label = WorldText.Make(go.transform, "잠김", new Vector3(0f, h * 0.5f + 0.25f, 0f), 0.05f, new Color(0.8f, 0.8f, 0.85f, 0.8f), body.sortingOrder + 3, TextAnchor.LowerCenter, 48);
            }
            else if (StageInteractions.IsDecor(id))
            {
                view.icon = SpriteLib.Make(go.transform, "Icon", p.pos, SpriteLib.Icon(id), GrayIcon, 1f, order);
            }
            else
            {
                // 담당 오브젝트: 발광 Disc + 링(숨김) + 아이콘 + 이름 라벨(숨김)
                float scale = IconScale(id);
                view.glow = SpriteLib.Make(go.transform, "Glow", p.pos + new Vector2(0f, -0.1f), SpriteLib.Disc(0.7f), new Color(Amber.r, Amber.g, Amber.b, 0.38f), 2.4f * scale, -985);
                view.ring = SpriteLib.Make(go.transform, "Ring", p.pos + new Vector2(0f, -0.35f), SpriteLib.Ring(0.1f), new Color(Amber.r, Amber.g, Amber.b, 0.9f), 1.7f * scale, -984);
                view.ring.transform.localScale = new Vector3(1.7f * scale, 0.9f * scale, 1f);
                // 아이콘·틴트·크기는 UpdateGlow(force)가 시점(내 담당 여부)에 맞춰 정한다 — 비담당은 "?" 상자(P-A1)
                view.icon = SpriteLib.Make(go.transform, "Icon", p.pos, SpriteLib.Icon("unknown"), Color.white, UnknownScale, order);
                view.label = WorldText.Make(go.transform, StageInteractions.PropName(id), new Vector3(0f, 0.62f * scale + 0.1f, 0f), 0.055f, Amber, order + 30, TextAnchor.LowerCenter, 56);
                // G1-12: 손 아이콘은 이름 라벨 자리(라벨은 Reach 안에서 숨으므로 서로 겹치지 않는다)
                view.hand = SpriteLib.Make(go.transform, "Hand", p.pos + new Vector2(0f, 0.62f * scale + 0.2f), SpriteLib.Icon("hand"), Color.white, 0.55f, order + 31);
                view.glow.enabled = false; view.ring.enabled = false; view.label.gameObject.SetActive(false); view.hand.enabled = false;
                view.mine = false; view.revealed = false;
                view.phase = Random.value * 6.28f;
                _missionProps.Add(view);
            }

            _props[id] = view;
        }

        private static string IconKind(string id) => id == "lever" ? "leverpad" : id;

        /// <summary>비담당 "?" 상자 크기(오브젝트 종류와 무관하게 같은 크기 — 무엇인지 드러내지 않는다).</summary>
        public const float UnknownScale = 1.1f;

        private static float IconScale(string id)
        {
            switch (id)
            {
                case "panel": return 1.35f;
                case "board": return 1.5f;
                case "junction": return 1.3f;
                case "mural": return 1.4f;
                case "lever": return 1.25f;
                case "poster": return 1.15f;
                default: return 1.1f;
            }
        }

        private void BuildGather()
        {
            float d = Stage1Logic.GatherRadius * 2f;
            for (int i = 0; i < 4; i++)
            {
                var c = Roles.Color(i);
                _gatherGlow[i] = SpriteLib.Make(_root.transform, "GatherGlow" + i, Vector2.zero, SpriteLib.Disc(0.5f), new Color(c.r, c.g, c.b, 0f), d * 1.15f, -991);
                _gatherRing[i] = SpriteLib.Make(_root.transform, "GatherRing" + i, Vector2.zero, SpriteLib.DashedRing(20, 0.11f), new Color(c.r, c.g, c.b, 0.75f), d, -990);
                // P-A12: 라벨은 원 위쪽 가장자리(한 줄) — 원 아래에 두면 옆 원에 선 아바타의 이름 배지와 겹친다
                _gatherLabel[i] = WorldText.Make(_root.transform, "", Vector3.zero, 0.07f, new Color(c.r, c.g, c.b, 0.9f), -989, TextAnchor.UpperCenter, 48);   // 원 아래(아바타 머리 위 배지와 안 겹치게)
                _gatherGlow[i].enabled = false; _gatherRing[i].enabled = false; _gatherLabel[i].gameObject.SetActive(false);
            }
            _gatherShown = false;
        }

        private void BuildShutter(Rect atrium)
        {
            // 북쪽 벽, 연못정원 문(x −1~1) 오른쪽에 셔터 한 짝
            float x0 = ShutterX0, x1 = ShutterX1;
            _shutterTop = atrium.yMax + 1.05f;
            _shutterHeight = 1.0f;
            var t = _root.transform;
            float cx = (x0 + x1) * 0.5f, w = x1 - x0;
            _shutterLight = SpriteLib.Quad(t, "ShutterLight", new Vector2(cx, _shutterTop - _shutterHeight * 0.5f), new Vector2(w, _shutterHeight), new Color(Amber.r, Amber.g, Amber.b, 0f), -996);
            _shutter = SpriteLib.Quad(t, "ClearShutter", new Vector2(cx, _shutterTop - _shutterHeight * 0.5f), new Vector2(w, _shutterHeight), new Color(0.46f, 0.48f, 0.55f), -995);
            for (int i = 0; i < 4; i++)
            {
                var s = SpriteLib.Quad(t, "ShutterStripe", new Vector2(cx, _shutterTop - 0.15f - i * 0.25f), new Vector2(w, 0.06f), new Color(0.25f, 0.26f, 0.3f), -994);
                _shutterStripes.Add(s);
            }
            SpriteLib.Quad(t, "ShutterFrameL", new Vector2(x0 - 0.05f, _shutterTop - _shutterHeight * 0.5f), new Vector2(0.12f, _shutterHeight + 0.1f), Border, -993);
            SpriteLib.Quad(t, "ShutterFrameR", new Vector2(x1 + 0.05f, _shutterTop - _shutterHeight * 0.5f), new Vector2(0.12f, _shutterHeight + 0.1f), Border, -993);
            _shutterLabel = WorldText.Make(t, "출구 셔터", new Vector3(cx, _shutterTop + 0.08f, 0f), 0.05f, new Color(0.75f, 0.78f, 0.85f, 0.7f), -992, TextAnchor.LowerCenter, 48);
        }

        // ---------------------------------------------------------------- 상태 갱신

        private static string Garble(int len)
        {
            var sb = new System.Text.StringBuilder();
            int n = Mathf.Clamp(len / 3, 3, 9);
            for (int i = 0; i < n; i++) { sb.Append('?', Random.Range(1, 4)); if (i < n - 1) sb.Append(' '); }
            return sb.ToString();
        }

        /// <summary>상태·시점(내 역할)에 맞게 사물 표시를 갱신. 발광은 위치에 따라 매 프레임 바뀌므로 Tick에서 따로 본다.</summary>
        public void RefreshProps()
        {
            if (_map == null) return;
            var st = Session.St;
            int me = Session.LocalSlot;

            // 표지판: 통역만 원문, 나머지는 ??? 지글(Tick에서 갱신)
            foreach (var v in _signs)
            {
                bool readable = v.prop.slot < 0 || v.prop.slot == me;
                v.text.text = readable ? v.prop.text : Garble(v.prop.text.Length);
                v.text.color = readable ? new Color(1f, 0.95f, 0.75f) : new Color(0.85f, 0.5f, 0.5f);
                v.text.characterSize = readable ? 0.05f : 0.06f;
            }

            // 잠긴 문: 관문 1이 열리면 사라진다
            if (_props.TryGetValue("door3", out var door)) door.go.SetActive(!st.gate1Done);
            if (_props.TryGetValue("door3b", out var doorB)) doorB.go.SetActive(!st.gate1Done);

            // 방 이름 라벨: 현지어(??? 원칙) — 도면을 가진 내비게이터와 현지어를 읽는 통역에게만 보인다(핑 D5·미니맵 "?" 규칙과 맞춘다)
            bool showRoomNames = me == Comms.Na || me == Comms.In;
            foreach (var l in _roomLabels) if (l != null) l.gameObject.SetActive(showRoomNames);

            UpdateGlow(true);
        }

        /// <summary>
        /// 발광 규칙 적용(담당자 + 같은 방 + 미완료). force면 바뀌지 않아도 다시 칠한다.
        /// P-A1: 내 담당이 아닌 오브젝트는 발광 여부와 무관하게 "?" 상자(무엇인지 모른다), 내 담당은 원래 아이콘(발광 중이면 흰색, 아니면 회색 틴트).
        /// </summary>
        private void UpdateGlow(bool force)
        {
            int me = Session.LocalSlot;
            var mePos = Session.PosOf(me);
            var st = Session.St;
            int myRoom = mePos != null ? _map.RoomAt(mePos.Value) : -1;
            int pingRoom = st != null ? st.lastPingRoom : -1;
            foreach (var v in _missionProps)
            {
                bool mine = v.prop.slot == me;
                bool glow = mine && mePos != null && StageInteractions.IsGlowing(v.prop, me, mePos.Value);
                // B3/S-03: 내 담당이라도 "내가 그 방에 있거나 · 그 방이 핑됐거나 · 이미 발광/완료"가 아니면 아직 "?" 상자. 한 번 드러나면 유지(완료는 회색 실제 아이콘)
                int propRoom = Comms.PropRoom(v.prop.id);
                bool revealed = mine && (v.revealed || glow || myRoom == propRoom || pingRoom == propRoom || StageInteractions.MissionDone(st, v.prop.id));
                if (!force && glow == v.glowing && mine == v.mine && revealed == v.revealed) continue;
                v.glowing = glow;
                v.mine = mine;
                v.revealed = revealed;
                if (revealed)
                {
                    v.icon.sprite = SpriteLib.Icon(IconKind(v.prop.id));
                    v.icon.transform.localScale = Vector3.one * IconScale(v.prop.id);
                    v.icon.color = glow ? Color.white : GrayIcon;
                }
                else
                {
                    v.icon.sprite = SpriteLib.Icon("unknown");
                    v.icon.transform.localScale = Vector3.one * UnknownScale;
                    v.icon.color = Color.white;
                }
                v.glow.enabled = glow;
                v.ring.enabled = glow;
                v.label.gameObject.SetActive(glow);
                if (!glow && v.hand != null) v.hand.enabled = false;
            }
        }

        /// <summary>테스트 훅: 오브젝트가 지금 "?" 상자로 그려지는가(내 담당이 아니다).</summary>
        public bool IsUnknownBox(string id) => _props.TryGetValue(id ?? "", out var v) && v.icon != null && v.icon.sprite == SpriteLib.Icon("unknown");

        public void OnEvent(GameEvent e)
        {
            if (_root == null || _map == null || e == null) return;
            switch (e.type)
            {
                case "Ping":
                    {
                        Vector2 pos = e.pos.V;
                        if (pos.sqrMagnitude < 0.0001f) pos = Stage1Logic.RoomDoor(e.a);
                        ShowBeacon(pos);
                        break;
                    }
                case "GateOpen":
                    if (e.a == 1)
                    {
                        // B6: 1.2초 버스트만으로는 놓친다 — 문 위치에 비콘(핑 비콘 재사용)을 5초 세워 "어디가 열렸는지" 보여준다
                        var door3 = _map.Prop("door3");
                        Vector2 doorPos = door3 != null ? door3.pos : StageMaps.LockedDoor3.center;
                        Burst(doorPos, Amber, 3.2f, 1.2f);
                        ShowBeacon(doorPos, GateOpenBeacon, "문이 열렸어요!");
                    }
                    else if (e.a == 2) Burst(StageMaps.GatherOrigin, Amber, 5f, 1.4f);
                    break;
                case "Alarm":
                    {
                        // 오답 경보: 관문 방 전체가 붉게 번쩍
                        var rooms = _map.rooms;
                        int room = Session.St.gate1Done ? 3 : 1;
                        if (rooms != null && room < rooms.Length)
                        {
                            var q = SpriteLib.Quad(_root.transform, "AlarmFx", rooms[room].center, rooms[room].size, new Color(1f, 0.25f, 0.2f, 0.35f), -979);
                            _fx.Add(new Fx { sr = q, born = Time.time, life = 0.8f, scale = -1f, alpha = 0.35f });
                        }
                        break;
                    }
                case "Clear":
                    if (_clearSeenAt < 0f) _clearSeenAt = Time.time;
                    break;
            }
        }

        private void Burst(Vector2 pos, Color c, float scale, float life)
        {
            var sr = SpriteLib.Make(_root.transform, "BurstFx", pos, SpriteLib.Ring(0.12f), new Color(c.r, c.g, c.b, 0.9f), 0.3f, 300);
            _fx.Add(new Fx { sr = sr, born = Time.time, life = life, scale = scale });
        }

        /// <summary>B6: 관문 1 해제 뒤 문 위치 비콘 유지 시간.</summary>
        public const float GateOpenBeacon = 5f;

        private void ShowBeacon(Vector2 pos, float duration = BeaconDuration, string text = "여기로!")
        {
            var t = _root.transform;
            if (_beacon == null)
            {
                _beacon = new GameObject("PingBeacon");
                _beacon.transform.SetParent(t, false);
                _beaconRing = SpriteLib.Make(_beacon.transform, "Ring", pos, SpriteLib.Ring(0.14f), new Color(Amber.r, Amber.g, Amber.b, 0.8f), 1.4f, -983);
                _beaconRing.transform.localScale = new Vector3(1.6f, 0.8f, 1f);
                _beaconIcon = SpriteLib.Make(_beacon.transform, "Icon", pos + new Vector2(0f, 1.0f), SpriteLib.Icon("beacon"), Amber, 1.3f, 300);
                _beaconText = WorldText.Make(_beacon.transform, "여기로!", new Vector3(0f, 0f, 0f), 0.06f, Amber, 301, TextAnchor.LowerCenter, 52);
            }
            _beaconPos = pos;
            _beacon.SetActive(true);
            _beaconRing.transform.position = new Vector3(pos.x, pos.y, 0f);
            _beaconIcon.transform.position = new Vector3(pos.x, pos.y + 1.0f, 0f);
            _beaconText.transform.position = new Vector3(pos.x, pos.y + 1.75f, 0f);
            _beaconText.text = text ?? "여기로!";
            _beaconUntil = Time.time + duration;
        }

        // ---------------------------------------------------------------- 매 프레임

        public void Tick()
        {
            if (_root == null || _map == null) return;
            var st = Session.St;
            float clock = Session.Clock;
            float now = Time.time;
            int me = Session.LocalSlot;
            var mePos = Session.PosOf(me);
            _jiggleT += Time.deltaTime;

            // 1) 담당 오브젝트 발광(위치 의존) + 맥동
            UpdateGlow(false);
            foreach (var v in _missionProps)
            {
                if (!v.glowing) continue;
                // 이름 라벨: 내가 Reach 안(하단 [E] 안내가 뜨는 거리)이면 숨기고 Reach+0.6까지 서서히 켠다
                // — 프롭 아래 0.4~1.2유닛에 선 내 역할 배지(로컬 y 1.7~2.0)와 같은 높이라 겹치기 때문
                float dLabel = mePos != null ? Vector2.Distance(mePos.Value, v.prop.pos) : 99f;
                float la = Mathf.Clamp01((dLabel - StageInteractions.Reach) / 0.6f);
                v.label.gameObject.SetActive(la > 0.01f);
                v.label.color = new Color(Amber.r, Amber.g, Amber.b, la);
                // G1-12: Reach 안이면 라벨 대신 손 아이콘이 프롭 위에서 까딱인다("여기서 E" — 글을 못 읽어도 보인다)
                if (v.hand != null)
                {
                    bool near = dLabel <= StageInteractions.Reach;
                    if (v.hand.enabled != near) v.hand.enabled = near;
                    if (near)
                    {
                        float hy = v.prop.pos.y + 0.62f * IconScale(v.prop.id) + 0.2f + 0.08f * Mathf.Sin(now * 5f);
                        v.hand.transform.position = new Vector3(v.prop.pos.x, hy, 0f);
                        v.hand.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(now * 5f));
                    }
                }
                float k = 0.5f + 0.5f * Mathf.Sin(now * 3.2f + v.phase);
                float s = IconScale(v.prop.id);
                v.glow.color = new Color(Amber.r, Amber.g, Amber.b, 0.28f + 0.18f * k);
                v.glow.transform.localScale = Vector3.one * (2.4f * s) * (1f + 0.08f * k);
                float rs = 1.7f * s * (1f + 0.12f * k);
                v.ring.transform.localScale = new Vector3(rs, rs * 0.53f, 1f);
                v.ring.color = new Color(Amber.r, Amber.g, Amber.b, 0.95f - 0.5f * k);
            }

            // 2) 침수 오버레이: 알파 = 침수율/100 × 0.55, 물결 흔들림
            int myRoom = mePos != null ? _map.RoomAt(mePos.Value) : -1;
            for (int r = 0; r < RoomCount; r++)
            {
                var sr = _floods[r];
                if (sr == null) continue;
                float f = Stage1Logic.Flood(st, r, clock);
                float a = Mathf.Clamp01(f / 100f) * 0.55f;
                bool danger = Stage1Logic.IsDanger(st, r, clock);
                var col = danger ? Color.Lerp(FloodBlue, new Color(0.55f, 0.35f, 0.9f), 0.25f + 0.2f * Mathf.Sin(now * 5f)) : FloodBlue;
                sr.color = new Color(col.r, col.g, col.b, a);
                float jx = Mathf.Sin(now * 1.3f + r * 1.1f) * 0.06f, jy = Mathf.Sin(now * 1.7f + r * 2.3f) * 0.05f;
                sr.transform.position = new Vector3(_floodBase[r].x + jx, _floodBase[r].y + jy, 0f);
                sr.transform.localScale = new Vector3(_floodSize[r].x + 0.1f + 0.06f * Mathf.Sin(now * 2.1f + r), _floodSize[r].y + 0.1f + 0.05f * Mathf.Cos(now * 1.9f + r), 1f);
            }

            // 3) 내가 위험 방 안이면 붉은 비네트
            bool inDanger = myRoom >= 0 && !st.cleared && !st.failed && Stage1Logic.IsDanger(st, myRoom, clock);
            FeedbackFX.Vignette(inDanger);

            // 4) ??? 표지판 지글거림
            if (_jiggleT > 0.15f)
            {
                _jiggleT = 0f;
                foreach (var v in _signs)
                    if (v.prop.slot >= 0 && v.prop.slot != me) v.text.text = Garble(v.prop.text.Length);
            }

            // 5) 분수 물결
            if (_fountainRipple != null)
            {
                float ph = Mathf.Repeat(now * 0.7f, 1f);
                _fountainRipple.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.6f, ph);
                _fountainRipple.color = new Color(0.75f, 0.9f, 1f, 0.55f * (1f - ph));
            }

            // 6) 최종 집결 원(step 2): 중심은 매 프레임 이동, 미도착 점선 / 도착 실선 발광
            TickGather(st, clock, now);

            // 7) 핑 비콘(10초 깜빡임)
            if (_beacon != null && _beacon.activeSelf)
            {
                if (now > _beaconUntil) _beacon.SetActive(false);
                else
                {
                    float left = _beaconUntil - now;
                    float blink = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(now * 5f));
                    if (left < 2f) blink *= Mathf.Clamp01(left / 2f);
                    _beaconIcon.color = new Color(Amber.r, Amber.g, Amber.b, blink);
                    _beaconIcon.transform.position = new Vector3(_beaconPos.x, _beaconPos.y + 1.0f + 0.12f * Mathf.Sin(now * 4f), 0f);
                    float rs = 1.2f + 0.5f * Mathf.Repeat(now * 0.9f, 1f);
                    _beaconRing.transform.localScale = new Vector3(rs * 1.3f, rs * 0.65f, 1f);
                    _beaconRing.color = new Color(Amber.r, Amber.g, Amber.b, (1f - Mathf.Repeat(now * 0.9f, 1f)) * blink);
                    _beaconText.color = new Color(Amber.r, Amber.g, Amber.b, blink);
                }
            }

            // 8) 클리어: 셔터가 올라간다
            if (st.cleared && _clearSeenAt < 0f) _clearSeenAt = now;
            if (!st.cleared) _clearSeenAt = -1f;
            TickShutter(now);

            // 9) 일회성 이펙트
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                if (f.sr == null) { _fx.RemoveAt(i); continue; }
                float k = Mathf.Clamp01((now - f.born) / f.life);
                if (k >= 1f) { Object.Destroy(f.sr.gameObject); _fx.RemoveAt(i); continue; }
                var c = f.sr.color; c.a = (1f - k) * f.alpha; f.sr.color = c;
                if (f.scale > 0f) f.sr.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, f.scale, Mathf.Sqrt(k));
            }
        }

        private void TickGather(StageState st, float clock, float now)
        {
            bool show = st.step == 2 && !st.failed;
            if (show != _gatherShown)
            {
                _gatherShown = show;
                for (int i = 0; i < 4; i++)
                {
                    if (_gatherRing[i] == null) continue;
                    _gatherRing[i].enabled = show;
                    _gatherGlow[i].enabled = show;
                    _gatherLabel[i].gameObject.SetActive(show);
                    _gatherInside[i] = false;
                    _gatherRing[i].sprite = SpriteLib.DashedRing(20, 0.11f);
                }
            }
            if (!show) return;

            float d = Stage1Logic.GatherRadius * 2f;
            for (int i = 0; i < 4; i++)
            {
                if (_gatherRing[i] == null) continue;
                Vector2 c = Stage1Logic.GatherCenter(i, st, clock);
                var p = Session.PosOf(i);
                bool inside = p != null && Stage1Logic.InGather(i, p.Value, st, clock);
                var rc = Roles.Color(i);
                if (inside != _gatherInside[i])
                {
                    _gatherInside[i] = inside;
                    _gatherRing[i].sprite = inside ? SpriteLib.Ring(0.11f) : SpriteLib.DashedRing(20, 0.11f);
                    if (inside) Burst(c, rc, d * 1.4f, 0.6f);
                }
                _gatherRing[i].transform.position = new Vector3(c.x, c.y, 0f);
                _gatherGlow[i].transform.position = new Vector3(c.x, c.y, 0f);
                _gatherLabel[i].transform.position = new Vector3(c.x, c.y - Stage1Logic.GatherRadius - 0.12f, 0f);   // 원 아래 — 위쪽은 아바타 이름 배지 자리
                float pulse = inside ? 1f + 0.05f * Mathf.Sin(now * 6f + i) : 1f;
                _gatherRing[i].transform.localScale = Vector3.one * d * pulse;
                _gatherRing[i].transform.localRotation = inside ? Quaternion.identity : Quaternion.Euler(0f, 0f, now * 12f * (i % 2 == 0 ? 1f : -1f));
                _gatherRing[i].color = inside ? Color.Lerp(rc, Color.white, 0.35f) : new Color(rc.r, rc.g, rc.b, 0.7f);
                _gatherGlow[i].color = new Color(rc.r, rc.g, rc.b, inside ? 0.22f + 0.08f * Mathf.Sin(now * 6f + i) : 0.05f);
                string state = inside ? "도착" : "미도착";
                _gatherLabel[i].text = $"<b>{Roles.Code(i)}</b> <size=36>{state}</size>";
                _gatherLabel[i].color = inside ? Color.white : new Color(rc.r, rc.g, rc.b, 0.85f);
            }
        }

        private void TickShutter(float now)
        {
            if (_shutter == null) return;
            float open = _clearSeenAt < 0f ? 0f : Mathf.Clamp01((now - _clearSeenAt) / ShutterOpenTime);
            open = Mathf.SmoothStep(0f, 1f, open);
            float h = Mathf.Lerp(_shutterHeight, 0.08f, open);
            float cx = _shutter.transform.position.x;
            _shutter.transform.position = new Vector3(cx, _shutterTop - h * 0.5f, 0f);
            _shutter.transform.localScale = new Vector3(_shutter.transform.localScale.x, h, 1f);
            for (int i = 0; i < _shutterStripes.Count; i++)
            {
                float y = _shutterTop - 0.15f - i * 0.25f * (1f - open);
                _shutterStripes[i].transform.position = new Vector3(cx, y, 0f);
                _shutterStripes[i].enabled = y > _shutterTop - h;
            }
            if (_shutterLight != null)
            {
                float glow = open * (0.55f + 0.15f * Mathf.Sin(now * 4f));
                _shutterLight.color = new Color(Amber.r, Amber.g, Amber.b, glow);
            }
            if (_shutterLabel != null)
            {
                _shutterLabel.text = open > 0.5f ? "출구 개방!" : "출구 셔터";
                _shutterLabel.color = open > 0.5f ? Amber : new Color(0.75f, 0.78f, 0.85f, 0.7f);
            }
        }
    }
}
