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
        
        [SerializeField] private List<ZombieAgent> activeZombies = new List<ZombieAgent>();

        [SerializeField] private GameMode gameMode = GameMode.Exploration;

        [SerializeField] private DangerTurnPhase currentPhase = DangerTurnPhase.None;
        
        public GameMode GameMode => gameMode;
        public DangerTurnPhase CurrentPhase => currentPhase;

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
            if (gameMode == GameMode.Danger)
            {
                if (!activeZombies.Contains(triggeringZombie))
                {
                    activeZombies.Add(triggeringZombie);
                    triggeringZombie.SetCombatState();
                }

                return;
            }

            gameMode = GameMode.Danger;
            currentPhase = DangerTurnPhase.PlayerTurn;
            
            activeZombies.Clear();
            activeZombies.Add(triggeringZombie);
            
            triggeringZombie.SetCombatState();
            Debug.Log("Entered Danger Mode");
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

            currentPhase = DangerTurnPhase.PlayerTurn;

            Debug.Log("Player turn started.");
        }
    }
}
