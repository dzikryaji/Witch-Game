using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{
    public sealed class GrinderDropZone : MonoBehaviour, IDropHandler
    {
        private ShopShellPresenter presenter;

        public void Initialize(ShopShellPresenter value) => presenter = value;

        public void OnDrop(PointerEventData eventData)
        {
            HerbDragItem herb = eventData.pointerDrag == null
                ? null
                : eventData.pointerDrag.GetComponent<HerbDragItem>();
            if (herb != null)
            {
                presenter.LoadHerbIntoGrinder(herb.HerbId);
            }
        }
    }
}
