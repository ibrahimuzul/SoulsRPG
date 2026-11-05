using System.Collections.Generic;
using UnityEngine;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Çalışma anında kodla oluşturulan materyaller için shader bulucu.
    /// Kullanılan shader'lar Editor'deki AlwaysIncludedShadersSetup ile build'e dahil edilir,
    /// böylece build'de pembe (eksik shader) materyal çıkmaz.
    /// </summary>
    public static class RuntimeShaders
    {
        public const string Standard = "Standard";
        public const string ParticlesUnlit = "Particles/Standard Unlit";
        public const string UnlitColor = "Unlit/Color";
        public const string SpritesDefault = "Sprites/Default";

        /// <summary>Editor script'inin build'e ekleyeceği shader listesi.</summary>
        public static readonly string[] RequiredShaders = { Standard, ParticlesUnlit, UnlitColor, SpritesDefault };

        private static readonly Dictionary<string, Shader> cache = new Dictionary<string, Shader>();
        private static readonly Dictionary<Color, Material> particleMaterials = new Dictionary<Color, Material>();

        /// <summary>Verilen sırayla ilk bulunan shader'ı döndürür; hiçbiri yoksa Sprites/Default'a düşer.</summary>
        public static Shader Find(params string[] names)
        {
            foreach (var name in names)
            {
                if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
                var s = Shader.Find(name);
                if (s != null)
                {
                    cache[name] = s;
                    return s;
                }
            }

            Debug.LogWarning($"[RuntimeShaders] Shader bulunamadı: {string.Join(", ", names)}. Sprites/Default kullanılıyor.");
            return Shader.Find(SpritesDefault);
        }

        public static Material CreateMaterial(Color color, params string[] shaderNames)
        {
            var mat = new Material(Find(shaderNames));
            mat.color = color;
            return mat;
        }

        /// <summary>
        /// AddComponent ile eklenen ParticleSystem'in renderer'ında materyal olmaz (pembe kareler çizer).
        /// Bu yardımcı paylaşımlı bir parçacık materyali atar.
        /// </summary>
        public static void ApplyParticleMaterial(ParticleSystem ps, Color color)
        {
            if (ps == null) return;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            if (!particleMaterials.TryGetValue(color, out var mat) || mat == null)
            {
                mat = new Material(Find(ParticlesUnlit, SpritesDefault));
                mat.color = color;
                particleMaterials[color] = mat;
            }
            renderer.sharedMaterial = mat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            cache.Clear();
            particleMaterials.Clear();
        }
    }
}
