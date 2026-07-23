using System;
using System.Collections.Generic;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using UnityEngine;

namespace IsometricPathfinding.Zombies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ZombieGridPosition))]
    public sealed class ZombieGridMover : MonoBehaviour
    {
       [SerializeField] private NavigationGrid navigationGrid;
       
       [SerializeField] private ZombieGridPosition zombieGridPosition;

       [SerializeField] [Min(0.01f)] private float movementSpeed = 3f;
       
       [SerializeField] [Min(0.0001f)] private float arrivalTolerance = 0.001f;
       
       [SerializeField] private GridDirection currentMovementDirection;

       [SerializeField] private GridDirection facingDirection = GridDirection.Down;
       
       private readonly List<Vector2Int> activePath = new List<Vector2Int>();

       private int nextPathIndex;
       
       private Vector2Int currentTargetCell;
       
       private bool isMoving;
       
       public Vector2Int CurrentTargetCell => currentTargetCell;

       public GridDirection CurrentMovementDirection => currentMovementDirection;

       public GridDirection FacingDirection => facingDirection;
       
       public bool IsMoving => isMoving;

       public event EventHandler<EventArgs> MovementCompleted;

       private void Awake()
       {
           if (zombieGridPosition == null)
           {
               zombieGridPosition = GetComponent<ZombieGridPosition>();
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

           if (path == null || path.Count < 2)
           {
               return false;
           }

           if (path[0] != zombieGridPosition.CurrentCell)
           {
               return false;
           }
           
           activePath.Clear();

           for (int i = 0; i < path.Count; i++)
           {
               activePath.Add(path[i]);
           }

           nextPathIndex = 1;
           SetCurrentTarget(activePath[nextPathIndex]);
           isMoving = true;

           return true;
       }

       private void MoveTowardsCurrentTarget()
       {
           Vector3 targetWorldPosition = navigationGrid.GetCellCenterWorld(currentTargetCell);
           
           transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, movementSpeed * Time.deltaTime);
           
           float squaredDistance = (transform.position - targetWorldPosition).sqrMagnitude;
           float squaredTolerance = arrivalTolerance * arrivalTolerance;

           if (squaredDistance > squaredTolerance)
           {
               return;
           }
           
           /*
            * Snap exactly to the center of the target cell.
            */
           transform.position = targetWorldPosition;

           /*
            * This is very important.
            * The logical zombie grid position must change when the zombie reaches a cell.
            */
           zombieGridPosition.SetCurrentCell(currentTargetCell);

           nextPathIndex++;

           if (nextPathIndex >= activePath.Count)
           {
               CompleteMovement();
               return;
           }

           SetCurrentTarget(activePath[nextPathIndex]);       
       }
       
       private void SetCurrentTarget(Vector2Int targetCell)
       {
           currentTargetCell = targetCell;

           Vector2Int movementDifference = currentTargetCell - zombieGridPosition.CurrentCell;

           GridDirection newDirection = GetDirectionFromDifference(movementDifference);

           if (newDirection == GridDirection.None)
           {
               Debug.LogError(
                   $"Could not determine zombie movement direction from " +
                   $"{zombieGridPosition.CurrentCell} to {currentTargetCell}.",
                   this
               );

               return;
           }

           currentMovementDirection = newDirection;

           /*
            * FacingDirection stays as the last valid direction.
            * We do not reset it to None when the zombie stops.
            */
           facingDirection = newDirection;
       }
       
       private static GridDirection GetDirectionFromDifference(Vector2Int difference)
       {
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

       private void CompleteMovement()
       {
           isMoving = false;
           nextPathIndex = 0;

           currentTargetCell = zombieGridPosition.CurrentCell;

           /*
            * The zombie is no longer moving, but FacingDirection
            * remains the last direction it looked/moved toward.
            */
           currentMovementDirection = GridDirection.None;

           activePath.Clear();

           MovementCompleted?.Invoke(this, EventArgs.Empty);
       }
       
       private void OnDisable()
       {
           isMoving = false;
           nextPathIndex = 0;

           currentMovementDirection = GridDirection.None;

           activePath.Clear();
       }
    }
}
