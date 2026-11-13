using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 박물관 방 하나(512×384 스프라이트 = 16×12 유닛)의 마커. 이 오브젝트의 위치가 방 중심이고, 플레이어가 이 방에 있는 동안 카메라가 여기에 고정된다.
    /// </summary>
    public sealed class MuseumRoom : MonoBehaviour
    {
        public string roomName;
        public Vector2 size = new Vector2(16f, 12f);
        [Tooltip("이 방에서의 카메라 크기(세로 절반). 작은 통로는 줄여서 확대해 보여 준다")]
        public float cameraSize = 6f;

        public Rect Bounds => new Rect((Vector2)transform.position - size * 0.5f, size);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
