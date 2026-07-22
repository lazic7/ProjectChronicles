using System;
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

            if (zombies.Contains(zombie))
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

            zombies.Remove(zombie);
        }
        
        public void RemoveNullReferences()
        {
            for (int i = zombies.Count - 1; i >= 0; i--)
            {
                if (zombies[i] == null)
                {
                    zombies.RemoveAt(i);
                }
            }
        }
    }
}
