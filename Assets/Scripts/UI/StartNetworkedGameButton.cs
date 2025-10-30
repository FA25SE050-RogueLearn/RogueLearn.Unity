using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

// Allows the host to trigger a synchronized load to the Gameplay scene.
// Clients will automatically follow via NetworkSceneManager.
public class StartNetworkedGameButton : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI statusLabelTMP;
    [SerializeField] private Text statusLabelUI;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }
    }

    private void SetStatus(string msg)
    {
        if (statusLabelTMP != null) statusLabelTMP.text = msg;
        if (statusLabelUI != null) statusLabelUI.text = msg;
        Debug.Log(msg);
    }

    private void OnStartClicked()
    {
        if (NetworkManager.Singleton == null)
        {
            SetStatus("No NetworkManager found.");
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            SetStatus("Only the host/server can start the game.");
            return;
        }

        var sceneManager = NetworkManager.Singleton.SceneManager;
        if (sceneManager == null || !NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            SetStatus("Network scene management is disabled.");
            return;
        }

        SetStatus("Loading Gameplay for all clients...");
        sceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }
}