using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace BossFight2D.Network
{
    /// <summary>
    /// Manages player identity by mapping Unity's network clientId to Supabase userId.
    /// Clients send their userId to the server when they connect.
    /// Server maintains the mapping for use in match results.
    ///
    /// SETUP INSTRUCTIONS:
    /// 1. Create an empty GameObject in your NetworkManager scene (e.g., "PlayerIdentityManager")
    /// 2. Add this PlayerIdentity script to it
    /// 3. Add a NetworkObject component to the same GameObject
    /// 4. Make sure the NetworkObject is set to spawn with the scene
    /// </summary>
    public class PlayerIdentity : NetworkBehaviour
    {
        // Server-side mapping of clientId -> userId
        private static Dictionary<ulong, string> _clientUserIds = new Dictionary<ulong, string>();
        private static PlayerIdentity _instance;

        public static event Action<ulong, string> UserIdRegistered;

        public static PlayerIdentity Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log($"[PlayerIdentity] OnNetworkSpawn - IsClient:{IsClient}, IsServer:{IsServer}, IsHost:{IsHost}, LocalClientId:{NetworkManager.LocalClientId}");

            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            if (IsClient && !IsServer)
            {
                // Client: Get userId from GameSessionClient and send to server
                // Wait a moment for GameSessionClient to be configured by the frontend
                StartCoroutine(SendUserIdWhenReady());
            }
            else if (IsHost)
            {
                // Host: Register own userId immediately from environment variable
                string hostUserId = System.Environment.GetEnvironmentVariable("USER_ID");
                if (!string.IsNullOrEmpty(hostUserId))
                {
                    ulong hostClientId = NetworkManager.LocalClientId;
                    _clientUserIds[hostClientId] = hostUserId;
                    Debug.Log($"[PlayerIdentity] Host registered own userId '{hostUserId}' for clientId {hostClientId}");
                }
            }
        }

        /// <summary>
        /// Wait for GameSessionClient to have a userId, then send it to the server
        /// </summary>
        private System.Collections.IEnumerator SendUserIdWhenReady()
        {
            // Wait up to 10 seconds for userId to be configured (increased timeout)
            float timeout = 10f;
            float elapsed = 0f;
            int attemptCount = 0;

            Debug.Log($"[PlayerIdentity] Starting SendUserIdWhenReady for clientId {NetworkManager.LocalClientId}");

            while (elapsed < timeout)
            {
                attemptCount++;
                var gameSessionClient = BossFight2D.Systems.GameSessionClient.Instance;

                if (gameSessionClient != null)
                {
                    string userId = gameSessionClient.GetUserId();
                    if (!string.IsNullOrEmpty(userId))
                    {
                        Debug.Log($"[PlayerIdentity] Client (clientId {NetworkManager.LocalClientId}) sending userId: {userId} (attempt {attemptCount})");
                        RegisterUserIdServerRpc(userId);
                        yield break;
                    }
                    else
                    {
                        Debug.Log($"[PlayerIdentity] GameSessionClient found but userId is empty (attempt {attemptCount}/{timeout / 0.5f})");
                    }
                }
                else
                {
                    Debug.Log($"[PlayerIdentity] GameSessionClient.Instance is null (attempt {attemptCount}/{timeout / 0.5f})");
                }

                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            Debug.LogError($"[PlayerIdentity] Client (clientId {NetworkManager.LocalClientId}) failed to get userId after {timeout}s timeout. Match results will not be saved for this player!");
        }

        [ServerRpc(RequireOwnership = false)]
        private void RegisterUserIdServerRpc(string userId, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!string.IsNullOrEmpty(userId))
            {
                _clientUserIds[clientId] = userId;
                Debug.Log($"[PlayerIdentity] Server registered userId '{userId}' for clientId {clientId}. Total registered: {_clientUserIds.Count}");
                UserIdRegistered?.Invoke(clientId, userId);
            }
            else
            {
                Debug.LogWarning($"[PlayerIdentity] Server received empty userId from clientId {clientId}");
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            if (_clientUserIds.Remove(clientId))
            {
                Debug.Log($"[PlayerIdentity] Removed userId mapping for disconnected clientId {clientId}");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        /// <summary>
        /// Server: Get the userId for a given clientId
        /// </summary>
        public static string GetUserIdForClient(ulong clientId)
        {
            Debug.Log($"[PlayerIdentity] GetUserIdForClient({clientId}) - Registered clients: {string.Join(", ", _clientUserIds.Keys)}");

            if (_clientUserIds.TryGetValue(clientId, out string userId))
            {
                Debug.Log($"[PlayerIdentity] Found userId '{userId}' for clientId {clientId}");
                return userId;
            }

            // Fallback to environment variable if this is the host
            if (clientId == 0 || (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId))
            {
                string hostUserId = System.Environment.GetEnvironmentVariable("USER_ID");
                if (!string.IsNullOrEmpty(hostUserId))
                {
                    Debug.Log($"[PlayerIdentity] Using fallback USER_ID env var '{hostUserId}' for clientId {clientId}");
                    return hostUserId;
                }
            }

            Debug.LogWarning($"[PlayerIdentity] No userId found for clientId {clientId}. This player's match results will not be saved!");
            return null;
        }

        /// <summary>
        /// Server: Get all registered client-userId pairs
        /// </summary>
        public static Dictionary<ulong, string> GetAllClientUserIds()
        {
            return new Dictionary<ulong, string>(_clientUserIds);
        }

        /// <summary>
        /// Server: Clear all mappings (called when match ends)
        /// </summary>
        public static void ClearMappings()
        {
            Debug.Log($"[PlayerIdentity] Clearing all {_clientUserIds.Count} userId mappings");
            _clientUserIds.Clear();
        }

        /// <summary>
        /// Server: Manually register a userId for a clientId (useful for debugging or fallback)
        /// </summary>
        public static void RegisterUserId(ulong clientId, string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                _clientUserIds[clientId] = userId;
                Debug.Log($"[PlayerIdentity] Manually registered userId '{userId}' for clientId {clientId}");
            }
        }

    }
}
