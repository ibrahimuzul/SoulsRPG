using System;
using System.IO;
using UnityEngine;
using SoulsRPG.Player;

namespace SoulsRPG.Save
{
    [System.Serializable]
    public class PlayerSaveData
    {
        public int soulLevel = 1;
        public int currentSouls = 500; // Başlangıçta seviye atlamayı test edebilmek için 500 ruh

        // Dark Souls Nitelikleri (Attributes)
        public int vigor = 10;       // Can (Health)
        public int endurance = 10;   // Dayanıklılık (Stamina)
        public int strength = 10;    // Saldırı Hasarı (Attack Damage)
        public int dexterity = 10;   // Hız / Verim
        public int vitality = 10;    // Denge & Zırh (Poise & Defense)

        // Bonfire Yeniden Doğma Konumu
        public float respawnX = 0f;
        public float respawnY = 0f;
        public float respawnZ = -14f;
        public bool hasSavedBonfire = false;

        public string saveTimestamp = "";
    }

    /// <summary>
    /// Dark Souls Karakter Kayıt Yöneticisi (JSON formatında disk üzerinde kalıcı kayıt).
    /// </summary>
    public static class SaveManager
    {
        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, "souls_save.json");

        public static bool HasSaveFile()
        {
            return File.Exists(SaveFilePath);
        }

        public static void SavePlayer(PlayerStats stats, Vector3? bonfirePosition = null)
        {
            if (stats == null) return;

            try
            {
                var data = new PlayerSaveData
                {
                    soulLevel = stats.SoulLevel,
                    currentSouls = stats.CurrentSouls,
                    vigor = stats.Vigor,
                    endurance = stats.Endurance,
                    strength = stats.Strength,
                    dexterity = stats.Dexterity,
                    vitality = stats.Vitality,
                    saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                if (bonfirePosition.HasValue)
                {
                    data.respawnX = bonfirePosition.Value.x;
                    data.respawnY = bonfirePosition.Value.y;
                    data.respawnZ = bonfirePosition.Value.z;
                    data.hasSavedBonfire = true;
                }
                else if (stats.LastBonfirePosition.HasValue)
                {
                    data.respawnX = stats.LastBonfirePosition.Value.x;
                    data.respawnY = stats.LastBonfirePosition.Value.y;
                    data.respawnZ = stats.LastBonfirePosition.Value.z;
                    data.hasSavedBonfire = true;
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SaveFilePath, json);

                Debug.Log($"<color=green><b>[SaveManager] Karakter Kaydedildi! (Level: {data.soulLevel}, Ruh: {data.currentSouls}, Dosya: {SaveFilePath})</b></color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Kayıt hatası: {ex.Message}");
            }
        }

        public static PlayerSaveData LoadPlayer()
        {
            if (!HasSaveFile())
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                var data = JsonUtility.FromJson<PlayerSaveData>(json);
                Debug.Log($"<color=cyan><b>[SaveManager] Kayıt Yüklendi! (Level: {data.soulLevel}, Ruh: {data.currentSouls})</b></color>");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Yükleme hatası: {ex.Message}");
                return null;
            }
        }

        public static void DeleteSave()
        {
            if (HasSaveFile())
            {
                File.Delete(SaveFilePath);
                Debug.Log("<color=yellow>[SaveManager] Kayıt dosyası silindi!</color>");
            }
        }
    }
}