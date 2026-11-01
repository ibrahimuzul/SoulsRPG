using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SoulsRPG.Combat
{
    [CustomEditor(typeof(WeaponDamageCollider))]
    public class WeaponDamageColliderEditor : Editor
    {
        private readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle();

        private void OnSceneGUI()
        {
            var weaponCol = (WeaponDamageCollider)target;
            if (weaponCol == null) return;

            // Silahın lokal uzayına geç
            Handles.matrix = weaponCol.transform.localToWorldMatrix;

            boundsHandle.center = weaponCol.ColliderCenter;
            boundsHandle.size = weaponCol.ColliderSize;
            boundsHandle.SetColor(new Color(0.2f, 1f, 0.4f, 0.8f));

            EditorGUI.BeginChangeCheck();
            boundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(weaponCol, "Resize Weapon Hitbox");
                weaponCol.SetColliderBounds(boundsHandle.center, boundsHandle.size);
                EditorUtility.SetDirty(weaponCol);
            }

            // Scene ekranında bilgi etiketi
            Vector3 labelPos = weaponCol.ColliderCenter + Vector3.up * (weaponCol.ColliderSize.y * 0.5f + 0.15f);
            Handles.Label(labelPos, $"Topuz Vuruş Alanı\nBoyut: {weaponCol.ColliderSize.x:F2} x {weaponCol.ColliderSize.y:F2} x {weaponCol.ColliderSize.z:F2}");
        }

        public override void OnInspectorGUI()
        {
            var weaponCol = (WeaponDamageCollider)target;

            // Başlık
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "💡 Scene ekranında topuzun etrafındaki yeşil kutunun yüzeylerini farenizle doğrudan tutup çekerek vuruş alanını 3D olarak büyütebilir veya küçültebilirsiniz.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("⚡ Hızlı Hazır Ayarlar (Presets)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Sadece Baş", GUILayout.Height(28)))
            {
                Undo.RecordObject(weaponCol, "Preset Head Only");
                weaponCol.PresetHeadOnly();
                EditorUtility.SetDirty(weaponCol);
            }
            if (GUILayout.Button("⚔️ Dengeli (Önerilen)", GUILayout.Height(28)))
            {
                Undo.RecordObject(weaponCol, "Preset Balanced");
                weaponCol.PresetBalanced();
                EditorUtility.SetDirty(weaponCol);
            }
            if (GUILayout.Button("🌟 Genişletilmiş", GUILayout.Height(28)))
            {
                Undo.RecordObject(weaponCol, "Preset Extended");
                weaponCol.PresetExtended();
                EditorUtility.SetDirty(weaponCol);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Standart Inspector alanları
            DrawDefaultInspector();
        }
    }
}
