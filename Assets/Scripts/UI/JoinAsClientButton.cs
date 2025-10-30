using UnityEngine;
using UnityEngine.UI;
using BossFight2D.Systems;
using Unity.Netcode;
using System.Threading.Tasks;
using BossFight2D.Systems;
using TMPro;



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
        connector = FindObjectOfType<RelayConnector>();
        if (connector == null)
        {
            var go = new GameObject("RelayConnector");
            connector = go.AddComponent<RelayConnector>();
        }
        joinAsClientButton.onClick.AddListener(JoinAsClient);
        connector.OnStatus += msg => { if (statusLabel != null) statusLabel.text = msg; };
    }

    public void JoinAsClient()
    {
        var code = joinCodeTMPInput != null ? joinCodeTMPInput.text : (joinCodeInput != null ? joinCodeInput.text : string.Empty);
        if (statusLabel != null) statusLabel.text = "Joining...";
        JoinClientAsync(code);

    }

    private async void JoinClientAsync(string code)
    {
        await connector.JoinClientWithRelayAsync(code);
    }
}