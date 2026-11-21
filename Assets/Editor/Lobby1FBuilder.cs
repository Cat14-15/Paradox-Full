using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Paradox.Editor
{
    /// <summary>
    /// "New Scene" 에 1층 로비(중앙 복도)를 무료 에셋(LPC 오브젝트 + Big32 타일)으로 배치한다.
    /// 전부 평범한 SpriteRenderer 오브젝트라서 열어서 직접 옮기고 바꿀 수 있다. 메뉴: Paradox ▸ Build Lobby 1F
    /// 단위: 1유닛 = 32px, 방 중심이 (0,0), 방 크기 16×12.
    /// </summary>
    public static class Lobby1FBuilder
    {
        private const string ScenePath = "Assets/Scenes/New Scene.unity";
        private const string Obj = "Assets/Art/Packs/LPC_Objects/";
        private static Transform _root;

        private static Sprite Load(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogWarning("[Lobby1F] 없음: " + path);
            return s;
        }
        private static Sprite Lpc(string sheet, string name) { return Load(Obj + sheet + "/" + name + ".png"); }
        private static Sprite Big(int n)
        {
            foreach (var g in AssetDatabase.FindAssets("tile" + n, new[] { "Assets/Art/Packs/Big32" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == "tile" + n) return Load(p);
            }
            Debug.LogWarning("[Lobby1F] Big32 타일 없음: " + n); return null;
        }

        private static GameObject Place(string name, Sprite s, float x, float y, int order, Transform parent, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = new Vector3(x, y, 0);
            go.transform.localScale = new Vector3(scale, scale, 1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order;
            return go;
        }

        private static Transform Group(string name)
        {
            var t = new GameObject(name).transform; t.SetParent(_root, false); return t;
        }

        [MenuItem("Paradox/Build Lobby 1F")]
        public static void Build()
        {
            if (Application.isPlaying) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var old = GameObject.Find("Lobby1F"); if (old != null) Object.DestroyImmediate(old);
            _root = new GameObject("Lobby1F").transform;

            // ---- 카메라 + 전체 조명(URP 2D)
            var camGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGo != null) { var c = camGo.GetComponent<Camera>(); c.orthographic = true; c.orthographicSize = 6f; camGo.transform.position = new Vector3(0, 0, -10); c.backgroundColor = new Color(0.07f, 0.07f, 0.1f); }
            if (Object.FindFirstObjectByType<Light2D>() == null)
            {
                var lg = new GameObject("Global Light 2D").AddComponent<Light2D>();
                lg.lightType = Light2D.LightType.Global; lg.intensity = 0.85f; lg.color = new Color(0.95f, 0.9f, 0.85f);
            }

            // ---- 바닥: 밝은 타일 체크
            var floor = Group("Floor");
            var t1 = Big(1017); var t2 = Big(938);
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 14; c++)
                    Place("floor_" + c + "_" + r, (r + c) % 2 == 0 ? t1 : t2, -6.5f + c, 2.25f - r, 0, floor);

            // ---- 붉은 러너 카펫 + 나침반
            var carpet = Group("Carpet");
            var c1 = Big(1223); var c2 = Big(1224);
            for (int r = 0; r < 8; r++) { Place("carpet_l_" + r, c1, -0.5f, 2.25f - r, 1, carpet); Place("carpet_r_" + r, c2, 0.5f, 2.25f - r, 1, carpet); }
            Place("Compass", Load("Assets/Art/Museum2/Compass.png"), 0, -1.2f, 2, _root);

            // ---- 위쪽 벽 (LPC 청록 벽 3칸 폭 블록 6개)
            var wallG = Group("Walls");
            var wall = Lpc("inside", "inside_00");
            for (int i = 0; i < 6; i++) Place("wall_top_" + i, wall, -6.5f + i * 3, 2.72f, 5, wallG);
            // 양옆 벽
            var side = Lpc("inside", "inside_09");
            for (int i = 0; i < 5; i++) { Place("wall_L_" + i, side, -7.5f, 2.75f - (i + 1) * 1.94f, 5, wallG); Place("wall_R_" + i, side, 7.5f, 2.75f - (i + 1) * 1.94f, 5, wallG); }
            // 아래 벽
            var bw = Big(365);
            for (int c = 0; c < 16; c++) Place("wall_bottom_" + c, bw, -7.5f + c, -5.5f, 5, wallG);

            // ---- 위층 계단 아치 + 기둥 + 횃불
            Place("Arch_2F", Lpc("castlewalls", "castlewalls_stone_arch"), 0, 2.0f, 6, _root);
            var pil = Lpc("inside", "inside_02");
            foreach (var x in new[] { -7.25f, -1.75f, 1.75f, 7.25f }) Place("Pillar", pil, x, 1.8f, 7, _root);
            var torch = Lpc("castle_lightsources", "castle_lightsources_01");
            foreach (var x in new[] { -2.5f, 2.5f })
            {
                Place("Torch", torch, x, 3.0f, 8, _root);
                var lt = new GameObject("TorchLight").AddComponent<Light2D>();
                lt.transform.SetParent(_root, false); lt.transform.localPosition = new Vector3(x, 4.0f, 0);
                lt.lightType = Light2D.LightType.Point; lt.color = new Color(1f, 0.72f, 0.35f); lt.intensity = 1.1f; lt.pointLightOuterRadius = 4.5f;
            }

            // ---- 벽 앞 가구
            Place("Cabinet_L", Lpc("cabinets", "cabinets_06"), -4.5f, 2.8f, 8, _root);
            Place("Bookshelf_R", Lpc("cabinets", "cabinets_00"), 4.5f, 2.8f, 8, _root);

            // ---- 양옆 출입구 문 (위 2 + 아래 2)
            var doors = Group("Doors_Side");
            var d1 = Big(466); var d2 = Big(467);
            foreach (var y in new[] { 0.9f, -2.1f })
            {
                Place("door_L_a", d1, -7.5f, y, 9, doors); Place("door_L_b", d2, -7.5f, y + 1, 9, doors);
                Place("door_R_a", d1, 7.5f, y, 9, doors); Place("door_R_b", d2, 7.5f, y + 1, 9, doors);
            }

            // ---- 지하 계단 입구(오른쪽 위) + 화분
            Place("Stairs_B1", Lpc("cementstair", "cementstair_00"), 5.6f, 0.4f, 6, _root, 0.45f);
            var pl = Load("Assets/Resources/MuseumOverlays/00_Central_Hall__PlantsLeft.png");
            var pr = Load("Assets/Resources/MuseumOverlays/00_Central_Hall__PlantsRight.png");
            if (pl != null) Place("Plant_L", pl, 0, 0, 10, _root);
            if (pr != null) Place("Plant_R", pr, 0, 0, 10, _root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Lobby1F] 1층 로비 배치 완료 — " + ScenePath);
        }
    }
}
