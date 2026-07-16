using UnityEngine;

namespace IsometricPathfinding.Navigation
{
    public sealed class GridNode
    {
        public Vector2Int Coordinates { get; }
        public bool IsWalkable { get; }
        public int GCost { get; set; }
        public int HCost { get; set; }

        public int FCost
        {
            get { return GCost == int.MaxValue ? int.MaxValue : GCost + HCost; }
        }

        public GridNode Parent { get; set; }

        public GridNode(Vector2Int coordinates, bool isWalkable)
        {
            Coordinates = coordinates;
            IsWalkable = isWalkable;

            ResetPathData();
        }

        public void ResetPathData()
        {
            GCost = int.MaxValue;
            HCost = 0;
            Parent = null;
        }

        public override string ToString()
        {
            return $"Node {Coordinates} - Walkable: {IsWalkable} - GCost: {GCost} - HCost: {HCost} - FCost: {FCost}";
        }
    }
}
