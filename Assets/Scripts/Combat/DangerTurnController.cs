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
        [SerializeField] private PlayerGridMover playerGridMover;

        [SerializeField] private PlayerGridPosition playerGridPosition;
        
        [SerializeField] private List<ZombieAgent> activeZombies = new List<ZombieAgent>();

        [SerializeField] private GameMode gameMode = GameMode.Exploration;

        [SerializeField] private DangerTurnPhase currentPhase = DangerTurnPhase.None;
        
        [Header("Danger Settings")]
        [SerializeField] [Min(0)] private int zombieJoinRange = 6;

        [SerializeField] [Min(0)] private int dangerExitRange = 8;
        
        public GameMode GameMode => gameMode;
        public DangerTurnPhase CurrentPhase => currentPhase;

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
            playerGridMover.MovementCompleted += OnPlayerMovementCompleted;
        }

        private void OnDisable()
        {
            playerGridMover.MovementCompleted -= OnPlayerMovementCompleted;
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

            ZombieAgent[] allZombies = FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None);

            for (int i = 0; i < allZombies.Length; i++)
            {
                ZombieAgent zombie = allZombies[i];

                if (zombie == null)
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

            StartCoroutine(RunZombieTurn());
        }

        private IEnumerator RunZombieTurn()
        {
            currentPhase = DangerTurnPhase.ZombieTurn;
            
            for (int i = 0; i < activeZombies.Count; i++)
            {
                RefreshActiveZombies();

                if (i >= activeZombies.Count)
                {
                    break;
                }

                ZombieAgent zombie = activeZombies[i];

                if (zombie == null)
                {
                    continue;
                }

                zombie.TakeTurn();

                while (zombie.IsActing)
                {
                    yield return null;
                }

                yield return new WaitForSeconds(0.15f);
            }

            RefreshActiveZombies();

            if (activeZombies.Count == 0)
            {
                ExitDangerMode();
                yield break;
            }

            currentPhase = DangerTurnPhase.PlayerTurn;

            Debug.Log("Player turn started.", this);
        }
        
        private void OnValidate()
        {
            zombieJoinRange = Mathf.Max(0, zombieJoinRange);
            dangerExitRange = Mathf.Max(zombieJoinRange, dangerExitRange);
        }
    }
}
