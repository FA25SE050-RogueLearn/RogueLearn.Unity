using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BossFight2D.Systems;

namespace BossFight2D.UI
{
    /// <summary>
    /// Manages the entire gameplay HUD layout including:
    /// - Boss health bar (top center)
    /// - Player stats (bottom left): Health, Charges, Score
    /// - Support items (right side): Lifeline, Power-ups
    /// - Match info (top right): Timer, Question counter
    /// </summary>
    public class GameHUDManager : MonoBehaviour
    {
        [Header("HUD Sections")]
        [SerializeField] private RectTransform topBarContainer;
        [SerializeField] private RectTransform bottomBarContainer;
        [SerializeField] private RectTransform supportItemsContainer;

        [Header("Top Bar Elements")]
        [SerializeField] private GameObject bossHealthSection;
        [SerializeField] private GameObject matchInfoSection;

        [Header("Bottom Bar Elements")]
        [SerializeField] private GameObject playerStatsSection;
        [SerializeField] private GameObject readyStatusSection;

        [Header("Support Items")]
        [SerializeField] private GameObject lifelineButton;
        [SerializeField] private GameObject powerUpIndicators;

        [Header("Tutorial")]
        [SerializeField] private bool showTutorialOnStart = true;

        [Header("Auto-Configuration")]
        [SerializeField] private bool autoConfigureOnAwake = true;
        [SerializeField] private bool hideHUDDuringQuestions = true;

        private CanvasGroup hudCanvasGroup;

        void Awake()
        {
            hudCanvasGroup = GetComponent<CanvasGroup>();
            if (hudCanvasGroup == null)
            {
                hudCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (autoConfigureOnAwake)
            {
                ConfigureHUDLayout();
            }

            // Subscribe to question events to hide/show HUD
            if (hideHUDDuringQuestions)
            {
                EventBus.QuestionStarted += OnQuestionStarted;
                EventBus.AnswerModeExited += OnQuestionEnded;
            }
        }

        void OnDestroy()
        {
            EventBus.QuestionStarted -= OnQuestionStarted;
            EventBus.AnswerModeExited -= OnQuestionEnded;
        }

        [ContextMenu("Configure HUD Layout")]
        public void ConfigureHUDLayout()
        {
            Debug.Log("[GameHUDManager] === Configuring HUD Layout ===");

            ConfigureTopBar();
            ConfigureBottomBar();
            ConfigureSupportItems();
            ApplyThemeColors();

            Debug.Log("[GameHUDManager] === HUD Configuration Complete ===");
        }

        private void ConfigureTopBar()
        {
            Debug.Log("[GameHUDManager] Configuring Top Bar...");

            // Create or configure top bar container
            if (topBarContainer == null)
            {
                var topBarObj = new GameObject("TopBar");
                topBarObj.transform.SetParent(transform, false);
                topBarContainer = topBarObj.AddComponent<RectTransform>();

                // Anchor to top, stretch horizontally
                topBarContainer.anchorMin = new Vector2(0, 1);
                topBarContainer.anchorMax = new Vector2(1, 1);
                topBarContainer.pivot = new Vector2(0.5f, 1f);
                topBarContainer.sizeDelta = new Vector2(0, 60f); // 60px height
                topBarContainer.anchoredPosition = new Vector2(0, 0);

                var bgImage = topBarObj.AddComponent<Image>();
                bgImage.color = new Color(0, 0, 0, 0.3f); // Semi-transparent background

                Debug.Log("  ✓ Created TopBar container (60px height)");
            }

            // Configure boss health section (center)
            if (bossHealthSection != null)
            {
                var bossRect = bossHealthSection.GetComponent<RectTransform>();
                if (bossRect != null)
                {
                    bossRect.anchorMin = new Vector2(0.5f, 0.5f);
                    bossRect.anchorMax = new Vector2(0.5f, 0.5f);
                    bossRect.pivot = new Vector2(0.5f, 0.5f);
                    bossRect.sizeDelta = new Vector2(400f, 40f); // Boss health bar size
                    bossRect.anchoredPosition = Vector2.zero;

                    Debug.Log("  ✓ Configured Boss Health section (center, 400x40px)");
                }
            }

            // Configure match info section (top right)
            if (matchInfoSection != null)
            {
                var infoRect = matchInfoSection.GetComponent<RectTransform>();
                if (infoRect != null)
                {
                    infoRect.anchorMin = new Vector2(1, 0.5f);
                    infoRect.anchorMax = new Vector2(1, 0.5f);
                    infoRect.pivot = new Vector2(1f, 0.5f);
                    infoRect.sizeDelta = new Vector2(200f, 50f);
                    infoRect.anchoredPosition = new Vector2(-20f, 0); // 20px padding from right

                    Debug.Log("  ✓ Configured Match Info section (top right, 200x50px)");
                }
            }
        }

        private void ConfigureBottomBar()
        {
            Debug.Log("[GameHUDManager] Configuring Bottom Bar...");

            // Create or configure bottom bar container
            if (bottomBarContainer == null)
            {
                var bottomBarObj = new GameObject("BottomBar");
                bottomBarObj.transform.SetParent(transform, false);
                bottomBarContainer = bottomBarObj.AddComponent<RectTransform>();

                // Anchor to bottom, stretch horizontally
                bottomBarContainer.anchorMin = new Vector2(0, 0);
                bottomBarContainer.anchorMax = new Vector2(1, 0);
                bottomBarContainer.pivot = new Vector2(0.5f, 0f);
                bottomBarContainer.sizeDelta = new Vector2(0, 80f); // 80px height
                bottomBarContainer.anchoredPosition = new Vector2(0, 0);

                var bgImage = bottomBarObj.AddComponent<Image>();
                bgImage.color = new Color(0, 0, 0, 0.3f); // Semi-transparent background

                Debug.Log("  ✓ Created BottomBar container (80px height)");
            }

            // Configure player stats section (bottom left)
            if (playerStatsSection != null)
            {
                var statsRect = playerStatsSection.GetComponent<RectTransform>();
                if (statsRect != null)
                {
                    statsRect.anchorMin = new Vector2(0, 0.5f);
                    statsRect.anchorMax = new Vector2(0, 0.5f);
                    statsRect.pivot = new Vector2(0f, 0.5f);
                    statsRect.sizeDelta = new Vector2(350f, 70f);
                    statsRect.anchoredPosition = new Vector2(20f, 0); // 20px padding from left

                    Debug.Log("  ✓ Configured Player Stats section (bottom left, 350x70px)");
                }
            }

            // Configure ready status section (bottom center)
            if (readyStatusSection != null)
            {
                var readyRect = readyStatusSection.GetComponent<RectTransform>();
                if (readyRect != null)
                {
                    readyRect.anchorMin = new Vector2(0.5f, 0.5f);
                    readyRect.anchorMax = new Vector2(0.5f, 0.5f);
                    readyRect.pivot = new Vector2(0.5f, 0.5f);
                    readyRect.sizeDelta = new Vector2(200f, 60f);
                    readyRect.anchoredPosition = Vector2.zero;

                    Debug.Log("  ✓ Configured Ready Status section (bottom center, 200x60px)");
                }
            }
        }

        private void ConfigureSupportItems()
        {
            Debug.Log("[GameHUDManager] Configuring Support Items...");

            // Create or configure support items container (right side)
            if (supportItemsContainer == null)
            {
                var supportObj = new GameObject("SupportItems");
                supportObj.transform.SetParent(transform, false);
                supportItemsContainer = supportObj.AddComponent<RectTransform>();

                // Anchor to right, centered vertically
                supportItemsContainer.anchorMin = new Vector2(1, 0.5f);
                supportItemsContainer.anchorMax = new Vector2(1, 0.5f);
                supportItemsContainer.pivot = new Vector2(1f, 0.5f);
                supportItemsContainer.sizeDelta = new Vector2(100f, 300f);
                supportItemsContainer.anchoredPosition = new Vector2(-20f, 0); // 20px from right edge

                // Add vertical layout group
                var verticalLayout = supportObj.AddComponent<VerticalLayoutGroup>();
                verticalLayout.spacing = 15f;
                verticalLayout.childAlignment = TextAnchor.UpperCenter;
                verticalLayout.childControlWidth = true;
                verticalLayout.childControlHeight = false;
                verticalLayout.childForceExpandWidth = true;
                verticalLayout.childForceExpandHeight = false;

                Debug.Log("  ✓ Created SupportItems container (right side, 100x300px)");
            }

            // Configure lifeline button
            if (lifelineButton != null)
            {
                var lifelineRect = lifelineButton.GetComponent<RectTransform>();
                if (lifelineRect != null)
                {
                    var layoutElement = lifelineButton.GetComponent<LayoutElement>();
                    if (layoutElement == null)
                    {
                        layoutElement = lifelineButton.AddComponent<LayoutElement>();
                    }
                    layoutElement.preferredHeight = 80f;

                    Debug.Log("  ✓ Configured Lifeline button (80px height)");
                }

                // Add button enhancer if not present
                if (lifelineButton.GetComponent<UIButtonEnhancer>() == null)
                {
                    lifelineButton.AddComponent<UIButtonEnhancer>();
                    Debug.Log("  ✓ Added UIButtonEnhancer to Lifeline button");
                }
            }
        }

        private void ApplyThemeColors()
        {
            Debug.Log("[GameHUDManager] Applying theme colors...");

            var theme = UIThemeManager.Instance;

            // Apply to top bar background
            if (topBarContainer != null)
            {
                var bgImage = topBarContainer.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.color = new Color(0, 0, 0, 0.3f);
                }
            }

            // Apply to bottom bar background
            if (bottomBarContainer != null)
            {
                var bgImage = bottomBarContainer.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.color = new Color(0, 0, 0, 0.3f);
                }
            }

            Debug.Log("  ✓ Theme colors applied");
        }

        private void OnQuestionStarted(Systems.QuestionData question)
        {
            // Dim or hide HUD during questions
            if (hudCanvasGroup != null)
            {
                StartCoroutine(FadeHUD(0.3f, 0.3f)); // Fade to 30% opacity
            }
        }

        private void OnQuestionEnded()
        {
            // Restore HUD visibility
            if (hudCanvasGroup != null)
            {
                StartCoroutine(FadeHUD(1f, 0.2f)); // Fade back to full opacity
            }
        }

        private System.Collections.IEnumerator FadeHUD(float targetAlpha, float duration)
        {
            float startAlpha = hudCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                hudCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            hudCanvasGroup.alpha = targetAlpha;
        }

        /// <summary>
        /// Show HUD immediately
        /// </summary>
        public void ShowHUD()
        {
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// Hide HUD immediately
        /// </summary>
        public void HideHUD()
        {
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = 0f;
            }
        }
    }
}
