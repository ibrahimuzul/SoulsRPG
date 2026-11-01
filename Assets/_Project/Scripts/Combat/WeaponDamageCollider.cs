using System.Collections.Generic;
using UnityEngine;
using SoulsRPG.Enemy;

namespace SoulsRPG.Combat
{
    [ExecuteAlways]
    public class WeaponDamageCollider : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int currentDamage = 35;
        [SerializeField] private float poiseDamage = 20f;
        public float PoiseDamage => poiseDamage;

        [Header("Collider Setup (Düzenlenebilir Vuruş Alanı)")]
        [SerializeField] private BoxCollider hitCollider;
        [SerializeField] private Vector3 colliderCenter = new Vector3(0f, 0.45f, 0f);
        [SerializeField] private Vector3 colliderSize = new Vector3(0.35f, 0.90f, 0.35f);

        public Vector3 ColliderCenter => colliderCenter;
        public Vector3 ColliderSize => colliderSize;

        public void SetColliderBounds(Vector3 center, Vector3 size)
        {
            colliderCenter = center;
            colliderSize = size;
            ApplyBoundsToCollider();
        }

        private void OnValidate()
        {
            ApplyBoundsToCollider();
        }

        public void ApplyBoundsToCollider()
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<BoxCollider>();
            }
            if (hitCollider != null)
            {
                hitCollider.center = colliderCenter;
                hitCollider.size = colliderSize;
            }
        }

        [Header("Effects")]
        [SerializeField] private bool spawnImpactSparks = true;

        private Transform ownerRoot;
        private bool isDamageActive = false;
        private readonly List<Collider> alreadyHitColliders = new List<Collider>();
        private readonly HashSet<IDamageable> alreadyHitDamageables = new HashSet<IDamageable>();

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
            alreadyHitDamageables.Clear();
            isDamageActive = true;
            if (hitCollider != null)
            {
                hitCollider.enabled = true;
            }
        }

        public void DisableDamageCollider()
        {
            isDamageActive = false;
            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }
            alreadyHitColliders.Clear();
            alreadyHitDamageables.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isDamageActive) return;

            // Kendimize veya sahibimize vurmayalım
            if (ownerRoot != null && (other.transform.IsChildOf(ownerRoot) || other.transform == ownerRoot))
            {
                return;
            }

            // Tek bir savurmada aynı collider'a birden fazla kez vurmayı engelle
            if (alreadyHitColliders.Contains(other))
            {
                return;
            }

            // Hasar alabilir bir hedef mi? (IDamageable: TrainingDummy, EnemyController, PlayerStats)
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                // Tek bir savurmada aynı karaktere birden fazla kez vurmayı engelle
                if (alreadyHitDamageables.Contains(damageable))
                {
                    return;
                }
                alreadyHitDamageables.Add(damageable);
                alreadyHitColliders.Add(other);

                Transform attacker = ownerRoot != null ? ownerRoot : transform;
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 attackerPos = attacker.position;
                Vector3 hitDirection = (other.transform.position - attackerPos).normalized;
                hitDirection.y = 0; // Yatay darbe yönü

                damageable.TakeDamage(currentDamage, hitPoint, hitDirection, attacker);

                // Temas Noktası Göstergesi (Hem Oyuncuda Hem Düşmanda Görünür)
                string attackerLabel = attacker.name;
                CombatDebugVisualizer.SpawnContactPoint(hitPoint, hitDirection, false, $"⚔️ {attackerLabel} TEMASI");

                if (spawnImpactSparks)
                {
                    SpawnImpactEffect(hitPoint, hitDirection);
                }
            }
        }

        private void Update()
        {
            if (isDamageActive)
            {
                // Hasar anında (Savurma penceresi) Game ve Scene ekranında parlak kırmızı vuruş kutusu çiz
                Vector3 worldCenter = transform.TransformPoint(colliderCenter);
                CombatDebugVisualizer.DrawDebugBox(worldCenter, colliderSize, transform.rotation, Color.red);
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

        [ContextMenu("🎯 Preset: Sadece Topuz Başı (Compact)")]
        public void PresetHeadOnly()
        {
            SetColliderBounds(new Vector3(0f, 0.65f, 0f), new Vector3(0.35f, 0.45f, 0.35f));
        }

        [ContextMenu("⚔️ Preset: Dengeli Vuruş Alanı (Balanced)")]
        public void PresetBalanced()
        {
            SetColliderBounds(new Vector3(0f, 0.45f, 0f), new Vector3(0.35f, 0.90f, 0.35f));
        }

        [ContextMenu("🛡️ Preset: Genişletilmiş / Kolay Temas (Extended)")]
        public void PresetExtended()
        {
            SetColliderBounds(new Vector3(0f, 0.45f, 0f), new Vector3(0.50f, 1.05f, 0.50f));
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (isDamageActive)
            {
                Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);
                Gizmos.DrawCube(colliderCenter, colliderSize);
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(colliderCenter, colliderSize);
            }
            else
            {
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
                Gizmos.DrawWireCube(colliderCenter, colliderSize);
            }
        }
    }
}
