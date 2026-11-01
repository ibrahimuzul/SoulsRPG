using System.Collections.Generic;
using UnityEngine;
using SoulsRPG.Enemy;

namespace SoulsRPG.Combat
{
    public class WeaponDamageCollider : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int currentDamage = 35;
        [SerializeField] private float poiseDamage = 20f;

        [Header("Collider Setup")]
        [SerializeField] private BoxCollider hitCollider;
        [SerializeField] private Vector3 colliderCenter = new Vector3(0f, 0.45f, 0f);
        [SerializeField] private Vector3 colliderSize = new Vector3(0.35f, 0.90f, 0.35f);

        [Header("Effects")]
        [SerializeField] private bool spawnImpactSparks = true;

        private Transform ownerRoot;
        private readonly List<Collider> alreadyHitColliders = new List<Collider>();

        private void Awake()
        {
            EnsureComponents();
            DisableDamageCollider();
        }

        public void SetOwner(Transform root)
        {
            ownerRoot = root;
        }

        private void EnsureComponents()
        {
            if (ownerRoot == null)
            {
                ownerRoot = transform.root;
            }

            if (hitCollider == null)
            {
                hitCollider = GetComponent<BoxCollider>();
                if (hitCollider == null)
                {
                    hitCollider = gameObject.AddComponent<BoxCollider>();
                }
            }

            hitCollider.isTrigger = true;
            hitCollider.center = colliderCenter;
            hitCollider.size = colliderSize;
            hitCollider.enabled = false;

            // Trigger algılaması için Kinematic Rigidbody
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void EnableDamageCollider(int damage)
        {
            currentDamage = damage;
            alreadyHitColliders.Clear();
            if (hitCollider != null)
            {
                hitCollider.enabled = true;
            }
        }

        public void DisableDamageCollider()
        {
            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }
            alreadyHitColliders.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Kendimize veya sahibimize vurmayalım
            if (ownerRoot != null && (other.transform.root == ownerRoot || other.transform == ownerRoot))
            {
                return;
            }

            // Tek bir savurmada aynı düşmana birden fazla kez vurmayı engelle
            if (alreadyHitColliders.Contains(other))
            {
                return;
            }

            // Düşmanı bul (Training Dummy veya gelecekteki düşmanlar)
            var dummy = other.GetComponentInParent<TrainingDummy>();
            if (dummy != null)
            {
                alreadyHitColliders.Add(other);

                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitDirection = (dummy.transform.position - ownerRoot.position).normalized;
                hitDirection.y = 0; // Yatay itme

                dummy.TakeDamage(currentDamage, hitPoint, hitDirection);

                if (spawnImpactSparks)
                {
                    SpawnImpactEffect(hitPoint, hitDirection);
                }
            }
        }

        private void SpawnImpactEffect(Vector3 point, Vector3 direction)
        {
            // Basit ve etkili kıvılcım görsel efekti oluştur
            GameObject sparkObj = new GameObject("HitSparks");
            sparkObj.transform.position = point;
            sparkObj.transform.forward = -direction;

            var ps = sparkObj.AddComponent<ParticleSystem>();
            var pMain = ps.main;
            pMain.startLifetime = 0.25f;
            pMain.startSpeed = 6f;
            pMain.startSize = 0.08f;
            pMain.startColor = new Color(1.0f, 0.75f, 0.2f); // Altın sarısı kıvılcım
            pMain.loop = false;
            pMain.stopAction = ParticleSystemStopAction.Destroy;

            var pEmission = ps.emission;
            pEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

            var pShape = ps.shape;
            pShape.shapeType = ParticleSystemShapeType.Cone;
            pShape.angle = 45f;
            pShape.radius = 0.1f;

            var pRenderer = sparkObj.GetComponent<ParticleSystemRenderer>();
            pRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            Destroy(sparkObj, 1.0f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = hitCollider != null && hitCollider.enabled ? Color.red : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(colliderCenter, colliderSize);
        }
    }
}
