using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulsRPG.Core;
using SoulsRPG.Enemy;
using SoulsRPG.Environment;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Örnek bölge: Kale Avlusu'nun doğusunda "Eski Mezarlık".
    /// Avlunun doğu duvarında bir geçit açılır, dar bir koridor mezarlığa bağlanır (yükleme bu geçişte gizlenir).
    /// Mezarlık kendi sahnesinde (Region_EskiMezarlik): zemin, kale duvarları ve kuleleri, mezar taşları,
    /// kuru ağaçlar, harabe şapel, meşaleler, iki Kara Şövalye ve "Mezarlık Ateşi" bonfire'ı.
    /// </summary>
    public static class ExampleGraveyardBuilder
    {
        private const string SceneName = "Region_EskiMezarlik";
        private const string DisplayName = "Eski Mezarlık";

        private static readonly Vector3 Center = new Vector3(70f, 0f, 0f);
        private const float Half = 22f;              // duvarların merkeze uzaklığı (44 m x 44 m alan)
        private const float CourtyardWallX = 26f;    // avlunun doğu duvarı
        private const float CourtyardFloorEndX = 30f;
        private const float GraveyardFloorStartX = 46f;
        private const float Scale = DarkSoulsArenaBuilder.ModelScaleMultiplier;

        private const string PrefabFolder = "Assets/Advance Studios/Medieval Castle/Prefabs/";
        private const string MaterialFolder = "Assets/_Project/Materials/World";

        [MenuItem("SoulsRPG/🌍 Örnek Bölge Oluştur: Eski Mezarlık", false, 24)]
        public static void Build()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!WorldRegionTools.EnsureWorldSplit()) return;

            if (File.Exists(WorldRegionTools.ScenePath(SceneName)))
            {
                EditorUtility.DisplayDialog("Eski Mezarlık", $"{SceneName} zaten var. Yeniden kurmak için önce sahneyi sil ve WorldMap'ten kaydını kaldır.", "Tamam");
                return;
            }

            bool ok = EditorUtility.DisplayDialog("Eski Mezarlık",
                "Kale Avlusu'nun doğusuna örnek bir bölge kurulacak:\n\n" +
                "• Avlunun doğu duvarında bir geçit açılır (duvar parçaları silinmez, sadece kapatılır)\n" +
                "• Dar bir koridor yeni bölgeye bağlanır\n" +
                $"• {SceneName} sahnesi: zemin, duvarlar, kuleler, mezar taşları, kuru ağaçlar,\n" +
                "  harabe şapel, meşaleler, 2 Kara Şövalye ve 'Mezarlık Ateşi'\n\n" +
                "Sahne Build listesine ve WorldMap'e eklenir.",
                "Kur", "Vazgeç");
            if (!ok) return;

            WorldRegionTools.OpenWholeWorld();
            var courtyard = SceneManager.GetSceneByName(WorldMap.CourtyardScene);
            if (!courtyard.IsValid() || !courtyard.isLoaded)
            {
                EditorUtility.DisplayDialog("Eski Mezarlık", $"{WorldMap.CourtyardScene} açılamadı.", "Tamam");
                return;
            }

            // 1. Avlunun doğu duvarında geçit aç
            OpenWallGap(courtyard, out float gapMin, out float gapMax);
            float gapCenter = (gapMin + gapMax) * 0.5f;

            // Düşman şablonu (avludaki Kara Şövalye)
            EnemyController enemyTemplate = null;
            foreach (var courtyardRoot in courtyard.GetRootGameObjects())
            {
                enemyTemplate = courtyardRoot.GetComponentInChildren<EnemyController>(true);
                if (enemyTemplate != null) break;
            }

            // 2. Bölge sahnesi (aktif sahne olur; aşağıda oluşturulan her şey buraya düşer)
            if (!WorldRegionTools.TryCreateRegion(SceneName, DisplayName, Center, out var region)) return;

            var root = new GameObject("--- ESKİ MEZARLIK ---");
            var stone = GetOrCreateMaterial("M_GraveyardStone", new Color(0.42f, 0.42f, 0.44f), "CastleWallTexture.png", new Vector2(2f, 2f));
            var ground = GetOrCreateMaterial("M_GraveyardGround", new Color(0.30f, 0.33f, 0.28f), "CastleTexture.png", new Vector2(10f, 10f));
            var wood = GetOrCreateMaterial("M_DeadWood", new Color(0.16f, 0.12f, 0.09f), null, Vector2.one);

            BuildCorridor(root.transform, gapMin, gapMax, gapCenter, ground, stone);
            BuildGround(root.transform, ground);
            BuildPerimeter(root.transform, gapMin, gapMax, stone);
            BuildTombstones(root.transform, stone);
            BuildDeadTrees(root.transform, wood);
            BuildRuins(root.transform);
            BuildTorches(root.transform);
            BuildBonfire(root.transform, gapCenter);
            BuildEnemies(enemyTemplate);

            // 3. Sınırlar, kayıt
            WorldRegionTools.RecalculateBounds(SceneName);
            EditorSceneManager.MarkSceneDirty(region);
            EditorSceneManager.MarkSceneDirty(courtyard);
            EditorSceneManager.SaveScene(region);
            EditorSceneManager.SaveScene(courtyard);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorUtility.DisplayDialog("Eski Mezarlık",
                "Kuruldu.\n\n" +
                $"• Bölge: {SceneName} (şu an aktif sahne)\n" +
                "• Avludan doğu duvarındaki geçitle girilir\n\n" +
                "Core'dan Play'e bas ve F4 ile bölge panelini aç: mezarlığa yürürken yeni bölgenin yüklendiğini, " +
                "derinlere ilerledikçe avlunun ve boss bölgesinin boşaltıldığını göreceksin.",
                "Tamam");
        }

        // ------------------------------------------------------------------

        /// <summary>Avlunun doğu duvarında koridorun geçeceği yerdeki duvar parçalarını kapatır (silmez).</summary>
        private static void OpenWallGap(Scene courtyard, out float gapMin, out float gapMax)
        {
            var opening = new Bounds(new Vector3(CourtyardWallX, 2.5f, 0f), new Vector3(6f, 5f, 4f));
            gapMin = float.MaxValue;
            gapMax = float.MinValue;
            int closed = 0;

            foreach (var root in courtyard.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.gameObject.activeInHierarchy) continue;
                    string n = t.name.ToLowerInvariant();
                    if (!n.StartsWith("wall")) continue;
                    if (t.parent != null && t.parent.name.ToLowerInvariant().StartsWith("wall")) continue; // segmentin içindeki parçalar

                    var b = RendererBounds(t.gameObject);
                    if (!b.HasValue || !b.Value.Intersects(opening)) continue;

                    Undo.RecordObject(t.gameObject, "Open Graveyard Gate");
                    t.gameObject.SetActive(false);
                    gapMin = Mathf.Min(gapMin, b.Value.min.z);
                    gapMax = Mathf.Max(gapMax, b.Value.max.z);
                    closed++;
                }
            }

            if (closed == 0)
            {
                gapMin = -2.5f;
                gapMax = 2.5f;
            }

            // Koridor en az 4 m genişliğinde olsun
            if (gapMax - gapMin < 4f)
            {
                float c = (gapMin + gapMax) * 0.5f;
                gapMin = c - 2f;
                gapMax = c + 2f;
            }

            Debug.Log($"[SoulsRPG] Avlu doğu duvarında {closed} parça kapatıldı, geçit z = {gapMin:F1} … {gapMax:F1}");
        }

        private static void BuildCorridor(Transform parent, float gapMin, float gapMax, float gapCenter, Material ground, Material stone)
        {
            var corridor = new GameObject("Gecit_Koridoru");
            corridor.transform.SetParent(parent, false);

            float width = gapMax - gapMin;
            float length = GraveyardFloorStartX - CourtyardFloorEndX;

            // Zemin: avlu zemininin bittiği yerden mezarlık zeminine kadar (üst üste binmeden, titreme olmasın)
            Box(corridor.transform, "Koridor_Zemini",
                new Vector3(CourtyardFloorEndX + length * 0.5f, -0.5f, gapCenter),
                new Vector3(length, 1f, width + 2f), ground);

            // Yan duvarlar (geçidi görüşe kapatır; dışarısı boşluk). Mezarlık duvarının içine kadar uzanır ki
            // iki duvar arasında dışarı sızılacak aralık kalmasın.
            float wallLen = GraveyardFloorStartX + 5f - (CourtyardWallX - 1f);
            float wallCx = (CourtyardWallX - 1f) + wallLen * 0.5f;
            Box(corridor.transform, "Koridor_Duvari_Kuzey", new Vector3(wallCx, 3f, gapMax + 0.5f), new Vector3(wallLen, 6f, 1f), stone);
            Box(corridor.transform, "Koridor_Duvari_Guney", new Vector3(wallCx, 3f, gapMin - 0.5f), new Vector3(wallLen, 6f, 1f), stone);

            // Kemerler (koridor hissi)
            foreach (float x in new[] { 34f, 40f })
            {
                Box(corridor.transform, "Kemer_Ust", new Vector3(x, 5.5f, gapCenter), new Vector3(1.2f, 1f, width + 2f), stone);
            }

            // Koridorda tek loş meşale (gölgesiz: ucuz)
            Torch(corridor.transform, new Vector3(37f, 3.5f, gapMax - 0.3f), false);
        }

        private static void BuildGround(Transform parent, Material ground)
        {
            float size = (Center.x + Half + 2f) - GraveyardFloorStartX;
            Box(parent, "Mezarlik_Zemini",
                new Vector3(GraveyardFloorStartX + size * 0.5f, -0.5f, Center.z),
                new Vector3(size, 1f, (Half + 2f) * 2f), ground);
        }

        private static void BuildPerimeter(Transform parent, float gapMin, float gapMax, Material stone)
        {
            var walls = new GameObject("Mezarlik_Duvarlari");
            walls.transform.SetParent(parent, false);

            var wallPrefab = Load("Wall");
            float minX = Center.x - Half, maxX = Center.x + Half, minZ = Center.z - Half, maxZ = Center.z + Half;
            var gate = new Bounds(new Vector3(minX, 2.5f, (gapMin + gapMax) * 0.5f), new Vector3(6f, 5f, (gapMax - gapMin) + 1f));

            if (wallPrefab != null)
            {
                float segment = MeasureSegmentLength(wallPrefab);
                WallLine(walls.transform, wallPrefab, new Vector3(minX, 0f, maxZ), new Vector3(maxX, 0f, maxZ), segment, 0f, gate);
                WallLine(walls.transform, wallPrefab, new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ), segment, 180f, gate);
                WallLine(walls.transform, wallPrefab, new Vector3(minX, 0f, minZ), new Vector3(minX, 0f, maxZ), segment, 270f, gate);
                WallLine(walls.transform, wallPrefab, new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), segment, 90f, gate);
            }
            else
            {
                // Prefab yoksa basit taş duvarlar (girişte boşluk)
                Box(walls.transform, "Duvar_K", new Vector3(Center.x, 3f, maxZ), new Vector3(Half * 2f, 6f, 1f), stone);
                Box(walls.transform, "Duvar_G", new Vector3(Center.x, 3f, minZ), new Vector3(Half * 2f, 6f, 1f), stone);
                Box(walls.transform, "Duvar_D", new Vector3(maxX, 3f, Center.z), new Vector3(1f, 6f, Half * 2f), stone);
                float northLen = maxZ - gapMax, southLen = gapMin - minZ;
                Box(walls.transform, "Duvar_B1", new Vector3(minX, 3f, gapMax + northLen * 0.5f), new Vector3(1f, 6f, northLen), stone);
                Box(walls.transform, "Duvar_B2", new Vector3(minX, 3f, minZ + southLen * 0.5f), new Vector3(1f, 6f, southLen), stone);
            }

            // Köşe kuleleri
            var towerA = Load("Tower A");
            var towerB = Load("Tower B") ?? towerA;
            if (towerA != null)
            {
                Place(walls.transform, towerA, new Vector3(minX, 0f, maxZ), 0f, "Kule_KB");
                Place(walls.transform, towerB, new Vector3(maxX, 0f, maxZ), 90f, "Kule_KD");
                Place(walls.transform, towerA, new Vector3(minX, 0f, minZ), 270f, "Kule_GB");
                Place(walls.transform, towerB, new Vector3(maxX, 0f, minZ), 180f, "Kule_GD");
            }
        }

        private static void WallLine(Transform parent, GameObject prefab, Vector3 start, Vector3 end, float segment, float yaw, Bounds gate)
        {
            float distance = Vector3.Distance(start, end);
            int count = Mathf.Max(1, Mathf.RoundToInt(distance / segment));
            Vector3 dir = (end - start).normalized;
            for (int i = 0; i <= count; i++)
            {
                var go = Place(parent, prefab, start + dir * (i * segment), yaw, "Wall_Segment");
                var b = RendererBounds(go);
                if (b.HasValue && b.Value.Intersects(gate))
                {
                    Object.DestroyImmediate(go); // giriş kapısının önü açık kalsın
                }
            }
        }

        private static void BuildTombstones(Transform parent, Material stone)
        {
            var graves = new GameObject("Mezar_Taslari");
            graves.transform.SetParent(parent, false);
            var rng = new System.Random(7);

            for (float x = Center.x - 14f; x <= Center.x + 12f; x += 3.5f)
            {
                for (float z = Center.z - 16f; z <= Center.z + 16f; z += 3.2f)
                {
                    if (Mathf.Abs(z - Center.z) < 3.5f) continue;        // orta yol açık
                    if (x > Center.x + 8f && z > Center.z + 6f) continue; // şapel alanı
                    if (rng.NextDouble() < 0.25) continue;               // seyrek, düzensiz dizilim

                    float jx = (float)(rng.NextDouble() - 0.5) * 0.8f;
                    float jz = (float)(rng.NextDouble() - 0.5) * 0.8f;
                    float yaw = (float)(rng.NextDouble() - 0.5) * 24f;
                    float tilt = (float)(rng.NextDouble() - 0.5) * 14f;
                    var pos = new Vector3(x + jx, 0f, z + jz);

                    if (rng.NextDouble() < 0.3)
                    {
                        // Haç
                        var cross = new GameObject("Mezar_Haci");
                        cross.transform.SetParent(graves.transform, false);
                        cross.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt, 90f + yaw, 0f));
                        Box(cross.transform, "Dikey", cross.transform.TransformPoint(new Vector3(0f, 0.9f, 0f)), new Vector3(0.22f, 1.8f, 0.22f), stone, true);
                        Box(cross.transform, "Yatay", cross.transform.TransformPoint(new Vector3(0f, 1.3f, 0f)), new Vector3(0.22f, 0.22f, 0.9f), stone, true);
                        // Çarpışma: tek basit kutu
                        var col = cross.AddComponent<BoxCollider>();
                        col.center = new Vector3(0f, 0.9f, 0f);
                        col.size = new Vector3(0.3f, 1.8f, 0.9f);
                    }
                    else
                    {
                        var slab = Box(graves.transform, "Mezar_Tasi", pos + Vector3.up * 0.6f, new Vector3(0.9f, 1.2f, 0.25f), stone);
                        slab.transform.rotation = Quaternion.Euler(tilt, 90f + yaw, 0f);
                    }
                }
            }
        }

        private static void BuildDeadTrees(Transform parent, Material wood)
        {
            var trees = new GameObject("Kuru_Agaclar");
            trees.transform.SetParent(parent, false);
            var positions = new[]
            {
                new Vector3(52f, 0f, 15f), new Vector3(55f, 0f, -17f), new Vector3(68f, 0f, 18f),
                new Vector3(74f, 0f, -18f), new Vector3(88f, 0f, -6f), new Vector3(60f, 0f, -6f),
            };
            var rng = new System.Random(11);
            foreach (var p in positions)
            {
                var tree = new GameObject("Kuru_Agac");
                tree.transform.SetParent(trees.transform, false);
                tree.transform.position = p;

                float h = 4.5f + (float)rng.NextDouble() * 2f;
                var trunk = Cylinder(tree.transform, "Govde", p + Vector3.up * (h * 0.5f), new Vector3(0.5f, h * 0.5f, 0.5f), wood, false);
                trunk.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 8f, 0f, (float)(rng.NextDouble() - 0.5) * 8f);

                for (int i = 0; i < 3; i++)
                {
                    float y = h * (0.55f + 0.15f * i);
                    float yaw = (float)rng.NextDouble() * 360f;
                    var branch = Cylinder(tree.transform, "Dal", p + Vector3.up * y, new Vector3(0.18f, 1.1f, 0.18f), wood, true);
                    branch.transform.rotation = Quaternion.Euler(0f, yaw, 55f + (float)rng.NextDouble() * 20f);
                    branch.transform.position += branch.transform.up * 1.0f;
                }
            }
        }

        private static void BuildRuins(Transform parent)
        {
            var ruins = new GameObject("Harabeler");
            ruins.transform.SetParent(parent, false);

            var chapel = Load("House A") ?? Load("House B");
            if (chapel != null) Place(ruins.transform, chapel, new Vector3(Center.x + 14f, 0f, Center.z + 13f), 270f, "Harabe_Sapel");

            var column = Load("Column");
            if (column != null)
            {
                foreach (var p in new[] { new Vector3(62f, 0f, 9f), new Vector3(62f, 0f, -9f), new Vector3(78f, 0f, 9f), new Vector3(78f, 0f, -9f) })
                {
                    Place(ruins.transform, column, p, 0f, "Yikik_Sutun");
                }
            }
        }

        private static void BuildTorches(Transform parent)
        {
            var torches = new GameObject("Mesaleler");
            torches.transform.SetParent(parent, false);

            // Performans: sadece 2 meşale gölge düşürür (gölgeli nokta ışıkları pahalıdır)
            Torch(torches.transform, new Vector3(62f, 4.2f, 9f), true);
            Torch(torches.transform, new Vector3(78f, 4.2f, -9f), true);
            Torch(torches.transform, new Vector3(62f, 4.2f, -9f), false);
            Torch(torches.transform, new Vector3(78f, 4.2f, 9f), false);
            Torch(torches.transform, new Vector3(Center.x + 14f, 3.5f, Center.z + 8f), false);
        }

        private static void BuildBonfire(Transform parent, float gapCenter)
        {
            // Bölgeye sahnede kalıcı yerleştirilmiş bonfire (gelecekteki bölgelerde de böyle eklenir)
            var go = new GameObject("Bonfire_mezarlik_atesi");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(Center.x - 12f, 0.09f, gapCenter), Quaternion.Euler(0f, 90f, 0f));
            var bonfire = go.AddComponent<Bonfire>();
            bonfire.Initialize("mezarlik_atesi", "Mezarlık Ateşi");
            EditorUtility.SetDirty(bonfire);
        }

        private static void BuildEnemies(EnemyController template)
        {
            if (template == null)
            {
                Debug.LogWarning("[SoulsRPG] Avluda Kara Şövalye bulunamadı; mezarlığa düşman eklenmedi.");
                return;
            }

            var spots = new[]
            {
                (new Vector3(Center.x + 8f, template.transform.position.y, Center.z + 7f), "Enemy_DarkKnight_Mezarlik_1"),
                (new Vector3(Center.x + 10f, template.transform.position.y, Center.z - 9f), "Enemy_DarkKnight_Mezarlik_2"),
            };

            var regionScene = SceneManager.GetActiveScene();
            foreach (var (pos, name) in spots)
            {
                var clone = Object.Instantiate(template.gameObject);
                clone.name = name;
                clone.transform.SetParent(null);
                clone.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 270f, 0f)); // girişe (batıya) bakar
                if (clone.scene != regionScene) SceneManager.MoveGameObjectToScene(clone, regionScene);
            }
        }

        // ------------------------------------------------------------------ yardımcılar

        private static GameObject Load(string prefabName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefabName + ".prefab");

        private static GameObject Place(Transform parent, GameObject prefab, Vector3 pos, float yaw, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (go == null) go = Object.Instantiate(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * Scale;
            return go;
        }

        private static float MeasureSegmentLength(GameObject wallPrefab)
        {
            var temp = Place(null, wallPrefab, new Vector3(0f, -500f, 0f), 0f, "temp");
            var b = RendererBounds(temp);
            Object.DestroyImmediate(temp);
            float len = b.HasValue ? Mathf.Max(b.Value.size.x, b.Value.size.z) : 0f;
            return len > 1f ? len : 1.75f * Scale;
        }

        private static Bounds? RendererBounds(GameObject go)
        {
            Bounds? result = null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.bounds.size == Vector3.zero) continue;
                if (result.HasValue) { var b = result.Value; b.Encapsulate(r.bounds); result = b; }
                else result = r.bounds;
            }
            return result;
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool noCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true; // statik batching + occlusion için
            if (noCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool noCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            if (noCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Torch(Transform parent, Vector3 pos, bool shadows)
        {
            var go = new GameObject("Mesale_Isigi");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.55f, 0.18f);
            light.range = 12f;
            light.intensity = 2.4f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            go.AddComponent<TorchFlicker>();
        }

        private static Material GetOrCreateMaterial(string name, Color color, string textureFile, Vector2 tiling)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Standard")) { name = name, color = color };
            mat.SetFloat("_Glossiness", 0.08f);
            if (!string.IsNullOrEmpty(textureFile))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Advance Studios/Medieval Castle/Art/Textures/" + textureFile);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    mat.mainTextureScale = tiling;
                }
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
