using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using SoulsRPG.Environment;
using SoulsRPG.UI;

namespace SoulsRPG.EditorTools
{
    public static class BonfireSetupWizard
    {
        [InitializeOnLoadMethod]
        private static void AutoSpawnBonfireOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var currentScene = EditorSceneManager.GetActiveScene();
                if (currentScene.IsValid() && currentScene.isLoaded)
                {
                    bool dirty = false;
                    if (GameObject.Find("Bonfire_Main") == null)
                    {
                        SpawnBonfire();
                        dirty = true;
                    }

                    if (Object.FindAnyObjectByType<EventSystem>() == null)
                    {
                        var esGo = new GameObject("--- EVENT SYSTEM ---");
                        esGo.AddComponent<EventSystem>();
                        esGo.AddComponent<StandaloneInputModule>();
                        dirty = true;
                        Debug.Log("<color=green><b>[BonfireSetup] --- EVENT SYSTEM --- sahneye eklendi ve kaydedildi!</b></color>");
                    }

                    if (dirty)
                    {
                        EditorSceneManager.MarkSceneDirty(currentScene);
                        EditorSceneManager.SaveScene(currentScene);
                    }
                }
            };
        }

        [MenuItem("SoulsRPG/🔥 8. Sahneye Bonfire Ekle (Spawn Bonfire)", false, 7)]
        public static void SpawnBonfire()
        {
            var existing = GameObject.Find("Bonfire_Main");
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("<color=yellow><b>[Bonfire] Bonfire sahnede zaten mevcut! Seçildi.</b></color>");
            }
            else
            {
                // 1. Root GameObject
                var bonfireGo = new GameObject("Bonfire_Main");
                // Taş zeminin hemen üzerinde, güney giriş avlusunda (oyuncunun spawn noktası civarı)
                bonfireGo.transform.position = new Vector3(0f, 0.09f, -10f);
                bonfireGo.transform.rotation = Quaternion.identity;

                bonfireGo.AddComponent<Bonfire>();
                Undo.RegisterCreatedObjectUndo(bonfireGo, "Create Bonfire");
                Selection.activeGameObject = bonfireGo;
                EditorGUIUtility.PingObject(bonfireGo);
            }

            // 2. Event System Kontrolü
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("--- EVENT SYSTEM ---");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
                Debug.Log("<color=green><b>[Bonfire] UI EventSystem sahneye eklendi!</b></color>");
            }

            // 3. Bonfire Menu HUD'ı kur
            var menu = BonfireMenuHUD.GetOrCreate();
            if (menu != null)
            {
                EditorUtility.SetDirty(menu);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green><b>[Bonfire] 🔥 Bonfire (Taltif Ateşi) ve Seviye Atlama Menüsü başarıyla hazırlandı!</b></color>");
        }
    }
}
