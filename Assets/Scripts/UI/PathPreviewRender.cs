using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    public sealed class PathPreviewRenderer : MonoBehaviour
    {
        [Header("Preview Tiles")]
        [SerializeField]
        private TileBase pathTile;

        [SerializeField]
        private TileBase targetTile;

        [SerializeField]
        private TileBase invalidTile;

        private Tilemap previewTilemap;

        private readonly List<Vector3Int> displayedCells = new List<Vector3Int>();

        private void Awake()
        {
            previewTilemap = GetComponent<Tilemap>();

            ValidateReferences();
        }

        public void ShowPath(IReadOnlyList<Vector2Int> path)
        {
            Clear();

            if (path == null || path.Count == 0)
            {
                return;
            }

            /*
             * Path sadrži i početno polje igrača.
             *
             * Njega ne prikazujemo plavim tileom jer igrač
             * već stoji na tom polju.
             */

            if (path.Count == 1)
            {
                SetPreviewTile(path[0], targetTile);

                return;
            }

            /*
             * Indeks 0 je početno polje igrača.
             * Posljednji indeks je ciljno polje.
             *
             * Sve ćelije između njih prikazujemo
             * plavim Path tileom.
             */

            for (int index = 1; index < path.Count - 1; index++)
            {
                SetPreviewTile(path[index], pathTile);
            }

            Vector2Int targetCoordinates = path[path.Count - 1];

            SetPreviewTile(targetCoordinates, targetTile);
        }

        public void ShowInvalid(Vector2Int targetCoordinates)
        {
            Clear();

            SetPreviewTile(targetCoordinates, invalidTile);
        }

        public void Clear()
        {
            if (previewTilemap == null)
            {
                return;
            }

            foreach (Vector3Int displayedCell in displayedCells)
            {
                previewTilemap.SetTile(displayedCell, null);
            }

            displayedCells.Clear();
        }

        private void SetPreviewTile(Vector2Int coordinates, TileBase tile)
        {
            if (tile == null)
            {
                Debug.LogError($"A preview Tile reference is missing " + $"on '{name}'.", this);

                return;
            }

            Vector3Int tilemapCell = new Vector3Int(coordinates.x, coordinates.y, 0);

            previewTilemap.SetTile(tilemapCell, tile);

            displayedCells.Add(tilemapCell);
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (pathTile == null)
            {
                Debug.LogError(
                    $"{nameof(PathPreviewRenderer)} on " + $"'{name}' is missing the Path Tile.",
                    this
                );

                referencesAreValid = false;
            }

            if (targetTile == null)
            {
                Debug.LogError(
                    $"{nameof(PathPreviewRenderer)} on " + $"'{name}' is missing the Target Tile.",
                    this
                );

                referencesAreValid = false;
            }

            if (invalidTile == null)
            {
                Debug.LogError(
                    $"{nameof(PathPreviewRenderer)} on " + $"'{name}' is missing the Invalid Tile.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
