using UnityEngine;

namespace SoulsRPG.Combat
{
    /// <summary>
    /// Dark Souls hasar alabilen tüm varlıklar (Oyuncu, Düşmanlar, Kuklalar) için ortak arayüz
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int damage, Vector3 hitPoint, Vector3 hitDirection, Transform attacker = null);
        bool IsDead { get; }
    }
}
