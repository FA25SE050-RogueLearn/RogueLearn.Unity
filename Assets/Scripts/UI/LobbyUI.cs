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
            _countsLabel.text = $"Ready: {_lobby.ReadyCount.Value}/{_lobby.TotalPlayers.Value}";
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
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.Log("Missing Canvas.");
        }

        var panel = GameObject.Find("LobbyPanel");
        if (panel == null)
        {
            Debug.Log("Missing LobbyPanel.");
        }
        else
        {
            // If already exists, try to get references
            _title = GameObject.Find("LobbyPanel/Title")?.GetComponent<TextMeshProUGUI>();
            _countsLabel = GameObject.Find("LobbyPanel/Counts")?.GetComponent<TextMeshProUGUI>();
            _readyButton = GameObject.Find("LobbyPanel/ReadyButton")?.GetComponent<Button>();
            _readyBtnLabel = GameObject.Find("LobbyPanel/ReadyButton/Text")?.GetComponent<TextMeshProUGUI>();
            _joinCodeLabel = GameObject.Find("LobbyPanel/JoinCode")?.GetComponent<TextMeshProUGUI>();
            _lobbyPanel = panel;
            if (_readyButton != null)
            {
                _readyButton.onClick.RemoveAllListeners();
                _readyButton.onClick.AddListener(OnReadyClicked);
            }
            // Default hidden until connected/in lobby
            _lobbyPanel.SetActive(false);
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
        if (_lobby != null)
        {
            _lobby.SetReadyServerRpc(_localReady);
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
