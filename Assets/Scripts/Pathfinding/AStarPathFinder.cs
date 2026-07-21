using System;
using System.Collections.Generic;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using UnityEngine;

namespace IsometricPathfinding.Pathfinding
{
    public sealed class AStarPathfinder
    {
        private readonly NavigationGrid navigationGrid;

        private readonly int turnPenaltyCost;
        private readonly int reversePenaltyCost;

        private readonly System.Func<Vector2Int, bool> canEnterCell;

        /*
         * Jedan dodatni korak mora biti skuplji od
         * najveće moguće razlike u turn penaltyju
         * između dviju putanja.
         */
        private readonly long stepPriorityCost;

        public AStarPathfinder(
            NavigationGrid navigationGrid,
            int turnPenaltyCost,
            int reversePenaltyCost,
            System.Func<Vector2Int, bool> canEnterCell = null
        )
        {
            this.navigationGrid = navigationGrid;

            this.turnPenaltyCost = Mathf.Max(0, turnPenaltyCost);

            this.reversePenaltyCost = Mathf.Max(this.turnPenaltyCost, reversePenaltyCost);

            this.canEnterCell = canEnterCell;

            stepPriorityCost = CalculateStepPriorityCost();
        }

        public bool TryFindPath(
            Vector2Int startCoordinates,
            Vector2Int targetCoordinates,
            GridDirection initialFacingDirection,
            out List<Vector2Int> path,
            out int totalTurnPenalty
        )
        {
            path = new List<Vector2Int>();
            totalTurnPenalty = 0;

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

            if (startNode == targetNode)
            {
                path.Add(startCoordinates);
                return true;
            }

            List<SearchState> openSet = new List<SearchState>();

            Dictionary<StateKey, SearchState> states = new Dictionary<StateKey, SearchState>();

            StateKey startKey = new StateKey(startCoordinates, initialFacingDirection);

            SearchState startState = new SearchState(startNode, initialFacingDirection)
            {
                MovementCost = 0,
                HeuristicCost = CalculateHeuristic(startCoordinates, targetCoordinates),
                TurnPenaltyScore = 0,
            };

            states.Add(startKey, startState);

            openSet.Add(startState);

            while (openSet.Count > 0)
            {
                SearchState currentState = GetLowestCostState(openSet);

                openSet.Remove(currentState);

                if (currentState.IsClosed)
                {
                    continue;
                }

                currentState.IsClosed = true;

                if (currentState.Node == targetNode)
                {
                    path = RetracePath(currentState);

                    totalTurnPenalty = currentState.TurnPenaltyScore;

                    return path.Count > 0;
                }

                List<GridNode> neighbors = navigationGrid.GetNeighbors(currentState.Node);

                foreach (GridNode neighbor in neighbors)
                {
                    if (!neighbor.IsWalkable)
                    {
                        continue;
                    }
                    
                    if (canEnterCell != null && !canEnterCell(neighbor.Coordinates))
                    {
                        continue;
                    }

                    GridDirection movementDirection = GetDirection(
                        currentState.Node.Coordinates,
                        neighbor.Coordinates
                    );

                    if (movementDirection == GridDirection.None)
                    {
                        continue;
                    }

                    int additionalTurnPenalty = CalculateTurnPenalty(
                        currentState.ArrivalDirection,
                        movementDirection
                    );

                    long tentativeMovementCost =
                        currentState.MovementCost + stepPriorityCost + additionalTurnPenalty;

                    int tentativeTurnPenaltyScore =
                        currentState.TurnPenaltyScore + additionalTurnPenalty;

                    StateKey neighborKey = new StateKey(neighbor.Coordinates, movementDirection);

                    if (!states.TryGetValue(neighborKey, out SearchState neighborState))
                    {
                        neighborState = new SearchState(neighbor, movementDirection)
                        {
                            HeuristicCost = CalculateHeuristic(
                                neighbor.Coordinates,
                                targetCoordinates
                            ),
                        };

                        states.Add(neighborKey, neighborState);
                    }

                    if (tentativeMovementCost >= neighborState.MovementCost)
                    {
                        continue;
                    }

                    neighborState.MovementCost = tentativeMovementCost;

                    neighborState.TurnPenaltyScore = tentativeTurnPenaltyScore;

                    neighborState.Parent = currentState;

                    /*
                     * Ako smo pronašli bolji put do
                     * prethodno zatvorenog stanja,
                     * dopuštamo njegovo ponovno otvaranje.
                     */
                    neighborState.IsClosed = false;

                    if (!openSet.Contains(neighborState))
                    {
                        openSet.Add(neighborState);
                    }
                }
            }

            return false;
        }

        private long CalculateStepPriorityCost()
        {
            long maximumPenaltyPerMovement = Math.Max(turnPenaltyCost, reversePenaltyCost);

            /*
             * Najkraća putanja nema razloga ponavljati
             * istu ćeliju. Zato je broj njezinih koraka
             * najviše NodeCount - 1.
             */
            long maximumStepsInSimplePath = Math.Max(0, navigationGrid.NodeCount - 1);

            long maximumPossibleTurnPenalty = maximumPenaltyPerMovement * maximumStepsInSimplePath;

            /*
             * Jedan korak vrijedi više od ukupnog
             * mogućeg turn penaltyja cijele putanje.
             */
            return maximumPossibleTurnPenalty + 1;
        }

        private long CalculateHeuristic(Vector2Int from, Vector2Int to)
        {
            int distanceX = Mathf.Abs(from.x - to.x);

            int distanceY = Mathf.Abs(from.y - to.y);

            int minimumRemainingStepCount = distanceX + distanceY;

            return minimumRemainingStepCount * stepPriorityCost;
        }

        private int CalculateTurnPenalty(
            GridDirection previousDirection,
            GridDirection nextDirection
        )
        {
            /*
             * Ako nema početnog FacingDirectiona,
             * prvi korak ne dobiva kaznu.
             */
            if (previousDirection == GridDirection.None)
            {
                return 0;
            }

            if (previousDirection == nextDirection)
            {
                return 0;
            }

            if (AreOppositeDirections(previousDirection, nextDirection))
            {
                return reversePenaltyCost;
            }

            return turnPenaltyCost;
        }

        private static bool AreOppositeDirections(GridDirection first, GridDirection second)
        {
            return (first == GridDirection.Up && second == GridDirection.Down)
                || (first == GridDirection.Down && second == GridDirection.Up)
                || (first == GridDirection.Left && second == GridDirection.Right)
                || (first == GridDirection.Right && second == GridDirection.Left);
        }

        private static GridDirection GetDirection(Vector2Int from, Vector2Int to)
        {
            Vector2Int difference = to - from;

            if (difference == Vector2Int.up)
            {
                return GridDirection.Up;
            }

            if (difference == Vector2Int.down)
            {
                return GridDirection.Down;
            }

            if (difference == Vector2Int.left)
            {
                return GridDirection.Left;
            }

            if (difference == Vector2Int.right)
            {
                return GridDirection.Right;
            }

            return GridDirection.None;
        }

        private static SearchState GetLowestCostState(List<SearchState> openSet)
        {
            SearchState lowestCostState = openSet[0];

            for (int index = 1; index < openSet.Count; index++)
            {
                SearchState candidate = openSet[index];

                bool hasLowerTotalCost = candidate.TotalCost < lowestCostState.TotalCost;

                bool hasEqualTotalButLowerHeuristic =
                    candidate.TotalCost == lowestCostState.TotalCost
                    && candidate.HeuristicCost < lowestCostState.HeuristicCost;

                if (hasLowerTotalCost || hasEqualTotalButLowerHeuristic)
                {
                    lowestCostState = candidate;
                }
            }

            return lowestCostState;
        }

        private static List<Vector2Int> RetracePath(SearchState targetState)
        {
            List<Vector2Int> reversedPath = new List<Vector2Int>();

            SearchState currentState = targetState;

            while (currentState != null)
            {
                reversedPath.Add(currentState.Node.Coordinates);

                currentState = currentState.Parent;
            }

            reversedPath.Reverse();

            return reversedPath;
        }

        private readonly struct StateKey : IEquatable<StateKey>
        {
            public Vector2Int Coordinates { get; }

            public GridDirection ArrivalDirection { get; }

            public StateKey(Vector2Int coordinates, GridDirection arrivalDirection)
            {
                Coordinates = coordinates;

                ArrivalDirection = arrivalDirection;
            }

            public bool Equals(StateKey other)
            {
                return Coordinates == other.Coordinates
                    && ArrivalDirection == other.ArrivalDirection;
            }

            public override bool Equals(object obj)
            {
                return obj is StateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Coordinates.GetHashCode() * 397) ^ (int)ArrivalDirection;
                }
            }
        }

        private sealed class SearchState
        {
            public GridNode Node { get; }

            public GridDirection ArrivalDirection { get; }

            public long MovementCost { get; set; } = long.MaxValue;

            public long HeuristicCost { get; set; }

            public long TotalCost
            {
                get
                {
                    if (MovementCost == long.MaxValue)
                    {
                        return long.MaxValue;
                    }

                    return MovementCost + HeuristicCost;
                }
            }

            public int TurnPenaltyScore { get; set; } = int.MaxValue;

            public SearchState Parent { get; set; }

            public bool IsClosed { get; set; }

            public SearchState(GridNode node, GridDirection arrivalDirection)
            {
                Node = node;

                ArrivalDirection = arrivalDirection;
            }
        }
    }
}
