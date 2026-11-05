using UnityEngine;
using UnityEditor;
using SoulsRPG.Combat;

namespace SoulsRPG.EditorTools
{
    [CustomEditor(typeof(EquipmentManager))]
    [CanEditMultipleObjects]
    public class EquipmentManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var eq = (EquipmentManager)target;

            serializedObject.Update();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🛡️ SOULS-LIKE EQUIPMENT CONTROLLER", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "İPUCU: Artık Scene ekranında 'Shield_Equipped' objesini seçip W (Taşıma) veya E (Döndürme) oklarıyla nereye çekerseniz, buradaki değerler otomatik güncellenir ve Play'e bastığınızda ASLA bozulmaz!",
                MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⚔️ Weapon (Sağ El - Gürz / Kılıç)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rightHandSocket"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponInstance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponPositionOffset"), new GUIContent("Weapon Position (X, Y, Z)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponRotationOffset"), new GUIContent("Weapon Rotation (X, Y, Z)"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🛡️ Shield (Sol El - Kalkan)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("leftHandSocket"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shieldInstance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shieldPositionOffset"), new GUIContent("Shield Position (X, Y, Z)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shieldRotationOffset"), new GUIContent("Shield Rotation (X, Y, Z)"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⚡ Hızlı Kalkan Düzeltme Butonları", EditorStyles.boldLabel);

            // Big Green Button: Ideal Chest Guard Position
            var defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("🛡️ KALKANI GÖĞÜS HİZASINA İNDİR (İDEAL DURUŞ)", GUILayout.Height(36)))
            {
                Undo.RecordObject(eq, "Lower Shield To Chest");
                eq.LowerShieldToChest();
                EditorUtility.SetDirty(eq);
            }
            GUI.backgroundColor = defaultColor;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Aşağı Kaydır (Baş Hizasını Aç)"))
            {
                Undo.RecordObject(eq, "Slide Shield Down");
                eq.shieldPositionOffset.z += 0.05f;
                eq.UpdateTransforms();
                EditorUtility.SetDirty(eq);
            }
            if (GUILayout.Button("Yukarı Kaydır"))
            {
                Undo.RecordObject(eq, "Slide Shield Up");
                eq.shieldPositionOffset.z -= 0.05f;
                eq.UpdateTransforms();
                EditorUtility.SetDirty(eq);
            }
            if (GUILayout.Button("🔄 180° Çevir (Flip)"))
            {
                Undo.RecordObject(eq, "Flip Shield");
                eq.FlipShield();
                EditorUtility.SetDirty(eq);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+90° X"))
            {
                Undo.RecordObject(eq, "Rotate Shield X");
                eq.RotateShieldX();
                EditorUtility.SetDirty(eq);
            }
            if (GUILayout.Button("+90° Y"))
            {
                Undo.RecordObject(eq, "Rotate Shield Y");
                eq.RotateShieldY();
                EditorUtility.SetDirty(eq);
            }
            if (GUILayout.Button("+90° Z"))
            {
                Undo.RecordObject(eq, "Rotate Shield Z");
                eq.RotateShieldZ();
                EditorUtility.SetDirty(eq);
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                eq.UpdateTransforms();
                EditorUtility.SetDirty(eq);
            }
        }

        /// <summary>Tüm karakterlerin silah ofsetini orijinal (0,0,0) tutuşa döndürür.</summary>
        [MenuItem("SoulsRPG/Equipment/Silahları Orijinal Tutuşa Döndür (0,0,0)")]
        public static void ResetWeaponGripToOriginal()
        {
            int count = 0;
            foreach (var e in Object.FindObjectsByType<EquipmentManager>())
            {
                if (e.weaponInstance == null) continue;
                Undo.RecordObject(e, "Reset Weapon Grip");
                Undo.RecordObject(e.weaponInstance.transform, "Reset Weapon Grip");
                e.weaponPositionOffset = Vector3.zero;
                e.weaponRotationOffset = Vector3.zero;
                e.UpdateTransforms();
                EditorUtility.SetDirty(e);
                if (e.gameObject.scene.isLoaded)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(e.gameObject.scene);
                }
                count++;
            }
            Debug.Log($"[SoulsRPG] {count} karakterin silah tutuşu (0,0,0) değerine döndürüldü.");
        }
    }
}
