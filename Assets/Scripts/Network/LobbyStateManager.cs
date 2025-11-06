using System;
using System.Collections.Generic;
using System.Linq;
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
        // When true, the gameplay scene has been launched; readiness toggles should no longer trigger scene loads
        private bool _gameSessionActive = false;

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
            // Ensure the lobby manager persists across local scene loads on clients and server
            // so readiness and player-spawn RPCs remain available during transitions.
            try { DontDestroyOnLoad(gameObject); } catch {}
            if (IsServer)
            {
                RecalculateCounts();
            }
            Debug.Log($"[LobbyStateManager] OnNetworkSpawn: role={(IsServer ? "Server" : "Client")}, IsSpawned={IsSpawned}, scene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");
            if (IsServer)
            {
                Debug.Log($"[LobbyStateManager] Current counts -> Total={TotalPlayers.Value}, Ready={ReadyCount.Value}");
            }
        }

        public override void OnNetworkDespawn()
        {
            Debug.Log($"[LobbyStateManager] OnNetworkDespawn: role={(IsServer ? "Server" : "Client")}, scene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");
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

            // Forward readiness to QuizManager if available (during gameplay)
            var quiz = BossFight2D.Quiz.QuizManager.Instance;
            if (quiz != null)
            {
                quiz.PlayerReadyChanged(clientId, ready);
            }
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

            // Auto-transition when everyone is ready and the game hasn't started yet
            if (!_gameSessionActive && total > 0 && ready == total)
            {
                TryStartGameplay();
            }
        }

        private void TryStartGameplay()
        {
            if (!IsServer) return;
            if (_gameSessionActive) return;
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            // With NGO scene management disabled, instruct clients to load the gameplay scene via ClientRpc
            // while the server can load it locally (optional) for headless physics/logic.
            Debug.Log($"[LobbyStateManager] All players ready: instructing clients to load '{gameplaySceneName}'.");
            LoadGameplayClientRpc(gameplaySceneName);
            _gameSessionActive = true;
        }

        /// <summary>
        /// Ensures the requesting client has a player object on the server. If not, spawns one using
        /// the NetworkManager's PlayerPrefab.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void EnsurePlayerSpawnServerRpc(ServerRpcParams rpcParams = default)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            var clientId = rpcParams.Receive.SenderClientId;

            var existing = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (existing != null)
            {
                Debug.Log($"[LobbyStateManager] Player object already exists for client {clientId}.");
                return;
            }

            // If there's an owned PlayerController that isn't registered as the player's object,
            // despawn it to avoid duplicates and replace with an official PlayerObject.
            try
            {
                var ownedOther = nm.SpawnManager.SpawnedObjectsList
                    .FirstOrDefault(o => o != null && o.OwnerClientId == clientId && o.GetComponent<BossFight2D.Player.PlayerController>() != null);
                if (ownedOther != null)
                {
                    Debug.LogWarning($"[LobbyStateManager] Found owned PlayerController for client {clientId} without PlayerObject mapping; replacing it.");
                    // Despawn and destroy the old instance to prevent duplicate player GOs on clients
                    ownedOther.Despawn(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyStateManager] Failed to check or despawn pre-existing owned player instance: {ex.Message}");
            }

            var playerPrefab = nm.NetworkConfig?.PlayerPrefab;
            if (playerPrefab == null)
            {
                Debug.LogError("[LobbyStateManager] PlayerPrefab is not configured on NetworkManager. Cannot spawn player.");
                return;
            }

            var go = Instantiate(playerPrefab);
            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                no = go.AddComponent<NetworkObject>();
            }
            try
            {
                // In newer NGO versions, a destroyWithScene overload exists; use default for compatibility.
                no.SpawnAsPlayerObject(clientId);
                Debug.Log($"[LobbyStateManager] Spawned player object for client {clientId}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyStateManager] Failed to spawn player for client {clientId}: {e.Message}");
            }
        }

        [ClientRpc]
        private void LoadGameplayClientRpc(string sceneName)
        {
            try
            {
                if (!string.IsNullOrEmpty(sceneName))
                {
                    // Clients stay in current UI during connection; when all are ready we transition them locally.
                    SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyStateManager] Client local load of '{sceneName}' failed: {e.Message}");
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