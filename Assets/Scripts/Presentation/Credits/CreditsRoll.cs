using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Witch.Presentation.Credits
{
    // Editing credits (scene-authored):
    // - Each child of Content is a row, in sibling order (top to bottom on screen). Logo row comes first.
    // - Edit text/style/size/color/alignment per row in the Inspector (code does not override row styles).
    // - Duplicate a row to add a line; delete/reorder rows freely.
    // - Use a Spacer row (empty object with a LayoutElement.preferredHeight) for gaps. Spacer rows have no Graphic.
    // - The last ACTIVE child of Content with a Graphic (TextMeshProUGUI or Image) decides when credits exit.
    public sealed class CreditsRoll : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Button returnButton;
        [SerializeField] private float scrollSpeed = 40f;
        [SerializeField] private float returnArmDelay = 0.5f;
        [Tooltip("Horizontal trigger line measured from the TOP edge of the viewport (0 = top, 1 = bottom).")]
        [SerializeField] private float exitTriggerFromTop = 0.3333f;
        [Tooltip("Optional delay after the trigger line is reached before exit (0 = immediate).")]
        [SerializeField] private float endHoldSeconds = 0f;

        public event Action ReturnRequested;

        public float EstimatedDurationSeconds { get; private set; }
        public float TotalDurationSeconds => EstimatedDurationSeconds + endHoldSeconds;
        public bool IsPlaying { get; private set; }

        private bool armed;
        private bool hintShown;
        private bool exitRequested;
        private bool triggerReached;
        private float armElapsed;
        private float holdElapsed;
        private float startY;
        private float triggerContentY;

        public void Play()
        {
            ResolveReferences();
            string missing = GetMissingReferenceNames();
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogError("CreditsRoll missing references: " + missing, this);
                return;
            }

            LayoutContent();
            ResetState();

            if (hint != null) hint.gameObject.SetActive(false);
            if (returnButton != null) returnButton.interactable = false;

            IsPlaying = true;
        }

        private void Update()
        {
            if (!IsPlaying || exitRequested)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;

            if (!armed)
            {
                armElapsed += dt;
                if (armElapsed >= returnArmDelay)
                {
                    Arm();
                }
            }

            Vector2 pos = content.anchoredPosition;
            pos.y += scrollSpeed * dt;
            content.anchoredPosition = pos;

            if (!triggerReached && TryGetLastRowBottomInViewport(out float lastLineViewportY))
            {
                if (lastLineViewportY >= TriggerViewportY())
                {
                    triggerReached = true;
                    holdElapsed = 0f;
                }
            }

            if (triggerReached)
            {
                if (endHoldSeconds <= 0f)
                {
                    RequestReturn();
                    return;
                }

                holdElapsed += dt;
                if (holdElapsed >= endHoldSeconds)
                {
                    RequestReturn();
                }
            }
        }

        private float TriggerViewportY()
        {
            float viewportHeight = viewport != null && viewport.rect.height > 1f ? viewport.rect.height : 1080f;
            float clamped = Mathf.Clamp01(exitTriggerFromTop);
            return viewportHeight * (1f - clamped);
        }

        private void ResolveReferences()
        {
            if (viewport == null) viewport = FindChildRect("Viewport");
            if (content == null) content = FindChildRect("Content");
            if (hint == null)
            {
                Transform t = FindChildTransform("Hint Text");
                if (t != null) hint = t.GetComponent<TMP_Text>();
            }
            if (returnButton == null)
            {
                Transform t = FindChildTransform("Return Button");
                if (t != null) returnButton = t.GetComponent<Button>();
            }
        }

        private RectTransform FindChildRect(string name)
        {
            Transform t = FindChildTransform(name);
            return t as RectTransform;
        }

        private Transform FindChildTransform(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].name, name, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }
            return null;
        }

        private string GetMissingReferenceNames()
        {
            StringBuilder sb = null;
            void Add(string n)
            {
                if (sb == null) sb = new StringBuilder();
                else sb.Append(", ");
                sb.Append(n);
            }
            if (viewport == null) Add("Viewport");
            if (content == null) Add("Content");
            if (hint == null) Add("Hint Text");
            if (returnButton == null) Add("Return Button");
            return sb == null ? string.Empty : sb.ToString();
        }

        private void LayoutContent()
        {
            if (content == null || viewport == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            // Start: content TOP edge exactly at viewport BOTTOM edge (nothing visible), derived from current rects.
            float viewportBottomY = GetViewportLocalBottomY();
            float contentTopY = GetContentTopInViewportLocal();
            startY = content.anchoredPosition.y + (viewportBottomY - contentTopY);

            Vector2 pos = content.anchoredPosition;
            pos.y = startY;
            content.anchoredPosition = pos;
            Canvas.ForceUpdateCanvases();

            float triggerViewportY = TriggerViewportY();
            if (TryGetLastRowBottomInViewport(out float lastLineViewportY))
            {
                float delta = triggerViewportY - lastLineViewportY;
                triggerContentY = startY + Mathf.Max(0f, delta);
            }
            else
            {
                triggerContentY = startY;
            }

            float distance = Mathf.Max(0f, triggerContentY - startY);
            EstimatedDurationSeconds = scrollSpeed > 0f ? distance / scrollSpeed : 0f;
        }

        private float GetViewportLocalBottomY()
        {
            Vector3[] corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            float minY = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                float y = viewport.InverseTransformPoint(corners[i]).y;
                if (y < minY) minY = y;
            }
            return minY;
        }

        private float GetContentTopInViewportLocal()
        {
            Vector3[] corners = new Vector3[4];
            content.GetWorldCorners(corners);
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                float y = viewport.InverseTransformPoint(corners[i]).y;
                if (y > maxY) maxY = y;
            }
            return maxY;
        }

        // Last ACTIVE child with a Graphic (Text/Image). Spacer rows have no Graphic and are ignored.
        private bool TryGetLastRowBottomInViewport(out float bottomViewportY)
        {
            bottomViewportY = 0f;
            if (content == null || viewport == null) return false;

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                Graphic g = child.GetComponent<Graphic>();
                if (g == null) continue;

                RectTransform rt = child as RectTransform;
                if (rt == null) continue;

                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                float minY = float.PositiveInfinity;
                for (int c = 0; c < 4; c++)
                {
                    float y = viewport.InverseTransformPoint(corners[c]).y;
                    if (y < minY) minY = y;
                }
                bottomViewportY = minY;
                return true;
            }

            return false;
        }

        private void ResetState()
        {
            armed = false;
            hintShown = false;
            exitRequested = false;
            triggerReached = false;
            armElapsed = 0f;
            holdElapsed = 0f;
        }

        private void Arm()
        {
            if (armed) return;
            armed = true;
            if (returnButton != null)
            {
                returnButton.interactable = true;
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
                }
            }
        }

        public void OnReturnClicked()
        {
            if (!IsPlaying || !armed) return;

            if (!hintShown)
            {
                hintShown = true;
                if (hint != null) hint.gameObject.SetActive(true);
                return;
            }

            RequestReturn();
        }

        private void RequestReturn()
        {
            if (exitRequested) return;
            exitRequested = true;
            IsPlaying = false;
            ReturnRequested?.Invoke();
        }

        private void OnEnable()
        {
            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(OnReturnClicked);
                returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        private void OnDisable()
        {
            IsPlaying = false;
        }
    }
}
