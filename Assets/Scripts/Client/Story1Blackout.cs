using System;
using Paradox.Core;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 정전 상태의 박물관: 화면 전체가 어둡고, 주인공 주변만 손전등 불빛으로 보인다.
    /// 전원이 복구되면(PowerOn) 서서히 걷힌다. UI 오브젝트(Blackout)는 씬에 놓여 있고 이 컴포넌트가 따라다니기만 한다.
    /// </summary>
    public sealed class Story1Blackout : MonoBehaviour
    {
        public static bool PowerOn { get; private set; }
        public static event Action PowerRestored;

        public CanvasGroup group;
        public RectTransform spot;      // 가운데가 뚫린 어둠 그림(손전등)
        public Canvas canvas;
        public Camera cam;
        public Transform player;
        [Range(0f, 1f)] public float darkness = 0.93f;
        public float fadeSpeed = 0.45f;

        private float _a = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { PowerOn = false; PowerRestored = null; }

        internal static void ResetState() { PowerOn = false; }

        public static void RestorePower()
        {
            if (PowerOn) return;
            PowerOn = true;
            PowerRestored?.Invoke();
        }

        private void Start()
        {
            _a = PowerOn ? 0f : 1f;
            Apply();
        }

        private void LateUpdate()
        {
            _a = Mathf.MoveTowards(_a, PowerOn ? 0f : 1f, Time.deltaTime * fadeSpeed);
            Apply();
        }

        private void Apply()
        {
            if (group == null) return;
            group.alpha = _a * darkness;
            if (_a > 0.001f && spot != null && cam != null && player != null && canvas != null)
            {
                Vector2 sp = cam.WorldToScreenPoint(player.position + Vector3.up * 0.3f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, sp, null, out var local);
                spot.anchoredPosition = local;
            }
        }
    }
}
