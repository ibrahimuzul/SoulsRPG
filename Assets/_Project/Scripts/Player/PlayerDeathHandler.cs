using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SoulsRPG.CameraSystem;
using SoulsRPG.Core;
using SoulsRPG.Environment;
using SoulsRPG.Save;

namespace SoulsRPG.Player
{
    /// <summary>
    /// Souls ölüm döngüsü:
    /// ÖLDÜN ekranı → ruhlar ölüm noktasına ruh lekesi olarak düşer → son bonfire'da (yoksa başlangıç noktasında)
    /// yeniden doğma → dünya (düşmanlar + boss) sıfırlanır → kayıt.
    /// PlayerStats.Awake tarafından otomatik eklenir.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("Zamanlama")]
        [SerializeField] private float delayBeforeText = 1.0f;
        [SerializeField] private float textFadeIn = 1.4f;
        [SerializeField] private float textHold = 2.2f;
        [SerializeField] private float fadeToBlack = 1.0f;
        [SerializeField] private float fadeFromBlack = 1.2f;

        [Header("Yeniden Doğma")]
        [Tooltip("Bonfire merkezinden ne kadar uzakta doğulacağı")]
        [SerializeField] private float bonfireSpawnOffset = 1.6f;

        private PlayerStats stats;
        private PlayerLocomotion locomotion;

        private Vector3 initialSpawnPosition;
        private Quaternion initialSpawnRotation;
        private bool isHandlingDeath;

        // UI
        private CanvasGroup textGroup;
        private Image blackOverlay;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            locomotion = GetComponent<PlayerLocomotion>();
            initialSpawnPosition = transform.position;
            initialSpawnRotation = transform.rotation;

            // Karakter pivotunun ayak tabanına göre yüksekliği (CharacterController kapsülünden)
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                float scaleY = transform.lossyScale.y;
                pivotHeightAboveFeet = Mathf.Max(0f, -(cc.center.y - cc.height * 0.5f) * scaleY) + cc.skinWidth;
            }
        }

        private float pivotHeightAboveFeet;

        private void OnEnable()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            stats.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (stats != null) stats.OnDeath -= HandleDeath;
        }

        private void Start()
        {
            BuildUI();

            // Oyuna girişte son kaydedilen bonfire'da başla
            SpawnAtSavedBonfire();

            // Önceki oturumdan kalan ruh lekesini geri getir
            if (stats.BloodstainPosition.HasValue && stats.BloodstainSouls > 0)
            {
                Bloodstain.Spawn(stats.BloodstainPosition.Value);
            }
        }

        private void HandleDeath()
        {
            if (isHandlingDeath) return;
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            isHandlingDeath = true;
            Vector3 deathPosition = transform.position;

            if (CameraManager.Instance != null) CameraManager.Instance.ClearLockOn();

            yield return new WaitForSeconds(delayBeforeText);

            // 1. ÖLDÜN yazısı
            yield return Fade(a => textGroup.alpha = a, 0f, 1f, textFadeIn);
            yield return new WaitForSeconds(textHold);

            // 2. Siyaha karar
            yield return Fade(a => SetBlackAlpha(a), 0f, 1f, fadeToBlack);
            textGroup.alpha = 0f;

            // 3. Ruhlar ölüm noktasında kalır (önceki leke varsa kaybolur)
            Bloodstain.Clear();
            stats.DropSoulsAsBloodstain(deathPosition);

            // 4. Son bonfire'da yeniden doğ
            GetRespawnPose(out Vector3 spawnPos, out Quaternion spawnRot);
            if (locomotion != null)
            {
                locomotion.ResetState();
                locomotion.TeleportTo(spawnPos, spawnRot);
            }
            else
            {
                transform.SetPositionAndRotation(spawnPos, spawnRot);
            }

            stats.Respawn();

            // Leke, oyuncu ışınlandıktan sonra oluşturulur (yere oturtma ışını oyuncunun kapsülüne çarpmasın)
            if (stats.BloodstainPosition.HasValue)
            {
                Bloodstain.Spawn(stats.BloodstainPosition.Value);
            }

            // 5. Düşmanlar ve boss baştan
            WorldEvents.ResetWorld();

            if (CameraManager.Instance != null) CameraManager.Instance.SnapBehindTarget();

            SaveManager.SavePlayer(stats);

            // 6. Siyahtan aç
            yield return Fade(a => SetBlackAlpha(a), 1f, 0f, fadeFromBlack);
            isHandlingDeath = false;
        }

        /// <summary>
        /// Oyun açılınca karakteri son kaydedilen bonfire'a koyar (kayıt yoksa sahnedeki başlangıç noktasında kalır).
        /// </summary>
        private void SpawnAtSavedBonfire()
        {
            if (!stats.LastBonfirePosition.HasValue) return;

            GetRespawnPose(out Vector3 spawnPos, out Quaternion spawnRot);
            if (locomotion != null) locomotion.TeleportTo(spawnPos, spawnRot);
            else transform.SetPositionAndRotation(spawnPos, spawnRot);

            if (CameraManager.Instance != null) CameraManager.Instance.SnapBehindTarget();
            GameLog.Info($"[PlayerDeathHandler] Oyun '{stats.LastBonfireName}' bonfire'ında başladı");
        }

        /// <summary>
        /// Son dinlenilen bonfire'ı kimliğiyle bulur (eski kayıtlarda konuma en yakın olanı);
        /// karakteri ateşin arkasına, ateşin baktığı yöne dönük ve yere oturmuş şekilde yerleştirir.
        /// Hiç bonfire'da dinlenilmediyse sahnedeki başlangıç noktası kullanılır.
        /// </summary>
        private void GetRespawnPose(out Vector3 position, out Quaternion rotation)
        {
            if (!stats.LastBonfirePosition.HasValue)
            {
                position = initialSpawnPosition;
                rotation = initialSpawnRotation;
                return;
            }

            Vector3 savedPos = stats.LastBonfirePosition.Value;
            Bonfire bonfire = Bonfire.FindById(stats.LastBonfireId);
            if (bonfire == null)
            {
                bonfire = Bonfire.FindNearest(savedPos);
                // Bulunan ateş kayıtlı konumdan çok uzaksa ona güvenme (bonfire silinmiş/taşınmış olabilir)
                if (bonfire != null && (bonfire.transform.position - savedPos).sqrMagnitude > 25f) bonfire = null;
            }

            if (bonfire != null)
            {
                bonfire.GetSpawnPose(bonfireSpawnOffset, out position, out rotation);
            }
            else
            {
                position = savedPos - Vector3.forward * bonfireSpawnOffset;
                rotation = Quaternion.identity;
            }

            position = SnapToGround(position);
        }

        /// <summary>
        /// Noktayı zemine oturtur ve karakterin pivot yüksekliğini ekler
        /// (pivot kapsülün ortasındaysa yere gömülmesini önler).
        /// </summary>
        private Vector3 SnapToGround(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * 3f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            float groundY = point.y;
            foreach (var h in hits)
            {
                // Oyuncunun kendi kapsülünü yok say
                if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    groundY = h.point.y;
                    found = true;
                }
            }

            if (!found) groundY = point.y;
            return new Vector3(point.x, groundY + pivotHeightAboveFeet + 0.05f, point.z);
        }

        private IEnumerator Fade(System.Action<float> apply, float from, float to, float duration)
        {
            float t = 0f;
            apply(from);
            while (t < duration)
            {
                t += Time.deltaTime;
                apply(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            apply(to);
        }

        private void SetBlackAlpha(float a)
        {
            if (blackOverlay == null) return;
            var c = blackOverlay.color;
            c.a = a;
            blackOverlay.color = c;
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("--- DEATH SCREEN ---");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Siyah perde
            var blackGo = new GameObject("BlackOverlay");
            blackGo.transform.SetParent(canvasGo.transform, false);
            var blackRect = blackGo.AddComponent<RectTransform>();
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.sizeDelta = Vector2.zero;
            blackOverlay = blackGo.AddComponent<Image>();
            blackOverlay.color = new Color(0f, 0f, 0f, 0f);
            blackOverlay.raycastTarget = false;

            // ÖLDÜN şeridi + yazı
            var groupGo = new GameObject("DeathText");
            groupGo.transform.SetParent(canvasGo.transform, false);
            var groupRect = groupGo.AddComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0f, 0.42f);
            groupRect.anchorMax = new Vector2(1f, 0.58f);
            groupRect.sizeDelta = Vector2.zero;
            textGroup = groupGo.AddComponent<CanvasGroup>();
            textGroup.alpha = 0f;
            textGroup.blocksRaycasts = false;
            textGroup.interactable = false;

            var band = groupGo.AddComponent<Image>();
            band.color = new Color(0f, 0f, 0f, 0.7f);
            band.raycastTarget = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(groupGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "ÖLDÜN";
            text.fontSize = 96;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.62f, 0.08f, 0.06f);
            text.raycastTarget = false;
        }
    }
}
