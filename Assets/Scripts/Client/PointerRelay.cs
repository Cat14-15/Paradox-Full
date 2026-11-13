using UnityEngine;
using UnityEngine.EventSystems;

namespace Paradox.Client
{
    /// <summary>UI 요소 위의 마우스 호버·클릭을 LobbyScene 에 알린다. group 은 요소 종류, id 는 그 안의 번호.</summary>
    public sealed class PointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public LobbyScene owner;
        public int group;
        public int id;
        public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.PointerEnter(group, id); }
        public void OnPointerClick(PointerEventData e) { if (owner != null) owner.PointerClick(group, id); }
    }
}
