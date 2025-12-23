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
    private RelayConnector _relayConnector;
    private bool _localReady;

    private void Awake()
    {
        EnsureCanvasAndPanel();

        ResolveJoinPanelReferences();
        if (joinButton != null)
        {
            joinButton.onClick.RemoveListener(OnJoinClicked);
            joinButton.onClick.AddListener(OnJoinClicked);
        }

        RefreshLobbyReference();
        BindRelayConnectorEvents();
    }

    private void Update()
    {
        UpdateLobbyStatusText();
        UpdatePanelVisibility();
    }

    private void ResolveJoinPanelReferences()
    {
        if (joinPanel == null)
        {
            joinPanel = GameObject.Find("JoinPanel");
        }

        if (joinPanel == null) return;

        if (joinCodeInput == null)
        {
            joinCodeInput = FindChildDeep(joinPanel.transform, "JoinCodeInput")?.GetComponent<TMP_InputField>();
        }

        if (joinButton == null)
        {
            joinButton = FindChildDeep(joinPanel.transform, "JoinButton")?.GetComponent<Button>();
        }

        if (statusLabel == null)
        {
            statusLabel = FindChildDeep(joinPanel.transform, "StatusLabel")?.GetComponent<TextMeshProUGUI>();
        }
    }

    private static Transform FindChildDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == name) return t;
        }
        return null;
    }

    private void BindRelayConnectorEvents()
    {
        if (_relayConnector != null) return;
        _relayConnector = RelayConnector.GetOrCreate("RelayConnector");
        if (_relayConnector == null) return;
        _relayConnector.OnJoinCodeGenerated += OnJoinCodeGenerated;
        _relayConnector.OnStatus += OnRelayStatus;
    }

    private void UpdateLobbyStatusText()
    {
        if (_lobby == null) return;
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

    private void UpdatePanelVisibility()
    {
        var nm = NetworkManager.Singleton;
        var isConnected = nm != null && (nm.IsHost || nm.IsConnectedClient);
        if (_lobby == null && isConnected)
        {
            RefreshLobbyReference();
        }

        var inLobby = isConnected && _lobby != null;
        if (_lobbyPanel != null) _lobbyPanel.SetActive(inLobby);
        if (joinPanel != null) joinPanel.SetActive(!inLobby);
    }

    private void EnsureCanvasAndPanel()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            canvas = new GameObject("Canvas");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

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
            BindExistingLobbyPanel(panel);
        }

        _lobbyPanel = panel;
        if (_readyButton != null)
        {
            _readyButton.onClick.RemoveListener(OnReadyClicked);
            _readyButton.onClick.AddListener(OnReadyClicked);
        }
        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(false);
        }
    }

    private void BindExistingLobbyPanel(GameObject panel)
    {
        _lobbyPanel = panel;

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

    private void RefreshLobbyReference()
    {
        _lobby = FindFirstObjectByType<LobbyStateManager>();
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
                RefreshLobbyReference();
            }
        }
    }

    private async void OnJoinClicked()
    {
        try
        {
            if (_relayConnector == null)
            {
                Debug.LogWarning("[LobbyUI] RelayConnector is missing.");
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
            await _relayConnector.JoinClientWithRelayAsync(code);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LobbyUI] Failed to join client: {ex.Message}");
            if (statusLabel != null) statusLabel.text = "Join failed.";
        }
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
        if (joinButton != null)
        {
            joinButton.onClick.RemoveListener(OnJoinClicked);
        }

        if (_readyButton != null)
        {
            _readyButton.onClick.RemoveListener(OnReadyClicked);
        }

        if (_relayConnector != null)
        {
            _relayConnector.OnJoinCodeGenerated -= OnJoinCodeGenerated;
            _relayConnector.OnStatus -= OnRelayStatus;
        }
    }
}
