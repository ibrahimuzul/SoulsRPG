using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulsRPG.Core;
using SoulsRPG.Environment;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Bonfire.DefaultBonfires listesindeki ateşler sahnede yoksa oyun başında otomatik oluşturulur.
    /// Bu komut onları sahneye kalıcı obje olarak ekler; böylece Scene ekranında taşıyıp döndürebilirsin
    /// (aynı kimlikte bonfire sahnede olduğu için oyun artık ikinci bir kopya oluşturmaz).
    /// </summary>
    public static class BonfirePlacementTool
    {
        [MenuItem("SoulsRPG/🔥 Bonfire'ları Sahneye Kalıcı Yerleştir", false, 8)]
        public static void PlaceDefaultBonfires()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Bonfire", "Bu komutu Play modundan çıkıp editörde çalıştır (Play modunda yapılan değişiklikler kaydedilmez).", "Tamam");
                return;
            }

            int created = 0;
            foreach (var def in Bonfire.DefaultBonfires)
            {
                if (Bonfire.ExistsFor(def)) continue;

                // Dünya bölgelere ayrıldıysa: bonfire'ın bölge sahnesi açık değilse atla (yanlış sahneye düşmesin)
                var map = WorldMap.Instance;
                Scene targetScene = SceneManager.GetActiveScene();
                if (map != null && map.IsRegion(def.RegionScene))
                {
                    targetScene = SceneManager.GetSceneByName(def.RegionScene);
                    if (!targetScene.IsValid() || !targetScene.isLoaded)
                    {
                        Debug.LogWarning($"[SoulsRPG] '{def.Name}' için {def.RegionScene} sahnesi açık değil, atlandı.");
                        continue;
                    }
                }

                var bonfire = Bonfire.Create(def);
                if (bonfire.gameObject.scene != targetScene)
                {
                    SceneManager.MoveGameObjectToScene(bonfire.gameObject, targetScene);
                }
                Undo.RegisterCreatedObjectUndo(bonfire.gameObject, "Place Bonfire");
                EditorUtility.SetDirty(bonfire);
                EditorSceneManager.MarkSceneDirty(bonfire.gameObject.scene);
                created++;
            }

            string msg = created > 0
                ? $"{created} bonfire sahneye eklendi. Kaydetmek için Ctrl+S'ye bas."
                : "Tüm bonfire'lar zaten sahnede.";
            Debug.Log("[SoulsRPG] " + msg);
            EditorUtility.DisplayDialog("Bonfire", msg, "Tamam");
        }
    }
}
