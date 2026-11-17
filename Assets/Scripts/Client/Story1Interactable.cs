using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>Story1 에서 [E] 로 상호작용하는 월드 오브젝트(퓨즈함·밸브 등)의 공통 부모. Story1Scene 이 가장 가까운 것을 골라 프롬프트를 띄운다.</summary>
    public abstract class Story1Interactable : MonoBehaviour
    {
        public static readonly List<Story1Interactable> All = new List<Story1Interactable>();

        public float radius = 1.7f;
        /// <summary>프롬프트에 띄울 글(예: "퓨즈함 열기"). 비어 있으면 지금은 상호작용 불가.</summary>
        public abstract string Label { get; }
        public abstract void Activate();

        // 멀리서도 보이게: 상호작용할 수 있는 동안 머리 위에서 위아래로 움직이는 "▼" 표시
        private Transform _marker;
        private SpriteRenderer _markerSr;
        private float _markerBaseY;
        private static Sprite _markerSprite;

        private void LateUpdate()
        {
            if (_marker == null)
            {
                var go = new GameObject("Marker");
                _marker = go.transform; _marker.SetParent(transform, false);
                _markerSr = go.AddComponent<SpriteRenderer>();
                _markerSr.sprite = MarkerSprite(); _markerSr.sortingOrder = 600;
                float top = transform.position.y + 0.8f;
                foreach (var r in GetComponentsInChildren<SpriteRenderer>()) if (r != _markerSr && r.sprite != null) top = Mathf.Max(top, r.bounds.max.y + 0.45f);
                _markerBaseY = top;
            }
            bool show = !string.IsNullOrEmpty(Label) && !Story1Scene.InputLocked;
            if (_markerSr.enabled != show) _markerSr.enabled = show;
            if (show) _marker.position = new Vector3(transform.position.x, _markerBaseY + Mathf.Sin(Time.time * 4f) * 0.12f, 0f);
        }

        private static Sprite MarkerSprite()
        {
            if (_markerSprite != null) return _markerSprite;
            const int W = 9, H = 7;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            Color ol = new Color(0.1f, 0.08f, 0.05f), fill = new Color(0.98f, 0.8f, 0.3f);
            for (int row = 0; row < 4; row++)   // 아래로 뾰족한 삼각형 + 테두리
            {
                int half = row;
                for (int x = 4 - half - 1; x <= 4 + half + 1; x++) if (x >= 0 && x < W) px[(row + 1) * W + x] = ol;
                for (int x = 4 - half; x <= 4 + half; x++) px[(row + 1) * W + x] = fill;
            }
            px[0 * W + 4] = ol;
            t.SetPixels(px); t.Apply();
            return _markerSprite = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 12f);
        }

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.8f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
