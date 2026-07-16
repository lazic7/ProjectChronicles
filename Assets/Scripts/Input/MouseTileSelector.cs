using IsometricPathfinding.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IsometricPathfinding.Input
{
    [DisallowMultipleComponent]
    public sealed class MouseTileSelector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        private Camera worldCamera;

        [SerializeField]
        private NavigationGrid navigationGrid;

        [Header("Runtime State")]
        [SerializeField]
        private bool hasHoveredCell;

        [SerializeField]
        private Vector2Int hoveredCell;

        [SerializeField]
        private bool hoveredCellIsWalkable;

        [Header("Debug")]
        [SerializeField]
        private bool logCellChanges;

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
            if (!TryGetCellUnderMouse(out Vector3Int tilemapCell))
            {
                ClearHoverState();
                hasLastCheckedCell = false;
                return;
            }

            if (hasLastCheckedCell && tilemapCell == lastCheckedCell)
            {
                return;
            }

            lastCheckedCell = tilemapCell;
            hasLastCheckedCell = true;

            EvaluateCell(tilemapCell);
        }

        private bool TryGetCellUnderMouse(out Vector3Int tilemapCell)
        {
            tilemapCell = default;

            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

            if (!worldCamera.pixelRect.Contains(mouseScreenPosition))
            {
                return false;
            }

            float distanceToGridPlane = Mathf.Abs(
                navigationGrid.WorldPlaneZ - worldCamera.transform.position.z
            );

            Vector3 screenPosition = new Vector3(
                mouseScreenPosition.x,
                mouseScreenPosition.y,
                distanceToGridPlane
            );

            Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);

            tilemapCell = navigationGrid.WorldToCell(worldPosition);

            return true;
        }

        private void EvaluateCell(Vector3Int tilemapCell)
        {
            Vector2Int logicalCell = new Vector2Int(tilemapCell.x, tilemapCell.y);

            if (!navigationGrid.TryGetNode(logicalCell, out GridNode node))
            {
                ClearHoverState();
                return;
            }

            hasHoveredCell = true;
            hoveredCell = logicalCell;
            hoveredCellIsWalkable = node.IsWalkable;

            if (logCellChanges)
            {
                Debug.Log($"Hovered cell: {hoveredCell}, " + $"walkable: {node.IsWalkable}.", this);
            }
        }

        private void ClearHoverState()
        {
            hasHoveredCell = false;
            hoveredCell = default;
            hoveredCellIsWalkable = false;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(MouseTileSelector)} on "
                        + $"'{name}' is missing the "
                        + "World Camera.",
                    this
                );

                referencesAreValid = false;
            }

            if (navigationGrid == null)
            {
                Debug.LogError(
                    $"{nameof(MouseTileSelector)} on "
                        + $"'{name}' is missing the "
                        + "Navigation Grid.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnDisable()
        {
            ClearHoverState();
        }
    }
}
