using UnityEngine;

namespace SoulsRPG.CameraSystem
{
    public class LockOnTarget : MonoBehaviour
    {
        [Header("Targeting Settings")]
        [Tooltip("Transform point where camera and lock-on icon should aim (e.g. chest/head)")]
        [SerializeField] private Transform lockOnPivot;

        [SerializeField] private bool canBeLocked = true;

        public Transform LockOnPivot => lockOnPivot != null ? lockOnPivot : transform;
        public bool CanBeLocked => canBeLocked;

        private void OnEnable()
        {
            CameraManager.RegisterLockTarget(this);
        }

        private void OnDisable()
        {
            CameraManager.UnregisterLockTarget(this);
        }

        public void SetLockable(bool value)
        {
            canBeLocked = value;
        }
    }
}
