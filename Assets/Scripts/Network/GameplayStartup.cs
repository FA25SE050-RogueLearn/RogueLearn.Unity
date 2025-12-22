using System.Linq;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Cinemachine;

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

            // Headless host runs as a Host to bind Relay, which creates a local client with ServerClientId.
            // We must skip client-side startup on the headless host to avoid requesting a player spawn
            // for the server's pseudo-client (which should never have a player).
            if (Application.isBatchMode && nm.IsHost && nm.LocalClientId == NetworkManager.ServerClientId)
            {
                Debug.Log("[GameplayStartup] Skipped on headless host client.");
                return;
            }
            StartCoroutine(InitializeRoutine());
        }

        private System.Collections.IEnumerator InitializeRoutine()
        {
            var nm = NetworkManager.Singleton;
            Debug.Log("[GameplayStartup] InitializeRoutine: begin");
            if (nm != null)
            {
                var localId = nm.LocalClientId;
                var playerObj = nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
                Debug.Log($"[GameplayStartup] LocalClientId={localId}, LocalClient.PlayerObject exists={playerObj != null}");
            }

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

            // 2) Cleanup any local unspawned player clones (e.g., persisted owner object from prior scene)
            //    to avoid confusion and duplicate visuals when the real networked player spawns.
            try
            {
                var staleOwned = FindObjectsOfType<BossFight2D.Player.PlayerController>(false)
                    .Where(pc => pc.IsOwner && pc.NetworkObject != null && !pc.NetworkObject.IsSpawned)
                    .ToList();
                foreach (var pc in staleOwned)
                {
                    Debug.LogWarning("[GameplayStartup] Destroying stale local owned PlayerController that is not network-spawned.");
                    Destroy(pc.gameObject);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameplayStartup] Failed to cleanup stale local owned players: {ex.Message}");
            }

            // 3) Ensure local player NetworkObject exists on server; request spawn only if we are certain
            //    there is no owned player instance on the client and no PlayerObject mapping on the server.
            var localClientId = nm.LocalClientId;
            var player = ResolveLocalPlayerObject(nm, localClientId);
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
                    // Final guard: double-check SpawnManager mapping in case ownership was established a frame later
                    var fromSpawnMgrLate = nm.SpawnManager.GetPlayerNetworkObject(localClientId);
                    if (fromSpawnMgrLate != null && fromSpawnMgrLate.IsSpawned)
                    {
                        player = fromSpawnMgrLate;
                        Debug.Log("[GameplayStartup] Late-resolved PlayerObject via SpawnManager; skipping server spawn.");
                    }
                    else
                    {
                        Debug.Log("[GameplayStartup] Local player not found; requesting server spawn via LobbyStateManager.");
                        lobby.EnsurePlayerSpawnServerRpc();
                    }
                }
            }

            float waitPlayerSeconds = 10f;
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

            Debug.Log($"[GameplayStartup] Local player object resolved. OwnerClientId={player.OwnerClientId}, IsSpawned={player.IsSpawned}");

            // 3) Position the player at a spawn point
            var index = IndexOfClient(localClientId);
            var targetPos = GetSpawnPosition(index);
            Debug.Log($"[GameplayStartup] Placing local player at spawn index {index} -> {targetPos}");
            player.transform.position = targetPos;
            player.transform.rotation = Quaternion.identity;

            EnsureLocalOwnershipBindings(player);
            // 4) Validate components
            ValidatePlayerComponents(player.gameObject);

            // 5) If a vcam exists and isn't following, attempt to set follow here as a final fallback
            var vcam = Object.FindFirstObjectByType<Cinemachine.CinemachineVirtualCamera>();
            if (vcam != null && vcam.Follow == null)
            {
                vcam.Follow = player.transform;
                Debug.Log("[GameplayStartup] Assigned vcam follow to local player (final fallback).");
            }

            Debug.Log("[GameplayStartup] InitializeRoutine: complete");
        }
        private NetworkObject ResolveLocalPlayerObject(NetworkManager nm, ulong localClientId)
        {
            var playerObject = nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (playerObject != null && playerObject.IsSpawned)
            {
                return playerObject;
            }

            var fromSpawnManager = nm.SpawnManager.GetPlayerNetworkObject(localClientId);
            if (fromSpawnManager != null && fromSpawnManager.IsSpawned)
            {
                return fromSpawnManager;
            }

            return null;
        }

        private void EnsureLocalOwnershipBindings(NetworkObject player)
        {
            var localController = player != null ? player.GetComponent<BossFight2D.Player.PlayerController>() : null;
            if (localController == null)
            {
                Debug.LogWarning("[GameplayStartup] Local player NetworkObject has no PlayerController component.");
                return;
            }

            if (!localController.IsOwner)
            {
                var ownedFallback = FindObjectsOfType<BossFight2D.Player.PlayerController>(false)
                    .FirstOrDefault(pc => pc != localController && pc.IsOwner);
                if (ownedFallback != null && ownedFallback.NetworkObject != null)
                {
                    Debug.LogWarning("[GameplayStartup] Resolved NetworkObject is not owner. Switching to owned PlayerController instance.");
                    player = ownedFallback.NetworkObject;
                    localController = ownedFallback;
                }
            }

            // Ensure input remains enabled after scene transition
            localController.inputEnabled = true;

            // Stop any residual velocity from lobby movement
            var rb = localController.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // Force the Cinemachine camera (if present) to follow the owned player
            try
            {
                var camSetup = FindFirstObjectByType<BossFight2D.CameraSystem.MultiplayerCinemachineSetup>();
                if (camSetup != null)
                {
                    camSetup.EnsureForLocalPlayer(localController.transform);
                }
                else
                {
                    var vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
                    if (vcam != null)
                    {
                        vcam.Follow = localController.transform;
                    }
                    else
                    {
                        Debug.LogWarning("[GameplayStartup] CinemachineVirtualCamera not found when ensuring follow target.");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameplayStartup] Failed to assign Cinemachine follow target: {ex.Message}");
            }
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
