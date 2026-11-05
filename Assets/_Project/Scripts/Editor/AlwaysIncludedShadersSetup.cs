using UnityEditor;
using UnityEngine;
using SoulsRPG.Core;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Kodla Shader.Find ile aranan shader'ları Project Settings > Graphics > Always Included Shaders
    /// listesine ekler. Aksi halde build'de bu shader'lar strip edilir ve materyaller pembe görünür.
    /// </summary>
    [InitializeOnLoad]
    public static class AlwaysIncludedShadersSetup
    {
        static AlwaysIncludedShadersSetup()
        {
            EditorApplication.delayCall += EnsureShadersIncluded;
        }

        [MenuItem("SoulsRPG/Build/Ensure Runtime Shaders Included")]
        public static void EnsureShadersIncluded()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            bool changed = false;
            foreach (var shaderName in RuntimeShaders.RequiredShaders)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) continue;

                bool exists = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    int index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    list.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                    changed = true;
                    Debug.Log($"[SoulsRPG] '{shaderName}' Always Included Shaders listesine eklendi.");
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
