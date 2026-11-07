using System.Collections.Generic;
using UnityEngine;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Nokta/spot ışıkları için performans bütçesi (Built-in RP).
    ///
    /// Gölgeli bir nokta ışığı, menzilindeki her şeyi her karede 6 kez daha çizdirir (küp gölge haritası).
    /// Meşaleler ve bonfire'lar çoğaldıkça bu çok pahalılaşır. Bu bileşen:
    ///  - Gölgeyi sadece kameraya en yakın 'maxShadowedLights' ışıkta açık tutar, diğerleri gölgesiz yanar
    ///  - Kameradan çok uzaktaki ışıkları tamamen kapatır (görünmüyorlar ama yine de maliyet çıkarıyorlar)
    /// Işıkların orijinal ayarını hatırlar; yaklaşınca geri açar. Oyun başında otomatik oluşturulur.
    /// </summary>
    public class LightBudget : MonoBehaviour
    {
        [Tooltip("Aynı anda gölge düşürebilecek en fazla nokta/spot ışık sayısı")]
        public int maxShadowedLights = 2;

        [Tooltip("Işık, kameraya (menzili + bu mesafe) kadar uzaksa tamamen kapatılır")]
        public float cullDistance = 40f;

        [Tooltip("Yön ışığı (ay) gölgelerinin en fazla çizileceği mesafe. Sis yüzünden ötesi zaten görünmüyor. 0 = kalite ayarına dokunma")]
        public float maxShadowDistance = 60f;

        [SerializeField] private float updateInterval = 0.25f;
        [SerializeField] private float rescanInterval = 1.0f;

        private class Entry
        {
            public Light light;
            public LightShadows desiredShadows;
            public bool disabledByBudget;
            public float distance;
        }

        private readonly Dictionary<Light, Entry> entries = new Dictionary<Light, Entry>();
        private readonly List<Entry> shadowCandidates = new List<Entry>();
        private readonly List<Light> dead = new List<Light>();
        private float updateTimer, rescanTimer;

        public static LightBudget Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            new GameObject("--- LIGHT BUDGET ---").AddComponent<LightBudget>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            originalShadowDistance = QualitySettings.shadowDistance;
            if (maxShadowDistance > 0f && QualitySettings.shadowDistance > maxShadowDistance)
            {
                QualitySettings.shadowDistance = maxShadowDistance;
            }
        }

        private float originalShadowDistance;

        private void OnDestroy()
        {
            // Editörde Play'den çıkınca kalite ayarı eski haline dönsün
            if (Instance == this)
            {
                QualitySettings.shadowDistance = originalShadowDistance;
                Instance = null;
            }
        }

        public void GetStats(out int activeLights, out int shadowedLights, out int totalLights)
        {
            activeLights = shadowedLights = totalLights = 0;
            foreach (var e in entries.Values)
            {
                if (e.light == null) continue;
                totalLights++;
                if (e.light.enabled) activeLights++;
                if (e.light.enabled && e.light.shadows != LightShadows.None) shadowedLights++;
            }
        }

        private void Update()
        {
            rescanTimer -= Time.unscaledDeltaTime;
            if (rescanTimer <= 0f)
            {
                rescanTimer = rescanInterval;
                Rescan();
            }

            updateTimer -= Time.unscaledDeltaTime;
            if (updateTimer > 0f) return;
            updateTimer = updateInterval;
            Apply();
        }

        /// <summary>Yeni yüklenen bölgelerdeki ve kodla oluşturulan (bonfire, aura) ışıkları listeye ekler.</summary>
        private void Rescan()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                if (entries.ContainsKey(light)) continue;
                entries[light] = new Entry { light = light, desiredShadows = light.shadows };
            }
        }

        private void Apply()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;

            shadowCandidates.Clear();
            dead.Clear();

            foreach (var kv in entries)
            {
                var e = kv.Value;
                if (e.light == null) { dead.Add(kv.Key); continue; }

                e.distance = Vector3.Distance(camPos, e.light.transform.position);

                // Uzaktaki ışığı kapat / yaklaşınca geri aç (başka bir kodun kapattığı ışığa dokunma)
                bool tooFar = e.distance > e.light.range + cullDistance;
                if (tooFar && e.light.enabled)
                {
                    e.light.enabled = false;
                    e.disabledByBudget = true;
                }
                else if (!tooFar && e.disabledByBudget)
                {
                    e.light.enabled = true;
                    e.disabledByBudget = false;
                }

                if (e.desiredShadows != LightShadows.None && e.light.enabled) shadowCandidates.Add(e);
                else if (e.light.shadows != LightShadows.None && e.desiredShadows == LightShadows.None) e.light.shadows = LightShadows.None;
            }

            foreach (var key in dead) entries.Remove(key);

            // En yakın N ışık gölgeli, diğerleri gölgesiz
            shadowCandidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            float shadowRange = QualitySettings.shadowDistance;
            for (int i = 0; i < shadowCandidates.Count; i++)
            {
                var e = shadowCandidates[i];
                bool allow = i < maxShadowedLights && e.distance - e.light.range < shadowRange;
                var target = allow ? e.desiredShadows : LightShadows.None;
                if (e.light.shadows != target) e.light.shadows = target;
            }
        }
    }
}
