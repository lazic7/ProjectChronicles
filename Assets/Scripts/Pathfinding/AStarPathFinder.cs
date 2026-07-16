using System.Collections.Generic;
using IsometricPathfinding.Navigation;
using UnityEngine;

namespace IsometricPathfinding.Pathfinding
{
    public sealed class AStarPathfinder
    {
        private const int StraightMovementCost = 10;

        private readonly NavigationGrid navigationGrid;

        public AStarPathfinder(NavigationGrid navigationGrid)
        {
            this.navigationGrid = navigationGrid;
        }

        public bool TryFindPath(
            Vector2Int startCoordinates,
            Vector2Int targetCoordinates,
            out List<Vector2Int> path
        )
        {
            path = new List<Vector2Int>();

            if (!navigationGrid.TryGetNode(startCoordinates, out GridNode startNode))
            {
                return false;
            }

            if (!navigationGrid.TryGetNode(targetCoordinates, out GridNode targetNode))
            {
                return false;
            }

            if (!startNode.IsWalkable || !targetNode.IsWalkable)
            {
                return false;
            }

            navigationGrid.ResetAllPathData();

            if (startNode == targetNode)
            {
                path.Add(startCoordinates);
                return true;
            }

            List<GridNode> openSet = new List<GridNode>();

            HashSet<GridNode> closedSet = new HashSet<GridNode>();

            startNode.GCost = 0;

            startNode.HCost = CalculateHeuristic(startNode.Coordinates, targetNode.Coordinates);

            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                GridNode currentNode = GetLowestCostNode(openSet);

                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                if (currentNode == targetNode)
                {
                    path = RetracePath(startNode, targetNode);

                    return path.Count > 0;
                }

                List<GridNode> neighbors = navigationGrid.GetNeighbors(currentNode);

                foreach (GridNode neighbor in neighbors)
                {
                    if (!neighbor.IsWalkable)
                    {
                        continue;
                    }

                    if (closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    int tentativeGCost = currentNode.GCost + StraightMovementCost;

                    if (tentativeGCost >= neighbor.GCost)
                    {
                        continue;
                    }

                    neighbor.Parent = currentNode;

                    neighbor.GCost = tentativeGCost;

                    neighbor.HCost = CalculateHeuristic(
                        neighbor.Coordinates,
                        targetNode.Coordinates
                    );

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }

            return false;
        }

        private static GridNode GetLowestCostNode(List<GridNode> openSet)
        {
            GridNode lowestCostNode = openSet[0];

            for (int index = 1; index < openSet.Count; index++)
            {
                GridNode candidate = openSet[index];

                bool hasLowerFCost = candidate.FCost < lowestCostNode.FCost;

                bool hasEqualFButLowerH =
                    candidate.FCost == lowestCostNode.FCost
                    && candidate.HCost < lowestCostNode.HCost;

                if (hasLowerFCost || hasEqualFButLowerH)
                {
                    lowestCostNode = candidate;
                }
            }

            return lowestCostNode;
        }

        private static int CalculateHeuristic(Vector2Int from, Vector2Int to)
        {
            int distanceX = Mathf.Abs(from.x - to.x);

            int distanceY = Mathf.Abs(from.y - to.y);

            return (distanceX + distanceY) * StraightMovementCost;
        }

        private static List<Vector2Int> RetracePath(GridNode startNode, GridNode targetNode)
        {
            List<Vector2Int> reversedPath = new List<Vector2Int>();

            GridNode currentNode = targetNode;

            while (currentNode != null)
            {
                reversedPath.Add(currentNode.Coordinates);

                if (currentNode == startNode)
                {
                    break;
                }

                currentNode = currentNode.Parent;
            }

            if (
                reversedPath.Count == 0
                || reversedPath[reversedPath.Count - 1] != startNode.Coordinates
            )
            {
                return new List<Vector2Int>();
            }

            reversedPath.Reverse();

            return reversedPath;
        }
    }
}
