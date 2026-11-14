using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation
{
    public sealed class DayTransitionController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text dayLabelText;
        [SerializeField] private Text dayNumberText;
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.45f;
        [SerializeField, Min(0f)] private float holdBeforeFadeOut = 1.0f;

        private Coroutine running;
        private Coroutine watchdogRoutine;
        private bool revealOwed;       // a transition promised the next Shop init will reveal the screen
        private bool revealRequested;  // the Shop has asked for the reveal
        private Action storedRevealComplete;
        private int pendingDayNumber;

        public event Action CoverFullyEstablished;

        public bool IsPlaying => running != null;
        public bool IsCovered => isActiveAndEnabled && gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha >= 0.999f;
        public bool HasPendingShopReveal => revealOwed || revealRequested;
        public float FadeDuration => fadeDuration;
        public float HoldBeforeFadeOut => holdBeforeFadeOut;

        public void Configure(CanvasGroup group, Image background, Text dayLabel, Text dayNumber, float duration, float holdDuration = 1.0f)
        {
            canvasGroup = group;
            backgroundImage = background;
            dayLabelText = dayLabel;
            dayNumberText = dayNumber;
            if (duration > 0f) fadeDuration = duration;
            if (holdDuration >= 0f) holdBeforeFadeOut = holdDuration;
        }

        public void SetDayNumber(int dayNumber)
        {
            pendingDayNumber = dayNumber;
            if (dayNumberText != null) dayNumberText.text = dayNumber.ToString();
        }

        public void CoverInstant(int dayNumber, bool revealShopOnInit = false)
        {
            SetDayNumber(dayNumber);
            if (running != null)
            {
                if (revealShopOnInit) revealOwed = true;
                return;
            }
            revealOwed = revealShopOnInit;
            revealRequested = false;
            storedRevealComplete = null;
            StopWatchdog();
            EnsureVisibleCovered();
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        public Coroutine PlayFadeOut(int dayNumber, Action onComplete = null)
        {
            if (running != null) return running;
            SetDayNumber(dayNumber);
            revealOwed = false;
            revealRequested = false;
            storedRevealComplete = onComplete;
            EnsureVisibleCovered();
            running = StartCoroutine(FadeOutRoutine(0, false));
            return running;
        }

        public Coroutine PlayDayToDayTransition(int nextDayNumber, Func<Task> whileCoveredAsync)
        {
            if (running != null) return running;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
                canvasGroup.alpha = 0f;
            }
            // Owed from the first frame: the new Shop initializes BEFORE whileCoveredAsync completes.
            revealOwed = true;
            revealRequested = false;
            storedRevealComplete = null;
            StopWatchdog();
            running = StartCoroutine(DayToDayRoutine(nextDayNumber, whileCoveredAsync));
            return running;
        }

        public Coroutine RevealIfPending(Action onComplete = null)
        {
            if (!revealOwed && !revealRequested)
            {
                InvokeCallbackOnce(onComplete);
                return null;
            }
            revealRequested = true;
            storedRevealComplete = MergeCallbacks(storedRevealComplete, onComplete);
            TryStartReveal();
            return running;
        }

        private void TryStartReveal()
        {
            if (!revealRequested || running != null) return;
            revealOwed = false;
            revealRequested = false;
            StopWatchdog();
            EnsureVisibleCovered();
            running = StartCoroutine(FadeOutRoutine(2, false));
            StartWatchdog(holdBeforeFadeOut + fadeDuration + 5f, "fade-out-did-not-finish");
        }

        public void CancelPendingReveal()
        {
            revealOwed = false;
            revealRequested = false;
            storedRevealComplete = null;
            StopWatchdog();
        }

        public void NotifyCoverFullyEstablished()
        {
            CoverFullyEstablished?.Invoke();
        }

        public void ForceReveal(Action onComplete = null, string reason = "forced")
        {
            Debug.LogError($"[DayTransition] ForceReveal reason={reason} t={Time.realtimeSinceStartup:0.000} alpha={(canvasGroup != null ? canvasGroup.alpha : -1f):0.###} running={(running != null)} owed={revealOwed} requested={revealRequested}");
            gameObject.SetActive(true);
            StopWatchdog();
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            revealOwed = false;
            revealRequested = false;
            storedRevealComplete = MergeCallbacks(storedRevealComplete, onComplete);
            running = StartCoroutine(FadeOutRoutine(0, true));
        }

        private IEnumerator DayToDayRoutine(int nextDayNumber, Func<Task> whileCoveredAsync)
        {
            yield return FadeAlpha(0f, 1f);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            SetDayNumber(nextDayNumber);
            NotifyCoverFullyEstablished();
            yield return null;

            if (whileCoveredAsync != null)
            {
                Task task = null;
                try { task = whileCoveredAsync(); }
                catch (Exception ex) { Debug.LogException(ex); }
                while (task != null && !task.IsCompleted) yield return null;
                if (task != null && task.IsFaulted) Debug.LogException(task.Exception);
            }

            running = null;

            // Cancelled while covered (for example the scene load failed): uncover whatever is behind.
            if (!revealOwed && !revealRequested) revealRequested = true;

            if (revealRequested) TryStartReveal();
            else StartWatchdog(10f, "covered-but-shop-never-requested-reveal");
        }

        private IEnumerator FadeOutRoutine(int stableFramesBeforeFade, bool skipHold)
        {
            EnsureVisibleCovered();
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            for (int i = 0; i < stableFramesBeforeFade; i++) yield return null;
            if (!skipHold && holdBeforeFadeOut > 0f)
            {
                float held = 0f;
                while (held < holdBeforeFadeOut)
                {
                    held += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            yield return FadeAlpha(1f, 0f);
            StopWatchdog();
            HideOverlay();
            running = null;
            Action callback = storedRevealComplete;   // delivered no matter which path started the fade
            storedRevealComplete = null;
            InvokeCallbackOnce(callback);
        }

        private void HideOverlay()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (backgroundImage != null) backgroundImage.raycastTarget = false;
            gameObject.SetActive(false);
        }

        private void EnsureVisibleCovered()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
                if (canvasGroup.alpha < 0.999f) canvasGroup.alpha = 1f;
            }
            if (backgroundImage != null) backgroundImage.raycastTarget = true;
        }

        private IEnumerator FadeAlpha(float from, float to)
        {
            float elapsed = 0f;
            if (canvasGroup != null) canvasGroup.alpha = from;
            while (elapsed < fadeDuration)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.05f) dt = 0.05f;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                t = t * t * (3f - 2f * t);
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = to;
        }

        private void StartWatchdog(float seconds, string reason)
        {
            StopWatchdog();
            watchdogRoutine = StartCoroutine(WatchdogRoutine(seconds, reason));
        }

        private void StopWatchdog()
        {
            if (watchdogRoutine != null)
            {
                StopCoroutine(watchdogRoutine);
                watchdogRoutine = null;
            }
        }

        private IEnumerator WatchdogRoutine(float seconds, string reason)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                if (!gameObject.activeSelf) { watchdogRoutine = null; yield break; }
                yield return null;
            }
            watchdogRoutine = null;
            if (gameObject.activeSelf) ForceReveal(null, reason);
        }

        private static Action MergeCallbacks(Action first, Action second)
        {
            if (first == null) return second;
            if (second == null) return first;
            Action firstCopy = first;
            Action secondCopy = second;
            return () =>
            {
                InvokeCallbackOnce(firstCopy);
                InvokeCallbackOnce(secondCopy);
            };
        }

        private static void InvokeCallbackOnce(Action callback)
        {
            try { callback?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public static DayTransitionController CreateRuntime(Transform parent)
        {
            var rootGo = new GameObject("Day Transition", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(Image), typeof(DayTransitionController));
            rootGo.layer = 5;
            if (parent != null)
            {
                rootGo.transform.SetParent(parent, false);
            }

            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;

            var canvasGroup = rootGo.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            var image = rootGo.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            var rect = (RectTransform)rootGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Font font = ResolveTransitionFont();

            Text dayLabel = CreateText(rootGo.transform, "Day Label", "DAY", font, 60, TextAnchor.MiddleRight,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-49f, 0f), new Vector2(139.8227f, 128f));
            Text dayNumber = CreateText(rootGo.transform, "Day Number", "1", font, 80, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(86.28966f, 8f), new Vector2(86f, 128f));

            var controller = rootGo.GetComponent<DayTransitionController>();
            controller.Configure(canvasGroup, image, dayLabel, dayNumber, 0.45f, 1.0f);
            rootGo.SetActive(false);
            return controller;
        }

        private static Font ResolveTransitionFont()
        {
            try
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(new[] { "Apple Chancery", "AppleChancery" }, 80);
                if (osFont != null) return osFont;
            }
            catch
            {
            }

            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return fallback;
        }

        private static Text CreateText(Transform parent, string name, string content, Font font, int fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
