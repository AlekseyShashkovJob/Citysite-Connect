using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public class GridAutoSizer : MonoBehaviour
    {
        public int columns = 6;
        public int rows = 8;
        public Vector2 cellPadding = Vector2.zero;

        private RectTransform rectTransform;
        private GridLayoutGroup grid;

        void Start()
        {
            grid = GetComponent<GridLayoutGroup>();
            rectTransform = GetComponent<RectTransform>();
            UpdateCellSize();
        }

        void OnRectTransformDimensionsChange()
        {
            if (grid == null || rectTransform == null) return;
            UpdateCellSize();
        }

        void UpdateCellSize()
        {
            float width = rectTransform.rect.width - grid.padding.left - grid.padding.right - (grid.spacing.x * (columns - 1));
            float height = rectTransform.rect.height - grid.padding.top - grid.padding.bottom - (grid.spacing.y * (rows - 1));
            float cellWidth = width / columns;
            float cellHeight = height / rows;

            grid.cellSize = new Vector2(cellWidth - cellPadding.x, cellHeight - cellPadding.y);
        }
    }
}