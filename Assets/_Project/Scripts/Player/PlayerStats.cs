using System;
using UnityEngine;
using SoulsRPG.Combat;
using SoulsRPG.Core;
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

        public const int BaseAttackDamage = 25;

        [Header("Dark Souls Level & Souls")]
        [SerializeField] private int soulLevel = 1;
        [SerializeField] private int currentSouls = 500; // Başlangıçta seviye atlamayı test etmek için 500 ruh

        [Header("Dark Souls Attributes (Nitelikler)")]
        [SerializeField] private int vigor = 10;      // Can (Max HP)
        [SerializeField] private int endurance = 10;  // Dayanıklılık (Max Stamina & Yenilenme)
        [SerializeField] private int strength = 10;   // Saldırı Gücü (Attack Damage)
        [SerializeField] private int dexterity = 10;  // Hasar + Saldırı Hızı
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
        [Tooltip("Son darbeden bu kadar saniye sonra poise tamamen yenilenir")]
        [SerializeField] private float poiseResetDelay = 4f;
        [Tooltip("Poise kırılınca oyuncunun sendeleme süresi")]
        [SerializeField] private float poiseBreakStaggerDuration = 0.6f;
        private float currentPoise;
        private float poiseResetTimer = 0f;

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
        public event Action OnRespawn;
        /// <summary>Bonfire'da dinlenildi (kayıttan hemen önce çağrılır).</summary>
        public event Action OnRestedAtBonfire;

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
        public float CurrentPoise => currentPoise;
        public float MaxPoise => maxPoise;
        public int PhysicalDefense => physicalDefense;
        public int BonusAttackDamage => bonusAttackDamage;

        /// <summary>Hafif saldırının toplam hasarı (taban + Kuvvet + Beceri).</summary>
        public int AttackDamage => BaseAttackDamage + bonusAttackDamage;

        /// <summary>Beceri ile artan saldırı hızı çarpanı (en fazla %25).</summary>
        public float AttackSpeedMultiplier => CalculateAttackSpeed(dexterity);

        public Vector3? LastBonfirePosition { get; private set; }
        public string LastBonfireId { get; private set; }
        public string LastBonfireName { get; private set; }
        public string LastBonfireScene { get; private set; }

        // Yenilen boss'lar (kayıtta tutulur)
        private readonly System.Collections.Generic.HashSet<string> defeatedBosses = new System.Collections.Generic.HashSet<string>();
        public System.Collections.Generic.IEnumerable<string> DefeatedBosses => defeatedBosses;

        public bool IsBossDefeated(string bossId) => !string.IsNullOrEmpty(bossId) && defeatedBosses.Contains(bossId);

        /// <summary>Boss'u yenildi olarak işaretler ve hemen kaydeder.</summary>
        public void MarkBossDefeated(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || !defeatedBosses.Add(bossId)) return;
            SaveManager.SavePlayer(this);
        }

        /// <summary>Test kolaylığı: yenilmiş boss listesini (bellekte ve kayıtta) boşaltır. Kaç boss sıfırlandığını döndürür.</summary>
        public int ClearDefeatedBosses()
        {
            int count = defeatedBosses.Count;
            defeatedBosses.Clear();
            SaveManager.SavePlayer(this);
            return count;
        }

        // Ruh lekesi (Bloodstain): ölünce kaybedilen ruhlar
        public Vector3? BloodstainPosition { get; private set; }
        public int BloodstainSouls { get; private set; }

        public static int CalculateBonusDamage(int str, int dex)
        {
            return (str - 10) * 2 + (dex - 10);
        }

        public static float CalculateAttackSpeed(int dex)
        {
            return 1f + Mathf.Clamp((dex - 10) * 0.01f, 0f, 0.25f);
        }

        private void Awake()
        {
            // Tek oyuncu: Core'daki oyuncu varken (ör. bir bölge sahnesine yanlışlıkla konmuş) ikinci bir kopya silinir
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[PlayerStats] İkinci oyuncu bulundu ('{name}', sahne: {gameObject.scene.name}); siliniyor. Oyuncu yalnızca Core sahnesinde olmalı.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GetComponent<PlayerDeathHandler>() == null)
            {
                gameObject.AddComponent<PlayerDeathHandler>();
            }

            // Item kutusu (iksirler vb.) ve item kullanımı
            if (GetComponent<Items.ItemBox>() == null) gameObject.AddComponent<Items.ItemBox>();
            if (GetComponent<Items.PlayerItemUser>() == null) gameObject.AddComponent<Items.PlayerItemUser>();

            // Kayıtlı veriyi yükle (veya varsayılanları hesapla)
            LoadSavedData();
            RecalculateStats(false);

            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentPoise = maxPoise;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            HandlePoiseReset();
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

            // 3. Kuvvet (Strength) +2 / Beceri (Dexterity) +1 hasar; Beceri ayrıca saldırı hızını artırır
            bonusAttackDamage = CalculateBonusDamage(strength, dexterity);

            // 4. Direnç (Vitality): Poise ve Fiziksel Savunma artışı
            maxPoise = 50f + (vitality - 10) * 4f;

            // 5. Otomatik Seviye Ölçeklenmesi: Dark Souls kuralı olarak her seviye genel savunmayı otomatik artırır!
            physicalDefense = (vitality - 10) + soulLevel;

            if (healToFull)
            {
                currentHealth = maxHealth;
                currentStamina = maxStamina;
                currentPoise = maxPoise;
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnStatsUpdated?.Invoke();
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
            GameLog.Info($"[PlayerStats] +{amount:N0} ruh (Toplam: {currentSouls:N0})");
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
            GameLog.Info($"[PlayerStats] Seviye atlandı. Seviye: {soulLevel}, Kalan Ruh: {currentSouls:N0}");
            return true;
        }

        /// <summary>
        /// Bonfire'a oturulduğunda can ve dayanıklılığı tamamen doldurur, dünyayı sıfırlar ve oyunu kaydeder.
        /// </summary>
        /// <param name="bonfireId">Bonfire'ın benzersiz kimliği (doğma noktası bununla bulunur)</param>
        /// <param name="bonfireName">Ekranda/kayıtta görünen adı</param>
        /// <param name="bonfireScene">Bonfire'ın bulunduğu sahne (bölge). Doğarken önce bu sahne yüklenir.</param>
        public void RestAtBonfire(string bonfireId, string bonfireName, string bonfireScene, Vector3 bonfirePosition)
        {
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentPoise = maxPoise;
            LastBonfirePosition = bonfirePosition;
            LastBonfireId = bonfireId;
            LastBonfireName = bonfireName;
            LastBonfireScene = bonfireScene;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            // Sesi çal
            Audio.SoulsAudioManager.GetOrCreate().PlayBonfireRest(bonfirePosition);

            // Souls kuralı: dinlenince düşmanlar yeniden doğar
            WorldEvents.ResetWorld();

            // İksirler vb. dolar (kayıttan önce, yenilenmiş adetler kaydedilsin)
            OnRestedAtBonfire?.Invoke();

            // Kaydet
            SaveManager.SavePlayer(this, bonfirePosition);
        }

        // ==========================================
        // ÖLÜM, RUH LEKESİ VE YENİDEN DOĞMA
        // ==========================================

        /// <summary>
        /// Ölümde taşınan tüm ruhları kaybeder ve ölüm noktasına ruh lekesi bırakır.
        /// Önceki (alınmamış) leke varsa o ruhlar sonsuza dek kaybolur.
        /// </summary>
        public void DropSoulsAsBloodstain(Vector3 position)
        {
            BloodstainSouls = currentSouls;
            BloodstainPosition = currentSouls > 0 ? position : (Vector3?)null;
            currentSouls = 0;
            OnSoulsChanged?.Invoke(currentSouls);
        }

        /// <summary>Ruh lekesine dokununca kaybedilen ruhları geri alır.</summary>
        public int RecoverBloodstain()
        {
            int amount = BloodstainSouls;
            BloodstainSouls = 0;
            BloodstainPosition = null;
            if (amount > 0)
            {
                currentSouls += amount;
                OnSoulsChanged?.Invoke(currentSouls);
            }
            SaveManager.SavePlayer(this);
            return amount;
        }

        /// <summary>Ölümden sonra tam can/stamina ile yeniden doğar.</summary>
        public void Respawn()
        {
            isDead = false;
            isInvulnerable = false;
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            currentPoise = maxPoise;
            regenTimer = 0f;
            poiseResetTimer = 0f;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnRespawn?.Invoke();
        }

        private void LoadSavedData()
        {
            var data = SaveManager.LoadPlayer();

            // Item kutusu kendi kısmını yükler (kayıt yoksa başlangıç itemları: 3 Can İksiri)
            var itemBox = GetComponent<Items.ItemBox>();
            if (itemBox != null) itemBox.LoadFromSave(data);

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
                    LastBonfireId = data.lastBonfireId;
                    LastBonfireName = data.lastBonfireName;
                    LastBonfireScene = data.lastBonfireScene;
                }

                defeatedBosses.Clear();
                if (data.defeatedBosses != null)
                {
                    foreach (var id in data.defeatedBosses) defeatedBosses.Add(id);
                }

                if (data.hasBloodstain && data.bloodstainSouls > 0)
                {
                    BloodstainSouls = data.bloodstainSouls;
                    BloodstainPosition = new Vector3(data.bloodstainX, data.bloodstainY, data.bloodstainZ);
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

        private void HandlePoiseReset()
        {
            if (isDead || currentPoise >= maxPoise) return;

            poiseResetTimer -= Time.deltaTime;
            if (poiseResetTimer <= 0f)
            {
                currentPoise = maxPoise;
            }
        }

        /// <summary>
        /// Souls kuralı: stamina 0'ın üzerindeyse eylem yapılabilir (bar eksiye düşmez, 0'da kalır).
        /// Stamina tamamen bitmişse eylem reddedilir.
        /// </summary>
        public bool UseStamina(float amount)
        {
            if (currentStamina <= 0f)
            {
                return false;
            }

            currentStamina = Mathf.Max(0f, currentStamina - amount);
            regenTimer = staminaRegenDelay;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            return true;
        }

        public bool HasStamina(float amount = 1f)
        {
            return currentStamina >= amount;
        }

        public void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, Transform attacker = null, float poiseMultiplier = 1f)
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

            // Normal Hasar (Fiziksel savunma ile hafif hasar kırma)
            int effectiveDamage = Mathf.Max(1, damage - (physicalDefense / 3));
            float poiseDamage = Mathf.Max(10f, damage * 0.75f) * poiseMultiplier;
            Audio.SoulsAudioManager.GetOrCreate().PlayHit(hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 1.2f);
            TakeDamage(effectiveDamage, attacker, poiseDamage);
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

            RuntimeShaders.ApplyParticleMaterial(ps, Color.white);
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

            if (currentHealth <= 0)
            {
                Die();
                return;
            }

            // Poise: kırılırsa oyuncu sendeler ve saldırısı iptal olur
            currentPoise -= poiseDamage;
            poiseResetTimer = poiseResetDelay;
            if (currentPoise <= 0f)
            {
                currentPoise = maxPoise;
                var locomotion = PlayerLocomotion.Instance;
                if (locomotion != null)
                {
                    Vector3 pushDir = attacker != null ? (transform.position - attacker.position) : -transform.forward;
                    locomotion.TriggerHitStagger(poiseBreakStaggerDuration, pushDir);
                }
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
            if (isDead) return;
            isDead = true;
            isInvulnerable = false;

            var locomotion = PlayerLocomotion.Instance;
            if (locomotion != null)
            {
                locomotion.OnPlayerDied();
            }

            OnDeath?.Invoke();
            GameLog.Info("[PlayerStats] YOU DIED");
        }
    }
}
