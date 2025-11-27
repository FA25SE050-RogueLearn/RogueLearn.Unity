// Headless server bootstrap for dedicated server runs.
// Previously wrapped in UNITY_SERVER, but we make it safe to include in all builds
// and only execute in batch/headless mode. This ensures Dockerized headless builds
// still start the server even if UNITY_SERVER is not defined at build time.
using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine.SceneManagement;
using BossFight2D.Systems;

namespace BossFight2D.Network
{
    public static class ServerBootstrap
    {
        // Configure your expected max concurrent clients per match.
        private const int MaxConnections = 20;

        // Entry point before any scene loads. Headless server spins up automatically.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void InitializeHeadlessServer()
        {
            // Extra guard: only run in batch mode to avoid accidental Editor invocation.
            if (!Application.isBatchMode)
            {
                Debug.Log("[ServerBootstrap] Skipped: not running in batch mode.");
                return;
            }

            Debug.Log("[ServerBootstrap] Starting initialization...");

            try
            {
                // Optionally force-load a server scene that contains NetworkManager.
                // You can override via env var UNITY_SERVER_SCENE; defaults to ServerHeadless.
                string serverScene = Environment.GetEnvironmentVariable("UNITY_SERVER_SCENE");
                // Default to ServerHeadless so host stays in server-only scene; clients will show lobby UI overlay.
                if (string.IsNullOrWhiteSpace(serverScene)) serverScene = "ServerHeadless";
                Debug.Log($"[ServerBootstrap] Loading server scene: {serverScene}");
                SceneManager.LoadScene(serverScene);

                await InitializeUnityServicesAsync();

                // Read optional env vars to control Relay region and max connections.
                var maxConnEnv = Environment.GetEnvironmentVariable("RL_MAX_CONNECTIONS");
                int maxConn = MaxConnections;
                if (!string.IsNullOrWhiteSpace(maxConnEnv) && int.TryParse(maxConnEnv, out var parsed))
                {
                    maxConn = Mathf.Clamp(parsed, 2, 100);
                }

                var relayRegion = Environment.GetEnvironmentVariable("RELAY_REGION");
                Allocation allocation;
                if (!string.IsNullOrWhiteSpace(relayRegion))
                {
                    Debug.Log($"[ServerBootstrap] Creating Relay allocation. max={maxConn}, region={relayRegion}");
                    allocation = await RelayService.Instance.CreateAllocationAsync(maxConn, relayRegion);
                }
                else
                {
                    Debug.Log($"[ServerBootstrap] Creating Relay allocation. max={maxConn}");
                    allocation = await RelayService.Instance.CreateAllocationAsync(maxConn);
                }
                var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"[ServerBootstrap] Relay Join Code: {joinCode}");
                Debug.Log($"[ServerBootstrap] {{\"event\":\"relay_join_code\",\"joinCode\":\"{joinCode}\",\"region\":\"{relayRegion}\",\"max\":{maxConn}}}");

                var manager = NetworkManager.Singleton;
                if (manager == null)
                {
                    Debug.LogError("[ServerBootstrap] NetworkManager.Singleton not found in the active scene.");
                    return;
                }

                // Enable Netcode scene management so the server can synchronize scene transitions
                // (e.g., moving everyone from lobby to gameplay together).
                manager.NetworkConfig.EnableSceneManagement = true;
                var transport = manager.NetworkConfig.NetworkTransport as UnityTransport;
                if (transport == null)
                {
                    // Try to attach UnityTransport at runtime for robustness in headless runs
                    transport = manager.gameObject.GetComponent<UnityTransport>();
                    if (transport == null)
                    {
                        transport = manager.gameObject.AddComponent<UnityTransport>();
                        Debug.Log("[ServerBootstrap] Attached UnityTransport to NetworkManager at runtime.");
                    }
                    manager.NetworkConfig.NetworkTransport = transport;
                }

                // Configure Relay for the host/server path. Use DTLS for secure UDP.
                var relayServerData = new RelayServerData(allocation, "dtls");
                transport.SetRelayServerData(relayServerData);

                // Ensure the dedicated server/host does NOT spawn its own Player object.
                // We enable NGO's ConnectionApproval and instruct it to skip player creation
                // for the server's local connection (ServerClientId) when running headless.
                manager.NetworkConfig.ConnectionApproval = true;
                manager.ConnectionApprovalCallback = ApproveConnectionNoHostPlayer;

                // IMPORTANT: For Relay, run StartHost() even on headless.
                // NGO StartServer() does not bind to Relay; StartHost opens the Relay host endpoint.
                if (!manager.IsListening)
                {
                    Debug.Log("[ServerBootstrap] Starting Host (headless server mode)...");
                    var started = manager.StartHost();
                    Debug.Log(started
                        ? "[ServerBootstrap] Host started successfully. Waiting for clients..."
                        : "[ServerBootstrap] Host failed to start.");
                    if (started)
                    {
                        Debug.Log("[ServerBootstrap] {\"event\":\"server_started\"}");
                        // Ensure ServerMatchRecorder exists so match completion is logged even without scene authoring.
                        try
                        {
                            var recorderGo = new GameObject("ServerMatchRecorder");
                            GameObject.DontDestroyOnLoad(recorderGo);
                            recorderGo.AddComponent<ServerMatchRecorder>();
                            Debug.Log("[ServerBootstrap] ServerMatchRecorder initialized.");
                            // Pre-create ServerAutoStartOnReady so when Gameplay loads, the quiz can auto-start when all players are ready.
                            var autoStartGo = new GameObject("ServerAutoStartOnReady");
                            GameObject.DontDestroyOnLoad(autoStartGo);
                            autoStartGo.AddComponent<BossFight2D.Network.ServerAutoStartOnReady>();
                            Debug.Log("[ServerBootstrap] ServerAutoStartOnReady initialized.");

                            // Find scene-authored LobbyStateManager (NetworkObject) and publish join code.
                            // Avoid runtime-spawned NetworkObject to prevent prefab registration errors on clients.
                            var lobby = GameObject.FindObjectOfType<LobbyStateManager>();
                            if (lobby != null)
                            {
                                lobby.SetJoinCode(joinCode);
                                Debug.Log("[ServerBootstrap] Join code published via scene-authored LobbyStateManager.");
                            }
                            else
                            {
                                // With NGO scene management disabled, clients will not load ServerHeadless
                                // and cannot see scene-authored NetworkObjects. Prefer a NetworkPrefab that
                                // we spawn here so clients in any scene can receive it.
                                try
                                {
                                    var lobbyPrefab = Resources.Load<GameObject>("Network/LobbyStateManager");
                                    if (lobbyPrefab != null)
                                    {
                                        var spawned = GameObject.Instantiate(lobbyPrefab);
                                        var no = spawned.GetComponent<NetworkObject>();
                                        if (no == null) no = spawned.AddComponent<NetworkObject>();
                                        // Spawn as a global object (don't tie to server scene) so clients in ClientUI can receive it
                                        no.Spawn(destroyWithScene: false);
                                        lobby = spawned.GetComponent<LobbyStateManager>();
                                        if (lobby != null)
                                        {
                                            lobby.SetJoinCode(joinCode);
                                        }
                                        Debug.Log("[ServerBootstrap] Spawned LobbyStateManager prefab and published join code.");
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[ServerBootstrap] LobbyStateManager not found in scene and prefab 'Resources/Network/LobbyStateManager' missing. Clients will not see join code in lobby UI.");
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"[ServerBootstrap] Failed to spawn LobbyStateManager prefab: {e.Message}");
                                }
                            }

                            // Fallback safety: if a host player object was auto-created anyway,
                            // remove it immediately to keep the server in host-only mode.
                            TryRemoveServerPlayer(manager);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[ServerBootstrap] Failed to initialize ServerMatchRecorder: {e.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerBootstrap] Initialization failed: {ex}");
            }
        }

        private static async Task InitializeUnityServicesAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                    Debug.Log("[ServerBootstrap] Unity Services initialized.");
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[ServerBootstrap] Signed in anonymously. PlayerId={AuthenticationService.Instance.PlayerId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerBootstrap] Unity Services/Auth init error: {e}");
                throw;
            }
        }

        // Connection approval: approve all clients and fully control player object creation ourselves.
        // We suppress automatic NGO PlayerPrefab spawning for ALL connections and instead spawn via
        // LobbyStateManager.EnsurePlayerSpawnServerRpc when Gameplay loads on clients. This avoids
        // race conditions between NGO auto-spawn and our manual spawn logic across scenes.
        private static void ApproveConnectionNoHostPlayer(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            // Disable auto PlayerObject spawning for all connections; we'll spawn explicitly later.
            response.CreatePlayerObject = false;
            // Ensure the response is applied immediately (older NGO versions require Pending=false).
            response.Pending = false;
        }

        // Safety helper: if a server player object exists for the host, despawn and destroy it.
        private static void TryRemoveServerPlayer(NetworkManager manager)
        {
            try
            {
                if (Application.isBatchMode && manager.IsHost)
                {
                    if (manager.ConnectedClients.TryGetValue(NetworkManager.ServerClientId, out var serverClient))
                    {
                        var playerObj = serverClient.PlayerObject;
                        if (playerObj != null)
                        {
                            Debug.Log("[ServerBootstrap] Removing server host PlayerObject to keep server non-playable.");
                            playerObj.Despawn(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerBootstrap] TryRemoveServerPlayer encountered an error: {ex.Message}");
            }
        }
    }
}









