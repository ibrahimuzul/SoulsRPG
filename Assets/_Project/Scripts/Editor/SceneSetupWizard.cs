using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using SoulsRPG.Player;
using SoulsRPG.CameraSystem;
using SoulsRPG.Enemy;
using SoulsRPG.UI;
using SoulsRPG.Combat;

namespace SoulsRPG.EditorTools
{
    public static class SceneSetupWizard
    {
        private const string KnightSkin1Path = "Assets/Fantasy knight/Prefab/Skin_1.prefab";
        private const string KnightSkin2Path = "Assets/Fantasy knight/Prefab/Skin_2.prefab";
        private const string KnightSkin3Path = "Assets/Fantasy knight/Prefab/Skin_3.prefab";
        private const string MacePath = "Assets/Fantasy knight/Prefab/Mace.prefab";
        private const string ShieldPath = "Assets/Fantasy knight/Prefab/Shield.prefab";
        private const string KnightModelFbx = "Assets/Fantasy knight/Base mesh/Skin_1.fbx";
        private const string ControllerPath = "Assets/_Project/Animations/KnightAnimatorController.controller";

        // Shield Default Alignment (Hand mount - upright vertical protecting torso)
        public static readonly Vector3 DefaultShieldPos = Vector3.zero;
        public static readonly Vector3 DefaultShieldRot = new Vector3(90f, 0f, 180f);

        // Mace Default Alignment (Right Hand)
        public static readonly Vector3 DefaultMacePos = new Vector3(0f, 0f, 0f);
        public static readonly Vector3 DefaultMaceRot = new Vector3(0f, 0f, 0f);

        [MenuItem("SoulsRPG/1. Setup Test Arena (Tüm Sahneyi Şövalyelerle Kur)", false, 0)]
        public static void CreateTestArena()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            RenderSettings.ambientLight = new Color(0.4f, 0.45f, 0.5f);
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Materials
            var groundMat = GetOrCreateMaterial("M_Ground", new Color(0.25f, 0.28f, 0.3f));
            var wallMat = GetOrCreateMaterial("M_Wall", new Color(0.35f, 0.35f, 0.38f));
            var pillarMat = GetOrCreateMaterial("M_Pillar", new Color(0.5f, 0.5f, 0.52f));
            var playerMat = GetOrCreateMaterial("M_Player", new Color(0.18f, 0.45f, 0.85f));
            var enemyMat = GetOrCreateMaterial("M_Enemy", new Color(0.85f, 0.2f, 0.2f));

            // Arena Environment
            var arenaRoot = new GameObject("--- ARENA ENVIRONMENT ---");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Arena_Floor";
            ground.transform.parent = arenaRoot.transform;
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            CreateWall(arenaRoot.transform, new Vector3(0f, 3f, 30f), new Vector3(60f, 6f, 1f), wallMat);
            CreateWall(arenaRoot.transform, new Vector3(0f, 3f, -30f), new Vector3(60f, 6f, 1f), wallMat);
            CreateWall(arenaRoot.transform, new Vector3(30f, 3f, 0f), new Vector3(1f, 6f, 60f), wallMat);
            CreateWall(arenaRoot.transform, new Vector3(-30f, 3f, 0f), new Vector3(1f, 6f, 60f), wallMat);

            CreatePillar(arenaRoot.transform, new Vector3(-8f, 2.5f, 8f), pillarMat);
            CreatePillar(arenaRoot.transform, new Vector3(8f, 2.5f, 8f), pillarMat);
            CreatePillar(arenaRoot.transform, new Vector3(-8f, 2.5f, -8f), pillarMat);
            CreatePillar(arenaRoot.transform, new Vector3(8f, 2.5f, -8f), pillarMat);

            // Player
            var player = CreateKnightPlayer(new Vector3(0f, 0f, -12f), playerMat);

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();

            var camManager = camGo.AddComponent<CameraManager>();
            camManager.SetTarget(player.transform);

            // Enemies / Dummies
            var enemiesRoot = new GameObject("--- TRAINING DUMMIES (KNIGHTS) ---");
            CreateKnightDummy(enemiesRoot.transform, new Vector3(0f, 0f, 4f), "Knight_Enemy_Center", KnightSkin2Path, enemyMat);
            CreateKnightDummy(enemiesRoot.transform, new Vector3(14f, 0f, 12f), "Knight_Enemy_Right", KnightSkin3Path, enemyMat);
            CreateKnightDummy(enemiesRoot.transform, new Vector3(-14f, 0f, 12f), "Knight_Enemy_Left", KnightSkin2Path, enemyMat);

            // HUD
            CreateSoulsHUD(player.GetComponent<PlayerStats>());

            // Save Scene
            string scenePath = "Assets/_Project/Scenes/SoulsTestArena.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green><b>[SoulsRPG] Sahne Kuruldu!</b></color>");
        }

        [MenuItem("SoulsRPG/5. Reload Input Settings (Gamepad Girdilerini Yenile)", false, 4)]
        public static void ReloadInputSettings()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("<color=green><b>[SoulsRPG] Gamepad ve Input ayarları yenilendi!</b></color>");
        }

        [MenuItem("SoulsRPG/4. Fix Shield Angle & Roll (Kalkan Açısı ve Taklayı Düzelt)", false, 3)]
        public static void FixShieldAndRoll()
        {
            // 1. Re-run animation configuration with In-Place roll
            AnimationSetupWizard.SetupAnimationFBX("X Bot@Standing Dive Forward.fbx", false, "Roll", false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 2. Re-assign controller to player
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller != null)
            {
                var player = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
                if (player != null)
                {
                    var visual = player.transform.Find("KnightVisual") ?? player.transform;
                    var anim = visual.GetComponent<Animator>();
                    if (anim != null)
                    {
                        anim.runtimeAnimatorController = controller;
                        anim.applyRootMotion = false;
                    }
                }
            }

            // 3. Fix Shields on Player and all Dummies
            FixEquipmentOnAllCharacters();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Düzeltme Tamamlandı!",
                "1. Kalkan açısı ön kola (lowerarm) gerçekçi savunma pozisyonunda bağlandı.\n" +
                "2. Takla (Roll) hareketi yerinde dönecek şekilde ayarlandı; karakter artık merkezden uzağa fırlayıp geriye zıplamayacak!\n\n" +
                "Ayrıca 'Player' objesinde 'EquipmentManager' bileşeni üzerinden kalkan açısını istediğiniz an canlı değiştirebilirsiniz.", "Harika!");
        }

        public static void FixEquipmentOnAllCharacters()
        {
            var characters = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in characters)
            {
                if (t.name.Equals("Player") || t.name.Contains("Knight_Enemy") || t.GetComponent<TrainingDummy>() != null)
                {
                    SetupEquipmentManager(t);
                }
            }
        }

        private static void SetupEquipmentManager(Transform charRoot)
        {
            var eq = charRoot.GetComponent<EquipmentManager>();
            if (eq == null) eq = charRoot.gameObject.AddComponent<EquipmentManager>();

            // Find visual root
            Transform visual = charRoot.Find("KnightVisual") ?? charRoot;
            Transform handL = FindBoneDeep(visual, "hand_l");
            Transform handR = FindBoneDeep(visual, "hand_r");

            // Shield
            Transform existingShield = FindBoneDeep(visual, "Shield_Equipped");
            if (existingShield == null && handL != null)
            {
                var shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPath);
                if (shieldPrefab != null)
                {
                    var shield = (GameObject)PrefabUtility.InstantiatePrefab(shieldPrefab, handL);
                    shield.name = "Shield_Equipped";
                    existingShield = shield.transform;
                }
            }
            if (existingShield != null)
            {
                eq.shieldInstance = existingShield.gameObject;
                eq.leftHandSocket = existingShield.parent;
                eq.shieldPositionOffset = DefaultShieldPos;
                eq.shieldRotationOffset = DefaultShieldRot;
            }

            // Mace
            Transform existingMace = FindBoneDeep(visual, "Mace_Equipped");
            if (existingMace == null && handR != null)
            {
                var macePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MacePath);
                if (macePrefab != null)
                {
                    var mace = (GameObject)PrefabUtility.InstantiatePrefab(macePrefab, handR);
                    mace.name = "Mace_Equipped";
                    existingMace = mace.transform;
                }
            }
            if (existingMace != null)
            {
                eq.weaponInstance = existingMace.gameObject;
                eq.rightHandSocket = existingMace.parent;
                eq.weaponPositionOffset = DefaultMacePos;
                eq.weaponRotationOffset = DefaultMaceRot;
            }

            eq.UpdateTransforms();
            EditorUtility.SetDirty(charRoot);
        }

        private static GameObject CreateKnightPlayer(Vector3 position, Material fallbackMat)
        {
            var player = new GameObject("Player");
            player.transform.position = position;

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 0.95f, 0f);

            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerLocomotion>();
            player.AddComponent<PlayerInputHandler>();

            AttachKnightVisual(player.transform, KnightSkin1Path, true, true);
            SetupEquipmentManager(player.transform);

            return player;
        }

        private static void CreateKnightDummy(Transform parent, Vector3 pos, string name, string skinPath, Material fallbackMat)
        {
            var dummy = new GameObject(name);
            dummy.transform.parent = parent;
            dummy.transform.position = pos;

            var col = dummy.AddComponent<CapsuleCollider>();
            col.height = 1.9f;
            col.radius = 0.45f;
            col.center = new Vector3(0f, 0.95f, 0f);

            var lockTarget = dummy.AddComponent<LockOnTarget>();
            dummy.AddComponent<TrainingDummy>();

            AttachKnightVisual(dummy.transform, skinPath, true, false);
            SetupEquipmentManager(dummy.transform);

            var lockPivot = new GameObject("LockOnPivot");
            lockPivot.transform.parent = dummy.transform;
            lockPivot.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            var so = new SerializedObject(lockTarget);
            so.FindProperty("lockOnPivot").objectReferenceValue = lockPivot.transform;
            so.ApplyModifiedProperties();
        }

        private static void AttachKnightVisual(Transform parent, string skinPath, bool equipWeapons, bool setupAnimator)
        {
            var skinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(skinPath);
            if (skinPrefab != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(skinPrefab, parent);
                visual.name = "KnightVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                var colliders = visual.GetComponentsInChildren<Collider>();
                foreach (var c in colliders)
                {
                    c.isTrigger = true;
                }

                if (setupAnimator)
                {
                    var anim = visual.GetComponent<Animator>();
                    if (anim == null) anim = visual.AddComponent<Animator>();

                    var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                    if (controller != null) anim.runtimeAnimatorController = controller;

                    var fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(KnightModelFbx);
                    if (fbxModel != null)
                    {
                        var srcAnim = fbxModel.GetComponent<Animator>();
                        if (srcAnim != null && srcAnim.avatar != null)
                        {
                            anim.avatar = srcAnim.avatar;
                        }
                    }

                    var loco = parent.GetComponent<PlayerLocomotion>();
                    if (loco != null) loco.SetAnimator(anim);
                }
            }
        }

        private static Transform FindBoneDeep(Transform current, string targetName)
        {
            if (current.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                return current;

            for (int i = 0; i < current.childCount; i++)
            {
                var found = FindBoneDeep(current.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"Assets/_Project/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void CreateWall(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.parent = parent;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreatePillar(Transform parent, Vector3 pos, Material mat)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.parent = parent;
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
            pillar.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateSoulsHUD(PlayerStats playerStats)
        {
            var canvasGo = new GameObject("--- SOULS HUD CANVAS ---");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var hud = canvasGo.AddComponent<StatBarHUD>();

            var panel = new GameObject("BarsContainer");
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(40f, -40f);
            panelRect.sizeDelta = new Vector2(400f, 80f);

            var hpSlider = CreateBar(panel.transform, "HealthBar", new Vector2(0f, 0f), new Vector2(350f, 22f), new Color(0.8f, 0.15f, 0.15f));
            var stSlider = CreateBar(panel.transform, "StaminaBar", new Vector2(0f, -28f), new Vector2(280f, 16f), new Color(0.18f, 0.75f, 0.25f));

            var so = new SerializedObject(hud);
            so.FindProperty("playerStats").objectReferenceValue = playerStats;
            so.FindProperty("healthSlider").objectReferenceValue = hpSlider;
            so.FindProperty("staminaSlider").objectReferenceValue = stSlider;
            so.ApplyModifiedProperties();
        }

        private static Slider CreateBar(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor)
        {
            var barGo = new GameObject(name);
            barGo.transform.SetParent(parent, false);
            var barRect = barGo.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = pos;
            barRect.sizeDelta = size;

            var bgImg = barGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(barGo.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;

            var slider = barGo.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;
            slider.transition = Selectable.Transition.None;

            return slider;
        }
    }
}
