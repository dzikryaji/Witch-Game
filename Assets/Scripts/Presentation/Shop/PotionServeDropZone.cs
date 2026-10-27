using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{
    public sealed class PotionServeDropZone : MonoBehaviour, IDropHandler
    {
        [SerializeField] private ShopShellPresenter presenter;

        public void Initialize(ShopShellPresenter value) => presenter = value;

        public void OnDrop(PointerEventData eventData)
        {
            if (presenter == null || eventData.pointerDrag == null) return;
            if (eventData.pointerDrag.GetComponent<PreparedPotionDragItem>() != null)
            {
                presenter.RequestServe();
            }
        }
    }
}
