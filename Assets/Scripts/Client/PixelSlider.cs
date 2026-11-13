using UnityEngine;
using UnityEngine.EventSystems;

namespace Paradox.Client
{
    /// <summary>슬라이더 막대를 누르거나 끌면 값을 바꾼다(0~1).</summary>
    public sealed class PixelSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public RectTransform track, fill, handle;
        public float value = 0.5f;
        public System.Action<float> changed;

        public void OnPointerDown(PointerEventData e) { Set(e); }
        public void OnDrag(PointerEventData e) { Set(e); }

        private void Set(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var p)) return;
            float v = Mathf.Clamp01((p.x - track.rect.xMin) / track.rect.width);
            v = Mathf.Round(v * 20f) / 20f;   // 5% 단위
            Apply(v, true);
        }

        public void Apply(float v, bool notify)
        {
            value = Mathf.Clamp01(v);
            if (fill != null) { fill.anchorMax = new Vector2(value, 1f); }
            if (handle != null) { handle.anchorMin = handle.anchorMax = new Vector2(value, 0.5f); }
            if (notify && changed != null) changed(value);
        }
    }
}
