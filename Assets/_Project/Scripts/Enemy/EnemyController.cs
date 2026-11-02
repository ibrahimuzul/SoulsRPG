using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SoulsRPG.CameraSystem;
using SoulsRPG.Combat;
using SoulsRPG.Player;

namespace SoulsRPG.Enemy
{
    public enum EnemyAIState
    {
        Idle,
        Patrol,
        Chase,
        CombatStance,
        Attacking,
        Staggered,
        Dead
    }

    [RequireComponent(typeof(LockOnTarget))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        [Header("Enemy Identity & Stats")]
        [SerializeField] private string enemyName = "Gezgin Kara Şövalye";
        [SerializeField] private int maxHealth = 120;
        [SerializeField] private int soulsReward = 250;
        [SerializeField] private float poise = 40f;

        [Header("Perception & Aggro")]
        [SerializeField] private float sightRange = 12.0f;
        [SerializeField] private float sightAngle = 130.0f;
        [SerializeField] private float hearingRange = 3.5f;
        [SerializeField] private float loseAggroDistance = 24.0f;
        [SerializeField] private float loseAggroTime = 4.0f;

        [Header("Movement & Locomotion")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float chaseSpeed = 4.6f;
        [SerializeField] private float strafeSpeed = 1.8f;
        [SerializeField] private float rotationSpeed = 8.0f;
        [SerializeField] private float combatDistance = 3.8f;
        [SerializeField] private float attackRange = 2.1f;
        [SerializeField] private float gravity = -20f;

        [Header("Attack Settings")]
        [SerializeField] private int attackDamage = 25;
        [SerializeField] private float attackWindup = 0.48f;
        [SerializeField] private float attackActive = 0.44f;
        [SerializeField] private float attackRecovery = 0.30f;
        [SerializeField] private float attackCooldown = 1.8f;

        [Header("Shield & Defense")]
        [SerializeField] private float blockChance = 0.35f;
        [SerializeField] private float blockMinDuration = 1.2f;
        [SerializeField] private float blockMaxDuration = 2.4f;

        [Header("Respawn")]
        [SerializeField] private bool autoRespawn = true;
        [SerializeField] private float respawnDelay = 5.0f;

        // Current State
        private EnemyAIState currentState = EnemyAIState.Idle;
        private int currentHealth;
        private float currentPoise;
        private bool isDead = false;
        private bool isBlocking = false;
        private float attackCooldownTimer = 0f;
        private float loseAggroTimer = 0f;
        private float blockTimer = 0f;
        private float strafeTimer = 0f;
        private int strafeDirection = 1; // 1 = right, -1 = left

        // Spatial & Gravity
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float verticalVelocity = 0f;

        // Target References
        private Transform targetPlayer;
        private PlayerStats playerStats;

        // Components
        private CharacterController characterController;
        private Animator animator;
        private LockOnTarget lockOnTarget;
        private EquipmentManager equipmentManager;
        private WeaponDamageCollider weaponCollider;
        private NavMeshAgent navAgent;

        // Hit Reaction & Renderers
        private Renderer[] allRenderers;
        private readonly List<Color> originalColors = new List<Color>();
        private Coroutine recoilRoutine;
        private Coroutine attackRoutine;

        // Dark Souls Floating Health Bar
        private float healthBarTimer = 0f;
        private float displayedHealthFraction = 1.0f;
        private float trailingYellowFraction = 1.0f;

        public bool IsDead => isDead;
        public EnemyAIState CurrentState => currentState;
        public int CurrentHealth => currentHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
            currentPoise = poise;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            var cap = GetComponent<CapsuleCollider>();
            if (cap != null) cap.enabled = false;

            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                Vector3 c = cap != null ? cap.center : new Vector3(0, 1, 0);
                float r = cap != null ? cap.radius : 0.45f;
                float h = cap != null ? cap.height : 2.0f;

                characterController = gameObject.AddComponent<CharacterController>();
                characterController.center = c;
                characterController.radius = r;
                characterController.height = h;
            }

            lockOnTarget = GetComponent<LockOnTarget>();
            navAgent = GetComponent<NavMeshAgent>();

            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<Animator>();
            }

#if UNITY_EDITOR
            if (animator.runtimeAnimatorController == null)
            {
                var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Project/Animations/KnightAnimatorController.controller");
                if (ctrl != null) animator.runtimeAnimatorController = ctrl;
            }
            if (animator.avatar == null)
            {
                var av = UnityEditor.AssetDatabase.LoadAssetAtPath<Avatar>(
                    "Assets/Fantasy knight/Base mesh/Skin_1.fbx");
                if (av != null) animator.avatar = av;
            }
#endif

            // If NavMeshAgent is attached, configure it smoothly
            if (navAgent != null)
            {
                navAgent.updateRotation = false; // We handle smooth rotation
                navAgent.stoppingDistance = attackRange * 0.85f;
            }

            // Equipment Manager (Mace & Shield)
            equipmentManager = GetComponent<EquipmentManager>();
            if (equipmentManager == null)
            {
                equipmentManager = gameObject.AddComponent<EquipmentManager>();
            }
            equipmentManager.FindSocketsIfMissing();
            equipmentManager.UpdateTransforms();

            // Collect renderers for red damage flash
            allRenderers = GetComponentsInChildren<Renderer>();
            originalColors.Clear();
            foreach (var r in allRenderers)
            {
                if (r != null && r.material != null && r.material.HasProperty("_Color"))
                {
                    originalColors.Add(r.material.color);
                }
                else
                {
                    originalColors.Add(Color.white);
                }
            }
        }

        private void Start()
        {
            if (equipmentManager != null)
            {
                weaponCollider = equipmentManager.GetWeaponCollider();
            }
            if (weaponCollider == null)
            {
                weaponCollider = GetComponentInChildren<WeaponDamageCollider>();
            }
            if (weaponCollider != null)
            {
                weaponCollider.SetOwner(transform);
            }

            FindPlayerTarget();
        }

        private void Update()
        {
            if (isDead) return;

            // Health bar floating animation
            if (healthBarTimer > 0f)
            {
                healthBarTimer -= Time.deltaTime;
            }
            float targetFraction = (float)currentHealth / maxHealth;
            displayedHealthFraction = targetFraction;
            trailingYellowFraction = Mathf.MoveTowards(trailingYellowFraction, targetFraction, Time.deltaTime * 0.45f);

            // Cooldowns
            if (attackCooldownTimer > 0f)
            {
                attackCooldownTimer -= Time.deltaTime;
            }
            if (blockTimer > 0f)
            {
                blockTimer -= Time.deltaTime;
                if (blockTimer <= 0f)
                {
                    SetBlocking(false);
                }
            }

            // AI Decision & Movement
            HandleStateFSM();
        }

        private void FindPlayerTarget()
        {
            if (PlayerStats.Instance != null)
            {
                playerStats = PlayerStats.Instance;
                targetPlayer = playerStats.transform;
            }
            else
            {
                var p = FindAnyObjectByType<PlayerStats>();
                if (p != null)
                {
                    playerStats = p;
                    targetPlayer = p.transform;
                }
            }
        }

        private void HandleStateFSM()
        {
            if (targetPlayer == null || (playerStats != null && playerStats.isDead))
            {
                FindPlayerTarget();
                if (targetPlayer == null || (playerStats != null && playerStats.isDead))
                {
                    ReturnToSpawnOrIdle();
                    return;
                }
            }

            // Cannot decide while attacking or staggered
            if (currentState == EnemyAIState.Attacking || currentState == EnemyAIState.Staggered)
            {
                ApplyGravityOnly();
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

            // 1. Check Perception if not already aware
            if (currentState == EnemyAIState.Idle || currentState == EnemyAIState.Patrol)
            {
                if (CanSensePlayer(distanceToPlayer))
                {
                    // Alert! Player spotted
                    currentState = distanceToPlayer <= combatDistance ? EnemyAIState.CombatStance : EnemyAIState.Chase;
                }
                else
                {
                    ReturnToSpawnOrIdle();
                    return;
                }
            }

            // 2. Active Combat Aggro Logic
            if (currentState == EnemyAIState.Chase || currentState == EnemyAIState.CombatStance)
            {
                // Has player escaped too far?
                if (distanceToPlayer > loseAggroDistance)
                {
                    loseAggroTimer += Time.deltaTime;
                    if (loseAggroTimer >= loseAggroTime)
                    {
                        // Give up and reset
                        loseAggroTimer = 0f;
                        SetBlocking(false);
                        currentState = EnemyAIState.Idle;
                        return;
                    }
                }
                else
                {
                    loseAggroTimer = 0f;
                }

                // If in attack reach and attack is ready -> ATTACK!
                if (distanceToPlayer <= attackRange && attackCooldownTimer <= 0f)
                {
                    attackRoutine = StartCoroutine(PerformAttackRoutine());
                    return;
                }

                // If close -> Combat Stance (stalk / circle / evaluate / shield block)
                if (distanceToPlayer <= combatDistance)
                {
                    currentState = EnemyAIState.CombatStance;
                    HandleCombatStance(distanceToPlayer);
                }
                else
                {
                    // Further away -> Chase down player
                    currentState = EnemyAIState.Chase;
                    SetBlocking(false);
                    MoveTowards(targetPlayer.position, chaseSpeed, 1.0f);
                }
            }
        }

        private bool CanSensePlayer(float distance)
        {
            if (targetPlayer == null) return false;

            // Proximity (hearing footsteps close behind)
            if (distance <= hearingRange) return true;

            // Sight cone check
            if (distance <= sightRange)
            {
                Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, toPlayer);
                if (angle <= sightAngle * 0.5f)
                {
                    // Raycast line of sight check
                    Vector3 eyePos = transform.position + Vector3.up * 1.5f;
                    Vector3 playerEyePos = targetPlayer.position + Vector3.up * 1.5f;
                    if (Physics.Linecast(eyePos, playerEyePos, out RaycastHit hit))
                    {
                        if (hit.transform == targetPlayer || hit.transform.root == targetPlayer.root)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void HandleCombatStance(float distanceToPlayer)
        {
            // Face target smoothly
            FaceDirection(targetPlayer.position - transform.position);

            // Randomly test shield blocking
            if (!isBlocking && blockTimer <= 0f && Random.value < (blockChance * Time.deltaTime))
            {
                SetBlocking(true);
                blockTimer = Random.Range(blockMinDuration, blockMaxDuration);
            }

            // Strafe & maintain distance
            strafeTimer -= Time.deltaTime;
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(1.5f, 3.5f);
                strafeDirection = Random.value > 0.5f ? 1 : -1;
            }

            Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
            Vector3 strafeVector = Vector3.Cross(Vector3.up, toPlayer) * strafeDirection;

            Vector3 desiredMove = Vector3.zero;

            // Back up slightly if player is too close, or step forward if too far
            if (distanceToPlayer < attackRange * 0.8f)
            {
                desiredMove = -toPlayer * 0.5f + strafeVector * 0.8f;
            }
            else
            {
                desiredMove = toPlayer * 0.35f + strafeVector * 0.65f;
            }

            MoveDirect(desiredMove.normalized * strafeSpeed, 0.4f);
        }

        private void MoveTowards(Vector3 destination, float speed, float animSpeedFraction)
        {
            Vector3 direction = (destination - transform.position);
            direction.y = 0;

            if (direction.magnitude < 0.2f)
            {
                SetAnimSpeed(0f);
                ApplyGravityOnly();
                return;
            }

            direction.Normalize();
            FaceDirection(direction);

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.speed = speed;
                navAgent.SetDestination(destination);
                SetAnimSpeed(animSpeedFraction);
            }
            else
            {
                MoveDirect(direction * speed, animSpeedFraction);
            }
        }

        private void MoveDirect(Vector3 horizontalVelocity, float animSpeedFraction)
        {
            // Ground gravity calculation
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 finalMove = horizontalVelocity;
            finalMove.y = verticalVelocity;

            characterController.Move(finalMove * Time.deltaTime);
            SetAnimSpeed(animSpeedFraction);
        }

        private void ApplyGravityOnly()
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
            characterController.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
            SetAnimSpeed(0f);
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private void SetAnimSpeed(float speedVal)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", speedVal, 0.15f, Time.deltaTime);
            }
        }

        private void SetBlocking(bool block)
        {
            isBlocking = block;
            if (animator != null)
            {
                animator.SetBool("isBlocking", isBlocking);
            }
        }

        private void ReturnToSpawnOrIdle()
        {
            float distToSpawn = Vector3.Distance(transform.position, spawnPosition);
            if (distToSpawn > 1.0f)
            {
                currentState = EnemyAIState.Patrol;
                MoveTowards(spawnPosition, walkSpeed, 0.5f);
            }
            else
            {
                currentState = EnemyAIState.Idle;
                FaceDirection(spawnRotation * Vector3.forward);
                ApplyGravityOnly();
            }
        }

        private IEnumerator PerformAttackRoutine()
        {
            currentState = EnemyAIState.Attacking;
            SetBlocking(false);
            SetAnimSpeed(0f); // Dark Souls kuralı: Saldırı anında yürüme/koşma hızı sıfırlanır

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
                navAgent.isStopped = true;
            }

            // Face player directly before swing (Saldırı başlangıcında hedefe dön, sonra savurma boyunca sabit kal)
            if (targetPlayer != null)
            {
                Vector3 toPlayer = targetPlayer.position - transform.position;
                toPlayer.y = 0;
                if (toPlayer != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(toPlayer);
                }
            }

            if (animator != null)
            {
                animator.SetTrigger("isAttacking");
            }

            // 1. Wind-up (Savurma öncesi gerilme - ayaklar yerde sabit)
            float elapsed = 0f;
            while (elapsed < attackWindup)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 2. Active Swing (Topuz savruluyor, hasar alanı AKTİF! - ayaklar yerde sabit)
            Audio.SoulsAudioManager.GetOrCreate().PlaySwing(transform.position + transform.forward * 0.8f + Vector3.up * 1.2f);
            if (weaponCollider != null)
            {
                weaponCollider.EnableDamageCollider(attackDamage);
            }

            elapsed = 0f;
            while (elapsed < attackActive)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 3. Recovery (Savurma bitti, hasar alanı KAPALI)
            if (weaponCollider != null)
            {
                weaponCollider.DisableDamageCollider();
            }

            yield return new WaitForSeconds(attackRecovery);

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
            }

            attackCooldownTimer = attackCooldown;
            currentState = EnemyAIState.CombatStance;
            attackRoutine = null;
        }

        public void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, Transform attacker = null)
        {
            if (isDead) return;

            // Aggro attacker immediately if not already active
            if (attacker != null)
            {
                targetPlayer = attacker;
                playerStats = attacker.GetComponent<PlayerStats>();
                currentState = EnemyAIState.CombatStance;
            }

            // Shield Block Check (Eğer düşman kalkan tutuyorsa ve darbe önden geliyorsa)
            if (isBlocking)
            {
                Vector3 toAttacker = Vector3.zero;
                if (attacker != null) toAttacker = attacker.position - transform.position;
                else if (hitPoint != Vector3.zero) toAttacker = hitPoint - transform.position;
                else toAttacker = -hitDirection;
                toAttacker.y = 0;

                float dot = Vector3.Dot(transform.forward, toAttacker.normalized);
                if (dot > -0.15f)
                {
                    // Düşman kalkanıyla darbeyi engelledi!
                    Audio.SoulsAudioManager.GetOrCreate().PlayBlock(hitPoint);
                    SpawnBlockSparks(hitPoint, toAttacker.normalized);
                    CombatDebugVisualizer.SpawnContactPoint(hitPoint, toAttacker.normalized, true, "🛡️ DÜŞMAN KALKAN TEMASI");
                    return;
                }
            }

            currentHealth = Mathf.Max(0, currentHealth - damage);
            healthBarTimer = 5.0f;
            Audio.SoulsAudioManager.GetOrCreate().PlayHit(hitPoint);

            // Uçan hasar rakamı
            SpawnFloatingDamageNumber(damage, hitPoint);

            // Kırmızı darbe flaşı
            StopCoroutine(nameof(FlashRedRoutine));
            StartCoroutine(nameof(FlashRedRoutine));

            // Fiziksel geri tepme
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            recoilRoutine = StartCoroutine(HitRecoilRoutine(hitDirection));

            // Poise & Stagger check
            currentPoise -= 15f;
            if (currentPoise <= 0f)
            {
                currentPoise = poise;
                TriggerStagger();
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void TriggerStagger()
        {
            if (isDead) return;
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            if (weaponCollider != null)
            {
                weaponCollider.DisableDamageCollider();
            }
            SetBlocking(false);

            StartCoroutine(StaggerRoutine(0.85f));
        }

        private IEnumerator StaggerRoutine(float duration)
        {
            currentState = EnemyAIState.Staggered;
            yield return new WaitForSeconds(duration);
            if (!isDead)
            {
                currentState = EnemyAIState.CombatStance;
            }
        }

        private IEnumerator FlashRedRoutine()
        {
            Color flashColor = new Color(1.0f, 0.2f, 0.2f);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null && allRenderers[i].material.HasProperty("_Color"))
                {
                    allRenderers[i].material.color = flashColor;
                }
            }

            yield return new WaitForSeconds(0.12f);

            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null && i < originalColors.Count)
                {
                    allRenderers[i].material.color = originalColors[i];
                }
            }
        }

        private IEnumerator HitRecoilRoutine(Vector3 hitDirection)
        {
            float pushTimer = 0f;
            while (pushTimer < 0.15f)
            {
                pushTimer += Time.deltaTime;
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(hitDirection * (3.0f * Time.deltaTime));
                }
                yield return null;
            }
        }

        private void SpawnBlockSparks(Vector3 point, Vector3 direction)
        {
            GameObject sparkObj = new GameObject("EnemyBlockSparks");
            sparkObj.transform.position = point;
            sparkObj.transform.forward = direction;

            var ps = sparkObj.AddComponent<ParticleSystem>();
            var pMain = ps.main;
            pMain.startLifetime = 0.2f;
            pMain.startSpeed = 5f;
            pMain.startSize = 0.08f;
            pMain.startColor = new Color(0.3f, 0.8f, 1.0f);
            pMain.loop = false;
            pMain.stopAction = ParticleSystemStopAction.Destroy;

            var pEmission = ps.emission;
            pEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });
        }

        private void SpawnFloatingDamageNumber(int damage, Vector3 hitPoint)
        {
            GameObject textObj = new GameObject("EnemyDamageNumber");
            textObj.transform.position = hitPoint + Vector3.up * 0.35f + Random.insideUnitSphere * 0.15f;

            var tm = textObj.AddComponent<TextMesh>();
            tm.text = $"-{damage}";
            tm.fontSize = 28;
            tm.characterSize = 0.08f;
            tm.color = new Color(1.0f, 0.4f, 0.1f);
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;

            textObj.AddComponent<FloatingDamageTextAnimator>();
        }

        private void Die()
        {
            isDead = true;
            currentState = EnemyAIState.Dead;

            if (attackRoutine != null) StopCoroutine(attackRoutine);
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();
            SetBlocking(false);

            if (lockOnTarget != null) lockOnTarget.SetLockable(false);
            if (characterController != null) characterController.enabled = false;
            if (navAgent != null) navAgent.enabled = false;

            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddSouls(soulsReward);
            }
            Debug.Log($"<color=yellow>{enemyName} yenildi! +{soulsReward} Ruh kazanıldı.</color>");

            StartCoroutine(DeathAndRespawnRoutine());
        }

        private IEnumerator DeathAndRespawnRoutine()
        {
            // Yere devrilme efekti
            Quaternion startRot = transform.rotation;
            Quaternion fallRot = Quaternion.Euler(-80f, transform.eulerAngles.y, 0f);
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRot, fallRot, t / 0.45f);
                yield return null;
            }

            if (!autoRespawn) yield break;

            yield return new WaitForSeconds(respawnDelay);

            // Yeniden Doğuş (Respawn)
            currentHealth = maxHealth;
            currentPoise = poise;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            isDead = false;
            currentState = EnemyAIState.Idle;

            if (characterController != null) characterController.enabled = true;
            if (navAgent != null) navAgent.enabled = true;
            if (lockOnTarget != null) lockOnTarget.SetLockable(true);

            // Renkleri sıfırla
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null && i < originalColors.Count)
                {
                    allRenderers[i].material.color = originalColors[i];
                }
            }
        }

        private void OnGUI()
        {
            if (isDead || healthBarTimer <= 0f) return;

            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 worldHeadPos = transform.position + Vector3.up * 2.2f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldHeadPos);

            if (screenPos.z <= 0.5f) return;

            float barWidth = 140f;
            float barHeight = 8f;
            float posX = screenPos.x - (barWidth / 2f);
            float posY = Screen.height - screenPos.y;

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            nameStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(posX - 30, posY - 22, barWidth + 60, 20), enemyName, nameStyle);

            // 1. Arka Plan
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(posX - 1, posY - 1, barWidth + 2, barHeight + 2), Texture2D.whiteTexture);

            // 2. Gecikmeli Sarı Can Barı
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(posX, posY, barWidth * trailingYellowFraction, barHeight), Texture2D.whiteTexture);

            // 3. Kırmızı Ön Can Barı
            GUI.color = new Color(0.85f, 0.15f, 0.15f, 1.0f);
            GUI.DrawTexture(new Rect(posX, posY, barWidth * displayedHealthFraction, barHeight), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }

        private void OnDrawGizmos()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = isBlocking ? Color.cyan : new Color(1.0f, 0.45f, 0.15f, 0.65f);
                Vector3 size = new Vector3(characterController.radius * 2f, characterController.height, characterController.radius * 2f);
                Gizmos.DrawWireCube(characterController.center, size);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Görme alanı (Sarı)
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, sightRange);

            // Melee Saldırı Alanı (Kırmızı)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Combat Stance Alanı (Mavi)
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, combatDistance);

            // Görme açısı konisi
            Vector3 leftRay = Quaternion.Euler(0, -sightAngle * 0.5f, 0) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0, sightAngle * 0.5f, 0) * transform.forward;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up, leftRay * sightRange);
            Gizmos.DrawRay(transform.position + Vector3.up, rightRay * sightRange);
        }

#if UNITY_EDITOR
        [ContextMenu("⚔️ Topuz Vuruş Alanını Seç ve Düzenle (Select Weapon Hitbox)")]
        public void SelectWeaponColliderInScene()
        {
            var eq = GetComponent<EquipmentManager>();
            if (eq != null)
            {
                eq.SelectWeaponColliderInScene();
            }
        }
#endif
    }
}
