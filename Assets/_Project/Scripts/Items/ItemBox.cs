using System;
using System.Collections.Generic;
using UnityEngine;
using SoulsRPG.Player;
using SoulsRPG.Save;

namespace SoulsRPG.Items
{
    /// <summary>
    /// Oyuncunun item kutusu: hangi itemdan kaç adet taşıdığı. Kayıt dosyasına yazılır.
    ///  - Add / TryConsume / GetCount: ileride yerden toplama, sandık, satıcı vb. bunu kullanacak
    ///  - refillAtBonfire işaretli itemlar (Can İksiri) bonfire'da dinlenince ve ölüp doğunca dolar
    /// PlayerStats.Awake tarafından oyuncuya otomatik eklenir.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class ItemBox : MonoBehaviour
    {
        [Serializable]
        public class Stack
        {
            public string id;
            public int count;
        }

        [SerializeField] private List<Stack> stacks = new List<Stack>();

        /// <summary>Herhangi bir item adedi değişince (id, yeni adet).</summary>
        public event Action<string, int> OnItemCountChanged;

        public IReadOnlyList<Stack> Stacks => stacks;

        private PlayerStats stats;
        private bool loaded;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            stats.OnRespawn += RefillAtBonfire;
            stats.OnRestedAtBonfire += RefillAtBonfire;
        }

        private void OnDisable()
        {
            if (stats == null) return;
            stats.OnRespawn -= RefillAtBonfire;
            stats.OnRestedAtBonfire -= RefillAtBonfire;
        }

        private void Start()
        {
            // PlayerStats kayıt yüklemeyi çağırmadıysa (ör. kayıt yok) başlangıç itemlarını ver
            if (!loaded) LoadFromSave(null);
        }

        // ==========================================
        // SORGULAMA
        // ==========================================

        public int GetCount(string id)
        {
            var s = Find(id);
            return s != null ? s.count : 0;
        }

        public int GetMaxCount(string id)
        {
            var def = ItemDatabase.Get(id);
            return def != null ? def.maxStack : 0;
        }

        public bool Has(string id, int amount = 1) => GetCount(id) >= amount;

        // ==========================================
        // DEĞİŞTİRME
        // ==========================================

        /// <summary>Item ekler (maxStack sınırına kadar). Gerçekten eklenen adedi döndürür.</summary>
        public int Add(string id, int amount = 1)
        {
            if (amount <= 0) return 0;
            var def = ItemDatabase.Get(id);
            if (def == null)
            {
                Debug.LogWarning($"[ItemBox] Bilinmeyen item: '{id}'");
                return 0;
            }

            var s = Find(id) ?? CreateStack(id);
            int before = s.count;
            s.count = Mathf.Min(def.maxStack, s.count + amount);
            int added = s.count - before;
            if (added > 0) OnItemCountChanged?.Invoke(id, s.count);
            return added;
        }

        /// <summary>Yeterli adet varsa düşer ve true döner; yoksa hiçbir şey yapmaz.</summary>
        public bool TryConsume(string id, int amount = 1)
        {
            var s = Find(id);
            if (s == null || s.count < amount) return false;
            s.count -= amount;
            OnItemCountChanged?.Invoke(id, s.count);
            return true;
        }

        /// <summary>Bonfire'da dinlenince / ölüp doğunca: yenilenen itemlar (iksir) tam dolar.</summary>
        public void RefillAtBonfire()
        {
            foreach (var def in ItemDatabase.All)
            {
                if (!def.refillAtBonfire) continue;
                var s = Find(def.id) ?? CreateStack(def.id);
                if (s.count == def.maxStack) continue;
                s.count = def.maxStack;
                OnItemCountChanged?.Invoke(def.id, s.count);
            }
        }

        // ==========================================
        // KAYIT
        // ==========================================

        /// <summary>Kayıttan yükler. Kayıt yoksa veya item kutusu içermiyorsa başlangıç itemlarını verir.</summary>
        public void LoadFromSave(PlayerSaveData data)
        {
            loaded = true;
            stacks.Clear();

            if (data != null && data.hasItemBox && data.items != null)
            {
                foreach (var saved in data.items)
                {
                    if (string.IsNullOrEmpty(saved.id)) continue;
                    var def = ItemDatabase.Get(saved.id);
                    int count = def != null ? Mathf.Clamp(saved.count, 0, def.maxStack) : saved.count; // bilinmeyen item kaybolmasın
                    stacks.Add(new Stack { id = saved.id, count = count });
                }
            }
            else
            {
                foreach (var def in ItemDatabase.All)
                {
                    if (def.startingCount > 0) stacks.Add(new Stack { id = def.id, count = Mathf.Min(def.startingCount, def.maxStack) });
                }
            }

            foreach (var s in stacks) OnItemCountChanged?.Invoke(s.id, s.count);
        }

        public void WriteToSave(PlayerSaveData data)
        {
            data.hasItemBox = true;
            data.items = new List<SavedItemStack>();
            foreach (var s in stacks)
            {
                data.items.Add(new SavedItemStack { id = s.id, count = s.count });
            }
        }

        // ------------------------------------------------------------------

        private Stack Find(string id)
        {
            foreach (var s in stacks)
            {
                if (s.id == id) return s;
            }
            return null;
        }

        private Stack CreateStack(string id)
        {
            var s = new Stack { id = id, count = 0 };
            stacks.Add(s);
            return s;
        }
    }
}
