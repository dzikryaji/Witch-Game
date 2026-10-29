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
        private Canvas rootCanvas;
        private RectTransform dragVisual;
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
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rootCanvas == null || canvasGroup == null || button == null || !button.interactable || string.IsNullOrWhiteSpace(HerbId)) return;
            DestroyDragVisual();
            GameObject copy = Instantiate(gameObject, rootCanvas.transform, true);
            copy.name = gameObject.name + " Drag Visual";
            copy.GetComponent<HerbDragItem>().enabled = false;
            copy.GetComponent<Button>().enabled = false;
            CanvasGroup copyCanvasGroup = copy.GetComponent<CanvasGroup>();
            copyCanvasGroup.blocksRaycasts = false;
            copyCanvasGroup.alpha = 0.82f;
            dragVisual = (RectTransform)copy.transform;
            dragVisual.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragVisual == null || rootCanvas == null) return;
            dragVisual.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DestroyDragVisual();
        }

        private void OnDisable() => DestroyDragVisual();

        private void OnDestroy()
        {
            DestroyDragVisual();
            UnbindSubmit();
        }

        private void DestroyDragVisual()
        {
            if (dragVisual == null) return;
            dragVisual.gameObject.SetActive(false);
            Destroy(dragVisual.gameObject);
            dragVisual = null;
        }
    }
}
