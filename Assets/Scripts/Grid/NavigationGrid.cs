using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IsometricPathfinding.Navigation
{
    [DisallowMultipleComponent]
    public sealed class NavigationGrid : MonoBehaviour
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
        };

        [Header("Scene References")]
        [SerializeField]
        private Tilemap groundTilemap;

        [SerializeField]
        private Tilemap obstaclesTilemap;

        [SerializeField] 
        private GridOccupancyManager occupancyManager;

        [Header("Runtime State")]
        [SerializeField]
        private int totalNodeCount;

        [SerializeField]
        private int walkableNodeCount;

        [SerializeField]
        private int blockedNodeCount;

        [SerializeField]
        private Vector2Int minimumCoordinates;

        [SerializeField]
        private Vector2Int maximumCoordinates;

        [Header("Debug")]
        [SerializeField]
        private bool logBuildSummary = true;

        private readonly Dictionary<Vector2Int, GridNode> nodes =
            new Dictionary<Vector2Int, GridNode>();

        public int NodeCount => nodes.Count;

        public int WalkableNodeCount => walkableNodeCount;

        public int BlockedNodeCount => blockedNodeCount;

        public float WorldPlaneZ
        {
            get
            {
                if (groundTilemap == null)
                {
                    return 0f;
                }

                return groundTilemap.transform.position.z;
            }
        }

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            BuildGrid();
        }

        [ContextMenu("Rebuild Navigation Grid")]
        public void BuildGrid()
        {
            if (!ValidateReferences())
            {
                return;
            }

            nodes.Clear();

            totalNodeCount = 0;
            walkableNodeCount = 0;
            blockedNodeCount = 0;

            bool hasFoundFirstNode = false;

            BoundsInt groundBounds = groundTilemap.cellBounds;

            foreach (Vector3Int tilemapCell in groundBounds.allPositionsWithin)
            {
                if (tilemapCell.z != 0)
                {
                    continue;
                }

                if (!groundTilemap.HasTile(tilemapCell))
                {
                    continue;
                }

                Vector2Int coordinates = new Vector2Int(tilemapCell.x, tilemapCell.y);

                bool isWalkable = !obstaclesTilemap.HasTile(tilemapCell);

                GridNode node = new GridNode(coordinates, isWalkable);

                nodes[coordinates] = node;

                if (isWalkable)
                {
                    walkableNodeCount++;
                }
                else
                {
                    blockedNodeCount++;
                }

                UpdateCoordinateBounds(coordinates, ref hasFoundFirstNode);
            }

            totalNodeCount = nodes.Count;

            if (totalNodeCount == 0)
            {
                Debug.LogWarning(
                    "Navigation Grid was built, " + "but no Ground tiles were found.",
                    this
                );

                return;
            }

            if (logBuildSummary)
            {
                Debug.Log(
                    $"Navigation Grid built successfully. "
                        + $"Total: {totalNodeCount}, "
                        + $"walkable: {walkableNodeCount}, "
                        + $"blocked: {blockedNodeCount}, "
                        + $"coordinates: {minimumCoordinates} "
                        + $"to {maximumCoordinates}.",
                    this
                );
            }
        }

        public bool TryGetNode(Vector2Int coordinates, out GridNode node)
        {
            return nodes.TryGetValue(coordinates, out node);
        }

        public bool ContainsCell(Vector2Int coordinates)
        {
            return nodes.ContainsKey(coordinates);
        }

        public bool IsWalkable(Vector2Int coordinates)
        {
            if (!TryGetNode(coordinates, out GridNode node))
            {
                return false;
            }

            return node.IsWalkable;
        }

        public List<GridNode> GetNeighbors(GridNode node)
        {
            List<GridNode> neighbors = new List<GridNode>(4);

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int neighborCoordinates = node.Coordinates + direction;

                if (TryGetNode(neighborCoordinates, out GridNode neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return groundTilemap.WorldToCell(worldPosition);
        }

        public Vector3 GetCellCenterWorld(Vector2Int coordinates)
        {
            Vector3Int tilemapCell = ToTilemapCell(coordinates);

            return groundTilemap.GetCellCenterWorld(tilemapCell);
        }

        public static Vector3Int ToTilemapCell(Vector2Int coordinates)
        {
            return new Vector3Int(coordinates.x, coordinates.y, 0);
        }

        private void UpdateCoordinateBounds(Vector2Int coordinates, ref bool hasFoundFirstNode)
        {
            if (!hasFoundFirstNode)
            {
                minimumCoordinates = coordinates;
                maximumCoordinates = coordinates;
                hasFoundFirstNode = true;
                return;
            }

            minimumCoordinates = Vector2Int.Min(minimumCoordinates, coordinates);

            maximumCoordinates = Vector2Int.Max(maximumCoordinates, coordinates);
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (groundTilemap == null)
            {
                Debug.LogError(
                    $"{nameof(NavigationGrid)} on "
                        + $"'{name}' is missing the "
                        + "Ground Tilemap reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (obstaclesTilemap == null)
            {
                Debug.LogError(
                    $"{nameof(NavigationGrid)} on "
                        + $"'{name}' is missing the "
                        + "Obstacles Tilemap reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }
        
        public bool IsOccupied(Vector2Int coordinates)
        {
            return occupancyManager != null && occupancyManager.IsOccupied(coordinates);
        }

        public bool IsOccupiedByOther(Vector2Int coordinates, GameObject actor)
        {
            return occupancyManager != null && occupancyManager.IsOccupiedByOther(coordinates, actor);
        }

        public bool IsWalkableAndUnoccupied(Vector2Int coordinates)
        {
            return IsWalkable(coordinates) && !IsOccupied(coordinates);
        }

        public bool IsWalkableForActor(Vector2Int coordinates, GameObject actor)
        {
            if (!IsWalkable(coordinates))
            {
                return false;
            }

            if (occupancyManager == null)
            {
                return true;
            }

            return !occupancyManager.IsOccupiedByOther(coordinates, actor);
        }
    }
}
