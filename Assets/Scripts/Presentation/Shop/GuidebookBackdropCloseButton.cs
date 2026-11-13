using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{
    /// <summary>Closes the guidebook when the backdrop is clicked (routes through presenter when available).</summary>
    public sealed class GuidebookBackdropCloseButton : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            ShopShellPresenter presenter = GetComponentInParent<ShopShellPresenter>();
            if (presenter == null)
            {
                presenter = FindObjectOfType<ShopShellPresenter>();
            }
            if (presenter != null)
            {
                presenter.CloseGuidebook();
                return;
            }
            gameObject.SetActive(false);
        }
    }
}
