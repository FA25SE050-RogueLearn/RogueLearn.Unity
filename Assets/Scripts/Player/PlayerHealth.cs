using UnityEngine;
using System;
using Unity.Netcode;
using BossFight2D.Network;
using BossFight2D.Systems;

namespace BossFight2D.Player {
  public class PlayerHealth : NetworkBehaviour, BossFight2D.Combat.IDamageable {
    public NetworkVariable<int> maxHearts = new NetworkVariable<int>(5, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> hearts = new NetworkVariable<int>(5, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public event Action OnDeath;
    public bool invincible = false;
    public float invincibleFlashInterval = 0.1f; // optional visual cue future use

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            hearts.Value = maxHearts.Value;
        }

        // Subscribe to health changes to update HUD
        hearts.OnValueChanged += (oldValue, newValue) =>
        {
            // Update HUD via EventBus
            EventBus.RaisePlayerHealthChanged(newValue, maxHearts.Value);
        };

        // Initialize HUD with starting health
        EventBus.RaisePlayerHealthChanged(hearts.Value, maxHearts.Value);
    }

    public void TakeDamage(int amount)
    {
        if (!IsServer) return;
        if (invincible) return;

        hearts.Value = Mathf.Max(0, hearts.Value - amount);

        if (hearts.Value == 0)
        {
            OnDeath?.Invoke(); 
            BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.GameManager>()?.LoseGame();
            NetworkGameState.ServerSetLose();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void HealServerRpc(int amount)
    {
        hearts.Value = Mathf.Min(maxHearts.Value, hearts.Value + amount);
    }

    public void Damage(int amount)
    {
        TakeDamage(amount);
    }

    public void Heal(int amount)
    {
        HealServerRpc(amount);
    }
  }
}