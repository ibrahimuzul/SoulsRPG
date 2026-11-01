using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SoulsRPG.Environment;

namespace SoulsRPG.EditorTools
{
    public static class DarkSoulsArenaBuilder
    {
        private const string WallPrefabPath = "Assets/Advance Studios/Medieval Castle/Prefabs/Wall.prefab";
        private const string TowerAPath = "Assets/Advance Studios/Medieval Castle/Prefabs/Tower A.prefab";
        private const string TowerBPath = "Assets/Advance Studios/Medieval Castle/Prefabs/Tower B.prefab";
        private const string ColumnPrefabPath = "Assets/Advance Studios/Medieval Castle/Prefabs/Column.prefab";
        private const string GatePrefabPath = "Assets/Advance Studios/Medieval Castle/Prefabs/Double Door Frame.prefab";
        private const string CastleKeepPath = "Assets/Advance Studios/Medieval Castle/Prefabs/Castle.prefab";
        private const string StoneTexturePath = "Assets/Advance Studios/Medieval Castle/Art/Textures/CastleTexture.png";
        private const string WallTexturePath = "Assets/Advance Studios/Medieval Castle/Art/Textures/CastleWallTexture.png";

        // Asset modelleri orijinalinde minyatür (oyuncak ölçeğinde) yapıldığı için karakter boyutuna (1:1 insan ölçeğine) getiren çarpan
        public const float ModelScaleMultiplier = 7.0f;

        [MenuItem("SoulsRPG/🏰 2. Kale Arenasını Kur (Build Dark Souls Castle Arena)", false, 1)]
        public static void BuildCastleArena()
        {
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            var towerAPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerAPath);
            var towerBPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TowerBPath);
            var columnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ColumnPrefabPath);
            var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath);
            var castleKeepPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastleKeepPath);

            if (wallPrefab == null || towerAPrefab == null)
            {
                EditorUtility.DisplayDialog("Hata", 
                    "Medieval Castle paketinin prefabları bulunamadı!\nLütfen paketin 'Assets/Advance Studios/Medieval Castle' altına import edildiğinden emin olun.", 
                    "Tamam");
                return;
            }

            // 1. Sahnedeki eski çevreyi temizle veya hazırla
            var arenaRoot = GameObject.Find("--- ARENA ENVIRONMENT ---");
            if (arenaRoot == null)
            {
                arenaRoot = new GameObject("--- ARENA ENVIRONMENT ---");
            }
            Undo.RegisterFullObjectHierarchyUndo(arenaRoot, "Build Dark Souls Castle Arena");

            // Eski duvar ve silindir sütunları temizle
            for (int i = arenaRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = arenaRoot.transform.GetChild(i).gameObject;
                if (child.name != "Arena_Floor")
                {
                    Undo.DestroyObjectImmediate(child);
                }
            }

            // 2. Taş Zemin Kaplaması
            var floorGo = arenaRoot.transform.Find("Arena_Floor");
            if (floorGo == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Arena_Floor";
                ground.transform.parent = arenaRoot.transform;
                ground.transform.position = new Vector3(0f, -0.5f, 0f);
                ground.transform.localScale = new Vector3(60f, 1f, 60f);
                floorGo = ground.transform;
            }

            var floorMat = CreateStoneFloorMaterial();
            var floorRenderer = floorGo.GetComponent<Renderer>();
            if (floorRenderer != null && floorMat != null)
            {
                floorRenderer.sharedMaterial = floorMat;
            }

            // 3. Duvar Boyutunu Hesapla (Ölçeklenmiş Uzunluk)
            float rawWallLength = GetPrefabRawLength(wallPrefab, 1.75f);
            float wallLength = rawWallLength * ModelScaleMultiplier;
            float arenaSize = 52f; // 52m x 52m heybetli avlu
            float halfSize = arenaSize * 0.5f;

            var wallsParent = new GameObject("Castle_Walls");
            wallsParent.transform.parent = arenaRoot.transform;

            // Kuzey, Güney, Doğu, Batı Duvarlarını Dizin
            BuildWallLine(wallsParent.transform, wallPrefab, new Vector3(-halfSize, 0f, halfSize), new Vector3(halfSize, 0f, halfSize), wallLength, 0f, ModelScaleMultiplier);
            BuildWallLine(wallsParent.transform, wallPrefab, new Vector3(-halfSize, 0f, -halfSize), new Vector3(halfSize, 0f, -halfSize), wallLength, 180f, ModelScaleMultiplier);
            BuildWallLine(wallsParent.transform, wallPrefab, new Vector3(-halfSize, 0f, -halfSize), new Vector3(-halfSize, 0f, halfSize), wallLength, 270f, ModelScaleMultiplier);
            BuildWallLine(wallsParent.transform, wallPrefab, new Vector3(halfSize, 0f, -halfSize), new Vector3(halfSize, 0f, halfSize), wallLength, 90f, ModelScaleMultiplier);

            // 4. Heybetli Köşe Kuleleri (4 Köşe)
            var towersParent = new GameObject("Corner_Towers");
            towersParent.transform.parent = arenaRoot.transform;
            InstantiateAt(towersParent.transform, towerAPrefab, new Vector3(-halfSize, 0f, halfSize), Quaternion.Euler(0f, 0f, 0f), "Tower_NorthWest", ModelScaleMultiplier);
            InstantiateAt(towersParent.transform, towerBPrefab ?? towerAPrefab, new Vector3(halfSize, 0f, halfSize), Quaternion.Euler(0f, 90f, 0f), "Tower_NorthEast", ModelScaleMultiplier);
            InstantiateAt(towersParent.transform, towerAPrefab, new Vector3(-halfSize, 0f, -halfSize), Quaternion.Euler(0f, 270f, 0f), "Tower_SouthWest", ModelScaleMultiplier);
            InstantiateAt(towersParent.transform, towerBPrefab ?? towerAPrefab, new Vector3(halfSize, 0f, -halfSize), Quaternion.Euler(0f, 180f, 0f), "Tower_SouthEast", ModelScaleMultiplier);

            // 5. Kuzey Kapısı ve Arka Kale Silüeti
            if (gatePrefab != null)
            {
                InstantiateAt(wallsParent.transform, gatePrefab, new Vector3(0f, 0f, halfSize), Quaternion.Euler(0f, 180f, 0f), "Grand_Castle_Gate", ModelScaleMultiplier);
            }
            if (castleKeepPrefab != null)
            {
                // Uzaktaki dev kale hisarı
                var keep = InstantiateAt(arenaRoot.transform, castleKeepPrefab, new Vector3(0f, 0f, halfSize + 25f), Quaternion.Euler(0f, 180f, 0f), "Castle_Keep_Background", ModelScaleMultiplier * 1.5f);
            }

            // 6. Avlu Gotik Taş Sütunları ve Titreşen Meşaleler
            var pillarsParent = new GameObject("Courtyard_Pillars");
            pillarsParent.transform.parent = arenaRoot.transform;

            Vector3[] pillarPositions = new Vector3[]
            {
                new Vector3(-10f, 0f, 10f),
                new Vector3(10f, 0f, 10f),
                new Vector3(-10f, 0f, -10f),
                new Vector3(10f, 0f, -10f),
                new Vector3(-18f, 0f, 0f),
                new Vector3(18f, 0f, 0f)
            };

            foreach (var pos in pillarPositions)
            {
                GameObject pillar;
                if (columnPrefab != null)
                {
                    pillar = InstantiateAt(pillarsParent.transform, columnPrefab, pos, Quaternion.identity, "Gothic_Column", ModelScaleMultiplier);
                }
                else
                {
                    pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pillar.transform.parent = pillarsParent.transform;
                    pillar.transform.position = pos + Vector3.up * 3f;
                    pillar.transform.localScale = new Vector3(2f, 6f, 2f);
                }

                // Sütun Meşale Işığı (Sıcak Turuncu Ateş Titreşimi)
                CreateTorchLight(pillar.transform, pos + Vector3.up * 4.2f);
            }

            // 7. Dark Souls Atmosfer Aydınlatması & Sis (Moonlight & Dark Fog)
            ApplyDarkSoulsLighting();

            // 8. Prefabların varsayılan boyutlarını da 7x yapalım ki elle sürüklenen parçalar da dev gibi olsun
            ScaleAllPrefabs(ModelScaleMultiplier);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("🏰 Dark Souls Arenası Kuruldu!", 
                "Tebrikler! Arena gerçek insan ölçeğinde (7x büyütülerek) kuruldu:\n\n" +
                "• Duvarlar ve siperlikler heybetli boyuta getirildi\n" +
                "• 4 köşeye devasa gözetleme kuleleri yerleştirildi\n" +
                "• Gotik taş sütunlar ve titreşen meşaleler eklendi\n" +
                "• Soğuk gece ay ışığı ve derin Dark Souls sisi aktif edildi\n\n" +
                "Şimdi Play tuşuna basarak gerçekçi atmosferde savaşı deneyebilirsiniz!", "Harika!");
            
            Debug.Log("<color=cyan><b>[SoulsRPG] Dark Souls Kale Arenası (7x Gerçekçi İnsan Ölçeğinde) başarıyla oluşturuldu!</b></color>");
        }

        [MenuItem("SoulsRPG/Modellerin Boyutunu Büyüt (Scale Prefabs to Human Size 7x)", false, 2)]
        public static void FixAllPrefabScalesMenu()
        {
            ScaleAllPrefabs(ModelScaleMultiplier);
            EditorUtility.DisplayDialog("Tamamlandı", 
                $"Tüm kale prefablarının varsayılan boyutu {ModelScaleMultiplier}x olarak ayarlandı.\nArtık sahneye sürüklediğiniz her parça gerçekçi insan boyutunda gelecektir!", 
                "Harika");
        }

        public static void ScaleAllPrefabs(float scale)
        {
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Advance Studios/Medieval Castle/Prefabs" });
            foreach (var guid in prefabs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root != null)
                {
                    root.transform.localScale = Vector3.one * scale;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildWallLine(Transform parent, GameObject prefab, Vector3 start, Vector3 end, float segmentLength, float rotationY, float scale)
        {
            float distance = Vector3.Distance(start, end);
            int count = Mathf.Max(1, Mathf.RoundToInt(distance / segmentLength));
            Vector3 dir = (end - start).normalized;

            for (int i = 0; i <= count; i++)
            {
                Vector3 pos = start + dir * (i * segmentLength);
                InstantiateAt(parent, prefab, pos, Quaternion.Euler(0f, rotationY, 0f), "Wall_Segment", scale);
            }
        }

        private static GameObject InstantiateAt(Transform parent, GameObject prefab, Vector3 pos, Quaternion rot, string name, float scale)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                instance = Object.Instantiate(prefab, pos, rot, parent);
            }
            else
            {
                instance.transform.position = pos;
                instance.transform.rotation = rot;
            }
            instance.name = name;
            instance.transform.localScale = Vector3.one * scale;
            Undo.RegisterCreatedObjectUndo(instance, "Create " + name);
            return instance;
        }

        private static void CreateTorchLight(Transform parent, Vector3 worldPos)
        {
            var torchGo = new GameObject("Torch_Light");
            torchGo.transform.parent = parent;
            torchGo.transform.position = worldPos;

            var light = torchGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.58f, 0.16f); // Sıcak turuncu meşale alevi
            light.range = 14f;
            light.intensity = 2.8f;
            light.shadows = LightShadows.Soft;

            torchGo.AddComponent<TorchFlicker>();
        }

        private static void ApplyDarkSoulsLighting()
        {
            // 1. Ay Işığı (Directional Light)
            var dirLightGo = GameObject.Find("Directional Light");
            if (dirLightGo == null)
            {
                dirLightGo = new GameObject("Directional Light");
                dirLightGo.AddComponent<Light>();
            }

            var dirLight = dirLightGo.GetComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.68f, 0.78f, 0.95f); // Soğuk çelik mavisi ay ışığı
            dirLight.intensity = 0.65f;
            dirLight.shadows = LightShadows.Soft;
            dirLightGo.transform.rotation = Quaternion.Euler(38f, -65f, 0f);

            // 2. Koyu Gece Sisi (Atmospheric Slate Navy Fog)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.016f;
            RenderSettings.fogColor = new Color(0.06f, 0.08f, 0.13f);

            // 3. Ortam Işığı (Trilight Ambient)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.16f, 0.24f);     // Gökyüzü gecesi
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.10f, 0.14f); // Ufuk çizgisi
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);  // Zemin yansıması
        }

        private static Material CreateStoneFloorMaterial()
        {
            string matPath = "Assets/_Project/Materials/M_CastleCourtyardFloor.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                var shader = Shader.Find("Standard");
                mat = new Material(shader);
                mat.name = "M_CastleCourtyardFloor";
                mat.color = new Color(0.75f, 0.75f, 0.78f);
                mat.SetFloat("_Glossiness", 0.15f);
                mat.SetFloat("_Metallic", 0.05f);

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(StoneTexturePath) 
                          ?? AssetDatabase.LoadAssetAtPath<Texture2D>(WallTexturePath);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    mat.mainTextureScale = new Vector2(12f, 12f); // Detaylı taş karo döşemesi
                }

                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
            }
            return mat;
        }

        private static float GetPrefabRawLength(GameObject prefab, float fallback)
        {
            if (prefab == null) return fallback;
            var renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return fallback;

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);

            float len = Mathf.Max(b.size.x, b.size.z);
            return len > 0.5f ? len : fallback;
        }
    }
}
