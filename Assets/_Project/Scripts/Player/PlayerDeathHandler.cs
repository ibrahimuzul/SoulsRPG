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
    /// Souls ölüm döngüsü ve oyuna giriş:
    /// ÖLDÜN ekranı → ruhlar ölüm noktasına ruh lekesi olarak düşer → son bonfire'ın bölgesi yüklenir →
    /// karakter o bonfire'da (yoksa başlangıç noktasında) doğar → dünya (düşmanlar + boss) sıfırlanır → kayıt.
    /// Oyuna girişte de aynı yerleştirme kullanılır.
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
            StartCoroutine(InitialSpawnRoutine());
        }

        /// <summary>
        /// Oyuna giriş: ekran siyahken son kaydedilen bonfire'ın bölgesi yüklenir, karakter oraya konur,
        /// sonra bölge akışı başlar ve ekran açılır. Bölgelere ayrılmamış eski tek sahnede yükleme yapılmaz.
        /// </summary>
        private IEnumerator InitialSpawnRoutine()
        {
            var streamer = WorldStreamer.Instance;
            bool streamed = streamer != null && streamer.Map != null;

            if (streamed)
            {
                ScreenFader.Instance.SetAlpha(1f);
                if (locomotion != null) locomotion.SetResting(true); // yükleme bitene kadar hareket/yerçekimi yok
            }

            if (streamed || stats.LastBonfirePosition.HasValue)
            {
                yield return PlaceAtRespawn();
            }

            // Önceki oturumdan kalan ruh lekesini geri getir
            if (stats.BloodstainPosition.HasValue && stats.BloodstainSouls > 0)
            {
                Bloodstain.Spawn(stats.BloodstainPosition.Value);
            }

            // Dünya hazır: yükleme ekranı (menüden gelindiyse) kapanır, siyah perde açılır
            WorldEvents.RaiseWorldReady();

            if (streamed)
            {
                if (locomotion != null) locomotion.SetResting(false);
                yield return ScreenFader.Instance.FadeTo(0f, fadeFromBlack);
            }

            if (stats.LastBonfirePosition.HasValue)
            {
                GameLog.Info($"[PlayerDeathHandler] Oyun '{stats.LastBonfireName}' bonfire'ında başladı");
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
            yield return ScreenFader.Instance.FadeTo(1f, fadeToBlack);
            textGroup.alpha = 0f;

            // 3. Ruhlar ölüm noktasında kalır (önceki leke varsa kaybolur)
            Bloodstain.Clear();
            stats.DropSoulsAsBloodstain(deathPosition);

            // 4. Son bonfire'da yeniden doğ (bonfire başka bölgedeyse önce o bölge yüklenir)
            if (locomotion != null) locomotion.ResetState();
            yield return PlaceAtRespawn();

            stats.Respawn();

            // Leke, oyuncu ışınlandıktan sonra oluşturulur (yere oturtma ışını oyuncunun kapsülüne çarpmasın)
            if (stats.BloodstainPosition.HasValue)
            {
                Bloodstain.Spawn(stats.BloodstainPosition.Value);
            }

            // 5. Düşmanlar ve boss baştan
            WorldEvents.ResetWorld();

            SaveManager.SavePlayer(stats);

            // 6. Siyahtan aç
            yield return ScreenFader.Instance.FadeTo(0f, fadeFromBlack);
            isHandlingDeath = false;
        }

        /// <summary>
        /// Karakteri son bonfire'a (yoksa başlangıç noktasına) koyar. Bölgelere ayrılmış dünyada önce
        /// bonfire'ın bölgesi ve doğma noktasının çevresindeki bölgeler yüklenir; ekran bu sırada siyah olmalı.
        /// </summary>
        private IEnumerator PlaceAtRespawn()
        {
            var streamer = WorldStreamer.Instance;
            if (streamer != null && streamer.Map != null)
            {
                streamer.PauseStreaming();

                string scene = ResolveRespawnScene(streamer.Map);
                if (!string.IsNullOrEmpty(scene)) yield return streamer.EnsureLoaded(scene);

                // Bonfire artık sahnede; doğma noktasının çevresini (zemin + komşu bölgeler) yükle
                GetRespawnPose(out Vector3 roughPos, out _);
                yield return streamer.EnsureLoadedAround(roughPos);

                // Yeni yüklenen colliderların fizik sorgularına girmesi için bir fizik adımı bekle
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
            }

            GetRespawnPose(out Vector3 spawnPos, out Quaternion spawnRot);
            if (locomotion != null) locomotion.TeleportTo(spawnPos, spawnRot);
            else transform.SetPositionAndRotation(spawnPos, spawnRot);

            if (CameraManager.Instance != null) CameraManager.Instance.SnapBehindTarget();

            if (streamer != null && streamer.Map != null) streamer.BeginStreaming(transform);
        }

        /// <summary>Doğulacak bonfire'ın hangi bölge sahnesinde olduğunu bulur.</summary>
        private string ResolveRespawnScene(WorldMap map)
        {
            if (!stats.LastBonfirePosition.HasValue)
            {
                var start = map.FindRegionAt(initialSpawnPosition);
                return start != null ? start.sceneName : map.defaultRegion;
            }

            // 1. Kayıttaki sahne (bölgeyse)
            if (map.IsRegion(stats.LastBonfireScene)) return stats.LastBonfireScene;

            // 2. Bonfire tanımındaki bölge (eski tek-sahne kayıtları için)
            var def = Bonfire.FindDefinition(stats.LastBonfireId);
            if (def.HasValue && map.IsRegion(def.Value.RegionScene)) return def.Value.RegionScene;

            // 3. Kayıtlı konumu kapsayan bölge
            var region = map.FindRegionAt(stats.LastBonfirePosition.Value);
            return region != null ? region.sceneName : map.defaultRegion;
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

        private void BuildUI()
        {
            // Siyah perde artık ortak ScreenFader'da; burada sadece ÖLDÜN yazısı var
            var canvasGo = new GameObject("--- DEATH SCREEN ---");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasGo.AddComponent<NotHudCanvas>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

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
