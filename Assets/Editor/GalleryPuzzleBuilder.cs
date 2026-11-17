using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>
    /// 퍼즐 2(젖은 바닥 건너기)를 Story1.unity 에 씬 오브젝트로 만든다. 메뉴: Paradox ▸ Add Gallery Puzzle To Story1.
    /// 만드는 것: 퍼즐 화면(GalleryPanel, UI), 회화 전시실의 끊어진 전선(GalleryFloor, 월드). 다시 실행하면 이 오브젝트들만 새로 만든다.
    /// </summary>
    public static class GalleryPuzzleBuilder
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

        [MenuItem("Paradox/Add Gallery Puzzle To Story1")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var n in new[] { "GalleryPanel", "GalleryFloor" })
                foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (g != null && g.name == n) Object.DestroyImmediate(g.gameObject);

            var canvas = GameObject.Find("UI").GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform;

            var pr = Node("GalleryPanel", root); Stretch(pr);
            var pg = pr.gameObject.AddComponent<CanvasGroup>(); pg.alpha = 0f; pg.blocksRaycasts = false; pg.interactable = false;
            var panel = pr.gameObject.AddComponent<GalleryPanel>();
            var dim = Node("Dim", pr); Stretch(dim); Img(dim, null, new Color(0, 0, 0, 0.78f)).raycastTarget = true;
            var content = Node("Content", pr); At(content, 0, 60, 1500, 860);   // 오른쪽 아래 기록 창과 겹치지 않게 조금 위로
            var back = Node("Back", content); Stretch(back); Sliced(back, PanelFill, new Color(0.05f, 0.06f, 0.09f, 0.98f));
            var border = Node("Border", content); Stretch(border); Sliced(border, PanelBorder, new Color(0.35f, 0.5f, 0.62f));

            var title = Txt(content, "Title", "", 4, Amber, 0, 380, 1300, 70, TextAnchor.MiddleCenter);
            var role = Txt(content, "Role", "", 2, Ink, 0, 328, 1400, 40, TextAnchor.MiddleCenter);

            // ---- 공유 격자: 구간 0 이 아래, 2 가 위(위로 올라가며 건넌다)
            float[] rowY = { -130, 30, 190 }; float[] colX = { -330, 0, 330 };
            var cells = new RectTransform[9]; var cellImgs = new Image[9]; var cellMarks = new PixelText[9];
            for (int s = 0; s < 3; s++)
                for (int l = 0; l < 3; l++)
                {
                    int i = s * 3 + l;
                    var c = Node("Cell_" + s + "_" + l, content); At(c, colX[l], rowY[s], 290, 130); cells[i] = c;
                    cellImgs[i] = Sliced(c, PanelFill, new Color(0.04f, 0.05f, 0.07f));
                    var cb = Node("Border", c); Stretch(cb); Sliced(cb, PanelBorder, new Color(0.22f, 0.27f, 0.33f));
                    cellMarks[i] = Txt(c, "Mark", "", 4, Slate, 0, 0, 280, 80, TextAnchor.MiddleCenter);
                }

            // ---- 조작자 면: 손전등 마커
            var vOp = Node("ViewOperator", content); Stretch(vOp);
            Txt(vOp, "Floor", "▲ 출구 쪽", 2, Slate, 0, 275, 800, 36, TextAnchor.MiddleCenter);
            var mk = Node("Marker", vOp); At(mk, colX[1], rowY[0], 312, 152);
            var mkImg = Sliced(mk, PanelBorder, Amber);
            var man = Node("Man", mk); At(man, 0, 0, 36, 60); Img(man, null, new Color(0.9f, 0.93f, 0.97f));

            // ---- 통역 면: 구간별 안내문 3장
            var vTr = Node("ViewInterpreter", content); Stretch(vTr);
            var stl = new PixelText[3]; var sko = new PixelText[3];
            var brown = new Color(0.25f, 0.17f, 0.1f); var teal = new Color(0.08f, 0.38f, 0.36f);
            for (int s = 0; s < 3; s++)
            {
                float y = rowY[s];
                var pg2 = Node("Sign" + (s + 1), vTr); At(pg2, 0, y, 1100, 130);
                Img(pg2, null, new Color(0.9f, 0.84f, 0.68f));
                var pb = Node("Border", pg2); Stretch(pb); Sliced(pb, PanelBorder, new Color(0.45f, 0.33f, 0.2f));
                Txt(pg2, "No", "구간 " + (s + 1), 2, new Color(0.45f, 0.33f, 0.2f), -460, 0, 160, 36, TextAnchor.MiddleCenter);
                stl[s] = Txt(pg2, "Tl", "", 3, brown, 80, 25, 820, 50, TextAnchor.MiddleCenter);
                sko[s] = Txt(pg2, "Ko", "", 2, teal, 80, -30, 820, 40, TextAnchor.MiddleCenter);
            }

            // ---- 관측자 면: 스파크 신호등 3줄
            var vOb = Node("ViewObserver", content); Stretch(vOb);
            var lamps = new Image[3]; var rows = new Image[3]; var labs = new PixelText[3];
            for (int s = 0; s < 3; s++)
            {
                float y = rowY[s];
                var rw = Node("Row" + (s + 1), vOb); At(rw, 0, y, 900, 130);
                rows[s] = Sliced(rw, PanelFill, new Color(0.1f, 0.12f, 0.16f));
                var rb = Node("Border", rw); Stretch(rb); Sliced(rb, PanelBorder, new Color(0.3f, 0.34f, 0.4f));
                labs[s] = Txt(rw, "Label", "", 3, Ink, -280, 0, 260, 50, TextAnchor.MiddleCenter);
                var lp = Node("Lamp", rw); At(lp, 180, 0, 160, 80); lamps[s] = Img(lp, null, Color.green);
                var lc = Node("Core", lp); At(lc, 0, 0, 60, 30); Img(lc, null, new Color(1, 1, 1, 0.55f));
                Txt(rw, "Cap", "신호등", 2, Slate, 330, 0, 140, 36, TextAnchor.MiddleCenter);
            }

            // ---- 내비게이터 면 (격자 그대로 + 설명)
            var vNv = Node("ViewNavigator", content); Stretch(vNv);
            Txt(vNv, "Floor", "▲ 출구 쪽", 2, Slate, 0, 275, 800, 36, TextAnchor.MiddleCenter);

            var status = Txt(content, "Status", "", 3, Ink, 0, -318, 1400, 50, TextAnchor.MiddleCenter);
            var hint = Txt(content, "Hint", "", 2, Slate, 0, -382, 1420, 80, TextAnchor.MiddleCenter);
            var flash = Node("Flash", content); Stretch(flash); var flashImg = Img(flash, null, new Color(1, 1, 1, 0));

            panel.group = pg; panel.content = content;
            panel.title = title; panel.roleText = role; panel.hint = hint; panel.status = status; panel.flash = flashImg;
            panel.cellRects = cells; panel.cellImages = cellImgs; panel.cellMarks = cellMarks;
            panel.viewOperator = vOp.gameObject; panel.marker = mk; panel.markerImage = mkImg;
            panel.viewInterpreter = vTr.gameObject; panel.signTl = stl; panel.signKo = sko;
            panel.viewObserver = vOb.gameObject; panel.lamps = lamps; panel.lampRows = rows; panel.lampLabels = labs;
            panel.viewNavigator = vNv.gameObject;

            // ---- 월드: 회화 전시실의 끊어진 전선
            var go = new GameObject("GalleryFloor");
            go.transform.position = new Vector3(24f, -2.2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 0;
            var gf = go.AddComponent<GalleryFloor>(); gf.panel = panel; gf.render = sr; gf.radius = 2.2f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GalleryPuzzleBuilder] Story1 에 젖은 바닥 퍼즐을 만들었습니다");
        }
    }
}
