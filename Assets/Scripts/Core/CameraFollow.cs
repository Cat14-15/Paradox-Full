using UnityEngine;

namespace Paradox.Core
{
    /// <summary>
    /// 카메라 제어. 기본은 내가 조종하는 캐릭터를 따라가는 모드(맵 경계 안에서만),
    /// 스테이지에서는 fixedMode로 한 층 전체가 보이는 고정 카메라가 된다(위치·크기 고정, 흔들림만 받는다).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float pixelsPerUnit = 0f;
        public Vector3 offset = new Vector3(0f, 0f, -10f);
        public float smooth = 12f;
        public bool useBounds;
        public Rect bounds;

        /// <summary>고정 카메라 모드. StageWorld.Build에서 켜고 Dispose에서 끈다.</summary>
        public bool fixedMode;
        public Vector3 fixedPosition = new Vector3(1.2f, -0.6f, -10f);
        public float fixedSize = 10.0f;

        [System.NonSerialized] public Vector2 shakeOffset;

        private Camera _cam;
        private float _savedSize = -1f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        /// <summary>고정 모드를 켠다. 이전 카메라 크기를 기억해 두었다가 ClearFixed에서 되돌린다.</summary>
        public void SetFixed(Vector3 position, float orthographicSize)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (!fixedMode && _cam != null) _savedSize = _cam.orthographicSize;
            fixedMode = true;
            fixedPosition = position;
            fixedSize = orthographicSize;
            ApplyFixed();
        }

        /// <summary>고정 모드를 끄고 따라가기 모드로 돌아간다.</summary>
        public void ClearFixed()
        {
            if (!fixedMode) return;
            fixedMode = false;
            if (_cam != null && _savedSize > 0f) _cam.orthographicSize = _savedSize;
            _savedSize = -1f;
            SnapToTarget();
        }

        private void ApplyFixed()
        {
            if (_cam != null) _cam.orthographicSize = fixedSize;
            var p = fixedPosition;
            p.x += shakeOffset.x; p.y += shakeOffset.y;
            transform.position = p;
        }

        public void SnapToTarget()
        {
            if (fixedMode) { ApplyFixed(); return; }
            if (target == null) return;
            var goal = ClampToBounds(target.position + offset);
            goal.z = offset.z;
            transform.position = goal;
        }

        private void LateUpdate()
        {
            if (fixedMode) { ApplyFixed(); return; }
            if (target == null) return;

            Vector3 goal = ClampToBounds(target.position + offset);
            Vector3 cur = transform.position;
            Vector3 next = smooth > 0f ? Vector3.Lerp(cur, goal, 1f - Mathf.Exp(-smooth * Time.deltaTime)) : goal;

            if (pixelsPerUnit > 0f)
            {
                next.x = Mathf.Round(next.x * pixelsPerUnit) / pixelsPerUnit;
                next.y = Mathf.Round(next.y * pixelsPerUnit) / pixelsPerUnit;
            }
            next.x += shakeOffset.x;
            next.y += shakeOffset.y;
            next.z = offset.z;
            transform.position = next;
        }

        private Vector3 ClampToBounds(Vector3 p)
        {
            if (!useBounds || _cam == null) return p;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            float minX = bounds.xMin + halfW, maxX = bounds.xMax - halfW;
            float minY = bounds.yMin + halfH, maxY = bounds.yMax - halfH;
            p.x = minX > maxX ? bounds.center.x : Mathf.Clamp(p.x, minX, maxX);
            p.y = minY > maxY ? bounds.center.y : Mathf.Clamp(p.y, minY, maxY);
            return p;
        }
    }
}
