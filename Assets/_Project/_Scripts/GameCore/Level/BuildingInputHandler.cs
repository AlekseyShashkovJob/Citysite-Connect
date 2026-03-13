using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Misc.Services;

namespace GameCore.Level
{
    public class BuildingInputHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public BuildingGridManager GridManager;
        public Core.GameManager GameManager;
        public GraphicRaycaster Raycaster;

        public Sprite[] LineSprites;
        public GameObject LineImagePrefab;
        public Transform LineLayerParent;

        private readonly List<GameObject> _activeLineImages = new List<GameObject>();
        private readonly List<Building> _selectedBuilding = new List<Building>();
        private BuildingColor? _currentColor = null;
        private bool _isDragging = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
            foreach (var e in _selectedBuilding)
                e.SetHighlighted(false);

            _selectedBuilding.Clear();
            _currentColor = null;
            TrySelectBuildingUnderPointer(eventData);
            UpdateChainLines();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            TrySelectBuildingUnderPointer(eventData);
            UpdateChainLines();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
            TrySelectBuildingUnderPointer(eventData);

            if (_selectedBuilding.Count >= 2)
            {
                foreach (var e in _selectedBuilding)
                    e.SetHighlighted(false);

                GameManager.CollectBuildings(new List<Building>(_selectedBuilding));
            }
            else
            {
                foreach (var e in _selectedBuilding)
                    e.SetHighlighted(false);
            }

            _selectedBuilding.Clear();
            _currentColor = null;
            UpdateChainLines();
        }

        private void TrySelectBuildingUnderPointer(PointerEventData eventData)
        {
            Building Building = RaycastBuilding(eventData);
            if (Building == null) return;

            if (_selectedBuilding.Count >= 2 && _selectedBuilding[_selectedBuilding.Count - 2] == Building)
            {
                var last = _selectedBuilding[_selectedBuilding.Count - 1];
                last.SetHighlighted(false);

                _selectedBuilding.RemoveAt(_selectedBuilding        .Count - 1);
                UpdateChainLines();
                return;
            }

            if (_selectedBuilding.Contains(Building))
                return;

            if (_selectedBuilding.Count == 0)
            {
                _currentColor = Building.Color;
                _selectedBuilding.Add(Building);

                VibroManager.Vibrate();
                Building.SetHighlighted(true);
            }
            else
            {
                Building lastBuilding = _selectedBuilding[_selectedBuilding.Count - 1];
                if (Building.Color == _currentColor && IsAdjacent(Building, lastBuilding))
                {
                    _selectedBuilding.Add(Building);

                    VibroManager.Vibrate();
                    Building.SetHighlighted(true);
                }
            }
            UpdateChainLines();
        }

        private bool IsAdjacent(Building a, Building b)
        {
            var diff = a.GridPosition - b.GridPosition;
            return (Mathf.Abs(diff.x) == 1 && diff.y == 0) || (Mathf.Abs(diff.y) == 1 && diff.x == 0);
        }

        private Building RaycastBuilding(PointerEventData eventData)
        {
            var results = new List<RaycastResult>();
            Raycaster.Raycast(eventData, results);
            foreach (var r in results)
            {
                var Building = r.gameObject.GetComponent<Building>();
                if (Building != null)
                    return Building;
            }
            return null;
        }

        private void UpdateChainLines()
        {
            foreach (var go in _activeLineImages)
            {
                if (go != null)
                    Destroy(go);
            }
            _activeLineImages.Clear();

            if (_selectedBuilding.Count < 2)
                return;

            int colorIndex = _currentColor.HasValue ? (int)_currentColor.Value : 0;
            Sprite lineSprite = LineSprites.Length > colorIndex ? LineSprites[colorIndex] : null;

            for (int i = 0; i < _selectedBuilding.Count - 1; i++)
            {
                Building a = _selectedBuilding[i];
                Building b = _selectedBuilding[i + 1];

                Vector3 posA = a.transform.position;
                Vector3 posB = b.transform.position;

                GameObject lineGO = Instantiate(LineImagePrefab, LineLayerParent != null ? LineLayerParent : a.transform.parent);
                Image img = lineGO.GetComponent<Image>();
                if (img != null && lineSprite != null)
                    img.sprite = lineSprite;

                RectTransform rt = lineGO.GetComponent<RectTransform>();
                rt.position = (posA + posB) / 2f;

                Vector2 dir = (posB - posA).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                rt.rotation = Quaternion.Euler(0, 0, angle);

                float dist = Vector2.Distance(posA, posB);
                rt.sizeDelta = new Vector2(dist, rt.sizeDelta.y);

                _activeLineImages.Add(lineGO);
            }
        }
    }
}