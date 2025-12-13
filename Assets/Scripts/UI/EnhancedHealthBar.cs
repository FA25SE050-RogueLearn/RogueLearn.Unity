using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BossFight2D.UI
{
    /// <summary>
    /// Enhanced health bar with smooth animations, color transitions, and damage numbers.
    /// Attach to any health bar UI element for visual improvements.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class EnhancedHealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private GameObject damageNumberPrefab;

        [Header("Settings")]
        [SerializeField] private bool useThemeColors = true;
        [SerializeField] private bool smoothTransition = true;
        [SerializeField] private float transitionSpeed = 5f;
        [SerializeField] private bool showHealthText = true;
        [SerializeField] private bool showDamageNumbers = true;
        [SerializeField] private bool useSegmentedBar = false;
        [SerializeField] private int segmentCount = 10;

        [Header("Visual Effects")]
        [SerializeField] private bool flashOnDamage = true;
        [SerializeField] private Color damageFlashColor = Color.red;
        [SerializeField] private float flashDuration = 0.2f;
        [SerializeField] private bool pulseOnLowHealth = true;
        [SerializeField] private float lowHealthThreshold = 0.25f;

        private float currentHealth = 1f;
        private float targetHealth = 1f;
        private float maxHealth = 100f;
        private bool isFlashing = false;
        private float flashTimer = 0f;
        private Color originalColor;

        void Awake()
        {
            if (healthSlider == null)
                healthSlider = GetComponent<Slider>();

            if (fillImage == null && healthSlider != null && healthSlider.fillRect != null)
                fillImage = healthSlider.fillRect.GetComponent<Image>();

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (fillImage != null)
                originalColor = fillImage.color;

            // Apply theme colors
            if (useThemeColors && backgroundImage != null)
            {
                backgroundImage.color = UIThemeManager.Instance.healthBackground;
            }
        }

        void Update()
        {
            // Smooth transition to target health
            if (smoothTransition && Mathf.Abs(currentHealth - targetHealth) > 0.01f)
            {
                currentHealth = Mathf.Lerp(currentHealth, targetHealth, Time.deltaTime * transitionSpeed);
                UpdateVisuals();
            }

            // Flash effect
            if (isFlashing)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f)
                {
                    isFlashing = false;
                    if (fillImage != null && useThemeColors)
                    {
                        fillImage.color = UIThemeManager.Instance.GetHealthColor(currentHealth);
                    }
                    else if (fillImage != null)
                    {
                        fillImage.color = originalColor;
                    }
                }
            }

            // Pulse on low health
            if (pulseOnLowHealth && currentHealth <= lowHealthThreshold && fillImage != null)
            {
                float pulse = 0.7f + Mathf.Sin(Time.time * 4f) * 0.3f;
                fillImage.color = UIThemeManager.Instance.healthLow * pulse;
            }
        }

        /// <summary>
        /// Set health value (0-1 normalized)
        /// </summary>
        public void SetHealth(float healthPercent, float maxHealthValue = 100f)
        {
            float previousHealth = targetHealth;
            targetHealth = Mathf.Clamp01(healthPercent);
            maxHealth = maxHealthValue;

            if (!smoothTransition)
            {
                currentHealth = targetHealth;
                UpdateVisuals();
            }

            // Trigger damage flash
            if (flashOnDamage && targetHealth < previousHealth)
            {
                Flash(damageFlashColor, flashDuration);
            }

            // Show damage number
            if (showDamageNumbers && damageNumberPrefab != null && targetHealth < previousHealth)
            {
                float damageTaken = (previousHealth - targetHealth) * maxHealth;
                ShowDamageNumber(damageTaken);
            }
        }

        /// <summary>
        /// Set health using actual values (current / max)
        /// </summary>
        public void SetHealthValues(float current, float max)
        {
            SetHealth(current / max, max);
        }

        private void UpdateVisuals()
        {
            if (healthSlider != null)
            {
                healthSlider.value = currentHealth;
            }

            // Update color based on health
            if (fillImage != null && useThemeColors && !isFlashing && currentHealth > lowHealthThreshold)
            {
                fillImage.color = UIThemeManager.Instance.GetHealthColor(currentHealth);
            }

            // Update health text
            if (showHealthText && healthText != null)
            {
                healthText.text = $"{Mathf.RoundToInt(currentHealth * maxHealth)}/{Mathf.RoundToInt(maxHealth)}";
            }
        }

        private void Flash(Color color, float duration)
        {
            if (fillImage == null) return;

            isFlashing = true;
            flashTimer = duration;
            fillImage.color = color;
        }

        private void ShowDamageNumber(float damage)
        {
            if (damageNumberPrefab == null) return;

            var damageObj = Instantiate(damageNumberPrefab, transform.position + Vector3.up * 50f, Quaternion.identity, transform.parent);
            var damageText = damageObj.GetComponent<TextMeshProUGUI>();
            if (damageText != null)
            {
                damageText.text = $"-{Mathf.RoundToInt(damage)}";
            }

            // Auto-destroy after animation (optional, depends on prefab setup)
            Destroy(damageObj, 1.5f);
        }

        /// <summary>
        /// Immediate set without animation
        /// </summary>
        public void SetHealthImmediate(float healthPercent)
        {
            currentHealth = Mathf.Clamp01(healthPercent);
            targetHealth = currentHealth;
            UpdateVisuals();
        }
    }
}
