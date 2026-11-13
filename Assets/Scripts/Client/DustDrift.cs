using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>화면 위로 천천히 떠오르며 깜빡이는 먼지 한 알.</summary>
    public sealed class DustDrift : MonoBehaviour
    {
        public float speed = 12f, sway = 10f, phase, alpha = 0.4f;
        private RectTransform _rt; private Image _img; private float _x0;
        private void Awake() { _rt = (RectTransform)transform; _img = GetComponent<Image>(); _x0 = _rt.anchoredPosition.x; }
        private void Update()
        {
            var p = _rt.anchoredPosition;
            p.y += speed * Time.deltaTime;
            if (p.y > 580f) p.y = -580f;
            p.x = _x0 + Mathf.Sin(Time.time * 0.5f + phase) * sway;
            _rt.anchoredPosition = p;
            var c = _img.color; c.a = alpha * (0.55f + 0.45f * Mathf.Sin(Time.time * 1.3f + phase * 3f)); _img.color = c;
        }
    }
}
