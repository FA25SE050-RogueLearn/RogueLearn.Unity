using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BossFight2D.Systems;

namespace BossFight2D.UI
{
    /// <summary>
    /// Displays player stats in the bottom-left HUD:
    /// - Health bar with smooth animations
    /// - Attack charges (0/3, 1/3, 2/3, 3/3) with icons
    /// - Current score/questions answered
    /// Automatically updates from game state.
    /// </summary>
    public class PlayerStatsDisplay : MonoBehaviour
    {
        [Header("Health Display")]
        [SerializeField] private EnhancedHealthBar healthBar;
        [SerializeField] private TextMeshProUGUI healthLabel;

        [Header("Charge Display")]
        [SerializeField] private GameObject chargesContainer;
        [SerializeField] private Image[] chargeIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI chargesLabel;
        [SerializeField] private Sprite chargeEmptySprite;
        [SerializeField] private Sprite chargeFullSprite;

        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private bool showAsQuestionCount = true; // "4/5" vs "Score: 80%"

        [Header("Auto-Configuration")]
        [SerializeField] private bool autoConfigureOnAwake = true;
        [SerializeField] private bool useThemeColors = true;

        private int currentCharges = 0;
        private int maxCharges = 3;
        private int questionsCorrect = 0;
        private int questionsTotal = 0;

        void Awake()
        {
            if (autoConfigureOnAwake)
            {
                ConfigureLayout();
            }

            // Subscribe to game events
            EventBus.PlayerHealthChanged += OnPlayerHealthChanged;
            EventBus.ChargesChanged += OnChargesChanged;
            EventBus.QuestionAnswered += OnQuestionAnswered;
        }

        void OnDestroy()
        {
            EventBus.PlayerHealthChanged -= OnPlayerHealthChanged;
            EventBus.ChargesChanged -= OnChargesChanged;
            EventBus.QuestionAnswered -= OnQuestionAnswered;
        }

        [ContextMenu("Configure Player Stats Layout")]
        public void ConfigureLayout()
        {
            Debug.Log("[PlayerStatsDisplay] === Configuring Player Stats Layout ===");

            // Create horizontal layout if not present
            if (GetComponent<HorizontalLayoutGroup>() == null)
            {
                var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 20f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(10, 10, 10, 10);
            }

            ConfigureHealthSection();
            ConfigureChargesSection();
            ConfigureScoreSection();
            ApplyThemeColors();

            Debug.Log("[PlayerStatsDisplay] === Configuration Complete ===");
        }

        private void ConfigureHealthSection()
        {
            // Create health section container if needed
            if (healthBar == null)
            {
                var healthSection = new GameObject("HealthSection");
                healthSection.transform.SetParent(transform, false);
                var healthRect = healthSection.AddComponent<RectTransform>();
                healthRect.sizeDelta = new Vector2(200f, 60f);

                // Add vertical layout
                var vertLayout = healthSection.AddComponent<VerticalLayoutGroup>();
                vertLayout.spacing = 5f;
                vertLayout.childAlignment = TextAnchor.MiddleLeft;

                // Create health label
                var labelObj = new GameObject("HealthLabel");
                labelObj.transform.SetParent(healthSection.transform, false);
                healthLabel = labelObj.AddComponent<TextMeshProUGUI>();
                healthLabel.text = "HEALTH";
                healthLabel.fontSize = UIThemeManager.Instance.fontSizeSmall;
                healthLabel.color = UIThemeManager.Instance.textSecondary;
                healthLabel.alignment = TextAlignmentOptions.Left;

                // Create health bar
                var healthBarObj = new GameObject("HealthBar");
                healthBarObj.transform.SetParent(healthSection.transform, false);
                var slider = healthBarObj.AddComponent<Slider>();
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 1f;

                var sliderRect = healthBarObj.GetComponent<RectTransform>();
                sliderRect.sizeDelta = new Vector2(200f, 20f);

                // Add EnhancedHealthBar component
                healthBar = healthBarObj.AddComponent<EnhancedHealthBar>();

                Debug.Log("  ✓ Created Health section (200x60px)");
            }
        }

        private void ConfigureChargesSection()
        {
            // Create charges section container if needed
            if (chargesContainer == null)
            {
                chargesContainer = new GameObject("ChargesSection");
                chargesContainer.transform.SetParent(transform, false);
                var chargesRect = chargesContainer.AddComponent<RectTransform>();
                chargesRect.sizeDelta = new Vector2(120f, 60f);

                // Add vertical layout
                var vertLayout = chargesContainer.AddComponent<VerticalLayoutGroup>();
                vertLayout.spacing = 5f;
                vertLayout.childAlignment = TextAnchor.MiddleCenter;

                // Create charges label
                var labelObj = new GameObject("ChargesLabel");
                labelObj.transform.SetParent(chargesContainer.transform, false);
                chargesLabel = labelObj.AddComponent<TextMeshProUGUI>();
                chargesLabel.text = "CHARGES";
                chargesLabel.fontSize = UIThemeManager.Instance.fontSizeSmall;
                chargesLabel.color = UIThemeManager.Instance.textSecondary;
                chargesLabel.alignment = TextAlignmentOptions.Center;

                // Create icons container
                var iconsObj = new GameObject("ChargeIcons");
                iconsObj.transform.SetParent(chargesContainer.transform, false);
                var iconsRect = iconsObj.AddComponent<RectTransform>();
                iconsRect.sizeDelta = new Vector2(100f, 30f);

                // Add horizontal layout for icons
                var horizLayout = iconsObj.AddComponent<HorizontalLayoutGroup>();
                horizLayout.spacing = 10f;
                horizLayout.childAlignment = TextAnchor.MiddleCenter;
                horizLayout.childControlWidth = false;
                horizLayout.childControlHeight = false;

                // Create 3 charge icons
                chargeIcons = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    var iconObj = new GameObject($"ChargeIcon{i + 1}");
                    iconObj.transform.SetParent(iconsObj.transform, false);
                    var iconRect = iconObj.AddComponent<RectTransform>();
                    iconRect.sizeDelta = new Vector2(24f, 24f);

                    chargeIcons[i] = iconObj.AddComponent<Image>();
                    chargeIcons[i].color = UIThemeManager.Instance.textDisabled; // Empty by default
                }

                Debug.Log("  ✓ Created Charges section with 3 icons (120x60px)");
            }
        }

        private void ConfigureScoreSection()
        {
            // Create score section if needed
            if (scoreText == null)
            {
                var scoreObj = new GameObject("ScoreSection");
                scoreObj.transform.SetParent(transform, false);
                var scoreRect = scoreObj.AddComponent<RectTransform>();
                scoreRect.sizeDelta = new Vector2(100f, 60f);

                scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
                scoreText.text = "0/0";
                scoreText.fontSize = UIThemeManager.Instance.fontSizeRegular;
                scoreText.color = UIThemeManager.Instance.textPrimary;
                scoreText.alignment = TextAlignmentOptions.Center;

                Debug.Log("  ✓ Created Score section (100x60px)");
            }
        }

        private void ApplyThemeColors()
        {
            if (!useThemeColors) return;

            var theme = UIThemeManager.Instance;

            if (healthLabel != null)
                healthLabel.color = theme.textSecondary;

            if (chargesLabel != null)
                chargesLabel.color = theme.textSecondary;

            if (scoreText != null)
                scoreText.color = theme.textPrimary;

            Debug.Log("  ✓ Theme colors applied");
        }

        #region Event Handlers

        private void OnPlayerHealthChanged(float currentHealth, float maxHealth)
        {
            if (healthBar != null)
            {
                healthBar.SetHealthValues(currentHealth, maxHealth);
            }
        }

        private void OnChargesChanged(int charges, int max)
        {
            currentCharges = charges;
            maxCharges = max;
            UpdateChargesDisplay();
        }

        private void OnQuestionAnswered(bool correct)
        {
            questionsTotal++;
            if (correct)
            {
                questionsCorrect++;
            }
            UpdateScoreDisplay();
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Manually set player health
        /// </summary>
        public void SetHealth(float current, float max)
        {
            if (healthBar != null)
            {
                healthBar.SetHealthValues(current, max);
            }
        }

        /// <summary>
        /// Manually set charge count
        /// </summary>
        public void SetCharges(int charges, int max = 3)
        {
            currentCharges = Mathf.Clamp(charges, 0, max);
            maxCharges = max;
            UpdateChargesDisplay();
        }

        /// <summary>
        /// Manually set score
        /// </summary>
        public void SetScore(int correct, int total)
        {
            questionsCorrect = correct;
            questionsTotal = total;
            UpdateScoreDisplay();
        }

        private void UpdateChargesDisplay()
        {
            // Update charge icons
            for (int i = 0; i < chargeIcons.Length && i < maxCharges; i++)
            {
                if (chargeIcons[i] != null)
                {
                    if (i < currentCharges)
                    {
                        // Charged - use primary color
                        chargeIcons[i].color = UIThemeManager.Instance.primaryBlue;
                        if (chargeFullSprite != null)
                            chargeIcons[i].sprite = chargeFullSprite;
                    }
                    else
                    {
                        // Empty - use disabled color
                        chargeIcons[i].color = UIThemeManager.Instance.textDisabled;
                        if (chargeEmptySprite != null)
                            chargeIcons[i].sprite = chargeEmptySprite;
                    }
                }
            }

            // Update charges label
            if (chargesLabel != null)
            {
                chargesLabel.text = $"CHARGES ({currentCharges}/{maxCharges})";
            }
        }

        private void UpdateScoreDisplay()
        {
            if (scoreText == null) return;

            if (showAsQuestionCount)
            {
                scoreText.text = $"{questionsCorrect}/{questionsTotal}";
            }
            else
            {
                float percentage = questionsTotal > 0 ? (float)questionsCorrect / questionsTotal * 100f : 0f;
                scoreText.text = $"{percentage:F0}%";
            }

            // Color code based on performance
            if (questionsTotal > 0)
            {
                float accuracy = (float)questionsCorrect / questionsTotal;
                if (accuracy >= 0.8f)
                    scoreText.color = UIThemeManager.Instance.successGreen;
                else if (accuracy >= 0.5f)
                    scoreText.color = UIThemeManager.Instance.warningOrange;
                else
                    scoreText.color = UIThemeManager.Instance.errorRed;
            }
        }

        #endregion

        /// <summary>
        /// Reset all stats to initial values
        /// </summary>
        public void ResetStats()
        {
            SetHealth(100f, 100f);
            SetCharges(0, 3);
            SetScore(0, 0);
        }
    }
}
