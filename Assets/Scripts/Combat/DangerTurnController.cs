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

        [Header("Shoot Difficulty")]
        [SerializeField] [Min(1)] private int toughShotDistance = 4;

        [SerializeField] [Min(1)] private int maximumShotDifficultyDistance = 8;

        [SerializeField] [Min(1f)] private float toughShotSpeedMultiplier = 1.75f;

        [SerializeField] [Min(1f)] private float maximumToughShotSpeedMultiplier = 2.5f;
        
        [SerializeField] [Min(0.05f)]
        private float activeZombieRefreshInterval = 0.25f;

        private float activeZombieRefreshTimer;
        
        public GameMode GameMode => gameMode;
        public DangerTurnPhase CurrentPhase => currentPhase;
        public bool IsInDangerMode => gameMode == GameMode.Danger;
        public bool IsActionMinigameActive => IsActionMinigamePhase(currentPhase);

        public event Action<ZombieAgent> StrikeMinigameStarted;

        public event Action<ZombieAgent> ShootMinigameStarted;
        
        public event Action StrikeMinigameEnded;

        public event Action ShootMinigameEnded;
        
        public event Action<ZombieAgent> PlayerStrikeMissed;

        public event Action<ZombieAgent> PlayerShootMissed;

        public event Action<ZombieAgent> PlayerKilledZombie;
        
        private Coroutine zombieTurnRoutine;
        
        private ZombieAgent pendingStrikeTarget;

        private ZombieAgent pendingShootTarget;
        
        private float timeScaleBeforeActionMinigame = 1f;

        private bool hasPausedTimeForActionMinigame;
        
        public int LastActionMinigameFinishedFrame { get; private set; } = -1;

        public int LastStrikeMinigameFinishedFrame => LastActionMinigameFinishedFrame;
        

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
            
            if (IsActionMinigameActive)
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
            ResumeGameAfterActionMinigame();
            
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
        
        private void PauseGameForActionMinigame(string minigameName)
        {
            if (hasPausedTimeForActionMinigame)
            {
                return;
            }

            timeScaleBeforeActionMinigame = Time.timeScale;
            Time.timeScale = 0f;
            hasPausedTimeForActionMinigame = true;

            Debug.Log($"Game paused for {minigameName} Minigame.", this);
        }

        private void ResumeGameAfterActionMinigame()
        {
            if (!hasPausedTimeForActionMinigame)
            {
                return;
            }

            Time.timeScale = timeScaleBeforeActionMinigame;
            hasPausedTimeForActionMinigame = false;

            Debug.Log("Game resumed after action minigame.", this);
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

        public bool CanStartShoot(ZombieAgent zombie)
        {
            return CanStartStrike(zombie);
        }

        public bool IsToughShoot(ZombieAgent zombie)
        {
            int distance = GetShootDistanceToPlayer(zombie);

            return distance != int.MaxValue
                   && distance >= toughShotDistance;
        }

        public float GetShootCursorSpeedMultiplier(ZombieAgent zombie)
        {
            int distance = GetShootDistanceToPlayer(zombie);

            if (distance == int.MaxValue || distance < toughShotDistance)
            {
                return 1f;
            }

            if (maximumShotDifficultyDistance <= toughShotDistance)
            {
                return maximumToughShotSpeedMultiplier;
            }

            float difficultyProgress = Mathf.InverseLerp(
                toughShotDistance,
                maximumShotDifficultyDistance,
                distance
            );

            return Mathf.Lerp(
                toughShotSpeedMultiplier,
                maximumToughShotSpeedMultiplier,
                difficultyProgress
            );
        }

        public int GetShootDistanceToPlayer(ZombieAgent zombie)
        {
            return GetDistanceToPlayer(zombie);
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

            ResumeGameAfterActionMinigame();
            
            if (currentPhase == DangerTurnPhase.StrikeMinigame)
            {
                LastActionMinigameFinishedFrame = Time.frameCount;
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

            PauseGameForActionMinigame("Strike");

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
            
            ResumeGameAfterActionMinigame();
            
            LastActionMinigameFinishedFrame = Time.frameCount;
            
            StrikeMinigameEnded?.Invoke();

            ZombieAgent target = pendingStrikeTarget;
            pendingStrikeTarget = null;

            if (target != null && target.State != ZombieState.Dead)
            {
                if (wasSuccessful)
                {
                    target.Kill();
                    PlayerKilledZombie?.Invoke(target);
                }
                else
                {
                    Debug.Log($"Strike missed {target.name}.", target);
                    PlayerStrikeMissed?.Invoke(target);
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

        public bool BeginShootMinigame(ZombieAgent zombie)
        {
            if (!CanStartShoot(zombie))
            {
                return false;
            }

            FacePlayerTowardZombie(zombie);

            pendingShootTarget = zombie;
            currentPhase = DangerTurnPhase.ShootMinigame;

            PauseGameForActionMinigame("Shoot");

            ShootMinigameStarted?.Invoke(zombie);

            return true;
        }

        public void CompleteShootMinigame(bool wasSuccessful)
        {
            if (gameMode != GameMode.Danger)
            {
                return;
            }

            if (currentPhase != DangerTurnPhase.ShootMinigame)
            {
                return;
            }

            ResumeGameAfterActionMinigame();

            LastActionMinigameFinishedFrame = Time.frameCount;

            ShootMinigameEnded?.Invoke();

            ZombieAgent target = pendingShootTarget;
            pendingShootTarget = null;

            if (target != null && target.State != ZombieState.Dead)
            {
                if (wasSuccessful)
                {
                    target.Kill();
                    PlayerKilledZombie?.Invoke(target);
                }
                else
                {
                    Debug.Log($"Shot missed {target.name}.", target);
                    PlayerShootMissed?.Invoke(target);
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

                    if (pendingShootTarget == zombie)
                    {
                        pendingShootTarget = null;
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

                    if (pendingShootTarget == zombie)
                    {
                        pendingShootTarget = null;
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
            ResumeGameAfterActionMinigame();
            StrikeMinigameEnded?.Invoke();
            ShootMinigameEnded?.Invoke();
            
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
            pendingShootTarget = null;
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
            pendingShootTarget = null;

            zombieTurnRoutine = StartCoroutine(RunZombieTurn());
        }

        private IEnumerator RunZombieTurn()
        {
            pendingStrikeTarget = null;
            pendingShootTarget = null;
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

        private static bool IsActionMinigamePhase(DangerTurnPhase phase)
        {
            return phase == DangerTurnPhase.StrikeMinigame
                   || phase == DangerTurnPhase.ShootMinigame;
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
            toughShotDistance = Mathf.Max(1, toughShotDistance);
            maximumShotDifficultyDistance = Mathf.Max(toughShotDistance, maximumShotDifficultyDistance);
            toughShotSpeedMultiplier = Mathf.Max(1f, toughShotSpeedMultiplier);
            maximumToughShotSpeedMultiplier = Mathf.Max(
                toughShotSpeedMultiplier,
                maximumToughShotSpeedMultiplier
            );
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
