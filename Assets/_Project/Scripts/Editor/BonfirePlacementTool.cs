using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

                var bonfire = Bonfire.Create(def);
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
