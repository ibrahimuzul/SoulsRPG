using System.Collections.Generic;
using UnityEngine;
using SoulsRPG.Player;
using SoulsRPG.Combat;

namespace SoulsRPG.Boss
{
    /// <summary>
    /// Boss zemin darbesiyle (Ground Slam) tetiklenen, dışa doğru genişleyen alan hasarı (AOE Shockwave).
    /// Tekil hedef koruması (Single-hit guarantee) sayesinde bir karaktere yalnızca 1 kez vurabilir.
    /// Oyuncu takla atarak (i-frame) tamamen kaçabilir veya kalkanıyla cepheden bloklayabilir.
    /// </summary>
    public class BossShockwave : MonoBehaviour
    {
        [Header("Shockwave Settings")]
        [SerializeField] private float maxRadius = 6.5f;
        [SerializeField] private float expansionSpeed = 11.0f;
        [SerializeField] private int damage = 22; // Dengeli Dark Souls alan hasarı
        [SerializeField] private float staminaDamage = 35f; // Kalkanla bloklanırsa harcanacak dayanıklılık

        private float currentRadius = 0.5f;
        private Transform bossSource;
        private readonly HashSet<IDamageable> hitEntities = new HashSet<IDamageable>();
        private LineRenderer ringRenderer;
        private const int RingSegments = 48;
        private float lifeTimer = 0f;
        private const float MaxLife = 0.85f;
        private Material ringMat;

        public void Initialize(Transform boss, int slamDamage, float radius = 6.5f)
        {
            bossSource = boss;
            damage = slamDamage;
            maxRadius = radius;
            currentRadius = 0.5f;
            transform.position = new Vector3(boss.position.x, 0.05f, boss.position.z);

            SetupVisualRing();
        }

        private void SetupVisualRing()
        {
            ringRenderer = gameObject.AddComponent<LineRenderer>();
            ringRenderer.useWorldSpace = true;
            ringRenderer.loop = true;
            ringRenderer.positionCount = RingSegments;
            ringRenderer.startWidth = 0.45f;
            ringRenderer.endWidth = 0.45f;

            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Standard");
            ringMat = new Material(shader);
            ringMat.color = new Color(1.0f, 0.35f, 0.05f, 0.9f);
            if (ringMat.HasProperty("_EmissionColor"))
            {
                ringMat.EnableKeyword("_EMISSION");
                ringMat.SetColor("_EmissionColor", new Color(1.0f, 0.3f, 0.05f) * 2.0f);
            }
            ringRenderer.material = ringMat;
        }

        private void Update()
        {
            lifeTimer += Time.deltaTime;
            currentRadius += expansionSpeed * Time.deltaTime;

            UpdateRingVisual();
            CheckDamageHit();

            if (currentRadius >= maxRadius || lifeTimer >= MaxLife)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateRingVisual()
        {
            if (ringRenderer == null) return;

            Vector3 center = transform.position;
            float angleStep = 360f / RingSegments;

            float alpha = Mathf.Clamp01(1.0f - (lifeTimer / MaxLife));
            Color c = new Color(1.0f, 0.35f, 0.05f, alpha);
            ringRenderer.startColor = c;
            ringRenderer.endColor = c;
            ringRenderer.startWidth = Mathf.Lerp(0.5f, 0.08f, lifeTimer / MaxLife);
            ringRenderer.endWidth = ringRenderer.startWidth;

            for (int i = 0; i < RingSegments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * angleStep);
                float x = center.x + Mathf.Cos(rad) * currentRadius;
                float z = center.z + Mathf.Sin(rad) * currentRadius;
                ringRenderer.SetPosition(i, new Vector3(x, center.y, z));
            }
        }

        private void CheckDamageHit()
        {
            var colliders = Physics.OverlapSphere(transform.position, currentRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.IsDead) continue;

                // Kritik Düzeltme: Bir karaktere yalnızca TEK BİR KEZ vurabilir!
                if (hitEntities.Contains(damageable)) continue;

                // Boss'un kendisine veya sahibine vurma
                if (bossSource != null && (col.transform.IsChildOf(bossSource) || col.transform == bossSource)) continue;

                // Halka sınırında mı kontrolü (annulus band ~ 1.8m)
                Transform targetTransform = ((Component)damageable).transform;
                Vector3 flatTargetPos = targetTransform.position;
                flatTargetPos.y = transform.position.y;
                float dist = Vector3.Distance(transform.position, flatTargetPos);

                if (dist <= currentRadius && dist >= Mathf.Max(0f, currentRadius - 2.0f))
                {
                    hitEntities.Add(damageable);

                    Vector3 hitDir = (targetTransform.position - transform.position).normalized;
                    hitDir.y = 0;
                    Vector3 hitPos = targetTransform.position + Vector3.up * 0.8f;

                    // Hasar gönderimi (Oyuncu kalkan tutuyorsa TakeDamage bunu bloklar ve canı korur!)
                    damageable.TakeDamage(damage, hitPos, hitDir, bossSource != null ? bossSource : transform);

                    CombatDebugVisualizer.SpawnContactPoint(hitPos, hitDir, false, "💥 BOSS ALAN HASARI (SHOCKWAVE)");
                }
            }
        }

        private void OnDestroy()
        {
            if (ringMat != null)
            {
                Destroy(ringMat);
            }
        }
    }
}