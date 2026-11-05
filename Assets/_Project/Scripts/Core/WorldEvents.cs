using System;
using UnityEngine;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Dünya çapındaki olaylar. Bonfire'da dinlenince veya oyuncu ölüp yeniden doğunca
    /// tüm düşmanlar ve boss başlangıç durumlarına döner (Souls kuralı).
    /// </summary>
    public static class WorldEvents
    {
        public static event Action OnWorldReset;

        public static void ResetWorld()
        {
            OnWorldReset?.Invoke();
        }

        // "Enter Play Mode Options" ile domain reload kapalıyken eski abonelikleri temizle
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            OnWorldReset = null;
        }
    }

    /// <summary>
    /// Bilgi amaçlı logları tek yerden açıp kapatmak için. Uyarı ve hatalar her zaman Debug ile yazılır.
    /// </summary>
    public static class GameLog
    {
        public static bool Verbose = false;

        public static void Info(string message)
        {
            if (Verbose) Debug.Log(message);
        }
    }
}
