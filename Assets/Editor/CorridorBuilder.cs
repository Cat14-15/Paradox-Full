using System.Collections.Generic;
using System.IO;
using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Paradox.Editor
{
    /// <summary>
    /// 방 그림의 좌우 출입구는 그림 끝에서 뚝 끊겨 벽에 붙은 짧은 돌출부처럼 보인다.
    /// 그림 가장자리에서 열린 부분(바닥색)을 찾아, 그 바깥으로 어둠 속으로 사라지는 복도를 씬에 직접 이어 붙인다.
    /// 다시 실행해도 이전 결과를 지우고 새로 만든다. 메뉴: Paradox/Story1/복도 이어 붙이기
    /// </summary>
    public static class CorridorBuilder
    {
        private const string FadePath = "Assets/Art/Museum/corridor_fade.png";
        private const string Tag = "Corridor_";
        private const float Length = 4f;        // 복도 길이(유닛) — 카메라가 보는 검은 여백보다 길게

        [MenuItem("Paradox/Story1/복도 이어 붙이기")]
        public static void Build()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/Story1.unity");
            var fade = EnsureFadeSprite();
            var scene = Object.FindFirstObjectByType<Story1Scene>();
            int made = 0;
            foreach (var room in scene.rooms)
            {
                // 이전 결과 정리
                for (int i = room.transform.childCount - 1; i >= 0; i--)
                    if (room.transform.GetChild(i).name.StartsWith(Tag)) Object.DestroyImmediate(room.transform.GetChild(i).gameObject);

                var bg = BackgroundOf(room);
                if (bg == null || bg.sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(bg.sprite.texture);
                if (string.IsNullOrEmpty(path)) continue;
                var tex = new Texture2D(2, 2);
                ImageConversion.LoadImage(tex, File.ReadAllBytes(path));
                var b = bg.bounds;   // 월드 크기
                for (int side = 0; side < 2; side++)
                {
                    int x = side == 0 ? 0 : tex.width - 1;
                    foreach (var run in OpenRuns(tex, x, out var floor))
                    {
                        float y0 = b.min.y + b.size.y * run.x / tex.height, y1 = b.min.y + b.size.y * (run.y + 1) / tex.height;
                        var go = new GameObject(Tag + (side == 0 ? "L" : "R") + (++made));
                        go.transform.SetParent(room.transform, true);
                        float edgeX = side == 0 ? b.min.x : b.max.x;
                        go.transform.position = new Vector3(edgeX, (y0 + y1) * 0.5f, 0f);
                        go.transform.localScale = new Vector3(side == 0 ? -Length : Length, (y1 - y0) * 8f, 1f);   // 스프라이트 높이가 0.125 유닛(8px/64)   // 왼쪽은 좌우 반전(그림 끝에서 바깥으로)
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = fade;
                        sr.color = floor;
                        sr.sortingLayerID = bg.sortingLayerID;
                        sr.sortingOrder = bg.sortingOrder;
                    }
                }
                Object.DestroyImmediate(tex);
            }
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            Debug.Log("[CorridorBuilder] 복도 " + made + "개 이어 붙임");
        }

        private static SpriteRenderer BackgroundOf(MuseumRoom room)
        {
            foreach (var sr in room.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.sprite != null && sr.sprite.rect.width >= 256f) return sr;
            return null;
        }

        // 가장자리 한 줄에서 밝은(바닥색) 연속 구간들. (y 시작, y 끝) — 텍스처 픽셀 기준
        private static List<Vector2Int> OpenRuns(Texture2D tex, int x, out Color floor)
        {
            var runs = new List<Vector2Int>();
            floor = Color.gray;
            int start = -1;
            Color sum = Color.black; int n = 0;
            for (int y = 0; y <= tex.height; y++)
            {
                bool open = false;
                if (y < tex.height) { var c = tex.GetPixel(x, y); open = (c.r + c.g + c.b) / 3f > 0.4f; if (open) { sum += c; n++; } }
                if (open && start < 0) start = y;
                else if (!open && start >= 0)
                {
                    if (y - start >= tex.height / 16) runs.Add(new Vector2Int(start, y - 1));   // 너무 얇은 조각은 무시
                    start = -1;
                }
            }
            if (n > 0) floor = sum / n;
            floor.a = 1f;
            return runs;
        }

        private static Sprite EnsureFadeSprite()
        {
            if (!File.Exists(FadePath))
            {
                const int W = 64, H = 8;
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                for (int x = 0; x < W; x++)
                {
                    float k = 1f - x / (float)(W - 1);          // 바깥으로 갈수록 어두워진다
                    float a = Mathf.Pow(k, 1.4f) * 0.9f;
                    for (int y = 0; y < H; y++)
                    {
                        float edge = (y == 0 || y == H - 1) ? 0.55f : 1f;   // 위아래 가장자리는 조금 어둡게(벽 그림자)
                        t.SetPixel(x, y, new Color(edge, edge, edge, a));
                    }
                }
                t.Apply();
                File.WriteAllBytes(FadePath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(FadePath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(FadePath);
                imp.textureType = TextureImporterType.Sprite;
                imp.spritePixelsPerUnit = 64f;            // 가로 1유닛
                imp.spritePivot = new Vector2(0f, 0.5f);  // 왼쪽 끝이 기준(그림 가장자리)
                imp.alphaIsTransparency = true;
                imp.filterMode = FilterMode.Bilinear;
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s); s.spriteAlignment = (int)SpriteAlignment.Custom; imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(FadePath);
        }
    }
}
