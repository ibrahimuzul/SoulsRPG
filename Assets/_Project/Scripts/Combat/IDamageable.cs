using UnityEngine;

namespace SoulsRPG.Combat
{
    /// <summary>
    /// Dark Souls hasar alabilen tüm varlıklar (Oyuncu, Düşmanlar, Kuklalar) için ortak arayüz
    /// </summary>
    public interface IDamageable
    {
        /// <param name="poiseMultiplier">Denge (poise) hasarı çarpanı. Ağır saldırılar 1'den büyük değer gönderir.</param>
        void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, Transform attacker = null, float poiseMultiplier = 1f);
        bool IsDead { get; }
    }
}
