using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Level
{
    public class BuildingGridManager : MonoBehaviour
    {
        public int Width = 8;
        public int Height = 8;
        public GameObject BuildingPrefab;
        public Transform GridParent;

        public Sprite[] BuildingSprites;
        public Sprite[] BuildingSpritesHighlighted;

        public BuildingInputHandler BuildingInputHandler;

        private Building[,] _buildings;

        private void Start()
        {
            GenerateGrid();
        }

        public void GenerateGrid()
        {
            _buildings = new Building[Width, Height];
            foreach (Transform child in GridParent)
                Destroy(child.gameObject);

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var BuildingGO = Instantiate(BuildingPrefab, GridParent);
                    BuildingGO.SetActive(true);
                    Building Building = BuildingGO.GetComponent<Building>();
                    var color = (BuildingColor)Random.Range(0, BuildingSprites.Length);

                    Sprite normal = BuildingSprites[(int)color];
                    Sprite highlighted = (BuildingSpritesHighlighted != null && BuildingSpritesHighlighted.Length > (int)color)
                        ? BuildingSpritesHighlighted[(int)color]
                        : BuildingSprites[(int)color];

                    Building.SetColor(color, normal, highlighted);
                    Building.SetGridPosition(new Vector2Int(x, y));
                    _buildings[x, y] = Building;
                }
        }

        public void RemoveBuildings(List<Building> toRemove)
        {
            foreach (var Building in toRemove)
            {
                var color = (BuildingColor)Random.Range(0, BuildingSprites.Length);
                Sprite normal = BuildingSprites[(int)color];
                Sprite highlighted = (BuildingSpritesHighlighted != null && BuildingSpritesHighlighted.Length > (int)color)
                    ? BuildingSpritesHighlighted[(int)color]
                    : BuildingSprites[(int)color];

                Building.SetColor(color, normal, highlighted);
                Building.SetHighlighted(false);
            }
        }

        public void CollapseAndRefill()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    if (_buildings[x, y] != null)
                        _buildings[x, y].SetGridPosition(new Vector2Int(x, y));
                }
        }
    }

    public enum BuildingColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        Orange,
        Purple,
        Pink
    }
}