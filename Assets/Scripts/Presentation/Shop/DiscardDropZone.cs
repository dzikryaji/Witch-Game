using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{
    public sealed class DiscardDropZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] private ShopShellPresenter presenter;

        public void Initialize(ShopShellPresenter value) => presenter = value;

        public void OnDrop(PointerEventData eventData)
        {
            if (presenter == null || eventData.pointerDrag == null) return;
            if (eventData.pointerDrag.GetComponent<GroundHerbDragItem>() != null)
            {
                presenter.DiscardGroundHerb();
            }
            else if (eventData.pointerDrag.GetComponent<PreparedPotionDragItem>() != null)
            {
                presenter.Discard();
            }
        }
    }
}
