using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using SoulsRPG.Audio;
using SoulsRPG.Boss;
using SoulsRPG.CameraSystem;
using SoulsRPG.Core;
using SoulsRPG.Environment;
using SoulsRPG.Player;
using SoulsRPG.UI;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Açık tek sahneyi (SoulsTestArena) Core + bölge sahnelerine ayırır:
    ///  - Core: Player, kamera, ışık, HUD canvas, EventSystem, ses ve HUD yöneticileri, WorldStreamer
    ///  - Region_KaleAvlusu: arena çevresi, kuklalar/düşmanlar, Kale Avlusu Ateşi
    ///  - Region_MuhafizKapisi: boss ve Muhafız Kapısı Ateşi
    /// Orijinal sahne dosyası DEĞİŞTİRİLMEZ (objeler bellekte taşınır, kaynak sahne kaydedilmeden kapatılır).
    /// </summary>
    public static class WorldSplitTool
    {
        private const string SourceSceneName = "SoulsTestArena";
        internal const string WorldFolder = "Assets/_Project/Scenes/World";
        internal const string ResourcesFolder = "Assets/_Project/Resources";
        internal static string CorePath => $"{WorldFolder}/{WorldMap.CoreSceneName}.unity";
        private static string CourtyardPath => $"{WorldFolder}/{WorldMap.CourtyardScene}.unity";
        private static string BossGatePath => $"{WorldFolder}/{WorldMap.BossGateScene}.unity";
        internal static string MapAssetPath => $"{ResourcesFolder}/{WorldMap.ResourcePath}.asset";

        [MenuItem("SoulsRPG/🌍 Sahneyi Core + Bölgelere Ayır", false, 20)]
        public static void SplitOpenScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Dünyayı Ayır", "Önce Play modundan çık.", "Tamam");
                return;
            }
            bool mapExists = File.Exists(MapAssetPath);
            bool anySceneFile = File.Exists(CorePath) || File.Exists(CourtyardPath) || File.Exists(BossGatePath);
            if (mapExists && File.Exists(CorePath))
            {
                EditorUtility.DisplayDialog("Dünyayı Ayır",
                    $"Dünya zaten ayrılmış ({CorePath} ve WorldMap mevcut).\nYeniden ayırmak istiyorsan önce Scenes/World klasörünü ve Resources/WorldMap'i sil.", "Tamam");
                return;
            }
            if (anySceneFile)
            {
                // Önceki deneme yarıda kalmış (WorldMap hiç yazılmamış): boş sahne dosyalarını temizle
                bool clean = EditorUtility.DisplayDialog("Dünyayı Ayır",
                    "Yarıda kalmış önceki bir ayırma denemesinden sahne dosyaları bulundu (WorldMap yok).\nBunlar silinip baştan başlansın mı?",
                    "Temizle ve devam et", "Vazgeç");
                if (!clean) return;
                foreach (var p in new[] { CorePath, CourtyardPath, BossGatePath })
                {
                    var loaded = SceneManager.GetSceneByPath(p);
                    if (loaded.IsValid()) EditorSceneManager.CloseScene(loaded, true);
                    if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
                }
            }
            // Ayrılacak sahne: adı SoulsTestArena olan yüklü sahne, yoksa aktif sahne
            var source = default(Scene);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == SourceSceneName) { source = s; break; }
            }
            if (!source.IsValid()) source = SceneManager.GetActiveScene();

            if (!source.IsValid() || !source.isLoaded || string.IsNullOrEmpty(source.path))
            {
                EditorUtility.DisplayDialog("Dünyayı Ayır", $"Ayrılacak sahne bulunamadı. {SourceSceneName} sahnesini aç ve kaydet.", "Tamam");
                return;
            }

            // Hierarchy'de başka gerçek (yüklü ve içi dolu) sahne var mı? Boş/yüklenmemiş olanlar yok sayılır.
            var others = new List<Scene>();
            var report0 = new StringBuilder();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s == source) continue;
                bool real = s.isLoaded && s.rootCount > 0;
                report0.AppendLine($"• {(string.IsNullOrEmpty(s.name) ? "(isimsiz)" : s.name)} — {(s.isLoaded ? "yüklü" : "yüklenmemiş")}, {s.rootCount} obje");
                if (real) others.Add(s);
            }

            if (others.Count > 0)
            {
                bool close = EditorUtility.DisplayDialog("Dünyayı Ayır",
                    $"'{source.name}' dışında açık sahneler var:\n\n{report0}\nBunlar Hierarchy'den kapatılsın mı? (Dosyaları silinmez; kaydedilmemiş değişiklik varsa önce sorulur.)",
                    "Kapat ve devam et", "Vazgeç");
                if (!close) return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }

            // Ayrılacak sahne dışındakileri (boş/yüklenmemiş olanlar dahil) Hierarchy'den çıkar
            SceneManager.SetActiveScene(source);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != source) EditorSceneManager.CloseScene(s, true);
            }

            if (source.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return; // kullanıcı vazgeçti
            }

            bool ok = EditorUtility.DisplayDialog("Dünyayı Ayır",
                $"'{source.name}' sahnesi şu sahnelere ayrılacak:\n\n" +
                $"• {WorldMap.CoreSceneName}: oyuncu, kamera, ışık, HUD, yöneticiler\n" +
                $"• {WorldMap.CourtyardScene}: arena, kuklalar/düşmanlar, Kale Avlusu Ateşi\n" +
                $"• {WorldMap.BossGateScene}: boss, Muhafız Kapısı Ateşi\n\n" +
                $"Yeni sahneler {WorldFolder} klasörüne kaydedilir ve Build listesine eklenir.\n" +
                $"Orijinal '{source.name}' dosyası değiştirilmez (yedek olarak kalır).",
                "Ayır", "Vazgeç");
            if (!ok) return;

            try
            {
                DoSplit(source);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Dünyayı Ayır",
                    "Ayırma sırasında hata oluştu (ayrıntı Console'da).\n\n" +
                    $"Orijinal '{source.name}' dosyası değişmedi. Yapılacak: sahneyi KAYDETMEDEN yeniden aç " +
                    "(File > Open Scene → Don't Save) ve komutu tekrar çalıştır; yarım kalan dosyaları araç temizler.",
                    "Tamam");
            }
        }

        private static void DoSplit(Scene source)
        {
            // 1. Sahneye kalıcı konmamış bonfire'ları oluştur (doğru bölgeye taşınsınlar)
            foreach (var def in Bonfire.DefaultBonfires)
            {
                if (!Bonfire.ExistsFor(def)) Bonfire.Create(def);
            }

            // 2. Işık/sis ayarlarını yakala (Core aktif sahne olacak, ayarlar oraya taşınır)
            var lighting = LightingSnapshot.Capture();

            // 3. Yeni sahneler
            Directory.CreateDirectory(WorldFolder);
            // Unity, kaydedilmemiş isimsiz sahne varken ikinci bir yeni sahne açtırmaz:
            // her sahne oluşturulur oluşturulmaz kendi dosyasına kaydedilir.
            var core = CreateSavedScene(CorePath);
            var courtyard = CreateSavedScene(CourtyardPath);
            var bossGate = CreateSavedScene(BossGatePath);

            // 4. Kök objeleri sınıflandırıp taşı
            var report = new StringBuilder();
            foreach (var root in source.GetRootGameObjects())
            {
                var target = Classify(root, core, courtyard, bossGate);
                SceneManager.MoveGameObjectToScene(root, target);
                report.AppendLine($"  {root.name} → {SceneLabel(target, core, courtyard, bossGate)}");
            }

            // 5. Core: bölge akış yöneticisi + HUD canvas işareti
            var streamerGo = new GameObject("--- WORLD STREAMER ---");
            streamerGo.AddComponent<WorldStreamer>();
            SceneManager.MoveGameObjectToScene(streamerGo, core);
            MarkHudCanvas(core);

            // 6. Bölge haritası (sınırlar içerikten hesaplanır)
            var map = CreateOrUpdateMap(courtyard, bossGate);

            // 7. Işık/sis ayarları. Oyunda Core aktif olduğu için onunki kullanılır; bölgelere de kopyalanır ki
            //    editörde bir bölgeyi aktif sahne yapınca Scene ekranı aynı atmosferle görünsün.
            SceneManager.SetActiveScene(courtyard);
            lighting.Apply(false);
            SceneManager.SetActiveScene(bossGate);
            lighting.Apply(false);
            SceneManager.SetActiveScene(core);
            lighting.Apply();

            // 8. Kaydet
            EditorSceneManager.SaveScene(core);
            EditorSceneManager.SaveScene(courtyard);
            EditorSceneManager.SaveScene(bossGate);

            // 9. Sahneler arası referans kontrolü (Unity desteklemez; kaydedilince boşalırlar)
            var crossRefs = FindCrossSceneReferences(new[] { core, courtyard, bossGate });

            // 10. Orijinali kaydetmeden kapat (dosya değişmez). Yolu kapatmadan önce al: kapanan sahnenin yolu boş döner.
            string sourcePath = source.path;
            EditorSceneManager.CloseScene(source, true);

            // 11. Build listesi: Core ilk sırada
            UpdateBuildSettings(sourcePath);

            AssetDatabase.SaveAssets();
            Selection.activeObject = map;

            Debug.Log("[SoulsRPG] Dünya bölgelere ayrıldı:\n" + report +
                      (crossRefs.Length > 0 ? "\nSahneler arası referanslar (elle yeniden bağlanmalı):\n" + crossRefs : ""));

            EditorUtility.DisplayDialog("Dünyayı Ayır",
                "Tamamlandı. Üç sahne birlikte açık; Core aktif sahne.\n\n" +
                "Oyunu Core sahnesinden başlat (bir bölge sahnesinden Play'e basarsan Core otomatik yüklenir).\n" +
                "Bölge sınırlarını Resources/WorldMap asset'inden düzenleyebilirsin.\n\n" +
                (crossRefs.Length > 0
                    ? "UYARI: Bazı sahneler arası referanslar koptu, ayrıntılar Console'da."
                    : "Sahneler arası kopan referans bulunmadı."),
                "Tamam");
        }

        /// <summary>
        /// Bölge sahnelerindeki objeleri taşıdıktan/ekledikten sonra WorldMap sınırlarını yeniden hesaplar.
        /// Hesaplanacak bölgelerin sahneleri editörde açık olmalı.
        /// </summary>
        [MenuItem("SoulsRPG/🌍 Bölge Sınırlarını Yeniden Hesapla", false, 21)]
        public static void RecalculateRegionBounds()
        {
            var map = AssetDatabase.LoadAssetAtPath<WorldMap>(MapAssetPath);
            if (map == null)
            {
                EditorUtility.DisplayDialog("Bölge Sınırları", "WorldMap bulunamadı. Önce 'Sahneyi Core + Bölgelere Ayır' komutunu çalıştır.", "Tamam");
                return;
            }

            var sb = new StringBuilder();
            foreach (var region in map.regions)
            {
                var scene = SceneManager.GetSceneByName(region.sceneName);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    sb.AppendLine($"• {region.sceneName}: açık değil, atlandı");
                    continue;
                }
                region.bounds = ComputeBounds(scene);
                sb.AppendLine($"• {region.sceneName}: merkez {region.bounds.center}, boyut {region.bounds.size}");
            }

            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            Debug.Log("[SoulsRPG] Bölge sınırları güncellendi:\n" + sb);
            EditorUtility.DisplayDialog("Bölge Sınırları", sb.ToString(), "Tamam");
        }

        // ------------------------------------------------------------------

        private static Scene CreateSavedScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new System.InvalidOperationException($"Sahne kaydedilemedi: {path}");
            }
            return scene;
        }

        private static Scene Classify(GameObject root, Scene core, Scene courtyard, Scene bossGate)
        {
            // Core: oyuncu ve oyun boyunca yaşayan her şey
            if (root.GetComponentInChildren<PlayerStats>(true) != null) return core;
            if (root.GetComponentInChildren<CameraManager>(true) != null) return core;
            if (root.GetComponentInChildren<Camera>(true) != null) return core;
            if (root.GetComponentInChildren<Canvas>(true) != null) return core;
            if (root.GetComponentInChildren<EventSystem>(true) != null) return core;
            if (root.GetComponentInChildren<SoulsAudioManager>(true) != null) return core;
            if (root.GetComponentInChildren<BossHealthBarHUD>(true) != null) return core;
            if (root.GetComponentInChildren<BonfireMenuHUD>(true) != null) return core;
            var light = root.GetComponent<Light>();
            if (light != null && light.type == LightType.Directional) return core;

            // Boss bölgesi
            if (root.GetComponentInChildren<BossController>(true) != null) return bossGate;

            // Bonfire'lar tanımlarındaki bölgeye
            var bonfire = root.GetComponentInChildren<Bonfire>(true);
            if (bonfire != null)
            {
                var def = Bonfire.FindDefinition(bonfire.Id);
                if (def.HasValue && def.Value.RegionScene == WorldMap.BossGateScene) return bossGate;
                return courtyard;
            }

            // Geri kalan her şey (arena çevresi, kuklalar, düşmanlar) kale avlusuna
            return courtyard;
        }

        private static string SceneLabel(Scene s, Scene core, Scene courtyard, Scene bossGate)
        {
            if (s == core) return WorldMap.CoreSceneName;
            if (s == bossGate) return WorldMap.BossGateScene;
            return WorldMap.CourtyardScene;
        }

        private static void MarkHudCanvas(Scene core)
        {
            Canvas hud = null;
            foreach (var root in core.GetRootGameObjects())
            {
                var statBar = root.GetComponentInChildren<StatBarHUD>(true);
                if (statBar != null)
                {
                    var c = statBar.GetComponentInParent<Canvas>(true);
                    if (c != null) hud = c.rootCanvas;
                    break;
                }
            }
            if (hud == null)
            {
                foreach (var root in core.GetRootGameObjects())
                {
                    hud = root.GetComponentInChildren<Canvas>(true);
                    if (hud != null) break;
                }
            }
            if (hud != null && hud.GetComponent<HudCanvas>() == null)
            {
                hud.gameObject.AddComponent<HudCanvas>();
            }
        }

        private static WorldMap CreateOrUpdateMap(Scene courtyard, Scene bossGate)
        {
            Directory.CreateDirectory(ResourcesFolder);
            var map = AssetDatabase.LoadAssetAtPath<WorldMap>(MapAssetPath);
            bool isNew = map == null;
            if (isNew) map = ScriptableObject.CreateInstance<WorldMap>();

            map.coreScene = WorldMap.CoreSceneName;
            map.defaultRegion = WorldMap.CourtyardScene;
            map.regions = new List<WorldMap.Region>
            {
                new WorldMap.Region { sceneName = WorldMap.CourtyardScene, displayName = "Kale Avlusu", bounds = ComputeBounds(courtyard) },
                new WorldMap.Region { sceneName = WorldMap.BossGateScene, displayName = "Muhafız Kapısı", bounds = ComputeBounds(bossGate) },
            };

            if (isNew) AssetDatabase.CreateAsset(map, MapAssetPath);
            else EditorUtility.SetDirty(map);
            return map;
        }

        /// <summary>
        /// Bölgenin OYNANABİLİR alanı: yürünebilen/çarpılabilen (collider) objeleri kapsayan kutu (+2 m pay).
        /// Akış kararları buna göre verilir, o yüzden şunlar hesaba katılmaz:
        ///  - kök objelerin konumu (grup objeleri genelde 0,0,0'da durur ve kutuyu haritanın ortasına kadar uzatıyordu)
        ///  - adında "Background" geçen uzak manzara objeleri (ör. Castle_Keep_Background)
        ///  - görsel renderer sınırları (collider yoksa yedek olarak kullanılır)
        /// </summary>
        internal static Bounds ComputeBounds(Scene scene)
        {
            bool hasColliders = false, hasRenderers = false;
            var colliderBounds = new Bounds();
            var rendererBounds = new Bounds();
            var pivotBounds = new Bounds();
            bool hasPivot = false;

            void Add(ref Bounds target, ref bool has, Bounds x)
            {
                if (!has) { target = x; has = true; }
                else target.Encapsulate(x);
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Collider>())
                {
                    if (!c.enabled || c.isTrigger || c.bounds.size == Vector3.zero || IsBackground(c.transform)) continue;
                    Add(ref colliderBounds, ref hasColliders, c.bounds);
                }
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r.bounds.size == Vector3.zero || IsBackground(r.transform)) continue;
                    Add(ref rendererBounds, ref hasRenderers, r.bounds);
                }
                // Görsel/collider'ı olmayan objeler (ör. runtime'da görsel kuran bonfire) için konumları
                foreach (var t in root.GetComponentsInChildren<Transform>())
                {
                    if (t == root.transform || t.GetComponent<Renderer>() != null || t.GetComponent<Collider>() != null) continue;
                    if (t.GetComponent<Bonfire>() == null && t.GetComponent<BossController>() == null && t.GetComponent<SoulsRPG.Enemy.EnemyController>() == null) continue;
                    Add(ref pivotBounds, ref hasPivot, new Bounds(t.position, Vector3.one * 2f));
                }
            }

            Bounds b;
            if (hasColliders) b = colliderBounds;
            else if (hasRenderers) b = rendererBounds;
            else b = new Bounds(Vector3.zero, Vector3.one * 20f);
            if (hasPivot) b.Encapsulate(pivotBounds);

            b.Expand(4f);
            return b;
        }

        private static bool IsBackground(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name.IndexOf("background", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string FindCrossSceneReferences(Scene[] scenes)
        {
            var sb = new StringBuilder();
            foreach (var scene in scenes)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var comp in root.GetComponentsInChildren<Component>(true))
                    {
                        if (comp == null || comp is Transform) continue;
                        var so = new SerializedObject(comp);
                        var it = so.GetIterator();
                        while (it.NextVisible(true))
                        {
                            if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;

                            GameObject targetGo = null;
                            if (it.objectReferenceValue is GameObject g) targetGo = g;
                            else if (it.objectReferenceValue is Component c) targetGo = c.gameObject;
                            if (targetGo == null || !targetGo.scene.IsValid()) continue;

                            if (targetGo.scene != scene)
                            {
                                sb.AppendLine($"  {scene.name}/{comp.gameObject.name}.{comp.GetType().Name}.{it.propertyPath} → {targetGo.scene.name}/{targetGo.name}");
                            }
                        }
                    }
                }
            }
            return sb.ToString();
        }

        private const string LegacyScenePath = "Assets/_Project/Scenes/SoulsTestArena.unity";

        /// <summary>
        /// Build listesini dünyaya göre düzenler: Core ilk sırada, sonra WorldMap'teki tüm bölgeler.
        /// Listede olan diğer sahneler korunur; eski tek sahne (SoulsTestArena) listede kalır ama kapalı olur.
        /// </summary>
        [MenuItem("SoulsRPG/🌍 Build Listesini Güncelle (Core + Bölgeler)", false, 25)]
        public static void UpdateBuildSettingsMenu()
        {
            UpdateBuildSettings(LegacyScenePath);
            var sb = new StringBuilder();
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var s = EditorBuildSettings.scenes[i];
                sb.AppendLine($"{i}. {(s.enabled ? "✔" : "✘")} {s.path}");
            }
            Debug.Log("[SoulsRPG] Build listesi güncellendi:\n" + sb);
            EditorUtility.DisplayDialog("Build Listesi", sb.ToString(), "Tamam");
        }

        private static void UpdateBuildSettings(string sourcePath)
        {
            var list = new List<EditorBuildSettingsScene>();
            var added = new HashSet<string>();

            void Add(string path, bool enabled)
            {
                if (string.IsNullOrEmpty(path) || added.Contains(path) || !File.Exists(path)) return;
                list.Add(new EditorBuildSettingsScene(path, enabled));
                added.Add(path);
            }

            // 1. Core + bölgeler (WorldMap'te sonradan eklenen bölgeler dahil)
            Add(CorePath, true);
            var map = AssetDatabase.LoadAssetAtPath<WorldMap>(MapAssetPath);
            if (map != null)
            {
                foreach (var r in map.regions) Add($"{WorldFolder}/{r.sceneName}.unity", true);
            }
            else
            {
                Add(CourtyardPath, true);
                Add(BossGatePath, true);
            }

            // 2. Listede zaten olan diğer sahneler korunur; eski tek sahne build'e girmesin
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s == null || string.IsNullOrEmpty(s.path)) continue;
                Add(s.path, s.path == sourcePath ? false : s.enabled);
            }
            Add(sourcePath, false);

            EditorBuildSettings.scenes = list.ToArray();
        }

        /// <summary>RenderSettings aktif sahneye aittir; Core aktif olacağı için ayarlar oraya kopyalanır.</summary>
        internal struct LightingSnapshot
        {
            bool fog; FogMode fogMode; Color fogColor; float fogDensity, fogStart, fogEnd;
            UnityEngine.Rendering.AmbientMode ambientMode; Color sky, equator, ground, ambientLight; float ambientIntensity;
            Material skybox; Light sun; float reflectionIntensity; UnityEngine.Rendering.DefaultReflectionMode reflectionMode;

            public static LightingSnapshot Capture()
            {
                return new LightingSnapshot
                {
                    fog = RenderSettings.fog,
                    fogMode = RenderSettings.fogMode,
                    fogColor = RenderSettings.fogColor,
                    fogDensity = RenderSettings.fogDensity,
                    fogStart = RenderSettings.fogStartDistance,
                    fogEnd = RenderSettings.fogEndDistance,
                    ambientMode = RenderSettings.ambientMode,
                    sky = RenderSettings.ambientSkyColor,
                    equator = RenderSettings.ambientEquatorColor,
                    ground = RenderSettings.ambientGroundColor,
                    ambientLight = RenderSettings.ambientLight,
                    ambientIntensity = RenderSettings.ambientIntensity,
                    skybox = RenderSettings.skybox,
                    sun = RenderSettings.sun,
                    reflectionIntensity = RenderSettings.reflectionIntensity,
                    reflectionMode = RenderSettings.defaultReflectionMode,
                };
            }

            public void Apply(bool includeSun = true)
            {
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogStartDistance = fogStart;
                RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.skybox = skybox;
                RenderSettings.sun = includeSun ? sun : null; // güneş ışığı Core'da; bölgeden ona referans verilemez
                RenderSettings.reflectionIntensity = reflectionIntensity;
                RenderSettings.defaultReflectionMode = reflectionMode;
            }
        }
    }
}
