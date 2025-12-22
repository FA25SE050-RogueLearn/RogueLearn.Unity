using UnityEngine;
using Unity.Netcode;
using Cinemachine;
using UnityEngine.SceneManagement;
using System.Linq;

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

        [Header("Validation")]
        [SerializeField] private float validationInterval = 0.5f;

        private Transform localPlayerTransform;
        private float lastSearchTime;
        private bool hasFoundLocalPlayer;
        private float lastValidationTime;
        private int lastPlayerCount = -1;

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

            if (Time.time - lastValidationTime > validationInterval)
            {
                ValidateAndEnforce();
                lastValidationTime = Time.time;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback += OnClientConnected;
                nm.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            hasFoundLocalPlayer = false;
            FindAndSetLocalPlayer();
            ValidateAndEnforce();
        }

        private void OnClientConnected(ulong clientId)
        {
            hasFoundLocalPlayer = false;
            FindAndSetLocalPlayer();
            ValidateAndEnforce();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            hasFoundLocalPlayer = false;
            FindAndSetLocalPlayer();
            ValidateAndEnforce();
        }

        /// <summary>
        /// Find the local player and set as virtual camera follow target
        /// </summary>
        private void FindAndSetLocalPlayer()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

            var ownedPlayers = players
                .Select(p => new { go = p, no = p != null ? p.GetComponent<NetworkObject>() : null })
                .Where(x => x.go != null)
                .Where(x => x.no == null || x.no.IsOwner)
                .Select(x => x.go)
                .ToList();

            if (ownedPlayers.Count > 1)
            {
                Debug.LogError($"[MultiplayerCinemachine] Multiple owned players detected ({ownedPlayers.Count}). Camera will follow the first one.");
            }

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

            EnsureCameraRigIsValid();
            DisableRemotePlayerCameras();

            if (showDebugLogs)
                Debug.Log($"[Cinemachine] Virtual camera now following: {target.name}");
        }

        public void EnsureForLocalPlayer(Transform localPlayer)
        {
            if (localPlayer == null) return;
            SetFollowTarget(localPlayer);
            ValidateAndEnforce();
        }

        private void ValidateAndEnforce()
        {
            int playerCount = 0;
            try
            {
                playerCount = GameObject.FindGameObjectsWithTag(playerTag)?.Length ?? 0;
            }
            catch
            {
                playerCount = 0;
            }

            if (playerCount != lastPlayerCount)
            {
                lastPlayerCount = playerCount;
                if (playerCount > 1)
                {
                    Debug.Log($"[MultiplayerCinemachine] Detected {playerCount} players in scene.");
                }
            }

            EnsureCameraRigIsValid();
            DisableRemotePlayerCameras();
            DisableNonManagedVirtualCameras();
            EnsureLocalPlayerHasFollowTarget();
            EnsureOnlyOneMainCameraTaggedEnabled();

            if (virtualCamera != null && virtualCamera.enabled)
            {
                if (virtualCamera.Follow == null)
                {
                    Debug.LogError("[MultiplayerCinemachine] Active virtual camera has no Follow target.");
                }
            }
        }

        private void EnsureCameraRigIsValid()
        {
            if (virtualCamera == null) return;

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var brain = mainCam.GetComponent<CinemachineBrain>();
                if (brain == null)
                {
                    try { mainCam.gameObject.AddComponent<CinemachineBrain>(); }
                    catch { }
                }
            }

            var brains = FindObjectsOfType<CinemachineBrain>(true);
            if (brains != null && brains.Length > 1)
            {
                var mainBrain = mainCam != null ? mainCam.GetComponent<CinemachineBrain>() : null;
                foreach (var b in brains)
                {
                    if (b == null) continue;
                    if (mainBrain != null && b == mainBrain) continue;
                    if (b.enabled)
                    {
                        b.enabled = false;
                    }
                }
            }
        }

        private void EnsureLocalPlayerHasFollowTarget()
        {
            if (virtualCamera == null) return;
            if (virtualCamera.Follow != null) return;

            hasFoundLocalPlayer = false;
            FindAndSetLocalPlayer();
            if (virtualCamera.Follow == null)
            {
                Debug.LogError("[MultiplayerCinemachine] Local player has no assigned follow target.");
            }
        }

        private void DisableRemotePlayerCameras()
        {
            GameObject[] players;
            try { players = GameObject.FindGameObjectsWithTag(playerTag); }
            catch { return; }

            foreach (var playerObj in players)
            {
                if (playerObj == null) continue;
                var netObj = playerObj.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsOwner)
                {
                    DisableCameraComponents(playerObj);

                    var anyEnabledCamera = playerObj.GetComponentsInChildren<Camera>(true).Any(c => c != null && c.enabled);
                    var anyEnabledVcam = playerObj.GetComponentsInChildren<CinemachineVirtualCamera>(true).Any(v => v != null && v.enabled);
                    if (anyEnabledCamera || anyEnabledVcam)
                    {
                        Debug.LogError($"[MultiplayerCinemachine] Remote player '{playerObj.name}' still has an enabled camera component after enforcement.");
                    }
                }
            }
        }

        private static void DisableCameraComponents(GameObject root)
        {
            var cams = root.GetComponentsInChildren<Camera>(true);
            foreach (var c in cams)
            {
                if (c != null && c.enabled) c.enabled = false;
            }

            var listeners = root.GetComponentsInChildren<AudioListener>(true);
            foreach (var l in listeners)
            {
                if (l != null && l.enabled) l.enabled = false;
            }

            var vcams = root.GetComponentsInChildren<CinemachineVirtualCamera>(true);
            foreach (var v in vcams)
            {
                if (v != null && v.enabled) v.enabled = false;
            }

            var brains = root.GetComponentsInChildren<CinemachineBrain>(true);
            foreach (var b in brains)
            {
                if (b != null && b.enabled) b.enabled = false;
            }
        }

        private void DisableNonManagedVirtualCameras()
        {
            if (virtualCamera == null) return;

            var vcams = FindObjectsOfType<CinemachineVirtualCamera>(true);
            foreach (var v in vcams)
            {
                if (v == null) continue;
                if (v == virtualCamera) continue;
                if (v.enabled)
                {
                    v.enabled = false;
                }
            }

            if (!virtualCamera.enabled)
            {
                virtualCamera.enabled = true;
            }
        }

        private void EnsureOnlyOneMainCameraTaggedEnabled()
        {
            Camera[] cams;
            try { cams = FindObjectsOfType<Camera>(true); }
            catch { return; }

            var mainTaggedEnabled = cams
                .Where(c => c != null && c.enabled && c.CompareTag("MainCamera"))
                .ToList();

            if (mainTaggedEnabled.Count <= 1) return;

            var keep = Camera.main != null && Camera.main.enabled ? Camera.main : mainTaggedEnabled[0];
            foreach (var c in mainTaggedEnabled)
            {
                if (c == null) continue;
                if (c == keep) continue;
                c.enabled = false;
                var listener = c.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
                var brain = c.GetComponent<CinemachineBrain>();
                if (brain != null) brain.enabled = false;
            }

            Debug.LogError($"[MultiplayerCinemachine] Multiple enabled MainCamera-tag cameras detected ({mainTaggedEnabled.Count}). Disabled extras.");
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
            ValidateAndEnforce();
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
