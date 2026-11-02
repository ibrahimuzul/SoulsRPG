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
        [SerializeField] private float maxStamina = 120f;
        [SerializeField] private float staminaRegenRate = 60f;
        [SerializeField] private float staminaRegenDelay = 0.35f; // Hızlı ve dinamik test için gecikme azaltıldı
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
                // Kalkan yukarıdayken stamina %50 hızında dolmaya devam eder (asla tamamen durmaz)
                float rate = staminaRegenRate;
                if (PlayerLocomotion.Instance != null && PlayerLocomotion.Instance.IsBlocking)
                {
                    rate *= 0.5f;
                }

                currentStamina += rate * Time.deltaTime;
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
                // Stamina bittiğinde bile hafifçe eksiye düşmeden 0'a çekilir ve hemen toparlanmaya başlar
                currentStamina = 0f;
                regenTimer = staminaRegenDelay;
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
                // Saldırganın yönünü net olarak belirle
                Vector3 toAttacker = Vector3.zero;
                if (attacker != null)
                {
                    toAttacker = attacker.position - transform.position;
                }
                else if (hitPoint != Vector3.zero)
                {
                    toAttacker = hitPoint - transform.position;
                }
                else
                {
                    toAttacker = -hitDirection;
                }
                toAttacker.y = 0;

                // Gelen saldırı ön cepheden mi (Geniş 200° blok açısı: dot > -0.15f)
                float dot = Vector3.Dot(transform.forward, toAttacker.normalized);
                if (dot > -0.15f)
                {
                    // BLOK BAŞARILI! Kesinlikle can hasarı almaz (0 Can Hasarı)
                    float staminaCost = Mathf.Max(6f, damage * 0.35f);
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

                    Audio.SoulsAudioManager.GetOrCreate().PlayBlock(hitPoint);
                    SpawnBlockEffect(hitPoint, toAttacker.normalized);
                    CombatDebugVisualizer.SpawnContactPoint(hitPoint, toAttacker.normalized, true, "🛡️ OYUNCU KALKAN TEMASI");
                    return; // Can hasarını kesinlikle engeller!
                }
            }

            // Normal Hasar
            Audio.SoulsAudioManager.GetOrCreate().PlayHit(hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 1.2f);
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
