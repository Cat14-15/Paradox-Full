using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>
    /// 퍼즐 3(유물 보관실)을 Story1.unity 에 씬 오브젝트로 만든다. 메뉴: Paradox ▸ Add Relic Puzzle To Story1.
    /// 만드는 것: 퍼즐 화면(RelicPanel, UI), 수장고의 유물 전시대(RelicCase, 월드). 다시 실행하면 이 오브젝트들만 새로 만든다.
    /// </summary>
    public static class RelicPuzzleBuilder
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

        // 유물 상자 하나(상자 + 이름표 글자). 선반·받침대·손에 든 것 모두 같은 모양.
        private static GameObject Relic(Transform parent, string name, float x, float y, float w, float h, out PixelText tag)
        {
            var r = Node(name, parent); At(r, x, y, w, h);
            var body = Node("Body", r); Stretch(body); Sliced(body, PanelFill, new Color(0.62f, 0.5f, 0.34f));
            var bb = Node("Border", r); Stretch(bb); Sliced(bb, PanelBorder, new Color(0.3f, 0.2f, 0.12f));
            var gem = Node("Gem", r); At(gem, 0, h * 0.5f - 42f, 40, 40); Img(gem, null, new Color(0.85f, 0.7f, 0.35f));
            tag = Txt(r, "Tag", "", 2, new Color(0.2f, 0.12f, 0.07f), 0, -h * 0.5f + 30f, w - 12f, 34, TextAnchor.MiddleCenter);
            return r.gameObject;
        }

        [MenuItem("Paradox/Add Relic Puzzle To Story1")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var n in new[] { "RelicPanel", "RelicCase" })
                foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (g != null && g.name == n) Object.DestroyImmediate(g.gameObject);

            var canvas = GameObject.Find("UI").GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform;

            var pr = Node("RelicPanel", root); Stretch(pr);
            var pg = pr.gameObject.AddComponent<CanvasGroup>(); pg.alpha = 0f; pg.blocksRaycasts = false; pg.interactable = false;
            var panel = pr.gameObject.AddComponent<RelicPanel>();
            var dim = Node("Dim", pr); Stretch(dim); Img(dim, null, new Color(0, 0, 0, 0.76f)).raycastTarget = true;
            var content = Node("Content", pr); At(content, 0, 0, 1500, 860);
            var back = Node("Back", content); Stretch(back); Sliced(back, PanelFill, new Color(0.07f, 0.06f, 0.08f, 0.98f));
            var border = Node("Border", content); Stretch(border); Sliced(border, PanelBorder, new Color(0.6f, 0.48f, 0.3f));

            var title = Txt(content, "Title", "", 4, Amber, 0, 380, 1300, 70, TextAnchor.MiddleCenter);
            var role = Txt(content, "Role", "", 2, Ink, 0, 328, 1400, 40, TextAnchor.MiddleCenter);

            // ---- 조작자 면: 선반 5칸 + 받침대 4개
            var vOp = Node("ViewOperator", content); Stretch(vOp);
            Txt(vOp, "ShelfLabel", "선반", 2, Slate, -650, 150, 100, 36, TextAnchor.MiddleCenter);
            Txt(vOp, "PedLabel", "받침대", 2, Slate, -640, -140, 110, 36, TextAnchor.MiddleCenter);
            float[] sx = { -480, -240, 0, 240, 480 }; float[] px = { -375, -125, 125, 375 };
            var shelfRects = new RectTransform[5]; var shelfItems = new GameObject[5]; var shelfTags = new PixelText[5];
            for (int s = 0; s < 5; s++)
            {
                var slot = Node("Slot" + (s + 1), vOp); At(slot, sx[s], 150, 226, 240); shelfRects[s] = slot;
                Sliced(slot, PanelFill, new Color(0.11f, 0.1f, 0.13f));
                var sb = Node("Border", slot); Stretch(sb); Sliced(sb, PanelBorder, new Color(0.3f, 0.28f, 0.3f));
                Txt(slot, "Num", (s + 1) + "번 칸", 2, Slate, 0, 92, 200, 30, TextAnchor.MiddleCenter);
                shelfItems[s] = Relic(slot, "Item", 0, -18, 196, 140, out shelfTags[s]);
            }
            var pedRects = new RectTransform[4]; var pedItems = new GameObject[4]; var pedTags = new PixelText[4];
            for (int p = 0; p < 4; p++)
            {
                var ped = Node("Ped" + (p + 1), vOp); At(ped, px[p], -140, 234, 200); pedRects[p] = ped;
                Sliced(ped, PanelFill, new Color(0.13f, 0.12f, 0.1f));
                var pb = Node("Border", ped); Stretch(pb); Sliced(pb, PanelBorder, new Color(0.45f, 0.4f, 0.3f));
                Txt(ped, "Num", (p + 1) + "번 받침대", 2, Slate, 0, -76, 220, 30, TextAnchor.MiddleCenter);
                pedItems[p] = Relic(ped, "Item", 0, 18, 190, 120, out pedTags[p]);
            }
            var cur = Node("Cursor", vOp); At(cur, sx[0], 150, 250, 264); Sliced(cur, PanelBorder, Amber);
            PixelText heldTag;
            var held = Relic(vOp, "Held", 0, 0, 180, 130, out heldTag);
            Txt(held.transform, "HeldLabel", "손에 든 유물", 2, Slate, 0, 96, 220, 30, TextAnchor.MiddleCenter);
            held.transform.SetAsLastSibling();
            // 손에 든 유물은 그림자를 둬서 떠 있는 느낌
            var hshadow = Node("Shadow", held.transform); At(hshadow, 6, -6, 180, 130); Img(hshadow, null, new Color(0, 0, 0, 0.35f)); hshadow.SetAsFirstSibling();

            // ---- 통역 면: 번역표
            var vTr = Node("ViewInterpreter", content); Stretch(vTr);
            var page = Node("Page", vTr); At(page, 0, 40, 1200, 480);
            Img(page, null, new Color(0.9f, 0.84f, 0.68f));
            var pageB = Node("PageBorder", page); Stretch(pageB); Sliced(pageB, PanelBorder, new Color(0.45f, 0.33f, 0.2f));
            Txt(page, "Head", "MGA PANAHON NG MGA ARTIPAKTO", 2, new Color(0.45f, 0.33f, 0.2f), 0, 186, 1000, 36, TextAnchor.MiddleCenter);
            var brown = new Color(0.25f, 0.17f, 0.1f); var teal = new Color(0.08f, 0.38f, 0.36f);
            var gtl = new PixelText[4]; var gko = new PixelText[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 125 - i * 90;
                gtl[i] = Txt(page, "Tl" + i, "", 3, brown, -360, y, 420, 50, TextAnchor.MiddleCenter);
                gko[i] = Txt(page, "Ko" + i, "", 2, teal, 200, y, 700, 50, TextAnchor.MiddleCenter);
            }

            // ---- 관측자 면: 자외선등
            var vOb = Node("ViewObserver", content); Stretch(vOb);
            var uvBoxes = new Image[5]; var uvTexts = new PixelText[5]; var uvSubs = new PixelText[5];
            for (int s = 0; s < 5; s++)
            {
                var b = Node("Uv" + (s + 1), vOb); At(b, sx[s], 60, 216, 270);
                uvBoxes[s] = Sliced(b, PanelFill, new Color(0.14f, 0.17f, 0.22f));
                var bb = Node("Border", b); Stretch(bb); Sliced(bb, PanelBorder, new Color(0.45f, 0.35f, 0.65f));
                uvTexts[s] = Txt(b, "Num", "", 3, Ink, 0, 60, 190, 50, TextAnchor.MiddleCenter);
                var gem = Node("Gem", b); At(gem, 0, -10, 70, 70); Img(gem, null, new Color(1, 1, 1, 0.18f));
                uvSubs[s] = Txt(b, "State", "", 3, Ink, 0, -90, 190, 50, TextAnchor.MiddleCenter);
            }

            // ---- 내비게이터 면: 받침대 배치도
            var vNv = Node("ViewNavigator", content); Stretch(vNv);
            var navTexts = new PixelText[4]; var navSubs = new PixelText[4];
            for (int p = 0; p < 4; p++)
            {
                var b = Node("Nav" + (p + 1), vNv); At(b, px[p], 60, 236, 260);
                Sliced(b, PanelFill, new Color(0.1f, 0.14f, 0.16f));
                var bb = Node("Border", b); Stretch(bb); Sliced(bb, PanelBorder, new Color(0.3f, 0.5f, 0.55f));
                navTexts[p] = Txt(b, "Num", "", 2, Amber, 0, 78, 220, 40, TextAnchor.MiddleCenter);
                var pd = Node("Pedestal", b); At(pd, 0, 0, 120, 60); Img(pd, null, new Color(0.35f, 0.38f, 0.42f));
                navSubs[p] = Txt(b, "Order", "", 2, Ink, 0, -82, 224, 40, TextAnchor.MiddleCenter);
            }

            var status = Txt(content, "Status", "", 3, Ink, 0, -318, 1400, 50, TextAnchor.MiddleCenter);
            var hint = Txt(content, "Hint", "", 2, Slate, 0, -382, 1420, 80, TextAnchor.MiddleCenter);
            var flash = Node("Flash", content); Stretch(flash); var flashImg = Img(flash, null, new Color(1, 1, 1, 0));

            panel.group = pg; panel.content = content;
            panel.title = title; panel.roleText = role; panel.hint = hint; panel.status = status; panel.flash = flashImg;
            panel.viewOperator = vOp.gameObject; panel.shelfRects = shelfRects; panel.shelfItems = shelfItems; panel.shelfTags = shelfTags;
            panel.pedRects = pedRects; panel.pedItems = pedItems; panel.pedTags = pedTags; panel.cursor = cur; panel.heldBox = (RectTransform)held.transform; panel.heldTag = heldTag;
            panel.viewInterpreter = vTr.gameObject; panel.glossTl = gtl; panel.glossKo = gko;
            panel.viewObserver = vOb.gameObject; panel.uvBoxes = uvBoxes; panel.uvTexts = uvTexts; panel.uvSubs = uvSubs;
            panel.viewNavigator = vNv.gameObject; panel.navTexts = navTexts; panel.navSubs = navSubs;

            // ---- 월드: 수장고의 유물 전시대
            var go = new GameObject("RelicCase");
            go.transform.position = new Vector3(0f, -36.7f, 0f);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 1;
            var rc = go.AddComponent<RelicCase>(); rc.panel = panel; rc.render = sr; rc.radius = 2.2f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RelicPuzzleBuilder] Story1 에 유물 퍼즐을 만들었습니다");
        }
    }
}
