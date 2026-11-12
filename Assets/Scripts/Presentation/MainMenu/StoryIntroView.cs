using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Witch.Presentation.MainMenu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class StoryIntroView : MonoBehaviour
    {
        [Serializable]
        private sealed class StoryPage
        {
            [TextArea(2, 5)] public string narration;
            public Sprite illustration;
            [Min(0f)] public float displaySeconds;
        }

        [SerializeField] private CanvasGroup pageGroup;
        [SerializeField] private TMP_Text narrationText;
        [SerializeField] private Image illustrationImage;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private StoryPage[] pages = Array.Empty<StoryPage>();
        [SerializeField, Min(0f)] private float panelFadeDuration = 0.4f;
        [SerializeField, Min(0f)] private float pageFadeDuration = 0.5f;
        [SerializeField] private bool autoAdvance = true;
        [SerializeField, Min(0f)] private float baseSeconds = 2f;
        [SerializeField, Min(0f)] private float secondsPerWord = 0.3f;
        [SerializeField, Min(0f)] private float minimumSeconds = 4f;

        private CanvasGroup panelGroup;
        private TaskCompletionSource<bool> finished;
        private Coroutine routine;
        private int pageIndex;
        private bool transitioning;
        private bool listenersBound;
        private float pageTimer;

        public bool IsPlaying => finished != null;

        public Task<bool> PlayAsync()
        {
            if (finished != null) return finished.Task;

            finished = new TaskCompletionSource<bool>();
            if (pages == null || pages.Length == 0)
            {
                return Complete(true);
            }

            BindListeners();
            gameObject.SetActive(true);
            pageIndex = 0;
            ShowPage(pageIndex);
            if (pageGroup != null) pageGroup.alpha = 1f;
            Panel.alpha = 0f;
            if (EventSystem.current != null && nextButton != null)
            {
                EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
            }

            StartTransition(Fade(Panel, 0f, 1f, panelFadeDuration));
            return finished.Task;
        }

        public void Hide()
        {
            StopTransition();
            if (finished != null)
            {
                Complete(false);
                return;
            }

            gameObject.SetActive(false);
        }

        private CanvasGroup Panel => panelGroup != null ? panelGroup : panelGroup = GetComponent<CanvasGroup>();

        private void OnDisable()
        {
            routine = null;
            transitioning = false;
            if (finished != null)
            {
                TaskCompletionSource<bool> signal = finished;
                finished = null;
                signal.TrySetResult(false);
            }
        }

        private void Update()
        {
            if (!autoAdvance || finished == null || transitioning) return;

            pageTimer += Time.unscaledDeltaTime;
            if (pageTimer >= PageSeconds(pageIndex)) OnNextClicked();
        }

        private float PageSeconds(int index)
        {
            StoryPage page = pages[index];
            if (page.displaySeconds > 0f) return page.displaySeconds;

            string narration = page.narration ?? string.Empty;
            int words = narration.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
            return Mathf.Max(minimumSeconds, baseSeconds + words * secondsPerWord);
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
            listenersBound = true;
        }

        private void OnNextClicked()
        {
            if (finished == null || transitioning) return;

            if (pageIndex >= pages.Length - 1)
            {
                StartTransition(Close());
                return;
            }

            StartTransition(TurnPage(pageIndex + 1));
        }

        private void OnSkipClicked()
        {
            if (finished == null) return;
            StartTransition(Close());
        }

        private IEnumerator TurnPage(int next)
        {
            yield return Fade(pageGroup, 1f, 0f, pageFadeDuration);
            pageIndex = next;
            ShowPage(pageIndex);
            yield return Fade(pageGroup, 0f, 1f, pageFadeDuration);
        }

        private IEnumerator Close()
        {
            Complete(true, true);
            yield break;
        }

        private Task<bool> Complete(bool completed, bool keepVisible = false)
        {
            TaskCompletionSource<bool> signal = finished;
            finished = null;
            if (!keepVisible) gameObject.SetActive(false);
            signal?.TrySetResult(completed);
            return signal != null ? signal.Task : Task.FromResult(completed);
        }

        private void ShowPage(int index)
        {
            StoryPage page = pages[index];
            pageTimer = 0f;
            if (narrationText != null) narrationText.text = page.narration;
            if (illustrationImage != null)
            {
                illustrationImage.sprite = page.illustration;
                illustrationImage.enabled = page.illustration != null;
            }
        }

        private void StartTransition(IEnumerator transition)
        {
            StopTransition();
            routine = StartCoroutine(RunTransition(transition));
        }

        private void StopTransition()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            transitioning = false;
        }

        private IEnumerator RunTransition(IEnumerator transition)
        {
            transitioning = true;
            yield return transition;
            transitioning = false;
            routine = null;
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
        }
    }
}