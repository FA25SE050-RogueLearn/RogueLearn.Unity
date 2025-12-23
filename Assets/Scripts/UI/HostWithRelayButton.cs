using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BossFight2D.Systems;

/// <summary>
/// UI driver for starting a Relay host.
/// </summary>
public class HostWithRelayButton : MonoBehaviour
{
    public Button hostButton;
    [Tooltip("Optional label to display the generated join code.")]
    public TextMeshProUGUI joinCodeLabel;
    [Tooltip("Optional status label for feedback.")]
    public TextMeshProUGUI statusLabel;

    private RelayConnector connector;

    private void Awake()
    {
        connector = RelayConnector.GetOrCreate("RelayConnector");
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostClicked);
        }
        else
        {
            Debug.LogWarning("[HostWithRelayButton] hostButton is not assigned.");
        }
        connector.OnJoinCodeGenerated += OnJoinCodeGenerated;
        connector.OnStatus += OnRelayStatus;
    }

    private void OnDestroy()
    {
        if (connector != null)
        {
            connector.OnJoinCodeGenerated -= OnJoinCodeGenerated;
            connector.OnStatus -= OnRelayStatus;
        }
    }

    private void OnJoinCodeGenerated(string code)
    {
        if (joinCodeLabel != null) joinCodeLabel.text = code;
    }

    private void OnRelayStatus(string message)
    {
        if (statusLabel != null) statusLabel.text = message;
    }

    private async void OnHostClicked()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        if (statusLabel != null) statusLabel.text = "Hosting is not supported in WebGL builds. Please host from desktop/editor.";
        Debug.LogWarning("Hosting is not supported in WebGL builds. Please host from desktop/editor.");
        return;
        #endif
        try
        {
            if (statusLabel != null) statusLabel.text = "Starting host...";
            if (connector == null)
            {
                Debug.LogError("[HostWithRelayButton] RelayConnector instance is missing.");
                if (statusLabel != null) statusLabel.text = "RelayConnector missing.";
                return;
            }
            await connector.StartHostWithRelayAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HostWithRelayButton] Failed to start host: {ex.Message}");
            if (statusLabel != null) statusLabel.text = "Host failed.";
        }
    }
}
