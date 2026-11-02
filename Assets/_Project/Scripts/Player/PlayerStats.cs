using System;
using UnityEngine;
using SoulsRPG.Combat;
using SoulsRPG.Save;

namespace SoulsRPG.Player
{
    /// <summary>
    /// Dark Souls Seviye (Level), Nitelik (Attributes) ve Karakter İstatistik Sistemi.
    /// Canlılık (Vigor), Dayanıklılık (Endurance), Kuvvet (Strength), Beceri (Dexterity), Direnç (Vitality)
    /// </summary>
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public static PlayerStats Instance { get; private set; }

        [Header("Dark Souls Level & Souls")]
        [SerializeField] private int soulLevel = 1;
        [SerializeField] private int currentSouls = 500; // Başlangıçta seviye atlamayı test etmek için 500 ruh

        [Header("Dark Souls Attributes (Nitelikler)")]
        [SerializeField] private int vigor = 10;      // Can (Max HP)
        [SerializeField] private int endurance = 10;  // Dayanıklılık (Max Stamina & Yenilenme)
        [SerializeField] private int strength = 10;   // Saldırı Gücü (Attack Damage)
        [SerializeField] private int dexterity = 10;  // Çeviklik & Vuruş Verimliliği
        [SerializeField] private int vitality = 10;   // Denge & Zırh (Poise & Defense)

        [Header("Derived Combat Stats")]
        [SerializeField] private int maxHealth = 100;
        private int currentHealth;

        [SerializeField] private float maxStamina = 120f;
        [SerializeField] private float staminaRegenRate = 60f;
        [SerializeField] private float staminaRegenDelay = 0.35f;
        private float currentStamina;
        private float regenTimer = 0f;

        [SerializeField] private float maxPoise = 50f;
        private float currentPoise;

        [SerializeField] private int physicalDefense = 10;
        [SerializeField] private int bonusAttackDamage = 0;

        [Header("State Flags")]
        public bool isInvulnerable = false; // i-frame active during roll
        public bool isDead = false;

        public bool IsDead => isDead;

        // Events for UI HUD
        public event Action<int, int> OnHealthChanged;
        public event Action<float, float> OnStaminaChanged;
        public event Action<int> OnSoulsChanged;
        public event Action OnStatsUpdated;
        public event Action OnDeath;

        // Public Properties
        public int SoulLevel => soulLevel;
        public int CurrentSouls => currentSouls;
        public int Vigor => vigor;
        public int Endurance => endurance;
        public int Strength => strength;
        public int Dexterity => dexterity;
        public int Vitality => vitality;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public int PhysicalDefense => physicalDefense;
        public int BonusAttackDamage => bonusAttackDamage;

        public Vector3? LastBonfirePosition { get; private set; }

        private void Awake()
        {
            Instance = this;

            // Kayıtlı veriyi yükle (veya varsayılanları hesapla)
            LoadSavedData();
            RecalculateStats(false);

            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentPoise = maxPoise;
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnSoulsChanged?.Invoke(currentSouls);
            OnStatsUpdated?.Invoke();
        }

        private void Update()
        {
            HandleStaminaRegeneration();
        }

        /// <summary>
        /// Niteliklere ve Soul Level'a göre tüm türetilmiş istatistikleri Dark Souls formülleriyle günceller.
        /// </summary>
        public void RecalculateStats(bool healToFull = false)
        {
            // 1. Can (Vigor): Base 100 + her vigor için 15 can + her level için 2 can otomatik
            maxHealth = 100 + (vigor - 10) * 15 + (soulLevel - 1) * 2;

            // 2. Dayanıklılık (Endurance): Base 120 + her endurance için 5 stamina + yenilenme artışı
            maxStamina = 120f + (endurance - 10) * 5f;
            staminaRegenRate = 60f + (endurance - 10) * 2f;

            // 3. Kuvvet (Strength): Her strength için +2 taban saldırı hasarı bonusu
            bonusAttackDamage = (strength - 10) * 2;

            // 4. Direnç (Vitality): Poise ve Fiziksel Savunma artışı
            maxPoise = 50f + (vitality - 10) * 4f;

            // 5. Otomatik Seviye Ölçeklenmesi: Dark Souls kuralı olarak her seviye genel savunmayı otomatik artırır!
            physicalDefense = (vitality - 10) + soulLevel;

            if (healToFull)
            {
                currentHealth = maxHealth;
                currentStamina = maxStamina;
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnStatsUpdated?.Invoke();

            // Oyuncu saldırı hasarını güncelle
            var loco = PlayerLocomotion.Instance;
            if (loco != null)
            {
                loco.UpdateAttackDamage(25 + bonusAttackDamage);
            }
        }

        /// <summary>
        /// Belirli bir seviyeye geçmek için gereken ruh maliyeti (Authentic Dark Souls scaling curve).
        /// </summary>
        public static int GetRequiredSoulsForLevel(int targetLevel)
        {
            if (targetLevel <= 1) return 500;
            float x = targetLevel;
            return Mathf.RoundToInt(0.02f * x * x * x + 3.1f * x * x + 95f * x + 400f);
        }

        public int GetNextLevelCost()
        {
            return GetRequiredSoulsForLevel(soulLevel + 1);
        }

        public void AddSouls(int amount)
        {
            if (amount <= 0) return;
            currentSouls += amount;
            OnSoulsChanged?.Invoke(currentSouls);
            Debug.Log($"<color=yellow><b>[PlayerStats] +{amount:N0} RUH KAZANILDI! (Toplam: {currentSouls:N0})</b></color>");
        }

        public bool SpendSouls(int amount)
        {
            if (currentSouls < amount) return false;
            currentSouls -= amount;
            OnSoulsChanged?.Invoke(currentSouls);
            return true;
        }

        /// <summary>
        /// Bonfire'da stat puanı vererek seviye atlama işlemi.
        /// </summary>
        public bool ApplyLevelUp(int addVig, int addEnd, int addStr, int addDex, int addVit)
        {
            int totalPoints = addVig + addEnd + addStr + addDex + addVit;
            if (totalPoints <= 0) return false;

            // Kademeli ruh maliyetini hesapla
            int totalCost = 0;
            for (int i = 0; i < totalPoints; i++)
            {
                totalCost += GetRequiredSoulsForLevel(soulLevel + 1 + i);
            }

            if (currentSouls < totalCost)
            {
                Debug.LogWarning("[PlayerStats] Seviye atlamak için yeterli ruh yok!");
                return false;
            }

            // Ruhları harca
            currentSouls -= totalCost;
            soulLevel += totalPoints;

            vigor += addVig;
            endurance += addEnd;
            strength += addStr;
            dexterity += addDex;
            vitality += addVit;

            RecalculateStats(true);

            // Sesi çal
            Audio.SoulsAudioManager.GetOrCreate().PlayBonfireLevelUp(transform.position);

            // Oyunu kaydet
            SaveManager.SavePlayer(this);

            OnSoulsChanged?.Invoke(currentSouls);
            Debug.Log($"<color=cyan><b>[PlayerStats] SEVİYE ATLAMA BAŞARILI! Seviye: {soulLevel}, Kalan Ruh: {currentSouls:N0}</b></color>");
            return true;
        }

        /// <summary>
        /// Bonfire'a oturulduğunda can ve dayanıklılığı tamamen doldurur ve oyunu kaydeder.
        /// </summary>
        public void RestAtBonfire(Vector3 bonfirePosition)
        {
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            LastBonfirePosition = bonfirePosition;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            // Sesi çal
            Audio.SoulsAudioManager.GetOrCreate().PlayBonfireRest(bonfirePosition);

            // Kaydet
            SaveManager.SavePlayer(this, bonfirePosition);
        }

        private void LoadSavedData()
        {
            var data = SaveManager.LoadPlayer();
            if (data != null)
            {
                soulLevel = data.soulLevel;
                currentSouls = data.currentSouls;
                vigor = data.vigor;
                endurance = data.endurance;
                strength = data.strength;
                dexterity = data.dexterity;
                vitality = data.vitality;

                if (data.hasSavedBonfire)
                {
                    LastBonfirePosition = new Vector3(data.respawnX, data.respawnY, data.respawnZ);
                }
            }
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

                float dot = Vector3.Dot(transform.forward, toAttacker.normalized);
                if (dot > -0.15f)
                {
                    float staminaCost = Mathf.Max(6f, damage * 0.35f);
                    bool hadStamina = currentStamina >= staminaCost;
                    UseStamina(staminaCost);

                    if (!hadStamina || currentStamina <= 0f)
                    {
                        locomotion.TriggerGuardBreak();
                    }
                    else
                    {
                        locomotion.TriggerBlockRecoil();
                    }

                    Audio.SoulsAudioManager.GetOrCreate().PlayBlock(hitPoint);
                    SpawnBlockEffect(hitPoint, toAttacker.normalized);
                    CombatDebugVisualizer.SpawnContactPoint(hitPoint, toAttacker.normalized, true, "🛡️ OYUNCU KALKAN TEMASI");
                    return;
                }
            }

            // Normal Hasar (Fiziksel savunma ile hafif hafif hasar kırma)
            int effectiveDamage = Mathf.Max(1, damage - (physicalDefense / 3));
            Audio.SoulsAudioManager.GetOrCreate().PlayHit(hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 1.2f);
            TakeDamage(effectiveDamage, attacker, 15f);
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
            pMain.startColor = new Color(0.4f, 0.8f, 1.0f);
            pMain.loop = false;
            pMain.stopAction = ParticleSystemStopAction.Destroy;

            var pEmission = ps.emission;
            pEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 14) });
        }

        public void TakeDamage(int damage, Transform attacker = null, float poiseDamage = 10f)
        {
            if (isDead || isInvulnerable)
            {
                return;
            }

            currentHealth -= damage;
            if (currentHealth < 0) currentHealth = 0;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            currentPoise -= poiseDamage;
            if (currentPoise <= 0f)
            {
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