using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Domain.Potions;

namespace Witch.Presentation.Shop
{
    /// <summary>Authored Shop HUD. Owns visual rendering; contains no game-flow decisions.</summary>
    public sealed class ShopHudView : MonoBehaviour
    {
        [SerializeField] private Text dayText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text suspicionText;
        [SerializeField] private UnityEngine.UI.Image suspicionFill;
        [SerializeField] private Text potText;
        [SerializeField] private Text customerText;
        [SerializeField] private GameObject orderBubblePrefab;
        [SerializeField] private RectTransform orderBubbleContent;
        [SerializeField] private UnityEngine.UI.ScrollRect orderBubbleScrollRect;
        [SerializeField] private RectTransform customerPortrait;
        [SerializeField] private CanvasGroup customerPortraitCanvasGroup;
        [SerializeField] private ShopDayCycleView dayCycleView;

        private Coroutine entranceRoutine;
        private readonly List<RectTransform> orderBubbles = new List<RectTransform>();
        private Vector2 portraitRestPosition;
        private bool playerScrolledOrders;

        private const float AnimationDistance = 260f;
        private const float AnimationDuration = 0.28f;
        private const float BubbleDelay = 0.12f;

        public bool IsAuthored => dayText != null
            && timerText != null
            && moneyText != null
            && suspicionText != null
            && suspicionFill != null
            && potText != null
            && customerText != null
            && orderBubblePrefab != null
            && orderBubbleContent != null
            && orderBubbleScrollRect != null
            && customerPortrait != null
            && customerPortraitCanvasGroup != null
            && dayCycleView != null
            && dayCycleView.IsAuthored;

        private void Awake()
        {
            if (customerPortrait != null)
            {
                portraitRestPosition = customerPortrait.anchoredPosition;
            }
        }

        public void SetCustomer(string customerName, IReadOnlyList<string> orderBubbles)
        {
            StopEntrance();
            ClearOrderBubbles();
            orderBubbleContent.anchorMin = new Vector2(orderBubbleContent.anchorMin.x, 0f);
            orderBubbleContent.anchorMax = new Vector2(orderBubbleContent.anchorMax.x, 0f);
            orderBubbleContent.pivot = new Vector2(orderBubbleContent.pivot.x, 0f);
            orderBubbleContent.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.LowerRight;
            customerPortrait.gameObject.SetActive(true);
            customerText.text = customerName ?? string.Empty;
            playerScrolledOrders = false;
            SetScrollPosition(1f);

            foreach (string bubbleContent in orderBubbles ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(bubbleContent)) continue;
                GameObject bubble = Instantiate(orderBubblePrefab, orderBubbleContent);
                bubble.name = "Order Bubble " + (this.orderBubbles.Count + 1);
                Text bubbleText = bubble.GetComponentInChildren<Text>(true);
                bubbleText.text = bubbleContent;
                RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
                RectTransform textRect = bubbleText.rectTransform;
                UnityEngine.UI.LayoutElement layout = bubble.GetComponent<UnityEngine.UI.LayoutElement>();
                float textWidthRatio = Mathf.Max(0.01f, textRect.anchorMax.x - textRect.anchorMin.x);
                layout.preferredWidth = Mathf.Min(
                    orderBubbleContent.rect.width,
                    (bubbleText.preferredWidth - textRect.sizeDelta.x) / textWidthRatio);
                bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, layout.preferredWidth);
                float textHeightRatio = Mathf.Max(0.01f, textRect.anchorMax.y - textRect.anchorMin.y);
                layout.preferredHeight = Mathf.Max(
                    layout.minHeight,
                    (bubbleText.preferredHeight - textRect.sizeDelta.y) / textHeightRatio);
                this.orderBubbles.Add(bubbleRect);
                bubble.SetActive(false);
            }

            entranceRoutine = StartCoroutine(AnimateCustomerEntrance());
        }

        public void ClearCustomer()
        {
            StopEntrance();
            customerText.text = string.Empty;
            ClearOrderBubbles();
            customerPortrait.gameObject.SetActive(false);
            customerPortrait.anchoredPosition = portraitRestPosition;
            customerPortraitCanvasGroup.alpha = 1f;
        }

        public async Task PlayCustomerExitAsync(CancellationToken cancellationToken)
        {
            if (customerPortrait == null || customerPortraitCanvasGroup == null) return;
            StopEntrance();

            float startedAt = Time.unscaledTime;
            Vector2 portraitStartPosition = customerPortrait.anchoredPosition;
            float portraitStartAlpha = customerPortraitCanvasGroup.alpha;
            var bubbleStartAlphas = new float[orderBubbles.Count];
            for (int index = 0; index < orderBubbles.Count; index++)
            {
                CanvasGroup group = orderBubbles[index].GetComponent<CanvasGroup>();
                bubbleStartAlphas[index] = group.alpha;
            }

            while (Time.unscaledTime - startedAt < AnimationDuration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / AnimationDuration);
                customerPortrait.anchoredPosition = Vector2.Lerp(
                    portraitStartPosition,
                    portraitRestPosition + Vector2.right * AnimationDistance,
                    progress);
                customerPortraitCanvasGroup.alpha = portraitStartAlpha * (1f - progress);
                for (int index = 0; index < orderBubbles.Count; index++)
                {
                    orderBubbles[index].GetComponent<CanvasGroup>().alpha = bubbleStartAlphas[index] * (1f - progress);
                }
                await Task.Yield();
            }

            customerPortrait.anchoredPosition = portraitRestPosition + Vector2.right * AnimationDistance;
            customerPortraitCanvasGroup.alpha = 0f;
            customerPortrait.gameObject.SetActive(false);
            ClearOrderBubbles();
        }

        public void Render(GameSession session, Func<string, string> displayName)
        {
            if (session == null) return;

            TimeSpan remaining = session.TimerState.RemainingTime;
            dayCycleView?.Render(remaining, session.DayStartSnapshot.TimerDuration);
            dayText.text = "Day " + session.RunState.CurrentDayNumber + "/14";
            timerText.text = "Time  " + ((int)remaining.TotalMinutes).ToString("00") + ":" + remaining.Seconds.ToString("00");
            moneyText.text = "Coins: $" + session.RunState.Money;
            suspicionText.text = "Suspicion  " + session.RunState.Suspicion + "/3";
            Vector2 fillMax = suspicionFill.rectTransform.anchorMax;
            fillMax.x = Mathf.Clamp01(session.RunState.Suspicion / 3f);
            suspicionFill.rectTransform.anchorMax = fillMax;

            var ingredientNames = new List<string>();
            foreach (PotIngredient ingredient in session.PotionDraft.Ingredients)
            {
                ingredientNames.Add(displayName(ingredient.HerbId));
            }

            potText.text = ingredientNames.Count == 0 ? "Cauldron empty" : string.Join(" + ", ingredientNames);
        }

        private IEnumerator AnimateCustomerEntrance()
        {
            if (customerPortrait == null || customerPortraitCanvasGroup == null) yield break;
            yield return AnimateEntry(customerPortrait, customerPortraitCanvasGroup, false);

            for (int bubbleIndex = 0; bubbleIndex < orderBubbles.Count; bubbleIndex++)
            {
                RectTransform bubble = orderBubbles[bubbleIndex];
                var previousPositions = new Vector3[bubbleIndex];
                for (int index = 0; index < bubbleIndex; index++)
                {
                    previousPositions[index] = orderBubbles[index].position;
                }

                bubble.GetComponent<CanvasGroup>().alpha = 0f;
                bubble.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(orderBubbleContent);
                if (!playerScrolledOrders) SetScrollPosition(0f);

                if (bubbleIndex > 0)
                {
                    var layoutPositions = new Vector3[bubbleIndex];
                    for (int index = 0; index < bubbleIndex; index++)
                    {
                        layoutPositions[index] = orderBubbles[index].position;
                        orderBubbles[index].position = previousPositions[index];
                    }

                    float shiftStartedAt = Time.unscaledTime;
                    while (Time.unscaledTime - shiftStartedAt < AnimationDuration)
                    {
                        float progress = Mathf.Clamp01((Time.unscaledTime - shiftStartedAt) / AnimationDuration);
                        float eased = 1f - Mathf.Pow(1f - progress, 3f);
                        for (int index = 0; index < bubbleIndex; index++)
                        {
                            orderBubbles[index].position = Vector3.Lerp(previousPositions[index], layoutPositions[index], eased);
                        }
                        yield return null;
                    }

                    for (int index = 0; index < bubbleIndex; index++)
                    {
                        orderBubbles[index].position = layoutPositions[index];
                    }
                }

                yield return AnimateEntry(bubble, bubble.GetComponent<CanvasGroup>(), true);
                yield return new WaitForSecondsRealtime(BubbleDelay);
            }

            entranceRoutine = null;
        }

        private IEnumerator AnimateEntry(RectTransform target, CanvasGroup group, bool scrollToNewest)
        {
            Vector2 restPosition = target == customerPortrait ? portraitRestPosition : target.anchoredPosition;
            target.anchoredPosition = restPosition + Vector2.right * AnimationDistance;
            group.alpha = 0f;
            float scrollStart = orderBubbleScrollRect.verticalNormalizedPosition;
            float startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < AnimationDuration)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / AnimationDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                target.anchoredPosition = restPosition + Vector2.right * Mathf.Lerp(AnimationDistance, 0f, eased);
                group.alpha = eased;
                if (scrollToNewest && !playerScrolledOrders)
                {
                    SetScrollPosition(Mathf.Lerp(scrollStart, 0f, eased));
                }
                yield return null;
            }

            target.anchoredPosition = restPosition;
            group.alpha = 1f;
        }

        private void SetScrollPosition(float value)
        {
            orderBubbleScrollRect.verticalNormalizedPosition = value;
        }

        public void StopOrderBubbleAutoScroll() => playerScrolledOrders = true;

        private void StopEntrance()
        {
            if (entranceRoutine == null) return;
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        private void ClearOrderBubbles()
        {
            foreach (RectTransform bubble in orderBubbles)
            {
                if (bubble == null) continue;
                bubble.gameObject.SetActive(false);
                Destroy(bubble.gameObject);
            }
            orderBubbles.Clear();
        }
    }
}
