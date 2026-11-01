using UnityEngine;

namespace SoulsRPG.Combat
{
    [ExecuteAlways]
    public class EquipmentManager : MonoBehaviour
    {
        [Header("Sockets (Bones)")]
        public Transform rightHandSocket;
        public Transform leftHandSocket;

        [Header("Equipped Weapon (Right Hand)")]
        public GameObject weaponInstance;
        public Vector3 weaponPositionOffset = Vector3.zero;
        public Vector3 weaponRotationOffset = Vector3.zero;

        [Header("Equipped Shield (Left Arm / Hand)")]
        public GameObject shieldInstance;
        // Default (90, 0, 180) stands upright; z = 0.22 lowers shield to chest level so face is visible
        public Vector3 shieldPositionOffset = new Vector3(-0.02f, 0.04f, 0.22f);
        public Vector3 shieldRotationOffset = new Vector3(90f, 0f, 180f);

        [SerializeField] private bool initializedFromScene = false;

        private void Awake()
        {
            FindSocketsIfMissing();

            // CRITICAL: If the user moved or positioned the shield in the Scene view in Edit Mode,
            // preserve the user's custom position and rotation instead of overwriting it!
            if (!initializedFromScene && shieldInstance != null)
            {
                if (shieldInstance.transform.localPosition != Vector3.zero)
                {
                    shieldPositionOffset = shieldInstance.transform.localPosition;
                }
                if (shieldInstance.transform.localEulerAngles != Vector3.zero)
                {
                    shieldRotationOffset = shieldInstance.transform.localEulerAngles;
                }
                initializedFromScene = true;
            }

            UpdateTransforms();
        }

        private void OnEnable()
        {
            FindSocketsIfMissing();
        }

        private void Start()
        {
            FindSocketsIfMissing();
            UpdateTransforms();
            GetWeaponCollider();
        }

        public WeaponDamageCollider GetWeaponCollider()
        {
            if (weaponInstance != null)
            {
                var col = weaponInstance.GetComponent<WeaponDamageCollider>();
                if (col == null)
                {
                    col = weaponInstance.AddComponent<WeaponDamageCollider>();
                }
                col.SetOwner(transform);
                return col;
            }
            return null;
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                // In play mode, bone animation runs every frame, so keep equipment pinned at the user's offset
                if (shieldInstance != null)
                {
                    shieldInstance.transform.localPosition = shieldPositionOffset;
                    shieldInstance.transform.localRotation = Quaternion.Euler(shieldRotationOffset);
                }
                if (weaponInstance != null)
                {
                    weaponInstance.transform.localPosition = weaponPositionOffset;
                    weaponInstance.transform.localRotation = Quaternion.Euler(weaponRotationOffset);
                }
            }
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying)
            {
                // TWO-WAY LIVE SYNC IN EDIT MODE:
                // If the user drags or rotates Shield_Equipped with Move (W) or Rotate (E) tools in the Scene view,
                // automatically capture the new position & rotation into our serialized fields!
                if (shieldInstance != null && shieldInstance.transform.hasChanged)
                {
                    shieldPositionOffset = shieldInstance.transform.localPosition;
                    shieldRotationOffset = shieldInstance.transform.localEulerAngles;
                    shieldInstance.transform.hasChanged = false;
                    initializedFromScene = true;

                    UnityEditor.EditorUtility.SetDirty(this);
                    if (gameObject.scene.isLoaded)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    }
                }

                if (weaponInstance != null && weaponInstance.transform.hasChanged)
                {
                    weaponPositionOffset = weaponInstance.transform.localPosition;
                    weaponRotationOffset = weaponInstance.transform.localEulerAngles;
                    weaponInstance.transform.hasChanged = false;

                    UnityEditor.EditorUtility.SetDirty(this);
                    if (gameObject.scene.isLoaded)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    }
                }
            }
        }
#endif

        private void OnValidate()
        {
            FindSocketsIfMissing();
            UpdateTransforms();
        }

        public void FindSocketsIfMissing()
        {
            // Find Weapon
            if (weaponInstance == null)
            {
                var foundMace = FindChildRecursive(transform, "Mace_Equipped");
                if (foundMace != null)
                {
                    weaponInstance = foundMace.gameObject;
                    rightHandSocket = foundMace.parent;
                }
            }
            if (rightHandSocket == null)
            {
                rightHandSocket = FindChildRecursive(transform, "hand_r");
            }

            // Find Shield
            if (shieldInstance == null)
            {
                var foundShield = FindChildRecursive(transform, "Shield_Equipped");
                if (foundShield != null)
                {
                    shieldInstance = foundShield.gameObject;
                    leftHandSocket = foundShield.parent;
                }
            }
            if (leftHandSocket == null)
            {
                leftHandSocket = FindChildRecursive(transform, "hand_l") ?? FindChildRecursive(transform, "lowerarm_l");
            }
        }

        public void UpdateTransforms()
        {
            if (weaponInstance != null)
            {
                weaponInstance.transform.localPosition = weaponPositionOffset;
                weaponInstance.transform.localRotation = Quaternion.Euler(weaponRotationOffset);
#if UNITY_EDITOR
                weaponInstance.transform.hasChanged = false;
#endif
            }

            if (shieldInstance != null)
            {
                shieldInstance.transform.localPosition = shieldPositionOffset;
                shieldInstance.transform.localRotation = Quaternion.Euler(shieldRotationOffset);
#if UNITY_EDITOR
                shieldInstance.transform.hasChanged = false;
#endif
            }
        }

        [ContextMenu("Kalkanı Göğüs Hizasına İndir (İdeal Duruş)")]
        public void LowerShieldToChest()
        {
            shieldPositionOffset = new Vector3(-0.02f, 0.04f, 0.22f);
            shieldRotationOffset = new Vector3(90f, 0f, 180f);
            UpdateTransforms();
        }

        [ContextMenu("Kalkanı Dik Yap / Reset Upright (90, 0, 180)")]
        public void PresetUprightShield()
        {
            shieldRotationOffset = new Vector3(90f, 0f, 180f);
            shieldPositionOffset = new Vector3(-0.02f, 0.04f, 0.22f);
            UpdateTransforms();
        }

        [ContextMenu("Kalkanı 180° Çevir (Flip)")]
        public void FlipShield()
        {
            shieldRotationOffset.z = (shieldRotationOffset.z + 180f) % 360f;
            UpdateTransforms();
        }

        [ContextMenu("Kalkan X Açısı +90")]
        public void RotateShieldX()
        {
            shieldRotationOffset.x = (shieldRotationOffset.x + 90f) % 360f;
            UpdateTransforms();
        }

        [ContextMenu("Kalkan Y Açısı +90")]
        public void RotateShieldY()
        {
            shieldRotationOffset.y = (shieldRotationOffset.y + 90f) % 360f;
            UpdateTransforms();
        }

        [ContextMenu("Kalkan Z Açısı +90")]
        public void RotateShieldZ()
        {
            shieldRotationOffset.z = (shieldRotationOffset.z + 90f) % 360f;
            UpdateTransforms();
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("⚔️ Topuz Vuruş Alanını Seç ve Düzenle (Select Weapon Hitbox)")]
        public void SelectWeaponColliderInScene()
        {
            var col = GetWeaponCollider();
            if (col != null)
            {
                UnityEditor.Selection.activeGameObject = col.gameObject;
                UnityEditor.SceneView.lastActiveSceneView?.FrameSelected();
            }
        }
#endif
    }
}
