using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>
    /// 퍼즐 4(물길 돌리기)를 Story1.unity 에 씬 오브젝트로 만든다. 메뉴: Paradox ▸ Add Water Puzzle To Story1.
    /// 만드는 것: 퍼즐 화면(WaterPanel, UI), 학예실의 배수 밸브 패널(WaterValves, 월드). 다시 실행하면 이 오브젝트들만 새로 만든다.
    /// </summary>
    public static class WaterPuzzleBuilder
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

        [MenuItem("Paradox/Add Water Puzzle To Story1")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var n in new[] { "WaterPanel", "WaterValves" })
                foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (g != null && g.name == n) Object.DestroyImmediate(g.gameObject);

            var canvas = GameObject.Find("UI").GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform;

            var pr = Node("WaterPanel", root); Stretch(pr);
            var pg = pr.gameObject.AddComponent<CanvasGroup>(); pg.alpha = 0f; pg.blocksRaycasts = false; pg.interactable = false;
            var panel = pr.gameObject.AddComponent<WaterPanel>();
            var dim = Node("Dim", pr); Stretch(dim); Img(dim, null, new Color(0, 0, 0, 0.74f)).raycastTarget = true;
            var content = Node("Content", pr); At(content, 0, 60, 1500, 860);   // 오른쪽 아래 기록 창과 겹치지 않게 조금 위로
            var back = Node("Back", content); Stretch(back); Sliced(back, PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.98f));
            var border = Node("Border", content); Stretch(border); Sliced(border, PanelBorder, new Color(0.3f, 0.52f, 0.62f));

            var title = Txt(content, "Title", "", 4, Amber, 0, 380, 1300, 70, TextAnchor.MiddleCenter);
            var role = Txt(content, "Role", "", 2, Ink, 0, 328, 1400, 40, TextAnchor.MiddleCenter);

            // ---- 수위 막대(오른쪽)
            var bar = Node("WaterBar", content); At(bar, 640, 40, 70, BarHeight); Sliced(bar, PanelFill, new Color(0.03f, 0.04f, 0.07f));
            var fill = Node("Fill", bar); At(fill, 0, -BarHeight / 2f, 58, 0); fill.pivot = new Vector2(0.5f, 0f);
            fill.anchoredPosition = new Vector2(0, -BarHeight / 2f + 6f);
            Img(fill, null, new Color(0.3f, 0.6f, 0.95f, 0.9f));
            var barB = Node("BarBorder", bar); Stretch(barB); Sliced(barB, PanelBorder, new Color(0.3f, 0.4f, 0.5f));
            var wlabel = Txt(content, "WaterLabel", "", 2, Ink, 640, -270, 200, 36, TextAnchor.MiddleCenter);

            float[] vx = { -400, -140, 120, 380 };   // 왼쪽 면 영역 (수위 막대 피해서)

            // ---- 조작자 면: 밸브 4개
            var vOp = Node("ViewOperator", content); Stretch(vOp);
            var bodies = new Image[4]; var handles = new Image[4]; var tags = new PixelText[4]; var states = new PixelText[4]; var vrects = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var v = Node("Valve" + (i + 1), vOp); At(v, vx[i], 40, 230, 420); vrects[i] = v;
                bodies[i] = Sliced(v, PanelFill, new Color(0.11f, 0.13f, 0.17f));
                var vb = Node("Border", v); Stretch(vb); Sliced(vb, PanelBorder, new Color(0.34f, 0.4f, 0.46f));
                var pipe = Node("Pipe", v); At(pipe, 0, 95, 26, 210); Img(pipe, null, new Color(0.38f, 0.46f, 0.52f));
                var hub = Node("Hub", v); At(hub, 0, 110, 70, 70); Img(hub, null, new Color(0.2f, 0.22f, 0.26f));
                var hd = Node("Handle", v); At(hd, 0, 110, 150, 22); handles[i] = Img(hd, null, Color.red);
                tags[i] = Txt(v, "Tag", "", 3, Amber, 0, -70, 220, 50, TextAnchor.MiddleCenter);
                states[i] = Txt(v, "State", "", 2, Ink, 0, -150, 220, 40, TextAnchor.MiddleCenter);
            }

            // ---- 통역 면: 번역표
            var vTr = Node("ViewInterpreter", content); Stretch(vTr);
            var page = Node("Page", vTr); At(page, -60, 40, 1000, 470);
            Img(page, null, new Color(0.9f, 0.84f, 0.68f));
            var pageB = Node("PageBorder", page); Stretch(pageB); Sliced(pageB, PanelBorder, new Color(0.45f, 0.33f, 0.2f));
            Txt(page, "Head", "MGA PANGALAN NG BALBULA", 2, new Color(0.45f, 0.33f, 0.2f), 0, 182, 900, 36, TextAnchor.MiddleCenter);
            var brown = new Color(0.25f, 0.17f, 0.1f); var teal = new Color(0.08f, 0.38f, 0.36f);
            var gtl = new PixelText[4]; var gko = new PixelText[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 120 - i * 90;
                gtl[i] = Txt(page, "Tl" + i, "", 3, brown, -220, y, 420, 50, TextAnchor.MiddleCenter);
                gko[i] = Txt(page, "Ko" + i, "", 3, teal, 220, y, 420, 50, TextAnchor.MiddleCenter);
            }

            // ---- 내비게이터 면: 파이프 지도
            var vNv = Node("ViewNavigator", content); Stretch(vNv);
            var line = Node("MainPipe", vNv); At(line, -130, 40, 900, 34); Img(line, null, new Color(0.32f, 0.4f, 0.46f));
            Txt(vNv, "In", "물 ▶", 2, new Color(0.7f, 0.85f, 1f), -640, 40, 120, 36, TextAnchor.MiddleCenter);
            Txt(vNv, "Out", "▶ 배수구", 2, Amber, 410, 40, 200, 36, TextAnchor.MiddleCenter);
            var nodes = new Image[4]; var nlabels = new PixelText[4];
            for (int i = 0; i < 4; i++)
            {
                var nd = Node("Node" + (i + 1), vNv); At(nd, -460 + i * 220, 40, 196, 150);
                nodes[i] = Sliced(nd, PanelFill, Color.gray);
                var nb = Node("Border", nd); Stretch(nb); Sliced(nb, PanelBorder, new Color(0.7f, 0.75f, 0.8f));
                nlabels[i] = Txt(nd, "Label", "", 2, Color.white, 0, 0, 180, 50, TextAnchor.MiddleCenter);
            }

            // ---- 관측자 면: 연동
            var vOb = Node("ViewObserver", content); Stretch(vOb);
            var links = new PixelText[4];
            for (int i = 0; i < 4; i++)
            {
                var row = Node("Row" + i, vOb); At(row, -60, 150 - i * 95, 1000, 80);
                Sliced(row, PanelFill, new Color(0.1f, 0.12f, 0.16f));
                var rb = Node("Border", row); Stretch(rb); Sliced(rb, PanelBorder, new Color(0.3f, 0.34f, 0.4f));
                links[i] = Txt(row, "Text", "", 3, Ink, 0, 0, 960, 50, TextAnchor.MiddleCenter);
            }

            var status = Txt(content, "Status", "", 3, Ink, 0, -318, 1400, 50, TextAnchor.MiddleCenter);
            var hint = Txt(content, "Hint", "", 2, Slate, 0, -382, 1420, 80, TextAnchor.MiddleCenter);
            var flash = Node("Flash", content); Stretch(flash); var flashImg = Img(flash, null, new Color(1, 1, 1, 0));

            panel.group = pg; panel.content = content;
            panel.title = title; panel.roleText = role; panel.hint = hint; panel.status = status; panel.flash = flashImg;
            panel.waterFill = fill; panel.waterLabel = wlabel;
            panel.viewOperator = vOp.gameObject; panel.valveBodies = bodies; panel.valveHandles = handles; panel.valveTags = tags; panel.valveStates = states; panel.valveRects = vrects;
            panel.viewInterpreter = vTr.gameObject; panel.glossTl = gtl; panel.glossKo = gko;
            panel.viewNavigator = vNv.gameObject; panel.mapNodes = nodes; panel.mapLabels = nlabels;
            panel.viewObserver = vOb.gameObject; panel.linkLines = links;

            // ---- 월드: 학예실의 배수 밸브 패널
            var go = new GameObject("WaterValves");
            go.transform.position = new Vector3(72.4f, -16.7f, 0f);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 1;
            var wv = go.AddComponent<WaterValves>(); wv.panel = panel; wv.render = sr; wv.radius = 2.2f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WaterPuzzleBuilder] Story1 에 물길 퍼즐을 만들었습니다");
        }

        private const float BarHeight = 560f;
    }
}
