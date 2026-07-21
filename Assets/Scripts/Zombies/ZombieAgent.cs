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
        
        [Header("Zombie Components")]
        
        [SerializeField] private ZombieGridPosition zombieGridPosition;
        
        [SerializeField] private ZombieGridMover zombieGridMover;

        [Header("AI Settings")] 
        
        [SerializeField] private DangerTurnController dangerTurnController;
        
        [SerializeField] private ZombieState state = ZombieState.Sleeping;

        [SerializeField] private int wakeRange = 4;

        [SerializeField] private int attackRange = 1;

        [SerializeField] private int movementPointsPerTurn = 3;
        
        [SerializeField] private int turnPenaltyCost = 1;

        [SerializeField] private int reversePenaltyCost = 2;

        [Header("Roaming Settings")] 
        
        [SerializeField] private float minimumRoamDelay = 1.5f;

        [SerializeField] private float maximumRoamDelay = 3.5f;

        [SerializeField] private int roamStepCount = 1;

        private float roamTimer;

        private AStarPathfinder pathFinder;

        public ZombieState State => state;
        public bool IsActing => zombieGridMover.IsMoving;

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
            if (state == ZombieState.Dead)
            {
                return;
            }
            if (state == ZombieState.Sleeping)
            {
                UpdateSleeping();
                return;
            }

            if (state == ZombieState.Roaming)
            {
                UpdateRoaming();
                return;
            }
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
            state = ZombieState.Alert;

            if (dangerTurnController != null)
            {
                dangerTurnController.EnterDangerMode(this);
            }
            else
            {
                Debug.LogWarning( $"{nameof(ZombieAgent)} on '{name}' woke up, but no DangerTurnController is assigned.", this);
            }
        }

        public void SetCombatState()
        {
            state = ZombieState.Combat;
        }

        public void TakeTurn()
        {
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

            MoveTowardPlayer();

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
            Debug.Log($"{name} attacks player.", this);

            // Later:
            // playerHealth.TakeDamage(damage);
        }

        private void MoveTowardPlayer()
        {
            Vector2Int start = zombieGridPosition.CurrentCell;
            Vector2Int target = FindBestAdjacentCellToPlayer();

            bool pathFound = pathFinder.TryFindPath(start, target, GridDirection.None, out List<Vector2Int> path, out int turnPenalty);

            if (!pathFound || path.Count < 2)
            {
                return;
            }

            List<Vector2Int> limitedPath = LimitPathToMovementPoints(path, movementPointsPerTurn);

            zombieGridMover.TryMoveAlongPath(limitedPath);
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

        private static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private void UpdateRoaming()
        {
            if (dangerTurnController != null && dangerTurnController.GameMode != GameMode.Exploration)
            {
                return;
            }

            if (zombieGridMover.IsMoving)
            {
                return;
            }

            if (IsPlayerInsideWakeRange())
            {
                WakeUp();
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
            attackRange = Mathf.Max(1, attackRange);
            movementPointsPerTurn = Mathf.Max(1, movementPointsPerTurn);

            minimumRoamDelay = Mathf.Max(0.1f, minimumRoamDelay);
            maximumRoamDelay = Mathf.Max(minimumRoamDelay, maximumRoamDelay);

            roamStepCount = Mathf.Max(1, roamStepCount);

            turnPenaltyCost = Mathf.Max(0, turnPenaltyCost);
            reversePenaltyCost = Mathf.Max(turnPenaltyCost, reversePenaltyCost);
        }
    }
}
