using UnityEngine;
using UnityEngine.EventSystems;

namespace SoulsRPG.UI
{
    /// <summary>
    /// Kodla oluşturulan yedek EventSystem'e eklenir. Sahnede (Core) asıl bir EventSystem varsa yedek kendini siler.
    /// İki EventSystem olunca Unity her karede uyarı basıyor ve editör ciddi yavaşlıyordu.
    /// </summary>
    public class RuntimeEventSystemGuard : MonoBehaviour
    {
        private float timer;

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.5f;

            var mine = GetComponent<EventSystem>();
            foreach (var es in FindObjectsByType<EventSystem>())
            {
                if (es != mine)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
