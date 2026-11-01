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
        [SerializeField] private float sprintStaminaCostPerSec = 15f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Dodge Roll Settings")]
        [SerializeField] private float rollStaminaCost = 22f;
        [SerializeField] private float rollDuration = 0.85f;
        [SerializeField] private float rollSpeed = 9.5f;
        [SerializeField] private float iFrameStart = 0.1f;
        [SerializeField] private float iFrameDuration = 0.45f;

        [Header("Backstep Settings")]
        [SerializeField] private float backstepStaminaCost = 15f;
        [SerializeField] private float backstepDuration = 0.5f;
        [SerializeField] private float backstepSpeed = 6.0f;

        [Header("Combat Actions")]
        [SerializeField] private float attackStaminaCost = 18f;
        [SerializeField] private float blockStaminaDrainPerSec = 5f;

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
        private float verticalVelocity;
        private bool isGrounded;
        private bool isRolling;
        private bool isSprinting;
        private bool isBlocking;
        private bool isAttacking;
        private bool isStaggered;

        public static PlayerLocomotion Instance { get; private set; }

        [Header("Weapon & Damage Settings")]
        [SerializeField] private int attackDamage = 35;
        [SerializeField] private float attackWindupTime = 0.20f;
        [SerializeField] private float attackActiveTime = 0.32f;
        [SerializeField] private float attackRecoveryTime = 0.33f;
        private Combat.WeaponDamageCollider weaponCollider;

        public bool IsRolling => isRolling;
        public bool IsSprinting => isSprinting;
        public bool IsBlocking => isBlocking;
        public bool IsGrounded => isGrounded;
        public bool IsStaggered => isStaggered;

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
        }

        public void SetAnimator(Animator anim)
        {
            animator = anim;
        }

        public void HandleMovement(float horizontal, float vertical, bool sprintInput)
        {
            if (playerStats.isDead || isRolling || isStaggered) return;

            CheckGrounded();

            // Calculate movement vector relative to camera
            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 inputVector = (camForward * vertical + camRight * horizontal).normalized;

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
                if (blockStaminaDrainPerSec > 0f)
                {
                    playerStats.UseStamina(blockStaminaDrainPerSec * Time.deltaTime);
                }
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
            if (CameraManager.Instance != null && CameraManager.Instance.IsLockedOn)
            {
                // Locked-on: Always face the locked enemy
                Vector3 targetDir = CameraManager.Instance.CurrentLockedTarget.transform.position - transform.position;
                targetDir.y = 0;
                if (targetDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(targetDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                // Free-look: Face direction of movement
                if (inputVector != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(inputVector);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }
        }

        public void AttemptDodgeRoll(float horizontal, float vertical)
        {
            if (playerStats.isDead || isRolling || isStaggered) return;

            // Cancel blocking and attack collider when rolling
            SetBlocking(false);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();
            isAttacking = false;

            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 inputVector = (camForward * vertical + camRight * horizontal).normalized;

            if (inputVector.magnitude > 0.1f)
            {
                if (playerStats.UseStamina(rollStaminaCost))
                {
                    StartCoroutine(PerformRollRoutine(inputVector));
                }
            }
            else
            {
                if (playerStats.UseStamina(backstepStaminaCost))
                {
                    StartCoroutine(PerformBackstepRoutine());
                }
            }
        }

        private IEnumerator PerformRollRoutine(Vector3 direction)
        {
            isRolling = true;
            rollDirection = direction;

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
        }

        public void AttemptAttack()
        {
            if (playerStats.isDead || isRolling || isAttacking || isStaggered) return;

            if (playerStats.UseStamina(attackStaminaCost))
            {
                StartCoroutine(PerformAttackRoutine());
            }
        }

        private IEnumerator PerformAttackRoutine()
        {
            isAttacking = true;

            // Ensure weapon collider reference is ready
            if (weaponCollider == null)
            {
                var eq = GetComponent<Combat.EquipmentManager>();
                if (eq != null) weaponCollider = eq.GetWeaponCollider();
                if (weaponCollider == null) weaponCollider = GetComponentInChildren<Combat.WeaponDamageCollider>();
            }

            Vector3 attackStepDir = transform.forward;

            if (animator != null)
            {
                animator.SetTrigger("isAttacking");
            }

            // 1. Wind-up Delay (Silah geriye çekiliyor)
            float elapsed = 0f;
            while (elapsed < attackWindupTime)
            {
                elapsed += Time.deltaTime;
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(attackStepDir * (1.5f * Time.deltaTime));
                }
                yield return null;
            }

            // 2. Active Damage Window (Topuz savruluyor, hasar alanı AKTİF!)
            if (weaponCollider != null)
            {
                weaponCollider.EnableDamageCollider(attackDamage);
            }

            elapsed = 0f;
            while (elapsed < attackActiveTime)
            {
                elapsed += Time.deltaTime;
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(attackStepDir * (1.0f * Time.deltaTime));
                }
                yield return null;
            }

            // 3. Recovery (Savurma bitti, hasar alanı KAPALI!)
            if (weaponCollider != null)
            {
                weaponCollider.DisableDamageCollider();
            }

            yield return new WaitForSeconds(attackRecoveryTime);
            isAttacking = false;
        }

        public void SetBlocking(bool blocking)
        {
            if (playerStats.isDead || isRolling)
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
            StartCoroutine(GuardBreakRoutine(duration));
        }

        private IEnumerator GuardBreakRoutine(float duration)
        {
            isStaggered = true;
            isAttacking = false;
            SetBlocking(false);
            if (weaponCollider != null) weaponCollider.DisableDamageCollider();

            Vector3 pushBack = -transform.forward;
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.deltaTime;
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(pushBack * (3.5f * Time.deltaTime));
                }
                yield return null;
            }

            yield return new WaitForSeconds(duration - 0.25f);
            isStaggered = false;
        }

        private void CheckGrounded()
        {
            isGrounded = characterController.isGrounded;
        }
    }
}
