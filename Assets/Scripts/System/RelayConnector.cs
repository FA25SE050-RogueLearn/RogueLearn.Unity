using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models;



namespace BossFight2D.Systems
{
    /// <summary>
    /// Helper for starting Host/Client via Unity Relay.
    /// Handles Unity Services init, anonymous sign-in, allocation/join, and configuring UnityTransport.
    /// </summary>
    [Preserve]
    public class RelayConnector : MonoBehaviour
    {
        public static RelayConnector Instance { get; private set; }

        public event Action<string> OnJoinCodeGenerated;
        public event Action<string> OnStatus;

        private bool _servicesInitialized = false;
        private string _pendingJoinCodeForServer = null;
        [SerializeField]
        [Tooltip("Optional: Set a fixed Relay region to avoid QoS selection on unsupported platforms (e.g., WebGL). Example: 'us-central' or 'eu-west'.")]
        private string preferredRegion = "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); }
            else { Instance = this; DontDestroyOnLoad(gameObject); }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // This method is invoked from the WebGL page via SendMessage. The Preserve attribute prevents IL2CPP
        // from stripping it during managed code stripping, which would otherwise cause
        // "null function or function signature mismatch" in the browser.
        [Preserve]
        public void JoinWithCode(string code)
        {
            Debug.Log($"[WebGL] JoinWithCode: {code}");
            _pendingJoinCodeForServer = code;
            _ = JoinClientWithRelayAsync(code);
            // Also resolve the session and fetch the pack so questions can be loaded.
            try { GameSessionClient.Instance?.BeginAutoResolveWithJoinCode(code); }
            catch (Exception e) { Debug.LogWarning($"[WebGL] JoinWithCode: failed to trigger GameSessionClient resolve: {e.Message}"); }
        }
#endif
        private async Task EnsureServicesAsync()
        {
            if (_servicesInitialized) return;
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                _servicesInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unity Services init/sign-in failed: {ex.Message}");
                OnStatus?.Invoke($"Services failure: {ex.Message}");
                throw;
            }
        }

        public async Task StartHostWithRelayAsync(int maxConnections = 4)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) nm = FindObjectOfType<NetworkManager>();
            if (nm == null) { Debug.LogError("No NetworkManager found."); OnStatus?.Invoke("No NetworkManager found."); return; }
            if (nm.IsServer || nm.IsClient || nm.IsHost) { Debug.LogWarning("Network instance already running."); OnStatus?.Invoke("Network instance already running."); return; }

#if UNITY_WEBGL && !UNITY_EDITOR
            // Netcode + UnityTransport cannot run a server/host in WebGL. Host from desktop/editor and let WebGL clients join via Relay.
            var msg = "Hosting is not supported in WebGL builds. Please start the host from a desktop build or the Unity Editor, then share the join code.";
            Debug.LogWarning(msg);
            OnStatus?.Invoke(msg);
            return;
#endif

            await EnsureServicesAsync();

            try
            {
                // Keep NetworkConfig consistent with clients and dedicated server: enable connection approval.
                // For Editor/desktop hosting we approve all clients and DO create player objects.
                nm.NetworkConfig.ConnectionApproval = true;
                // Enable NGO scene management so the server can synchronize scene transitions
                // (clients will follow server-initiated scene loads such as moving to Gameplay).
                nm.NetworkConfig.EnableSceneManagement = true;
                nm.ConnectionApprovalCallback = (request, response) =>
                {
                    response.Approved = true;
                    response.CreatePlayerObject = true;
                    response.Pending = false;
                };
                Debug.Log($"[RelayConnector] Host NetworkConfig: Approval={nm.NetworkConfig.ConnectionApproval}, SceneMgmt={nm.NetworkConfig.EnableSceneManagement}");

                Allocation alloc;
                if (!string.IsNullOrWhiteSpace(preferredRegion))
                {
                    alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections, preferredRegion);
                }
                else
                {
                    alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                }
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

                var transport = nm.GetComponent<UnityTransport>();
                if (transport == null) { Debug.LogError("UnityTransport not found on NetworkManager."); OnStatus?.Invoke("UnityTransport not found."); return; }

#if UNITY_WEBGL && !UNITY_EDITOR
                transport.SetRelayServerData(new RelayServerData(alloc, "wss"));
#else
                transport.SetRelayServerData(new RelayServerData(alloc, "dtls"));
#endif
                bool ok = nm.StartHost();
                if (!ok)
                {
                    OnStatus?.Invoke("Failed to start host.");
                }
                else
                {
                    OnStatus?.Invoke("Host started.");
                    OnJoinCodeGenerated?.Invoke(joinCode);
                    Debug.Log($"Relay Host started. Join Code: {joinCode}");
                    WireNetworkDebugCallbacks(nm);
                }
            }
            catch (RequestFailedException rfe)
            {
                Debug.LogError($"Relay allocation failed: {rfe.Message}");
                OnStatus?.Invoke($"Relay error: {rfe.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error starting host with Relay: {ex.Message}");
                OnStatus?.Invoke($"Unexpected error: {ex.Message}");
            }
        }

        public async Task JoinClientWithRelayAsync(string joinCode)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) nm = FindObjectOfType<NetworkManager>();
            if (nm == null) { Debug.LogError("No NetworkManager found."); OnStatus?.Invoke("No NetworkManager found."); return; }
            if (nm.IsServer || nm.IsClient || nm.IsHost) { Debug.LogWarning("Network instance already running."); OnStatus?.Invoke("Network instance already running."); return; }
            if (string.IsNullOrWhiteSpace(joinCode)) { OnStatus?.Invoke("Enter a join code."); Debug.LogWarning("Join code is empty."); return; }

            await EnsureServicesAsync();

            try
            {
                // Ensure client NetworkConfig matches server expectations when joining a headless host that uses ConnectionApproval.
                // Having mismatched NetworkConfig (e.g., server requires approval but client doesn’t) can yield
                // "Incomplete connection request message given config" during the handshake.
                nm.NetworkConfig.ConnectionApproval = true;
                // Enable NGO scene management on clients to match the server and support synchronized scene loads.
                nm.NetworkConfig.EnableSceneManagement = true;
                nm.ConnectionApprovalCallback = (request, response) => { };
                Debug.Log($"[RelayConnector] Client NetworkConfig: Approval={nm.NetworkConfig.ConnectionApproval}, SceneMgmt={nm.NetworkConfig.EnableSceneManagement}");

                // Remember the join code so we can forward it to the server once connected,
                // and trigger client-side resolve for visibility in the Inspector.
                // Note: Only the server will fetch and inject the pack; the client resolve stops after obtaining session/pack_url.
                _pendingJoinCodeForServer = joinCode.Trim();
                try
                {
                    GameSessionClient.Instance?.BeginAutoResolveWithJoinCode(joinCode.Trim());
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RelayConnector] Failed to trigger GameSessionClient resolve on client: {e.Message}");
                }

                JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());
                var transport = nm.GetComponent<UnityTransport>();
                if (transport == null) { Debug.LogError("UnityTransport not found on NetworkManager."); OnStatus?.Invoke("UnityTransport not found."); return; }

#if UNITY_WEBGL && !UNITY_EDITOR
                transport.SetRelayServerData(new RelayServerData(joinAlloc, "wss"));
#else
                transport.SetRelayServerData(new RelayServerData(joinAlloc, "dtls"));
#endif
                bool ok = nm.StartClient();
                if (!ok)
                {
                    OnStatus?.Invoke("Failed to start client.");
                }
                else
                {
                    OnStatus?.Invoke("Client started.");
                    Debug.Log("Relay Client started.");
                    WireNetworkDebugCallbacks(nm);
                    // If we joined via a code from WebGL, forward it to the server once connected
                    nm.OnClientConnectedCallback += id =>
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(_pendingJoinCodeForServer))
                            {
                                var lobby = FindFirstObjectByType<BossFight2D.Network.LobbyStateManager>();
                                lobby?.ResolveAndLoadPackServerRpc(_pendingJoinCodeForServer);
                                Debug.Log($"[RelayConnector] Forwarded join code to server for pack resolution: {_pendingJoinCodeForServer}");
                                _pendingJoinCodeForServer = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[RelayConnector] Failed to forward join code to server: {ex.Message}");
                        }
                    };
                }
            }
            catch (RequestFailedException rfe)
            {
                Debug.LogError($"Relay join failed: {rfe.Message}");
                OnStatus?.Invoke($"Relay error: {rfe.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error joining client with Relay: {ex.Message}");
                OnStatus?.Invoke($"Unexpected error: {ex.Message}");
            }
        }

        [Preserve]
        public void ConfigureUserApiBase(string baseUrl)
        {
            try
            {
                var gsc = GameSessionClient.Instance ?? FindFirstObjectByType<GameSessionClient>();
                if (gsc == null)
                {
                    var go = new GameObject("GameSessionClient");
                    gsc = go.AddComponent<GameSessionClient>();
                }
                gsc.SetUserApiBaseUrl(baseUrl);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RelayConnector] ConfigureUserApiBase failed: {e.Message}");
            }
        }

        [Preserve]
        public void StartSoloPractice(string packJson)
        {
            try
            {
                var gsc = GameSessionClient.Instance ?? FindFirstObjectByType<GameSessionClient>();
                if (gsc == null)
                {
                    var go = new GameObject("GameSessionClient");
                    gsc = go.AddComponent<GameSessionClient>();
                }
                gsc.SetAppMode("solo");
                gsc.InjectPackJson(packJson);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RelayConnector] StartSoloPractice failed: {e.Message}");
            }
        }

        private void WireNetworkDebugCallbacks(NetworkManager nm)
        {
            try
            {
                nm.OnClientConnectedCallback += id => Debug.Log($"[RelayConnector] OnClientConnected: {id}");
                nm.OnClientDisconnectCallback += id => Debug.Log($"[RelayConnector] OnClientDisconnected: {id}");
                nm.OnServerStarted += () => Debug.Log("[RelayConnector] OnServerStarted");
                if (nm.SceneManager != null)
                {
                    nm.SceneManager.OnLoadEventCompleted += (sceneName, mode, completed, timeouts) =>
                        Debug.Log($"[RelayConnector] OnLoadEventCompleted: scene='{sceneName}', completed={completed?.Count ?? 0}, timedOut={timeouts?.Count ?? 0}");
                    nm.SceneManager.OnSceneEvent += (evt) =>
                        Debug.Log($"[RelayConnector] OnSceneEvent: type={evt.SceneEventType}, scene='{evt.SceneName}', clientId={evt.ClientId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RelayConnector] Failed to wire network debug callbacks: {e.Message}");
            }
        }


        [Preserve]
        public void ConfigureUserId(string userId)
        {
            try
            {
                var gsc = GameSessionClient.Instance ?? UnityEngine.Object.FindFirstObjectByType<GameSessionClient>();
                if (gsc == null)
                {
                    var go = new UnityEngine.GameObject("GameSessionClient");
                    gsc = go.AddComponent<GameSessionClient>();
                }
                gsc.SetUserId(userId);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[RelayConnector] ConfigureUserId failed: " + (e.Message ?? e.ToString()));
            }
        }
    }
}
