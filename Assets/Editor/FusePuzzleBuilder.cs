using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>
    /// 퍼즐 1(퓨즈 복구)을 Story1.unity 에 씬 오브젝트로 만든다. 메뉴: Paradox ▸ Add Fuse Puzzle To Story1.
    /// 만드는 것: 정전 어둠(Blackout) + 손전등, 퓨즈함 화면(FusePanel, UI), 안내실 벽의 퓨즈함(FuseBox, 월드). 다시 실행하면 이 오브젝트들만 새로 만든다.
    /// </summary>
    public static class FusePuzzleBuilder
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

        private static FuseGfx Fuse(Transform parent, string name, float x, float y, float w, float h)
        {
            var root = Node(name, parent); At(root, x, y, w, h);
            var body = Node("Body", root); At(body, 0, 0, w, h);
            var bi = Img(body, null, Color.white);
            var cap = Node("Cap", body); At(cap, 0, h * 0.5f - h * 0.14f, w * 1.1f, h * 0.28f);
            var ci = Img(cap, null, Color.white);
            var shine = Node("Shine", body); At(shine, -w * 0.25f, -h * 0.05f, w * 0.14f, h * 0.6f);
            Img(shine, null, new Color(1, 1, 1, 0.28f));
            return new FuseGfx { body = bi, cap = ci };
        }

        [MenuItem("Paradox/Add Fuse Puzzle To Story1")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var n in new[] { "Blackout", "FusePanel", "Story1Blackout", "FuseBox" })
                foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (g != null && g.name == n) Object.DestroyImmediate(g.gameObject);

            var canvas = GameObject.Find("UI").GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform;
            var cam = Camera.main;
            var player = GameObject.Find("Player").transform;
            var story = Object.FindFirstObjectByType<Story1Scene>();

            // ---------------- 정전 어둠 + 손전등 (맨 뒤: 안내 문구·페이드보다 아래)
            var dark = Node("Blackout", root); Stretch(dark); dark.SetAsFirstSibling();
            var dg = dark.gameObject.AddComponent<CanvasGroup>(); dg.blocksRaycasts = false; dg.interactable = false; dg.alpha = 0.93f;
            var spot = Node("Spotlight", dark); At(spot, 0, 0, 4200, 4200);
            Img(spot, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Camp/spotlight.png"), Color.white);
            var bo = new GameObject("Story1Blackout").AddComponent<Story1Blackout>();
            bo.group = dg; bo.spot = spot; bo.canvas = canvas; bo.cam = cam; bo.player = player;

            // ---------------- 퓨즈함 화면
            var pr = Node("FusePanel", root); Stretch(pr);
            var pg = pr.gameObject.AddComponent<CanvasGroup>(); pg.alpha = 0f; pg.blocksRaycasts = false; pg.interactable = false;
            var panel = pr.gameObject.AddComponent<FusePanel>();
            var dim = Node("Dim", pr); Stretch(dim); Img(dim, null, new Color(0, 0, 0, 0.72f)).raycastTarget = true;
            var content = Node("Content", pr); At(content, 0, 0, 1500, 860);
            var back = Node("Back", content); Stretch(back); Sliced(back, PanelFill, new Color(0.06f, 0.07f, 0.1f, 0.98f));
            var border = Node("Border", content); Stretch(border); Sliced(border, PanelBorder, new Color(0.62f, 0.5f, 0.3f));

            var title = Txt(content, "Title", "", 4, Amber, 0, 380, 1300, 70, TextAnchor.MiddleCenter);
            var role = Txt(content, "Role", "", 2, Ink, 0, 328, 1400, 40, TextAnchor.MiddleCenter);

            float[] sx = { -330, -110, 110, 330 };
            var slotRects = new RectTransform[4]; var frames = new Image[4]; var nums = new PixelText[4]; var fuses = new FuseGfx[4];
            for (int i = 0; i < 4; i++)
            {
                var s = Node("Slot" + (i + 1), content); At(s, sx[i], 70, 170, 250); slotRects[i] = s;
                var fr = Node("Frame", s); Stretch(fr); frames[i] = Sliced(fr, PanelFill, new Color(0.13f, 0.15f, 0.2f, 1f));
                var fb = Node("FrameBorder", s); Stretch(fb); Sliced(fb, PanelBorder, new Color(0.36f, 0.4f, 0.46f));
                var hole = Node("Hole", s); At(hole, 0, -5, 86, 176); Img(hole, null, new Color(0.04f, 0.05f, 0.07f));
                nums[i] = Txt(s, "Num", (i + 1).ToString(), 4, Slate, 0, 176, 80, 60, TextAnchor.MiddleCenter);
                fuses[i] = Fuse(s, "Fuse", 0, -5, 76, 168);
            }

            // ---- 조작자 면: 퓨즈 서랍(트레이) + 커서
            var vOp = Node("ViewOperator", content); Stretch(vOp);
            var cur = Node("Cursor", vOp); At(cur, sx[0], 70, 196, 276);
            var curImg = Sliced(cur, PanelBorder, Amber);
            var trayRects = new RectTransform[4]; var tray = new FuseGfx[4]; var trayLabels = new PixelText[4];
            for (int i = 0; i < 4; i++)
            {
                var t = Node("Tray" + (i + 1), vOp); At(t, sx[i], -215, 150, 180); trayRects[i] = t;
                var tb = Node("Bg", t); Stretch(tb); Sliced(tb, PanelFill, new Color(0.1f, 0.12f, 0.16f, 1f));
                var tbb = Node("Border", t); Stretch(tbb); Sliced(tbb, PanelBorder, new Color(0.3f, 0.34f, 0.4f));
                tray[i] = Fuse(t, "Fuse", 0, 32, 38, 86);
                trayLabels[i] = Txt(t, "Label", "", 2, Ink, 0, -62, 146, 32, TextAnchor.MiddleCenter);
            }

            // ---- 통역 면: 설명서
            var vTr = Node("ViewInterpreter", content); Stretch(vTr);
            var page = Node("Page", vTr); At(page, 0, 20, 1240, 480);
            Img(page, null, new Color(0.9f, 0.84f, 0.68f));
            var pageB = Node("PageBorder", page); Stretch(pageB); Sliced(pageB, PanelBorder, new Color(0.45f, 0.33f, 0.2f));
            var brown = new Color(0.25f, 0.17f, 0.1f); var teal = new Color(0.08f, 0.38f, 0.36f);
            var mTl = new PixelText[2]; var mKo = new PixelText[2];
            for (int i = 0; i < 2; i++)
            {
                mTl[i] = Txt(page, "Tl" + i, "", 3, brown, 0, 130 - i * 150, 1160, 56, TextAnchor.MiddleCenter);
                mKo[i] = Txt(page, "Ko" + i, "", 2, teal, 0, 78 - i * 150, 1160, 40, TextAnchor.MiddleCenter);
            }
            var note = Txt(page, "Note", "", 2, new Color(0.4f, 0.3f, 0.2f), 0, -165, 1160, 40, TextAnchor.MiddleCenter);
            var mark = Txt(page, "Head", "MANWAL NG PAGPAPAAYOS", 2, new Color(0.45f, 0.33f, 0.2f), 0, 190, 1100, 36, TextAnchor.MiddleCenter);

            // ---- 관측자 면: 그을음
            var vOb = Node("ViewObserver", content); Stretch(vOb);
            var soot = new RectTransform[2]; var sparks = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var so = Node("Soot" + i, vOb); At(so, 0, 0, 130, 86); soot[i] = so;
                Img(so, null, new Color(0.3f, 0.23f, 0.17f, 0.95f));
                var so2 = Node("Soot2", so); At(so2, 18, 26, 70, 40); Img(so2, null, new Color(0.22f, 0.17f, 0.12f, 0.95f));
                var sp = Node("Spark", so); At(sp, 0, 0, 46, 46); sparks[i] = Img(sp, null, Color.white);
                var spc = Node("SparkCore", sp); At(spc, 0, 0, 20, 20); Img(spc, null, new Color(1, 1, 1, 0.9f));
            }

            // ---- 내비게이터 면: 배선도
            var vNv = Node("ViewNavigator", content); Stretch(vNv);
            var lugs = new RectTransform[4]; var lugImgs = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var lg = Node("Lug" + i, vNv); At(lg, sx[i], -200, 64, 64); lugs[i] = lg;
                lugImgs[i] = Img(lg, null, Color.white);
                var lgc = Node("Center", lg); At(lgc, 0, 0, 22, 22); Img(lgc, null, new Color(0.05f, 0.05f, 0.07f));
            }
            var wireR = Node("Wire", vNv); At(wireR, 0, 0, 100, 10); var wireImg = Img(wireR, null, Color.white);
            var wq = new PixelText[0];
            Txt(vNv, "LugLabel", "퓨즈 단자", 2, Slate, 0, -262, 1300, 36, TextAnchor.MiddleCenter);

            var status = Txt(content, "Status", "", 3, Ink, 0, -318, 1400, 50, TextAnchor.MiddleCenter);
            var hint = Txt(content, "Hint", "", 2, Slate, 0, -382, 1420, 80, TextAnchor.MiddleCenter);
            var flash = Node("Flash", content); Stretch(flash); var flashImg = Img(flash, null, new Color(1, 1, 1, 0));

            panel.group = pg; panel.content = content;
            panel.title = title; panel.roleText = role; panel.hint = hint; panel.status = status; panel.flash = flashImg;
            panel.slotRects = slotRects; panel.slotFrames = frames; panel.slotNums = nums; panel.slotFuses = fuses;
            panel.viewOperator = vOp.gameObject; panel.cursor = curImg; panel.trayRects = trayRects; panel.trayFuses = tray; panel.trayLabels = trayLabels;
            panel.viewInterpreter = vTr.gameObject; panel.manualTl = mTl; panel.manualKo = mKo; panel.manualNote = note;
            panel.viewObserver = vOb.gameObject; panel.sootRects = soot; panel.sootSpark = sparks;
            panel.viewNavigator = vNv.gameObject; panel.lugRects = lugs; panel.lugImages = lugImgs; panel.wire = wireR; panel.wireImage = wireImg; panel.wireQ = wq;

            // ---------------- 월드: 안내실 벽의 퓨즈함
            var boxGo = new GameObject("FuseBox");
            boxGo.transform.position = new Vector3(28.9f, -17.1f, 0f);
            var sr = boxGo.AddComponent<SpriteRenderer>(); sr.sortingOrder = 0;
            var ledGo = new GameObject("Led"); ledGo.transform.SetParent(boxGo.transform, false); ledGo.transform.localPosition = new Vector3(0.32f, 0.52f, 0f);
            var ledSr = ledGo.AddComponent<SpriteRenderer>(); ledSr.sortingOrder = 1;
            var fuseBox = boxGo.AddComponent<FuseBox>(); fuseBox.panel = panel; fuseBox.render = sr; fuseBox.led = ledSr; fuseBox.radius = 1.9f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FusePuzzleBuilder] Story1 에 퓨즈 퍼즐을 만들었습니다");
        }
    }
}
