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

        [Header("Dinamik Bar Uzunluğu (Souls tarzı)")]
        [Tooltip("Açıkken can/stamina barı, maksimum değer arttıkça sağa doğru uzar")]
        [SerializeField] private bool dynamicBarLength = true;
        [Tooltip("Sahnedeki bar genişliğinin karşılık geldiği maksimum can (Vigor 10 = 100 HP)")]
        [SerializeField] private float healthReferenceMax = 100f;
        [Tooltip("Sahnedeki bar genişliğinin karşılık geldiği maksimum stamina (Endurance 10 = 120)")]
        [SerializeField] private float staminaReferenceMax = 120f;
        [Tooltip("Barların ekrandan taşmaması için en fazla bu kadar katına uzayabilir")]
        [SerializeField] private float maxLengthMultiplier = 2.6f;
        [Tooltip("Uzama animasyonu hızı (0 = anında)")]
        [SerializeField] private float lengthAnimSpeed = 6f;

        private RectTransform healthRect, healthEaseRect, staminaRect;
        private float healthBaseWidth, healthEaseBaseWidth, staminaBaseWidth;
        private float healthTargetMul = 1f, staminaTargetMul = 1f;
        private float healthCurrentMul = 1f, staminaCurrentMul = 1f;
        private bool widthsCaptured;

        private void Awake()
        {
            CaptureBaseWidths();
        }

        /// <summary>Sahnede ayarlanmış bar genişliklerini referans (taban) olarak kaydeder.</summary>
        private void CaptureBaseWidths()
        {
            if (widthsCaptured) return;
            healthRect = healthSlider != null ? healthSlider.GetComponent<RectTransform>() : null;
            healthEaseRect = healthEaseSlider != null ? healthEaseSlider.GetComponent<RectTransform>() : null;
            staminaRect = staminaSlider != null ? staminaSlider.GetComponent<RectTransform>() : null;

            if (healthRect != null) healthBaseWidth = healthRect.rect.width;
            if (healthEaseRect != null) healthEaseBaseWidth = healthEaseRect.rect.width;
            if (staminaRect != null) staminaBaseWidth = staminaRect.rect.width;
            widthsCaptured = true;
        }

        private float LengthMultiplier(float max, float reference)
        {
            if (!dynamicBarLength || reference <= 0f) return 1f;
            return Mathf.Clamp(max / reference, 0.5f, maxLengthMultiplier);
        }

        private static void SetWidth(RectTransform rect, float width)
        {
            if (rect == null || width <= 0f) return;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void ApplyBarLengths(bool instant)
        {
            if (instant || lengthAnimSpeed <= 0f)
            {
                healthCurrentMul = healthTargetMul;
                staminaCurrentMul = staminaTargetMul;
            }
            else
            {
                float k = 1f - Mathf.Exp(-lengthAnimSpeed * Time.unscaledDeltaTime);
                healthCurrentMul = Mathf.Lerp(healthCurrentMul, healthTargetMul, k);
                staminaCurrentMul = Mathf.Lerp(staminaCurrentMul, staminaTargetMul, k);
                if (Mathf.Abs(healthCurrentMul - healthTargetMul) < 0.001f) healthCurrentMul = healthTargetMul;
                if (Mathf.Abs(staminaCurrentMul - staminaTargetMul) < 0.001f) staminaCurrentMul = staminaTargetMul;
            }

            SetWidth(healthRect, healthBaseWidth * healthCurrentMul);
            SetWidth(healthEaseRect, healthEaseBaseWidth * healthCurrentMul);
            SetWidth(staminaRect, staminaBaseWidth * staminaCurrentMul);
        }

        private void Start()
        {
            CaptureBaseWidths();

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

            // Oyun başında (kayıttan yüklenen stat'larla) barlar animasyonsuz doğru uzunlukta başlasın
            ApplyBarLengths(true);
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

            // Seviye atlayınca barlar yumuşakça uzar
            if (healthCurrentMul != healthTargetMul || staminaCurrentMul != staminaTargetMul)
            {
                ApplyBarLengths(false);
            }
        }

        private void UpdateHealthBar(int current, int max)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }
            if (healthEaseSlider != null)
            {
                healthEaseSlider.maxValue = max;
                // Can arttıysa (iyileşme/seviye) sarı bar geride kalmasın
                if (healthEaseSlider.value < current) healthEaseSlider.value = current;
            }
            healthTargetMul = LengthMultiplier(max, healthReferenceMax);
        }

        private void UpdateStaminaBar(float current, float max)
        {
            if (staminaSlider != null)
            {
                staminaSlider.maxValue = max;
                staminaSlider.value = current;
            }
            staminaTargetMul = LengthMultiplier(max, staminaReferenceMax);
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

            Canvas canvas = GetComponentInParent<Canvas>() ?? HudCanvas.Get();
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