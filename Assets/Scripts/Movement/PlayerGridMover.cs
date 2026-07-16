using System.Collections.Generic;
using IsometricPathfinding.Navigation;
using UnityEngine;

namespace IsometricPathfinding.Movement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerGridPosition))]
    public sealed class PlayerGridMover : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        private NavigationGrid navigationGrid;

        [SerializeField]
        private PlayerGridPosition playerGridPosition;

        [Header("Movement Settings")]
        [SerializeField]
        [Min(0.01f)]
        private float movementSpeed = 2f;

        [SerializeField]
        [Min(0.0001f)]
        private float arrivalTolerance = 0.001f;

        [Header("Runtime State")]
        [SerializeField]
        private bool isMoving;

        [SerializeField]
        private Vector2Int currentTargetCell;

        [SerializeField]
        private int remainingStepCount;

        private readonly List<Vector2Int> activePath = new List<Vector2Int>();

        private int nextPathIndex;

        public bool IsMoving => isMoving;

        public Vector2Int CurrentTargetCell => currentTargetCell;

        public int RemainingStepCount => remainingStepCount;

        private void Reset()
        {
            playerGridPosition = GetComponent<PlayerGridPosition>();
        }

        private void Awake()
        {
            if (playerGridPosition == null)
            {
                playerGridPosition = GetComponent<PlayerGridPosition>();
            }

            if (!ValidateReferences())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (!isMoving)
            {
                return;
            }

            MoveTowardsCurrentTarget();
        }

        public bool TryMoveAlongPath(IReadOnlyList<Vector2Int> path)
        {
            if (isMoving)
            {
                return false;
            }

            /*
             * Put mora sadržavati najmanje:
             *
             * indeks 0 = trenutačna ćelija igrača
             * indeks 1 = prva ćelija prema kojoj ide
             */

            if (path == null || path.Count < 2)
            {
                return false;
            }

            if (path[0] != playerGridPosition.CurrentCell)
            {
                Debug.LogWarning(
                    $"Movement path begins at {path[0]}, "
                        + $"but the player is currently at "
                        + $"{playerGridPosition.CurrentCell}.",
                    this
                );

                return false;
            }

            if (!IsPathValid(path))
            {
                return false;
            }

            /*
             * Ne čuvamo referencu na listu koju posjeduje
             * PathfindingController.
             *
             * Radimo vlastitu kopiju kako bi controller
             * mogao odmah očistiti preview i CurrentPath,
             * a da igrač ipak nastavi koristiti svoj put.
             */

            activePath.Clear();

            for (int index = 0; index < path.Count; index++)
            {
                activePath.Add(path[index]);
            }

            /*
             * Indeks 0 preskačemo jer igrač već stoji
             * na početnoj ćeliji.
             */

            nextPathIndex = 1;

            currentTargetCell = activePath[nextPathIndex];

            remainingStepCount = activePath.Count - nextPathIndex;

            isMoving = true;

            return true;
        }

        private void MoveTowardsCurrentTarget()
        {
            Vector3 targetWorldPosition = navigationGrid.GetCellCenterWorld(currentTargetCell);

            float maximumMovementThisFrame = movementSpeed * Time.deltaTime;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetWorldPosition,
                maximumMovementThisFrame
            );

            float squaredDistance = (transform.position - targetWorldPosition).sqrMagnitude;

            float squaredTolerance = arrivalTolerance * arrivalTolerance;

            if (squaredDistance > squaredTolerance)
            {
                return;
            }

            /*
             * Osiguravamo da se igrač nalazi točno na
             * centru ćelije, bez malih floating-point
             * odstupanja.
             */

            transform.position = targetWorldPosition;

            /*
             * Tek nakon stvarnog dolaska mijenjamo
             * logičku grid poziciju igrača.
             */

            playerGridPosition.SetCurrentCell(currentTargetCell);

            nextPathIndex++;

            if (nextPathIndex >= activePath.Count)
            {
                CompleteMovement();
                return;
            }

            currentTargetCell = activePath[nextPathIndex];

            remainingStepCount = activePath.Count - nextPathIndex;
        }

        private bool IsPathValid(IReadOnlyList<Vector2Int> path)
        {
            for (int index = 0; index < path.Count; index++)
            {
                Vector2Int coordinates = path[index];

                if (!navigationGrid.TryGetNode(coordinates, out GridNode node))
                {
                    Debug.LogWarning(
                        $"Movement path contains cell "
                            + $"{coordinates}, which is not part "
                            + "of the Navigation Grid.",
                        this
                    );

                    return false;
                }

                if (!node.IsWalkable)
                {
                    Debug.LogWarning(
                        $"Movement path contains blocked " + $"cell {coordinates}.",
                        this
                    );

                    return false;
                }

                if (index == 0)
                {
                    continue;
                }

                Vector2Int previousCoordinates = path[index - 1];

                Vector2Int difference = coordinates - previousCoordinates;

                int cardinalDistance = Mathf.Abs(difference.x) + Mathf.Abs(difference.y);

                if (cardinalDistance != 1)
                {
                    Debug.LogWarning(
                        $"Invalid movement step from "
                            + $"{previousCoordinates} to "
                            + $"{coordinates}. Each step must "
                            + "move to exactly one cardinal "
                            + "neighbor.",
                        this
                    );

                    return false;
                }
            }

            return true;
        }

        private void CompleteMovement()
        {
            isMoving = false;

            currentTargetCell = playerGridPosition.CurrentCell;

            remainingStepCount = 0;
            nextPathIndex = 0;

            activePath.Clear();
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (navigationGrid == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerGridMover)} on "
                        + $"'{name}' is missing the "
                        + "Navigation Grid.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerGridPosition == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerGridMover)} on "
                        + $"'{name}' is missing the "
                        + "Player Grid Position.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0.01f, movementSpeed);

            arrivalTolerance = Mathf.Max(0.0001f, arrivalTolerance);
        }
    }
}
