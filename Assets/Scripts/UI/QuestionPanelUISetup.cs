using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BossFight2D.UI
{
    /// <summary>
    /// Automatically configures the QuestionPanel UI to match the full-screen focus design.
    /// Attach this to the QuestionPanel prefab and it will set up the layout on Awake.
    /// </summary>
    [ExecuteInEditMode]
    public class QuestionPanelUISetup : MonoBehaviour
    {
        [Header("Auto-Configuration")]
        [SerializeField] private bool autoConfigureOnAwake = true;
        [SerializeField] private bool showDebugLogs = true;

        [Header("Design Specifications (Full-Screen Focus)")]
        [SerializeField] private Vector2 modalSize = new Vector2(1200f, 864f); // 80% of 1080p
        [SerializeField] private float answerButtonHeight = 180f;
        [SerializeField] private float answerButtonSpacing = 20f;
        [SerializeField] private float questionFontSize = 36f;
        [SerializeField] private float answerFontSize = 28f;
        [SerializeField] private float headerFontSize = 24f;
        [SerializeField] private float topicFontSize = 20f;

        [Header("Colors - Full-Screen Focus")]
        [SerializeField] private Color modalBackground = new Color(0.12f, 0.16f, 0.22f, 1f); // Dark blue-gray
        [SerializeField] private Color darkOverlay = new Color(0f, 0f, 0f, 0.75f); // 75% black
        [SerializeField] private Color headerTint = new Color(0.23f, 0.51f, 0.96f, 0.2f); // Blue
        [SerializeField] private Color topicTint = new Color(0.55f, 0.34f, 0.76f, 0.3f); // Purple
        [SerializeField] private Color answerButtonNormal = new Color(0.2f, 0.23f, 0.3f, 1f);
        [SerializeField] private Color answerButtonHover = new Color(0.23f, 0.51f, 0.96f, 0.8f);
        [SerializeField] private Color answerTextColor = Color.white;
        [SerializeField] private Color questionTextColor = Color.white;

        void Awake()
        {
            if (autoConfigureOnAwake)
            {
                ConfigureFullScreenFocusLayout();
            }
        }

        [ContextMenu("Configure Full-Screen Focus Layout")]
        public void ConfigureFullScreenFocusLayout()
        {
            Log("=== Starting QuestionPanel Full-Screen Focus Configuration ===");

            // 1. Configure the main panel (this GameObject)
            ConfigureMainPanel();

            // 2. Add or configure dark overlay
            ConfigureDarkOverlay();

            // 3. Configure question text
            ConfigureQuestionText();

            // 4. Configure answer buttons
            ConfigureAnswerButtons();

            // 5. Configure timer
            ConfigureTimer();

            // 6. Add topic bar (if not exists)
            ConfigureTopicBar();

            // 7. Configure layout groups
            ConfigureLayoutGroups();

            Log("=== QuestionPanel Configuration Complete ===");
        }

        private void ConfigureMainPanel()
        {
            Log("Configuring main panel...");

            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Center the modal
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.sizeDelta = modalSize;

                Log($"  ✓ Set modal size to {modalSize.x}x{modalSize.y}px");
                Log($"  ✓ Centered modal with anchors (0.5, 0.5)");
            }

            // Configure background image
            var bgImage = GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = modalBackground;
                Log($"  ✓ Set modal background color");
            }
            else
            {
                Log("  ⚠ No Image component on main panel - consider adding one for background");
            }

            // Ensure CanvasGroup exists for fade animations
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                Log("  ✓ Added CanvasGroup for fade animations");
            }
        }

        private void ConfigureDarkOverlay()
        {
            Log("Configuring dark overlay...");

            // Look for existing overlay or create one
            Transform overlayTransform = transform.parent?.Find("DarkOverlay");
            GameObject overlayObj;

            if (overlayTransform == null && transform.parent != null)
            {
                // Create new dark overlay as sibling (before the modal in hierarchy)
                overlayObj = new GameObject("DarkOverlay");
                overlayObj.transform.SetParent(transform.parent, false);
                overlayObj.transform.SetSiblingIndex(transform.GetSiblingIndex()); // Place before modal

                var overlayRect = overlayObj.AddComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.sizeDelta = Vector2.zero;
                overlayRect.anchoredPosition = Vector2.zero;

                var overlayImage = overlayObj.AddComponent<Image>();
                overlayImage.color = darkOverlay;
                overlayImage.raycastTarget = true; // Block clicks to gameplay

                Log("  ✓ Created new DarkOverlay (full screen, 75% black)");
            }
            else if (overlayTransform != null)
            {
                var overlayImage = overlayTransform.GetComponent<Image>();
                if (overlayImage != null)
                {
                    overlayImage.color = darkOverlay;
                    Log("  ✓ Updated existing DarkOverlay color");
                }
            }
            else
            {
                Log("  ⚠ Cannot create DarkOverlay - panel has no parent Canvas");
            }
        }

        private void ConfigureQuestionText()
        {
            Log("Configuring question text...");

            var questionTransform = transform.Find("Question");
            if (questionTransform != null)
            {
                var questionTMP = questionTransform.GetComponent<TextMeshProUGUI>();
                if (questionTMP != null)
                {
                    questionTMP.fontSize = questionFontSize;
                    questionTMP.color = questionTextColor;
                    questionTMP.alignment = TextAlignmentOptions.Center;
                    questionTMP.enableWordWrapping = true;
                    questionTMP.overflowMode = TextOverflowModes.Ellipsis;
                    questionTMP.lineSpacing = 5f; // 1.3x spacing approximation

                    Log($"  ✓ Set question font size to {questionFontSize}px");
                    Log($"  ✓ Enabled word wrapping and center alignment");
                }

                // Configure RectTransform for better layout
                var questionRect = questionTransform.GetComponent<RectTransform>();
                if (questionRect != null)
                {
                    questionRect.sizeDelta = new Vector2(questionRect.sizeDelta.x, 150f); // Reserve space
                    Log($"  ✓ Set question area height to 150px");
                }
            }
            else
            {
                Log("  ⚠ Question text not found (expected child named 'Question')");
            }
        }

        private void ConfigureAnswerButtons()
        {
            Log("Configuring answer buttons...");

            string[] buttonNames = { "Answer_A", "Answer_B", "Answer_C", "Answer_D" };
            int configured = 0;

            foreach (var buttonName in buttonNames)
            {
                var buttonTransform = transform.Find(buttonName);
                if (buttonTransform != null)
                {
                    // Configure button layout
                    var buttonRect = buttonTransform.GetComponent<RectTransform>();
                    if (buttonRect != null)
                    {
                        var layoutElement = buttonRect.GetComponent<LayoutElement>();
                        if (layoutElement == null)
                        {
                            layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
                        }
                        layoutElement.preferredHeight = answerButtonHeight;
                        layoutElement.flexibleWidth = 1f;
                    }

                    // Configure button colors
                    var button = buttonTransform.GetComponent<Button>();
                    if (button != null)
                    {
                        var colors = button.colors;
                        colors.normalColor = answerButtonNormal;
                        colors.highlightedColor = answerButtonHover;
                        colors.pressedColor = answerButtonHover;
                        colors.fadeDuration = 0.1f;
                        button.colors = colors;
                    }

                    // Configure button text
                    var buttonText = buttonTransform.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.fontSize = answerFontSize;
                        buttonText.color = answerTextColor;
                        buttonText.alignment = TextAlignmentOptions.Left;
                        buttonText.enableWordWrapping = true;
                        buttonText.overflowMode = TextOverflowModes.Ellipsis;
                        buttonText.lineSpacing = 2f; // 1.2x spacing
                        buttonText.margin = new Vector4(20, 20, 20, 20); // Padding
                    }

                    configured++;
                }
            }

            Log($"  ✓ Configured {configured}/4 answer buttons");
            Log($"  ✓ Set button height to {answerButtonHeight}px");
            Log($"  ✓ Set answer font size to {answerFontSize}px");
        }

        private void ConfigureTimer()
        {
            Log("Configuring timer...");

            var timerSlider = GetComponentInChildren<Slider>();
            if (timerSlider != null)
            {
                var timerRect = timerSlider.GetComponent<RectTransform>();
                if (timerRect != null)
                {
                    timerRect.sizeDelta = new Vector2(timerRect.sizeDelta.x, 30f);
                    Log("  ✓ Set timer height to 30px");
                }

                // Configure timer colors (done via QuestionPanelController, but we can set defaults)
                var fillImage = timerSlider.fillRect?.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = new Color(0.23f, 0.51f, 0.96f, 1f); // Blue
                    Log("  ✓ Set timer fill color to blue");
                }
            }
            else
            {
                Log("  ⚠ Timer slider not found");
            }
        }

        private void ConfigureTopicBar()
        {
            Log("Configuring topic bar...");

            var topicTransform = transform.Find("TopicBar");
            if (topicTransform == null)
            {
                // Create topic bar
                var topicObj = new GameObject("TopicBar");
                topicObj.transform.SetParent(transform, false);
                topicObj.transform.SetAsFirstSibling(); // Place at top

                var topicRect = topicObj.AddComponent<RectTransform>();
                topicRect.anchorMin = new Vector2(0, 1);
                topicRect.anchorMax = new Vector2(1, 1);
                topicRect.pivot = new Vector2(0.5f, 1f);
                topicRect.sizeDelta = new Vector2(0, 30f);
                topicRect.anchoredPosition = Vector2.zero;

                var topicBg = topicObj.AddComponent<Image>();
                topicBg.color = topicTint;

                // Add topic text
                var topicTextObj = new GameObject("TopicText");
                topicTextObj.transform.SetParent(topicObj.transform, false);

                var topicTextRect = topicTextObj.AddComponent<RectTransform>();
                topicTextRect.anchorMin = Vector2.zero;
                topicTextRect.anchorMax = Vector2.one;
                topicTextRect.sizeDelta = Vector2.zero;
                topicTextRect.anchoredPosition = Vector2.zero;

                var topicTMP = topicTextObj.AddComponent<TextMeshProUGUI>();
                topicTMP.text = "Topic: [Loading...]";
                topicTMP.fontSize = topicFontSize;
                topicTMP.color = Color.white;
                topicTMP.alignment = TextAlignmentOptions.Center;
                topicTMP.margin = new Vector4(10, 5, 10, 5);

                Log("  ✓ Created TopicBar (30px height, purple tint)");
            }
            else
            {
                Log("  ✓ TopicBar already exists");
            }
        }

        private void ConfigureLayoutGroups()
        {
            Log("Configuring layout groups...");

            // Find or create answer buttons container
            var answersContainer = transform.Find("AnswersContainer");
            if (answersContainer == null)
            {
                // Check if buttons are direct children - if so, create container
                if (transform.Find("Answer_A") != null)
                {
                    Log("  ℹ Answer buttons are direct children - consider grouping them in a VerticalLayoutGroup");
                    // Note: Manually restructuring hierarchy is complex - recommend doing this in Unity Editor
                }
            }
            else
            {
                // Configure layout group on container
                var verticalLayout = answersContainer.GetComponent<VerticalLayoutGroup>();
                if (verticalLayout == null)
                {
                    verticalLayout = answersContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                }

                verticalLayout.spacing = answerButtonSpacing;
                verticalLayout.childControlWidth = true;
                verticalLayout.childControlHeight = true;
                verticalLayout.childForceExpandWidth = true;
                verticalLayout.childForceExpandHeight = false;
                verticalLayout.childAlignment = TextAnchor.UpperCenter;
                verticalLayout.padding = new RectOffset(20, 20, 20, 20);

                Log($"  ✓ Configured VerticalLayoutGroup (spacing: {answerButtonSpacing}px)");
            }
        }

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[QuestionPanelUISetup] {message}");
            }
        }

        // Editor helper - call this from Inspector context menu
        [ContextMenu("Reset to Default Values")]
        public void ResetToDefaultValues()
        {
            modalSize = new Vector2(1200f, 864f);
            answerButtonHeight = 180f;
            answerButtonSpacing = 20f;
            questionFontSize = 36f;
            answerFontSize = 28f;
            headerFontSize = 24f;
            topicFontSize = 20f;

            modalBackground = new Color(0.12f, 0.16f, 0.22f, 1f);
            darkOverlay = new Color(0f, 0f, 0f, 0.75f);
            headerTint = new Color(0.23f, 0.51f, 0.96f, 0.2f);
            topicTint = new Color(0.55f, 0.34f, 0.76f, 0.3f);
            answerButtonNormal = new Color(0.2f, 0.23f, 0.3f, 1f);
            answerButtonHover = new Color(0.23f, 0.51f, 0.96f, 0.8f);
            answerTextColor = Color.white;
            questionTextColor = Color.white;

            Debug.Log("[QuestionPanelUISetup] Reset to default full-screen focus values");
        }
    }
}
