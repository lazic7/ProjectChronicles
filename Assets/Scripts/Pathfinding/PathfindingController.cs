using System.Collections.Generic;
using System.Text;
using IsometricPathfinding.Combat;
using IsometricPathfinding.Input;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using IsometricPathfinding.UI;
using IsometricPathfinding.Zombies;
using UnityEngine;
using UnityEngine.InputSystem;

// ReSharper disable once CheckNamespace
namespace IsometricPathfinding.Pathfinding
{
    [DisallowMultipleComponent]
    public sealed class PathfindingController : MonoBehaviour
    {
        [Header("Scene References")]
        
        [SerializeField] private NavigationGrid navigationGrid;

        [SerializeField] private PlayerGridPosition playerGridPosition;

        [SerializeField] private PlayerGridMover playerGridMover;

        [SerializeField] private MouseTileSelector mouseTileSelector;

        [SerializeField] private PathPreviewRenderer pathPreviewRenderer;

        [SerializeField] private DangerTurnController dangerTurnController;
        
        [Header("Tactical Path Costs")]

        [SerializeField] [Min(1)] private int stepCost = 100;

        [SerializeField] [Min(0)] private int heuristicCost = 60;

        [SerializeField] [Min(0)] private int turnPenaltyCost = 8;

        [SerializeField] [Min(0)] private int reversePenaltyCost = 20;

        [SerializeField] private bool preferDiagonalZigZag = true;

        [SerializeField] [Min(0)] private int diagonalZigZagBalanceCost = 12;

        [SerializeField] private bool preferObstacleHugging = true;

        [SerializeField] [Min(0)] private int obstacleHuggingReward = 8;

        [SerializeField] [Min(0)] private int directionalProgressReward = 4;

        [SerializeField] [Min(0)] private int maximumExtraStepCount = 4;

        [Header("Runtime State")]

        [SerializeField] private bool hasValidPath;

        [SerializeField] private int movementStepCount;

        [SerializeField] private GridDirection pathInitialFacingDirection;

        [SerializeField] private int turnPenaltyScore;

        [SerializeField] private int tacticalScore;

        [SerializeField] private int stepScore;

        [SerializeField] private int heuristicScore;

        [SerializeField] private int zigZagScore;

        [SerializeField] private int obstacleHuggingScore;

        [SerializeField] private int directionalProgressScore;

        [SerializeField] private int playerMovementPoints = 5;

        [Header("Debug")]

        [SerializeField] private bool logPathResults;

        private AStarPathfinder pathfinder;

        private readonly List<Vector2Int> currentPath = new List<Vector2Int>();

        private bool hasProcessedHover;
        private bool lastTargetWasWalkable;

        private Vector2Int lastStartCoordinates;
        private Vector2Int lastTargetCoordinates;

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

        private void LateUpdate()
        {
            /*
             * Dok se igrač kreće ne prikazujemo novu
             * hover putanju i ne prihvaćamo novi cilj.
             */

            if (playerGridMover.IsMoving)
            {
                if (hasProcessedHover || currentPath.Count > 0 || hasValidPath)
                {
                    ClearCurrentPath();
                }

                return;
            }

            if (!mouseTileSelector.HasHoveredCell)
            {
                if (hasProcessedHover || currentPath.Count > 0)
                {
                    ClearCurrentPath();
                }

                return;
            }

            Vector2Int startCoordinates = playerGridPosition.CurrentCell;

            Vector2Int targetCoordinates = mouseTileSelector.HoveredCell;

            bool targetIsWalkable = mouseTileSelector.HoveredCellIsWalkable;

            bool resultIsAlreadyCurrent =
                hasProcessedHover
                && startCoordinates == lastStartCoordinates
                && targetCoordinates == lastTargetCoordinates
                && targetIsWalkable == lastTargetWasWalkable;

            /*
             * A* pokrećemo samo kada se promijenio:
             *
             * - početak
             * - cilj
             * - prohodnost cilja
             */

            if (!resultIsAlreadyCurrent)
            {
                hasProcessedHover = true;

                lastStartCoordinates = startCoordinates;

                lastTargetCoordinates = targetCoordinates;

                lastTargetWasWalkable = targetIsWalkable;

                CalculateAndDisplayPath(startCoordinates, targetCoordinates);
            }

            HandleMovementClick();
        }

        private void HandleMovementClick()
        {
            if (Mouse.current == null)
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (!hasValidPath)
            {
                return;
            }

            /*
             * Put od samo jednog nodea znači da je cilj
             * trenutačna ćelija igrača.
             *
             * Nema nijednog stvarnog koraka.
             */

            if (currentPath.Count < 2)
            {
                return;
            }
            
            if (dangerTurnController != null
                && dangerTurnController.GameMode == GameMode.Danger
                && dangerTurnController.CurrentPhase != DangerTurnPhase.PlayerTurn)
            {
                return;
            }

            IReadOnlyList<Vector2Int> pathToUse = LimitPathToMovementPoints(currentPath, playerMovementPoints);

            bool movementStarted = playerGridMover.TryMoveAlongPath(pathToUse);

            if (!movementStarted)
            {
                return;
            }

            /*
             * PlayerGridMover je napravio vlastitu kopiju.
             * Sada sigurno možemo ukloniti preview.
             */

            ClearCurrentPath();
        }

        private void CalculateAndDisplayPath(
            Vector2Int startCoordinates,
            Vector2Int targetCoordinates
        )
        {
            currentPath.Clear();

            hasValidPath = false;
            movementStepCount = 0;
            turnPenaltyScore = 0;
            tacticalScore = 0;
            stepScore = 0;
            heuristicScore = 0;
            zigZagScore = 0;
            obstacleHuggingScore = 0;
            directionalProgressScore = 0;

            pathInitialFacingDirection = playerGridMover.FacingDirection;

            Vector2Int requestedTargetCoordinates = targetCoordinates;

            if (!TryResolveMovementTarget(
                    startCoordinates,
                    requestedTargetCoordinates,
                    out targetCoordinates
                ))
            {
                pathPreviewRenderer.ShowInvalid(requestedTargetCoordinates);

                LogInvalidTarget(requestedTargetCoordinates, "the target cell is occupied");

                return;
            }

            bool targetIsWalkable = navigationGrid.IsWalkable(targetCoordinates);

            if (!targetIsWalkable)
            {
                pathPreviewRenderer.ShowInvalid(requestedTargetCoordinates);

                LogInvalidTarget(requestedTargetCoordinates, "the target cell is blocked");

                return;
            }

            bool pathWasFound = pathfinder.TryFindPath(
                startCoordinates,
                targetCoordinates,
                pathInitialFacingDirection,
                BuildPlayerPathProfile(startCoordinates, targetCoordinates),
                out TacticalPathResult pathResult
            );

            if (!pathWasFound)
            {
                pathPreviewRenderer.ShowInvalid(requestedTargetCoordinates);

                LogInvalidTarget(targetCoordinates, "no route exists");

                return;
            }

            currentPath.AddRange(LimitPathToMovementPoints(pathResult.Path, playerMovementPoints));

            hasValidPath = currentPath.Count >= 2;

            movementStepCount = Mathf.Max(0, currentPath.Count - 1);

            turnPenaltyScore = pathResult.TurnPenalty;

            tacticalScore = pathResult.TotalScore;
            stepScore = pathResult.Score.StepScore;
            heuristicScore = pathResult.Score.HeuristicScore;
            zigZagScore = pathResult.Score.ZigZagScore;
            obstacleHuggingScore = pathResult.Score.ObstacleHuggingScore;
            directionalProgressScore = pathResult.Score.DirectionalProgressScore;

            pathPreviewRenderer.ShowPath(currentPath, requestedTargetCoordinates);

            if (logPathResults)
            {
                Debug.Log(
                    $"Path found from "
                    + $"{startCoordinates} to "
                    + $"{targetCoordinates}. "
                    + $"Initial facing: "
                    + $"{pathInitialFacingDirection}. "
                    + $"Steps shown: {movementStepCount}. "
                    + $"Full path steps: {pathResult.StepCount}. "
                    + $"Tactical score: {tacticalScore}. "
                    + $"Step score: {stepScore}. "
                    + $"Heuristic score: {heuristicScore}. "
                    + $"Turn score: {turnPenaltyScore}. "
                    + $"Zigzag score: {zigZagScore}. "
                    + $"Obstacle score: {obstacleHuggingScore}. "
                    + $"Directional score: {directionalProgressScore}. "
                    + $"Route: "
                    + $"{BuildPathText(currentPath)}",
                    this
                );
            }
        }

        private TacticalPathProfile BuildPlayerPathProfile(
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
                preferDiagonalZigZag && targetIsDiagonal
                    ? diagonalZigZagBalanceCost
                    : 0,
                preferObstacleHugging
                    ? obstacleHuggingReward
                    : 0,
                directionalProgressReward,
                maximumExtraStepCount
            );
        }

        private bool TryResolveMovementTarget(
            Vector2Int startCoordinates,
            Vector2Int requestedTargetCoordinates,
            out Vector2Int movementTargetCoordinates
        )
        {
            movementTargetCoordinates = requestedTargetCoordinates;

            if (!navigationGrid.TryGetOccupant(requestedTargetCoordinates, out GameObject occupant))
            {
                return true;
            }

            if (occupant == playerGridMover.gameObject)
            {
                return true;
            }

            ZombieAgent zombie = occupant.GetComponent<ZombieAgent>();

            if (zombie == null)
            {
                return false;
            }

            return TryFindBestAdjacentCellToOccupiedTarget(
                startCoordinates,
                requestedTargetCoordinates,
                out movementTargetCoordinates
            );
        }

        private bool TryFindBestAdjacentCellToOccupiedTarget(
            Vector2Int startCoordinates,
            Vector2Int occupiedTargetCoordinates,
            out Vector2Int bestCell
        )
        {
            bestCell = default;

            Vector2Int[] candidates =
            {
                occupiedTargetCoordinates + Vector2Int.up,
                occupiedTargetCoordinates + Vector2Int.down,
                occupiedTargetCoordinates + Vector2Int.left,
                occupiedTargetCoordinates + Vector2Int.right,
            };

            bool foundCandidate = false;
            int bestTacticalScore = int.MaxValue;
            int bestStepCount = int.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2Int candidate = candidates[i];

                if (candidate != startCoordinates
                    && !navigationGrid.IsWalkableForActor(candidate, playerGridMover.gameObject))
                {
                    continue;
                }

                bool pathWasFound = pathfinder.TryFindPath(
                    startCoordinates,
                    candidate,
                    playerGridMover.FacingDirection,
                    BuildPlayerPathProfile(startCoordinates, candidate),
                    out TacticalPathResult candidateResult
                );

                if (!pathWasFound)
                {
                    continue;
                }

                int candidateStepCount = candidateResult.StepCount;
                int candidateTacticalScore = candidateResult.TotalScore;

                bool candidateIsBetter =
                    !foundCandidate
                    || candidateTacticalScore < bestTacticalScore
                    || (candidateTacticalScore == bestTacticalScore
                        && candidateStepCount < bestStepCount);

                if (!candidateIsBetter)
                {
                    continue;
                }

                foundCandidate = true;
                bestCell = candidate;
                bestTacticalScore = candidateTacticalScore;
                bestStepCount = candidateStepCount;
            }

            return foundCandidate;
        }

        private void ClearCurrentPath()
        {
            hasProcessedHover = false;

            currentPath.Clear();

            hasValidPath = false;
            movementStepCount = 0;
            turnPenaltyScore = 0;
            tacticalScore = 0;
            stepScore = 0;
            heuristicScore = 0;
            zigZagScore = 0;
            obstacleHuggingScore = 0;
            directionalProgressScore = 0;

            pathInitialFacingDirection = GridDirection.None;

            pathPreviewRenderer.Clear();
        }

        private static List<Vector2Int> LimitPathToMovementPoints(
            IReadOnlyList<Vector2Int> path,
            int movementPoints
        )
        {
            List<Vector2Int> limitedPath = new List<Vector2Int>();

            if (path == null || path.Count == 0)
            {
                return limitedPath;
            }

            /*
             * path[0] is the current player cell.
             *
             * movementPoints means actual movement steps.
             *
             * Example:
             * movementPoints = 4
             *
             * path indices allowed:
             * 0 = start cell
             * 1 = step 1
             * 2 = step 2
             * 3 = step 3
             * 4 = step 4
             */

            int safeMovementPoints = Mathf.Max(0, movementPoints);

            int maxIndex = Mathf.Min(path.Count - 1, safeMovementPoints);

            for (int i = 0; i <= maxIndex; i++)
            {
                limitedPath.Add(path[i]);
            }

            return limitedPath;
        }

        private void LogInvalidTarget(Vector2Int targetCoordinates, string reason)
        {
            if (!logPathResults)
            {
                return;
            }

            Debug.Log($"No valid path to " + $"{targetCoordinates}: {reason}.", this);
        }

        private static string BuildPathText(IReadOnlyList<Vector2Int> path)
        {
            StringBuilder builder = new StringBuilder();

            for (int index = 0; index < path.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(" -> ");
                }

                builder.Append(path[index]);
            }

            return builder.ToString();
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (navigationGrid == null)
            {
                Debug.LogError(
                    $"{nameof(PathfindingController)} "
                        + $"on '{name}' is missing the "
                        + "Navigation Grid.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerGridPosition == null)
            {
                Debug.LogError(
                    $"{nameof(PathfindingController)} "
                        + $"on '{name}' is missing the "
                        + "Player Grid Position.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerGridMover == null)
            {
                Debug.LogError(
                    $"{nameof(PathfindingController)} "
                        + $"on '{name}' is missing the "
                        + "Player Grid Mover.",
                    this
                );

                referencesAreValid = false;
            }

            if (mouseTileSelector == null)
            {
                Debug.LogError(
                    $"{nameof(PathfindingController)} "
                        + $"on '{name}' is missing the "
                        + "Mouse Tile Selector.",
                    this
                );

                referencesAreValid = false;
            }

            if (pathPreviewRenderer == null)
            {
                Debug.LogError(
                    $"{nameof(PathfindingController)} "
                        + $"on '{name}' is missing the "
                        + "Path Preview Renderer.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnDisable()
        {
            if (pathPreviewRenderer != null)
            {
                pathPreviewRenderer.Clear();
            }
        }

        private void OnValidate()
        {
            stepCost = Mathf.Max(1, stepCost);

            heuristicCost = Mathf.Max(0, heuristicCost);

            turnPenaltyCost = Mathf.Max(0, turnPenaltyCost);

            reversePenaltyCost = Mathf.Max(turnPenaltyCost, reversePenaltyCost);

            playerMovementPoints = Mathf.Max(1, playerMovementPoints);

            diagonalZigZagBalanceCost = Mathf.Max(0, diagonalZigZagBalanceCost);

            obstacleHuggingReward = Mathf.Clamp(
                obstacleHuggingReward,
                0,
                stepCost / 4
            );

            directionalProgressReward = Mathf.Clamp(
                directionalProgressReward,
                0,
                stepCost / 4
            );

            maximumExtraStepCount = Mathf.Max(0, maximumExtraStepCount);
        }
    }
}
