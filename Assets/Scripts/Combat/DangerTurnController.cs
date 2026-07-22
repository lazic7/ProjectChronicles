using System;
using System.Collections;
using System.Collections.Generic;
using IsometricPathfinding.Movement;
using IsometricPathfinding.Zombies;
using UnityEngine;



namespace IsometricPathfinding.Combat
{
    
    public sealed class DangerTurnController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlayerGridMover playerGridMover;

        [SerializeField] private PlayerGridPosition playerGridPosition;

        [SerializeField] private ZombieManager zombieManager;

        [Header("Runtime State")]
        [SerializeField] private List<ZombieAgent> activeZombies = new List<ZombieAgent>();

        [SerializeField] private GameMode gameMode = GameMode.Exploration;

        [SerializeField] private DangerTurnPhase currentPhase = DangerTurnPhase.None;

        [Header("Danger Settings")]
        [SerializeField] [Min(0)] private int zombieJoinRange = 6;

        [SerializeField] [Min(0)] private int dangerExitRange = 8;
        
        public GameMode GameMode => gameMode;
        public DangerTurnPhase CurrentPhase => currentPhase;
        public bool IsInDangerMode => gameMode == GameMode.Danger;
        
        private Coroutine zombieTurnRoutine;

        private void Awake()
        {
            if (zombieManager == null)
            {
                zombieManager = ZombieManager.Instance;
            }

            if (!ValidateReferences())
            {
                enabled = false;
            }
        }
        
        private void Update()
        {
            if (gameMode != GameMode.Danger)
            {
                return;
            }

            RefreshActiveZombies();

            if (activeZombies.Count == 0)
            {
                ExitDangerMode();
            }
        }

        private void OnEnable()
        {
            if (playerGridMover != null)
            {
                playerGridMover.MovementCompleted += OnPlayerMovementCompleted;
            }
        }

        private void OnDisable()
        {
            if (playerGridMover != null)
            {
                playerGridMover.MovementCompleted -= OnPlayerMovementCompleted;
            }

            if (zombieTurnRoutine != null)
            {
                StopCoroutine(zombieTurnRoutine);
                zombieTurnRoutine = null;
            }
        }

        public void EnterDangerMode(ZombieAgent triggeringZombie)
        {
            if (triggeringZombie == null)
            {
                return;
            }

            if (gameMode != GameMode.Danger)
            {
                gameMode = GameMode.Danger;
                currentPhase = DangerTurnPhase.PlayerTurn;
                activeZombies.Clear();

                Debug.Log("Entered Danger Mode", this);
            }

            TryAddActiveZombie(triggeringZombie);

            RefreshActiveZombies();
        }
        
        private void TryAddActiveZombie(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                return;
            }

            if (zombie.State == ZombieState.Dead)
            {
                return;
            }

            if (activeZombies.Contains(zombie))
            {
                return;
            }

            activeZombies.Add(zombie);
            zombie.SetCombatState();

            Debug.Log($"{zombie.name} joined Danger Mode.", zombie);
        }
        
        private void RefreshActiveZombies()
        {
            RemoveInvalidOrEscapedZombies();

            if (zombieManager == null)
            {
                return;
            }

            zombieManager.RemoveNullReferences();

            IReadOnlyList<ZombieAgent> allZombies = zombieManager.Zombies;

            for (int i = 0; i < allZombies.Count; i++)
            {
                ZombieAgent zombie = allZombies[i];

                if (zombie == null)
                {
                    continue;
                }

                if (zombie.State == ZombieState.Dead)
                {
                    continue;
                }

                if (activeZombies.Contains(zombie))
                {
                    continue;
                }

                if (GetDistanceToPlayer(zombie) > zombieJoinRange)
                {
                    continue;
                }

                TryAddActiveZombie(zombie);
            }
        }
        
        private void RemoveInvalidOrEscapedZombies()
        {
            for (int i = activeZombies.Count - 1; i >= 0; i--)
            {
                ZombieAgent zombie = activeZombies[i];

                if (zombie == null)
                {
                    activeZombies.RemoveAt(i);
                    continue;
                }

                if (GetDistanceToPlayer(zombie) > dangerExitRange)
                {
                    activeZombies.RemoveAt(i);

                    zombie.SetRoamingState();

                    Debug.Log($"{zombie.name} was escaped and left Danger Mode.", zombie);
                }
            }
        }
        
        private int GetDistanceToPlayer(ZombieAgent zombie)
        {
            if (zombie == null || playerGridPosition == null)
            {
                return int.MaxValue;
            }

            return GetGridDistance(zombie.CurrentCell, playerGridPosition.CurrentCell);
        }

        private static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
        
        private void ExitDangerMode()
        {
            ExitDangerMode(true);
        }

        private void ExitDangerMode(bool stopZombieTurnRoutine)
        {
            if (stopZombieTurnRoutine && zombieTurnRoutine != null)
            {
                StopCoroutine(zombieTurnRoutine);
                zombieTurnRoutine = null;
            }

            for (int i = 0; i < activeZombies.Count; i++)
            {
                ZombieAgent zombie = activeZombies[i];

                if (zombie == null)
                {
                    continue;
                }

                zombie.SetRoamingState();
            }

            gameMode = GameMode.Exploration;
            currentPhase = DangerTurnPhase.None;
            activeZombies.Clear();

            Debug.Log("Exited Danger Mode", this);
        }

        private void OnPlayerMovementCompleted(object sender, EventArgs e)
        {
            if (gameMode != GameMode.Danger)
            {
                return;
            }

            if (currentPhase != DangerTurnPhase.PlayerTurn)
            {
                return;
            }

            if (zombieTurnRoutine != null)
            {
                return;
            }

            zombieTurnRoutine = StartCoroutine(RunZombieTurn());
        }

        private IEnumerator RunZombieTurn()
        {
            currentPhase = DangerTurnPhase.ZombieTurn;

            int zombieIndex = 0;

            while (gameMode == GameMode.Danger && zombieIndex < activeZombies.Count)
            {
                RefreshActiveZombies();

                if (gameMode != GameMode.Danger)
                {
                    break;
                }

                if (zombieIndex >= activeZombies.Count)
                {
                    break;
                }

                ZombieAgent zombie = activeZombies[zombieIndex];
                zombieIndex++;

                if (zombie == null)
                {
                    continue;
                }

                if (zombie.State == ZombieState.Dead)
                {
                    continue;
                }

                if (!activeZombies.Contains(zombie))
                {
                    continue;
                }

                zombie.TakeTurn();

                while (gameMode == GameMode.Danger && zombie != null && zombie.IsActing)
                {
                    yield return null;
                }

                if (gameMode != GameMode.Danger)
                {
                    break;
                }

                yield return new WaitForSeconds(0.15f);
            }

            RefreshActiveZombies();

            if (activeZombies.Count == 0)
            {
                zombieTurnRoutine = null;
                ExitDangerMode(false);
                yield break;
            }

            currentPhase = DangerTurnPhase.PlayerTurn;
            zombieTurnRoutine = null;

            Debug.Log("Player turn started.", this);
        }
        
        public bool IsZombieActive(ZombieAgent zombie)
        {
            return zombie != null && activeZombies.Contains(zombie);
        }
        
        private void OnValidate()
        {
            zombieJoinRange = Mathf.Max(0, zombieJoinRange);
            dangerExitRange = Mathf.Max(zombieJoinRange, dangerExitRange);
        }
        
        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (playerGridMover == null)
            {
                Debug.LogError(
                    $"{nameof(DangerTurnController)} on '{name}' is missing the " +
                    $"{nameof(PlayerGridMover)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerGridPosition == null)
            {
                Debug.LogError(
                    $"{nameof(DangerTurnController)} on '{name}' is missing the " +
                    $"{nameof(PlayerGridPosition)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (zombieManager == null)
            {
                Debug.LogError(
                    $"{nameof(DangerTurnController)} on '{name}' is missing the " +
                    $"{nameof(ZombieManager)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }
    }
}
