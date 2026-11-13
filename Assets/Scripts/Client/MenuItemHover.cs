using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>메뉴 항목 위에 마우스를 올리면 그 항목을 선택하고, 클릭하면 실행한다.</summary>
    public sealed class MenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public IntroSequence owner;
        public int index;
        public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.Select(index); }
        public void OnPointerClick(PointerEventData e) { if (owner != null) owner.Activate(index); }
    }
}
