using UnityEngine;
using UnityEngine.EventSystems;

namespace Paradox.Client
{
    /// <summary>스테이지 카드 위에 마우스를 올리면 선택하고, 클릭하면 시작(잠겨 있으면 안내).</summary>
    public sealed class StageCardPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public StageSelectScene owner;
        public int index;
        public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.Select(index); }
        public void OnPointerClick(PointerEventData e) { if (owner != null) owner.Activate(index); }
    }
}
