using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulsRPG.CameraSystem;
using SoulsRPG.Combat;

namespace SoulsRPG.Enemy
{
    [RequireComponent(typeof(LockOnTarget))]
    [RequireComponent(typeof(Collider))]
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private string dummyName = "Antrenman Şövalyesi";
        [SerializeField] private int maxHealth = 150;
        [SerializeField] private float knockbackDistance = 0.35f;

        [Header("Health Bar Settings")]
        [SerializeField] private float healthBarHeightOffset = 2.2f;
        [SerializeField] private float healthBarDisplayDuration = 5.0f;

        private int currentHealth;
        private bool isDead = false;

        public bool IsDead => isDead;

        // Renderers & Flash
        private Renderer[] allRenderers;
        private List<Color> originalColors = new List<Color>();

        // Physical Hit Reaction
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Coroutine recoilRoutine;

        // Floating Health Bar Tracker
        private float healthBarTimer = 0f;
        private float displayedHealthFraction = 1.0f;
        private float trailingYellowFraction = 1.0f;

        private LockOnTarget lockOnTarget;
        private Collider dummyCollider;

        private void Awake()
        {
            currentHealth = maxHealth;
            originalPosition = transform.position;
            originalRotation = transform.rotation;

            lockOnTarget = GetComponent<LockOnTarget>();
            dummyCollider = GetComponent<Collider>();

            // Tüm zırh ve vücut parçalarının renderers'larını topla
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

            // Ekipmanları otomatik bul ve hizala
            var eq = GetComponent<Combat.EquipmentManager>();
            if (eq == null)
            {
                eq = gameObject.AddComponent<Combat.EquipmentManager>();
            }
            eq.FindSocketsIfMissing();
            eq.UpdateTransforms();
        }

        private void Update()
        {
            if (healthBarTimer > 0f)
            {
                healthBarTimer -= Time.deltaTime;
            }

            // Dark Souls tarzı gecikmeli sarı can çubuğu animasyonu
            float targetFraction = (float)currentHealth / maxHealth;
            displayedHealthFraction = targetFraction;
            trailingYellowFraction = Mathf.MoveTowards(trailingYellowFraction, targetFraction, Time.deltaTime * 0.45f);
        }

        public void TakeDamage(int damage)
        {
            Vector3 defaultDir = -transform.forward;
            TakeDamage(damage, transform.position + Vector3.up * 1.2f, defaultDir);
        }

        public void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, Transform attacker = null, float poiseMultiplier = 1f)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            healthBarTimer = healthBarDisplayDuration;
            Audio.SoulsAudioManager.GetOrCreate().PlayHit(hitPoint);

            // 1. Uçan Hasar Rakamı Oluştur
            SpawnFloatingDamageNumber(damage, hitPoint);

            // 2. Kırmızı Darbe Flaş Efekti
            StopCoroutine(nameof(FlashRedRoutine));
            StartCoroutine(nameof(FlashRedRoutine));

            // 3. Fiziksel Geri Tepme ve Sarsılma (Hit Recoil)
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            recoilRoutine = StartCoroutine(HitRecoilRoutine(hitDirection));

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private IEnumerator FlashRedRoutine()
        {
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] != null && allRenderers[i].material != null)
                {
                    allRenderers[i].material.color = new Color(1.0f, 0.2f, 0.2f);
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
            Vector3 startPos = transform.position;
            Vector3 recoilTarget = startPos + (hitDirection.normalized * knockbackDistance);

            // Darbe açısına göre hafif geriye yatma rotasyonu
            Quaternion tiltRot = Quaternion.Euler(hitDirection.z * 14f, 0f, -hitDirection.x * 14f) * originalRotation;

            float t = 0f;
            float duration = 0.12f;

            // 1. Geriye savrulma
            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;
                transform.position = Vector3.Lerp(startPos, recoilTarget, progress);
                transform.rotation = Quaternion.Slerp(originalRotation, tiltRot, progress);
                yield return null;
            }

            // 2. Yaylanarak geri toparlanma
            t = 0f;
            duration = 0.18f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;
                transform.rotation = Quaternion.Slerp(tiltRot, originalRotation, progress);
                yield return null;
            }

            transform.rotation = originalRotation;
        }

        private void SpawnFloatingDamageNumber(int damage, Vector3 hitPoint)
        {
            GameObject textObj = new GameObject("DamageNumber");
            textObj.transform.position = hitPoint + Vector3.up * 0.35f + Random.insideUnitSphere * 0.15f;

            var tm = textObj.AddComponent<TextMesh>();
            tm.text = $"-{damage}";
            tm.fontSize = 28;
            tm.characterSize = 0.08f;
            tm.color = new Color(1.0f, 0.4f, 0.1f); // Souls turuncu-kırmızı hasar rengi
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;

            textObj.AddComponent<FloatingDamageTextAnimator>();
        }

        private void Die()
        {
            isDead = true;

            if (Player.PlayerStats.Instance != null)
            {
                Player.PlayerStats.Instance.AddSouls(100);
            }

            if (lockOnTarget != null)
            {
                lockOnTarget.SetLockable(false);
            }
            if (dummyCollider != null)
            {
                dummyCollider.enabled = false;
            }

            StartCoroutine(DeathAndRespawnRoutine());
        }

        private IEnumerator DeathAndRespawnRoutine()
        {
            // Yere devrilme efekti
            Quaternion fallRot = Quaternion.Euler(-80f, transform.eulerAngles.y, 0f);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(originalRotation, fallRot, t / 0.4f);
                yield return null;
            }

            // 3 saniye yerde bekle
            yield return new WaitForSeconds(3.0f);

            // Yeniden doğuş (Respawn)
            currentHealth = maxHealth;
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            isDead = false;

            if (dummyCollider != null)
            {
                dummyCollider.enabled = true;
            }
            if (lockOnTarget != null)
            {
                lockOnTarget.SetLockable(true);
            }

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
            // Sadece hasar aldığında veya canlıyken başının üstünde can barı göster
            if (isDead || healthBarTimer <= 0f) return;

            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 worldHeadPos = transform.position + Vector3.up * healthBarHeightOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(worldHeadPos);

            // Kamera arkasındaysa çizme
            if (screenPos.z <= 0.5f) return;

            float barWidth = 140f;
            float barHeight = 8f;
            float posX = screenPos.x - (barWidth / 2f);
            float posY = Screen.height - screenPos.y;

            // İsim Etiketi
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            nameStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(posX - 30, posY - 22, barWidth + 60, 20), dummyName, nameStyle);

            // 1. Arka Plan (Koyu Gri Çerçeve)
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(posX - 1, posY - 1, barWidth + 2, barHeight + 2), Texture2D.whiteTexture);

            // 2. Gecikmeli Sarı Can Barı (Trailing Bar)
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
            GUI.DrawTexture(new Rect(posX, posY, barWidth * trailingYellowFraction, barHeight), Texture2D.whiteTexture);

            // 3. Kırmızı Ön Can Barı (Current Health)
            GUI.color = new Color(0.85f, 0.15f, 0.15f, 1.0f);
            GUI.DrawTexture(new Rect(posX, posY, barWidth * displayedHealthFraction, barHeight), Texture2D.whiteTexture);

            // Rengi geri yükle
            GUI.color = Color.white;
        }

        private void OnDrawGizmos()
        {
            var cap = GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
                Vector3 size = new Vector3(cap.radius * 2f, cap.height, cap.radius * 2f);
                Gizmos.DrawWireCube(cap.center, size);
            }
        }
    }

    /// <summary>
    /// Uçan hasar rakamını yukarı doğru süzüp solduran yardımcı bileşen
    /// </summary>
    public class FloatingDamageTextAnimator : MonoBehaviour
    {
        private TextMesh tm;
        private Color initialColor;
        private float lifeTimer = 0.85f;
        private float maxLife;

        private void Awake()
        {
            tm = GetComponent<TextMesh>();
            if (tm != null)
            {
                initialColor = tm.color;
            }
            maxLife = lifeTimer;
        }

        private void Update()
        {
            lifeTimer -= Time.deltaTime;

            // Kameraya dön
            if (UnityEngine.Camera.main != null)
            {
                transform.rotation = UnityEngine.Camera.main.transform.rotation;
            }

            // Yukarı doğru yavaşça süzül
            transform.position += Vector3.up * (1.1f * Time.deltaTime);

            // Saydamlaş
            if (tm != null)
            {
                float alpha = lifeTimer / maxLife;
                tm.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
            }

            if (lifeTimer <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
