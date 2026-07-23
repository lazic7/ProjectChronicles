using System;
using System.Collections.Generic;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using UnityEngine;

namespace IsometricPathfinding.Pathfinding
{
    
    public readonly struct PathPreferenceSettings
    {
        public static PathPreferenceSettings Default =>
            new PathPreferenceSettings(false, 0, 0);

        public bool PreferDiagonalZigZag { get; }

        public int ZigZagBalanceCost { get; }

        public int ObstacleHuggingReward { get; }

        public PathPreferenceSettings(
            bool preferDiagonalZigZag,
            int zigZagBalanceCost,
            int obstacleHuggingReward
        )
        {
            PreferDiagonalZigZag = preferDiagonalZigZag;
            ZigZagBalanceCost = Mathf.Max(0, zigZagBalanceCost);
            ObstacleHuggingReward = Mathf.Max(0, obstacleHuggingReward);
        }
    }
    
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
    return TryFindPath(
        startCoordinates,
        targetCoordinates,
        initialFacingDirection,
        PathPreferenceSettings.Default,
        out path,
        out totalTurnPenalty
    );
}

public bool TryFindPath(
    Vector2Int startCoordinates,
    Vector2Int targetCoordinates,
    GridDirection initialFacingDirection,
    PathPreferenceSettings pathPreferenceSettings,
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

    PathSearchContext searchContext = new PathSearchContext(
        startCoordinates,
        targetCoordinates,
        pathPreferenceSettings
    );

    List<SearchState> openSet = new List<SearchState>();

    Dictionary<StateKey, SearchState> states =
        new Dictionary<StateKey, SearchState>();

    StateKey startKey = new StateKey(startCoordinates, initialFacingDirection);

    SearchState startState = new SearchState(startNode, initialFacingDirection)
    {
        StepCount = 0,
        HeuristicStepCount = CalculateHeuristicStepCount(
            startCoordinates,
            targetCoordinates
        ),
        TurnPenaltyScore = 0,
        ZigZagPenaltyScore = 0,
        ObstacleHuggingScore = 0,
        HorizontalProgress = 0,
        VerticalProgress = 0,
    };

    states.Add(startKey, startState);

    openSet.Add(startState);

    SearchState bestTargetState = null;

    while (openSet.Count > 0)
    {
        SearchState currentState = GetLowestCostState(openSet, searchContext);

        openSet.Remove(currentState);

        if (currentState.IsClosed)
        {
            continue;
        }

        /*
         * Once we already found a target path, we still keep searching
         * equally short paths so we can choose a nicer zigzag/obstacle-hugging route.
         *
         * But we stop as soon as every remaining candidate would require
         * more movement steps.
         */
        if (bestTargetState != null
            && currentState.EstimatedTotalStepCount > bestTargetState.StepCount)
        {
            break;
        }

        currentState.IsClosed = true;

        if (currentState.Node == targetNode)
        {
            if (bestTargetState == null
                || IsSearchStateBetter(currentState, bestTargetState, searchContext))
            {
                bestTargetState = currentState;
            }

            continue;
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

            int tentativeStepCount = currentState.StepCount + 1;

            int tentativeTurnPenaltyScore =
                currentState.TurnPenaltyScore + additionalTurnPenalty;

            int tentativeHorizontalProgress =
                currentState.HorizontalProgress
                + CalculateHorizontalProgressDelta(
                    currentState.Node.Coordinates,
                    neighbor.Coordinates,
                    searchContext
                );

            int tentativeVerticalProgress =
                currentState.VerticalProgress
                + CalculateVerticalProgressDelta(
                    currentState.Node.Coordinates,
                    neighbor.Coordinates,
                    searchContext
                );

            int tentativeZigZagPenaltyScore =
                currentState.ZigZagPenaltyScore
                + CalculateZigZagPenalty(
                    tentativeHorizontalProgress,
                    tentativeVerticalProgress,
                    searchContext
                );

            int tentativeObstacleHuggingScore =
                currentState.ObstacleHuggingScore
                - CalculateObstacleHuggingReward(
                    neighbor.Coordinates,
                    searchContext
                );

            StateKey neighborKey = new StateKey(
                neighbor.Coordinates,
                movementDirection
            );

            if (!states.TryGetValue(neighborKey, out SearchState neighborState))
            {
                neighborState = new SearchState(neighbor, movementDirection)
                {
                    HeuristicStepCount = CalculateHeuristicStepCount(
                        neighbor.Coordinates,
                        targetCoordinates
                    ),
                };

                states.Add(neighborKey, neighborState);
            }

            if (!IsTentativeStateBetter(
                    neighborState,
                    tentativeStepCount,
                    tentativeZigZagPenaltyScore,
                    tentativeObstacleHuggingScore,
                    tentativeTurnPenaltyScore,
                    searchContext
                ))
            {
                continue;
            }

            neighborState.StepCount = tentativeStepCount;
            neighborState.TurnPenaltyScore = tentativeTurnPenaltyScore;
            neighborState.ZigZagPenaltyScore = tentativeZigZagPenaltyScore;
            neighborState.ObstacleHuggingScore = tentativeObstacleHuggingScore;
            neighborState.HorizontalProgress = tentativeHorizontalProgress;
            neighborState.VerticalProgress = tentativeVerticalProgress;
            neighborState.Parent = currentState;

            /*
             * If this state was closed before, but we found a better
             * equal-length styled path to it, allow it to be processed again.
             */
            neighborState.IsClosed = false;

            if (!openSet.Contains(neighborState))
            {
                openSet.Add(neighborState);
            }
        }
    }

    if (bestTargetState == null)
    {
        return false;
    }

    path = RetracePath(bestTargetState);
    totalTurnPenalty = bestTargetState.TurnPenaltyScore;

    return path.Count > 0;
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
        
        private static int CalculateHeuristicStepCount(Vector2Int from, Vector2Int to)
{
    int distanceX = Mathf.Abs(from.x - to.x);
    int distanceY = Mathf.Abs(from.y - to.y);

    return distanceX + distanceY;
}

private static int CalculateHorizontalProgressDelta(
    Vector2Int from,
    Vector2Int to,
    PathSearchContext searchContext
)
{
    int deltaX = to.x - from.x;

    if (deltaX == 0 || searchContext.HorizontalTargetSign == 0)
    {
        return 0;
    }

    return deltaX == searchContext.HorizontalTargetSign ? 1 : -1;
}

private static int CalculateVerticalProgressDelta(
    Vector2Int from,
    Vector2Int to,
    PathSearchContext searchContext
)
{
    int deltaY = to.y - from.y;

    if (deltaY == 0 || searchContext.VerticalTargetSign == 0)
    {
        return 0;
    }

    return deltaY == searchContext.VerticalTargetSign ? 1 : -1;
}

private static int CalculateZigZagPenalty(
    int horizontalProgress,
    int verticalProgress,
    PathSearchContext searchContext
)
{
    if (!searchContext.UseDiagonalZigZag)
    {
        return 0;
    }

    int imbalance = Mathf.Abs(horizontalProgress - verticalProgress);

    return imbalance * searchContext.Preferences.ZigZagBalanceCost;
}

private int CalculateObstacleHuggingReward(
    Vector2Int coordinates,
    PathSearchContext searchContext
)
{
    if (!searchContext.UseObstacleHugging)
    {
        return 0;
    }

    int adjacentBlockedCellCount = CountAdjacentBlockedCells(coordinates);

    return adjacentBlockedCellCount
        * searchContext.Preferences.ObstacleHuggingReward;
}

private int CountAdjacentBlockedCells(Vector2Int coordinates)
{
    int blockedCount = 0;

    Vector2Int[] directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right,
    };

    for (int i = 0; i < directions.Length; i++)
    {
        Vector2Int neighborCoordinates = coordinates + directions[i];

        if (!navigationGrid.TryGetNode(neighborCoordinates, out GridNode neighbor))
        {
            continue;
        }

        if (!neighbor.IsWalkable)
        {
            blockedCount++;
        }
    }

    return blockedCount;
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

        private static SearchState GetLowestCostState(
            List<SearchState> openSet,
            PathSearchContext searchContext
        )
        {
            SearchState lowestCostState = openSet[0];

            for (int index = 1; index < openSet.Count; index++)
            {
                SearchState candidate = openSet[index];

                if (IsOpenSetCandidateBetter(candidate, lowestCostState, searchContext))
                {
                    lowestCostState = candidate;
                }
            }

            return lowestCostState;
        }
        
        private static bool IsOpenSetCandidateBetter(
    SearchState candidate,
    SearchState currentBest,
    PathSearchContext searchContext
)
{
    if (candidate.EstimatedTotalStepCount != currentBest.EstimatedTotalStepCount)
    {
        return candidate.EstimatedTotalStepCount
            < currentBest.EstimatedTotalStepCount;
    }

    if (searchContext.UseDiagonalZigZag
        && candidate.ZigZagPenaltyScore != currentBest.ZigZagPenaltyScore)
    {
        return candidate.ZigZagPenaltyScore < currentBest.ZigZagPenaltyScore;
    }

    if (searchContext.UseObstacleHugging
        && candidate.ObstacleHuggingScore != currentBest.ObstacleHuggingScore)
    {
        return candidate.ObstacleHuggingScore < currentBest.ObstacleHuggingScore;
    }

    if (candidate.TurnPenaltyScore != currentBest.TurnPenaltyScore)
    {
        return candidate.TurnPenaltyScore < currentBest.TurnPenaltyScore;
    }

    return candidate.HeuristicStepCount < currentBest.HeuristicStepCount;
}

private static bool IsSearchStateBetter(
    SearchState candidate,
    SearchState currentBest,
    PathSearchContext searchContext
)
{
    if (candidate.StepCount != currentBest.StepCount)
    {
        return candidate.StepCount < currentBest.StepCount;
    }

    if (searchContext.UseDiagonalZigZag
        && candidate.ZigZagPenaltyScore != currentBest.ZigZagPenaltyScore)
    {
        return candidate.ZigZagPenaltyScore < currentBest.ZigZagPenaltyScore;
    }

    if (searchContext.UseObstacleHugging
        && candidate.ObstacleHuggingScore != currentBest.ObstacleHuggingScore)
    {
        return candidate.ObstacleHuggingScore < currentBest.ObstacleHuggingScore;
    }

    return candidate.TurnPenaltyScore < currentBest.TurnPenaltyScore;
}

private static bool IsTentativeStateBetter(
    SearchState existingState,
    int tentativeStepCount,
    int tentativeZigZagPenaltyScore,
    int tentativeObstacleHuggingScore,
    int tentativeTurnPenaltyScore,
    PathSearchContext searchContext
)
{
    if (tentativeStepCount != existingState.StepCount)
    {
        return tentativeStepCount < existingState.StepCount;
    }

    if (searchContext.UseDiagonalZigZag
        && tentativeZigZagPenaltyScore != existingState.ZigZagPenaltyScore)
    {
        return tentativeZigZagPenaltyScore < existingState.ZigZagPenaltyScore;
    }

    if (searchContext.UseObstacleHugging
        && tentativeObstacleHuggingScore != existingState.ObstacleHuggingScore)
    {
        return tentativeObstacleHuggingScore < existingState.ObstacleHuggingScore;
    }

    return tentativeTurnPenaltyScore < existingState.TurnPenaltyScore;
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
        
        private readonly struct PathSearchContext
        {
            public Vector2Int StartCoordinates { get; }

            public Vector2Int TargetCoordinates { get; }

            public PathPreferenceSettings Preferences { get; }

            public int HorizontalTargetSign { get; }

            public int VerticalTargetSign { get; }

            public bool UseDiagonalZigZag
            {
                get
                {
                    return Preferences.PreferDiagonalZigZag
                           && Preferences.ZigZagBalanceCost > 0
                           && HorizontalTargetSign != 0
                           && VerticalTargetSign != 0;
                }
            }

            public bool UseObstacleHugging
            {
                get
                {
                    return Preferences.ObstacleHuggingReward > 0;
                }
            }

            public PathSearchContext(
                Vector2Int startCoordinates,
                Vector2Int targetCoordinates,
                PathPreferenceSettings preferences
            )
            {
                StartCoordinates = startCoordinates;
                TargetCoordinates = targetCoordinates;
                Preferences = preferences;

                HorizontalTargetSign = Math.Sign(targetCoordinates.x - startCoordinates.x);
                VerticalTargetSign = Math.Sign(targetCoordinates.y - startCoordinates.y);
            }
        }

        private sealed class SearchState
        {
            public GridNode Node { get; }

            public GridDirection ArrivalDirection { get; }

            public int StepCount { get; set; } = int.MaxValue;

            public int HeuristicStepCount { get; set; }

            public int EstimatedTotalStepCount
            {
                get
                {
                    if (StepCount == int.MaxValue)
                    {
                        return int.MaxValue;
                    }

                    return StepCount + HeuristicStepCount;
                }
            }

            public int TurnPenaltyScore { get; set; } = int.MaxValue;

            public int ZigZagPenaltyScore { get; set; } = int.MaxValue;

            public int ObstacleHuggingScore { get; set; } = int.MaxValue;

            public int HorizontalProgress { get; set; }

            public int VerticalProgress { get; set; }

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
