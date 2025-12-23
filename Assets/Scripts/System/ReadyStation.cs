using UnityEngine;
using BossFight2D.Core;
using Unity.Netcode;
using BossFight2D.Quiz;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace BossFight2D.Systems
{
    [RequireComponent(typeof(BoxCollider2D))]
    /// <summary>
    /// World-space ready station that gates quiz progression by letting players toggle readiness.
    /// </summary>
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

        private int _lastDisplayedReadyPlayers = -1;
        private int _lastDisplayedTotalPlayers = -1;

        private void Awake()
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

        private void OnReadyStateChanged(bool previousValue, bool newValue)
        {
            UpdateVisuals(newValue);
        }

        private void UpdateVisuals(bool ready)
        {
            UpdateStationColor(ready);
            UpdateReadyCountTextIfNeeded(force: true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!TryGetOwnedPlayerNetworkObject(other, out var netObj)) return;
            playerInsideStatus[netObj.OwnerClientId] = true;
            EnterStationServerRpc();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!TryGetOwnedPlayerNetworkObject(other, out var netObj)) return;
            playerInsideStatus[netObj.OwnerClientId] = false;
            ExitStationServerRpc();
        }

        private void Update()
        {
            UpdateReadyCountTextIfNeeded(force: false);
            TryHandleManualReadyInput();
        }

        private void UpdateStationColor(bool ready)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = ready ? readyColor : idleColor;
            }
        }

        private void UpdateReadyCountTextIfNeeded(bool force)
        {
            if (readyText == null) return;

            var totalPlayers = 0;
            var readyPlayers = 0;
            if (QuizManager.Instance != null)
            {
                totalPlayers = QuizManager.Instance.TotalPlayers.Value;
                readyPlayers = QuizManager.Instance.ReadyCount.Value;
            }

            if (!force && totalPlayers == _lastDisplayedTotalPlayers && readyPlayers == _lastDisplayedReadyPlayers)
            {
                return;
            }

            _lastDisplayedTotalPlayers = totalPlayers;
            _lastDisplayedReadyPlayers = readyPlayers;
            readyText.text = $"Ready: {readyPlayers}/{totalPlayers}";
        }

        private void TryHandleManualReadyInput()
        {
            if (!manualReady) return;
            if (!TryGetLocalClientId(out var localClientId)) return;
            if (!playerInsideStatus.TryGetValue(localClientId, out var isInside) || !isInside) return;
            if (!Input.GetKeyDown(KeyCode.R)) return;
            ToggleReady(!isReady.Value);
        }

        private static bool TryGetLocalClientId(out ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.LocalClient != null)
            {
                clientId = nm.LocalClientId;
                return true;
            }

            clientId = 0UL;
            return false;
        }

        private static bool TryGetOwnedPlayerNetworkObject(Collider2D other, out NetworkObject netObj)
        {
            netObj = other != null ? other.GetComponentInParent<NetworkObject>() : null;
            if (netObj == null) return false;

            var root = netObj.transform.root != null ? netObj.transform.root.gameObject : netObj.gameObject;
            if (!(root.CompareTag("Player") || root.GetComponent<BossFight2D.Player.PlayerController>() != null)) return false;
            return netObj.IsOwner;
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
            if (!manualReady && QuizManager.Instance != null)
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
            var lobby = FindFirstObjectByType<BossFight2D.Network.LobbyStateManager>();
            if (lobby != null && lobby.IsSpawned)
            {
                lobby.SetReadyServerRpc(ready);
                return;
            }

            if (!IsServer) return;
            if (QuizManager.Instance == null) return;
            if (!TryGetLocalClientId(out var localClientId)) return;
            QuizManager.Instance.PlayerReadyChanged(localClientId, ready);
        }

        private static void TrySetLocalPlayerInputEnabled(bool enabled)
        {
            var nm = NetworkManager.Singleton;
            var localPlayerObject = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (localPlayerObject != null)
            {
                var pc = localPlayerObject.GetComponent<BossFight2D.Player.PlayerController>();
                if (pc != null)
                {
                    pc.inputEnabled = enabled;
                    return;
                }
            }

            var fallback = GameObject.FindObjectsOfType<BossFight2D.Player.PlayerController>(false)
                .FirstOrDefault(p => p != null && p.IsOwner);
            if (fallback != null)
            {
                fallback.inputEnabled = enabled;
            }
        }

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
            Debug.Log("[ReadyStation] Ejecting player due to wrong answer");
            ulong targetId = playerClientId.HasValue ? playerClientId.Value : (NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
            if (QuizManager.Instance != null)
            {
                QuizManager.Instance.PlayerReadyChanged(targetId, false);
                QuizManager.Instance.MarkPlayerEjected(targetId);
            }

            var stationCol = GetComponent<BoxCollider2D>();
            if (stationCol == null) return;
            var center = stationCol.bounds.center;
            var ext = stationCol.bounds.extents;

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

            var clientParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetId } } };
            float duration = ejectLerpDuration;
            if (BossFight2D.Core.PowerPlayManager.Instance != null)
            {
                duration = BossFight2D.Core.PowerPlayManager.Instance.SmoothMoveDuration;
            }
            SmoothEjectClientRpc(newPos, duration, clientParams);

            Debug.Log("[ReadyStation] Ejection complete");
        }

        [ClientRpc]
        private void SmoothEjectClientRpc(Vector3 targetPosition, float duration, ClientRpcParams clientRpcParams = default)
        {
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

            var ppm = BossFight2D.Core.PowerPlayManager.Instance;
            if (ppm != null)
            {
                ppm.SmoothMoveLocalPlayer(targetPosition, duration);
            }
            else
            {
                StartCoroutine(LocalLerpPlayerPosition(t, targetPosition, duration));
            }

            TrySetLocalPlayerInputEnabled(true);
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
        /// <summary>
        /// Check if a specific player (by clientId) is inside this ready station
        /// </summary>
        /// <param name="clientId">Target client id.</param>
        /// <returns>True if the player is currently inside the station on the server.</returns>
        public bool IsPlayerInside(ulong clientId)
        {
            return insidePlayers.Contains(clientId);
        }

        /// <summary>
        /// Eject a specific player (by clientId) from this ready station
        /// Called by QuizManager after resolution to synchronize ejection
        /// </summary>
        /// <param name="clientId">Target client id.</param>
        public void EjectPlayer(ulong clientId)
        {
            if (!IsServer) return;
            DoEjectServer(clientId);
        }
    }
}
