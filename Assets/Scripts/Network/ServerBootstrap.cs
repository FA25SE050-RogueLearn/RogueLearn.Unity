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
                // You can override via env var UNITY_SERVER_SCENE; defaults to HostUI.
                string serverScene = Environment.GetEnvironmentVariable("UNITY_SERVER_SCENE");
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
    }
}









