using System.Collections.Generic;
using UnityEngine;

namespace SoulsRPG.Items
{
    /// <summary>
    /// Tüm item tanımlarının kaydı.
    ///  1. Assets/_Project/Resources/Items altındaki ItemDefinition asset'leri okunur
    ///  2. Oyunun temel itemlarından asset'i olmayanlar koddan oluşturulur (proje asset'siz de çalışsın diye)
    /// </summary>
    public static class ItemDatabase
    {
        public const string ResourcesFolder = "Items";

        // Temel itemların kimlikleri
        public const string HealingPotionId = "can_iksiri";

        private static Dictionary<string, ItemDefinition> items;

        public static IEnumerable<ItemDefinition> All
        {
            get
            {
                EnsureLoaded();
                return items.Values;
            }
        }

        public static ItemDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            return items.TryGetValue(id, out var def) ? def : null;
        }

        private static void EnsureLoaded()
        {
            if (items != null) return;
            items = new Dictionary<string, ItemDefinition>();

            foreach (var def in Resources.LoadAll<ItemDefinition>(ResourcesFolder))
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                if (items.ContainsKey(def.id))
                {
                    Debug.LogWarning($"[ItemDatabase] Aynı kimlikte iki item var: '{def.id}' ({def.name}). İkincisi yok sayıldı.");
                    continue;
                }
                items[def.id] = def;
            }

            foreach (var def in CreateBuiltInItems())
            {
                if (!items.ContainsKey(def.id)) items[def.id] = def;
            }
        }

        private static readonly Dictionary<string, Sprite> generatedIcons = new Dictionary<string, Sprite>();

        /// <summary>
        /// Item'ın ikonu; atanmamışsa koddan üretilen yer tutucu ikon.
        /// (Üretilen ikon asset'e yazılmaz; editörde asset'i kirletmesin.)
        /// </summary>
        public static Sprite GetIcon(ItemDefinition def)
        {
            if (def == null) return null;
            if (def.icon != null) return def.icon;
            if (!generatedIcons.TryGetValue(def.id, out var sprite) || sprite == null)
            {
                sprite = ItemIconFactory.CreateIcon(def);
                generatedIcons[def.id] = sprite;
            }
            return sprite;
        }

        /// <summary>Asset'i olmasa da oyunda hep bulunan temel itemlar.</summary>
        public static List<ItemDefinition> CreateBuiltInItems()
        {
            var potion = ScriptableObject.CreateInstance<ItemDefinition>();
            potion.name = "CanIksiri";
            potion.id = HealingPotionId;
            potion.displayName = "Can İksiri";
            potion.description = "Kehribar renkli, sıcak bir iksir. İçen kişinin yaralarını kapatır. Ateş başında dinlenince yeniden dolar.";
            potion.category = ItemCategory.Consumable;
            potion.maxStack = 3;
            potion.startingCount = 3;
            potion.refillAtBonfire = true;
            potion.healAmount = 400;
            potion.useDuration = 1.1f;
            potion.effectTime = 0.65f;
            potion.moveSpeedWhileUsing = 0.35f;
            potion.effectColor = new Color(1f, 0.62f, 0.2f);

            return new List<ItemDefinition> { potion };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            items = null;
            generatedIcons.Clear();
        }
    }
}
