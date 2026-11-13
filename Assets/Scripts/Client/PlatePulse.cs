using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>카펫 위 발판 표시. 4개가 차례로 밝아지다가 가끔 모두 함께 켜진다 — "네 명이 동시에"를 뜻한다.</summary>
    public sealed class PlatePulse : MonoBehaviour
    {
        public int index;
        public Color color = new Color(0.376f, 0.984f, 0.796f);
        private Image _img;
        private void Awake() { _img = GetComponent<Image>(); }
        private void Update()
        {
            float t = Mathf.Repeat(Time.time, 6f);
            float a;
            if (t < 4f) { float k = Mathf.Repeat(t - index, 4f); a = k < 1f ? 0.8f * Mathf.Sin(k * Mathf.PI) : 0f; }
            else { a = 0.9f * Mathf.Sin((t - 4f) / 2f * Mathf.PI); }
            _img.color = new Color(color.r, color.g, color.b, a);
        }
    }
}
