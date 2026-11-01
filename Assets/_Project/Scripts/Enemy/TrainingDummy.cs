using UnityEngine;
using SoulsRPG.CameraSystem;

namespace SoulsRPG.Enemy
{
    [RequireComponent(typeof(LockOnTarget))]
    public class TrainingDummy : MonoBehaviour
    {
        [Header("Dummy Stats")]
        [SerializeField] private int maxHealth = 150;
        private int currentHealth;

        private Renderer meshRenderer;
        private Color originalColor;

        private void Awake()
        {
            currentHealth = maxHealth;
            meshRenderer = GetComponentInChildren<Renderer>();
            if (meshRenderer != null)
            {
                originalColor = meshRenderer.material.color;
            }

            // Auto-align equipment
            var eq = GetComponent<Combat.EquipmentManager>();
            if (eq == null)
            {
                eq = gameObject.AddComponent<Combat.EquipmentManager>();
            }
            eq.FindSocketsIfMissing();
            eq.UpdateTransforms();
        }

        public void TakeDamage(int damage)
        {
            currentHealth -= damage;
            StartCoroutine(FlashRedRoutine());

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private System.Collections.IEnumerator FlashRedRoutine()
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.red;
                yield return new WaitForSeconds(0.12f);
                meshRenderer.material.color = originalColor;
            }
        }

        private void Die()
        {
            GetComponent<LockOnTarget>().SetLockable(false);
            Destroy(gameObject, 0.5f);
        }
    }
}
