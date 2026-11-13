using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Core
{
    /// <summary>
    /// 화면 전체 색 번쩍임(손맛)과 카메라 흔들림. 성공=초록, 실패=빨강 등.
    /// 처음 호출될 때 스스로 생성된다(씬 배치 불필요). 다른 UI 뒤에서 번쩍여 글자를 가리지 않는다.
    /// </summary>
    public class FeedbackFX : MonoBehaviour
    {
        public static FeedbackFX Instance { get; private set; }

        private Image _img;
        private Color _color;
        private float _alpha;
        private float _shake;

        private void Awake()
        {
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Flash(Color color, float strength)
        {
            Ensure();
            Instance._color = color;
            Instance._alpha = Mathf.Max(Instance._alpha, strength);
        }

        public static void Shake(float amount)
        {
            Ensure();
            Instance._shake = Mathf.Max(Instance._shake, amount);
        }

        private static void Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("FeedbackFX");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<FeedbackFX>();
            }
        }

        private void Build()
        {
            var canvas = UIRoot.Canvas;
            var go = new GameObject("FlashOverlay", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _img = go.AddComponent<Image>();
            _img.raycastTarget = false;
            _img.color = new Color(0, 0, 0, 0);
            rt.SetAsFirstSibling();
        }

        private void Update()
        {
            if (_img == null) return;
            if (_alpha > 0f)
            {
                _alpha = Mathf.Max(0f, _alpha - Time.deltaTime * 2.4f);
                _img.color = new Color(_color.r, _color.g, _color.b, _alpha);
            }
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (_shake > 0f)
            {
                _shake = Mathf.Max(0f, _shake - Time.deltaTime * 1.5f);
                if (follow != null) follow.shakeOffset = Random.insideUnitCircle * _shake * 0.35f;
            }
            else if (follow != null && follow.shakeOffset != Vector2.zero)
            {
                follow.shakeOffset = Vector2.zero;
            }
        }
    }
}
