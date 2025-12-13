using UnityEngine;
using Unity.Netcode;
using Cinemachine;

namespace BossFight2D.CameraSystem
{
    /// <summary>
    /// Automatically sets Cinemachine virtual camera to follow the local player in multiplayer.
    /// Attach to the Virtual Camera GameObject.
    ///
    /// MULTIPLAYER READY:
    /// - Each client's virtual camera follows only their owned player
    /// - Automatically finds local player via NetworkObject.IsOwner
    /// - Supports cinematics by temporarily changing follow target
    /// </summary>
    public class MultiplayerCinemachineSetup : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Tag used to find player objects")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("How often to search for local player (seconds)")]
        [SerializeField] private float searchInterval = 0.5f;

        [Tooltip("Virtual camera component (auto-finds if empty)")]
        [SerializeField] private CinemachineVirtualCamera virtualCamera;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        private Transform localPlayerTransform;
        private float lastSearchTime;
        private bool hasFoundLocalPlayer;

        void Awake()
        {
            if (virtualCamera == null)
            {
                virtualCamera = GetComponent<CinemachineVirtualCamera>();
            }

            if (virtualCamera == null)
            {
                Debug.LogError("[MultiplayerCinemachine] No CinemachineVirtualCamera found!");
            }
        }

        void Start()
        {
            // Try to find local player immediately
            FindAndSetLocalPlayer();
        }

        void Update()
        {
            // Periodically search for local player if not found yet
            if (!hasFoundLocalPlayer)
            {
                if (Time.time - lastSearchTime > searchInterval)
                {
                    FindAndSetLocalPlayer();
                    lastSearchTime = Time.time;
                }
            }
        }

        /// <summary>
        /// Find the local player and set as virtual camera follow target
        /// </summary>
        private void FindAndSetLocalPlayer()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

            foreach (GameObject playerObj in players)
            {
                NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

                // If no NetworkObject, assume single-player
                if (netObj == null)
                {
                    SetFollowTarget(playerObj.transform);
                    if (showDebugLogs)
                        Debug.Log($"[Cinemachine] Found local player (non-networked): {playerObj.name}");
                    return;
                }

                // In networked game, only follow player owned by this client
                if (netObj.IsOwner)
                {
                    SetFollowTarget(playerObj.transform);
                    if (showDebugLogs)
                        Debug.Log($"[Cinemachine] Found local player (networked): {playerObj.name}, ClientId: {netObj.OwnerClientId}");
                    return;
                }
            }

            // Player not found yet
            if (showDebugLogs && Time.frameCount % 300 == 0)
            {
                Debug.Log($"[Cinemachine] Waiting for local player... (searched {players.Length} players)");
            }
        }

        /// <summary>
        /// Set the virtual camera follow target
        /// </summary>
        private void SetFollowTarget(Transform target)
        {
            if (virtualCamera == null) return;

            virtualCamera.Follow = target;
            localPlayerTransform = target;
            hasFoundLocalPlayer = true;

            if (showDebugLogs)
                Debug.Log($"[Cinemachine] Virtual camera now following: {target.name}");
        }

        #region Public Methods

        /// <summary>
        /// Manually set follow target (for cinematics)
        /// </summary>
        public void SetManualTarget(Transform target)
        {
            if (virtualCamera != null)
            {
                virtualCamera.Follow = target;

                if (showDebugLogs)
                    Debug.Log($"[Cinemachine] Manual target set: {target.name}");
            }
        }

        /// <summary>
        /// Return to following local player
        /// </summary>
        public void ReturnToLocalPlayer()
        {
            if (localPlayerTransform != null && virtualCamera != null)
            {
                virtualCamera.Follow = localPlayerTransform;

                if (showDebugLogs)
                    Debug.Log("[Cinemachine] Returned to following local player");
            }
            else
            {
                // Try to find player again
                FindAndSetLocalPlayer();
            }
        }

        /// <summary>
        /// Force immediate search for local player
        /// </summary>
        public void RefreshLocalPlayer()
        {
            hasFoundLocalPlayer = false;
            FindAndSetLocalPlayer();
        }

        /// <summary>
        /// Get current follow target
        /// </summary>
        public Transform GetFollowTarget()
        {
            return virtualCamera != null ? virtualCamera.Follow : null;
        }

        #endregion

#if UNITY_EDITOR
        [ContextMenu("Find Local Player Now")]
        private void DebugFindPlayer()
        {
            FindAndSetLocalPlayer();
            Debug.Log($"Search complete. Found: {hasFoundLocalPlayer}");
        }

        [ContextMenu("Log Current Target")]
        private void DebugLogTarget()
        {
            if (virtualCamera != null && virtualCamera.Follow != null)
            {
                Debug.Log($"Currently following: {virtualCamera.Follow.name}");
            }
            else
            {
                Debug.Log("No follow target set");
            }
        }
#endif
    }
}
