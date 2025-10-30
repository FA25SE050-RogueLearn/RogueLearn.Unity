using System;
using System.Threading.Tasks;
using UnityEngine;
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
    public class RelayConnector : MonoBehaviour
    {
        public static RelayConnector Instance { get; private set; }

        public event Action<string> OnJoinCodeGenerated;
        public event Action<string> OnStatus;

        private bool _servicesInitialized = false;
        [SerializeField]
        [Tooltip("Optional: Set a fixed Relay region to avoid QoS selection on unsupported platforms (e.g., WebGL). Example: 'us-central' or 'eu-west'.")]
        private string preferredRegion = "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); }
            else { Instance = this; DontDestroyOnLoad(gameObject); }
        }

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
    }
}