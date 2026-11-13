#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Witch.Presentation.Shop;

namespace Witch.Editor
{
    public static class ShopTutorialMaskTools
    {
        [MenuItem("Witch/Tutorial/Wire Step Masks")]
        public static void WireStepMasks()
        {
            TutorialMaskView view = FindMaskView();
            if (view == null)
            {
                Debug.LogError("TutorialMaskView not found in open scenes.");
                return;
            }

            Transform root = view.transform;
            Transform stepHoles = root.Find("StepHoles");
            if (stepHoles == null)
            {
                Debug.LogError("StepHoles container not found under Guided Tutorial Mask.");
                return;
            }

            var list = new List<TutorialStepMask>();
            var missing = new List<string>();
            foreach (ShopGuidedTutorialStep step in Enum.GetValues(typeof(ShopGuidedTutorialStep)))
            {
                string name = "Hole_" + step;
                Transform t = stepHoles.Find(name);
                if (t == null)
                {
                    missing.Add(step + " (missing " + name + ")");
                    continue;
                }
                TutorialStepMask mask = t.GetComponent<TutorialStepMask>();
                if (mask == null) mask = Undo.AddComponent<TutorialStepMask>(t.gameObject);
                mask.EditorSetStep(step);
                list.Add(mask);
                if (!mask.HasHoleRect()) missing.Add(step + " (no holeRects)");
            }

            Undo.RecordObject(view, "Wire Step Masks");
            view.EditorSetStepMasks(list.ToArray());
            EditorUtility.SetDirty(view);
            if (stepHoles != null) EditorUtility.SetDirty(stepHoles.gameObject);
            Debug.Log("Wired " + list.Count + " step masks." + (missing.Count > 0 ? " Missing: " + string.Join(", ", missing) : ""));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }

        [MenuItem("Witch/Tutorial/Bake Step Mask Panels")]
        public static void BakeStepMaskPanels()
        {
            TutorialMaskView view = FindMaskView();
            if (view == null)
            {
                Debug.LogError("TutorialMaskView not found in open scenes.");
                return;
            }
            RectTransform overlay = view.GetComponent<RectTransform>();
            if (overlay == null) { Debug.LogError("TutorialMaskView has no RectTransform."); return; }

            foreach (var mask in view.Masks)
            {
                if (mask == null) continue;
                BakeMask(mask, overlay);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Debug.Log("Baked step mask panels.");
        }

        private static void BakeMask(TutorialStepMask mask, RectTransform overlay)
        {
            RectTransform[] holes = mask.HoleRects;
            if (holes == null || holes.Length == 0) return;
            if (!TryGetUnionBounds(overlay, holes, out float minX, out float minY, out float maxX, out float maxY)) return;
            Rect r = overlay.rect;

            Image top = GetOrCreatePanel(mask.transform, "DimTop");
            Image bottom = GetOrCreatePanel(mask.transform, "DimBottom");
            Image left = GetOrCreatePanel(mask.transform, "DimLeft");
            Image right = GetOrCreatePanel(mask.transform, "DimRight");

            // anchors stretch full overlay, offsets in local overlay space
            SetPanel(top.rectTransform, 0, 1, 0, 1, r.xMin, maxY, r.xMax, r.yMax);
            SetPanel(bottom.rectTransform, 0, 1, 0, 1, r.xMin, r.yMin, r.xMax, minY);
            SetPanel(left.rectTransform, 0, 1, 0, 1, r.xMin, minY, minX, maxY);
            SetPanel(right.rectTransform, 0, 1, 0, 1, maxX, minY, r.xMax, maxY);

            EditorUtility.SetDirty(mask);
        }

        private static bool TryGetUnionBounds(RectTransform overlay, RectTransform[] targets, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue; maxX = maxY = float.MinValue;
            bool any = false;
            var corners = new Vector3[4];
            for (int i = 0; i < targets.Length; i++)
            {
                RectTransform t = targets[i];
                if (t == null) continue;
                t.GetWorldCorners(corners);
                for (int c = 0; c < 4; c++)
                {
                    Vector3 local = overlay.InverseTransformPoint(corners[c]);
                    if (local.x < minX) minX = local.x;
                    if (local.y < minY) minY = local.y;
                    if (local.x > maxX) maxX = local.x;
                    if (local.y > maxY) maxY = local.y;
                }
                any = true;
            }
            return any;
        }

        private static void SetPanel(RectTransform rt, float axMin, float axMax, float ayMin, float ayMax, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(axMin, ayMin);
            rt.anchorMax = new Vector2(axMax, ayMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // offsets relative to anchors spanning whole parent: offsetMin=(left,bottom), offsetMax=(right,top)
            rt.offsetMin = new Vector2(xMin, yMin);
            rt.offsetMax = new Vector2(xMax, yMax);
        }

        private static Image GetOrCreatePanel(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, "Create Dim Panel");
            }
            else go = t.gameObject;
            var img = go.GetComponent<Image>();
            if (img == null) img = Undo.AddComponent<Image>(go);
            img.raycastTarget = false;
            if (img.color.a <= 0f) img.color = new Color(0, 0, 0, 0.72f);
            return img;
        }

        private static TutorialMaskView FindMaskView()
        {
            var views = Resources.FindObjectsOfTypeAll<TutorialMaskView>();
            for (int i = 0; i < views.Length; i++)
            {
                if (!EditorUtility.IsPersistent(views[i])) return views[i];
            }
            return null;
        }
    }
}
#endif
