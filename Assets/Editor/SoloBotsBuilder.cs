using Paradox.Client;
using Paradox.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>혼자 테스트용 AI 동료 말풍선 띠를 Story1.unity 에 만든다. 메뉴: Paradox ▸ Add Solo Bots To Story1.</summary>
    public static class SoloBotsBuilder
    {
        private const string ScenePath = "Assets/Scenes/Story1.unity";
        private static Sprite PanelFill => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_fill.png");
        private static Sprite PanelBorder => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_border.png");

        private static Image Sliced(RectTransform r, Sprite s, Color c)
        {
            var i = Img(r, s, c); i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f / 3f; return i;
        }

        [MenuItem("Paradox/Add Solo Bots To Story1")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (g != null && g.name == "SoloBots") Object.DestroyImmediate(g.gameObject);

            var root = (RectTransform)GameObject.Find("UI").GetComponent<Canvas>().transform;
            var r = Node("SoloBots", root);
            At(r, 0, -488, 1500, 88);
            var cg = r.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
            var bots = r.gameObject.AddComponent<SoloBots>();

            var back = Node("Back", r); Stretch(back); Sliced(back, PanelFill, new Color(0.05f, 0.07f, 0.1f, 0.98f));
            var border = Node("Border", r); Stretch(border); Sliced(border, PanelBorder, new Color(0.3f, 0.52f, 0.62f));

            var rows = new GameObject[3]; var names = new PixelText[3]; var lines = new PixelText[3];
            for (int i = 0; i < 3; i++)
            {
                var row = Node("Row" + (i + 1), r); At(row, 0, 26 - i * 26, 1500, 26);
                rows[i] = row.gameObject;
                names[i] = Txt(row, "Name", "", 1, Color.white, -625, 0, 180, 24, TextAnchor.MiddleLeft);
                lines[i] = Txt(row, "Line", "", 1, new Color(0.92f, 0.95f, 0.96f), 95, 0, 1260, 24, TextAnchor.MiddleLeft);
            }
            bots.group = cg; bots.rows = rows; bots.names = names; bots.lines = lines;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SoloBotsBuilder] Story1 에 AI 동료 말풍선을 만들었습니다");
        }
    }
}
