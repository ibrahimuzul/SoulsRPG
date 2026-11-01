using System;
using UnityEngine;
using SoulsRPG.Combat;

namespace SoulsRPG.Player
{
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public static PlayerStats Instance { get; private set; }

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

        public bool IsDead => isDead;

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
            Instance = this;
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

        public void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, Transform attacker = null)
        {
            if (isDead || isInvulnerable)
            {
                return;
            }

            // Kalkan Blok Kontrolü (Shield Block Check)
            var locomotion = PlayerLocomotion.Instance;
            if (locomotion != null && locomotion.IsBlocking)
            {
                // Gelen saldırı ön cepheden mi (Dot product check)
                float dot = Vector3.Dot(transform.forward, -hitDirection);
                if (dot > 0.25f)
                {
                    // Blok Başarılı! Hasar staminadan düşülür
                    float staminaCost = damage * 0.75f;
                    bool hadStamina = currentStamina >= staminaCost;
                    UseStamina(staminaCost);

                    if (!hadStamina || currentStamina <= 0f)
                    {
                        // Gard Kırıldı! (Guard Break Stagger)
                        locomotion.TriggerGuardBreak();
                    }
                    else
                    {
                        // Kalkan darbeyi savuşturdu
                        locomotion.TriggerBlockRecoil();
                    }

                    SpawnBlockEffect(hitPoint, -hitDirection);
                    return;
                }
            }

            // Normal Hasar
            TakeDamage(damage, attacker, 15f);
        }

        private void SpawnBlockEffect(Vector3 point, Vector3 direction)
        {
            GameObject sparkObj = new GameObject("BlockSparks");
            sparkObj.transform.position = point;
            sparkObj.transform.forward = direction;

            var ps = sparkObj.AddComponent<ParticleSystem>();
            var pMain = ps.main;
            pMain.startLifetime = 0.2f;
            pMain.startSpeed = 5f;
            pMain.startSize = 0.09f;
            pMain.startColor = new Color(0.4f, 0.8f, 1.0f); // Mavi/çelik kalkan kıvılcımı
            pMain.loop = false;
            pMain.stopAction = ParticleSystemStopAction.Destroy;

            var pEmission = ps.emission;
            pEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 14) });
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
