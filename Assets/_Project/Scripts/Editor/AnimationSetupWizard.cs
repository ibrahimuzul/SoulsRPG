using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using SoulsRPG.Player;

namespace SoulsRPG.EditorTools
{
    public static class AnimationSetupWizard
    {
        private const string KnightModelPath = "Assets/Fantasy knight/Base mesh/Skin_1.fbx";
        private const string AnimationsFolder = "Assets/_Project/Animations";
        private const string ControllerPath = "Assets/_Project/Animations/KnightAnimatorController.controller";

        [MenuItem("SoulsRPG/3. Setup Knight Animations (Animasyonları Otomatik Bağla)", false, 2)]
        public static void SetupKnightAnimations()
        {
            Debug.Log("<color=cyan><b>[AnimationSetup] Başlıyor...</b></color>");

            // 1. Convert Knight FBX to Humanoid
            SetupModelAsHumanoid(KnightModelPath);

            // 2. Setup Animation FBX files (lockRootPositionXZ = FALSE -> in-place, no drift/snap-back!)
            SetupAnimationFBX("X Bot@Sword And Shield Idle.fbx", true, "Idle", false);
            SetupAnimationFBX("X Bot@Sword And Shield Walk.fbx", true, "Walk", false);
            SetupAnimationFBX("X Bot@Sword And Shield Run.fbx", true, "Run", false);
            SetupAnimationFBX("X Bot@Standing Dive Forward.fbx", false, "Roll", false);
            SetupAnimationFBX("X Bot@Sword And Shield Slash.fbx", false, "Attack", false);
            SetupAnimationFBX("X Bot@Sword And Shield Block Idle.fbx", true, "Block", false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 3. Load Animation Clips
            var idleClip = LoadAnimationClip("X Bot@Sword And Shield Idle.fbx");
            var walkClip = LoadAnimationClip("X Bot@Sword And Shield Walk.fbx");
            var runClip = LoadAnimationClip("X Bot@Sword And Shield Run.fbx");
            var rollClip = LoadAnimationClip("X Bot@Standing Dive Forward.fbx");
            var slashClip = LoadAnimationClip("X Bot@Sword And Shield Slash.fbx");
            var blockClip = LoadAnimationClip("X Bot@Sword And Shield Block Idle.fbx");

            if (idleClip == null || walkClip == null || runClip == null)
            {
                EditorUtility.DisplayDialog("Hata", "Animasyon klipleri yüklenemedi! Lütfen Assets/_Project/Animations klasöründeki dosyaları kontrol edin.", "Tamam");
                return;
            }

            // 4. Create or Recreate Animator Controller
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("isRolling", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("isAttacking", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("isBlocking", AnimatorControllerParameterType.Bool);

            var rootStateMachine = controller.layers[0].stateMachine;

            // --- BlendTree for Locomotion (Idle -> Walk -> Run) ---
            var locomotionState = rootStateMachine.AddState("Locomotion");
            rootStateMachine.defaultState = locomotionState;

            var blendTree = new BlendTree();
            blendTree.name = "LocomotionBlendTree";
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";
            blendTree.useAutomaticThresholds = false;

            blendTree.AddChild(idleClip, 0.0f);
            blendTree.AddChild(walkClip, 0.5f);
            blendTree.AddChild(runClip, 1.0f);

            AssetDatabase.AddObjectToAsset(blendTree, ControllerPath);
            locomotionState.motion = blendTree;

            // --- Roll State ---
            if (rollClip != null)
            {
                var rollState = rootStateMachine.AddState("Roll");
                rollState.motion = rollClip;

                var anyToRoll = rootStateMachine.AddAnyStateTransition(rollState);
                anyToRoll.AddCondition(AnimatorConditionMode.If, 0, "isRolling");
                anyToRoll.duration = 0.08f;
                anyToRoll.canTransitionToSelf = false;

                var rollToLoco = rollState.AddTransition(locomotionState);
                rollToLoco.hasExitTime = true;
                rollToLoco.exitTime = 0.82f;
                rollToLoco.duration = 0.15f;
            }

            // --- Attack State ---
            if (slashClip != null)
            {
                var attackState = rootStateMachine.AddState("Attack");
                attackState.motion = slashClip;

                var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
                anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "isAttacking");
                anyToAttack.duration = 0.08f;
                anyToAttack.canTransitionToSelf = false;

                var attackToLoco = attackState.AddTransition(locomotionState);
                attackToLoco.hasExitTime = true;
                attackToLoco.exitTime = 0.85f;
                attackToLoco.duration = 0.15f;
            }

            // --- Block State ---
            if (blockClip != null)
            {
                var blockState = rootStateMachine.AddState("Block");
                blockState.motion = blockClip;

                var locoToBlock = locomotionState.AddTransition(blockState);
                locoToBlock.AddCondition(AnimatorConditionMode.If, 0, "isBlocking");
                locoToBlock.duration = 0.1f;
                locoToBlock.hasExitTime = false;

                var blockToLoco = blockState.AddTransition(locomotionState);
                blockToLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "isBlocking");
                blockToLoco.duration = 0.15f;
                blockToLoco.hasExitTime = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // 5. Assign Animator to Player in current scene
            AssignToScenePlayer(controller);

            EditorUtility.DisplayDialog("Animasyon Kurulumu Tamamlandı!",
                "Animasyonlar ve takla/yürüme düzeltmesi başarıyla uygulandı!\n\n" +
                "Karakter artık yürürken veya koşarken merkezden kaymayacak ve geriye ışınlanmayacak.", "Tamam");
        }

        [MenuItem("SoulsRPG/Düzelt: Karakter Yürüme ve Koşma Kaymasını Düzelt (Fix Locomotion Drift)", false, 3)]
        public static void FixLocomotionDrift()
        {
            Debug.Log("<color=green><b>[AnimationSetup] Yürüme ve Koşma Animasyon Kayması Düzeltiliyor...</b></color>");
            SetupAnimationFBX("X Bot@Sword And Shield Walk.fbx", true, "Walk", false);
            SetupAnimationFBX("X Bot@Sword And Shield Run.fbx", true, "Run", false);
            SetupAnimationFBX("X Bot@Sword And Shield Idle.fbx", true, "Idle", false);
            SetupAnimationFBX("X Bot@Standing Dive Forward.fbx", false, "Roll", false);
            SetupAnimationFBX("X Bot@Sword And Shield Slash.fbx", false, "Attack", false);
            SetupAnimationFBX("X Bot@Sword And Shield Block Idle.fbx", true, "Block", false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Tamamlandı", "Yürüme ve koşma animasyonlarının merkezden kayma ve geriye ışınlanma sorunu düzeltildi!\n\nAnimasyonlar artık karakter kapsülü merkezinde sabit oynatılacak.", "Tamam");
        }

        private static void SetupModelAsHumanoid(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                Debug.Log($"[Humanoid Rig] {path} Humanoid olarak ayarlandı.");
            }
        }

        public static void SetupAnimationFBX(string fbxFileName, bool isLooping, string clipName, bool bakePositionXZ)
        {
            string path = $"{AnimationsFolder}/{fbxFileName}";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"FBX bulunamadı: {path}");
                return;
            }

            bool needsReimport = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }

            var clipAnimations = importer.defaultClipAnimations;
            if (clipAnimations != null && clipAnimations.Length > 0)
            {
                foreach (var clip in clipAnimations)
                {
                    clip.name = clipName;
                    clip.loopTime = isLooping;

                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    // IF bakePositionXZ is false: root motion is extracted from the hips,
                    // so the skeleton STAYS IN PLACE without drifting away from the capsule!
                    clip.lockRootPositionXZ = bakePositionXZ;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = false;
                }
                importer.clipAnimations = clipAnimations;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
                Debug.Log($"[Animation Configured] {fbxFileName} (BakeXZ: {bakePositionXZ}) ayarlandı.");
            }
        }

        private static AnimationClip LoadAnimationClip(string fbxFileName)
        {
            string path = $"{AnimationsFolder}/{fbxFileName}";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void AssignToScenePlayer(RuntimeAnimatorController controller)
        {
            var player = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
            if (player == null) return;

            var visual = player.transform.Find("KnightVisual") ?? player.transform;

            var animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.gameObject.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var knightModel = AssetDatabase.LoadAssetAtPath<GameObject>(KnightModelPath);
            if (knightModel != null)
            {
                var sourceAnimator = knightModel.GetComponent<Animator>();
                if (sourceAnimator != null && sourceAnimator.avatar != null)
                {
                    animator.avatar = sourceAnimator.avatar;
                }
            }

            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (locomotion != null)
            {
                locomotion.SetAnimator(animator);
            }

            EditorUtility.SetDirty(player);
        }
    }
}
