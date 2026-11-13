using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Core
{
    /// <summary>
    /// 화면 전체 색 번쩍임(손맛), 카메라 흔들림, 위험 구역 붉은 비네트.
    /// 처음 호출될 때 스스로 생성된다(씬 배치 불필요). 다른 UI 뒤에서 그려져 글자를 가리지 않는다.
    /// </summary>
    public class FeedbackFX : MonoBehaviour
    {
        public static FeedbackFX Instance { get; private set; }

        private Image _img;
        private Image _vig;
        private Color _color;
        private float _alpha;
        private float _shake;
        private bool _vigOn;
        private float _vigAlpha;

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

        /// <summary>
        /// 위험 구역(침수율 70% 이상인 방)에 내가 있을 때 화면 가장자리에 붉은 비네트를 띄운다.
        /// 매 프레임 현재 상태로 불러도 되고(StageWorld.Tick), 화면을 떠날 때 false로 끈다.
        /// </summary>
        public static void Vignette(bool on)
        {
            if (!on && Instance == null) return;
            Ensure();
            Instance._vigOn = on;
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

            var vgo = new GameObject("DangerVignette", typeof(RectTransform));
            var vrt = (RectTransform)vgo.transform;
            vrt.SetParent(canvas, false);
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            _vig = vgo.AddComponent<Image>();
            _vig.sprite = SpriteLib.Vignette(0.45f);
            _vig.type = Image.Type.Simple;
            _vig.preserveAspect = false;
            _vig.raycastTarget = false;
            _vig.color = new Color(0.85f, 0.12f, 0.1f, 0f);
            vrt.SetSiblingIndex(1);
        }

        private void Update()
        {
            if (_img == null) return;
            if (_alpha > 0f)
            {
                _alpha = Mathf.Max(0f, _alpha - Time.deltaTime * 2.4f);
                _img.color = new Color(_color.r, _color.g, _color.b, _alpha);
            }

            // 위험 비네트: 켜지면 천천히 올라오며 맥동, 꺼지면 사라진다
            if (_vig != null)
            {
                float want = _vigOn ? 0.62f + 0.14f * Mathf.Sin(Time.time * 4.5f) : 0f;
                _vigAlpha = Mathf.MoveTowards(_vigAlpha, want, Time.deltaTime * (_vigOn ? 1.8f : 2.6f));
                var c = _vig.color; c.a = _vigAlpha; _vig.color = c;
                _vig.enabled = _vigAlpha > 0.002f;
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
