using Paradox.Client;
using Paradox.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>Story1 의 목표 표시 · 도윤의 속마음 · 엔딩 UI 를 만든다. 메뉴: Paradox ▸ Add Story1 Flow.</summary>
    public static class Story1FlowBuilder
    {
        private const string ScenePath = "Assets/Scenes/Story1.unity";
        private static readonly Color Ink = new Color(0.92f, 0.95f, 0.96f);
        private static readonly Color Amber = new Color(0.949f, 0.773f, 0.447f);
        private static readonly Color Slate = new Color(0.55f, 0.6f, 0.66f);
        private static Sprite PanelFill => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_fill.png");
        private static Sprite PanelBorder => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_border.png");

        private static Image Sliced(RectTransform r, Sprite s, Color c)
        {
            var i = Img(r, s, c); i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f / 3f; return i;
        }

        private static CanvasGroup Group(RectTransform r) { var g = r.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0f; g.blocksRaycasts = false; g.interactable = false; return g; }

        [MenuItem("Paradox/Add Story1 Flow")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var c in Object.FindObjectsByType<Story1Flow>(FindObjectsInactive.Include)) Object.DestroyImmediate(c.gameObject);

            var root = (RectTransform)GameObject.Find("UI").GetComponent<Canvas>().transform;
            var fr = Node("Story1Flow", root); Stretch(fr);
            var flow = fr.gameObject.AddComponent<Story1Flow>();
            flow.scene = Object.FindFirstObjectByType<Story1Scene>();

            // ---- 목표 (왼쪽 위)
            var hud = Node("Objective", fr); At(hud, -600, 432, 680, 188);
            var hg = Group(hud);
            Sliced(Node("Back", hud).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.9f));
            Sliced(Node("Border", hud).Also(Stretch), PanelBorder, new Color(0.3f, 0.52f, 0.62f));
            var hTitle = Txt(hud, "Title", "", 2, Amber, 0, 59, 620, 36, TextAnchor.MiddleLeft);
            var hLines = new PixelText[3];
            for (int i = 0; i < 3; i++) hLines[i] = Txt(hud, "Line" + i, "", 2, Ink, 0, 15 - i * 38, 620, 36, TextAnchor.MiddleLeft);
            flow.hudRect = hud; flow.hud = hg; flow.hudTitle = hTitle; flow.hudLines = hLines;

            // ---- 도윤의 속마음 (위 가운데)
            var th = Node("Thought", fr); At(th, 420, 470, 1000, 96);
            var tg = Group(th);
            Sliced(Node("Back", th).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.92f));
            Sliced(Node("Border", th).Also(Stretch), PanelBorder, new Color(0.62f, 0.5f, 0.3f));
            var tName = Txt(th, "Name", "", 1, Amber, 0, 24, 940, 24, TextAnchor.MiddleCenter);
            var tText = Txt(th, "Text", "", 2, Ink, 0, -14, 940, 40, TextAnchor.MiddleCenter);
            flow.thought = tg; flow.thoughtName = tName; flow.thoughtText = tText;

            // ---- 엔딩
            var en = Node("Ending", fr); Stretch(en);
            var eg = Group(en);
            Img(Node("Black", en).Also(Stretch), null, new Color(0.02f, 0.03f, 0.05f, 1f));
            var heroes = new Image[4]; var hn = new PixelText[4];
            float[] hx = { -480, -160, 160, 480 };
            for (int i = 0; i < 4; i++)
            {
                var h = Node("Hero" + (i + 1), en); At(h, hx[i], 150, 128, 256);
                heroes[i] = Img(h, null, Color.white); heroes[i].preserveAspect = true;
                hn[i] = Txt(en, "HeroName" + (i + 1), "", 2, Ink, hx[i], 5, 260, 36, TextAnchor.MiddleCenter);
            }
            var nName = Txt(en, "NarName", "", 2, Amber, 0, -150, 1500, 40, TextAnchor.MiddleCenter);
            var nText = Txt(en, "NarText", "", 2, Ink, 0, -215, 1500, 50, TextAnchor.MiddleCenter);
            var nHint = Txt(en, "NarHint", "", 2, Slate, 0, -330, 1500, 36, TextAnchor.MiddleCenter);
            var rTitle = Txt(en, "ResTitle", "", 5, Amber, 0, -150, 1500, 90, TextAnchor.MiddleCenter);
            var rTime = Txt(en, "ResTime", "", 2, Ink, 0, -240, 1500, 40, TextAnchor.MiddleCenter);
            var rBest = Txt(en, "ResBest", "", 2, new Color(0.45f, 0.95f, 0.6f), 0, -295, 1500, 40, TextAnchor.MiddleCenter);
            var rHint = Txt(en, "ResHint", "", 2, Slate, 0, -365, 1500, 36, TextAnchor.MiddleCenter);
            var fl = Node("Flash", fr); Stretch(fl); var flashImg = Img(fl, null, new Color(1, 1, 1, 0));
            flow.ending = eg; flow.flash = flashImg; flow.heroes = heroes; flow.heroNames = hn;
            flow.narName = nName; flow.narText = nText; flow.narHint = nHint;
            flow.resTitle = rTitle; flow.resTime = rTime; flow.resBest = rBest; flow.resHint = rHint;
            flow.sheets = new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/korean.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/phil.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/vietnam.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/southafrica.png")
            };

            // ---- 신호 카드 띠(아래) + 최근 신호 기록(오른쪽)
            var sb = Node("SignalBar", fr); At(sb, 0, -488, 1500, 88);
            var sg = Group(sb);
            var bar = sb.gameObject.AddComponent<SignalBar>();
            Sliced(Node("Back", sb).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.96f));
            Sliced(Node("Border", sb).Also(Stretch), PanelBorder, new Color(0.3f, 0.52f, 0.62f));
            int n = Story1Net.CardCount;
            var cards = new RectTransform[n]; var clabels = new PixelText[n]; var cbacks = new Image[n];
            float cw = 98f, gap = 6f, x0 = -(n * cw + (n - 1) * gap) / 2f + cw / 2f;
            Color[] tint = { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white,
                new Color(0.92f, 0.27f, 0.27f), new Color(0.27f, 0.52f, 0.96f), new Color(0.98f, 0.80f, 0.22f), new Color(0.31f, 0.82f, 0.42f),
                new Color(0.45f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.45f), new Color(0.95f, 0.85f, 0.55f) };
            for (int i = 0; i < n; i++)
            {
                var c = Node("Card" + (i + 1), sb); At(c, x0 + i * (cw + gap), 0, cw, 64);
                cards[i] = c;
                cbacks[i] = Sliced(c, PanelFill, new Color(0.14f, 0.17f, 0.22f, 0.6f));
                Sliced(Node("Border", c).Also(Stretch), PanelBorder, tint[i] * 0.8f);
                clabels[i] = Txt(c, "Label", "", 2, tint[i], 0, 0, cw - 8, 40, TextAnchor.MiddleCenter);
            }
            // 채팅창(오른쪽 아래): 제목 + 최근 신호 8줄. 글자 크기 2(32px), 사방 여백 17px로 똑같이
            var chat = Node("Chat", fr); At(chat, 668, -214, 560, 420);
            var cgp = Group(chat);
            Sliced(Node("Back", chat).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.9f));
            Sliced(Node("Border", chat).Also(Stretch), PanelBorder, new Color(0.3f, 0.52f, 0.62f));
            var cTitle = Txt(chat, "Title", "신호 기록", 2, Amber, 0, 210 - 17 - 18, 526, 36, TextAnchor.MiddleLeft);
            var cEmpty = Txt(chat, "Empty", "", 2, Slate, 0, 0, 526, 36, TextAnchor.MiddleCenter);
            var feed = new PixelText[8];
            for (int i = 0; i < feed.Length; i++) feed[i] = Txt(chat, "Feed" + i, "", 2, Ink, 0, 210 - 17 - 36 - 12 - 18 - i * 40, 526, 36, TextAnchor.MiddleLeft);
            bar.chatGroup = cgp; bar.chatTitle = cTitle; bar.chatEmpty = cEmpty;
            bar.group = sg; bar.cards = cards; bar.labels = clabels; bar.backs = cbacks; bar.feedRows = feed;

            // ---- 방 이름 (위 가운데, 방에 들어갈 때 잠깐). 글자 너비에 맞춰 늘어나는 패널은 쓰지 않고 넉넉한 고정 폭
            var rb = Node("RoomBanner", fr); At(rb, 80, 440, 460, 84);   // 왼쪽 위 "목표" 패널(오른쪽 끝 -262)과 겹치지 않게 폭을 줄이고 살짝 오른쪽으로
            var rbg = Group(rb);
            Sliced(Node("Back", rb).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.88f));
            Sliced(Node("Border", rb).Also(Stretch), PanelBorder, new Color(0.62f, 0.5f, 0.3f));
            var rbt = Txt(rb, "Name", "", 3, Amber, 0, 0, 420, 56, TextAnchor.MiddleCenter);
            flow.roomBanner = rbg; flow.roomBannerText = rbt;

            // ---- 조작 안내(처음 한 번 + H)
            var tut = Node("Tutorial", fr); Stretch(tut);
            var tgrp = Group(tut);
            Img(Node("Dim", tut).Also(Stretch), null, new Color(0.02f, 0.03f, 0.05f, 0.7f));
            var tp = Node("Panel", tut); At(tp, 0, 0, 1240, 560);
            Sliced(Node("Back", tp).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.97f));
            Sliced(Node("Border", tp).Also(Stretch), PanelBorder, new Color(0.3f, 0.52f, 0.62f));
            Txt(tp, "Title", "조작 방법", 3, Amber, 0, 235, 1170, 56, TextAnchor.MiddleCenter);
            var troleLine = Txt(tp, "Role", "", 2, Ink, 0, 170, 1170, 40, TextAnchor.MiddleCenter);
            var tkeys = new PixelText[5]; var tdesc = new PixelText[5];
            for (int i = 0; i < 5; i++)
            {
                float yy = 90 - i * 58;
                tkeys[i] = Txt(tp, "Key" + i, "", 2, Amber, -330, yy, 420, 40, TextAnchor.MiddleRight);
                tdesc[i] = Txt(tp, "Desc" + i, "", 2, Ink, 330, yy, 700, 40, TextAnchor.MiddleLeft);
            }
            var thint = Txt(tp, "Hint", "", 2, Slate, 0, -245, 1170, 36, TextAnchor.MiddleCenter);
            var tut_c = tut.gameObject.AddComponent<Story1Tutorial>();
            tut_c.group = tgrp; tut_c.keys = tkeys; tut_c.descs = tdesc; tut_c.hint = thint; tut_c.roleLine = troleLine;

            BuildMap(fr, flow.scene);
            BuildPause(fr);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Story1FlowBuilder] Story1 에 목표·속마음·엔딩을 만들었습니다");
        }

        /// <summary>내비게이터 전용 전체 지도(M). 방 칸 위치는 방의 월드 위치에서 따온다.</summary>
        private static void BuildMap(RectTransform fr, Story1Scene sc)
        {
            var rooms = sc.rooms;
            var root = Node("Map", fr); Stretch(root);
            var g = Group(root);
            Img(Node("Dim", root).Also(Stretch), null, new Color(0.02f, 0.03f, 0.05f, 0.78f));
            const float PW = 1500f, PH = 900f;
            var panel = Node("Panel", root); At(panel, 0, 0, PW, PH);
            Sliced(Node("Back", panel).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.97f));
            Sliced(Node("Border", panel).Also(Stretch), PanelBorder, new Color(0.3f, 0.52f, 0.62f));
            Txt(panel, "Title", "박물관 지도", 3, Amber, 0, PH / 2 - 17 - 28, PW - 40, 56, TextAnchor.MiddleCenter);
            Txt(panel, "Close", "X 닫기", 2, Slate, 0, -PH / 2 + 17 + 18, PW - 40, 36, TextAnchor.MiddleCenter);

            // 월드 위치 → 칸 위치
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var r in rooms) { var p = r.transform.position; minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            float step = float.MaxValue;   // 가장 가까운 두 방 사이 거리(칸 크기 기준)
            for (int i = 0; i < rooms.Length; i++)
                for (int j = i + 1; j < rooms.Length; j++)
                {
                    var d = rooms[i].transform.position - rooms[j].transform.position;
                    float a = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                    if (a > 0.5f) step = Mathf.Min(step, a);
                }
            float areaW = PW - 120f, areaH = PH - 220f;
            float spanX = Mathf.Max(maxX - minX, 0.01f), spanY = Mathf.Max(maxY - minY, 0.01f);
            float s = Mathf.Min((areaW - 260f) / spanX, (areaH - 120f) / spanY);
            float tw = 270f, th = 132f;
            Debug.Log("[Map] 방 " + rooms.Length + "개 · 가로 " + spanX + " 세로 " + spanY + " · 최소간격 " + step + " · 배율 " + s);

            var tiles = new RectTransform[rooms.Length]; var backs = new Image[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                var p = rooms[i].transform.position;
                float x = (p.x - (minX + maxX) / 2f) * s, y = (p.y - (minY + maxY) / 2f) * s - 15f;
                var t = Node("Room" + i, panel); At(t, x, y, tw, th);
                backs[i] = Sliced(Node("Back", t).Also(Stretch), PanelFill, new Color(0.12f, 0.15f, 0.2f, 0.95f));
                Sliced(Node("Border", t).Also(Stretch), PanelBorder, new Color(0.3f, 0.52f, 0.62f));
                string nm = string.IsNullOrEmpty(rooms[i].roomName) ? rooms[i].name : rooms[i].roomName;
                Txt(t, "Name", nm, 2, Ink, 0, 24, tw - 16, 40, TextAnchor.MiddleCenter);
                string tag = PuzzleTag(rooms, rooms[i]);
                if (tag != "") Txt(t, "Tag", tag, 2, Amber, 0, -24, tw - 16, 40, TextAnchor.MiddleCenter);
                tiles[i] = t;
            }
            var dots = new RectTransform[Roles.Count]; var dimg = new Image[Roles.Count];
            for (int r = 0; r < Roles.Count; r++)
            {
                var d = Node("Dot" + r, panel); At(d, 0, 0, 20, 20);
                dimg[r] = Img(d, null, Color.white);
                dots[r] = d;
            }

            // 지도 키 안내(내비게이터에게만 보이는 작은 표시, 왼쪽 아래)
            var bd = Node("MapBadge", fr); At(bd, 855, 262, 180, 56);   // 나가기 단추 바로 아래
            var bg = Group(bd);
            Sliced(Node("Back", bd).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.9f));
            Sliced(Node("Border", bd).Also(Stretch), PanelBorder, new Color(0.62f, 0.5f, 0.3f));
            Txt(bd, "Text", "M 지도", 2, Amber, 0, 0, 170, 36, TextAnchor.MiddleCenter);

            var map = root.gameObject.AddComponent<Story1Map>();
            map.scene = sc; map.group = g; map.badge = bg; map.tiles = tiles; map.tileBack = backs; map.dots = dots; map.dotImg = dimg;
        }

        /// <summary>오른쪽 위 "나가기" 버튼 + 확인 창.</summary>
        private static void BuildPause(RectTransform fr)
        {
            var root = Node("Pause", fr); Stretch(root);
            var pause = root.gameObject.AddComponent<Story1Pause>();

            var btn = Node("ExitButton", root); At(btn, 855, 330, 180, 56);   // 오른쪽 가장자리 세로 단추 줄(위: 나가기, 아래: 지도)
            var bg = btn.gameObject.AddComponent<CanvasGroup>();
            var bimg = Sliced(btn, PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.9f)); bimg.raycastTarget = true;
            Sliced(Node("Border", btn).Also(Stretch), PanelBorder, new Color(0.62f, 0.3f, 0.3f));
            Txt(btn, "Text", "나가기 P", 2, Ink, 0, 0, 170, 36, TextAnchor.MiddleCenter);
            var b = btn.gameObject.AddComponent<Button>(); b.targetGraphic = bimg;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(b.onClick, pause.OpenFromButton);

            var cf = Node("Confirm", root); Stretch(cf);
            var cg = Group(cf);
            var dim = Img(Node("Dim", cf).Also(Stretch), null, new Color(0.02f, 0.03f, 0.05f, 0.78f)); dim.raycastTarget = true;
            var p = Node("Panel", cf); At(p, 0, 0, 900, 420);
            Sliced(Node("Back", p).Also(Stretch), PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.97f));
            Sliced(Node("Border", p).Also(Stretch), PanelBorder, new Color(0.3f, 0.52f, 0.62f));
            Txt(p, "Title", "스테이지 선택으로 나갈까요?", 3, Amber, 0, 110, 840, 56, TextAnchor.MiddleCenter);
            Txt(p, "Sub", "지금까지 푼 퍼즐은 저장되지 않아요", 2, Slate, 0, 40, 840, 40, TextAnchor.MiddleCenter);

            var stay = Node("Stay", p); At(stay, -200, -90, 360, 84);
            var simg = Sliced(stay, PanelFill, new Color(0.1f, 0.2f, 0.2f, 0.95f)); simg.raycastTarget = true;
            Sliced(Node("Border", stay).Also(Stretch), PanelBorder, new Color(0.3f, 0.82f, 0.7f));
            Txt(stay, "Text", "계속하기  X", 2, Ink, 0, 0, 340, 36, TextAnchor.MiddleCenter);
            var sb = stay.gameObject.AddComponent<Button>(); sb.targetGraphic = simg;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(sb.onClick, pause.Stay);

            var leave = Node("Leave", p); At(leave, 200, -90, 360, 84);
            var limg = Sliced(leave, PanelFill, new Color(0.2f, 0.1f, 0.1f, 0.95f)); limg.raycastTarget = true;
            Sliced(Node("Border", leave).Also(Stretch), PanelBorder, new Color(0.85f, 0.4f, 0.4f));
            Txt(leave, "Text", "나가기  Enter", 2, Ink, 0, 0, 340, 36, TextAnchor.MiddleCenter);
            var lb = leave.gameObject.AddComponent<Button>(); lb.targetGraphic = limg;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(lb.onClick, pause.Leave);

            pause.confirm = cg; pause.button = bg;
        }

        private static string PuzzleTag(MuseumRoom[] rooms, MuseumRoom room)
        {
            var tags = new System.Collections.Generic.List<string>();
            void Add<T>(string label) where T : Component
            {
                foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                {
                    MuseumRoom best = null; float bd = float.MaxValue;
                    foreach (var r in rooms) { float d = ((Vector2)r.transform.position - (Vector2)c.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = r; } }
                    if (best == room && !tags.Contains(label)) tags.Add(label);
                }
            }
            Add<FuseBox>("퓨즈 퍼즐"); Add<GalleryFloor>("바닥 퍼즐"); Add<RelicCase>("유물 퍼즐"); Add<WaterValves>("밸브 퍼즐");
            return string.Join(" · ", tags);
        }

        private static RectTransform Also(this RectTransform r, System.Func<RectTransform, RectTransform> f) { f(r); return r; }
    }
}
