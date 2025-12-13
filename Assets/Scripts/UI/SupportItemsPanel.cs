using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using BossFight2D.Systems;

namespace BossFight2D.UI
{
    /// <summary>
    /// Displays support items on the right side of the HUD:
    /// - Lifeline button (50/50 help, eliminate 2 wrong answers)
    /// - Power-up indicators (double damage, shield, time freeze, etc.)
    /// Automatically manages availability and cooldowns.
    /// </summary>
    public class SupportItemsPanel : MonoBehaviour
    {
        [Header("Lifeline Button")]
        [SerializeField] private Button lifelineButton;
        [SerializeField] private TextMeshProUGUI lifelineText;
        [SerializeField] private TextMeshProUGUI lifelineCountText;
        [SerializeField] private Image lifelineIcon;
        [SerializeField] private int maxLifelines = 1;

        [Header("Power-Up Slots")]
        [SerializeField] private GameObject powerUpSlotPrefab;
        [SerializeField] private Transform powerUpsContainer;
        [SerializeField] private List<PowerUpSlot> powerUpSlots = new List<PowerUpSlot>();

        [Header("Auto-Configuration")]
        [SerializeField] private bool autoConfigureOnAwake = true;
        [SerializeField] private bool useThemeColors = true;

        private int lifelinesRemaining = 1;
        private bool lifelineUsedThisQuestion = false;

        void Awake()
        {
            if (autoConfigureOnAwake)
            {
                ConfigureLayout();
            }

            // Subscribe to events
            EventBus.QuestionStarted += OnQuestionStarted;
            EventBus.LifelineUsed += OnLifelineUsed;
            EventBus.PowerUpActivated += OnPowerUpActivated;
            EventBus.PowerUpExpired += OnPowerUpExpired;

            // Setup lifeline button click
            if (lifelineButton != null)
            {
                lifelineButton.onClick.AddListener(OnLifelineButtonClicked);
            }
        }

        void OnDestroy()
        {
            EventBus.QuestionStarted -= OnQuestionStarted;
            EventBus.LifelineUsed -= OnLifelineUsed;
            EventBus.PowerUpActivated -= OnPowerUpActivated;
            EventBus.PowerUpExpired -= OnPowerUpExpired;

            if (lifelineButton != null)
            {
                lifelineButton.onClick.RemoveListener(OnLifelineButtonClicked);
            }
        }

        [ContextMenu("Configure Support Items Layout")]
        public void ConfigureLayout()
        {
            Debug.Log("[SupportItemsPanel] === Configuring Support Items Layout ===");

            // Create vertical layout if not present
            if (GetComponent<VerticalLayoutGroup>() == null)
            {
                var layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 15f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(10, 10, 10, 10);
            }

            ConfigureLifelineButton();
            ConfigurePowerUpsContainer();
            ApplyThemeColors();

            Debug.Log("[SupportItemsPanel] === Configuration Complete ===");
        }

        private void ConfigureLifelineButton()
        {
            // Create lifeline button if not present
            if (lifelineButton == null)
            {
                var buttonObj = new GameObject("LifelineButton");
                buttonObj.transform.SetParent(transform, false);
                var buttonRect = buttonObj.AddComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(80f, 80f);

                // Add background image
                var buttonImage = buttonObj.AddComponent<Image>();
                buttonImage.color = UIThemeManager.Instance.buttonNormal;

                // Add button component
                lifelineButton = buttonObj.AddComponent<Button>();

                // Add layout element
                var layoutElement = buttonObj.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 80f;

                // Add button enhancer for animations
                buttonObj.AddComponent<UIButtonEnhancer>();

                // Create icon container
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(buttonObj.transform, false);
                var iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0f, 10f);
                iconRect.sizeDelta = new Vector2(40f, 40f);

                lifelineIcon = iconObj.AddComponent<Image>();
                lifelineIcon.color = UIThemeManager.Instance.textPrimary;

                // Create text label
                var textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0f, 0f);
                textRect.anchorMax = new Vector2(1f, 0f);
                textRect.anchoredPosition = new Vector2(0f, 10f);
                textRect.sizeDelta = new Vector2(0f, 20f);

                lifelineText = textObj.AddComponent<TextMeshProUGUI>();
                lifelineText.text = "50/50";
                lifelineText.fontSize = 14f;
                lifelineText.color = UIThemeManager.Instance.textSecondary;
                lifelineText.alignment = TextAlignmentOptions.Center;

                // Create count badge
                var countObj = new GameObject("Count");
                countObj.transform.SetParent(buttonObj.transform, false);
                var countRect = countObj.AddComponent<RectTransform>();
                countRect.anchorMin = new Vector2(1f, 1f);
                countRect.anchorMax = new Vector2(1f, 1f);
                countRect.anchoredPosition = new Vector2(-5f, -5f);
                countRect.sizeDelta = new Vector2(24f, 24f);

                var countBg = countObj.AddComponent<Image>();
                countBg.color = UIThemeManager.Instance.primaryPurple;

                lifelineCountText = countObj.AddComponent<TextMeshProUGUI>();
                lifelineCountText.text = "1";
                lifelineCountText.fontSize = 16f;
                lifelineCountText.color = Color.white;
                lifelineCountText.alignment = TextAlignmentOptions.Center;
                lifelineCountText.fontStyle = FontStyles.Bold;

                Debug.Log("  ✓ Created Lifeline button (80x80px)");
            }

            UpdateLifelineButton();
        }

        private void ConfigurePowerUpsContainer()
        {
            // Create power-ups container if not present
            if (powerUpsContainer == null)
            {
                var containerObj = new GameObject("PowerUpsContainer");
                containerObj.transform.SetParent(transform, false);
                var containerRect = containerObj.AddComponent<RectTransform>();
                containerRect.sizeDelta = new Vector2(80f, 200f);

                powerUpsContainer = containerObj.transform;

                // Add vertical layout
                var layout = containerObj.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                Debug.Log("  ✓ Created Power-Ups container (80x200px)");
            }
        }

        private void ApplyThemeColors()
        {
            if (!useThemeColors) return;

            var theme = UIThemeManager.Instance;

            if (lifelineButton != null)
            {
                var colors = lifelineButton.colors;
                colors.normalColor = theme.buttonNormal;
                colors.highlightedColor = theme.buttonHover;
                colors.pressedColor = theme.buttonPressed;
                colors.disabledColor = theme.buttonDisabled;
                lifelineButton.colors = colors;
            }

            if (lifelineText != null)
                lifelineText.color = theme.textSecondary;

            if (lifelineIcon != null)
                lifelineIcon.color = theme.textPrimary;

            Debug.Log("  ✓ Theme colors applied");
        }

        #region Lifeline Management

        private void OnLifelineButtonClicked()
        {
            if (!CanUseLifeline())
            {
                Debug.Log("[SupportItemsPanel] Cannot use lifeline (already used or none remaining)");
                return;
            }

            UseLifeline();
        }

        private bool CanUseLifeline()
        {
            return lifelinesRemaining > 0 && !lifelineUsedThisQuestion;
        }

        private void UseLifeline()
        {
            lifelinesRemaining--;
            lifelineUsedThisQuestion = true;

            Debug.Log($"[SupportItemsPanel] Lifeline used! Remaining: {lifelinesRemaining}");

            UpdateLifelineButton();

            // Trigger lifeline effect (eliminate 2 wrong answers)
            EventBus.TriggerLifeline();
        }

        private void UpdateLifelineButton()
        {
            if (lifelineButton == null) return;

            // Update interactability
            lifelineButton.interactable = CanUseLifeline();

            // Update count text
            if (lifelineCountText != null)
            {
                lifelineCountText.text = lifelinesRemaining.ToString();
                lifelineCountText.gameObject.SetActive(lifelinesRemaining > 0);
            }

            // Update visual state
            if (lifelineIcon != null)
            {
                lifelineIcon.color = lifelineButton.interactable
                    ? UIThemeManager.Instance.textPrimary
                    : UIThemeManager.Instance.textDisabled;
            }
        }

        /// <summary>
        /// Manually set lifeline count
        /// </summary>
        public void SetLifelineCount(int count)
        {
            lifelinesRemaining = Mathf.Max(0, count);
            lifelineUsedThisQuestion = false;
            UpdateLifelineButton();
        }

        /// <summary>
        /// Add lifelines (e.g., from power-up or achievement)
        /// </summary>
        public void AddLifeline(int count = 1)
        {
            lifelinesRemaining = Mathf.Min(lifelinesRemaining + count, maxLifelines);
            UpdateLifelineButton();
        }

        #endregion

        #region Power-Up Management

        /// <summary>
        /// Add a power-up indicator to the panel
        /// </summary>
        public void AddPowerUp(string powerUpId, string displayName, Sprite icon, float duration = 0f)
        {
            // Check if power-up already exists
            var existingSlot = powerUpSlots.Find(slot => slot.PowerUpId == powerUpId);
            if (existingSlot != null)
            {
                // Refresh duration if timed
                if (duration > 0f)
                {
                    existingSlot.SetDuration(duration);
                }
                return;
            }

            // Create new power-up slot
            GameObject slotObj;
            if (powerUpSlotPrefab != null)
            {
                slotObj = Instantiate(powerUpSlotPrefab, powerUpsContainer);
            }
            else
            {
                slotObj = CreateDefaultPowerUpSlot();
            }

            var slot = slotObj.GetComponent<PowerUpSlot>();
            if (slot == null)
            {
                slot = slotObj.AddComponent<PowerUpSlot>();
            }

            slot.Initialize(powerUpId, displayName, icon, duration);
            powerUpSlots.Add(slot);

            Debug.Log($"[SupportItemsPanel] Added power-up: {displayName}");
        }

        /// <summary>
        /// Remove a power-up indicator
        /// </summary>
        public void RemovePowerUp(string powerUpId)
        {
            var slot = powerUpSlots.Find(s => s.PowerUpId == powerUpId);
            if (slot != null)
            {
                powerUpSlots.Remove(slot);
                Destroy(slot.gameObject);
                Debug.Log($"[SupportItemsPanel] Removed power-up: {powerUpId}");
            }
        }

        private GameObject CreateDefaultPowerUpSlot()
        {
            var slotObj = new GameObject("PowerUpSlot");
            slotObj.transform.SetParent(powerUpsContainer, false);
            var slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(60f, 60f);

            // Add background
            var bgImage = slotObj.AddComponent<Image>();
            bgImage.color = UIThemeManager.Instance.backgroundMedium;

            // Add layout element
            var layoutElement = slotObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 60f;

            return slotObj;
        }

        #endregion

        #region Event Handlers

        private void OnQuestionStarted(Systems.QuestionData question)
        {
            // Reset lifeline availability for new question
            lifelineUsedThisQuestion = false;
            UpdateLifelineButton();
        }

        private void OnLifelineUsed()
        {
            // External lifeline usage (e.g., from keyboard shortcut)
            if (CanUseLifeline())
            {
                UseLifeline();
            }
        }

        private void OnPowerUpActivated(string powerUpId, string displayName, Sprite icon, float duration)
        {
            AddPowerUp(powerUpId, displayName, icon, duration);
        }

        private void OnPowerUpExpired(string powerUpId)
        {
            RemovePowerUp(powerUpId);
        }

        #endregion
    }

    /// <summary>
    /// Individual power-up slot component
    /// </summary>
    public class PowerUpSlot : MonoBehaviour
    {
        public string PowerUpId { get; private set; }

        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image durationFill;
        [SerializeField] private TextMeshProUGUI timerText;

        private float duration = 0f;
        private float remainingTime = 0f;
        private bool isTimed = false;

        public void Initialize(string id, string displayName, Sprite icon, float durationSeconds)
        {
            PowerUpId = id;
            duration = durationSeconds;
            remainingTime = durationSeconds;
            isTimed = duration > 0f;

            // Find or create UI elements
            if (iconImage == null)
                iconImage = GetComponentInChildren<Image>();

            if (iconImage != null && icon != null)
                iconImage.sprite = icon;

            if (nameText != null)
                nameText.text = displayName;

            if (isTimed && durationFill != null)
                durationFill.fillAmount = 1f;
        }

        void Update()
        {
            if (!isTimed) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                EventBus.TriggerPowerUpExpired(PowerUpId);
                return;
            }

            // Update duration fill
            if (durationFill != null)
            {
                durationFill.fillAmount = remainingTime / duration;
            }

            // Update timer text
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(remainingTime).ToString();
            }
        }

        public void SetDuration(float newDuration)
        {
            duration = newDuration;
            remainingTime = newDuration;
            isTimed = true;
        }
    }
}
