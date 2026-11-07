using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoulsRPG.UI
{
    /// <summary>
    /// Dark Souls tarzı ekranın altında beliren görkemli Boss Can Barı ve 'DÜŞMAN KATLEDİLDİ' zafer afişi.
    /// </summary>
    public class BossHealthBarHUD : MonoBehaviour
    {
        public static BossHealthBarHUD Instance { get; private set; }

        [Header("Boss Identity UI")]
        [SerializeField] private CanvasGroup bossBarCanvasGroup;
        [SerializeField] private Text bossNameText;
        [SerializeField] private Text bossTitleText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider healthEaseSlider;

        [Header("Victory Banner UI")]
        [SerializeField] private CanvasGroup victoryBannerCanvasGroup;
        [SerializeField] private Text victoryBannerText;
        [SerializeField] private Text soulsRewardText;

        private float targetHealthValue = 1f;
        private bool isBossActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureUIElementsCreated();
            HideBossBarImmediate();
            HideVictoryBannerImmediate();
        }

        private void Update()
        {
            // Yellow ease bar smoothly catches up to main health slider
            if (healthEaseSlider != null && healthSlider != null)
            {
                if (healthEaseSlider.value > healthSlider.value)
                {
                    healthEaseSlider.value = Mathf.Lerp(healthEaseSlider.value, healthSlider.value, Time.deltaTime * 3.5f);
                }
            }
        }

        public void ShowBoss(string name, string title, int currentHealth, int maxHealth)
        {
            isBossActive = true;
            if (bossNameText != null) bossNameText.text = name;
            if (bossTitleText != null) bossTitleText.text = title;

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }
            if (healthEaseSlider != null)
            {
                healthEaseSlider.maxValue = maxHealth;
                healthEaseSlider.value = currentHealth;
            }

            StartCoroutine(FadeCanvasGroup(bossBarCanvasGroup, 0f, 1f, 1.2f));
        }

        public void UpdateBossHealth(int currentHealth, int maxHealth)
        {
            if (healthSlider != null)
            {
                healthSlider.value = currentHealth;
            }
        }

        public void SetPhase2Title(string phase2Title)
        {
            if (bossTitleText != null)
            {
                if (!hasOriginalTitleColor)
                {
                    originalTitleColor = bossTitleText.color;
                    hasOriginalTitleColor = true;
                }
                bossTitleText.text = phase2Title;
                bossTitleText.color = new Color(1.0f, 0.45f, 0.2f); // Fiery orange/red
            }
        }

        private Color originalTitleColor;
        private bool hasOriginalTitleColor;

        /// <summary>Boss sıfırlanınca (oyuncu ölünce) can barını hemen gizler.</summary>
        public void HideBoss()
        {
            isBossActive = false;
            StopAllCoroutines();
            HideBossBarImmediate();
            HideVictoryBannerImmediate();
            if (bossTitleText != null && hasOriginalTitleColor)
            {
                bossTitleText.color = originalTitleColor;
            }
        }

        public void OnBossDefeated(int soulsAwarded = 3000)
        {
            isBossActive = false;
            StartCoroutine(BossDefeatedSequence(soulsAwarded));
        }

        private IEnumerator BossDefeatedSequence(int soulsAwarded)
        {
            // 1. Can barını 1.5 saniye sonra yavaşça kapat
            yield return new WaitForSeconds(1.2f);
            yield return StartCoroutine(FadeCanvasGroup(bossBarCanvasGroup, 1f, 0f, 1.5f));

            // 2. 'DÜŞMAN KATLEDİLDİ' Zafer afişini aç
            if (soulsRewardText != null)
            {
                soulsRewardText.text = $"+{soulsAwarded:N0} RUH KAZANILDI";
            }

            yield return StartCoroutine(FadeCanvasGroup(victoryBannerCanvasGroup, 0f, 1f, 1.8f));
            yield return new WaitForSeconds(3.5f);
            yield return StartCoroutine(FadeCanvasGroup(victoryBannerCanvasGroup, 1f, 0f, 2.0f));
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;

            float elapsed = 0f;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            cg.alpha = to;
        }

        private void HideBossBarImmediate()
        {
            if (bossBarCanvasGroup != null) bossBarCanvasGroup.alpha = 0f;
        }

        private void HideVictoryBannerImmediate()
        {
            if (victoryBannerCanvasGroup != null) victoryBannerCanvasGroup.alpha = 0f;
        }

        private static Font GetDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return font;
        }

        /// <summary>
        /// Sahneye elle prefab koyulmamışsa Canvas altında Dark Souls stili Boss Bar ve Banner oluşturur.
        /// </summary>
        public void EnsureUIElementsCreated()
        {
            if (bossBarCanvasGroup != null && victoryBannerCanvasGroup != null) return;

            Font defaultFont = GetDefaultFont();

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = HudCanvas.Get();
            }

            if (canvas == null)
            {
                var canvasGo = new GameObject("--- SOULS HUD CANVAS ---");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // 1. Boss Bar Container (Bottom Center)
            var bossBarGo = new GameObject("BossHealthBar_Container");
            bossBarGo.transform.SetParent(canvas.transform, false);
            var rect = bossBarGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 48f);
            rect.sizeDelta = new Vector2(640f, 54f);

            bossBarCanvasGroup = bossBarGo.AddComponent<CanvasGroup>();
            bossBarCanvasGroup.alpha = 0f;

            // Boss Title (Subtitle)
            var titleGo = new GameObject("BossTitleText");
            titleGo.transform.SetParent(bossBarGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, 0f);
            titleRect.sizeDelta = new Vector2(0f, 16f);
            bossTitleText = titleGo.AddComponent<Text>();
            bossTitleText.font = defaultFont;
            bossTitleText.fontSize = 11;
            bossTitleText.alignment = TextAnchor.MiddleCenter;
            bossTitleText.color = new Color(0.8f, 0.75f, 0.65f, 0.9f);
            bossTitleText.text = "LANETLİ KALE MUHAFIZI";

            // Boss Name Text
            var nameGo = new GameObject("BossNameText");
            nameGo.transform.SetParent(bossBarGo.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -14f);
            nameRect.sizeDelta = new Vector2(0f, 22f);
            bossNameText = nameGo.AddComponent<Text>();
            bossNameText.font = defaultFont;
            bossNameText.fontStyle = FontStyle.Bold;
            bossNameText.fontSize = 15;
            bossNameText.alignment = TextAnchor.MiddleCenter;
            bossNameText.color = new Color(0.95f, 0.88f, 0.72f); // Souls Gold
            bossNameText.text = "GÖLGE ŞÖVALYE KUMANDANI - MALAKOR";

            // Bar Background Frame
            var bgGo = new GameObject("BarBackground");
            bgGo.transform.SetParent(bossBarGo.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(1f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0f);
            bgRect.anchoredPosition = new Vector2(0f, 0f);
            bgRect.sizeDelta = new Vector2(0f, 14f);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

            // Ease Slider (Yellow damage lag bar)
            healthEaseSlider = CreateBarSlider(bossBarGo.transform, "HealthEaseSlider", new Color(0.85f, 0.65f, 0.15f, 0.9f), 14f);

            // Main Health Slider (Dark Souls Crimson)
            healthSlider = CreateBarSlider(bossBarGo.transform, "HealthSlider", new Color(0.72f, 0.08f, 0.08f, 1.0f), 14f);

            // 2. Victory Banner ("DÜŞMAN KATLEDİLDİ")
            var victoryGo = new GameObject("VictoryBanner_Container");
            victoryGo.transform.SetParent(canvas.transform, false);
            var vRect = victoryGo.AddComponent<RectTransform>();
            vRect.anchorMin = new Vector2(0.5f, 0.5f);
            vRect.anchorMax = new Vector2(0.5f, 0.5f);
            vRect.pivot = new Vector2(0.5f, 0.5f);
            vRect.anchoredPosition = new Vector2(0f, 40f);
            vRect.sizeDelta = new Vector2(900f, 140f);

            victoryBannerCanvasGroup = victoryGo.AddComponent<CanvasGroup>();
            victoryBannerCanvasGroup.alpha = 0f;

            // Victory Text
            var vTextGo = new GameObject("VictoryText");
            vTextGo.transform.SetParent(victoryGo.transform, false);
            var vtRect = vTextGo.AddComponent<RectTransform>();
            vtRect.anchorMin = Vector2.zero;
            vtRect.anchorMax = Vector2.one;
            vtRect.sizeDelta = Vector2.zero;
            victoryBannerText = vTextGo.AddComponent<Text>();
            victoryBannerText.font = defaultFont;
            victoryBannerText.fontStyle = FontStyle.Bold;
            victoryBannerText.fontSize = 42;
            victoryBannerText.alignment = TextAnchor.MiddleCenter;
            victoryBannerText.color = new Color(1.0f, 0.88f, 0.45f); // Brilliant Gold
            victoryBannerText.text = "D Ü Ş M A N   K A T L E D İ L D İ";

            // Souls Text
            var soulsGo = new GameObject("SoulsRewardText");
            soulsGo.transform.SetParent(victoryGo.transform, false);
            var sRect = soulsGo.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0f);
            sRect.anchorMax = new Vector2(1f, 0f);
            sRect.pivot = new Vector2(0.5f, 0f);
            sRect.anchoredPosition = new Vector2(0f, -28f);
            sRect.sizeDelta = new Vector2(0f, 26f);
            soulsRewardText = soulsGo.AddComponent<Text>();
            soulsRewardText.font = defaultFont;
            soulsRewardText.fontSize = 18;
            soulsRewardText.alignment = TextAnchor.MiddleCenter;
            soulsRewardText.color = new Color(0.85f, 0.90f, 1.0f, 0.9f);
            soulsRewardText.text = "+3,000 RUH KAZANILDI";
        }

        private Slider CreateBarSlider(Transform parent, string name, Color fillColor, float height)
        {
            var sliderGo = new GameObject(name);
            sliderGo.transform.SetParent(parent, false);
            var rect = sliderGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;
            slider.transition = Selectable.Transition.None;

            return slider;
        }

        public static BossHealthBarHUD GetOrCreate()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<BossHealthBarHUD>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("--- BOSS HUD MANAGER ---");
            Instance = go.AddComponent<BossHealthBarHUD>();
            return Instance;
        }
    }
}