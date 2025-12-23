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

        private int lifelinesRemaining;
        private bool lifelineUsedThisQuestion = false;

        void Awake()
        {
            lifelinesRemaining = Mathf.Max(0, maxLifelines);
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

            UpdateLifelineButton();
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
            var theme = UIThemeManager.Instance;

            // Create lifeline button if not present
            if (lifelineButton == null)
            {
                var buttonObj = new GameObject("LifelineButton");
                buttonObj.transform.SetParent(transform, false);
                var buttonRect = buttonObj.AddComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(80f, 80f);

                // Add background image
                var buttonImage = buttonObj.AddComponent<Image>();
                buttonImage.color = theme != null ? theme.buttonNormal : Color.white;

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
                lifelineIcon.color = theme != null ? theme.textPrimary : Color.white;

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
                lifelineText.color = theme != null ? theme.textSecondary : Color.white;
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
                countBg.color = theme != null ? theme.primaryPurple : Color.black;

                lifelineCountText = countObj.AddComponent<TextMeshProUGUI>();
                lifelineCountText.text = "1";
                lifelineCountText.fontSize = 16f;
                lifelineCountText.color = Color.white;
                lifelineCountText.alignment = TextAlignmentOptions.Center;
                lifelineCountText.fontStyle = FontStyles.Bold;

                Debug.Log("[SupportItemsPanel] Created Lifeline button (80x80px)");
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

                Debug.Log("[SupportItemsPanel] Created Power-Ups container (80x200px)");
            }
        }

        private void ApplyThemeColors()
        {
            if (!useThemeColors) return;

            var theme = UIThemeManager.Instance;
            if (theme == null) return;

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

            Debug.Log("[SupportItemsPanel] Theme colors applied");
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
                var theme = UIThemeManager.Instance;
                if (theme != null)
                {
                    lifelineIcon.color = lifelineButton.interactable
                        ? theme.textPrimary
                        : theme.textDisabled;
                }
            }
        }

        /// <summary>
        /// Manually set lifeline count
        /// </summary>
        public void SetLifelineCount(int count)
        {
            lifelinesRemaining = Mathf.Clamp(count, 0, Mathf.Max(0, maxLifelines));
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
            if (string.IsNullOrEmpty(powerUpId))
            {
                Debug.Log("[SupportItemsPanel] Ignoring power-up with empty id");
                return;
            }

            PruneNullPowerUpSlots();

            var existingSlot = FindPowerUpSlot(powerUpId);
            if (existingSlot != null)
            {
                // Refresh duration if timed
                if (duration > 0f)
                {
                    existingSlot.SetDuration(duration);
                }
                return;
            }

            if (powerUpsContainer == null)
            {
                ConfigurePowerUpsContainer();
            }

            var parent = powerUpsContainer != null ? powerUpsContainer : transform;

            // Create new power-up slot
            GameObject slotObj;
            if (powerUpSlotPrefab != null)
            {
                slotObj = Instantiate(powerUpSlotPrefab, parent);
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
            if (string.IsNullOrEmpty(powerUpId)) return;

            PruneNullPowerUpSlots();

            var slot = FindPowerUpSlot(powerUpId);
            if (slot != null)
            {
                powerUpSlots.Remove(slot);
                Destroy(slot.gameObject);
                Debug.Log($"[SupportItemsPanel] Removed power-up: {powerUpId}");
            }
        }

        private void PruneNullPowerUpSlots()
        {
            for (var i = powerUpSlots.Count - 1; i >= 0; i--)
            {
                if (powerUpSlots[i] == null)
                {
                    powerUpSlots.RemoveAt(i);
                }
            }
        }

        private PowerUpSlot FindPowerUpSlot(string powerUpId)
        {
            for (var i = 0; i < powerUpSlots.Count; i++)
            {
                var slot = powerUpSlots[i];
                if (slot != null && slot.PowerUpId == powerUpId)
                {
                    return slot;
                }
            }

            return null;
        }

        private GameObject CreateDefaultPowerUpSlot()
        {
            if (powerUpsContainer == null)
            {
                ConfigurePowerUpsContainer();
            }

            var slotObj = new GameObject("PowerUpSlot");
            slotObj.transform.SetParent(powerUpsContainer != null ? powerUpsContainer : transform, false);
            var slotRect = slotObj.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(60f, 60f);

            // Add background
            var bgImage = slotObj.AddComponent<Image>();
            var theme = UIThemeManager.Instance;
            bgImage.color = theme != null ? theme.backgroundMedium : Color.black;

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
        private bool expiredRaised = false;

        public void Initialize(string id, string displayName, Sprite icon, float durationSeconds)
        {
            PowerUpId = id;
            duration = Mathf.Max(0f, durationSeconds);
            remainingTime = duration;
            isTimed = duration > 0f;
            expiredRaised = false;

            EnsureUi();

            if (iconImage == null)
                iconImage = GetComponentInChildren<Image>(true);

            if (iconImage != null && icon != null)
                iconImage.sprite = icon;

            if (nameText != null)
                nameText.text = displayName;

            UpdateDurationUi();
        }

        void Update()
        {
            if (!isTimed) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                isTimed = false;
                UpdateDurationUi();

                if (!expiredRaised)
                {
                    expiredRaised = true;
                    if (!string.IsNullOrEmpty(PowerUpId))
                    {
                        EventBus.TriggerPowerUpExpired(PowerUpId);
                    }
                }
                return;
            }

            UpdateDurationUi();
        }

        public void SetDuration(float newDuration)
        {
            duration = Mathf.Max(0f, newDuration);
            remainingTime = duration;
            isTimed = duration > 0f;
            expiredRaised = false;
            EnsureUi();
            UpdateDurationUi();
        }

        private void EnsureUi()
        {
            if (iconImage == null)
            {
                var iconTransform = transform.Find("Icon");
                if (iconTransform == null)
                {
                    var iconObj = new GameObject("Icon");
                    iconObj.transform.SetParent(transform, false);
                    var rect = iconObj.AddComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(36f, 36f);
                    iconImage = iconObj.AddComponent<Image>();
                }
                else
                {
                    iconImage = iconTransform.GetComponent<Image>();
                    if (iconImage == null)
                    {
                        iconImage = iconTransform.gameObject.AddComponent<Image>();
                    }
                }
            }

            if (durationFill == null)
            {
                var fillTransform = transform.Find("DurationFill");
                if (fillTransform == null)
                {
                    var fillObj = new GameObject("DurationFill");
                    fillObj.transform.SetParent(transform, false);
                    var rect = fillObj.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    durationFill = fillObj.AddComponent<Image>();
                    durationFill.type = Image.Type.Filled;
                    durationFill.fillMethod = Image.FillMethod.Radial360;
                    durationFill.fillOrigin = (int)Image.Origin360.Top;
                    durationFill.fillClockwise = false;

                    var theme = UIThemeManager.Instance;
                    durationFill.color = theme != null ? theme.primaryPurple : new Color(1f, 1f, 1f, 0.35f);
                }
                else
                {
                    durationFill = fillTransform.GetComponent<Image>();
                    if (durationFill == null)
                    {
                        durationFill = fillTransform.gameObject.AddComponent<Image>();
                    }
                }
            }

            if (timerText == null)
            {
                var timerTransform = transform.Find("TimerText");
                if (timerTransform == null)
                {
                    var timerObj = new GameObject("TimerText");
                    timerObj.transform.SetParent(transform, false);
                    var rect = timerObj.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    timerText = timerObj.AddComponent<TextMeshProUGUI>();
                    timerText.fontSize = 14f;
                    timerText.alignment = TextAlignmentOptions.Center;
                    timerText.fontStyle = FontStyles.Bold;

                    var theme = UIThemeManager.Instance;
                    timerText.color = theme != null ? theme.textPrimary : Color.white;
                }
                else
                {
                    timerText = timerTransform.GetComponent<TextMeshProUGUI>();
                    if (timerText == null)
                    {
                        timerText = timerTransform.gameObject.AddComponent<TextMeshProUGUI>();
                    }
                }
            }

            if (durationFill != null)
            {
                durationFill.transform.SetAsFirstSibling();
            }
            if (iconImage != null)
            {
                iconImage.transform.SetAsLastSibling();
            }
            if (timerText != null)
            {
                timerText.transform.SetAsLastSibling();
            }
        }

        private void UpdateDurationUi()
        {
            if (durationFill != null)
            {
                durationFill.gameObject.SetActive(isTimed);
                if (isTimed && duration > 0f)
                {
                    durationFill.fillAmount = Mathf.Clamp01(remainingTime / duration);
                }
            }

            if (timerText != null)
            {
                timerText.gameObject.SetActive(isTimed);
                if (isTimed)
                {
                    timerText.text = Mathf.CeilToInt(remainingTime).ToString();
                }
            }
        }
    }
}
