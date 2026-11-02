using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SoulsRPG.Boss;
using SoulsRPG.CameraSystem;
using SoulsRPG.Combat;
using SoulsRPG.UI;
using SoulsRPG.Audio;

namespace SoulsRPG.EditorTools
{
    public static class BossSetupWizard
    {
        private const string KnightSkin3Path = "Assets/Fantasy knight/Prefab/Skin_3.prefab";
        private const string MacePath = "Assets/Fantasy knight/Prefab/Mace.prefab";
        private const string ShieldPath = "Assets/Fantasy knight/Prefab/Shield.prefab";
        private const string KnightModelFbx = "Assets/Fantasy knight/Base mesh/Skin_1.fbx";
        private const string ControllerPath = "Assets/_Project/Animations/KnightAnimatorController.controller";

        public static readonly Vector3 DefaultShieldPos = Vector3.zero;
        public static readonly Vector3 DefaultShieldRot = new Vector3(90f, 0f, 180f);
        public static readonly Vector3 DefaultMacePos = Vector3.zero;
        public static readonly Vector3 DefaultMaceRot = Vector3.zero;

        [InitializeOnLoadMethod]
        private static void AutoSpawnOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var currentScene = EditorSceneManager.GetActiveScene();
                if (currentScene.IsValid() && currentScene.isLoaded)
                {
                    if (GameObject.Find("Boss_Commander_Malakor") == null)
                    {
                        SpawnBossMalakor();
                        EditorSceneManager.SaveScene(currentScene);
                    }
                }
            };
        }

        [MenuItem("SoulsRPG/👑 7. Sahneye Boss Ekle (Spawn Boss Malakor)", false, 6)]
        public static void SpawnBossMalakor()
        {
            var existingBoss = GameObject.Find("Boss_Commander_Malakor");
            if (existingBoss != null)
            {
                Selection.activeGameObject = existingBoss;
                EditorGUIUtility.PingObject(existingBoss);
                Debug.Log("<color=yellow><b>[SoulsRPG] Boss Malakor sahnede zaten mevcut! Konumlandırıldı ve seçildi.</b></color>");
                return;
            }

            // 1. Root GameObject
            var bossGo = new GameObject("Boss_Commander_Malakor");
            // Kuzey kale avlusunda, güneye (girişe doğru) bakar şekilde
            bossGo.transform.position = new Vector3(0f, 0.0f, 17f);
            bossGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            bossGo.transform.localScale = Vector3.one * 1.45f; // Heybetli 2.7m devasa boy

            // 2. Character Controller
            var cc = bossGo.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.55f;
            cc.center = new Vector3(0f, 1.0f, 0f);

            // 3. Lock-On Target
            var lockTarget = bossGo.AddComponent<LockOnTarget>();
            var lockPivot = new GameObject("LockOnPivot");
            lockPivot.transform.parent = bossGo.transform;
            lockPivot.transform.localPosition = new Vector3(0f, 1.7f, 0f);

            var so = new SerializedObject(lockTarget);
            so.FindProperty("lockOnPivot").objectReferenceValue = lockPivot.transform;
            so.ApplyModifiedProperties();

            // 4. Knight Visual & Animator
            AttachBossVisual(bossGo.transform, KnightSkin3Path);

            // 5. Equipment (Devasa Topuz ve Kalkan)
            SetupBossEquipment(bossGo.transform);

            // 6. Boss Scripts
            bossGo.AddComponent<BossPhaseAura>();
            var bossCtrl = bossGo.AddComponent<BossController>();

            // 7. Audio & HUD
            SetupBossHUD();
            EnsureAudioManager();

            Undo.RegisterCreatedObjectUndo(bossGo, "Spawn Boss Malakor");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = bossGo;
            EditorGUIUtility.PingObject(bossGo);

            Debug.Log("<color=green><b>[SoulsRPG] 👑 Gölge Şövalye Kumandanı Malakor (Boss) başarıyla sahneye eklendi!</b></color>");
        }

        private static void AttachBossVisual(Transform parent, string skinPath)
        {
            var skinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(skinPath);
            if (skinPrefab == null) skinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fantasy knight/Prefab/Skin_1.prefab");

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
            }
        }

        private static void SetupBossEquipment(Transform charRoot)
        {
            var eq = charRoot.GetComponent<EquipmentManager>();
            if (eq == null) eq = charRoot.gameObject.AddComponent<EquipmentManager>();

            Transform visual = charRoot.Find("KnightVisual") ?? charRoot;
            Transform handL = FindBoneDeep(visual, "hand_l");
            Transform handR = FindBoneDeep(visual, "hand_r");

            // Kalkan
            Transform shieldTransform = null;
            if (handL != null)
            {
                var shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPath);
                if (shieldPrefab != null)
                {
                    var shield = (GameObject)PrefabUtility.InstantiatePrefab(shieldPrefab, handL);
                    shield.name = "Shield_Equipped";
                    shield.transform.localScale = Vector3.one * 1.25f;
                    shieldTransform = shield.transform;
                }
            }
            if (shieldTransform != null)
            {
                eq.shieldInstance = shieldTransform.gameObject;
                eq.leftHandSocket = shieldTransform.parent;
                eq.shieldPositionOffset = DefaultShieldPos;
                eq.shieldRotationOffset = DefaultShieldRot;
            }

            // Devasa Topuz (Mace)
            Transform maceTransform = null;
            if (handR != null)
            {
                var macePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MacePath);
                if (macePrefab != null)
                {
                    var mace = (GameObject)PrefabUtility.InstantiatePrefab(macePrefab, handR);
                    mace.name = "Mace_Equipped";
                    mace.transform.localScale = Vector3.one * 1.35f;
                    maceTransform = mace.transform;

                    // Weapon Damage Collider
                    var col = mace.AddComponent<WeaponDamageCollider>();
                    col.SetOwner(charRoot);
                    col.SetColliderBounds(new Vector3(0f, 0.65f, 0f), new Vector3(0.55f, 1.4f, 0.55f));
                }
            }
            if (maceTransform != null)
            {
                eq.weaponInstance = maceTransform.gameObject;
                eq.rightHandSocket = maceTransform.parent;
                eq.weaponPositionOffset = DefaultMacePos;
                eq.weaponRotationOffset = DefaultMaceRot;
            }

            eq.UpdateTransforms();
            EditorUtility.SetDirty(charRoot);
        }

        private static void SetupBossHUD()
        {
            var hud = BossHealthBarHUD.GetOrCreate();
            if (hud != null)
            {
                hud.EnsureUIElementsCreated();
                EditorUtility.SetDirty(hud);
            }
        }

        private static void EnsureAudioManager()
        {
            var audioMgr = SoulsAudioManager.GetOrCreate();
            if (audioMgr != null)
            {
                audioMgr.AutoLoadClipsIfEmpty();
                EditorUtility.SetDirty(audioMgr);
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
    }
}