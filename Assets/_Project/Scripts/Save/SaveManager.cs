using System;
using System.IO;
using UnityEngine;
using SoulsRPG.Core;
using SoulsRPG.Player;

namespace SoulsRPG.Save
{
    [System.Serializable]
    public class PlayerSaveData
    {
        // Karakter yuvası bilgileri (menüdeki Continue / Load Game)
        public string characterName = "";
        public string slotId = "";
        public string createdAt = "";

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

        // Son dinlenilen bonfire'ın kimliği ve adı (doğma noktası bu id ile bulunur; konum yedek olarak kalır)
        public string lastBonfireId = "";
        public string lastBonfireName = "";
        // Bonfire'ın bulunduğu bölge sahnesi (doğarken önce bu sahne yüklenir)
        public string lastBonfireScene = "";

        // Yenilen boss'lar (bölge yeniden yüklenince geri gelmesinler)
        public System.Collections.Generic.List<string> defeatedBosses = new System.Collections.Generic.List<string>();

        // Ruh Lekesi (Bloodstain): ölünce düşen ruhlar
        public bool hasBloodstain = false;
        public int bloodstainSouls = 0;
        public float bloodstainX = 0f;
        public float bloodstainY = 0f;
        public float bloodstainZ = 0f;

        // Item kutusu (ItemBox): eldeki itemlar ve adetleri.
        // hasItemBox=false olan eski kayıtlarda oyuncuya başlangıç itemları verilir.
        public bool hasItemBox = false;
        public System.Collections.Generic.List<SavedItemStack> items = new System.Collections.Generic.List<SavedItemStack>();

        public string saveTimestamp = "";
    }

    [System.Serializable]
    public class SavedItemStack
    {
        public string id;
        public int count;
    }

    /// <summary>Menüdeki "Load Game" listesi için bir karakter yuvası.</summary>
    public class SaveSlotInfo
    {
        public string slotId;
        public PlayerSaveData data;
    }

    [System.Serializable]
    internal class SaveProfile
    {
        public string lastSlotId = "";
    }

    /// <summary>
    /// Dark Souls Karakter Kayıt Yöneticisi (JSON, çoklu karakter yuvası).
    ///
    /// Her karakter kendi dosyasında: persistentDataPath/saves/slot_YYYYMMDD_HHMMSS_fff.json
    /// saves/profile.json son oynanan yuvayı hatırlar (menüdeki "Continue").
    /// Oyunun geri kalanı her zaman "aktif yuva" ile çalışır (SavePlayer / LoadPlayer).
    /// Eski tek dosyalı kayıt (souls_save.json) ilk açılışta otomatik olarak bir yuvaya taşınır
    /// (eski dosya souls_save.json.bak olarak durur).
    /// </summary>
    public static class SaveManager
    {
        private const string SaveFolderName = "saves";
        private const string LegacyFileName = "souls_save.json";
        private const string ProfileFileName = "profile.json";
        private const string DefaultTestName = "Test Karakteri";

        private static string SaveFolder => Path.Combine(Application.persistentDataPath, SaveFolderName);
        private static string ProfilePath => Path.Combine(SaveFolder, ProfileFileName);
        private static string SlotPath(string slotId) => Path.Combine(SaveFolder, slotId + ".json");
        private static string SaveFilePath => SlotPath(ActiveSlotId);

        private static string activeSlotId;
        private static string pendingCharacterName; // dosyası henüz yazılmamış yuvanın adı
        private static bool migrated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            activeSlotId = null;
            pendingCharacterName = null;
            migrated = false;
        }

        // ==========================================
        // YUVALAR (SLOT)
        // ==========================================

        /// <summary>
        /// Oyunun kaydettiği/yüklediği yuva. Menüden seçilmediyse (editörde doğrudan Core'dan Play)
        /// son oynanan yuva; hiç kayıt yoksa yeni bir "Test Karakteri" yuvası (dosyası ilk kayıtta oluşur).
        /// </summary>
        public static string ActiveSlotId
        {
            get
            {
                if (string.IsNullOrEmpty(activeSlotId))
                {
                    var last = LastPlayedSlotId;
                    if (last != null)
                    {
                        activeSlotId = last;
                    }
                    else
                    {
                        activeSlotId = NewSlotId();
                        pendingCharacterName = DefaultTestName;
                    }
                }
                return activeSlotId;
            }
        }

        /// <summary>Son oynanan (veya en son kaydedilen) yuva; hiç kayıt yoksa null.</summary>
        public static string LastPlayedSlotId
        {
            get
            {
                EnsureFolderAndMigrate();
                var profile = ReadProfile();
                if (profile != null && !string.IsNullOrEmpty(profile.lastSlotId) && File.Exists(SlotPath(profile.lastSlotId)))
                {
                    return profile.lastSlotId;
                }
                var slots = ListSlots();
                return slots.Count > 0 ? slots[0].slotId : null;
            }
        }

        public static bool HasAnySave => LastPlayedSlotId != null;

        /// <summary>Tüm karakter yuvaları, en son oynanan en üstte.</summary>
        public static System.Collections.Generic.List<SaveSlotInfo> ListSlots()
        {
            EnsureFolderAndMigrate();
            var list = new System.Collections.Generic.List<SaveSlotInfo>();
            foreach (var path in Directory.GetFiles(SaveFolder, "slot_*.json"))
            {
                try
                {
                    var data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
                    if (data == null) continue;
                    list.Add(new SaveSlotInfo { slotId = Path.GetFileNameWithoutExtension(path), data = data });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveManager] Okunamayan kayıt atlandı: {Path.GetFileName(path)} ({ex.Message})");
                }
            }
            // saveTimestamp "yyyy-MM-dd HH:mm:ss" olduğu için metin sıralaması tarih sıralamasıdır
            list.Sort((a, b) => string.CompareOrdinal(b.data.saveTimestamp ?? "", a.data.saveTimestamp ?? ""));
            return list;
        }

        /// <summary>Menüden bir karakter seçildi (Continue / Load Game).</summary>
        public static void SetActiveSlot(string slotId)
        {
            activeSlotId = slotId;
            pendingCharacterName = null;
            WriteProfile(slotId);
        }

        /// <summary>New Game: yeni karakter yuvası oluşturur (başlangıç değerleriyle) ve aktif yapar.</summary>
        public static string CreateNewSlot(string characterName)
        {
            EnsureFolderAndMigrate();
            string id = NewSlotId();
            string now = Now();
            var data = new PlayerSaveData
            {
                characterName = string.IsNullOrWhiteSpace(characterName) ? "İsimsiz" : characterName.Trim(),
                slotId = id,
                createdAt = now,
                saveTimestamp = now,
            };
            try
            {
                File.WriteAllText(SlotPath(id), JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Yeni karakter kaydı oluşturulamadı: {ex.Message}");
            }
            SetActiveSlot(id);
            return id;
        }

        private static string NewSlotId() => "slot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        private static SaveProfile ReadProfile()
        {
            try
            {
                return File.Exists(ProfilePath) ? JsonUtility.FromJson<SaveProfile>(File.ReadAllText(ProfilePath)) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void WriteProfile(string lastSlotId)
        {
            try
            {
                Directory.CreateDirectory(SaveFolder);
                File.WriteAllText(ProfilePath, JsonUtility.ToJson(new SaveProfile { lastSlotId = lastSlotId }, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Profil yazılamadı: {ex.Message}");
            }
        }

        /// <summary>saves klasörünü oluşturur; eski tek dosyalı kaydı (souls_save.json) bir yuvaya taşır.</summary>
        private static void EnsureFolderAndMigrate()
        {
            if (migrated) return;
            migrated = true;
            try
            {
                Directory.CreateDirectory(SaveFolder);
                string legacy = Path.Combine(Application.persistentDataPath, LegacyFileName);
                if (!File.Exists(legacy)) return;

                var data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(legacy));
                if (data != null)
                {
                    string id = NewSlotId();
                    data.slotId = id;
                    if (string.IsNullOrEmpty(data.characterName)) data.characterName = "İlk Karakter";
                    if (string.IsNullOrEmpty(data.createdAt)) data.createdAt = string.IsNullOrEmpty(data.saveTimestamp) ? Now() : data.saveTimestamp;
                    File.WriteAllText(SlotPath(id), JsonUtility.ToJson(data, true));
                    if (ReadProfile() == null) WriteProfile(id);
                    Debug.Log($"[SaveManager] Eski kayıt '{data.characterName}' karakter yuvasına taşındı ({id}).");
                }

                string backup = legacy + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(legacy, backup);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Eski kayıt taşınamadı: {ex.Message}");
            }
        }

        // ==========================================
        // AKTİF YUVA: KAYDET / YÜKLE
        // ==========================================

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
                    saveTimestamp = Now(),
                    slotId = ActiveSlotId,
                    characterName = !string.IsNullOrEmpty(stats.CharacterName) ? stats.CharacterName : (pendingCharacterName ?? "İsimsiz"),
                    createdAt = !string.IsNullOrEmpty(stats.CreatedAt) ? stats.CreatedAt : Now(),
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

                if (data.hasSavedBonfire)
                {
                    data.lastBonfireId = stats.LastBonfireId ?? "";
                    data.lastBonfireName = stats.LastBonfireName ?? "";
                    data.lastBonfireScene = stats.LastBonfireScene ?? "";
                }

                data.defeatedBosses = new System.Collections.Generic.List<string>(stats.DefeatedBosses);

                var itemBox = stats.GetComponent<Items.ItemBox>();
                if (itemBox != null) itemBox.WriteToSave(data);

                if (stats.BloodstainPosition.HasValue && stats.BloodstainSouls > 0)
                {
                    data.hasBloodstain = true;
                    data.bloodstainSouls = stats.BloodstainSouls;
                    data.bloodstainX = stats.BloodstainPosition.Value.x;
                    data.bloodstainY = stats.BloodstainPosition.Value.y;
                    data.bloodstainZ = stats.BloodstainPosition.Value.z;
                }

                string json = JsonUtility.ToJson(data, true);
                Directory.CreateDirectory(SaveFolder);
                File.WriteAllText(SaveFilePath, json);
                pendingCharacterName = null;
                WriteProfile(ActiveSlotId); // Continue bu karakteri açsın

                GameLog.Info($"[SaveManager] Kaydedildi (Level: {data.soulLevel}, Ruh: {data.currentSouls}, Bonfire: {(data.hasSavedBonfire ? data.lastBonfireName : "yok")}, Dosya: {SaveFilePath})");
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
                GameLog.Info($"[SaveManager] Kayıt yüklendi (Level: {data.soulLevel}, Ruh: {data.currentSouls})");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Yükleme hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Test kolaylığı: kayıttaki yenilmiş boss listesini boşaltır, diğer her şeyi (seviye, ruh, bonfire, itemlar) korur.
        /// Kaç boss'un sıfırlandığını döndürür (kayıt yoksa -1).
        /// </summary>
        public static int ClearDefeatedBossesInFile()
        {
            var data = LoadPlayer();
            if (data == null) return -1;
            int count = data.defeatedBosses != null ? data.defeatedBosses.Count : 0;
            data.defeatedBosses = new System.Collections.Generic.List<string>();
            try
            {
                File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Kayıt yazılamadı: {ex.Message}");
                return -1;
            }
            return count;
        }

        public static void DeleteSave()
        {
            if (HasSaveFile())
            {
                File.Delete(SaveFilePath);
                GameLog.Info("[SaveManager] Kayıt dosyası silindi");
            }
        }
    }
}