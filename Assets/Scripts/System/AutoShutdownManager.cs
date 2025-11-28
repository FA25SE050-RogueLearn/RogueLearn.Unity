using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace BossFight2D.Systems
{
    /// <summary>
    /// Manages automatic shutdown of headless server when all clients disconnect.
    /// Also handles client disconnection after match ends to free up resources.
    /// </summary>
    public class AutoShutdownManager : MonoBehaviour
    {
        [Header("Shutdown Settings")]
        [SerializeField] private float shutdownDelayAfterEmpty = 30f; // Wait 30 seconds after all clients leave
        [SerializeField] private float clientDisconnectDelayAfterMatchEnd = 5f; // Wait 5 seconds after match ends before disconnecting clients

        private bool _matchEnded = false;
        private Coroutine _shutdownCoroutine;
        private bool _isShuttingDown = false;

        private void Awake()
        {
            // Subscribe to game end events
            EventBus.GameWon += OnMatchEnded;
            EventBus.GameLost += OnMatchEnded;
        }

        private void OnDestroy()
        {
            EventBus.GameWon -= OnMatchEnded;
            EventBus.GameLost -= OnMatchEnded;
        }

        private void OnMatchEnded()
        {
            if (_matchEnded) return;
            _matchEnded = true;

            Debug.Log("[AutoShutdown] Match ended, scheduling client disconnect and server monitoring");

            // On clients: disconnect after a short delay to allow stats to be sent
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                StartCoroutine(DisconnectClientAfterDelay());
            }

            // On server: start monitoring for empty server
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                StartCoroutine(MonitorServerActivity());
            }
        }

        /// <summary>
        /// Disconnect client after match ends to free resources
        /// </summary>
        private IEnumerator DisconnectClientAfterDelay()
        {
            Debug.Log($"[AutoShutdown] Client will disconnect in {clientDisconnectDelayAfterMatchEnd} seconds");
            yield return new WaitForSeconds(clientDisconnectDelayAfterMatchEnd);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                Debug.Log("[AutoShutdown] Disconnecting client from match");
                NetworkManager.Singleton.Shutdown();

                // Optional: Navigate to stats page or show a message
                // This could be triggered via an event that the UI listens to
                Debug.Log("[AutoShutdown] Client disconnected. Players can now view stats.");
            }
        }

        /// <summary>
        /// Monitor server for client connections and shut down when empty
        /// </summary>
        private IEnumerator MonitorServerActivity()
        {
            // Wait a moment for clients to potentially start disconnecting
            yield return new WaitForSeconds(2f);

            while (true)
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                {
                    yield break;
                }

                int connectedClients = GetConnectedClientCount();

                // In headless mode, the server itself is not counted as a "player"
                // So we check if there are any clients connected
                bool isEmpty = connectedClients == 0;

                if (isEmpty)
                {
                    // Server is empty, start shutdown countdown if not already started
                    if (_shutdownCoroutine == null && !_isShuttingDown)
                    {
                        Debug.Log($"[AutoShutdown] Server is empty. Starting shutdown countdown ({shutdownDelayAfterEmpty}s)");
                        _shutdownCoroutine = StartCoroutine(ShutdownCountdown());
                    }
                }
                else
                {
                    // Server has clients, cancel shutdown if it was scheduled
                    if (_shutdownCoroutine != null)
                    {
                        Debug.Log($"[AutoShutdown] Clients reconnected ({connectedClients}). Canceling shutdown.");
                        StopCoroutine(_shutdownCoroutine);
                        _shutdownCoroutine = null;
                    }
                }

                yield return new WaitForSeconds(5f); // Check every 5 seconds
            }
        }

        /// <summary>
        /// Get the count of connected clients (excluding the server itself in headless mode)
        /// </summary>
        private int GetConnectedClientCount()
        {
            if (NetworkManager.Singleton == null) return 0;

            int count = 0;
            bool isHeadlessHost = Application.isBatchMode && NetworkManager.Singleton.IsHost;

            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                // Skip the server/host in headless mode (it's not a real player)
                if (isHeadlessHost && clientId == NetworkManager.ServerClientId)
                    continue;

                count++;
            }

            return count;
        }

        /// <summary>
        /// Countdown before shutting down the server
        /// </summary>
        private IEnumerator ShutdownCountdown()
        {
            float remainingTime = shutdownDelayAfterEmpty;

            while (remainingTime > 0)
            {
                // Log countdown at intervals
                if (remainingTime == shutdownDelayAfterEmpty || remainingTime <= 10f || remainingTime % 10 == 0)
                {
                    Debug.Log($"[AutoShutdown] Server shutting down in {remainingTime:F0} seconds...");
                }

                yield return new WaitForSeconds(1f);
                remainingTime -= 1f;

                // Double-check if clients reconnected during countdown
                int connectedClients = GetConnectedClientCount();
                if (connectedClients > 0)
                {
                    Debug.Log($"[AutoShutdown] Clients reconnected during countdown. Aborting shutdown.");
                    _shutdownCoroutine = null;
                    yield break;
                }
            }

            // Time's up, shut down the server
            ShutdownServer();
        }

        /// <summary>
        /// Shut down the server application
        /// </summary>
        private void ShutdownServer()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            Debug.Log("[AutoShutdown] ========================================");
            Debug.Log("[AutoShutdown] Shutting down headless server (no clients connected)");
            Debug.Log("[AutoShutdown] ========================================");

            // Shutdown network
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // In headless/batch mode, quit the application
            if (Application.isBatchMode)
            {
                Debug.Log("[AutoShutdown] Application.Quit() called");
                Application.Quit();

#if UNITY_EDITOR
                // In editor, stop play mode
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        /// <summary>
        /// Manual shutdown trigger (can be called from other scripts if needed)
        /// </summary>
        public void TriggerShutdown()
        {
            Debug.Log("[AutoShutdown] Manual shutdown triggered");
            ShutdownServer();
        }
    }
}
