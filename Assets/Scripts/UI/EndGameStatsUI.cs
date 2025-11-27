using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BossFight2D.Systems;
using BossFight2D.Quiz;
using System.Linq;
using System.Collections.Generic;

namespace BossFight2D.UI
{
    /// <summary>
    /// MVP: Shows end-game stats after win/lose and provides button to view detailed stats or return to lobby
    /// </summary>
    public class EndGameStatsUI : MonoBehaviour
    {
        public static EndGameStatsUI Instance { get; private set; }

        private Canvas canvas;
        private GameObject panel;
        private Text titleText;
        private Text statsText;
        private Button viewStatsButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EventBus.GameWon += OnGameWon;
            EventBus.GameLost += OnGameLost;
        }

        private void OnDestroy()
        {
            EventBus.GameWon -= OnGameWon;
            EventBus.GameLost -= OnGameLost;
        }

        private void OnGameWon()
        {
            ShowEndGameStats("Victory!", "You defeated the boss!");
        }

        private void OnGameLost()
        {
            ShowEndGameStats("Defeat", "The boss was too strong...");
        }

        private void ShowEndGameStats(string title, string subtitle)
        {
            EnsureUI();

            titleText.text = title;
            // MVP: Show simple message - full stats will be on frontend
            statsText.text = subtitle + "\n\nClick below to view your detailed stats!";
            panel.SetActive(true);
        }

        private void EnsureUI()
        {
            if (panel != null) return;

            // Create canvas
            canvas = new GameObject("EndGameStatsCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000; // Higher than other UI
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create panel background
            panel = new GameObject("EndGamePanel");
            panel.transform.SetParent(canvas.transform, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.9f);

            // Title
            titleText = CreateLabel(panel.transform, "Game Over", new Vector2(0, 300), 48, TextAnchor.MiddleCenter);
            titleText.fontStyle = FontStyle.Bold;

            // Stats text
            statsText = CreateLabel(panel.transform, "", new Vector2(0, 50), 24, TextAnchor.MiddleCenter);
            var statsRect = statsText.GetComponent<RectTransform>();
            statsRect.sizeDelta = new Vector2(800, 300);

            // View Stats Button (centered, single button)
            viewStatsButton = CreateButton(panel.transform, "View Your Stats", new Vector2(0, -250));
            viewStatsButton.onClick.AddListener(OnViewStatsClicked);

            panel.SetActive(false);
        }

        private Text CreateLabel(Transform parent, string text, Vector2 position, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(800, 100);
            rect.anchoredPosition = position;
            var label = go.AddComponent<Text>();
            label.text = text;
            label.alignment = alignment;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.color = Color.white;
            label.fontSize = fontSize;
            return label;
        }

        private Button CreateButton(Transform parent, string text, Vector2 position)
        {
            var go = new GameObject(text.Replace(" ", "") + "Button");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 60);
            rect.anchoredPosition = position;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.6f, 1f, 0.9f);
            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
            var label = textGo.AddComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.color = Color.white;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;

            return btn;
        }

        private void OnViewStatsClicked()
        {
            Debug.Log("[EndGameStatsUI] View Stats clicked - Redirecting to frontend stats page...");
            OpenStatsPage();
        }

        private void OpenStatsPage()
        {
            // MVP: Redirect to frontend stats page (Next.js on port 3000)
            // Get the frontend URL from environment or config
            string frontendUrl = System.Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
            string statsUrl = $"{frontendUrl}/stats";

            // For WebGL builds, open in same tab
            #if UNITY_WEBGL && !UNITY_EDITOR
            Application.ExternalEval($"window.location.href='{statsUrl}'");
            #else
            // For standalone builds, open in browser
            Application.OpenURL(statsUrl);
            Debug.Log($"[EndGameStatsUI] Opening stats page: {statsUrl}");
            #endif
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }
}
