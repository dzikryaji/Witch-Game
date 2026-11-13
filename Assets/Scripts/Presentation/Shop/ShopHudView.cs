using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private UnityEngine.UI.Image customerPortraitImage;
        [SerializeField] private CanvasGroup customerPortraitCanvasGroup;
        [SerializeField] private ShopDayCycleView dayCycleView;

        [Header("Dialogue typing")]
        [SerializeField] private float charactersPerSecond = 40f;
        [SerializeField] private float bubblePause = 0.8f;
        [SerializeField] private float growDuration = 0.5f;

        /// <summary>One order bubble plus the layout slot that owns its space in the list.</summary>
        private sealed class BubbleSlot
        {
            public RectTransform Slot;
            public UnityEngine.UI.LayoutElement SlotLayout;
            public RectTransform Bubble;
            public CanvasGroup Group;
            public Text Label;
            public string FullText;
            public float FullSlotHeight;
        }

        private Coroutine entranceRoutine;
        private TaskCompletionSource<bool> entranceComplete = new TaskCompletionSource<bool>();
        private readonly List<BubbleSlot> bubbleSlots = new List<BubbleSlot>();
        private VerticalLayoutGroup orderLayoutGroup;
        private float bubbleSpacing;
        private Vector2 portraitRestPosition;
        private Sprite placeholderCustomerSprite;
        private bool playerScrolledOrders;
        private bool skipTyping;

        private const float AnimationDistance = 260f;
        private const float AnimationDuration = 0.5f;
        private const string HiddenTextOpenTag = "<color=#00000000>";
        private const string HiddenTextCloseTag = "</color>";

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

       public Task<bool> EntranceComplete => entranceComplete.Task;

        private void Awake()
        {
            if (customerPortrait != null)
            {
                portraitRestPosition = customerPortrait.anchoredPosition;
                if (customerPortraitImage == null)
                {
                    customerPortraitImage = customerPortrait.GetComponent<UnityEngine.UI.Image>();
                }
            }

            if (customerPortraitImage != null) placeholderCustomerSprite = customerPortraitImage.sprite;

            if (orderBubbleContent != null)
            {
                orderLayoutGroup = orderBubbleContent.GetComponent<VerticalLayoutGroup>();
                if (orderLayoutGroup != null)
                {
                    // Spacing moves into each slot so a new slot never makes the older bubbles jump.
                    bubbleSpacing = orderLayoutGroup.spacing;
                    orderLayoutGroup.spacing = 0f;
                }
            }
        }

        /// <summary>Call this from a click/tap to finish the line that is currently being typed.</summary>
        public void SkipTyping() => skipTyping = true;

        public void SetCustomer(string customerName, IReadOnlyList<string> orderBubbles)
        {
            SetCustomer(customerName, orderBubbles, null);
        }

        public void SetCustomer(
            string customerName,
            IReadOnlyList<string> orderBubbles,
            Sprite customerIllustration)
        {
            StopEntrance();
            ResetEntranceSignal();
            ClearOrderBubbles();
            orderBubbleContent.anchorMin = new Vector2(orderBubbleContent.anchorMin.x, 0f);
            orderBubbleContent.anchorMax = new Vector2(orderBubbleContent.anchorMax.x, 0f);
            orderBubbleContent.pivot = new Vector2(orderBubbleContent.pivot.x, 0f);
            orderLayoutGroup.childAlignment = TextAnchor.LowerRight;
            customerPortrait.gameObject.SetActive(true);
            if (customerPortraitImage != null)
            {
                customerPortraitImage.sprite = customerIllustration != null
                    ? customerIllustration
                    : placeholderCustomerSprite;
            }
            customerText.text = customerName ?? string.Empty;
            playerScrolledOrders = false;
            SnapToNewest();
            RectTransform canvasRoot = orderBubbleScrollRect.GetComponentInParent<Canvas>()
                .rootCanvas.transform as RectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRoot);
            Canvas.ForceUpdateCanvases();

            foreach (string bubbleContent in orderBubbles ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(bubbleContent)) continue;
                bubbleSlots.Add(CreateBubbleSlot(bubbleContent));
            }

            entranceRoutine = StartCoroutine(AnimateCustomerEntrance());
        }

        public void ClearCustomer()
        {
            StopEntrance();
            entranceComplete.TrySetResult(false);
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
            entranceComplete.TrySetResult(false);

            // If the exit starts mid-sentence, show the whole line while it fades out.
            foreach (BubbleSlot bubble in bubbleSlots)
            {
                bubble.Label.text = bubble.FullText;
            }

            float startedAt = Time.unscaledTime;
            Vector2 portraitStartPosition = customerPortrait.anchoredPosition;
            float portraitStartAlpha = customerPortraitCanvasGroup.alpha;
            var bubbleStartAlphas = new float[bubbleSlots.Count];
            for (int index = 0; index < bubbleSlots.Count; index++)
            {
                bubbleStartAlphas[index] = bubbleSlots[index].Group.alpha;
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
                for (int index = 0; index < bubbleSlots.Count; index++)
                {
                    bubbleSlots[index].Group.alpha = bubbleStartAlphas[index] * (1f - progress);
                }
                await Task.Yield();
            }

            customerPortrait.anchoredPosition = portraitRestPosition + Vector2.right * AnimationDistance;
            customerPortraitCanvasGroup.alpha = 0f;
            customerPortrait.gameObject.SetActive(false);
            ClearOrderBubbles();
        }

        /// <param name="pendingCoins">Coins still lying on the counter; shown in the header only once collected.</param>
        public void Render(GameSession session, Func<string, string> displayName, int pendingCoins = 0)
        {
            if (session == null) return;

            TimeSpan remaining = session.TimerState.RemainingTime;
            dayCycleView?.Render(remaining, session.DayStartSnapshot.TimerDuration);
            dayText.text = "Day " + session.RunState.CurrentDayNumber + "/14";
            timerText.text = "Time  " + ((int)remaining.TotalMinutes).ToString("00") + ":" + remaining.Seconds.ToString("00");
            moneyText.text = ": $" + Mathf.Max(0, session.RunState.Money - pendingCoins);
            suspicionText.text = session.RunState.Suspicion + "/3";
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

        // ---------------------------------------------------------------------
        // Bubble creation
        // ---------------------------------------------------------------------

        /// <summary>
        /// Each bubble lives inside a "slot". The layout group only ever controls the slot,
        /// and animations only ever touch the bubble inside it, so the two never fight.
        /// </summary>
        private BubbleSlot CreateBubbleSlot(string content)
        {
            int number = bubbleSlots.Count + 1;
            var slotObject = new GameObject(
                "Order Slot " + number,
                typeof(RectTransform),
                typeof(UnityEngine.UI.LayoutElement));
            slotObject.layer = orderBubbleContent.gameObject.layer;
            RectTransform slotRect = (RectTransform)slotObject.transform;
            slotRect.SetParent(orderBubbleContent, false);
            UnityEngine.UI.LayoutElement slotLayout = slotObject.GetComponent<UnityEngine.UI.LayoutElement>();

            GameObject bubble = Instantiate(orderBubblePrefab, slotRect);
            bubble.name = "Order Bubble " + number;
            RectTransform bubbleRect = (RectTransform)bubble.transform;
            // Stretch horizontally, pin to the bottom of the slot, fixed height set below.
            bubbleRect.anchorMin = Vector2.zero;
            bubbleRect.anchorMax = new Vector2(1f, 0f);
            bubbleRect.pivot = new Vector2(0.5f, 0f);
            bubbleRect.sizeDelta = Vector2.zero;
            bubbleRect.anchoredPosition = Vector2.zero;

            CanvasGroup group = bubble.GetComponent<CanvasGroup>();
            if (group == null) group = bubble.AddComponent<CanvasGroup>();

            // Measure with the FULL text so the bubble never resizes while it is being typed.
            Text label = bubble.GetComponentInChildren<Text>(true);
            label.text = content;
            Canvas.ForceUpdateCanvases();
            RectTransform textRect = label.rectTransform;
            UnityEngine.UI.LayoutElement prefabLayout = bubble.GetComponent<UnityEngine.UI.LayoutElement>();
            float minHeight = prefabLayout != null ? Mathf.Max(0f, prefabLayout.minHeight) : 0f;

            float textWidthRatio = Mathf.Max(0.01f, textRect.anchorMax.x - textRect.anchorMin.x);
            TextGenerator textGenerator = label.cachedTextGeneratorForLayout;
            TextGenerationSettings widthSettings = label.GetGenerationSettings(new Vector2(10000f, 0f));
            widthSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float preferredTextWidth = textGenerator.GetPreferredWidth(content, widthSettings)
                / label.pixelsPerUnit;
            int wordCount = content.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries).Length;
            preferredTextWidth *= Mathf.Max(1, wordCount) * 1.2f;
            float width = Mathf.Min(
                orderBubbleScrollRect.viewport.rect.width,
                (preferredTextWidth - textRect.sizeDelta.x) / textWidthRatio);
            slotLayout.preferredWidth = width;
            slotRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            float textHeightRatio = Mathf.Max(0.01f, textRect.anchorMax.y - textRect.anchorMin.y);
            textRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                width * textWidthRatio + textRect.sizeDelta.x);
            bubbleRect.sizeDelta = new Vector2(0f, minHeight);
            slotLayout.preferredHeight = minHeight + bubbleSpacing;
            Canvas.ForceUpdateCanvases();
            label.cachedTextGeneratorForLayout.Invalidate();
            float preferredTextHeight = label.preferredHeight;
            // UGUI can undercount one wrap while the parent layout is still settling.
            preferredTextHeight += label.fontSize * 1.5f;
            float height = Mathf.Max(
                minHeight,
                (preferredTextHeight - textRect.sizeDelta.y) / textHeightRatio);
            bubbleRect.sizeDelta = new Vector2(0f, height);

            float fullSlotHeight = height + bubbleSpacing;
            slotLayout.preferredHeight = fullSlotHeight;

            // Start fully hidden. The text keeps its layout, it is just invisible.
            label.text = RevealedText(content, 0);
            group.alpha = 0f;
            slotObject.SetActive(false);

            return new BubbleSlot
            {
                Slot = slotRect,
                SlotLayout = slotLayout,
                Bubble = bubbleRect,
                Group = group,
                Label = label,
                FullText = content,
                FullSlotHeight = fullSlotHeight
            };
        }

        // ---------------------------------------------------------------------
        // Animations
        // ---------------------------------------------------------------------

        private IEnumerator AnimateCustomerEntrance()
        {
            if (customerPortrait == null || customerPortraitCanvasGroup == null)
            {
                entranceRoutine = null;
                yield break;
            }

            yield return AnimateEntry(customerPortrait, customerPortraitCanvasGroup, portraitRestPosition);

            for (int index = 0; index < bubbleSlots.Count; index++)
            {
                BubbleSlot bubble = bubbleSlots[index];
                skipTyping = false;

                yield return GrowSlot(bubble);                                        // older bubbles smoothly make room
                yield return AnimateEntry(bubble.Bubble, bubble.Group, Vector2.zero); // new bubble slides in
                yield return TypeText(bubble);                                        // typewriter
                yield return new WaitForSecondsRealtime(bubblePause);
            }

            entranceComplete.TrySetResult(true);
            entranceRoutine = null;
        }

        /// <summary>Opens the slot's height from 0 so the layout group pushes older bubbles up smoothly.</summary>
        private IEnumerator GrowSlot(BubbleSlot bubble)
        {
            bubble.Group.alpha = 0f;
            bubble.SlotLayout.preferredHeight = 0f;
            bubble.Slot.gameObject.SetActive(true);
            if (!playerScrolledOrders) SnapToNewest();

            float startedAt = Time.unscaledTime;
            float duration = Mathf.Max(0.01f, growDuration);
            while (Time.unscaledTime - startedAt < duration)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                bubble.SlotLayout.preferredHeight = Mathf.Lerp(0f, bubble.FullSlotHeight, eased);
                yield return null;
            }

            bubble.SlotLayout.preferredHeight = bubble.FullSlotHeight;
        }

        private IEnumerator AnimateEntry(RectTransform target, CanvasGroup group, Vector2 restPosition)
        {
            target.anchoredPosition = restPosition + Vector2.right * AnimationDistance;
            group.alpha = 0f;
            float startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < AnimationDuration)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / AnimationDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                target.anchoredPosition = restPosition + Vector2.right * Mathf.Lerp(AnimationDistance, 0f, eased);
                group.alpha = eased;
                yield return null;
            }

            target.anchoredPosition = restPosition;
            group.alpha = 1f;
        }

        /// <summary>
        /// Typewriter effect. Speed is characters per second, so longer lines take longer.
        /// Uses the invisible-color trick so the remaining text still occupies space
        /// and words never jump to the next line while typing.
        /// </summary>
        private IEnumerator TypeText(BubbleSlot bubble)
        {
            string full = bubble.FullText;
            float secondsPerCharacter = 1f / Mathf.Max(1f, charactersPerSecond);
            float elapsed = 0f;
            float nextRevealAt = 0f;
            int shown = 0;

            while (shown < full.Length && !skipTyping)
            {
                elapsed += Time.unscaledDeltaTime;
                int before = shown;
                while (shown < full.Length && elapsed >= nextRevealAt)
                {
                    nextRevealAt += secondsPerCharacter + PunctuationPause(full[shown]);
                    shown++;
                }

                if (shown != before) bubble.Label.text = RevealedText(full, shown);
                yield return null;
            }

            bubble.Label.text = full;
        }

        private static string RevealedText(string full, int visibleCount)
        {
            if (visibleCount >= full.Length) return full;
            return full.Substring(0, visibleCount)
                + HiddenTextOpenTag
                + full.Substring(visibleCount)
                + HiddenTextCloseTag;
        }

        private static float PunctuationPause(char character)
        {
            switch (character)
            {
                case '.':
                case '!':
                case '?':
                    return 0.25f;
                case ',':
                case ';':
                case ':':
                    return 0.12f;
                default:
                    return 0f;
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        /// <summary>The list is bottom anchored, so content y = 0 shows the newest bubble.</summary>
        private void SnapToNewest()
        {
            orderBubbleScrollRect.StopMovement();
            Vector2 position = orderBubbleContent.anchoredPosition;
            position.y = 0f;
            orderBubbleContent.anchoredPosition = position;
        }

        public void StopOrderBubbleAutoScroll() => playerScrolledOrders = true;

        private void StopEntrance()
        {
            if (entranceRoutine == null) return;
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        private void ResetEntranceSignal()
        {
            entranceComplete.TrySetResult(false); // release anyone awaiting an interrupted entrance
            entranceComplete = new TaskCompletionSource<bool>();
        }

        private void ClearOrderBubbles()
        {
            foreach (BubbleSlot bubble in bubbleSlots)
            {
                if (bubble.Slot == null) continue;
                bubble.Slot.gameObject.SetActive(false);
                Destroy(bubble.Slot.gameObject);
            }
            bubbleSlots.Clear();
        }
    }
}
