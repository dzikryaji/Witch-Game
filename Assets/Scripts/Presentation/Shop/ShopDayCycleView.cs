using System;
using UnityEngine;

namespace Witch.Presentation.Shop
{
    public sealed class ShopDayCycleView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image sky;
        [SerializeField] private UnityEngine.UI.Image sun;
        [SerializeField] private UnityEngine.UI.Image clouds;
        [SerializeField] private UnityEngine.UI.Image distantLandscape;
        [SerializeField] private UnityEngine.UI.Image midgroundVillage;
        [SerializeField] private UnityEngine.UI.Image rightTree;
        [SerializeField] private UnityEngine.UI.Image foregroundFrame;

        private Vector2 sunStartPosition;
        private Vector2 cloudsStartPosition;
        private bool positionsCached;

        public bool IsAuthored => sky != null
            && sun != null
            && clouds != null
            && distantLandscape != null
            && midgroundVillage != null
            && rightTree != null
            && foregroundFrame != null;

        private void Awake() => CachePositions();

        public void Render(TimeSpan remaining, TimeSpan duration)
        {
            if (!IsAuthored) return;
            if (!positionsCached) CachePositions();

            float progress = CalculateProgress(remaining, duration);
            float sunset = Mathf.SmoothStep(0f, 1f, progress);

            sky.color = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.43f), sunset);
            sun.color = Color.Lerp(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.48f, 0.2f), sunset);
            clouds.color = Color.Lerp(Color.white, new Color(1f, 0.68f, 0.55f), sunset);
            distantLandscape.color = Color.Lerp(Color.white, new Color(0.92f, 0.66f, 0.52f), sunset);
            midgroundVillage.color = Color.Lerp(Color.white, new Color(0.9f, 0.58f, 0.42f), sunset);
            rightTree.color = Color.Lerp(Color.white, new Color(0.72f, 0.48f, 0.38f), sunset);
            foregroundFrame.color = Color.Lerp(Color.white, new Color(0.76f, 0.56f, 0.5f), sunset);

            sun.rectTransform.anchoredPosition = sunStartPosition + new Vector2(-70f, -300f) * progress;
            clouds.rectTransform.anchoredPosition = cloudsStartPosition + Vector2.right * (20f * progress);
        }

        public static float CalculateProgress(TimeSpan remaining, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero) return 0f;
            return Mathf.Clamp01(1f - (float)(remaining.TotalSeconds / duration.TotalSeconds));
        }

        private void CachePositions()
        {
            if (sun == null || clouds == null) return;
            sunStartPosition = sun.rectTransform.anchoredPosition;
            cloudsStartPosition = clouds.rectTransform.anchoredPosition;
            positionsCached = true;
        }
    }
}
