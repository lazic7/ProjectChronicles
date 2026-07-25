using System;
using System.Collections.Generic;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace IsometricPathfinding.Pathfinding
{
    public readonly struct TacticalPathProfile
    {
        public static TacticalPathProfile Default =>
            new TacticalPathProfile(
                stepCost: 100,
                heuristicCost: 100,
                turnCost: 8,
                reverseCost: 20,
                zigZagBalanceCost: 0,
                obstacleHuggingReward: 0,
                directionalProgressReward: 0,
                maximumExtraStepCount: 0
            );

        public int StepCost { get; }

        public int HeuristicCost { get; }

        public int TurnCost { get; }

        public int ReverseCost { get; }

        public int ZigZagBalanceCost { get; }

        public int ObstacleHuggingReward { get; }

        public int DirectionalProgressReward { get; }

        public int MaximumExtraStepCount { get; }

        public TacticalPathProfile(
            int stepCost,
            int heuristicCost,
            int turnCost,
            int reverseCost,
            int zigZagBalanceCost,
            int obstacleHuggingReward,
            int directionalProgressReward,
            int maximumExtraStepCount
        )
        {
            StepCost = Mathf.Max(1, stepCost);
            HeuristicCost = Mathf.Max(0, heuristicCost);
            TurnCost = Mathf.Max(0, turnCost);
            ReverseCost = Mathf.Max(TurnCost, reverseCost);

            ZigZagBalanceCost = Mathf.Clamp(
                zigZagBalanceCost,
                0,
                StepCost
            );

            ObstacleHuggingReward = Mathf.Clamp(
                obstacleHuggingReward,
                0,
                StepCost / 4
            );

            DirectionalProgressReward = Mathf.Clamp(
                directionalProgressReward,
                0,
                StepCost / 4
            );

            MaximumExtraStepCount = Mathf.Max(0, maximumExtraStepCount);
        }
    }

    public readonly struct TacticalPathScore
    {
        public int StepScore { get; }

        public int HeuristicScore { get; }

        public int TurnScore { get; }

        public int ZigZagScore { get; }

        public int ObstacleHuggingScore { get; }

        public int DirectionalProgressScore { get; }

        public int TotalScore
        {
            get
            {
                return StepScore
                       + HeuristicScore
                       + TurnScore
                       + ZigZagScore
                       + ObstacleHuggingScore
                       + DirectionalProgressScore;
            }
        }

        public TacticalPathScore(
            int stepScore,
            int heuristicScore,
            int turnScore,
            int zigZagScore,
            int obstacleHuggingScore,
            int directionalProgressScore
        )
        {
            StepScore = stepScore;
            HeuristicScore = heuristicScore;
            TurnScore = turnScore;
            ZigZagScore = zigZagScore;
            ObstacleHuggingScore = obstacleHuggingScore;
            DirectionalProgressScore = directionalProgressScore;
        }
    }

    public readonly struct TacticalPathResult
    {
        public IReadOnlyList<Vector2Int> Path { get; }

        public int StepCount { get; }

        public int TurnPenalty { get; }

        public TacticalPathScore Score { get; }

        public int TotalScore
        {
            get
            {
                return Score.TotalScore;
            }
        }

        public TacticalPathResult(
            IReadOnlyList<Vector2Int> path,
            int stepCount,
            int turnPenalty,
            TacticalPathScore score
        )
        {
            Path = path;
            StepCount = stepCount;
            TurnPenalty = turnPenalty;
            Score = score;
        }
    }

    public sealed class AStarPathfinder
    {
        private readonly NavigationGrid navigationGrid;

        private readonly Func<Vector2Int, bool> canEnterCell;

        private readonly TacticalPathProfile defaultProfile;

        public AStarPathfinder(
            NavigationGrid navigationGrid,
            int turnPenaltyCost,
            int reversePenaltyCost,
            Func<Vector2Int, bool> canEnterCell = null
        )
        {
            this.navigationGrid = navigationGrid;

            this.canEnterCell = canEnterCell;

            defaultProfile = new TacticalPathProfile(
                stepCost: 100,
                heuristicCost: 100,
                turnCost: turnPenaltyCost,
                reverseCost: reversePenaltyCost,
                zigZagBalanceCost: 0,
                obstacleHuggingReward: 0,
                directionalProgressReward: 0,
                maximumExtraStepCount: 0
            );
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
                defaultProfile,
                out path,
                out totalTurnPenalty
            );
        }

        public bool TryFindPath(
            Vector2Int startCoordinates,
            Vector2Int targetCoordinates,
            GridDirection initialFacingDirection,
            TacticalPathProfile pathProfile,
            out List<Vector2Int> path,
            out int totalTurnPenalty
        )
        {
            path = new List<Vector2Int>();
            totalTurnPenalty = 0;

            bool pathWasFound = TryFindPath(
                startCoordinates,
                targetCoordinates,
                initialFacingDirection,
                pathProfile,
                out TacticalPathResult result
            );

            if (!pathWasFound)
            {
                return false;
            }

            for (int i = 0; i < result.Path.Count; i++)
            {
                path.Add(result.Path[i]);
            }

            totalTurnPenalty = result.TurnPenalty;

            return true;
        }

        public bool TryFindPath(
            Vector2Int startCoordinates,
            Vector2Int targetCoordinates,
            GridDirection initialFacingDirection,
            TacticalPathProfile pathProfile,
            out TacticalPathResult result
        )
        {
            result = default;

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
                List<Vector2Int> sameCellPath = new List<Vector2Int>
                {
                    startCoordinates
                };

                TacticalPathScore sameCellScore = new TacticalPathScore(
                    stepScore: 0,
                    heuristicScore: 0,
                    turnScore: 0,
                    zigZagScore: 0,
                    obstacleHuggingScore: 0,
                    directionalProgressScore: 0
                );

                result = new TacticalPathResult(
                    sameCellPath,
                    stepCount: 0,
                    turnPenalty: 0,
                    score: sameCellScore
                );

                return true;
            }

            PathSearchContext searchContext = new PathSearchContext(
                startCoordinates,
                targetCoordinates,
                pathProfile
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
                TravelScore = 0,
                HeuristicScore = CalculateHeuristicScore(
                    startCoordinates,
                    targetCoordinates,
                    searchContext
                ),
                TurnPenaltyScore = 0,
                ZigZagPenaltyScore = 0,
                ObstacleHuggingScore = 0,
                DirectionalProgressScore = 0,
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

                if (bestTargetState != null
                    && currentState.EstimatedTotalStepCount
                    > bestTargetState.StepCount + searchContext.Profile.MaximumExtraStepCount)
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
                        movementDirection,
                        searchContext.Profile
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

                    int additionalZigZagPenalty = CalculateZigZagPenalty(
                        tentativeHorizontalProgress,
                        tentativeVerticalProgress,
                        searchContext
                    );

                    int additionalObstacleHuggingScore =
                        -CalculateObstacleHuggingReward(
                            neighbor.Coordinates,
                            searchContext
                        );

                    int additionalDirectionalProgressScore =
                        CalculateDirectionalProgressScore(
                            currentState.Node.Coordinates,
                            neighbor.Coordinates,
                            searchContext
                        );

                    int tentativeZigZagPenaltyScore =
                        currentState.ZigZagPenaltyScore
                        + additionalZigZagPenalty;

                    int tentativeObstacleHuggingScore =
                        currentState.ObstacleHuggingScore
                        + additionalObstacleHuggingScore;

                    int tentativeDirectionalProgressScore =
                        currentState.DirectionalProgressScore
                        + additionalDirectionalProgressScore;

                    int tentativeTravelScore =
                        currentState.TravelScore
                        + searchContext.Profile.StepCost
                        + additionalTurnPenalty
                        + additionalZigZagPenalty
                        + additionalObstacleHuggingScore
                        + additionalDirectionalProgressScore;

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
                            HeuristicScore = CalculateHeuristicScore(
                                neighbor.Coordinates,
                                targetCoordinates,
                                searchContext
                            ),
                        };

                        states.Add(neighborKey, neighborState);
                    }

                    if (!IsTentativeStateBetter(
                            neighborState,
                            tentativeTravelScore,
                            tentativeStepCount,
                            tentativeZigZagPenaltyScore,
                            tentativeObstacleHuggingScore,
                            tentativeDirectionalProgressScore,
                            tentativeTurnPenaltyScore,
                            searchContext
                        ))
                    {
                        continue;
                    }

                    neighborState.StepCount = tentativeStepCount;
                    neighborState.TravelScore = tentativeTravelScore;
                    neighborState.TurnPenaltyScore = tentativeTurnPenaltyScore;
                    neighborState.ZigZagPenaltyScore = tentativeZigZagPenaltyScore;
                    neighborState.ObstacleHuggingScore = tentativeObstacleHuggingScore;
                    neighborState.DirectionalProgressScore = tentativeDirectionalProgressScore;
                    neighborState.HorizontalProgress = tentativeHorizontalProgress;
                    neighborState.VerticalProgress = tentativeVerticalProgress;
                    neighborState.Parent = currentState;

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

            List<Vector2Int> foundPath = RetracePath(bestTargetState);

            TacticalPathScore score = BuildPathScore(
                bestTargetState,
                searchContext
            );

            result = new TacticalPathResult(
                foundPath,
                stepCount: Mathf.Max(0, foundPath.Count - 1),
                turnPenalty: bestTargetState.TurnPenaltyScore,
                score: score
            );

            return foundPath.Count > 0;
        }

        private static int CalculateTurnPenalty(
            GridDirection previousDirection,
            GridDirection nextDirection,
            TacticalPathProfile profile
        )
        {
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
                return profile.ReverseCost;
            }

            return profile.TurnCost;
        }

        private static int CalculateHeuristicStepCount(Vector2Int from, Vector2Int to)
        {
            int distanceX = Mathf.Abs(from.x - to.x);
            int distanceY = Mathf.Abs(from.y - to.y);

            return distanceX + distanceY;
        }

        private static int CalculateHeuristicScore(
            Vector2Int from,
            Vector2Int to,
            PathSearchContext searchContext
        )
        {
            return CalculateHeuristicStepCount(from, to)
                   * searchContext.Profile.HeuristicCost;
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

        private static int CalculateDirectionalProgressScore(
            Vector2Int from,
            Vector2Int to,
            PathSearchContext searchContext
        )
        {
            if (searchContext.Profile.DirectionalProgressReward <= 0)
            {
                return 0;
            }

            int horizontalProgress = CalculateHorizontalProgressDelta(
                from,
                to,
                searchContext
            );

            int verticalProgress = CalculateVerticalProgressDelta(
                from,
                to,
                searchContext
            );

            int forwardProgress =
                Mathf.Max(0, horizontalProgress)
                + Mathf.Max(0, verticalProgress);

            int backwardProgress =
                Mathf.Max(0, -horizontalProgress)
                + Mathf.Max(0, -verticalProgress);

            return (backwardProgress - forwardProgress)
                   * searchContext.Profile.DirectionalProgressReward;
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

            return imbalance * searchContext.Profile.ZigZagBalanceCost;
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

            return adjacentBlockedCellCount * searchContext.Profile.ObstacleHuggingReward;
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
            if (candidate.EstimatedTotalScore != currentBest.EstimatedTotalScore)
            {
                return candidate.EstimatedTotalScore < currentBest.EstimatedTotalScore;
            }

            if (candidate.TravelScore != currentBest.TravelScore)
            {
                return candidate.TravelScore < currentBest.TravelScore;
            }

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

            if (candidate.DirectionalProgressScore != currentBest.DirectionalProgressScore)
            {
                return candidate.DirectionalProgressScore < currentBest.DirectionalProgressScore;
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
            if (candidate.TravelScore != currentBest.TravelScore)
            {
                return candidate.TravelScore < currentBest.TravelScore;
            }

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

            if (candidate.DirectionalProgressScore != currentBest.DirectionalProgressScore)
            {
                return candidate.DirectionalProgressScore < currentBest.DirectionalProgressScore;
            }

            return candidate.TurnPenaltyScore < currentBest.TurnPenaltyScore;
        }

        private static bool IsTentativeStateBetter(
            SearchState existingState,
            int tentativeTravelScore,
            int tentativeStepCount,
            int tentativeZigZagPenaltyScore,
            int tentativeObstacleHuggingScore,
            int tentativeDirectionalProgressScore,
            int tentativeTurnPenaltyScore,
            PathSearchContext searchContext
        )
        {
            if (tentativeTravelScore != existingState.TravelScore)
            {
                return tentativeTravelScore < existingState.TravelScore;
            }

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

            if (tentativeDirectionalProgressScore != existingState.DirectionalProgressScore)
            {
                return tentativeDirectionalProgressScore < existingState.DirectionalProgressScore;
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

        private static TacticalPathScore BuildPathScore(
            SearchState targetState,
            PathSearchContext searchContext
        )
        {
            int stepScore =
                targetState.StepCount
                * searchContext.Profile.StepCost;

            return new TacticalPathScore(
                stepScore,
                targetState.HeuristicScore,
                targetState.TurnPenaltyScore,
                targetState.ZigZagPenaltyScore,
                targetState.ObstacleHuggingScore,
                targetState.DirectionalProgressScore
            );
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
            public TacticalPathProfile Profile { get; }

            public int HorizontalTargetSign { get; }

            public int VerticalTargetSign { get; }

            public bool UseDiagonalZigZag
            {
                get
                {
                    return Profile.ZigZagBalanceCost > 0
                           && HorizontalTargetSign != 0
                           && VerticalTargetSign != 0;
                }
            }

            public bool UseObstacleHugging
            {
                get
                {
                    return Profile.ObstacleHuggingReward > 0;
                }
            }

            public PathSearchContext(
                Vector2Int startCoordinates,
                Vector2Int targetCoordinates,
                TacticalPathProfile profile
            )
            {
                Profile = profile;

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

            public int TravelScore { get; set; } = int.MaxValue;

            public int HeuristicScore { get; set; }

            public int EstimatedTotalScore
            {
                get
                {
                    if (TravelScore == int.MaxValue)
                    {
                        return int.MaxValue;
                    }

                    return TravelScore + HeuristicScore;
                }
            }

            public int TurnPenaltyScore { get; set; } = int.MaxValue;

            public int ZigZagPenaltyScore { get; set; } = int.MaxValue;

            public int ObstacleHuggingScore { get; set; } = int.MaxValue;

            public int DirectionalProgressScore { get; set; }

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
