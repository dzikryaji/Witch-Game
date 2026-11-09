using System;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Rewind
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class RewindBoardLayout : MonoBehaviour
    {
        [Serializable]
        private struct ColumnRule
        {
            public int cardCount;
            public int columns;
        }

        [SerializeField]
        private ColumnRule[] columnRules =
        {
            new ColumnRule { cardCount = 6, columns = 3 }, 
            new ColumnRule { cardCount = 8, columns = 4 },  
            new ColumnRule { cardCount = 12, columns = 6 },  
            new ColumnRule { cardCount = 16, columns = 8 },  
        };
        [SerializeField] private Vector2 spacing = new Vector2(14f, 14f);
        [SerializeField, Range(0.4f, 1f)] private float cardAspect = 5f / 7f;   // width / height
        [SerializeField, Min(40f)] private float maxCardWidth = 180f;

        private GridLayoutGroup grid;
        private int lastCardCount;

        public void Apply(int cardCount, bool refreshCanvas = true)
        {
            lastCardCount = cardCount;
            if (cardCount <= 0) return;

            if (grid == null) grid = GetComponent<GridLayoutGroup>();
            if (refreshCanvas) Canvas.ForceUpdateCanvases();

            Rect rect = ((RectTransform)transform).rect;
            int columns = Mathf.Clamp(ColumnsFor(cardCount), 1, cardCount);
            int rows = Mathf.CeilToInt(cardCount / (float)columns);

            float availableWidth = rect.width - grid.padding.left - grid.padding.right - spacing.x * (columns - 1);
            float availableHeight = rect.height - grid.padding.top - grid.padding.bottom - spacing.y * (rows - 1);

            float width = Mathf.Min(availableWidth / columns, availableHeight / rows * cardAspect, maxCardWidth);
            width = Mathf.Max(width, 1f);

            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.spacing = spacing;
            grid.cellSize = new Vector2(width, width / cardAspect);
        }

        private int ColumnsFor(int cardCount)
        {
            foreach (ColumnRule rule in columnRules)
            {
                if (rule.cardCount == cardCount) return rule.columns;
            }
            return Mathf.CeilToInt(cardCount / 2f); // fallback: two rows
        }

        // Re-fit if the window or canvas size changes.
        private void OnRectTransformDimensionsChange()
        {
            if (lastCardCount > 0 && isActiveAndEnabled) Apply(lastCardCount, false);
        }
    }
}