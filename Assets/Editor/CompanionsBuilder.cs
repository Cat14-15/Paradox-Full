using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Paradox.Editor
{
    /// <summary>혼자 테스트용 AI 동료(월드 캐릭터)를 Story1.unity 에 놓는다. 메뉴: Paradox ▸ Add Solo Companions To Story1.</summary>
    public static class CompanionsBuilder
    {
        private const string ScenePath = "Assets/Scenes/Story1.unity";

        [MenuItem("Paradox/Add Solo Companions To Story1")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var c in Object.FindObjectsByType<Story1Companions>(FindObjectsInactive.Include)) Object.DestroyImmediate(c.gameObject);

            var go = new GameObject("Companions");
            var comp = go.AddComponent<Story1Companions>();
            comp.scene = Object.FindFirstObjectByType<Story1Scene>();
            comp.sheets = new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/korean.png"),     // 조작자(한국)
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/phil.png"),       // 통역(필리핀)
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/vietnam.png"),    // 내비게이터(베트남)
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/southafrica.png") // 관측자(남아공)
            };
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CompanionsBuilder] Story1 에 AI 동료 캐릭터를 놓았습니다");
        }
    }
}
