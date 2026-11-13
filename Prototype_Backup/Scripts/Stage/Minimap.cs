using Paradox.Language;
using Paradox.Roles;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Stage
{
    /// <summary>
    /// 내비게이터 전용 미니맵. 내비게이터만 전체 지도(레버 위치·색, 도착존, 내 위치)를 본다.
    /// 다른 역할에겐 보이지 않으므로, 내비게이터가 조작자에게 "어디로/어느 레버"를 안내해야 한다.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        private static readonly Vector2 WorldMin = new Vector2(-8f, -4.5f);
        private static readonly Vector2 WorldMax = new Vector2(8f, 4.5f);
        private static readonly Vector2 FieldSize = new Vector2(200f, 118f);

        private GameObject _panel;
        private RectTransform _field;
        private RectTransform _playerDot;
        private Transform _player;

        private void Start()
        {
            var p = Object.FindFirstObjectByType<Player>();
            if (p != null) _player = p.transform;
            Build();
            RoleService.Changed += UpdateVisibility;
            UpdateVisibility();
        }

        private void OnDestroy() => RoleService.Changed -= UpdateVisibility;

        private void UpdateVisibility()
        {
            if (_panel != null) _panel.SetActive(RoleService.LocalRole == GameRole.Navigator);
        }

        private void Build()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var font = UIFontHelper.Load();

            _panel = new GameObject("Minimap", typeof(RectTransform));
            var rt = (RectTransform)_panel.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16f, 200f);
            rt.sizeDelta = new Vector2(224f, 168f);
            _panel.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.06f, 0.92f);

            var title = new GameObject("Title", typeof(RectTransform));
            var trt = (RectTransform)title.transform; trt.SetParent(rt, false);
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -4f); trt.sizeDelta = new Vector2(0f, 22f);
            var tt = title.AddComponent<Text>();
            tt.font = font; tt.fontSize = 14; tt.alignment = TextAnchor.MiddleCenter;
            tt.color = new Color(0.6f, 0.9f, 1f); tt.text = "미니맵 (내비게이터 전용)";

            _field = new GameObject("Field", typeof(RectTransform)).GetComponent<RectTransform>();
            _field.SetParent(rt, false);
            _field.anchorMin = new Vector2(0.5f, 0.5f); _field.anchorMax = new Vector2(0.5f, 0.5f);
            _field.pivot = new Vector2(0.5f, 0.5f);
            _field.anchoredPosition = new Vector2(0f, -8f);
            _field.sizeDelta = FieldSize;
            _field.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.18f, 0.12f, 1f);

            // 레버: 기호를 지도 위치에 표시 (내비게이터가 "어느 기호가 어디"인지 안내)
            foreach (var lever in Object.FindObjectsByType<SymbolLever>(FindObjectsSortMode.None))
                MakeSymbol(lever.Symbol, ToMap(lever.transform.position));

            var zone = Object.FindFirstObjectByType<ArrivalZone>();
            if (zone != null)
            {
                var zd = MakeDot(new Color(1f, 0.9f, 0.3f), 14f);
                zd.anchoredPosition = ToMap(zone.transform.position);
            }

            _playerDot = MakeDot(Color.white, 9f);
        }

        private RectTransform MakeDot(Color color, float size)
        {
            var go = new GameObject("Dot", typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(_field, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            go.AddComponent<Image>().color = color;
            return rt;
        }

        private void MakeSymbol(string symbol, Vector2 pos)
        {
            var go = new GameObject("Symbol", typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(_field, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(20f, 20f);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = UIFontHelper.Load(); t.text = symbol; t.fontSize = 18; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private Vector2 ToMap(Vector3 world)
        {
            float u = Mathf.InverseLerp(WorldMin.x, WorldMax.x, world.x);
            float v = Mathf.InverseLerp(WorldMin.y, WorldMax.y, world.y);
            return new Vector2((u - 0.5f) * FieldSize.x, (v - 0.5f) * FieldSize.y);
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (_player != null && _playerDot != null)
                _playerDot.anchoredPosition = ToMap(_player.position);
        }
    }
}
