using UnityEditor;
using UnityEngine;
using SoulsRPG.Boss;
using SoulsRPG.Player;
using SoulsRPG.Save;

namespace SoulsRPG.EditorTools
{
    /// <summary>
    /// Test kolaylığı: yenilen boss'ları kayıttan siler, böylece tekrar dövüşülebilir.
    /// Kayıttaki diğer hiçbir şeye (seviye, ruhlar, bonfire, itemlar) dokunmaz.
    /// Hem editörde hem Play sırasında çalışır.
    /// </summary>
    public static class BossDebugTools
    {
        [MenuItem("SoulsRPG/Debug/Yenilen Boss'ları Sıfırla", false, 101)]
        public static void ResetDefeatedBosses()
        {
            if (EditorApplication.isPlaying && PlayerStats.Instance != null)
            {
                // Oyun açık: bellekteki listeyi de temizle (yoksa sonraki kayıtta geri yazılırdı)
                int count = PlayerStats.Instance.ClearDefeatedBosses();

                // Bölgesi yüklü ve "yenildi" diye kapatılmış boss'ları hemen geri aç
                int revived = 0;
                foreach (var boss in Object.FindObjectsByType<BossController>(FindObjectsInactive.Include))
                {
                    if (!boss.gameObject.activeSelf && !boss.IsDead && boss.gameObject.scene.isLoaded)
                    {
                        boss.gameObject.SetActive(true);
                        revived++;
                    }
                }

                string msg = $"{count} boss sıfırlandı.";
                if (revived > 0) msg += $"\n{revived} boss hemen geri getirildi.";
                if (count > revived) msg += "\nBu oturumda öldürülen boss, bölgesi yeniden yüklenince (uzaklaşıp geri gelince) ya da Play yeniden başlatılınca doğar.";
                Debug.Log("[SoulsRPG] " + msg);
                EditorUtility.DisplayDialog("Boss'ları Sıfırla", msg, "Tamam");
                return;
            }

            // Oyun kapalı: kayıt dosyasını doğrudan düzenle
            int cleared = SaveManager.ClearDefeatedBossesInFile();
            string result = cleared < 0
                ? "Kayıt dosyası bulunamadı (henüz kayıt yok) — sıfırlanacak boss yok."
                : $"{cleared} boss sıfırlandı. Play'e basınca yeniden doğacaklar.";
            Debug.Log("[SoulsRPG] " + result);
            EditorUtility.DisplayDialog("Boss'ları Sıfırla", result, "Tamam");
        }
    }
}
