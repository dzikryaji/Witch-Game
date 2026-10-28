using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    public sealed class HerbDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ISubmitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        private Transform originalParent;
        private int originalSiblingIndex;
        private Vector2 originalAnchoredPosition;
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private UnityAction boundSubmitAction;

        public string HerbId { get; private set; }

        public void Initialize(string herbId) => HerbId = herbId;

        public void BindSubmit(UnityAction action) => boundSubmitAction = action;

        public void UnbindSubmit() => boundSubmitAction = null;

        public void OnSubmit(BaseEventData eventData)
        {
            if (button != null && button.interactable && !string.IsNullOrWhiteSpace(HerbId))
            {
                boundSubmitAction?.Invoke();
            }
        }

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rootCanvas == null || canvasGroup == null || button == null || !button.interactable || string.IsNullOrWhiteSpace(HerbId)) return;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();
            originalAnchoredPosition = rectTransform.anchoredPosition;
            transform.SetParent(rootCanvas.transform, true);
            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.82f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (originalParent == null || rootCanvas == null) return;
            rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (originalParent == null) return;
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.anchoredPosition = originalAnchoredPosition;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            originalParent = null;
        }

        private void OnDestroy() => UnbindSubmit();
    }
}
