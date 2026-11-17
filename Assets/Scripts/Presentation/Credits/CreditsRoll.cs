using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Witch.Presentation.Credits
{
    public sealed class CreditsRoll : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Image companyIcon;
        [SerializeField] private TMP_Text creditsText;
        [SerializeField] private TextAsset creditsSource;
        [SerializeField] private TMP_FontAsset creditsFontAsset;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Button returnButton;
        [SerializeField] private float scrollSpeed = 40f;
        [SerializeField] private float returnArmDelay = 0.5f;
        [Tooltip("Horizontal trigger line measured from the TOP edge of the viewport (0 = top, 1 = bottom).")]
        [SerializeField] private float exitTriggerFromTop = 0.3333f;
        [Tooltip("Optional delay after the trigger line is reached before exit (0 = immediate).")]
        [SerializeField] private float endHoldSeconds = 0f;
        [SerializeField] private float iconSpacing = 48f;

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

            ApplyTextAndStyle();
            LayoutContent();
            ResetState();

            hint.gameObject.SetActive(false);
            returnButton.interactable = false;

            IsPlaying = true;
            float lastY = GetLastVisibleLineBottomInViewport();
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

            float triggerViewportY = TriggerViewportY();
            float lastLineViewportY = GetLastVisibleLineBottomInViewport();
            if (!triggerReached && lastLineViewportY >= triggerViewportY)
            {
                triggerReached = true;
                holdElapsed = 0f;
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
            if (companyIcon == null)
            {
                Transform t = FindChildTransform("Company Icon");
                if (t != null) companyIcon = t.GetComponent<Image>();
            }
            if (creditsText == null)
            {
                Transform t = FindChildTransform("Credits Text");
                if (t != null) creditsText = t.GetComponent<TMP_Text>();
            }
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
            if (companyIcon == null) Add("Company Icon");
            if (creditsText == null) Add("Credits Text");
            if (hint == null) Add("Hint Text");
            if (returnButton == null) Add("Return Button");
            return sb == null ? string.Empty : sb.ToString();
        }

        private void ApplyTextAndStyle()
        {
            if (creditsSource != null)
            {
                creditsText.text = creditsSource.text ?? string.Empty;
            }

            if (creditsFontAsset != null)
            {
                creditsText.font = creditsFontAsset;
                hint.font = creditsFontAsset;
            }

            ApplyWhiteStyle(creditsText);
            ApplyWhiteStyle(hint);

            creditsText.alignment = TextAlignmentOptions.Center;
            creditsText.textWrappingMode = TextWrappingModes.Normal;
            creditsText.fontSize = 34f;
            creditsText.fontStyle = FontStyles.Normal;

            hint.text = "Click again to return to Main Menu";
            hint.alignment = TextAlignmentOptions.BottomRight;
            hint.textWrappingMode = TextWrappingModes.Normal;
            hint.fontSize = 34f;
            hint.fontStyle = FontStyles.Normal;
        }

        private static void ApplyWhiteStyle(TMP_Text text)
        {
            if (text == null) return;
            text.color = Color.white;
            text.fontStyle = FontStyles.Normal;
            text.fontWeight = FontWeight.Regular;
            text.outlineWidth = 0f;
        }

        private void LayoutContent()
        {
            Canvas.ForceUpdateCanvases();
            if (creditsText != null) creditsText.ForceMeshUpdate(true, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            if (creditsText != null) creditsText.ForceMeshUpdate(true, true);

            float width = Mathf.Max(content.rect.width > 1f ? content.rect.width : 1500f, 1024f);
            content.sizeDelta = new Vector2(width, content.sizeDelta.y);
            float y = 0f;

            if (companyIcon != null)
            {
                RectTransform iconRt = companyIcon.rectTransform;
                iconRt.anchorMin = new Vector2(0.5f, 1f);
                iconRt.anchorMax = new Vector2(0.5f, 1f);
                iconRt.pivot = new Vector2(0.5f, 1f);
                float iconW = 1024f;
                float iconH = 404f;
                if (companyIcon.sprite != null && companyIcon.sprite.rect.width > 0f)
                {
                    iconH = iconW * (companyIcon.sprite.rect.height / companyIcon.sprite.rect.width);
                }
                iconRt.sizeDelta = new Vector2(iconW, iconH);
                iconRt.anchoredPosition = new Vector2(0f, -y);
                y += iconRt.rect.height > 0f ? iconRt.rect.height : iconH;
                y += iconSpacing;
            }

            float textTop = y;
            if (creditsText != null)
            {
                RectTransform textRt = creditsText.rectTransform;
                textRt.anchorMin = new Vector2(0.5f, 1f);
                textRt.anchorMax = new Vector2(0.5f, 1f);
                textRt.pivot = new Vector2(0.5f, 1f);
                textRt.sizeDelta = new Vector2(width, 0f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(textRt);
                float preferred = creditsText.preferredHeight;
                if (preferred < 1f) preferred = textRt.rect.height;
                textRt.sizeDelta = new Vector2(width, preferred);
                textRt.anchoredPosition = new Vector2(0f, -textTop);
                y += preferred;
            }

            float contentHeight = Mathf.Max(1f, y);
            content.sizeDelta = new Vector2(width, contentHeight);

            float viewportHeight = viewport.rect.height > 1f ? viewport.rect.height : 1080f;
            startY = -viewportHeight;
            Vector2 pos = content.anchoredPosition;
            pos.y = startY;
            content.anchoredPosition = pos;
            Canvas.ForceUpdateCanvases();

            // Measure bottom of last visible (non-empty) text line in viewport space via TMP textInfo.
            float triggerViewportY = TriggerViewportY();
            float lastLineViewportY = GetLastVisibleLineBottomInViewport();
            float delta = triggerViewportY - lastLineViewportY;
            triggerContentY = startY + Mathf.Max(0f, delta);
            EstimatedDurationSeconds = scrollSpeed > 0f ? Mathf.Max(0f, triggerContentY - startY) / scrollSpeed : 0f;
        }

        // Bottom of last line with visible characters in viewport local Y (ignores trailing blank/newline-only lines).
        private float GetLastVisibleLineBottomInViewport()
        {
            if (creditsText == null || viewport == null)
            {
                return 0f;
            }

            creditsText.ForceMeshUpdate(true, true);
            TMP_TextInfo info = creditsText.textInfo;
            if (info == null || info.lineCount == 0)
            {
                info = creditsText.GetTextInfo(creditsText.text);
            }

            if (info != null && info.lineCount > 0)
            {
                for (int i = info.lineCount - 1; i >= 0; i--)
                {
                    TMP_LineInfo line = info.lineInfo[i];
                    if (line.characterCount > 0 && line.visibleCharacterCount > 0)
                    {
                        Vector3 world = creditsText.rectTransform.TransformPoint(new Vector3(0f, line.lineExtents.min.y, 0f));
                        Vector3 local = viewport.InverseTransformPoint(world);
                        return local.y;
                    }
                }
            }

            // Fallback: text bounds bottom in viewport space.
            Vector3 fbWorld = creditsText.rectTransform.TransformPoint(new Vector3(0f, creditsText.textBounds.min.y, 0f));
            return viewport.InverseTransformPoint(fbWorld).y;
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
