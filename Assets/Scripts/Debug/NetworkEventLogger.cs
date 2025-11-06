using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement; 
using System.Collections.Generic; 

// Attaches to a lightweight GameObject and subscribes to NetworkManager callbacks
// to capture detailed logs about network and scene loading.
// This helps diagnose client join failures and scene transition issues.
public class NetworkEventLogger : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogWarning("[NetworkEventLogger] NetworkManager.Singleton is null at OnEnable");
            return;
        }

        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        nm.OnServerStarted += OnServerStarted;

        if (nm.SceneManager != null)
        {
            nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            // Subscribe via lambda so we don't need to reference the SceneEvent type directly.
            nm.SceneManager.OnSceneEvent += (evt) =>
                Debug.Log($"[NetworkEventLogger] OnSceneEvent: type={evt.SceneEventType}, scene='{evt.SceneName}', clientId={evt.ClientId}");
        }
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnServerStarted -= OnServerStarted;
            if (nm.SceneManager != null)
            {
                nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                // No explicit unsubscribe for OnSceneEvent since we subscribed with an inline lambda.
            }
        }
    }

    private void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            var transport = nm.GetComponent<UnityTransport>();
            string transportInfo = transport == null ? "null" : transport.ToString();
            Debug.Log($"[NetworkEventLogger] Start. IsServer={nm.IsServer}, IsClient={nm.IsClient}, IsHost={nm.IsHost}, EnableSceneManagement={nm.NetworkConfig.EnableSceneManagement}, Transport={transportInfo}");
        }
    }

    private void OnServerStarted()
    {
        Debug.Log("[NetworkEventLogger] OnServerStarted");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkEventLogger] OnClientConnected: clientId={clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkEventLogger] OnClientDisconnected: clientId={clientId}");
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"[NetworkEventLogger] OnLoadEventCompleted: scene='{sceneName}', mode={loadSceneMode}, completed={clientsCompleted?.Count ?? 0}, timedOut={clientsTimedOut?.Count ?? 0}");
    }

    // We intentionally avoid a method that explicitly references SceneEvent to remain compatible
    // with NGO versions where SceneEvent's namespace differs.
}