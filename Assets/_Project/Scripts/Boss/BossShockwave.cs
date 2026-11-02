using System.Collections.Generic;
using UnityEngine;
using SoulsRPG.Player;
using SoulsRPG.Combat;

namespace SoulsRPG.Boss
{
    /// <summary>
    /// Boss zemin darbesiyle (Ground Slam) tetiklenen, dışa doğru genişleyen alan hasarı (AOE Shockwave).
    /// Tekil hedef koruması (Single-hit guarantee) sayesinde bir karaktere yalnızca 1 kez vurabilir.
    /// Zemin üzerinde parlak alev halkası ve ışık dalgası olarak net şekilde görünür.
    /// </summary>
    public class BossShockwave : MonoBehaviour
    {
        [Header("Shockwave Settings")]
        [SerializeField] private float maxRadius = 7.0f;
        [SerializeField] private float expansionSpeed = 10.5f;
        [SerializeField] private int damage = 20;

        private float currentRadius = 0.5f;
        private Transform bossSource;
        private readonly HashSet<IDamageable> hitEntities = new HashSet<IDamageable>();
        private LineRenderer ringRenderer;
        private Light shockwaveLight;
        private const int RingSegments = 48;
        private float lifeTimer = 0f;
        private const float MaxLife = 0.85f;
        private Material ringMat;

        public void Initialize(Transform boss, int slamDamage, float radius = 7.0f)
        {
            bossSource = boss;
            damage = slamDamage;
            maxRadius = radius;
            currentRadius = 0.5f;

            // Zemin 0.09m yüksekliğinde, halkanın zemin üstünde pürüzsüz görünmesi için 0.16m
            transform.position = new Vector3(boss.position.x, 0.16f, boss.position.z);

            SetupVisuals();
        }

        private void SetupVisuals()
        {
            // 1. Parlak Alev Halkası (LineRenderer)
            ringRenderer = gameObject.AddComponent<LineRenderer>();
            ringRenderer.useWorldSpace = true;
            ringRenderer.loop = true;
            ringRenderer.positionCount = RingSegments;
            ringRenderer.startWidth = 0.65f;
            ringRenderer.endWidth = 0.65f;

            // Sprites/Default shader'ı Built-in RP'de her zaman mevcuttur ve vertex rengiyle pürüzsüz parlar
            var shader = Shader.Find("Sprites/Default");
            ringMat = new Material(shader);
            ringRenderer.material = ringMat;

            // 2. Zemini Aydınlatan Alev Işığı
            shockwaveLight = gameObject.AddComponent<Light>();
            shockwaveLight.type = LightType.Point;
            shockwaveLight.color = new Color(1.0f, 0.40f, 0.08f);
            shockwaveLight.range = 12.0f;
            shockwaveLight.intensity = 4.0f;
        }

        private void Update()
        {
            lifeTimer += Time.deltaTime;
            currentRadius += expansionSpeed * Time.deltaTime;

            UpdateVisuals();
            CheckDamageHit();

            if (currentRadius >= maxRadius || lifeTimer >= MaxLife)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateVisuals()
        {
            float progress = Mathf.Clamp01(lifeTimer / MaxLife);

            // Ring
            if (ringRenderer != null)
            {
                Vector3 center = transform.position;
                float angleStep = 360f / RingSegments;

                float alpha = Mathf.Clamp01(1.0f - progress);
                Color ringColor = new Color(1.0f, 0.35f, 0.05f, alpha);
                ringRenderer.startColor = ringColor;
                ringRenderer.endColor = ringColor;
                ringRenderer.startWidth = Mathf.Lerp(0.70f, 0.12f, progress);
                ringRenderer.endWidth = ringRenderer.startWidth;

                for (int i = 0; i < RingSegments; i++)
                {
                    float rad = Mathf.Deg2Rad * (i * angleStep);
                    float x = center.x + Mathf.Cos(rad) * currentRadius;
                    float z = center.z + Mathf.Sin(rad) * currentRadius;
                    ringRenderer.SetPosition(i, new Vector3(x, center.y, z));
                }
            }

            // Light fade
            if (shockwaveLight != null)
            {
                shockwaveLight.intensity = Mathf.Lerp(4.0f, 0f, progress);
                shockwaveLight.range = Mathf.Lerp(6.0f, 14.0f, progress);
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

                if (hitEntities.Contains(damageable)) continue;

                if (bossSource != null && (col.transform.IsChildOf(bossSource) || col.transform == bossSource)) continue;

                Transform targetTransform = ((Component)damageable).transform;
                Vector3 flatTargetPos = targetTransform.position;
                flatTargetPos.y = transform.position.y;
                float dist = Vector3.Distance(transform.position, flatTargetPos);

                if (dist <= currentRadius && dist >= Mathf.Max(0f, currentRadius - 2.2f))
                {
                    hitEntities.Add(damageable);

                    Vector3 hitDir = (targetTransform.position - transform.position).normalized;
                    hitDir.y = 0;
                    Vector3 hitPos = targetTransform.position + Vector3.up * 0.8f;

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