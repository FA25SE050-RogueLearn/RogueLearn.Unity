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

        private bool isPlayerInside;
        private SpriteRenderer spriteRenderer;
        private TextMeshPro readyText;
        // Server-side tracking of which players are physically inside this station
        private readonly HashSet<ulong> insidePlayers = new HashSet<ulong>();

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

            // On the server, react to changes in ready counts/total players to auto-toggle station ready state
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
            if (!other.CompareTag("Player")) return;
            // Use parent lookup so we correctly detect ownership when child colliders (Hitbox/Hurtbox) enter
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                isPlayerInside = true;
                if (IsServer)
                {
                    insidePlayers.Add(netObj.OwnerClientId);
                    RefreshAutoReadyServer();
                }
                else
                {
                    EnterStationServerRpc();
                }
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                isPlayerInside = false;
                if (IsServer)
                {
                    insidePlayers.Remove(netObj.OwnerClientId);
                    RefreshAutoReadyServer();
                }
                else
                {
                    ExitStationServerRpc();
                }
            }
        }

        void Update()
        {
            // Only clients should send the RPC based on local input
            if (!IsClient) return;
            if (isPlayerInside && Input.GetKeyDown(KeyCode.R))
            {
                ToggleReadyServerRpc();
            }

            // Update station visuals/text each frame for clients based on replicated values
            UpdateVisuals(isReady.Value);
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

            bool shouldBeReady = totalPlayers > 0 && readyPlayers == totalPlayers && insidePlayers.Count == totalPlayers;
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
            RefreshAutoReadyServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExitStationServerRpc(ServerRpcParams rpcParams = default)
        {
            insidePlayers.Remove(rpcParams.Receive.SenderClientId);
            RefreshAutoReadyServer();
        }

        private void OnAnswerSubmitted(int _, bool correct)
        {
            // If the player answered wrong while inside and ready, eject them from the station
            if (!correct && isPlayerInside && isReady.Value)
            {
                if (IsServer)
                {
                    DoEjectServer();
                }
                else
                {
                    EjectPlayerServerRpc();
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void EjectPlayerServerRpc(ServerRpcParams rpcParams = default)
        {
            DoEjectServer();
        }

        private void DoEjectServer()
        {
            Debug.Log("ReadyStation: Ejecting player due to wrong answer");
            // Turn off ready state server-side
            isReady.Value = false;
            if (QuizManager.Instance != null && Unity.Netcode.NetworkManager.Singleton != null)
            {
                QuizManager.Instance.PlayerReadyChanged(Unity.Netcode.NetworkManager.Singleton.LocalClientId, false);
            }

            var stationCol = GetComponent<BoxCollider2D>();
            if (stationCol == null) return;
            var center = stationCol.bounds.center;
            var ext = stationCol.bounds.extents;

            // Find player and compute ejection position just outside the station bounds
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                var t = playerGO.transform;
                var root = t.parent != null ? t.parent : t;
                var pos = root.position;
                Vector2 dir = (Vector2)(pos - center);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up; // default direction if centered
                dir.Normalize();
                float margin = 0.5f;
                float pushDist = Mathf.Max(ext.x, ext.y) + margin;
                Vector3 newPos = center + (Vector3)(dir * pushDist);
                root.position = newPos;
            }

            // Update internal flag
            isPlayerInside = false;
            Debug.Log("ReadyStation: Ejection complete");
        }

        // Allow any client (not just the owner of this in-scene NetworkObject) to toggle readiness
        [ServerRpc(RequireOwnership = false)]
        private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
        {
            Debug.Log($"ReadyStation: ToggleReadyServerRpc called - isReady: {isReady.Value}");
            isReady.Value = !isReady.Value;
            QuizManager.Instance.PlayerReadyChanged(rpcParams.Receive.SenderClientId, isReady.Value);
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
    }
}