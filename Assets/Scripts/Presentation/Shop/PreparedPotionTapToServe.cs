using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{
    public sealed class PreparedPotionTapToServe : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private ShopShellPresenter presenter;

        public bool IsLocked { get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (presenter == null) return;   
            if (IsLocked) return;            
            if (eventData.dragging) return;  
            
            presenter.RequestServe();
        }
    }
}