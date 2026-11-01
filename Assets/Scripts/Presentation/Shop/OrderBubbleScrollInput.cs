using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{
    public sealed class OrderBubbleScrollInput : MonoBehaviour, IBeginDragHandler, IPointerDownHandler, IScrollHandler
    {
        [SerializeField] private ShopHudView hudView;

        public void Initialize(ShopHudView view) => hudView = view;

        public void OnBeginDrag(PointerEventData eventData) => StopAutoScroll();
        public void OnPointerDown(PointerEventData eventData) => StopAutoScroll();
        public void OnScroll(PointerEventData eventData) => StopAutoScroll();

        private void StopAutoScroll() => hudView?.StopOrderBubbleAutoScroll();
    }
}
