using System.Collections.Generic;
using UnityEngine;

namespace IsometricPathfinding.Zombies
{
    [DisallowMultipleComponent]
    public sealed class ZombieManager : MonoBehaviour
    {
        public static ZombieManager Instance { get; private set; }

        [SerializeField] private bool registerSceneZombiesOnAwake = true;
        
        [SerializeField] private List<ZombieAgent> zombies = new List<ZombieAgent>();

        private readonly HashSet<ZombieAgent> zombieLookup = new HashSet<ZombieAgent>();
        
        public IReadOnlyList<ZombieAgent> Zombies => zombies;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"Multiple {nameof(ZombieManager)} instances exist in the scene. " +
                    $"'{name}' will be disabled.",
                    this
                );

                enabled = false;
                return;
            }

            Instance = this;

            RebuildZombieLookup();

            if (registerSceneZombiesOnAwake)
            {
                RegisterSceneZombies();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        [ContextMenu("Register Scene Zombies")]
        public void RegisterSceneZombies()
        {
            zombies.Clear();
            zombieLookup.Clear();

            ZombieAgent[] sceneZombies = FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None);

            foreach (ZombieAgent zombie in sceneZombies)
            {
                Register(zombie);
            }
        }
        
        public void Register(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                return;
            }

            if (!zombieLookup.Add(zombie))
            {
                return;
            }

            zombies.Add(zombie);
        }
        
        public void Unregister(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                return;
            }

            if (!zombieLookup.Remove(zombie))
            {
                return;
            }

            zombies.Remove(zombie);
        }
        
        public void RemoveNullReferences()
        {
            RebuildZombieLookup();
        }

        public void NotifyGunshotAt(Vector2Int shotCell)
        {
            RemoveNullReferences();

            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieAgent zombie = zombies[i];

                if (zombie == null)
                {
                    continue;
                }

                if (zombie.State == ZombieState.Dead)
                {
                    continue;
                }

                zombie.InvestigateGunshot(shotCell);
            }
        }

        private void RebuildZombieLookup()
        {
            for (int i = zombies.Count - 1; i >= 0; i--)
            {
                ZombieAgent zombie = zombies[i];

                if (zombie == null)
                {
                    zombies.RemoveAt(i);
                }
            }

            zombieLookup.Clear();

            for (int i = zombies.Count - 1; i >= 0; i--)
            {
                ZombieAgent zombie = zombies[i];

                if (!zombieLookup.Add(zombie))
                {
                    zombies.RemoveAt(i);
                }
            }
        }
    }
}
