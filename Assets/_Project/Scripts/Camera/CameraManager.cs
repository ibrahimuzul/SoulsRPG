using System.Collections.Generic;
using UnityEngine;

namespace SoulsRPG.CameraSystem
{
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [Header("Target Follow")]
        [SerializeField] private Transform target; // The Player
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Orbit Speeds & Limits")]
        [SerializeField] private float mouseSensitivityX = 150f;
        [SerializeField] private float mouseSensitivityY = 120f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Distance & Collision")]
        [SerializeField] private float defaultDistance = 4.0f;
        [SerializeField] private float minDistance = 1.0f;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionLayers;

        [Header("Lock-On Settings")]
        [SerializeField] private float maxLockDistance = 22f;
        [SerializeField] private float lockBreakDistance = 28f;
        [SerializeField] private LayerMask obstructionLayers;
        [SerializeField] private float lockOnRotationSpeed = 8f;

        // Current Camera Angles
        private float yaw;
        private float pitch;
        private float currentDistance;

        // Lock-On State
        private LockOnTarget currentLockedTarget;
        private static readonly List<LockOnTarget> allLockTargets = new List<LockOnTarget>();

        // Camera Shake State
        private Vector3 shakeOffset = Vector3.zero;
        private float shakeTimer = 0f;
        private float shakeTotalDuration = 0.45f;
        private float shakeMagnitude = 0f;

        public void TriggerShake(float magnitude = 0.35f, float duration = 0.45f)
        {
            shakeMagnitude = magnitude;
            shakeTotalDuration = Mathf.Max(0.05f, duration);
            shakeTimer = shakeTotalDuration;
        }

        public bool IsLockedOn => currentLockedTarget != null;
        public LockOnTarget CurrentLockedTarget => currentLockedTarget;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            currentDistance = defaultDistance;

            if (target != null)
            {
                yaw = target.eulerAngles.y;
                pitch = 15f;
            }
        }

        public static void RegisterLockTarget(LockOnTarget target)
        {
            if (!allLockTargets.Contains(target))
                allLockTargets.Add(target);
        }

        public static void UnregisterLockTarget(LockOnTarget target)
        {
            allLockTargets.Remove(target);
        }

        public void HandleCameraInput(float inputX, float inputY)
        {
            if (IsLockedOn)
            {
                // When locked on, mouse look can be disabled or allow slight tilt
                return;
            }

            yaw += inputX * mouseSensitivityX * Time.deltaTime;
            pitch -= inputY * mouseSensitivityY * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateLockOn();
            UpdateCameraPositionAndRotation();
        }

        private void UpdateLockOn()
        {
            if (currentLockedTarget == null) return;

            // Check if target disabled or dead
            if (!currentLockedTarget.gameObject.activeInHierarchy || !currentLockedTarget.CanBeLocked)
            {
                ClearLockOn();
                return;
            }

            // Check distance
            float dist = Vector3.Distance(target.position, currentLockedTarget.transform.position);
            if (dist > lockBreakDistance)
            {
                ClearLockOn();
                return;
            }

            // Check line of sight
            Vector3 origin = target.position + targetOffset;
            Vector3 dir = (currentLockedTarget.LockOnPivot.position - origin).normalized;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, obstructionLayers))
            {
                if (hit.transform != currentLockedTarget.transform && !hit.transform.IsChildOf(currentLockedTarget.transform))
                {
                    // Wall between player and target
                    ClearLockOn();
                    return;
                }
            }
        }

        private void UpdateCameraPositionAndRotation()
        {
            Vector3 targetFocusPos = target.position + targetOffset;

            Quaternion targetRotation;

            if (IsLockedOn)
            {
                // Smoothly look at the midpoint between target and player
                Vector3 lookDir = (currentLockedTarget.LockOnPivot.position - targetFocusPos).normalized;
                lookDir.y = 0; // Keep yaw horizontal
                if (lookDir != Vector3.zero)
                {
                    Quaternion targetYaw = Quaternion.LookRotation(lookDir);
                    yaw = Mathf.LerpAngle(yaw, targetYaw.eulerAngles.y, lockOnRotationSpeed * Time.deltaTime);
                }
                pitch = Mathf.Lerp(pitch, 18f, lockOnRotationSpeed * Time.deltaTime);
                targetRotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else
            {
                targetRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Wall Collision Check (SphereCast)
            float targetDist = defaultDistance;
            Vector3 cameraDirection = -(targetRotation * Vector3.forward);

            if (Physics.SphereCast(targetFocusPos, collisionRadius, cameraDirection, out RaycastHit hit, defaultDistance, collisionLayers))
            {
                targetDist = Mathf.Clamp(hit.distance - collisionRadius, minDistance, defaultDistance);
            }

            currentDistance = Mathf.Lerp(currentDistance, targetDist, Time.deltaTime * 20f);

            // Apply camera shake if active
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.deltaTime;
                float damp = Mathf.Clamp01(shakeTimer / shakeTotalDuration);
                shakeOffset = Random.insideUnitSphere * (shakeMagnitude * damp);
            }
            else
            {
                shakeOffset = Vector3.zero;
            }

            Vector3 finalCamPos = targetFocusPos + cameraDirection * currentDistance + shakeOffset;
            transform.position = finalCamPos;
            transform.LookAt(targetFocusPos + (IsLockedOn ? Vector3.up * 0.25f : Vector3.zero));
        }

        public void ToggleLockOn()
        {
            if (IsLockedOn)
            {
                ClearLockOn();
            }
            else
            {
                FindBestLockTarget();
            }
        }

        private void FindBestLockTarget()
        {
            LockOnTarget bestTarget = null;
            float shortestDistance = maxLockDistance;
            Vector3 camForward = transform.forward;
            Vector3 targetFocusPos = target.position + targetOffset;

            foreach (var candidate in allLockTargets)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.CanBeLocked)
                    continue;

                Vector3 toCandidate = candidate.LockOnPivot.position - targetFocusPos;
                float dist = toCandidate.magnitude;
                if (dist > maxLockDistance) continue;

                // View angle check
                float angle = Vector3.Angle(camForward, toCandidate.normalized);
                if (angle > 65f) continue; // Must be somewhat in front of camera

                // Line of sight check
                if (Physics.Raycast(targetFocusPos, toCandidate.normalized, out RaycastHit hit, dist, obstructionLayers))
                {
                    if (hit.transform != candidate.transform && !hit.transform.IsChildOf(candidate.transform))
                    {
                        continue; // Obstructed by wall
                    }
                }

                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    bestTarget = candidate;
                }
            }

            if (bestTarget != null)
            {
                currentLockedTarget = bestTarget;
            }
        }

        public void ClearLockOn()
        {
            currentLockedTarget = null;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
