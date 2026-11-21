using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Paradox.Editor
{
    /// <summary>
    /// 무료 에셋 팩(LPC 오브젝트, Big32 일부, 고양이)을 스프라이트로 설정하고,
    /// 씬 "AssetCatalog" 에 오브젝트를 카테고리별로 펼쳐 놓는다. 복사(Ctrl+C) → 다른 씬에 붙여넣기(Ctrl+V)로 바로 배치할 수 있다.
    /// 메뉴: Paradox ▸ Build Asset Catalog
    /// </summary>
    public static class AssetCatalogBuilder
    {
        private const string ScenePath = "Assets/Scenes/AssetCatalog.unity";

        private static readonly string[] BigCategories =
        {
            "Doors", "Windows", "Posters", "Carpets", "Classical Decoration", "Random Decoration", "Machinery (Airconditioners)", "Platforms", "Fences", "Other", "Grafitti", "Vegetation"
        };

        [MenuItem("Paradox/Build Asset Catalog")]
        public static void Build()
        {
            if (Application.isPlaying) return;
            ConfigureImporters();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 8; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f); cam.clearFlags = CameraClearFlags.SolidColor; cam.tag = "MainCamera";
            cam.transform.position = new Vector3(20, -10, -10);

            float y = 0f;
            y = AddGroups("Assets/Art/Packs/LPC_Objects", "LPC ", y);
            foreach (var cat in BigCategories) y = AddGroup("Assets/Art/Packs/Big32/Separated Sprites/" + cat, "Big32 " + cat, y);
            y = AddGroup("Assets/Art/Packs/Cats/sprites", "Cats", y);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[AssetCatalog] 만들었어요: " + ScenePath);
        }

        private static float AddGroups(string root, string prefix, float y)
        {
            foreach (var dir in Directory.GetDirectories(root))
                y = AddGroup(dir.Replace('\\', '/'), prefix + Path.GetFileName(dir), y);
            return y;
        }

        private static float AddGroup(string dir, string title, float y)
        {
            if (!Directory.Exists(dir)) return y;
            var files = Directory.GetFiles(dir, "*.png");
            if (files.Length == 0) return y;
            var parent = new GameObject("== " + title + " ==").transform;
            parent.position = new Vector3(0, y, 0);
            float x = 0f, rowH = 0f, startY = y;
            const float MaxW = 44f;
            foreach (var f in files)
            {
                var path = f.Replace('\\', '/');
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) continue;
                float w = sp.rect.width / 32f, h = sp.rect.height / 32f;
                if (x + w > MaxW) { x = 0; y -= rowH + 1.0f; rowH = 0; }
                var go = new GameObject(Path.GetFileNameWithoutExtension(path));
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x, y, 0);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp;
                x += w + 0.6f; rowH = Mathf.Max(rowH, h);
            }
            y -= rowH + 3f;
            return y;
        }

        private static void ConfigureImporters()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Packs/LPC_Objects", "Assets/Art/Packs/Big32", "Assets/Art/Packs/Cats" });
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    bool sheet = path.EndsWith("FullSpritesheet.png") || path.EndsWith("32x32spritesheet-0padding-sample.png");
                    if (sheet) continue;   // 시트는 이미 32 격자로 잘라 둠
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = 32;
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    var ts = new TextureImporterSettings(); imp.ReadTextureSettings(ts);
                    // 오브젝트(LPC_Objects, 고양이)는 바닥 가운데 기준, 타일(Big32)은 가운데 기준(타일맵에서 어긋나지 않게)
                    ts.spriteAlignment = path.Contains("/Big32/") ? (int)SpriteAlignment.Center : (int)SpriteAlignment.BottomCenter;
                    imp.SetTextureSettings(ts);
                    imp.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
        }
    }
}
