using UnityEngine;
using UnityEngine.UI;

namespace GameCore.Level
{
    [RequireComponent(typeof(Image))]
    public class Building : MonoBehaviour
    {
        public BuildingColor Color;
        public Image BuildingImage;
        [HideInInspector] public Vector2Int GridPosition;

        private Sprite normalSprite;
        private Sprite highlightedSprite;

        public void SetColor(BuildingColor col, Sprite normal, Sprite highlighted)
        {
            Color = col;
            if (BuildingImage == null)
                BuildingImage = GetComponent<Image>();

            normalSprite = normal;
            highlightedSprite = highlighted;

            BuildingImage.sprite = normalSprite;
        }

        public void SetGridPosition(Vector2Int pos)
        {
            GridPosition = pos;
        }

        public void SetHighlighted(bool highlighted)
        {
            if (BuildingImage == null)
                BuildingImage = GetComponent<Image>();

            BuildingImage.sprite = highlighted ? (highlightedSprite ?? normalSprite) : (normalSprite ?? highlightedSprite);
        }
    }
}