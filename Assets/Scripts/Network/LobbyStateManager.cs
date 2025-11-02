using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Unity.Collections;

namespace BossFight2D.Network
{
    /// <summary>
    /// Networked lobby manager that tracks total playable clients and ready count.
    /// When all players ready, the server transitions everyone to the Gameplay scene.
    /// Excludes the headless host from counts in batch mode.
    /// </summary>
    public class LobbyStateManager : NetworkBehaviour
    {
        public NetworkVariable<int> TotalPlayers = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> ReadyCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // Relay Join Code broadcast to all clients (so they can share it). Server writes; everyone reads.
        public NetworkVariable<FixedString64Bytes> JoinCode = new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(""), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] private string gameplaySceneName = "Gameplay";

        private readonly Dictionary<ulong, bool> _playerReady = new Dictionary<ulong, bool>();

        private void OnEnable()
        {
            // Subscribe to connection events on server to keep counts updated
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                RecalculateCounts();
            }
        }

        private bool IsPlayableClient(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;
            // Exclude server host pseudo-client in batch mode; include all others
            if (Application.isBatchMode && nm.IsHost && clientId == NetworkManager.ServerClientId)
            {
                return false;
            }
            return true;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            if (IsPlayableClient(clientId))
            {
                if (!_playerReady.ContainsKey(clientId)) _playerReady[clientId] = false;
                RecalculateCounts();
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            if (_playerReady.Remove(clientId))
            {
                RecalculateCounts();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool ready, ServerRpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            if (!IsPlayableClient(clientId)) return;

            _playerReady[clientId] = ready;
            RecalculateCounts();
        }

        private void RecalculateCounts()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            int total = 0;
            int ready = 0;

            // Ensure dictionary contains entries for currently connected clients
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (!IsPlayableClient(id)) continue;
                total++;
                if (!_playerReady.ContainsKey(id)) _playerReady[id] = false;
                if (_playerReady[id]) ready++;
            }

            TotalPlayers.Value = total;
            ReadyCount.Value = ready;

            // Auto-transition when everyone is ready
            if (total > 0 && ready == total)
            {
                TryStartGameplay();
            }
        }

        private void TryStartGameplay()
        {
            if (!IsServer) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null) return;
            var active = SceneManager.GetActiveScene().name;
            if (!string.Equals(active, gameplaySceneName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[LobbyStateManager] All players ready: loading '{gameplaySceneName}' for all clients.");
                nm.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
            }
        }

        // Helper for server to set join code (e.g., headless ServerBootstrap)
        public void SetJoinCode(string code)
        {
            if (!IsServer) return;
            JoinCode.Value = new FixedString64Bytes(code ?? "");
        }
    }
}