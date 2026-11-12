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
            new ColumnRule { cardCount = 16, columns = 8 }
        };
        [SerializeField] private float verticalSpacing = 14f;
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
            int columns = ColumnsFor(cardCount);
            int rows = 2;

            float availableWidth = rect.width - grid.padding.left - grid.padding.right;
            float availableHeight = rect.height - grid.padding.top - grid.padding.bottom;

            float minimumSpacing = 16f;

            float maxWidthByHorizontal =
                (availableWidth - minimumSpacing * (columns - 1)) / columns;

            float maxWidthByVertical =
                ((availableHeight - verticalSpacing * (rows - 1)) / rows) * cardAspect;

            float width = Mathf.Min(
                maxWidthByHorizontal,
                maxWidthByVertical,
                maxCardWidth
            );

            width = Mathf.Max(width, 1f);

            float horizontalSpacing = columns switch
            {
                3 => 40f,
                4 => 32f,
                6 => 22f,
                8 => 14f,
                _ => 20f
            };

            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.spacing = new Vector2(horizontalSpacing, verticalSpacing);
            grid.cellSize = new Vector2(width, width / cardAspect);
        }

        private int ColumnsFor(int cardCount)
        {
            foreach (ColumnRule rule in columnRules)
            {
                if (rule.cardCount == cardCount)
                {
                    return rule.columns;
                }
            }

            return Mathf.CeilToInt(cardCount / 2f);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (lastCardCount > 0 && isActiveAndEnabled) Apply(lastCardCount, false);
        }
    }
}