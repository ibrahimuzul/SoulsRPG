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
        [SerializeField] private Slider healthEaseSlider; // The delayed yellow bar when taking damage

        [Header("Stamina Bar UI")]
        [SerializeField] private Slider staminaSlider;

        private void Start()
        {
            if (playerStats == null)
            {
                playerStats = PlayerStats.Instance != null ? PlayerStats.Instance : FindAnyObjectByType<PlayerStats>();
            }

            if (playerStats != null)
            {
                playerStats.OnHealthChanged += UpdateHealthBar;
                playerStats.OnStaminaChanged += UpdateStaminaBar;

                UpdateHealthBar(playerStats.CurrentHealth, playerStats.MaxHealth);
                UpdateStaminaBar(playerStats.CurrentStamina, playerStats.MaxStamina);
            }
        }

        private void OnDestroy()
        {
            if (playerStats != null)
            {
                playerStats.OnHealthChanged -= UpdateHealthBar;
                playerStats.OnStaminaChanged -= UpdateStaminaBar;
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
    }
}
