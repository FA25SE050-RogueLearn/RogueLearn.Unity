using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BossFight2D.Systems;

namespace BossFight2D.UI
{
    /// <summary>
    /// Displays boss information in the top-center HUD:
    /// - Boss name/title
    /// - Health bar with smooth animations and color coding
    /// - Optional phase indicator (Phase 1, Phase 2, etc.)
    /// Automatically updates from boss state.
    /// </summary>
    public class BossHUDDisplay : MonoBehaviour
    {
        [Header("Boss Info")]
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private bool showPhaseIndicator = true;

        [Header("Health Bar")]
        [SerializeField] private EnhancedHealthBar healthBar;
        [SerializeField] private GameObject healthBarContainer;

        [Header("Visual Effects")]
        [SerializeField] private Image bossIcon;
        [SerializeField] private Sprite defaultBossIcon;
        [SerializeField] private bool useGlowEffect = true;
        [SerializeField] private Image glowImage;

        [Header("Auto-Configuration")]
        [SerializeField] private bool autoConfigureOnAwake = true;
        [SerializeField] private bool useThemeColors = true;

        private string currentBossName = "Boss";
        private int currentPhase = 1;
        private int maxPhase = 1;

        void Awake()
        {
            if (autoConfigureOnAwake)
            {
                ConfigureLayout();
            }

            // Subscribe to boss events
            EventBus.BossHealthChanged += OnBossHealthChanged;
            EventBus.BossPhaseChanged += OnBossPhaseChanged;
            EventBus.BossSpawned += OnBossSpawned;
        }

        void OnDestroy()
        {
            EventBus.BossHealthChanged -= OnBossHealthChanged;
            EventBus.BossPhaseChanged -= OnBossPhaseChanged;
            EventBus.BossSpawned -= OnBossSpawned;
        }

        [ContextMenu("Configure Boss HUD Layout")]
        public void ConfigureLayout()
        {
            Debug.Log("[BossHUDDisplay] === Configuring Boss HUD Layout ===");

            // Create vertical layout if not present
            if (GetComponent<VerticalLayoutGroup>() == null)
            {
                var layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(10, 10, 10, 10);
            }

            ConfigureBossNameSection();
            ConfigureHealthBarSection();
            ConfigurePhaseSection();
            ApplyThemeColors();

            Debug.Log("[BossHUDDisplay] === Configuration Complete ===");
        }

        private void ConfigureBossNameSection()
        {
            // Create boss name container if needed
            if (bossNameText == null)
            {
                var nameObj = new GameObject("BossName");
                nameObj.transform.SetParent(transform, false);
                var nameRect = nameObj.AddComponent<RectTransform>();
                nameRect.sizeDelta = new Vector2(400f, 30f);

                bossNameText = nameObj.AddComponent<TextMeshProUGUI>();
                bossNameText.text = "BOSS NAME";
                bossNameText.fontSize = UIThemeManager.Instance.fontSizeRegular;
                bossNameText.color = UIThemeManager.Instance.textPrimary;
                bossNameText.alignment = TextAlignmentOptions.Center;
                bossNameText.fontStyle = FontStyles.Bold;

                Debug.Log("  ✓ Created Boss Name section (400x30px)");
            }
        }

        private void ConfigureHealthBarSection()
        {
            // Create health bar container if needed
            if (healthBarContainer == null)
            {
                healthBarContainer = new GameObject("HealthBarContainer");
                healthBarContainer.transform.SetParent(transform, false);
                var containerRect = healthBarContainer.AddComponent<RectTransform>();
                containerRect.sizeDelta = new Vector2(400f, 30f);

                // Create health bar slider
                var healthBarObj = new GameObject("BossHealthBar");
                healthBarObj.transform.SetParent(healthBarContainer.transform, false);
                var slider = healthBarObj.AddComponent<Slider>();
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 1f;

                var sliderRect = healthBarObj.GetComponent<RectTransform>();
                sliderRect.anchorMin = Vector2.zero;
                sliderRect.anchorMax = Vector2.one;
                sliderRect.sizeDelta = Vector2.zero;
                sliderRect.anchoredPosition = Vector2.zero;

                // Create background
                var bgObj = new GameObject("Background");
                bgObj.transform.SetParent(healthBarObj.transform, false);
                var bgRect = bgObj.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                var bgImage = bgObj.AddComponent<Image>();
                bgImage.color = UIThemeManager.Instance.healthBackground;

                // Create fill area
                var fillAreaObj = new GameObject("Fill Area");
                fillAreaObj.transform.SetParent(healthBarObj.transform, false);
                var fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
                fillAreaRect.anchorMin = Vector2.zero;
                fillAreaRect.anchorMax = Vector2.one;
                fillAreaRect.sizeDelta = new Vector2(-10f, -10f);
                fillAreaRect.anchoredPosition = Vector2.zero;

                // Create fill
                var fillObj = new GameObject("Fill");
                fillObj.transform.SetParent(fillAreaObj.transform, false);
                var fillRect = fillObj.AddComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.sizeDelta = Vector2.zero;
                var fillImage = fillObj.AddComponent<Image>();
                fillImage.color = UIThemeManager.Instance.healthHigh;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;

                // Assign fill to slider
                slider.fillRect = fillRect;
                slider.targetGraphic = fillImage;

                // Add EnhancedHealthBar component
                healthBar = healthBarObj.AddComponent<EnhancedHealthBar>();

                Debug.Log("  ✓ Created Boss Health Bar (400x30px)");
            }
        }

        private void ConfigurePhaseSection()
        {
            if (!showPhaseIndicator) return;

            // Create phase indicator if needed
            if (phaseText == null)
            {
                var phaseObj = new GameObject("PhaseIndicator");
                phaseObj.transform.SetParent(transform, false);
                var phaseRect = phaseObj.AddComponent<RectTransform>();
                phaseRect.sizeDelta = new Vector2(150f, 20f);

                phaseText = phaseObj.AddComponent<TextMeshProUGUI>();
                phaseText.text = "PHASE 1";
                phaseText.fontSize = UIThemeManager.Instance.fontSizeSmall;
                phaseText.color = UIThemeManager.Instance.primaryPurple;
                phaseText.alignment = TextAlignmentOptions.Center;
                phaseText.fontStyle = FontStyles.Bold;

                Debug.Log("  ✓ Created Phase Indicator (150x20px)");
            }
        }

        private void ApplyThemeColors()
        {
            if (!useThemeColors) return;

            var theme = UIThemeManager.Instance;

            if (bossNameText != null)
                bossNameText.color = theme.textPrimary;

            if (phaseText != null)
                phaseText.color = theme.primaryPurple;

            Debug.Log("  ✓ Theme colors applied");
        }

        #region Event Handlers

        private void OnBossSpawned(string bossName, Sprite icon = null)
        {
            currentBossName = bossName;
            if (bossNameText != null)
            {
                bossNameText.text = bossName.ToUpper();
            }

            if (bossIcon != null && icon != null)
            {
                bossIcon.sprite = icon;
            }
            else if (bossIcon != null && defaultBossIcon != null)
            {
                bossIcon.sprite = defaultBossIcon;
            }

            // Reset health bar to full
            if (healthBar != null)
            {
                healthBar.SetHealthImmediate(1f);
            }
        }

        private void OnBossHealthChanged(float currentHealth, float maxHealth)
        {
            if (healthBar != null)
            {
                healthBar.SetHealthValues(currentHealth, maxHealth);
            }

            // Pulse glow effect on damage
            if (useGlowEffect && glowImage != null)
            {
                StartCoroutine(PulseGlow());
            }
        }

        private void OnBossPhaseChanged(int phase, int totalPhases)
        {
            currentPhase = phase;
            maxPhase = totalPhases;
            UpdatePhaseDisplay();
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Manually set boss name
        /// </summary>
        public void SetBossName(string bossName)
        {
            currentBossName = bossName;
            if (bossNameText != null)
            {
                bossNameText.text = bossName.ToUpper();
            }
        }

        /// <summary>
        /// Manually set boss health
        /// </summary>
        public void SetBossHealth(float current, float max)
        {
            if (healthBar != null)
            {
                healthBar.SetHealthValues(current, max);
            }
        }

        /// <summary>
        /// Manually set boss phase
        /// </summary>
        public void SetPhase(int phase, int totalPhases = 1)
        {
            currentPhase = phase;
            maxPhase = totalPhases;
            UpdatePhaseDisplay();
        }

        private void UpdatePhaseDisplay()
        {
            if (!showPhaseIndicator || phaseText == null) return;

            if (maxPhase > 1)
            {
                phaseText.text = $"PHASE {currentPhase}/{maxPhase}";
                phaseText.gameObject.SetActive(true);
            }
            else
            {
                phaseText.gameObject.SetActive(false);
            }

            // Change color based on phase
            if (currentPhase == maxPhase && maxPhase > 1)
            {
                // Final phase - use warning color
                phaseText.color = UIThemeManager.Instance.errorRed;
            }
            else
            {
                phaseText.color = UIThemeManager.Instance.primaryPurple;
            }
        }

        #endregion

        #region Visual Effects

        private System.Collections.IEnumerator PulseGlow()
        {
            if (glowImage == null) yield break;

            float duration = 0.3f;
            float elapsed = 0f;
            Color startColor = glowImage.color;
            Color targetColor = new Color(1f, 0.3f, 0.3f, 0.5f); // Red glow

            // Fade in
            while (elapsed < duration / 2)
            {
                elapsed += Time.unscaledDeltaTime;
                glowImage.color = Color.Lerp(startColor, targetColor, elapsed / (duration / 2));
                yield return null;
            }

            // Fade out
            elapsed = 0f;
            while (elapsed < duration / 2)
            {
                elapsed += Time.unscaledDeltaTime;
                glowImage.color = Color.Lerp(targetColor, startColor, elapsed / (duration / 2));
                yield return null;
            }

            glowImage.color = startColor;
        }

        /// <summary>
        /// Show entrance animation
        /// </summary>
        public void PlayEntranceAnimation()
        {
            StartCoroutine(EntranceAnimationCoroutine());
        }

        private System.Collections.IEnumerator EntranceAnimationCoroutine()
        {
            // Scale up from 0
            transform.localScale = Vector3.zero;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // Ease out back
                float scale = 1f + Mathf.Sin(t * Mathf.PI * 0.5f) * 0.2f;
                transform.localScale = Vector3.one * Mathf.Lerp(0f, scale, t);
                yield return null;
            }

            transform.localScale = Vector3.one;
        }

        #endregion

        /// <summary>
        /// Show or hide the entire boss HUD
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
