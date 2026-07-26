using System.Collections.Generic;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using IsometricPathfinding.Pathfinding;
using IsometricPathfinding.Zombies;
using UnityEngine;

namespace IsometricPathfinding.Combat
{
    [DisallowMultipleComponent]
    public sealed class ZombieActionExecutionController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private ZombieStrikeTargetingController targetingController;

        [SerializeField] private DangerTurnController dangerTurnController;

        [SerializeField] private NavigationGrid navigationGrid;

        [SerializeField] private PlayerGridPosition playerGridPosition;

        [SerializeField] private PlayerGridMover playerGridMover;

        [Header("Path Settings")]
        [SerializeField] [Min(1)] private int stepCost = 100;

        [SerializeField] [Min(0)] private int heuristicCost = 60;

        [SerializeField] [Min(0)] private int turnPenaltyCost = 8;

        [SerializeField] [Min(0)] private int reversePenaltyCost = 20;

        [SerializeField] [Min(0)] private int maximumExtraStepCount = 4;

        [Header("Debug")]
        [SerializeField] private bool logActionExecution;

        private AStarPathfinder pathfinder;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            pathfinder = new AStarPathfinder(
                navigationGrid,
                turnPenaltyCost,
                reversePenaltyCost,
                cell => navigationGrid.IsWalkableForActor(cell, playerGridMover.gameObject)
            );
        }

        private void OnEnable()
        {
            if (targetingController != null)
            {
                targetingController.ActionRequested += OnActionRequested;
            }
        }

        private void OnDisable()
        {
            if (targetingController != null)
            {
                targetingController.ActionRequested -= OnActionRequested;
            }
        }

        private void OnActionRequested(ZombieAgent zombie, ZombieAttackOption option)
        {
            if (zombie == null)
            {
                return;
            }

            if (option == ZombieAttackOption.Strike)
            {
                ExecuteStrike(zombie);
                return;
            }

            if (option == ZombieAttackOption.Shoot)
            {
                /*
                 * Shoot is intentionally not implemented yet.
                 * Later this can become a ranged attack or ranged minigame.
                 */
                if (logActionExecution)
                {
                    Debug.Log($"Shoot selected against {zombie.name}, but shooting is not implemented yet.", zombie);
                }

                return;
            }
        }

        private void ExecuteStrike(ZombieAgent zombie)
        {
            if (!dangerTurnController.CanStartStrike(zombie))
            {
                return;
            }

            Vector2Int playerCell = playerGridPosition.CurrentCell;
            Vector2Int zombieCell = zombie.CurrentCell;

            if (AreAdjacent(playerCell, zombieCell))
            {
                FacePlayerTowardZombie(zombie);

                bool minigameStarted = dangerTurnController.BeginStrikeMinigame(zombie);

                if (logActionExecution)
                {
                    Debug.Log(
                        minigameStarted
                            ? $"Player is already adjacent to {zombie.name}. Strike minigame started."
                            : $"Could not start strike minigame against {zombie.name}.",
                        zombie
                    );
                }

                return;
            }

            if (!TryFindBestAdjacentPathToZombie(zombie, out List<Vector2Int> path))
            {
                if (logActionExecution)
                {
                    Debug.Log($"No adjacent path found for strike against {zombie.name}.", zombie);
                }

                return;
            }

            bool approachStarted = dangerTurnController.BeginStrikeApproach(zombie);

            if (!approachStarted)
            {
                return;
            }

            bool movementStarted = playerGridMover.TryMoveAlongPath(path);

            if (!movementStarted)
            {
                dangerTurnController.CancelStrikeAction();

                if (logActionExecution)
                {
                    Debug.LogWarning($"Strike approach path was found but player movement failed.", this);
                }

                return;
            }

            if (logActionExecution)
            {
                Debug.Log($"Player is moving adjacent to {zombie.name} for Strike.", zombie);
            }
        }

        private bool TryFindBestAdjacentPathToZombie(
            ZombieAgent zombie,
            out List<Vector2Int> bestPath
        )
        {
            bestPath = null;

            if (zombie == null)
            {
                return false;
            }

            Vector2Int start = playerGridPosition.CurrentCell;
            Vector2Int zombieCell = zombie.CurrentCell;

            Vector2Int[] candidates =
            {
                zombieCell + Vector2Int.up,
                zombieCell + Vector2Int.down,
                zombieCell + Vector2Int.left,
                zombieCell + Vector2Int.right,
            };

            bool foundCandidate = false;
            TacticalPathResult bestResult = default;

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2Int candidate = candidates[i];

                if (candidate != start
                    && !navigationGrid.IsWalkableForActor(candidate, playerGridMover.gameObject))
                {
                    continue;
                }

                bool pathWasFound = pathfinder.TryFindPath(
                    start,
                    candidate,
                    playerGridMover.FacingDirection,
                    BuildPathProfile(start, candidate),
                    out TacticalPathResult candidateResult
                );

                if (!pathWasFound)
                {
                    continue;
                }

                bool candidateIsBetter =
                    !foundCandidate
                    || candidateResult.TotalScore < bestResult.TotalScore
                    || (candidateResult.TotalScore == bestResult.TotalScore
                        && candidateResult.StepCount < bestResult.StepCount);

                if (!candidateIsBetter)
                {
                    continue;
                }

                foundCandidate = true;
                bestResult = candidateResult;
            }

            if (!foundCandidate)
            {
                return false;
            }

            bestPath = new List<Vector2Int>();

            for (int i = 0; i < bestResult.Path.Count; i++)
            {
                bestPath.Add(bestResult.Path[i]);
            }

            return bestPath.Count >= 2;
        }

        private TacticalPathProfile BuildPathProfile(
            Vector2Int startCoordinates,
            Vector2Int targetCoordinates
        )
        {
            bool targetIsDiagonal =
                startCoordinates.x != targetCoordinates.x
                && startCoordinates.y != targetCoordinates.y;

            return new TacticalPathProfile(
                stepCost,
                heuristicCost,
                turnPenaltyCost,
                reversePenaltyCost,
                zigZagBalanceCost: targetIsDiagonal ? 12 : 0,
                obstacleHuggingReward: 0,
                directionalProgressReward: 0,
                maximumExtraStepCount
            );
        }

        private void FacePlayerTowardZombie(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                return;
            }

            GridDirection direction = GetDirectionTowardCell(
                playerGridPosition.CurrentCell,
                zombie.CurrentCell
            );

            playerGridMover.FaceDirection(direction);
        }

        private static bool AreAdjacent(Vector2Int a, Vector2Int b)
        {
            return GetGridDistance(a, b) == 1;
        }

        private static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static GridDirection GetDirectionTowardCell(Vector2Int fromCell, Vector2Int toCell)
        {
            Vector2Int difference = toCell - fromCell;

            if (difference == Vector2Int.zero)
            {
                return GridDirection.None;
            }

            int absoluteX = Mathf.Abs(difference.x);
            int absoluteY = Mathf.Abs(difference.y);

            if (absoluteX >= absoluteY)
            {
                return difference.x > 0
                    ? GridDirection.Right
                    : GridDirection.Left;
            }

            return difference.y > 0
                ? GridDirection.Up
                : GridDirection.Down;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (targetingController == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionExecutionController)} on '{name}' is missing the " +
                    $"{nameof(ZombieStrikeTargetingController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (dangerTurnController == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionExecutionController)} on '{name}' is missing the " +
                    $"{nameof(DangerTurnController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (navigationGrid == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionExecutionController)} on '{name}' is missing the " +
                    $"{nameof(NavigationGrid)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerGridPosition == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionExecutionController)} on '{name}' is missing the " +
                    $"{nameof(PlayerGridPosition)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerGridMover == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionExecutionController)} on '{name}' is missing the " +
                    $"{nameof(PlayerGridMover)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            stepCost = Mathf.Max(1, stepCost);
            heuristicCost = Mathf.Max(0, heuristicCost);
            turnPenaltyCost = Mathf.Max(0, turnPenaltyCost);
            reversePenaltyCost = Mathf.Max(turnPenaltyCost, reversePenaltyCost);
            maximumExtraStepCount = Mathf.Max(0, maximumExtraStepCount);
        }
    }
}