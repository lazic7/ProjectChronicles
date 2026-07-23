using System;
using System.Collections.Generic;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Navigation;
using IsometricPathfinding.Pathfinding;
using IsometricPathfinding.Combat;
using UnityEngine;

namespace IsometricPathfinding.Zombies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ZombieGridPosition))]
    [RequireComponent(typeof(ZombieGridMover))]
    public sealed class ZombieAgent : MonoBehaviour
    {
        [Header("Scene References")]
        
        [SerializeField] private NavigationGrid navigationGrid;
        
        [SerializeField] private PlayerGridPosition playerGridPosition;
        
        [SerializeField] private PlayerGridMover playerGridMover;

        
        [Header("Zombie Components")]
        
        [SerializeField] private ZombieGridPosition zombieGridPosition;
        
        [SerializeField] private ZombieGridMover zombieGridMover;

        [Header("AI Settings")] 
        
        [SerializeField] private DangerTurnController dangerTurnController;
        
        [SerializeField] private ZombieState state = ZombieState.Sleeping;

        [SerializeField] private int wakeRange = 4;
        
        [SerializeField] private int facingDetectionRange = 8;

        [SerializeField] private int attackRange = 1;

        [SerializeField] private int movementPointsPerTurn = 3;
        
        [SerializeField] [Min(0f)] private float alertDuration = 0.5f;
        
        [SerializeField] private int turnPenaltyCost = 1;

        [SerializeField] private int reversePenaltyCost = 2;
        
        [SerializeField] [Range(0f, 1f)] private float attackMissChance = 0.25f;


        [Header("Roaming Settings")] 
        
        [SerializeField] private float minimumRoamDelay = 1.5f;

        [SerializeField] private float maximumRoamDelay = 3.5f;

        [SerializeField] private int roamStepCount = 1;
        
        [SerializeField] private bool logPathResults;

        private float roamTimer;

        private float alertTimer;
        
        private bool shouldAttackAfterMovement;
        
        private AStarPathfinder pathFinder;
        
        public ZombieState State => state;
        public bool IsActing => zombieGridMover.IsMoving;
        
        public Vector2Int CurrentCell => zombieGridPosition.CurrentCell;
        
        public GridDirection CurrentMovementDirection => zombieGridMover.CurrentMovementDirection;

        public GridDirection FacingDirection => zombieGridMover.FacingDirection;

        private void Awake()
        {
            if (zombieGridPosition == null)
            {
                zombieGridPosition = GetComponent<ZombieGridPosition>();
            }

            if (zombieGridMover == null)
            {
                zombieGridMover = GetComponent<ZombieGridMover>();
            }
            
            if (playerGridMover == null && playerGridPosition != null)
            {
                playerGridMover = playerGridPosition.GetComponent<PlayerGridMover>();
            }
            
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            pathFinder = new AStarPathfinder(
                navigationGrid, 
                turnPenaltyCost, 
                reversePenaltyCost,
                cell=>navigationGrid.IsWalkableForActor(cell, gameObject)
            );

            ResetRoamTimer();
        }

        private void Update()
        {
            switch (state)
            {
                case ZombieState.Dead:
                    return;

                case ZombieState.Sleeping:
                    UpdateSleeping();
                    return;

                case ZombieState.Roaming:
                    UpdateRoaming();
                    return;

                case ZombieState.Alert:
                    UpdateAlert();
                    return;

                case ZombieState.Combat:
                    UpdateCombat();
                    return;
            }
        }
        
        private void OnEnable()
        {
            if (zombieGridMover != null)
            {
                zombieGridMover.MovementCompleted += OnZombieMovementCompleted;
            }

            
            if (ZombieManager.Instance == null)
            {
                return;
            }

            ZombieManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (zombieGridMover != null)
            {
                zombieGridMover.MovementCompleted -= OnZombieMovementCompleted;
            }

            ClearAttackAfterMovement();
    
            if (ZombieManager.Instance == null)
            {
                return;
            }

            ZombieManager.Instance.Unregister(this);
        }

        private void UpdateSleeping()
        {
            if (!IsPlayerInsideWakeRange())
            {
                return;
            }
            
            WakeUp();
        }

        private void WakeUp()
        {
            if (state == ZombieState.Dead || state == ZombieState.Combat || state == ZombieState.Alert)
            {
                return;
            }

            state = ZombieState.Alert;
            alertTimer = alertDuration;

            Debug.Log($"{name} became alert.", this);
        }
        
        private void UpdateAlert()
        {
            bool playerIsClose = IsPlayerInsideWakeRange();
            bool playerIsSeen = CanSeePlayerInFacingDirection();

            if (!playerIsClose && !playerIsSeen)
            {
                state = ZombieState.Roaming;
                ResetRoamTimer();

                Debug.Log($"{name} lost the player and returned to roaming.", this);

                return;
            }

            if (playerIsSeen && !playerIsClose)
            {
                Debug.Log($"{name} saw the player from farther away while alert.", this);
                EnterCombat();
                return;
            }

            alertTimer -= Time.deltaTime;

            if (alertTimer > 0f)
            {
                return;
            }

            EnterCombat();
        }
        
        private void EnterCombat()
        {
            if (state == ZombieState.Dead || state == ZombieState.Combat)
            {
                return;
            }

            if (dangerTurnController != null)
            {
                dangerTurnController.EnterDangerMode(this);
                return;
            }

            state = ZombieState.Combat;

            Debug.LogWarning(
                $"{nameof(ZombieAgent)} on '{name}' entered combat, but no DangerTurnController is assigned.",
                this
            );
        }
        
        private void UpdateCombat()
        {
            /*
             * Combat zombies are controlled by DangerTurnController.
             *
             * Do not move toward the player here, because that would make
             * zombies move every frame instead of only during their turn.
             */
        }

        public void SetCombatState()
        {
            if (state == ZombieState.Dead)
            {
                return;
            }

            state = ZombieState.Combat;
            alertTimer = 0f;
        }
        
        public void SetRoamingState()
        {
            if (state == ZombieState.Dead)
            {
                return;
            }

            ClearAttackAfterMovement();

            state = ZombieState.Roaming;
            alertTimer = 0f;
            ResetRoamTimer();
        }

        public void TakeTurn()
        {
            ClearAttackAfterMovement();
            
            if (state == ZombieState.Dead)
            {
                return;
            }

            state = ZombieState.Combat;

            if (IsPlayerInAttackRange())
            {
                AttackPlayer();
                return;
            }

            bool movementStarted = TryMoveTowardPlayer(out int remainingMovementPoints);

            if (!movementStarted)
            {
                return;
            }

            if (remainingMovementPoints > 0)
            {
                QueueAttackAfterMovement();
            }

        }
        
        private void QueueAttackAfterMovement()
        {
            shouldAttackAfterMovement = true;
        }

        private void ClearAttackAfterMovement()
        {
            shouldAttackAfterMovement = false;
        }
        
        private void OnZombieMovementCompleted(object sender, EventArgs e)
        {
            if (!shouldAttackAfterMovement)
            {
                return;
            }

            ClearAttackAfterMovement();

            if (state != ZombieState.Combat)
            {
                return;
            }

            if (!IsPlayerInAttackRange())
            {
                return;
            }

            AttackPlayer();
        }
        
        private bool CanSeePlayerInFacingDirection()
        {
            if (zombieGridPosition == null || playerGridPosition == null)
            {
                return false;
            }

            GridDirection facing = FacingDirection;

            if (facing == GridDirection.None)
            {
                return false;
            }

            Vector2Int facingVector = GetVectorFromDirection(facing);

            if (facingVector == Vector2Int.zero)
            {
                return false;
            }

            Vector2Int zombieCell = zombieGridPosition.CurrentCell;
            Vector2Int playerCell = playerGridPosition.CurrentCell;

            Vector2Int difference = playerCell - zombieCell;

            /*
             * The player must be exactly in the same row or column
             * that the zombie is facing.
             *
             * Example:
             * Zombie facing Right can only see player if:
             * - player is on same Y
             * - player X is greater than zombie X
             */
            bool playerIsOnFacingLine =
                facingVector.x != 0
                    ? difference.y == 0 && Math.Sign(difference.x) == facingVector.x
                    : difference.x == 0 && Math.Sign(difference.y) == facingVector.y;

            if (!playerIsOnFacingLine)
            {
                return false;
            }

            int distance = GetGridDistance(zombieCell, playerCell);

            if (distance <= 0 || distance > facingDetectionRange)
            {
                return false;
            }

            /*
             * Optional but useful:
             * Check that there are no blocked grid cells between
             * the zombie and the player.
             */
            Vector2Int currentCell = zombieCell;

            for (int step = 1; step <= distance; step++)
            {
                currentCell += facingVector;

                if (currentCell == playerCell)
                {
                    return true;
                }

                if (!navigationGrid.IsWalkable(currentCell))
                {
                    return false;
                }
            }

            return false;
        }

        private bool IsPlayerInsideWakeRange()
        {
            int distance = GetGridDistance(zombieGridPosition.CurrentCell, playerGridPosition.CurrentCell);

            return distance <= wakeRange;
        }

        private bool IsPlayerInAttackRange()
        {
            int distance = GetGridDistance(zombieGridPosition.CurrentCell, playerGridPosition.CurrentCell);

            return distance <= attackRange;
        }
        
        private void AttackPlayer()
        {
            FacePlayerAndZombieBeforeAttack();

            if (!DoesAttackHit())
            {
                Debug.Log($"{name} attacks player but misses.", this);
                return;
            }

            Debug.Log($"{name} attacks player and hits.", this);

            // Later:
            // playerHealth.TakeDamage(damage);
        }
        
        private bool DoesAttackHit()
        {
            return UnityEngine.Random.value >= attackMissChance;
        }
        
        private void FacePlayerAndZombieBeforeAttack()
        {
            Vector2Int zombieCell = zombieGridPosition.CurrentCell;
            Vector2Int playerCell = playerGridPosition.CurrentCell;

            GridDirection zombieFacingDirection = GetDirectionTowardCell(zombieCell, playerCell);

            GridDirection playerFacingDirection = GetDirectionTowardCell(playerCell, zombieCell);

            zombieGridMover.FaceDirection(zombieFacingDirection);

            if (playerGridMover != null)
            {                              
                playerGridMover.FaceDirection(playerFacingDirection);
            }
        }

        private bool TryMoveTowardPlayer(out int remainingMovementPoints)
        {
            remainingMovementPoints = 0;

            Vector2Int start = zombieGridPosition.CurrentCell;
            Vector2Int target = FindBestAdjacentCellToPlayer();

            GridDirection initialFacingDirection = FacingDirection;

            if (initialFacingDirection == GridDirection.None)
            {
                initialFacingDirection = GridDirection.Down;
            }

            bool pathFound = pathFinder.TryFindPath(
                start,
                target,
                initialFacingDirection,
                out List<Vector2Int> path,
                out int turnPenalty
            );

            if (logPathResults)
            {
                Debug.Log(
                    $"{name} path to player. Facing: {FacingDirection}. Turn penalty: {turnPenalty}.",
                    this
                );
            }

            if (!pathFound || path.Count < 2)
            {
                return false;
            }

            List<Vector2Int> limitedPath = LimitPathToMovementPoints(path, movementPointsPerTurn);

            if (limitedPath.Count < 2)
            {
                return false;
            }

            int usedMovementPoints = limitedPath.Count - 1;

            remainingMovementPoints = Mathf.Max(0, movementPointsPerTurn - usedMovementPoints);

            bool movementStarted = zombieGridMover.TryMoveAlongPath(limitedPath);

            if (!movementStarted)
            {
                remainingMovementPoints = 0;
                return false;
            }

            return true;
        }

        private Vector2Int FindBestAdjacentCellToPlayer()
        {
            Vector2Int playerCell = playerGridPosition.CurrentCell;

            Vector2Int[] candidates =
            {
                playerCell + Vector2Int.up,
                playerCell + Vector2Int.down,
                playerCell + Vector2Int.right,
                playerCell + Vector2Int.left,
            };

            Vector2Int bestCell = zombieGridPosition.CurrentCell;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2Int candidate = candidates[i];

                if (!navigationGrid.IsWalkableForActor(candidate, gameObject))
                {
                    continue;
                }
                
                int distance = GetGridDistance(zombieGridPosition.CurrentCell, candidate);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                }
            }

            return bestCell;
        }

        private static List<Vector2Int> LimitPathToMovementPoints(IReadOnlyList<Vector2Int> path, int movementPoints)
        {
            List<Vector2Int> limitedPath = new List<Vector2Int>();

            int maxIndex = Mathf.Min(path.Count - 1, movementPoints);

            for (int i = 0; i <= maxIndex; i++)
            {
                limitedPath.Add(path[i]);
            }

            return limitedPath;
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
        
        private static GridDirection GetPrimaryDirectionFromDifference(Vector2Int difference)
        {
            if (difference == Vector2Int.zero)
            {
                return GridDirection.None;
            }

            if (Mathf.Abs(difference.x) >= Mathf.Abs(difference.y))
            {
                return difference.x > 0
                    ? GridDirection.Right
                    : GridDirection.Left;
            }

            return difference.y > 0
                ? GridDirection.Up
                : GridDirection.Down;
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

        private static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private void UpdateRoaming()
        {
            if (zombieGridMover.IsMoving)
            {
                return;
            }

            if (IsPlayerInsideWakeRange())
            {
                WakeUp();
                return;
            }

            if (CanSeePlayerInFacingDirection())
            {
                Debug.Log($"{name} saw the player while roaming.", this);
                EnterCombat();
                return;
            }

            roamTimer -= Time.deltaTime;

            if (roamTimer > 0f)
            {
                return;
            }

            ResetRoamTimer();

            TryRoam();
        }

        private void TryRoam()
        {
            Vector2Int start = zombieGridPosition.CurrentCell;

            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.right,
                Vector2Int.left,
            };

            List<Vector2Int> candidates = new List<Vector2Int>();

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int candidate = start + directions[i];

                if (!navigationGrid.IsWalkableForActor(candidate, gameObject))
                {
                    continue;
                }
                
                candidates.Add(candidate);
            }

            if (candidates.Count == 0)
            {
                return;
            }
            
            Vector2Int target = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            List<Vector2Int> path = new List<Vector2Int>()
            {
                start,
                target
            };
            
            zombieGridMover.TryMoveAlongPath(path);
        }

        private void ResetRoamTimer()
        {
            float minimum = MathF.Min(minimumRoamDelay, maximumRoamDelay);
            float maximum =  MathF.Max(maximumRoamDelay, minimumRoamDelay);
            
            roamTimer = UnityEngine.Random.Range(minimum, maximum);
        }
        
        private void OnValidate()
        {
            wakeRange = Mathf.Max(0, wakeRange);
            facingDetectionRange = Mathf.Max(wakeRange, facingDetectionRange);

            attackRange = Mathf.Max(1, attackRange);
            attackMissChance = Mathf.Clamp01(attackMissChance);
            
            movementPointsPerTurn = Mathf.Max(1, movementPointsPerTurn);
            alertDuration = Mathf.Max(0f, alertDuration);

            minimumRoamDelay = Mathf.Max(0.1f, minimumRoamDelay);
            maximumRoamDelay = Mathf.Max(minimumRoamDelay, maximumRoamDelay);

            roamStepCount = Mathf.Max(1, roamStepCount);

            turnPenaltyCost = Mathf.Max(0, turnPenaltyCost);
            reversePenaltyCost = Mathf.Max(turnPenaltyCost, reversePenaltyCost);
        }
        
        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (navigationGrid == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieAgent)} on '{name}' is missing the " +
                    $"{nameof(NavigationGrid)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerGridPosition == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieAgent)} on '{name}' is missing the " +
                    $"{nameof(PlayerGridPosition)} reference.",
                    this
                );

                referencesAreValid = false;
            }
            
            if (playerGridMover == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieAgent)} on '{name}' is missing the " +
                    $"{nameof(PlayerGridMover)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (dangerTurnController == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieAgent)} on '{name}' is missing the " +
                    $"{nameof(DangerTurnController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (zombieGridPosition == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieAgent)} on '{name}' is missing the " +
                    $"{nameof(ZombieGridPosition)} component.",
                    this
                );

                referencesAreValid = false;
            }

            if (zombieGridMover == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieAgent)} on '{name}' is missing the " +
                    $"{nameof(ZombieGridMover)} component.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }
    }
}
