using UnityEngine;
using Unity.Netcode;
using BossFight2D.Core;
using BossFight2D.Systems;

namespace BossFight2D.Network
{
    public class NetworkGameState : NetworkBehaviour
    {
        public static NetworkGameState Instance { get; private set; }
        private NetworkVariable<int> state = new NetworkVariable<int>((int)GameState.Init, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (Instance == null) Instance = this;
            try { DontDestroyOnLoad(gameObject); } catch {}
            state.OnValueChanged += OnStateChanged;
            OnStateChanged((int)GameState.Init, state.Value);
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }

        private void OnStateChanged(int previous, int current)
        {
            var s = (GameState)current;
            if (s == GameState.Win) EventBus.RaiseGameWon();
            else if (s == GameState.Lose) EventBus.RaiseGameLost();
        }

        public static void EnsureSpawned()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            if (Instance != null && Instance.IsSpawned) return;
            var go = new GameObject("NetworkGameState");
            var ngs = go.AddComponent<NetworkGameState>();
            var no = go.AddComponent<NetworkObject>();
            no.Spawn(true);
        }

        public static void ServerSetWin()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            EnsureSpawned();
            if (Instance != null && Instance.IsSpawned)
            {
                Instance.state.Value = (int)GameState.Win;
            }
        }

        public static void ServerSetLose()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            EnsureSpawned();
            if (Instance != null && Instance.IsSpawned)
            {
                Instance.state.Value = (int)GameState.Lose;
            }
        }
    }
}