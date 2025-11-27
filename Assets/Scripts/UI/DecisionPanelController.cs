using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace BossFight2D.UI
{
    public class DecisionPanelController : MonoBehaviour
    {
        public static DecisionPanelController Instance { get; private set; }
        private Canvas canvas;
        private GameObject panel;
        private Button continueButton;
        private Button attackButton;
        private Text timerText;
        private Text tallyText;
        private Text titleText;
        private Text ruleText;
        private float hideAt;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void EnsureUI()
        {
            if (panel != null) return;
            canvas = new GameObject("DecisionCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var raycaster = canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            panel = new GameObject("DecisionPanel");
            panel.transform.SetParent(canvas.transform, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320, 140);
            rect.anchoredPosition = Vector2.zero;
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);

            titleText = CreateLabel(panel.transform, "Make a choice", new Vector2(0, 36), 20);
            ruleText = CreateLabel(panel.transform, "", new Vector2(0, 20), 14);
            timerText = CreateLabel(panel.transform, "", new Vector2(0, 4), 16);
            tallyText = CreateLabel(panel.transform, "", new Vector2(0, -12), 14);
            continueButton = CreateButton(panel.transform, "Continue Quiz", new Vector2(-80, -46));
            attackButton = CreateButton(panel.transform, "Attack Boss", new Vector2(80, -46));

            continueButton.onClick.AddListener(() => Submit(false));
            attackButton.onClick.AddListener(() => Submit(true));
        }

        private Button CreateButton(Transform parent, string text, Vector2 pos)
        {
            var go = new GameObject(text.Replace(" ", "") + "Button");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(140, 40);
            rect.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.9f);
            var btn = go.AddComponent<Button>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one; tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.text = text; label.alignment = TextAnchor.MiddleCenter; label.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); label.color = Color.black;
            return btn;
        }

        private Text CreateLabel(Transform parent, string text, Vector2 pos, int size)
        {
            var go = new GameObject(text.Replace(" ", "") + "Label");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 22);
            rect.anchoredPosition = pos;
            var label = go.AddComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.color = Color.white;
            label.fontSize = size;
            return label;
        }

        public void Show(float seconds, int rule)
        {
            EnsureUI();
            panel.SetActive(true);
            hideAt = Time.time + seconds;
            UpdateTimer();
            UpdateTally(0, 0);
            ruleText.text = rule == 0 ? "Rule: Any Attack" : "Rule: Majority Attack";
            titleText.text = "Make a choice";
            // MVP FIX: Re-enable buttons in case they were hidden by ShowResult()
            if (continueButton != null) continueButton.gameObject.SetActive(true);
            if (attackButton != null) attackButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            hideAt = 0f;
        }

        void Update()
        {
            if (hideAt > 0f && Time.time >= hideAt)
            {
                Hide();
            }
            if (hideAt > 0f) UpdateTimer();
        }

        private void Submit(bool attack)
        {
            var qm = BossFight2D.Quiz.QuizManager.Instance;
            if (qm != null && NetworkManager.Singleton != null)
            {
                qm.SubmitDecisionServerRpc(attack);
            }
            // MVP FIX: Don't hide locally - let server control the flow via HideDecisionClientRpc
            // This prevents the panel from flickering (hide → show result → hide)
        }

        public void UpdateTally(int cont, int atk)
        {
            if (tallyText != null) tallyText.text = "Continue: " + cont + "  Attack: " + atk;
        }

        private void UpdateTimer()
        {
            if (timerText == null) return;
            if (panel == null || !panel.activeSelf) return;
            var qm = BossFight2D.Quiz.QuizManager.Instance;
            float remain = 0f;
            if (qm != null)
            {
                try { remain = qm.DecisionRemaining.Value; } catch { remain = 0f; }
            }
            // Fallback to local timer when replication is not yet applied
            if (remain <= 0f)
            {
                var localRemain = Mathf.Max(0f, hideAt - Time.time);
                if (localRemain <= 0f && panel != null) { panel.SetActive(false); }
                timerText.text = "Time: " + Mathf.CeilToInt(localRemain) + "s";
            }
            else
            {
                timerText.text = "Time: " + Mathf.CeilToInt(remain) + "s";
            }
        }

        public void ShowResult(string message, float seconds)
        {
            EnsureUI();
            // MVP FIX: Don't call SetActive(true) - panel should already be active from Show()
            // This prevents the double-appearance bug where panel hides then immediately shows again
            hideAt = Time.time + seconds;
            titleText.text = message;
            ruleText.text = "";
            timerText.text = "";
            tallyText.text = "";
            // Hide buttons during result display so users can't click them
            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (attackButton != null) attackButton.gameObject.SetActive(false);
        }
    }
}
