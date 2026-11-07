using UnityEngine;
using SoulsRPG.Core;

namespace SoulsRPG.UI
{
    /// <summary>
    /// Oyunun ana HUD canvas'ını işaretler (Core sahnesindeki "--- SOULS HUD CANVAS ---").
    /// HUD'lar, bonfire yazısı ve menüler buraya yerleşir. Birden fazla sahne yüklüyken
    /// FindAnyObjectByType&lt;Canvas&gt;() bir bölge sahnesindeki canvas'ı bulup bölge boşaltılınca
    /// UI'ı da silebiliyordu; bu sınıf bunu önler.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HudCanvas : MonoBehaviour
    {
        private static HudCanvas instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance == null) instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// Ana HUD canvas'ı. Sıra: işaretli canvas → oyuncunun sahnesindeki canvas → herhangi bir HUD canvas'ı → yeni canvas.
        /// </summary>
        public static Canvas Get()
        {
            if (instance != null) return instance.GetComponent<Canvas>();

            var marked = Object.FindAnyObjectByType<HudCanvas>();
            if (marked != null)
            {
                instance = marked;
                return marked.GetComponent<Canvas>();
            }

            var playerScene = Player.PlayerStats.Instance != null ? Player.PlayerStats.Instance.gameObject.scene : default;
            Canvas fallback = null;
            foreach (var c in Object.FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || c.GetComponent<NotHudCanvas>() != null) continue;
                if (playerScene.IsValid() && c.gameObject.scene == playerScene)
                {
                    instance = c.gameObject.AddComponent<HudCanvas>();
                    return c;
                }
                if (fallback == null) fallback = c;
            }

            if (fallback != null) return fallback;

            // Hiç canvas yoksa oluştur (aktif sahne = Core olduğu için oraya düşer)
            var go = new GameObject("--- SOULS HUD CANVAS ---");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            instance = go.AddComponent<HudCanvas>();
            return canvas;
        }
    }
}
