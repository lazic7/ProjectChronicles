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

        private bool hasInitialized;

        private bool isRegisteredWithOccupancy;

        public Vector2Int CurrentCell => currentCell;

        private void Awake()
        {
            InitializeFromWorldPosition();
        }

        private void OnEnable()
        {
            /*
             * If this zombie is ever re-enabled, register it again.
             *
             * For normal scene startup:
             * Awake runs first, registers the zombie,
             * then OnEnable runs and does nothing because it is already registered.
             */
            if (!hasInitialized)
            {
                return;
            }

            RegisterCurrentCell();
        }

        private void OnDisable()
        {
            /*
             * Important:
             * A disabled/dead zombie should not continue blocking its grid cell.
             */
            UnregisterFromOccupancy();
        }

        private void InitializeFromWorldPosition()
        {
            Vector3Int tilemapCell = navigationGrid.WorldToCell(transform.position);

            currentCell = new Vector2Int(tilemapCell.x, tilemapCell.y);

            hasInitialized = true;

            RegisterCurrentCell();

            SnapToCurrentCell();
        }

        private void RegisterCurrentCell()
        {
            if (occupancyManager == null)
            {
                return;
            }

            if (isRegisteredWithOccupancy)
            {
                return;
            }

            /*
             * If the occupancy manager already knows this same object
             * is occupying this cell, consider the registration valid.
             */
            if (occupancyManager.TryGetOccupant(currentCell, out GameObject occupant)
                && occupant == gameObject)
            {
                isRegisteredWithOccupancy = true;
                return;
            }

            isRegisteredWithOccupancy = occupancyManager.TryRegister(gameObject, currentCell);
        }

        public void UnregisterFromOccupancy()
        {
            if (occupancyManager == null)
            {
                return;
            }

            if (!isRegisteredWithOccupancy)
            {
                return;
            }

            occupancyManager.Unregister(gameObject, currentCell);

            isRegisteredWithOccupancy = false;
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
                bool moved;

                if (isRegisteredWithOccupancy)
                {
                    moved = occupancyManager.TryMove(gameObject, previousCell, newCell);
                }
                else
                {
                    moved = TryRegisterAtCell(newCell);
                }

                if (!moved)
                {
                    return;
                }

                isRegisteredWithOccupancy = true;
            }

            currentCell = newCell;

            SnapToCurrentCell();
        }

        private bool TryRegisterAtCell(Vector2Int cell)
        {
            if (occupancyManager == null)
            {
                return true;
            }

            if (occupancyManager.TryGetOccupant(cell, out GameObject occupant)
                && occupant == gameObject)
            {
                return true;
            }

            return occupancyManager.TryRegister(gameObject, cell);
        }
    }
}