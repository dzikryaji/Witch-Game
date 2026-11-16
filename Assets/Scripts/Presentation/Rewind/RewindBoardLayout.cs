
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Witch.Presentation.Rewind
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class RewindBoardLayout : MonoBehaviour
    {
        [Serializable]
        private struct RowRule
        {
            public int cardCount;
            public int topRow;
            public int middleRow;
            public int bottomRow;
        }

        [SerializeField]
        private RowRule[] rowRules =
        {
            new RowRule { cardCount = 16, topRow = 5, middleRow = 6, bottomRow = 5 },
            new RowRule { cardCount = 18, topRow = 6, middleRow = 6, bottomRow = 6 },
            new RowRule { cardCount = 20, topRow = 6, middleRow = 8, bottomRow = 6 },
            new RowRule { cardCount = 22, topRow = 7, middleRow = 8, bottomRow = 7 },
            new RowRule { cardCount = 24, topRow = 8, middleRow = 8, bottomRow = 8 },
        };

        [SerializeField] private float verticalSpacing = 14f;
        [SerializeField] private float horizontalSpacing = 14f;
        [SerializeField, Range(0.4f, 1f)]
        private float cardAspect = 5f / 7f;

        // [SerializeField, Min(40f)]
        // private float maxCardWidth = 180f;
        [SerializeField, Min(40f)]
        private float maxCardWidth = 150f;

        private GridLayoutGroup grid;
        private int lastCardCount;

        public void Apply(int cardCount, bool refreshCanvas = true)
        {
            lastCardCount = cardCount;

            if (cardCount <= 0) return;

            if (grid == null)
                grid = GetComponent<GridLayoutGroup>();

            if (refreshCanvas)
                Canvas.ForceUpdateCanvases();

            RowRule rule = RowsFor(cardCount);

            int[] rowCounts =
            {
                rule.topRow,
                rule.middleRow,
                rule.bottomRow
            };

            int maxColumns = Mathf.Max(
                rule.topRow,
                rule.middleRow,
                rule.bottomRow
            );

            if (rule.topRow + rule.middleRow + rule.bottomRow != cardCount)
            {
                throw new InvalidOperationException(
                    $"Invalid row layout for {cardCount} cards.");
            }

            RectTransform boardRect = (RectTransform)transform;
            Rect rect = boardRect.rect;

            float availableWidth =
                rect.width - grid.padding.left - grid.padding.right;

            float availableHeight =
                rect.height - grid.padding.top - grid.padding.bottom;

            float maxWidthByHorizontal =
                (availableWidth - horizontalSpacing * (maxColumns - 1))
                / maxColumns;

            float maxWidthByVertical =
                ((availableHeight - verticalSpacing * 2f) / 3f)
                * cardAspect;

            float width = Mathf.Min(
                maxWidthByHorizontal,
                maxWidthByVertical,
                maxCardWidth
            );

            width = Mathf.Max(width, 1f);

            float height = width / cardAspect;

            // Disable GridLayoutGroup because we position each card manually.
            grid.enabled = false;

            if (transform.childCount < cardCount)
            {
                throw new InvalidOperationException(
                    $"Board requires {cardCount} card children, " +
                    $"but only {transform.childCount} were found.");
            }

            float centerX =
                (grid.padding.left - grid.padding.right) / 2f;

            float centerY =
                (grid.padding.bottom - grid.padding.top) / 2f;

            float totalHeight =
                height * 3f + verticalSpacing * 2f;

            float[] rowY =
            {
                centerY + totalHeight / 2f - height / 2f,
                centerY,
                centerY - totalHeight / 2f + height / 2f
            };

            int cardIndex = 0;

            for (int row = 0; row < 3; row++)
            {
                int cardsInRow = rowCounts[row];

                float rowWidth =
                    cardsInRow * width +
                    (cardsInRow - 1) * horizontalSpacing;

                float startX = centerX - rowWidth / 2f + width / 2f;

                for (int column = 0; column < cardsInRow; column++)
                {
                    RectTransform card =
                        transform.GetChild(cardIndex).GetComponent<RectTransform>();

                    if (card == null)
                    {
                        throw new InvalidOperationException(
                            $"Card child {cardIndex} has no RectTransform.");
                    }

                    card.anchorMin = new Vector2(0.5f, 0.5f);
                    card.anchorMax = new Vector2(0.5f, 0.5f);
                    card.pivot = new Vector2(0.5f, 0.5f);

                    card.sizeDelta = new Vector2(width, height);

                    card.anchoredPosition = new Vector2(
                        startX + column * (width + horizontalSpacing),
                        rowY[row]
                    );

                    cardIndex++;
                }
            }
        }

        private RowRule RowsFor(int cardCount)
        {
            foreach (RowRule rule in rowRules)
            {
                if (rule.cardCount == cardCount)
                    return rule;
            }

            throw new InvalidOperationException(
                $"No row layout configured for {cardCount} cards.");
        }

        private void OnRectTransformDimensionsChange()
        {
            if (lastCardCount > 0 && isActiveAndEnabled)
                Apply(lastCardCount, false);
        }
    }
}
