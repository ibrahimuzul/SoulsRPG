using UnityEngine;

namespace SoulsRPG.Items
{
    public enum ItemCategory
    {
        Consumable, // kullanılınca tükenen (iksir, yiyecek)
        Material,   // yükseltme/craft malzemesi
        Key,        // anahtar, görev eşyası
    }

    /// <summary>
    /// Bir item türünün sabit bilgileri. Oyuncunun elindeki adet ItemBox'ta tutulur.
    /// Yeni item eklemek için: Project penceresinde sağ tık → Create → SoulsRPG → Item,
    /// asset'i Assets/_Project/Resources/Items klasörüne koy (ItemDatabase oradan otomatik okur).
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "SoulsRPG/Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Kimlik")]
        [Tooltip("Kayıtta tutulan benzersiz kimlik (değiştirirsen eski kayıtlardaki adetler kaybolur)")]
        public string id = "yeni_item";
        public string displayName = "Yeni Item";
        [TextArea] public string description = "";
        [Tooltip("Boş bırakılırsa koddan basit bir ikon üretilir")]
        public Sprite icon;
        public ItemCategory category = ItemCategory.Consumable;

        [Header("Yığın")]
        [Tooltip("ItemBox'ta en fazla kaç adet taşınabilir")]
        public int maxStack = 99;
        [Tooltip("Bonfire'da dinlenince ve ölüp doğunca adet maxStack'e tamamlanır (iksir gibi)")]
        public bool refillAtBonfire = false;
        [Tooltip("Yeni oyunda (kayıt yokken) başlangıç adedi")]
        public int startingCount = 0;

        [Header("Kullanım (Consumable)")]
        [Tooltip("Verilen can miktarı")]
        public int healAmount = 0;
        [Tooltip("Kullanım animasyonunun toplam süresi (saniye). Bu sürede saldırı/yuvarlanma/blok yapılamaz")]
        public float useDuration = 1.1f;
        [Tooltip("Etkinin uygulandığı an (useDuration içinde). Bundan önce sendelersen item boşa gider")]
        public float effectTime = 0.65f;
        [Tooltip("Kullanırken yürüme hızı çarpanı")]
        [Range(0f, 1f)] public float moveSpeedWhileUsing = 0.35f;
        [Tooltip("Kullanım efekt rengi (parçacık ve ışık)")]
        public Color effectColor = new Color(1f, 0.62f, 0.2f);
    }
}
