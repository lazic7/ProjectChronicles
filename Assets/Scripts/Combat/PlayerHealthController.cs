using System;
using IsometricPathfinding.Zombies;
using UnityEngine;

namespace IsometricPathfinding.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealthController : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] [Min(1f)] private float maximumHealth = 100f;

        [SerializeField] [Min(0f)] private float startingHealth = 100f;

        [SerializeField] [Range(0f, 100f)] private float zombieHitDamagePercent = 49f;

        [Header("Runtime State")]
        [SerializeField] [Min(0f)] private float currentHealth = 100f;

        [SerializeField] private bool isDefeated;

        public event Action<float, float> HealthChanged;

        public event Action Defeated;

        public float CurrentHealth => currentHealth;

        public float MaximumHealth => maximumHealth;

        public bool IsDefeated => isDefeated;

        public float CurrentHealthNormalized => maximumHealth <= 0f
            ? 0f
            : Mathf.Clamp01(currentHealth / maximumHealth);

        private void Awake()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            startingHealth = Mathf.Clamp(startingHealth, 0f, maximumHealth);
            currentHealth = startingHealth;
            isDefeated = currentHealth <= 0f;
        }

        private void OnEnable()
        {
            ZombieAgent.ZombieAttackHitPlayer += OnZombieAttackHitPlayer;
        }

        private void OnDisable()
        {
            ZombieAgent.ZombieAttackHitPlayer -= OnZombieAttackHitPlayer;
        }

        public void RestoreFullHealth()
        {
            SetHealth(maximumHealth);
        }

        public void TakeDamage(float amount)
        {
            if (isDefeated)
            {
                return;
            }

            if (amount <= 0f)
            {
                return;
            }

            SetHealth(currentHealth - amount);
        }

        private void OnZombieAttackHitPlayer(ZombieAgent _)
        {
            float damage = maximumHealth * (zombieHitDamagePercent / 100f);
            TakeDamage(damage);
        }

        private void SetHealth(float value)
        {
            float previousHealth = currentHealth;
            currentHealth = Mathf.Clamp(value, 0f, maximumHealth);

            if (Mathf.Approximately(previousHealth, currentHealth))
            {
                return;
            }

            HealthChanged?.Invoke(currentHealth, maximumHealth);

            if (isDefeated || currentHealth > 0f)
            {
                return;
            }

            isDefeated = true;
            Debug.LogWarning("Player was defeated.", this);
            Defeated?.Invoke();
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            startingHealth = Mathf.Clamp(startingHealth, 0f, maximumHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }
    }
}

