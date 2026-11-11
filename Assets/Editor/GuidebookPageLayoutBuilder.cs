using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Witch.Infrastructure.Content;
using Witch.Presentation.Shop;

namespace Witch.Editor
{
    /// <summary>
    /// Adds the herb, contents and plain-text layouts to GuidebookPage.prefab and wires its
    /// GuidebookPageView. Order, spacing and text sizes follow the old guidebook pages in
    /// Shop.unity. The prefab is previewed with real data (Herb_Ginger + herb categories);
    /// in the game ShopShellPresenter renders every page from the HerbDefinitions.
    /// Runs at edit time only.
    /// </summary>
    public static class GuidebookPageLayoutBuilder
    {
        private const string PagePrefabPath = "Assets/Prefabs/Guidebook/GuidebookPage.prefab";
        private const string PanelPrefabPath = "Assets/Prefabs/Guidebook/GuidebookPanel.prefab";
        private const string BookPrefabPath = "Assets/Prefabs/Guidebook/GuidebookBook.prefab";
        private const string OpenGroupName = "Book Open Group";
        private const string PreviousButtonName = "PreviousPage";
        private const string NextButtonName = "NextPage";
        private const string ContentsButtonName = "BackToContents";
        private const string BackButtonSpritePath = "Assets/Art/Guidebook/BackButton.png";
        private const string SpineShadowPath = "Assets/Art/Guidebook/SpineShadow.png";
        private const string SpineShadowName = "SpineShadow";
        private const float SpineShadowWidth = 70f;     // about an eighth of the page
        private const float SpineShadowStrength = 0.45f; // darkest alpha, right at the spine
        private const string TitleFontPath = "Assets/Fonts/BlackKiyrand-Regular.ttf";
        private const string HeadingFontPath = "Assets/Fonts/MedievalSharp.ttf";
        private const string CategoryTagPath = "Assets/Art/Guidebook/TagCategoryPlain.png"; // plain block, no ribbon notch
        private const string PreviewHerbPath = "Assets/Content/Herbs/Herb_Ginger.asset";
        private const string CategoryContentFolder = "Assets/Content/Categories";
        private const int ContentsRowCount = 6;

        private static readonly string[] GeneratedContentChildren = { "HerbLayout", "ContentsLayout", "PlainText" };
        private const string CategoryTagName = "CategoryTag";

        // Colours copied from the old guidebook pages.
        private static readonly Color Ink = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        private static readonly Color PlainInk = new Color(0.11764706f, 0.09019608f, 0.08627451f, 1f);
        private static readonly Color TagInk = new Color(0.9529412f, 0.9019608f, 0.76862746f, 1f);

        private sealed class Fonts
        {
            public TMP_FontAsset Title;   // names, Latin names, headings (blackletter, as in the new art)
            public TMP_FontAsset Heading; // plain text pages (MedievalSharp, as in the old pages)
            public TMP_FontAsset Body;    // descriptions and warnings (TMP default, like the old Arial text)
        }

        [MenuItem("Witch/Guidebook/Build Page Text Layout")]
        public static void BuildPageLayout()
        {
            if (TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == null)
            {
                Fail("TextMeshPro is not set up in this project yet.\n\n"
                    + "Open Window → TextMeshPro → Import TMP Essential Resources, click Import, then run this menu again.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PagePrefabPath) == null)
            {
                Fail("Run \"Build Blank Book Prefabs\" first: " + PagePrefabPath + " is missing.");
                return;
            }

            var fonts = new Fonts
            {
                Title = EnsureFontAsset(TitleFontPath),
                Heading = EnsureFontAsset(HeadingFontPath),
                Body = TMP_Settings.defaultFontAsset
            };
            if (fonts.Title == null || fonts.Heading == null)
            {
                Fail("Could not create the TMP font assets from Assets/Fonts. Check the Console for details.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PagePrefabPath);
            try
            {
                Transform content = root.transform.Find("Content");
                if (content == null)
                {
                    Fail("GuidebookPage.prefab has no \"Content\" child. Rebuild it with \"Build Blank Book Prefabs\".");
                    return;
                }

                bool hasLayout = GeneratedContentChildren.Any(childName => content.Find(childName) != null)
                    || root.transform.Find(CategoryTagName) != null;
                if (hasLayout && !EditorUtility.DisplayDialog(
                        "Guidebook",
                        "GuidebookPage already has a text layout. Replace it? Changes you made inside HerbLayout, ContentsLayout, PlainText and CategoryTag will be lost.",
                        "Replace",
                        "Cancel"))
                {
                    return;
                }

                foreach (string childName in GeneratedContentChildren) DestroyChild(content, childName);
                DestroyChild(root.transform, CategoryTagName);

                PageParts parts = BuildLayouts(root.transform, content, fonts);
                WireView(root, parts);
                FillPreview(parts);

                PrefabUtility.SaveAsPrefabAsset(root, PagePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Object page = AssetDatabase.LoadAssetAtPath<GameObject>(PagePrefabPath);
            Selection.activeObject = page;
            EditorGUIUtility.PingObject(page);
        }

        [MenuItem("Witch/Guidebook/Use New Guidebook In Open Shop Scene")]
        public static void UseNewGuidebookInShopScene()
        {
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>(FindObjectsInactive.Include);
            if (presenter == null)
            {
                Fail("Open Assets/Scenes/Shop.unity first.");
                return;
            }

            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            GameObject panel = presenter.gameObject.scene.GetRootGameObjects()
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<Transform>(true))
                .Select(child => child.gameObject)
                .FirstOrDefault(candidate => PrefabUtility.IsAnyPrefabInstanceRoot(candidate)
                    && PrefabUtility.GetCorrespondingObjectFromSource(candidate) == panelPrefab);
            if (panel == null)
            {
                Fail("Put GuidebookPanel.prefab into the Shop scene first.");
                return;
            }

            GuidebookPageView left = FindPage(panel, "LeftPage", "GuidebookBook");
            GuidebookPageView right = FindPage(panel, "RightPage", "GuidebookBook");
            if (left == null || right == null)
            {
                Fail("LeftPage / RightPage have no GuidebookPageView yet. Run \"Build Page Text Layout\" first.");
                return;
            }

            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("guidebookPanel").objectReferenceValue = panel;
            serialized.FindProperty("guidebookLeftPage").objectReferenceValue = left;
            serialized.FindProperty("guidebookRightPage").objectReferenceValue = right;

            // Page turn and Prev/Next buttons, once "Add Page Turn And Buttons" has been run.
            PageFlipAnimator flip = panel.GetComponentInChildren<PageFlipAnimator>(true);
            GuidebookPageView flipFront = FindPage(panel, "FlipFront", "TurnClip");
            GuidebookPageView flipBack = FindPage(panel, "FlipBack", "TurnClip");
            Button previous = FindButton(panel, PreviousButtonName);
            Button next = FindButton(panel, NextButtonName);
            if (flip != null && flipFront != null && flipBack != null)
            {
                serialized.FindProperty("guidebookFlipAnimator").objectReferenceValue = flip;
                serialized.FindProperty("guidebookFlipFront").objectReferenceValue = flipFront;
                serialized.FindProperty("guidebookFlipBack").objectReferenceValue = flipBack;
            }

            if (previous != null && next != null)
            {
                serialized.FindProperty("guidebookPreviousButton").objectReferenceValue = previous;
                serialized.FindProperty("guidebookNextButton").objectReferenceValue = next;
            }

            Button contents = FindButton(panel, ContentsButtonName);
            if (contents != null) serialized.FindProperty("guidebookContentsButton").objectReferenceValue = contents;

            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
            Selection.activeObject = presenter;
            EditorGUIUtility.PingObject(presenter);
            Debug.Log("ShopShellPresenter now uses the new guidebook panel and pages. Save the Shop scene to keep it.", presenter);
        }

        [MenuItem("Witch/Guidebook/Add Page Turn And Buttons")]
        public static void AddPageTurnAndButtons()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BookPrefabPath) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath) == null)
            {
                Fail("Build the book and panel prefabs first.");
                return;
            }

            // 1. The page-turn animator on the book, wired to the objects that already exist.
            GameObject book = PrefabUtility.LoadPrefabContents(BookPrefabPath);
            try
            {
                Transform turnClip = book.transform.Find("TurnClip");
                Transform flipFront = turnClip != null ? turnClip.Find("FlipFront") : null;
                Transform flipBack = turnClip != null ? turnClip.Find("FlipBack") : null;
                if (flipFront == null || flipBack == null)
                {
                    Fail("GuidebookBook.prefab has no TurnClip/FlipFront/FlipBack. Rebuild it with \"Build Blank Book Prefabs\".");
                    return;
                }

                PageFlipAnimator flip = book.GetComponent<PageFlipAnimator>();
                if (flip == null) flip = book.AddComponent<PageFlipAnimator>();
                var serialized = new SerializedObject(flip);
                Set(serialized, "spread", book.transform);
                Set(serialized, "turnClip", turnClip);
                Set(serialized, "flipFront", flipFront);
                Set(serialized, "flipBack", flipBack);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // The turning pages are drawn by code; their links must never be clicked.
                foreach (Graphic graphic in turnClip.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }

                turnClip.gameObject.SetActive(false);
                if (book.transform.Find(ContentsButtonName) == null) BuildContentsButton(book.transform);
                PrefabUtility.SaveAsPrefabAsset(book, BookPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(book);
            }

            // 2. Previous / Next buttons beside the book, styled like the old guidebook.
            GameObject panel = PrefabUtility.LoadPrefabContents(PanelPrefabPath);
            try
            {
                Transform group = panel.transform.Find(OpenGroupName);
                if (group == null)
                {
                    Fail("GuidebookPanel.prefab has no \"" + OpenGroupName + "\".");
                    return;
                }

                TMP_FontAsset labelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MedievalSharp SDF.asset")
                    ?? TMP_Settings.defaultFontAsset;
                if (group.Find(PreviousButtonName) == null)
                {
                    BuildPageButton(group, PreviousButtonName, "<", new Vector2(0f, 0.45f), new Vector2(0.042f, 0.55f), labelFont);
                }

                if (group.Find(NextButtonName) == null)
                {
                    BuildPageButton(group, NextButtonName, ">", new Vector2(0.958f, 0.45f), new Vector2(1f, 0.55f), labelFont);
                }

                PrefabUtility.SaveAsPrefabAsset(panel, PanelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Guidebook",
                "Page turn and buttons added.\n\nOpen Shop.unity and run \"Use New Guidebook In Open Shop Scene\" again so ShopController uses them.",
                "OK");
        }

        [MenuItem("Witch/Guidebook/Add Spine Shadow")]
        public static void AddSpineShadow()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PagePrefabPath) == null)
            {
                Fail("Run \"Build Blank Book Prefabs\" first: " + PagePrefabPath + " is missing.");
                return;
            }

            Sprite sprite = EnsureSpineShadowSprite();
            if (sprite == null)
            {
                Fail("Could not create " + SpineShadowPath + ". Check the Console for details.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PagePrefabPath);
            try
            {
                GuidebookPageView view = root.GetComponent<GuidebookPageView>();
                if (view == null)
                {
                    Fail("Run \"Build Page Text Layout\" first: GuidebookPage has no GuidebookPageView.");
                    return;
                }

                DestroyChild(root.transform, SpineShadowName);

                // Authored for a right-hand page: dark on its left edge (the spine), fading toward the text.
                // GuidebookPageView mirrors it onto the right edge when the page is shown on the left.
                GameObject shadow = NewUi(SpineShadowName, root.transform);
                shadow.transform.SetSiblingIndex(0); // over the paper, under the text
                var rect = (RectTransform)shadow.transform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(SpineShadowWidth, 0f);
                Image image = shadow.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;

                var serialized = new SerializedObject(view);
                Set(serialized, "spineShadow", rect);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PagePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Guidebook",
                "Spine shadow added to every guidebook page. Adjust its width or colour on GuidebookPage/SpineShadow.",
                "OK");
        }

        // A small horizontal gradient: dark at x = 0, fully transparent at the far end.
        private static Sprite EnsureSpineShadowSprite()
        {
            if (!File.Exists(SpineShadowPath))
            {
                const int width = 128;
                const int height = 4;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var shade = new Color(0.16f, 0.10f, 0.06f, 0f);
                for (int x = 0; x < width; x++)
                {
                    float fade = 1f - x / (width - 1f);
                    shade.a = SpineShadowStrength * fade * fade; // soft falloff away from the spine
                    for (int y = 0; y < height; y++) texture.SetPixel(x, y, shade);
                }

                File.WriteAllBytes(SpineShadowPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SpineShadowPath);
            }

            if (AssetImporter.GetAtPath(SpineShadowPath) is TextureImporter importer
                && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpineShadowPath);
        }

        // The back arrow: top-left corner of the book (the left page). It sits on the book, not on a
        // page, so there is only one, and it stays put while pages turn. The category tag sits below it.
        private static void BuildContentsButton(Transform book)
        {
            Sprite arrow = AssetDatabase.LoadAllAssetsAtPath(BackButtonSpritePath).OfType<Sprite>().FirstOrDefault();
            GameObject buttonObject = NewUi(ContentsButtonName, book);
            buttonObject.transform.SetAsLastSibling(); // above the pages and the turning page
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(36f, -18f);
            rect.sizeDelta = new Vector2(66f, 48f);

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = arrow;
            image.preserveAspect = true;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            buttonObject.SetActive(false); // the presenter shows it once the reader leaves the Contents spread
        }

        private static void BuildPageButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TMP_FontAsset font)
        {
            GameObject buttonObject = NewUi(name, parent);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            buttonObject.transform.SetAsLastSibling(); // above the book, so it is always clickable

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.6666667f, 0.57254905f, 0.57254905f, 1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;

            TMP_Text text = NewText("Label", buttonObject.transform, font, 25f, FontStyles.Bold,
                TextAlignmentOptions.Center, PlainInk, wrap: false);
            Stretch((RectTransform)text.transform);
            text.text = label;
            text.enableAutoSizing = true;
            text.fontSizeMin = 17f;
            text.fontSizeMax = 26f;
        }

        private sealed class PageParts
        {
            public TMP_Text PlainText;
            public Image Illustration;
            public GameObject HerbLayout;
            public GameObject ContentsLayout;
            public TMP_Text Title;
            public TMP_Text ScientificName;
            public TMP_Text Description;
            public TMP_Text Warning;
            public GameObject CategoryTag;
            public TMP_Text CategoryLabel;
            public TMP_Text ContentsTitle;
            public readonly List<GameObject> Rows = new List<GameObject>();
            public readonly List<TMP_Text> RowNames = new List<TMP_Text>();
            public readonly List<TMP_Text> RowDescriptions = new List<TMP_Text>();
            public readonly List<Button> RowButtons = new List<Button>();
        }

        private static PageParts BuildLayouts(Transform page, Transform content, Fonts fonts)
        {
            var parts = new PageParts();

            // ---- Herb page: same order and spacing as the old page --------------------------
            GameObject herb = NewUi("HerbLayout", content);
            Stretch((RectTransform)herb.transform);
            VerticalLayoutGroup herbLayout = AddVertical(herb, spacing: 6f, TextAnchor.UpperCenter);
            herbLayout.padding = new RectOffset(0, 0, 72, 0); // start below the ornament panel
            parts.HerbLayout = herb;

            // Name + Latin name sit on the ornament panel printed in BlankPageNew
            // (page px 129-440 x 56-169). The plate is placed by hand, not by the page layout.
            GameObject plate = NewUi("TitlePlate", herb.transform);
            AddVertical(plate, spacing: 0f, TextAnchor.MiddleCenter);
            plate.AddComponent<LayoutElement>().ignoreLayout = true;
            var plateRect = (RectTransform)plate.transform;
            plateRect.anchorMin = new Vector2(0.5f, 1f);
            plateRect.anchorMax = new Vector2(0.5f, 1f);
            plateRect.pivot = new Vector2(0.5f, 0.5f);
            plateRect.anchoredPosition = new Vector2(1f, -16f); // content starts 96 px down; panel centre is at 112
            plateRect.sizeDelta = new Vector2(270f, 100f);

            parts.Title = NewText("Title", plate.transform, fonts.Title, 40f, FontStyles.Normal,
                TextAlignmentOptions.Center, Ink, wrap: false);
            parts.Title.enableAutoSizing = true;
            parts.Title.fontSizeMin = 20f;
            parts.Title.fontSizeMax = 40f;

            parts.ScientificName = NewText("ScientificName", plate.transform, fonts.Title, 22f, FontStyles.Normal,
                TextAlignmentOptions.Center, Ink, wrap: false);
            parts.ScientificName.enableAutoSizing = true;
            parts.ScientificName.fontSizeMin = 14f;
            parts.ScientificName.fontSizeMax = 22f;

            AddSpacer("Spacer", herb.transform, 8f);

            GameObject illustration = NewUi("Illustration", herb.transform);
            parts.Illustration = illustration.AddComponent<Image>();
            parts.Illustration.preserveAspect = true;
            parts.Illustration.raycastTarget = false;
            LayoutElement illustrationLayout = illustration.AddComponent<LayoutElement>();
            illustrationLayout.preferredHeight = 300f;
            illustrationLayout.minHeight = 220f;
            illustrationLayout.flexibleHeight = 1f;

            parts.Description = NewText("Description", herb.transform, fonts.Body, 19f, FontStyles.Bold,
                TextAlignmentOptions.Top, Ink, wrap: true);
            AddSpacer("WarningSpacer", herb.transform, 14f);
            parts.Warning = NewText("Warning", herb.transform, fonts.Body, 19f, FontStyles.Bold,
                TextAlignmentOptions.Top, Ink, wrap: true);

            // Category tag sits in the page corner, outside the content area, as in the new art.
            GameObject tag = NewUi(CategoryTagName, page);
            var tagRect = (RectTransform)tag.transform;
            tagRect.anchorMin = Vector2.one;
            tagRect.anchorMax = Vector2.one;
            tagRect.pivot = Vector2.one;
            tagRect.anchoredPosition = new Vector2(0f, -74f); // level with the herb title on the ornament panel
            tagRect.sizeDelta = new Vector2(118f, 49f); // proportions of the tag in the Ginger design
            Image tagImage = tag.AddComponent<Image>();
            tagImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CategoryTagPath);
            tagImage.raycastTarget = false;
            parts.CategoryTag = tag;
            parts.CategoryLabel = NewText("CategoryLabel", tag.transform, fonts.Title, 20f, FontStyles.Normal,
                TextAlignmentOptions.Center, TagInk, wrap: false);
            Stretch((RectTransform)parts.CategoryLabel.transform);
            parts.CategoryLabel.enableAutoSizing = true;
            parts.CategoryLabel.fontSizeMin = 12f;
            parts.CategoryLabel.fontSizeMax = 20f;

            // ---- Contents page: same structure as the old Contents Layout ------------------
            GameObject contents = NewUi("ContentsLayout", content);
            Stretch((RectTransform)contents.transform);
            AddVertical(contents, spacing: 8f, TextAnchor.UpperLeft);
            parts.ContentsLayout = contents;

            parts.ContentsTitle = NewText("ContentsTitle", contents.transform, fonts.Title, 44f, FontStyles.Normal,
                TextAlignmentOptions.Center, Ink, wrap: false);
            AddSpacer("ContentsSpacer", contents.transform, 14f);
            TMP_Text heading = NewText("HerbElementHeading", contents.transform, fonts.Title, 30f, FontStyles.Normal,
                TextAlignmentOptions.Left, Ink, wrap: false);
            heading.text = "Herb Element";

            for (int index = 0; index < ContentsRowCount; index++)
            {
                BuildContentsRow(parts, contents.transform, fonts, index + 1);
            }

            AddSpacer("ContentsSpacer2", contents.transform, 10f);

            // ---- Plain text page (fallback for pages that are neither herb nor contents) ----
            parts.PlainText = NewText("PlainText", content, fonts.Heading, 23f, FontStyles.Bold,
                TextAlignmentOptions.Top, PlainInk, wrap: true);
            Stretch((RectTransform)parts.PlainText.transform);
            parts.PlainText.enableAutoSizing = true;
            parts.PlainText.fontSizeMin = 2f;
            parts.PlainText.fontSizeMax = 30f;
            parts.PlainText.lineSpacing = 20f;

            return parts;
        }

        private static void BuildContentsRow(PageParts parts, Transform parent, Fonts fonts, int number)
        {
            GameObject row = NewUi("Row " + number, parent);
            AddVertical(row, spacing: 2f, TextAnchor.UpperLeft);
            Image hitArea = row.AddComponent<Image>();
            hitArea.color = Color.clear; // invisible, but makes the whole row clickable
            // A culled (fully transparent) mesh is skipped by the GraphicRaycaster, so keep it.
            row.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
            Button button = row.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            GameObject nameHolder = NewUi("NameHolder", row.transform);
            HorizontalLayoutGroup nameLayout = nameHolder.AddComponent<HorizontalLayoutGroup>();
            nameLayout.childAlignment = TextAnchor.UpperLeft;
            nameLayout.childControlWidth = true;
            nameLayout.childControlHeight = true;
            nameLayout.childForceExpandWidth = false;
            nameLayout.childForceExpandHeight = false;

            TMP_Text name = NewText("Name", nameHolder.transform, fonts.Title, 21f, FontStyles.Normal,
                TextAlignmentOptions.Left, Ink, wrap: false);

            GameObject underline = NewUi("Underline", name.transform);
            var underlineRect = (RectTransform)underline.transform;
            underlineRect.anchorMin = Vector2.zero;
            underlineRect.anchorMax = new Vector2(1f, 0f);
            underlineRect.pivot = new Vector2(0.5f, 0f);
            underlineRect.anchoredPosition = new Vector2(0f, -1f);
            underlineRect.sizeDelta = new Vector2(0f, 2f);
            Image underlineImage = underline.AddComponent<Image>();
            underlineImage.color = Ink;
            underlineImage.raycastTarget = false;

            TMP_Text description = NewText("Description", row.transform, fonts.Body, 19f, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, Ink, wrap: true);

            parts.Rows.Add(row);
            parts.RowNames.Add(name);
            parts.RowDescriptions.Add(description);
            parts.RowButtons.Add(button);
        }

        private static void WireView(GameObject root, PageParts parts)
        {
            GuidebookPageView view = root.GetComponent<GuidebookPageView>();
            if (view == null) view = root.AddComponent<GuidebookPageView>();

            var serialized = new SerializedObject(view);
            Set(serialized, "plainText", parts.PlainText);
            Set(serialized, "illustration", parts.Illustration);
            Set(serialized, "textLayout", null);
            Set(serialized, "herbLayout", parts.HerbLayout);
            Set(serialized, "contentsLayout", parts.ContentsLayout);
            Set(serialized, "title", parts.Title);
            Set(serialized, "scientificName", parts.ScientificName);
            Set(serialized, "description", parts.Description);
            Set(serialized, "warning", parts.Warning);
            Set(serialized, "categoryTag", parts.CategoryTag);
            Set(serialized, "categoryLabel", parts.CategoryLabel);
            Set(serialized, "contentsTitle", parts.ContentsTitle);
            SetArray(serialized, "contentsRows", parts.Rows);
            SetArray(serialized, "contentsRowNames", parts.RowNames);
            SetArray(serialized, "contentsRowDescriptions", parts.RowDescriptions);
            SetArray(serialized, "contentsRowButtons", parts.RowButtons);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Edit-time preview with real content; the game overwrites all of it when a page renders.
        private static void FillPreview(PageParts parts)
        {
            Dictionary<string, string> categoryNames = new Dictionary<string, string>();
            List<HerbCategoryDefinition> categories = AssetDatabase
                .FindAssets("t:" + nameof(HerbCategoryDefinition), new[] { CategoryContentFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<HerbCategoryDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(category => category != null)
                .OrderBy(category => category.DisplayName, System.StringComparer.Ordinal)
                .ToList();
            foreach (HerbCategoryDefinition category in categories)
            {
                categoryNames[category.StableId] = category.DisplayName;
            }

            var herb = AssetDatabase.LoadAssetAtPath<HerbDefinition>(PreviewHerbPath);
            if (herb != null)
            {
                parts.Title.text = herb.DisplayName;
                parts.ScientificName.text = herb.ScientificName;
                parts.Description.text = herb.TreatmentText;
                parts.Warning.text = herb.WarningText;
                parts.Illustration.sprite = herb.HerbIllustration;
                parts.CategoryLabel.text = categoryNames.TryGetValue(herb.CategoryId ?? string.Empty, out string label)
                    ? label
                    : string.Empty;
            }

            parts.ContentsTitle.text = "Content";
            for (int index = 0; index < parts.Rows.Count; index++)
            {
                bool used = index < categories.Count;
                parts.Rows[index].SetActive(used);
                if (!used) continue;
                parts.RowNames[index].text = categories[index].DisplayName;
                parts.RowDescriptions[index].text = categories[index].Description;
            }

            parts.PlainText.text = string.Empty;
            parts.HerbLayout.SetActive(true);
            parts.CategoryTag.SetActive(!string.IsNullOrEmpty(parts.CategoryLabel.text));
            parts.ContentsLayout.SetActive(false);
            parts.PlainText.gameObject.SetActive(false);
        }

        private static TMP_FontAsset EnsureFontAsset(string fontPath)
        {
            string fontAssetPath = Path.Combine(
                    Path.GetDirectoryName(fontPath) ?? "Assets",
                    Path.GetFileNameWithoutExtension(fontPath) + " SDF.asset")
                .Replace('\\', '/');
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError("Font not found: " + fontPath);
                return null;
            }

            // Same as right-clicking the font → Create → TextMeshPro → Font Asset → SDF.
            Object[] previousSelection = Selection.objects;
            Selection.objects = new Object[] { font };
            EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset/SDF");
            Selection.objects = previousSelection;

            var created = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (created == null) Debug.LogError("Could not create TMP font asset for " + fontPath);
            return created;
        }

        private static GuidebookPageView FindPage(GameObject panel, string pageName, string parentName)
        {
            return panel.GetComponentsInChildren<GuidebookPageView>(true)
                .FirstOrDefault(view => view.gameObject.name == pageName && view.transform.parent.name == parentName);
        }

        private static Button FindButton(GameObject panel, string buttonName)
        {
            return panel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.gameObject.name == buttonName);
        }

        private static TMP_Text NewText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color,
            bool wrap)
        {
            GameObject gameObject = NewUi(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = name;
            return text;
        }

        private static VerticalLayoutGroup AddVertical(GameObject target, float spacing, TextAnchor alignment)
        {
            VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static void AddSpacer(string name, Transform parent, float height)
        {
            GameObject spacer = NewUi(name, parent);
            spacer.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private static GameObject NewUi(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void Set(SerializedObject serialized, string field, Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError("GuidebookPageView has no field named " + field);
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedObject serialized, string field, IReadOnlyList<T> values) where T : Object
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError("GuidebookPageView has no field named " + field);
                return;
            }

            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void Fail(string message)
        {
            EditorUtility.DisplayDialog("Guidebook", message, "OK");
        }
    }
}
