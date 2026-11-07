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

        /// <summary>
        /// Oyuncu ilk kez yerleşti, çevresindeki bölgeler yüklendi; oyun oynanabilir.
        /// Yükleme ekranı bunu bekleyip kapanır.
        /// </summary>
        public static event Action OnWorldReady;
        public static bool IsWorldReady { get; private set; }

        public static void ResetWorld()
        {
            OnWorldReset?.Invoke();
        }

        public static void RaiseWorldReady()
        {
            IsWorldReady = true;
            OnWorldReady?.Invoke();
        }

        /// <summary>Yeni bir dünya yüklemesi başlarken (menüden Continue / New Game).</summary>
        public static void ResetWorldReady()
        {
            IsWorldReady = false;
        }

        // "Enter Play Mode Options" ile domain reload kapalıyken eski abonelikleri temizle
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            OnWorldReset = null;
            OnWorldReady = null;
            IsWorldReady = false;
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
