using UnityEngine;
using UnityEngine.Tilemaps;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    public sealed class TileHoverPreview : MonoBehaviour
    {
        [Header("Preview Tiles")]
        [SerializeField]
        private TileBase walkableTile;

        [SerializeField]
        private TileBase blockedTile;

        private Tilemap previewTilemap;
        private Vector3Int displayedCell;
        private bool hasDisplayedCell;

        private void Awake()
        {
            previewTilemap = GetComponent<Tilemap>();
        }

        public void Show(Vector3Int cell, bool isWalkable)
        {
            TileBase selectedTile = isWalkable ? walkableTile : blockedTile;

            if (selectedTile == null)
            {
                Debug.LogError($"A preview tile is missing on '{name}'.", this);

                Clear();
                return;
            }

            if (hasDisplayedCell && displayedCell != cell)
            {
                previewTilemap.SetTile(displayedCell, null);
            }

            previewTilemap.SetTile(cell, selectedTile);

            displayedCell = cell;
            hasDisplayedCell = true;
        }

        public void Clear()
        {
            if (!hasDisplayedCell)
            {
                return;
            }

            if (previewTilemap != null)
            {
                previewTilemap.SetTile(displayedCell, null);
            }

            hasDisplayedCell = false;
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
