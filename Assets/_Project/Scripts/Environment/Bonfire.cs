using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SoulsRPG.Core;
using SoulsRPG.Player;
using SoulsRPG.UI;
using SoulsRPG.Audio;

namespace SoulsRPG.Environment
{
    /// <summary>
    /// Dark Souls Bonfire (Taltif / Şenlik Ateşi).
    /// Kılıcın kül ve taş yığınına saplandığı, alevle yanan dinlenme ve kayıt noktası.
    /// Oyuncu yaklaştığında dinlenip oyunu kaydedebilir, can/dayanıklılığını doldurabilir ve seviye atlayabilir.
    /// </summary>
    public class Bonfire : MonoBehaviour, IStreamingAnchor
    {
        /// <summary>Oyunun bonfire listesi. Sahnede aynı kimlikte (veya 3 m yakınında) bir bonfire yoksa oyun başında otomatik oluşturulur.</summary>
        public struct BonfireDefinition
        {
            public string Id;
            public string Name;
            public Vector3 Position;
            /// <summary>Bonfire'ın baktığı yön (derece). Oyuncu ateşin arkasında, bu yöne bakarak doğar.</summary>
            public float Yaw;
            /// <summary>Bonfire'ın ait olduğu bölge sahnesi (WorldMap).</summary>
            public string RegionScene;

            public BonfireDefinition(string id, string name, Vector3 position, float yaw, string regionScene)
            {
                Id = id; Name = name; Position = position; Yaw = yaw; RegionScene = regionScene;
            }
        }

        public static readonly BonfireDefinition[] DefaultBonfires =
        {
            // Başlangıç: kale avlusunun güneyi, oyuncu kuzeye (arenaya) bakarak doğar
            new BonfireDefinition("kale_avlusu_atesi", "Kale Avlusu Ateşi", new Vector3(0f, 0.09f, -10f), 0f, WorldMap.CourtyardScene),
            // Boss bölgesine yakın ama boss'un (22 m) ve Kara Şövalye'nin (12 m) algı menzili dışında; oyuncu batıya, boss'a bakarak doğar
            new BonfireDefinition("muhafiz_kapisi_atesi", "Muhafız Kapısı Ateşi", new Vector3(-4f, 0.09f, 20f), -90f, WorldMap.BossGateScene),
        };

        public static BonfireDefinition? FindDefinition(string id)
        {
            foreach (var def in DefaultBonfires)
            {
                if (def.Id == id) return def;
            }
            return null;
        }

        private static readonly List<Bonfire> all = new List<Bonfire>();
        public static IReadOnlyList<Bonfire> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            all.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// Sahnede eksik bonfire'ları oluşturur (sahneye kalıcı yerleştirilmemişlerse):
        /// - Bölge sahnesi yüklendiyse sadece o bölgeye ait bonfire'lar, o sahnenin içine.
        /// - Bölgelere ayrılmamış eski tek sahnede (ör. SoulsTestArena) tüm bonfire'lar.
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var map = WorldMap.Instance;
            bool isRegion = map != null && map.IsRegion(scene.name);
            bool isCore = map != null && scene.name == map.coreScene;
            if (isCore) return;

            foreach (var def in DefaultBonfires)
            {
                if (isRegion && def.RegionScene != scene.name) continue;
                if (ExistsFor(def)) continue;

                var bonfire = Create(def);
                if (bonfire.gameObject.scene != scene)
                {
                    SceneManager.MoveGameObjectToScene(bonfire.gameObject, scene);
                }
            }
        }

        /// <summary>Sahnede bu tanıma karşılık gelen bir bonfire var mı (aynı id veya 3 m yakınında)?</summary>
        public static bool ExistsFor(BonfireDefinition def)
        {
            foreach (var b in FindObjectsByType<Bonfire>())
            {
                if (b.Id == def.Id) return true;
                if ((b.transform.position - def.Position).sqrMagnitude < 9f) return true;
            }
            return false;
        }

        public static Bonfire Create(BonfireDefinition def)
        {
            var go = new GameObject("Bonfire_" + def.Id);
            go.transform.SetPositionAndRotation(def.Position, Quaternion.Euler(0f, def.Yaw, 0f));
            var bonfire = go.AddComponent<Bonfire>();
            bonfire.Initialize(def.Id, def.Name);
            return bonfire;
        }

        public static Bonfire FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var b in all)
            {
                if (b != null && b.Id == id) return b;
            }
            return null;
        }

        public static Bonfire FindNearest(Vector3 position)
        {
            Bonfire best = null;
            float bestDist = float.MaxValue;
            foreach (var b in all)
            {
                if (b == null) continue;
                float d = (b.transform.position - position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }
            return best;
        }

        [Header("Bonfire Settings")]
        [Tooltip("Benzersiz kimlik (kayıtta bu tutulur). Boş bırakılırsa addan üretilir.")]
        [SerializeField] private string bonfireId = "";
        [SerializeField] private string bonfireName = "Kale Avlusu Ateşi";
        [SerializeField] private float interactionRadius = 2.8f;
        [SerializeField] private Color fireColor = new Color(1.0f, 0.55f, 0.15f);

        [Header("Visual References")]
        [SerializeField] private Light fireLight;
        [SerializeField] private ParticleSystem fireParticles;

        private bool isPlayerInRange = false;
        private bool isLit = true;
        private Transform playerTransform;
        private PlayerStats playerStats;
        private PlayerLocomotion playerLocomotion;

        private GameObject promptUI;
        private Text promptText;

        /// <summary>Kayıtta tutulan benzersiz kimlik.</summary>
        public string Id => string.IsNullOrEmpty(bonfireId) ? Slugify(bonfireName) : bonfireId;
        public string DisplayName => bonfireName;

        public void Initialize(string id, string displayName)
        {
            bonfireId = id;
            bonfireName = displayName;
        }

        /// <summary>Oyuncunun bu bonfire'da doğacağı yatay konum ve yön: ateşin arkasında, ateşin baktığı yöne dönük.</summary>
        public void GetSpawnPose(float distance, out Vector3 position, out Quaternion rotation)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            position = transform.position - forward * distance;
            rotation = Quaternion.LookRotation(forward);
        }

        private static string Slugify(string text) => IdUtil.Slugify(text);

        /// <summary>Bonfire'ın bulunduğu sahne (bölgelere ayrılmış dünyada bölge sahnesi).</summary>
        public string SceneName => gameObject.scene.name;

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        private void Start()
        {
            SetupBonfireVisuals();
            SetupInteractionTrigger();
            CreateInteractionPrompt();
        }

        private void SetupBonfireVisuals()
        {
            // Ateş Işığı
            if (fireLight == null)
            {
                var lightGo = new GameObject("BonfireLight");
                lightGo.transform.parent = transform;
                lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                fireLight = lightGo.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = fireColor;
                fireLight.range = 8.0f;
                fireLight.intensity = 2.8f;
                fireLight.shadows = LightShadows.Soft;

                var flicker = lightGo.AddComponent<TorchFlicker>();
            }

            // Kül ve Taş Tabanı
            var baseGo = transform.Find("BonfireBase");
            if (baseGo == null)
            {
                var mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mound.name = "BonfireBase";
                mound.transform.parent = transform;
                mound.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                mound.transform.localScale = new Vector3(1.6f, 0.12f, 1.6f);

                var r = mound.GetComponent<Renderer>();
                r.material = Core.RuntimeShaders.CreateMaterial(new Color(0.12f, 0.11f, 0.10f), Core.RuntimeShaders.Standard); // Charred ash

                var col = mound.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            // Saplı Kılıç / Topuz (Coiled Sword)
            var swordGo = transform.Find("CoiledSword");
            if (swordGo == null)
            {
                var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sword.name = "CoiledSword";
                sword.transform.parent = transform;
                sword.transform.localPosition = new Vector3(0.05f, 0.75f, 0f);
                sword.transform.localRotation = Quaternion.Euler(12f, 25f, -15f);
                sword.transform.localScale = new Vector3(0.08f, 1.35f, 0.16f);

                var r = sword.GetComponent<Renderer>();
                var mat = Core.RuntimeShaders.CreateMaterial(new Color(0.25f, 0.22f, 0.20f), Core.RuntimeShaders.Standard); // Twisted iron
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.05f) * 0.8f);
                }
                r.material = mat;

                var col = sword.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }

        private void SetupInteractionTrigger()
        {
            var triggerCol = GetComponent<SphereCollider>();
            if (triggerCol == null)
            {
                triggerCol = gameObject.AddComponent<SphereCollider>();
            }
            triggerCol.isTrigger = true;
            triggerCol.radius = interactionRadius;
        }

        private void CreateInteractionPrompt()
        {
            var canvas = HudCanvas.Get();
            if (canvas == null) return;

            promptUI = new GameObject("BonfirePromptUI");
            promptUI.transform.SetParent(canvas.transform, false);

            var rect = promptUI.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.22f);
            rect.anchorMax = new Vector2(0.5f, 0.22f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(400f, 44f);

            var bg = promptUI.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.75f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(promptUI.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            promptText = textGo.AddComponent<Text>();
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            promptText.fontSize = 15;
            promptText.fontStyle = FontStyle.Bold;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = new Color(1.0f, 0.88f, 0.55f);
            promptText.text = "[E] / Ⓨ Bonfire'da Dinlen";
            promptText.raycastTarget = false;

            promptUI.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<PlayerStats>() != null || other.GetComponentInParent<PlayerStats>() != null)
            {
                playerTransform = other.transform.root;
                playerStats = playerTransform.GetComponent<PlayerStats>() ?? PlayerStats.Instance;
                playerLocomotion = playerTransform.GetComponent<PlayerLocomotion>() ?? PlayerLocomotion.Instance;

                isPlayerInRange = true;
                if (promptUI != null && (BonfireMenuHUD.Instance == null || !BonfireMenuHUD.Instance.IsOpen))
                {
                    promptUI.SetActive(true);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (playerTransform != null && (other.transform == playerTransform || other.transform.IsChildOf(playerTransform)))
            {
                isPlayerInRange = false;
                if (promptUI != null)
                {
                    promptUI.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (!isPlayerInRange || playerStats == null || playerStats.isDead) return;

            // Zaten menü açıksa etkileşim tuşunu bekleme
            if (BonfireMenuHUD.Instance != null && BonfireMenuHUD.Instance.IsOpen)
            {
                if (promptUI != null && promptUI.activeSelf) promptUI.SetActive(false);
                return;
            }

            // E tuşu veya Kol Üst Tuşu (Y / Triangle)
            bool interact = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton3);
            if (interact)
            {
                InteractWithBonfire();
            }
        }

        public void InteractWithBonfire()
        {
            if (promptUI != null) promptUI.SetActive(false);

            // 1. Oyuncuyu durdur
            if (playerLocomotion != null)
            {
                playerLocomotion.SetResting(true);
            }

            // 2. Canı ve dayanıklılığı doldur, oyunu kaydet
            if (playerStats != null)
            {
                playerStats.RestAtBonfire(Id, bonfireName, SceneName, transform.position);
            }

            // 3. Bonfire Menüsünü Aç
            var menu = BonfireMenuHUD.GetOrCreate();
            if (menu != null)
            {
                menu.OpenMenu(this);
            }
        }

        public void OnPlayerLeftBonfire()
        {
            if (playerLocomotion != null)
            {
                playerLocomotion.SetResting(false);
            }

            if (isPlayerInRange && promptUI != null)
            {
                promptUI.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            if (promptUI != null)
            {
                Destroy(promptUI);
            }
        }
    }
}