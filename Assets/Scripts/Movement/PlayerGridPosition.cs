using UnityEngine;
using UnityEngine.Tilemaps;

namespace IsometricPathfinding.Movement
{
    [DisallowMultipleComponent]
    public sealed class PlayerGridPosition : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Tilemap groundTilemap;

        [Header("Runtime State")]
        [SerializeField]
        private Vector2Int currentCell;

        public Vector2Int CurrentCell => currentCell;

        private void Start()
        {
            InitializeFromWorldPosition();
        }

        public void InitializeFromWorldPosition()
        {
            if (groundTilemap == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerGridPosition)} on '{name}' is missing the Ground Tilemap reference.",
                    this
                );

                enabled = false;
                return;
            }

            Vector3Int tilemapCell = groundTilemap.WorldToCell(transform.position);

            Vector2Int logicalCell = new Vector2Int(tilemapCell.x, tilemapCell.y);

            if (!groundTilemap.HasTile(tilemapCell))
            {
                Debug.LogError(
                    $"Player is not positioned above a Ground tile. Calculated cell: {tilemapCell}.",
                    this
                );

                enabled = false;
                return;
            }

            currentCell = logicalCell;

            SnapToCurrentCell();

            Debug.Log($"Player initialized at grid cell {currentCell}.", this);
        }

        public void SetCurrentCell(Vector2Int newCell)
        {
            if (groundTilemap == null)
            {
                Debug.LogError(
                    "Cannot change the player's cell because the Ground Tilemap reference is missing.",
                    this
                );

                return;
            }

            Vector3Int tilemapCell = ToTilemapCell(newCell);

            if (!groundTilemap.HasTile(tilemapCell))
            {
                Debug.LogWarning(
                    $"Cannot place the player on cell {newCell} because Ground has no tile there.",
                    this
                );

                return;
            }

            currentCell = newCell;

            SnapToCurrentCell();
        }

        public void SnapToCurrentCell()
        {
            if (groundTilemap == null)
            {
                return;
            }

            Vector3Int tilemapCell = ToTilemapCell(currentCell);

            transform.position = groundTilemap.GetCellCenterWorld(tilemapCell);
        }

        private static Vector3Int ToTilemapCell(Vector2Int logicalCell)
        {
            return new Vector3Int(logicalCell.x, logicalCell.y, 0);
        }
    }
}
