using System.Collections.Generic;
using System.Text;
using IsometricPathfinding.Combat;
using IsometricPathfinding.Input;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using IsometricPathfinding.Zombies;
using IsometricPathfinding.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Path Preferences")]
        
        [SerializeField] [Min(0)] private int turnPenaltyCost = 1;

        [SerializeField] [Min(0)] private int reversePenaltyCost = 2;
        
        [SerializeField] private bool preferDiagonalZigZag = true;

        [SerializeField] [Min(0)] private int diagonalZigZagBalanceCost = 10;

        [SerializeField] private bool preferObstacleHugging = true;

        [SerializeField] [Min(0)] private int obstacleHuggingReward = 1;

        [Header("Runtime State")]
        
        [SerializeField] private bool hasValidPath;

        [SerializeField] private Vector2Int currentTarget;

        [SerializeField] private int movementStepCount;

        [SerializeField] private GridDirection pathInitialFacingDirection;

        [SerializeField] private int turnPenaltyScore;

        [SerializeField] private int playerMovementPoints = 5;

        [Header("Debug")]
        
        [SerializeField] private bool logPathResults;
        
        private AStarPathfinder pathfinder;

        private readonly List<Vector2Int> currentPath = new List<Vector2Int>();

        private bool hasProcessedHover;
        private bool lastTargetWasWalkable;

        private Vector2Int lastStartCoordinates;
        private Vector2Int lastTargetCoordinates;

        public bool HasValidPath => hasValidPath;

        public IReadOnlyList<Vector2Int> CurrentPath => currentPath;

        public int MovementStepCount => movementStepCount;

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

                currentTarget = targetCoordinates;

                CalculateAndDisplayPath(startCoordinates, targetCoordinates, targetIsWalkable);
            }

            /*
             * Klik provjeravamo i kada rezultat nije nov.
             *
             * Korisnik će često prvo nekoliko trenutaka
             * držati miš nad ciljem, a zatim kliknuti.
             */

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
            
            if(dangerTurnController != null && dangerTurnController.GameMode == GameMode.Danger && dangerTurnController.CurrentPhase != DangerTurnPhase.PlayerTurn)
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
            Vector2Int targetCoordinates,
            bool targetIsWalkable
        )
        {
            currentPath.Clear();

            hasValidPath = false;
            movementStepCount = 0;
            turnPenaltyScore = 0;

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

            currentTarget = targetCoordinates;

            targetIsWalkable = navigationGrid.IsWalkable(targetCoordinates);

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
                BuildPlayerPathPreferences(startCoordinates, targetCoordinates),
                out List<Vector2Int> foundPath,
                out int foundTurnPenalty
            );

            if (!pathWasFound)
            {
                pathPreviewRenderer.ShowInvalid(requestedTargetCoordinates);

                LogInvalidTarget(targetCoordinates, "no route exists");

                return;
            }

            currentPath.AddRange(LimitPathToMovementPoints(foundPath, playerMovementPoints));

            hasValidPath = currentPath.Count >= 2;

            movementStepCount = Mathf.Max(0, currentPath.Count - 1);

            turnPenaltyScore = foundTurnPenalty;

            pathPreviewRenderer.ShowPath(currentPath, requestedTargetCoordinates);

            if (logPathResults)
            {
                Debug.Log(
                    $"Path found from "
                        + $"{startCoordinates} to "
                        + $"{targetCoordinates}. "
                        + $"Initial facing: "
                        + $"{pathInitialFacingDirection}. "
                        + $"Steps: {movementStepCount}. "
                        + $"Turn penalty: "
                        + $"{turnPenaltyScore}. "
                        + $"Route: "
                        + $"{BuildPathText(currentPath)}",
                    this
                );
            }
        }
        
        private PathPreferenceSettings BuildPlayerPathPreferences(
            Vector2Int startCoordinates,
            Vector2Int targetCoordinates
        )
        {
            bool targetIsDiagonal =
                startCoordinates.x != targetCoordinates.x
                && startCoordinates.y != targetCoordinates.y;

            return new PathPreferenceSettings(
                preferDiagonalZigZag && targetIsDiagonal,
                diagonalZigZagBalanceCost,
                preferObstacleHugging ? obstacleHuggingReward : 0
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
            int bestStepCount = int.MaxValue;
            int bestTurnPenalty = int.MaxValue;

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
                    BuildPlayerPathPreferences(startCoordinates, candidate),
                    out List<Vector2Int> candidatePath,
                    out int candidateTurnPenalty
                );

                if (!pathWasFound)
                {
                    continue;
                }

                int candidateStepCount = Mathf.Max(0, candidatePath.Count - 1);

                bool candidateIsBetter =
                    !foundCandidate
                    || candidateStepCount < bestStepCount
                    || candidateStepCount == bestStepCount
                    && candidateTurnPenalty < bestTurnPenalty;

                if (!candidateIsBetter)
                {
                    continue;
                }

                foundCandidate = true;
                bestCell = candidate;
                bestStepCount = candidateStepCount;
                bestTurnPenalty = candidateTurnPenalty;
            }

            return foundCandidate;
        }

        private void ClearCurrentPath()
        {
            hasProcessedHover = false;

            currentPath.Clear();

            hasValidPath = false;
            currentTarget = default;
            movementStepCount = 0;
            turnPenaltyScore = 0;

            pathInitialFacingDirection = GridDirection.None;

            pathPreviewRenderer.Clear();
        }

        private static List<Vector2Int> LimitPathToMovementPoints(IReadOnlyList<Vector2Int> path, int movementPoints)
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
             * movementPoints = 6
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
            
            for(int i = 0; i <= maxIndex; i++)
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
            turnPenaltyCost = Mathf.Max(0, turnPenaltyCost);

            reversePenaltyCost = Mathf.Max(turnPenaltyCost, reversePenaltyCost);
            
            playerMovementPoints = Mathf.Max(1, playerMovementPoints);
            
            diagonalZigZagBalanceCost = Mathf.Max(0, diagonalZigZagBalanceCost);

            obstacleHuggingReward = Mathf.Max(0, obstacleHuggingReward);
        }
    }
}
