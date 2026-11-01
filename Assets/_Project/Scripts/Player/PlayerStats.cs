using System;
using UnityEngine;

namespace SoulsRPG.Player
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 100;
        private int currentHealth;

        [Header("Stamina Settings")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 35f;
        [SerializeField] private float staminaRegenDelay = 1.0f; // Dark souls delay before regen starts
        private float currentStamina;
        private float regenTimer = 0f;

        [Header("Poise & Armor")]
        [SerializeField] private float maxPoise = 50f;
        private float currentPoise;

        [Header("State Flags")]
        public bool isInvulnerable = false; // i-frame active during roll
        public bool isDead = false;

        // Events for UI HUD
        public event Action<int, int> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action OnDeath;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;

        private void Awake()
        {
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentPoise = maxPoise;
        }

        private void Update()
        {
            HandleStaminaRegeneration();
        }

        private void HandleStaminaRegeneration()
        {
            if (isDead) return;

            if (regenTimer > 0f)
            {
                regenTimer -= Time.deltaTime;
                return;
            }

            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina > maxStamina)
                {
                    currentStamina = maxStamina;
                }
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        public bool UseStamina(float amount)
        {
            if (currentStamina < amount)
            {
                // Even with low stamina, Dark Souls allows a roll/attack at cost of going into negative stamina
                currentStamina = 0f;
                regenTimer = staminaRegenDelay * 1.5f; // Extra penalty for stamina exhaustion
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
                return true;
            }

            currentStamina -= amount;
            regenTimer = staminaRegenDelay;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            return true;
        }

        public bool HasStamina(float amount = 1f)
        {
            return currentStamina >= amount;
        }

        public void TakeDamage(int damage, Transform attacker = null, float poiseDamage = 10f)
        {
            if (isDead || isInvulnerable)
            {
                // Dodged through attack with i-frame!
                return;
            }

            currentHealth -= damage;
            if (currentHealth < 0) currentHealth = 0;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // Poise check
            currentPoise -= poiseDamage;
            if (currentPoise <= 0f)
            {
                // Staggered / flinched
                currentPoise = maxPoise;
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(int amount)
        {
            if (isDead) return;
            currentHealth += amount;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            isDead = true;
            OnDeath?.Invoke();
            Debug.Log("<color=red>YOU DIED</color>");
        }
    }
}
