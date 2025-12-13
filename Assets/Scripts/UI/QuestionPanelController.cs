using UnityEngine;
using TMPro;
using UnityEngine.UI;
using BossFight2D.Systems;
using BossFight2D.Quiz;
using Unity.Netcode;
using BossFight2D.Player;
using UnityEngine.EventSystems;

namespace BossFight2D.UI
{
    public class QuestionPanelController : MonoBehaviour
    {
        public static QuestionPanelController Instance { get; private set; }
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private Button answerA;
        [SerializeField] private Button answerB;
        [SerializeField] private Button answerC;
        [SerializeField] private Button answerD;
        [SerializeField] private Slider timerSlider;

        [Header("Text References")]
        [SerializeField] private TextMeshProUGUI answerAText;
        [SerializeField] private TextMeshProUGUI answerBText;
        [SerializeField] private TextMeshProUGUI answerCText;
        [SerializeField] private TextMeshProUGUI answerDText;

        [Header("Timer Visuals")]
        [SerializeField] private Image timerFillImage; // Fill image of the slider
        [SerializeField] private Color timerNormalColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color timerWarningColor = new Color(1f, 0.65f, 0f);
        [SerializeField] private Color timerDangerColor = new Color(0.9f, 0.2f, 0.2f);
        [Range(0f, 1f)][SerializeField] private float warningThreshold = 0.3f;
        [Range(0f, 1f)][SerializeField] private float dangerThreshold = 0.15f;

        [Header("Answer Feedback")]
        [SerializeField] private Color correctFlashColor = new Color(0.2f, 0.9f, 0.2f);
        [SerializeField] private Color incorrectFlashColor = new Color(0.9f, 0.25f, 0.25f);
        [SerializeField] private float flashDuration = 0.35f;

        private bool isActive = false;
        [Header("Transitions & Debug")]
        [SerializeField] private bool useFadeTransitions = true;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private bool debugPanelState = true;
        private CanvasGroup _canvasGroup;

        // Cache base colors to restore after flashes
        private Color baseColorA = Color.white;
        private Color baseColorB = Color.white;
        private Color baseColorC = Color.white;
        private Color baseColorD = Color.white;

        // Event-driven UI overlays
        bool showAdvancePrompt = false;
        bool showPowerPlayBanner = false;
        float powerPlayEndTime = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }
            else
            {
                Instance = this;
            }

            // Prefer an explicitly named child for the question prompt
            if (questionText == null)
            {
                var qTransform = transform.Find("Question");
                if (qTransform != null)
                {
                    questionText = qTransform.GetComponent<TextMeshProUGUI>();
                    if (questionText == null)
                    {
                        // In case TMP is deeper
                        questionText = qTransform.GetComponentInChildren<TextMeshProUGUI>();
                    }
                }
                // Fallback: first TMP under panel (may pick a button label if layout differs)
                if (questionText == null)
                {
                    questionText = GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            if (answerA == null) answerA = transform.Find("Answer_A")?.GetComponent<Button>();
            if (answerB == null) answerB = transform.Find("Answer_B")?.GetComponent<Button>();
            if (answerC == null) answerC = transform.Find("Answer_C")?.GetComponent<Button>();
            if (answerD == null) answerD = transform.Find("Answer_D")?.GetComponent<Button>();

            // Get text components from buttons
            if (answerAText == null && answerA != null) answerAText = answerA.GetComponentInChildren<TextMeshProUGUI>();
            if (answerBText == null && answerB != null) answerBText = answerB.GetComponentInChildren<TextMeshProUGUI>();
            if (answerCText == null && answerC != null) answerCText = answerC.GetComponentInChildren<TextMeshProUGUI>();
            if (answerDText == null && answerD != null) answerDText = answerD.GetComponentInChildren<TextMeshProUGUI>();

            if (timerSlider == null)
                timerSlider = GetComponentInChildren<Slider>();

            // Try to resolve slider fill image
            if (timerFillImage == null && timerSlider != null && timerSlider.fillRect != null)
            {
                timerFillImage = timerSlider.fillRect.GetComponent<Image>();
            }

            // Cache base colors from target graphics
            if (answerA != null && answerA.targetGraphic != null) baseColorA = answerA.targetGraphic.color;
            if (answerB != null && answerB.targetGraphic != null) baseColorB = answerB.targetGraphic.color;
            if (answerC != null && answerC.targetGraphic != null) baseColorC = answerC.targetGraphic.color;
            if (answerD != null && answerD.targetGraphic != null) baseColorD = answerD.targetGraphic.color;

            // Setup CanvasGroup for fade transitions
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null && useFadeTransitions)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                _canvasGroup.alpha = 0f;
            }
        }

        void Start()
        {
            // Wire up answer button events
            if (answerA != null) answerA.onClick.AddListener(() => SubmitAnswer(0));
            if (answerB != null) answerB.onClick.AddListener(() => SubmitAnswer(1));
            if (answerC != null) answerC.onClick.AddListener(() => SubmitAnswer(2));
            if (answerD != null) answerD.onClick.AddListener(() => SubmitAnswer(3));

            // Suppress attacks on pointer down so the click does not trigger combat before OnClick
            SetupPointerDownSuppressor(answerA);
            SetupPointerDownSuppressor(answerB);
            SetupPointerDownSuppressor(answerC);
            SetupPointerDownSuppressor(answerD);

            // Subscribe to events
            EventBus.AdvancePromptShown += OnAdvanceShown;
            EventBus.AdvancePromptHidden += OnAdvanceHidden;
            EventBus.PowerPlayStarted += OnPowerPlayStarted;
            EventBus.PowerPlayEnded += OnPowerPlayEnded;
            EventBus.GameWon += OnGameEnded;
            EventBus.GameLost += OnGameEnded;

            // Validate bindings and warn if anything is missing
            ValidateReferences();

            // Start hidden
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            EventBus.AdvancePromptShown -= OnAdvanceShown;
            EventBus.AdvancePromptHidden -= OnAdvanceHidden;
            EventBus.PowerPlayStarted -= OnPowerPlayStarted;
            EventBus.PowerPlayEnded -= OnPowerPlayEnded;
            EventBus.GameWon -= OnGameEnded;
            EventBus.GameLost -= OnGameEnded;
        }

        void Update()
        {
            if (isActive && QuizManager.Instance != null && timerSlider != null)
            {
                // Update timer visual based on server-driven value
                float progress = QuizManager.Instance.RemainingTime.Value / QuizManager.Instance.CurrentQuestionTimeLimit.Value;
                progress = Mathf.Clamp01(progress);
                timerSlider.value = progress;

                // Update timer color based on thresholds
                if (timerFillImage != null)
                {
                    if (progress <= dangerThreshold)
                        timerFillImage.color = timerDangerColor;
                    else if (progress <= warningThreshold)
                        timerFillImage.color = timerWarningColor;
                    else
                        timerFillImage.color = timerNormalColor;
                }
            }
        }

        public void ShowQuestion(QuestionData question)
        {
            // Ensure no pending hide from previous question interferes
            CancelInvoke(nameof(HidePanel));

            isActive = true;
            gameObject.SetActive(true);
            if (useFadeTransitions && _canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeCanvas(1f, fadeInDuration));
            }
            if (debugPanelState)
            {
                LogPanelState("ShowQuestion");
            }
            showAdvancePrompt = false; // hidden while a question is active

            // Populate question text
            if (questionText != null)
                questionText.text = question.prompt;

            // Populate answer options
            if (question.options != null && question.options.Length >= 4)
            {
                if (answerAText != null) answerAText.text = "A) " + question.options[0];
                if (answerBText != null) answerBText.text = "B) " + question.options[1];
                if (answerCText != null) answerCText.text = "C) " + question.options[2];
                if (answerDText != null) answerDText.text = "D) " + question.options[3];
            }

            // Reset timer
            if (timerSlider != null)
            {
                timerSlider.maxValue = 1f;
                timerSlider.value = 1f;
            }
            if (timerFillImage != null)
                timerFillImage.color = timerNormalColor;

            // Enable buttons and reset visuals/visibility
            SetButtonsInteractable(true);
            ResetAnswerVisibilityAndColors();
        }

        public void ShowResolution(int selectedIndex, bool isCorrect, int correctIndex)
        {
            isActive = false;
            SetButtonsInteractable(false);

            // Notify HUD of question answer
            EventBus.RaiseQuestionAnswered(isCorrect);

            // Flash the selected button to indicate correctness
            if (selectedIndex >= 0)
            {
                var selectedBtn = GetButtonByIndex(selectedIndex);
                if (selectedBtn != null)
                    StartCoroutine(FlashButton(selectedBtn, isCorrect ? correctFlashColor : incorrectFlashColor, flashDuration));
            }

            // Highlight the correct answer if the player was wrong
            if (!isCorrect)
            {
                var correctBtn = GetButtonByIndex(correctIndex);
                if (correctBtn != null)
                    StartCoroutine(FlashButton(correctBtn, correctFlashColor, flashDuration));
            }

            // Hide the panel after a delay
            Invoke(nameof(HidePanel), 2.5f);
        }

        public void HidePanel()
        {
            // Raise AnswerModeExited to let systems re-enable input/combat appropriately
            EventBus.RaiseAnswerModeExited();

            if (debugPanelState)
            {
                LogPanelState("HidePanel");
            }

            if (useFadeTransitions && _canvasGroup != null && gameObject.activeSelf)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOutAndDeactivate());
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void SubmitAnswer(int choice)
        {
            if (QuizManager.Instance != null && isActive)
            {
                // Immediately suppress local player attacks for the duration of post-answer lock
                var localCombat = FindLocalPlayerCombat();
                if (localCombat != null)
                {
                    localCombat.BeginAnswerInteractionLocal();
                }
                // Send the answer through the network so non-host clients are handled correctly
                QuizManager.Instance.SubmitAnswerServerRpc(choice);
                SetButtonsInteractable(false); // Prevent multiple submissions
            }
        }

        private PlayerCombat FindLocalPlayerCombat()
        {
            var combats = Object.FindObjectsOfType<PlayerCombat>();
            foreach (var pc in combats)
            {
                if (pc.IsOwner) return pc;
            }
            return null;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (answerA != null) answerA.interactable = interactable;
            if (answerB != null) answerB.interactable = interactable;
            if (answerC != null) answerC.interactable = interactable;
            if (answerD != null) answerD.interactable = interactable;
        }

        private void ResetAnswerVisibilityAndColors()
        {
            if (answerA != null)
            {
                answerA.gameObject.SetActive(true);
                if (answerA.targetGraphic != null) answerA.targetGraphic.color = baseColorA;
            }
            if (answerB != null)
            {
                answerB.gameObject.SetActive(true);
                if (answerB.targetGraphic != null) answerB.targetGraphic.color = baseColorB;
            }
            if (answerC != null)
            {
                answerC.gameObject.SetActive(true);
                if (answerC.targetGraphic != null) answerC.targetGraphic.color = baseColorC;
            }
            if (answerD != null)
            {
                answerD.gameObject.SetActive(true);
                if (answerD.targetGraphic != null) answerD.targetGraphic.color = baseColorD;
            }
        }

        private UnityEngine.UI.Button GetButtonByIndex(int idx)
        {
            switch (idx)
            {
                case 0: return answerA;
                case 1: return answerB;
                case 2: return answerC;
                case 3: return answerD;
                default: return null;
            }
        }

        private System.Collections.IEnumerator FlashButton(Button btn, Color flashColor, float duration)
        {
            if (btn == null || btn.targetGraphic == null) yield break;
            var g = btn.targetGraphic;
            Color original = g.color;
            g.color = flashColor;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // flash unaffected by time freeze
                yield return null;
            }
            // Smoothly revert back
            float back = 0.15f;
            t = 0f;
            while (t < back)
            {
                t += Time.unscaledDeltaTime;
                g.color = Color.Lerp(flashColor, original, t / back);
                yield return null;
            }
            g.color = original;
        }

        private void ValidateReferences()
        {
            if (questionText == null)
                Debug.LogWarning("QuestionPanelController: Missing reference for 'Question' text. Ensure a child named 'Question' with a TextMeshProUGUI is present.");
            if (answerA == null || answerAText == null)
                Debug.LogWarning("QuestionPanelController: Missing binding for Answer_A button or its TMP text child.");
            if (answerB == null || answerBText == null)
                Debug.LogWarning("QuestionPanelController: Missing binding for Answer_B button or its TMP text child.");
            if (answerC == null || answerCText == null)
                Debug.LogWarning("QuestionPanelController: Missing binding for Answer_C button or its TMP text child.");
            if (answerD == null || answerDText == null)
                Debug.LogWarning("QuestionPanelController: Missing binding for Answer_D button or its TMP text child.");
            if (timerSlider == null)
                Debug.LogWarning("QuestionPanelController: Timer Slider was not found. Timer UI will not update.");
            if (timerSlider != null && timerFillImage == null)
                Debug.LogWarning("QuestionPanelController: Timer fill Image not found. Urgency coloring will be disabled.");

            if (debugPanelState)
            {
                var canvases = GetComponentsInParent<Canvas>(true);
                foreach (var c in canvases)
                {
                    Debug.Log($"[QPC] Canvas '{c.name}': renderMode={c.renderMode}, sortLayerID={c.sortingLayerID}, sortOrder={c.sortingOrder}, overrideSorting={c.overrideSorting}");
                }
                var ray = GetComponentInParent<UnityEngine.UI.GraphicRaycaster>();
                Debug.Log($"[QPC] GraphicRaycaster present: {(ray != null)}; EventSystem present: {(EventSystem.current != null)}; Panel layer: {gameObject.layer}");
                Debug.Log($"[QPC] CanvasGroup present: {(_canvasGroup != null)}; useFadeTransitions={useFadeTransitions}");
            }
        }

        // Event handlers for overlays
        void OnAdvanceShown() { showAdvancePrompt = true; }
        void OnAdvanceHidden() { showAdvancePrompt = false; }
        void OnPowerPlayStarted(float duration)
        {
            showPowerPlayBanner = true; powerPlayEndTime = Time.time + duration;
            if (debugPanelState)
            {
                Debug.Log($"[QPC] Power Play START (duration={duration:F2}s). Panel active={gameObject.activeSelf}, isActiveFlag={isActive}");
            }
            // Immediately pause question/answer interaction during Power Play
            CancelInvoke(nameof(HidePanel));
            SetButtonsInteractable(false);
            isActive = false;
            // Force immediate hide to avoid any fade-in/out race conditions
            ImmediateHidePanel();
        }
        void OnPowerPlayEnded() { showPowerPlayBanner = false; if (debugPanelState) Debug.Log("[QPC] Power Play END."); }
        void OnGameEnded()
        {
            // Immediately hide the panel on win/lose
            ImmediateHidePanel();
        }

        void OnGUI()
        {
            // Simple overlay prompts for prototype
            if (showAdvancePrompt)
            {
                var gm = BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.GameManager>();
                if (gm != null && gm.State == BossFight2D.Core.GameState.Playing)
                {
                    var style = new GUIStyle(GUI.skin.label); style.fontSize = 20; style.alignment = TextAnchor.LowerCenter; style.normal.textColor = Color.white;
                    GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 30), "Press E to continue", style);
                }
            }
            if (showPowerPlayBanner)
            {
                float remaining = Mathf.Max(0f, powerPlayEndTime - Time.time);
                string txt = $"POWER PLAY! First Hit Bonus – {Mathf.CeilToInt(remaining)}s";
                var style2 = new GUIStyle(GUI.skin.box); style2.fontSize = 18; style2.alignment = TextAnchor.UpperCenter;
                GUI.Box(new Rect(Screen.width / 2 - 180, 10, 360, 28), txt, style2);
            }
        }

        private System.Collections.IEnumerator FadeOutAndDeactivate()
        {
            yield return FadeCanvas(0f, fadeOutDuration);
            gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator FadeCanvas(float targetAlpha, float duration)
        {
            if (_canvasGroup == null)
            {
                yield break;
            }
            float start = _canvasGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, targetAlpha, duration > 0f ? (t / duration) : 1f);
                _canvasGroup.alpha = a;
                yield return null;
            }
            _canvasGroup.alpha = targetAlpha;
        }

        private void LogPanelState(string context)
        {
            Debug.Log($"[QPC] {context}: activeSelf={gameObject.activeSelf}, isActiveFlag={isActive}, canvasGroupAlpha={(_canvasGroup != null ? _canvasGroup.alpha : -1f)}");
        }

        private void ImmediateHidePanel()
        {
            // Raise exit event and hide instantly
            EventBus.RaiseAnswerModeExited();
            StopAllCoroutines();
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            if (debugPanelState)
            {
                LogPanelState("ImmediateHidePanel");
            }
        }

        /// <summary>
        /// Set up a button to suppress PointerDown events when pressed.
        /// This is useful for buttons that trigger answer interactions.
        /// Avoid trigger attack of the character while answering.
        /// </summary>
        private void SetupPointerDownSuppressor(Button btn)
        {
            if (btn == null) return;
            var trigger = btn.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = btn.gameObject.AddComponent<EventTrigger>();
            }
            // Avoid duplicate entries
            bool hasPointerDown = false;
            foreach (var e in trigger.triggers)
            {
                if (e.eventID == EventTriggerType.PointerDown) { hasPointerDown = true; break; }
            }
            if (!hasPointerDown)
            {
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                entry.callback.AddListener((BaseEventData data) =>
                {
                    var localCombat = FindLocalPlayerCombat();
                    if (localCombat != null)
                    {
                        localCombat.BeginAnswerInteractionLocal();
                        if (debugPanelState)
                        {
                            Debug.Log("[QPC] PointerDown suppression: BeginAnswerInteractionLocal invoked.");
                        }
                    }
                });
                trigger.triggers.Add(entry);
            }
        }
    }
}
