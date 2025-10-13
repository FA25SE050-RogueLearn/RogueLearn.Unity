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