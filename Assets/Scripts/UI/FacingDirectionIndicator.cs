using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using IsometricPathfinding.Zombies;
using UnityEngine;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class FacingDirectionIndicator : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private NavigationGrid navigationGrid;

        [SerializeField] private PlayerGridMover playerGridMover;

        [SerializeField] private PlayerGridPosition playerGridPosition;

        [SerializeField] private ZombieAgent zombieAgent;

        [Header("Line Settings")]
        [SerializeField] [Min(0.05f)] private float lineLength = 0.45f;

        [SerializeField] [Min(0.001f)] private float lineWidth = 0.05f;

        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.25f, -0.05f);

        [SerializeField] private Color lineColor = Color.yellow;

        [SerializeField] private int sortingOrder = 100;

        private LineRenderer lineRenderer;

        private void Reset()
        {
            lineRenderer = GetComponent<LineRenderer>();

            playerGridMover = GetComponentInParent<PlayerGridMover>();
            playerGridPosition = GetComponentInParent<PlayerGridPosition>();
            zombieAgent = GetComponentInParent<ZombieAgent>();
        }

        private void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (playerGridMover == null)
            {
                playerGridMover = GetComponentInParent<PlayerGridMover>();
            }

            if (playerGridPosition == null)
            {
                playerGridPosition = GetComponentInParent<PlayerGridPosition>();
            }

            if (zombieAgent == null)
            {
                zombieAgent = GetComponentInParent<ZombieAgent>();
            }

            ConfigureLineRenderer();

            if (!ValidateReferences())
            {
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            UpdateLine();
        }

        private void ConfigureLineRenderer()
        {
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;

            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;

            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            lineRenderer.sortingOrder = sortingOrder;

            /*
             * This creates a simple runtime material if none is assigned.
             * Useful while prototyping with capsules and no custom assets.
             */
            if (lineRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    lineRenderer.sharedMaterial = new Material(shader);
                }
            }
        }

        private void UpdateLine()
        {
            GridDirection facingDirection = GetFacingDirection();

            if (facingDirection == GridDirection.None)
            {
                lineRenderer.enabled = false;
                return;
            }

            Vector2Int currentCell = GetCurrentCell();

            Vector2Int directionVector = GetVectorFromDirection(facingDirection);

            if (directionVector == Vector2Int.zero)
            {
                lineRenderer.enabled = false;
                return;
            }

            Vector3 startPosition = GetStartWorldPosition();
            Vector3 directionWorld = GetWorldDirection(currentCell, directionVector);

            if (directionWorld == Vector3.zero)
            {
                lineRenderer.enabled = false;
                return;
            }

            Vector3 endPosition = startPosition + directionWorld.normalized * lineLength;

            lineRenderer.enabled = true;

            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);
        }

        private GridDirection GetFacingDirection()
        {
            if (playerGridMover != null)
            {
                return playerGridMover.FacingDirection;
            }

            if (zombieAgent != null)
            {
                return zombieAgent.FacingDirection;
            }

            return GridDirection.None;
        }

        private Vector2Int GetCurrentCell()
        {
            if (playerGridPosition != null)
            {
                return playerGridPosition.CurrentCell;
            }

            if (zombieAgent != null)
            {
                return zombieAgent.CurrentCell;
            }

            return Vector2Int.zero;
        }

        private Vector3 GetStartWorldPosition()
        {
            Transform actorTransform = GetActorTransform();

            if (actorTransform != null)
            {
                return actorTransform.position + worldOffset;
            }

            return transform.position + worldOffset;
        }
        
        private Transform GetActorTransform()
        {
            if (playerGridMover != null)
            {
                return playerGridMover.transform;
            }

            if (zombieAgent != null)
            {
                return zombieAgent.transform;
            }

            return transform;
        }

        private Vector3 GetWorldDirection(Vector2Int currentCell, Vector2Int directionVector)
        {
            if (navigationGrid == null)
            {
                return new Vector3(directionVector.x, directionVector.y, 0f);
            }

            Vector3 currentWorldPosition = navigationGrid.GetCellCenterWorld(currentCell);

            Vector2Int targetCell = currentCell + directionVector;

            Vector3 targetWorldPosition = navigationGrid.GetCellCenterWorld(targetCell);

            return targetWorldPosition - currentWorldPosition;
        }

        private static Vector2Int GetVectorFromDirection(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.Up:
                    return Vector2Int.up;

                case GridDirection.Down:
                    return Vector2Int.down;

                case GridDirection.Left:
                    return Vector2Int.left;

                case GridDirection.Right:
                    return Vector2Int.right;

                default:
                    return Vector2Int.zero;
            }
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            bool hasPlayerReferences = playerGridMover != null && playerGridPosition != null;
            bool hasZombieReference = zombieAgent != null;

            if (!hasPlayerReferences && !hasZombieReference)
            {
                Debug.LogError(
                    $"{nameof(FacingDirectionIndicator)} on '{name}' needs either " +
                    $"a {nameof(PlayerGridMover)} with {nameof(PlayerGridPosition)}, " +
                    $"or a {nameof(ZombieAgent)}.",
                    this
                );

                referencesAreValid = false;
            }

            if (lineRenderer == null)
            {
                Debug.LogError(
                    $"{nameof(FacingDirectionIndicator)} on '{name}' is missing a LineRenderer.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            lineLength = Mathf.Max(0.05f, lineLength);
            lineWidth = Mathf.Max(0.001f, lineWidth);

            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;

            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;

            lineRenderer.sortingOrder = sortingOrder;
        }
    }
}