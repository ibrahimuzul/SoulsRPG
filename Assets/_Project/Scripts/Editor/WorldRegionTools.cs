using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulsRPG.Core;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Bölgelere ayrılmış dünya için günlük editör araçları:
    ///  - Tüm Dünyayı Aç: Core + bütün bölgeler birlikte, tek dünya gibi düzenlemek için
    ///  - Yeni Bölge Oluştur: sahneyi oluşturur, Build listesine ve WorldMap'e ekler, aktif sahne yapar
    /// </summary>
    public static class WorldRegionTools
    {
        public const string RegionPrefix = "Region_";

        public static WorldMap LoadMap() => AssetDatabase.LoadAssetAtPath<WorldMap>(WorldSplitTool.MapAssetPath);

        /// <summary>Dünya bölgelere ayrılmadıysa kullanıcıya sorup ayırır. Ayrılmışsa (veya ayrıldıysa) true.</summary>
        public static bool EnsureWorldSplit()
        {
            if (LoadMap() != null) return true;

            bool split = EditorUtility.DisplayDialog("Dünya",
                "Dünya henüz Core + bölgelere ayrılmamış. Bölge eklemek için önce ayrılması gerekiyor.\n\n" +
                "Açık sahne (SoulsTestArena) şimdi ayrılsın mı? Orijinal dosya değiştirilmez.",
                "Ayır", "Vazgeç");
            if (!split) return false;

            WorldSplitTool.SplitOpenScene();
            return LoadMap() != null;
        }

        [MenuItem("SoulsRPG/🌍 Tüm Dünyayı Aç", false, 22)]
        public static void OpenWholeWorldMenu()
        {
            if (EditorApplication.isPlaying) return;
            if (LoadMap() == null)
            {
                EditorUtility.DisplayDialog("Dünya", "Önce 'Sahneyi Core + Bölgelere Ayır' komutunu çalıştır.", "Tamam");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            OpenWholeWorld();
        }

        /// <summary>Core'u ve WorldMap'teki tüm bölgeleri birlikte açar (zaten açık olanlara dokunmaz). Core aktif kalır.</summary>
        public static Scene OpenWholeWorld()
        {
            var map = LoadMap();
            Scene core = SceneManager.GetSceneByName(map.coreScene);
            if (!core.IsValid() || !core.isLoaded)
            {
                core = EditorSceneManager.OpenScene(ScenePath(map.coreScene), OpenSceneMode.Single);
            }

            foreach (var region in map.regions)
            {
                var s = SceneManager.GetSceneByName(region.sceneName);
                if (s.IsValid() && s.isLoaded) continue;
                string path = ScenePath(region.sceneName);
                if (File.Exists(path)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                else Debug.LogWarning($"[SoulsRPG] WorldMap'teki '{region.sceneName}' sahnesi bulunamadı: {path}");
            }

            SceneManager.SetActiveScene(core);
            return core;
        }

        public static string ScenePath(string sceneName) => $"{WorldSplitTool.WorldFolder}/{sceneName}.unity";

        [MenuItem("SoulsRPG/🌍 Yeni Bölge Oluştur...", false, 23)]
        public static void NewRegionMenu()
        {
            if (EditorApplication.isPlaying) return;
            if (!EnsureWorldSplit()) return;
            NewRegionWindow.Open();
        }

        /// <summary>
        /// Yeni boş bölge sahnesi: Scenes/World'e kaydedilir, Build listesine ve WorldMap'e eklenir,
        /// Core'un ışık/sis ayarları kopyalanır ve aktif sahne yapılır (yeni objeler buraya düşer).
        /// </summary>
        public static bool TryCreateRegion(string sceneName, string displayName, Vector3 approxCenter, out Scene scene)
        {
            scene = default;
            if (!sceneName.StartsWith(RegionPrefix)) sceneName = RegionPrefix + sceneName;
            string path = ScenePath(sceneName);
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("Yeni Bölge", $"'{sceneName}' zaten var.", "Tamam");
                return false;
            }

            var map = LoadMap();
            var coreCheck = SceneManager.GetSceneByName(map.coreScene);
            if ((!coreCheck.IsValid() || !coreCheck.isLoaded) && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false; // Core açılırken kaydedilmemiş değişiklikler kaybolmasın
            }
            var core = OpenWholeWorld();

            // Core'un atmosferini yakala
            SceneManager.SetActiveScene(core);
            var lighting = WorldSplitTool.LightingSnapshot.Capture();

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, path);
            SceneManager.SetActiveScene(scene);
            lighting.Apply(false);
            EditorSceneManager.SaveScene(scene);

            // Build listesi
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            // WorldMap (sınırlar içerik eklendikçe 'Bölge Sınırlarını Yeniden Hesapla' ile güncellenir)
            map.regions.RemoveAll(r => r.sceneName == sceneName);
            map.regions.Add(new WorldMap.Region
            {
                sceneName = sceneName,
                displayName = string.IsNullOrEmpty(displayName) ? sceneName : displayName,
                bounds = new Bounds(approxCenter, new Vector3(40f, 20f, 40f)),
            });
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SoulsRPG] Yeni bölge '{sceneName}' oluşturuldu ve aktif sahne yapıldı. Eklediğin objeler bu sahneye kaydedilir.");
            return true;
        }

        /// <summary>Tek bir bölgenin sınırlarını sahne içeriğinden yeniden hesaplar.</summary>
        public static void RecalculateBounds(string sceneName)
        {
            var map = LoadMap();
            var region = map.Find(sceneName);
            var scene = SceneManager.GetSceneByName(sceneName);
            if (region == null || !scene.IsValid() || !scene.isLoaded) return;
            region.bounds = WorldSplitTool.ComputeBounds(scene);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
        }
    }

    public class NewRegionWindow : EditorWindow
    {
        private string sceneName = "Region_YeniBolge";
        private string displayName = "Yeni Bölge";

        public static void Open()
        {
            var w = GetWindow<NewRegionWindow>(true, "Yeni Bölge Oluştur", true);
            w.minSize = new Vector2(380f, 150f);
            w.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Sahne Scenes/World klasörüne kaydedilir, Build listesine ve WorldMap'e eklenir. " +
                                    "Aktif sahne yapılır: Hierarchy'de oluşturduğun objeler bu bölgeye kaydedilir.", MessageType.Info);
            sceneName = EditorGUILayout.TextField("Sahne adı", sceneName);
            displayName = EditorGUILayout.TextField("Görünen ad", displayName);

            EditorGUILayout.Space();
            if (GUILayout.Button("Oluştur", GUILayout.Height(28)))
            {
                Vector3 center = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
                if (WorldRegionTools.TryCreateRegion(sceneName.Trim(), displayName.Trim(), center, out _))
                {
                    Close();
                }
            }
        }
    }
}
