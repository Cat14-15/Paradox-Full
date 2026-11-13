using UnityEngine;

namespace Paradox.Core
{
    /// <summary>
    /// 내가 조종하는 캐릭터를 따라가는 카메라. 맵 경계 안에서만 움직이고, 흔들림 연출을 받는다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float pixelsPerUnit = 0f;
        public Vector3 offset = new Vector3(0f, 0f, -10f);
        public float smooth = 12f;
        public bool useBounds;
        public Rect bounds;

        [System.NonSerialized] public Vector2 shakeOffset;

        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            var goal = ClampToBounds(target.position + offset);
            goal.z = offset.z;
            transform.position = goal;
        }

        private void LateUpdate()
        {
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
