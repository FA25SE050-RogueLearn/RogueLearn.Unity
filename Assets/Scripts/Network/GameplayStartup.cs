using System.Linq;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace BossFight2D.Network
{
    /// <summary>
    /// Runs on clients when the Gameplay scene loads. Ensures the local player exists,
    /// places them at a spawn point, and performs basic validation of required components.
    /// </summary>
    public class GameplayStartup : MonoBehaviour
    {
        [Tooltip("Optional explicit spawn points; if empty, default positions will be used.")]
        public Transform[] spawnPoints;

        [Tooltip("Fallback positions if no spawnPoints are provided.")]
        public Vector3[] defaultPositions = new Vector3[]
        {
            new Vector3(-3f, 0f, 0f),
            new Vector3(3f, 0f, 0f),
            new Vector3(-3f, -2f, 0f),
            new Vector3(3f, -2f, 0f)
        };

        void Start()
        {
            // Only run on connected clients
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient) return;
            StartCoroutine(InitializeRoutine());
        }

        private System.Collections.IEnumerator InitializeRoutine()
        {
            var nm = NetworkManager.Singleton;
            Debug.Log("[GameplayStartup] InitializeRoutine: begin");

            // 1) Wait for LobbyStateManager to exist and be network-spawned
            LobbyStateManager lobby = null;
            float waitLobbySeconds = 5f;
            float lobbyElapsed = 0f;
            while (lobbyElapsed < waitLobbySeconds)
            {
                lobby = FindFirstObjectByType<LobbyStateManager>();
                if (lobby != null && lobby.IsSpawned)
                {
                    Debug.Log("[GameplayStartup] LobbyStateManager found and spawned.");
                    break;
                }

                if (lobby == null)
                {
                    Debug.LogWarning("[GameplayStartup] Waiting for LobbyStateManager to appear...");
                }
                else if (!lobby.IsSpawned)
                {
                    Debug.LogWarning("[GameplayStartup] LobbyStateManager found but not spawned yet; waiting...");
                }
                lobbyElapsed += Time.deltaTime;
                yield return null;
            }

            if (lobby == null || !lobby.IsSpawned)
            {
                Debug.LogError("[GameplayStartup] Timed out waiting for LobbyStateManager. Player spawn fallback will be limited.");
                yield break;
            }

            // 2) Ensure local player NetworkObject exists on server; request spawn if missing
            var localClientId = nm.LocalClientId;
            var player = nm.SpawnManager.GetPlayerNetworkObject(localClientId);
            if (player == null)
            {
                // Fallback: if a local owned PlayerController persisted across the scene load,
                // use it to avoid duplicate spawns.
                var existingOwned = FindObjectsOfType<BossFight2D.Player.PlayerController>(false)
                    .FirstOrDefault(pc => pc.IsOwner && pc.NetworkObject != null && pc.NetworkObject.IsSpawned);

                if (existingOwned != null)
                {
                    player = existingOwned.NetworkObject;
                    Debug.Log("[GameplayStartup] Found existing owned PlayerController; will not request spawn.");
                }
                else
                {
                    Debug.Log("[GameplayStartup] Local player not found; requesting server spawn via LobbyStateManager.");
                    lobby.EnsurePlayerSpawnServerRpc();
                }
            }

            float waitPlayerSeconds = 5f;
            float playerElapsed = 0f;
            while (player == null && playerElapsed < waitPlayerSeconds)
            {
                player = nm.SpawnManager.GetPlayerNetworkObject(localClientId);
                playerElapsed += Time.deltaTime;
                yield return null;
            }

            if (player == null)
            {
                Debug.LogError("[GameplayStartup] Timed out waiting for server to spawn local player.");
                yield break;
            }

            // 3) Position the player at a spawn point
            var index = IndexOfClient(localClientId);
            var targetPos = GetSpawnPosition(index);
            Debug.Log($"[GameplayStartup] Placing local player at spawn index {index} -> {targetPos}");
            player.transform.position = targetPos;

            // 4) Validate components
            ValidatePlayerComponents(player.gameObject);

            Debug.Log("[GameplayStartup] InitializeRoutine: complete");
        }

        private int IndexOfClient(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return 0;
            var ids = nm.ConnectedClientsIds.ToList();
            ids.Sort();
            var idx = ids.IndexOf(clientId);
            return idx < 0 ? 0 : idx;
        }

        private Vector3 GetSpawnPosition(int index)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                return spawnPoints[index % spawnPoints.Length].position;
            }
            if (defaultPositions != null && defaultPositions.Length > 0)
            {
                return defaultPositions[index % defaultPositions.Length];
            }
            return Vector3.zero;
        }

        private void ValidatePlayerComponents(GameObject player)
        {
            var missing = false;
            if (player.GetComponent<Unity.Netcode.NetworkObject>() == null)
            {
                Debug.LogError("[GameplayStartup] Player is missing NetworkObject component.");
                missing = true;
            }
            if (player.GetComponent<BossFight2D.Player.PlayerController>() == null)
            {
                Debug.LogError("[GameplayStartup] Player is missing PlayerController component.");
                missing = true;
            }
            if (player.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null &&
                player.GetComponent<BossFight2D.Network.ClientNetworkTransform>() == null)
            {
                Debug.LogError("[GameplayStartup] Player is missing NetworkTransform/ClientNetworkTransform component.");
                missing = true;
            }

            if (!missing)
            {
                Debug.Log("[GameplayStartup] Player components validated successfully.");
            }
        }
    }
}