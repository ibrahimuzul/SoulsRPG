using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulsRPG.CameraSystem;
using SoulsRPG.Combat;
using SoulsRPG.Player;
using SoulsRPG.UI;
using SoulsRPG.Audio;

namespace SoulsRPG.Boss
{
    public enum BossAIState
    {
        Idle,
        Approaching,
        CombatStance,
        Attacking,
        PhaseTransition,
        Staggered,
        Dead
    }

    /// <summary>
    /// Dark Souls Boss Mekaniği: Gölge Şövalye Kumandanı Malakor
    /// Faz 1 (Ağır Zırh + Kalkan Koruması) -> Faz Geçişi (Kükreme + Alev Gazabı) -> Faz 2 (Zemin Patlaması + Hızlı Öfkeli Kombolar)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(LockOnTarget))]
    public class BossController : MonoBehaviour, IDamageable
    {
        [Header("Boss Identity")]
        [SerializeField] private string bossName = "GÖLGE ŞÖVALYE KUMANDANI - MALAKOR";
        [SerializeField] private string phase1Title = "LANETLİ KALE MUHAFIZI";
        [SerializeField] private string phase2Title = "MALAKOR - LANETLİ GAZAP";
        [SerializeField] private int maxHealth = 600;
        [SerializeField] private int soulsReward = 3000;
        [SerializeField] private float maxPoise = 120f;

        [Header("Movement & Range")]
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float phase2Speed = 3.8f;
        [SerializeField] private float strafeSpeed = 2.0f;
        [SerializeField] private float rotationSpeed = 7.0f;
        [SerializeField] private float aggroRange = 22.0f;
        [SerializeField] private float combatDistance = 4.2f;
        [SerializeField] private float meleeAttackRange = 3.2f;
        [SerializeField] private float gravity = -20f;

        [Header("Phase 1 Attacks")]
        [SerializeField] private int heavySlashDamage = 22;
        [SerializeField] private int overheadSmashDamage = 26;
        [SerializeField] private int shieldBashDamage = 14;
        [SerializeField] private float phase1AttackCooldown = 1.9f;
        [SerializeField] private float blockChance = 0.40f;

        [Header("Phase 2 Attacks")]
        [SerializeField] private int shockwaveDamage = 20;
        [SerializeField] private float shockwaveRadius = 6.5f;
        [SerializeField] private int comboSlash1Damage = 15;
        [SerializeField] private int comboSlash2Damage = 18;
        [SerializeField] private float phase2AttackCooldown = 1.2f;
        [SerializeField] private float phase2ParryChance = 0.30f;

        // Current State
        private BossAIState currentState = BossAIState.Idle;
        private int currentHealth;
        private float currentPoise;
        private bool isDead = false;
        private bool isPhase2 = false;
        private bool isInvulnerable = false;
        private bool isBlocking = false;
        private bool isHUDActivated = false;

        private float attackCooldownTimer = 0f;
        private float blockTimer = 0f;
        private float strafeTimer = 0f;
        private int strafeDir = 1;
        private float verticalVelocity = 0f;

        // References
        private CharacterController characterController;
        private Animator animator;
        private Transform playerTarget;
        private PlayerStats playerStats;
        private WeaponDamageCollider weaponCollider;
        private BossPhaseAura phaseAura;
        private Renderer[] allRenderers;
        private readonly List<Color> originalColors = new List<Color>();
        private Coroutine activeAttackRoutine;
        private Coroutine recoilRoutine;

        public bool IsDead => isDead;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsPhase2 => isPhase2;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            phaseAura = GetComponent<BossPhaseAura>() ?? gameObject.AddComponent<BossPhaseAura>();

            currentHealth = maxHealth;
            currentPoise = maxPoise;

            CacheRenderers();
        }

        private void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (p != null)
            {
                playerTarget = p.transform;
                playerStats = p.GetComponent<PlayerStats>();
            }

            FindWeaponCollider();
        }

        private void FindWeaponCollider()
        {
            if (weaponCollider == null)
            {
                weaponCollider = GetComponentInChildren<WeaponDamageCollider>();
            }
        }

        private void CacheRenderers()
        {
            allRenderers = GetComponentsInChildren<Renderer>();
            originalColors.Clear();
            foreach (var r in allRenderers)
            {
                if (r != null && r.material != null && r.material.HasProperty("_Color"))
                {
                    originalColors.Add(r.material.color);
                }
            }
        }

        private void Update()
        {
            if (isDead) return;

            ApplyGravity();

            if (playerTarget == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
                if (p != null)
                {
                    playerTarget = p.transform;
                    playerStats = p.GetComponent<PlayerStats>();
                }
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);

            // Proximity HUD Trigger
            if (!isHUDActivated && distToPlayer <= aggroRange)
            {
                ActivateBossFight();
            }

            // Cooldowns
            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;

            // Shield block timer (Phase 1)
            if (isBlocking)
            {
                blockTimer -= Time.deltaTime;
                if (blockTimer <= 0f)
                {
                    SetBlocking(false);
                }
            }

            // State Machine
            switch (currentState)
            {
                case BossAIState.Idle:
                    UpdateIdleState(distToPlayer);
                    break;
                case BossAIState.Approaching:
                    UpdateApproachingState(distToPlayer);
                    break;
                case BossAIState.CombatStance:
                    UpdateCombatStanceState(distToPlayer);
                    break;
                case BossAIState.Attacking:
                case BossAIState.PhaseTransition:
                case BossAIState.Staggered:
                    // Handled in Coroutines
                    break;
            }
        }

        private void ActivateBossFight()
        {
            isHUDActivated = true;
            var hud = BossHealthBarHUD.GetOrCreate();
            if (hud != null)
            {
                hud.ShowBoss(bossName, phase1Title, currentHealth, maxHealth);
            }
        }

        private void ApplyGravity()
        {
            if (characterController.isGrounded)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            characterController.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
        }

        private void UpdateIdleState(float distToPlayer)
        {
            SetAnimationSpeed(0f);
            if (distToPlayer <= aggroRange)
            {
                currentState = BossAIState.Approaching;
            }
        }

        private void UpdateApproachingState(float distToPlayer)
        {
            RotateTowards(playerTarget.position);

            float speed = isPhase2 ? phase2Speed : walkSpeed;
            SetAnimationSpeed(isPhase2 ? 1.0f : 0.5f);

            Vector3 moveDir = (playerTarget.position - transform.position).normalized;
            moveDir.y = 0;
            characterController.Move(moveDir * (speed * Time.deltaTime));

            if (distToPlayer <= combatDistance)
            {
                currentState = BossAIState.CombatStance;
            }
        }

        private void UpdateCombatStanceState(float distToPlayer)
        {
            RotateTowards(playerTarget.position);

            // Strafe & pacing
            strafeTimer -= Time.deltaTime;
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(1.5f, 3.0f);
                strafeDir = Random.value > 0.5f ? 1 : -1;

                // Phase 1 shield guard & Phase 2 mace deflect stance
                if (!isPhase2 && Random.value < blockChance && !isBlocking)
                {
                    SetBlocking(true, Random.Range(1.5f, 2.8f));
                }
                else if (isPhase2 && Random.value < phase2ParryChance && !isBlocking)
                {
                    SetBlocking(true, Random.Range(1.2f, 2.2f));
                }
            }

            Vector3 strafeVector = transform.right * strafeDir * strafeSpeed * Time.deltaTime;
            characterController.Move(strafeVector);
            SetAnimationSpeed(0.4f);

            // Trigger Attacks
            if (attackCooldownTimer <= 0f && distToPlayer <= meleeAttackRange + 1.0f)
            {
                ChooseAndExecuteAttack(distToPlayer);
            }
            else if (distToPlayer > combatDistance + 2.5f)
            {
                currentState = BossAIState.Approaching;
            }
        }

        private void ChooseAndExecuteAttack(float distToPlayer)
        {
            SetBlocking(false);

            if (!isPhase2)
            {
                // Phase 1 Attacks
                float roll = Random.value;
                if (distToPlayer < 2.2f && roll < 0.35f)
                {
                    activeAttackRoutine = StartCoroutine(ShieldBashRoutine());
                }
                else if (roll < 0.70f)
                {
                    activeAttackRoutine = StartCoroutine(HeavySlashRoutine());
                }
                else
                {
                    activeAttackRoutine = StartCoroutine(OverheadSmashRoutine());
                }
            }
            else
            {
                // Phase 2 Attacks (Furious & Devastating)
                float roll = Random.value;
                if (roll < 0.40f)
                {
                    activeAttackRoutine = StartCoroutine(GroundSlamShockwaveRoutine());
                }
                else if (roll < 0.75f)
                {
                    activeAttackRoutine = StartCoroutine(FuriousDoubleComboRoutine());
                }
                else
                {
                    activeAttackRoutine = StartCoroutine(HeavySlashRoutine(1.3f));
                }
            }
        }

        // ==========================================
        // ATTACK COROUTINES
        // ==========================================

        private IEnumerator HeavySlashRoutine(float speedMultiplier = 1.0f)
        {
            currentState = BossAIState.Attacking;
            SetAnimationSpeed(0f);
            if (animator != null) animator.SetTrigger("isAttacking");

            FindWeaponCollider();

            // 1. Wind-up (0.45s)
            float windup = 0.46f / speedMultiplier;
            yield return new WaitForSeconds(windup);

            // 2. Active Slash
            SoulsAudioManager.GetOrCreate().PlaySwing(transform.position + transform.forward * 1.2f + Vector3.up * 1.5f);
            if (weaponCollider != null) weaponCollider.EnableDamageCollider(heavySlashDamage);

            float active = 0.42f / speedMultiplier;
            yield return new WaitForSeconds(active);

            // 3. Recovery
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();

            float recovery = 0.35f / speedMultiplier;
            yield return new WaitForSeconds(recovery);

            EndAttack();
        }

        private IEnumerator OverheadSmashRoutine()
        {
            currentState = BossAIState.Attacking;
            SetAnimationSpeed(0f);
            if (animator != null) animator.SetTrigger("isAttacking");

            FindWeaponCollider();

            // Windup: High heavy preparation
            yield return new WaitForSeconds(0.55f);

            SoulsAudioManager.GetOrCreate().PlayBossOverhead(transform.position + transform.forward * 1.5f + Vector3.up * 2f);
            if (weaponCollider != null) weaponCollider.EnableDamageCollider(overheadSmashDamage);

            yield return new WaitForSeconds(0.40f);

            if (weaponCollider != null) weaponCollider.DisableDamageCollider();
            yield return new WaitForSeconds(0.50f);

            EndAttack();
        }

        private IEnumerator ShieldBashRoutine()
        {
            currentState = BossAIState.Attacking;
            SetAnimationSpeed(0f);
            SetBlocking(true, 0.8f);

            // Forward lunge impulse
            Vector3 bashDir = transform.forward;
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.deltaTime;
                characterController.Move(bashDir * (4.5f * Time.deltaTime));
                yield return null;
            }

            // Check hit in front
            if (playerStats != null && Vector3.Distance(transform.position, playerStats.transform.position) < 2.4f)
            {
                playerStats.TakeDamage(shieldBashDamage, transform.position + transform.forward * 1.5f, bashDir, transform);
                SoulsAudioManager.GetOrCreate().PlayBlock(transform.position + transform.forward * 1.2f);
            }

            yield return new WaitForSeconds(0.4f);
            SetBlocking(false);
            EndAttack();
        }

        private IEnumerator GroundSlamShockwaveRoutine()
        {
            currentState = BossAIState.Attacking;
            SetAnimationSpeed(0f);
            if (animator != null) animator.SetTrigger("isAttacking");

            FindWeaponCollider();

            // 1. High Leap into the air
            Vector3 leapDir = (playerTarget.position - transform.position).normalized;
            leapDir.y = 0;
            float leapElapsed = 0f;
            while (leapElapsed < 0.48f)
            {
                leapElapsed += Time.deltaTime;
                characterController.Move((leapDir * 3.5f + Vector3.up * 3.2f) * Time.deltaTime);
                yield return null;
            }

            // 2. Downward impact slam!
            float downElapsed = 0f;
            while (downElapsed < 0.22f)
            {
                downElapsed += Time.deltaTime;
                characterController.Move((Vector3.down * 12.0f) * Time.deltaTime);
                yield return null;
            }

            // 3. Impact & Explosive AOE Shockwave! (Tekil ve dengeli hasar)
            Vector3 impactPos = transform.position + transform.forward * 1.2f;
            SoulsAudioManager.GetOrCreate().PlayBossSlam(impactPos);

            var shockwaveGo = new GameObject("BossGroundShockwave");
            var shockwave = shockwaveGo.AddComponent<BossShockwave>();
            shockwave.Initialize(transform, shockwaveDamage, shockwaveRadius);

            yield return new WaitForSeconds(0.60f);
            EndAttack();
        }

        private IEnumerator FuriousDoubleComboRoutine()
        {
            currentState = BossAIState.Attacking;
            SetAnimationSpeed(0f);
            FindWeaponCollider();

            // Swing 1
            if (animator != null) animator.SetTrigger("isAttacking");
            yield return new WaitForSeconds(0.38f);
            SoulsAudioManager.GetOrCreate().PlaySwing(transform.position + transform.forward * 1.2f + Vector3.up * 1.5f);
            if (weaponCollider != null) weaponCollider.EnableDamageCollider(comboSlash1Damage);
            yield return new WaitForSeconds(0.32f);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();

            // Tiny delay between combo strikes
            yield return new WaitForSeconds(0.15f);

            // Swing 2
            if (animator != null) animator.SetTrigger("isAttacking");
            RotateTowards(playerTarget.position);
            yield return new WaitForSeconds(0.35f);
            SoulsAudioManager.GetOrCreate().PlaySwing(transform.position + transform.forward * 1.2f + Vector3.up * 1.5f);
            if (weaponCollider != null) weaponCollider.EnableDamageCollider(comboSlash2Damage);
            yield return new WaitForSeconds(0.32f);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();

            yield return new WaitForSeconds(0.40f);
            EndAttack();
        }

        private void EndAttack()
        {
            currentState = BossAIState.CombatStance;
            attackCooldownTimer = isPhase2 ? phase2AttackCooldown : phase1AttackCooldown;
        }

        // ==========================================
        // PHASE TRANSITION (KÜKREME & ALEV GAZABI)
        // ==========================================

        private void TriggerPhaseTransition()
        {
            isPhase2 = true;
            if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();

            SetBlocking(false);
            StartCoroutine(PhaseTransitionRoutine());
        }

        private IEnumerator PhaseTransitionRoutine()
        {
            currentState = BossAIState.PhaseTransition;
            isInvulnerable = true;
            SetAnimationSpeed(0f);

            // 1. Kükreme sesi ve UI güncellemesi
            SoulsAudioManager.GetOrCreate().PlayBossRoar(transform.position + Vector3.up * 2f);

            var hud = BossHealthBarHUD.Instance;
            if (hud != null) hud.SetPhase2Title(phase2Title);

            // 2. Kalkanı devre dışı bırak / stowed
            StowShield();

            // 3. Alev aurası ve kırmızı silah ışıltısı
            if (phaseAura != null)
            {
                phaseAura.ActivatePhase2Aura();
            }

            // Göz alıcı kırmızı flaş
            FlashRedEffect(2.2f);

            yield return new WaitForSeconds(2.4f);

            isInvulnerable = false;
            currentState = BossAIState.CombatStance;
            attackCooldownTimer = 0.5f; // Hemen saldırıya hazır!
        }

        private void StowShield()
        {
            var shield = transform.Find("KnightVisual/root/pelvis/spine_01/spine_02/spine_03/clavicle_l/upperarm_l/lowerarm_l/hand_l/Shield_Equipped")
                         ?? FindChildRecursive(transform, "Shield_Equipped");
            if (shield != null)
            {
                shield.gameObject.SetActive(false);
            }
        }

        private Transform FindChildRecursive(Transform cur, string target)
        {
            if (cur.name.Equals(target, System.StringComparison.OrdinalIgnoreCase)) return cur;
            for (int i = 0; i < cur.childCount; i++)
            {
                var f = FindChildRecursive(cur.GetChild(i), target);
                if (f != null) return f;
            }
            return null;
        }

        // ==========================================
        // IDAMAGEABLE & COMBAT
        // ==========================================

        public void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, Transform attacker = null)
        {
            if (isDead || isInvulnerable) return;

            // HUD aktifleşmemişse vurulunca hemen aç
            if (!isHUDActivated) ActivateBossFight();

            // Block / Parry Check (Faz 1 Kalkan Koruması & Faz 2 Topuzla Darbe Savuşturma)
            if (isBlocking)
            {
                Vector3 toAttacker = attacker != null ? attacker.position - transform.position : -hitDirection;
                toAttacker.y = 0;
                float dot = Vector3.Dot(transform.forward, toAttacker.normalized);

                if (dot > -0.2f)
                {
                    // Darbe engellendi!
                    SoulsAudioManager.GetOrCreate().PlayBlock(hitPoint != Vector3.zero ? hitPoint : transform.position + transform.forward);
                    string blockLabel = isPhase2 ? "🔥 BOSS TOPUZLA SAVUŞTURDU (DEFLECT)" : "🛡️ BOSS KALKAN SAVUNMASI";
                    CombatDebugVisualizer.SpawnContactPoint(hitPoint, toAttacker.normalized, true, blockLabel);
                    return;
                }
            }

            // Normal Damage
            currentHealth = Mathf.Max(0, currentHealth - damage);
            SoulsAudioManager.GetOrCreate().PlayHit(hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 1.5f);

            // Update UI
            if (BossHealthBarHUD.Instance != null)
            {
                BossHealthBarHUD.Instance.UpdateBossHealth(currentHealth, maxHealth);
            }

            // Poise calculation
            currentPoise -= damage;
            if (currentPoise <= 0f && currentState != BossAIState.PhaseTransition)
            {
                TriggerStagger();
            }

            // Check Phase 2 Trigger (At <= 50% HP)
            if (!isPhase2 && currentHealth <= maxHealth * 0.5f)
            {
                TriggerPhaseTransition();
                return;
            }

            // Check Death
            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                FlashWhite();
            }
        }

        private void TriggerStagger()
        {
            currentPoise = maxPoise;
            if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();

            StartCoroutine(StaggerRoutine());
        }

        private IEnumerator StaggerRoutine()
        {
            currentState = BossAIState.Staggered;
            SetAnimationSpeed(0f);
            SetBlocking(false);

            // Stagger recoil
            Vector3 pushBack = -transform.forward;
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.deltaTime;
                characterController.Move(pushBack * (2.8f * Time.deltaTime));
                yield return null;
            }

            yield return new WaitForSeconds(1.8f);
            currentState = BossAIState.CombatStance;
            attackCooldownTimer = 0.8f;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            currentState = BossAIState.Dead;

            if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();

            SetBlocking(false);
            SetAnimationSpeed(0f);

            // Zafer Afişi ve Ruh Ödülü
            if (BossHealthBarHUD.Instance != null)
            {
                BossHealthBarHUD.Instance.OnBossDefeated(soulsReward);
            }

            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            // Yavaşça diz çökme ve çözülme
            float elapsed = 0f;
            Vector3 initialPos = transform.position;

            while (elapsed < 3.0f)
            {
                elapsed += Time.deltaTime;
                // Slowly sink into ground slightly
                transform.position = initialPos + Vector3.down * (elapsed * 0.15f);
                yield return null;
            }

            // Karakteri sahneden temizle veya ceset bırak
            Destroy(gameObject, 1.0f);
        }

        private void SetBlocking(bool blocking, float duration = 0f)
        {
            isBlocking = blocking;
            if (animator != null) animator.SetBool("isBlocking", blocking);
            if (blocking) blockTimer = duration;
        }

        private void SetAnimationSpeed(float speed)
        {
            if (animator != null) animator.SetFloat("Speed", speed);
        }

        private void RotateTowards(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private void FlashWhite()
        {
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            recoilRoutine = StartCoroutine(FlashColorRoutine(new Color(1f, 0.4f, 0.4f), 0.12f));
        }

        private void FlashRedEffect(float duration)
        {
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            recoilRoutine = StartCoroutine(FlashColorRoutine(new Color(1f, 0.1f, 0.05f), duration));
        }

        private IEnumerator FlashColorRoutine(Color flashColor, float duration)
        {
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null && allRenderers[i].material.HasProperty("_Color"))
                {
                    allRenderers[i].material.color = flashColor;
                }
            }

            yield return new WaitForSeconds(duration);

            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null && allRenderers[i].material.HasProperty("_Color") && i < originalColors.Count)
                {
                    allRenderers[i].material.color = originalColors[i];
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, meleeAttackRange);

            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
        }
    }
}