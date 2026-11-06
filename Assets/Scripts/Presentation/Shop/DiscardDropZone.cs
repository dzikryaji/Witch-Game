using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    public sealed class DiscardDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private ShopShellPresenter presenter;

        [Header("Drop hint")]
        [Tooltip("The bin image to darken. Leave empty to use this object's own Image.")]
        [SerializeField] private Graphic highlightTarget;
        [SerializeField] private Color dropHintColor = new Color(0.784f, 0.706f, 0.588f, 1f); // 200, 180, 150

        private Color restingColor;
        private bool highlighted;

        public void Initialize(ShopShellPresenter value) => presenter = value;

        // Only reacts while a ground herb or a potion is being dragged over the bin, not to a plain cursor.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.dragging && IsDiscardable(eventData.pointerDrag)) SetHighlight(true);
        }

        public void OnPointerExit(PointerEventData eventData) => SetHighlight(false);

        public void OnDrop(PointerEventData eventData)
        {
            SetHighlight(false);
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

        private void OnDisable() => SetHighlight(false);

        private static bool IsDiscardable(GameObject dragged) =>
            dragged != null
            && (dragged.GetComponent<GroundHerbDragItem>() != null
                || dragged.GetComponent<PreparedPotionDragItem>() != null);

        private void SetHighlight(bool on)
        {
            Graphic target = highlightTarget != null ? highlightTarget : GetComponent<Graphic>();
            if (target == null || on == highlighted) return;
            if (on)
            {
                restingColor = target.color;
                target.color = dropHintColor;
            }
            else
            {
                target.color = restingColor;
            }
            highlighted = on;
        }
    }
}
