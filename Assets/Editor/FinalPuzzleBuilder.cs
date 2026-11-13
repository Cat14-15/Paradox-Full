using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>
    /// 퍼즐 5(4인 동시 입력)를 Story1.unity 에 씬 오브젝트로 만든다. 메뉴: Paradox ▸ Add Final Puzzle To Story1.
    /// 만드는 것: 퍼즐 화면(FinalPanel, UI), 중앙 홀 바닥의 원(FinalFloor, 월드). 다시 실행하면 이 오브젝트들만 새로 만든다.
    /// </summary>
    public static class FinalPuzzleBuilder
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

        [MenuItem("Paradox/Add Final Puzzle To Story1")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var n in new[] { "FinalPanel", "FinalFloor" })
                foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (g != null && g.name == n) Object.DestroyImmediate(g.gameObject);

            var canvas = GameObject.Find("UI").GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform;

            var pr = Node("FinalPanel", root); Stretch(pr);
            var pg = pr.gameObject.AddComponent<CanvasGroup>(); pg.alpha = 0f; pg.blocksRaycasts = false; pg.interactable = false;
            var panel = pr.gameObject.AddComponent<FinalPanel>();
            var dim = Node("Dim", pr); Stretch(dim); Img(dim, null, new Color(0, 0, 0, 0.8f)).raycastTarget = true;
            var content = Node("Content", pr); At(content, 0, 0, 1500, 860);
            var back = Node("Back", content); Stretch(back); Sliced(back, PanelFill, new Color(0.05f, 0.05f, 0.09f, 0.98f));
            var border = Node("Border", content); Stretch(border); Sliced(border, PanelBorder, new Color(0.65f, 0.55f, 0.32f));

            var title = Txt(content, "Title", "", 4, Amber, 0, 380, 1300, 70, TextAnchor.MiddleCenter);
            var role = Txt(content, "Role", "", 2, Ink, 0, 328, 1400, 40, TextAnchor.MiddleCenter);
            var round = Txt(content, "Round", "", 3, Ink, 0, 268, 1300, 50, TextAnchor.MiddleCenter);

            float[] cx = { -450, -150, 150, 450 };
            var rects = new RectTransform[4]; var rings = new Image[4]; var lamps = new Image[4]; var labels = new PixelText[4];
            for (int i = 0; i < 4; i++)
            {
                var c = Node("Circle" + (i + 1), content); At(c, cx[i], 90, 210, 210); rects[i] = c;
                var ring = Node("Ring", c); Stretch(ring); rings[i] = Img(ring, null, Color.white);
                var inner = Node("Inner", c); At(inner, 0, 0, 176, 176); Img(inner, null, new Color(0.05f, 0.05f, 0.09f));
                var lamp = Node("Lamp", c); At(lamp, 0, 0, 176, 176); lamps[i] = Img(lamp, null, Color.white);
                labels[i] = Txt(content, "Label" + (i + 1), "", 2, Ink, cx[i], -45, 290, 40, TextAnchor.MiddleCenter);
            }

            var ticks = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var t = Node("Tick" + i, content); At(t, -160 + i * 80, -120, 44, 44);
                ticks[i] = Sliced(t, PanelFill, new Color(0.22f, 0.25f, 0.3f));
            }
            var tickLabel = Txt(content, "TickLabel", "", 2, Slate, -380, -120, 200, 36, TextAnchor.MiddleCenter);

            var dots = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var d = Node("Seq" + i, content); At(d, -120 + i * 80, -205, 54, 54);
                dots[i] = Img(d, null, new Color(0.25f, 0.28f, 0.33f));
            }
            var seqLabel = Txt(content, "SeqLabel", "입력한 순서", 2, Slate, -380, -205, 240, 36, TextAnchor.MiddleCenter);

            var status = Txt(content, "Status", "", 4, Ink, 0, -300, 1400, 60, TextAnchor.MiddleCenter);
            var hint = Txt(content, "Hint", "", 2, Slate, 0, -382, 1420, 80, TextAnchor.MiddleCenter);
            var flash = Node("Flash", content); Stretch(flash); var flashImg = Img(flash, null, new Color(1, 1, 1, 0));

            panel.group = pg; panel.content = content;
            panel.title = title; panel.roleText = role; panel.roundText = round; panel.hint = hint; panel.status = status; panel.flash = flashImg;
            panel.circleRects = rects; panel.circleRings = rings; panel.circleLamps = lamps; panel.circleLabels = labels;
            panel.ticks = ticks; panel.tickLabel = tickLabel; panel.seqDots = dots; panel.seqLabel = seqLabel;

            // ---- 월드: 중앙 홀 바닥의 원
            var go = new GameObject("FinalFloor");
            go.transform.position = new Vector3(0f, -2.6f, 0f);
            go.transform.localScale = new Vector3(1.45f, 1.55f, 1f);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 0;
            var ff = go.AddComponent<FinalFloor>(); ff.panel = panel; ff.glow = sr; ff.radius = 2.0f / 1.45f;
            go.transform.position = new Vector3(0f, -2.4f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FinalPuzzleBuilder] Story1 에 최종 퍼즐을 만들었습니다");
        }
    }
}
