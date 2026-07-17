using UnityEngine;

namespace IsometricPathfinding.Navigation
{
    public sealed class GridNode
    {
        public Vector2Int Coordinates { get; }

        public bool IsWalkable { get; }

        public GridNode(Vector2Int coordinates, bool isWalkable)
        {
            Coordinates = coordinates;
            IsWalkable = isWalkable;
        }

        public override string ToString()
        {
            return $"Node {Coordinates}, " + $"walkable: {IsWalkable}";
        }
    }
}
