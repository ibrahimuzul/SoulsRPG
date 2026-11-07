using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Tam ekran siyah perde. Ölümde, ışınlanmada ve bölge yüklenirken yüklemeyi gizlemek için kullanılır.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader instance;
        private Image overlay;

        public static ScreenFader Instance
        {
            get
            {
                if (instance == null) instance = Create();
                return instance;
            }
        }

        public float Alpha => overlay != null ? overlay.color.a : 0f;

        private static ScreenFader Create()
        {
            var go = new GameObject("--- SCREEN FADER ---");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            go.AddComponent<NotHudCanvas>();

            var imgGo = new GameObject("Black");
            imgGo.transform.SetParent(go.transform, false);
            var rect = imgGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var fader = go.AddComponent<ScreenFader>();
            fader.overlay = imgGo.AddComponent<Image>();
            fader.overlay.color = new Color(0f, 0f, 0f, 0f);
            fader.overlay.raycastTarget = false;
            return fader;
        }

        public void SetAlpha(float a)
        {
            if (overlay == null) return;
            var c = overlay.color;
            c.a = Mathf.Clamp01(a);
            overlay.color = c;
        }

        public IEnumerator FadeTo(float target, float duration)
        {
            float start = Alpha;
            if (duration <= 0f)
            {
                SetAlpha(target);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(start, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
