using UnityEngine;
using Unity.Netcode;
using BossFight2D.Systems;
using BossFight2D.Quiz;
using System.Collections.Generic;

namespace BossFight2D.Core
{
    public class PowerPlayManager : NetworkBehaviour
    {
        [Header("Window")]
        public float windowDurationDefault = 10f;

        [Header("State")]
        public NetworkVariable<bool> IsPowerPlayActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private float _windowEndTime;
        private ulong _powerPlayPlayerId;
        
        [Header("Player Position Snapshot")]
        [Tooltip("Duration for smoothly returning players to their original positions after Power Play ends.")]
        [SerializeField] private float smoothReturnDuration = 0.75f;
        public float SmoothMoveDuration => smoothReturnDuration;
        private Dictionary<ulong, Vector3> _positionsAtStart = new Dictionary<ulong, Vector3>();

        [Header("Dependencies")]
        public Transform readyStationTransform;

        public static PowerPlayManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        void Update()
        {
            if (!IsServer) return;

            if (IsPowerPlayActive.Value && Time.time >= _windowEndTime)
            {
                IsPowerPlayActive.Value = false;
                Debug.Log("Power Play ended.");
                // Notify clients to update UI overlays and state
                NotifyPowerPlayEndedClientRpc();

                // Smoothly return all players to their original positions captured at the start of Power Play
                foreach (var kvp in _positionsAtStart)
                {
                    var clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { kvp.Key } }
                    };
                    SmoothReturnPlayerClientRpc(kvp.Value, smoothReturnDuration, clientRpcParams);
                }

                QuizManager.Instance.EndPowerPlayAndStartNextQuestion();
            }
        }

        public void StartPowerPlay(ulong ownerClientId)
        {
            if (!IsServer) return;

            IsPowerPlayActive.Value = true;
            _windowEndTime = Time.time + windowDurationDefault;
            _powerPlayPlayerId = ownerClientId;
            Debug.Log($"Power Play started for {windowDurationDefault} seconds for player {ownerClientId}.");
            // Grant full attack charges to the activating player to maximize damage potential during Power Play
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerClientId, out var client))
            {
                var pc = client.PlayerObject != null ? client.PlayerObject.GetComponent<Player.PlayerCombat>() : null;
                if (pc != null)
                {
                    pc.FillChargesToMax();
                }
            }

            // Snapshot all players' positions at the start of Power Play to enable smooth return afterwards
            _positionsAtStart.Clear();
            foreach (var c in NetworkManager.Singleton.ConnectedClients)
            {
                var playerObj = c.Value.PlayerObject;
                if (playerObj != null)
                {
                    _positionsAtStart[c.Key] = playerObj.transform.position;
                }
            }
            // Notify clients to show Power Play overlay and adjust UI
            NotifyPowerPlayStartedClientRpc(windowDurationDefault);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPowerPlayServerRpc(ServerRpcParams rpcParams = default)
        {
            StartPowerPlay(rpcParams.Receive.SenderClientId);
        }

        public void EndPowerPlay()
        {
            if (!IsServer) return;

            IsPowerPlayActive.Value = false;
            Debug.Log("Power Play ended by boss hit.");
            // Notify clients to hide overlay and restore UI
            NotifyPowerPlayEndedClientRpc();

            // Smoothly return all players to their original positions captured at the start of Power Play
            foreach (var kvp in _positionsAtStart)
            {
                var clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { kvp.Key } }
                };
                SmoothReturnPlayerClientRpc(kvp.Value, smoothReturnDuration, clientRpcParams);
            }
            QuizManager.Instance.EndPowerPlayAndStartNextQuestion();
        }

        [ClientRpc]
        private void NotifyPowerPlayStartedClientRpc(float duration)
        {
            EventBus.RaisePowerPlayStarted(duration);
        }

        [ClientRpc]
        private void NotifyPowerPlayEndedClientRpc()
        {
            EventBus.RaisePowerPlayEnded();
        }



        [ClientRpc]
        private void SmoothReturnPlayerClientRpc(Vector3 targetPosition, float duration, ClientRpcParams clientRpcParams = default)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient != null ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
            if (localPlayerObj == null) return;
            var t = localPlayerObj.transform;
            Instance.StartCoroutine(LerpPlayerPosition(t, targetPosition, duration));
        }

        // Public wrapper to reuse the same smoothing visual for other systems (e.g., ReadyStation ejection)
        public void SmoothMoveLocalPlayer(Vector3 targetPosition, float duration)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient != null ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
            if (localPlayerObj == null) return;
            var t = localPlayerObj.transform;
            StartCoroutine(LerpPlayerPosition(t, targetPosition, duration));
        }

        private System.Collections.IEnumerator LerpPlayerPosition(Transform playerTransform, Vector3 target, float duration)
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

        public int ModifyDamageOnBossHit(int damage)
        {
            if (IsPowerPlayActive.Value)
            {
                // Apply bonus damage during Power Play
                return damage * 2; // Example: double damage
            }
            return damage;
        }
    }
}