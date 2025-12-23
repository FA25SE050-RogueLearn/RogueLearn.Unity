using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BossFight2D.Systems;

/// <summary>
/// UI driver for joining as a Relay client.
/// </summary>
public class JoinAsClientButton : MonoBehaviour
{
    public Button joinAsClientButton;
    [Tooltip("Input field for the Relay join code (legacy UI InputField).")]
    public InputField joinCodeInput;
    [Tooltip("Input field for the Relay join code (TMP_InputField).")]
    public TMP_InputField joinCodeTMPInput;
    [Tooltip("Optional status label for feedback.")]
    public TextMeshProUGUI statusLabel;

    private RelayConnector connector;

    private void Awake()
    {
        connector = RelayConnector.GetOrCreate("RelayConnector");
        if (joinAsClientButton != null)
        {
            joinAsClientButton.onClick.AddListener(JoinAsClient);
        }
        else
        {
            Debug.LogWarning("[JoinAsClientButton] joinAsClientButton is not assigned.");
        }

        connector.OnStatus += OnRelayStatus;
    }

    private void OnDestroy()
    {
        if (connector != null)
        {
            connector.OnStatus -= OnRelayStatus;
        }
    }

    private void OnRelayStatus(string message)
    {
        if (statusLabel != null) statusLabel.text = message;
    }

    public void JoinAsClient()
    {
        var code = GetJoinCode();
        if (statusLabel != null) statusLabel.text = "Joining...";
        _ = JoinClientAsync(code);
    }

    private string GetJoinCode()
    {
        if (joinCodeTMPInput != null) return joinCodeTMPInput.text;
        if (joinCodeInput != null) return joinCodeInput.text;
        return string.Empty;
    }

    private async System.Threading.Tasks.Task JoinClientAsync(string code)
    {
        try
        {
            if (connector == null)
            {
                Debug.LogError("[JoinAsClientButton] RelayConnector instance is missing.");
                if (statusLabel != null) statusLabel.text = "RelayConnector missing.";
                return;
            }
            await connector.JoinClientWithRelayAsync(code);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[JoinAsClientButton] Failed to join as client: {ex.Message}");
            if (statusLabel != null) statusLabel.text = "Join failed.";
        }
    }
}
