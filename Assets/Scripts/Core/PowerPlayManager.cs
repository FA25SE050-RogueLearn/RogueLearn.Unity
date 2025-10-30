using UnityEngine;
using Unity.Netcode;
using BossFight2D.Systems;
using BossFight2D.Quiz;

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

                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { _powerPlayPlayerId }
                    }
                };
                MovePlayerToReadyStationClientRpc(clientRpcParams);

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

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { _powerPlayPlayerId }
                }
            };

            MovePlayerToReadyStationClientRpc(clientRpcParams);
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
        private void MovePlayerToReadyStationClientRpc(ClientRpcParams clientRpcParams = default)
        {
         
            if (readyStationTransform != null)
            {
                NetworkObject playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (playerObject != null)
                {
                    playerObject.transform.position = readyStationTransform.position;
                }
            }
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