using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Bölge sahnelerini oyuncunun konumuna göre arka planda (additive) yükler ve boşaltır.
    /// Core sahnesinde durur; Core her zaman aktif sahnedir, böylece ışık/sis ayarları ve
    /// kodla oluşturulan objeler (efektler, yöneticiler) bölge boşaltılınca kaybolmaz.
    ///
    /// Akış: oyuncu bir bölgenin sınır kutusuna loadDistance kadar yaklaşınca bölge yüklenir,
    /// unloadDistance'tan uzaklaşınca boşaltılır (aradaki fark, sınırda gidip gelirken sürekli
    /// yükle/boşalt yapılmasını önler).
    /// </summary>
    public class WorldStreamer : MonoBehaviour
    {
        public static WorldStreamer Instance { get; private set; }

        [Tooltip("Konum kontrolü aralığı (saniye)")]
        [SerializeField] private float checkInterval = 0.25f;

        // Bölge durum paneli (oyunda F4 ile aç/kapat). Varsayılan kapalı: açıkken HUD'ın üstüne binebiliyordu.
        private bool showDebugPanel = false;

        private readonly List<string> eventLog = new List<string>();
        private float lastLoadMs;

        private WorldMap map;
        private Transform player;
        private bool streamingEnabled;
        private float checkTimer;
        private readonly HashSet<string> pending = new HashSet<string>();

        public WorldMap Map => map;
        public bool IsStreaming => streamingEnabled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Core bir şekilde iki kez yüklendiyse sadece bu objeyi değil, kopya sahnenin tamamını kaldır
                var dupScene = gameObject.scene;
                if (dupScene.IsValid() && dupScene != Instance.gameObject.scene && WorldMap.Instance != null && dupScene.name == WorldMap.Instance.coreScene)
                {
                    Debug.LogWarning($"[WorldStreamer] '{dupScene.name}' ikinci kez yüklenmiş; kopya kaldırılıyor.");
                    foreach (var root in dupScene.GetRootGameObjects()) root.SetActive(false); // kopyadaki HUD/oyuncu bu karede bile çalışmasın
                    SceneManager.UnloadSceneAsync(dupScene);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }
            Instance = this;
            map = WorldMap.Instance;

            // Not: Awake sırasında Core sahnesi henüz "yüklendi" sayılmaz; SetActiveScene burada hata veriyor
            // ve Awake'in geri kalanını (sınır güncellemesi) çalıştırmıyordu. Aktif sahne Start'ta ayarlanır.
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            EnsureCoreIsActive();

            // Oyun başında zaten açık olan bölgelerin sınırlarını güncelle
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) RefreshRegionBounds(s);
            }
        }

        /// <summary>Core aktif sahne olsun (ışık/sis buradan gelir, runtime objeler buraya düşer).</summary>
        private void EnsureCoreIsActive()
        {
            var core = gameObject.scene;
            if (core.IsValid() && core.isLoaded && SceneManager.GetActiveScene() != core)
            {
                SceneManager.SetActiveScene(core);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshRegionBounds(scene);
        }

        /// <summary>
        /// Bölge yüklenince sınırını içeriğinden yeniden hesaplar (WorldMap'teki değer eskiyse bile akış doğru çalışsın).
        /// Editörde değer WorldMap asset'ine de yazılır; bir sonraki açılışta doğru sınırlarla başlar.
        /// </summary>
        private void RefreshRegionBounds(Scene scene)
        {
            if (map == null) return;
            var region = map.Find(scene.name);
            if (region == null) return;

            var computed = WorldMap.ComputePlayableBounds(scene);
            if (!computed.HasValue) return;

            var b = computed.Value;
            if ((b.center - region.bounds.center).sqrMagnitude < 0.01f && (b.size - region.bounds.size).sqrMagnitude < 0.01f) return;

            GameLog.Info($"[WorldStreamer] {scene.name} sınırı güncellendi: {region.bounds.center}/{region.bounds.size} → {b.center}/{b.size}");
            region.bounds = b;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(map);
#endif
        }

        /// <summary>İlk yerleştirme bittikten sonra PlayerDeathHandler tarafından açılır.</summary>
        public void BeginStreaming(Transform playerTransform)
        {
            player = playerTransform;
            streamingEnabled = true;
            checkTimer = 0f;
        }

        public void PauseStreaming()
        {
            streamingEnabled = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4)) showDebugPanel = !showDebugPanel;

            if (!streamingEnabled || map == null || player == null) return;

            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer > 0f) return;
            checkTimer = checkInterval;

            Vector3 pos = player.position;
            foreach (var region in map.regions)
            {
                float dist = Mathf.Sqrt(region.bounds.SqrDistance(pos));
                bool loaded = IsLoaded(region.sceneName);

                if (dist <= map.loadDistance && !loaded && !pending.Contains(region.sceneName))
                {
                    StartCoroutine(LoadRoutine(region.sceneName));
                }
                else if (dist > map.unloadDistance && loaded && !pending.Contains(region.sceneName))
                {
                    StartCoroutine(UnloadRoutine(region.sceneName));
                }
            }
        }

        public static bool IsLoaded(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>Bölge yüklü değilse yükler ve bitene kadar bekler.</summary>
        public IEnumerator EnsureLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || IsLoaded(sceneName)) yield break;

            if (pending.Contains(sceneName))
            {
                while (pending.Contains(sceneName)) yield return null;
                if (IsLoaded(sceneName)) yield break;
            }

            yield return LoadRoutine(sceneName);
        }

        /// <summary>Noktanın loadDistance yakınındaki tüm bölgeleri yükler (doğma noktasının zemini ve komşuları).</summary>
        public IEnumerator EnsureLoadedAround(Vector3 position)
        {
            if (map == null) yield break;
            foreach (var region in map.regions)
            {
                if (Mathf.Sqrt(region.bounds.SqrDistance(position)) <= map.loadDistance)
                {
                    yield return EnsureLoaded(region.sceneName);
                }
            }
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[WorldStreamer] '{sceneName}' Build Settings'te yok. File > Build Profiles > Scene List'e ekle.");
                yield break;
            }

            pending.Add(sceneName);
            float startTime = Time.realtimeSinceStartup;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
            pending.Remove(sceneName);
            lastLoadMs = (Time.realtimeSinceStartup - startTime) * 1000f;
            LogEvent($"<color=lime>+ {sceneName}</color> ({lastLoadMs:F0} ms)");

            // Additive yükleme aktif sahneyi değiştirmez ama garantiye al
            EnsureCoreIsActive();
            GameLog.Info($"[WorldStreamer] Yüklendi: {sceneName}");
        }

        private IEnumerator UnloadRoutine(string sceneName)
        {
            pending.Add(sceneName);
            var op = SceneManager.UnloadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;
            pending.Remove(sceneName);
            LogEvent($"<color=orange>- {sceneName}</color>");

            // Boşaltılan sahnenin artık kullanılmayan doku/mesh'lerini bellekten at
            Resources.UnloadUnusedAssets();
            GameLog.Info($"[WorldStreamer] Boşaltıldı: {sceneName}");
        }

        // ==========================================
        // BÖLGE DURUM PANELİ (F4)
        // ==========================================
        private void LogEvent(string text)
        {
            eventLog.Insert(0, $"{Time.time:F1}s  {text}");
            if (eventLog.Count > 6) eventLog.RemoveAt(eventLog.Count - 1);
        }

        // Kare süresi ölçümü (yumuşatılmış)
        private float smoothedDelta = 1f / 60f;

        private void LateUpdate()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);
        }

        private void OnGUI()
        {
            if (!showDebugPanel || map == null) return;

            // Sağ ortada dursun: sol üstteki can/stamina barlarının ve sağ alttaki ruh sayacının üstüne binmesin
            int lines = map.regions.Count + eventLog.Count + 6;
            float height = Mathf.Min(lines * 20f + 14f, Screen.height * 0.5f);
            float width = Mathf.Min(420f, Screen.width * 0.45f);
            var rect = new Rect(Screen.width - width - 10f, Screen.height * 0.5f - height * 0.5f, width, height);
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 8f));
            GUILayout.Label($"<b>🌍 Bölgeler</b>  <size=10>(yükle ≤ {map.loadDistance:F0} m, boşalt > {map.unloadDistance:F0} m — F4 kapat)</size>");

            Vector3 pos = player != null ? player.position : Vector3.zero;
            foreach (var r in map.regions)
            {
                float d = Mathf.Sqrt(r.bounds.SqrDistance(pos));
                string state = pending.Contains(r.sceneName) ? "<color=yellow>YÜKLENİYOR…</color>"
                             : IsLoaded(r.sceneName) ? "<color=lime>YÜKLÜ</color>"
                             : "<color=grey>boşaltıldı</color>";
                string inside = d <= 0.01f ? "  <color=cyan>◀ buradasın</color>" : "";
                GUILayout.Label($"{r.displayName,-16} {state}   {d,5:F0} m{inside}");
            }

            GUILayout.Space(4f);
            float fps = 1f / Mathf.Max(0.0001f, smoothedDelta);
            string fpsColor = fps >= 55f ? "lime" : fps >= 30f ? "yellow" : "red";
            GUILayout.Label($"<b>Performans</b>  <color={fpsColor}>{fps:F0} FPS</color> ({smoothedDelta * 1000f:F1} ms)   yüklü sahne: {SceneManager.sceneCount}");
            if (LightBudget.Instance != null)
            {
                LightBudget.Instance.GetStats(out int active, out int shadowed, out int total);
                GUILayout.Label($"Nokta ışık: {active}/{total} açık, {shadowed} gölgeli   gölge mesafesi: {QualitySettings.shadowDistance:F0} m");
            }

            foreach (var e in eventLog) GUILayout.Label($"<size=11>{e}</size>");
            GUILayout.EndArea();
        }

        // ==========================================
        // EDİTÖR KOLAYLIĞI: bir bölge sahnesini açıp Play'e basınca Core otomatik yüklensin
        // ==========================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadCoreIfStartedFromRegion()
        {
            var worldMap = WorldMap.Instance;
            if (worldMap == null) return;

            // Core listede varsa (yüklenmekte olsa bile) ya da çalışan bir WorldStreamer/oyuncu varsa dokunma.
            // Önceden sadece "yüklendi mi" bakılıyordu; editörde birden fazla sahne açıkken Play'e basınca Core
            // henüz yüklenirken ikinci bir kopyası yükleniyordu (çift HUD, çift EventSystem, çift ışık).
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == worldMap.coreScene) return;
            }
            if (Instance != null || Object.FindAnyObjectByType<WorldStreamer>(FindObjectsInactive.Include) != null) return;

            bool anyRegionLoaded = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (worldMap.IsRegion(SceneManager.GetSceneAt(i).name))
                {
                    anyRegionLoaded = true;
                    break;
                }
            }
            if (!anyRegionLoaded) return;

            if (!Application.CanStreamedLevelBeLoaded(worldMap.coreScene))
            {
                Debug.LogWarning($"[WorldStreamer] Bölge sahnesinden başlatıldı ama '{worldMap.coreScene}' Build Settings'te yok.");
                return;
            }
            SceneManager.LoadSceneAsync(worldMap.coreScene, LoadSceneMode.Additive);
        }
    }
}
