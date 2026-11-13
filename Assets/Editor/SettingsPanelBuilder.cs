using Paradox.Client;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Editor
{
    /// <summary>소리 설정 창 + 오른쪽 위 '설정' 버튼을 캔버스에 올린다. 메뉴: Paradox ▸ Add Settings To Scenes (Intro·StageSelect·Lobby).</summary>
    public static class SettingsPanelBuilder
    {
        private static readonly Color Ink = new Color(0.9f, 0.95f, 0.96f);
        private static readonly Color Amber = new Color(0.949f, 0.773f, 0.447f);

        private static RectTransform Node(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            return r;
        }
        private static RectTransform At(RectTransform r, float x, float y, float w, float h) { r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); return r; }
        private static RectTransform Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r; }
        private static Image Img(RectTransform r, Sprite s, Color c, bool ray = false)
        {
            var i = r.gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = ray; return i;
        }
        private static PixelText Txt(Transform parent, string name, string text, int scale, Color color, float x, float y, float w, float h, TextAnchor a)
        {
            var r = Node(name, parent); At(r, x, y, w, h);
            var t = r.gameObject.AddComponent<PixelText>();
            t.scale = scale; t.align = a; t.color = color; t.text = text; t.raycastTarget = false;
            return t;
        }
        private static Sprite Fill => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_fill.png");
        private static Sprite Border => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_border.png");

        private static void Frame(RectTransform box, Color fill, Color border, bool ray)
        {
            var f = Img(box, Fill, fill, ray); f.type = Image.Type.Sliced; f.pixelsPerUnitMultiplier = 1f / 3f;
            var bd = Node("Border", box); Stretch(bd);
            var bi = Img(bd, Border, border); bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 1f / 3f;
        }

        private static PixelSlider Slider(Transform parent, string name, float y, out Image handleImg)
        {
            var root = Node(name, parent); At(root, 70, y, 460, 60);
            var track = Node("Track", root); At(track, 0, 0, 460, 16);
            Img(track, null, new Color(0.17f, 0.2f, 0.25f), true);
            var fill = Node("Fill", track); fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0.5f, 1); fill.pivot = new Vector2(0, 0.5f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            Img(fill, null, Amber);
            var handle = Node("Handle", track); handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f); handle.sizeDelta = new Vector2(20, 44); handle.anchoredPosition = Vector2.zero;
            handleImg = Img(handle, null, Ink);
            // 막대 주변을 넓게 눌러도 되도록 투명 판정 영역
            var hit = Node("Hit", track); Stretch(hit); hit.offsetMin = new Vector2(-14, -22); hit.offsetMax = new Vector2(14, 22);
            Img(hit, null, new Color(0, 0, 0, 0), true);
            var sl = hit.gameObject.AddComponent<PixelSlider>();
            sl.track = track; sl.fill = fill; sl.handle = handle;
            return sl;
        }

        public static void Add(RectTransform canvas)
        {
            // 이미 있으면 지우고 다시 만든다
            foreach (var old in new[] { "SettingsPanel", "SettingsButton" })
            { var t = canvas.Find(old); if (t != null) Object.DestroyImmediate(t.gameObject); }

            var root = Node("SettingsPanel", canvas); Stretch(root);
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            var panel = root.gameObject.AddComponent<SettingsPanel>();
            panel.group = cg;

            var dim = Node("Dim", root); Stretch(dim); Img(dim, null, new Color(0.01f, 0.012f, 0.02f, 0.82f), true);
            var back = Node("BoxBack", root); At(back, 0, 0, 994, 654); Img(back, null, new Color(0.05f, 0.06f, 0.09f, 1f), true);
            var box = Node("Box", root); At(box, 0, 0, 1000, 660);
            Frame(box, new Color(0.05f, 0.06f, 0.09f, 0.98f), new Color(0.62f, 0.5f, 0.3f), true);

            Txt(root, "Title", "설정", 4, Amber, 0, 240, 600, 70, TextAnchor.MiddleCenter);

            panel.sfxLabel = Txt(root, "SfxLabel", "효과음", 3, Ink, -290, 95, 260, 56, TextAnchor.MiddleLeft);
            panel.sfxSlider = Slider(root, "SfxSlider", 95, out panel.sfxHandle);
            panel.sfxPct = Txt(root, "SfxPct", "50%", 3, Amber, 360, 95, 140, 56, TextAnchor.MiddleRight);

            panel.bgmLabel = Txt(root, "BgmLabel", "배경음악", 3, Ink, -290, -25, 260, 56, TextAnchor.MiddleLeft);
            panel.bgmSlider = Slider(root, "BgmSlider", -25, out panel.bgmHandle);
            panel.bgmPct = Txt(root, "BgmPct", "50%", 3, Amber, 360, -25, 140, 56, TextAnchor.MiddleRight);

            Txt(root, "Hint", "↑ ↓  고르기      ← →  조절      Esc  닫기", 2, new Color(0.55f, 0.6f, 0.66f), 0, -150, 800, 36, TextAnchor.MiddleCenter);

            var close = Node("Close", root); At(close, 0, -240, 260, 64);
            Frame(close, new Color(0.08f, 0.1f, 0.14f, 1f), new Color(0.62f, 0.5f, 0.3f), true);
            var cb = close.gameObject.AddComponent<Button>(); cb.targetGraphic = close.GetComponent<Image>();
            Txt(close, "Label", "닫기", 3, Ink, 0, 0, 240, 56, TextAnchor.MiddleCenter);
            UnityEventTools.AddVoidPersistentListener(cb.onClick, panel.Close);

            // 오른쪽 위 설정 버튼(패널보다 뒤에 있도록 패널 앞에 배치)
            var btn = Node("SettingsButton", canvas); At(btn, 0, 0, 200, 76);
            btn.anchorMin = btn.anchorMax = btn.pivot = new Vector2(1, 1); btn.anchoredPosition = new Vector2(-36, -32);
            Frame(btn, new Color(0.05f, 0.06f, 0.09f, 0.75f), new Color(0.45f, 0.4f, 0.3f), true);
            panel.buttonGroup = btn.gameObject.AddComponent<CanvasGroup>();
            var bb = btn.gameObject.AddComponent<Button>(); bb.targetGraphic = btn.GetComponent<Image>();
            Txt(btn, "Label", "설정", 3, Ink, 0, -2, 160, 56, TextAnchor.MiddleCenter);
            UnityEventTools.AddVoidPersistentListener(bb.onClick, panel.Open);
            // 버튼은 화면 전환(검은 막)·연결 중 어두운 막보다 뒤에 둔다 → 그런 때는 가려져서 안 보인다
            int at = root.GetSiblingIndex();
            foreach (Transform ch in canvas)
            {
                string n = ch.name.ToLowerInvariant();
                if (ch != btn && ch != root && (n.Contains("black") || n.Contains("busy") || n.Contains("fade") || n.Contains("overlay"))) at = Mathf.Min(at, ch.GetSiblingIndex());
            }
            btn.SetSiblingIndex(at);
            root.SetAsLastSibling();                       // 패널이 맨 위
        }

        [MenuItem("Paradox/Add Settings To Scenes")]
        public static void AddToScenes()
        {
            if (Application.isPlaying) return;
            foreach (var p in new[] { "Assets/Scenes/Intro.unity", "Assets/Scenes/StageSelect.unity", "Assets/Scenes/Lobby.unity" })
            {
                var sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                var cv = Object.FindFirstObjectByType<Canvas>();
                if (cv == null) { Debug.LogWarning("캔버스 없음: " + p); continue; }
                Add(cv.GetComponent<RectTransform>());
                EditorSceneManager.SaveScene(sc);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Intro.unity", OpenSceneMode.Single);
            Debug.Log("[SettingsPanelBuilder] Intro / StageSelect / Lobby 에 설정 창 추가 완료");
        }
    }
}
