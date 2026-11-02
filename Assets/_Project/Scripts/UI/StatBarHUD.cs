using UnityEngine;
using UnityEngine.UI;
using SoulsRPG.Player;

namespace SoulsRPG.UI
{
    public class StatBarHUD : MonoBehaviour
    {
        [Header("Player Reference")]
        [SerializeField] private PlayerStats playerStats;

        [Header("Health Bar UI")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider healthEaseSlider;

        [Header("Stamina Bar UI")]
        [SerializeField] private Slider staminaSlider;

        [Header("Souls Counter UI")]
        [SerializeField] private Text soulsCounterText;

        private void Start()
        {
            if (playerStats == null)
            {
                playerStats = PlayerStats.Instance != null ? PlayerStats.Instance : FindAnyObjectByType<PlayerStats>();
            }

            EnsureSoulsCounterCreated();

            if (playerStats != null)
            {
                playerStats.OnHealthChanged += UpdateHealthBar;
                playerStats.OnStaminaChanged += UpdateStaminaBar;
                playerStats.OnSoulsChanged += UpdateSoulsDisplay;

                UpdateHealthBar(playerStats.CurrentHealth, playerStats.MaxHealth);
                UpdateStaminaBar(playerStats.CurrentStamina, playerStats.MaxStamina);
                UpdateSoulsDisplay(playerStats.CurrentSouls);
            }
        }

        private void OnDestroy()
        {
            if (playerStats != null)
            {
                playerStats.OnHealthChanged -= UpdateHealthBar;
                playerStats.OnStaminaChanged -= UpdateStaminaBar;
                playerStats.OnSoulsChanged -= UpdateSoulsDisplay;
            }
        }

        private void Update()
        {
            // Ease health bar down smoothly
            if (healthEaseSlider != null && healthSlider != null)
            {
                if (healthEaseSlider.value > healthSlider.value)
                {
                    healthEaseSlider.value = Mathf.Lerp(healthEaseSlider.value, healthSlider.value, Time.deltaTime * 5f);
                }
            }
        }

        private void UpdateHealthBar(int current, int max)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }
        }

        private void UpdateStaminaBar(float current, float max)
        {
            if (staminaSlider != null)
            {
                staminaSlider.maxValue = max;
                staminaSlider.value = current;
            }
        }

        private void UpdateSoulsDisplay(int currentSouls)
        {
            if (soulsCounterText != null)
            {
                soulsCounterText.text = $"✦  {currentSouls:N0}";
            }
        }

        private void EnsureSoulsCounterCreated()
        {
            if (soulsCounterText != null) return;

            Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            var existing = canvas.transform.Find("SoulsCounter_Container");
            if (existing != null)
            {
                soulsCounterText = existing.GetComponentInChildren<Text>();
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var container = new GameObject("SoulsCounter_Container");
            container.transform.SetParent(canvas.transform, false);

            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-28f, 26f);
            rect.sizeDelta = new Vector2(180f, 38f);

            var bg = container.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.08f, 0.85f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(container.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            soulsCounterText = textGo.AddComponent<Text>();
            soulsCounterText.font = font;
            soulsCounterText.fontSize = 16;
            soulsCounterText.fontStyle = FontStyle.Bold;
            soulsCounterText.alignment = TextAnchor.MiddleCenter;
            soulsCounterText.color = new Color(0.95f, 0.88f, 0.55f); // Dark Souls Gold
            soulsCounterText.text = "✦  0";
        }
    }
}