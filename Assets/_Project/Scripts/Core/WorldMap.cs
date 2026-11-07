using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Dünyanın bölge haritası: hangi bölge sahnesi dünyanın neresini kapsıyor.
    /// "SoulsRPG/🌍 Sahneyi Core + Bölgelere Ayır" komutu bunu Assets/_Project/Resources/WorldMap.asset olarak oluşturur;
    /// bölge sınırları sahne içeriğinden otomatik hesaplanır, Inspector'dan elle de düzenlenebilir.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldMap", menuName = "SoulsRPG/World Map")]
    public class WorldMap : ScriptableObject
    {
        public const string ResourcePath = "WorldMap";
        public const string CoreSceneName = "Core";
        public const string CourtyardScene = "Region_KaleAvlusu";
        public const string BossGateScene = "Region_MuhafizKapisi";

        [Serializable]
        public class Region
        {
            public string sceneName;
            public string displayName;
            [Tooltip("Bölgenin dünyada kapladığı alan. Oyuncu bu kutuya 'loadDistance' kadar yaklaşınca bölge yüklenir.")]
            public Bounds bounds;
        }

        [Tooltip("Hep yüklü kalan çekirdek sahne (oyuncu, kamera, HUD, yöneticiler)")]
        public string coreScene = CoreSceneName;

        [Tooltip("Kayıt yoksa oyunun başladığı bölge")]
        public string defaultRegion = CourtyardScene;

        [Tooltip("Oyuncu bölge sınırına bu kadar yaklaşınca bölge arka planda yüklenir (metre)")]
        public float loadDistance = 30f;

        [Tooltip("Oyuncu bölge sınırından bu kadar uzaklaşınca bölge boşaltılır (metre). loadDistance'tan büyük olmalı.")]
        public float unloadDistance = 45f;

        public List<Region> regions = new List<Region>();

        private static WorldMap cached;
        private static bool searched;

        /// <summary>Resources'taki haritayı döndürür; proje henüz bölgelere ayrılmadıysa null.</summary>
        public static WorldMap Instance
        {
            get
            {
                if (!searched)
                {
                    cached = Resources.Load<WorldMap>(ResourcePath);
                    searched = true;
                }
                return cached;
            }
        }

        public Region Find(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            foreach (var r in regions)
            {
                if (r.sceneName == sceneName) return r;
            }
            return null;
        }

        public bool IsRegion(string sceneName) => Find(sceneName) != null;

        /// <summary>Noktayı içeren (yoksa en yakın) bölge.</summary>
        public Region FindRegionAt(Vector3 position)
        {
            Region best = null;
            float bestDist = float.MaxValue;
            foreach (var r in regions)
            {
                float d = r.bounds.SqrDistance(position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = r;
                }
            }
            return best;
        }

        /// <summary>
        /// Sahnenin OYNANABİLİR alanı: çarpılabilen (trigger olmayan) collider'ları kapsayan kutu (+2 m pay).
        /// Grup objelerinin konumu ve adında "Background" geçen uzak manzara objeleri sayılmaz.
        /// Collider yoksa renderer'lar kullanılır; hiçbiri yoksa null.
        /// </summary>
        public static Bounds? ComputePlayableBounds(UnityEngine.SceneManagement.Scene scene)
        {
            bool hasCol = false, hasRen = false;
            Bounds col = default, ren = default;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Collider>())
                {
                    if (!c.enabled || c.isTrigger || c is CharacterController || c.bounds.size == Vector3.zero || IsBackground(c.transform)) continue;
                    if (!hasCol) { col = c.bounds; hasCol = true; } else col.Encapsulate(c.bounds);
                }
                if (hasCol) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r.bounds.size == Vector3.zero || IsBackground(r.transform)) continue;
                    if (!hasRen) { ren = r.bounds; hasRen = true; } else ren.Encapsulate(r.bounds);
                }
            }

            // Bonfire, boss, düşman gibi sabitleyiciler her zaman kutunun içinde olsun
            bool hasAnchor = false;
            Bounds anchors = default;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>())
                {
                    if (!(mb is IStreamingAnchor)) continue;
                    var ab = new Bounds(mb.transform.position, Vector3.one * 4f);
                    if (!hasAnchor) { anchors = ab; hasAnchor = true; } else anchors.Encapsulate(ab);
                }
            }

            if (!hasCol && !hasRen && !hasAnchor) return null;
            Bounds b;
            if (hasCol) b = col;
            else if (hasRen) b = ren;
            else b = anchors;
            if (hasAnchor) b.Encapsulate(anchors);
            b.Expand(4f);
            return b;
        }

        private static bool IsBackground(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            cached = null;
            searched = false;
        }
    }

    public static class IdUtil
    {
        /// <summary>"Kale Avlusu Ateşi" → "kale_avlusu_atesi"</summary>
        public static string Slugify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "id";
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char raw in text.ToLowerInvariant())
            {
                char c = raw;
                switch (c)
                {
                    case 'ş': c = 's'; break;
                    case 'ğ': c = 'g'; break;
                    case 'ı': c = 'i'; break;
                    case 'ö': c = 'o'; break;
                    case 'ü': c = 'u'; break;
                    case 'ç': c = 'c'; break;
                }
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }
    }
}
