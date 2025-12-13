using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using BossFight2D.Network;
using BossFight2D.Systems;


/// <summary>
/// Simple lobby overlay with a Ready button and Ready X/Y display.
/// Clients press Ready to signal to the server; when all are ready, the server will load Gameplay.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [SerializeField] private string panelTitle = "Waiting Room";
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.3f, 1f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.7f, 1f);

    // Join Panel refs (assign in Inspector or auto-discovered by name)
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private TextMeshProUGUI statusLabel;

    private GameObject _lobbyPanel;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _countsLabel;
    private Button _readyButton;
    private TextMeshProUGUI _readyBtnLabel;
    private TextMeshProUGUI _joinCodeLabel;
    private LobbyStateManager _lobby;
    private bool _localReady;

    private void Awake()
    {
        EnsureCanvasAndPanel();

        // Auto-discover Join Panel refs if not assigned
        if (joinPanel == null) joinPanel = GameObject.Find("JoinPanel");
        if (joinCodeInput == null) joinCodeInput = GameObject.Find("JoinPanel/JoinCodeInput")?.GetComponent<TMP_InputField>();
        if (joinButton == null) joinButton = GameObject.Find("JoinPanel/JoinButton")?.GetComponent<Button>();
        if (statusLabel == null) statusLabel = GameObject.Find("JoinPanel/StatusLabel")?.GetComponent<TextMeshProUGUI>();

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinClicked);
        }

        FindLobbyManager();
    }

    private void Update()
    {
        // Update counts label if lobby exists
        if (_lobby != null)
        {
            if (_countsLabel != null)
            {
                _countsLabel.text = $"Ready: {_lobby.ReadyCount.Value}/{_lobby.TotalPlayers.Value}";
            }
            var code = _lobby.JoinCode.Value.ToString();
            if (!string.IsNullOrWhiteSpace(code) && _joinCodeLabel != null)
            {
                _joinCodeLabel.text = $"Join Code: {code}";
            }
        }

        // Panel switching: show Join Panel when not connected; show Lobby Panel when connected and lobby exists
        var nm = NetworkManager.Singleton;
        bool isConnected = nm != null && (nm.IsHost || nm.IsConnectedClient);
        if (_lobby == null && isConnected)
        {
            _lobby = FindFirstObjectByType<LobbyStateManager>();
        }
        bool inLobby = isConnected && _lobby != null;
        if (_lobbyPanel != null) _lobbyPanel.SetActive(inLobby);
        if (joinPanel != null) joinPanel.SetActive(!inLobby);
    }

    private void EnsureCanvasAndPanel()
    {
        // Ensure EventSystem exists for UI input
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Ensure a Canvas exists
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            canvas = new GameObject("Canvas");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Ensure LobbyPanel exists (create a minimal one if missing)
        var panel = GameObject.Find("LobbyPanel");
        if (panel == null)
        {
            panel = new GameObject("LobbyPanel");
            panel.transform.SetParent(canvas.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            var img = panel.AddComponent<Image>();
            img.color = panelColor;
            // Position top-center strip
            rt.anchorMin = panelAnchorMin;
            rt.anchorMax = panelAnchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0, 120);

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            _title = titleGo.AddComponent<TextMeshProUGUI>();
            _title.text = panelTitle;
            _title.fontSize = 28;
            _title.alignment = TMPro.TextAlignmentOptions.Center;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -10);

            // Counts
            var countsGo = new GameObject("Counts");
            countsGo.transform.SetParent(panel.transform, false);
            _countsLabel = countsGo.AddComponent<TextMeshProUGUI>();
            _countsLabel.text = "Ready: 0/0";
            _countsLabel.fontSize = 20;
            _countsLabel.alignment = TMPro.TextAlignmentOptions.Center;
            var crt = countsGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0, -45);

            // Ready button
            var btnGo = new GameObject("ReadyButton");
            btnGo.transform.SetParent(panel.transform, false);
            _readyButton = btnGo.AddComponent<Button>();
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.8f, 0.3f, 0.8f);
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 1);
            brt.anchorMax = new Vector2(0.5f, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.sizeDelta = new Vector2(180, 40);
            brt.anchoredPosition = new Vector2(0, -80);

            var btnTextGo = new GameObject("Text");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            _readyBtnLabel = btnTextGo.AddComponent<TextMeshProUGUI>();
            _readyBtnLabel.text = "I'm Ready";
            _readyBtnLabel.fontSize = 22;
            _readyBtnLabel.alignment = TMPro.TextAlignmentOptions.Center;
            var btrt = btnTextGo.GetComponent<RectTransform>();
            btrt.anchorMin = new Vector2(0, 0);
            btrt.anchorMax = new Vector2(1, 1);
            btrt.pivot = new Vector2(0.5f, 0.5f);
            btrt.offsetMin = Vector2.zero;
            btrt.offsetMax = Vector2.zero;

            // Join code label
            var codeGo = new GameObject("JoinCode");
            codeGo.transform.SetParent(panel.transform, false);
            _joinCodeLabel = codeGo.AddComponent<TextMeshProUGUI>();
            _joinCodeLabel.text = "Join Code: -";
            _joinCodeLabel.fontSize = 18;
            _joinCodeLabel.alignment = TMPro.TextAlignmentOptions.Center;
            var jrt = codeGo.GetComponent<RectTransform>();
            jrt.anchorMin = new Vector2(0, 1);
            jrt.anchorMax = new Vector2(1, 1);
            jrt.pivot = new Vector2(0.5f, 1f);
            jrt.anchoredPosition = new Vector2(0, -120);
        }
        else
        {
            // Bind to existing authored UI elements by name so we don't recreate duplicates
            BindExistingLobbyPanel(panel);
        }

        // Hook up button handler and default visibility
        _lobbyPanel = panel;
        if (_readyButton != null)
        {
            _readyButton.onClick.RemoveAllListeners();
            _readyButton.onClick.AddListener(OnReadyClicked);
        }
        // Default hidden until connected/in lobby
        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(false);
        }
    }

    private void BindExistingLobbyPanel(GameObject panel)
    {
        _lobbyPanel = panel;

        // Helper to find children by name anywhere under the panel
        Transform FindChildDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        _title = FindChildDeep(panel.transform, "Title")?.GetComponent<TextMeshProUGUI>();
        _countsLabel = FindChildDeep(panel.transform, "Counts")?.GetComponent<TextMeshProUGUI>();

        var readyRoot = FindChildDeep(panel.transform, "ReadyButton");
        if (readyRoot != null)
        {
            _readyButton = readyRoot.GetComponent<Button>();
            if (_readyButton == null)
            {
                _readyButton = readyRoot.gameObject.AddComponent<Button>();
            }
            _readyBtnLabel = FindChildDeep(readyRoot, "Text")?.GetComponent<TextMeshProUGUI>();
        }

        _joinCodeLabel = FindChildDeep(panel.transform, "JoinCode")?.GetComponent<TextMeshProUGUI>();

        // Initialize the ready button label if present
        if (_readyBtnLabel != null)
        {
            _readyBtnLabel.text = _localReady ? "Unready" : "I'm Ready";
        }
    }

    private void FindLobbyManager()
    {
        _lobby = FindFirstObjectByType<LobbyStateManager>();
        if (RelayConnector.Instance != null)
        {
            RelayConnector.Instance.OnJoinCodeGenerated += OnJoinCodeGenerated;
            RelayConnector.Instance.OnStatus += OnRelayStatus;
        }
    }

    private void OnReadyClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }
        _localReady = !_localReady;
        _readyBtnLabel.text = _localReady ? "Unready" : "I'm Ready";
        if (_lobby != null && _lobby.IsSpawned)
        {
            _lobby.SetReadyServerRpc(_localReady);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] Ready toggle ignored: LobbyStateManager is missing or not spawned yet.");
            if (_lobby == null)
            {
                // Attempt to rediscover in case it spawned late
                FindLobbyManager();
            }
        }
    }

    private async void OnJoinClicked()
    {
        if (RelayConnector.Instance == null)
        {
            Debug.LogWarning("RelayConnector.Instance not found.");
            if (statusLabel != null) statusLabel.text = "Relay connector missing.";
            return;
        }
        var code = joinCodeInput != null ? joinCodeInput.text : string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            if (statusLabel != null) statusLabel.text = "Enter a join code.";
            return;
        }
        if (statusLabel != null) statusLabel.text = "Joining...";
        await RelayConnector.Instance.JoinClientWithRelayAsync(code);
    }

    private void OnJoinCodeGenerated(string code)
    {
        if (_joinCodeLabel != null && !string.IsNullOrWhiteSpace(code))
        {
            _joinCodeLabel.text = $"Join Code: {code}";
        }
    }

    private void OnRelayStatus(string msg)
    {
        if (statusLabel != null)
        {
            statusLabel.text = msg;
        }
    }

    private void OnDestroy()
    {
        if (RelayConnector.Instance != null)
        {
            RelayConnector.Instance.OnJoinCodeGenerated -= OnJoinCodeGenerated;
            RelayConnector.Instance.OnStatus -= OnRelayStatus;
        }
    }
}
