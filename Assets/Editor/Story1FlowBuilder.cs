using Paradox.Client;
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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Story1FlowBuilder] Story1 에 목표·속마음·엔딩을 만들었습니다");
        }

        private static RectTransform Also(this RectTransform r, System.Func<RectTransform, RectTransform> f) { f(r); return r; }
    }
}
