using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 방의 문 마커. 플레이어가 서 있어야 할 자리에 놓으면 반경(radius) 안에서 "[E] 라벨" 이 뜨고, E 를 누르면 화면이 검게 페이드된 뒤
    /// destination(상대 문 앞 도착 자리)으로 옮겨지고 카메라가 그 방으로 바뀐다. Emberwrought 의 VillageSpot(문)과 같은 방식.
    /// </summary>
    public sealed class RoomDoor : MonoBehaviour
    {
        [Tooltip("프롬프트에 표시되는 말: [E]  라벨")]
        public string label = "들어가기";
        public float radius = 1.1f;
        [Tooltip("문을 열고 나왔을 때 서는 자리(보통 상대 문의 Arrive 자식). 비어 있으면 이 문은 쓸 수 없다")]
        public Transform destination;

        private void OnDrawGizmos()
        {
            Gizmos.color = destination != null ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(1f, 0.4f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
            if (destination != null) Gizmos.DrawLine(transform.position, destination.position);
        }
    }
}
