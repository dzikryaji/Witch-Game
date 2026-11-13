using System;
using UnityEngine;

namespace Witch.Presentation.Shop
{
    public sealed class TutorialStepMask : MonoBehaviour
    {
        [SerializeField] private ShopGuidedTutorialStep step;
        [SerializeField] private RectTransform[] holeRects = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform[] extraInputRects = Array.Empty<RectTransform>();

        public ShopGuidedTutorialStep Step => step;
        public RectTransform[] HoleRects => holeRects;
        public RectTransform[] ExtraInputRects => extraInputRects;

        public bool Contains(Vector2 screenPoint, Camera eventCamera)
        {
            return HitAny(holeRects, screenPoint, eventCamera) || HitAny(extraInputRects, screenPoint, eventCamera);
        }

        public bool HasHoleRect()
        {
            if (holeRects == null) return false;
            for (int i = 0; i < holeRects.Length; i++)
            {
                if (holeRects[i] != null) return true;
            }
            return false;
        }

        public bool HasDimPanel()
        {
            var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && !images[i].raycastTarget && images[i].gameObject != gameObject) return true;
            }
            return images != null && images.Length > 1; // fallback
        }

        private static bool HitAny(RectTransform[] rects, Vector2 screenPoint, Camera eventCamera)
        {
            if (rects == null) return false;
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rt = rects[i];
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, eventCamera)) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetStep(ShopGuidedTutorialStep value) { step = value; }
        public void EditorSetHoleRects(RectTransform[] value) { holeRects = value ?? Array.Empty<RectTransform>(); }
        public void EditorSetExtraInputRects(RectTransform[] value) { extraInputRects = value ?? Array.Empty<RectTransform>(); }
#endif
    }
}
