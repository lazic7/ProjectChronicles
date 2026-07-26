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

        private readonly HashSet<ZombieAgent> activeZombieLookup = new HashSet<ZombieAgent>();

        [Header("Danger Settings")]
        [SerializeField] [Min(0)] private int zombieJoinRange = 6;

        [SerializeField] [Min(0)] private int dangerExitRange = 8;
        
        [SerializeField] [Min(0.05f)]
        private float activeZombieRefreshInterval = 0.25f;

        private float activeZombieRefreshTimer;
        
        public GameMode GameMode => gameMode;
        public DangerTurnPhase CurrentPhase => currentPhase;
        public bool IsInDangerMode => gameMode == GameMode.Danger;

        public event Action<ZombieAgent> StrikeMinigameStarted;
        
        public event Action StrikeMinigameEnded;
        
        private Coroutine zombieTurnRoutine;
        
        private ZombieAgent pendingStrikeTarget;
        
        private float timeScaleBeforeStrikeMinigame = 1f;

        private bool hasPausedTimeForStrikeMinigame;
        
        public int LastStrikeMinigameFinishedFrame { get; private set; } = -1;
        

        private void Awake()
        {
            RebuildActiveZombieLookup();

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
            
            if (currentPhase == DangerTurnPhase.StrikeMinigame)
            {
                return;
            }
            
            activeZombieRefreshTimer -= Time.deltaTime;

            if (activeZombieRefreshTimer > 0f)
            {
                return;
            }

            activeZombieRefreshTimer = activeZombieRefreshInterval;

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
            ResumeGameAfterStrikeMinigame();
            
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
        
        private void PauseGameForStrikeMinigame()
        {
            if (hasPausedTimeForStrikeMinigame)
            {
                return;
            }

            timeScaleBeforeStrikeMinigame = Time.timeScale;
            Time.timeScale = 0f;
            hasPausedTimeForStrikeMinigame = true;

            Debug.Log("Game paused for Strike Minigame.", this);
        }

        private void ResumeGameAfterStrikeMinigame()
        {
            if (!hasPausedTimeForStrikeMinigame)
            {
                return;
            }

            Time.timeScale = timeScaleBeforeStrikeMinigame;
            hasPausedTimeForStrikeMinigame = false;

            Debug.Log("Game resumed after Strike Minigame.", this);
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
                activeZombieRefreshTimer = 0f;
                ClearActiveZombies();

                Debug.Log("Entered Danger Mode", this);
            }

            TryAddActiveZombie(triggeringZombie);

            RefreshActiveZombies();
        }
        
        public bool CanPlayerAct()
        {
            return gameMode == GameMode.Danger
                   && currentPhase == DangerTurnPhase.PlayerTurn
                   && zombieTurnRoutine == null;
        }

        public bool CanStartStrike(ZombieAgent zombie)
        {
            if (!CanPlayerAct())
            {
                return false;
            }

            if (zombie == null)
            {
                return false;
            }

            if (zombie.State == ZombieState.Dead)
            {
                return false;
            }

            return IsZombieActive(zombie);
        }
        
        public bool BeginStrikeApproach(ZombieAgent zombie)
        {
            if (!CanStartStrike(zombie))
            {
                return false;
            }

            pendingStrikeTarget = zombie;
            currentPhase = DangerTurnPhase.PlayerStrikeApproach;

            return true;
        }
        
        public void CancelStrikeAction()
        {
            if (gameMode != GameMode.Danger)
            {
                return;
            }

            if (currentPhase != DangerTurnPhase.PlayerStrikeApproach
                && currentPhase != DangerTurnPhase.StrikeMinigame)
            {
                return;
            }

            ResumeGameAfterStrikeMinigame();
            
            if (currentPhase == DangerTurnPhase.StrikeMinigame)
            {
                LastStrikeMinigameFinishedFrame = Time.frameCount;
            }
            
            StrikeMinigameEnded?.Invoke();

            pendingStrikeTarget = null;
            currentPhase = DangerTurnPhase.PlayerTurn;
        }
        
        public bool BeginStrikeMinigame(ZombieAgent zombie)
        {
            if (gameMode != GameMode.Danger)
            {
                return false;
            }

            if (zombie == null)
            {
                return false;
            }

            if (zombie.State == ZombieState.Dead)
            {
                return false;
            }

            if (!IsZombieActive(zombie))
            {
                return false;
            }
            
            FacePlayerTowardZombie(zombie);

            pendingStrikeTarget = zombie;
            currentPhase = DangerTurnPhase.StrikeMinigame;

            PauseGameForStrikeMinigame();

            StrikeMinigameStarted?.Invoke(zombie);

            return true;
        }
        
        public void CompleteStrikeMinigame(bool wasSuccessful)
        {
            if (gameMode != GameMode.Danger)
            {
                return;
            }

            if (currentPhase != DangerTurnPhase.StrikeMinigame)
            {
                return;
            }
            
            ResumeGameAfterStrikeMinigame();
            
            LastStrikeMinigameFinishedFrame = Time.frameCount;
            
            StrikeMinigameEnded?.Invoke();

            ZombieAgent target = pendingStrikeTarget;
            pendingStrikeTarget = null;

            if (target != null && target.State != ZombieState.Dead)
            {
                if (wasSuccessful)
                {
                    target.Kill();
                }
                else
                {
                    Debug.Log($"Strike missed {target.name}.", target);
                }
            }

            RefreshActiveZombies();

            if (activeZombies.Count == 0)
            {
                ExitDangerMode();
                return;
            }

            StartZombieTurn();
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

            if (activeZombieLookup.Contains(zombie))
            {
                return;
            }

            activeZombies.Add(zombie);
            activeZombieLookup.Add(zombie);
            zombie.SetCombatState();

            Debug.Log($"{zombie.name} joined Danger Mode.", zombie);
        }
        
        private void FacePlayerTowardZombie(ZombieAgent zombie)
        {
            if (zombie == null || playerGridMover == null || playerGridPosition == null)
            {
                return;
            }

            GridDirection direction = GetDirectionTowardCell(
                playerGridPosition.CurrentCell,
                zombie.CurrentCell
            );

            playerGridMover.FaceDirection(direction);
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

                if (activeZombieLookup.Contains(zombie))
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
                    RemoveActiveZombieAt(i);
                    continue;
                }

                if (zombie.State == ZombieState.Dead)
                {
                    if (pendingStrikeTarget == zombie)
                    {
                        pendingStrikeTarget = null;
                    }

                    RemoveActiveZombieAt(i);
                    continue;
                }

                if (GetDistanceToPlayer(zombie) > dangerExitRange)
                {
                    RemoveActiveZombieAt(i);

                    if (pendingStrikeTarget == zombie)
                    {
                        pendingStrikeTarget = null;
                    }

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
            ResumeGameAfterStrikeMinigame();
            StrikeMinigameEnded?.Invoke();
            
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
            pendingStrikeTarget = null;
            ClearActiveZombies();

            Debug.Log("Exited Danger Mode", this);
        }

        private void OnPlayerMovementCompleted(object sender, EventArgs e)
        {
            if (gameMode != GameMode.Danger)
            {
                return;
            }

            /*
             * Special case:
             * The player was moving toward a zombie after clicking Strike.
             *
             * This movement should NOT end the player turn immediately.
             * Instead, it should open the strike minigame.
             */
            if (currentPhase == DangerTurnPhase.PlayerStrikeApproach)
            {
                if (pendingStrikeTarget == null || pendingStrikeTarget.State == ZombieState.Dead)
                {
                    pendingStrikeTarget = null;
                    currentPhase = DangerTurnPhase.PlayerTurn;
                    return;
                }

                BeginStrikeMinigame(pendingStrikeTarget);
                return;
            }

            /*
             * Normal case:
             * The player moved during their turn.
             * Now the zombie turn starts.
             */
            if (currentPhase != DangerTurnPhase.PlayerTurn)
            {
                return;
            }

            StartZombieTurn();
        }
        
        public void StartZombieTurn()
        {
            if (gameMode != GameMode.Danger)
            {
                return;
            }

            if (zombieTurnRoutine != null)
            {
                return;
            }

            pendingStrikeTarget = null;

            zombieTurnRoutine = StartCoroutine(RunZombieTurn());
        }

        private IEnumerator RunZombieTurn()
        {
            pendingStrikeTarget = null;
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

                if (!activeZombieLookup.Contains(zombie))
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
            return zombie != null && activeZombieLookup.Contains(zombie);
        }

        private void ClearActiveZombies()
        {
            activeZombies.Clear();
            activeZombieLookup.Clear();
        }

        private void RemoveActiveZombieAt(int index)
        {
            ZombieAgent zombie = activeZombies[index];

            if (zombie != null)
            {
                activeZombieLookup.Remove(zombie);
            }

            activeZombies.RemoveAt(index);
        }

        private void RebuildActiveZombieLookup()
        {
            activeZombieLookup.Clear();

            for (int i = activeZombies.Count - 1; i >= 0; i--)
            {
                ZombieAgent zombie = activeZombies[i];

                if (zombie == null || !activeZombieLookup.Add(zombie))
                {
                    activeZombies.RemoveAt(i);
                }
            }
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
