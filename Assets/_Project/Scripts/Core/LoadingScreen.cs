using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SoulsRPG.Core
{
    /// <summary>
    /// Menüden oyuna geçişteki yükleme ekranı.
    /// Core sahnesini yükler (menü sahnesinin yerine), sonra oyuncu son bonfire'ın bölgesine yerleşene kadar
    /// (WorldEvents.OnWorldReady) ekranda kalır. Sahne değişiminde silinmez (DontDestroyOnLoad).
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const float WorldReadyTimeout = 60f;

        private CanvasGroup group;
        private Image barFill;
        private Text statusText;
        private float displayedProgress;

        /// <summary>Yükleme ekranını açar ve dünyayı yükler.</summary>
        public static void LoadWorld()
        {
            var go = new GameObject("--- LOADING SCREEN ---");
            DontDestroyOnLoad(go);
            var screen = go.AddComponent<LoadingScreen>();
            screen.Build();
            screen.StartCoroutine(screen.LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            WorldEvents.ResetWorldReady();

            // 1. Kararma
            yield return Fade(0f, 1f, 0.35f);

            // 2. Core (oyuncu, kamera, HUD) — menü sahnesinin yerine
            var map = WorldMap.Instance;
            string coreScene = map != null ? map.coreScene : WorldMap.CoreSceneName;
            SetStatus("Dünya yükleniyor");

            var op = SceneManager.LoadSceneAsync(coreScene, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[LoadingScreen] '{coreScene}' yüklenemedi. Build listesinde olduğundan emin ol.");
                yield break;
            }
            while (!op.isDone)
            {
                SetProgress(Mathf.Clamp01(op.progress / 0.9f) * 0.5f);
                yield return null;
            }

            // 3. Oyuncunun bölgesi yükleniyor ve yerleşiyor
            SetStatus("Ateş yakılıyor");
            float waited = 0f;
            while (!WorldEvents.IsWorldReady && waited < WorldReadyTimeout)
            {
                waited += Time.unscaledDeltaTime;
                // Gerçek ilerleme bilinmiyor: %50'den %95'e yavaşça yaklaş
                SetProgress(Mathf.Lerp(displayedProgress, 0.95f, Time.unscaledDeltaTime * 1.5f));
                yield return null;
            }
            if (!WorldEvents.IsWorldReady)
            {
                Debug.LogWarning("[LoadingScreen] Dünya hazır sinyali gelmedi; yükleme ekranı yine de kapatılıyor.");
            }

            // 4. Kapat (altında Core'un siyah perdesi açılıyor)
            SetProgress(1f);
            yield return Fade(1f, 0f, 0.3f);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ UI

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            gameObject.AddComponent<NotHudCanvas>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var bg = CreateRect("Background", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = Color.black;

            // Durum yazısı (sağ alt, Souls tarzı sade)
            var textRect = CreateRect("Status", transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-460f, 70f), new Vector2(-60f, 110f));
            statusText = textRect.gameObject.AddComponent<Text>();
            statusText.font = font;
            statusText.fontSize = 26;
            statusText.alignment = TextAnchor.MiddleRight;
            statusText.color = new Color(0.85f, 0.78f, 0.62f);

            // İnce ilerleme çubuğu
            var barBg = CreateRect("BarBg", transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-460f, 56f), new Vector2(-60f, 60f));
            barBg.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.18f, 0.15f, 1f);
            var fill = CreateRect("BarFill", barBg, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            barFill = fill.gameObject.AddComponent<Image>();
            barFill.color = new Color(0.95f, 0.6f, 0.2f, 1f);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private string baseStatus = "";

        private void SetStatus(string text)
        {
            baseStatus = text;
        }

        private void SetProgress(float value)
        {
            displayedProgress = Mathf.Max(displayedProgress, value);
            if (barFill != null) barFill.rectTransform.anchorMax = new Vector2(displayedProgress, 1f);
        }

        private void Update()
        {
            if (statusText == null) return;
            int dots = (int)(Time.unscaledTime * 2f) % 4;
            statusText.text = baseStatus + new string('.', dots);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
