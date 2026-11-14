using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Witch.Editor
{
    /// <summary>
    /// Builds the guidebook prefabs once, at edit time. After that the prefabs are
    /// edited by hand like any other prefab; run a menu again only to start over.
    /// Nothing here runs in the game.
    /// </summary>
    public static class GuidebookPrefabBuilder
    {
        private const string BlankPagePath = "Assets/Art/Guidebook/BlankPageNew.png";
        private const string PanelControllerPath = "Assets/Art/Guidebook/Guidebook Panel.controller";
        private const string Folder = "Assets/Prefabs/Guidebook";
        private const string PagePrefabPath = Folder + "/GuidebookPage.prefab";
        private const string BookPrefabPath = Folder + "/GuidebookBook.prefab";
        private const string PanelPrefabPath = Folder + "/GuidebookPanel.prefab";

        // Same ratio as BlankPageNew (1414 x 2000); fits a 1920 x 1080 reference canvas.
        private static readonly Vector2 PageSize = new Vector2(566f, 800f);

        // Safe area inside the floral border, as fractions of the page.
        private const float ContentInsetSide = 0.14f;
        private const float ContentInsetTop = 0.12f;
        private const float ContentInsetBottom = 0.11f;

        // Copied from the old "Guidebook Panel" in Shop.unity so the panel behaves the same.
        // The open/close clips find these objects by name, so the names must stay exactly like this.
        private const string OpenGroupName = "Book Open Group";
        private const string BackdropName = "Read Backdrop";
        private static readonly Vector2 PanelSize = new Vector2(1244f, 886f);
        private static readonly Vector2 BackdropSize = new Vector2(2000f, 1200f);
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.87058824f);

        [MenuItem("Witch/Guidebook/Build Blank Book Prefabs")]
        public static void BuildBook()
        {
            Sprite blankPage = AssetDatabase.LoadAllAssetsAtPath(BlankPagePath).OfType<Sprite>().FirstOrDefault();
            if (blankPage == null)
            {
                Fail("Sprite not found: " + BlankPagePath + "\nCheck that its Texture Type is Sprite (2D and UI).");
                return;
            }

            if (!ConfirmReplace(PagePrefabPath, BookPrefabPath)) return;
            EnsureFolder();

            GameObject pagePrefab = BuildPagePrefab(blankPage);
            BuildBookPrefab(pagePrefab);
            Finish(BookPrefabPath);
        }

        [MenuItem("Witch/Guidebook/Build Guidebook Panel Prefab")]
        public static void BuildPanel()
        {
            var bookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BookPrefabPath);
            if (bookPrefab == null)
            {
                Fail("Run \"Build Blank Book Prefabs\" first: " + BookPrefabPath + " is missing.");
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PanelControllerPath);
            if (controller == null)
            {
                Fail("Animator controller not found: " + PanelControllerPath);
                return;
            }

            if (!ConfirmReplace(PanelPrefabPath)) return;
            EnsureFolder();

            GameObject root = NewUi("GuidebookPanel", null);
            ((RectTransform)root.transform).sizeDelta = PanelSize;
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            // Faded in by "Open" (BookOpening clip), hidden by "Close" (BookClosed clip).
            // Must start visible: every state writes defaults and the BookOpen clip is empty,
            // so once the fade-in ends the group snaps back to these starting values.
            GameObject group = NewUi(OpenGroupName, root.transform);
            Stretch((RectTransform)group.transform);
            CanvasGroup canvasGroup = group.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // Dark full-screen layer behind the book. Clicking it closes the guidebook.
            GameObject backdrop = NewUi(BackdropName, group.transform);
            ((RectTransform)backdrop.transform).sizeDelta = BackdropSize;
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = BackdropColor;
            backdropImage.raycastTarget = true;
            Button backdropButton = backdrop.AddComponent<Button>();
            backdropButton.targetGraphic = backdropImage;
            UnityEventTools.AddStringPersistentListener(
                backdropButton.onClick,
                new UnityAction<string>(animator.SetTrigger),
                "Close");

            // The book sits on top of the backdrop, so clicks on the pages do not close it.
            var book = (GameObject)PrefabUtility.InstantiatePrefab(bookPrefab, group.transform);
            book.name = "GuidebookBook";

            PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
            Object.DestroyImmediate(root);
            Finish(PanelPrefabPath);
        }

        private static GameObject BuildPagePrefab(Sprite blankPage)
        {
            GameObject root = NewUi("GuidebookPage", null);
            ((RectTransform)root.transform).sizeDelta = PageSize;

            Image paper = root.AddComponent<Image>();
            paper.sprite = blankPage;
            paper.raycastTarget = false;

            // Herb / Contents layouts go inside this later, so they never cover the border.
            GameObject content = NewUi("Content", root.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(ContentInsetSide, ContentInsetBottom);
            contentRect.anchorMax = new Vector2(1f - ContentInsetSide, 1f - ContentInsetTop);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PagePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildBookPrefab(GameObject pagePrefab)
        {
            GameObject root = NewUi("GuidebookBook", null);
            ((RectTransform)root.transform).sizeDelta = new Vector2(PageSize.x * 2f, PageSize.y);

            // Invisible click area under the whole book, grown by Raycast Padding to also cover the
            // book cover around the pages. Clicks here never reach the Read Backdrop behind the book,
            // which would close the guidebook. It must not be culled, or the raycaster skips it.
            Image clickArea = root.AddComponent<Image>();
            clickArea.color = Color.clear;
            clickArea.raycastPadding = new Vector4(-60f, -40f, -60f, -45f); // left, bottom, right, top
            root.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            PlacePage(pagePrefab, root.transform, "LeftPage", new Vector2(-PageSize.x / 2f, 0f));
            PlacePage(pagePrefab, root.transform, "RightPage", new Vector2(PageSize.x / 2f, 0f));

            // Prepared for the page-turn animation; stays hidden until PageFlipAnimator is added.
            GameObject clip = NewUi("TurnClip", root.transform);
            ((RectTransform)clip.transform).sizeDelta = new Vector2(PageSize.x * 2f + PageSize.y, PageSize.y * 3f);
            Image clipImage = clip.AddComponent<Image>();
            clipImage.raycastTarget = false;
            clip.AddComponent<Mask>().showMaskGraphic = false;
            PlacePage(pagePrefab, clip.transform, "FlipFront", Vector2.zero);
            PlacePage(pagePrefab, clip.transform, "FlipBack", Vector2.zero);
            clip.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, BookPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void PlacePage(GameObject pagePrefab, Transform parent, string name, Vector2 position)
        {
            var page = (GameObject)PrefabUtility.InstantiatePrefab(pagePrefab, parent);
            page.name = name;
            ((RectTransform)page.transform).anchoredPosition = position;
        }

        private static GameObject NewUi(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            if (parent != null) gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool ConfirmReplace(params string[] paths)
        {
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                return EditorUtility.DisplayDialog(
                    "Guidebook",
                    "Some of these prefabs already exist. Replace them? Changes you made to them will be lost.\n\n"
                        + string.Join("\n", paths),
                    "Replace",
                    "Cancel");
            }

            return true;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Guidebook");
        }

        private static void Finish(string selectPath)
        {
            AssetDatabase.SaveAssets();
            Object asset = AssetDatabase.LoadAssetAtPath<GameObject>(selectPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void Fail(string message)
        {
            EditorUtility.DisplayDialog("Guidebook", message, "OK");
        }
    }
}
