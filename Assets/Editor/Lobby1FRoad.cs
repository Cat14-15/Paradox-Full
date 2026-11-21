using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Paradox.Editor
{
    /// <summary>
    /// 열려 있는 1층 로비 씬의 "길"을 다듬는다: 어두운 보라 돌길을 밝은 붉은 카펫으로 바꾸고,
    /// 가로 길은 2칸 폭으로 되돌리고 세로 길(중앙 복도)만 4칸으로 둔다. Ctrl+Z 로 되돌릴 수 있다.
    /// 메뉴: Paradox ▸ Lobby 1F 길 다듬기
    /// </summary>
    public static class Lobby1FRoad
    {
        private static Sprite Big(int n)
        {
            foreach (var g in AssetDatabase.FindAssets("tile" + n, new[] { "Assets/Art/Packs/Big32" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == "tile" + n) return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }
            return null;
        }

        [MenuItem("Paradox/Lobby 1F 길 다듬기")]
        public static void Run()
        {
            if (Application.isPlaying) return;
            var carpet = Big(1223);
            if (carpet == null) { Debug.LogWarning("[Lobby1FRoad] Big32 타일 1223 이 없어요"); return; }
            int recolored = 0, removed = 0;
            var scene = EditorSceneManager.GetActiveScene();
            var toDelete = new System.Collections.Generic.List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.sprite == null) continue;
                    string go = sr.gameObject.name;
                    // 가로 길을 넓히려고 복제했던 줄은 지운다(2칸 폭으로 복귀)
                    if (go.EndsWith("_road_up") || go.EndsWith("_road_down")) { toDelete.Add(sr.gameObject); continue; }
                    if (sr.sprite.name.StartsWith("house_4_"))
                    {
                        Undo.RecordObject(sr, "road carpet");
                        sr.sprite = carpet; sr.sortingOrder = 1; recolored++;
                    }
                }
            }
            foreach (var g in toDelete) { Undo.DestroyObjectImmediate(g); removed++; }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Lobby1FRoad] 카펫으로 바꾼 칸 " + recolored + " · 지운 복제 줄 " + removed);
        }
    }
}
