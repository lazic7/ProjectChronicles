using IsometricPathfinding.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace IsometricPathfinding.Input
{
    [DisallowMultipleComponent]
    public sealed class MouseTileSelector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        private Camera worldCamera;

        [SerializeField]
        private Tilemap groundTilemap;

        [SerializeField]
        private Tilemap obstaclesTilemap;

        [SerializeField]
        private TileHoverPreview hoverPreview;

        [Header("Runtime State")]
        [SerializeField]
        private bool hasHoveredCell;

        [SerializeField]
        private Vector2Int hoveredCell;

        [SerializeField]
        private bool hoveredCellIsWalkable;

        [Header("Debug")]
        [SerializeField]
        private bool logCellChanges = true;

        private Vector3Int lastCheckedCell;
        private bool hasLastCheckedCell;

        public bool HasHoveredCell => hasHoveredCell;

        public Vector2Int HoveredCell => hoveredCell;

        public bool HoveredCellIsWalkable => hoveredCellIsWalkable;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (!TryGetCellUnderMouse(out Vector3Int cell))
            {
                ClearHover();

                hasLastCheckedCell = false;
                return;
            }

            if (hasLastCheckedCell && cell == lastCheckedCell)
            {
                return;
            }

            lastCheckedCell = cell;
            hasLastCheckedCell = true;

            EvaluateCell(cell);
        }

        private bool TryGetCellUnderMouse(out Vector3Int cell)
        {
            cell = default;

            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

            if (!worldCamera.pixelRect.Contains(mouseScreenPosition))
            {
                return false;
            }

            float distanceToTilemapPlane = Mathf.Abs(
                groundTilemap.transform.position.z - worldCamera.transform.position.z
            );

            Vector3 screenPosition = new Vector3(
                mouseScreenPosition.x,
                mouseScreenPosition.y,
                distanceToTilemapPlane
            );

            Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);

            cell = groundTilemap.WorldToCell(worldPosition);

            return true;
        }

        private void EvaluateCell(Vector3Int cell)
        {
            if (!groundTilemap.HasTile(cell))
            {
                ClearHover();
                return;
            }

            bool isWalkable = !obstaclesTilemap.HasTile(cell);

            hasHoveredCell = true;

            hoveredCell = new Vector2Int(cell.x, cell.y);

            hoveredCellIsWalkable = isWalkable;

            hoverPreview.Show(cell, isWalkable);

            if (logCellChanges)
            {
                Debug.Log($"Hovered cell: {hoveredCell}, " + $"walkable: {isWalkable}.", this);
            }
        }

        private void ClearHover()
        {
            hasHoveredCell = false;
            hoveredCell = default;
            hoveredCellIsWalkable = false;

            if (hoverPreview != null)
            {
                hoverPreview.Clear();
            }
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(MouseTileSelector)} on " + $"'{name}' is missing the World Camera.",
                    this
                );

                referencesAreValid = false;
            }

            if (groundTilemap == null)
            {
                Debug.LogError(
                    $"{nameof(MouseTileSelector)} on " + $"'{name}' is missing the Ground Tilemap.",
                    this
                );

                referencesAreValid = false;
            }

            if (obstaclesTilemap == null)
            {
                Debug.LogError(
                    $"{nameof(MouseTileSelector)} on "
                        + $"'{name}' is missing the Obstacles Tilemap.",
                    this
                );

                referencesAreValid = false;
            }

            if (hoverPreview == null)
            {
                Debug.LogError(
                    $"{nameof(MouseTileSelector)} on " + $"'{name}' is missing the Hover Preview.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnDisable()
        {
            if (hoverPreview != null)
            {
                hoverPreview.Clear();
            }
        }
    }
}
