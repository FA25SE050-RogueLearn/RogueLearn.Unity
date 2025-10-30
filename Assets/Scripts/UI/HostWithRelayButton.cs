using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using BossFight2D.Systems;
using TMPro;

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
        connector = FindObjectOfType<RelayConnector>();
        if (connector == null)
        {
            var go = new GameObject("RelayConnector");
            connector = go.AddComponent<RelayConnector>();
        }
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostClicked);
        }
        // Subscribe to the OnJoinCodeGenerated event so that whenever RelayConnector
        // finishes allocating a relay and produces a join code, we automatically
        // display that code in the joinCodeLabel (if one is assigned in the Inspector).
        connector.OnJoinCodeGenerated += code => { if (joinCodeLabel != null) joinCodeLabel.text = code; };
        connector.OnStatus += msg => { if (statusLabel != null) statusLabel.text = msg; };
    }

    private async void OnHostClicked()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        if (statusLabel != null) statusLabel.text = "Hosting is not supported in WebGL builds. Please host from desktop/editor.";
        Debug.LogWarning("Hosting is not supported in WebGL builds. Please host from desktop/editor.");
        return;
        #endif
        if (statusLabel != null) statusLabel.text = "Starting host...";
        await connector.StartHostWithRelayAsync();
    }
}