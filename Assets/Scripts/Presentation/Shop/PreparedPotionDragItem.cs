using UnityEngine;
using UnityEngine.EventSystems;

namespace Witch.Presentation.Shop
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PreparedPotionDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private int homeSiblingIndex;
        private Vector2 homeAnchoredPosition;
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private bool isDragging;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            homeSiblingIndex = transform.GetSiblingIndex();
            homeAnchoredPosition = rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            RestoreHome(false);
        }

        private void OnDisable()
        {
            RestoreHome(false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rootCanvas == null || canvasGroup == null) return;
            RestoreHome();
            isDragging = true;
            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.82f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || rootCanvas == null) return;
            rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            RestoreHome();
        }

        private void RestoreHome(bool restoreSiblingOrder = true)
        {
            if (rectTransform == null || canvasGroup == null) return;
            if (restoreSiblingOrder)
            {
                transform.SetSiblingIndex(homeSiblingIndex);
            }
            rectTransform.anchoredPosition = homeAnchoredPosition;
            ResetCanvasState();
        }

        private void ResetCanvasState()
        {
            if (canvasGroup == null) return;

            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            isDragging = false;
        }
    }
}
