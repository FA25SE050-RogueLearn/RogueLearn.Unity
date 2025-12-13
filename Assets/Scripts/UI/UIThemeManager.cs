using UnityEngine;

namespace BossFight2D.UI
{
    /// <summary>
    /// Centralized theme manager for consistent UI styling across the game.
    /// Provides color palette, font sizes, and design tokens based on the UI/UX plan.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "RogueLearn/UI Theme", order = 1)]
    public class UIThemeManager : ScriptableObject
    {
        private static UIThemeManager _instance;
        public static UIThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<UIThemeManager>("UITheme");
                    if (_instance == null)
                    {
                        Debug.LogWarning("[UITheme] No UITheme asset found in Resources folder. Using default values.");
                        _instance = CreateInstance<UIThemeManager>();
                        _instance.ResetToDefaults();
                    }
                }
                return _instance;
            }
        }

        [Header("Color Palette - Modern Dark Theme")]
        [Space(5)]

        [Header("Background Colors")]
        public Color backgroundDark = new Color(0.12f, 0.16f, 0.22f, 1f); // #1E2937
        public Color backgroundMedium = new Color(0.16f, 0.2f, 0.27f, 1f);
        public Color backgroundLight = new Color(0.2f, 0.24f, 0.32f, 1f);

        [Header("Primary Colors")]
        public Color primaryBlue = new Color(0.23f, 0.51f, 0.96f, 1f); // #3B82F6
        public Color primaryPurple = new Color(0.55f, 0.36f, 0.95f, 1f); // #8B5CF6

        [Header("Status Colors")]
        public Color successGreen = new Color(0.06f, 0.72f, 0.51f, 1f); // #10B981
        public Color warningOrange = new Color(0.96f, 0.62f, 0.04f, 1f); // #F59E0B
        public Color errorRed = new Color(0.94f, 0.27f, 0.27f, 1f); // #EF4444

        [Header("Text Colors")]
        public Color textPrimary = Color.white; // #FFFFFF
        public Color textSecondary = new Color(0.9f, 0.91f, 0.92f, 1f); // #E5E7EB
        public Color textDisabled = new Color(0.61f, 0.64f, 0.69f, 1f); // #9CA3AF

        [Header("UI Element Colors")]
        public Color buttonNormal = new Color(0.2f, 0.23f, 0.3f, 1f);
        public Color buttonHover = new Color(0.23f, 0.51f, 0.96f, 0.8f);
        public Color buttonPressed = new Color(0.23f, 0.51f, 0.96f, 1f);
        public Color buttonDisabled = new Color(0.2f, 0.23f, 0.3f, 0.5f);

        [Header("Overlay Colors")]
        public Color overlayDark = new Color(0f, 0f, 0f, 0.75f); // 75% black
        public Color overlayMedium = new Color(0f, 0f, 0f, 0.5f); // 50% black
        public Color overlayLight = new Color(0f, 0f, 0f, 0.25f); // 25% black

        [Header("Typography - Font Sizes")]
        [Space(5)]
        public float fontSizeHuge = 48f; // Boss name, Victory/Defeat
        public float fontSizeLarge = 36f; // Question text
        public float fontSizeMedium = 28f; // Answer text, important labels
        public float fontSizeRegular = 24f; // UI labels, buttons
        public float fontSizeSmall = 18f; // Timer, counters, hints

        [Header("Spacing & Layout")]
        [Space(5)]
        public float spacingTiny = 5f;
        public float spacingSmall = 10f;
        public float spacingMedium = 20f;
        public float spacingLarge = 40f;
        public float spacingHuge = 60f;

        [Header("Border & Radius")]
        public float borderRadiusSmall = 4f;
        public float borderRadiusMedium = 8f;
        public float borderRadiusLarge = 12f;

        [Header("Animation Timings")]
        public float transitionFast = 0.1f;
        public float transitionNormal = 0.2f;
        public float transitionSlow = 0.3f;

        [Header("Health Bar Colors")]
        public Color healthHigh = new Color(0.2f, 0.9f, 0.2f, 1f); // Green
        public Color healthMedium = new Color(0.96f, 0.62f, 0.04f, 1f); // Orange
        public Color healthLow = new Color(0.94f, 0.27f, 0.27f, 1f); // Red
        public Color healthBackground = new Color(0.2f, 0.23f, 0.3f, 0.5f);

        [Header("Timer Colors")]
        public Color timerNormal = new Color(0.23f, 0.51f, 0.96f, 1f); // Blue
        public Color timerWarning = new Color(0.96f, 0.62f, 0.04f, 1f); // Orange
        public Color timerCritical = new Color(0.94f, 0.27f, 0.27f, 1f); // Red

        /// <summary>
        /// Reset all values to default theme (Modern Dark)
        /// </summary>
        public void ResetToDefaults()
        {
            // Background Colors
            backgroundDark = new Color(0.12f, 0.16f, 0.22f, 1f);
            backgroundMedium = new Color(0.16f, 0.2f, 0.27f, 1f);
            backgroundLight = new Color(0.2f, 0.24f, 0.32f, 1f);

            // Primary Colors
            primaryBlue = new Color(0.23f, 0.51f, 0.96f, 1f);
            primaryPurple = new Color(0.55f, 0.36f, 0.95f, 1f);

            // Status Colors
            successGreen = new Color(0.06f, 0.72f, 0.51f, 1f);
            warningOrange = new Color(0.96f, 0.62f, 0.04f, 1f);
            errorRed = new Color(0.94f, 0.27f, 0.27f, 1f);

            // Text Colors
            textPrimary = Color.white;
            textSecondary = new Color(0.9f, 0.91f, 0.92f, 1f);
            textDisabled = new Color(0.61f, 0.64f, 0.69f, 1f);

            // UI Element Colors
            buttonNormal = new Color(0.2f, 0.23f, 0.3f, 1f);
            buttonHover = new Color(0.23f, 0.51f, 0.96f, 0.8f);
            buttonPressed = new Color(0.23f, 0.51f, 0.96f, 1f);
            buttonDisabled = new Color(0.2f, 0.23f, 0.3f, 0.5f);

            // Overlay Colors
            overlayDark = new Color(0f, 0f, 0f, 0.75f);
            overlayMedium = new Color(0f, 0f, 0f, 0.5f);
            overlayLight = new Color(0f, 0f, 0f, 0.25f);

            // Font Sizes
            fontSizeHuge = 48f;
            fontSizeLarge = 36f;
            fontSizeMedium = 28f;
            fontSizeRegular = 24f;
            fontSizeSmall = 18f;

            // Spacing
            spacingTiny = 5f;
            spacingSmall = 10f;
            spacingMedium = 20f;
            spacingLarge = 40f;
            spacingHuge = 60f;

            // Border Radius
            borderRadiusSmall = 4f;
            borderRadiusMedium = 8f;
            borderRadiusLarge = 12f;

            // Animation Timings
            transitionFast = 0.1f;
            transitionNormal = 0.2f;
            transitionSlow = 0.3f;

            // Health Bar Colors
            healthHigh = new Color(0.2f, 0.9f, 0.2f, 1f);
            healthMedium = new Color(0.96f, 0.62f, 0.04f, 1f);
            healthLow = new Color(0.94f, 0.27f, 0.27f, 1f);
            healthBackground = new Color(0.2f, 0.23f, 0.3f, 0.5f);

            // Timer Colors
            timerNormal = new Color(0.23f, 0.51f, 0.96f, 1f);
            timerWarning = new Color(0.96f, 0.62f, 0.04f, 1f);
            timerCritical = new Color(0.94f, 0.27f, 0.27f, 1f);
        }

        /// <summary>
        /// Get health bar color based on health percentage (0-1)
        /// </summary>
        public Color GetHealthColor(float healthPercent)
        {
            if (healthPercent > 0.6f)
                return healthHigh;
            else if (healthPercent > 0.3f)
                return Color.Lerp(healthMedium, healthHigh, (healthPercent - 0.3f) / 0.3f);
            else
                return Color.Lerp(healthLow, healthMedium, healthPercent / 0.3f);
        }

        /// <summary>
        /// Get timer color based on time remaining percentage (0-1)
        /// </summary>
        public Color GetTimerColor(float timePercent)
        {
            if (timePercent > 0.5f)
                return timerNormal;
            else if (timePercent > 0.25f)
                return timerWarning;
            else
                return timerCritical;
        }
    }
}
