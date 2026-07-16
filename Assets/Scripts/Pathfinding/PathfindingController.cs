using System.Collections.Generic;
using System.Text;
using IsometricPathfinding.Input;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using IsometricPathfinding.UI;
using UnityEngine;

namespace IsometricPathfinding.Pathfinding
{
    [DisallowMultipleComponent]
    public sealed class PathfindingController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        private NavigationGrid navigationGrid;

        [SerializeField]
        private PlayerGridPosition playerGridPosition;

        [SerializeField]
        private MouseTileSelector mouseTileSelector;

        [SerializeField]
        private PathPreviewRenderer pathPreviewRenderer;

        [Header("Runtime State")]
        [SerializeField]
        private bool hasValidPath;

        [SerializeField]
        private Vector2Int currentTarget;

        [SerializeField]
        private int movementStepCount;

        [Header("Debug")]
        [SerializeField]
        private bool logPathResults = true;

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

            pathfinder = new AStarPathfinder(navigationGrid);
        }

        private void LateUpdate()
        {
            if (!mouseTileSelector.HasHoveredCell)
            {
                if (hasProcessedHover)
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

            if (resultIsAlreadyCurrent)
            {
                return;
            }

            hasProcessedHover = true;

            lastStartCoordinates = startCoordinates;

            lastTargetCoordinates = targetCoordinates;

            lastTargetWasWalkable = targetIsWalkable;

            currentTarget = targetCoordinates;

            CalculateAndDisplayPath(startCoordinates, targetCoordinates, targetIsWalkable);
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

            if (!targetIsWalkable)
            {
                pathPreviewRenderer.ShowInvalid(targetCoordinates);

                LogInvalidTarget(targetCoordinates, "the target cell is blocked");

                return;
            }

            bool pathWasFound = pathfinder.TryFindPath(
                startCoordinates,
                targetCoordinates,
                out List<Vector2Int> foundPath
            );

            if (!pathWasFound)
            {
                pathPreviewRenderer.ShowInvalid(targetCoordinates);

                LogInvalidTarget(targetCoordinates, "no route exists");

                return;
            }

            currentPath.AddRange(foundPath);

            hasValidPath = true;

            movementStepCount = Mathf.Max(0, currentPath.Count - 1);

            pathPreviewRenderer.ShowPath(currentPath);

            if (logPathResults)
            {
                Debug.Log(
                    $"Path found from "
                        + $"{startCoordinates} to "
                        + $"{targetCoordinates}. "
                        + $"Steps: {movementStepCount}. "
                        + $"Route: "
                        + $"{BuildPathText(currentPath)}",
                    this
                );
            }
        }

        private void ClearCurrentPath()
        {
            hasProcessedHover = false;

            currentPath.Clear();

            hasValidPath = false;
            currentTarget = default;
            movementStepCount = 0;

            pathPreviewRenderer.Clear();
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
    }
}
