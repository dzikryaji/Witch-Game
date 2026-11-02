using System;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Shop
{
    /// <summary>
    /// Renders one side of the open guidebook. The visual layout lives in the scene/prefab;
    /// this component only fills authored slots and toggles which layout is visible.
    /// Slots that are not authored yet fall back to the single plain text box.
    /// </summary>
    public sealed class GuidebookPageView : MonoBehaviour
    {
        [Header("Always required")]
        [SerializeField] private Text plainText;
        [SerializeField] private Image illustration;

        [Header("Layouts (optional)")]
        [SerializeField] private GameObject textLayout;
        [SerializeField] private GameObject herbLayout;
        [SerializeField] private GameObject contentsLayout;

        [Header("Herb page (optional)")]
        [SerializeField] private Text title;
        [SerializeField] private Text scientificName;
        [SerializeField] private Text description;
        [SerializeField] private Text warning;
        [SerializeField] private GameObject categoryTag;
        [SerializeField] private Text categoryLabel;

        [Header("Contents page (optional)")]
        [SerializeField] private Text contentsTitle;
        [SerializeField] private GameObject[] contentsRows = new GameObject[0];
        [SerializeField] private Text[] contentsRowNames = new Text[0];
        [SerializeField] private Text[] contentsRowDescriptions = new Text[0];
        [Tooltip("Optional: one Button per row, same order as Contents Rows. Clicking jumps to that category.")]
        [SerializeField] private Button[] contentsRowButtons = new Button[0];

        private int[] rowTargets = new int[0];
        private Action<int> pageLinkRequested;
        private bool linksBound;

        public bool IsAuthored => plainText != null && illustration != null;

        public bool HasHerbLayout => title != null && description != null;

        public bool HasContentsLayout => contentsRows != null
            && contentsRows.Length > 0
            && contentsRowNames != null
            && contentsRowDescriptions != null
            && contentsRowNames.Length == contentsRows.Length
            && contentsRowDescriptions.Length == contentsRows.Length;

        /// <summary>Calls <paramref name="onPageRequested"/> with a page index when a Contents row is clicked.</summary>
        public void BindPageLinks(Action<int> onPageRequested)
        {
            pageLinkRequested = onPageRequested;
            if (linksBound || contentsRowButtons == null) return;
            for (int index = 0; index < contentsRowButtons.Length; index++)
            {
                int row = index;
                if (contentsRowButtons[row] != null) contentsRowButtons[row].onClick.AddListener(() => OnRowClicked(row));
            }
            linksBound = true;
        }

        private void OnRowClicked(int row)
        {
            if (row < rowTargets.Length && rowTargets[row] >= 0) pageLinkRequested?.Invoke(rowTargets[row]);
        }

        /// <summary>Shows a page, or clears this side of the book when <paramref name="page"/> is null.</summary>
        public void Render(GuidebookPageViewData page)
        {
            bool showHerb = page != null && page.Kind == GuidebookPageKind.Herb && HasHerbLayout;
            bool showContents = page != null && page.Kind == GuidebookPageKind.Contents && HasContentsLayout;
            bool showText = page != null && !showHerb && !showContents;

            SetActive(herbLayout, showHerb);
            SetActive(contentsLayout, showContents);
            SetActive(textLayout, showText);
            plainText.gameObject.SetActive(showText);

            bool showIllustration = page != null
                && page.Illustration != null
                && page.Kind != GuidebookPageKind.Contents;
            illustration.sprite = showIllustration ? page.Illustration : null;
            illustration.gameObject.SetActive(showIllustration);

            // Parts may live directly under the page (no layout wrapper), so hide the ones not in use.
            if (!showHerb) HideHerbParts();
            if (!showContents) HideContentsParts();

            if (showHerb) RenderHerb(page);
            else if (showContents) RenderContents(page);
            else plainText.text = page == null ? string.Empty : page.Text;
        }

        private void HideHerbParts()
        {
            SetActive(title != null ? title.gameObject : null, false);
            SetActive(scientificName != null ? scientificName.gameObject : null, false);
            SetActive(description != null ? description.gameObject : null, false);
            SetActive(warning != null ? warning.gameObject : null, false);
            SetActive(categoryTag, false);
        }

        private void HideContentsParts()
        {
            SetActive(contentsTitle != null ? contentsTitle.gameObject : null, false);
            if (contentsRows == null) return;
            foreach (GameObject row in contentsRows) SetActive(row, false);
        }

        private void RenderHerb(GuidebookPageViewData page)
        {
            SetText(title, page.Title);
            SetText(scientificName, page.ScientificName);
            SetText(description, page.Description);
            SetText(warning, page.Warning);
            SetText(categoryLabel, page.CategoryLabel);
            SetActive(categoryTag, !string.IsNullOrEmpty(page.CategoryLabel));
        }

        private void RenderContents(GuidebookPageViewData page)
        {
            SetText(contentsTitle, page.Title);
            if (rowTargets.Length != contentsRows.Length) rowTargets = new int[contentsRows.Length];
            for (int index = 0; index < contentsRows.Length; index++)
            {
                bool used = index < page.Rows.Count;
                SetActive(contentsRows[index], used);
                rowTargets[index] = used ? page.Rows[index].TargetPageIndex : -1;
                Button button = contentsRowButtons != null && index < contentsRowButtons.Length ? contentsRowButtons[index] : null;
                if (button != null) button.interactable = used && page.Rows[index].HasLink;
                if (!used) continue;
                SetText(contentsRowNames[index], page.Rows[index].Name);
                SetText(contentsRowDescriptions[index], page.Rows[index].Description);
            }
        }

        // An empty part is hidden so a Vertical Layout Group closes the gap.
        private static void SetText(Text target, string value)
        {
            if (target == null) return;
            target.text = value ?? string.Empty;
            target.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
