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
        [SerializeField] private Text statusText;
        [SerializeField] private Text dayText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text suspicionText;
        [SerializeField] private UnityEngine.UI.Image suspicionFill;
        [SerializeField] private Text potText;
        [SerializeField] private Text customerText;
        [SerializeField] private Text orderText;
        [SerializeField] private Text[] orderBubbleTexts;
        [SerializeField] private RectTransform customerPanel;
        [SerializeField] private CanvasGroup customerCanvasGroup;
        [SerializeField] private GameObject customerVisual;

        private Coroutine entranceRoutine;
        private Vector2 customerRestPosition;

        public bool IsAuthored => statusText != null
            && dayText != null
            && timerText != null
            && moneyText != null
            && suspicionText != null
            && suspicionFill != null
            && potText != null
            && customerText != null
            && orderText != null
            && orderBubbleTexts != null
            && orderBubbleTexts.Length == 3
            && customerPanel != null
            && customerCanvasGroup != null
            && customerVisual != null;

        private void Awake()
        {
            if (customerPanel != null)
            {
                customerRestPosition = customerPanel.anchoredPosition;
            }
        }

        public void SetStatus(string message)
        {
            statusText.text = message ?? string.Empty;
        }

        public void SetCustomer(string customerName, string clueText)
        {
            customerVisual.SetActive(true);
            customerText.text = customerName ?? string.Empty;
            string[] lines = SplitOrderIntoThreeLines(clueText ?? string.Empty);
            orderText.text = lines[0];
            for (int index = 0; index < orderBubbleTexts.Length; index++)
            {
                orderBubbleTexts[index].text = string.Empty;
            }
            if (entranceRoutine != null) StopCoroutine(entranceRoutine);
            entranceRoutine = StartCoroutine(AnimateCustomerEntrance(lines));
        }

        public void ClearCustomer()
        {
            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
                entranceRoutine = null;
            }
            customerText.text = string.Empty;
            orderText.text = string.Empty;
            foreach (Text bubble in orderBubbleTexts)
            {
                if (bubble != null) bubble.text = string.Empty;
            }
            customerVisual.SetActive(false);
            customerPanel.anchoredPosition = customerRestPosition;
            customerCanvasGroup.alpha = 1f;
        }

        public async Task PlayCustomerExitAsync(CancellationToken cancellationToken)
        {
            if (customerPanel == null || customerCanvasGroup == null) return;
            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
                entranceRoutine = null;
            }

            float startedAt = Time.unscaledTime;
            const float duration = 0.24f;
            while (Time.unscaledTime - startedAt < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                customerPanel.anchoredPosition = customerRestPosition + Vector2.right * Mathf.Lerp(0f, 260f, progress);
                customerCanvasGroup.alpha = 1f - progress;
                await Task.Yield();
            }

            customerPanel.anchoredPosition = customerRestPosition + Vector2.right * 260f;
            customerCanvasGroup.alpha = 0f;
            customerVisual.SetActive(false);
        }

        public void Render(GameSession session, Func<string, string> displayName)
        {
            if (session == null) return;

            TimeSpan remaining = session.TimerState.RemainingTime;
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

        private IEnumerator AnimateCustomerEntrance(string[] orderLines)
        {
            if (customerPanel == null || customerCanvasGroup == null) yield break;
            customerPanel.anchoredPosition = customerRestPosition + Vector2.right * 260f;
            customerCanvasGroup.alpha = 0f;
            float startedAt = Time.unscaledTime;
            const float duration = 0.28f;
            while (Time.unscaledTime - startedAt < duration)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                customerPanel.anchoredPosition = customerRestPosition + Vector2.right * Mathf.Lerp(260f, 0f, eased);
                customerCanvasGroup.alpha = eased;
                yield return null;
            }
            customerPanel.anchoredPosition = customerRestPosition;
            customerCanvasGroup.alpha = 1f;
            for (int index = 0; index < orderBubbleTexts.Length; index++)
            {
                orderBubbleTexts[index].text = orderLines[index];
                yield return new WaitForSecondsRealtime(0.12f);
            }
            entranceRoutine = null;
        }

        private static string[] SplitOrderIntoThreeLines(string clue)
        {
            var result = new[] { string.Empty, string.Empty, string.Empty };
            if (string.IsNullOrWhiteSpace(clue)) return result;
            string[] words = clue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int wordsPerLine = Mathf.Max(1, Mathf.CeilToInt(words.Length / 3f));
            for (int line = 0; line < result.Length; line++)
            {
                int start = line * wordsPerLine;
                if (start >= words.Length) break;
                int count = Mathf.Min(wordsPerLine, words.Length - start);
                result[line] = string.Join(" ", words, start, count);
            }
            return result;
        }
    }
}
