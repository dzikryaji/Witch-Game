using System;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    /// <summary>
    /// Full-screen blocker that passes raycasts only through the active step's authored mask rects.
    /// Mask visuals live in scene and are toggled by step; this script does not create, resize, move, or recolor them.
    /// </summary>
    public sealed class TutorialMaskView : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private Image blocker;
        [SerializeField] private TutorialStepMask[] stepMasks = Array.Empty<TutorialStepMask>();

        private TutorialStepMask currentMask;

        public bool IsVisible => overlayRoot != null && overlayRoot.activeSelf;

        private void Awake()
        {
            if (overlayRoot == null) overlayRoot = gameObject;
            if (blocker == null) blocker = GetComponent<Image>();
            if (blocker != null)
            {
                blocker.raycastTarget = true;
                blocker.canvasRenderer.cullTransparentMesh = false;
            }
        }

        public void ShowStep(ShopGuidedTutorialStep step)
        {
            currentMask = null;
            if (overlayRoot != null) overlayRoot.SetActive(true);
            if (blocker != null)
            {
                blocker.raycastTarget = true;
                blocker.canvasRenderer.cullTransparentMesh = false;
            }
            if (stepMasks != null)
            {
                for (int i = 0; i < stepMasks.Length; i++)
                {
                    TutorialStepMask mask = stepMasks[i];
                    if (mask == null) continue;
                    bool on = mask.Step == step;
                    if (mask.gameObject.activeSelf != on) mask.gameObject.SetActive(on);
                    if (on) currentMask = mask;
                }
            }
        }

        public void Hide()
        {
            currentMask = null;
            if (stepMasks != null)
            {
                for (int i = 0; i < stepMasks.Length; i++)
                {
                    if (stepMasks[i] != null) stepMasks[i].gameObject.SetActive(false);
                }
            }
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        public bool TryGetMask(ShopGuidedTutorialStep step, out TutorialStepMask mask)
        {
            mask = null;
            if (stepMasks == null) return false;
            for (int i = 0; i < stepMasks.Length; i++)
            {
                if (stepMasks[i] != null && stepMasks[i].Step == step) { mask = stepMasks[i]; return true; }
            }
            return false;
        }

        public TutorialStepMask[] Masks => stepMasks;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!IsVisible) return false;
            if (currentMask == null) return true; // block all if misconfigured
            return !currentMask.Contains(screenPoint, eventCamera);
        }

#if UNITY_EDITOR
        public void EditorSetStepMasks(TutorialStepMask[] masks) { stepMasks = masks ?? Array.Empty<TutorialStepMask>(); }
#endif
    }
}
