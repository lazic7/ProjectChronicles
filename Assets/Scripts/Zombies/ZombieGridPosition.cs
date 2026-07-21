using System;
using IsometricPathfinding.Navigation;
using UnityEngine;

namespace IsometricPathfinding.Zombies
{
    [DisallowMultipleComponent]
    public sealed class ZombieGridPosition : MonoBehaviour
    {   
        [SerializeField] private NavigationGrid navigationGrid;

        [SerializeField] private GridOccupancyManager occupancyManager;

        [SerializeField] private Vector2Int currentCell;
        
        public Vector2Int CurrentCell => currentCell;

        private void Awake()
        {
            InitializeFromWorldPosition();
        }

        private void InitializeFromWorldPosition()
        {
            Vector3Int tilemapCell = navigationGrid.WorldToCell(transform.position);
            
            currentCell = new Vector2Int(tilemapCell.x, tilemapCell.y);
            
            if (occupancyManager != null)
            {
                occupancyManager.TryReister(gameObject, currentCell);
            }

            SnapToCurrentCell();
        }
        
        private void SnapToCurrentCell()
        {
            transform.position = navigationGrid.GetCellCenterWorld(currentCell);
        }

        public void SetCurrentCell(Vector2Int newCell)
        {
            Vector2Int previousCell = currentCell;

            if (occupancyManager != null)
            {
                bool moved = occupancyManager.TryMove(gameObject, previousCell, newCell);

                if (!moved)
                {
                    return;
                }
            }

            currentCell = newCell;

            SnapToCurrentCell();
        }
    }
}
