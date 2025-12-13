using UnityEngine;
using BossFight2D.Core;
using Unity.Netcode;
using BossFight2D.Quiz;
using TMPro;
using System.Collections.Generic;

namespace BossFight2D.Systems
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class ReadyStation : NetworkBehaviour
    {
        [Header("Prompt Settings")]
        [SerializeField] private string interactPrompt = "Press R to READY at Station";
        [SerializeField] private string readyPrompt = "Press R to UNREADY";
        [SerializeField] private Color readyColor = new Color(0.3f, 0.9f, 0.3f);
        [SerializeField] private Color idleColor = new Color(0.9f, 0.9f, 0.3f);
        [Header("UI Display")]
        [SerializeField] private bool showReadyCountText = true;
        [SerializeField] private Vector3 readyTextOffset = new Vector3(0, 0.25f, 0);
        [SerializeField] private Color readyTextColor = Color.white;

        private NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        [Header("Manual Ready")]
        [SerializeField] private bool manualReady = true;

        // Track which players are inside (by clientId)
        private readonly Dictionary<ulong, bool> playerInsideStatus = new Dictionary<ulong, bool>();
        private SpriteRenderer spriteRenderer;
        private TextMeshPro readyText;
        // Server-side tracking of which players are physically inside this station
        private readonly HashSet<ulong> insidePlayers = new HashSet<ulong>();

        [Header("Ejection Transition")]
        [Tooltip("Duration of the smooth ejection move (reuses PowerPlay smoothing style).")]
        [SerializeField] private float ejectLerpDuration = 0.75f;

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CreateRectSprite();
            }
            GetComponent<BoxCollider2D>().isTrigger = true;

            if (showReadyCountText)
            {
                // Create a simple world-space TextMeshPro to show Ready count above the station
                var go = new GameObject("ReadyCountText");
                go.transform.SetParent(transform);
                go.transform.localPosition = readyTextOffset;
                readyText = go.AddComponent<TextMeshPro>();
                readyText.alignment = TextAlignmentOptions.Center;
                readyText.fontSize = 10f;
                readyText.color = readyTextColor;
                readyText.text = "Ready: 0/0";
            }
        }

        public override void OnNetworkSpawn()
        {
            isReady.OnValueChanged += OnReadyStateChanged;
            UpdateVisuals(isReady.Value);

            if (IsServer && QuizManager.Instance != null)
            {
                QuizManager.Instance.ReadyCount.OnValueChanged += OnReadyCountChanged;
                QuizManager.Instance.TotalPlayers.OnValueChanged += OnTotalPlayersChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            isReady.OnValueChanged -= OnReadyStateChanged;
            if (IsServer && QuizManager.Instance != null)
            {
                QuizManager.Instance.ReadyCount.OnValueChanged -= OnReadyCountChanged;
                QuizManager.Instance.TotalPlayers.OnValueChanged -= OnTotalPlayersChanged;
            }
        }

        void OnEnable()
        {
            BossFight2D.Systems.EventBus.AnswerSubmitted += OnAnswerSubmitted;
        }

        void OnDisable()
        {
            BossFight2D.Systems.EventBus.AnswerSubmitted -= OnAnswerSubmitted;
        }

        private void OnReadyStateChanged(bool previousValue, bool newValue)
        {
            UpdateVisuals(newValue);
        }

        private void UpdateVisuals(bool ready)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = ready ? readyColor : idleColor;
            }
            if (readyText != null)
            {
                int totalPlayers = 0;
                int readyPlayers = 0;
                if (QuizManager.Instance != null)
                {
                    totalPlayers = QuizManager.Instance.TotalPlayers.Value;
                    readyPlayers = QuizManager.Instance.ReadyCount.Value;
                }
                readyText.text = $"Ready: {readyPlayers}/{totalPlayers}";
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // Accept any collider belonging to a Player object (root tagged "Player" or has PlayerController)
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null)
            {
                return;
            }

            // Resolve the root GameObject so we don't require the child collider to be tagged "Player"
            var rootGO = netObj.transform.root != null ? netObj.transform.root.gameObject : netObj.gameObject;
            bool isPlayerRoot = (rootGO.CompareTag("Player") || rootGO.GetComponent<BossFight2D.Player.PlayerController>() != null);
            if (!isPlayerRoot)
            {
                return;
            }

            // Only the local owner should toggle readiness when entering their safe zone
            if (!netObj.IsOwner)
            {
                return;
            }

            // Track this player as inside
            playerInsideStatus[netObj.OwnerClientId] = true;
            // Manual ready: no auto-toggle on enter. Show prompt and wait for keypress.
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null)
            {
                return;
            }

            var rootGO = netObj.transform.root != null ? netObj.transform.root.gameObject : netObj.gameObject;
            bool isPlayerRoot = (rootGO.CompareTag("Player") || rootGO.GetComponent<BossFight2D.Player.PlayerController>() != null);
            if (!isPlayerRoot)
            {
                return;
            }

            if (!netObj.IsOwner)
            {
                return;
            }

            // Mark this player as no longer inside
            playerInsideStatus[netObj.OwnerClientId] = false;

            // If leaving while ready, auto-unready and re-enable movement locally
            if (isReady.Value)
            {
                ToggleReady(false);
            }
        }

        void Update()
        {
            UpdateVisuals(isReady.Value);
            if (manualReady)
            {
                // Check if the local player is inside
                var localClientId = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0UL;

                if (playerInsideStatus.TryGetValue(localClientId, out bool isInside) && isInside)
                {
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        ToggleReady(!isReady.Value);
                    }
                }
            }
        }

        // Server-only: recompute whether the station should be auto-ready
        private void RefreshAutoReadyServer()
        {
            if (!IsServer) return;
            int totalPlayers = 0;
            int readyPlayers = 0;
            if (QuizManager.Instance != null)
            {
                totalPlayers = QuizManager.Instance.TotalPlayers.Value;
                readyPlayers = QuizManager.Instance.ReadyCount.Value;
            }

            // Simplify readiness: turn station 'ready' when all connected playable clients report Ready.
            // We no longer require the server-side insidePlayers physical count because in-scene objects
            // may not receive enter/exit RPCs when NGO scene sync is disabled (e.g., WebGL client path).
            bool shouldBeReady = totalPlayers > 0 && readyPlayers == totalPlayers;
            if (isReady.Value != shouldBeReady)
            {
                isReady.Value = shouldBeReady;
                UpdateVisuals(isReady.Value);
            }
        }

        private void OnReadyCountChanged(int previous, int current)
        {
            RefreshAutoReadyServer();
        }

        private void OnTotalPlayersChanged(int previous, int current)
        {
            RefreshAutoReadyServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void EnterStationServerRpc(ServerRpcParams rpcParams = default)
        {
            insidePlayers.Add(rpcParams.Receive.SenderClientId);
            // Auto-ready the player when they enter the station
            if (QuizManager.Instance != null)
            {
                QuizManager.Instance.PlayerReadyChanged(rpcParams.Receive.SenderClientId, true);
            }
            RefreshAutoReadyServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExitStationServerRpc(ServerRpcParams rpcParams = default)
        {
            insidePlayers.Remove(rpcParams.Receive.SenderClientId);
            // Auto-unready the player when they leave the station
            if (QuizManager.Instance != null)
            {
                QuizManager.Instance.PlayerReadyChanged(rpcParams.Receive.SenderClientId, false);
            }
            RefreshAutoReadyServer();
        }

        private void OnAnswerSubmitted(int _, bool correct)
        {
            // MVP FIX: EJECTION MOVED TO AFTER RESOLUTION FOR SYNCHRONIZATION
            //
            // OLD FLOW (CAUSES DESYNC):
            // - Player 1 answers wrong → immediately ejected → panel hides
            // - Player 2 still answering → panel still showing
            // - Resolution happens → Player 1 and 2 see different things
            //
            // NEW FLOW (SYNCHRONIZED):
            // - All players answer (or timeout)
            // - Show resolution feedback to ALL players
            // - THEN eject wrong-answer players
            // - Everyone stays in sync!
            //
            // Ejection now handled in QuizManager.ResolveAnswers() after resolution
        }

        private void ToggleReady(bool ready)
        {
            var playerGO = GameObject.FindWithTag("Player");
            var pc = playerGO != null ? playerGO.GetComponent<BossFight2D.Player.PlayerController>() : null;
            if (pc != null && pc.IsOwner)
            {
                pc.inputEnabled = !ready;
            }
            if (IsServer)
            {
                if (QuizManager.Instance != null)
                {
                    var id = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null ? NetworkManager.Singleton.LocalClientId : 0;
                    QuizManager.Instance.PlayerReadyChanged(id, ready);
                }
                if (isReady.Value != ready)
                {
                    isReady.Value = ready;
                }
                UpdateVisuals(isReady.Value);
            }
            else
            {
                var lobby = FindFirstObjectByType<BossFight2D.Network.LobbyStateManager>();
                if (lobby != null && lobby.IsSpawned)
                {
                    lobby.SetReadyServerRpc(ready);
                }
                UpdateVisuals(ready);
            }
        }

        // When NGO scene management is disabled, this in-scene NetworkBehaviour might not be spawned on the server.
        // To avoid RPC errors, perform local ejection on the client and notify the server via LobbyStateManager to clear ready.
        private void EjectPlayerClientSide()
        {
            var stationCol = GetComponent<BoxCollider2D>();
            if (stationCol == null) return;
            var center = stationCol.bounds.center;
            var ext = stationCol.bounds.extents;

            Vector3 newPos = center + Vector3.up;
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                var t = playerGO.transform;
                var root = t.parent != null ? t.parent : t;
                var pos = root.position;
                Vector2 dir = (Vector2)(pos - center);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                dir.Normalize();
                float margin = 2.5f;
                float pushDist = Mathf.Max(ext.x, ext.y) + margin;
                newPos = center + (Vector3)(dir * pushDist);
            }

            var ppm = BossFight2D.Core.PowerPlayManager.Instance;
            float duration = ejectLerpDuration;
            if (ppm != null)
            {
                duration = ppm.SmoothMoveDuration;
                ppm.SmoothMoveLocalPlayer(newPos, duration);
            }
            else if (playerGO != null)
            {
                StartCoroutine(LocalLerpPlayerPosition(playerGO.transform, newPos, duration));
            }

            // Notify server to clear ready for this client
            var lobby = FindFirstObjectByType<BossFight2D.Network.LobbyStateManager>();
            if (lobby != null && lobby.IsSpawned)
            {
                lobby.SetReadyServerRpc(false);
            }

            // Clear the local player's inside status
            var localClientId = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClientId
                : 0UL;
            playerInsideStatus[localClientId] = false;

            // MVP FIX: Hide quiz panel when ejected so player can dodge attack
            var questionPanel = BossFight2D.UI.QuestionPanelController.Instance;
            if (questionPanel != null)
            {
                questionPanel.HidePanel();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void EjectPlayerServerRpc(ServerRpcParams rpcParams = default)
        {
            DoEjectServer(rpcParams.Receive.SenderClientId);
        }

        private void DoEjectServer(ulong? playerClientId = null)
        {
            Debug.Log("ReadyStation: Ejecting player due to wrong answer");
            // Turn off ready state server-side for the answering player
            ulong targetId = playerClientId.HasValue ? playerClientId.Value : (NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
            if (QuizManager.Instance != null)
            {
                QuizManager.Instance.PlayerReadyChanged(targetId, false);
                // MVP: Mark player as ejected - they must re-ready to rejoin quiz
                QuizManager.Instance.MarkPlayerEjected(targetId);
            }

            var stationCol = GetComponent<BoxCollider2D>();
            if (stationCol == null) return;
            var center = stationCol.bounds.center;
            var ext = stationCol.bounds.extents;

            // Compute ejection position just outside the station bounds
            Vector3 newPos = center + Vector3.up; // fallback
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null)
                {
                    var t = playerGO.transform;
                    var root = t.parent != null ? t.parent : t;
                    var pos = root.position;
                    Vector2 dir = (Vector2)(pos - center);
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up; // default direction if centered
                    dir.Normalize();
                    float margin = 2.5f;
                    float pushDist = Mathf.Max(ext.x, ext.y) + margin;
                    newPos = center + (Vector3)(dir * pushDist);
                }
            }

            // Update server-side inside tracking immediately to reflect ejection, independent of trigger exit timing
            insidePlayers.Remove(targetId);

            // Use client-side smooth movement to match established visual style (PowerPlay return)
            var clientParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetId } } };
            float duration = ejectLerpDuration;
            if (BossFight2D.Core.PowerPlayManager.Instance != null)
            {
                // If available, reuse the same duration configured for Power Play smoothing
                duration = BossFight2D.Core.PowerPlayManager.Instance.SmoothMoveDuration;
            }
            SmoothEjectClientRpc(newPos, duration, clientParams);

            Debug.Log("ReadyStation: Ejection complete");
        }

        [ClientRpc]
        private void SmoothEjectClientRpc(Vector3 targetPosition, float duration, ClientRpcParams clientRpcParams = default)
        {
            // Move only the local player's object smoothly to the target position
            var localPlayerObj = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
            Transform t = null;
            if (localPlayerObj != null)
            {
                t = localPlayerObj.transform;
            }
            else
            {
                var goFallback = GameObject.FindWithTag("Player");
                if (goFallback == null) return;
                t = goFallback.transform;
            }
            if (t == null) return;

            // Prefer reusing PowerPlayManager's smoothing coroutine for consistent visuals
            var ppm = BossFight2D.Core.PowerPlayManager.Instance;
            if (ppm != null)
            {
                ppm.SmoothMoveLocalPlayer(targetPosition, duration);
            }
            else
            {
                // Fallback local lerp if PowerPlayManager is unavailable
                StartCoroutine(LocalLerpPlayerPosition(t, targetPosition, duration));
            }
        }

        private System.Collections.IEnumerator LocalLerpPlayerPosition(Transform playerTransform, Vector3 target, float duration)
        {
            Vector3 start = playerTransform.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                playerTransform.position = Vector3.Lerp(start, target, alpha);
                yield return null;
            }
            playerTransform.position = target;
        }

        // Allow any client (not just the owner of this in-scene NetworkObject) to toggle readiness
        // Manual toggle removed in favor of auto-ready on station enter/exit

        private Sprite CreateRectSprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var c = new Color(1f, 1f, 1f, 0.5f);
            var pixels = new Color[8 * 8];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 16f);
        }

        // void OnEnable() { BossFight2D.Systems.EventBus.AnswerSubmitted += OnAnswerSubmitted; }
        // void OnDisable() { BossFight2D.Systems.EventBus.AnswerSubmitted -= OnAnswerSubmitted; BossFight2D.Systems.EventBus.AnswerModeExited -= OnAnswerModeExited; }
        // void Start() { BossFight2D.Systems.EventBus.AnswerModeExited += OnAnswerModeExited; }
        // void OnAnswerSubmitted(int _, bool correct)
        // {
        //     Debug.Log($"ReadyStation: OnAnswerSubmitted called - correct: {correct}, playerInside: {_playerInside}, ready: {_ready}");
        //     if (!correct && _playerInside && _ready)
        //     {
        //         Debug.Log("ReadyStation: Ejecting player due to wrong answer");
        //         EjectAndCancel();
        //     }
        // }

        // void EjectAndCancel()
        // {
        //     Debug.Log("ReadyStation: EjectAndCancel called");
        //     // Cancel ready state
        //     _ready = false; _sr.color = idleColor;
        //     // Compute an ejection position just outside the station bounds, away from center
        //     var stationCol = GetComponent<BoxCollider2D>();
        //     if (stationCol == null) return;
        //     var center = stationCol.bounds.center;
        //     var ext = stationCol.bounds.extents;
        //     if (player)
        //     {
        //         var pos = player.transform.position; Vector2 dir = (Vector2)(pos - center);
        //         if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up; dir.Normalize();
        //         float margin = 5f;
        //         float pushDist = Mathf.Max(ext.x, ext.y) + margin;
        //         Vector3 newPos = center + (Vector3)(dir * pushDist);
        //         Debug.Log($"ReadyStation: Moving player from {pos} to {newPos}");
        //         player.parent.position = newPos;
        //     }

        //     // Update internal flags and safe zone
        //     _playerInside = false;
        //     UpdateSafeZone();
        //     ShowPrompt(false);
        //     Debug.Log("ReadyStation: Ejection complete");
        // }

        // // Public method to turn off ready state (used when the question phase is canceled)
        // public void TurnOffReady()
        // {
        //     if (_ready)
        //     {
        //         _ready = false;
        //         _sr.color = idleColor;
        //         UpdateSafeZone();
        //         Debug.Log("ReadyStation: TurnOffReady invoked; safe zone deactivated");
        //     }
        // }

        // void OnAnswerModeExited()
        // {
        //     // Whenever answer mode exits (timeout, submit, or manual cancel), ensure ready is off
        //     TurnOffReady();
        // }

        // // Force return player inside station and re-enable ready state (used after Power Play ends)
        // public void ForceReturnPlayerAndReady()
        // {
        //     var col = GetComponent<BoxCollider2D>(); if (col == null) return;
        //     Vector3 center = col.bounds.center;
        //     if (player == null)
        //     {
        //         var go = GameObject.FindWithTag("Player");
        //         if (go != null) player = go.transform;
        //     }
        //     if (player != null)
        //     {
        //         var root = player.parent != null ? player.parent : player;
        //         root.position = center;
        //     }
        //     _playerInside = true;
        //     _playerColliderCount = Mathf.Max(_playerColliderCount, 1);
        //     _ready = true;
        //     _sr.color = readyColor;
        //     UpdateSafeZone();
        //     ShowPrompt(true);
        //     Debug.Log("ReadyStation: ForceReturnPlayerAndReady executed; player placed inside and ready ON");
        // }

        // MVP: Public methods for QuizManager to check/eject players
        /// <summary>
        /// Check if a specific player (by clientId) is inside this ready station
        /// </summary>
        public bool IsPlayerInside(ulong clientId)
        {
            return insidePlayers.Contains(clientId);
        }

        /// <summary>
        /// Eject a specific player (by clientId) from this ready station
        /// Called by QuizManager after resolution to synchronize ejection
        /// </summary>
        public void EjectPlayer(ulong clientId)
        {
            if (!IsServer) return;
            DoEjectServer(clientId);
        }
    }
}