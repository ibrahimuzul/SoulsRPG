using System.Collections;
using UnityEngine;
using SoulsRPG.CameraSystem;

namespace SoulsRPG.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float sprintStaminaCostPerSec = 8f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Dodge Roll Settings")]
        [SerializeField] private float rollStaminaCost = 14f;
        [SerializeField] private float rollDuration = 0.85f;
        [SerializeField] private float rollSpeed = 9.5f;
        [SerializeField] private float iFrameStart = 0.1f;
        [SerializeField] private float iFrameDuration = 0.45f;

        [Header("Backstep Settings")]
        [SerializeField] private float backstepStaminaCost = 10f;
        [SerializeField] private float backstepDuration = 0.5f;
        [SerializeField] private float backstepSpeed = 6.0f;

        [Header("Combat Actions")]
        [SerializeField] private float attackStaminaCost = 12f;

        [Header("Heavy Attack (R2)")]
        [SerializeField] private float heavyStaminaMultiplier = 1.6f;
        [SerializeField] private float heavyDamageMultiplier = 1.8f;
        [Tooltip("Hedefe verilen poise (denge) hasarı çarpanı — düşmanları daha kolay sendeletir")]
        [SerializeField] private float heavyPoiseMultiplier = 2.2f;
        [Tooltip("Gerilme (kılıç geriye çekilirken) süresi çarpanı. Animasyon da bu oranda yavaşlar, böylece ağır vuruşun 'yüklenme' hissi oluşur")]
        [SerializeField] private float heavyWindupMultiplier = 1.8f;
        [Tooltip("Savurma (hasar penceresi) süresi çarpanı. 1'in altı = savurma hafif saldırıdan daha sert/hızlı")]
        [SerializeField] private float heavySwingMultiplier = 0.9f;
        [Tooltip("Toparlanma süresi çarpanı (vuruştan sonra açık kalma)")]
        [SerializeField] private float heavyRecoveryMultiplier = 1.6f;
        [Tooltip("Savurma başında öne atılma mesafesi (metre)")]
        [SerializeField] private float heavyLungeDistance = 0.9f;
        [Tooltip("Ağır saldırı isabet edince kamera sarsıntısı şiddeti")]
        [SerializeField] private float heavyHitShake = 0.18f;

        // Animator'da "isHeavyAttack" trigger'ı varsa ayrı ağır saldırı animasyonu oynatılır
        private const string HeavyAttackTrigger = "isHeavyAttack";
        private bool hasHeavyAttackTrigger;

        [Header("Mesh & Locomotion Stabilization")]
        [Tooltip("Animasyon kaynaklı root-motion kaymalarını engelleyerek karakter modelini daima CharacterController merkezinde tutar")]
        [SerializeField] private bool stabilizeLocomotionMesh = true;

        [Header("Block Stance Tuning")]
        [Tooltip("Blok animasyonunda gövde sola açılı durduğu için, kalkanı hedefe tam hizalamak üzere görsel modeli sağa çevirme açısı")]
        [SerializeField] private float blockVisualOffsetAngle = 30f;

        [Header("Gravity & Grounding")]
        [SerializeField] private float gravity = -20f;
        [SerializeField] private LayerMask groundLayers;

        // Components
        private CharacterController characterController;
        private PlayerStats playerStats;
        private UnityEngine.Camera mainCamera;
        private Animator animator;

        // Movement State
        private Vector3 moveDirection;
        private Vector3 rollDirection;
        private Vector3 lastInputVector;
        private float verticalVelocity;
        private bool isGrounded;
        private bool isRolling;
        private bool isSprinting;
        private bool isBlocking;
        private bool isAttacking;
        private bool isStaggered;

        public static PlayerLocomotion Instance { get; private set; }

        [Header("Weapon & Damage Settings")]
        [Tooltip("Geriye çekilme süresi: Silah omuz arkasına kalkana kadar hasar kapalıdır (Yeşil)")]
        [SerializeField] private float attackWindupTime = 0.48f;
        [Tooltip("Öne savurma süresi: Silah öne doğru savrulurken hasar açıktır (Kırmızı)")]
        [SerializeField] private float attackActiveTime = 0.44f;
        [Tooltip("Savurma bittikten sonra garda dönüş süresi (Yeşil)")]
        [SerializeField] private float attackRecoveryTime = 0.22f;
        private Combat.WeaponDamageCollider weaponCollider;

        // İptal edilebilir eylem coroutine'leri
        private Coroutine attackRoutine;
        private Coroutine rollRoutine;
        private Coroutine staggerRoutine;

        public bool IsRolling => isRolling;
        public bool IsSprinting => isSprinting;
        public bool IsBlocking => isBlocking;
        public bool IsGrounded => isGrounded;
        public bool IsStaggered => isStaggered;
        public bool IsAttacking => isAttacking;
        public bool isResting = false;
        public bool IsResting => isResting;

        private UnityEngine.Camera MainCamera
        {
            get
            {
                if (mainCamera == null) mainCamera = UnityEngine.Camera.main;
                return mainCamera;
            }
        }

        /// <summary>Kameraya göre yatay giriş vektörü (kamera yoksa karakterin kendi eksenleri kullanılır).</summary>
        private Vector3 GetCameraRelativeInput(float horizontal, float vertical)
        {
            Transform reference = MainCamera != null ? MainCamera.transform : transform;
            Vector3 camForward = reference.forward;
            Vector3 camRight = reference.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();
            return (camForward * vertical + camRight * horizontal).normalized;
        }

        private void Awake()
        {
            Instance = this;
            characterController = GetComponent<CharacterController>();
            playerStats = GetComponent<PlayerStats>();
            mainCamera = UnityEngine.Camera.main;
            animator = GetComponentInChildren<Animator>();

            // Ensure EquipmentManager exists without stomping scene transforms
            var eq = GetComponent<Combat.EquipmentManager>();
            if (eq == null)
            {
                eq = gameObject.AddComponent<Combat.EquipmentManager>();
            }
            eq.FindSocketsIfMissing();
        }

        private Transform animatorTransform;
        private Transform pelvisTransform;
        private Vector3 initialPelvisRelativePos;
        private bool hasPelvisReference;

        private void Start()
        {
            var eq = GetComponent<Combat.EquipmentManager>();
            if (eq != null)
            {
                weaponCollider = eq.GetWeaponCollider();
            }
            if (weaponCollider == null)
            {
                weaponCollider = GetComponentInChildren<Combat.WeaponDamageCollider>();
            }

            InitializeMeshStabilization();
        }

        public void SetAnimator(Animator anim)
        {
            animator = anim;
            InitializeMeshStabilization();
        }

        private void InitializeMeshStabilization()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            RefreshAnimatorParameters();

            if (animator != null)
            {
                animatorTransform = animator.transform;
                pelvisTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (pelvisTransform == null)
                {
                    pelvisTransform = animator.transform.Find("root/pelvis") ??
                                     animator.transform.Find("pelvis") ??
                                     animator.transform.Find("root");
                }

                if (pelvisTransform != null)
                {
                    initialPelvisRelativePos = transform.InverseTransformPoint(pelvisTransform.position);
                    hasPelvisReference = true;
                }
            }
        }

        private void LateUpdate()
        {
            if (!stabilizeLocomotionMesh) return;

            if (!hasPelvisReference && animator != null)
            {
                InitializeMeshStabilization();
            }

            // 1. Animator child objesini daima Player merkezinde (0,0,0) tut ve blok anında kalkanı hedefe hizala
            if (animatorTransform != null && animatorTransform != transform)
            {
                animatorTransform.localPosition = Vector3.zero;

                float targetAngle = isBlocking ? blockVisualOffsetAngle : 0f;
                Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);
                animatorTransform.localRotation = Quaternion.Slerp(animatorTransform.localRotation, targetRot, 15f * Time.deltaTime);
            }

            // 2. Animasyonun Hips kemiğini ileri veya yana kaydırmasını (root motion drift) önle
            // Horizontal (X ve Z) kapsüle kilitlenir; dikey (Y) adım/sekme animasyonu serbest kalır!
            if (hasPelvisReference && pelvisTransform != null)
            {
                Vector3 relPos = transform.InverseTransformPoint(pelvisTransform.position);
                relPos.x = initialPelvisRelativePos.x;
                relPos.z = initialPelvisRelativePos.z;
                pelvisTransform.position = transform.TransformPoint(relPos);
            }
        }

        public void HandleMovement(float horizontal, float vertical, bool sprintInput)
        {
            if (playerStats.isDead || isRolling || isStaggered || isResting) return;

            CheckGrounded();

            // Dark Souls Saldırı Kuralı: Kılıç/silah sallarken yürünemez ve koşulamaz! (Attack Commitment)
            if (isAttacking)
            {
                isSprinting = false;
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
                }

                // Sadece yerçekimi uygula (ayaklar yere sağlam bassın)
                if (isGrounded && verticalVelocity < 0f)
                {
                    verticalVelocity = -2f;
                }
                else
                {
                    verticalVelocity += gravity * Time.deltaTime;
                }
                characterController.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
                return;
            }

            // Calculate movement vector relative to camera
            Vector3 inputVector = GetCameraRelativeInput(horizontal, vertical);
            lastInputVector = inputVector;

            // Handle Sprinting (Cannot sprint while blocking)
            isSprinting = sprintInput && !isBlocking && inputVector.magnitude > 0.1f && playerStats.HasStamina(5f);
            if (isSprinting)
            {
                playerStats.UseStamina(sprintStaminaCostPerSec * Time.deltaTime);
            }

            float currentSpeed = walkSpeed;
            if (isBlocking)
            {
                currentSpeed = walkSpeed * 0.7f;
            }
            else if (isSprinting) currentSpeed = sprintSpeed;
            else if (inputVector.magnitude > 0.5f) currentSpeed = runSpeed;

            // Drive Animator Speed parameter
            if (animator != null)
            {
                float targetAnimSpeed = 0f;
                if (inputVector.magnitude > 0.1f)
                {
                    targetAnimSpeed = isSprinting ? 1.0f : 0.5f;
                }
                animator.SetFloat("Speed", targetAnimSpeed, 0.1f, Time.deltaTime);
            }

            // Apply Horizontal Movement
            Vector3 finalMove = inputVector * currentSpeed;

            // Handle Gravity
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
            finalMove.y = verticalVelocity;

            characterController.Move(finalMove * Time.deltaTime);

            // Handle Rotation
            HandleRotation(inputVector);
        }

        private void HandleRotation(Vector3 inputVector)
        {
            var lockTarget = CameraManager.Instance != null ? CameraManager.Instance.CurrentLockedTarget : null;
            if (lockTarget != null)
            {
                // Locked-on: Rakibe tam cepheden odaklan
                Vector3 targetDir = lockTarget.transform.position - transform.position;
                targetDir.y = 0;
                if (targetDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                // Free-look:
                if (isBlocking)
                {
                    // Serbest bakışta blok yapılırken kameranın baktığı yöne tam cepheden dön
                    Vector3 camFwd = MainCamera != null ? MainCamera.transform.forward : transform.forward;
                    camFwd.y = 0;
                    if (camFwd != Vector3.zero)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(camFwd);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                    }
                }
                else if (inputVector != Vector3.zero)
                {
                    // Normal serbest hareket: Hareket yönüne dön
                    Quaternion targetRot = Quaternion.LookRotation(inputVector);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }
        }

        public void AttemptDodgeRoll(float horizontal, float vertical)
        {
            if (playerStats.isDead || isRolling || isStaggered || isAttacking || isResting) return;

            Vector3 inputVector = GetCameraRelativeInput(horizontal, vertical);
            bool isRoll = inputVector.magnitude > 0.1f;

            // Stamina yoksa (0) yuvarlanma/backstep yapılamaz
            if (!playerStats.UseStamina(isRoll ? rollStaminaCost : backstepStaminaCost)) return;

            // Cancel blocking when rolling
            SetBlocking(false);
            lastInputVector = inputVector;

            rollRoutine = StartCoroutine(isRoll ? PerformRollRoutine(inputVector) : PerformBackstepRoutine());
        }

        private IEnumerator PerformRollRoutine(Vector3 direction)
        {
            isRolling = true;
            rollDirection = direction;
            Audio.SoulsAudioManager.GetOrCreate().PlayRoll(transform.position);

            transform.rotation = Quaternion.LookRotation(rollDirection);

            if (animator != null)
            {
                animator.SetTrigger("isRolling");
            }

            float timer = 0f;
            float iFrameTimer = 0f;

            while (timer < rollDuration)
            {
                timer += Time.deltaTime;
                iFrameTimer += Time.deltaTime;

                if (iFrameTimer >= iFrameStart && iFrameTimer <= (iFrameStart + iFrameDuration))
                {
                    playerStats.isInvulnerable = true;
                }
                else
                {
                    playerStats.isInvulnerable = false;
                }

                float speedMultiplier = Mathf.Lerp(rollSpeed, 2.0f, timer / rollDuration);

                Vector3 rollVelocity = rollDirection * speedMultiplier;
                rollVelocity.y = verticalVelocity;
                characterController.Move(rollVelocity * Time.deltaTime);

                if (animator != null)
                {
                    animator.transform.localPosition = Vector3.zero;
                }

                yield return null;
            }

            if (animator != null)
            {
                animator.transform.localPosition = Vector3.zero;
            }

            playerStats.isInvulnerable = false;
            isRolling = false;
            rollRoutine = null;
        }

        private IEnumerator PerformBackstepRoutine()
        {
            isRolling = true;
            Vector3 backDir = -transform.forward;

            if (animator != null)
            {
                animator.SetTrigger("isRolling");
            }

            float timer = 0f;
            while (timer < backstepDuration)
            {
                timer += Time.deltaTime;
                float speedMultiplier = Mathf.Lerp(backstepSpeed, 1.0f, timer / backstepDuration);

                Vector3 stepVel = backDir * speedMultiplier;
                stepVel.y = verticalVelocity;
                characterController.Move(stepVel * Time.deltaTime);

                yield return null;
            }

            isRolling = false;
            rollRoutine = null;
        }

        /// <param name="heavy">true: R2 ağır saldırı (daha yavaş, daha fazla hasar ve stamina)</param>
        public void AttemptAttack(bool heavy = false)
        {
            if (playerStats.isDead || isRolling || isAttacking || isStaggered || isResting) return;

            float cost = heavy ? attackStaminaCost * heavyStaminaMultiplier : attackStaminaCost;
            if (playerStats.UseStamina(cost))
            {
                // Saldırıya başlarken: Serbest bakışta hareket yönü varsa o yöne dönerek saldırıya başla
                if (CameraManager.Instance == null || !CameraManager.Instance.IsLockedOn)
                {
                    if (lastInputVector.sqrMagnitude > 0.05f)
                    {
                        transform.rotation = Quaternion.LookRotation(lastInputVector);
                    }
                }
                attackRoutine = StartCoroutine(PerformAttackRoutine(heavy));
            }
        }

        /// <summary>Devam eden saldırıyı keser ve hasar alanını kapatır (stagger, ölüm vb.).</summary>
        private void CancelAttack()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();
            isAttacking = false;
            SetAnimatorSpeed(1f);
        }

        private void SetAnimatorSpeed(float value)
        {
            if (animator != null) animator.speed = value;
        }

        private void RefreshAnimatorParameters()
        {
            hasHeavyAttackTrigger = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == HeavyAttackTrigger)
                {
                    hasHeavyAttackTrigger = true;
                    break;
                }
            }
        }

        /// <summary>Verilen süre boyunca bekler (her fazın sonunda saldırı iptal edildiyse coroutine zaten durur).</summary>
        private static IEnumerator WaitPhase(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void CancelRoll()
        {
            if (rollRoutine != null)
            {
                StopCoroutine(rollRoutine);
                rollRoutine = null;
            }
            isRolling = false;
            if (playerStats != null) playerStats.isInvulnerable = false;
            if (animator != null) animator.transform.localPosition = Vector3.zero;
        }

        private IEnumerator PerformAttackRoutine(bool heavy)
        {
            isAttacking = true;
            isSprinting = false;

            // Beceri saldırı hızını artırır. Ağır saldırıda her fazın kendi çarpanı var.
            // Animator hızı da her fazda aynı oranda değiştirilir; böylece kılıcın ekrandaki hareketi
            // ile hasar penceresi senkron kalır (animasyon, hafif saldırı süreleriyle hizalı varsayılır).
            float speed = playerStats != null ? playerStats.AttackSpeedMultiplier : 1f;
            float windupMul = (heavy ? heavyWindupMultiplier : 1f) / speed;
            float swingMul = (heavy ? heavySwingMultiplier : 1f) / speed;
            float recoveryMul = (heavy ? heavyRecoveryMultiplier : 1f) / speed;

            int baseDamage = playerStats != null ? playerStats.AttackDamage : PlayerStats.BaseAttackDamage;
            int damage = heavy ? Mathf.RoundToInt(baseDamage * heavyDamageMultiplier) : baseDamage;
            float poiseMul = heavy ? heavyPoiseMultiplier : 1f;
            float hitShake = heavy ? heavyHitShake : 0f;

            // Ayrı ağır saldırı klibi varsa onu kullan ve zamanlamayı onun kendi hızına bırak
            bool useHeavyClip = heavy && hasHeavyAttackTrigger;

            // Ensure weapon collider reference is ready
            if (weaponCollider == null)
            {
                var eq = GetComponent<Combat.EquipmentManager>();
                if (eq != null) weaponCollider = eq.GetWeaponCollider();
                if (weaponCollider == null) weaponCollider = GetComponentInChildren<Combat.WeaponDamageCollider>();
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetTrigger(useHeavyClip ? HeavyAttackTrigger : "isAttacking");
            }

            // 1. Wind-up (Silah geriye çekiliyor - ayaklar yerde sabit). Ağır saldırıda animasyon yavaşlar.
            if (!useHeavyClip) SetAnimatorSpeed(1f / windupMul);
            yield return WaitPhase(attackWindupTime * windupMul);

            // 2. Active Damage Window (Silah savruluyor, hasar alanı AKTİF!)
            if (!useHeavyClip) SetAnimatorSpeed(1f / swingMul);
            Vector3 swingSoundPos = transform.position + transform.forward * 0.8f + Vector3.up * 1.2f;
            if (heavy) Audio.SoulsAudioManager.GetOrCreate().PlayHeavySwing(swingSoundPos);
            else Audio.SoulsAudioManager.GetOrCreate().PlaySwing(swingSoundPos);

            if (weaponCollider != null)
            {
                weaponCollider.EnableDamageCollider(damage, poiseMul, hitShake);
            }

            float activeDuration = attackActiveTime * swingMul;
            if (heavy && heavyLungeDistance > 0f)
            {
                // Savurmanın ilk kısmında öne atıl (ağırlık hissi + menzil)
                float lungeTime = Mathf.Min(0.18f, activeDuration);
                float lungeDistance = GetLungeDistance(heavyLungeDistance);
                float lungeSpeed = lungeDistance / Mathf.Max(0.01f, lungeTime);
                float t = 0f;
                while (t < lungeTime)
                {
                    t += Time.deltaTime;
                    if (characterController != null && characterController.enabled)
                    {
                        characterController.Move(transform.forward * (lungeSpeed * Time.deltaTime));
                    }
                    yield return null;
                }
                yield return WaitPhase(activeDuration - lungeTime);
            }
            else
            {
                yield return WaitPhase(activeDuration);
            }

            // 3. Recovery (Savurma bitti, hasar alanı KAPALI!) — ağır saldırıda daha uzun açık kalırsın
            if (weaponCollider != null)
            {
                weaponCollider.DisableDamageCollider();
            }

            if (!useHeavyClip) SetAnimatorSpeed(1f / recoveryMul);
            yield return WaitPhase(attackRecoveryTime * recoveryMul);

            SetAnimatorSpeed(1f);
            isAttacking = false;
            attackRoutine = null;
        }

        /// <summary>Kilitli hedef çok yakındaysa içine girmemek için atılma mesafesini kısaltır.</summary>
        private float GetLungeDistance(float desired)
        {
            var lockTarget = CameraManager.Instance != null ? CameraManager.Instance.CurrentLockedTarget : null;
            if (lockTarget == null) return desired;

            Vector3 toTarget = lockTarget.transform.position - transform.position;
            toTarget.y = 0f;
            const float minGap = 1.1f;
            return Mathf.Clamp(toTarget.magnitude - minGap, 0f, desired);
        }

        public void SetBlocking(bool blocking)
        {
            if (playerStats.isDead || isRolling || isStaggered || isAttacking)
            {
                isBlocking = false;
                if (animator != null) animator.SetBool("isBlocking", false);
                return;
            }

            isBlocking = blocking;
            if (animator != null)
            {
                animator.SetBool("isBlocking", isBlocking);
            }
        }

        public void SetResting(bool resting)
        {
            isResting = resting;
            if (resting)
            {
                isSprinting = false;
                SetBlocking(false);
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f);
                }
            }
        }

        public void TriggerBlockRecoil()
        {
            if (isRolling || playerStats.isDead) return;
            StartCoroutine(BlockRecoilRoutine());
        }

        private IEnumerator BlockRecoilRoutine()
        {
            Vector3 recoilDir = -transform.forward;
            float elapsed = 0f;
            while (elapsed < 0.12f)
            {
                elapsed += Time.deltaTime;
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(recoilDir * (2.0f * Time.deltaTime));
                }
                yield return null;
            }
        }

        public void TriggerGuardBreak(float duration = 1.3f)
        {
            if (playerStats.isDead || isRolling) return;
            StartStagger(duration, -transform.forward, 3.5f);
        }

        /// <summary>Poise kırılınca oyuncu sendeler: saldırı iptal olur, kısa süre kontrol kaybedilir.</summary>
        public void TriggerHitStagger(float duration, Vector3 pushDirection)
        {
            if (playerStats.isDead || isRolling) return;
            pushDirection.y = 0f;
            if (pushDirection.sqrMagnitude < 0.001f) pushDirection = -transform.forward;
            StartStagger(duration, pushDirection.normalized, 2.5f);
        }

        private void StartStagger(float duration, Vector3 pushDirection, float pushSpeed)
        {
            // Saldırı coroutine'i de durdurulur; aksi halde hasar alanı stagger sırasında açılırdı
            CancelAttack();
            SetBlockingForced(false);

            if (staggerRoutine != null) StopCoroutine(staggerRoutine);
            staggerRoutine = StartCoroutine(StaggerRoutine(duration, pushDirection, pushSpeed));
        }

        private IEnumerator StaggerRoutine(float duration, Vector3 pushDirection, float pushSpeed)
        {
            isStaggered = true;
            isSprinting = false;
            if (animator != null) animator.SetFloat("Speed", 0f);

            float pushTime = Mathf.Min(0.25f, duration);
            float elapsed = 0f;
            while (elapsed < pushTime)
            {
                elapsed += Time.deltaTime;
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(pushDirection * (pushSpeed * Time.deltaTime));
                }
                yield return null;
            }

            if (duration > pushTime)
            {
                yield return new WaitForSeconds(duration - pushTime);
            }
            isStaggered = false;
            staggerRoutine = null;
        }

        /// <summary>Durum kontrollerine takılmadan bloğu kapatır.</summary>
        private void SetBlockingForced(bool blocking)
        {
            isBlocking = blocking;
            if (animator != null) animator.SetBool("isBlocking", blocking);
        }

        // ==========================================
        // ÖLÜM & YENİDEN DOĞMA
        // ==========================================

        /// <summary>PlayerStats ölümü bildirince tüm eylemleri durdurur.</summary>
        public void OnPlayerDied()
        {
            CancelAttack();
            CancelRoll();
            if (staggerRoutine != null)
            {
                StopCoroutine(staggerRoutine);
                staggerRoutine = null;
            }
            isStaggered = false;
            isSprinting = false;
            SetBlockingForced(false);
            if (animator != null) animator.SetFloat("Speed", 0f);
        }

        /// <summary>Tüm durumu sıfırlar (yeniden doğmada).</summary>
        public void ResetState()
        {
            OnPlayerDied();
            isResting = false;
            verticalVelocity = -2f;
            lastInputVector = Vector3.zero;
        }

        /// <summary>CharacterController'ı geçici kapatıp oyuncuyu verilen noktaya ışınlar.</summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (characterController != null) characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (characterController != null) characterController.enabled = true;
            verticalVelocity = -2f;
        }

        private void CheckGrounded()
        {
            isGrounded = characterController.isGrounded;
        }

        private void OnDrawGizmos()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = isBlocking ? Color.cyan : new Color(0.2f, 0.6f, 1.0f, 0.65f);
                Vector3 size = new Vector3(characterController.radius * 2f, characterController.height, characterController.radius * 2f);
                Gizmos.DrawWireCube(characterController.center, size);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("⚔️ Topuz Vuruş Alanını Seç ve Düzenle (Select Weapon Hitbox)")]
        public void SelectWeaponColliderInScene()
        {
            var eq = GetComponent<Combat.EquipmentManager>();
            if (eq != null)
            {
                eq.SelectWeaponColliderInScene();
            }
        }
#endif
    }
}
